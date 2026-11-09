# 검수 — 제련(자동 가공)·공용 자원·늦은 검수 수정 (2026-11-09)

- 대상: worktree `C:\Dev\CoreKeeperModSDK-celeris`, 브랜치 ai-celeris-decisions-20261108, `git diff 4171d9b8..30c0093c`(21ccac87, 54c0e0e9, 30c0093c)
- 읽은 것: `AISmelt.cs`, `Core/AISmeltRules.cs` 전부, 관련 diff(AIPlanner·AIGoals·AIGoalFeatures·AIFieldChoices·AIMemory·AIPlanReport·AICraft·AIBrain.Prep·AIBrain.Preparation·AIBrain.Candidates·AICombat·AIAttackSkill·AITunnel 일부·protocol.mjs·brief.md)
- 디컴파일 원문: `_ref/corekeeper-decomp/Pug.Other/Inventory/InventoryUtility.cs`(ActivateRecipeSlot :3063-3101, MoveAllToOrDrop :3206-3245, FindAndMoveTo :2766-2793, HasMaterials… :2738-2764, GetRecipeInfo :2214-2234, GetNearbyChests… :3404-3433·:3796-3814, Craft :1640-1660·:1765-1810), `InventoryUpdateSystem.cs`(:409-465), `Create.cs`(:438-450), `ProcessResourcesCraftingUI.cs`(:93-109), `CraftingHandler.cs`(:455-480), `TriggerUIActionsSystem.cs`(:218-224)
- 작업자 보고는 근거로 쓰지 않았다. 코드는 고치지 않았다.

## 판정: 고친 뒤 합격

막을 것은 찾지 못했다. 사용자 기준 1~5는 코드 경로로 모두 성립한다. 다만 남의 가공을 꺼내 버리는 경우가 하나 있고(고칠 것 1), 꺼내기 단계가 스스로 끝나지 않는 경우가 하나 있다(고칠 것 2). 둘 다 몇 줄이면 고칠 수 있다.

## 직접 돌린 시험
| 시험 | 결과 |
|---|---|
| core-tests | ALL CHECKS OK (1761) |
| life-smelt-tests | ALL 47 CHECKS OK |
| path-preparation-tests | 37 OK |
| life-craft-tests | 61, 실패 0 |
| action-input-tests | `node run.mjs`로 52 통과. `dotnet run`만 하면 `.generated/Step.cs`가 없어 빌드되지 않는다(생성기 필요, 원래 구조) |

---

## 기준별 확인

### 1. 버튼 요청 경로(ActivateRecipeSlot → QueueCraftAction)
- **경로 근거는 있다.**
  - `Create.ActivateRecipeSlot`(Create.cs:438)은 `mainInventoryEntity = playerEntity`, `int0 = multiplier`, `bool0 = mod1`, `int1 = recipeIndex`로 만든다.
  - UIActionBuffer의 Craft는 TriggerUIActionsSystem:218에서 CraftBuffer로 간다. 그다음 InventoryUpdateSystem:409-431이 inventories를 [플레이어, 시설 위치 기준 근처 상자…]로 만든다. 마지막으로 `case CraftAction.ActivateRecipeSlot`가 `InventoryUtility.ActivateRecipeSlot(…, int0, bool0, int1)`을 부른다.
  - 거리 검사는 없다. 클라이언트 UI(ProcessResourcesCraftingUI:93-109)도 같은 데이터를 큐에 넣는다.
  - 모드의 QueueObservedUIAction은 AIMaintenance와 같은 함수다.
- **레시피 번호**
  - 서버 `GetRecipeInfo(index, craftingEntity, …)`(:2214)는 살아 있는 시설의 `CanCraftObjectsBuffer[index]`를 그대로 읽는다.
  - 모드 `RecipeIndex`(AISmelt.cs:174-181)도 살아 있는 시설 버퍼의 순번을 쓴다. 맞다.
- **양**
  - 서버 확인은 `HasMaterials…(multiplier=1)`이다. 옮기는 양은 `requiredObjectsToCraft[0].amount × (mod1 ? 10 : 1)`이고, FindAndMoveTo가 있는 만큼만 옮긴다.
  - 모드는 10개분이 다 있을 때만 mod1을 누른다(`PutResults`·`Presses`). 맞다.
