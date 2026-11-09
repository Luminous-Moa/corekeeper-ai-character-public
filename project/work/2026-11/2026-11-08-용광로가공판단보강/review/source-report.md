# ProcessResources 가공 사슬 보완: 독립 원문 검수 보고

- 검수 일자: 2026-11-09
- 검수 대상 계약: `contract.json` SHA-256 `dd458c46ab3dcf79a11da534da552769c2022ca9184b4942188fd4ddbbc21289`
- 검수한 후보: `candidate-review/`
  - graph.json `debdc024b710476d5cab2048afe60957ce737bdb05cb7f2332a3f18dd456ddc5`
  - coverage.json `7f0720ae1448df30e0f757531dd4095a9cb52ba7d7fc903c9b687ab218d7ac94`
  - manifest.json `1d07f03d3f2c1f5e9ddaeea4e5d5736ad889e87938e6596388f84dbcb6f7b47f`
- 근거 루트: `C:\Dev\.head\sessions\코어키퍼-AI-캐릭터\work\게임원본-상호작용\code\`(아래 경로는 이 루트 기준), `...\게임원본-상호작용\raw\components.jsonl`, `...\raw\objects.json`
- 판정: **changes_required**(열린 지적 3건)

기록 본문은 믿지 않고 인용된 줄을 직접 읽었다. 인용이 가리키는 범위 밖의 호출 함수(TryFindSlotToAddTo, ObjectIsValidToPutInInventory, GetTotalAmount, RecipeInfo 등)까지 따라갔다. 원본 authoring은 components.jsonl 전체를 한 번 훑어 CraftingAuthoring과 같은 GameObject에 붙은 컴포넌트를 모아 확인했다. 실게임은 실행하지 않았다.

---

## 1. 무결성·형식

- supplement.json 4건의 codeReferences 전부를 다시 해시했다. 경로·sha256·줄 범위가 모두 실제 파일과 맞고, conditions/flow/results/failures의 `refs`가 모두 같은 기록의 codeReferences id를 가리킨다(누락 0).
- contract.json의 pins 29개를 모두 다시 해시했다. 전부 일치한다. `baselineGraphSha256`(`a16c7394…`)은 `baseline-260/graph.json`의 실제 해시와 같다.
- supplement.json 최상위와 계약은 `runtimeTested=false`, `wholeScopeComplete=false`다.

## 2. 원문 대조: 확인된 사실

### 2.1 recipe-activation-v1

| 진술 | 원문 | 결과 |
|---|---|---|
| ProcessResources만 processResourcesCraftingUI를 연다. Simple은 simpleCraftingUIContainer | Pug.Other/UIManager.cs:799-834(switch 전체), Pug.Base/CraftingType.cs:1-14(ProcessResources=1) | 확인. Cooking/Extract/Incinerate/Fishing 등은 각자 다른 UI라 이 UI를 쓰지 않는다 |
| IsProcessAutoCrafter에 ProcessResources 포함 | Pug.ECS.Components/CraftingCD.cs:22-30 | 확인 |
| hideRecipes면 레시피 칸 숨김 | Pug.Other/ProcessResourcesCraftingUI.cs:39-55 | 확인 |
| 클라이언트 관문: 재료 정확히 1종 + 가방·근처 상자 재료 | ProcessResourcesCraftingUI.cs:93-108, CraftingHandler.cs:294-320(inventories=[player]+nearbyChests) | 확인. Create.ActivateRecipeSlot(…, 1, mod1, visibleSlotIndex)도 맞다(Inventory/Create.cs:438-450) |
| RecipeSlotUI 클릭·누르고 있기 반복 | Pug.Other/RecipeSlotUI.cs:91-131 | 대체로 확인(표현 메모 N1) |
| 읽기 전용 월드(WorldInfoCD.guestMode)에서 adminPrivileges<=0이면 건너뜀 | Inventory/InventoryUpdateSystem.cs:414-417, 738/752 | 확인 |
| inventories = [요청 플레이어] + 시설 반경 10, InventoryAutoTransferEnabledCD, 거리순 최대 20 | InventoryUpdateSystem.cs:422-431, Inventory/InventoryUtility.cs:3404-3431, 3796-3799, 3803-3813 | 확인(mainInventoryEntity=playerEntity, Create.cs:446) |
| 7개 원본 autoTransferEnabled=0이라 시설 자신은 목록에 없음 | components.jsonl 436, 32429, 28963, 43384, 35714, 33476, 67869 | 확인(7개 모두 0) |
| GetRecipeInfo: index 범위 밖이거나 DB에 없으면 None | InventoryUtility.cs:2214-2235 | 확인 |
| HasMaterials: max(1, round(amount×비용 배수))×multiplier와 inventories 합계 비교 | InventoryUtility.cs:2743-2764 | 확인 |
| 누른 레시피를 시설에 쓰지 않는다. 실제 제작 레시피는 타이머 단계가 다시 고른다 | InventoryUtility.cs:3063-3102(쓰기는 slot 0/출력 정리와 FindAndMoveTo뿐), PugAutomationStartCraftSystem.cs:200-221 | 확인 |

### 2.2 material-move-v1

- **MoveAllToOrDrop(…, 0, -1, position)의 떨어뜨림**: InventoryUtility.cs:3206-3244를 직접 확인했다. `indexToHint` 기본값이 -1이라 3214 분기를 건너뛴다. 두 반복문은 `for (i = otherStartIndex(0); i < otherEndIndex(-1); …)`라 한 번도 돌지 않는다. 그래서 3240에서 `DropItem(…, int.MaxValue, position)`으로 칸 전체를 떨어뜨린다. DropItem(695-735)은 쌓이는 물건이면 `min(수량, 9999)`를 칸에서 빼고, isFirstTimeFullyPredictingTick일 때만 ECB로 떨어진 엔티티를 만든다. 서버 경로에는 -1을 끝 인덱스로 바꿔 주는 처리가 없다. 같은 호출 형태가 2655/2661/2670/3294에도 있다. **기록의 "가방에 넣지 않고 플레이어 위치에 떨어뜨린다"는 정적 읽기로 맞다.** 기존 process_recipe_selection result의 "back to player or drop"과 craft-diagnosis의 "가방으로 돌려준다"는 정적 코드와 다르다. 기록 boundary가 이 차이를 실게임 확인 대상으로 남긴 처리도 적절하다.
- **출력 칸 정리**: 3091-3095. 레시피 제품과 다른 물건만 떨어뜨리고 같은 제품은 둔다. 확인.
- **mod1 ×10과 1회분 확인**: 3080(HasMaterials에 multiplier=int0 전달)과 3096(`mod1 ? amount*10 : amount`). 확인. 비용 배수는 옮길 양에 들어가지 않는다. 확인.
- **FindAndMoveTo**: 2766-2794. `num2 -= num3`가 MoveAmount 호출 앞에 있다. 실패한 몫을 다시 채우지 않는다는 진술이 맞다. variation은 `new ObjectDataCD{objectID}`라 0으로만 찾는다. hint는 `indexToHint=0`이라 num=0이다. 확인.
- **목적지가 입력 칸 0뿐**: TryFindSlotToAddTo(509-588)에 endIndex=-1, hint=0을 주면 각 InventoryBuffer 구간 `[startIndex, startIndex+size)`만 훑는다. InventoryBuffer.size는 `sizeX*sizeY+extraSize`다(Pug.ECS.Components/InventoryBuffer.cs:38). InventoryConverter는 SetupInventory(186-273) 뒤 SetupCrafting(278-328)에서 출력 칸을 `AddToBuffer`로 맨 뒤에 붙인다(Pug.ECS.Conversion/InventoryConverter.cs:36-41, 318-321). 7개 원본은 모두 1×1에 maxExtraSize 0이다(raw). 확인.
- **9999 상한**: TryFindSlotToAddTo 555(같은 물건·variation·<9999)과 GetAmountToMoveToInventory 2549-2564(`min(출처, 요청, 9999-목적지)`). 확인.
- **변경이 타이머 단계를 다시 평가하게 함**: TriggerCrafterChangedSystem.cs:37-52(ContainedObjectsBuffer·PugAutomationCD·ObjectDataCD chunk 변경이나 순서 변경). 확인.

### 2.3 craft-timer-v1

- crafter 엔티티 생성: PugAutomationInstantiateSystem.cs:1294-1365. `type & Crafter`이고 isActive일 때 CraftingTimerSlotBuffer 길이만큼 만든다. ProcessResources는 crafterArchetype과 CraftingTimerTriggerCD를 쓴다. 확인. Crafter 타입은 AutomatedCrafterAuthoring에서 온다(Pug.Automation.Conversion/PugAutomationConverter.cs:23-26). 7개 원본 모두 AutomatedCrafterAuthoring이 있다(raw).
- isActive = ElectricityCD 없음 또는 hasEnoughElectricityToPowerStuff: 1125. 확인.
- ElectricityAuthoring은 4009(components.jsonl:62223)와 1200(:82374)에만 있고, 3905·4004·4015·4032·4044에는 없다. raw 전체 훑기로 확인.
- StartCraft 질의는 BigEntityCraftingDataChangedTriggerCD 변경 필터를 쓴다: PugAutomationStartCraftSystem.cs:70-81. 확인.
- 레시피 선택은 inventoryEntities=[시설]에서 CanCraft가 참인 첫 i다. 없으면 0/-1이다. i가 바뀌면 시간을 DB craftingTime으로 두고 override>0이면 그 값을 쓴다. 같으면 유지한다. 타이머 없음·isActive이면 StartTimer(min(1, 남은 시간))다: 180-226. 확인(표현 메모 N2).
- PugAutomationCraftingSystem.cs:162-222: 비활성이면 감소 없이 return한다(171-174). `timeLeftToCraft--` 뒤 남은 시간이 0보다 크면 1초 타이머를 다시 건다(191-196). index가 범위 밖이면 return한다(198-201). 확인.
- 예시 "4004의 제품 1001은 7": raw objects.json 1001 craftingTime=7.0. 확인.

### 2.4 output-v1

- `Craft(시설, mainEntityObjectData, [시설], objectToCraft, 0, 위치, useCraftingCostMultiplier:false)`이고 playerEntity는 기본값(Null)이다: PugAutomationCraftingSystem.cs:207. 확인.
- CanCraft의 Cooking이 아닌 분기(InventoryUtility.cs:1644-1704): 출력 칸이 None이 아니면서 (쌓이지 않음 ∨ 다른 제품 ∨ ≥9999)이면 거짓이다. 태그 레시피는 태그 물건이 하나 이상 있으면 참이다. 그 밖에는 각 재료를 `round(amount×1)×레시피 amount`만큼 시설 버퍼 전체(출력 칸 포함)에서 센다. 확인.
- Craft의 Cooking이 아닌 분기(1756-1853): 재료는 ConsumeObject로, 태그 레시피는 ConsumeObjectWithTag로 소비한다. 출력은 `amount>0`이면 쌓이는 물건은 initialAmount×amount, 아니면 initialAmount다. 출력 칸이 None이면 새로 쓰고 아니면 더한다(9999 초과 검사 없음). playerEntity가 Null이라 스킬은 오르지 않는다. 확인.
- 연속 가공: 208-221. 확인.

## 3. 원본 신원(sourceKey·objectId·variation)

components.jsonl을 전부 훑어 CraftingAuthoring을 craftingType별로 셌다. 결과는 `{0:50, 4:14, 1:7, 7:6, 2:3, 5:2, 3:1, 6:1, 8:1, 9:1, 10:1}`이다. craftingType==1은 정확히 7개이고 모두 m_Enabled=1이다.

| CraftingAuthoring 줄 | EntityMonoBehaviourData(sourceKey) | objectId/var | 그래픽 컴포넌트(hideRecipes) | Electricity |
|---|---|---|---|---|
| 28914 | …:1851371803100069835 | 3905/0 | SantasLittleHelper(0) | 없음 |
| 75121 | …:7493579351412767700 | 4004/0 | Furnace(0) | 없음 |
| 32584 | …:8084277474615349155 | 4009/0 | TableSaw(0) | 62223 |
| 43743 | …:-4282708749011488034 | 4015/0 | SmelterKiln(0) | 없음 |
| 74095 | …:-7713936278173450791 | 4032/0 | FuryForge(0) | 없음 |
| 69335 | …:7364225312847973667 | 4044/0 | GlassSmelter(0) | 없음 |
| 69100 | …:-5362974161099365890 | 1200/0 | EggIncubator(1, 줄 3711) | 82374 |

sourceBindings는 원본마다 별개의 sourceKey/objectId/variation으로 적혀 있고 합쳐진 곳이 없다. 1200은 recipe-activation·material-move에서 빠지고 craft-timer·output에만 묶였다. 계약과 맞다. codeReferences의 raw 줄(28914/32584/43743/69100/69335/74095/75121, 62223/82374, 67869=4004 InventoryAuthoring)도 위 대응과 맞다.

## 4. 상태 규율·기존 행 보존

- 후보 노드 status는 pending_independent_review이고 coverage `processResourcesCompletionStatus`도 pending이다. acceptedBatches는 260 그대로이고 runtimeTested·wholeScopeComplete는 false다. boundary마다 실게임 미관측이 적혀 있다.
- 직접 점검: baseline-260과 candidate-review의 `base:D::CAB-2ebcba97367158c9b08f790c25d642c9:7493579351412767700#inventory_transfer` 노드는 같다(dict 동등). 이 노드에서 나가는 간선은 `has_process_resources_supplement → craft-timer-v1`과 `→ output-v1` 2개만 늘었고 사라진 간선은 없다. 들어오는 간선은 `clarifies_open_statement` 2개가 늘었다.
- 노드는 251477에서 251522로 45개 늘었다(새 기록 44개와 acceptance 1개). 간선은 350373에서 350534로 늘었다. verify.py summary(priorNodesPreserved 251477, priorEdgesPreserved 350373, origin 45건 확인, CLI 질의 전부 exit 0)와 맞다.

