using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Services.InRaid;
using System.Reflection;

namespace Manimal.Icebreaker.Server;

// The crossing gate used to live only on the /client/quest/list route, which is one of
// several ways the client learns a quest exists. Accepting a quest asks the server which
// quests that unlocks (GetNewlyAccessibleQuestsWhenStartingQuest), and that path never
// went through the router - so a gated quest could be handed over the moment its
// predecessor was accepted, without the crossing it is supposed to require. Patching
// QuestHelper directly puts the gate on every path instead of the one.
//
// The raid-start prefix is the other half: marks are stamped for finished gating quests
// before the raid begins, so the crossing about to be made counts toward the next gate
// rather than being swallowed by a mark stamped after the fact.
[Injectable(TypePriority = OnLoadOrder.Preload + 92000)]
public class IcebreakerProgression(ProfileHelper profileHelper) : IOnLoad
{
    private static ProfileHelper? _profiles;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _profiles = profileHelper;

        var harmony = new Harmony("com.manimal.icebreaker.progression");

        foreach (var name in new[] { nameof(QuestHelper.GetClientQuests),
                                     nameof(QuestHelper.GetNewlyAccessibleQuestsWhenStartingQuest) })
            harmony.Patch(AccessTools.Method(typeof(QuestHelper), name),
                          postfix: new HarmonyMethod(typeof(IcebreakerProgression), nameof(Filter)));

        harmony.Patch(AccessTools.Method(typeof(LocationLifecycleService), nameof(LocationLifecycleService.StartLocalRaidAsync)),
                      prefix: new HarmonyMethod(typeof(IcebreakerProgression), nameof(BeforeRaid)));

        return Task.CompletedTask;
    }

    private static void Filter(MongoId sessionId, List<Quest> __result) =>
        IcebreakerFlyerGateRouter.FilterQuests(__result, _profiles!.GetPmcProfile(sessionId),
            sessionId.ToString(), IcebreakerRaidWatchRouter.Visits);

    private static void BeforeRaid(MongoId sessionId) =>
        IcebreakerFlyerGateRouter.MarkFinishedQuests(_profiles!.GetPmcProfile(sessionId),
            sessionId.ToString(), IcebreakerRaidWatchRouter.Visits);
}
