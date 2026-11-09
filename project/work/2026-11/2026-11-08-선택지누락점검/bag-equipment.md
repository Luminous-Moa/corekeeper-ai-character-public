# 선택지 누락 점검 — 가방·장비

2026-11-08. 읽기 전용 점검. 기준은 `C:\Dev\CoreKeeperModSDK`, 브랜치 ai-character, 커밋 20b1ace다. 코드 경로는 `Assets/AIPlayerSpike/Scripts/` 기준이다. 게임 원본 경로는 세션 `work/게임원본-상호작용/code/` 기준이다. 코드·문서·그래프는 수정하지 않았다. 행동 그래프 조회(`work/지식그래프/tools/product.mjs behavior R.game-topic.inventory`)를 실행했을 때 "입력 원문 2개 변경/없음" 경고가 나왔다. 그래프 근거는 독립 검수 지위(`independent_pass`, `runtimeTested=false`)까지만 사용했다.

## 1. 지금 제공되는 선택지

### 트리 구조 (AIInventoryMenu)

| 단계 | 판단 모델에 나가는 자식 | 코드 위치 |
|---|---|---|
| 닫힘 | `인벤토리에서 물품 분류와 가능한 조작 고르기` 하나 | AIInventoryMenu.cs:222-226 |
| categories | 실제로 물품이 있는 분류마다 하나(게임 ObjectType 기준 15개 분류, 해당 없음은 "기타·분류 불명"). 자기 상자가 기억에 있으면 `자기 상자에서 꺼낼 물품 고르기`도 나옴 | AIInventoryMenu.cs:87-107, 233-247 |
| items | 해당 분류의 개별 슬롯. 대상은 주 가방·핫바(`MainStart..MainEnd`)와 활성 착용 슬롯뿐 | AIInventoryMenu.cs:128-149, 249-256 |
| operations | 가방 물품이면 equip(착용·교체), unequip(해제), hold(손에 들기), store(자기 상자 보관), drop(바닥에 내려놓기), give(보이는 동료에게 전달). 상자 물품이면 take(꺼내기)만 나옴. 각 조작은 실행 가능한 이동이 하나라도 있을 때만 보임 | AIInventoryMenu.cs:314-343 |
| chests → access → await | 기억한 자기 상자 선택 → 걸어가기 또는 열어보기(leaf) → 관측 대기 | AIInventoryMenu.cs:258-279, 199-218 |
| recipients | 보이고 살아 있는 동료 | AIInventoryMenu.cs:280-288 |
| destinations | 유효한 목적 슬롯 전부. equip은 같은 타입의 활성 장비칸, unequip은 빈 주 가방 칸, hold는 핫바 0~9, store/take는 상자 칸 또는 주 가방 칸 | AIInventoryMenu.cs:289-295, AIInventory.cs:482-529 |
| quantity / number | 1개, 현재 부탁 잔량, 가능한 전량, 자리별 숫자 입력 | AIInventoryMenu.cs:353-390 |
| confirm | 최종 실행 leaf 하나 | AIInventoryMenu.cs:298-303 |
| 공통 | 뒤로, 가방 선택 전체 취소 | AIInventoryMenu.cs:161-165 |

### 실제 실행되는 말단 동작

- equip / unequip / hold: 같은 플레이어 인벤토리 안에서 정상 `Create.Swap`을 보내고, 양쪽 슬롯과 손 장비를 관측한다(AIInventory.cs:884-910). hold로 고른 무기나 채굴 도구는 이후 공격·채굴에 그대로 쓰인다(AIInventory.cs:801-805, 814-822).
- store / take: 열어서 관측한 자기 상자와 주 가방 사이의 MoveAmount 계열 이동(AIInventory.cs:513-524, 654-671).
- drop / give: 주 가방 슬롯에서 정해진 수량을 바닥에 내려놓는다. give는 수령자 근처로 가서 내려놓은 뒤 수령자 가방이 실제로 늘었는지 확인한다(AIInventory.cs:525-527, 677-686).

### 공개 선택면 연결

- `InventoryChoiceSurface`는 생활 연결(`life`)이 없는 예전 평면 `inventory/store/take` 후보를 모두 지우고 메뉴 후보만 붙인다. 메뉴가 열려 있으면 계속·중지·완료·재계획·먹기와 위험할 때의 전투 대응만 함께 남는다(AIBrain.Loop.cs:1315-1336).
- 목표가 Equip/Hold/Store 같은 가방 목표여도 바로 실행할 후보는 나오지 않고 메뉴를 거친다. 생활에서 슬롯과 상자를 모두 지정한 보관·꺼내기만 직접 후보로 나온다(AIGoalFeatures.cs:12-31).
- 먹기는 메뉴 밖의 별도 후보다. 허기 음식(AIBase.cs:776-801)과, 체력이 가득 차지 않았을 때의 회복 물품(AIBrain.Candidates.cs:348-362, 회복 판정은 AICombat.cs:404-460)만 대상이다.

