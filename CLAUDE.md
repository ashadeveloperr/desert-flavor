# CLAUDE.md

사막 정착지 경영 시뮬레이션(Desert Settlement Simulator) — Unity 3D 헥스타일 기반 정착지 개발·생산망·경매 경영 게임 프로토타입.
현재 Core에 헥스 그리드·수로 그래프·GameClock·설정 로딩·물 배분·정착지 경제(건설 큐·생산·식량·유지비·가치 평가)까지 구현됐고,
`SampleScene`에서 임시 그래픽·IMGUI 건설 UI·탑다운 카메라로 플레이할 수 있다.
경매(입찰자 예고·당일 시세·산업가/농장주·긴급 매각·파산)와 캠페인(매각 → 장비 상점 → 다음 정착지: 첫 정착지는 고정 맵 1, 이후 보유 자금 비례 난이도의 BSP 절차 생성 맵)까지 MVP 루프가 돈다.
구현 결정 기록과 수정 가이드: `Docs/implementation_notes.md`. 진행 중 작업 목록: `Docs/work_items_2026-10-23.md`.

## 환경

- **Unity 버전:** 6000.3.15f1 (revision c1aa84e375f6) — `ProjectSettings/ProjectVersion.txt`
- **Unity.exe:** `C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe`
  - 같은 PC에 6000.3.9f1도 설치돼 있으니 혼동 주의. 다른 버전으로 열면 패키지 재해석/업그레이드가 발생한다.
- **렌더 파이프라인:** URP 17.3.0 (PC / Mobile RP Asset 분리, `Assets/Settings`)
- **주요 패키지:** Input System 1.19.0, Cinemachine 3.1.7, AI Navigation 2.0.12, ProBuilder 6.0.9,
  Test Framework 1.6.0, Newtonsoft Json 3.2.2, UniTask(Git URL), Timeline, uGUI, Visual Scripting
- **플랫폼:** Windows 11. 셸은 PowerShell(기본)과 Git Bash.

## 폴더 구조

```
Assets/
  Scenes/SampleScene.unity        # 기본 빈 씬 (빌드 설정에 등록된 유일한 씬)
  Settings/                       # URP 에셋·렌더러·볼륨 프로파일
  InputSystem_Actions.inputactions
  Data/Config/economy_balance_v0.8_config.json   # 경제 수치 원본 (TextAsset)
  Data/Config/procedural_map_config.json        # 절차 맵 난이도 곡선·생성 규칙 수치 (TextAsset)
  Scripts/
    Core/     # DesertSettlement.Core — 순수 C# 규칙 (noEngineReferences). Hex/ Canal/ Timing/ Config/
    Runtime/  # DesertSettlement.Runtime — MonoBehaviour (GameSession, 디버그 뷰, 낮/밤 조명, HUD)
    Editor/   # DesertSettlement.EditorTools — 에디터 메뉴
  Tests/EditMode/                 # DesertSettlement.Core.Tests (NUnit)
Packages/                         # manifest.json, packages-lock.json
ProjectSettings/
Docs/                             # 기획 문서 (Assets 밖에 두어 Unity 임포트 제외)
Tools/
  desert_balance_sim_v08.py       # 경제 밸런스 시뮬레이터 (Python 표준 라이브러리만)
  balance_output/                 # 시뮬레이터 결과 (git 무시)
```

Git 무시: `Library/ Temp/ Obj/ Build/ Builds/ Logs/ UserSettings/`, `Tools/balance_output/`.

## 주요 스크립트 / 문서

- **`Core/Hex`:** Pointy-top axial `HexCoord`, `HexLayout`(월드 좌표), `HexMap`(반경 3~7), 공유 꼭짓점·변의 정규화 키
  `VertexId`(타일의 N/S 꼭짓점) / `EdgeId`(타일의 E/SE/SW 변), `HexTopology`(타일↔변↔꼭짓점 조회, 경계 포함).
