# 내려놓기·전달 경로 교차확인

2026-11-07. 읽기 전용 교차조사. 제품 기준은 `C:/Dev/CoreKeeperModSDK-bag-equipment`; 게임·설치·제품 수정·모델 호출 없음. 구현 담당의 별도 [정상 입력 경로 조사](transfer-rules.md)를 재사용하고 아래 핵심 소비자와 한계를 독립 대조했다. 실게임 관측을 수행한 보고서가 아니다.

## 판단에 필요한 결론

- **자기 현재 위치에 정상 DropItem 요청→dropper 재줍기 차단→수령자의 게임 자동 줍기→실제 수령 확인** 경로는 정상 UI와 게임 소비자의 연결로 뒷받침된다. 지정 상대 인벤토리에 `MoveAmount`로 바로 넣는 정상 플레이 UI 경로는 확인하지 못했다. 소비자에 소유·거리 검사가 없다는 이유로 직접 인벤토리 이전을 채택하지 않는다.
- **전달은 독점 수령을 보장하는 거래가 아니다.** dropper 차단은 버린 사람만 대상으로 한다. 다른 AI/플레이어가 먼저 주울 수 있다. 수령 지정은 모드가 시도와 결과를 귀속하는 목적이며 게임 픽업의 권한을 새로 설정하는 입력이 아니다.
- **대상이 받은 것이 확인되어야 give 성공이다.** 출발 가방 감소, 바닥 물건 생성, pickup target 지정, HasBeenPickedUp는 서로 다른 단계다. pickup 이동 요청 후에도 실제 대상 슬롯 증가가 필요하다. 다른 물품의 동시 획득과 구별할 상관관계가 없으면 미확인으로 기록한다.
- **가방 합계 대신 개별 슬롯/서명과 드롭 entity를 추적해야 한다.** 현재 AIObservation의 items 튜플은 entity·variation·auxDataIndex를 포함하지 않는다. 해당 튜플만으로 정확 전달 귀속을 구현할 수 없다. 몸 실행의 내부 관측에서 필요한 최소 서명을 보강하고 모델에는 목적 물품의 결과만 전달한다.

## 직접 확인한 정상 경로

아래 기준은 최신 추출에 쓰인 보관 원문 `work/게임원본-상호작용/code/Pug.Other/`이다. 별도 조사자는 `C:/Dev/_ref/corekeeper-decomp/Pug.Other/`를 사용했다. 전체 파일 해시는 다르지만 이번 핵심 함수 구간을 문자열 비교해 같은 내용을 확인했다: `InventoryUtility.DropItem`, `GetAmountToMoveFromInventory`/`GetAmountToMoveToInventory`, `UIMouse.DropSelectedObjectToWorld`/`ReleaseGrabbedItemToWorld`, `CheckIfCanPickup`. 전체 두 디컴파일이 동일하다는 주장은 하지 않는다.

| 단계 | 직접 확인한 근거와 의미 |
|---|---|
| 정상 UI 진입 | `UIMouse.cs:1121–1126`: 선택 슬롯은 handler 존재, `!objectsGetLockedInPlace`, `!isBuyInventory` 조건 후 자기 월드 위치와 자기 entity를 사용해 DropItem. `:1130–1145`: 커서 물품은 자기 방향/포인터 방향 0.4칸에 전량 또는 1개 입력. 원격 수령자 좌표에 생성하는 입력으로 확대하지 않는다. |
| 큐 생성 | `InventoryHandler.cs:245–259`→`Inventory/Create.cs:88–110`: DropAllItemsAt/DropItem을 UIInputAction.InventoryChange로 넣고 blockImmediatePickFor를 전달. 기존 모드의 AICraft.QueueInventoryChange 경로와 맞는다. |
| actor 권한 | `TriggerUIActionsSystem.cs:207–213`: actor playerEntity를 넣는다. `Inventory/InventoryUpdateSystem.cs:365`에서 guest/read-only 및 actor 관리자 조건 적용. IgnoreGuest 입력이나 playerEntity 누락으로 우회하지 않는다. |
| 소비 | `InventoryUpdateSystem.cs:797–824`는 존재·버퍼·파괴 검사를, `:862–864`는 DropItem→InventoryUtility.DropItem 호출을 수행한다. 요청 처리의 bool만으로 실제 출발/드롭 변화를 증명하지 않는다. |
| 물품 생성 | `InventoryUtility.cs:695–736`: 출발 슬롯에서 양을 빼고, 최초 전체 예측 tick에 objectID/variation/이동 raw amount/auxDataIndex를 가진 DropNewEntity 생성. 출발이 비면 슬롯과 잠금값 제거. `EntityUtility.cs:543–570`는 DroppedItem entity에 containedObject를 저장. |
| 실제 양 | `InventoryUtility.cs:2539–2547`: stackable은 max(0,min(source.amount,request,9999)), nonstackable은 원래 amount 전체. 장비 '1개'를 요청해도 raw amount를 1로 덮지 않으므로 내구도/상태 보존을 실제 전후로 확인할 수 있다. |
| 재습득 억제 | `InventoryUtility.cs:717–725`: PickUpItemCD의 BlockPickupUntilReEnterStart, targetEntity=dropper. `PickUpItemSystem.cs:570–588`: 해당 dropper가 1.5칸 미만이면 차단, 멀어짐/현재 pickup 반경 이탈에 따른 상태 전환. 다른 수령자는 이 차단의 대상이 아니다. 영구 금지나 수령자 독점이 아니다. |
| 자동 줍기 | `PickUpItemSystem.cs:554–619`: 생존/파괴, guest 권한, slot 공간·요구조건, 거리와 line of sight를 검사하고 실제 targetEntity와 IsBeingPickedUp를 기록한다. 후보 순회에서 허용되는 대상이 잡히므로 지정 수령자가 반드시 선점하는 것은 아니다. |
| 인벤토리 수령 | `PickUpItemSystem.cs:1263–1324`: 대상 생존/공간을 재확인하고 도착 시 Create.MoveOrDropAllItems(dropEntity,targetEntity,...) 요청을 낸 뒤 HasBeenPickedUp. 소비 후 실제 target의 전후 물품 변화 확인이 별도로 필요하다. |