- **바닥에 떨어지는 부작용은 디컴파일대로라면 피한다.**
  - MoveAllToOrDrop(:3206)은 끝 번호 −1을 반복 상한으로 쓴다. 그래서 두 반복(:3218, :3229)이 돌지 않고 DropItem(:3242)으로 간다. 작업자의 읽기가 맞다.
  - 모드는 누르기 직전마다 `Stale`(AISmelt.cs:648-651)로 입력 칸의 다른 재료와 출력 칸의 다른 결과물을 본다. 서버 조건 `objectID != 재료/결과물 objectID`(:3086, :3092)와 같은 비교다.
  - 남는 위험은 확인한 틱과 서버가 처리하는 틱 사이의 1~2틱뿐이다. 그 틈에 다른 플레이어가 다른 물건을 넣는 경우다. 자동 가공 자체는 다른 물건을 만들지 않는다.
- 실제 게임에서 처리되는 것은 아직 보지 않았다(작업자도 그렇게 적었다).

### 2. craft 거절
- 다음 경우에 거절 문구가 나오고 로그가 남는다(AIPlanner.cs:1113-1124).
  - `AISmelt.IsSmeltOnly`인 품목
  - where가 자동 가공 시설인 craft
- 준비 사슬(AIBrain.Prep.cs:80-97)과 F4(AICraft LifeEntries·RememberedStationFor)도 craft 단계를 만들지 않는다.

### 3. 공용 자원
- **같은 재료에 보태기:** PutBlock은 같은 재료를 막지 않는다.
- **남의 결과물 꺼내기:** TakeOutput과 TakeAmount는 주인을 보지 않는다.
- **근처 상자:** `AICraft.CraftChests`로 센다. 시설 기준 10칸·20개, 주인 무관이다.
- **가방에 둘지 상자에 넣을지:** `AfterTake`가 완료 근거에 판단 근거를 붙이고, 판단은 위쪽 LLM이 한다. 사용자의 "판단하에 알아서"와 맞는다. 빈틈은 작은 것 4다.

### 4. 늦은 검수 수정
- **F1**
  - `walls`에는 뿌리·물체 칸도 들어간다(AITunnel.cs:1043). 그래서 `OpenPlanC`의 'walls 0이면 그대로'는 참으로 열린 길이다.
  - walkOnly의 `Cost`(AITunnel.cs:938)는 Solid와 미관측(Cost −1)을 모두 막는다. 못 본 칸으로 길을 내지 않는다.
  - RetreatSkill.NextLeg(AICombat.cs:687-693), 물러나기 선택지(Candidates.cs:352-356), 실행 중의 가는 쪽(240)이 같은 기준을 쓴다.
  - 캐시 키는 따로 쓰고(`WalkOnlyKey`), ForgetPlan이 둘 다 지운다. 키 충돌은 좌표가 약 10억이 넘어야 생긴다. 실질적으로 없다.
- **F2:** 행 종류를 heal로 바꾸는 곳은 AIBrain.Loop.cs:1406 한 곳이다. TreeOnly 규칙은 AIFieldChoices.cs:111이다. 전투 중이거나 배고픔이 25 미만이면 남는다.
- **F3:** 1.3칸 안에서 휘두를 때 `_pathFails`를 되돌린다(AIAttackSkill.cs:169-172). 작은 것 5의 `_wayWorks`도 맞다.

### 5. 선택지 정리 규칙과 맞물림
- smelt·smeltwait 선택지는 `progress=true`라 빠지지 않는다.
- smeltwait는 NotBodyWork가 아니므로 일반 wait를 빼게 만든다.
- MenuFree에 Smelt가 들어가 생활 트리·가방 메뉴는 빠진다. 가방이 가득 차면(bagFull) 가방 메뉴는 남는다.
- 트리가 열린 동안 smelt 선택지는 TreeOnly로 빠진다. 규칙과 맞다.
- 모자란 광석은 PutShortage가 wanted에 넣으므로 채집이 남는다.

### 보고 숫자
- 디컴파일과 맞는 것
  - `GetNearbyChestsForCraftingByDistance` → `GetNearbyChestsByDistance(…, 10f, 20)`(:3798)
  - OverlapSphere(시설 위치, 10), `InventoryAutoTransferEnabledCD`, 가까운 순, 20개(:3404-3433)
  - inventories[0]이 플레이어다.
