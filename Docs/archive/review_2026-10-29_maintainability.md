# 유지보수성 검토 (2026-10-29)

> 목표: 사용자가 직접 읽고 고칠 수 있는 코드·저장소. 기준 커밋 e98357f(#18 시장까지).
> 방법: Core와 Runtime·도구·테스트를 각각 읽기 전용으로 검토하고, 핵심 지적은 코드에서 직접 다시 확인했다. 규모는 C# 약 1만 8천 줄(게임 코드 약 1만 줄).
> 이 문서는 검토 시점의 기록이다. 정리 항목은 2026-10-30 이슈 #19~#30(라벨 `maintenance`, 순서는 #14 댓글)으로 옮겨 같은 날 모두 끝냈고, 이 문서는 `Docs/archive/`로 옮겼다(결과는 이슈마다의 결과 댓글과 `decision_log_auto.md`).
>
> | 이슈 | 항목 | 이슈 | 항목 |
> |---|---|---|---|
> | #19 | M1 | #25 | M12 |
> | #20 | M2·M5 | #26 | M13 |
> | #21 | M3·M4 | #27 | M14·M15 |
> | #22 | M6 | #28 | M16·M17·M18 |
> | #23 | M7 | #29 | M19 |
> | #24 | M8·M9·M10 | #30 | M20·M21·M22 |
>
> M11(연혁 주석)은 이슈 없이 손대는 파일마다 정리한다.

## 1. 요약

**이미 좋은 점(원칙으로 유지):** Core가 Unity 없이 돌고(`noEngineReferences`) 결정론적이라 테스트·시뮬레이션·저장 재실행이 된다. 수치 대부분이 설정 JSON에 있다. 시설 종류는 `FacilityCatalog` 한 목록이고 JSON과 어긋나면 검증이 막는다. `Can*`이 불가 이유를 돌려줘 화면과 규칙이 같은 검사를 쓴다. 회귀값이 테스트와 시뮬레이터 양쪽에 고정돼 있다. 공개 타입에 한국어 주석이 거의 다 있다.

**어려운 점(세 갈래):**
1. **문서가 흩어지고 같은 내용이 여러 곳에 있었다.** CLAUDE.md·handoff·implementation_notes·decision_log·work_items·roadmap·review·AGENTS.md(낡은 사본)가 같은 결정과 값을 되풀이해, 한 곳만 고쳐지면 어긋났다. README는 "게임플레이 코드는 아직 없다"고 적혀 있었다.
2. **주석이 변경 기록처럼 쓰였다.** Core에 날짜 표기 약 120개, 결정 번호 약 100개가 있고, 현재 코드와 어긋난 주석이 있었다(착공은 다음 틱, 재해 유예 48h, 방역 장비 1.5, 전염병 R ≥ S 등).
3. **빠뜨려도 오류 없이 조용히 틀어지는 곳이 있다.** 새 명령을 저장 재실행에 빠뜨림, 효과 항목을 `Scaled`에 빠뜨림, JSON 키 오타(0으로 읽힘), 같은 공식의 사본 중 하나만 고침 — 이미 한 번 일어났다(아래 M1).

## 2. 오늘 한 정리 (동작 변화 없음)

- **원칙을 `CLAUDE.md`에 넣었다**(새 세션이 자동으로 읽음): 한 사실은 한 곳에, 주석은 지금의 코드를 설명, 구조 기준, 변경을 마칠 때 체크리스트, 문서 지도.
- **`Docs/code_guide.md` 새로 씀:** 세 층 구조, 폴더별 역할, 시작·한 시간·플레이어 명령의 흐름, 자주 하는 변경의 체크리스트(조용히 틀어지는 곳 ⚠), '무엇을 바꾸려면 어디를' 표(implementation_notes에서 옮겨 낡은 행 수정), 확인 방법, 알려진 함정.
- `implementation_notes.md`는 결정 표만, `handoff.md`는 상태·다음 할 일만 남기고 겹치던 절을 링크로 바꿈. README 새로 씀. AGENTS.md는 "CLAUDE.md를 따른다" 안내만.
- 끝난 2026-10-23 문서 4개(+ 캡처)를 `Docs/archive/`로, 참조 경로 수정.
- CLAUDE.md의 회사 PC 고정 경로(`$Proj`)를 어느 PC에서나 쓰는 형태로.
- 코드: 시장 코드를 `Settlement.Market.cs`로 분리(상태 파일에 섞여 있었음), 현재 코드와 어긋난 주석 수정(`Settlement`·`.Actions`·`.Commands`·`.Tick` 머리 주석, `DisasterManager`, 방역 장비 배율 두 곳, `EpidemicSettings`의 R 조건·잘못 붙은 주석).

## 3. 정리 목록

크기: 작음 = 1~2시간, 중간 = 반나절. 위험: 동작이 바뀔 수 있는 정도. **[결정]**은 사용자가 정해야 하는 항목.

### A. 조용히 틀어지는 것 막기 (먼저 추천 — 대부분 테스트 추가라 동작이 안 바뀜)

| # | 내용 | 근거 위치 | 크기·위험 |
|---|---|---|---|
| M1 **[결정]** | 공사 중 인프라 보너스(`InfraBonus`)에 **물탱크·시장이 빠져 있다.** 운영 중 합계에는 들어 있다. 실수로 보이지만 고치면 경매 가치가 조금 바뀐다 | `SettlementValuation.cs` `InfraBonus` ↔ 운영 중 `miRaw` | 작음·낮음(값 변화) |
| M2 | 설정 JSON 키 오타를 Newtonsoft가 조용히 기본값으로 읽는다. 코드가 안 읽는 키도 있다(`foodRecoveryRequiresOperatingWateredFarm`, `wellWaterSupplyIgnoresFoodMultiplier`, `economicTickGameHours`, canalEdge 레벨의 `lossPerWholeEdge`) → 로더가 모르는 키를 알려 주고, 안 읽는 키는 지우거나 '기록용' 표시 | `EconomyConfigLoader`, 설정 JSON | 작음·없음 |
| M3 | `EconomyEffects` 항목을 `Combine`·`Scaled`·`Describe`에 빠뜨려도 모른다 → 모든 속성이 유지되는지 반사(reflection) 테스트 | `EconomyEffects.cs` | 작음·없음 |
| M4 | 새 명령을 `SaveGame.Apply`에 빠뜨리면 불러오기에서 '건너뜀'으로만 남는다 → 모든 `GameCommand` 종류가 Apply에 있는지 테스트 | `SaveGame.cs` | 작음·없음 |
| M5 | 시뮬레이터가 게임과 다른 JSON 파서(System.Text.Json, 검증 없음)를 쓴다 → `EconomyConfigLoader`를 링크해 같은 파서·검증 | `Tools/SettlementSim/Program.cs` | 작음·낮음 |

### B. 읽기 쉽게 (Core)

| # | 내용 | 근거 위치 | 크기·위험 |
|---|---|---|---|
| M6 | 생산 공식("레벨 생산량 × 배율")이 4곳에 있고 '생산 시설 목록'도 2곳 → 도우미 하나 | `Settlement.OutputOf`, `.Tick.cs`, `SettlementValuation` 2곳, `Disasters.ProductionFactor` | 중간·낮음(회귀 테스트가 지킴) |
| M7 | '원본 v0.8 회귀 모드' 스위치가 흩어져 있다(`startJobsOnCommand`, `Trade == null`, 숙성 1, 채석장 능력, 건설 배율 1) → `SettlementRules`에 프리셋 하나 + 설명 | `SettlementRules`, `SettlementTests`, 시뮬레이터 | 중간·낮음 |
| M8 | 시드 섞는 상수(7919, 4421, 7777, 104729, `0xD15A`)가 흩어져 있다 → 한 클래스에 '바꾸면 저장 파일이 깨짐' 주석과 함께 | `Campaign.cs`, `Disasters.cs` | 작음·없음 |
| M9 | `GameCommand.Index`가 변 방향·새 위치·입찰 번호·지역 후보를 모두 뜻한다 → 종류별 생성 함수 | `SaveGame.cs` | 작음·없음 |
| M10 | 물 배분 ID가 문자열(`f.Id.ToString()`, `"tank"+id`, `int.Parse`)로 여러 곳에서 만들어진다 → 도우미 하나 | `Settlement.Water.cs`, `.WaterTanks.cs`, `.Tick.cs` | 작음·없음 |
| M11 | 연혁 주석(날짜·이슈 번호·"원래 X") → 원칙대로 **손대는 파일부터** 정리. 한 번에 다 고치지 않는다 | Core 전반 | 진행형 |

### C. Runtime·Unity

| # | 내용 | 근거 위치 | 크기·위험 |
|---|---|---|---|
| M12 **[결정]** | 예전 IMGUI 화면(`Runtime/Legacy/`, F1) 삭제. 먼저 F5/F9·배속 단축키를 정식 UI로 옮겨야 한다(지금은 Legacy `GameClockHud`에만 있음). 시장 판매가 Legacy에 없는 등 이미 뒤처짐 | `Legacy/`, `GameUi`, `HudInputBlocker`, PlayMode 테스트 F1 부분 | 중간·낮음 |
| M13 **[결정: 에디터 닫기]** | 씬 파일이 낡았다: `GameUi`·`WaterPathOverlay`·`BuildableOverlay`·`DesertBackdrop`이 씬에 없고 실행 중에 세 곳에서 붙는다, 씬에 옛 필드가 남아 있다 → 붙이는 곳을 `DebugSessionBuilder` 한 곳으로 모으고 씬 재구성 | `BuildInputController.Awake`, `SettlementView.Awake`, `DebugSessionBuilder` | 작음·낮음 |
| M14 | 코드로 만든 UI 오브젝트 이름이 전부 'Text'·'Button'이라 Hierarchy에서 못 찾는다 → `UiKit`이 문구로 이름 짓기 | `UiKit.cs` | 작음·없음 |
| M15 | `ModalView`·`AlertsView`가 문자열 키 비교로 다시 그릴지 정한다(키에 빠뜨리면 갱신 안 됨) → 명령 뒤 dirty 표시 | `ModalView.cs`, `AlertsView.cs` | 중간·낮음 |
| M16 | 명령 입구가 제각각(매각은 `GameSession`, 장비 장착은 `Campaign` 직접, 건설은 `Settlement` 직접) → 규칙을 정해 적기 | `ModalContent`, `InspectorView`, `BuildInputController` | 작음·없음 |
| M17 | `SettlementView`(464줄)에 등불·녹지화·수로·IMGUI 라벨이 함께 → 등불 컴포넌트 분리 | `SettlementView.cs` | 중간·낮음 |
| M18 | `GameSession`의 저장 파일 입출력·대체 지형을 partial로 분리, `HudMode`를 자기 파일로 | `GameSession.cs`, `HudInputBlocker.cs` | 작음·없음 |

### D. 테스트·도구

| # | 내용 | 근거 위치 | 크기·위험 |
|---|---|---|---|
| M19 | 설정 파일 경로가 테스트 약 15곳에 복사, 규칙에서 나오는 기대값(`36 + 48` 등)이 숫자로 박힘 → `TestData` 도우미, 규칙 파생값은 설정에서 읽고 의도한 골든 값만 한 곳에 | `Assets/Tests/EditMode/*` | 중간·없음 |
| M20 **[결정]** | 시뮬레이터 일회성 측정 모드(`exploit`, `epidemic`, `funcheck`, `funboost`, `bidders`, `upgrades`, `botdiag`)를 '결정 완료' 표시만 할지 지울지. 일부는 예전 규칙(72h 경매)을 전제 | `Tools/SettlementSim/` | 작음·없음 |
| M21 **[결정]** | `Tools/desert_balance_sim_v08.py`: 같은 계산이 C#에 있어 실행할 일이 없다. 기록으로 둘지 지울지 | `Tools/` | 아주 작음 |
| M22 | 시뮬레이터 csproj가 Progression 파일을 하나씩 나열 → 새 파일이면 빌드가 깨질 수 있다. 폴더 단위로 바꿀 수 있는지 확인 | `SettlementSim.csproj` | 작음·낮음 |

## 4. 추천 순서

1. **A(M1~M5)** — 안전장치. M1만 값이 바뀌므로 먼저 결정.
2. **M12·M13**(결정 후) — 혼동의 큰 원인(화면 두 벌, 씬과 코드 불일치)을 없앤다.
3. **B·C·D 나머지** — 기능 작업 사이사이에. M11(연혁 주석)은 따로 하지 않고 손대는 파일마다.
