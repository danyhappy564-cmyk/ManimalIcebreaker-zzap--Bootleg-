using SPTarkov.Server.Core.Helpers.InRaid;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services.Items;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Services.Server;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Reflection.Patching;

namespace Manimal.Icebreaker.Server;

// which map the ACTIVE raid runs on, latched by the loot firewall at StartLocalRaid.
// bot generation arrives on later requests with no location in its payload — this is
// how per-map bot tweaks (the knight's SZ-1 charge) know they're on the ship. single
// active raid per server holds for local SPT and fika alike.
internal static class IcebreakerRaidContext
{
    internal static bool OnIcebreaker;
}

// LOOT ISOLATION — on the icebreaker, loot generation runs with every third-party
// harmony patch on LocationLootGenerator SUSPENDED, then restored. mods keep working
// on every other map; on ours, authored loot generates exactly as intended.
//
// why not reverse-patch pristine copies (the first attempt): pristine is one level
// deep. LotsofLoot hooks PRIVATE CreateStaticLootItem, which the pristine copy of
// GenerateStaticContainers still CALLS — the call lands in the detour and their
// per-map dictionary throws from beneath us (fresh-install repro, 2026-08-03; the
// original fatal was the same mod's GenerateDynamicLoot prefix hanging
// /client/match/local/start with no response). suspend/restore has no depth limit:
// every method of the class is swept, so any hook by any mod at any level is out of
// the picture for exactly one call.
//
// Wrap the raid lifecycle's call to GenerateLocationLoot. Suspend foreign patches
// before entering the generator, then restore their owner IDs and ordering.
//
// the try/catch stays as the last line: if something still throws (a mod hooking a
// DIFFERENT class entirely), the raid starts lootless with the culprit named — which
// still beats the fatal-with-no-response infinite loading screen.
[Injectable(TypePriority = SPTarkov.Server.Core.DI.OnLoadOrder.Preload + 6)]
public class IcebreakerLootFirewall(
    ISptLogger<LocationLootGenerator> logger,
    LocationTable locationTable) : SPTarkov.Server.Core.DI.IOnLoad
{
    private static IcebreakerLootFirewall _instance = null!;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _instance = this;
        new LootGenerationPatch().Enable();
        new GoonRotationPatch().Enable();
        return Task.CompletedTask;
    }

    private sealed class LootGenerationPatch() : AbstractPatch(OwnPrefix + ".lootfirewall")
    {
        protected override MethodBase GetTargetMethod()
            => AccessTools.Method(typeof(SPTarkov.Server.Core.Services.InRaid.LocationLifecycleService), "GenerateLocationAndLoot");

        [PatchTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            => WrapLootGeneration(instructions);
    }

    // the goon rotation runs at post-db-load and again every RotationIntervalHours from the
    // server's update loop, and a local raid CLONES the location before loot generates, so
    // a restore that only runs at loot time fixes the database one raid too late. put it
    // right behind the zeroing instead.
    private sealed class GoonRotationPatch() : AbstractPatch(OwnPrefix + ".goonguard")
    {
        protected override MethodBase GetTargetMethod()
            => AccessTools.Method(typeof(SPTarkov.Server.Core.Services.InRaid.GoonLocationSpawnService), "AdjustGoonMapSpawns");

        [PatchPostfix]
        private static void Postfix() => _instance?.RestoreKnightChance("after SPT's goon rotation");
    }

    // Wrap the caller's generator call so foreign prefixes/postfixes on the generator
    // are suspended before Harmony enters it. A prefix on the generator itself would
    // be too late: its outer invocation would still run foreign postfixes.
    private static IEnumerable<CodeInstruction> WrapLootGeneration(IEnumerable<CodeInstruction> instructions)
    {
        var target = AccessTools.Method(typeof(LocationLootGenerator), nameof(LocationLootGenerator.GenerateLocationLoot));
        var wrapper = AccessTools.Method(typeof(IcebreakerLootFirewall), nameof(GenerateIsolated));
        int replaced = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(target))
            {
                instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                instruction.operand = wrapper;
                replaced++;
            }
            yield return instruction;
        }
        if (replaced != 1) throw new InvalidOperationException("Icebreaker expected one location loot generation call, found " + replaced);
    }

    private static List<SpawnpointTemplate> GenerateIsolated(LocationLootGenerator generator, string locationId)
        => _instance.GenerateLocationLoot(locationId, () => generator.GenerateLocationLoot(locationId));
    private const string OwnPrefix = BuildInfo.ModGuid;
    private readonly ISptLogger<LocationLootGenerator> _log = logger;
    private static readonly object Gate = new();

    private sealed record Suspended(MethodBase Target, HarmonyLib.Patch Patch, HarmonyPatchType Kind);

    private static bool Ours(string id) => IcebreakerLocation.Matches(id);

    // GOON-SYSTEM GUARD (2026-08-11, clone-order correction 2026-09-14). SPT relocates the
    // goons by zeroing BossChance on EVERY bossKnight row on EVERY map (post-db-load, then
    // every RotationIntervalHours from the update loop), restoring it only on one map from
    // its own location pool, which ours is never in. our T1 knight row is a bossKnight row,
    // so it gets zeroed and the knight never arrives. StartLocalRaid clones the location
    // BEFORE loot generation, so the raid-start restore below only helps the NEXT raid;
    // GoonRotationPatch restores immediately behind each zeroing, and the raid-start call
    // stays as the backstop plus the Info line that proves what the client was served.
    private void RestoreKnightChance(string when)
    {
        try
        {
            var spawns = locationTable.GetLocation(IcebreakerLocation.Key)?.Base?.BossLocationSpawn;
            if (spawns is null) return;
            foreach (var row in spawns)
                if (row.BossName == "bossKnight" && row.BossChance != 100)
                {
                    _log.Info($"[Icebreaker] knight T1 spawn chance was {row.BossChance}, restored to 100 {when}");
                    row.BossChance = 100;
                }
        }
        catch (Exception e) { _log.Warning($"[Icebreaker] knight chance restore failed: {e.Message}"); }
    }

    // what the client's clone of this raid actually carries — the clone is taken before
    // this runs, so a zero here means the knight is already gone for this raid
    private void LogKnightChanceForRaid()
    {
        try
        {
            var spawns = locationTable.GetLocation(IcebreakerLocation.Key)?.Base?.BossLocationSpawn;
            if (spawns is null) return;
            foreach (var row in spawns)
                if (row.BossName == "bossKnight")
                    _log.Info($"[Icebreaker] knight T1 row at raid start: chance {row.BossChance} zone {row.BossZone} trigger {row.TriggerId}");
        }
        catch (Exception e) { _log.Warning($"[Icebreaker] knight row read failed: {e.Message}"); }
    }

    private List<SpawnpointTemplate> GenerateLocationLoot(string locationId, Func<List<SpawnpointTemplate>> generate)
    {
        // raid-context latch: loot generates at StartLocalRaid, bots on later requests —
        // this is the only place the server tells us which map the active raid is on
        IcebreakerRaidContext.OnIcebreaker = Ours(locationId);
        if (Ours(locationId)) LogKnightChanceForRaid();
        RestoreKnightChance("at raid start");
        if (!Ours(locationId)) return generate();

        lock (Gate) // raid starts are rare; simplest way to keep suspend/restore atomic
        {
            // PBS MASQUERADE (policy exception, user-approved 2026-08-03): Progressive
            // Bot System keys hardcoded per-map switches off its static
            // RaidInformation.RaidLocation — 'Suburbs' throws for EVERY bot the server
            // generates (empty map). its router hook writes that static BEFORE
            // StartLocalRaid, we run INSIDE it, and bot generation reads it after — so
            // overwriting here makes PBS run its LABS profile on the icebreaker:
            // LongRange 10 / ShortRange 90 — tight-interior CQB, and labs tier tables
            // skew high-end gear, which fits a ship crewed by rogues and Black Division.
            // reflection + soft: no reference, no-op when PBS is absent or drifts.
            IcebreakerPbsMasquerade.Apply(_log);

            List<Suspended> suspended = null;
            try
            {
                suspended = SuspendForeign();
                if (suspended.Count > 0)
                    _log.Info($"[Icebreaker] loot isolation: {suspended.Count} third-party patch(es) on "
                        + $"LocationLootGenerator suspended for this raid's generation "
                        + $"({string.Join(", ", OwnersOf(suspended))}) — restored right after");
                return generate();
            }
            catch (Exception e)
            {
                _log.Error("[Icebreaker] loot generation failed for the icebreaker — "
                    + "starting the raid WITHOUT generated loot rather than hanging the client. "
                    + $"stack component: {Culprit(e)} (may be processing invalid map data). Exception: {e}");
                return new List<SpawnpointTemplate>();
            }
            finally
            {
                if (suspended != null) Restore(suspended);
            }
        }
    }

    private static List<Suspended> SuspendForeign()
    {
        var outp = new List<Suspended>();
        var h = new Harmony(OwnPrefix + ".lootisolation");
        foreach (var m in typeof(LocationLootGenerator).GetMethods(
                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            var info = Harmony.GetPatchInfo(m);
            if (info == null) continue;
            Collect(outp, m, info.Prefixes, HarmonyPatchType.Prefix);
            Collect(outp, m, info.Postfixes, HarmonyPatchType.Postfix);
            Collect(outp, m, info.Transpilers, HarmonyPatchType.Transpiler);
            Collect(outp, m, info.Finalizers, HarmonyPatchType.Finalizer);
        }
        foreach (var s in outp)
            h.Unpatch(s.Target, s.Patch.PatchMethod);
        return outp;
    }

    private static void Collect(List<Suspended> outp, MethodBase m, IEnumerable<HarmonyLib.Patch> patches, HarmonyPatchType kind)
    {
        foreach (var p in patches ?? Array.Empty<HarmonyLib.Patch>())
            if (!p.owner.StartsWith(OwnPrefix, StringComparison.OrdinalIgnoreCase))
                outp.Add(new Suspended(m, p, kind));
    }

    private static void Restore(List<Suspended> suspended)
    {
        foreach (var s in suspended)
        {
            try
            {
                // re-applied under the ORIGINAL owner id with the original ordering
                // metadata, so a later unpatch by that mod still finds its own patch
                var h = new Harmony(s.Patch.owner);
                var hm = new HarmonyMethod(s.Patch.PatchMethod)
                {
                    priority = s.Patch.priority,
                    before = s.Patch.before?.Length > 0 ? s.Patch.before : null,
                    after = s.Patch.after?.Length > 0 ? s.Patch.after : null,
                };
                h.Patch(s.Target,
                    prefix: s.Kind == HarmonyPatchType.Prefix ? hm : null,
                    postfix: s.Kind == HarmonyPatchType.Postfix ? hm : null,
                    transpiler: s.Kind == HarmonyPatchType.Transpiler ? hm : null,
                    finalizer: s.Kind == HarmonyPatchType.Finalizer ? hm : null,
                    ilmanipulator: null);
            }
            catch
            {
                // a failed restore only means that mod's loot feature stays off for the
                // rest of THIS server session — still strictly better than the crash
            }
        }
    }

    private static IEnumerable<string> OwnersOf(List<Suspended> s)
    {
        var seen = new HashSet<string>();
        foreach (var x in s) if (seen.Add(x.Patch.owner)) yield return x.Patch.owner;
    }

    private static string Culprit(Exception e)
    {
        foreach (var line in (e.StackTrace ?? "").Split('\n'))
        {
            var m = Regex.Match(line, @"at (?!SPTarkov\.|System\.|Microsoft\.|DMD|SyncProxy)([A-Za-z_][\w]*)[\w.]*\.");
            if (m.Success) return m.Groups[1].Value;
        }
        return "unknown (see stack in this log)";
    }
}