## 5. 생성기 diff(이동수면판단보강 대비)

overlay.py, compile_contract.py, verify.py, install_reviewed.py, check_published_inputs.py는 이름·숫자·relation·recordFormat 치환만 달라졌다. finalize_reviewed.py와 query_published.py는 같다. overlay.py:129는 decision_evidence가 받는 recordFormat에 `process_resources_decision_connection`을 더했다. 의도한 변경이다. 의도하지 않은 남은 문자열 1건은 F3에 적었다.

---

## 6. 열린 지적

### F1. 4044 GlassSmelter에서는 레시피 클릭 경로가 정적으로 성립하지 않는데 has_completion으로 묶였다

- 기록: recipe-activation-v1과 material-move-v1. summary는 "ProcessResources 시설(3905·4004·4009·4015·4032·4044)에서 레시피 칸을 눌렀을 때…"이고, sourceBindings에 4044 has_completion이 있다.
- 근거: 4044의 유일한 레시피 제품은 1025 Glass다(components.jsonl:69335). raw objects.json에서 1025는 `requiredObjectsToCraft=[]`이고 `craftingSettings.canOnlyUseAnyMaterialsWithTag=29`(Sand)다. 클라이언트 관문 `recipeInfo.requiredObjectsToCraft.Count == 1`(ProcessResourcesCraftingUI.cs:100)은 RecipeInfo가 ObjectInfo.requiredObjectsToCraft를 그대로 쓰므로(CraftingHandler.cs:14-36) 거짓이다. 서버 관문 `requiredObjectsToCraft.Length == 1`(InventoryUtility.cs:3080)도 거짓이다. 그래서 4044에서 레시피를 눌러도 요청이 큐에 들어가지 않고 재료도 옮겨지지 않는다.
- 문제: 일반 조건 "재료가 정확히 1종"으로는 걸러지지만, 기록은 4044를 이 경로가 있는 시설로 명시하고 묶었다. 그 결과 판단 단계는 "유리 제련기에서 레시피를 누르면 모래가 입력 칸에 들어간다"고 읽게 된다. 실제로는 모래를 손으로 넣어야 하고, 넣은 뒤 타이머·출력 단계(태그 분기)만 성립한다.
- 요청: 4044를 두 기록에서 빼거나, 원본별 분기로 "클릭 경로 없음(재료 0종·태그 레시피), 손으로 넣기만"을 명시한다.

