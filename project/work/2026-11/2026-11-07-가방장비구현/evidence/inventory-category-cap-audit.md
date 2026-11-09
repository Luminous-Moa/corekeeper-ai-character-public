# 보유 물품 분류와 현장 후보 상한 조사

- 조사일: 2026-11-07. 구현 전 bounded 사실조사이며 제품 코드·게임·Unity·공식 그래프를 쓰지 않았다.
- 제품 기준: `C:/Dev/CoreKeeperModSDK-bag-common`, `1fc89b93edbee31a9001a174581e32568119efc1` (이하 **R**). 아래 제품 라인은 이 기준판이다. 구현 중 라인이 이동하면 commit 기준으로 확인한다.
- 게임 코드 기준: `C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/게임원본-상호작용/code` (이하 **G**). 기존 `work/게임행동-그래프/graph.json`은 교차 확인만 했고 쓰지 않았다.
- 결정 기준: 같은 폴더 `tree-agreement.md`. 실제 보유 슬롯만 분류, 미분류 유지, 8/14 및 같은 목적의 선택지 숫자 상한 제거, 시야·소유권·실행 조건·호출/비용·기억/로그 제한은 별도 보존.

## 1. 결론

현재 게임은 `ObjectInfo.objectType`, `ObjectInfo.tags`, `ObjectInfo.isStackable`를 제공한다. 보유 목록의 출발점은 `ContainedObjectsBuffer`의 실제 슬롯이며, 각 비어 있지 않은 슬롯에 대해서만 `PugDatabase.GetObjectInfo(id, variation)`를 조회하면 된다. DB 모든 물품을 열거할 필요가 없다. 분류 결과와 실행 가능 여부를 분리해야 한다. 고장 난 장비나 info를 찾지 못한 실제 물품을 보유 목록에서 지우면 안 된다.

게임에 `ObjectIDCategoryCache`와 부모/자식 카테고리도 실제 존재한다. 다만 이는 제작/creative selector가 사용하는 정적 ID 분류이다. 일반 보유 인벤토리 UI가 같은 카테고리 트리를 보여준다는 근거는 확인되지 않았다. 따라서 아래 `ObjectType`/태그 기반 권고표는 게임 의미에 대응하는 **새 보유 메뉴 분류**이며 게임 인벤토리 UI의 기존 이름을 복제했다고 설명하면 안 된다.

숫자 상한은 최종 assembler의 14와 가방 8 외에도 가방 생성의 12/20/상자12, 이미 관측된 world 대상의 nearest-N과 후보 목록 countcut에 있다. 아래 A표는 같은 목적의 선택 누락이므로 제거 대상이다. 시야 범위, 경로 탐색 작업량, 물품 이동 수량, 행동 묶음 크기, 보고/기억/로그 요약량은 같은 뜻이 아니다.

## 2. 게임의 실제 분류 API와 UI 근거

