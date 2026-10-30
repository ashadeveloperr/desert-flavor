# 사막 정착지 경영 시뮬레이션 (Desert Settlement Simulator)

Unity 3D 헥스타일 기반 정착지 개발·생산망·경매 경영 게임의 **1인 개발 프로토타입**입니다.
학원 수업용 Unity 저장소를 개인 계정으로 포크해 새 프로젝트로 정리했습니다. 《사막의 열병》 세계관의 스토리 게임과는 별개입니다.

> **현재 상태:** MVP 게임 루프가 돕니다 — 정착지 건설·운영 → 경매 → 매각/보류 → 행선지 선택 → 장비 상점 → 다음 정착지(2번째부터 절차 생성 맵).
> 그래픽은 기본 도형, UI는 코드로 만든 uGUI입니다. 남은 작업은 [GitHub 이슈](../../issues)(순서표 #14)로 관리합니다.

## 게임 개요

- **맵:** 반경 3~7의 헥스 타일. 시설은 타일 안, **수로는 육각형 변(Edge)**, 분기점은 꼭짓점(Vertex)
- **물:** 우물에서 수로를 따라 흐르고, 상류 시설이 먼저 받는다. 물이 모자라면 가동률이 떨어진다
- **자원:** 식량·석재·광물·자금. 건설은 석재만, 유지비·구매는 자금. 식량이 모자라면 모든 생산이 줄어든다(식량 계수)
- **경매:** 경매장을 지으면 정기 경매가 열리고, 성향이 다른 입찰자들이 직전 실적으로 정착지 값을 매긴다. 팔거나 보류한다
- **캠페인:** 매각 대금으로 장비를 사서 다음 정착지로. 자금이 많을수록 다음 땅이 어려워진다
- **주택·노동력:** 주택 주민이 수로 물·식량을 쓰고 우물·농장·채석장·광산·공사에 자동 배치(우선 고용 버튼), 모자라면 떠남
- **식품·생활 수준:** 경작지·과수원·목축지·허브 농장(산지)·제분소·양조장·포도주 공방, 재화를 가중치대로 함께 먹고 상위 재화일수록 생활 수준이 올라 이주·생산·경매에 효과
- **재해·시장:** 전염병(주민이 아파 일 못 함, 병원·방역 장비)·모래폭풍·메뚜기떼·작물 탄저병, 시장에서 식량·석재 사고팔기

## 실행

1. Unity Hub에서 저장소 루트를 프로젝트로 추가하고 **Unity 6000.3.15f1**로 엽니다(다른 버전은 패키지가 업그레이드됩니다).
2. `Assets/Scenes/SampleScene.unity`를 열고 Play. 정착지는 일시정지로 시작합니다.
3. 왼쪽 아래 **샘플 배치**로 예시 시설을 예약하거나, 아래 도구 막대에서 시설을 골라 타일·변을 클릭합니다.
   배속 `` ` `` 1 2 3, 카메라 WASD·휠·Q/E, 저장/불러오기 F5/F9.

## 테스트

```bash
dotnet run --project Tools/SettlementSim     # 게임 규칙 회귀 검증(.NET 10 SDK)
bash Tools/unity-tests.sh All                # Unity 테스트(테스트 전용 사본에서, 열린 에디터와 무관)
```

## 문서

| 알고 싶은 것 | 문서 |
|---|---|
| **코드 구조, 흐름, 무엇을 바꾸려면 어디를 고치나** | [`Docs/code_guide.md`](Docs/code_guide.md) |
| 정한 규칙(한 줄 표) → 이유·측정 | [`Docs/implementation_notes.md`](Docs/implementation_notes.md) → [`Docs/decision_log_auto.md`](Docs/decision_log_auto.md) |
| 지금 수치 | [`Assets/Data/Config/`](Assets/Data/Config/) 의 JSON 두 개 |
| 지금 상태·다음 할 일, 다른 PC 준비 | [`Docs/handoff.md`](Docs/handoff.md) |
| 개발 원칙·AI 작업 지침 | [`CLAUDE.md`](CLAUDE.md) |
| 기획 원본 | [통합 기획서](Docs/desert_settlement_game_design_checkpoint_2026-10-20.md) · [경제 v0.8](Docs/economy_balance_v0.8.md) · [식량·저장 v0.6](Docs/economy_food_storage_v0.6.md) · [컨셉 v4](Docs/desert_settlement_concept_plan_v4.html) |
| 끝난 작업 기록 | [`Docs/archive/`](Docs/archive/) |

## 포크와 조직 원본에 대한 주의

- 개인 포크에만 적용한 정리입니다. 원본 `KooHoo-Dev` 조직 저장소는 변경하지 않았고, 정리 전 상태는 [`backup/original-unity-course-2026-10-20`](../../tree/backup/original-unity-course-2026-10-20) 브랜치에 보존했습니다.
- 이 포크는 수업 자료를 대량 제거했으므로, 조직 원본에 그대로 PR을 보내면 원본에서도 수업 파일을 지우는 변경을 제안하게 됩니다. 원본에 반영하려면 관리자 동의 아래 **공유할 코드·문서만 분리한 별도 PR**을 만드세요.
- **Sync fork**는 원본의 최신 커밋을 내 포크로 가져올 뿐 원본을 덮어쓰지 않습니다.
