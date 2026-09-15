using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace Manimal.Icebreaker
{
    // The engine-room and stern squads are trigger waves: base.json carries them with
    // Time 9999 and TriggerName botEvent, so the ONLY thing that ever spawns them is a
    // player crossing their one authored AIPlaceInfo box. Both boxes sit a long way from
    // the squad they deploy - the hide box is at z=+59 and drops the squad at z=-21, the
    // stern box is at z=+2 and drops two fireteams at z=-67..-71 - which is the intended
    // "they are already in position before you get there" staging.
    //
    // It also means a route that misses the box leaves the whole area empty. Player report
    // (2026-09-08): the engine room, the helipad and the deck under it are sometimes
    // deserted, and the squad only turns up later, once you have pushed well past them.
    // The logs show why: the same hide box was crossed at t=43s in one raid and t=1032s in
    // the next. It is one box, at deck height y=20 inside the bow superstructure, and the
    // engine room is reachable without ever entering it.
    //
    // So back the box up with proximity. If a human gets close to a squad's own spawn
    // markers and its trigger still has not fired, raise the trigger here - through
    // GlobalEventDispatcher.AnyEvent, exactly the call the authored box makes, so BSG's
    // BossSpawnScenario delivers the wave down its normal path. Nothing changes when the
    // box works: the event is already raised by then and this never runs.
    internal static class IcebreakerWaveBackstop
    {
        // Far enough that the squad is placed before the area is in view, and far short of
        // the distance from any player start to these zones (104m to the engine hides,
        // 156m to the stern), so a raid can never open with the backstop already tripped.
        private const float ApproachRadius = 40f;

        // Hide and Sten get the approach radius: they are group-size families with a single
        // box each, their zones sit alone at the far end of the ship, and the box is authored
        // tens of metres short of the squad, so 40m is unambiguous.
        //
        // T3 and T4 are a different shape and needed a second, much tighter radius (added
        // 2026-09-15). They are one authored id each rather than a group-size family, and
        // their box sits in or at the room they fill instead of out on the approach. Backing
        // them up at ApproachRadius would raise them from the deck below - EARLIER than
        // authored - and rearrange the choreography. At 12m the player is already in the
        // space and can see it is empty, so the squad arrives late rather than never. That
        // is the trade the player asked for: on 09-15 the wedge-approach squad only turned
        // up once he was climbing to the third deck, and on the next raid the top deck squad
        // never came at all.
        private const float ArrivedRadius = 12f;

        // A sphere reaches through a deck. Decks here are ~3-4m apart, so a 12m sphere round
        // a marker on the top deck also covers the floor below it and would fire T4 while the
        // player is still on the stairs - the early raise the paragraph above is trying to
        // avoid. Band the tight guards to roughly one deck so "close" means close on the same
        // floor. The 40m approach guards deliberately reach across decks and stay unbanded.
        private const float SameDeck = 3.5f;

        private sealed class Guard
        {
            internal readonly string Family;   // GroupSizeEventLogic.TableFor keyword, or null
            internal readonly string SingleId; // authored event id when there is no family
            internal readonly string Label;
            internal readonly string[] Zones;
            internal readonly float Radius;
            internal readonly float MaxDeltaY; // float.MaxValue = no band, measure as a sphere
            internal bool Done;
            internal Guard(string family, string singleId, float radius, float maxDeltaY, string label, params string[] zones)
            {
                Family = family; SingleId = singleId; Radius = radius; MaxDeltaY = maxDeltaY;
                Label = label; Zones = zones;
            }
        }

        private static readonly Guard[] Guards =
        {
            new Guard("Hide", null, ApproachRadius, float.MaxValue, "engine room", "BotZoneEngineHide"),
            new Guard("Sten", null, ApproachRadius, float.MaxValue, "stern + helipad", "BotZoneSternTop", "BotZoneStern"),
            new Guard(null, "T3", ArrivedRadius, SameDeck, "wedge approach", "BotZoneOutside_t3"),
            new Guard(null, "T4", ArrivedRadius, SameDeck, "top deck", "BotZoneInside_t4"),
        };

        internal static void ResetForRaid()
        {
            foreach (var g in Guards) g.Done = false;
        }

        internal static IEnumerator Watch()
        {
            var wait = new WaitForSeconds(0.5f);
            var markers = new Dictionary<string, List<Vector3>>();
            var humans = new List<Player>();

            while (true)
            {
                yield return wait;
                if (!IceGate.On || !FikaBridge.BotsAuthority) continue;

                int remaining = 0;
                BotZone[] zones = null; // fetched at most once per tick, and only if needed

                foreach (var g in Guards)
                {
                    if (g.Done) continue;

                    (int, int, string)[] table = null;
                    if (g.Family != null)
                    {
                        table = GroupSizeEventLogic.TableFor(g.Family);
                        if (table == null) { g.Done = true; continue; }

                        // The box (or an earlier backstop pass) already raised one of this
                        // family's ids - the wave is on its way, nothing to back up.
                        if (table.Any(t => IcebreakerAIPlaces.Raised.Contains(t.Item3)))
                        {
                            g.Done = true;
                            continue;
                        }
                    }
                    else if (IcebreakerAIPlaces.Raised.Contains(g.SingleId))
                    {
                        g.Done = true;
                        continue;
                    }

                    remaining++;
                    var points = Markers(g, markers, ref zones);
                    if (points.Count == 0) continue;

                    humans.Clear();
                    FikaBridge.CollectHumans(humans);
                    if (humans.Count == 0) continue;

                    float nearest = float.MaxValue;
                    foreach (var point in points)
                        foreach (var h in humans)
                        {
                            var offset = h.Position - point;
                            if (Mathf.Abs(offset.y) > g.MaxDeltaY) continue; // wrong deck
                            float d = offset.sqrMagnitude;
                            if (d < nearest) nearest = d;
                        }
                    if (nearest > g.Radius * g.Radius) continue;

                    // Same group-size table the authored box uses, resolved now rather
                    // than at build time so a late joiner is counted.
                    string id;
                    int size = 0;
                    if (table != null)
                    {
                        size = GroupSizeEventLogic.GroupSize();
                        id = null;
                        foreach (var (min, max, name) in table)
                            if (size >= min && size <= max) { id = name; break; }
                        if (id == null) { g.Done = true; continue; }
                    }
                    else
                    {
                        id = g.SingleId;
                    }

                    g.Done = true;
                    Plugin.Log.LogWarning(
                        $"[WaveBackstop] {g.Label}: a player got within {Mathf.Sqrt(nearest):0}m of the spawn markers "
                        + $"and the authored trigger never fired - raising '{id}'"
                        + (table != null ? $" (group={size})" : string.Empty)
                        + " so the squad is in place");
                    Singleton<GlobalEventDispatcher>.Instance?.AnyEvent(id);
                }

                if (remaining == 0) yield break;
            }
        }

        // Read the markers off the live BotZone rather than hardcoding coordinates, so this
        // follows the bundle and base.json instead of drifting from them.
        private static List<Vector3> Markers(Guard g, Dictionary<string, List<Vector3>> cache, ref BotZone[] zones)
        {
            var all = new List<Vector3>();
            foreach (var name in g.Zones)
            {
                if (cache.TryGetValue(name, out var cached)) { all.AddRange(cached); continue; }

                // upstream 1.1.3 caches this; FindObjectsOfType<BotZone> is a full scene
                // scan and this runs from a 0.5s loop.
                if (zones == null) zones = IcebreakerCrew.AllBotZones();
                var zone = zones.FirstOrDefault(z => z != null && z.name == name);
                if (zone == null || zone.SpawnPoints == null) continue;

                var points = new List<Vector3>();
                foreach (var sp in zone.SpawnPoints)
                    if (sp != null) points.Add(sp.Position);

                // Only cache once the zone has actually produced markers. An empty list
                // early in the raid means the zone is not built yet, not that it has none,
                // and caching that would disarm the backstop for the rest of the raid.
                if (points.Count == 0) continue;
                cache[name] = points;
                all.AddRange(points);
            }
            return all;
        }
    }
}