## 2. 빠진 선택지

중요도 표기: **막힘** = 실제 플레이 흐름을 막는다. **중간** = 우회는 되지만 판단 횟수나 시간이 크게 는다. **있으면 좋음**.

### B1. 파우치 안 물품을 보거나 옮길 수 없음 — 중요도: 막힘(파우치를 착용했을 때)
- 빠진 것: 착용한 파우치 4개의 내부 칸(InventoryBuffer[1..4])에 든 물품이 분류·물품 단계에 나오지 않는다. 그 물품은 보관·꺼내기·내려놓기·전달·들기 대상이 될 수 없다. 가방 물품을 파우치에 넣는 선택도 없다.
- 게임 근거: 파우치 슬롯마다 10칸짜리 별도 인벤토리가 있고, 파우치 슬롯이 그 인벤토리의 extraInventorySizeSlot이다(`Pug.ECS.Conversion/InventoryConverter.cs:131-149`). 슬롯 탐색은 모든 InventoryBuffer를 돌고 슬롯 요구 조건이 맞는 칸을 먼저 고르므로, 광석·씨앗·물약·물고기·작은 생물 같은 물품은 줍거나 옮길 때 파우치로 들어갈 수 있다(`Pug.Other/Inventory/InventoryUtility.cs:509-587`). 게임 데이터에는 파우치 18종이 있다(광석·블록, 씨앗·작물, 귀중품, 물고기, 물약, 작은 생물; `Tools/knowledge` graph 26332184).
- 코드 확인: Items는 `MainStart..MainEnd`와 GearSlots만 읽는다(AIInventoryMenu.cs:145-146). MainEnd는 InventoryBuffer[0]만 본다(AIBody.cs:515-527). 착용 메뉴에서는 파우치 장착이 가능하다(AIInventory.cs:122). 그러므로 AI가 파우치를 착용하면, 그 뒤 자동으로 들어간 물품을 메뉴에서 다룰 수 없게 된다.
- 실제로 AI 줍기에서 파우치로 들어가는지는 실기에서 확인하지 않았다(4절).

### B2. 한 번에 여러 칸을 보관·정리하는 선택이 없음 — 중요도: 중간 (경계: 생활의 보관)
- 빠진 것: 열린 상자에 같은 종류를 쌓아 넣는 QuickStack, 근처 상자들에 쌓아 넣는 QuickStackToNearbyChests, 가방·상자 Sort, 예전의 "상자에 넣기"(보관할 수 있는 물품을 한 칸씩 연속으로 넣는 skill)가 모두 없다.
- 게임 근거: `InventoryAction`에 QuickStack, Sort, QuickStackToNearbyChests가 있다(`Pug.ECS.Components/Inventory/InventoryAction.cs`). 키 입력 경로는 `Pug.Other/PlayerController.cs:1818-1848`, UI 버튼은 `Pug.Other/ItemSlotsUIContainer.cs:283-310`에 있다. 행동 그래프 가방 주제의 label에는 이 동작들이 없다. 따라서 근거는 게임 원본 코드뿐이다.
- 합의 근거: 계획안의 "나무 10개 남기고 나머지 보관"과 11-06 누락 조사의 가방 가득참 정리(`2026-11-06-행동누락조사/01-inventory.md:14,17`).
- 코드 확인: 메뉴와 생활 트리의 보관은 모두 슬롯 하나씩 처리한다(AIInventoryMenu.cs:317-325, AIInventory.cs:246-247, 287-301). 예전 일괄 `store` 후보(AIBase.cs:503-520)와 자유 시간 가방 정리(AIBase.cs:456-466)는 아직 만들어지지만, 공개 선택면에서 삭제된다(AIBrain.Loop.cs:1321). 가방 목표일 때는 그 전 단계에서도 삭제된다(AIGoalFeatures.cs:17). 이렇게 숨긴 것은 통합 때의 의도된 결정이다("일반 예전 store/take … 숨기되", `2026-11-07-가방장비구현/integration-report.md:13`). 다만 대신할 일괄 경로는 만들지 않았다. 지금은 한 칸을 보관할 때마다 판단 호출이 7~9번 필요하다(진입→분류→물품→보관→상자→접근/열기→목적 칸→수량→확인).

