using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace Manimal.Icebreaker
{
    // DIAGNOSTIC ONLY (2026-09-24) - changes nothing about how the wedge room spawns.
    //
    // Player report + logs: the wedge room (bossWedge + blackDivIb, TriggerIds
    // wedges1..4) sometimes comes up one bot short. Counted in five field logs: 2 of 5
    // raids spawned 3 of the 4 wedges1 bots, and the missing one differed (a standalone
    // BD on 09-24, Wedge's own escort on 09-20). Nothing was logged either time - these
    // waves go through BSG's own BotBossSpawn path (unlike T4, IcebreakerFinalSquad),
    // and its async failures go to TasksExtensions.HandleExceptions, which writes to
    // Unity's player log, not BepInEx's LogOutput.
    //
    // Suspected cause: the same empty server profile hand-off T4 hit (leader and
    // escorts). These hooks record, for wedge waves only, (1) every TrySpawn result and
    // whether the boss profile it was handed was usable, (2) whether the
    // SpawnBossAndFollowers task faulted, and (3) 25s after the raise, expected vs
    // actual wedge-room bot count. One raid showing the shortfall is enough to confirm
    // or rule out the profile theory before any fix goes in.
    internal static class IcebreakerWedgeDiag
    {
        // 1 + BossEscortAmount summed over base.json's rows for each id.
        private static readonly Dictionary<string, int> Expected = new Dictionary<string, int>
        {
            ["wedges1"] = 4, // BD(0) + Wedge(1) + BD(0)
            ["wedges2"] = 5, // BD(0) + Wedge(1) + BD(1)
            ["wedges3"] = 7, // BD(1) + BD(1) + Wedge(2)
            ["wedges4"] = 7, // BD(1) + BD(1) + Wedge(2)
        };

        // TrySpawn can be retried every frame for a wave that keeps failing - log each
        // wave's failure once instead of flooding.
        private static readonly HashSet<BossLocationSpawn> _failLogged = new HashSet<BossLocationSpawn>();

        private static bool IsWedge(BossLocationSpawn wave) =>
            IceGate.On && wave != null && wave.TriggerId != null && wave.TriggerId.StartsWith("wedges");

        private static string Describe(BotCreationData data)
        {
            if (data == null) return "null";
            try
            {
                return data.Count != 1 ? $"count={data.Count}"
                    : data.Profiles[0] == null ? "profile missing"
                    : data.SpawnStopped ? "spawn stopped" : "ok";
            }
            catch (Exception e) { return "unreadable (" + e.GetType().Name + ")"; }
        }

        [HarmonyPatch]
        internal static class Patch_TrySpawn
        {
            private static MethodBase Target() => AccessTools.Method(typeof(BotBossSpawn), "TrySpawn");

            // resolved by name; if BSG renames it, skip this patch instead of taking PatchAll down
            [HarmonyPrepare]
            private static bool Prepare()
            {
                if (Target() != null) return true;
                Plugin.Log.LogWarning("[WedgeDiag] BotBossSpawn.TrySpawn not found - TrySpawn diagnostic skipped");
                return false;
            }

            [HarmonyTargetMethod]
            private static MethodBase TargetMethod() => Target();

            [HarmonyPostfix]
            private static void Postfix(BossLocationSpawn wave, int followersCount, BotCreationData creationData, bool __result)
            {
                try
                {
                    if (!IsWedge(wave)) return;
                    if (!__result && !_failLogged.Add(wave)) return;
                    string msg = $"[WedgeDiag] TrySpawn '{wave.TriggerId}' {wave.BossName} zone='{wave.BossZone}' followers={followersCount} " +
                        $"bossProfile={Describe(creationData)} -> {(__result ? "spawn started" : "FAILED (no spawn this attempt)")}";
                    if (__result) Plugin.Log.LogInfo(msg); else Plugin.Log.LogWarning(msg);
                }
                catch (Exception e) { Plugin.Log.LogDebug("[WedgeDiag] TrySpawn log failed: " + e.Message); }
            }
        }

        [HarmonyPatch(typeof(BotBossSpawn), nameof(BotBossSpawn.SpawnBossAndFollowers))]
        internal static class Patch_SpawnBossAndFollowers
        {
            [HarmonyPostfix]
            private static void Postfix(BossLocationSpawn wave, BotCreationData creationData, int followersCount, Task __result)
            {
                try
                {
                    if (!IsWedge(wave) || __result == null) return;
                    string label = $"'{wave.TriggerId}' {wave.BossName}+{followersCount}";
                    __result.ContinueWith(t =>
                    {
                        // background thread: BepInEx's logger is thread-safe, nothing Unity here
                        if (t.IsFaulted)
                            Plugin.Log.LogWarning($"[WedgeDiag] SpawnBossAndFollowers {label} FAULTED: {t.Exception?.GetBaseException()}");
                        else if (t.IsCanceled)
                            Plugin.Log.LogWarning($"[WedgeDiag] SpawnBossAndFollowers {label} cancelled");
                        else
                            Plugin.Log.LogInfo($"[WedgeDiag] SpawnBossAndFollowers {label} finished");
                    });
                }
                catch (Exception e) { Plugin.Log.LogDebug("[WedgeDiag] SpawnBossAndFollowers log failed: " + e.Message); }
            }
        }

        // Started from IcebreakerCrew's trigger handler when a wedges id is raised.
        internal static IEnumerator Census(string eventId)
        {
            if (!Expected.TryGetValue(eventId, out int expected)) yield break;

            var before = new HashSet<string>();
            foreach (var b in IcebreakerCrew.LiveBots())
                if (b?.Profile != null) before.Add(b.Profile.Id);

            yield return new WaitForSeconds(25f);

            int got = 0, wedge = 0;
            var names = new List<string>();
            foreach (var b in IcebreakerCrew.LiveBots())
            {
                var role = b?.Profile?.Info?.Settings?.Role;
                if (role == null || before.Contains(b.Profile.Id)) continue;
                if ((int)role.Value != IcebreakerCrew.BdIb && (int)role.Value != IcebreakerCrew.BdWedge) continue;
                got++;
                if ((int)role.Value == IcebreakerCrew.BdWedge) wedge++;
                names.Add(b.name);
            }
            // counts bots alive at the 25s mark - one killed inside that window reads as missing
            string msg = $"[WedgeDiag] '{eventId}' census after 25s: expected {expected}, found {got} alive (wedge {wedge}) [{string.Join(", ", names)}]";
            if (got < expected) Plugin.Log.LogWarning(msg + " - SHORT, see TrySpawn/SpawnBossAndFollowers lines above");
            else Plugin.Log.LogInfo(msg);
        }
    }
}
