# 다음 생활 실행 단위의 개발 방향

## 맡은 범위와 결론

첫 단위의 공통 트리·제작·요리·농사·보관/꺼내기 구현과 분리한 다음 단위의 방향 조사다. 제품 코드를 수정하지 않았고 첫 단위의 후보/테스트/실게임 상태를 승인하지 않는다.

다음 일관된 단위로 **정확한 장비 하나를 선택해 일반 수리하고 실제 내구도와 재료 변화를 확인한 뒤 원래 일로 돌아가는 흐름**을 권고한다. 이어 같은 장비 신원·비용·시설 관측을 재사용해 **레벨 업그레이드**를 연결한다. 내구 강화는 동일 정상 요청의 별도 mode로 만들 수 있지만, 기존 독립 수용된 일반 수리의 범위를 그대로 내구 강화에 확대하지 않고 별도 검증을 붙인다.

수리를 먼저 권고하는 이유는 첫 단위가 장비의 정확 슬롯·상태와 트리·수량·실행 결과를 이미 다루며, 수리는 기존 물품을 다른 생산물로 바꾸지 않고 하나의 정상 Craft 요청과 상태 전후로 완결할 수 있기 때문이다. 레벨 업그레이드는 동일 장비 관측을 쓰지만 대장간 입력 슬롯에 넣기와 회수까지 필요해 다음 작은 경계로 두는 편이 직접적이다. 자동 가공은 요리와 비슷하지만 시설의 입력 생산·회수 및 자기 시설 기억 연결을 새로 닫아야 한다. 거래/낚시는 별도 소비자·상태/시간 관측이 더 필요하다.

## 기준과 실제 조회

프로젝트 그래프에서 `product.mjs intent P.life`, `find 수리`, `source P.execution`을 실행했다. 최상위 P.release-goal 아래 동료 경험·생활 계획이 있으며, LLM이 대상·수량·준비·순서·대안을 고르고 코드는 정상 입력과 실제 결과 확인을 맡는 합의를 유지한다. 그래프에서 수리 검색이 R.missing만 반환했다는 사실은 게임 수리 기능의 부재를 뜻하지 않는다.

게임 정본 G와 별도 후속 staging의 상태는 구분했다. 조사 시점 integration README에는 stage04의36묶음/1,754고유case 독립 PASS·수용이 명시되어 있고 stage05의42묶음은 변경검수 진행으로 적혀 있었다. 따라서 이번 방향의 후속 근거는 **stage04 고정 인덱스와 재현 출력**을 사용했다. 실행한 명령의 형식은 다음과 같다.

```powershell
& <고정 Python> -X utf8 semantic.py --standard-index stage-04.json --semantic-index semantic-04.json --output-dir stage04-evidence-v2/replay-1 target 8555 --variation 0
# 같은 인자로 아래 정확한 application의 show/source 실행
```

최초 output-dir를 stage04-evidence-v2로 지정한 조회는 graph.json이 없어서 실패했다. 실제 재현 그래프가 있는 replay-1로 수정해 성공했고 정본/생성물은 변경하지 않았다.

| 조회·근거 | 확인한 사실 | 적용 경계 |
|---|---|---|
| 8555:0 CookingPotHelm source `CAB-2ebcba97367158c9b08f790c25d642c9:-6961237460797322265` | repair application과 upgrade application의 status=independent_pass, source별 primary/level/비용 규칙 | 해당 고정 source의 조건부 정적 근거. 봇 실행·현재 슬롯 성공은 아님 |
| `completion:C:repair-item-conditional-v1/application/<위 source>`의 show/source | primary 선택·LevelEntitiesBuffer/DurabilityCD·실제 amount와 수리 비용의 의미 | [원본 조회](queries/repair-source.json). 일반 수리 수용을 reinforcement 전체 수용으로 확대하지 않음 |
| `completion:C:upgrade-equipment-conditional-v1/application/<위 source>`의 show/source | 표본 기본 유효레벨6→7, 비용 인덱스7. 실제 variation>0이면 그 값, 아니면 prefab level | [원본 조회](queries/upgrade-source.json). 모든 장비의 비용을 표본10개/132개로 고정하지 않음 |
| Furnace4004:0의 target, automatic_recipe_result show 및 stage04 후속 source | ProcessResources 자동 소비·생산과 조건/실패. stage04 relatedActions reviewStatus=independent_pass | [후속 원본](queries/furnace-accepted-source.json), [정본 조회](queries/furnace.json). producer의 pending 문장은 원문 그대로 보존하며 receipt의 수용과 구별 |
| fishing50:0 및 merchant3900→5013 판매 completion show | 입력→조건부 전리품, 실제 판매 소비·화폐 생성 경로 | [낚시 조회](queries/fishing-accepted.json), [판매 조회](queries/sale-accepted.json). 원문의 생산 당시 review_pending과 accepted_by 수용관계 구별. 판매를 구매 수용으로 확장하지 않음 |

