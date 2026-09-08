# ManimalIcebreaker (fork)

> **원작자 · 원본 레포**
> **danauraborealis** — https://github.com/danauraborealis/ManimalIcebreaker
>
> **라이선스: MIT**
>
> 이 레포는 위 원작의 **포크**입니다. 맵도 에셋도 퀘스트도 전부 원작자의 것이고,
> 여기서 한 건 실사용 중에 잡힌 성능/크래시 문제를 고쳐서 얹은 것뿐입니다.
> 기능 추가나 밸런스 변경은 없습니다.

현재 기준: **upstream 1.0.0 / SPT 4.1.5**

---

## 이 포크가 원작과 다른 점

원작 1.0.0의 SPT 4.1 마이그레이션은 **머지로 받았습니다** (덮어쓰기 아님). 원작 쪽
변경은 역난독화 리네임과 신규 컨텐츠(IcebreakerFinalSquad, IcebreakerSplash,
IcebreakerOpticWeather, 트립와이어 재작성, CustomSpawnpoints)이고, 아래 수정들은
원작에 들어있지 않아서 그대로 유지했습니다.

**성능**

| 수정 | 증상 |
|---|---|
| `BuildDistanceCuller` 프레임 분산 | 라이드 시작 시 씬 전체 148000+ 렌더러를 한 프레임에 동기 스캔 |
| 봇 폴링에서 `FindObjectsOfType` 제거 | `HoldEngineSquad`/`C3KeycardSweep`/`PlaceChargeSweep`/`PlaceWedgeTag`/`UnstackPatrol`이 매 주기 씬 전체를 훑으며 ~55ms 스파이크 |
| 렌즈 플레어 노드당 Dictionary 할당 제거 | 라이드 시작 시 17.9초짜리 프레임 하나 |
| LOD 컬 플로어 재계산 프레임 예산제 | 갑판 올라갈 때 프레임 처짐 |
| `AICorePoint` 월드 단위 캐싱 | 봇 스폰마다 54ms 재탐색 |

**크래시 / 버그**

| 수정 | 증상 |
|---|---|
| `IcebreakerGoonGuard` (신규, 09/07) | SPT 4.1의 군즈 로테이션이 T1 웨이브를 0%로 죽임 |
| `IcebreakerWaveBackstop` (신규, 09/08) | 엔진룸·선미 트리거 박스를 우회하면 그 구역이 통째로 비어 있다가 한참 뒤에 스폰됨 |
| `looseLoot.json` 중복 엔트리 제거 (09/08) | 한 스폰포인트에 같은 `composedKey` 가 두 번 — Lots of Loot 등에서 루팅 생성 실패 (원작 1.0.1과 동일 결과 검증) |
| 원작 1.0.1 서버 수정 이식 (09/08) | 퀘스트 게이트 우회, 맵 언락 실패, 라이드 종료 중복 카운트 — 아래 참고 |
| T4 스폰 지점 폴백 (09/08) | 시야 밖 지점 5개를 못 찾으면 스쿼드 전체를 미뤄서 앰부시가 늦게 도착 |
| `IcebreakerSnowGusts` 중복 생성 가드 | 라이드당 최대 12번 중복 생성, 프레임의 90%+ 점유 |
| `BreathEffector` 파이널라이저 | NRE 5500+회 스팸으로 크래시 |
| onIce 디바운스 + off-ice 정착 가드 | 쇄빙선 나간 뒤 다른 맵에서 쇄빙선 로직이 계속 돎 |
| `PatrolScanner` 좁은 방 폴백 | 좁은 구역에서 봇이 그 자리에 못 박힘 |
| `OrbitBrainLayerCompat` | ORBIT이 `Suburbs`를 몰라서 던지는 예외가 우리 AI 레이어 생성까지 같이 죽임 |
| `base.json` 중복 `BossLocationSpawn` 7개 제거 | 같은 스쿼드가 트리거마다 두 번씩 스폰 (라이드당 봇 29마리 여분) |

**설정 차이**

- `EscapeTimeLimit` 90분 (원작 50분)
- `DeployToGame` 기본값 `true` (원작 `false`)

---

## 빌드 / 설치

```
dotnet build ManimalIcebreaker.sln
```

경로는 `Directory.Build.props`에서 나옵니다. 기본값은 `SPTPath = E:\SPT 4.1`이고,
`-p:SPTPath=...` 또는 동명의 환경변수로 덮어쓸 수 있습니다.

빌드하면 자동으로 아래에 배포됩니다:

