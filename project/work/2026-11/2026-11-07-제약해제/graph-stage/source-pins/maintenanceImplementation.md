# 장비 정비 트리 구현·자체 검증 결과

## 고정 후보와 범위

- 작업트리: `C:/Dev/CoreKeeperModSDK-maintenance-tree`, 브랜치 `maintenance-tree-20261107`.
- 고정 기반: `c4a0f5e38016c79567f880bbaea5e56e0eeae347`.
- 최종 후보: `b1f5f4290c97d5b68dc64e5cce6b09bc1aa29635`. 중간 후보 `df9a1eab589017ea84cbb3d2e8d4e35c397e9dac` 뒤에 M1/M2와 직접 영향 회귀를 포함했다.
- 일반 수리·단일 레벨 업그레이드·자기 pending 회수를 구현했다. 내구 강화 true, 일괄/최대 레벨 반복, 자동 가공·거래·낚시·설치는 추가하지 않았다.
- 제품·Tools 변경은 후보 커밋으로 고정했다. Packages/ProjectSettings/SDK assembly 사본과 Unity가 생성한 환경 파일은 제품 커밋에서 제외했다. 준비 근거는 [03-environment.md](03-environment.md)에 있다.
- 작업 도중 진행한 공식 main을 병합하지 않았다. 동시 진행한 가방 트리·렌더 소유권·전투 탐색 정지와의 최종 통합은 HEAD의 별도 통합 경계로 남긴다. 공식/다른 담당 worktree와 그래프 정본은 수정하지 않았다.

## 실제 연결

`Core/AIMaintenanceRules.cs`는 maintain 형식, 단일 count와 수리/업그레이드 결과·요청 처리 조건을 Unity 없이 판정한다. GoalKind/GoalFeature는 기존 번호를 보존하고 끝에 Maintenance를 추가했다. PlanProtocol→PlanStep→StepForm/CheckGoal→SetGoalFrom→AIGoals에 기존 mode/item/count/where/slot을 전달한다. 누락 count만1이며 명시 null/-1/문자열/분수/false/2이상과 reinforce/keep/second는 거절한다. 현재 단계는 실제 품목·슬롯과 알려진 시설을 확인하고, 거리/재료 부족은 현재 사실로 보고한다.

AILife는 maintenance root와 기존 mode→정확 슬롯→시설→실행 탐색, 페이지5개/back/root/close, 읽기 전용 응답 선처리를 재사용한다. `AIMaintenance.LifeEntries/ExactCandidate`가 실제 슬롯 품목을 해석한 뒤 `AILife.Resolve`로 목표 일치와 수량을 재판정한다. 다른 장비/시설은 별도 행동1회이며 원래 진척에 합산하지 않는다. 직접 지정과 트리 말단은 같은 생산 입구를 사용한다. `AIActions`의 maintain 행은 고정 MaintenanceRequest를 Make/Stale에 전달한다.

`AIMaintenance.ReadCosts`는 AI player의 PugQuerySystem 공개 lookup과 실제 InventoryUtility 비용 helper를 호출한다. 수리는 선택 variation primary/현재 내구도·isRepairing=true/isReinforcing=false, 업그레이드는 실제 유효 레벨+1/UpgradeCostsTable을 쓴다. NativeList를 Dispose하고 자기 main 재료만 보유량으로 대조한다. 비용/조건이 바뀌면 이전 요청을 대체 실행하지 않는다. source8555 비용을 일반 비용식으로 하드코딩하지 않았다.

`AICraft.QueueCraftAction`과 strict InventoryChange overload는 UIActionBuffer/UIActionsCD/NetworkTime을 모두 요구하고, 정상 다음 server tick부터 미적용 항목과 충돌하지 않는 tick에 게시한다. 정비는 기존 일반 제작의 CraftBuffer fallback을 쓰지 않는다. 수리는 정상 RepairOrReinforce false/free=false, 업그레이드는 `Create.Upgrade(forge, player, forge, inputAbsoluteSlot, itemId, 1, false)`를 쓴다. 이 7인수 시그니처 정정은 HEAD가 명세와 승인 기록에 반영했다.

업그레이드는 기존 InventorySkill/TransferTick의 정상 MoveAmount로 정확 source1개를 forge local0의 실제 absolute 슬롯에 넣고, 출발 감소/입력 증가를 확인한다. 단일 Upgrade→실제 예상 variation과 재료 관측→정확 main 빈 슬롯 회수→양쪽 receipt가 있어야 work1/received1이다. 원래 착용 슬롯에 자동 복원하거나 드롭 fallback을 쓰지 않는다. 용량 제공 장비 제거는 기존 CapacityAfterSwap의 대체 슬롯 계산을 공통화한 CapacityAfterRemoval로 확인한다.

