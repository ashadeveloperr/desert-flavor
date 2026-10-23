# 이어서 작업하기 (다른 PC·새 Claude 세션용 인계)

마지막 정리: 2026-10-23 · 최신 커밋은 `git log -1`로 확인 (origin = 개인 포크 `ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr`, main)

새 Claude 세션은 이전 대화를 기억하지 못한다. 이 문서 + `CLAUDE.md` + `Docs/implementation_notes.md`(결정 기록) + `Docs/work_items_2026-10-23.md`(진행 중 작업 목록)
+ **`Docs/roadmap_2026-10-23.md`(초기 기획 완성까지 남은 작업과 순서)**를 읽으면 이어 갈 수 있다.
새 세션을 시작할 때 이렇게 말하면 된다: **"Docs/handoff.md를 읽고 이어서 작업하자."**

---

## 1. 집 PC 준비 (처음 한 번)

1. **저장소 받기**
   ```bash
   git clone https://github.com/ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr.git
   ```
   이미 받아 둔 폴더가 있으면 `git pull origin main`.
   upstream(`KooHoo-Dev/...`)에는 **푸시하지 않는다**(README 경고).
2. **Unity 6000.3.15f1** (Unity Hub로 설치, 다른 버전으로 열면 패키지가 업그레이드되니 주의).
   기본 경로 `C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe`. 다른 곳에 깔았으면 테스트 스크립트에 `UNITY_EXE` 환경 변수로 알려 준다.
   UniTask가 Git URL 패키지라 첫 실행 때 **git이 PATH에 있어야** 한다.
3. **.NET 10 SDK** — 게임 로직 시뮬레이터(`Tools/SettlementSim`)용. Python은 필요 없다(이 PC에는 없었음).
4. **경로 확인:** `CLAUDE.md` 명령어 절의 `$Proj` 경로는 회사 PC 기준(`C:\Users\vr9chae\...`)이다. 집 PC 경로가 다르면 그 줄만 바꿔 쓴다.
   `Tools/unity-tests.sh`·시뮬레이터는 저장소 위치를 스스로 찾으므로 경로를 고칠 필요가 없다.
5. **확인:**
   ```bash
   dotnet run --project Tools/SettlementSim
   bash Tools/unity-tests.sh All
   ```
   시뮬레이터 마지막 줄 '모든 검증 통과', 테스트는 EditMode 132 · PlayMode 4 통과가 기준(2026-10-23).
   `unity-tests.sh` 첫 실행은 테스트 사본(`.unity-testcopy/`) 임포트로 몇 분 걸린다.

## 2. 작업 방식 (사용자 선호 — 회사 PC의 Claude 메모리에만 있던 내용)