| 대상 | 내용 |
|---|---|
| `$(SPTPath)\BepInEx\plugins\ManimalIcebreaker\` | 클라이언트 dll |
| `$(SPTPath)\SPT_Runtime\user\mods\ManimalIcebreaker\` | 서버 dll + **`db/` 전체** + `bundles.json` |

**주의**: 서버 쪽은 `db/` 전체를 덮어씁니다. 설치 폴더에서 직접 수정한 json이 있으면
날아갑니다. 반대로, 레포에서 `base.json`을 고쳤는데 인게임에 반영이 안 된다면 빌드가
배포까지 갔는지부터 확인하세요.

씬 번들 2개(615MB scenes + preset)는 용량 때문에 매 빌드 복사 대상이 아닙니다.
최초 1회 수동 배치하거나 `package-release.ps1`을 쓰세요.

배포를 끄려면 `-p:DeployToGame=false`.

---

## 알려진 문제

**안티앨리어싱 / LOD 거리 흐려짐 (미해결)**

같은 클라이언트 세션에서 재현되며, **원작 1.0.0 빌드에서도 동일하게 발생합니다** —
이 포크의 diff가 원인이 아닙니다. SPT 4.0.10 시절에는 하이드아웃을 다녀온 뒤에만
터졌는데, 4.1.5에서는 바로 라이드에 들어가도 터집니다.

09/03 조사에서 제외 확인된 것: TargetDummies, HideoutShootout, BetterVision,
Hideout Init Race Fix, PiP-Disabler, CompoundingPerf, DLSS5/OptiScaler/ReShade 잔재,
`ScopeZoomHandler` NRE, `CamDonorSkip`, `QualitySettings.lodBias` 잔재.
자세한 경위는 아래 `<26/09/03 상세 변경점>` 참고.

**T4 스쿼드가 1/5만 스폰됨 (09/08 완화)**

```
[T4Squad] whole squad deferred: T4 has only 1/5 safe, separated spawn positions
```

원작 1.0.0이 새로 넣은 `IcebreakerFinalSquad`는 T4에 분리된 스폰 지점 5개를
요구하는데, `BotZoneInside_t4`의 마커는 2개뿐입니다 (`markers=2`). 원작은 마커 주변
반경 5.4m를 훑어 나머지를 만들어내지만, **플레이어 시야에 걸리는 지점을 전부 버립니다.**
그래서 플레이어가 그 방을 볼 수 있는 위치에 있으면 후보가 1개까지 떨어지고, 스쿼드
전체가 `DelayBossSpawn` 으로 미뤄집니다 — 09/07 로그에서 3번 연기된 뒤에야 5/5로
붙었고, 그때는 플레이어가 이미 그 구간을 지나간 뒤였습니다.

이 포크는 **시야 밖 지점을 여전히 우선하되 필수 조건에서는 뺐습니다.** 5개가 안 나오면
사람에게서 가장 먼 지점들로 채우고(8m 이내는 계속 거부), 스쿼드를 미루지 않습니다.
숨어서 나오는 게 최선이지만, 늦게 오는 스쿼드가 보이는 스쿼드보다 나쁩니다.

**원작 1.0.1 이식 (09/08)**

원작 1.0.1은 **GitHub에 코드가 안 올라왔습니다** — `1.0.1` 태그가 `1.0.0`과 같은 커밋을
가리키고 tree 해시까지 동일한데, 배포된 DLL은 `1.0.1+c7d6430` 으로 찍혀 있습니다
(그 커밋의 `ModVersion` 은 `1.0.0`). 즉 커밋 안 한 로컬 수정본으로 빌드해서 배포한
것이라, 변경분을 **배포 파일에서 역으로 복원**했습니다.

| 원작 changelog | 실제 원인 | 이식 방법 |
|---|---|---|
| duplicate loot entries | `looseLoot.json` 한 스폰포인트에 같은 `composedKey` 2번 (505개 중 225개, 여분 1005개) | 배포 파일과 대조해 변환 규칙을 읽어내고 우리 파일에 적용 → **505개 전부 일치 확인** |
| trader quests appearing before visiting the map | 게이트가 "퀘스트 행이 프로필에 있으면 통과"였는데, SPT는 **보이는 모든 퀘스트에 `AvailableForStart` 행을 씁니다** → 트레이더가 제안하는 순간 게이트가 풀림 | `Started` 이상일 때만 통과하도록 변경 |
| later btr quests bypassing required return visits | 위 게이트를 `/client/quest/list` 라우터에서만 검사 → 퀘스트 수락 시 해금 경로(`GetNewlyAccessibleQuestsWhenStartingQuest`)는 무검사 | `IcebreakerProgression` 이 `QuestHelper` 를 직접 패치 |
| boreas part 3 not unlocking Icebreaker | `GenerateAll` 이 돌려주는 `LocationBase` 는 **DB와 공유되는 인스턴스**인데 거기에 프로필별 `Enabled/Locked` 를 직접 씀 → 마지막에 조회한 프로필이 전역 상태를 덮어씀 | `ICloner` 로 복제 후 사본만 수정 (락도 불필요해져서 제거) |
| duplicate raid ends counting twice | 클라가 `/client/match/local/end` 를 재시도/fika에서 여러 번 보내는데 매번 카운트 | 원장에 `Raids` 집합 추가 — 같은 `ServerId` 는 한 번만 |
| improved map visit tracking | 방문 원장이 라우터 안에 static 더미로 흩어져 있었음 | `IcebreakerVisitLedger` 로 분리 + 임시파일 후 move 방식 원자적 저장 |
| fika headless loading crashes | 클라이언트 DLL 쪽 | **미이식** — 서버 DLL만 확보 |

검증: 이식 후 빌드한 DLL을 공식 1.0.1 DLL과 메타데이터 단위로 대조 → **타입·메서드·필드
구성 완전 일치** (차이는 우리 `IcebreakerGoonGuard` 와 private 필드명뿐).

**엔진룸 / 헬리패드 스폰 타이밍 (09/08 수정)**

블랙디비전 웨이브는 전부 `base.json` 에 `Time: 9999` + `TriggerName: botEvent` 로
들어있어서, **오직 플레이어가 트리거 박스를 지나야만** 스폰됩니다. 그리고 박스는
스쿼드가 배치되는 곳에서 한참 떨어져 있습니다:

| 트리거 | 박스 위치 | 스쿼드가 뜨는 곳 | 거리 |
|---|---|---|---|
| `hides*` (엔진룸) | z=+59, y=20 (선수 상부구조) | `BotZoneEngineHide` z=-21 | **80m** |
| `stern*` (헬리패드/그 아래) | z=+2 (중앙부) | `BotZoneSternTop` z=-67, `BotZoneStern` z=-71 | **69~73m** |

"미리 자리를 잡고 있다"는 연출 의도지만, **박스를 안 밟는 경로로 가면 그 구역이
통째로 비어 있습니다.** 로그가 그대로 보여줍니다 — 같은 hides 박스가 한 판은 t=43s에,
다음 판은 **t=1032s(17분)** 에 밟혔습니다. 박스는 하나뿐이고, 엔진룸은 그 박스를 안
거치고도 갈 수 있습니다. 그래서 "멀리까지 진행하고 나서야 뒤늦게 스폰"이 됩니다.

`IcebreakerWaveBackstop` 이 박스를 거리로 받쳐줍니다. 사람이 어떤 스쿼드의 스폰
마커 **40m 안**까지 접근했는데 그 트리거가 아직 안 떴으면, 박스가 부르는 것과 똑같은
`GlobalEventDispatcher.AnyEvent` 를 대신 불러서 BSG 웨이브 파이프라인이 정상 경로로
배치하게 합니다. 박스가 제대로 밟히는 판에서는 이미 이벤트가 떠 있으므로 아무 일도
하지 않습니다.

- 40m인 이유: 어떤 플레이어 시작 지점에서든 가장 가까운 해당 존까지 **104m(엔진룸) /
  156m(선미)** 라, 라이드 시작부터 걸릴 일이 없습니다
- 엔진룸/선미 **두 개만** 받칩니다. 웨지 박스는 자기가 채우는 방 안(4~16m)에 있고,
  T1/T3/T4 박스는 필수 동선의 티어 진행 게이트라 거리로 받치면 오히려 원작보다
  **빨리** 터져서 연출 순서가 흐트러집니다

로그:

```
[WaveBackstop] engine room: a player got within 39m of the spawn markers
               and the authored trigger never fired - raising 'hides0' (group=1)