pending은 캐릭터·world/player Entity/Version에 묶인 임시 상태다. source/시설·full snapshot·원래 goal 참조/revision·실제 레벨 변화·게시 tick/종류/payload·각 단계 근거를 유지한다. 목표 변경·stop_action·Dispose 뒤에도 게시/투입된 건은 남는다. 다음 조건을 모두 확인해야 회수/다음 요청이 가능하다: posted보다 새 server tick, lastActionTick의 적용 확인, dependency를 완료한 해당 player/target/index/action 소비 버퍼의 부재, 현재 정확 input snapshot. UIActionBuffer의 과거 항목이나 경과시간을 재실행/성공 근거로 사용하지 않는다.

시야 밖 forge buffer를 현재 상태로 읽지 않으며, 기억한 시설은 가서 확인하는 이동만 제공한다. 처리 미확인 요청의 원래 슬롯과 실제 이동 endpoint만 명시 InventoryMove에서 보호하고 다른 물품·이동·전투 전체를 잠그지 않는다. pending을 타인의 기존 forge 내용에서 새로 채택하지 않는다. AIBrain.Reset/world 경계에서는 임시 근거를 지우며 목표 변경만으로 지우지 않는다.

## M1/M2와 정상 소비자 경계 수정

- **M1:** 정상 로컬 큐 등록/게시도 없고 실제 투입 확인도 없는 intent만 Cleanup에서 제거한다(`Posted=false && PutConfirmed=false`). 이미 등록/게시하거나 투입된 건은 보존한다. 이 분기는 HEAD의 명세 보충 승인에 따랐다. 등록 전 실패/중단 뒤 새 Exact가 가능하고, 게시 후에는 처리 proof 전까지 busy가 유지되는 직접 회귀를 통과했다.
- **M2:** expected 레벨을 처음 관측하면 그 forge의 full snapshot을 동결한다. 같은 ID/aux의 원본 variation이나 같은 variation의 다른 내구도/updateCount를 다시 채택하지 않는다. 교체되면 fresh recover와 오래된 실행 모두 차단한다. 업그레이드 work1은 bool/expected끼리의 비교가 아니라 실제 정확 수령 snapshot의 variation과 예상 레벨을 비교한다.
- **정상 Move stamp:** InventoryUtility.TryMove:607~645는 목적 슬롯의 기존 variationUpdateCount를 유지하고 ID/variation/amount/aux만 옮긴다. source 전체 snapshot과 main 수령을 그대로 EqualsExact하면 정상 회수도 막히므로, 출발의 동결 full snapshot 검사는 유지하면서 main receipt는 실제 정상 필드 전이를 대조한다. fixture도 같은 전이로 고쳤다. source VUC2→forge0, 업그레이드 후 forge1→main0에서도 레벨7/work1/received1이며, M2 교체는 계속 차단한다. 수정 전 실패 발췌는 [maintenance-normal-move-before-fix.log](logs/maintenance-normal-move-before-fix.log)에 구별해 남겼다.

중단 뒤 같은 원래 upgrade goal 참조/revision의 이미 강화된 pending 회수만 그 단계를 한 번 마무리한다. 미강화 원본 회수와 목표 변경 뒤 회수는 이전/새 upgrade 진척을 올리지 않는다. 구조화 ILifeResult와 AILife.Ended의 run당1회 기록을 재사용하며 별도 중복 결과 집계는 추가하지 않았다. 완료는 관측이며 finish_goal/order=done의 기존 주체를 유지한다.

## 실행한 직접 영향 검증

| 검증 | 결과 | 근거 |
|---|---|---|
| Core: 새 규칙·공유 Goal/Plan/Protocol 호환 | 1,393/0, exit0 | [core-tests.log](logs/core-tests.log) |
| bridge protocol·공유 maintain fixture | 188/0, exit0 | [bridge-protocol.log](logs/bridge-protocol.log) |
| 기존 공통 실행 실제 입구 | 36/36, exit0 | [execution-regression.log](logs/execution-regression.log) |
| 생활 후보·nav·공통 진척 직접 영향 | 96/0, exit0 | [life-regression.log](logs/life-regression.log) |
| 기존 제작/준비 및 공유 queue·Inventory 호환 | 19/0, exit0 | [craft-final.log](logs/craft-final.log) |
| 기존 가방 이동·용량·슬롯·receipt 직접 영향 | 91/0, exit0 | [inventory-final.log](logs/inventory-final.log) |
| 새 정비 실제 생산 파이프라인·교차 경계 | 88/0, exit0 | [maintenance-tests.log](logs/maintenance-tests.log), [summary](logs/maintenance-tests-summary.json) |
| 실제 SDK assembly compile-check | 경고0/오류0, exit0 | [compile-final.log](logs/compile-final.log) |
| 최종 고정 SHA Unity6000.0.59f2 CompileCheck | exit0 / CS오류0 / CompileCheck:ok 1건(1816행) | [unity-final.log](logs/unity-final.log), [exit](logs/unity-final-exit.json), [summary](logs/unity-final-summary.json) |