| 근거 | 확인 내용 | 사용상 경계 |
|---|---|---|
| G `Pug.Base/ObjectInfo.cs:21`, `:23`, `:43` | 단일 `ObjectType objectType`, 다중 `List<ObjectCategoryTag> tags`, `isStackable` | 태그는 상호 배타적 카테고리가 아니다. 공격 차단·해체 가능 등 시스템 태그도 섞인다. |
| G `Pug.Base/ObjectType.cs:1` | 장착 100~108, 무기 500~503/610, 도구 600~609, 설치 800, 생물 801/802/900, 섭취1100 등 명시 enum | 숫자 대역 비교보다 enum 값을 명시적으로 map하는 편이 향후 값 추가에 안전하다. |
| G `Pug.Base/ObjectCategoryTag.cs:1` | Ore, TileBlock, Seed, Fish, CookingIngredient, PetEgg, Bomb, Valuable, Potion 등 | `CanBeSalvaged`, `CanBeUpgraded`, `DamageCantGoThrough`, 재질/생물 태그는 곧바로 메뉴 대분류로 쓰지 않는다. |
| G `Pug.Other/PugDatabase.cs:245` | 정확한 variation 조회 실패 시 variation 0의 ObjectInfo로 fallback | 실제 슬롯의 variation을 0으로 바꾸지 말 것. 이것은 메타데이터 조회 fallback이다. |
| G `Pug.Other/Inventory/InventoryUtility.cs:26` | 게임 정렬 comparer가 stackability → objectType → ID → variation → amount 역순 비교 | 게임도 유형/변형/상태를 구별한다. 이 정렬은 카테고리 UI 자체는 아니다. |
| G `Pug.Other/Inventory/InventoryUtility.cs:3553`, `Pug.Other/InventorySlotUI.cs:786` | 장착 UI 슬롯별 ObjectType 일치 검사, Pet/vanity/pouch 포함 | 메뉴 분류가 equip 허용 여부를 대신하면 안 된다. 실제 슬롯 요구사항도 대조한다. |
| G `Pug.Other/InventoryUI.cs:349`, `:376` 및 `InventoryHandler.cs:544` | 인벤토리 수용 태그/파우치 배경색 처리 | 이 태그는 해당 가방·슬롯이 받는 물품 조건이다. 모든 보유물품의 단일 카테고리 트리가 아니다. |
| G `Pug.ObjectIDCategory/ObjectIDCategoryCache.cs:17`, `:33`, `:65`, `:123` | Categories/ParentCategories/SubCategories, ScriptableData 로드, 이름 접두어로 부모 연결 | 게임 정적 카테고리 API의 존재는 확실하다. 별도 DLL 참조/데이터 로드 의존성이 있다. |
| G `Pug.ObjectIDCategory/ObjectIDCategoryDataBlock.cs:6`, `:23`, `:52`, `:141` | orderedSubCategories, ParentCategory, Contains(ObjectID), 자식 ID를 부모 ID 집합에 합침 | ID 기반 membership이므로 variant/상태·소유 슬롯을 식별하지 않는다. |
| G `Pug.Other/CraftingSelectorFilterCategory.cs:23`, `:37`, `:50` | 카테고리 ID 집합으로 `RecipeSlot`을 필터링 | 일반 보유 목록 API로 오인하지 않는다. |

현지 `probe-index-defaultlocalgroup_assets_all.json`에서 실제 category data block 이름으로 `Equipment_Helm`, `Equipment_Pouch`, `Weapon_Melee`, `Weapon_Ranged`, `Weapon_Magic`, `Tool_Mining`, `Tool_Gardening`, `Consumable_Food`, `Consumable_Potion`, `Material_RawMaterial`, `Building_Chest`, `Other_Seed` 등이 확인된다. `ObjectIDCategoryDataBlock` 검색 결과 133개 중 하나는 MonoScript 항목이고 132개는 named data assets다. 이 인덱스는 이름/필드 구조 근거이며, ID membership 원본의 전량 복원·게임 런타임 로딩 검증은 이번 조사에서 하지 않았다. 따라서 기존 creative 이름과 타입이 언제나 일대일이라는 주장을 하지 않는다.

`raw/objects.json`은 3235개의 정적 ObjectInfo/변형 항목을 담고 있다. 이 수는 보유 수량이 아니다. 예를 들어 type0 NonUsable에는 CopperBar 같은 재료와 GreatWallBlock 같은 블록이 함께 있다. PlaceablePrefab은 상자·시설·블록 등을 폭넓게 포함한다. Eatable에는 PetCandy도 있다. 그러므로 `NonUsable=모든 재료`, `Eatable=사람 음식`, `CastingItem=마법무기`처럼 단정하면 안 된다. 현지 정식 타입 이름은 `ObjectInfo`이며 별도 일반 `ItemInfo` 클래스는 확인되지 않았다 (`TryGetTileItemInfo` 등 이름에는 ItemInfo가 들어감).

## 3. 구현 권고 map

가장 단순한 완전성 보장은 실제 슬롯마다 `ObjectType`을 읽고 해당 타입 이름에 대응하는 분류를 주는 방식이다. 메뉴를 크게 묶는 경우 아래처럼 의미를 유지할 수 있다. 하위 타입을 별도 노드로 둘지는 현재 메뉴 설계에 맞추되 빈 분류는 만들지 않는다.

