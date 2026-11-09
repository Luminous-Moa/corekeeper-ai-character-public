# 개발 지식그래프 인계

2026-11-08 최신 상태: [사용자 범위 결정](../2026-11/2026-11-08-채집이동귀환-조명후속분리.md)에 따라 조명 시야 R1은 별도 후속이다. HEAD가 본 소스 C:/Dev/CoreKeeperModSDK의 ai-character를 7af8f91로 통합했고 기존 dirty 파일 해시 보존을 [integration.json](integration.json)에 기록했다. 개발 그래프 정본 반영은 별도 담당 진행 중이며 완료 대기다. 아래 검증 기록은 그대로 재사용하며 제품코드 수정이나 재검증은 하지 않았다.


후보 commit `7af8f91e5d26e4fd9ce5996809ae41e213340118` (출발 `5cc5a43`). `feature/expedition-20261107`, `C:/Dev/worktrees/corekeeper-expedition`. 구현자는 게시 그래프를 수정하지 않았다. 기존 HEAD의 정본 절차대로 목적→확정합의→모듈/코드→검증을 추가하되 아래 미해결 시야를 완료 노드로 승격하지 않는다.

## 합의와 코드 앵커

- 확정합의1 직접/트리 → `AIExpedition.cs:87 Compose`, `:132 Stale`, `:141 Navigate`, `AIBrain.Loop.cs Apply expedition_nav` → execution92.
- 확정합의2 곁일/원작업/원부탁 → `AIExpedition.cs:9 WorkRecord`, `:147 Starting`, `:195 Report`, `Core/AIExpeditionRules.cs ExpeditionLedger/ExpeditionCursor` → execution92.
- 확정합의3 목표/명시상한/귀환 → `AIGathering.cs:127 LimitReason`, `AIExpedition.cs:70 EnforceExplicitLimit`, `AIGoals.SetGoal`, `Core/AIPlanProtocol.cs`, `Core/AIPlanRules.cs PlanStep.MaxCount`, `AIPlanner.SetGoalFrom/OnGoalEnded/StartNextStep`, `Tools/ai-bridge/protocol.mjs` → gather61/core22/bridge25/life105/execution92.
- 확정합의4 고정묶음/실제수령 → `AIGathering.cs:9 AIGatherTarget`, `:25 AIGatherSelection`, `:38 AddCandidates`, `:180 AIGatherRuntime`, `:249 AIGatherSkill`, `Core/AIGatheringRules.cs` → gather61.
- 역할판단의 탐색/목적지/선행예외 → `AITravel.cs:42 ObserveView`, `:78 KnownPath`, `:87 Compose`, `:122 TryApplyTurn`, `:133 TravelSkill`, `Core/AITravelRules.cs:47 PassageExits`, `:91 NextJunction`, `:116 KnownPath`, `AINavigator.cs:332` → travel43/navigator9.
- 자동상위보고 두 사건 → `AIExpedition.cs:147 Starting`, `:177 AfterBuild`, `:188 Sent`, `:189 Accepted`, `AIPlanReport.ReportJson expedition_observation` → execution92.
- 사용자 지식공개 → 일반 관측/가시성 API 재사용, 선행예외 읽기를 지도·자원사실로 저장하지 않는 `TravelSkill.Forecast` 경계. 단 **AIVision의 조명 미반영 R1은 사용자 결정으로 별도 후속**. vision-authority-investigation.md에 게임 원본 근거/대안이 있다.

## 신규·변경 공개 계약

- `AIExpedition.Compose(World,Entity,AIObservation,List<AICandidate>)`, `Stale(AICandidate)`, `Navigate(AICandidate)`, `Resolve(AICandidate)`, `Starting(World,Entity,AICandidate,AISkill,float)`, `Observe(World,Entity)`, `Ended(AISkill)`, `AfterBuild(World,Entity,List<AICandidate>,bool)`, `Gathered(AIGoal,int)->long`, `EnforceExplicitLimit(World,Entity,float)`, `Sent(int)`, `Accepted(int,string guidance=null)`, `Report()->string`.
- `AIGathering.AddCandidates(World,Entity,AIObservation,List<AICandidate>)`, `Make(AICandidate)`, `Stale(World,Entity,AICandidate)`, `LimitReason(World,Entity,AICandidate)->string|null`, `LimitNote(World,Entity)->string`.
- `AIGatherSelection` 고정 Targets 사본/Key/Player, `AIGatherTarget.MaxYield`와 optional ctor `maxYield=-1`. `AIGatherSkill.Selection`, `RemainingSelection`, `Received`, `CompletedTargets`, `ObserveDiscoveries(AIObservation)`.
- `AITravel.Compose(...,float responseSeconds=1.5f)`, `Make`, `Stale`, `TryApplyTurn(World,Entity,AICandidate,out string)`; 내부 `KnownPath(...,float tolerance=0f)`. `TravelSkill.ValidateTurn`/`ReserveTurn`은 같은run의 다음갈림길 방향만 예약.
- `PlanStep.MaxCount` 및 ctor 마지막 `maxCount=-1`, `PlanAnswer.GoalMaxCount=-1`, `AIGoal.maxCount=-1`, `AIGoals.SetGoal` full overload 마지막 `maxCount=-1`; wire `max_count`는 gather 전용 선택값. 생략은 무상한. 기존 count 의미 불변.
- `AIObservation.rootTiles: List<(int2 p,int tileset)>`는 기존 가시타일 순회의 관측이며 새 지식 권한을 주지 않는다.
- `AICandidate.expeditionNav/resume/gather`; `AISlot.Expedition=19`, `AISlot.Travel=20`, `Count=21`.

