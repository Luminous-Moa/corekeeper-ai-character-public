# 전달 관측 구현

소유 변경은 AIInventory.cs와 기존 life-inventory-tests 어댑터다. Core 규칙 ObservedDelivery는 유지했다.

## 실제 처리 경계

게임의 PickUpItemSystem은 PredictedSimulationSystemGroup에서 EndPredictedSimulationSystemGroup보다 먼저 실행된다. pickup 완료 직전에 InventoryChangeBuffer에 MoveOrDropAllItems(floor, recipient)를 넣고 floor의 PickUpItemCD를 HasBeenPickedUp으로 바꾼다.

InventoryUpdateSystem은 EndPredictedSimulationSystemGroup 안의 InventorySystemGroup에서 요청을 소비하고 바닥 인벤토리를 비운다. InventoryChangeResultBuffer에는 bool 결과만 있으며 요청 버퍼는 처리 직후 비워진다. 일반 모드 Update가 그 뒤 실행되면 짧게 존재한 floor 엔터티와 pickup 상태를 놓칠 수 있다.

InventoryDeliverySourceObservationSystem과 InventoryDeliveryObservationSystem을 선택한 sender의 서버 World에만 명시적으로 만든다. 첫 관측자는 PredictedSimulationSystemGroup의 PickUpItemSystem 직전에 실행해 DropItem이 만든 BlockPickupUntilReEnterStart와 targetEntity=sender, 자기 출발 슬롯 감소가 일치하는 새 유일 floor에 귀속한다. 두 번째 관측자는 이미 귀속된 동일 엔터티의 pickup 상태를 포착한다. DisableAutoCreation과 UpdateBefore(InventoryUpdateSystem)를 사용하고 기존 InventorySystemGroup에 등록·정렬한다. 실행 중 give만 watch한다. 매 서버 tick의 consumer 직전에 관련 floor와 target/state 사실을 읽고, 실제 완료·모델 통지는 기존 AI scope의 InventorySkill.Tick에서 수행한다. inventory, pickup, 요청 buffer를 쓰지 않는다.

World.GetOrCreateSystemManaged<T>는 Unity.Entities 구현에서 모듈 초기화 뒤 추가된 managed system도 TypeManager.AddSystemTypeToTablesAfterInit으로 등록한다. 컴파일은 성공했다. 실제 게임의 동적 등록·실행 순서 검증은 후속 Unity/실기로 확인해야 한다.

## 변경한 관측

- 수령자 수량은 floor 바인딩 여부와 관계없이 매 Tick 읽는다.
- 엔터티·buffer를 읽을 수 없는 경우 -1을 사용하고 문구에 미관측이라고 표시한다.
- 전체 ContainedObjectsBuffer를 세므로 main 가방에서 착용/커서로 옮긴 장비도 포함한다. objectID, variation, auxData identity를 유지하며 내구도 변화는 물품 수를 바꾸지 않는다.
- 전달 진행 문구는 송신 횟수, 출발 감소, floor 소비, 수령자 이전/현재/증가 수량, pickup 관측 상태를 표시한다.
- 정확한 floor 귀속 + 지정 recipient pickup + 실제 floor 감소 + receiver gain 일치를 계속 요구한다. 양측 수량만으로 성공하지 않는다.
- 정상 수령 완료 시 ConfirmedWork/ConfirmedReceived/ResultStage도 갱신한다.
- Cleanup은 watcher에서 제거한다. 완료된 실행과 사라진 sender는 observer에서도 제거한다.
- 모호한 동일 floor 여러 개와 타인 pickup의 기존 실패 처리는 유지한다. drop 재전송은 발생하지 않는다.

## 검증

기존 실제 AIInventory/InventorySkill 직접 링크 어댑터 171개 통과, 실패0이다. inventory-adapter.log를 보존했다. 설치 게임 DLL을 참조한 Tools/compile-check는 경고0/오류0이다.

추가 회귀는 floor를 전혀 관측하지 못한 경우 receiver+1 사실이 표시되지만 성공은 단정하지 않음, consumer전 observer → floor소비 → 다음 render Tick 순서에서 정확한 수령 완료, 착용과 내구도 변화, 다른 플레이어 pickup, 동시 동일 floor 모호함, 타인이 떨어뜨린 동일 floor가 먼저 유일하게 보이는 반례, cleanup 뒤 관측 해제, 수령자 사라짐 미관측, floor가 그대로인데 receiver만 늘어난 경우를 포함한다.

어댑터는 실제 생산 observer/skill 코드를 실행한다. ECS UpdateBefore 정렬과 게임 consumer의 실행 자체는 대역이므로 실기 증거로 보완해야 한다.

## 게임 소스 근거

C:/Dev/_ref/corekeeper-decomp/Pug.Other/PickUpItemSystem.cs의 1299~1307행, Inventory/InventoryUpdateSystem.cs의 355~382행, Inventory/InventoryUtility.cs의 695~735행을 확인했다. 이 경계는 원래 DropItem/auto-pickup 경로를 그대로 사용한다.