| 권고 분류 | ObjectType/태그 근거 | 주의 |
|---|---|---|
| 착용 장비 | Helm, BreastArmor, PantsArmor, Necklace, Ring, Offhand, Bag, Lantern, Pouch | 장착 위치(head/ring1/ring2 등)는 물품 인스턴스의 위치로 별도 표시 |
| 무기 | MeleeWeapon, RangeWeapon, SummoningWeapon, ThrowingWeapon, BeamWeapon | BeamWeapon은 enum상 610이지만 무기 의미이므로 단순 600대 도구로 잡지 않음 |
| 도구 | Shovel, Hoe, MiningPick, PaintTool, FishingRod, BugNet, Sledge, RoofingTool, DrillTool, WaterCan, Bucket, Seeder | 물뿌리개/양동이의 amount는 상태일 수 있음. 수량으로 합산 금지 |
| 사용 아이템 | CastingItem | 포괄 이름 유지. 스캐너·알 등 실제 속성 확인 없이 더 좁게 단정하지 않음 |
| 섭취물 | Eatable | Potion 태그가 있으면 `물약`, Fish/CookingIngredient는 `생선/요리 재료`, cooked-food 태그는 `요리`로 세분 가능. PetCandy도 Eatable이므로 `음식`만으로 설명하지 않음 |
| 설치물 | PlaceablePrefab, TrainingDummy | TileBlock 태그는 `블록`, Seed 태그는 `씨앗` 등 아래 우선 태그로 먼저 분리 가능. 이 분류는 실제 설치 실행 제공을 뜻하지 않음 |
| 생물·반려동물 | Critter, Pet, Creature | 실제 보유 슬롯에 있는 것만. Creature/NonObtainable이라도 보유한 비정상·모드 항목을 버리지 않음 |
| 귀중품 | Valuable 또는 Valuable 태그 | 태그가 type과 중복돼도 같은 슬롯을 중복 노출하지 않음 |
| 특수 제작 부품 | UniqueCraftingComponent | ID 이름을 추측해서 통상 재료와 합치지 않아도 됨 |
| 열쇠·중요 물품 | KeyItem | 특수 ID를 임의 열거할 필요 없음 |
| 악기 | Instrument | MusicSheet 태그는 악보로 별도 분류 가능 |
| 재료·기타 | NonUsable | NonUsable은 UI 사용 불가능 유형이지 물품 삭제 사유가 아님. Ore/TileBlock/Seed 등 유효 태그가 있으면 먼저 좁게 분류 |
| 기타·분류 불명 | info==null, 정의되지 않은 enum값, NonObtainable, PlayerType 등 위에 포함되지 않은 값 | ID·원본 type 값·슬롯·variation·amount를 보존하고 접근 가능하게 둠 |

태그 세분화를 채택하면 우선순위를 명시한다. 안전한 예: 장비/무기/도구 같은 구체 type 먼저 → Seed → Potion → TileBlock → Ore → Fish/CookingIngredient → 나머지 구체 type → 기타. 다중 태그의 첫 번째 원소에 따라 바뀌게 하지 않는다. 태그를 채택하지 않고 ObjectType만 분류해도 빈틈 없는 타입 map과 미분류 fallback이면 합의에 맞는다.

### 실제 보유 source와 인스턴스 보존

