# 빈 선택지 구현 — 가방·장비 / 상자 공용 / 정비 M4

2026-11-08. 기준 [합의](agreement.md), 점검 [bag-equipment.md](bag-equipment.md), [maintenance.md](maintenance.md) M4.
작업 공간 `C:\Dev\CoreKeeperModSDK-gap-bag`, 브랜치 `gap-bag-20261108`(기준 20b1ace). 실게임·설치·공식 병합은 하지 않았다. 독립 검수도 하지 않았다(HEAD가 맡김).

## 커밋

| 커밋 | 내용 |
|---|---|
| `00f5e33` | 상자 공용: 본 상자 모두 기억, 아무 상자나 열면 내용 기록, 공용 판정 `AIBase.Usable` |
| `351e6df` | 가방 트리 빈 선택지 B1~B9, 정비 M4, 테스트 스텁 호환 |
| `a1e83e5` | 새 어댑터 시험 `Tools/life-inventory-tests/GapCases.cs` |

## 상자 공용 판정 (통합 때 맞출 기준)

- `AIBase.Usable(World, Entity chest)` (`Assets/AIPlayerSpike/Scripts/AIBase.cs:166`): 상자 형식 엔티티면 누구 것이든 쓸 수 있다. 상자 형식은 InventoryAutoTransferEnabledCD가 있고 플레이어·적·제작 시설이 아닌 컨테이너(기존 `View`)다. 소유는 검사하지 않는다. 생활 포크가 제작 재료 상자를 고를 때 이 함수를 쓰면 된다.
- `AIBase.Observe`(`AIBase.cs:124`): 보이는 상자를 모두 기억한다. 전에는 자기가 놓은 상자만 기억했다. `NoteOpened`(`:172`)도 아무 상자나 열면 기록한다. `placed`는 '내가 놓음' 표시로만 남는다.
- `AIBase.Own(view)`와 `OwnChests()`는 이름은 그대로 두고, 이제 "기억한 모든 상자"를 돌려준다. 그래서 가방 메뉴, 생활 트리 보관/꺼내기, `AIInventory.OwnChest`가 호스트·다른 AI 상자를 포함한다.
- 그대로 둔 것: `AIBase.BaseChests()`는 여전히 `placed`만 센다. 예전 자동 보관과 상자 제작 판단에 쓰이는 함수다. 멀리서 본 던전·월드 상자 때문에 기지에 상자를 만들지 않는 일을 막으려고 그대로 뒀다. 이 흐름까지 공용으로 넓힐지는 정하지 않았다.
- 문구: 가방·생활 트리의 "자기 상자"를 "상자(모두 공용)"로 바꿨다. 상자 목록에는 "내가 놓음/공용"을 표시한다.

## 항목별 구현