## 사용한 게임행동 그래프 조회와 원문근거

`work/게임행동-그래프/README.md`의 게시본 CLI로 실제 조회했다. 정적 게임가능성 근거이며 runtimeTested=false를 성공으로 바꾸지 않는다.

1. `find WallDirtBlock` → source `CAB-2ebcba97367158c9b08f790c25d642c9:6681704455517238174`; `graph/gather-wall.json`. place 아이템 관계를 채굴수령으로 오해하지 않음.
2. `find Pick` → AncientMiningPick tool_for → `show completion:B:melee-pick-2-0:cost-followup-v1`; `graph/gather-pick-cost.json`.
3. `source completion:B:melee-pick-2-0:cost-followup-v1/condition/conditions` → `full/B/evidence/combat-followups-v2-replay-1/cases.json#/cases/75/conditions`; SHA256 `c16abe006ebab5b88f8045039bab89e3a09c334fabd345a746e2aa15a4348943`; `graph/gather-pick-source.json`. 실제 장비/정상입력/적중/피해/상태는 동적이므로 관측수령을 별도 기록.
4. `find WoodDoor` → `show door`; `movement-door-query.json`. toggle_door의 variation/입력/조건을 기존 문 실행과 연결, 열린문/닫힌문을 단순 영구벽으로 동일시하지 않음.
5. `find WoodBridge` → `show completion:B:place-tile:CAB-2ebcba97367158c9b08f790c25d642c9:-1592303294285069928`; `movement-bridge-query.json`. ObjectID4703/variation0, pit/water 필요·ground불가, 배치검사/소비/실제타일결과 구분. 막힘만으로 다리성공/자동생성하지 않음.

## 검증 노드와 보류

`coverage.md`가 합의별 code/evidence/missing 표다. `validation.md`에 최종 명령/로그/컴파일 결과를 추가한다. 핵심 흐름은 production `Apply`→선택된 body→관측/원정 report→production `RequestReplan/Report`→offline transport, 그리고 production `finish_goal`→`OnGoalEnded`→next-tick branch→`StartNextStep`→`SetGoalFrom`→다음 goal이다. 물리/ECS fixture와 offline transport는 경계이며 게임실행/외부모델 성공이 아니다.

R1은 2026-11-08 사용자 결정으로 별도 후속에 분리됐다. R2~R7은 독립 검수 수용됐고 본 소스는 7af8f91로 통합됐다. 개발 그래프 정본 반영은 별도 담당이 진행 중이며 완료 대기다.

## 최종 7af8f91 앵커 보정·추가

위 기능별 메서드 이름 연결은 유지하되 최종 줄 번호는 아래를 사용한다.

- `AIExpedition.cs`: WorkRecord9, Observe77, Gathered92, EnforceExplicitLimit97, BindPlanLimits107, ActiveLimits127, Compose150, Stale197, Navigate206, Resolve211, Starting212, Choosing227, Ended242, AfterBuild249, Sent260, Accepted261, Report267.
- `AIGathering.cs`: WallLimit182, RootLimit192, ObjectLimit200, HarvestLimit223, AIGatherSkill327, ObserveDiscoveries366. 해당 검사들은 실제 Mine/Root/Break Start·Tick, Tunnel.NextStep 매벽, FarmSkill.Start/Tick 수확 입력과 연결한다. native14·life105 수확음성경계로 검증한다.
- `Core/AIExpeditionRules.cs` 신규 `ExpeditionLimit { int OrderId, Maximum; string Item; long Acquired; }`. `AIExpedition.BindPlanLimits(List<PlanStep>,List<HostOrder>)->string|null`은 모호소유·동일부탁 상한변경을 거절하며 유효한 미래단계 조건까지 등록한다. `ActiveLimits()->IReadOnlyList<ExpeditionLimit>`는 현재 수행하는 원부탁/현재몸의 소유에 해당하는 열린 조건만 반환한다.
- `PlanStep.MaxOrder`, `PlanAnswer.GoalMaxOrder`, `AIGoal.maxOrder`, full SetGoal의 마지막 `maxOrder=-1`, wire `max_order`. 양의 원부탁 ID이며 max_count와 동반한다. 생략은 단일 원부탁에서만 해석. 상위 목표 count와 사용자 조건은 다른 소유다.
- 원부탁 완료/취소 전에는 계획단계/계획 source 변경·보관·정비·미룸으로 조건을 해제하지 않는다. 별도 원부탁에 상한이나 확보량을 전파하지 않는다. 미래단계 즉시등록·모호소유 거절·별도부탁/획득 격리·source변경·미룸재개·완료해제를 execution92가 실제 생산 planner 적용 경로로 검증한다.
- R5 최종 생산자 분류 `378561e`와 `Tools/travel-tests/candidate-progress`의 실제생성→HasProgress→보고11. ordinary 탐험 후보는 삭제하지 않는다.
- 같은 실행/continue/turn 선택에서 `Choosing`을 통해 이탈기회를 검토한다. actual AddEvent/pending dispatch/Report JSON필드/HTTP payload를 실행한 execution92가 이전 함수단위 증거를 보완한다.

최종 합의대조는 coverage.md 마지막 보완항목까지 포함한다. 시야 R1은 이번 수용 범위에서 후속으로 분리됐으며 조명 기능을 구현했다는 뜻은 아니다.
