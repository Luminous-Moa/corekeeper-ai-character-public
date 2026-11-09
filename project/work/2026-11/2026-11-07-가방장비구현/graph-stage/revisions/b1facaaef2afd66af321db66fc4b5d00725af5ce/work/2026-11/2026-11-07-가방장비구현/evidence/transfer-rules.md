# 내려놓기·전달의 정상 입력 경로 조사

2026-11-07. 읽기 전용 조사. 게임·설치·프로세스·모드·세션 기록을 변경하지 않았다. 이 보고서만 작성했다. 근거는 고정된 로컬 게임 디컴파일이며 실게임 성공을 확인한 결과가 아니다.

## 결론

정상 플레이에서 사용되는 **자기 물건 내려놓기 → 다른 플레이어의 자동 줍기**를 재사용할 수 있다. 특정 수령자에게 가까이 간 뒤 자기 슬롯에서 요청 수량을 내려놓고, 지정 수령자의 실제 가방 증가를 관측하는 방식이 적절하다. 기존 줍기 규칙이 실제 수령을 결정하므로 공간 부족·이동·시야 차단·다른 플레이어의 선점은 성공으로 취급하지 않는다.

다른 플레이어의 인벤토리를 `Create.MoveAmount` 목적지로 직접 지정하는 내부 기술 경로도 존재하지만, 이 조사에서는 정상 플레이어 UI가 다른 플레이어의 가방에 직접 넣는 경로를 찾지 못했다. 소비자 자체의 거리·소유 검사가 없다는 것을 직접 전달 허용 근거로 쓰지 않는다. 인벤토리 강제 수정이나 ForcePickUp 상태 쓰기 없이 내려놓기와 자연스러운 수령을 연결할 수 있다.

## 요청 API와 실제 소비

아래 줄은 `C:/Dev/_ref/corekeeper-decomp/Pug.Other/` 기준이다.

- `Inventory/Create.cs:100`: `DropItem(Entity inventory, int index, int amount, float3 position, Entity blockImmediatePickFor=default)`는 `InventoryAction.DropItem`, inventory1, index1, amount, position1, entityOrInventory2를 채운다.
- `InventoryHandler.cs:254–259`: 정상 UI는 이 데이터를 `UIInputAction.InventoryChange`로 큐에 넣는다. 전량은 `:245–250`의 `DropAllItemsAt`이다.
- `UIMouse.cs:1121–1126`: 선택 슬롯 내려놓기는 자기 현재 월드 위치와 자기 entity를 blockImmediatePickFor로 사용한다. 잠긴 인벤토리(`objectsGetLockedInPlace`)와 구매 인벤토리는 이 UI 경로에서 제외한다.
- `UIMouse.cs:1130–1145`: 커서 물건 내려놓기는 자기 방향 또는 포인터 방향으로 0.4칸 위치, 자기 entity 차단을 사용한다. 우클릭 한 개와 좌클릭 전량이 구분된다.
- `TriggerUIActionsSystem.cs:207–213`: 정상 큐를 actor playerEntity와 함께 InventoryChangeBuffer로 옮긴다.
- `Inventory/InventoryUpdateSystem.cs:365–367`: guest/read-only에서는 actor가 관리자 권한이 없을 때 정상 변경을 차단한다. IgnoreGuest 변형을 이 기능에서 쓰지 않는다.
- `Inventory/InventoryUpdateSystem.cs:797–829`: 출발 인벤토리·버퍼·파괴 상태를 검사하며, 이동 종류는 도착 인벤토리·버퍼·파괴 상태도 검사한다. 플레이어 간 거리와 인벤토리 소유 관계를 여기서 검사하지는 않는다.
- `Inventory/InventoryUpdateSystem.cs:862–864`: DropItem을 InventoryUtility.DropItem에 전달한다. 요청 처리가 실제 슬롯 변화나 수령 완료를 뜻하지 않는다.

## 물건과 수량의 의미

- `Inventory/InventoryUtility.cs:695–736`: 원본 슬롯에서 물건을 읽고 실제 이동량을 계산한다. 해당 양을 출발에서 빼고, 최초 전체 예측 tick이면 같은 objectID·variation·auxDataIndex와 이동량을 가진 바닥 entity를 생성한다. 원본 amount가 0이면 슬롯과 잠금을 비운다.
- `:2539–2547`: 중첩 물건은 `max(0,min(source.amount,request,9999))`를 이동한다. **비중첩 장비는 요청의 숫자와 무관하게 원래 amount 전체를 이동한다.** 이 amount는 장비 한 개의 상태값일 수 있으므로 개수로 세지 않는다. 여러 장비 전달은 개별 슬롯마다 한 물건씩 선택한다.
- 출발 snapshot은 슬롯 index·objectID·variation·auxDataIndex·raw amount를 저장하고 큐 직전에 다시 대조한다. 내려놓을 개수는 중첩이면 요청과 가용량의 최소값, 비중첩이면 1개로 표시하되 API의 상태값을 보존한다.
- 실제 감소량, 생성된 바닥 물건의 같은 서명과 위치, 수령자의 같은 서명 변화는 서로 다른 관측이다. 출발 감소만으로 내려놓기 성공 또는 전달 완료를 선언하지 않는다.

## 즉시 재습득 차단과 수령 규칙

