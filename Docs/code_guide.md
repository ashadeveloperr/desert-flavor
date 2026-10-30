# 코드 가이드 — 구조·흐름·바꾸는 법

> 코드를 직접 읽고 고칠 때 **처음 펴는 문서**다. "어디에 무엇이 있나", "한 시간이 흐르면 무엇이 어떤 순서로 도나", "X를 바꾸려면 어디를 고치나"를 적는다.
> 규칙의 **값과 이유**는 여기 없다: 지금 값은 설정 JSON, 정한 이유는 `Docs/implementation_notes.md`(결정 표)·`Docs/decision_log_auto.md`(근거·측정).
> 코드 구조가 바뀌면(파일 추가·이동, 흐름 변경, 새 확장 지점) **같은 커밋에서 이 문서를 고친다.**

## 1. 큰 그림: 세 층

```
 설정 JSON (Assets/Data/Config)         숫자. 코드 수정 없이 바꾼다
        │  EconomyConfigLoader.Parse → EconomyConfig → SettlementRules.FromConfig
        ▼
 Core (Assets/Scripts/Core)             규칙. 순수 C#, Unity 없이 테스트·시뮬레이션
        │  Settlement·AuctionManager·Campaign이 이벤트와 조회 메서드를 내놓는다
        ▼
 Runtime (Assets/Scripts/Runtime)       표시·입력. Core를 읽어서 그리고, 플레이어 명령을 Core에 전한다
```

- **Core는 Runtime을 모른다.** Core 어셈블리는 `noEngineReferences`라 `UnityEngine`을 쓰면 컴파일이 안 된다. 그래서 같은 Core 파일을 `Tools/SettlementSim`(.NET 콘솔)도 링크해서 돌린다.
- **Runtime은 규칙을 계산하지 않는다.** 화면에 보이는 숫자(생산 예상·공사 시간·불가 이유)는 Core의 조회 메서드(`OutputOf`, `SettlementRules.JobHours`, `Can*`)를 부른다. 같은 계산을 Runtime에 다시 쓰면 둘이 어긋난다.
- **결정론:** 같은 시드 + 같은 명령 순서 = 같은 결과. 저장 파일은 이것에 기대어 "시드 + 명령 기록"만 저장한다. 난수는 `DeterministicRandom`(경매 시세만 예외로 `System.Random`, 바꾸면 기존 저장 파일이 깨진다).

## 2. 폴더별 역할

```
Assets/Scripts/
  Core/                DesertSettlement.Core
    Hex/                 HexCoord(axial 좌표), HexLayout(월드 좌표), HexMap(지형), VertexId/EdgeId(꼭짓점·변 키), HexTopology(타일↔변↔꼭짓점)
    Canal/               CanalGraph: 변 위 수로·레벨·막힌 변, 꼭짓점 사이 경로
    Water/               WaterNetworkSolver(물 배분), WaterTypes(입력·설정·결과), WaterRoutes(물길 표시용 조회)
    Economy/             Settlement(정착지 하나, partial 7개 — 아래), SettlementRules(규칙 값), SettlementTypes,
                         SettlementValuation(경매 가치), FacilityCatalog(시설 종류 목록), EconomyEffects(장비·모디파이어 효과 묶음),
                         Disasters(DisasterManager: 재해 일정·전염병), WaterIds(물 배분에 넘기는 id 규칙)
    Auction/             AuctionManager: 경매 일정·예고·입찰·긴급 매각·파산
    Progression/         Campaign(정착지 연속·상점·장비), SaveGame(명령 기록 저장·재실행), MapDefinition/MapLoader(고정 맵),
                         ProceduralMapGenerator(+Config, BSP 절차 맵), RegionModifiers/RegionOffer(지역 약점·강점), DeterministicRandom, SeedSalts(시드 섞는 수)
    Timing/              GameClock(게임 시각·배속·정각 이벤트), GameTimeSettings
    Config/              EconomyConfig(JSON 모양 그대로의 데이터 클래스), EconomyConfigLoader(읽기·검증), JsonKeyCheck(모르는 키 찾기)
  Runtime/             DesertSettlement.Runtime — MonoBehaviour
    (루트)               GameSession(진입점, + .Commands 캠페인 명령·.SaveLoad 저장·.FallbackMap 대체 지형), BuildInputController(마우스 도구), StrategyCameraRig(카메라),
                         DebugSessionBuilder(씬 구성 한 벌을 코드로), RecordBook(최고 기록), HudInputBlocker(UI 위 클릭 차단)
    UI/                  정식 화면(uGUI를 코드로 만든다). GameUi가 뿌리, 구역마다 *View 하나, UiKit = 버튼·글자 만드는 도구 + Theme(색·크기), Hotkeys = 단축키 표
    View/                맵 표시: SettlementView(타일·시설·수로, + .Lanterns 등불·.Labels 머리 위 글자), FacilityModels(시설 모양), FacilityInfo(한국어 문구),
                         DayNightLighting, WaterPathOverlay, BuildableOverlay, DesertBackdrop, WorldVisuals(머티리얼·메시 도우미)
    Tools/               MapSketch(예시 맵 그리기), HexMapDebugView(Gizmo 확인용)
  Editor/              DebugSessionMenu(메뉴·SetupSampleScene), MapSketchEditor
Assets/Tests/EditMode/ Core 단위 테스트(영역별 파일)       Assets/Tests/PlayMode/ 스모크 테스트·화면 캡처
Assets/Data/Config/    economy_balance_v0.8_config.json(경제), procedural_map_config.json(절차 맵)
Assets/Data/Maps/      고정 맵 JSON(지형 + 샘플 배치)       Assets/Data/MapSketches/ 맵 스케치 저장본
Tools/SettlementSim/   .NET 시뮬레이터: Core 파일을 링크해 회귀 검증·밸런스 측정(6절)
Tools/unity-tests.sh   테스트 전용 사본에서 Unity 테스트 실행(열린 에디터와 무관)
```

