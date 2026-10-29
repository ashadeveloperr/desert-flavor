# CLAUDE.md

> **가장 먼저 지킬 규칙: 사용자에게 보이는 모든 글은 한국어로 쓴다.** 답변·진행 안내·도구 호출 사이의 짧은 문장·마무리 요약·질문과 선택지·버튼 문구까지 전부. 영어로 쓰지 않는다(코드·커밋 메시지·파일 이름은 예외).

사막 정착지 경영 시뮬레이션(Desert Settlement Simulator) — Unity 3D 헥스타일 기반 정착지 개발·생산망·경매 경영 게임 프로토타입.
헥스 맵·수로 물 배분·건설 큐·생산·식량·경매(입찰자 6종)·재해 4종·시장·캠페인(매각 → 행선지 → 장비 상점 → 다음 정착지, 2번째부터 자금 비례 난이도의 절차 생성 맵)까지 MVP 루프가 돈다.
그래픽은 기본 도형, UI는 코드로 만든 uGUI다.

**새 세션·다른 PC에서 이어 갈 때는 먼저 `Docs/handoff.md`를 읽는다**(준비 절차·현재 상태·다음 할 일). 남은 작업은 GitHub 이슈로 관리하고, 고정된 순서표 이슈 **#14**에 처리 순서와 플레이테스트 기록이 있다:
`gh issue view 14 -R ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr --comments`.

## 문서 지도 — 한 사실은 한 곳에