기존 `full/C/repair-rule-v2.json`, `upgrade-rule-v2.json`과 로컬 디컴파일 `C:/Dev/_ref/corekeeper-decomp`에서 정상 요청·소비자를 직접 읽었다. 필요한 source의 출처 pointer/hash는 위 queries에 보존했다.

## 첫 단위와 연결하는 방식

첫 단위 구현 담당의 실제 인터페이스 확인과 현재 후보 소스 기준으로, AILife는 per-agent LifeCursor/LifeNav 및 `LifeEntries(world,player,observation,currentKey,LifeSelection)`에서 한 깊이 Entry를 받고 페이지/back/root/close를 만든다. 구체 실행은 각 모듈 ExactCandidate→고정 payload→AIActions.Make/Stale→skill.Start 재검증이다. ILifeResult의 RequestedWork/ConfirmedWork/ConfirmedReceived/ResultStage를 실제 OnSkillEnded가 실행Run당1회 집계한다. 다른 목표의 별도 행동은 goal=null이다. 이 후보 인터페이스는 아직 첫 단위 검수 전이므로 **후속 명세 시작 때 수용된 첫 단위 커밋으로 다시 고정**한다.

새 장비 정비 생산자는 이 입구를 사용하고 nav/페이지/응답처리를 복제하지 않는다. 현재 branch whitelist·goal form·report/brief에는 네 갈래만 있으므로 실제 새 실행과 함께 장비 정비 갈래를 추가한다. 다음 명세에서는 action 우선으로 mode(수리/내구강화/레벨업)를 고르고 정확 슬롯·시설·비용을 좁히는 순서가 가장 작다. 장비를 먼저 고르는 순서를 채택한다면 현재 mode 선택이 Item/Where/Slot을 지우는 커서 규칙을 그 의존에 맞춰 고쳐야 한다. 트리 사용 순서가 몸의 작업 순서를 강제하는 새 루틴이 되지는 않는다.

상위가 정확한 장비·작업·시설을 이미 지정했으면 직접 실행 후보도 같은 생산자/재검증을 사용한다. 일반 수리/강화 한 번은 장비 하나·단계 하나로 제시하며, 모든 장비 일괄 처리나 최대레벨까지 자동 반복은 하지 않는다. 추가 단계는 확인된 상태·다음 비용을 보고 AI가 다시 고르거나 상위 계획이 명시한다. 정상 시뮬레이션/현재 공개 지식의 범위를 유지하고 전체 개발 그래프를 모델에 넣지 않는다.

## 다음 coherent 단위: 장비 수리

### 사용자부터 다음 판단까지

상위가 원래 부탁을 이어 가는 중 도구 손상 사실을 보고 수리 작업을 계획한다. 현장 AI는 정확한 장비 슬롯(가방/착용·손 슬롯, ObjectID, variation, auxData/상태 stamp)을 고른다. 해당 variation의 primary가 수리 대상인지, 내구도가 얼마에서 어디까지 오르는지, 자신의 가방에 필요한 재료가 있는지를 비교한다. 실제 알고 있는 수리대에 접근해 단일 정상 수리 요청을 넣고 **대상 슬롯의 내구도 증가와 실제 재료 감소를 따로 확인**한다. 원래 부탁을 유지한 채 다음 행동/단계 완료/계획 변경을 AI가 정한다.

### 정상 게임 요청과 소비

로컬 원본 경로는 다음으로 확인했다.

```text
RepairToggle/SalvageAndRepairUI.ToggleRepair
→ 수리 모드에서 대상 inventory slot
→ CraftingHandler.RepairOrReinforce
→ Inventory.Create.RepairOrReinforce(... reinforce=false ...)
→ UIInputAction.Craft → TriggerUIActionsSystem → CraftBuffer
→ InventoryUpdateSystem.ProcessCraftingJob
→ InventoryUtility.RepairOrReinforce
→ 재료 ConsumeObject/TryConsume + 대상 ContainedObjectsBuffer.amount 변경
```