| 항목 | 상태 | 구현과 위치 |
|---|---|---|
| B1 파우치 | 완료 | `AIInventory.Pouches/IsPouch/IsCarried/CarriedSlots/InventoryEnd`(`AIInventory.cs:138-175`). 메뉴 물품 목록에 파우치 칸 추가(`AIInventoryMenu.cs:148`). 보관·꺼내기·옮기기·해제·내려놓기·전달·들기에서 파우치 칸을 출발 또는 목적으로 쓴다. 꺼내기 MoveAmount의 endIndex는 그 칸이 속한 인벤토리의 끝이다 |
| B2 일괄 정리 | 완료 | `AIInventoryBulk`(`AIInventory.cs:1352~`). 네 동작: 가방 정렬(`Create.Sort(player,true)`), 열린 상자 정렬, 열린 상자에 빠른 쌓기(`Create.QuickStack`), 근처 상자에 빠른 쌓기(`Create.QuickStackToNearbyChests`). 메뉴에서는 분류 단계의 "가방 전체 조작"과 열린 상자 분류 단계의 "열린 상자 전체 조작" 아래에 있다. 같은 종류가 없거나 물품이 2개 미만이면 후보를 내지 않는다. 완료는 실제 칸 변화와 옮겨진 수량으로 확인한다(`BulkTick`, `AIInventory.cs:1093`) |
| B3 프리셋 | 완료 | "착용 프리셋 보기·전환" 노드. 다른 프리셋마다 그 프리셋의 착용 장비를 문구로 보여 준다. 실행은 `BodyInput.presetAsk`에서 `ClientInput.equipmentPresetIndex`로 이어지고(`AISkill.cs`, `AIBody.cs:306-308`), 활성 프리셋 값이 바뀐 것을 보고 완료한다. 전에는 AI 입력이 이 값을 항상 0으로 보내 프리셋 1에 고정돼 있었다. 이제는 현재 값을 유지한다 |
| B4 사용 | 완료 | 조작 "사용(먹기·쓰기·연주)": Eatable·CastingItem·Instrument(`UseKind`, `:542`). 핫바 칸을 고른 뒤 Swap한다. 먹기·쓰기는 보조 사용 유지, 악기는 주 사용 1회다. 완료 판정: 먹기·쓰기는 같은 물품 수량 감소, 악기는 입력 전송까지(소리는 관측하지 않음). 8초 동안 변화가 없으면 실패로 보고한다(`UseTick`, `:1070`) |
| B5 펫 칸 | 완료 | `GearSlots`에 `pet`(PetOwnerCD.SlotIndex) 추가(`:127`) |
| B6 꾸미기 칸 | 완료 | `vanity_head/chest/legs`(VanitySlotsCD). `WantsSlot`(`:197`)에서 칸을 지정하지 않은 착용 목표는 꾸미기 칸을 세지 않는다 |
| B7 착용·상자 장비 직접 이동 | 완료 | 착용 칸에서 바로 할 수 있는 것: 보관(빈 상자 칸과 칸 전체 Swap), 내려놓기, 전달. 해제는 빈 가방·파우치 칸 어디로든 된다. 상자 칸에서 바로 착용·들기는 상자↔플레이어 Swap(`CrossSwap`, `:543`)이다. 착용 칸을 비울 때 가방·파우치 용량이 줄면 막는다(`CapacityAfterRemoval`, `CapacityAfterSwap`는 플레이어 쪽 칸만 계산) |
| B8 옮기기·나누기·합치기 | 완료 | 조작 "가방·파우치 안 옮기기": 빈 칸 또는 같은 스택이면 MoveAmount로 수량을 고른다. 다른 물품이면 칸 전체를 Swap한다 |
| B9 DB 정보 없는 물품 | 완료 | 조작이 0개였던 문제를 고쳤다. 칸 전체로 옮기기·보관·꺼내기(Swap)를 제공한다. 수량 기반 이동·내려놓기는 추측하지 않는다 |
| 정비 M4 파우치 장비 | 완료 | `AIMaintenance.ActiveSlot`이 `IsCarried`를 쓰고, `SourceSlots`가 `CarriedSlots`를 쓴다(`AIMaintenance.cs:74,238`). `maintenance_put`의 출발 칸도 가방·파우치·착용으로 넓혔다(`AIInventory.Stale`) |
| B10 단축 조작 | 제외(합의) | — |

공통: 고르는 것은 판단 모델이다. 새 조작은 모두 기존 트리(분류→물품→조작→목적 칸→수량→확인) 또는 분류 단계의 새 노드 아래 말단(leaf)이다. 후보 상한·페이지는 만들지 않았다. `Stale`(`:749`)을 다시 짜서 Swap 계열(같은 엔티티와 상자↔플레이어)과 MoveAmount 계열을 나눠 검사한다.

## 시험 결과

- `dotnet build Tools/compile-check -c Release`(설치된 게임 DLL 대상): 오류 0.
- `Tools/life-inventory-tests`: ADAPTER CHECKS 217, FAILED 0. 기존 171에 새 GapCases 46이 늘었다. 새 시험 범위: 파우치 이동, 나누기·합치기·자리 바꾸기, 착용 칸 직접 보관·해제, 상자 장비 직접 착용·들기, 먹기와 연주, 정렬, 빠른 쌓기(열린 상자, 근처), 프리셋 전환, DB 정보 없는 물품, 펫·꾸미기 칸.
- `Tools/maintenance-tests/run.ps1`: 115 passed, 0 failed. 실제 `AIInventoryMenu`·`AIInventory`를 링크한다. 생성 목록에 QuickStack/Sort/QuickStackToNearbyChests 원문 추출을 더했다.
- `life-craft-tests` 29/29, `life-tests` 105/105, `life-cook-tests` 51/51, `execution-tests` 181/181, `core-tests` 1571 OK, `gather-tests` 61, `travel-tests` 51/51, `info-tests` 통과, `path-preparation-tests` 25 OK, `action-input-tests` 43 통과.
- `restriction-tests`: 컴파일 실패. 시험 코드가 생산 코드에 없는 `FoodProbe.PlantEntry`를 부른다. 이번 변경과 무관한 기존 상태로 보지만, 기준판에서 같은지는 직접 돌려 보지 않았다.
- Unity 컴파일: 미실행. 점검 시점에 `CoreKeeper.exe`(PID 9008)가 실행 중이었다.