- **`Core/Canal/CanalGraph`:** 변 위 수로 설치·제거·레벨·수문, 꼭짓점 간 경로 길이(변 개수, 닫힌 수문 차단), 연결 요소, 순환 감지.
- **`Core/Water/WaterNetworkSolver`:** 확정 규칙 W1~W6(`Docs/implementation_notes.md`)의 물 배분. 우물 타일 꼭짓점 방출 → 우물별 최단 경로 트리
  → 시설은 타일 꼭짓점 중 수로가 닿은 점에서 여러 경로·여러 우물로 취수 → 반쪽 변 거리 오름차순(상류 우선)·동순위 수요 비례 progressive filling,
  변 입구 용량·우물 생산량 제약. 수치는 `WaterNetworkSettings.FromConfig`. `Tools/SettlementSim`의 프로토타입과 교차 검증된다.
- **`Core/Timing/GameClock`:** 1배속 하루 90초, 정착지마다 1일차 12:00 시작, 낮 05:00~밤 20:00, 정각/자정/낮밤 전환 이벤트(누락 없이 순서대로). 설정은 `GameTimeSettings`(조명 `DayNightLighting`도 이 값을 따름).
- **`Core/Config`:** `EconomyConfigLoader.Parse(json)` → `EconomyConfig`(Newtonsoft). 스키마·필수 필드 검증.
- **`Core/Economy/Settlement`:** 정착지 상태(자금·식량·석재·F·시설·작업)와 플레이어 명령(건설·수로·수문·개량·철거·취소·순서 변경·폐쇄·구매),
  `AdvanceHour()` 경제 틱(v0.8 209행 순서, FIFO 2슬롯). 규칙 값은 `SettlementRules.FromConfig`, 가치는 `SettlementValuation.Evaluate`.
  `OutputOf(facility)`로 시간당 생산·소비 예상치. 경매 가치는 가동 숙성(막 지은 생산 시설 70% → 48h 100%)과 석재 배율의 채석장 능력을 반영(2026-10-23, v0.8 회귀 비교 때는 끔). 회귀값(v0.8 스크립트 + 꼭짓점 취수 + 배율 폐기분 포함 + 광물 가치 계수 2.5:
  첫 경매 133h·현금 540.05·V 1149.63·보류 전략 1959.54)을 테스트와 `Tools/SettlementSim` 교차 검증으로 고정.
  무료 완공 배치(`SettlementRules.FreeBuilds`, 농장주 혜택)는 `RequestBuild`가 대기열 없이 바로 운영 시설을 만든다. 원본 v0.8 값과 다른 이유는 `Docs/implementation_notes.md` 참고.
- **`Core/Economy/Disasters`:** `DisasterManager`(캠페인이 정착지마다 붙임) — 재해 일정(시드 결정론, 48h 유예, 0~2번, 6h 예고), 전염병 심각도·저항력, 병원 반경 1칸 피해 절반, 방역 장비(시작 저항력). 수치는 설정 `disasters`.
- **`Core/Auction/AuctionManager`:** 정착지 정각 정산 직후 판정. 경매장 완공 +72h 첫 경매·이후 72h(설정 `auctionRepeatsGameHours`), 완공·보류 직후 입찰자 예고,
  자금 ≤ 0이면 긴급 매각(경매장·광산이 모두 있을 때) 또는 즉시 파산, 음수 24h 지속 시 파산.
- 배치 모드 CLI는 **에디터가 이 프로젝트를 열고 있으면 바로 종료 코드 1로 끝난다**(`Temp/UnityLockfile`, Unity는 한 프로젝트를 한 프로세스만 연다). 테스트는 **`bash Tools/unity-tests.sh [EditMode|PlayMode|All]`**로 돌린다: 테스트 전용 사본(`.unity-testcopy/`, git 무시)에 작업 트리를 미러 복사하고 그 사본에서 배치 모드로 실행한다. **사용자가 연 에디터에는 손대지 않는다**(열린 에디터에 테스트를 시키는 방식은 작업을 방해해서 2026-10-23 폐기). 에디터를 닫아 달라고 하지 않는다. 가치는 `SettlementValuation.EvaluateFor`(입찰자별·개발 중 자산).
- **`Core/Progression/Campaign`:** 정착지 연속. 매각 대금 100%, 식량·석재 미이월, 장비(우물 ×1.2) 유지, 다음 맵은 `ChooseNextMap`.
  절차 맵 설정이 있으면 처음 `fixedMapsBeforeGenerated`(1)개 뒤로는 들어가는 순간의 자금으로 D를 정해 생성(`PreviewNextDifficulty`로 상점에 표시).
  상점에서 다음 지역 후보 2곳(`NextOffers`, 약점·강점 `RegionModifiers`) 중 고르고, 현장 장비 슬롯 5개(`Loadout`·`SetEquipped`)에 넣은 장비만 효과.
  장비·모디파이어 효과는 같은 `EconomyEffects` 형식이고 `SettlementRules.ApplyEffects`로 걸린다.