### F2. 3905 SantasLittleHelper에서는 재료 이동이 입력 칸에 들어가지 못하고, 넣어 둔 음식을 떨어뜨리는 부작용만 남는다

- 기록: material-move-v1의 결과 "입력 칸 0에 재료가 레시피 1회분(mod1이면 10회분)까지 쌓이고…". recipe-activation-v1도 3905를 묶었다.
- 근거: 3905 레시피 10200·10206(components.jsonl:28914)은 objects.json에서 `requiredObjectsToCraft=[5192 LumpOfCoal ×1]`·`[5192 ×2]`이면서 `canOnlyUseAnyMaterialsWithTag=17/18`(UncommonOrLower/RareOrHigher CookedFood)이다. 3905 입력 칸의 slotRequirements는 태그 [17,18]만 받는다(components.jsonl:436). LumpOfCoal의 태그는 [38](Valuable)이다(objects.json 5192, Pug.Base/ObjectCategoryTag.cs:1-48). 흐름은 다음과 같다.
  1. 가방·근처 상자에 석탄이 있으면 클라이언트·서버 관문(재료 1종, 수량 충분)을 통과한다.
  2. 입력 칸 0에 석탄이 아닌 물건, 곧 이 시설에 맞게 넣은 음식이 있으면 MoveAllToOrDrop이 그것을 플레이어 위치에 떨어뜨린다(InventoryUtility.cs:3086-3089, 3206-3244). 출력 칸에 다른 선물이 있으면 그것도 떨어뜨린다.
  3. FindAndMoveTo → TryMove → TryFindSlotToAddTo는 ObjectIsValidToPutInInventory(2368-2455)에서 태그 요구 불충족(FailedRequirement)으로 -1을 돌려준다. 석탄은 옮겨지지 않는다.
