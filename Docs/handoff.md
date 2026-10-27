# 이어서 작업하기 (다른 PC·새 Claude 세션용 인계)

마지막 정리: 2026-10-27(회사 PC, #5·#6·#7·#13·#12까지) · 최신 커밋은 `git log -1`로 확인 (origin = 개인 포크 `ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr`, main)

> **2026-10-26부터 남은 작업은 GitHub 이슈로 관리한다.** 먼저 고정된 순서표 이슈 **#14**를 읽는다:
> `gh issue view 14 -R ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr --comments` (또는 https://github.com/ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr/issues/14)
> #14에 처리 순서(#1~#13, 끝난 것은 체크 + 정한 내용 한 줄), 작업 방식, 2차 플레이테스트 기록, **3차 플레이테스트에서 확인할 목록(댓글)**이 있다. 이슈는 git이 아니라 GitHub에 저장되므로 `git pull` 없이도 보인다.

새 Claude 세션은 이전 대화를 기억하지 못한다. 이 문서 + `CLAUDE.md` + **이슈 #14** + `Docs/implementation_notes.md`(결정 표) + `Docs/decision_log_auto.md`(결정 근거·측정)
+ `Docs/roadmap_2026-10-23.md`(초기 기획 완성까지 남은 작업과 순서)를 읽으면 이어 갈 수 있다.
새 세션을 시작할 때 이렇게 말하면 된다: **"Docs/handoff.md와 이슈 #14를 읽고 이어서 작업하자."**

---

## 1. 다른 PC 준비 (학원·회사·집, 처음 한 번)

1. **저장소 받기**
   ```bash
   git clone https://github.com/ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr.git
   ```
   이미 받아 둔 폴더가 있으면 `git pull origin main`.
   upstream(`KooHoo-Dev/...`)에는 **푸시하지 않는다**(README 경고).
2. **Unity 6000.3.15f1** (Unity Hub로 설치, 다른 버전으로 열면 패키지가 업그레이드되니 주의).
   기본 경로 `C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe`. 다른 곳에 깔았으면 테스트 스크립트에 `UNITY_EXE` 환경 변수로 알려 준다.
   UniTask가 Git URL 패키지라 첫 실행 때 **git이 PATH에 있어야** 한다.
3. **.NET 10 SDK** — 게임 로직 시뮬레이터(`Tools/SettlementSim`)용. Python은 필요 없다(회사 PC엔 없음, 집 PC엔 있어서 문서 편집 보조로만 썼다).
4. **GitHub CLI(`gh`) 로그인** — 이슈 읽기·댓글·닫기에 필요. `gh auth status`로 확인하고, 안 돼 있으면 `gh auth login`.
   Claude가 이슈에 쓰는 일(댓글·닫기·#14 편집)은 자동 권한 검사에 막힐 수 있어서, 사용자가 채팅으로 "커밋·푸시하고 이슈에 댓글 달고 닫아 줘"처럼 **직접 요청한 뒤에** 한다(2026-10-26 흐름).
5. **경로 확인:** `CLAUDE.md` 명령어 절의 `$Proj` 경로는 회사 PC 기준(`C:\Users\vr9chae\...`)이다(집 PC는 `C:\Users\liuye\...`). 다른 경로면 그 줄만 바꿔 쓴다.
   `Tools/unity-tests.sh`·시뮬레이터는 저장소 위치를 스스로 찾으므로 경로를 고칠 필요가 없다.
6. **확인:**
   ```bash
   dotnet run --project Tools/SettlementSim
   bash Tools/unity-tests.sh All
   ```
   시뮬레이터 마지막 줄 '모든 검증 통과', 테스트는 **EditMode 169 · PlayMode 7 통과**가 기준(2026-10-27, 화면 캡처 `ScreenCaptureTests`는 [Explicit]이라 따로 실행).
   `unity-tests.sh` 첫 실행은 테스트 사본(`.unity-testcopy/`) 임포트로 몇 분 걸린다.

## 2. 작업 방식 (사용자 선호)

`CLAUDE.md`의 '작업 방식' 절에도 같은 내용이 있다(새 세션이 자동으로 읽음).
- **모든 답변은 한국어**(짧은 답·진행 안내·마무리 요약·버튼까지).
- **사용자가 직접 고칠 수 있는 구조:** 규칙은 Core(순수 C#), 표시는 Runtime. 조정할 숫자는 설정 JSON이나 인스펙터로. 공개 타입에 한국어 주석. 새 시스템마다 `Docs/implementation_notes.md` 결정 표·수정 가이드 갱신.
- **[미정] 항목은 사용자 결정 없이 확정하지 않는다.** 정할 것이 있으면 하나씩 설명하고 추천안을 첫 번째에 두고 묻는다.
- **커밋·푸시는 요청받았을 때만.** 푸시는 origin(개인 포크)만.
- **Unity 테스트는 사용자 에디터를 건드리지 않는다:** `bash Tools/unity-tests.sh`(테스트 전용 사본)로 돌리고, 에디터를 닫아 달라고 하지 않는다.
  원본에 결과를 남겨야 하는 배치 작업(빌드, `-executeMethod SetupSampleScene`)만 예외이고 그때는 먼저 묻는다.
- 밸런스는 바로 고치기보다 **시뮬레이터로 재현·비교한 뒤** 제안한다(후보를 표로 보여 주고 고르게).
- **플레이테스트 결과는 반드시 기록**하고(체크리스트 웹 페이지의 확인·문제·메모 → 순서표 이슈나 Docs), 다음 테스트 체크리스트에는 **고친 부분·새 기능만** 넣어 이미 확인한 것을 다시 묻지 않는다.
- **이슈 하나씩 처리한다.** 흐름(2026-10-26에 굳어짐):
  이슈 읽기 → [질문]을 하나씩(추천안 첫 번째, 측정이 필요하면 먼저 시뮬레이터로 재서 표로) → 구현·테스트(시뮬레이터 + `unity-tests.sh All`) → 결정 표·결정 기록 갱신 → 결과 댓글 초안 →
  사용자가 "커밋·푸시하고 이슈에 댓글 달고 닫고 #14 체크해 줘"라고 요청 → 실행.

## 3. 지금 상태 요약 (2026-10-27 마감)

**순서표 #14의 #1~#13이 모두 끝났다.** 1단계 버그(#1~#4)·2단계 규칙(#8~#11)은 2026-10-26, 3단계 UI·연출(#5·#6·#7·#13)·4단계 밸런스(#12)는 2026-10-27.
이슈마다 결과 댓글을 달고 닫았고, #14에 체크와 정한 내용 한 줄을 붙였다. 다음 순서는 **3차 플레이테스트**다.

**들어간 것**(근거·측정·다른 안은 `Docs/decision_log_auto.md`에서 결정 번호로 찾기, 결정 표는 `Docs/implementation_notes.md`)
| 이슈 | 내용 | 결정 번호 | 커밋 |
|---|---|---|---|
| #1 | 전염병 곡선 재설계: R ≥ 0.7×S이면 감소, 시작 S 10%·상승 1.5%p/h → 정점 31h·35%·50h에 끝. 방역 장비 ×1.5 → ×1.3. 알림에 ↑ 확산 중 / ↓ 약해지는 중 | EP1~EP3 | e3d0d78 |
| #2 | 우물 옆 시설의 거리 0 취수를 그 꼭짓점을 이어 준 수로에 '직접 취수'로 표시(용량은 안 씀, 계산 그대로). 우물 정보창은 경로마다 시설·칸 수·양 | WD1·WD2 | 871dc27 |
| #3 | 막힌 작업 뒤의 **철거만** 건너뛰어 착공(건설·개량은 FIFO 그대로) | JQ1 | 149fbbe |
| #4 | UI 글자 흐림: 캔버스 `pixelPerfect`(화면 캡처로 확인). `UiKit.Clear`가 옛 내용을 창 크기 계산에 섞던 버그 | TX1·TX2 | 994bc9b |
| #8 | 명령 순간 착공, 진행 중인 시간을 첫 1시간으로(12:30 + 5h → 17:00). 설정 `startJobsOnCommand`(v0.8 회귀 비교는 끔) | JQ2 | 88206ac |
| #9 | 경매 주기 72 → **48h**, 재해 유예 48 → 36h·창 72 → 48h(재해 정착지 47% 유지), 가동 숙성 48h 그대로 | AU1~AU3 | 85e1aff |
| #10 | 입찰자 수 = **경매장 1·2·3개에 2·3·4명**(최대 4). 경매장 추가 완공 → 예고 +1명, 닫으면 뒤에서부터 빠짐. 일정은 첫 경매장 기준 | AH1~AH4 | 0662a71 |
| #11 | 창고 보너스 L1 +150·L2 +300, 물탱크 360·630(×1.5), 충전·방출 속도 그대로 | ST1 | a4180a6 |
| #5 | 위쪽 띠 `시계·배속 │ 저장·불러오기`, 구매·샘플 배치는 **왼쪽 아래 '구매·예시' 패널**(`BuyPanelView`), 좁은 화면은 위쪽 띠·아래 줄 **통째 축소**(16:9·16:10은 그대로), 자원 칸 넘침 수정, 캡처에 16:10·5:4 | LY1~LY5 | b459b9c |
| #6 | **매각 → 행선지 창(후보 카드 클릭) → 상점 → 출발**, 카드에 운영비 ×(`Campaign.PreviewUpkeepMultiplier`), 가운데 창은 화면 크기가 바뀌면 높이 다시 잼 | DS1~DS6 | 6a17fbb |
| #7 | 시설 건설 도구일 때 **지을 수 있는 타일 초록 맥동**(2초, 투명도 15~45%), 석재 부족이면 노랑(`Runtime/View/BuildableOverlay`) | BO1~BO4 | 698548e |
| #13 | 밤 **달빛**(같은 방향광, 푸른색 0.35)·환경광 최소 0.5, 시설 **등불 + 점광원**, 폐쇄된 시설만 꺼짐, 19~20시 켜짐·4~5시 꺼짐 | MN1·MN2·LT1~LT4 | bd4c935 |
| #12 | **D4까지 그대로, 그 뒤만 가속**(x = D−4): 운영비 1 + 0.5D + **0.2x²**, 약점 효과 **×(1+0.5x)**(줄어드는 배율 하한 0.3), 재해 세기 **×(1+0.5x)**(지속 시간·피해). 수치 잠정 | LD0~LD4 | 6853551 |

**현재 기준값**(숙련 봇 `dotnet run --project Tools/SettlementSim -- mods campaign 0.25 14000 30 smart`, 2026-10-27 #12 반영 뒤):
D=1 도달 6번째 정착지, 처음 적자 D3.7~4.7, D 구간별 정착지 순이익 평균 D0~1 +974 · D1~2 +1174 · D2~3 +743 · D3~4 +278 · D4~5 −360 · D5~6 −690.
숙련 봇은 사람보다 훨씬 못 번다(사람은 2차 플레이에서 D8 자산 1만 이상, 봇은 보류를 거의 안 하고 물탱크·창고를 안 지음). 그래서 #12의 후반 세기는 3차 플레이로 확인한다.
그 밖의 흐름 값: 1배속 하루 180초(FL1), 건설·개량·철거 시간 ×0.5(FL2), 시작 자금 500(FB1), 경매 48h(AU1).

**플레이테스트 체크리스트:** 웹 페이지 https://claude.ai/artifact/FVtbqQ1jc1MrT5QiiKSFrj (체크·메모가 저장되고 Claude가 읽을 수 있음, 비공개) / 저장소 문서 `Docs/playtest_checklist_2026-10-23.md`(1차 항목만).
1차(2026-10-23) 54개 모두 확인. 2차(2026-10-26) 40개 중 확인 37 · 문제 3 → 이슈 #1~#13. 기록은 이슈 #14 본문.
**3차에서 확인할 것** 22개(10-26분 8 + 10-27분 14)는 #14 댓글 '3차 플레이테스트에서 확인할 것'에 모아 두었다(이슈별 결과 댓글 끝에도 있음). 3차 체크리스트는 이 항목들(고친 부분)만으로 만든다.

## 4. 다음에 할 일 (우선순위)

1. **3차 플레이테스트:** 체크리스트 웹 페이지 맨 위 '3차 — 고친 부분만' 절(A~F, 22개, 절 id v3a~v3f)로 확인한다. 이미 확인한 것은 다시 묻지 않는다.
2. **3차 플레이테스트 결과 기록:** 확인·문제·메모를 #14 본문(또는 새 이슈)에 남기고, 문제는 이슈로 만든다. #12 후반 세기가 지나치거나 약하면 `procedural_map_config.json`의 `difficulty.lateStartD`·`upkeepLatePerD2`·`weaknessLatePerD`·`disasterLatePerD`부터 조정.
3. 그 뒤: 예전 IMGUI 화면(F1) 삭제 여부 결정, 측정 봇 보완(보류·개량·물탱크를 사람처럼 써서 후반 측정이 사람 플레이에 가깝게, #12 LD0에서 미룸), 폴리싱.

## 5. 주의할 것

- 시뮬레이터 측정 모드: `-- tune`(난이도 곡선) · `-- mods [D]` / `-- mods campaign [정착지항] [cashPerD] [정착지 수] [smart] [halls=k] [attendees=a-b] [cash=x] [late=D upkeep2=k weak=s dis=s]`(모디파이어·장비. `halls=k`는 봇이 경매장 k개, `late= upkeep2= weak= dis=`는 #12 후반 가속 값을 덮어씀 — 모두 0이면 #12 이전과 같음) · `-- exploit [맵 수]`(몰아짓기·철거) · `-- epidemic [맵 수]` / `-- epidemic sweep [맵 수]`(전염병 후보 표) · `-- savecheck`(저장 재실행). 결과는 `Tools/balance_output/`(git 무시).
- 여러 후보를 동시에 재려면 한 번 빌드(`dotnet build Tools/SettlementSim -c Release`) 뒤 `dotnet Tools/SettlementSim/bin/Release/net10.0/SettlementSim.dll mods campaign …`을 병렬로 돌린다. 콘솔 출력은 cp949라 Git Bash에서는 `iconv -f cp949 -t utf-8`로 읽는다.
- 설정 값을 바꿔 비교 측정할 때는 설정 JSON을 백업해 두고 바꾼 뒤 측정하고 **반드시 되돌린다**(`git diff Assets/Data/Config/`로 확인). `procedural_map_config.json`의 값은 코드 기본값과 같아야 한다(`ProceduralMapTests.Config_JsonMatchesCodeDefaults`).
- UI 모양 확인: `bash Tools/unity-tests.sh PlayMode ScreenCaptureTests` → `.unity-testcopy/Captures/*.png`. 1920×1080 장면 + `01b_build_farm_overlay`(#7)·`03b_16x10`·`03c_5x4`(#5)·`03d_night_lanterns`(#13)·`06_destination`·`06b_shop`(#6). 캡처는 캔버스 배율을 실제 게임 식(ScaleWithScreenSize 0.5)으로 계산한다.
- 반투명·발광 머티리얼은 `WorldVisuals.Unlit`(`Sprites/Default`, 그래픽 설정 '항상 포함')로 만든다. URP Lit 반투명·발광을 코드로 켜면 빌드에서 셰이더 변형이 빠질 수 있다.
- 경매 시각이 바뀌는 규칙을 건드리면 테스트 기대값이 여러 곳에 있다: `AuctionCampaignTests`(첫 경매 84h·보류 뒤 132h), `DebugSessionSmokeTests`(샘플 배치 경매장 21h·정기 경매 69h), `SettlementTests`(v0.8 스크립트는 예전 착공·96h 기준).
- 테스트 사본은 새 파일의 `.meta`를 만들면 원본으로 되가져온다. 새 스크립트를 추가했다면 커밋 전에 `.meta`가 있는지 확인.
- v0.8 원본 회귀값(133h·V 1149.63 등)은 원본 스크립트 기준이라 가동 숙성·채석장 능력·명령 순간 착공을 끄고 비교한다(EditMode `SettlementTests`의 Script, 시뮬레이터 `RunCoreScript`).