- 모드와 다른 것: 모드는 상자 중심까지의 xz 거리로 재고, 게임은 콜라이더까지의 거리로 잰다. 그래서 모드 셈이 게임보다 적게 나오는 쪽이다(보수적).

### memory.json 이전 형식
- 21ccac87은 숫자 7개를 썼다: `[per, sec*10, expected, carry, taken, put, ready]`.
- `FromSaved`가 이것을 읽는 방식(expected − taken, put = n[5], ready = n[6])은 맞다.
- 개수로 구별한다(count×7 / count×5). count>0이면 겹치지 않는다.
- 21ccac87은 설치되지 않았으므로 실제로 쓰일 일은 거의 없다.

### 시험이 무엇을 시험하는가
- life-smelt는 실제 `AISmelt.cs`와 Core 전체를 링크한다.
- 대역은 다음과 같다.
  - `AICraft.CraftChests`·`ChestMaterialCount`(Boundary.cs:110-117): 상자 고르기는 진짜 AICraft가 아니다.
  - 서버의 ActivateRecipeSlot
  - `AIPlanner.PlanRest`
- core SM과 path-preparation은 실제 Core와 실제 `AIBrain.Preparation.cs`를 시험한다.

---

## 막음
없음.

## 고칠 것

### 고칠 것 1. 지원하지 않는 레시피로 가공 중인 남의 재료를 '가공되지 않는 나머지'로 꺼낸다
- **위치:** `Core/AISmeltRules.cs:151`(`Stuck`: `inputRecipe == null`이면 막힘으로 봄), `AISmelt.cs:167-172`(`InputRecipe`가 `Recipe()`를 씀), `AISmelt.cs:137-162`(태그 레시피와 1회 출력이 1이 아닌 레시피에 null을 돌려줌), `AISmelt.cs:568-570`(clear "input" 선택지)
- **상황**
  - 시설에 태그 레시피나 1회 여러 개 레시피가 있고, 다른 플레이어가 그 재료를 넣어 가공 중이다.
  - AI가 같은 시설에 `smelt put CopperBar`를 받는다.
  - `InputRecipe`가 null이 된다. 그러면 `Stuck`이 참이 되고, ClearFirst가 "input"을 돌려준다.
  - 그 결과 `먼저 꺼내기 — 입력 칸의 …를 가방으로(가공되지 않는 나머지…)` 선택지가 나오고, 가공 중인 남의 재료를 AI 가방으로 빼 온다.
- **근거**
  - 작업자 문서의 규칙("다른 재료가 가공 중이면 꺼내지 않는다… 남의 가공을 끊지 않음", smelting.md 241행)과 어긋난다.
  - 'smelt가 지원하지 않음'과 '그 시설이 가공하지 않음'은 다르다. 그런데 같은 null로 판정한다.
- **고치는 방법**
  - InputRecipe가 지원 여부와 상관없이 그 시설 레시피의 재료와 맞는지 보게 한다. 지원 안 함이면 '가공 중으로 보고 기다림'으로 둔다.
  - 또는 보이는 타이머(`TimerLeft > 0`)가 있으면 막힘으로 보지 않게 한다.

### 고칠 것 2. 꺼내기 단계에서 가공 타이머가 멈춘 시설을 하염없이 기다린다
- **위치:** `AISmelt.cs:616-622`(타이머와 상관없이 `coming > 0`이면 smeltwait 선택지), `AISmelt.cs:623`(타이머가 안 보인다는 이유 문구는 `res.Count == 0`일 때만 나와서 사실상 닿지 않음), `AISmeltRules.cs:221`(TakeDone이 `Coming > 0`이면 끝나지 않음)
- **상황**
  - 시설이 보이는데 입력에 1개분 이상이 있고 타이머가 돌지 않는다. 예: 전기가 필요한 시설, 자동화 비활성, 그 밖에 가공이 멈춘 상태.
  - 이때 count 없는 `smelt take`(또는 아직 count에 못 미친 take)는 매번 '곁에서 기다리기 1~10초'만 고른다.
  - 그 결과 단계가 스스로 끝나지도 정체되지도 않는다. smeltwait가 `progress=true`라 goal_stalled도 나지 않는다.
  - 위쪽 LLM은 정기 점검(약 2분) 때 smelt_stations의 '타이머가 보이지 않음'을 보고서야 바꿀 수 있다.
