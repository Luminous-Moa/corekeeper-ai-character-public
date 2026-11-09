# 장비 정비 생산 파이프라인 어댑터

저장소 루트에서 실행합니다.

```powershell
pwsh -NoProfile -File Tools/maintenance-tests/run.ps1
```

`run.ps1`은 현재 생산 파일을 읽어 `.generated`를 매번 다시 만듭니다. 추출 표식이 바뀌면 실패하며, 이전 생성본으로 우회하지 않습니다. 이 검증은 이전 생활 단위의 baseline/그래프 검수를 다시 수행하지 않습니다.

직접 링크하는 생산 소스는 `AIMaintenance`, `MaintenanceSkill`, 전체 `AICraft`, 전체 `AIInventory`/`InventorySkill`, `AILife`, `AISkill` 및 관련 Core입니다. 기존 life-tests 방식으로 실제 PlanProtocol/PlanStep→AIPlanner.SetGoalFrom→AIGoals, 후보 조립 입구, HandleResponse/ApplyOrHold/Apply/OnSkillEnded와 AIActions의 maintain dispatch/Make/Stale 본문을 추출합니다. 탐색/실행/큐 처리/비용 분기/결과 판단을 fixture에 복사하지 않습니다.

정상 게임 요청 생성은 로컬 게임 원문 `C:/Dev/_ref/corekeeper-decomp/Pug.Other/Inventory/Create.cs`의 `RepairOrReinforce`, 7인수 `Upgrade`, `MoveAmount` 등 생성 본문을 그대로 추출합니다. 따라서 targetInventory/craftingEntity=선택 forge, local0의 실제 absolute slot, reinforce/free=false와 정상 이동의 payload를 직접 확인합니다.

`WorldStubs`/`RuntimeBoundary`는 Unity ECS·lookup·NativeList·NetworkTick·DB와 public 게임 비용 helper의 경계입니다. 비용 helper는 고정 반환 행을 공급하며 실제 호출 인수(선택 primary/현재 amount, AI player/시설, main-only inventory, chestsStartIndex1, repairing/reinforcing flags, 다음 레벨)와 Dispose를 확인합니다. supplied `next/queries/repair-source.json`·`upgrade-source.json`도 읽어 source8555:0의 primary max90/기본 level6→7 및 IronBar1002×10/AncientCoin2021×132와 대조합니다. 이 반환 행을 모든 물품의 비용식으로 쓰지 않으며, native 게임 비용 helper의 실제 실행을 증명하지 않습니다. 공개 실제 시그니처와 SDK 컴파일은 별도의 compile-check/Unity 검증이 담당합니다.

`Fixture`는 게시된 UI 요청을 matching CraftBuffer/InventoryChangeBuffer에 옮기고 lastActionTick을 올리는 게임 소비 경계를 제공합니다. 요청과 별개로 지정된 target/material/level/양쪽 슬롯 변화를 공급합니다. UIActionBuffer 과거 항목은 유지하므로 소비 증거와 단순 큐 잔류를 구별할 수 있습니다. 실제 게임 소비자·원자적 거래·외부 플레이어 동시 조작을 실행한 증거가 아닙니다.

정상 이동 fixture는 `InventoryUtility.TryMove:607-645`와 같이 목적 슬롯의 기존 variationUpdateCount를 유지하고 ID/variation/amount/aux만 변경합니다. source vuc2→forge vuc0, 업그레이드 후 forge vuc1→main vuc0의 정상 전이에서도 작업1/확보1을 확인합니다. source 전체 snapshot을 목적 슬롯에 복사해 과엄격한 receipt 비교를 감추지 않습니다. 확인된 forge source의 전체 snapshot 동결과 다른 variation/상태 교체 차단은 별도로 유지합니다.

표본은 다음을 포함합니다.

- 실제 계획·트리 및 직접 지정 수리→정상 요청→실제 내구도/재료 변화→구조화 결과/다음 판단.
- 정확 장비→정상 forge 투입→한 단계 Upgrade→정확 main 회수, 장비만/재료만/미변화와 잔류 구분.
- owner/world/entity version/aux/variation/state stamp/활성 장비 preset/비용·거리·시야/최대 내구·레벨/회수 공간/용량 제공 장비 제거/남의 forge 입력 경계.
- 게시 전/후 stop·목표 변경, 미게시 intent 해제와 게시 intent 보존, posted 다음 tick·lastActionTick·matching consumer buffer 부재가 모두 있어야 하는 회수 장벽.
- 중단 후 같은 목표의 이미 강화된 물품 회수만 upgrade를 한 번 마무리하며, 원본 회수나 새 목표의 회수는 업그레이드 quota를 오염하지 않음.
- M2: 기대 레벨 snapshot 확인 뒤 같은 ID/aux의 원본 variation 또는 같은 variation의 다른 상태 stamp를 끼워 넣어도 재채택/Move/완료가 발생하지 않음.
- posted take의 실제 main 목적 슬롯을 포함한 정확 endpoint 장벽과 처리 확인 뒤 해제. 다른 슬롯은 예약하지 않음.
- 실제 shared nav와 prefetch가 현재 몸·held/queued·목표·실행 카운터를 보존함.

전체 `AIBrain.Tick` 스케줄러, 전체 CandBuild의 경로 계산, 실제 모델/API, 설치·실게임은 실행하지 않습니다. pending은 이번 월드 세션의 임시 근거이며 reset 뒤 기존 forge 내용의 소유를 다시 채택하지 않습니다.

## 가방·생활 트리 통합 표본

통합판은 전체 생산 `AIInventoryMenu`를 추가 링크하며 `InventoryChoiceSurface`/`InventoryMenuControl` 본문을 현재 Loop 소스에서 추출한다. `IntegrationCases`의 25개 검사는 렌더된 생활 화면/전투 일시중지/가방 진입·취소의 소유권 전환과 구토큰 거절, 정비 direct/기억시설 이동 필터, 실제 MenuMoves의 repair source 및 posted take destination 장벽, 메뉴 전환 뒤 pending 회수의 원래 목표 진척과 lifecycle reset을 함께 실행한다. 관측 경계는 적/기억시설 목록만 공급하고, 메뉴 정책·슬롯 장벽·완료 판단을 새 stub에 복사하지 않는다. 원래 대표 파이프라인 88개와 합쳐 113개다.