모드는 기존 AICraft.QueueCraft처럼 client와 같은 UIActionBuffer를 다음 server tick으로 큐에 넣는 방식으로 확장한다. `Create.Craft` 일반 제작으로 수리를 흉내 내지 않고 정식 RepairOrReinforce action data를 사용한다. 직접 inventory.amount를 고치지 않는다. 서버가 시설 거리/사용 가능 UI 조건을 모두 검사해 주는 것은 아니므로 후보 생성과 skill의 실제 접근/현재 시설 검사가 필요하다.

`InventoryUtility.CanBeRepaired`는 선택 primary의 LevelEntitiesBuffer와 DurabilityCD 존재, 일반 수리는 amount<maxDurability를 확인한다. 최종 수리량은 **현재 선택 primary의 maxDurability**다. 비용은 ID-only 기본 recipe와 기본 primary/손상비율·ScrapPart·prioritized 재료·플레이어/수리대 비용 보정을 사용한다. 강화된 레벨이나 현재 variation을 비용식에 임의 대입하지 않는다. 일반 제작의 ObjectInfo.requiredObjectsToCraft만으로 수리비를 추정하지 말고 게임의 실제 비용 helper/동등한 검증된 runtime 조회를 사용한다. 정확한 ECS lookup 취득과 함수 호출 배치는 다음 명세의 일이다.

현재 슬롯·primary/재료/비용은 실행 직전에 다시 확인한다. 슬롯이 바뀌었거나 다른 variation/auxData의 물건이면 다른 장비로 바꿔 수리하지 않고 변경 사실을 보고한다. 요청 후에는 선택 슬롯 ID/variation/auxData를 유지한 amount 변화와 해당 재료의 main inventory 전후를 확인한다. 소비자 코드에는 재료 소비 부족 로그 뒤에도 SetAmount로 가는 분기가 있어 비용과 상태변경을 원자적 성공 하나로 가정하지 않는다. 상태만 바뀌거나 재료만 바뀌면 각각 관측을 보고하고 같은 요청을 무조건 반복하지 않는다.

### 비용·권한 경계

ProcessCraftingJob은 main inventory 뒤에 craftingEntity 근처 자동이체 상자를 거리순으로 더한다. 수리/강화의 소비 루프는 그 inventory 순서로 읽는다. 이번 방향은 **실제 필요한 재료를 먼저 자기 가방에 준비한 단일 요청**을 기본으로 하며 부족한 재료는 기존 자기 상자 꺼내기 선택으로 가져온다. 새로운 호스트/공유상자 사용 정책을 허용하지 않는다. 게임 소비자가 주변 상자도 볼 수 있는 점은 다음 명세에서 권한 검증에 포함한다. 자기 가방의 정확한 비용 확보를 검사하지 않은 채 근처 상자 재료를 가용량으로 더하지 않는다. 큐에 들어간 뒤 외부 슬롯 변경까지 완전히 격리하는 새 예약/잠금 체계는 현재 승인된 범위가 아니므로 필요해지면 그 구체 근거를 HEAD에 올린다.

`Create.RepairAllItems`→`InventoryUtility.RepairAllItems`는 내부 단일 호출에 비용면제를 전달하는 별도 helper다. 이 경로를 재사용해 무료 일괄 수리로 바꾸지 말고, 선택한 장비의 정상 단일 action과 게임의 실제 비용면제 상태만 사용한다. 새로운 무료 수리 옵션/소비예산은 만들지 않는다.

### 완료 의미와 검증

완료는 정상 요청 전송이 아니라 선택 장비의 실제 내구도 변화 확인이다. 수리량은 장비1개의 작업, 재료 소모는 별도 사실이다. 원래 부탁 종료는 상위 order=done 계약을 유지한다. 대표 오프라인 경로는 실제 plan/트리→정확 슬롯·수리대 선택→생산 Make/Start→정상 Craft action→어댑터 소비 경계→장비/재료 버퍼 변화→ILifeResult→다음 입력이다. 이미 최대, 도구 교체, 같은 ID 다른 variation, 재료 일부변경, 요청 접수 후 미변화, 수리대 소실을 직접 관련 회귀로 둔다. 이후 Unity compile 및 독립 검수, 보류 해제 후 대표 실게임 한 번이 필요하다.