### B3. 착용 프리셋 전환이 없고, 비활성 프리셋의 장비에 손댈 수 없음 — 중요도: 있으면 좋음 (프리셋을 써 둔 캐릭터라면 중간)
- 빠진 것: 프리셋 1/2/3 고르기·순환·편집이 없다. 비활성 프리셋 칸에 끼워 둔 장비는 목록에 나오지 않는다.
- 게임 근거: 프리셋은 최대 3개이고(`Pug.ECS.Components/EquipmentPresetsBuffer.cs:6`), 프리셋 1·2는 머리·목걸이·상의·하의·반지 2·보조손 칸을 따로 가진다(`Pug.ECS.Conversion/InventoryConverter.cs:93-104`). 입력 EQUIP_PRESET_1~3/CYCLE은 `Pug.Other/PlayerController.cs:1855-1870`, UI 경로는 `CharacterWindowUI.cs:104`에 있다. 그래프 `game:completion:C:equipment-preset-ui-handoff-v1`이 independent_pass다.
- 코드 확인: GearSlots는 활성 프리셋만 읽는다(AIInventory.cs:103-124). 실행 도중 프리셋이 바뀌면 실패로 처리하기만 한다(AIInventory.cs:893). 프리셋을 바꾸는 후보는 어디에도 없다.

### B4. 먹기·사용 조작이 회복과 허기에만 묶여 있음 — 중요도: 중간 (경계: 섭취·휴식 / 전투 / 귀환 / 동물)
- 빠진 것:
  - (a) 회복이나 허기 효과가 없는 섭취물을 먹을 수 없다. 예: 격노·석피·예리함·수호자·하수인·화상 저항 물약, 마나 물약. 회복 물품은 체력이 가득하면 미리 먹을 수 없다.
  - (b) CastingItem을 쓸 수 없다. 예: 선물 상자·삼하인 가방·빨간 봉투 열기, 보스 탐지기, 소환 우상.
  - (c) 악기를 연주할 수 없다.
  - 메뉴의 "섭취물"·"사용 아이템" 분류에도 사용 조작이 없다.
- 게임·합의 근거: 섭취물 184종, 그중 물약 13종. CastingItem 66종(`Tools/knowledge` graph). 11-06 조사에서 이미 "버프용 선소비·사전 보급"을 공백으로 짚었다(`2026-11-06-행동누락조사/01-inventory.md:20`, `03-findings.md:25`). 정비순서 2번이나 가방 합의에는 들어가지 않았다.
- 코드 확인: 메뉴 조작 목록은 AIInventoryMenu.cs:317에 고정돼 있고 사용 조작이 없다. 먹기 후보는 HungerAdd>0인 물품(AIBase.cs:759-760) 또는 HealthAddition/HealOverTime이 있는 물품만 고른다(AICombat.cs:425-427, 452-454). 메뉴에 개별 먹기를 넣지 않은 것은 기록된 한계다("선택한 개별 물품의 새 eat 실행을 추가하지 않았다", `2026-11-07-가방장비구현/tree-implementation.md:30`). 다만 이를 후속 할 일로 남긴 기록은 찾지 못했다.

### B5. 펫 칸에 장착할 수 없음 — 중요도: 있으면 좋음 (경계: 동물·펫)
- 게임 근거: PetOwnerCD.SlotIndex가 플레이어 인벤토리 안의 펫 칸 1개다(`Pug.Other/EquipmentHandler.cs:45-54`, `InventoryConverter.cs:162-168`). Pet 타입 17종.
- 코드 확인: GearSlots에 펫 칸이 없다(AIInventory.cs:118-122). 그래서 "생물·반려동물"로 분류된 펫 물품에는 equip 이동이 만들어지지 않는다(AIInventory.cs:494-498). 이미 펫 칸에 있는 물품은 목록에도 나오지 않는다.

### B6. 꾸미기(vanity) 칸이 없음 — 중요도: 있으면 좋음(낮음)
- 게임 근거: 머리·상의·하의 꾸미기 칸(`Pug.Other/VanitySlotsHandler.cs:17-24`, `InventoryConverter.cs:80-91`).
- 코드 확인: GearSlots와 Items가 이 칸을 읽지 않는다(AIInventory.cs:113-124).

