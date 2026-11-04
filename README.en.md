<p align="center">
  <a href="README.md"><img alt="한국어" src="https://img.shields.io/badge/%ED%95%9C%EA%B5%AD%EC%96%B4-%ED%95%9C%EA%B5%AD%EC%96%B4%EB%A1%9C%20%EB%B3%B4%EA%B8%B0-6e7681?style=for-the-badge"></a>
  <a href="README.en.md"><img alt="English (this page)" src="https://img.shields.io/badge/English-This%20page-1f6feb?style=for-the-badge"></a>
</p>

### ⚠️ IMPORTANT NOTICE / DISCLAIMER

- **Original Author:** danauraborealis
- **Original Repository:** ManimalIcebreaker
- **Original Link:** https://github.com/danauraborealis/ManimalIcebreaker
- **License:** MIT
- **This Port By:** R_F (danyhappy564-cmyk) — unofficial, AI-assisted port. Not affiliated with or endorsed by the original author.

1. **Reflection & Take-Downs:** I deeply reflect on the ECOT incident. As an AI-assisted "vibe coder," I will immediately delete files if the original authors ask.
2. **No Re-Distribution:** These ported builds are unverified, temporary fixes. Please do NOT re-upload or share them anywhere else.
3. **Do Not Pester Original Authors:** Never report bugs or pester original modders regarding issues from my unofficial ports.
4. **Full Credit & Respect:** I will always credit original creators on GitHub and prioritize their decisions above all else.
5. **Support Original Creators:** Instead of using my ports, please visit the original authors' Forge pages to leave kind words or tips.

## Before installing — install the original mod first

This repository's build is **a patch to install over the original Manimal's Icebreaker Backport**. Original files such as map bundles are not included.
**You must download and install the original mod first, then extract this repository's archive over it for it to work.**

- Original mod: https://sp-mod.com/mod/2866/manimals-icebreaker-backport

This build only **overwrites the original Manimal's Icebreaker Backport** - it does not contain the original files (map bundles etc.).
**Install the original mod first, then extract this build over it.** Original: https://sp-mod.com/mod/2866/manimals-icebreaker-backport

## Changelog

The full changelog (15 entries) is in the [`changelog/`](changelog/) folder, 10 per file (higher number = newer, latest: [`CHANGELOG-002.md`](changelog/CHANGELOG-002.md)). Per-release summaries: [Releases](../../releases).

---

# ManimalIcebreaker (fork)

> **Original author · Original repository**
> **danauraborealis** — https://github.com/danauraborealis/ManimalIcebreaker
>
> **License: MIT**
>
> This repository is a **fork** of the original above. The map, assets, and quests all belong to the original author;
> this fork only adds fixes for performance/crash issues found during actual use.
> No added features or balance changes.

Current target: **upstream 1.1.0 / SPT 4.1.5**