- **`Core/Progression/ProceduralMapGenerator`:** BSP 절차 맵(채택 이유는 클래스 주석), `DifficultyParams.For(d, curve)`·`FromCash`. 수치는 `ProceduralMapConfig`(JSON).
  난수는 `DeterministicRandom`(Unity·.NET 같은 수열) — 생성 절차를 바꾸면 시뮬레이터 '기준 맵 서명'과 `ProceduralMapTests` 골든 값을 함께 갱신.
  맵 파일은 `Assets/Data/Maps/*.json`(`MapLoader`).
- **`Runtime/GameSession`:** 진입점. 설정 로드 → 맵(인스펙터 지형 목록) → `Settlement` → 시계, 정각마다 `AdvanceHour()`.
- **`Runtime` 표시·조작:** `SettlementView`(기본 도형 그래픽), `BuildInputController`(Input System 마우스 도구), `SettlementHud`(IMGUI),
  `StrategyCameraRig`(Cinemachine 3). 전체 구성은 `DebugSessionBuilder.Build`로 만든다. 프로젝트는 **새 Input System 전용**.
- **맵 스케치 도구:** `Assets/Scenes/MapSketch.unity` + `Runtime/MapSketch`(데이터·Gizmo 표시) + `Editor/MapSketchEditor`(씬 뷰 칠하기, JSON 저장·불러오기, 생성 맵 미리보기).
  사용자가 절차 생성 규칙을 설명할 예시 맵을 그리는 용도. 저장 파일은 `Assets/Data/MapSketches/*.json`(캠페인 맵과 같은 `MapDefinition` 형식, `MapLoader.ToJson`).
  예시를 받으면 JSON을 읽어 정확한 좌표·지형을 확인한다.
- 씬 재구성: `Unity.exe -batchmode -nographics -quit -projectPath $Proj -executeMethod DesertSettlement.EditorTools.DebugSessionMenu.SetupSampleScene`
- **`Tools/desert_balance_sim_v08.py`:** v0.8 경제의 결정론적 시간당 시뮬레이션. 우물 1 + 수로 변 3개의 직렬 취수 그래프로
  시설 레벨표(`LEVELS`), 건설/개량 작업 큐, 식량 계수, 경매 가치를 계산하고 시나리오 테스트(`do_tests`)와
  민감도 분석(`sensitivity`)을 수행한다. 일반 HexTile/CanalVertex 네트워크 솔버가 **아니다** — Unity 솔버는 분기·공유 변을 별도로 테스트해야 한다.
- **문서 우선순위:** `Docs/desert_settlement_game_design_checkpoint_2026-10-20.md`(통합 기획서, 현재 기준)
  > `Docs/economy_balance_v0.8.md` + `economy_balance_v0.8_config.json`(구현용 수치)
  > `Docs/economy_food_storage_v0.6.md` > `Docs/desert_settlement_concept_plan_v4.html`(구 컨셉, 수로 설명은 오래됨).
  수로는 육각형 **변(Edge)**, 분기점은 **꼭짓점(Vertex)** 기준.

## 명령어