| 알고 싶은 것 | 단일 출처 |
|---|---|
| 처음 보는 사람용 개요·실행 방법 | `README.md` |
| 코드 구조, 한 시간의 흐름, X를 바꾸려면 어디를 고치나, 확인 방법, 함정 | `Docs/code_guide.md` |
| 지금 수치 | `Assets/Data/Config/economy_balance_v0.8_config.json`, `procedural_map_config.json` (+ 씬 인스펙터) |
| 무엇을 정했나(한 줄) → 왜 그렇게 정했나(근거·측정·다른 안) | `Docs/implementation_notes.md`(결정 표) → `Docs/decision_log_auto.md`(결정 번호로 찾기) |
| 할 일·진행·플레이테스트 기록 | GitHub 이슈(#14 순서표) |
| 지금 상태와 다음 할 일 | `Docs/handoff.md` |
| 기획 원본 | `Docs/desert_settlement_game_design_checkpoint_2026-10-20.md` > `economy_balance_v0.8.md` > `economy_food_storage_v0.6.md` > `desert_settlement_concept_plan_v4.html`(수로 설명은 오래됨). 이후 바뀐 것은 결정 표가 우선 |
| 끝난 작업의 기록(고치지 않음) | `Docs/archive/` |

수로는 육각형 **변(Edge)**, 분기점은 **꼭짓점(Vertex)** 기준이다.

## 유지보수 원칙 (2026-10-29, 모든 변경에 적용)

목표: **사용자가 Claude 없이도 코드를 읽고 고칠 수 있는 상태를 유지한다.** 기능을 더할 때마다 이해하기 어려운 곳이 늘지 않게 한다.

**한 사실은 한 곳에**
- 수치는 설정 JSON(또는 인스펙터)에만 둔다. 문서·주석에는 값을 베끼지 않고 키 이름을 적는다(예: "경매 주기는 `auction.auctionRepeatsGameHours`"). 값을 꼭 적어야 하면 "(지금 48)"처럼 바뀔 수 있음을 드러낸다.
- 같은 계산을 두 곳에 쓰지 않는다. Runtime은 Core의 조회 메서드(`OutputOf`, `JobHours`, `Can*`)를 부르고, Core 안에서도 같은 공식은 도우미 하나로 모은다.
- 같은 내용을 여러 문서에 쓰지 않는다. 위 문서 지도의 단일 출처에 쓰고, 다른 곳에서는 링크만 한다.

**주석은 지금의 코드를 설명한다**
- 주석에는 **무엇을 하는지와 왜 그런지**만 쓴다. 날짜·이슈 번호·"원래 X였다" 같은 연혁은 커밋 메시지와 `decision_log_auto.md`에 둔다. 결정을 가리킬 때는 "(결정 AU1)"처럼 짧게.
- 코드를 바꾸면 그 코드를 설명하는 주석(클래스 머리 주석의 순서·목록 포함)도 같은 커밋에서 고친다.
- 공개 타입·메서드에는 한국어 요약 주석을 단다.

**구조**
- 규칙은 Core(순수 C#), 표시·입력은 Runtime, 조정할 숫자는 설정 JSON·인스펙터.
- 파일이 약 300줄, 메서드가 약 60줄을 넘으면 나눌지 먼저 검토한다(목적별 partial 파일, 이름 있는 도우미 메서드).
- 확장 지점은 한 곳에 모은다(예: 시설 종류는 `FacilityCatalog` 한 목록). 빠뜨리면 **오류 없이 조용히 틀어지는 곳**이 생기면 테스트로 막거나, 막을 수 없으면 `code_guide.md` 4절 체크리스트에 ⚠로 적는다.
- 화면은 정식 UI(`Runtime/UI/`) 한 벌만 둔다. 같은 기능의 화면·입력 처리를 두 곳에 만들지 않는다(예전 IMGUI 화면은 결정 HK2로 삭제).
- 시뮬레이터의 일회성 측정 모드는 결정이 끝나면 머리 주석에 "결정 완료(결정 번호)"를 적거나 지운다.
- 큰 구조 변경(동작이 바뀔 수 있는 정리, 파일 대량 이동)은 이슈로 만들어 사용자와 순서를 정한 뒤 한다.

**변경을 마칠 때 체크리스트**(커밋 전)
1. `dotnet run --project Tools/SettlementSim`과 `bash Tools/unity-tests.sh All` 통과.
2. 정한 것이 있으면 결정 표에 한 줄 + `decision_log_auto.md`에 근거.
3. 파일 추가·이동, 흐름·확장 지점 변경이 있으면 `Docs/code_guide.md`를 같은 커밋에서 고친다.
4. 새 스크립트의 `.meta`, 시뮬레이터가 링크하는 폴더면 `Tools/SettlementSim/SettlementSim.csproj`.
5. 손댄 파일에서 본 낡은 주석·연혁 주석은 그 자리에서 정리한다(손대지 않은 파일은 일부러 고치지 않는다).
6. `Docs/handoff.md`에는 지금 상태와 다음 할 일만 남긴다(지난 기록은 이슈 댓글·결정 기록으로).

## 작업 방식 (사용자 선호)

- [미정] 항목은 사용자 결정 없이 확정하지 않는다. 정할 것은 하나씩 설명하고 추천안을 첫 번째로 두고 묻는다.
- 이슈 하나씩 처리한다: 이슈 읽기 → [질문]을 하나씩 → 구현 → 시뮬레이터·Unity 테스트 → 결정 표·결정 기록 갱신 → 결과 댓글 초안 → 사용자가 요청하면 커밋·푸시·댓글·닫기·#14 체크.
- 커밋·푸시는 요청받았을 때만, 푸시는 origin(개인 포크)만. upstream(`KooHoo-Dev`)에는 푸시하지 않는다.
- 밸런스는 바로 고치지 않고 시뮬레이터로 재현·비교해 후보를 표로 보여 준 뒤 고르게 한다.
- 플레이테스트 결과(확인·문제·메모)는 **반드시 기록**하고(체크리스트 웹 페이지 → #14나 이슈), 다음 체크리스트에는 **고친 부분·새 기능만** 넣는다.
- Unity 테스트는 `bash Tools/unity-tests.sh`(테스트 전용 사본)로 돌린다. 사용자가 연 에디터를 쓰거나 닫아 달라고 하지 않는다. 원본에 결과를 남겨야 하는 배치 작업(빌드, `-executeMethod`)만 예외이고 그때는 먼저 묻는다.
- 사용자가 맵 스케치(`Assets/Data/MapSketches/*.json`)로 예시를 주면 JSON을 읽어 정확한 좌표·지형을 확인한다.

## 환경

- **Unity 6000.3.15f1** (`ProjectSettings/ProjectVersion.txt`). 같은 PC에 6000.3.9f1이 있을 수 있으니 혼동 주의(다른 버전으로 열면 패키지가 업그레이드된다).
- URP 17.3.0, Input System 1.19.0(**새 Input System 전용**), Cinemachine 3.1.7, Test Framework 1.6.0, Newtonsoft Json 3.2.2, UniTask(Git URL — 첫 실행 때 git이 PATH에 있어야 함).
- .NET 10 SDK(시뮬레이터), GitHub CLI `gh`(이슈). Python은 필요 없다.
- Windows. 셸은 PowerShell(기본)과 Git Bash. Git 무시: `Library/ Temp/ Logs/ UserSettings/ Builds/`, `.unity-testcopy/`, `Tools/balance_output/`, 루트 `*.csproj`·`*.sln`.

## 명령어

```bash
dotnet run --project Tools/SettlementSim            # Core 규칙 회귀 검증. 마지막 줄 '모든 검증 통과'
bash Tools/unity-tests.sh All                       # EditMode·PlayMode (EditMode|PlayMode|All, 두 번째 인자 = 테스트 이름 필터)
bash Tools/unity-tests.sh PlayMode ScreenCaptureTests   # 화면 캡처 → .unity-testcopy/Captures/*.png
```

- `unity-tests.sh`는 작업 트리를 `.unity-testcopy/`에 미러 복사하고 그 사본에서 배치 모드로 돈다. 종료 코드 0 = 통과, 1 = 실패, 4 = 동기화 실패. 처음 한 번은 임포트로 몇 분. 에디터에서 저장 안 한 변경은 들어가지 않는다. 다른 위치의 Unity는 `UNITY_EXE` 환경 변수.
- 시뮬레이터 측정 모드(`tune`, `mods`, `savecheck` …)와 병렬 측정 방법은 `Docs/code_guide.md` 6절.

원본 프로젝트에서 직접 도는 배치 작업(**에디터가 이 프로젝트를 열고 있으면 바로 실패**하므로 사용자에게 먼저 묻는다). PowerShell, 저장소 루트에서:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe"
$Proj  = (Resolve-Path .).Path
& $Unity -batchmode -nographics -quit -projectPath $Proj -logFile "$Proj\Logs\compile.log"            # 컴파일 확인
& $Unity -batchmode -nographics -quit -projectPath $Proj -executeMethod DesertSettlement.EditorTools.DebugSessionMenu.SetupSampleScene   # 씬 재구성
& $Unity -batchmode -nographics -quit -projectPath $Proj -buildTarget Win64 -buildWindows64Player "$Proj\Builds\Win64\DesertSettlement.exe" -logFile "$Proj\Logs\build.log"
```

- `-runTests`를 직접 쓸 때는 `-quit`을 붙이지 않는다. PowerShell `Start-Process -Wait`는 Unity 자식 프로세스까지 기다려 멈출 수 있으니 `-PassThru` 후 `$p.WaitForExit()`.

<!-- 이 아래는 사용자가 직접 추가 -->
