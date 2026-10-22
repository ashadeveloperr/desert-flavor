# CLAUDE.md

사막 정착지 경영 시뮬레이션(Desert Settlement Simulator) — Unity 3D 헥스타일 기반 정착지 개발·생산망·경매 경영 게임 프로토타입.
현재 기반 코드(헥스 그리드·수로 그래프·GameClock·설정 로딩)까지 구현됐다. 물 배분·건설·생산·경매는 아직 없다.
구현 결정 기록과 수정 가이드: `Docs/implementation_notes.md`.

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
- **`Core/Canal/CanalGraph`:** 변 위 수로 설치·제거·레벨·수문, 꼭짓점 간 경로 길이(변 개수, 닫힌 수문 차단), 연결 요소, 순환 감지. 물 배분은 아직 없음.
- **`Core/Timing/GameClock`:** 1배속 하루 90초, 정착지마다 1일차 12:00 시작, 정각/자정/낮밤 전환 이벤트(누락 없이 순서대로). 설정은 `GameTimeSettings`.
- **`Core/Config`:** `EconomyConfigLoader.Parse(json)` → `EconomyConfig`(Newtonsoft). 스키마·필수 필드 검증.
- **`Runtime/GameSession`:** 진입점. 설정 로드 → 맵·수로망·시계 생성 → `Time.unscaledDeltaTime`으로 시계 진행.
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

**테스트 실행** (`-runTests`에는 `-quit`을 붙이지 않는다. 현재 PlayMode 테스트는 없음):
```powershell
& $Unity -batchmode -nographics -projectPath $Proj -runTests -testPlatform EditMode `
  -testResults "$Proj\Logs\editmode-results.xml" -logFile "$Proj\Logs\editmode.log"
& $Unity -batchmode -projectPath $Proj -runTests -testPlatform PlayMode `
  -testResults "$Proj\Logs\playmode-results.xml" -logFile "$Proj\Logs\playmode.log"
```

- 종료 코드 0 = 성공. 실패 시 `-logFile`에 지정한 로그를 확인한다.
- Unity 에디터가 이 프로젝트를 열고 있으면 batch mode가 실패한다(프로젝트 잠금). 먼저 에디터를 닫는다.

**경제 시뮬레이터:**
```powershell
python Tools/desert_balance_sim_v08.py
```

## 규칙

<!-- TODO: 사용자가 직접 작성 -->