- R `AIInventory.cs:66` Read, `:77` MainStart, `:84` MainEnd, `:90` Info: 실제 `ContainedObjectsBuffer` 인덱스를 먼저 읽는다. 주 가방 경계는 `InventoryBuffer[0]` startIndex/size이며 핫바는 게임의 실제 0~9 슬롯과 구분한다. R `AIBody.cs:513`의 MainEnd는 정상 metadata 우선, fallback만 40이다. 이 fallback 40을 전체 후보 상한으로 취급하지 않는다.
- R `AIInventory.cs:105` Equipment, `:114` GearSlots: ActiveEquipmentPresetCD가 있으면 현재 preset의 EquipmentCD 슬롯을 읽는다. 같은 실제 슬롯을 주 가방과 장착 목록에서 중복 열거하지 않는다. G `Pug.ECS.Components/EquipmentCD.cs:5`에는 9개 기본 장착 위치와 pouch1~4가 있다.
- 장비 위치 외의 player contained buffer를 모두 `bag`로 부르면 제작 커서/입출력 등 다른 슬롯을 잘못 소유물 목록에 넣을 수 있다. 현재 지원 범위의 main inventory와 active gear 위치를 명시적으로 합친다. 다른 Pet/vanity 관련 위치를 추가 지원하려면 해당 별도 컴포넌트/실제 UI 규칙을 먼저 확인한다. 이번 조사에서 해당 동작 확장을 확정하지 않는다.
- G `Pug.ECS.Components/ContainedObjectsBuffer.cs:8`, `:29`: objectData와 auxDataIndex를 보존하며 EqualsExact는 objectID/amount/variation/variationUpdateCount/auxDataIndex를 대조한다. R `AIInventory.cs:93` Stamp와 같은 수준으로 엔티티·슬롯·ID·variation·aux·amount·variationUpdateCount를 유지한다. ID만으로 합쳐 장비 개체 선택을 잃지 않는다.
- R `AIInventory.cs:91` Quantity는 nonstackable이면 1, stackable이면 amount이다. G `Pug.Other/PlayerController.cs:7211`은 amount<=0 && DurabilityCD일 때 고장으로 판단한다. 따라서 amount 0의 고장 장비도 objectID가 있으면 실제 보유 한 개로 남긴다. amount를 모든 아이템의 count로 표시하면 안 된다. info가 없으면 stackability도 모르는 것이므로 원본 amount와 `수량/상태 불명`을 보존하는 것이 안전하다.
- 일반 상자도 동일 `ContainedObjectsBuffer` + objectID/variation을 쓰므로 같은 순수 분류 함수를 재사용할 수 있다. R `AIInventory.cs:286` ChestCandidates는 자기 소유, 시야/접근, S.openChest를 먼저 확인하고 그 뒤에 ci[0].startIndex~size 실제 슬롯을 읽는다. 분류 자체가 chest 권한이나 관측 시점을 우회하면 안 된다. 현재 메뉴에선 열어보지 않은 상자에 `아직 내용 모름`을 유지한다.

## 4. cap 지도 A — 같은 목적의 선택지 누락, 제거 대상

아래 모두 **R 기준**, 경로 앞 `Assets/AIPlayerSpike/Scripts/` 생략. 단순 순서 정렬은 유지 가능하다. 그룹별 후보 생성·몸 가능성·소유권·시야 필터는 이 표와 별개다.

