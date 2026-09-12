using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.InRaid;

namespace Manimal.Icebreaker.Server;

// THE GOONS DO NOT SPAWN ON THIS MAP WITHOUT THIS (field log 2026-09-07: the T1
// trigger fires — "[Waves] botEvent 'T1' raised t=1044s" — and nothing arrives, while
// every blackDivIb/bossWedge wave that same raid delivers normally).
//
// SPT 4.1 added GoonLocationSpawnService, which rotates the goons between four vanilla
// maps every GoonSpawnSystem.RotationIntervalHours. Its reset pass is unconditional:
//
//     foreach (var (locationId, location) in allLocations)
//         if (!locationBlacklist.Contains(locationId) && location?.Base?.BossLocationSpawn is not null)
//             foreach (var goonSpawn in location.Base.BossLocationSpawn.Where(x => x.BossName == "bossKnight"))
//                 goonSpawn.BossChance = 0;
//
// — every bossKnight row on every map except "hideout"/"develop" goes to 0%, and only
// the map it then rolls out of GoonSpawnSystem.LocationPool (bigmap/woods/shoreline/
// lighthouse) gets its chance back. The icebreaker's T1 wave IS a bossKnight row (the
// knight plus two exUsec escorts at BotZoneMash_t1), Suburbs is not in that pool and is
// not blacklisted, so the wave is zeroed and never restored. BossSpawnScenario then has
// a 0%-chance wave to act on when the trigger box raises T1, and the deck stays empty.
//
// The service is IOnUpdate, so it re-runs on a timer: putting the chance back once at
// load would survive only until the next rotation window. Postfix it instead and restore
// our row every time it runs. Values come from a snapshot taken at load — whatever
// base.json authored — so editing the wave's chance there still works.
[Injectable(TypePriority = OnLoadOrder.Preload + 91000)]
public class IcebreakerGoonGuard(
    LocationTable locationTable,
    ISptLogger<IcebreakerGoonGuard> logger) : IOnLoad
{
    private const string GoonBoss = "bossKnight";

    private static ISptLogger<IcebreakerGoonGuard> _log = null!;
    private static LocationTable _locations = null!;

    // authored chance per bossKnight row, by position in BossLocationSpawn. captured
    // before the rotation service has ever run, so it is base.json's value and not a
    // zero the service already wrote.
    private static Dictionary<int, double?>? _authored;
    private static bool _warnedEmpty;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _log = logger;
        _locations = locationTable;

        // load order: SPT runs preload IOnLoad components in ASCENDING TypePriority
        // (ProgramExtensions sorts them, then takes everything below GameCallbacks), so
        // Preload+91000 lands just after IcebreakerMod's Preload+90000 — the location is
        // already registered and the rows below are ours. GoonLocationSpawnService is
        // IOnUpdate and first runs on the 5s update loop after startup, so this snapshot
        // always precedes the first zeroing pass.
        Snapshot();

        try
        {
            var target = AccessTools.Method(typeof(GoonLocationSpawnService),
                nameof(GoonLocationSpawnService.AdjustGoonMapSpawns));
            if (target is null)
            {
                logger.Warning("[Icebreaker] GoonLocationSpawnService.AdjustGoonMapSpawns not found — "
                    + "goon-rotation guard NOT installed; the T1 knight wave may be zeroed by SPT");
                return Task.CompletedTask;
            }

            new Harmony("com.manimal.icebreaker.goonguard").Patch(target,
                postfix: new HarmonyMethod(typeof(IcebreakerGoonGuard), nameof(RestoreOurGoons)));
        }
        catch (Exception e)
        {
            logger.Warning($"[Icebreaker] goon-rotation guard failed to install ({e.Message}) — "
                + "the T1 knight wave may be zeroed by SPT");
        }

        return Task.CompletedTask;
    }

    // upstream 1.1.0 stopped installing the map into the dormant Suburbs slot and
    // registers an independent location instead, so reading Suburbs here would guard
    // the vanilla stub's (empty) spawn list and leave our T1 wave to be zeroed after
    // all. Resolve through the same key the registration uses.
    private static List<BossLocationSpawn>? OurWaves()
        => _locations?.GetLocation(IcebreakerLocation.Key)?.Base?.BossLocationSpawn;

    private static void Snapshot()
    {
        var waves = OurWaves();
        if (waves is null) return;

        var map = new Dictionary<int, double?>();
        for (var i = 0; i < waves.Count; i++)
            if (string.Equals(waves[i].BossName, GoonBoss, StringComparison.Ordinal))
                map[i] = waves[i].BossChance;

        _authored = map;

        if (map.Count == 0 && !_warnedEmpty)
        {
            _warnedEmpty = true;
            _log.Warning("[Icebreaker] no bossKnight wave in the icebreaker's spawn table — "
                + "goon-rotation guard has nothing to protect (T1 will stay empty by design)");
        }
    }

    private static void RestoreOurGoons()
    {
        try
        {
            var waves = OurWaves();
            if (waves is null || _authored is null || _authored.Count == 0) return;

            var restored = 0;
            foreach (var (index, chance) in _authored)
            {
                if (index >= waves.Count) continue;
                var wave = waves[index];
                if (!string.Equals(wave.BossName, GoonBoss, StringComparison.Ordinal)) continue;
                if (Nullable.Equals(wave.BossChance, chance)) continue;
                wave.BossChance = chance;
                restored++;
            }

            if (restored > 0)
                _log.Debug($"[Icebreaker] goon rotation zeroed {restored} bossKnight wave(s) on this map — restored");
        }
        catch (Exception e)
        {
            _log.Warning($"[Icebreaker] goon-rotation guard threw: {e.Message}");
        }
    }
}
