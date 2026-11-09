# 5번 — 구리 주괴 제작 선택지가 안 나오는 원인 (2026-11-08)

커밋 `d3fcf40f`(브랜치 ai-celeris-decisions-20261108, worktree `C:\Dev\CoreKeeperModSDK-celeris`, a6320486 위). 설치·브리지 재기동은 하지 않았다.

## 판정: (b) 용광로는 실제로 자동 가공이다. 필터는 맞다.
`AICraft.ManualType()`이 `CraftingCD.IsProcessAutoCrafter(type)`인 제작대를 빼는 것은 게임 규칙과 맞다. 11-04 판에 '만들기 — CopperBar … 제작대 Furnace' 선택지가 나온 것은 이 필터가 생기기 전이었기 때문이다. 그때 나온 선택지가 오히려 게임 규칙에 맞지 않았다.

### 근거
| 확인한 것 | 근거 |
|---|---|
| `IsProcessAutoCrafter`가 참인 타입 | `ProcessResources`, `Cooking`, `Extract`, `Incinerate`, `Fishing`, `CritterCatching` (`_ref/corekeeper-decomp/Pug.ECS.Components/CraftingCD.cs:22`) |
| 용광로 화면 | `UIManager`가 `CraftingType.ProcessResources`일 때 `processResourcesCraftingUI`를 연다(`Pug.Other/UIManager.cs:807`). 외부 모드 두 곳도 용광로를 이 타입으로 다룬다. Archipelago는 "Furnace recipe icons" 때문에 `ProcessResourcesCraftingUI.ActivateRecipeSlot`을 따로 패치한다. ExpandNullforge는 "the furnace … one slot"을 ProcessResources 묶음에 넣는다. |
| 레시피를 누르면 일어나는 일 | `ProcessResourcesCraftingUI.ActivateRecipeSlot`은 `CraftAction.ActivateRecipeSlot`을 보낸다. 서버의 `InventoryUtility.ActivateRecipeSlot`은 **재료(광석)를 가방·근처 상자에서 용광로 입력 칸 0으로 옮기기만 한다**(한 번 누르면 레시피 1회분, mod1이면 10회분). 주괴는 바로 나오지 않는다. 이 동작은 **지금 설치된 게임 DLL**(`CoreKeeper_Data/Managed/Pug.Other.dll`, 11-03)을 ilspycmd로 디컴파일해 다시 확인했다. |
| 주괴가 나오는 방식 | 자동화 타이머가 입력 칸을 보고 시간이 지나면 출력 칸에 주괴를 채운다(`AICook.cs` 머리말의 PugAutomationStartCraftSystem 분석과 같은 경로). `Furnace : CraftingBuilding`(`Pug.Objects/Furnace.cs`)은 출력 칸의 주괴 수가 늘면 소리와 불빛을 바꾼다. |
| 일반 제작 버튼 | `Simple` 타입만 `simpleCraftingUIContainer`(즉시 제작 버튼)를 연다. 용광로는 이 화면을 쓰지 않는다. |

- 확인하지 못한 것: Furnace 프리팹의 `craftingType` 필드 값 자체는 게임 에셋을 열어 보지 않았다. 위 UI 경로와 외부 모드 두 곳으로 판단했다. 1번 로그가 들어간 빌드로 실게임을 돌리면 `BRAIN: craft exact CopperBar at station:Furnace@…: no option — 자동 가공 시설로 봄(제작 버튼 아님): CraftingType ProcessResources(자동 가공으로 봄)`이 찍혀야 한다. 다른 타입이 찍히면 이 판정을 다시 본다.
- 참고(확인 안 함): 서버의 `CraftAction.Craft` 처리(`InventoryUtility.Craft`)는 제작대 타입을 막지 않는다. 그래서 필터 전처럼 용광로에 일반 Craft 요청을 보내면 서버가 주괴를 바로 만들어 줄 수도 있다. 게임 UI는 이 요청을 보내지 않는다. 그렇게 만들어지면 녹이는 시간을 건너뛰는 비정상 경로이므로 쓰지 않는 것이 맞다.

## 1번 — 이유 로그 (커밋 d3fcf40f)
- `AICraft.ExactCandidate`·`ReadRecipe`가 실패하는 모든 경로에 이유를 붙인다. 이유는 `AICraft.LastExactWhy`에 담는다. (품목, where, 이유) 조합마다 한 번만 `BRAIN: craft exact <품목> at <where>: no option — <이유>`로 남긴다.
  - Goal.where와 같은 제작대가 안 보이면, 보이는 제작대 id와 각각의 CraftingType을 함께 적는다. 좌표나 형식이 어긋났는지 여기서 보인다.
  - 자동 가공 시설이면 CraftingType을 적는다.
  - 그 밖에 재료 부족, 레시피 없음, 출력 공간, DB 읽기 실패도 각각 이유로 남는다.