| 파일:라인 | 현재 제한과 누락 경로 | 판정/구현 주의 |
|---|---|---|
| `AIInventory.cs:204`, `:206`, `:209` | 일반 후보 `Diverse(...,8)` | 명시 제거 대상8. 다양성 순서 자체는 남겨도 숫자컷 제거 |
| `AIInventory.cs:183` | 핫바 복수 목적지 생성 중 전체 result>=12면 break | 상한12 제거. 물리 핫바10은 유지 |
| `AIInventory.cs:186` | own equip/hold 생성 result>=20에서 주 가방 루프 중단 | 상한20 제거 |
| `AIInventory.cs:325`, `:327` | 상자 물품/상자 반복에서 result>=12면 중단 | 상한12 제거. 관측/소유권 유지 |
| `Core/AICandidateAssembler.cs:54`, `:199`, `:203`, `:218`, `:236` → `AIBrain.Candidates.cs:1163` | 비전투14, 전투 combatMaxCandidates>0이면 해당 개수, Finish→Cut GetRange | 14 제거. 전투 설정도 같은 최종 선택지 숫자컷. 제거 후 value 부여·wait 추가·failure/body 필터는 유지. EdgeShare는 cut의 자리배분이므로 더는 누락 제어에 쓰지 않음 |
| `AIBrain.Candidates.cs:167`, `:173`, `:174` | 전체 관측 enemies에서 최근접3, 기지 안 우선 최대4만 near3 | 관측범위 제약 아님. 이미 본 적의 공격 후보수 제한이므로 제거 |
| `AIBrain.Candidates.cs:353`, `:355` | 보이는 목표 벽3, 기타 벽2 | 거리 정렬 후 숫자 선택컷. 목표/자유시간 filler 정책은 별도 유지 |
| `AIBrain.Candidates.cs:364` | DigOre 성공 후보2, plan 시도4 | 결과수2 제거. 시도4도 후보 생성 전에 다른 관측 대상 접근가능성을 검사조차 하지 않는 shortlist라 같은 누락 목적. 경로탐색 내부 guard와 구분 |
| `AIBrain.Candidates.cs:423` | 물체 부수기 거리순 brk2 | 같은 선택컷. 물체/작물 소유권·목표 일치 유지 |
| `AIBrain.Candidates.cs:470` | 큰 뿌리2 | 같은 선택컷. :460 시야 스캔 창과 혼동하지 않음 |
| `AIBrain.Candidates.cs:487`, `:489`, `:492` + `AIMemory.cs:412` | 기억광석 nearest12→후보2, 시도4 | 기억 보관량이 아님. 저장된 기억에서 당장 후보로 쓸 목록 자르기이므로 제거. 기억시점/화면 밖 표기·광맥 동일집단 규칙은 별도 |
| `AIBrain.Candidates.cs:546` → `Core/AIPickupOptions.cs:13`, `:23` | 목표/음식 다양성 순으로2개만 | 같은 선택컷. free/food/automatic 적합성은 유지. 단순 while 숫자만 지우면 AddFirst의 조기 return이 남는지 확인 |
| `AIBrain.Candidates.cs:564` | 따라가기2 | 같은 선택컷. 거리·goal 조건 유지 |
| `AIBrain.Candidates.cs:669` | 화면 끝 탐험 굴2 | `nov<=0`는 유지, count>=2만 제거 |
| `AIBrain.Candidates.cs:722` | 기억한 frontier 중 후보2 | 경로검색예산과 별개인 결과수 제한. :706 FrontierJob(40000) guard와 구별 |
| `AIBrain.Loop.cs:1278` | 방향별 대표를 만든 뒤3개로 RemoveRange | 방향 대표화 자체와 별도로, 4방향 중 하나를 숫자로 지우는 최종3 제한 제거 |
| `AIBase.cs:507` | 기지 상자 store 후보2 | 같은 선택컷. :521~532의 첫 기억상자 break는 아래 비숫자 대표 규칙도 확인 |
| `AIBase.cs:546` | 제작 준비재료 꺼내기 전체6 | 같은 선택컷. 각 물품의 첫 성공상자만 쓰는 :551 break는 별도 대표 규칙 |
| `AIBase.cs:783` (`AIFood.Candidates`) | 배고픔 음식3 | 같은 선택컷. 음식 종류별 현 메타데이터/variation 보존 문제와 혼동하지 않음 |
| `AIFarm.cs:774` | 야생 씨앗 얻기 후보2 | 같은 선택컷. :592 수확 행동묶음6과는 다름 |
| `AIEdge.cs:598` | 횃불 설치자리2 | 같은 선택컷. 토치 품종 첫 선택 :590은 별도 대표 규칙 |
| `Core/AIPlaceGrid.cs:91`, `:325`, `:333` → `AIPlace.cs:134` | consequence별 대표를 만든 뒤 MaxOptions3, 잘린 edge에서2만 추가 | 숫자3 및 추가복구2 제한 제거. consequence별 대표화와 물리불가 필터는 별개 |
| `Core/AIBaseEdge.cs:129`, `:439`, `:444` | 기지 둘레 면적별 대표를 만든 뒤 MaxOptions4만 | **사용 경로를 구분**: 현장 후보뿐 아니라 `/ai edge`/위쪽 기지선택 보고 생성에도 쓰는 core. 숫자4는 표시 선택 누락용이나 메뉴 state와 다른 경로이므로 owner가 callsite 기준 적용/검증. 후보 제거 범위의 상한 지도에서 누락하면 안 됨 |