DropItem에 존재하는 `blockImmediatePickFor`를 쓰는 것은 정상 UI와 일치한다. `pullTowardsEntity`/ForcePickUp를 임의 설정하거나 수령자의 autoPickup 컴포넌트를 수정하는 방식은 이번 정상 경로에 포함하지 않는다.

## 물품·잠금·관측 경계

- 출발은 자기 inventory의 선택된 일반 물품 슬롯이며, itemID만 같은 다른 장비로 바꾸지 않는다. queue 직전에 slot/index/ID/variation/raw amount/auxDataIndex를 다시 대조한다. 착용·커서 슬롯까지 drop 후보에 일괄 포함하지 않는다.
- `objectsGetLockedInPlace`는 인벤토리 handler의 추출 제한이다. 개인 슬롯의 즐겨찾기 잠금과 구분한다. Drop 소비자는 물품이 비었을 때 잠금값을 지우지만 이것은 잠긴 물품을 자동 후보로 버려도 좋다는 근거가 아니다. 기존 명시선택/자동정리의 경계를 유지한다.
- 바닥 서명은 objectID/variation/auxDataIndex/raw amount다. 동일 서명이 원래 주변에 있거나 같은 tick에 여럿 생성되면 새 entity의 원인을 단정하지 않는다. 바닥 entity를 확보하지 못한 채 대상 가방에 같은 물품이 늘면 전달 완료로 올리지 않는다.
- 대상에게 빨려 들어가는 관측은 recipient targetEntity와 연결하되 실제 해당 서명 변화가 뒤따라야 한다. 비중첩 장비는 개별 상태가 보존된 새 슬롯, 중첩 물품은 합쳐진 수량 변화까지 고려한다. 자동 장착 가능한 장비는 일반 가방 밖 유효 장비 슬롯도 읽는다.
- 이미 내려놓은 뒤 대상 이동/자리 부족/다른 플레이어 선점이 생기면 남은 물품과 실제 수령자를 사실로 보고한다. 자동으로 다시 같은 수량을 내려놓거나 새로운 수령자를 고르지 않는다.
- 정상 차단의 유지 여부는 dropper가 떠나거나 픽업 후보 맵에서 빠지는 등 게임 상태에 영향을 받는다. 자기 즉시 재습득이 억제되었다고 장시간 방치 후에도 억제가 보장된다고 설명하지 않는다.
- 직접 전달은 호스트/공용 상자 접근 허용과 연결하지 않는다. 전달을 위해 다른 플레이어의 기존 물품을 읽어서 후보를 만드는 것도 필요하지 않다. 내부 결과 검증에서 목적 물품의 전후만 검사한다.

## 최신 그래프와의 관계

읽은 증분은 `2026-11-07-행동결과완결/integration/stage04-evidence-v2/replay-1/graph.json`이다. action_completion_rule/action_completion의 drop/give/transfer/pickup 명칭을 직접 확인했다. 컨테이너 이동 규칙 `completion:C:container-inventory-transfer-v2`는 independent_pass다. 픽업 사례 `completion:full-d:pickup-after-pilot-dig`도 있으나 원래 생산 status만으로 현재 검수 상태를 단정하지 않는다. **이번 확인에서 자기 물품 DropItem 또는 플레이어 간 give의 독립 검수된 전용 완결 규칙을 확인하지 못했다.** 따라서 컨테이너 이동 규칙을 give 완료 근거로 돌리지 않고 위 정상 UI와 실제 소비자 조사 근거를 사용한다. 그래프 전체가 불완전하므로 규칙을 못 찾은 것을 게임 불가능 판정으로 쓰지 않는다.

## 구현 및 실제 검증에 미치는 영향

제안된 drop→자연 픽업 방식을 유지할 수 있다. 완료 조건은 **실제로 내려놓음**과 **지정 대상에게 실제 수령됨**으로 구별한다. 무경쟁 대표 실게임에서 출발·새 드롭 entity·targetEntity·지정 대상 슬롯의 연결을 확인하고, 공간 부족 또는 타인 선점 한 가지 실패에서 거짓 성공/중복 drop이 없는지 본다. 아직 실게임·병합경쟁·권한 차단을 재현하지 않았으므로 독점 전달이나 모든 조건의 성공을 약속할 근거는 없다.

이 보고서는 구현 담당 `/root/bag_equipment_implementation`에 핵심 경계를 전달했고, 해당 담당의 [기존 조사](transfer-rules.md)와 결론이 일치함을 확인했다.