- 준비 문구(`AIBrain.Prep.cs`)는 '실제 제작 선택지를 만들지 못함 — <이유>'로 쓴다. 추정 재료가 부족한 경로에도 실제 이유를 덧붙인다. Goal.where가 없으면 장소별 이유를 모아 쓴다(예: '자동 가공 시설은 일반 제작 실행 범위 밖').
- 시험(life-craft 7개 추가, 모두 통과):
  - 보이는 제작대와 Goal.where가 같고 재료가 충분하면 선택지가 1개 나오고 이유는 비어 있다.
  - where가 어긋나면 보이는 제작대 id·타입이 이유와 준비 문구에 나온다.
  - 그 로그는 한 번만 찍힌다(같은 판단이 반복돼도 1줄).
  - 자동 가공 제작대(ProcessResources)이면 이유·문구·로그에 타입이 나온다.
  - 재료가 부족하면 이유에 '부족'이 나온다.
  - Goal.where가 없을 때도 자동 가공 이유가 준비 문구에 나온다.
- 함께 돌린 시험: gates --unity 실패 0(compile-check 오류 0, core 1664, Unity CS 0), execution 246, life 143, maintenance 171, gather 62, inventory-menu 6, life-craft 56, life-inventory 253.

## 위쪽 LLM이 'craft CopperBar at Furnace'를 세우지 않게 하려면 (바꾸지 않음, 결정 필요)
지금은 게임 쪽이 스스로 이 목표를 권하고 받아 준다.
1. **보고 `can_do.craft`** (`AIPlanReport.cs` 81–86행): 보이는 제작대의 레시피를 모두 올린다. 솥만 빼고 용광로는 뺀다. 자동 가공 제작대(`!Manual`)도 빼거나, `auto_process` 같은 별도 칸에 '자동 가공(넣고 기다리기) — 지원 범위 밖'으로 따로 적어야 한다.
2. **목표 검사** (`AIPlanner.cs` 1038–1045행): `AICraft.KnownRecipe`가 `StationFor`/`RememberedStationFor`로 용광로 프리팹 레시피까지 '아는 레시피'로 본다. 자동 가공 제작대에서만 나오는 품목은 '용광로(자동 가공)에서 나오는 품목 — craft 목표 지원 범위 밖'으로 거절해야 한다. where가 자동 가공 제작대인 craft도 같은 방식으로 거절한다.
3. **준비 사슬** (`AIBrain.Prep.cs` 143·171·198행, `AICraft.StationFor`): 재료 품목(CopperBar)을 '만들 단계'로 추가할 때 자동 가공 제작대를 제작처로 센다. 그래서 '구리 곡괭이 → CopperBar 만들기 → Furnace 놓기' 같은 준비 메모가 생긴다. `PrefabManual`로 걸러야 한다.
4. **브리지 brief** (`Tools/ai-bridge/brief.md` 72행): '자동 가공/제련의 새 실행은 포함하지 않는다'는 문장은 있지만, 어떤 품목이 그런 품목인지 모델은 알 수 없다. 보고의 표시(1번)와 함께 'CopperBar 같은 주괴는 craft로 만들지 않는다. 상자·호스트·가방에서 구한다'는 지침을 넣어야 한다.

## 자동 가공(넣기·기다리기·꺼내기)을 지원하려면 (범위 결정 필요)
- **넣기**: 용광로 앞에서 열고 `ActivateRecipeSlot`(레시피 번호, 배수)을 보내거나, 솥처럼 입력 칸 0에 광석을 옮긴다. 솥 넣기(`AICook`, 클라이언트 인벤토리 이동, 실기 통과 11-04)를 거의 그대로 쓸 수 있다. 다른 광석이 들어 있으면 서버가 가방으로 돌려준다.
- **기다리기**: 레시피의 craftingTime × 개수만큼 걸린다. 용광로는 전기가 필요 없다(자동화 활성 조건은 솥과 같음, 확인은 실게임으로). 그동안 다른 일을 할지, 곁에서 기다릴지 정해야 한다. 출력 칸의 수량은 관측할 수 있다.
- **꺼내기**: 출력 칸(`CraftingCD.outputSlotIndex`)에서 가방으로 옮긴다. 수령은 가방 수량으로 확인한다(제작 수령 규칙과 같음).
- **목표 꼴**: craft와 따로(예: `smelt` 또는 craft의 mode). 진척은 '넣은 광석 수 / 꺼낸 주괴 수'. 남이 넣은 것이나 이미 들어 있는 것을 어떻게 셀지 정해야 한다(공용 용광로).
- **시험**: 솥 시험(life-cook)처럼 넣기·출력 관측·꺼내기 시험을 만들고, 실게임 1회로 확인한다.

## 사용자 결정이 필요한 것
- 위 4곳을 고쳐 용광로 목표를 막을지, 아니면 자동 가공 실행을 지원 범위에 넣을지.
