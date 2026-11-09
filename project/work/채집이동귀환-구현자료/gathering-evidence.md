# 선택 당시 채집 묶음 구현 근거

기준 소스는 CoreKeeperModSDK 5cc5a43이며 구현 위치는 격리 작업본 `C:/Dev/worktrees/corekeeper-expedition`이다. 합의는 [확정 합의](../2026-11/2026-11-07-채집이동귀환-확정합의.md)의 선택 당시 보이는 묶음/개별 병행, 새 대상 분리, 실제 확보량 구분을 따른다.

## 행동 판단 그래프 실제 조회

현재 `게임행동-그래프/graph.py`의 게시본 기본 조회를 사용했다.

- `find WallDirtBlock` → [조회 원문](graph/gather-wall.json). source `CAB-2ebcba97367158c9b08f790c25d642c9:6681704455517238174`의 place 관계는 아이템 자체와 타일 파괴를 동일시할 근거가 아님을 확인했다.
- `find Pick` → AncientMiningPick의 `tool_for` 관계에서 `completion:B:melee-pick-2-0:cost-followup-v1` 확인. [show 원문](graph/gather-pick-cost.json)의 조건은 실제 장비·입력·쿨다운·적중과 피해·대상 상태가 동적임을 구분한다. `status=independent_pass`, `runtimeTested=false`이며 정적 경로를 실제 성공으로 승격하지 않는다.
- `source completion:B:melee-pick-2-0:cost-followup-v1/condition/conditions` → [원문 역조회](graph/gather-pick-source.json). `full/B/evidence/combat-followups-v2-replay-1/cases.json#/cases/75/conditions`, 값 SHA256 `c16abe006ebab5b88f8045039bab89e3a09c334fabd345a746e2aa15a4348943`.

위 근거에 따라 그래프 존재만으로 실행능력이나 획득 보상을 부여하지 않는다. 현재 생산 후보의 mine/root/break/pickup만 묶고 실제 장비/경로/입력은 기존 기술의 시작·실행 조건에 맡긴다. 파괴 대상 수와 관측 가방 증가를 별개 기록한다. 그래프 전체를 런타임 모델 입력에 넣거나 지식 공개 정책을 변경하지 않는다.

## 코드와 연결점

- `Assets/AIPlayerSpike/Scripts/Core/AIGatheringRules.cs`: 같은 실행 종류·원본 종류·변형이 같고 서로 접한 가시 타일의 연결 묶음. 대상 수 상한 없음. 양의 관측 가방 변화 누적 규칙.
- `Assets/AIPlayerSpike/Scripts/AIGathering.cs`: `AddCandidates`는 실제 후보와 시야를 재확인해 원본 단일 후보를 보존하며 2개 이상 묶음을 추가한다. 광석은 관측된 ore의 품목, 물체는 entity/version과 원본 ID/variation, 바닥 물품은 별도 실제 entity snapshot, 벽/뿌리는 위치·tileset/ore 서명으로 고정한다.
- `AIGatherSelection`/`AIGatherTarget`: 외부 수정이 불가능한 선택 당시 스냅샷. 새로운 대상은 자동 편입하지 않는다.
- `AIGathering.Make`/`Stale`, `AIGatherRuntime.Check`: 선택의 캐릭터 및 남은 정확 대상 재검증. 새 entity/version·다른 원본·시야 이탈은 해당 선택의 실행 근거가 되지 않는다.
- `AIGatherSkill`: 기존 MineSkill/RootSkill/BreakSkill/GotoSkill을 접근·채집·회수 순서로 실행하고, 자식 실패 이유를 보존한다. 회수 접근 이후 정상 게임의 자동 줍기 처리 관측 시간을 둔다. `Received`는 실제 관측 가방 증가이고, `CompletedTargets`와 구별한다. 인근 자동줍기가 섞일 수 있으므로 특정 파괴의 인과적 보상이라고 주장하지 않는다.
- `RemainingSelection`: 전환 당시 아직 처리하지 않은 기존 대상을 보존한다. 다시 선택하면 이미 없어진 대상은 건너뛰고 남은 정확 대상만 실행한다. `Cleanup` 후 새 자식 동작을 시작하지 않는다.
- 기존 농사 harvest는 AIFarm.Selection의 고정 타일 및 FarmSkill의 연속 실행을 재사용하는 별도 생활 행동이다. 새 묶음은 실제 직접 생산된 mine/root/break/pickup 대상으로 제한한다.

## 검증