**`Settlement` partial 파일:** 상태·조회·시장 가격 = `Settlement.cs` · 명령 처리(`Do*`·`Can*`) = `.Actions.cs` · 공개 명령 진입점·기록 = `.Commands.cs` · 경제 틱·작업 큐 = `.Tick.cs` · 물 계산 = `.Water.cs` · 물탱크 = `.WaterTanks.cs` · 구매·판매 = `.Market.cs` · 주택·노동력(주민·배치·우선 고용) = `.Labor.cs`.

## 3. 흐름

### 3-1. 시작

`GameSession.Awake/Initialize` → 설정 JSON·맵 읽기 → `Campaign` 생성 → 첫 `Settlement`(+ `AuctionManager`·`DisasterManager` 부착) → `GameClock` 연결 → `Initialized` 이벤트.
`Initialized`를 구독해 다시 그리는 곳: `SettlementView.RebuildAll`, `GameUi`(모든 View `Reset`), `BuildInputController`, `DesertBackdrop.Rebuild`.

### 3-2. 한 시간이 흐를 때

```
GameSession.Update → GameClock.Advance(실제 초 × 배속)
  → 정각마다 HourPassed → GameSession.OnHourPassed → Settlement.AdvanceHour()   (Settlement.Tick.cs)
       1. 재해 BeginHour(예고·시작·끝)  2. 인원 배치(공사 몫) → 공사 지연(모래폭풍·공사 인원 부족)  3. 끝난 공사 완료  4. 대기열 착공(슬롯·석재)
       5. 인원 배치(AssignLabor: 공사 → 우선 고용 → 물 100% → 물 일부)  6. 물 배분(SolveWater, 우물 방출 × 배치율, 주택 취수, 물탱크 충전·방출 포함)
       7. 시설마다 생산(물 가동률 × 일손 × 재해)·유지비, 식량 수요 = (주민 + 기본 인부) × 1인당  8. 유지비 차감  9. 저장 한도(넘친 생산 폐기)
       10. 주민 떠남·돌아옴(UpdateResidents)  11. 기록(HourRecord)  12. 시장 가격 회복  13. 재해 EndHour(전염병 새 환자·회복, 아픈 주민은 다음 시간 배치에서 빠짐)  14. CurrentHour + 1
  → HourSettled 이벤트 → AuctionManager.OnHourSettled: 경매 예고·개최·긴급 매각·파산 판정
```
일손 = 노동력이 켜져 있으면 시설 배치율(배치 ÷ 필요), 꺼져 있으면(원본 v0.8 회귀) 식량 계수 F — 그때만 시설 `foodUse`가 식량 수요이고 10번 자리에서 다음 시간의 F를 정한다.
배치 순서의 '물 100%'는 직전 시간 물 배분(`LastWater`)으로 본다. 화면 표시용 배치(`LastLabor`)는 명령 직후에도 다시 계산하지만 정산은 매번 새로 계산하므로 결과에 영향이 없다.