## 추정·미확인

- 실제 게임 요청 결과는 모두 미확인이다. 시험은 가짜 월드와 수동 상태 공급으로 돌렸다. 확인하지 못한 것:
  - 착용 칸에서 DropItem, 착용 칸↔상자 Swap, 상자 칸↔착용 칸 Swap이 실제로 처리되는지.
  - 펫·꾸미기 칸이 Swap을 받는지(SlotAllows가 그 칸을 인벤토리 범위로 보는지 포함).
  - 파우치에 실제로 들어가는 품목.
- 근처 빠른 쌓기: 게임은 물리 겹침 반경 10과 최대 20개로 상자를 고른다. 모드는 "보이는 상자 중 10칸 안 20개"로 근사해 예측과 관측에 쓴다. 게임은 쌓을 수 없는 물품도 같은 종류가 있으면 옮기고, 핫바와 파우치도 포함한다. 후보 문구에 그대로 적었다.
- 악기는 주 사용 1회로 한 음을 낸다고 추정한다(QueueHitSystem에서 InstrumentSlot이 주 사용 대상). 보조 사용으로 들어가는 연주 상태는 쓰지 않는다. 소모되지 않는 사용 아이템(예: 탐지기)은 수량 감소가 없어 8초 뒤 실패로 보고된다.
- 프리셋: 이전 동작은 AI 입력이 프리셋 인덱스 0을 계속 보내는 것이었다. 이제 첫 프레임에 현재 값을 읽어 유지하므로, 프리셋 2·3이 저장돼 있던 캐릭터는 첫 입력부터 그 프리셋을 쓰게 된다.
- 기억하는 상자 수 상한이 없다. 본 상자를 모두 memory.json에 저장한다.
- 상자 형식 판정(`View`)은 CanCraftObjectsBuffer가 있는 시설만 뺀다. 대장간·수리대 같은 정비 시설에 InventoryAutoTransferEnabledCD가 있고 CanCraftObjectsBuffer가 없다면 공용 상자로 기억된다. 확인하지 못했다.

## 통합 주의점

- 생활 포크: 제작 재료를 근처 상자에서 셀 때 `AIBase.Usable`을 공용 판정으로 쓴다. `AILife.cs:130`에서 생활 트리 보관/꺼내기 갈래 문구 한 줄을 바꿨다. 생활 트리 보관(`AIInventory.LifeEntries`)의 출발 칸은 여전히 주 가방뿐이다. 파우치는 가방 메뉴에서만 다룬다.
- 정비 포크: `AIMaintenance.cs`의 `ActiveSlot`과 `SourceSlots` 두 줄만 바꿨다. `AIInventory.Stale`의 `maintenance_put` 출발 조건을 가방·파우치·착용으로 넓혔다.
- 이동전달-개선4건(`CoreKeeperModSDK-move-deliver`) 4/4가 가방 메뉴에 목표 품목 표시와 완료 거절 문구를 더했으므로 `AIInventoryMenu.cs`·`AIInventory.cs`에서 충돌이 예상된다. 특히 `AddOperations`, `Items`, `Stale`, 분류 단계.
- 시험 스텁 세 곳(life-inventory, life-craft, maintenance)에 ObjectType(Pet·Eatable·CastingItem·Instrument), PetOwnerCD, VanitySlotsCD, Create.Sort/QuickStack/QuickStackToNearbyChests, BodyInput.primary/secondary/presetAsk를 추가했다. 이 파일들을 링크하는 다른 브랜치의 시험도 같은 추가가 필요하다.