- 문제: 3905에서는 "재료가 입력 칸에 쌓인다"는 결과가 정적으로 틀린다. 실제로 남는 것은 "입력 칸의 음식이 바닥에 떨어진다"는 손해다. 일반 실패 문구("slotRequirements가 그 재료를 받지 않으면 옮기지 않는다")가 있지만, 이 원본에서는 이 실패가 늘 일어난다. 떨어뜨림과 겹쳐 판단을 오도한다.
- 요청: 3905를 recipe-activation·material-move에서 빼거나, 원본별로 "DB 재료(석탄)와 칸 요구(조리 음식 태그)가 맞지 않아 클릭은 입력 칸을 비우기만 함, 재료는 손으로 넣기"를 명시한다. 실게임 확인 대상으로도 남긴다.

### F3. 후보 manifest의 publication.scope가 이전 보완(이동·수면)의 설명으로 남았다

- 위치: overlay.py:96. candidate-review/manifest.json `publication.scope` = "Four bounded movement and condition-sleep continuations over preserved accepted260; no runtime success inferred."
- 원인: prepare_framework.py:12가 `accepted256`을 `accepted260`으로 먼저 바꾼다. 그래서 prepare_framework.py:20의 의도한 치환("…over preserved accepted256…"을 "ProcessResources recipe activation, material move, automation timer and output chain over preserved accepted260…"으로)이 더는 일치하지 않아 적용되지 않았다.
- 문제: 게시될 manifest가 이번 보완의 범위를 틀리게 설명한다. 기록 내용과는 별개지만 게시물의 진술이 틀린다. overlay.py가 계약 pin에 들어 있으므로 고치면 계약 SHA가 바뀐다.