- `Inventory/InventoryUtility.cs:717–725`: blockImmediatePickFor가 있으면 바닥 물건에 `PickUpItemCD { state=BlockPickupUntilReEnterStart, targetEntity=dropper }`를 설정한다. 이 차단은 지정된 **버린 사람**에 적용되고 수령자 독점권은 아니다.
- `PickUpItemSystem.cs:150`: 기본 자동 줍기 반경은 `1.4*(1+pickup bonus)`다. 구현의 근접 이동은 기본 반경 내에서 정상 시야를 확보하고 현재 위치에서 내려놓는 것으로 시작할 수 있다. 이 수치는 전달 거리 정책을 새로 만드는 근거가 아니라 기본 게임 수령 가능성이다.
- `:554–567`: 죽거나 파괴된 수령자는 받을 수 없고, guest mode의 비관리자도 차단된다.
- `:569–588`: dropper는 1.5칸 내에 있으면 처음 재습득할 수 없다. 멀어졌다가 재진입하는 상태 변화를 처리한다.
- `:589–610`: 물건 유형과 슬롯 요구조건에 맞는 공간, 거리, 시야를 검사한다. `:613–619`에서 대상 entity와 pickup 시작 상태를 기록한다.
- `:1263–1314`: 이동 중에도 수령 대상·공간을 재확인하고, 도착했을 때 바닥 인벤토리에서 수령 인벤토리로 정상 `MoveOrDropAllItems`를 큐에 넣는다. `HasBeenPickedUp`는 이 큐 요청 상태이므로 수령자 슬롯 증가 관측이 여전히 필요하다.
- 수령 결과 검사는 장비 자동 전용 슬롯 이동을 포함한 전체 유효 슬롯을 읽되, 결과에는 전달 대상 물품의 변화만 공개한다. 상대의 전체 가방 내용을 모델에 공개할 필요는 없다.

## 구현에 필요한 최소 상태 흐름 제안

1. **접근:** 이미 관측 가능한 지정 수령자를 대상으로 삼고, 가까이 가며 실제 현재 위치·생존·시야를 재확인한다. 상자 공유 정책은 변경하지 않는다.
2. **준비:** 출발 개별 슬롯 snapshot과 수령자 해당 물품 합계를 기록한다. 물품을 자의적으로 바꾸거나 자동으로 다른 수령자를 고르지 않는다.
3. **정상 내려놓기:** 자기 현재 위치에서 `Create.DropItem(self, sourceSlot, amount, selfPosition, self)`를 정상 UI 큐로 한 번 전송한다. 비중첩이면 해당 물건 한 개 전체 상태를 옮긴다.
4. **내려놓기 관측:** 출발 감소와 해당 위치의 같은 objectID/variation/aux 물건 생성 또는 실제 pickup 진행을 확인한다. 바닥 물건의 entity·targetEntity를 가능한 한 추적한다. 단순 바닥 생성이면 내려놓기 성공 사실만 기록한다.
5. **수령 관측:** 지정 수령자의 실제 해당 물품 증가와 출발 감소를 확인해 관측 수령량을 기록한다. 일부 수령이면 수령량과 남은 바닥 물건을 나눠 보고한다. 같은 ID가 동시 획득되는 장면에서는 해당 바닥 entity의 pickup target 추적을 함께 확인해야 원래 전달물의 수령으로 귀속할 수 있다.
6. **다음 판단:** 수령자가 이동하거나 공간이 없거나 바닥 물건을 다른 사람이 줍는 경우 그 관측을 AI에 돌려준다. 이미 내려놓았는데 새 물건을 또 내려 중복 전달하지 않는다. 대기만으로 원래 부탁을 자동 종료하지 않는다.

보장되는 것은 정상 입력 경로와 관측 가능한 수령 결과다. 전달 대상에게만 물건을 보장하는 전용 거래 기능은 이 조사에서 확인되지 않았다. 정확한 바닥 entity 상관관계가 생성/병합과 같은 tick에 사라져 미확인되면 수령 귀속을 추정으로 올리지 않고 미확인 사실을 남긴다.

## 고정 입력 해시(SHA-256)

모두 `C:/Dev/_ref/corekeeper-decomp/Pug.Other/` 아래이며 2026-11-07 읽은 값이다.

| 파일 | SHA-256 |
|---|---|
| Inventory/Create.cs | EB1062460A0AF763B8850CC3046787939BF4599EF5988EF8C4BB35549CB09B27 |
| Inventory/InventoryUtility.cs | 06806858C94DA514DCDE66A06A8BF62F11ADC2DFF5DABBCBACE6D92A0D5DF192 |
| Inventory/InventoryUpdateSystem.cs | E2E311E641A6B30C51EFEC991F9B358398D680C015FD40AAF66FEF288DB80E13 |
| PickUpItemSystem.cs | 030B363FAACFE0756F0C793EBD93D0557C0D03ADB7CF59B0A3F37B70D3F6C480 |
| UIMouse.cs | C2B126396A3E1CF1B2DA145AB7AA1B42C7E384B0F25002A7FB8C1510501A5C92 |
| InventoryHandler.cs | EC8F7E7126708BBF17187B5BE64B2A52B866A2F430CE5054223C105ED501A949 |

이 조사에는 실게임 시험·정상 권한 실패 재현·병합 경쟁 재현이 없다. 구현과 독립 검수에서 위 경계에 맞춘 검증을 수행해야 한다.