전달 최종단은 `AIBrain.Loop.cs:1338` BuildJson, `:1346`의 `for i<cands.Count`가 목록 전부를 직렬화한다. 이 함수 자체에 FirstN/Take/cap은 발견되지 않았다. `:739`의 후보 desc120자 Trim은 SEND 로그 요약이고, `:1349` 실제 전송 description에는 그 Trim이 없다. `AICandidateAssembler.Finish`를 통과한 리스트가 `_reqCands`에 저장되는 `AIBrain.Loop.cs:705`까지 기준으로 회귀 검증해야 한다.

## 5. cap 지도 B — 다른 의미, 이번 숫자선택컷과 구별

| 위치 | 제한 의미 | 권고 |
|---|---|---|
| `AIVision.cs:61`; `AIObservation.cs:59`, `:83`, `:101`, `:125`; `AIBrain.Candidates.cs:460` | 기본 화면 half15x8.5, CanSee, root 스캔 ±14/±10 | 실제 관측 범위. 이번 변경에서 삭제/무한 확장하지 않음 |
| `AIBrain.Candidates.cs:156`, `AIBrain.Loop.cs:1243`; `Core/AITunnelSearch.cs:26`, `:39`; `AITunnel.cs:195`, `:213` | 경로 진행 미리보기5, in-view BFS guard3000, Dijkstra guard120000, frontier 작업량/반경120 | 탐색 안정성/범위. 후보 리스트 숫자컷과 다르다. 계획시도 shortlist4와 구별 |
| `AITunnel.cs:195`, `:204` | frontier `_best.Count<4`, 한 방향 첫 frontier | 동/서/남/북 각 대표 하나를 찾는 알고리즘 구조. 4는 실제 방향수. 4방향→3/2 후속 slice와 구별 |
| `AIInventory.cs:168`, `:172`, `:343`, `:435`; `Core/AIInventoryGoalForm.cs:14` | 핫바10, 물품 최대스택9999, 실제 이동수량/슬롯범위 | 물리/프로토콜 제약. 선택지 개수와 무관. main inventory size는 실제 메타데이터로 읽음 |
| `AIBody.cs:524` | 메타데이터 없을 때 main end fallback40 | 안전 fallback이며 통상 실제 slot size 우선. 후보 개수40 의미가 아님 |
| `AIFarm.cs:592`, `:599`, `:608`, `:618`, `:645`; `AICook.cs:33`, `:228`; `Core/AIPlaces.cs:36` | 행동 하나의 수확6/심기6/일구기4, 물 용량별 물주기, 요리 한 회5접시, 한 shape30칸 | 실행 행동의 작업량/형상 크기. 선택 노드의 물품/대상 목록 상한과 구별하여 보존 |
| `Core/AIApproachSearch.cs:10`, `AIPlaces.cs:204`, `:238`, `:533` | 대상에 접근할 때 경로 시도 stand3 | 접근 solver의 성능/후보 stand 탐색 예산. 모델이 고르는 대상 목록3이 아님. 삭제 확정하지 않음 |
| `AIBase.cs:816`→`:885` | `AIFoodSource.Visible(...,5)` | 확인한 호출은 상위 LLM ReportFields의 음식 출처 요약. 현장 후보 생성에서 사용되지 않음. 후보 cap으로 잘못 제거하지 않음 |
| `Core/AIPlaces.cs:37`, `:38`, `:258`, `:270`, `:319` | 상위 계획 보고의 places12, mine5/other2/ore3/water1, 문자 예산 | 보고 요약량. 현장 tree 메뉴에 재사용하면 누락을 만들지만 현재 보고용 제한 전체 삭제를 이번 합의로 단정하지 않음 |
| `Core/AIBaseMap.cs:73` | 지도 torch 제안 최대12 | 기지도 제안/출력 생성. 현장 횃불 자리2 컷과는 별도. 호출 경로별 검토 대상으로 남김 |
| `AIBrain.Loop.cs:1301`, `AIPlanReport.cs:29`, `AIBase.cs:644` | 일반 상황 bag 종류12, 상위 보고 bag10/상자4 | 보유 메뉴 source로 쓰면 안 됨. 메뉴에는 실제 슬롯 전체를 별도 전달. 기존 요약 정책 자체는 별도 |
| `AIExperience.cs:414`, `AIExpCore.cs:418`, `:423`, `:498`; `AIInspectSkills.cs:40`, `:44`, `:52`; `AICook.cs:544` | 경험 entity4/요약3/최근N, inspect 로그20/15, 시험 명령 요리예측8 | 기억·로그·진단 출력량. 현장 선택지 누락 cap과 다름 |
| `AIDecisions.cs:30`, `:37`; `AIBrain.Loop.cs:235`, `:379` | 월비용70, 호출분당30/전투 호출분당 등 | 기존 호출/비용 상한을 늘리지 않음. 메뉴 탐색이 호출 제한으로 막혀도 몸 동작/목표를 강제 취소하지 않음 |
| `AIPlanner.cs:675`, `:677` | 상위 모델 응답의 유효 계획 prefix/최대 단계 | 이미 반환된 계획의 수용 정책. 현장 메뉴 자식 목록과 구분 |

