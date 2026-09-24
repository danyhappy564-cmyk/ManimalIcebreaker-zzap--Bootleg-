using System;
using System.Collections.Generic;
using ZLinq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace Manimal.Icebreaker
{
    // T4 is five identical BD soldiers, not a distinct boss plus follower roles.
    // BSG's boss path activates the leader, THEN generates/queues the four escorts
    // with forceSpawn=false and four copies of the leader's point. Prepare this
    // particular squad together and submit five single-bot activations instead.
    [HarmonyPatch(typeof(BotBossSpawn), nameof(BotBossSpawn.SpawnBossAndFollowers))]
    internal static class IcebreakerFinalSquad
    {
        [HarmonyPrefix]
        private static bool Prefix(BotBossSpawn __instance, BotCreationData creationData,
            BossLocationSpawn wave, BotSpawnParams spawnParams, int followersCount,
            BotZone botZone, ref Task __result)
        {
            if (!IceGate.On || !FikaBridge.BotsAuthority || wave == null ||
                wave.TriggerId != "T4" || !wave.ForceSpawn || followersCount != 4 ||
                (int)wave.BossType != IcebreakerCrew.BdIb || wave.EscortType != wave.BossType ||
                botZone == null || botZone.name != "BotZoneInside_t4") return true;
            __result = Spawn(__instance, creationData, wave, spawnParams, botZone);
            return false;
        }

        private static async Task Spawn(BotBossSpawn boss, BotCreationData leader,
            BossLocationSpawn wave, BotSpawnParams spawnParams, BotZone zone)
        {
            var token = boss._spawner.GetCancelToken();
            var ready = new List<BotCreationData>();
            bool submitted = false;
            try
            {
                // 2026-09-21: BSG occasionally hands this hook a leader BotCreationData
                // whose profile never got attached (seen once in a field log: "T4 leader
                // must contain one valid profile", squad deferred and never retried since
                // - see the comment on the deferred-retry guard below). Rather than give up
                // immediately, request one fresh leader profile ourselves with the same
                // BotCreationData.Create call the four escorts already use below, just with
                // the boss's own type/difficulty (wave.BossType/BossDif, confirmed via
                // Assembly-CSharp.dll field dump - same naming as EscortType/EscortDif).
                // One retry only; if that also comes back empty, this is a real failure.
                if (leader == null || leader.Count != 1 || leader.Profiles[0] == null)
                {
                    Plugin.Log.LogWarning("[T4Squad] leader profile missing on first hand-off - requesting a fresh one before giving up");
                    var original = leader;
                    var fresh = await BotCreationData.Create(new GetProfileDataParams(EPlayerSide.Savage,
                        wave.BossType, wave.BossDif, wave.Time, spawnParams, false),
                        boss._botCreator, 1, boss._spawner);
                    if (fresh != null && fresh.Count == 1 && fresh.Profiles[0] != null && !fresh.SpawnStopped)
                    {
                        // 2026-09-24: the replaced hand-off is never used again - release its slot.
                        leader = fresh;
                        Release(original);
                    }
                    else
                    {
                        // Retry failed too: keep BSG's own leader so the deferred-retry path
                        // below behaves exactly like upstream, and release the unusable retry.
                        Release(fresh);
                    }
                    token.ThrowIfCancellationRequested();
                }
                if (leader == null || leader.Count != 1 || leader.Profiles[0] == null)
                    throw new InvalidOperationException("T4 leader must contain one valid profile");
                ready.Add(leader);
                spawnParams.ShallBeGroup = new ShallBeGroupParams(true, true, 5);
                // SPT's profile-client path may ignore Create(count), so make four
                // explicit requests and await all of them BEFORE activating the leader.
                var requests = new Task<BotCreationData>[4];
                for (int i = 0; i < requests.Length; i++)
                    requests[i] = BotCreationData.Create(new GetProfileDataParams(EPlayerSide.Savage,
                        wave.EscortType, wave.EscortDif, wave.Time, spawnParams, false),
                        boss._botCreator, 1, boss._spawner);
                try { await Task.WhenAll(requests); }
                finally
                {
                    foreach (var request in requests)
                        if (request.Status == TaskStatus.RanToCompletion && request.Result != null &&
                            request.Result.Count == 1 && request.Result.Profiles[0] != null && !request.Result.SpawnStopped)
                            ready.Add(request.Result);
                        else if (request.Status == TaskStatus.RanToCompletion)
                            Release(request.Result); // unusable escort: release its slot (upstream did this via the finally below)
                }
                token.ThrowIfCancellationRequested();

                // 2026-09-21: the same server-side profile hand-off flakiness that can empty
                // out the leader (above) can also empty out an escort slot - a field log
                // showed 3 of 5 total T4 defers were exactly this ("profile preparation did
                // not produce five ready bots"), with BSG's own DelayBossSpawn loop eventually
                // succeeding ~26s later, by which point the player had walked past the room
                // and the squad spawned behind them instead of ahead. One bounded retry for
                // just the missing slots costs nothing on the happy path (missing == 0) and
                // should cut down how often the slower outer retry loop is needed.
                int missing = 5 - ready.Count;
                if (missing > 0)
                {
                    Plugin.Log.LogWarning($"[T4Squad] {missing} escort profile(s) missing on first pass - requesting replacement(s) before giving up");
                    var retryRequests = new Task<BotCreationData>[missing];
                    for (int i = 0; i < retryRequests.Length; i++)
                        retryRequests[i] = BotCreationData.Create(new GetProfileDataParams(EPlayerSide.Savage,
                            wave.EscortType, wave.EscortDif, wave.Time, spawnParams, false),
                            boss._botCreator, 1, boss._spawner);
                    try { await Task.WhenAll(retryRequests); }
                    finally
                    {
                        foreach (var request in retryRequests)
                            if (request.Status == TaskStatus.RanToCompletion && request.Result != null &&
                                request.Result.Count == 1 && request.Result.Profiles[0] != null && !request.Result.SpawnStopped)
                                ready.Add(request.Result);
                            else if (request.Status == TaskStatus.RanToCompletion)
                                Release(request.Result);
                    }
                    token.ThrowIfCancellationRequested();
                }
                if (ready.Count != 5 || ready.AsValueEnumerable().Any(d => d.Count != 1 || d.Profiles[0] == null || d.SpawnStopped))
                    throw new InvalidOperationException("T4 profile preparation did not produce five ready bots");
                if (ready.AsValueEnumerable().Select(d => d.Profiles[0].Id).Distinct().Count() != 5)
                    throw new InvalidOperationException("T4 profile preparation returned duplicate IDs");

                var paths = ready.AsValueEnumerable().SelectMany(d => d.Profiles[0].GetAllPrefabPaths(false)).ToArray();
                await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(
                    ObjectsFactory.PoolsCategory.Raid, ObjectsFactory.AssemblyType.Local,
                    paths, Diz.Jobs.JobYieldPriority.Low, null, token);
                token.ThrowIfCancellationRequested();
                if (!IceGate.On || !FikaBridge.BotsAuthority || ready.AsValueEnumerable().Any(d => d.SpawnStopped)) return;

                // Resolve immediately before activation: the player can move upstairs
                // while the backend is preparing profiles. Never release a partial team.
                var points = PickPositions(zone, 5);
                if (points.Count != 5)
                    throw new InvalidOperationException($"T4 has only {points.Count}/5 safe, separated spawn positions");
                int activated = 0;
                float started = Time.time;
                submitted = true;
                for (int i = 0; i < ready.Count; i++)
                {
                    // This is the same low-level activation used by SpawnBossMain.
                    // No min/max or proximity-delay queue can split the prepared squad.
                    boss._spawner.SpawnBotsInZoneOnPositions(new List<ISpawnPoint> { points[i] },
                        zone, ready[i], bot =>
                        {
                            if (!spawnParams.ShallBeGroup.IsBossSetted)
                            {
                                spawnParams.ShallBeGroup.IsBossSetted = true;
                                bot.Boss.SetBoss(4);
                            }
                            activated++;
                            Plugin.Log.LogInfo($"[T4Squad] active {activated}/5 at {bot.Position}, +{Time.time - started:0.00}s");
                        });
                }
                Plugin.Log.LogInfo("[T4Squad] all five profiles ready; five separated activations submitted together");
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Plugin.Log.LogError("[T4Squad] " + (submitted ? "activation failed: " : "whole squad deferred: ") + e);
                if (!submitted && leader != null && !leader.SpawnStopped && !token.IsCancellationRequested && IceGate.On)
                    boss.DelayBossSpawn(wave, spawnParams, leader);
            }
            finally
            {
                if (!submitted)
                    foreach (var data in ready.AsValueEnumerable().Skip(1)) data.StopSpawn();
            }
        }

        // Frees the bot reservation of a BotCreationData we decided not to use.
        // Guarded because a half-empty hand-off is exactly what we are discarding.
        private static void Release(BotCreationData data)
        {
            if (data == null || data.SpawnStopped) return;
            try { data.StopSpawn(); }
            catch (Exception e) { Plugin.Log.LogWarning("[T4Squad] could not release unused profile: " + e.Message); }
        }

        // Out-of-sight positions are preferred but not required. Requiring them threw the
        // whole squad back into DelayBossSpawn whenever the player happened to be standing
        // where they could see the room ("T4 has only 1/5 safe, separated spawn positions"
        // in the 09-07 log), and the retry loop then delivered the ambush a good while
        // after the player had walked through it. A hidden spawn is better; a late squad is
        // worse than a visible one, so short of five we top up with the positions furthest
        // from anyone, and only refuse the ones close enough to appear in someone's face.
        private const float VisibleSpawnMinDistSqr = 64f; // 8m

        private static List<ISpawnPoint> PickPositions(BotZone zone, int count)
        {
            var result = new List<ISpawnPoint>();
            if (zone.SpawnPoints == null) return result;
            var seeds = zone.SpawnPoints.AsValueEnumerable().Where(p => p != null)
                .OrderByDescending(p => FikaBridge.NearestHumanSqr(p.Position)).ToArray();
            var humans = new List<Player>();
            FikaBridge.CollectHumans(humans);
            var living = IcebreakerCrew.LiveBots();
            int sightMask = LayerMask.GetMask("HighPolyCollider", "Terrain");
            var visible = new List<KeyValuePair<float, ISpawnPoint>>(); // fallback, by distance
            // The authored T4 zone has only TWO markers. Add nearby positions on
            // the same deck, reachable in a straight line on the baked NavMesh.
            for (int ring = 0; ring <= 3; ring++)
            foreach (var seed in seeds)
            for (int direction = 0; direction < (ring == 0 ? 1 : 12); direction++)
            {
                if (!NavMesh.SamplePosition(seed.Position, out var anchor, 0.75f, NavMesh.AllAreas)) continue;
                float angle = direction * Mathf.PI / 6f;
                var candidate = anchor.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * 1.8f);
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.75f, NavMesh.AllAreas)) continue;
                var position = hit.position;
                if (Mathf.Abs(position.y - seed.Position.y) > 0.75f ||
                    NavMesh.Raycast(anchor.position, position, out _, NavMesh.AllAreas)) continue;
                if (result.AsValueEnumerable().Any(p => (p.Position - position).sqrMagnitude < 2.25f)) continue;
                if (visible.AsValueEnumerable().Any(p => (p.Value.Position - position).sqrMagnitude < 2.25f)) continue;
                if (living.AsValueEnumerable().Any(b => b != null && !b.IsDead && (b.Position - position).sqrMagnitude < 2.25f)) continue;

                float nearestHuman = float.MaxValue;
                foreach (var h in humans)
                {
                    float d = (h.Position - position).sqrMagnitude;
                    if (d < nearestHuman) nearestHuman = d;
                }
                bool exposed = humans.AsValueEnumerable().Any(h => (h.Position - position).sqrMagnitude < 9f ||
                    !Physics.Linecast(h.Position + Vector3.up * 1.5f, position + Vector3.up * 1.5f,
                        sightMask, QueryTriggerInteraction.Ignore));

                var point = new SpawnPoint
                {
                    Id = seed.Id + "-t4-" + (result.Count + visible.Count), Name = "Icebreaker T4 squad",
                    Position = position, Rotation = seed.Rotation, CorePointId = seed.CorePointId,
                    BotZone = zone, Sides = seed.Sides, Categories = seed.Categories
                };
                if (!exposed)
                {
                    result.Add(point);
                    if (result.Count == count) return result;
                }
                else if (nearestHuman >= VisibleSpawnMinDistSqr)
                {
                    visible.Add(new KeyValuePair<float, ISpawnPoint>(nearestHuman, point));
                }
            }

            if (result.Count < count && visible.Count > 0)
            {
                int hidden = result.Count;
                foreach (var v in visible.AsValueEnumerable().OrderByDescending(v => v.Key))
                {
                    result.Add(v.Value);
                    if (result.Count == count) break;
                }
                Plugin.Log.LogWarning($"[T4Squad] only {hidden}/{count} spawn positions were out of sight; " +
                    $"topped up to {result.Count} with the furthest visible ones rather than deferring the squad");
            }
            return result;
        }
    }
}