## 이어지는 장비 강화 두 종류

### 레벨 업그레이드

독립 수용된 source별 근거와 공개 소비자 경로가 있어 수리 다음에 직접 연결할 수 있다.

```text
UpgradeForge.Use → UpgradeForgeUI의 입력 slot0에 대상 물품
→ UpgradeForgeUI.Upgrade → CraftingHandler.Upgrade
→ Create.Upgrade → UIInputAction.Craft → CraftBuffer
→ InventoryUtility.Upgrade → 비용 소비와 대상 variation=다음 레벨
→ 바뀐 물품을 가방으로 회수 → 다음 판단
```

실제 UI 흐름은 대장간 입력 inventory의slot0에 대상 물품을 넣는다. 서버 API가 다른 targetInventory를 받아 준다는 이유로 가방에 둔 채 UI 준비단계를 생략하는 편법으로 만들지 않는다. 기존 정확 물품 이동을 재사용해 투입·단일 upgrade·회수의 전후를 확인한다. 대장간에 다른 물건이 있으면 임의로 치우지 않고 상태를 돌려준다. 원래 착용 슬롯으로 다시 장착하는 것은 명시한 계획/후속 AI 선택으로 연결한다.

runtime primary의 LevelEntitiesBuffer와 CanBeUpgraded tag, 유효 level, UpgradeCostsTableCD의 다음 비용을 사용한다. 유효 level은 variation>0이면 variation, 아니면 primary LevelCD.level이며 최대는 게임 LevelScaling.GetMaxLevel에서 읽는다(조사 고정판은20). 고정 표본8555의 비용을 모든 장비에 적용하지 않는다. 레벨 변화는 variation으로 관측하며 새 variation은 성공한 동일 물품의 변화로 식별한다. 재료 전후와 물품 회수는 별도 확인한다.

### 내구 강화(reinforce)

일반 수리와 같은 Create.RepairOrReinforce의 reinforce=true 경로를 실제 코드로 읽었다. CanBeRepaired의 isReinforcing은 현재 amount<2×maxDurability 조건이며 최종 SetAmount는 round(2×maxDurability)다. 수리와 다른 비용식(기본 재료와 ScrapPart, reinforceCostMultiplier, 현재 내구도 비율)이 적용된다. 이를 레벨 업그레이드나 일반 수리와 같은 효과로 표시하지 않는다.

그러나 stage04의 일반 repair application 수용은 false 경로를 중심으로 한 자료다. true 경로에 대한 UI·비용·target variation 원본 표본과 실제 정상요청/결과 대조를 별도 검수해야 한다. 공통 코드라는 이유만으로 기존 독립 수용 지위를 복사하지 않는다. 이 검증이 끝나기 전에는 내구 강화를 지원 가능한 말단으로 열지 않는다. 이 차이는 추가 사용자 정책이 아니라 구현 근거/검증의 남은 부분이다.

## 나머지 전체 범위의 방향·의존

| 단위 | 정상 입력→소비자→결과 | 첫 단위에서 재사용할 것과 남는 일 |
|---|---|---|
| 자동 가공/제련 | 정상 InventoryChange로 입력 슬롯에 선택 원료 이동→PugAutomationStartCraftSystem이 CanCraft 가능한 recipe를 buffer순으로 선택→timer→PugAutomationCraftingSystem→InventoryUtility.Craft→출력 슬롯 생산→정확 수량 가방 회수 | 요리의 투입/기다림/복귀/수령과 정확 물품 이동. 일반 Craft와 별도 producer/skill로 둠. 자기 가공시설의 관측·기억, recipe/output/실제 timer·전기 적용 여부를 source/runtime별로 확인. 원료를 넣은 사실과 주괴 확보를 구별. 전력망/자동 배관 건설은 새로 넣지 않음 |
| 구매 | 상인 실제 판매 inventory slot 선택→Create.Buy→InventoryChangeBuffer→InventoryUtility.Buy/SellObject→화폐 소비·상품/재고 변화 | 트리/정확 슬롯/main inventory 관측. Buy는 원본상1개 단위이므로 수량은 실제 결과를 확인한 명시 반복. 고정 구매가·재고나 상품 수령을 추정하지 않음. 판매의 기존 수용표본은 구매 완결 증거가 아님 |
| 판매 | 선택 물품의 정상 판매 슬롯 이동/명시 Sell→InventoryUtility.TrySell/SellObject→물품 소모·AncientCoin 생성/드롭 | 개별 물품/수량 명시. SellAll을 전체가방에 적용하지 않음. 소비자에는 가치0 물품 파괴 경로가 있어 가격0을 정상 판매라고 노출하지 않음. 한 상품·양을 팔아 실제 화폐를 가방에 받은 표본부터 검증 |
| 낚시 | 선택 낚싯대·미끼·물타일→secondInteractHeld→FishingRodSlot→FishingState/입질·미니게임→당기기→미끼 소비 결과→DelayedFishLoot→실제 가방/드롭 | 장비 선택·BodyInput·위험 대응·실행결과. 준비는 트리, 입질/당기기는 직접 현장 선택으로 둠. 미끼는 offHand, 물/지형·스킬·난수와 미니게임 설정을 실제 상태로 관측. 예측못한 물고기 품종을 고정 보장하지 않음. 미니게임을 임의 끄거나 새로운 고속 모델/호출예산을 가정하지 않음 |