## 6. 비숫자 first/best와 미결

아래는 선택지를 하나로 대표하거나 자동 실행 인자를 고르는 곳이다. `Take(N)`이 없다고 전수 선택을 보장하지는 않지만, 숫자 상한 철회가 곧 자동화/행동의 모든 대상을 새 트리로 재설계한다는 뜻도 아니다. 사용자가 요구한 물품·목적 슬롯·상자·수량 메뉴와 직접 겹치는 것은 owner가 분리해 고쳐야 한다.

- `AIInventory.cs:198` 해제는 첫 빈 가방 슬롯 한 개만 후보로 만들고 break. `:278`~`:279` Destination은 첫 유효 목적 슬롯을 반환. 새 메뉴가 목적 슬롯을 고르게 한다면 첫 빈칸/첫 스택만 노출하지 말고 모든 실제 유효 슬롯을 열거해야 한다. 슬롯요구·잠금·용량은 그대로 검사한다.
- `AIBase.cs:601`, `:620` FoodTake/ItemTake는 best 정렬 후 첫 성공상자 하나. `ItemTakeCandidates(...max)`의 max는 꺼낼 **수량**이며 후보 수가 아니다. 같은 파일 `:532` 기억상자 첫 경로, `:551` 물품별 첫 준비상자도 대표화이다. 새 자기상자 메뉴에서는 모든 소유 상자 및 관측한 해당 물품 슬롯에 접근 가능해야 한다.
- `Core/AICandidateAssembler.cs:130` 근접전 goalSide는 continue 우선 하나만 `keep`한다. 소스 주석에 **ONE option**을 명시한 기존 전투세트 설계다. 단순 물리불가 필터가 아니고 선택수 축약이며 숫자리터럴이 없는 사실을 owner에 알린다. 이것을 이번에 없애는지는 HEAD가 기존 전투세트 정책과 새 상한 철회의 관계를 판정할 항목이다. 숨겨진 countcut으로 누락시키지 말 것.
- `AIBrain.Candidates.cs:596`~`:600` 동료 돕기는 가장 가까운 적 하나/첫 동료 하나. `AICook.cs:214` BestPair 및 `:468`~`:471` 첫 remembered pot, `AIEdge.cs:590` 첫 토치 품종, `Core/AIPlaceGrid.cs:328` consequence 대표 하나, `AIBrain.Loop.cs:1273` 방향대표 하나 등은 행위 대표 생성 규칙이다. 새 전체 world 트리 설계 없이 모두 제거한다고 확정하지 않는다.
- 태그 세분화와 native creative category membership 중 어느 쪽을 채택하든 category lookup 실패·알 수 없는 enum·null tag로 실제 물품을 잃지 않는 fallback을 검증한다. native category 데이터의 ID membership/런타임 ordering은 이번 조사에서 검증하지 않았으므로 새 의존성을 넣기 전에 따로 확인해야 한다.
- Pet/vanity·비활성 preset·pouch 내부 등 현재 조작 범위를 넘는 추가 위치 공개는 이번 조사에서 확정하지 않는다. 현재 active gear/main inventory를 정확히 노출하는 것과 별도다.

