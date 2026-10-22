# 구현 노트 — 코드 구조와 수정 가이드

> 기획 문서(`desert_settlement_game_design_checkpoint_2026-10-20.md`)에 없던 **구현 단계 결정**과, 코드를 직접 고칠 때 **어디를 보면 되는지**를 정리한다.

## 1. 구현 단계 결정 기록

| 날짜 | 항목 | 결정 | 상태 |
|---|---|---|---|
| 2026-10-22 | 헥스 방향 | **Pointy-top** (꼭짓점이 위·아래). axial `(q, r)`, r이 커질수록 남쪽(월드 −Z) | [확정] |
| 2026-10-22 | 설정 JSON 위치 | `Assets/Data/Config/economy_balance_v0.8_config.json` 하나만 원본으로 둔다 | [확정] |
| 2026-10-22 | 배속 목록 | 0 / 1 / 2 / 4배속, 인스펙터에서 수정 가능 | [잠정 유지] |
| 2026-10-22 | 정착지 시작 시각 | **정착지마다 1일차 12:00(정오)** 에서 시작 | [확정] |
| 2026-10-22 | 낮/밤 | **구현한다.** 시계가 낮/밤 상태와 전환 이벤트를 제공하고, 태양 조명이 시각에 따라 바뀐다 | [확정] |
| 2026-10-22 | 낮/밤 경계 시각 | 낮 06:00, 밤 18:00 (인스펙터에서 수정 가능) | [잠정 확정] |
| 2026-10-22 | 낮/밤의 게임 규칙 영향 | 현재는 **시각 표현만**. 생산 보정 등은 넣지 않음(기획서 4.1 "주야간 생산 보정은 도입하지 않는 방향") | [잠정] |
| 2026-10-22 | 맵 경계 수로 | **맵 바깥 경계 변에도 수로 설치 가능** | [확정] |
| 2026-10-22 | 수로 경로 길이 단위 | 변 개수(변 1개 = 1). 변 중간 취수점(CanalIntake)은 물 배분 단계에서 추가 | [잠정] |
| 2026-10-22 | 수로 순환(고리) | 감지 함수만 있고 막지 않음 | [미정 유지] |

**개발 순서 (2026-10-22 합의):** 핵심 시스템 하나를 확실히 → 얇은 전체 루프 → 확장.
1. ~~기반: 헥스 그리드·수로 그래프·GameClock·설정 로딩~~ (완료)
2. 물 배분 계산기 (상류 우선·동순위 비례·용량·손실, 테스트 포함)
3. 건설 큐 + 생산·식량·유지비 (경제 틱)
4. 얇은 경매·매각 + 두 번째 맵 → 두 정착지 연속 매각 루프
5. 미정 항목을 결정하며 확장 (장비·파산·물탱크·입찰자 등)

**문서 간 충돌(미해결):** 경제 틱 처리 순서가 통합 기획서 4.2와 `economy_balance_v0.8.md` 209행에서 다르다. 생산·건설 정산 구현 전에 하나로 정해야 한다.

## 2. 코드 구조

```
Assets/Scripts/
  Core/        DesertSettlement.Core — 게임 규칙. 순수 C#, UnityEngine 참조 없음
    Hex/         HexCoord, HexLayout, HexMap, VertexId/EdgeId, HexTopology
    Canal/       CanalGraph, CanalSegment
    Timing/      GameClock, GameTimeSettings
    Config/      EconomyConfig(데이터 클래스), EconomyConfigLoader(읽기·검증)
  Runtime/     DesertSettlement.Runtime — Unity 컴포넌트(표시·입력·연결)
    GameSession        진입점. Core 객체를 만들고 매 프레임 시계를 진행
    HexMapDebugView    씬 뷰 Gizmo로 타일·수로·수문 표시(임시)
    DayNightLighting   시각에 따라 태양 회전·색·밝기
    GameClockHud       시각·배속 표시와 버튼(임시 IMGUI)
  Editor/      DesertSettlement.EditorTools — 에디터 메뉴
    DebugSessionMenu   Tools > Desert Settlement > 디버그 세션 만들기
Assets/Tests/EditMode/  DesertSettlement.Core.Tests — Core 단위 테스트
Assets/Data/Config/     경제 설정 JSON
```

**원칙**
- 규칙은 Core에, 보여주기는 Runtime에 둔다. Core는 Unity 없이 테스트할 수 있다.
- 조정할 숫자는 코드에 박지 않는다. 경제 수치는 JSON, 시간·표시 값은 인스펙터에서 바꾼다.
- [확정] 규칙은 코드에 고정하고, [잠정] 값은 설정으로 뺀다. [미정]은 구현하지 않는다.

## 3. 무엇을 바꾸려면 어디를 고치나

| 바꾸고 싶은 것 | 위치 |
|---|---|
| 시설 비용·생산량·유지비·수로 용량 등 경제 수치 | `Assets/Data/Config/economy_balance_v0.8_config.json` (코드 수정 불필요) |
| 맵 반경, 타일 크기 | 씬의 `GameSession` 인스펙터 → 맵 |
| 시작 시각, 낮/밤 경계, 배속 목록 | `GameSession` 인스펙터 → 시간 → Time Settings |
| 하루 길이(1배속 90초) | JSON `confirmedRules.gameDayRealSecondsAt1x` (확정값이므로 기획 변경 시에만) |
| 태양 색·밝기·일출/일몰 각도 | `DayNightLighting` 인스펙터의 Gradient / Curve |
| 디버그 표시 색, 꼭짓점·좌표 라벨 표시 | `HexMapDebugView` 인스펙터 |
| JSON에 새 항목 추가 | JSON에 키 추가 → `Core/Config/EconomyConfig.cs`에 같은 이름의 속성 추가 |
| 설정 검증 규칙 추가 | `Core/Config/EconomyConfigLoader.cs`의 `Validate` |
| 새 지형 종류 | `Core/Hex/HexMap.cs`의 `TerrainType` (끝에 추가) |
| 정각·자정·낮밤 전환 시 처리 추가 | `GameSession.Initialize`에서 `Clock.HourPassed` / `DayStarted` / `PhaseChanged` 구독 |

## 4. 확인 방법

1. Unity에서 `SampleScene`을 열고 메뉴 **Tools > Desert Settlement > 디버그 세션 만들기**를 실행한다.
2. 씬 뷰에 반경 3 헥스 맵이 보인다(경계선은 주황색).
3. Play를 누르면 확인용 수로(파란색)와 닫힌 수문(빨간 상자)이 보이고, 왼쪽 위 HUD에 `1일차 12:00 (낮)`이 표시된다. 배속 버튼으로 낮/밤 조명 변화를 볼 수 있다.
4. 테스트: Window > General > Test Runner > EditMode > Run All, 또는 CLAUDE.md의 batch mode 명령.