- **근거:** `StateText`가 이미 이 상태를 '멈췄을 수 있음'으로 구분한다(AISmeltRules.cs:287). 그런데 선택지 쪽은 이것을 쓰지 않는다.
- **고치는 방법:** 보이는 시설의 `TimerLeft < 0`이 몇 초 이어지면 smeltwait를 내지 않는다. 그러면 why가 정체 문구로 올라간다. 피스 사이 한 틱 동안 타이머가 비는 것은 몇 초 유예로 흡수한다.

## 작은 것

### 1. 같은 put 목표를 다시 set하면 넣은 양을 처음부터 다시 센다
- **위치:** `AISmelt.cs:384-389`(진척을 GoalRevision 단위로 둠), `AIGoals.cs:239`(SetGoal마다 GoalRevision++)
- **상황:** 대량 put(예: 50개분, 중간에 광석 모으기) 도중에 smelt_ready나 periodic 답이 같은 `smelt put`을 keep이 아니라 set으로 다시 준다. 그러면 putUnits가 0부터 다시 시작해, 이미 넣은 만큼을 한 번 더 넣는다.
- **근거:** gather와 craft는 가방 수량으로 진척을 재므로 다시 set해도 괜찮다. put은 메모리 안의 누계만 쓴다.

### 2. 보낸 뒤 확인 전에 끊기거나 동시에 꺼내면 누름이 한 번 더 나간다
- **위치:** `AISmelt.cs:748-753`(`Added`의 produced = max(0, 출력 − 이전 출력)), `AISmelt.cs:820-824`, `AISmelt.cs:829`(Cleanup이 Confirmed를 부르지 않음)
- **상황**
  - 누른 뒤 확인하기 전에 전투 등으로 스킬이 끊긴다.
  - 또는 확인 사이에 한 개가 다 되면서 남이 출력을 꺼낸다. 이때 ×1 누름이 added 0으로 보인다.
  - 결과: 6초 뒤 실패하거나 일부만 친다. 다음 선택이 같은 양을 다시 눌러, 1개분(×10이면 10개분)을 더 넣는다. take도 같은 식으로 count보다 더 꺼낼 수 있다.
- **근거:** 확인은 관측 차이로만 하고, 보낸 누름의 기록은 스킬 객체 안에만 있다.

### 3. 시설 자신이 '근처 상자'에 들어가는지 확인하지 않았다
- **위치:** `AICraft.cs:88-106`(CraftChests는 player와 PlayerGhost만 뺀다. 시설은 빼지 않는다)
- **상황:** 용광로 프리팹에 `InventoryAutoTransferEnabledCD`가 있다면 다음 두 일이 생긴다.
  - 게임: 거리 0이라 플레이어 다음 첫 순위다. FindAndMoveTo가 시설 입력 칸 0에서 칸 0으로 옮기며 남은 양을 깎는다(:2780-2785). 그래서 가방이 모자랄 때 상자 재료가 덜 옮겨진다.
  - 모드: 입력 칸의 광석을 '시설 근처 상자' 재료로 두 번 센다.
- **근거:** 이 컴포넌트를 누가 갖는지는 에셋 정보라 디컴파일로 알 수 없다. 실게임에서 `/ai smelt`의 상자 수와 실제 상자 수를 비교하면 확인할 수 있다.

### 4. 먼저 꺼낸 남의 결과물에는 보관 판단 근거가 붙지 않는다
- **위치:** `AISmelt.cs:436`(put 완료 문구), `AISmelt.cs:788-789`(clear 확인 Facts)
- **상황:** put 전에 출력의 TinBar를 가방으로 꺼낸다. 그러나 완료 근거와 다음 보고 events에 이것이 없어서, 위쪽 LLM이 store 단계를 넣을 계기가 없다.
- **근거:** brief는 '먼저 꺼낸 다른 결과물 포함' 보관을 요구하는데, AfterTake는 take 완료에서만 부른다.

### 5. smelt_alert가 덮어쓰이고 events에도 같은 문장이 두 번 들어간다
- **위치:** `AIPlanner.cs:543-544`
- **상황:**
  - 보고가 가는 동안 2초 간격으로 다른 시설이 다 되면, 앞의 smelt_alert가 덮어쓰인다.
  - 같은 문장이 events에도 들어간다.
- **근거:** 같은 커밋 묶음이 gear_alert는 이어 붙이고 events에서 뺐다(작은 것 10). smelt 쪽만 예전 방식이다.

