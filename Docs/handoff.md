# 이어서 작업하기 (다른 PC·새 Claude 세션용 인계)

마지막 정리: 2026-10-29(집 PC, 유지보수성 정리 — 기능 변화 없음) · 최신 커밋은 `git log -1`로 확인 (origin = 개인 포크 `ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr`, main)

> **2026-10-26부터 남은 작업은 GitHub 이슈로 관리한다.** 먼저 고정된 순서표 이슈 **#14**를 읽는다:
> `gh issue view 14 -R ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr --comments` (또는 https://github.com/ashadeveloperr/3rd_3D_1-3D-Project-ashadeveloperr/issues/14)
> #14에 처리 순서(#1~#13, 끝난 것은 체크 + 정한 내용 한 줄), 작업 방식, 2차 플레이테스트 기록, **3차 플레이테스트에서 확인할 목록(댓글)**이 있다. 이슈는 git이 아니라 GitHub에 저장되므로 `git pull` 없이도 보인다.

새 Claude 세션은 이전 대화를 기억하지 못한다. 이 문서 + `CLAUDE.md`(원칙·작업 방식·문서 지도) + **이슈 #14** + `Docs/code_guide.md`(코드 구조·고칠 곳)
+ `Docs/implementation_notes.md`(결정 표) + `Docs/decision_log_auto.md`(결정 근거·측정)를 읽으면 이어 갈 수 있다.
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
5. **경로:** 경로를 고칠 곳은 없다. `Tools/unity-tests.sh`·시뮬레이터는 저장소 위치를 스스로 찾고, `CLAUDE.md`의 배치 명령은 저장소 루트에서 `$Proj = (Resolve-Path .).Path`로 쓴다.
6. **확인:**
   ```bash
   dotnet run --project Tools/SettlementSim
   bash Tools/unity-tests.sh All
   ```
   시뮬레이터 마지막 줄 '모든 검증 통과', 테스트는 **EditMode 181 · PlayMode 10 통과**가 기준(2026-10-30, 화면 캡처 `ScreenCaptureTests`는 [Explicit]이라 건너뜀·따로 실행).
   `unity-tests.sh` 첫 실행은 테스트 사본(`.unity-testcopy/`) 임포트로 몇 분 걸린다.

## 2. 작업 방식 (사용자 선호)

`CLAUDE.md`의 '유지보수 원칙'·'작업 방식' 절에 있다(새 세션이 자동으로 읽는다). 여기에는 베끼지 않는다.

## 3. 지금 상태 요약 (2026-10-27 기능 마감, 2026-10-29 유지보수성 정리)

**2026-10-29 유지보수성 정리**(동작 변화 없음): `CLAUDE.md`에 유지보수 원칙·변경 마무리 체크리스트·문서 지도, 새 `Docs/code_guide.md`(구조·흐름·고칠 곳),
끝난 10-23 문서는 `Docs/archive/`, 시장 코드를 `Settlement.Market.cs`로 분리, 낡은 주석 수정. 검토 결과와 남은 정리 목록 M1~M22는 `Docs/review_2026-10-29_maintainability.md`.

**순서표 #14의 #1~#13과 3차 플레이테스트분 #15~#18이 모두 끝났다.** 1단계 버그(#1~#4)·2단계 규칙(#8~#11)은 2026-10-26, 3단계 UI·연출(#5·#6·#7·#13)·4단계 밸런스(#12)는 2026-10-27.
3차 플레이테스트(2026-10-27, 22개 중 확인 20 · 문제 2)의 문제·메모는 #15~#18로 만들어 같은 날 처리했다.
이슈마다 결과 댓글을 달고 닫았고, #14에 체크와 정한 내용 한 줄을 붙였다. 다음 순서는 **4차 플레이테스트**다.

**들어간 것:** 이슈마다 #14 순서표에 체크와 정한 내용 한 줄이 있고, 결정 표(`Docs/implementation_notes.md`)·근거(`Docs/decision_log_auto.md`, 결정 번호로 찾기)에 자세히 있다. 커밋은 `git log --oneline`에서 이슈 번호로 찾는다.

**현재 기준값**(숙련 봇 `dotnet run --project Tools/SettlementSim -- mods campaign 0.25 14000 30 smart`, 2026-10-27 #18 시장 반영 뒤, 봇은 시장을 짓고 넘치는 자원을 팖):
처음 적자 D3.1~4.4, D 구간별 정착지 순이익 평균 D0~1 +1248 · D1~2 +1231 · D2~3 +755 · D3~4 +284 · D4~5 −425 · D5~6 −635(#18 이전 +974 · +1174 · +743 · +278 · −360 · −690, `trade=off`로 재현).
숙련 봇은 사람보다 훨씬 못 번다(사람은 2차 플레이에서 D8 자산 1만 이상, 봇은 보류를 거의 안 하고 물탱크·창고를 안 지음). #12 후반 세기는 3차 플레이에서 확인됐다(F절 모두 확인).
그 밖의 흐름 값: 1배속 하루 180초(FL1), 건설·개량·철거 시간 ×0.5(FL2), 시작 자금 500(FB1), 경매 48h(AU1).

**플레이테스트 체크리스트:** 웹 페이지 https://claude.ai/artifact/FVtbqQ1jc1MrT5QiiKSFrj (체크·메모가 저장되고 Claude가 읽을 수 있음, 비공개) / 저장소 문서 `Docs/archive/playtest_checklist_2026-10-23.md`(1차 항목만, 보관).
1차(2026-10-23) 54개 모두 확인. 2차(2026-10-26) 40개 중 확인 37 · 문제 3 → 이슈 #1~#13. 3차(2026-10-27) 22개 중 확인 20 · 문제 2 → 이슈 #15~#18(메모 2건 포함). 기록은 이슈 #14 본문·댓글.
**4차에서 확인할 것** 11개(#15 1 · #16 2 · #17 2 · #18 6, 고친 부분만)는 #14 댓글 끝 '4차 플레이테스트에서 확인할 것'(A~D절)에 모아 두었고(이슈별 결과 댓글 끝에도 있음), 체크리스트 웹 페이지 맨 위에 '4차 — 고친 부분만' 절(절 id v4a~v4d, 펼침)로 올려 두었다. 3차 절(v3a~v3f)은 '완료'로 접었다.

## 4. 다음에 할 일 (우선순위)

1. **4차 플레이테스트:** 체크리스트 웹 페이지 맨 위 '4차 — 고친 부분만' 절(A~D, 11개)로 확인한다(위쪽 진행률도 4차 기준). 시장은 샘플 배치에 없으니 '기반시설 → 시장'으로 직접 짓는다. 끝나면 새 세션에서 "4차 결과 읽고 기록해 줘" — 페이지의 체크·메모(db `checks`·`notes`, 키는 절 id·항목 번호)를 읽어 #14에 기록하고 문제는 이슈로 만든다. 이미 확인한 것은 다시 묻지 않는다.
2. **4차 플레이테스트 결과 기록:** 확인·문제·메모를 #14에 남기고, 문제는 이슈로 만든다. 시장 판매가 지나치거나 약하면 `economy_balance_v0.8_config.json`의 `purchase.*.sellLimit`·`purchase.trade.recoveryPerHour`부터 조정. #12 후반 세기가 지나치거나 약하면 `procedural_map_config.json`의 `difficulty.lateStartD`·`upkeepLatePerD2`·`weaknessLatePerD`·`disasterLatePerD`부터 조정.
3. **유지보수 정리:** 이슈 #19~#30(라벨 `maintenance`)에 있고, 처리 순서는 #14의 '유지보수 정리 순서' 댓글이다. 1단계 #19·#20·#21은 2026-10-30에 끝났고, 다음은 2단계 #25(예전 화면 삭제)·#26(씬 재구성, 에디터를 닫아야 함) → 나머지는 기능 작업 사이사이. 4차 플레이테스트와는 따로 진행할 수 있다.
4. 그 뒤: 측정 봇 보완(보류·개량·물탱크를 사람처럼 써서 후반 측정이 사람 플레이에 가깝게, #12 LD0에서 미룸), 폴리싱.

## 5. 주의할 것

시뮬레이터 측정 모드·병렬 측정·설정 되돌리기, 화면 캡처, 반투명 머티리얼, 경매 시각 테스트 기대값, `.meta`, v0.8 회귀 모드는 `Docs/code_guide.md` 4·6·7절로 옮겼다(2026-10-29).