```

**루팅 아이템 아이콘 반투명 (미해결)**

`CamDonorSkip`과 무관한 것으로 09/03에 확인됨. 별도 조사 필요.

---

## 변경점

<26/08/29 상세 변경점>

- 배 탑승/계단 구간 스터터링: LOD 컬 플로어 재계산을 프레임당 예산제로 분산 처리

- 인벤토리 아이콘 반투명하게 보이던 문제: 카메라 그래프트 기본 스킵 목록 확장
  (DesaturateEffect/Antialiasing/Tonemapping/PerfectCullingCamera) — 기존 cfg 파일에
  값이 저장돼 있으면 자동 반영 안 되니 CamDonorSkip 값 수동 수정 필요

- 맵 밖(로비 등)에서도 쇄빙선 관련 비용이 찍히던 문제: off-ice 정리 로직이 매 프레임
  반복되던 걸 1회성으로 정리

- 봇 스폰 시 54ms씩 걸리던 AICorePoint 재탐색: 월드 단위로 캐싱해서 매 봇마다 다시
  스캔 안 하게 함 — 문 앞/구역 진입 시 다수 봇 스폰될 때 스터터링 원인 중 하나였음

- 웨지 앰부시가 재시작될 때마다(교전 중 최대 166회) 커버 지점을 매번 새로 탐색하던
  문제 — 같은 교전 윈도우 내에서는 처음 정한 커버 지점 재사용하게 함

- 좁은 방(BotZoneRoomsThird 등)에서 순찰 경로 생성이 실패해서(도달 가능 지점 1개뿐)
  블디/웨지가 그 자리에 완전히 못 박혀 있던 문제 — 기존 간격으로 실패하면 더 좁은
  간격으로 재시도하는 폴백 추가

- BreathEffector NullReferenceException 5500+회 스팸으로 인한 크래시 — 쇄빙선에서만
  조용히 삼키도록 수정 (바닐라 맵은 그대로 예외 발생, 다른 이펙터들과 동일 패턴)

- IcebreakerSnowGusts 파티클 시스템이 라이드당 최대 12번 중복 생성되며 프레임의
  90%+ 잡아먹던 크래시 — 중복 생성 방지 가드 추가

- (위 수정과 같이 넣었던 onIce 디바운스가 새 버그였음 — 아래서 다시 수정) 쇄빙선
  이탈 후 다른 맵(우드 등) 진입 시 첫 30프레임 동안 여전히 쇄빙선인 것처럼 오판해서
  쇄빙선 전용 로직(카메라/그래프트 관련 포함)이 그 맵에서 실행되던 문제 — 우드 맵이
  흑백으로 보이고, 이후 쇄빙선 자체가 안 들어가지는 원인이었음. GameWorld를 아직 못
  읽은 진짜 전환 구간에서만 디바운스 적용하고, 맵이 확인되면 즉시 반영하도록 수정

- BBQ-S43 아이콘 안 보이는 문제 고친다고 추가했던 Prefab 오버라이드가 실제로는
  라이드 시작 시 정적 루팅 배치에서 크래시를 일으키고 있었음 — 미확인 가설이었던
  수정을 되돌림 (아이콘은 원래 상태로, 즉 여전히 안 보일 수 있음)

- 오빗(ORBIT) 모드가 우리 맵을 몰라서 자체 초기화에 실패 → 웨지 스폰 시 오빗의
  BigBrain 레이어 생성자가 예외를 던지며 웨지의 다른 AI 레이어(룸모드/앰부시 등)까지
  같이 못 붙던 문제 — 웨지가 총 맞아도 완전 무반응이던 원인. 오빗 쪽 코드는 안 건드
  리고, 쇄빙선에서 오빗의 "이 봇 제외" 경로를 우리 맵에서 강제로 타게 만들어서 충돌
  자체를 차단

- 라이드 시작 시 17.9초짜리 프레임 하나가 통째로 멈추던 문제 — 렌즈 플레어 배치 시
  씬 전체를 훑으면서 노드마다(리프 노드 포함) Dictionary를 새로 할당하던 게 원인
  (맵 전체 17만+ 렌더러 규모라 낭비가 컸음). 자식 없는 노드는 할당 자체를 건너뛰게 함

- 빌드 경로를 실제 로컬 SPT 설치 경로로 고정

- (재테스트 리포트 반영) UnstackPatrol(20초마다 4분간)·C3KeycardSweep(5초마다 5분간)
  코루틴이 봇 목록을 가져올 때마다 FindObjectsOfType<BotOwner>()로 씬 전체를 훑고
  있어서, 모드 프로파일러 상에서 점유율이 잡혔다 사라지는 스터터링 스파이크(최대
  ~55ms)로 보였던 문제 — 게임이 이미 들고 있는 GameWorld.AllAlivePlayersList를
  순회하는 방식으로 교체 (웨지 보스 추적 로직이 쓰던 것과 같은 패턴)

- 결과: 성능 스터터링 크게 개선(필드 리포트 기준 프레임 영향 19~30% → 8~10%), 엔진룸/
  C-1 스폰 렉 해소, 좁은 구역 순찰 정상화, 우드 흑백/쇄빙선 미입장 크래시 해결, 재테스트
  때 잡힌 UnstackPatrol/C3KeycardSweep 스파이크 해소. 문 앞 15초 낑김·바터 아이템
  아이콘은 SAIN/설정 쪽 후속 수정으로 넘어감 (SAIN README 참고)

---

<26/08/30 상세 변경점>

- 빌드 경로(SPTPath/FikaPath)가 Condition 없이 무조건식으로 박혀 있어서 커맨드라인
  으로 다른 경로를 줘도 무시되던 문제 — 오버라이드 가능하게 수정 (실사용엔 영향 없음)

- 웨지가 총/칼에 맞아도 정면 시야 아니면 완전 무반응이던 문제의 진짜 근본 원인 발견:
  블러드 앰부시가 은신처를 찾을 때 검색 반경과 "최소 10m 이상 떨어져야 함" 조건이
  서로 모순돼서 항상 실패 → 거리 제한 없는 폴백이 "자기 발밑(0m)"을 은신처로 골라
  버리던 버그. 검색 반경을 넉넉히 넓히고, 그래도 못 찾으면(정말 좁은 방) 아예 앰부시
  포기하고 바로 바닐라/SAIN 전투로 넘기도록 수정 — 필드 테스트로 정상 작동 확인 완료

- 다른 모드(HollywoodGraphics)가 저희 카메라에서 초기화 실패하면서 라이드 내내 매
  프레임 예외를 던지던 문제 — 쇄빙선 쪽에 방어용 패치 추가 (HollywoodGraphics 자체
  수정과 별개로 이중 안전장치, HollywoodGraphics README 참고)

- 결과: 웨지 무반응 문제 완전 해결 확인, 그 외 새 이슈 없음

---

<26/09/02 상세 변경점>

- 라이드 시작 시 씬 전체 148000+개 렌더러를 한 프레임에 동기 스캔하던
  BuildDistanceCuller — RebindShaders와 같은 프레임당 예산제 슬라이싱 패턴으로
  변경 (측정 결과 388.9ms/53콜로 감소, 라이드 시작 프리즈 자체의 원인은 아니었지만
  별개로 실재하던 비용이라 정리)

- 문 따고 진입해서 군즈+로그 2마리 다 스폰될 때까지 프레임이 초당 한 번꼴로
  60~95ms까지 튀며(평상시 15ms 대비 4~6배) 최대 70초 넘게 지속되던 렉의 진짜
  원인 발견: hides/stern 트리거가 뜨면 시작되는 HoldEngineSquad(0.5초 주기)·
  PlaceChargeSweep·PlaceWedgeTag(둘 다 1초 주기) 코루틴이, 목표 봇 수를 채울
  때까지 매 주기 FindObjectsOfType<BotOwner>()로 씬 전체를 훑고 있었음 — 08/29에
  UnstackPatrol/C3KeycardSweep에서 이미 고쳤던 것과 완전히 같은 패턴인데 이 세
  코루틴만 그때 빠짐. 모드 프로파일러로 확인해보니 HoldEngineSquad 하나만 렉
  지속 구간(로그 상 39~110초)에 +6초 넘게 누적되다가, 봇 홀드 카운트가 다
  찬(=군즈+로그 2마리 스폰 완료) 시점부터 정확히 늘어나길 멈춤 — 유저가 말한
  "군즈+로그 2마리 스폰돼야 렉 풀림" 증상과 정확히 일치. 세 코루틴 모두
  GameWorld.AllAlivePlayersList 순회 방식(AliveRogues 등이 쓰던 것과 동일)으로
  교체

- base.json의 BossLocationSpawn에 hides1·hides2·hides3·stern0·stern1·stern2·
  stern3 총 7개 항목이 완전히 똑같은 내용으로 중복 등록되어 있던 문제 발견 —
  BossSpawnScenario가 TriggerId로 base.json을 직접 매칭해서 스폰하기 때문에,
  해당 트리거가 뜰 때마다 같은 스쿼드가 두 번씩 스폰되고 있었음(라이드당 봇
  29마리 여분 생성, 하필 위 코루틴들이 한창 폴링 중이던 바로 그 트리거들). 중복
  항목 전부 제거(40개 → 33개)

- 라이드 시작 직후(t≈9초)에 한 번 지나가는 17~18초짜리 프리즈는 이번 렉과는
  별개 현상(BotsController.Init 전체 체인 — 커버/복셀/AIPlaces/렌즈플레어
  생성에 다른 모드 훅까지 한 프레임에 동기 실행)으로 확인. 라이드 진행에는
  영향 없고 고치려면 훨씬 위험한 구조 변경이 필요해서 이번엔 보류

- 결과: 필드 리포트로 확인 — 수정 전엔 문 진입 후 약 70초 동안 프레임이 초당
  한 번꼴로 60~95ms까지 튀며 평균 프레임타임도 20ms대로 같이 올라갔는데, 수정
  후 같은 구간에서 평균 14.7~15ms로 안정되고 튀는 빈도도 5~8초에 한 번, 폭도
  30~60ms대로 줄어듦. 문 따고 들어갈 때 렉 해소 확인 완료

---

<26/09/03 상세 변경점>

- 하이드아웃 갔다가 나온 직후 곧바로 쇄빙선 라이드에 들어가면, 안티앨리어싱이
  깨져서 사물 테두리가 계단현상으로 보이는 문제 발견 — 같은 클라이언트 세션
  안에서 100% 재현됨(하이드아웃 안 거치고 바로 쇄빙선 들어가면 항상 정상,
  하이드아웃 다녀오면 항상 깨짐). 다른 맵으로 가면 재현 안 됨 — 쇄빙선 카메라
  자체 문제로 확인

- 원인 찾는 과정에서 아닌 것으로 확인되어 제외한 것들: TargetDummies,
  HideoutShootout, BetterVision/BetterThermalNightVision, Hideout Init Race
  Fix, PiP-Disabler, CompoundingPerf(서버 전용이라 클라이언트에 코드 자체가
  없음), DLSS5/OptiScaler/ReShade 잔재 파일. `ScopeZoomHandler.method_1()`
  NRE 폭주도 처음엔 유력한 단서로 보였으나(관련 있어 보이는 라이드 2개에서
  확인) 이후 그 NRE 없이도 터지는 라이드가 나와서 원인이 아닌 것으로 정정 —
  단순 우연히 같이 발생했던 별개 현상이었음. `PostProcessLayer`/`SSAA`
  필드값(CamAutopsy 덤프)도 매번 정상으로 나와서, 문제는 이 진단들이 보는
  것보다 더 아래(실제 렌더 경로) 레벨에서 발생하는 것으로 추정

- 진짜 원인: `CamDonorSkip` 기본값이 08/29에 `DesaturateEffect,Antialiasing,
  Tonemapping,PerfectCullingCamera`로(넷 다 스킵) 바뀌어 있었음 — 그 커밋
  자체는 그 넷 중 정확히 뭐가 범인인지 밝히지 못한 채로 "일단 넷 다 스킵하고
  보자"는 임시방편이었음(그 전엔 기본값이 빈 문자열 — 넷 다 그래프트하는
  쪽이었음). 이 스킵 상태에서는 쇄빙선의 Cam2 껍데기 카메라에 실제 맵 카메라가
  갖고 있을 부품 네 개가 빠진 채로 남는데, 정상 맵은 매 라이드 카메라를 완전히
  새로 만들어서 하이드아웃발 잔재가 자동으로 씻겨나가는 반면, 쇄빙선은 기존
  오브젝트를 재활용 + ADD-ONLY 그래프트(이미 있는 컴포넌트는 절대 안 건드림)
  방식이라 그 빈 자리에 하이드아웃에서 넘어온 상태를 그대로 물려받는 것으로
  추정됨. `CamDonorSkip`을 다시 빈 문자열("")로 되돌려서 네 컴포넌트를 전부
  그래프트하게 하니 재현 시도마다 전부 고쳐짐

- 곁다리로 확인된 것: 08/29에 이 스킵을 넣은 이유였던 "안 본 루트 아이템
  아이콘이 반투명하게 보이는" 버그는 이 값을 스킵/복원 어느 쪽이어도 동일하게
  존재함 — 즉 애초에 그 아이콘 버그는 이 네 컴포넌트 중 어느 것 때문도
  아니었던 것으로 보임(08/29의 추측이 빗나감). 아이콘 버그는 여전히 미해결
  상태로 남음, 별도 조사 필요

- (같은 날 재수정) `CamDonorSkip`을 ""로 완전히 비우니, 이번엔 **거리가 멀어질
  수록 오브젝트 테두리가 이상해지는 새 증상**이 생김 — 커밋 시각(15:01 UTC)
  기준으로 이 전엔 없었다고 확인됨(사용자가 처음엔 "그 전에도 있었다"고 했다가
  재확인 후 정정). 네 컴포넌트 중 `PerfectCullingCamera`가 유력 용의자(써드파티
  오클루전 컬링이라 이 맵 자체 사이드카 컬링 볼륨이랑 거리 기준으로 충돌할
  걸로 추정) — `CamDonorSkip`을 `PerfectCullingCamera` 하나만 스킵하도록 좁힘.
  나머지 셋(DesaturateEffect/Antialiasing/Tonemapping)만 그래프트해도 하이드아웃
  →쇄빙선 AA 깨짐은 그대로 고쳐진 채 유지되는지 확인 필요

- (또 재수정, 최종) 사용자가 재테스트 후 정정: `CamDonorSkip`을 뭘로 바꾸든
  (전체 스킵/전체 해제/PerfectCullingCamera만 스킵) 실제로는 증상 재현 여부에
  아무 차이가 없었음 — 앞서 "고쳐졌다"고 봤던 결과는 그 테스트 때 하필
  하이드아웃을 안 거쳐서 우연히 정상으로 보였던 것으로 정정. `CamDonorSkip`은
  원래 08/29 기본값(`DesaturateEffect,Antialiasing,Tonemapping,PerfectCullingCamera`
  전부 스킵)으로 되돌림 — 이 설정은 애초에 이 버그랑 무관했음

- 추가로 `QualitySettings.lodBias`(유니티 전역 상태, 08-18에 "쇄빙선 나가면
  다른 맵 렌더 거리도 줄어든 채 남는" 비슷한 버그가 있었던 그 값)가 하이드아웃
  발 잔재일 가능성을 의심해서, 라이드 시작 시 캡처하는 "game was X" 로그를
  LogDebug에서 LogWarning으로 올려 상시 노출되게 해봄 — 재현 로그에서 값이
  2.00(정상)으로 나와서 이 이론도 기각, 로그 레벨 변경도 되돌림

- 결과: 원인 특정 실패. `CamDonorSkip`/`lodBias` 둘 다 원상복구(`Plugin.cs`,
  `RaidFixPatches.cs`) — 이번 조사에서 시도한 수정 중 실제로 증상에 영향을 준
  것은 하나도 확인되지 않음. 버그 자체는 100% 재현됨이 확인된 상태로 남음
  (같은 클라이언트 세션에서 하이드아웃 방문 후 쇄빙선 진입 시 매번 발생: TAA
  깨짐 + 거리에 따라 바닥/천장 등에 도트 노이즈 패턴 + 먼 거리 불빛이 떨리는
  것처럼 보임 — 종합하면 TAA의 프레임별 서브픽셀 지터가 적용은 되는데 리졸브/
  블렌드가 안 되고 있는 것으로 추정. 하이드아웃 없이 바로 쇄빙선 들어가거나,
  하이드아웃 후 쇄빙선 아닌 다른 맵으로 가면 100% 정상 — 쇄빙선 자체의 라이드
  재진입 처리 문제로 좁혀졌으나 정확한 메커니즘은 못 찾음). 이 세션에서 제외
  확인된 것: TargetDummies, HideoutShootout, BetterVision/BetterThermalNightVision,
  Hideout Init Race Fix, PiP-Disabler, CompoundingPerf, DLSS5/OptiScaler/ReShade
  잔재, `ScopeZoomHandler` NRE(우연의 일치였음), `CamDonorSkip`, `QualitySettings.
  lodBias` 잔재. 당장의 우회법은 "하이드아웃 다녀온 직후엔 쇄빙선 바로 안
  들어가기"뿐 — 재현 확실한 새 단서 나오면 재조사

---

<26/09/07 상세 변경점>

- SPT 4.1.5 / 원작 1.0.0으로 이동. 원작의 4.1 마이그레이션을 **머지**로 받음(GitHub
  "Sync fork → Discard commits"를 쓰면 이 포크의 성능/크래시 수정이 전부 날아감).
  충돌은 4개뿐이었고, 나머지는 이쪽 수정과 원작의 리네임이 서로 다른 hunk라 자동
  머지됨. `WedgeBrainLayers.cs`는 원작 것을 통째로 채택 — 원작이 765줄에서 146줄로
  줄이면서 커스텀 룸/앰부시/호위대기 전투 레이어를 삭제하고 BD 네이티브 브레인에
  넘겼기 때문에, 08/30에 고쳤던 웨지 앰부시 관련 수정들은 고치던 코드 자체가 사라짐

- HollywoodGraphics Bloom NRE 방어 패치 제거 — HollywoodGraphics 4.1 포팅본이
  `GraphicsController.Update`에서 직접 null 체크하도록 고쳐져서 이제 중복. 매 프레임
  리플렉션 필드 읽기만 하고 있었음

- **문 따고 진입 후 군즈(bossKnight) + 로그 2명이 안 나오던 문제 — 원인 확정, 수정.**
  모드 문제가 아니라 SPT 4.1이 새로 추가한 `GoonLocationSpawnService`가 원인이었음.
  이 서비스는 군즈를 바닐라 맵 4개(`bigmap`/`woods`/`shoreline`/`lighthouse`) 사이에서
  3시간마다 로테이션시키는데, 리셋 패스가 무조건적임 — `hideout`/`develop`을 뺀 **모든**
  맵의 `bossKnight` 행을 `BossChance = 0`으로 만든 뒤, 위 풀에서 뽑은 맵 하나만 확률을
  돌려받음. 쇄빙선 T1이 바로 그 `bossKnight` 행(기사 + exUsec 호위 2명, `BotZoneMash_t1`)
  인데 `Suburbs`는 풀에도 블랙리스트에도 없어서 0%로 죽은 채 방치됨. 로그가 정확히 그
  모양이었음 — `[Waves] botEvent 'T1' raised t=1044s`로 트리거는 정상적으로 울리는데
  아무것도 안 나오고, 같은 판의 `hides0`/`stern0`/`T3`/`wedges1`(blackDivIb/bossWedge)은
  전부 정상 딜리버리. **그 판에서 실패한 웨이브는 유일한 bossKnight 웨이브 하나뿐이었음**.
  `IcebreakerGoonGuard`가 `AdjustGoonMapSpawns`에 postfix를 걸어 우리 행의 확률을
  되돌림 — 이 서비스가 `IOnUpdate`(5초 루프, 로테이션 창마다 실제 실행)라서 로드 시
  한 번 고치는 걸로는 다음 창에서 다시 0이 됨. 확률은 하드코딩이 아니라 로드 시점
  스냅샷에서 가져오므로 `base.json`에서 값을 바꿔도 그대로 먹힘

- **빌드해도 서버 모드가 설치 폴더에 안 생기던 문제 — 원인 확정, 수정.** 경로는 원래도
  맞았음(`$(SPTServerPath)\user\mods\ManimalIcebreaker`). 원작 1.0.0이 `DeployToGame`
  기본값을 `false`로 두고 `icebreaker-server`의 PostBuild 타겟 **전체**를 거기에 걸어놔서
  dll도 db도 복사가 안 되고 있었음. 이 포크는 기본값을 `true`로 되돌림.
  **부작용으로 그동안 `base.json` 편집이 하나도 반영되지 않고 있었음** — 중복 스폰 7개
  제거도, 90분도, `BossEscortAmount` 조정도 전부. 설치돼 있던 건 릴리스 zip의 원본

- 안티앨리어싱/LOD 흐려짐은 이번에도 원인 특정 실패. 로그에서 확인된 사실 세 가지:
  (1) `[LOD] bias clamp on (game was 2.00)` — 모드의 `LodBiasClamp` 기본값 0.8이 게임의
  2.00을 덮어씀. `IcebreakerLodCullFloor` 주석대로 BSG는 컬 높이를 lodBias ≥ 2 기준으로
  저작했으므로 근거리 보정(실내 19m/실외 27m) 밖은 2.5배 공격적으로 컬됨.
  (2) 이번 판은 `CamDonorSkip`이 빈 값이라 그래프트가
  `[UltimateBloom, DesaturateEffect, Antialiasing, Tonemapping, PerfectCullingCamera]`
  전부를 얹었음(직전 판은 `[UltimateBloom]`만) — BepInEx가 cfg 저장값을 코드 기본값보다
  우선하기 때문. (3) 직전 판에 없던 PiP-Disabler 1.5.0이 새로 설치됐고,
  `CameraLodBiasController.SetBiasByFov`에 prefix를 걸어 EFT 자체의 FOV 기반 LOD
  바이어스 조정을 통째로 스킵함. 다만 (1)(2)(3) 모두 09/03 조사에서 이미 "증상과 무관"
  으로 한 번씩 걸러진 것들이고, 카메라 부검의 `SSAA.UseJitter=False`도 두 판 로그에서
  동일하게 나와서 변수로 쓸 수 없음. 원작 1.0.0 빌드에서도 동일 재현되므로 이 포크의
  코드 문제는 아닌 것으로 확인

- 결과: 군즈 T1 스폰 정상화 확인, 서버 모드 자동 배포 정상화 확인. `icebreaker-server`는
  실제 SPT 4.1.5 패키지로 빌드 검증(에러 0). AA/LOD와 루팅 아이콘 반투명은 미해결로
  남음. 새로 확인된 원작 쪽 문제로 T4 스쿼드 스폰 지점 부족(`markers=2` vs 필요 5)