### 6. smelt_ready가 다시 불러올 때마다 한 번 더 온다
- **위치:** `AISmeltRules.cs:310`(Numbers에 Alerted 없음)
- **상황:** 예상 끝이 지난 기록이 남은 채로 월드를 다시 불러온다. 그러면 Alerted가 false로 돌아와 smelt_ready가 한 번 더 온다.

### 7. where 없는 put이 막힌 가까운 시설을 골라 거절된다
- **위치:** `AISmelt.cs:338`, `AISmelt.cs:367-371`
- **상황:** 용광로가 둘 있고 가까운 쪽에 다른 재료가 가공 중이다. where 없는 `smelt put`이 가까운 쪽으로 정해지고 목표 검사에서 거절된다. 비어 있는 다른 용광로는 고르지 않는다.
- **근거:** Resolve는 레시피가 있는지만 본다.

### 8. 출력이 다른 결과물로 막혀 멈춘 입력을 '가공 중'이라고 말한다
- **위치:** `AISmeltRules.cs:147`, `AISmeltRules.cs:151`, `AISmeltRules.cs:156-159`
- **상황:** 입력에 TinOre 10, 출력에 CopperBar 3이 있다. 실제로는 TinBar가 나올 수 없어 멈춘 상태다. 이때 `smelt put CopperBar`가 "다른 재료가 가공 중 — 기다림"으로 거절된다. 출력의 CopperBar를 꺼내면 풀리는데 그 안내가 없다.
- **근거:** ClearFirst가 입력을 먼저 보고 null을 돌려주므로 출력 검사까지 가지 않는다.

### 9. 화면 밖 시설의 입력 칸 한도를 다른 재료와 상관없이 센다
- **위치:** `AISmelt.cs:676`
- **상황:** 기억한 시설의 입력에 CopperOre가 있는데, TinBar 줄에 '입력 칸 한도 9999개'로 나온다. 실제로는 넣을 수 없다(버튼을 누르면 바닥에 떨어지는 상태다).

### 10. 자동 가공 재료(광석)를 가방만 세어 모자라다고 본다
- **위치:** `AIBrain.Prep.cs:91`
- **상황:** craft CopperMiningPick 단계에서 CopperBar가 모자라다. 이때 광석 부족을 가방만으로 센다. 용광로 10칸 안 상자에 광석이 충분해도 NoteMissing·wanted가 생겨, 광석 캐기와 상자에서 꺼내기 선택지가 붙는다.
- **근거:** PutShortage(AISmelt.cs:475-476)는 상자까지 센다. 같은 판단을 두 곳이 다르게 한다.

### 11. F1이 파 들어가기를 길 길이와 상관없이 뺀다
- **위치:** `AIBrain.Preparation.cs:228-229`, `AIBrain.Candidates.cs:308`, `AIBrain.Candidates.cs:525`, `AIBrain.Candidates.cs:658`
- **상황:** 단지 하나를 부수면 3칸인데 열린 우회로가 80칸이다. 이때도 적·물체 옆·떨어진 아이템 쪽 파 들어가기가 빠진다.
- **근거**
  - 문구가 '열린 길 없음'으로 틀리지 않게 하려는 의도는 맞다. 그러나 비용을 비교하지 않는다.
  - 물체마다 네 옆칸에 걷기 전용 탐색이 한 번씩 더 든다(캐시가 있음).
  - 물러나기에는 회귀가 없다. 부수는 계획에 벽이 0개면 전과 같고, 걷기 전용 계획은 새로 성공하는 경우만 더한다.

---

## 확인하지 못한 것
- 레시피 버튼이 실제로 처리되는지, ×10이 10개분을 옮기는지, 끝 번호 −1에서 정말 바닥에 떨어지는지는 실게임 확인이 필요하다.
- Furnace CopperBar가 빠지지 않는지는 에셋을 봐야 한다. `CanCraftObjectsBuffer.amount`·`initialAmount`가 1이고 태그 레시피가 아니어야 한다. 카탈로그와 `not supported` 로그로 확인할 수 있다.
- 작은 것 3: 시설에 `InventoryAutoTransferEnabledCD`가 있는지.
- Pug.Automation.dll(자동화 타이머) 원문은 `_ref`에 없다. AICook 머리말의 이전 분석을 따랐다.