## 7. 구현/검수 인수 항목

1. 분류는 현재 슬롯 스냅샷에서만 생성. 빈 분류/미소유 DB항목은 0개. unknown와 amount0 고장장비는 남음.
2. 같은 ID인 서로 다른 슬롯/variation/amount/aux/variationUpdateCount가 각각 선택 가능. bag/hotbar/active gear/chest 위치 명시.
3. 주 가방과 장착을 합친 메뉴, 각 분류 자식, 목적 슬롯·상자·수량 목록, 최종 모델 요청에 8/14를 넘는 항목을 넣고 시작/끝 항목 누락 없음 검증.
4. cap A의 제거는 최종 prompt만 확인하지 말고 각 생성기의 입력 관측→생성 결과→assembler 결과→BuildJson candidates 배열을 비교. 전투설정 cap이 양수여도 같은 목적의 컷이 재등장하지 않음 검증.
5. cap B의 시야·소유권·실행허용·호출/비용·stack 수량은 유지 확인. 요약용 bag12/상자4/음식출처5를 보유 메뉴 데이터로 사용하지 않음.
6. 실행은 원래 Swap/MoveAmount/DropItem과 결과 관측을 재사용. 이 보고서는 구현/단위검사/실게임 검증을 수행했다는 근거가 아니다.

조사 방법: 로컬 `rg`로 enum/API/Take/GetRange/RemoveRange/Count/break/최종 JSON 전달을 찾고 해당 함수 본문을 읽었다. 정적 JSON은 필요한 타입·태그/카테고리 메타데이터만 로컬 파싱했다. 제품 수정·빌드·Unity·실게임·외부 모델 호출 없음. 조사 핵심 및 수정 분류(음식출처5는 보고요약)는 구현 owner에게 직접 전달했다.

## 8. HEAD 적용 판정 — 조사 후 인수 지시

출처: 2026-11-07 HEAD가 이 조사 대화에 전달한 적용 지시. 아래는 `tree-agreement.md`의 기존 합의를 현재 Decisions 후보 목록 변경 범위에 적용한 **HEAD 판정**이며, 새로운 사용자 원문이나 사용자 직접 인용이 아니다. 앞 절의 소스 근거는 보존하고, 다음 두 미결의 실행 방향을 이 판정으로 정한다.

- **상위 둘레 설계 제안의 CoreEdge.Options4는 보존한다.** `Core/AIBaseEdge.cs:129`, `:439`~`:446`의 네 개 제한은 소비자가 기지 둘레/설계 제안임이 확인되는 경우, 이번 현장 Decisions 후보 목록 변경과 다른 소비 경로이다. 같은 숫자 기반 축약이어도 이 별도 소비 경로까지 제거하는 것으로 범위를 넓히지 않는다. A표의 해당 행은 발견 사실/후보용 여부를 확인할 경계로 남기며, 확인된 상위 둘레 설계 소비자에는 제거를 적용하지 않는다. 구현·검수 문서에 이 소비 경로 차이를 명시한다.
- **현재 Decisions 목록의 closeFight goalSide ONE 할당량은 제거한다.** `Core/AICandidateAssembler.cs:130`에서 유효한 목표 후보 중 하나만 keep하는 것은 현재 Decisions 선택지를 개수로 누락시키는 할당량이므로 제거한다. 근접전의 의미상 적합성, 긴급성, 후보 순서와 기존의 실행 가능성 검사는 보존한다. 6절에서 HEAD 판정 대상으로 남긴 이 항목은 이 지시로 해소되었다.
- **내부 First/Best 자동화를 전부 숫자 상한으로 재해석하지 않는다.** 목적 슬롯 등 새 물품 메뉴와 직접 겹치는 경로는 합의에 맞게 모든 유효 선택지를 제공하되, solver·행동 대표·내부 자동화의 모든 First/Best를 일괄 제거하거나 새 전체 world 트리로 확장하지 않는다.

이 부록은 조사 결과에 대한 범위 판정을 기록한 것이며, 해당 구현이 완료되었거나 검증을 통과했다는 주장은 아니다.