PowerShell 기준. `$Unity`와 `$Proj`는 먼저 설정한다.

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe"
$Proj  = "C:\Users\vr9chae\Documents\3rd_3D_1-3D-Project-ashadeveloperr"
```

**컴파일 확인 (스크립트 에러 체크):**
```powershell
& $Unity -batchmode -nographics -quit -projectPath $Proj -logFile "$Proj\Logs\compile.log"
```

**Windows 64bit 플레이어 빌드** (빌드 설정의 씬 사용, 커스텀 빌드 스크립트 불필요):
```powershell
& $Unity -batchmode -nographics -quit -projectPath $Proj -buildTarget Win64 `
  -buildWindows64Player "$Proj\Builds\Win64\DesertSettlement.exe" -logFile "$Proj\Logs\build.log"
```

**테스트 실행(권장):** `bash Tools/unity-tests.sh All` — 사본 동기화(robocopy /MIR: Assets·Packages·ProjectSettings) 후 사본에서 아래 CLI를 실행. 종료 코드 0 = 통과, 1 = 실패, 4 = 동기화 실패.
처음 한 번은 사본 Library 임포트로 몇 분 걸린다. 결과·로그는 `.unity-testcopy/Logs/`. 에디터에서 저장 안 한 변경은 사본에 들어가지 않는다.

**테스트 실행** (`-runTests`에는 `-quit`을 붙이지 않는다. PowerShell에서 `Start-Process -Wait`는 Unity 자식 프로세스까지 기다려 멈출 수 있으니 `-PassThru` 후 `$p.WaitForExit()`를 쓴다):
```powershell
& $Unity -batchmode -nographics -projectPath $Proj -runTests -testPlatform EditMode `
  -testResults "$Proj\Logs\editmode-results.xml" -logFile "$Proj\Logs\editmode.log"
& $Unity -batchmode -projectPath $Proj -runTests -testPlatform PlayMode `
  -testResults "$Proj\Logs\playmode-results.xml" -logFile "$Proj\Logs\playmode.log"
```

- 종료 코드 0 = 성공. 실패 시 `-logFile`에 지정한 로그를 확인한다.
- Unity 에디터가 이 프로젝트를 열고 있으면 batch mode가 실패한다(프로젝트 잠금). 테스트는 위 `unity-tests.sh`(사본)로 피한다. 빌드·씬 구성(-executeMethod)은 원본 프로젝트에 결과를 남겨야 하므로 여전히 에디터를 닫아야 한다(사용자에게 먼저 묻는다).

**경제 시뮬레이터:**
```powershell
python Tools/desert_balance_sim_v08.py
```
- 이 PC에는 Python이 설치돼 있지 않다(`python`은 Microsoft Store 별칭). 대신 아래 C# 시뮬레이터를 쓴다.

**게임 로직 시뮬레이터 (C#, .NET 10 SDK):** Core의 Hex/Canal 소스를 링크하고, 범용 물 배분기 프로토타입과
v0.8 경제 시뮬레이터 이식본으로 규칙 후보를 비교한다. 원본 v0.8 회귀값을 함께 검증한다.
```powershell
dotnet run --project Tools/SettlementSim
```
- 결과: 콘솔 + `Tools/balance_output/settlement_sim_report.txt` (git 무시). 실패가 있으면 종료 코드 1.
- 절차 맵: 생성기는 Core 파일을 링크하고 수치는 `procedural_map_config.json`을 읽는다. `ReferenceBot.cs`(난이도 측정용 기준 플레이: `Single` 우물·광맥 하나, `Multi` 모든 광맥 개발).
  난이도 곡선 후보 비교는 `dotnet run --project Tools/SettlementSim -- tune [후보 이름…]`(`DifficultyTuning.cs`, 결과 `Tools/balance_output/difficulty_tuning.txt`).
  모디파이어·장비 측정: `-- mods [D]`, `-- mods campaign`(`ModifierTuning.cs`). 몰아짓기·채석장 철거 재현과 대책 비교: `-- exploit [맵 수]`(`ExploitTuning.cs`). 전염병 측정: `-- epidemic [맵 수]`(`EpidemicTuning.cs`).
  난이도별 예시 맵은 `Tools/balance_output/generated_maps/*.json`(맵 스케치 씬에서 'JSON 불러오기', 또는 인스펙터 '생성 맵 미리보기'로 바로 생성).

## 규칙

<!-- TODO: 사용자가 직접 작성 -->