> **1.1.0 sync (09/12).** With the original's 1.1.0 release, the author **published the actual source**
> for the 1.0.1 server fixes we had reconstructed from the DLL. Our reconstruction was replaced
> with that source. See [1.1.0 sync](#110-동기화) below for the detailed comparison.

---

## How this fork differs from the original

The original 1.0.0 SPT 4.1 migration was **merged** (rather than overwritten). Upstream changes
included deobfuscation renames and new content (IcebreakerFinalSquad, IcebreakerSplash,
IcebreakerOpticWeather, tripwire rewrite, CustomSpawnpoints). The following fixes were
retained because they were absent upstream.

**Performance**

| Fix | Symptom |
|---|---|
| Spread `BuildDistanceCuller` across frames | Synchronous scan of 148000+ renderers across the scene in one frame at raid start |
| Remove `FindObjectsOfType` from bot polling | `HoldEngineSquad`/`C3KeycardSweep`/`PlaceChargeSweep`/`PlaceWedgeTag`/`UnstackPatrol` scanned the whole scene each cycle, causing ~55ms spikes |
| Remove per-node Dictionary allocations for lens flares | A single 17.9-second frame at raid start |
| Frame budget for recalculating LOD cull floors | Frame drops when going up on deck |
| World-level `AICorePoint` cache | 54ms rediscovery per bot spawn |

**Crashes / bugs**

| Fix | Symptom |
|---|---|
| `IcebreakerGoonGuard` (new, 09/07) | SPT 4.1's Goons rotation set the T1 wave to 0% |
| `IcebreakerWaveBackstop` (new, 09/08 · expanded 09/15) | Bypassing engine room/stern trigger boxes left those areas empty until much later. On 09/15, the same symptoms were confirmed for the wedge approach (T3) and top deck (T4), so both were added |
| T4 spawn position fallback (09/08) | Failure to find 5 out-of-sight positions deferred the whole squad, making the ambush arrive late |
| Duplicate spawn guard for `IcebreakerSnowGusts` | Up to 12 duplicate spawns per raid, using 90%+ of frame time |
| `BreathEffector` finalizer | Crash from 5500+ NRE spam entries |
| onIce debounce + off-ice settling guard | Icebreaker logic continued on other maps after leaving Icebreaker |
| `PatrolScanner` fallback for small rooms | Bots stuck in place in cramped areas |
| `OrbitBrainLayerCompat` | ORBIT exceptions caused by an unknown map also broke our AI layer creation |
| Remove 7 duplicate `BossLocationSpawn` entries from `base.json` | Each trigger spawned the same squad twice (29 extra bots per raid) |

**Fixes discarded because upstream 1.1.0 fixed them directly**

Duplicate `looseLoot.json` entries and all reconstructed 1.0.1 server fixes (2 quest gates,
Boreas Part 3 unlock, duplicate raid-end counting, visit ledger) were replaced with upstream's
actual source published in 1.1.0. → [1.1.0 sync](#110-동기화)

**Configuration differences**

- `EscapeTimeLimit`: 90 minutes (original: 50 minutes)
- `DeployToGame` defaults to `true` (original: `false`)

---

## Build / installation

```
dotnet build ManimalIcebreaker.sln
```

Paths come from `Directory.Build.props`. The default is `SPTPath = E:\SPT 4.1`;
override with `-p:SPTPath=...` or an environment variable of the same name.

Building automatically deploys to:

| Destination | Contents |
|---|---|
| `$(SPTPath)\BepInEx\plugins\ManimalIcebreaker\` | Client DLL |
| `$(SPTPath)\SPT_Runtime\user\mods\ManimalIcebreaker\` | Server DLL + **entire `db/`** + `bundles.json` |

**Note**: server deployment overwrites the entire `db/`. Any JSON edited directly in the installation
folder will be lost. Conversely, if a `base.json` change in the repository does not appear in-game,
first check whether the build completed deployment.

The 2 scene bundles (615MB scenes + preset) are not copied on every build due to their size.
Place them manually once, or use `package-release.ps1`.

To disable deployment: `-p:DeployToGame=false`.

---

## Fixes made on 09/08

### Engine room / helipad spawn timing

All Black Division waves in `base.json` use `Time: 9999` + `TriggerName: botEvent`,
so they spawn **only when a player crosses a trigger box**. Those boxes are also far
from the squads' deployment areas:

| Trigger | Box position | Squad spawn area | Distance |
|---|---|---|---|
| `hides*` (engine room) | z=+59, y=20 (bow superstructure) | `BotZoneEngineHide` z=-21 | **80m** |
| `stern*` (helipad/below it) | z=+2 (midship) | `BotZoneSternTop` z=-67, `BotZoneStern` z=-71 | **69~73m** |

The intent is to have squads in position ahead of you, but **a route that avoids the box leaves
the entire area empty**. Logs show the same hides box crossed at t=43s in one raid,
but **t=1032s (17 minutes)** in the next. There is only one box, and the engine room is accessible
without crossing it, causing squads to spawn late after the player has already progressed far ahead.

`IcebreakerWaveBackstop` backs up those boxes with a distance check. If a human player comes
**within 40m** of a squad's spawn markers and its trigger has not fired, it raises the same
`GlobalEventDispatcher.AnyEvent` the box would, allowing the normal BSG wave pipeline to deploy it.
If the box was crossed normally, the event has already fired and the backstop does nothing.

- Why 40m: the nearest relevant zone is **104m (engine room) / 156m (stern)** from any player
  spawn point, so this cannot fire at raid start
- Wedge boxes (`wedges*`) sit inside the rooms they populate (4~16m), and T1 is the first gate
  on the required route, so those three trigger in the intended order and still have no backstop

#### 09/15 second pass — arm only after the preceding tier fires (distance alone was insufficient)

The first 09/15 build was tested in-game, and **T3 triggered too early**.

```
[WaveBackstop] wedge approach: a player got within 11m of the spawn markers
               and the authored trigger never fired - raising 'T3'
[Waves] botEvent 'T3' raised t=89s      ← 백스톱이 올림
[Waves] botEvent 'T2' raised t=201s
[Waves] botEvent 'T3' raised t=328s     ← 원작 박스가 뒤늦게 올림
```

t=89s is **4 minutes earlier than intended, and even before T2**. A 12m radius plus deck band
was insufficient because the route toward the wedge approach passes within 11m of its zone markers,
even before the player intends to enter that area.

Tier guards therefore **do not arm until the preceding tier actually fires**.

| Guard | Prerequisite |
|---|---|
| `hides*` / `stern*` | None (not tiers) |
| `T3` | `T2` must have fired |
| `T4` | `T3` must have fired |

The intended order is the same every raid: T1 → T2 → T3 → T4 (09/15: 53 / 201 / 328 / 422 seconds;
09/08: 48 / 239 / 276 / 518 seconds). Prerequisites return the backstop to its intended role:
**insurance when the player bypasses a box**, rather than a second trigger that activates
boxes the player has not reached yet.

#### 09/15 — T3/T4 added (approach 40m / arrival 12m)

The 09/15 report showed **the wedge approach (T3) squad spawning only after the player went up
to the third level, and the top deck (T4) squad never arriving in the next raid**. Same symptoms as engine room/stern.

T3/T4 are **structured differently**, though. Each has a single event ID rather than a group-size
table, and the boxes sit **inside or around the rooms they populate**, rather than on an approach route.
Reusing 40m would trigger them early from the deck below and recreate the premature spawning
problem above. A second radius was therefore added:

| Guard | Radius | Height band | Reason |
|---|---|---|---|
| `hides*` / `stern*` | 40m | None | Intentionally deploy in advance across decks |
| `T3` / `T4` | **12m** | **±3.5m** | Only after the player has entered the space and found it empty |

Why a height band: a sphere passes through decks. Decks on this ship are 3~4m apart,
so a 12m sphere around top-deck markers **also covers the floor below**, triggering T4
while climbing the stairs. Limiting the band to one deck makes "nearby" mean on the same floor.

Late arrivals are still late. The tradeoff is that **late is better than never**; every backstop
intervention logs a warning, letting you distinguish whether the original box was crossed normally.

The execution path is identical to the original: `AnyEvent("T3")` reaches
`IcebreakerCrew.OnSpawnEvent`'s `PlaceChargeSweep("BotZoneOutside_t3")`, while
`AnyEvent("T4")` reaches BSG `BossSpawnScenario` → `BotBossSpawn.SpawnBossAndFollowers`
→ the `IcebreakerFinalSquad` prefix. These are the same calls made by the boxes.

Log:

```
[WaveBackstop] engine room: a player got within 39m of the spawn markers
               and the authored trigger never fired - raising 'hides0' (group=1)
```

### T4 squad spawning only 1/5

```
[T4Squad] whole squad deferred: T4 has only 1/5 safe, separated spawn positions
```

The original 1.0.0's new `IcebreakerFinalSquad` requires 5 separated T4 spawn positions,
but `BotZoneInside_t4` has only 2 markers (`markers=2`). It searches a 5.4m radius around markers
to generate the rest, but **rejects every position visible to the player**.
If the player can see the room, candidates can fall to 1, deferring the entire squad through
`DelayBossSpawn`. In the 09/07 log it took 3 deferrals to reach 5/5, by which time
the player had already passed the area.

This fork **still prefers out-of-sight positions but no longer requires them**. If fewer than 5
are found, it fills the remainder with positions farthest from humans (still rejects anything within 8m),
without deferring the squad. Hidden deployment is ideal, but a late squad is worse than a visible one.

### Porting the original 1.0.1 changes

The original 1.0.1 **source was not uploaded to GitHub** — its `1.0.1` tag points to the same commit
and tree hash as `1.0.0`, while the distributed DLL reports `1.0.1+c7d6430`
(that commit's `ModVersion` is `1.0.0`). It was built from uncommitted local changes,
so the changes were **reconstructed from the distributed files**.

| Original changelog | Actual cause | Porting approach |
|---|---|---|
| duplicate loot entries | Same `composedKey` twice at one spawn point in `looseLoot.json` (225 of 505 points, 1005 extra entries) | Inferred the transformation from the distributed file and applied it to ours → **all 505 confirmed identical** |
| trader quests appearing before visiting the map | The gate passed if a quest row existed in the profile, but SPT **writes `AvailableForStart` rows for every visible quest** → the gate opened as soon as a trader offered it | Changed to pass only at `Started` or later |
| later btr quests bypassing required return visits | The gate was checked only in `/client/quest/list`; the unlock path on quest acceptance (`GetNewlyAccessibleQuestsWhenStartingQuest`) was unchecked | `IcebreakerProgression` directly patches `QuestHelper` |
| boreas part 3 not unlocking Icebreaker | `GenerateAll` returns a `LocationBase` **shared with the DB**, but wrote per-profile `Enabled/Locked` directly onto it → the last queried profile overwrote global state | Clone with `ICloner` and modify only the copy (also removed the now-unnecessary lock) |
| duplicate raid ends counting twice | The client retries `/client/match/local/end`, or Fika sends it multiple times, and every call was counted | Added a `Raids` set to the ledger — each `ServerId` counts once |
| improved map visit tracking | The visit ledger was scattered as static data inside routers | Extracted to `IcebreakerVisitLedger` + atomic save via temporary file then move |
| fika headless loading crashes | Client DLL side | **Deliberately not ported** — this fork does not use Fika |

Verification: compared the rebuilt ported DLL with the official 1.0.1 DLL at metadata level → **identical type/method/field
structure** (differences only in our `IcebreakerGoonGuard` and private field names).

---

<a id="110-동기화"></a>

## 1.1.0 sync

Merged the original 1.1.0 (`728ea51`, 09/12). Upstream had rewritten history, making the common
ancestor for the three-way merge pre-4.1 and causing conflicts in 27 files. Each file was
reviewed individually to determine whether upstream had already fixed the issue.

### Our fixes replaced by upstream fixes

| Our work | Upstream 1.1.0 |
|---|---|
| Removed duplicate `composedKey` entries in `looseLoot.json` | Fixed directly upstream (`loose loot duplicate key fix`) — reconfirmed **0 duplicates** after merging |
| Reconstructed 1.0.1 server fixes from DLL (`IcebreakerProgression`, `IcebreakerVisitLedger`) | Actual source published. **Upstream is a superset** — includes our 2 patches, adds `QuestHelper.FailedUnlocked`, and uses SPT's `AbstractPatch` instead of raw Harmony |
| — | Fika headless/MP loading fix (`FikaBridge.CanRender` guard), which we had deferred as outside scope |
| — | **Abandoned hijacking the Suburbs slot** in favor of registering a unique location ID (`icebreaker` / `882b2fa04bbd616567022938`) |

### Our fixes retained because they are still absent upstream

The 5 performance fixes (`BuildDistanceCuller` frame spreading, removal of bot polling `FindObjectsOfType`,
lens flare allocation removal, LOD recalculation budget, `AICorePoint` cache) and 6 crash/bug fixes
(`IcebreakerGoonGuard`, `IcebreakerWaveBackstop`, T4 spawn fallback, `IcebreakerSnowGusts` duplicate guard,
`BreathEffector` finalizer, onIce debounce, `PatrolScanner` small-room fallback, `OrbitBrainLayerCompat`)
are absent in 1.1.0 and were all retained. Configuration differences also remain:
`EscapeTimeLimit` 90 minutes and `DeployToGame` default `true`.

### Our fixes required by the location ID transition

Once upstream stopped using Suburbs, **our code depending on that slot would silently inspect
the wrong location**. Two places were affected.

- `IcebreakerGoonGuard.OurWaves()` read `locationTable.Suburbs.Base.BossLocationSpawn`.
  Left unchanged, it would guard the empty vanilla Suburbs stub while SPT's Goons rotation
  still reduced our T1 Knight wave to 0%.
  → Replaced with `locationTable.GetLocation(IcebreakerLocation.Key)`.
- The onIce check in `RaidFixPatches` used the literal `"Suburbs"`.
  → Replaced with `IcebreakerLocation.Matches()` (debounce logic retained).

Upstream had already moved `IceGate` to `IcebreakerLocation.Key`, so the SAIN/ORBIT
compatibility patches attached to it follow automatically.

### Where both fixes were needed

`IcebreakerSnowGusts.Spawn()` — upstream added the `FikaBridge.CanRender` guard where we
added the duplicate spawn guard. They serve different purposes, so **both** were retained.

### Verification

- Server project built successfully (0 errors). Remaining warnings are all pre-existing nullable warnings in upstream code.
- `verification` project built successfully. Running it requires an SPT installation + built client DLL,
  so it could not be executed in this environment.
- The client project needs 11 game assemblies, including `UnityEngine.AIModule`, and cannot build here.
  **All source was checked with Roslyn syntax parsing**, and all build-target source passed
  (only 11 decompiled dumps in `docs/wedge-ai-src/` failed parsing; they are not build targets).
- Confirmed 0 `looseLoot.json` duplicates, 0 duplicate `BossLocationSpawn` entries in `base.json`,
  and `EscapeTimeLimit` still 90.

---

## Bots dying while still standing (2026-09-15)

> "There is always one bot dying in its previous healing/shooting pose (instead of becoming a ragdoll)."

Both error logs from that day contain the same stack **once each**:

```
NullReferenceException
  OfflinePlayerCulling.ApplyVisibleState ()      [0x00016]
  BasePlayerCulling.SetMode (EMode mode)
  BasePlayerCulling.Disable ()
  BasePlayerCulling.DisableCullingOnDead ()
  EFT.LocalPlayer.OnDead (EDamageType)
  ... ActiveHealthController.Kill → TryToKillAfterDestroyPart → ApplyDamage
```

`OnDead` calls `DisableCullingOnDead()` early during death handling. An exception there
**aborts the rest of `OnDead`**, including the ragdoll transition. The body therefore freezes
in whatever pose the animator held — healing or firing. This also explains roughly one bot per raid:
the culling state must already be broken at the exact moment of death.

**No mod patches `OfflinePlayerCulling`** (all Harmony logs from that raid were checked).
The null is inside BSG code, so there is no field we can fix. Instead, **prevent that exception
from aborting `OnDead`**: `Patch_CullingDeathAirbag` swallows it at the call site,
letting `OnDead` continue to the ragdoll transition.

The tradeoff is leaving that corpse's culling state partially unresolved — at worst it may flicker
at a distance. **Flickering is preferable to freezing upright**, and every occurrence is logged:

```
[CullDeath] OfflinePlayerCulling.ApplyVisibleState threw (1 this session) —
            swallowed so the rest of OnDead runs and the body still ragdolls.
```

The method is found by name (`AccessTools.TypeByName`). If BSG renames it, `Prepare`
returns `false` and only this patch quietly drops out, keeping the entire `PatchAll` from failing.

## Korean locale (2026-09-16)

Map/banner/UI text goes into `TranslatedLocales`'s `kr`; quests use each trader folder's `kr.json`.
The Korean quest translations from 0.3.0 were updated for 1.1.3.

| Trader folder | Keys | Reused 0.3.0 translations | New/changed |
|---|---|---|---|
| `656f0f98d80a697f855d34b1` | 66 | 64 | **2** (English text changed) |
| `mechanic` | 37 | 37 | 0 |
| `peacekeeper` | 39 | 13 | **26** (2 new 1.1.3 quests) |
| `prapor` | 35 | 35 | 0 |
| `ragman` | 24 | 24 | 0 |
| `skier` | 24 | 24 | 0 |
| `therapist` | 24 | 24 | 0 |

**Compared against the 0.3.0 tag rather than simply copying.** An unchanged key can hide changed
English text, silently making a translation outdated. Files extracted with `git show 0.3.0:<경로>`
were compared by value against current `en.json`; 2 differences were found:

- `42fae5461bd56964c4fb5107` — Interchange added to target locations
  (`... on Shoreline` → `... on Shoreline or Interchange`)
- `3f8d2c5a9b17e04d6ca8f312 successMessageText` — fare changed from "paid"
  to "half price, 250,000"

The 26 entries in `peacekeeper` belong to the new 1.1.3 quests `PeacefulAtom` / `WiringTheVessel`.
They follow Peacekeeper's existing polite Korean speech and established terms such as
`만(灣)` (bay), `쇄빙선` (Icebreaker), and `메카닉` (Mechanic).

Upstream removed the quest definitions for `ragman` / `skier` but left their locales (only
`Locales` folders remain, without `Quests`). They are unused now, but `kr.json` is retained
to match upstream's retained `ch.json`.

Key order matches each folder's `en.json` (or `ch.json` if absent), making future upstream
text changes immediately visible with the same comparison.


Added `kr` to `TranslatedLocales`, covering the same scope as `ru`: map name/description,
3 loading banners, extraction names, and client plugin UI text.

**Why a separate Korean translation mod did not work.** Locale writes are `LazyLoad` transformers,
so **English is written back every time `.Value` is read**.

```csharp
kv.Value.AddTransformer(locale =>
{
    locale[IcebreakerLocation.Id + " Name"] = "Icebreaker";   // ← 읽을 때마다 실행
    ...
    if (translated is not null)
        foreach (var (key, text) in translated) locale[key] = text;   // ← 여기만 이김
    return locale;
});
```

External locale files therefore cannot override these keys; **only entries in the per-language table
win**. This is also why upstream added `ru` / `ch` that way.

Both key forms are populated, as with vanilla maps (for example, both `bigmap` and
`56f40101d2720b2a4d8b45d6 Name` exist).

| Key | Used for |
|---|---|
| `882b2fa04bbd616567022938 Name` | Map selection card title |
| `icebreaker` | Map list, transits, and other references by map key |
| `882b2fa04bbd616567022938 Description` | Map selection card description (`LocationInfoPanel`) |
| `Icebreaker_Exit_Heli` | Extraction name on the extraction timer panel |

## Live verification (2026-09-15 evening raid)

Logs confirmed that both fixes actually triggered. This records **the mechanism running**,
rather than merely an absence of symptoms.

**Frozen-corpse airbag** — caught 4 occurrences in one raid. Without the fix, those 4 corpses
would have frozen in their animation poses.

```
[CullDeath] OfflinePlayerCulling.ApplyVisibleState threw (4 this session) —
            swallowed so the rest of OnDead runs and the body still ragdolls.
```

**T3 prerequisite** — the backstop did not trigger even when the player came **within 5m** of markers;
it fired **only immediately after T2**. Without the prerequisite, that distance would have triggered it much earlier.

```
[Waves] botEvent 'T2' raised t=132s
[WaveBackstop] wedge approach: a player got within 5m of the spawn markers ... raising 'T3'
[Waves] botEvent 'T3' raised t=134s
```

### Issues confirmed to originate outside this map

- **Frame drops in engine room fights, Goons encounters, and the final wave** — instrumentation gave the answer.
  All 93 stutters showed `OURS=1~2ms`, with the remainder `UNTRACKED`. Icebreaker code did not cause them.
  ```
  [Stutter] f=2248 1023ms frame: OURS=1.7ms UNTRACKED=1021ms distCull=1.3ms ...
  ```
- **Black Division standing idle without reloading empty magazines** — `Use Items Anywhere` 2.1.4 expanded BSG's
  static `Inventory.FastAccessSlots` array by union, causing SAIN's reload check to throw
  `IndexOutOfRangeException` every tick. UIA 2.1.3 produced **0 occurrences** on the same map/settings.
  The issue was especially visible here because BlackDiv removes vanilla combat layers and leaves only SAIN;
  the cause is outside this repository.
- **ORBIT** throws `The given key 'icebreaker' was not present in the dictionary` on this map.
  `RaidFirewall` swallows it, leaving other mods functional, but ORBIT itself is disabled here.
  Its per-map table needs an `icebreaker` entry (a fix in that mod).

## 1.1.3 sync

Merged upstream's `1.1.3` tag. Upstream mainly changed content while this fork mainly changed code,
so overlap was limited. However, **a conflict-free merge is not necessarily safe**, so every fix
was verified individually.

### What upstream brought

| Category | Contents |
|---|---|
| New quests | Peacekeeper's `PeacefulAtom`, `WiringTheVessel` (+ 2 banners and quest assortments) |
| Translations | Full Russian (PR #12), full Chinese (PR #10) |
| Data | `looseLoot.json` rework, `BoreasQuests` adjustments |
| Verification | New `verification/ClientAudioChecks.cs` |

### One conflict — `IcebreakerCrew.cs`

**Upstream independently made the same optimization as us.** With this map's prop count,
`FindObjectsOfType<BotOwner>()` takes 70~80ms per call. Both sides replaced it with
iteration over `GameWorld.AllAlivePlayersList`.

Comparison showed **upstream was a superset of our implementation**.

| | This fork | Upstream 1.1.3 |
|---|---|---|
| Removed `FindObjectsOfType<BotOwner>` | 4 sites (`AliveRogues` and 3 others) | **13 sites** (`LiveBots()`) |
| Removed `FindObjectsOfType<BotZone>` | 0 sites | **6 sites** (`AllBotZones()` + zone cache) |

We therefore **accepted upstream's implementation in full** and reinserted only the 2 lines
for this fork's `IcebreakerWaveBackstop` hooks. Our `AllBotOwners()` helper was deleted;
a full search confirmed no remaining callers.

### Verified checks

- **Full conflict-marker search** — 0 matches
- **Symbol comparison** — all 4 external symbols referenced by fork-specific files
  (`IcebreakerAIPlaces.Raised`, `IcebreakerLocation.Key`, `IcebreakerCrew.BdIb`,
  `IcebreakerTripwires.OwnerId`) remain defined after the merge
- **`IcebreakerCrew` member comparison** — all 11 members called across the tree remain defined
  (checked for references broken by replacing our implementation with upstream's)
- **Hardcoded key comparison** — checked again because this caused a regression during the 1.1.0 sync.
  The zone names used by `IcebreakerWaveBackstop`, `BotZoneEngineHide` / `BotZoneStern` /
  `BotZoneSternTop`, exist in `base.json`; `bossKnight`, guarded by `IcebreakerGoonGuard`,
  exists in `BossLocationSpawn`. `ModLocationKey` / `ModLocationId` are unchanged upstream
- **Full JSON parsing** — all 53 valid
- **Brace balance** — balanced in all 6 modified source files

### ⚠️ New hard dependencies added in 1.1.3

Upstream added another dependency. **The mod will not load without it.**

```csharp
// icebreaker-server/IcebreakerMod.cs
// Boreas Part 6 counts kills in the retail q14_10_kill_ice zone, which only
// exists on the backported Interchange map
{ "com.manimal.interchange", new SemanticVersioning.Range("~1.0.0") }

// icebreaker-client/Plugin.cs
[BepInDependency("com.manimal.interchange", "1.0.0")]
[BepInDependency("com.arys.unitytoolkit")]
```

- **`com.manimal.interchange` ~1.0.0** — Interchange backport mod. Boreas Part 6 counts kills
  in the retail `q14_10_kill_ice` zone, which exists only on the backported Interchange map.
- **`com.arys.unitytoolkit`** — new

Without the Interchange backport, or with an incompatible version, Icebreaker will not load at all.
This requirement did not exist in 1.1.0.

### What could not be verified

**Compilation was not attempted.** This work environment has no .NET SDK, and network policy
blocks its download host. The checks above are static comparisons and do not replace a build.
**You must check the build and an actual raid yourself.**

### Upstream's broader fix — removed `IcebreakerGoonGuard.cs`

This fork's `IcebreakerGoonGuard`, created on 09/07, fixed SPT 4.1's `GoonLocationSpawnService`
setting every map's `bossKnight` rows to 0%, preventing the T1 Knight wave from ever arriving.

**Upstream fixed the same problem more broadly in 1.1.3.** It is `GoonRotationPatch` inside
`IcebreakerLootFirewall`; even its patch name is the same `.goonguard`.

| | This fork's `GoonGuard` | Upstream `GoonRotationPatch` |
|---|---|---|
| Target method | `GoonLocationSpawnService.AdjustGoonMapSpawns` | Same |
| Restore timing | **1** rotation postfix site | Rotation postfix + **2 raid-start backstop sites** |
| Restored value | `base.json` snapshot | Hardcoded `100` |
| Clone order | Not considered | **Considered** |

Upstream's comment identified what we missed: `StartLocalRaid` clones the location **before**
loot generation, so restoring only at loot generation is already too late for that raid and fixes
only subsequent raids. Upstream therefore restores **immediately after the code that zeroes the chance**.

The hardcoded restored value is the only disadvantage compared with this fork, but `bossKnight`'s
`BossChance` in `base.json` is **exactly 100**, so there is no practical difference.

Keeping both would attach two postfixes to the same method; after editing `base.json`, they could
fight by writing different values. **This fork's file was deleted.** A full search confirmed no callers
(it was registered only through automatic DI discovery).

### Fork-specific fixes retained

| File | Status |
|---|---|
| `IcebreakerWaveBackstop.cs` | Retained (absent upstream) |
| `IcebreakerCompatPatches.cs` | Retained |
| 2 `IcebreakerSnowGusts` guards | Retained |
| `RaidFixPatches` off-ice debounce | Retained |
| `Directory.Build.props` path override | Retained (accepted upstream `ModVersion 1.1.3`) |
| `ragman` / `skier` Chinese locales | Retained (traders not covered by upstream translation PRs) |
| `AllBotOwners()` helper | **Deleted** — upstream `LiveBots()` covers more |
| `IcebreakerGoonGuard.cs` | **Deleted** — upstream `GoonRotationPatch` covers more |

---

## Reduce simultaneous engine room/helipad backstop spawns (2026-09-19)

> **Unverified** — the cause was found through code review and the fix follows that analysis;
> it has not been checked in an actual raid. Please report if squads still spawn together next raid.

**Report**: after entering the engine room, going above/below the helipad or near the entrance
just before the wedge approach spawns 3 squads on one tick. With SAIN active, they immediately
hear sounds from afar and take positions, making the encounter feel unnatural.

**Cause**: `IcebreakerWaveBackstop`'s `Hide` (engine room) and `Sten` (above/below helipad)
guards checked a 40m radius **without a height limit (a sphere)**. With decks 3~4m apart,
a 40m unrestricted sphere reaches through 10 floors vertically. At certain stairs/intersections,
engine room, upper helipad, and lower helipad markers on different decks all fall within 40m.
All three guards (plus the wedge tier guard if its conditions are met) then trip on the same 0.5-second tick.
T3/T4 already received a "same deck" height band (`SameDeck`, 3.5m) on 09/15 for this reason,
but Hide/Sten did not, based on the assumption that they were too far apart to overlap.
This report shows that assumption was wrong.

**Fix**: added the `SameDeck` (3.5m) height band to Hide/Sten too. The 40m horizontal
range remains (its original purpose is early squad placement when a box is missed), but
"nearby" now requires **both** within 40m horizontally and on the same deck vertically (±3.5m).
Squads on different decks no longer trip each other's guards.

**Tradeoff**: an approach involving a vertical turn (stairs from another deck) will not trigger
the backstop until that deck is reached. The "missed box leads to late spawning" issue fixed
on 09/08 may therefore slightly return on such routes. These two zones were designed around
mostly flat approaches 104m/156m long to their spawn positions (§09/08), so the practical impact
is expected to be small.

### Follow-up — height bands alone were insufficient (2026-09-20)

> **Unverified** — this analysis is also inferred from coordinate comparison and has not
> been checked in an actual raid.

**Follow-up report**: reproduced in live play after the fix. Going upstairs through the ER keycard
door sometimes triggered the upper wedge/T3 trigger first. Reaching only the middle of the
engine room could also bring out helipad squads, although the engine room should have only 4 enemies.

**Actual cause**: **distance itself**, rather than floor height. Coordinates measured on 09/08
put the engine room squad (BotZoneEngineHide, z=-21) and helipad squads
(BotZoneStern/SternTop, z=-67~-71) only 46~50m apart. With a 40m proximity radius,
the **midpoint between them** (z≈-45, roughly the middle of the engine room) is within 40m
of both using horizontal distance alone. The height band added on 09-19 cannot help
when both groups are at similar heights.

**Fix**: reduced `ApproachRadius` from 40m to **20m**. This is safely below half the distance
between squads (23~25m out of 46~50m), so no point between them is "near" both simultaneously.
The fallback still spawns squads when a box is completely missed; only its range is shorter.

**Early T3 (wedge) triggering**: separate code (`ArrivedRadius=12m`), unrelated to this radius
change and outside this fix. The ER keycard shortcut may satisfy both T2 and T3 proximity
conditions earlier than the normal route, but the exact cause needs `WaveBackstop` /
`botEvent 'T3'` logs. Those logs have not been obtained, so this remains unresolved.

### Follow-up 2 — enlarge trigger boxes instead of tuning radii (2026-09-20)

> **Unverified** — not checked in an actual raid.

The issue was still reported in live play after the 20m change. Instead of tuning the radius/height
band a third time, the approach changed.

**Comparison with upstream (danauraborealis/ManimalIcebreaker)**: `IcebreakerAIPlaces.cs` is
**identical** upstream and in this fork. It reactivates physical `BoxCollider` triggers in the level
bundle; upstream uses **only** those physical triggers. `IcebreakerWaveBackstop` (distance-based
insurance) is an additional safety net unique to this fork.

**Reframing the cause**: `IcebreakerWaveBackstop` is only insurance for missed boxes,
but its radius tuning caused the issue to recur three times. Instead of asking about the
insurance logic's radius calculation, ask why the insurance is needed so often. As recorded
on 09/08, **the original physical trigger boxes are too narrow for the corridors**, allowing
even normal routes to miss them (the same box was crossed after 43 seconds in one raid
and 17 minutes in another).

**Fix**: instead of tuning detection again, **expanded the Hide/Sten boxes' width/depth (X/Z)
by 1.5×** at the point where `IcebreakerAIPlaces.cs` reinserts them into the scene
(initially 3×, reduced after feedback that this was excessive). Height Y is unchanged to avoid
reintroducing the cross-deck problem fixed on 09-19. Normal routes are now more likely
to cross the enlarged boxes directly.

`IcebreakerWaveBackstop`'s 20m insurance remains. Larger boxes are expected to greatly reduce
its actual use, leaving it as the final safety net for unusual routes that avoid even these boxes.

---

## Known issues

**Antialiasing / blurry LOD distance (unresolved)**

Reproduces within the same client session and **also occurs in the original 1.0.0 build**;
this fork's diff is not the cause. In SPT 4.0.10 it occurred only after visiting the hideout;
in 4.1.5 it can occur even when entering a raid directly.

Ruled out in the 09/03 investigation: TargetDummies, HideoutShootout, BetterVision,
Hideout Init Race Fix, PiP-Disabler, CompoundingPerf, leftover DLSS5/OptiScaler/ReShade,
`ScopeZoomHandler` NRE, `CamDonorSkip`, and leftover `QualitySettings.lodBias`.
See `<26/09/03 상세 변경점>` (<26/09/03 detailed changes>) below for the full history.

**Translucent loot item icons (unresolved)**

Confirmed unrelated to `CamDonorSkip` on 09/03. Requires a separate investigation.

---

## Changes

<26/09/08 detailed changes>

- Fixed enemies appearing much later in the engine room, helipad, and below the helipad.
  All Black Division waves use `Time: 9999` + `TriggerName: botEvent` in `base.json`, so they
  spawn **only after a trigger box is crossed**. Boxes are 69~80m from squad positions,
  with only one per wave; a route missing it leaves the area empty. Logs show the same hides
  box crossed at t=43s in one raid and t=1032s (17 minutes) in the next.
  Added `IcebreakerWaveBackstop`: if a human reaches within 40m of spawn markers without
  the trigger firing, it calls the same `GlobalEventDispatcher.AnyEvent` as the box.
  Covers only engine room/stern (wedge boxes are inside the rooms they populate; T1/T3/T4
  are tier gates on the required route, and distance checks would fire early and disrupt their intended timing)

- T4 squad spawning only 1/5: the cause was **the visibility filter**, rather than 2 markers.
  The original generates more positions around markers but rejects all visible to the player.
  Seeing the room can reduce candidates to 1 and defer the whole squad through `DelayBossSpawn`
  (09/07 logs: arrived after 3 deferrals, after the player had passed). Out-of-sight remains
  preferred but is no longer required: fill missing positions from those farthest from humans,
  still rejecting positions within 8m

- Ported 5 original 1.0.1 server fixes. **Source was not uploaded** (tag added to the 1.0.0 commit,
  identical tree hash, distributed DLL `1.0.1+c7d6430` while that commit's `ModVersion` is `1.0.0`),
  so they were reconstructed from the distributed `looseLoot.json` and `icebreaker-server.dll`.
  All 17 quest JSON files were identical on comparison; all fixes were in code.
  See "Porting the original 1.0.1 changes" above for details

- Verification: inferred the `looseLoot.json` transformation from the distribution and applied it to ours,
  then confirmed all 505 spawn points matched (first verified byte-identical reserialization of the
  original, eliminating formatting noise in the diff). Compared rebuilt server DLL metadata against
  official 1.0.1 and confirmed identical type/method/field structure (differences only in our
  `IcebreakerGoonGuard` and private field names)

- Not ported: Fika headless loading crash — client-side, and this fork does not use Fika

---

<26/08/29 detailed changes>

- Ship boarding/stair stutters: spread LOD cull floor recalculation across frames with a per-frame budget

- Translucent inventory icons: expanded the default camera graft skip list
  (DesaturateEffect/Antialiasing/Tonemapping/PerfectCullingCamera). Existing cfg values
  are not updated automatically; edit CamDonorSkip manually

- Icebreaker costs appearing outside the map (lobby, etc.): changed off-ice cleanup
  from every-frame repetition to a one-time operation

- 54ms `AICorePoint` rediscovery on bot spawns: cached per world to avoid rescanning
  for every bot — one source of stutters when multiple bots spawn at doors/zone entrances

- Wedge ambush cover search repeated on every restart (up to 166 times during combat):
  reuse the initial cover position within the same combat window

- Black Division/Wedge stuck in small rooms (BotZoneRoomsThird, etc.) because patrol path
  generation failed (only 1 reachable point): added a tighter-spacing retry fallback
  when the original spacing fails

- Crash from 5500+ BreathEffector NullReferenceException spam entries: silently swallow
  only on Icebreaker (vanilla maps still throw, matching the pattern used by other effectors)

- IcebreakerSnowGusts particle systems spawning up to 12 duplicates per raid and using
  90%+ of frame time: added a duplicate spawn guard

- (The onIce debounce bundled with that fix introduced a new bug, fixed below.) After leaving
  Icebreaker for another map (Woods, etc.), the first 30 frames still misidentified the map as
  Icebreaker, running Icebreaker-only logic (including camera/graft handling) there. This caused
  grayscale Woods and later failure to enter Icebreaker. Debounce now applies only during genuine
  transitions when GameWorld cannot yet be read; once the map is known, it updates immediately

- The Prefab override added to fix the missing BBQ-S43 icon actually crashed static loot
  placement at raid start. Reverted that unverified hypothesis-based fix (the icon returns
  to its original state and may still be invisible)

- ORBIT failed its own initialization because it did not recognize this map. During Wedge spawning,
  its BigBrain layer constructor threw, also preventing Wedge's other AI layers (room/ambush, etc.)
  from attaching. This caused Wedge to remain completely unresponsive even when hit. Without changing
  ORBIT's code, this fork forces ORBIT's "exclude this bot" path on Icebreaker to prevent the conflict

- A single 17.9-second frame at raid start: lens flare setup scanned the entire scene and allocated
  a Dictionary per node, including leaf nodes (170,000+ renderers made this especially wasteful).
  Nodes with no children now skip allocation

- Fixed the build path to the actual local SPT installation

- (Retest report) UnstackPatrol (every 20 seconds for 4 minutes) and C3KeycardSweep (every 5 seconds
  for 5 minutes) used FindObjectsOfType<BotOwner>() to scan the whole scene whenever they fetched bots.
  This appeared as intermittent profiler load spikes/stutters up to ~55ms. Replaced with iteration over
  the game's existing GameWorld.AllAlivePlayersList (same pattern as Wedge boss tracking)

- Result: major stutter improvement (field reports: frame impact 19~30% → 8~10%), engine room/C-1
  spawn lag resolved, small-room patrols restored, grayscale Woods/Icebreaker entry crashes fixed,
  and retest UnstackPatrol/C3KeycardSweep spikes removed. Getting stuck at doors for 15 seconds
  and barter item icons moved to follow-up SAIN/settings fixes (see the SAIN README)

---

<26/08/30 detailed changes>

- Build paths (SPTPath/FikaPath) were assigned unconditionally without Condition, ignoring
  command-line path overrides. Made them overridable (no effect on actual use)

- Found the root cause of Wedge ignoring gunshots/knife hits outside its frontal vision:
  Blood Ambush's hide-position search radius contradicted its "at least 10m away" requirement,
  always failing; an unrestricted fallback then selected its own feet (0m) as cover.
  Expanded the search radius, and if no position is found even then (a truly small room),
  abandon the ambush and hand control directly to vanilla/SAIN combat. Confirmed working in field testing

- HollywoodGraphics failed initialization on our camera and threw every frame throughout the raid.
  Added a defensive Icebreaker patch (a second safeguard separate from HollywoodGraphics's own fix;
  see the HollywoodGraphics README)

- Result: Wedge unresponsiveness confirmed fully resolved; no other new issues

---

<26/09/02 detailed changes>

- BuildDistanceCuller synchronously scanned 148000+ scene renderers in one frame at raid start.
  Changed to the same per-frame budget slicing pattern as RebindShaders (measured down to
  388.9ms/53 calls; not the cause of the raid-start freeze itself, but a separate real cost worth fixing)

- Found the actual cause of lag after breaching the door until the Goons + 2 Rogues finished spawning:
  frames spiked to 60~95ms about once per second (4~6× the normal 15ms) for over 70 seconds.
  HoldEngineSquad (0.5-second interval), PlaceChargeSweep, and PlaceWedgeTag (both 1-second interval),
  started by hides/stern triggers, scanned the entire scene with FindObjectsOfType<BotOwner>()
  every cycle until their target bot counts were met. This was exactly the pattern fixed in
  UnstackPatrol/C3KeycardSweep on 08/29, but these three coroutines had been missed.
  The profiler showed HoldEngineSquad alone accumulating over +6 seconds during the lag
  (39~110 seconds in the log), stopping exactly when the bot hold count filled (= Goons + 2 Rogues
  finished spawning). This precisely matched the user's "lag stops only after the Goons + 2 Rogues
  spawn" report. Replaced all three with GameWorld.AllAlivePlayersList iteration,
  matching AliveRogues and similar methods

- Found 7 entirely duplicate entries in base.json's BossLocationSpawn: hides1·hides2·hides3·stern0·
  stern1·stern2·stern3. BossSpawnScenario matches base.json directly by TriggerId, so every trigger
  spawned the same squad twice (29 extra bots per raid, on exactly the triggers being polled above).
  Removed all duplicates (40 → 33 entries)

- The single 17~18-second freeze just after raid start (t≈9 seconds) was confirmed separate:
  the whole BotsController.Init chain synchronously executes cover/voxels/AIPlaces/lens flares
  and other mods' hooks within one frame. It does not affect the rest of the raid and fixing it
  requires much riskier structural changes, so it was deferred

- Result confirmed by field reports: before the fix, door entry caused 60~95ms spikes roughly every
  second for about 70 seconds, with average frame time also rising into the 20ms range. Afterward,
  the same area stabilized at 14.7~15ms average, with spikes only every 5~8 seconds and reduced
  to 30~60ms. Lag after breaching and entering confirmed resolved

---

<26/09/03 detailed changes>

- Found broken antialiasing/jagged object edges when entering an Icebreaker raid immediately
  after leaving the hideout. Reproduced 100% within the same client session (direct Icebreaker
  entry without visiting the hideout always worked; visiting the hideout always broke it).
  Other maps did not reproduce it, confirming an issue with Icebreaker's camera

- Ruled out during investigation: TargetDummies, HideoutShootout, BetterVision/BetterThermalNightVision,
  Hideout Init Race Fix, PiP-Disabler, CompoundingPerf (server only, no client code), and leftover
  DLSS5/OptiScaler/ReShade files. `ScopeZoomHandler.method_1()` NRE spam initially looked promising
  (seen in 2 apparently relevant raids), but later raids reproduced the issue without it, so that
  conclusion was corrected: an unrelated coincidence. `PostProcessLayer`/`SSAA` field values
  (CamAutopsy dumps) were always normal, suggesting a problem below what these diagnostics inspect,
  in the actual rendering path

- Actual cause: the 08/29 default `CamDonorSkip` had become `DesaturateEffect,Antialiasing,
  Tonemapping,PerfectCullingCamera` (skip all four). That commit had not identified which component
  was responsible; skipping all four was a temporary workaround (previous default: empty string,
  graft all four). With them skipped, Icebreaker's shell Cam2 camera lacks four components a real
  map camera would have. Normal maps recreate cameras each raid, automatically clearing hideout
  leftovers, whereas Icebreaker reuses objects with ADD-ONLY grafting (never modifying existing
  components), so the empty slots were suspected of inheriting hideout state. Resetting
  `CamDonorSkip` to an empty string ("") and grafting all four appeared to fix every reproduction attempt

- Incidental finding: the translucent unexamined loot icons that motivated the 08/29 skip occur
  identically whether components are skipped or restored. They therefore appear unrelated to all
  four components (the 08/29 hypothesis was wrong). The icon bug remains unresolved and needs
  a separate investigation

- (Another fix the same day) Completely emptying `CamDonorSkip` to "" introduced **object edges
  becoming stranger at longer distances**. Confirmed absent before the commit time (15:01 UTC),
  after the user initially said it had existed earlier and then corrected that on rechecking.
  `PerfectCullingCamera` was the main suspect (third-party occlusion culling, suspected of conflicting
  with this map's sidecar culling volumes by distance). Narrowed `CamDonorSkip` to skip only
  `PerfectCullingCamera`. Whether grafting only the other three (DesaturateEffect/Antialiasing/Tonemapping)
  still fixed hideout → Icebreaker AA needed verification

- (Another correction, final) After retesting, the user corrected the result: changing `CamDonorSkip`
  to anything (skip all / skip none / skip only PerfectCullingCamera) actually made no difference
  to reproduction. The apparently fixed test had coincidentally skipped the hideout. Restored
  `CamDonorSkip` to its original 08/29 default (`DesaturateEffect,Antialiasing,Tonemapping,PerfectCullingCamera`,
  skip all); the setting was unrelated to this bug

- Also suspected leftover hideout `QualitySettings.lodBias` (Unity global state; the same value
  involved in the 08-18 bug where other maps retained reduced render distance after leaving Icebreaker).
  Promoted the raid-start "game was X" log from LogDebug to LogWarning so it always appeared.
  Reproduction logs showed 2.00 (normal), rejecting this theory too; the log-level change was reverted

- Result: cause not identified. Both `CamDonorSkip`/`lodBias` restored (`Plugin.cs`,
  `RaidFixPatches.cs`); none of the attempted fixes were confirmed to affect symptoms.
  The bug remains 100% reproducible within the same client session after visiting the hideout
  then entering Icebreaker: broken TAA, distance-dependent dot-noise patterns on floors/ceilings,
  and distant lights appearing to shimmer. Taken together, this suggests TAA's per-frame subpixel
  jitter is applied without resolve/blending. Direct Icebreaker entry without the hideout, or
  another map after the hideout, is always normal. Narrowed to Icebreaker's raid re-entry handling,
  but the exact mechanism remains unknown. Ruled out in this session: TargetDummies, HideoutShootout,
  BetterVision/BetterThermalNightVision, Hideout Init Race Fix, PiP-Disabler, CompoundingPerf,
  leftover DLSS5/OptiScaler/ReShade, `ScopeZoomHandler` NRE (coincidence), `CamDonorSkip`, and
  leftover `QualitySettings.
  lodBias`. The only immediate workaround is to avoid entering Icebreaker directly after visiting
  the hideout; investigate again when a reliable new clue appears

---

<26/09/07 detailed changes>

- Moved to SPT 4.1.5 / original 1.0.0. Accepted the original's 4.1 migration through a **merge**
  (GitHub "Sync fork → Discard commits" would erase all this fork's performance/crash fixes).
  Only 4 conflicts; the rest merged automatically because our fixes and upstream renames touched
  different hunks. Accepted upstream `WedgeBrainLayers.cs` in full: upstream reduced it from
  765 to 146 lines, deleting custom room/ambush/escort-wait combat layers and handing control
  to BD's native brain. The Wedge ambush code fixed on 08/30 no longer exists

- Removed HollywoodGraphics Bloom NRE defense: the HollywoodGraphics 4.1 port now directly
  null-checks in `GraphicsController.Update`, making our patch redundant. It was only reading
  fields through reflection every frame

- **Goons (bossKnight) + 2 Rogues not spawning after door breach — cause confirmed and fixed.**
  The cause was SPT 4.1's new `GoonLocationSpawnService`, rather than a mod issue. It rotates the
  Goons between 4 vanilla maps (`bigmap`/`woods`/`shoreline`/`lighthouse`) every 3 hours, but its reset
  pass is unconditional: sets `bossKnight` rows to `BossChance = 0` on **all** maps except
  `hideout`/`develop`, then restores chance only on the selected map from that pool. Icebreaker's
  T1 is that exact `bossKnight` row (Knight + 2 exUsec followers, `BotZoneMash_t1`), but `Suburbs`
  is in neither the pool nor blacklist, leaving it at 0%. Logs showed exactly this: the trigger
  fired normally as `[Waves] botEvent 'T1' raised t=1044s`, yet nothing spawned, while the same raid's
  `hides0`/`stern0`/`T3`/`wedges1` (blackDivIb/bossWedge) all delivered normally.
  **The only failed wave that raid was the single bossKnight wave.** `IcebreakerGoonGuard`
  postfixes `AdjustGoonMapSpawns` to restore our row's chance. The service uses `IOnUpdate`
  (5-second loop, actual execution at rotation windows), so a one-time load fix would be zeroed
  again at the next window. Chance comes from a load-time snapshot rather than a hardcoded value,
  preserving edits in `base.json`

- **Server mod missing from the installation after building — cause confirmed and fixed.**
  The path was already correct (`$(SPTServerPath)\user\mods\ManimalIcebreaker`). Original 1.0.0
  defaulted `DeployToGame` to `false` and gated the **entire** `icebreaker-server` PostBuild target
  on it, so neither DLL nor db was copied. This fork restores the default to `true`.
  **As a side effect, none of the `base.json` edits had been applied**: removal of 7 duplicate
  spawns, 90 minutes, or `BossEscortAmount` adjustments. The installed file was the release zip's original

- Antialiasing/blurry LOD cause still unidentified. Three facts from logs:
  (1) `[LOD] bias clamp on (game was 2.00)` — the mod's default `LodBiasClamp` 0.8 overwrites
  the game's 2.00. As `IcebreakerLodCullFloor` comments explain, BSG authored culling heights
  for lodBias ≥ 2, so beyond close-range correction (19m indoors/27m outdoors), culling is 2.5× more aggressive.
  (2) `CamDonorSkip` was empty this raid, grafting all of
  `[UltimateBloom, DesaturateEffect, Antialiasing, Tonemapping, PerfectCullingCamera]`
  (previous raid: only `[UltimateBloom]`), because BepInEx prioritizes saved cfg values over code defaults.
  (3) PiP-Disabler 1.5.0, absent in the previous raid, was newly installed and prefixes
  `CameraLodBiasController.SetBiasByFov`, entirely skipping EFT's own FOV-based LOD bias adjustment.
  However, (1), (2), and (3) had each already been ruled unrelated to symptoms during 09/03's investigation;
  camera autopsy also showed identical `SSAA.UseJitter=False` in both raids, so it is not a useful variable.
  The original 1.0.0 build reproduces the same issue, confirming it is not caused by this fork's code

- Result: Goons T1 spawning and automatic server deployment confirmed restored. `icebreaker-server`
  built against actual SPT 4.1.5 packages (0 errors). AA/LOD and translucent loot icons remain
  unresolved. A newly identified upstream issue is insufficient T4 squad spawn positions (`markers=2` vs 5 required)