거래의 Buy는 가방 공간이 없을 때 일부 inventory를 옮기거나 드롭하는 경로도 있으므로 공간 확인과 실제 가방 확보가 필요하다. 판매의 zero-price 파괴와 전설/판매불가를 구별한다. 낚시의 미끼 소비 성공 전에는 전리품 생성을 완료로 세지 않고, 입질 판단/미니게임의 실제 상태를 읽을 수 있는지와 현행 모델 호출 지연에서 동작이 가능한지는 별도 대표시험으로 판정한다. 이것이 막히면 그 관측을 HEAD로 올려야 하며 코드가 자동으로 게임 옵션이나 모델을 바꾸지 않는다.

## 명세로 넘길 조건과 사용자 결정

첫 수리 단위는 기존 자기 자원·정확 장비·정상 요청·결과 확인·원래 부탁 보존으로 만들 수 있어 **현재 추가 사용자 정책 결정이 필요하지 않다**. 다음 명세가 고정할 것은 수용된 첫 단위 커밋, 새 goal/mode 형식, 시설 식별/거리, 비용 조회 ECS 입구, exact slot stamp와 정상 요청 payload, 부분 결과·중복 방지·진척과 검증이다. 이들은 합의된 결과를 구현하는 일반 설계다.

공유 자원 접근, 전 장비 일괄 수리/최대 강화, 희귀 재료 장기 소비, 거래 자동 매각, 미니게임 비활성화, 새 모델/새 호출예산은 추가하지 않는다. 그런 변경이 구체적으로 필요해지면 실제 막힌 흐름과 대안/영향을 HEAD에 돌려 사용자 소유 선택으로 남긴다. 현재 게임/설치 보류는 그대로이며 정적 소비자 경로의 발견을 봇의 실게임 성공으로 보고하지 않는다.

첫 단위 전체가 아직 검증 중이어도 이 방향을 별도 다음 명세의 입력으로 보존할 수 있다. 구현은 첫 단위와 새로운 실행이 섞이지 않게 고정된 수용 기반에서 진행한다. 장비 정비 뒤 자동 가공·거래·낚시는 위 의존을 사용해 각각 별도 한 줄기를 완결한다.

## 주요 직접 대조 원본

- `Pug.Other/Inventory/Create.cs:377-424`: 단일 수리/내구강화/업그레이드 action data.
- `Pug.Other/CraftingHandler.cs:428-520`: 실제 비용 조회, 단일 대상/슬롯·UI 큐.
- `Pug.Other/Inventory/InventoryUpdateSystem.cs:405-456`: 권한·main inventory·주변 상자 목록과 소비 action 분기.
- `Pug.Other/Inventory/InventoryUtility.cs:1966-2207,2250-2385`: 업그레이드·수리/내구강화 조건/비용·실제 상태와 소비.
- `Pug.Other/Inventory/InventoryUtility.cs:1038-1115,3266-3333`: 판매/가치·구매·공간/소비 경로.
- `Pug.Other/FishingRodSlot.cs:10-63`, `PlayerState/Fishing.cs:312-418`: 정상 낚시 입력과 미끼/지연 전리품 생성. 상세 정적 경로는 위 수용 그래프 조회 참조.
- 첫 단위 후보 `AILife.cs`, `Core/AILifeTree.cs`, `AIInventory.cs`, `AIActions.cs`: 현재 인터페이스/경계 확인용이며 아직 수용 커밋을 대신하지 않음.