`./Tools/gather-tests/run.ps1`: 생산 `AIGathering.cs` 전체, `AISkill`, 순수 규칙을 직접 링크한다. ECS/타일/물리 기술만 제어 가능한 경계로 제공한다. 실제 AddCandidates → Stale → Make → runtime adapter → sequence → 가방/대상 결과가 연결된 오프라인 흐름을 실행했다. 32 checks PASS. 301대상 상한 없음, 새로 드러난 대상 제외, 정확 entity/version·벽 서명, 채집/회수 순서, 중단과 남은 대상 재개, 실패, 무획득 파괴, 남아 있는 바닥 물품을 포함한다. 기존 개별 채집에서 이미 회수했다면 추가 접근/대기를 하지 않고 다음 선택 대상의 실행을 잇는다. 해당 위치에 보이는 잔여 드롭이 있을 때만 회수 접근을 추가한다.

`dotnet build Tools/compile-check -c Release --nologo`: 설치된 게임 어셈블리 대상 C#8 전체 모드 컴파일 경고0/오류0 PASS(당시 공유 작업본 기준). 실제 Unity/게임 실행·외부 모델 호출은 하지 않았다. 통합 담당의 최신 전체 컴파일·회귀·독립 검수는 별도이다.

## 검수 보완: 명시 상한과 새 발견

합의3의 단순 목표량과 사용자가 명시한 엄격 상한을 분리했다. `count`는 목표량 그대로이며 `max_count`가 생략되면 제한이 없다. `Tools/ai-bridge/protocol.mjs`의 shape 검증과 `brief.md` 지시 → `AIPlanProtocol`의 GoalMaxCount/PlanStep.MaxCount → `AIPlanner.CheckGoal/StepForm/SetGoalFrom` → `AIGoal.maxCount`, `AIPlanReport` 및 `AIGoalText`가 이를 보존한다. Gather 장소도 `AIGoals.SetGoal`에서 보존하고 목표 identity 교체 직전 `AIExpedition.Observe`로 이전 실제 확보량을 관측한다.

HEAD 수용 판단은 불확정 단일 채집 최대량 또는 남은 허용량보다 큰 불가분 물품이면 새 동작을 보류하고 사실·실제 대안·재계획에 제공한다는 것이다. `AIGatheringRules.LimitReason`, `AIGathering.LimitReason/LimitNote`와 `AIGatherSkill.Tick`의 매대상 시작 경계가 이 계약을 구현한다. 바닥 묶음의 MaxYield는 선택 당시 실제 stack 관측치이며 증가한 스택은 재검증에서 거부한다. 직접 pickup도 실행 재검증 때 현재 스택을 관측한다. 광맥 등의 최대 산출량은 임의 숫자로 추정하지 않으며 unknown으로 보류한다. 원래 목표·남은 선택은 유지한다. 게임 자동줍기까지 최종 절대 수량을 보장한다는 주장은 하지 않는다. 부모 통합의 `AIActions.Stale`·`AIExpedition.Compose`·진행중 명시조건 재검증이 개별/묶음 생산 경계를 연결한다.

기존 running watch는 적·HP 등의 위험만 처리하여 묶음 도중 새로운 자원을 판단하지 못했다. `AIObservation`의 기존 가시타일 순회에 rootTiles를 추가했고, `AIGatherSkill.Start`는 초기 가시 자원 baseline을 보존한다. 부모 `AIBrain.Loop`의 기존 watch가 full tiles 관측을 제공하면 `ObserveDiscoveries`가 새 ore/root/바닥 물품/물체만 한 번씩 `ObserveExecution`으로 보고한다. 실행 몸은 유지되고 원 선택에는 추가하지 않는다. 선택 대상 자리에서 생기는 회수 드롭은 해당 회수 맥락이므로 새 곁일 발견으로 반복 질의하지 않는다.

최신 오프라인 증거: gather 생산경로 51 PASS(9/10+5대상 매타깃 경계, 일반 목표 10→14, unknown/불가분 보류, 직접 pickup 재관측, 새 ore/root 발견 중복억제·body유지·비확장 포함), Core GATHER-LIMIT 13 PASS, bridge max_count 14 PASS, life 생산 adapter 100 PASS(실제 Protocol→SetGoalFrom→Goal max/where 및 무제한 목표 포함), maintenance 115 PASS. 설치 게임 어셈블리 전체 compile-check 경고0/오류0 PASS. 실제 게임·외부 모델 성공을 의미하지 않는다.

## R4 재검수: 실제 파괴 입력에서 상한 우회 차단