### B7. 착용 중인 장비를 바로 옮기는 경로와 해제 목적지가 좁음 — 중요도: 있으면 좋음 (가방이 꽉 차면 중간)
- 빠진 것:
  - 착용 장비를 상자에 바로 보관하거나, 바닥에 내려놓거나, 동료에게 건넬 수 없다. 먼저 해제해야 한다.
  - 해제 목적지는 빈 주 가방 칸뿐이다. 가방이 꽉 차면 같은 타입 장비와 교체하는 방법 말고는 해제할 수 없다.
  - 상자에 있는 장비를 바로 착용하거나 손에 들 수 없다. take 다음에 equip/hold를 따로 해야 한다.
- 게임 근거: UI 칸 이동은 InventoryHandler/MoveAmount/Swap을 쓰며, 출발 칸은 착용 칸이나 상자 칸이어도 된다(`2026-11-07-행동결과완결` container-v2 수용, `game:completion:C:extended-transfer-v1` move 설명). 정확히 어떤 UI 칸에서 어떤 칸으로 옮길 수 있는지는 "추정"으로 둔다.
- 코드 확인:
  - store는 IsMain 슬롯에서만 나간다(AIInventoryMenu.cs:323, AIInventory.cs:661).
  - drop/give도 IsMain 슬롯만 가능하다(AIInventory.cs:679).
  - unequip은 빈 주 가방 칸으로만 간다(AIInventory.cs:501-502, 696).
  - 상자 물품에는 take만 나온다(AIInventoryMenu.cs:317).

### B8. 가방 안에서 자리를 옮기거나 스택을 나누고 합치는 선택이 없음 — 중요도: 있으면 좋음
- 빠진 것: 핫바 물품을 일반 가방 칸으로 보내기(빈 핫바 칸 만들기), 같은 품목 스택 합치기, 스택 일부를 다른 칸으로 나누기. 핫바로 넣는 쪽은 hold가 대신한다. 핫바가 가득하면 교체로 처리된다.
- 게임 근거: 같은 인벤토리 안의 Swap/MoveAmount(`Pug.Other/Inventory/Create.cs` MoveAmount·Swap, 가방 조사 `2026-11-07-가방장비-조사.md:73`).
- 코드 확인: 주 가방 칸끼리의 이동은 hold의 핫바 목적지 말고는 없다(AIInventory.cs:504-512).

### B9. 분류를 알 수 없는 물품(ObjectInfo 없음)은 조작이 하나도 안 나옴 — 중요도: 있으면 좋음(드묾)
- 코드 확인: `AIInventory.Info(frame.before) == null`이면 조작 단계가 비어 버린다(AIInventoryMenu.cs:316). 합의는 "분류를 판별하지 못한 항목은 명시적으로 남겨 접근 불가가 되지 않게 한다"였다(`tree-agreement.md` "만들 결과" 셋째 항목). 목록에는 남지만 내려놓기·보관 같은 조작으로 이어지지 않는다. DB에 없는 물품에만 해당한다.

### B10. 게임 단축 조작 — 중요도: 있으면 좋음(낮음)
- 빠진 것:
  - 횃불 빠른 교체: 게임 근거는 `Pug.Other/PlayerEquipment/InventoryQuickActionsSystem.cs:163-191`, 그래프 `game:completion:B:new-quick-swap-torch:v2`. hold로 횃불을 드는 것으로 대신할 수 있다.
  - 핫바 줄 전환: SWAP_NEXT/PREVIOUS_HOTBAR, `PlayerController.cs:1911-1973`.
  - 칸 잠금: ToggleLock, `InventoryHandler.cs:509`.
- 코드 확인: hold의 목적지가 핫바 0~9로 고정돼 있다(AIInventory.cs:506, 701). 칸 잠금은 AI에게 쓸모가 적다고 추정한다.

## 3. 알려진 후속과 경계

### 알려진 후속·명시 제외 (빠짐으로 세지 않음)