화면은 이벤트를 기다리지 않고 **주기적으로 Core 상태를 읽어 다시 그린다**(`GameUi.Update`가 `refreshSeconds`마다 모든 View의 `Refresh`, 버튼 직후엔 `RefreshNow`).

### 3-3. 플레이어 명령

```
UI 버튼·마우스 도구 → Settlement의 공개 명령(Settlement.Commands.cs: RequestBuild, Upgrade, SellFood …)
  → Do*(Settlement.Actions.cs)가 Can*으로 검사하고 상태를 바꿈
  → 성공하면 Record: CommandRecorded 이벤트(Campaign이 저장용 명령 기록에 쌓음) + 명령 순간 착공(StartJobs)
```

- 불가 이유 문자열은 `Can*`이 돌려주고, 툴팁·버튼 비활성도 같은 `Can*`을 쓴다(규칙과 화면이 한 검사).
- 저장(F5)은 시드 + 명령 기록, 불러오기(F9)는 같은 시드로 새 캠페인을 만들고 `SaveGame.Apply`로 명령을 다시 실행한다.
- **명령 입구 규칙(결정 CM1):** 정착지 안 명령(건설·수로·개량·철거·취소·순서·폐쇄·구매·판매)은 화면이 `Settlement`를 직접 부른다. 캠페인 단계 명령(매각·보류·장비 구매·강화·장착·출발·새 캠페인·샘플 배치·저장·불러오기)은 모두 `GameSession`(`GameSession.Commands.cs`·`GameSession.SaveLoad.cs`)을 거친다 — 시계 재개·정착지 연결처럼 세션이 함께 할 일이 있어서 입구를 하나로 둔다. 새 캠페인 단계 명령도 `GameSession.Commands.cs`에 메서드를 더한다.

## 4. 자주 하는 변경 — 고칠 곳 체크리스트

빠뜨려도 **오류 없이 조용히 틀어지는 곳**에 ⚠를 붙였다.