---

## 7. 의미를 바꾸지 않는 표현 메모(열린 지적 아님)

- N1. RecipeSlotUI 누르고 있기: 첫 클릭 뒤 두 번째 실행은 약 0.35초 뒤이고, 그 뒤부터 0.1초 간격이다(RecipeSlotUI.cs:93-113). "둘째부터 0.1초 간격"은 다소 압축된 표현이다.
- N2. 타이머 시간: `instantCraftingLocal`이면 0을 둔 뒤에도 `craftingTimeOverride>0`이면 그 값으로 덮인다(StartCraft 215-219, Crafting 210-214). 기록의 괄호 순서는 즉시 제작이 우선인 것처럼 읽힌다. 7개 원본의 authoring craftingTime(override)은 모두 0.0이라 실제 영향은 없다.
- N3. 서버가 받는 multiplier는 클라이언트가 보낸 int0이다(바닐라 클라이언트는 1). "multiplier=1"은 바닐라 기준으로 맞다.
- N4. HasMaterials의 GetTotalAmount(InventoryUtility.cs:228-243)는 variation을 가리지 않고 센다. 이동은 variation 0만 한다. 그래서 확인은 통과하고 0개가 옮겨질 수 있다. 기록의 variation 실패 문구와 합치면 추론할 수 있다.
- N5. 같은 칸에 더해 쌓으려면 재료가 stackable이어야 한다(TryFindSlotToAddTo 524, 555). 대상 원본의 1종 재료(광석 등)는 모두 stackable이라 실질 차이는 없다.
- N6. "플레이어 없음이면 아무것도 바뀌지 않는다"는 클라이언트 관문(ProcessResourcesCraftingUI.cs:96)으로 읽으면 맞다. 서버 쪽에서 playerEntity가 Null이면 inventories[0]이 상자일 수 있고, ActivateRecipeSlot의 `localTransformLookup[playerEntity]`(3082)에서 예외나 정의되지 않은 동작이 된다. 바닐라 경로에서는 생기지 않는다.
- N7. CanCraft의 "시설 인벤토리 전체"에는 출력 칸도 들어간다. 기록 표현("전체")과 맞다.
- N8. 인스턴스화 시점에 전력이 없으면 crafter 엔티티 자체가 만들어지지 않는다(1294). "isActive=false면 타이머를 시작하지도 줄이지도 않는다"와 모순되지 않는다. boundary의 "자동화 재구성" 미관측 항목이 이 부분을 덮는다.

## 8. 결론

사슬의 코드 사실은 서버 활성 관문, 떨어뜨림(정적), ×10과 1회분 확인, 남은 요청량 선감소, variation 0, 9999, 입력 칸 0 단일 목적지, 근처 상자 10/20, 읽기 전용 월드 관리자 예외, 타이머 재평가·초기화, 1초 감소, 비활성 return, Craft 무배수·무플레이어, 연속 가공까지 원문과 맞는다. 원본 신원 7개, Electricity(4009·1200), hideRecipes(1200)도 raw로 확인했다. 기존 행 보존도 확인했다.

다만 원본별로 묶은 결과가 4044(클릭 경로 자체가 없음)와 3905(재료가 칸 요구와 맞지 않아 떨어뜨림만 남음)에서 정적으로 성립하지 않는다(F1, F2). 게시 manifest의 범위 설명도 틀렸다(F3). 이 세 건을 고친 뒤 계약을 다시 고정하고 재검수해야 한다. runtimeTested=false, wholeScopeComplete=false.