`CLAUDE.md`의 '작업 방식' 절에도 같은 내용을 옮겨 두었다(새 세션이 자동으로 읽음).
- **모든 답변은 한국어**(짧은 답·진행 안내·마무리 요약·버튼까지).
- **사용자가 직접 고칠 수 있는 구조:** 규칙은 Core(순수 C#), 표시는 Runtime. 조정할 숫자는 설정 JSON이나 인스펙터로. 공개 타입에 한국어 주석. 새 시스템마다 `Docs/implementation_notes.md` 결정 표·수정 가이드 갱신.
- **[미정] 항목은 사용자 결정 없이 확정하지 않는다.** 정할 것이 있으면 하나씩 설명하고 추천안을 첫 번째에 두고 묻는다.
- **커밋·푸시는 요청받았을 때만.** 푸시는 origin(개인 포크)만.
- **Unity 테스트는 사용자 에디터를 건드리지 않는다:** `bash Tools/unity-tests.sh`(테스트 전용 사본)로 돌리고, 에디터를 닫아 달라고 하지 않는다.
  원본에 결과를 남겨야 하는 배치 작업(빌드, `-executeMethod SetupSampleScene`)만 예외이고 그때는 먼저 묻는다.
  (열린 에디터에 테스트를 시키는 방식은 작업을 방해해서 폐기했다.)
- 밸런스는 바로 고치기보다 **시뮬레이터로 재현·비교한 뒤** 제안한다.

## 3. 지금 상태 요약

**완료된 큰 줄기:** 헥스·수로·물 배분 → 경제(건설 큐·식량·유지비·가치) → 경매·캠페인 → BSP 절차 맵(난이도 곡선) → 지역 모디파이어·후보 지역 2곳·장비 9종·장비 슬롯 5 →
낮 05~20시·경매 72h → 가치 평가 보완(가동 숙성 70%→48h·석재 배율에 채석장 능력) → 재해(전염병)·병원·방역 장비.
자세한 결정과 근거는 `Docs/implementation_notes.md` 1절 결정 표.

**최근 결정(2026-10-23)과 위치**
| 내용 | 어디에 |
|---|---|
| 작업 목록 4개(낮·경매 주기·몰아짓기 방지·재해) | `Docs/work_items_2026-10-23.md` |
| 난이도 곡선(자금 14000당 D 1 + 정착지마다 +0.2) | `Assets/Data/Config/procedural_map_config.json` |
| 모디파이어(약점 6·강점 3)·후보 지역 2곳 | 같은 파일 `modifiers`, `regionChoices` |
| 장비 9종·가치 숙성·재해·병원 | `Assets/Data/Config/economy_balance_v0.8_config.json` (`equipment`, `auctionSeed`, `disasters`, `buildingLevels.hospital`) |

## 4. 다음에 할 일 (우선순위)

> **전체 순서는 `Docs/roadmap_2026-10-23.md`가 기준이다**(2026-10-23 정리: 1단계 기획 규칙 채우기 → 2단계 플레이 완성도 → 3단계 폴리싱).
> 추천: 재해 구상을 기다리는 동안 **물탱크**(로드맵 1-②) → 재해 구상이 오면 다른 재해(1-①) → L3·창고 폐쇄 → 장비 강화 → 도로 범위 → 입찰자 6종 → 난이도 재측정.
> 아래 목록은 그 전까지의 기록이다.

1. **재해 밸런스 B1~B3 결정**(`Docs/work_items_2026-10-23.md` 4번 끝, 측정 `dotnet run --project Tools/SettlementSim -- epidemic 20`)
   - ~~B1 방역 장비가 거의 면역~~ → 2026-10-23 결정·구현: 방역 장비 = 저항력 상승 속도 ×1.5. 후보 비교는 `-- epidemic sweep 20`.
   - ~~B2 병원이 손해~~ → 2026-10-23 결정: 병원 유지비 0.5·식량 0(전염병 중 식량 소비가 손해를 키우던 문제 제거).
   - ~~B3 전염병 자체가 약함~~ → 2026-10-23 결정: 시작 심각도 0.35·저항 상승 0.04·S 감소 0.03(D1 전염병 1회 +660 → +469).
   - **B1~B3 모두 끝남.**
2. **다른 재해 구상**(사용자가 직접 구상하기로 함 — **지금 1순위**): 모래폭풍·작물병(탄저병)·메뚜기떼. 이전 초안·지역 모디파이어 연결 제안은 작업 목록 4-4.
3. **밸런스 재측정 5-1:** 2026-10-23 재해(전염병) 포함으로 측정 완료 — D=1 순이익 +353(충족), D=1 도달 5~7번째(충족), 첫 적자 약 D4.3(목표 D 3~4보다 약간 늦음).
   **결정: 첫 적자는 그대로 두고, 다른 재해를 넣은 뒤 다시 잰다.** 기준 표와 명령은 작업 목록 '5-1 측정 결과'.
4. **에디터 플레이테스트**(미뤄 둠): 상점(장비 9종·후보 지역 2곳·슬롯), 생성 맵 캠페인, 병원·전염병 HUD를 직접 확인.
5. 그 뒤 후보: 저장·불러오기, 입찰자 6종 확장(게임 시스템이 다 정해진 뒤로 미룸 — 사용자 결정), L3 개량·창고 폐쇄, 물탱크, 정식 UI.

## 5. 주의할 것

- **`Assets/Scenes/MapSketch.unity`에 커밋하지 않은 변경이 회사 PC에 남아 있다**(맵 스케치 씬에 생성 예시 맵을 불러온 상태). 집 PC에는 없다. 필요하면 회사 PC에서 커밋하거나 버린다.
- 시뮬레이터 측정 모드: `-- tune`(난이도 곡선) · `-- mods [D]` / `-- mods campaign [정착지항] [cashPerD] [정착지 수]`(모디파이어·장비) · `-- exploit [맵 수]`(몰아짓기·철거) · `-- epidemic [맵 수]`(전염병). 결과는 `Tools/balance_output/`(git 무시).
- 테스트 사본은 새 파일의 `.meta`를 만들면 원본으로 되가져온다. 새 스크립트를 추가했다면 커밋 전에 `.meta`가 있는지 확인.
- v0.8 원본 회귀값(133h·V 1149.63 등)은 원본 스크립트 기준이라 가동 숙성·채석장 능력을 끄고 비교한다(EditMode `SettlementTests`의 Script, 시뮬레이터 `RunCoreScript`).