### 새 시설 종류
1. JSON `buildingLevels`에 레벨표 2. `Core/Economy/FacilityCatalog.cs`에 한 줄(이름·분류·지형·색) — 둘이 어긋나면 설정 검증과 `FacilityCatalogTests`가 실패
3. (선택) 모양 `Runtime/View/FacilityModels.cs`(없으면 상자)
4. 생산 시설이면: `SettlementRules.ResourceOf`(만드는 자원)와 `MaxOutput`(레벨표 × 배율)에 한 줄씩. 정산(`AdvanceHour`)·화면 예상치(`OutputOf`)·경매 가치(개발 중 자산·채석장 능력)·재해 배율이 모두 이 둘을 쓴다(결정 PR1). 식량·석재·광물이 아닌 새 자원이면 `ResourceKind`, `AdvanceHour`의 자원별 합계, `HourRecord`, 경매 가치까지 손대야 한다
5. 경매 가치 인프라 보너스가 있으면: `InfraMultipliers`(EconomyConfig) + `SettlementValuation.InfraTable`에 한 줄(운영 중 합계와 공사 중 보너스가 함께 씀, 몇 개든 한 번만 세면 Once)
6. 노동력(이슈 #31): 레벨마다 `buildWorkers`(공사 인원, 없으면 0명)를 넣는다. 운영에 사람이 필요하면 `workers`도 — 그러면 배치 순서·우선 고용·배치율이 저절로 걸린다(`SettlementRules.NeedsWorkers`). ⚠ `buildWorkers`를 빠뜨리면 공사에 사람이 안 들어 노동력 규칙을 건너뛴다. 봇 계획(`ReferenceBot.Plan`)이 그 시설을 지으면 주택 수도 따라 늘어난다

### 노동력 수치 바꾸기(이슈 #31)
JSON `labor`(1인당 식량·물, 떠남·복귀 속도, 무료 주택 수, 슬롯당 기본 인부)와 `buildingLevels.*.workers`·`buildWorkers`·`house[].residents`. 후보 비교는 `Tools/SettlementSim/LaborTuning.cs`의 `LaborCandidates`에 한 줄 더하고 `-- mods campaign 0.25 14000 30 smart lab=이름`(여러 개는 `+`). 흐름 진단 `-- labor [D] [간격]`. 샘플 배치 시각이 바뀌면 `-- labor sample` 값으로 `AuctionCampaignTests`·`DebugSessionSmokeTests` 기대값을 고친다.

### 새 플레이어 명령
1. `Settlement.Actions.cs`에 `Can*`(불가 이유)·`Do*` 2. `Settlement.Commands.cs`에 공개 메서드 — 성공하면 반드시 `Record` 호출 ⚠(안 부르면 저장에서 빠지고 순간 착공도 안 됨)
3. `GameCommand`의 종류 상수 + `SaveGame.Apply`의 case — case를 빠뜨리면 `SaveGameTests.EveryCommandType_IsHandledByReplay`가 실패
4. UI 버튼(`Runtime/UI/…View.cs`) 5. 확인: `dotnet run --project Tools/SettlementSim -- savecheck`, EditMode `SaveGameTests`

### 새 설정 숫자
1. JSON에 키 2. `Core/Config/EconomyConfig.cs`에 같은 이름 속성(+ 기본값·한국어 주석) — 클래스에 없는 키는 불러오기 오류라서 오타는 게임·테스트·시뮬레이터가 바로 잡는다(`JsonKeyCheck`). 설명용 키는 `_`로 시작. 코드가 읽지 않는 값은 두지 않는다
3. 규칙에서 쓰면 `SettlementRules`에 속성 + `FromConfig`에서 옮기기 4. 범위가 중요하면 `EconomyConfigLoader.Validate`에 검사
5. 시뮬레이터는 게임과 같은 로더로 읽으므로 따로 할 일 없음

### 새 효과(장비·지역 모디파이어 공통)
`EconomyEffects`에 속성 → 같은 파일의 `Combine`·`Scaled`·`Describe`(약점을 세게 할 때 줄어드는 배율이면 `ScaledWeakness`의 하한도) → `SettlementRules`에 속성 → `SettlementRules.ApplyEffects` → 규칙에서 사용.
`Combine`·`Scaled`·`Describe`·`ApplyEffects` 중 하나를 빠뜨리면 `ModifierEquipmentTests.Effects_EveryPropertySurvivesCombineScaledDescribeAndApply`가 실패한다. `Scaled`가 일부러 바꾸지 않는 속성이면 그 테스트의 `notScaled`에 더한다.

### 새 재해
`DisasterManager`의 id 상수·지속 시간 switch → `EconomyConfig`에 설정 클래스 → 효과를 거는 곳(생산·수로·공사) → JSON `disasters.types[]`·`regionWeightMultipliers` → 피해 배율 효과면 위 '새 효과'. 문구는 `FacilityInfo.TimedDisasterText`.

### 경매 시각이 바뀌는 규칙
테스트 기대값이 여러 곳에 있다: `AuctionCampaignTests`, `DebugSessionSmokeTests`(샘플 배치 기준 시각), `SettlementTests`(v0.8 스크립트는 예전 착공 기준).

### 새 스크립트 파일
Unity가 만드는 `.meta`도 함께 커밋한다(테스트 사본이 만든 `.meta`는 원본으로 되가져온다). 시뮬레이터는 Core 폴더(Timing 제외)를 폴더 단위로 링크하므로 Core 안의 새 파일은 따로 할 일이 없다. Core에 새 폴더를 만들면 `Tools/SettlementSim/SettlementSim.csproj`에 한 줄 더한다.
세션 오브젝트에 붙는 새 컴포넌트(MonoBehaviour)라면 `DebugSessionBuilder.EnsureComponents`에 한 줄 더하고 씬 재구성(`SetupSampleScene`, 에디터를 닫아야 하므로 먼저 묻는다)으로 SampleScene에 넣는다. 다른 컴포넌트의 `Awake`에서 붙이지 않는다(결정 SC1). 씬에 넣지 않으면 플레이해도 그 컴포넌트가 없다.

### 원본 v0.8 회귀 모드
v0.8 원본 값(테스트의 `V08Golden`, `Assets/Tests/EditMode/TestData.cs`)과 비교할 때는 `SettlementRules.ForV08Regression(config)`로 규칙을 만든다(결정 RG1). v0.8 뒤에 넣은 규칙(시작 자금·가동 숙성·채석장 능력·건설 시간 배율·명령 순간 착공·시장)을 여기서 끈다. EditMode `SettlementTests`의 Script와 시뮬레이터 `RunCoreScript`가 이것을 쓴다. 새 규칙을 넣을 때 **끄는 값**을 이 프리셋에 한 줄 더한다.

## 5. 무엇을 바꾸려면 어디를 고치나

| 바꾸고 싶은 것 | 위치 |
|---|---|
| 시설 비용·생산량·유지비·수로 용량 등 경제 수치 | `Assets/Data/Config/economy_balance_v0.8_config.json` (코드 수정 불필요) |
| 하루 길이 · 건설·개량·철거 시간 배율 | JSON `confirmedRules.gameDayRealSecondsAt1x` · `confirmedRules.constructionHoursMultiplier` |
| 맵 반경, 타일 크기 · 시작 시각, 낮/밤 경계, 배속 목록 | 씬의 `GameSession` 인스펙터 → 맵 · 시간 → Time Settings |
| 태양 색·밝기 · 밤(달빛·환경광 최소) | `DayNightLighting` 인스펙터 |
| 수로 손실률·레벨별 용량 | JSON `prototype.canalLossPerWholeEdge`, `buildingLevels.canalEdge[].capacityPerHour` |
| 물 배분 규칙 자체(방출·취수 지점, 우선순위) | `Core/Water/WaterNetworkSolver.cs` — 바꾸면 `Tools/SettlementSim/WaterSolver.cs`(독립 구현)와 EditMode 기대값도 함께 |
| 물 배분 규칙 후보를 비교·실험 | `Tools/SettlementSim/WaterSolver.cs`의 옵션 + `Program.cs` 시나리오 |
| 경제 틱 순서·생산 공식 | `Core/Economy/Settlement.Tick.cs`의 `AdvanceHour` — v0.8 교차 검증이 깨지면 의도된 변경인지 확인 |
| 실제 공사 시간 계산 | `SettlementRules.JobHours` (작업 큐와 화면이 같은 값을 씀) |
| 철거 환급·철거 시간·광물 배율 · 지형별 건설 가능 시설 | `SettlementRules`(`DemolishRefundRatio`·`DemolishHoursRatio`·`OreMultiplier`) · `FacilityCatalog` |
| 시작 자금 · 경매 입찰자 수 | JSON `prototype.startingCash` · `auction.attendeesWithOneAuctionHall`·`attendeesMin`·`attendeesMax` |
| 입찰자 성향·혜택, 시세 폭, 긴급 감액, 개발 중 자산 비율, 파산 유예, 경매 주기 | JSON `auction` |
| 가동 숙성, 석재 배율의 채석장 능력 사용 | JSON `auctionSeed.maturityStartRatio`·`maturityFullHours`·`stoneMultiplierUsesQuarryCapacity` |
| 시장(판매 한도·가격 회복·상인 배율) | JSON `purchase`(시장 가격 계산은 `Settlement.cs` 아래쪽) |
| 영구 장비 목록·가격·효과 | JSON `equipment[]` (`effects`는 모디파이어와 같은 형식, 설명 `Core/Economy/EconomyEffects.cs`) |
| 현장 장비 슬롯 수 · 다음 맵 선택(고정 맵 몇 개 뒤 생성) | `Campaign.EquipmentSlots` · `Campaign.ChooseNextMap`, 절차 맵 JSON `fixedMapsBeforeGenerated` |
| 절차 맵 난이도 곡선 · 생성 규칙 확률·크기 | `procedural_map_config.json`의 `difficulty` · `generator` — 바꾼 뒤 `-- tune`. 이 JSON 값은 코드 기본값과 같아야 한다(`ProceduralMapTests.Config_JsonMatchesCodeDefaults`) |
| 생성 절차 자체(BSP·배치 순서·검사) | `ProceduralMapGenerator.cs` — 바꾸면 시뮬레이터 '기준 맵 서명'과 `ProceduralMapTests` 골든 값 갱신 |
| 지역 모디파이어 목록·수치·출현, 후보 지역 수 | `procedural_map_config.json`의 `modifiers`, `regionChoices` |
| 재해 빈도·유예·예고, 전염병 전파·회복·면역 이월 · 병원 병상 | JSON `disasters`(설명 `EconomyConfig.cs`의 `DisasterSettings`·`EpidemicSettings`) · `buildingLevels.hospital[].beds` |
| 맵 지형·샘플 배치, 맵 추가 | `Assets/Data/Maps/*.json` (새 파일은 `GameSession`의 Maps 목록에) |
| 예시 맵 그리기 · 생성 맵 눈으로 보기 | 메뉴 Tools > Desert Settlement > 맵 스케치 씬 열기 → `MapSketch` 인스펙터 |
| 경매 입찰자·시세 시드 | `GameSession` 인스펙터 → Campaign Seed |
| 지형·시설·수로 색, 수로 폭, 머리 위 글자, 녹지화, 등불 | `SettlementView` 인스펙터 (시설 기본 색은 `FacilityCatalog`, 종류별 덮어쓰기는 `facilityColors`) |
| 시설 모양(부품 위치·크기·레벨 부품·비계·등불 위치) | `Runtime/View/FacilityModels.cs` |
| 건설 가능 타일 강조 색·주기 · 맵 바깥 모래언덕 | `BuildableOverlay` · `DesertBackdrop` 인스펙터(GameSession 오브젝트) |
| 팝업·정보창 문구, 지형 이름, 재해 문구 | `Runtime/View/FacilityInfo.cs` (시설 이름은 `FacilityCatalog`) |
| UI 색·글자 크기·간격 | `Runtime/UI/UiKit.cs`의 `Theme` |
| 키보드 단축키 | `Runtime/UI/Hotkeys.cs`의 표(카메라 이동·회전은 `StrategyCameraRig`, 도구 해제 Esc는 `BuildInputController`) |
| UI 구역 위치·크기 | 위쪽 띠 `TopBarView`, 도구 막대 `BuildBarView`, 시장·예시 `BuyPanelView`, 알림 `AlertsView`, 정보창 `InspectorView`, 가운데 창(경매·행선지·상점) `ModalView`·`ModalContent`, 툴팁 `TooltipView` |
| 도구 목록·조작 방식 | `Runtime/BuildInputController.cs`(`BuildTool`, `Apply`) |
| 카메라 속도·각도·줌·회전 | `CameraRig` 오브젝트의 `StrategyCameraRig` 인스펙터 |
| 자금 0 예측 경고 | `Runtime/UI/AlertsView.cs`의 `CashRunsOutIn` |
| 최고 기록 | `Runtime/RecordBook.cs`, 파일 `Application.persistentDataPath/records.json` |
| 새 지형 종류 | `Core/Hex/HexMap.cs`의 `TerrainType`(끝에 추가 — 맵 JSON이 숫자로 저장) |
| 정각·자정·낮밤 전환 때 할 일 | `GameSession`에서 `Clock.HourPassed`/`DayStarted`/`PhaseChanged` 구독 |

## 6. 확인 방법

| 무엇 | 명령 | 기준 |
|---|---|---|
| Core 규칙·회귀값 | `dotnet run --project Tools/SettlementSim` | 마지막 줄 '모든 검증 통과', 종료 코드 0 |
| Unity 테스트 | `bash Tools/unity-tests.sh All` | 2026-10-30: EditMode 181 · PlayMode 10 통과(캡처 1개는 `[Explicit]`이라 건너뜀) |
| 새 EditMode 테스트 | 설정·맵은 `TestData`(경로·불러오기 한 곳), 설정에서 나오는 기대값은 설정에서 읽고, 일부러 고정하는 회귀값만 `V08Golden`에(결정 TD1·TD2) |
| 화면 모양(에디터 없이) | `bash Tools/unity-tests.sh PlayMode ScreenCaptureTests` | `.unity-testcopy/Captures/*.png` |
| 저장·불러오기 재실행 | `dotnet run --project Tools/SettlementSim -- savecheck` | |
| 직접 플레이 | `SampleScene` Play → 일시정지로 시작 → 왼쪽 아래 '샘플 배치' → 배속(` 1 2 3) | F5/F9 = 저장/불러오기(단축키 표는 `Runtime/UI/Hotkeys.cs`) |

**시뮬레이터 모드** (`dotnet run --project Tools/SettlementSim -- <모드>`, 결과는 `Tools/balance_output/`, git 무시)
- 계속 쓰는 것: (없음 = 회귀 검증) · `savecheck` · `tune [후보…]`(난이도 곡선) · `mods [D]`(D별 약점 하나씩·대응 장비 회복률) · `mods campaign …`(Core 캠페인 봇 측정)
- `mods campaign [정착지당 D 증가] [cashPerD] [정착지 수] [smart] [nodisaster] [vaccine] [upgrade] [upkeep=k] [halls=k] [attendees=a-b] [cash=x] [late=D upkeep2=k weak=s dis=s] [trade=off flimit= slimit= rec= market=0 sell=0]`
  — 난이도 기준은 `smart`(숙련 봇). `halls=k` 봇이 경매장 k개, `late=…`는 후반 가속 값 덮어쓰기(모두 0 = 가속 없음), `trade=off` 시장 규칙 없음, `market=0`·`sell=0` 봇이 시장을 안 짓거나 안 팖.
  기준 측정: `mods campaign 0.25 14000 30 smart`(결과 해석은 `Docs/handoff.md` 3절)
- 결정이 끝난 측정(다시 잴 일이 있을 때만, 머리 주석에 '결정 완료'): `exploit`, `funboost`, `bidders`. 전염병 후보 비교 `epidemic [맵 수]`·`epidemic sweep`(감염·회복 모형, 결정 EV7), 노동력 `labor`(진단·`sample`)와 `mods campaign … lab=후보`(결정 LB13). 진단 도구: `botdiag`(측정 봇 보완용). 예전 72h 경매를 전제한 `funcheck`·`upgrades`는 지웠다(결정 SM1)
- 여러 후보를 동시에 재려면 `dotnet build Tools/SettlementSim -c Release` 뒤 `dotnet Tools/SettlementSim/bin/Release/net10.0/SettlementSim.dll mods campaign …`을 병렬로. 콘솔 출력은 cp949라 Git Bash에서는 `iconv -f cp949 -t utf-8`
- 설정 값을 바꿔 비교 측정할 때는 JSON을 바꾼 뒤 **반드시 되돌린다**(`git diff Assets/Data/Config/`)
- 원본 v0.8 Python 스크립트는 지웠다(결정 SM2, git 기록에 있음). 같은 계산이 C# `EconomySim.cs`에 있고 회귀값도 거기서 검증한다

## 7. 알려진 함정

- **씬에 남은 옛 값:** 직렬화된 필드는 씬 값이 코드 기본값을 이긴다. 코드에서 기본값을 바꿨는데 화면이 그대로면 씬의 값을 의심한다(씬 재구성: `DebugSessionMenu.SetupSampleScene`, 에디터를 닫아야 하므로 먼저 묻는다).
- **UI 오브젝트 이름:** 코드로 만든 글자·버튼은 '종류 + 처음 문구'로 이름이 붙는다(예: `Button 저장 F5`, 결정 UK1). 플레이 중 Hierarchy 검색창에 화면 문구를 치면 찾을 수 있다. 처음에 비어 있다가 채워지는 글자는 이름이 'Text'로 남는다.
- **가운데 창·알림의 다시 그리기(결정 UK2):** `ModalView`는 캠페인 명령 기록이 늘거나 캠페인 상태·열린 경매가 바뀌거나 창 안 버튼이 `MarkDirty`를 부를 때 다시 그린다. 창에 새 버튼을 넣으면 그 동작이 명령으로 기록되지 않는 한(화면 안의 선택 등) `_changed()`를 부른다. `AlertsView`는 그릴 글자·색 자체를 비교하므로 따로 할 일이 없다.
- **시드 상수:** `Core/Progression/SeedSalts`의 숫자를 바꾸면 같은 시드로 다른 게임이 나와 저장 파일과 골든 값이 깨진다(결정 SD1).
- **반투명·발광 머티리얼**은 `WorldVisuals.Unlit`(`Sprites/Default`)로 만든다. URP Lit 반투명을 코드로 켜면 빌드에서 셰이더 변형이 빠질 수 있다.
- **배치 모드 Unity**는 에디터가 이 프로젝트를 열고 있으면 바로 실패한다. 테스트는 `unity-tests.sh`(사본)로, 빌드·씬 재구성만 원본에서.