명령은 각 Tools README와 아래 명령을 따른다.

```powershell
dotnet build Tools/compile-check -c Release
dotnet run --project Tools/core-tests -c Release
node Tools/ai-bridge/test/protocol.test.mjs
pwsh -NoProfile -File Tools/execution-tests/run.ps1
pwsh -NoProfile -File Tools/life-tests/run.ps1
& Tools/life-craft-tests/ExtractLifeBinding.ps1
dotnet run --project Tools/life-craft-tests -c Release
& Tools/life-inventory-tests/ExtractBase.ps1
& Tools/life-inventory-tests/ExtractLifeBinding.ps1
dotnet run --project Tools/life-inventory-tests -c Release
pwsh -NoProfile -File Tools/maintenance-tests/run.ps1
```

새 대표 harness는 실제 AIMaintenance/MaintenanceSkill·AICraft·AIInventory/InventorySkill·AILife를 직접 링크하고, 기존 방식으로 실제 Protocol/Plan/Goal·후보 조립 입구·HandleResponse/Apply/OnSkillEnded·AIActions 분기를 추출한다. 정상 Create 생성 본문도 로컬 게임 원문에서 추출한다. UI/ECS lookup·비용 helper 반환·소비자만 경계로 둔다. 정상 false 수리와 main12→forge absolute5(local0)→단일 Upgrade→variation6→7→main 회수, 부분 변화, 정확 variation/aux/stamp·active preset·비용 변경, 최대 상태/가방 공간/남의 forge, 게시 전후 stop/목표 변경, UI/소비 버퍼 지연, nav prefetch 보존, owner/world reset을 실행했다.

비용 helper 표본은 source8555:0의 primary max90/기본 level6→7, IronBar1002×10/AncientCoin2021×132 원문 조건과 실제 공개 호출 인수·Dispose를 대조한다. 테스트의 고정 반환 행이나 수동 소비 경계를 native helper/게임 소비자의 실제 실행이라고 주장하지 않는다. 실제 공개 시그니처는 SDK/Unity 컴파일이 확인한다.

완료한 첫 단위 그래프 검수와 동일 baseline 전수 조사는 반복하지 않았다. 공유 Goal/Plan/Protocol/Inventory를 이번에 바꿔 직접 영향받는 호환 suite를 실행했고, 실패나 실제 코드 수정 뒤에 필요한 표본만 재검증했다.

새 정비 88개 표본은 후속 소스를 아직 커밋하기 전 실행했으므로 원래 summary의 run 당시 HEAD는 df9로 남겼다. 기록한 생산 4파일 SHA256을 최종 b1f5 작업트리와 대조해 모두 같음을 확인했으며, [final-validation.json](logs/final-validation.json)에 최종 후보와의 정합성을 별도로 기록했다. 같은 소스의 성공 검증을 SHA 표기만 바꾸려고 반복하지 않았다.

Unity는 다른 배치가 없는 것을 확인하고 `-batchmode -quit -projectPath C:/Dev/CoreKeeperModSDK-maintenance-tree -executeMethod HeadlessSdk.CompileCheck`만 실행했다. 초기 Library import 배치 PID46212는 자연 종료 exit0이었고, 이후 최종 커밋 고정 뒤 PID3408로 다시 실행해 sourceCommit b1f5/exit0/CompileCheck:ok/CS오류0을 함께 확인했다. 최종 종료 시 Unity.exe가 없음을 확인하여 별도 통합 배치와 겹치지 않게 HEAD에 전달했다. 초기 import 당시에는 후속 소스가 작성 중이었으므로 초기 로그를 최종 SHA의 증거로 대신 사용하지 않는다.

## 미확인과 전달 경계

실제 모델/API, 전체 AIBrain.Tick 스케줄러와 CandBuild 경로 탐색, 실제 게임 소비자·외부 동시 조작·설치·실게임은 실행하지 않았다. 정상 소비자는 주변 자동이체 상자를 추가할 수 있으므로 큐 직전 필요한 재료 전량을 자기 main에 검사하고 실제 main 감소를 관측하지만, 외부 조작을 막는 원자적 거래/예약을 제공하는 것은 아니다.

pending은 이번 월드 세션의 임시 근거이며 재입장 뒤 기존 forge 물품의 소유를 복원하지 않는다. source/시설이 외부에서 교체되면 임의 대체 회수/새 물품 투입을 하지 않고 불일치 사실을 남긴다. 본 후보는 독립 수정 검수와 별도 main 통합을 기다리는 격리 구현이다. 공식 반영·그래프 원본 갱신은 HEAD가 해당 변경분과 수용 지위를 확인해 수행한다. BuildAndInstall, 게임 실행, 기존 게임/Unity/브리지 프로세스 종료는 하지 않았다.