| 항목 | 분류 | 기록 근거 |
|---|---|---|
| 영구 삭제(trash) | 합의에서 명시 제외 | `2026-11-07-가방장비구현/agreement.md` 경계 줄, `tree-agreement.md` "영구 삭제(trash)는 별도 동작이며 이번에 추가하지 않는다" |
| 호스트·다른 AI 상자 접근, 자원 공유 정책 | 명시 제외(자기 상자 범위 유지) | agreement.md 경계 줄, 계획안 "별도 판단으로 남기는 것" |
| 자동 최적 장비 정책 | 명시 제외 | agreement.md 판단 주체 줄 |
| 개별 물품 먹기를 메뉴에 넣기 | 기록된 한계(할 일 없음) | tree-implementation.md:30. B4로 따로 적음 |
| 장비 성능 비교 정보가 적음 | 기록된 한계 | tree-agreement.md "실행·모델 연결 조건" 마지막 줄 |
| 가방 목표를 메뉴 없이 후보 하나로 바로 주는 구조 | 사용자 미결정 | todo.md `[이동·전달 개선 4/4]`(2026-11-08), "구조 변경은 합의된 선택 트리를 건드리므로 … 포함하지 않는다(사용자 미결정)" |
| 장비 전달 때 메뉴를 잘못 고르는 문제, 완료 거절 안내 | 후속 할 일 | todo.md 2026-11-08 "장비 전달의 메뉴 오선택과 탐험 왕복", `[이동·전달 개선 4/4]` |
| 가방·장비 트리 실게임 검증 | 후속 할 일(치트 환경 이후) | todo.md 2026-11-07 "치트 환경 구축 후 가방·장비와 생활 선택 트리의 실게임 검증", completion.md |
| 수리·강화·분해 | 다른 범위(장비 정비) | 계획안 "별도 판단으로 남기는 것", 정비 트리 |

### 경계 항목

- **B2 일괄 보관**: 생활 트리에도 "보관/꺼내기" 갈래가 있으므로(AIInventory.cs:241-307) 생활 점검과 겹친다.
- **예전 평면 take 숨김의 파급**: 배고플 때 자기 상자에서 음식 꺼내기(AIBase.cs:584 FoodTakeCandidates), 밭에 심을 씨앗 꺼내기(AIFarm.cs:771), 제작 재료 꺼내기(AIBase.cs:545-553) 후보는 모두 `kind="take"`이고 life 연결이 없다. 그래서 AIBrain.Loop.cs:1321에서 공개 선택면에서 지워진다. 남는 길은 가방 메뉴의 "자기 상자에서 꺼낼 물품 고르기"뿐이다. 생활(음식·농사·제작) 점검자가 의도된 동작인지 확인해야 한다.
- **B4 사용 아이템**: 소환 우상 사용은 귀환, 목줄·안장·펫 알은 동물·펫, 마나·전투 물약은 전투 준비, 탐지기는 탐험과 겹친다.
- **B5 펫 칸**: 동물·펫 주제(P.life)와 겹친다.
- **들기 이후 비무기 유지**: 횃불·삽 같은 물건을 들어도 S.weapon에 기록된다. 공격 때 TryChosenHand가 "현재 작업의 도구/무기가 아님(다른 물품으로 바꾸지 않음)"을 돌려준다(AIInventory.cs:804, 820). 전투 범위에서 볼 동작이다.
- **보조손 사용(USE_OFF_HAND)**, **설치물 놓기**, **바닥 물품 줍기**, **판매·구매(Sell/Buy/SellAll)**, **분해(SalvageAll)**: 각각 전투·설치·채집·거래·정비 범위다. 게임 근거는 `PlayerInput.cs` 입력 목록과 `InventoryAction` 열거형이다.

## 4. 확인하지 못한 것

- 실게임·실제 모델에서 메뉴의 각 조작이 성공하는지는 미확인이다. 실기 기록으로는 동료 전달(give) 3건 성공과 메뉴 오선택만 있다(`2026-11-08-observation-redecision/latency-loops/report.md:7-14`).
- AI 캐릭터가 파우치를 착용했을 때 실제 줍기나 꺼내기에서 물품이 파우치로 들어가는지는 실기로 확인하지 않았다. 게임 코드의 슬롯 탐색 경로만 확인했다(B1).
- AI 캐릭터의 핫바 줄(hotbarStartIndex)이 늘 0~9로 고정돼 있는지 확인하지 않았다. 모드는 0~9를 고정으로 가정한다.
- 착용 칸이나 상자 칸에서 다른 인벤토리로 바로 옮기는 UI 경로(B7)와, 게임 Sort·QuickStack의 정확한 결과 규칙은 행동 그래프 레코드가 없어 게임 원본 코드의 호출 위치까지만 확인했다.
- 큰 상자(칸이 많은 상자)에서 destinations 단계의 자식 수가 API 255개 상한에 닿는지는 계산하거나 시험하지 않았다.
- 행동 그래프 CLI가 "입력 원문 2개 변경/없음" 경고를 냈다. 어떤 원문인지, 가방 주제에 영향이 있는지는 확인하지 않았다.
- 정비순서 2번의 "공격 코드가 모델이 고른 무기를 유지"는 TryChosenHand 연결(AIInventory.cs:814-822, AICombatOptions.cs:33)까지만 읽었다. 공격·채굴 skill 안의 실제 사용 경로는 전투 범위라 따라가지 않았다.