`progress`와 후보 `itemId`는 산출품 계약이 아니다. `AIGathering.WallLimit/RootLimit/ObjectLimit/HarvestLimit`가 현재 보이는 실제 wall/ore/entity/ripe crop를 재관측하고, 다른 품목만 산출된다고 확정할 근거와 최대 산출계약이 없다는 사실을 명시한다. 보이지 않는 대상은 현재 원본을 공개하지 않고 재검증 불가로 보류한다. 다른 원본 ID를 다른 산출품 확정으로 오인하지 않는다. 명시 상한이 없으면 기존 실행을 그대로 허용한다.

입구 대조: AIActions mine/edgemat 및 AIBody의 직접 mine 명령 → MineSkill.Start/Tick; root → RootSkill.Start/Tick; break(자연물/시설/야생작물 포함) 및 직접 break 명령 → BreakSkill.Start/Tick; dig → TunnelSkill.NextStep의 매벽 Mine 자식 생성 직전 및 그 자식 Start/Tick; gather_group → 같은 실제 기술; farm harvest → FarmSkill.Start 및 반복 secondary press를 포함한 Tick 경계. farm의 hoe/plant/water/fill은 이 harvest 검사 대상이 아니다. 무파괴 순수걷기 경로는 허용하며, 걷기 후 새 계획에 벽이 나타나면 그 다음 파괴 자식 직전에 검사한다.

후보 재검증도 dig의 실제 경로벽, edgemat의 실제 벽, farm harvest의 실제 tiles/PlantCD를 검사한다. 후보 false progress/itemNone 또는 선택괭이 itemId로 우회되지 않는다. PlantCD 기본 수량은 보너스를 포함한 최대 산출량으로 승격하지 않는다.

오프라인 재현: `Tools/gather-tests/native/run.ps1 -Baseline`은 e1cedcf의 실제 MineSkill/TunnelSkill을 추출·컴파일하여 명시 max 상태에도 native primary가 나오는 결함을 재현했다. 수정판 동일 harness는 native 입력 경계 14 PASS. gather 후보/묶음 55 PASS, life 105 PASS(생산 harvest helper/실제 FarmSkill secondary 억제 5개 신규 포함), compile-check 경고0/오류0 PASS. 게임 물리/외부 모델은 호출하지 않았다.

## 원부탁 수명에 따른 명시 상한 소비

HEAD 후속 결정에 따라 상한은 Gather 단계가 아니라 원래 HostOrder의 품목별 조건이다. 선택적 `max_order` 양의 ID를 bridge/AIPlanProtocol/PlanStep/AIPlanner/AIGoal/report에 보존한다. 값 형식과 max_count 동반 여부는 form 단계에서 검증한다. Planner set의 carried 원부탁 목록이 결정된 뒤 `AIExpedition.BindPlanLimits(steps,carry)`로 소유를 확정한다. 불명확하거나 허용되지 않는 변경이면 계획·몸을 교체하지 않고 거절 사유와 retry를 남기며, 성공 후에만 accepted/plan 설정을 진행한다.

AIExpedition의 실제 원부탁 소유·수명 구현은 부모 담당이다. 채집 실행은 현재 GoalKind나 goal.maxCount를 더 이상 조건 소유로 사용하지 않고 `ActiveLimits()`의 모든 OrderId/Item/Maximum/Acquired 조건을 소비한다. 따라서 같은 원부탁의 Home/Store/정비에서도 유효하며 다른 부탁에 대한 적용 여부는 owner 서비스가 결정한다. 명확한 pickup은 해당 품목 조건만 적용하고 unknown native 파괴는 유효 조건이 하나라도 있으면 보류한다. 묶음은 owner+품목별 시작 누적 및 실행 중 실제 receipt를 함께 사용하여 한 Tick 안의 연속 대상에도 재검증한다.

소비자 검증 최신: gather 61 PASS(Home 단계·두 소유/품목 조건·다른 품목 pickup 비전파·owner 해제 포함), native 14 PASS, life 105 PASS, Core GATHER-LIMIT 22 PASS, bridge 상한14+owner11=25 PASS, 게임 어셈블리 compile 경고0/오류0 PASS. 컴파일은 부모의 Core/AIExpedition API 구현이 존재하는 공유 작업본 기준이며 소비자 커밋 단독의 자체완결을 주장하지 않는다. 실제 HostOrder 소유/미래 단계 즉시 등록/완료·취소 수명 통합검증은 부모 execution suite에서 수행한다.
