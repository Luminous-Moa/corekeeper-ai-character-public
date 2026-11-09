# 7번 — 자동 가공(제련) 지원 (2026-11-08)

커밋 `21ccac87`(브랜치 ai-celeris-decisions-20261108, worktree `C:\Dev\CoreKeeperModSDK-celeris`, 4171d9b8 위). 설치·브리지 재기동은 하지 않았다. 검수 F4([review-20261108-late.md](review-20261108-late.md))도 같은 커밋에 들어 있다.

## 정한 것과 이유

### 대상
- `CraftingType.ProcessResources`인 시설 전체를 같은 코드로 다룬다. 시설 종류를 코드에 적어 두지 않았다.
  - 처음 한 번 게임 DB의 모든 ObjectID 프리팹을 훑어 'ProcessResources 시설 → 레시피'와 '버튼 제작대가 만드는 품목'을 만든다(로그 `SMELT: catalog — …`).
- 재료 한 가지인 레시피만 다룬다. 게임의 레시피 버튼(`ActivateRecipeSlot`)도 재료가 하나일 때만 동작한다.
- 솥(Cooking)은 AICook 그대로 둔다.

### 넣는 방법
- 게임 버튼 `ActivateRecipeSlot`은 쓰지 않는다. 디컴파일(`InventoryUtility.cs:3063`)을 보면 이 버튼은 입력 칸에 다른 재료가 있거나 출력 칸에 다른 결과물이 있을 때 그것을 누른 사람에게 먼저 옮긴다. 다른 플레이어 물건을 건드리게 된다.
- 그래서 솥과 같은 일반 인벤토리 이동을 쓴다(실기 통과 경로). 가방 재료 칸 → 시설 칸 0으로 넣고, 출력 칸 → 가방으로 꺼낸다.
- 가방 재료만 넣는다. 근처 상자는 쓰지 않는다. 재료가 모자라면 준비 사슬이 모으기와 상자에서 꺼내기 선택지를 낸다(`AIBase.NoteMissing`).

### 목표 형식(위쪽 LLM)
- 넣기: `{"do":"smelt","mode":"put","item":"CopperBar","count":10,"where":"station:Furnace@3,0"}`
  - item은 만들 결과물이고, count는 결과물 몇 개분의 재료를 넣을지다(필수).
  - where는 생략할 수 있다. 생략하면 그 레시피가 있는 가장 가까운 아는 시설로 정한다(보이는 것 먼저, 그다음 기억한 것).
  - 완료: 입력 칸에 들어간 것이 확인된 재료가 count × (개당 재료)에 닿으면 코드가 끝낸다. 결과물 확보로 세지 않는다.
- 꺼내기: `{"do":"smelt","mode":"take","item":"CopperBar","where":"station:Furnace@3,0"}`
  - item과 count는 선택이다. where를 생략하면 내 몫이 있는 시설로 정한다.
  - 완료: count가 있으면 가방이 count만큼 늘었을 때 끝난다. 없으면 시설을 볼 때 출력 칸에도 입력 칸에도 내 몫이 남지 않았을 때 끝난다.
  - 내 몫이 출력에서 사라졌으면(남이 꺼냄) 그렇다고 적고 끝낸다.
- 이름을 정한 이유
  - 기존 do/item/count/where/mode 문법을 그대로 쓴다. cook의 put/take와 같은 낱말이다.
  - 이름 `smelt`는 GoalKind 끝에 추가했다. 저장된 경험 번호는 그대로다.
  - 형식 검사는 세 곳이 같게 한다: 브리지 `protocol.mjs`, 게임 `AISmeltRules.Check`(AILifeGoalForm에서 부름), 공용 fixture `goal-contract.json` wireNames.
- 계획 예: `smelt put` → `gather Wood 20` → `smelt take`. `smelt put` → 곧바로 `smelt take`도 된다. 이때 하위 모델이 곁에서 기다린다.

### 다른 플레이어 물건(보수적으로 처리)
시설에는 누가 넣었는지 기록이 없다. 그래서 캐릭터마다 시설별 장부(내가 넣은 재료로 나올 개수, 꺼낸 개수, 1개분이 안 되는 나머지)를 두고 memory.json에 저장한다.

| 상황 | 할 일 |
|---|---|
| 입력 칸 비어 있음 | 넣음 |
| 입력 칸에 같은 재료, 장부에 내 몫 있음 | 보탬 |
| 입력 칸에 같은 재료, 내 몫 없음 | 넣지 않음. 결과물이 섞여 몫을 셀 수 없음 |
| 입력 칸에 다른 재료 | 넣지 않고 빼지도 않음. 내가 넣은 다른 재료면 '그 몫을 꺼낸 뒤'라고 적음 |
| 출력 칸에 다른 결과물 | 가공이 막힘. 내 몫이면 먼저 꺼내라고 적고, 아니면 건드리지 않음 |
| 출력 칸 가득 참(9999, 입력에 남은 몫 포함) | 넣지 않거나 들어갈 만큼만 넣음 |
| 출력에 남의 결과물이 섞임 | 내 몫(나올 개수 − 꺼낸 개수)까지만 꺼냄 |
| 내 몫이 없는 시설의 take | 위쪽 LLM 목표 검사에서 거절 |

- 막힌 이유는 정체 문구(goal_stalled)와 로그 `SMELT: put … no option — …`(한 번)에 남는다.
- 목표 검사(CheckGoal)는 보이는 시설이 막혀 있으면 그 자리에서 거절한다. 가방에 재료가 하나도 없는 넣기도 거절한다.

### 시간
- 게임 규칙: 타이머가 한 개씩 만들고, 재료는 한 개가 다 됐을 때 빠진다(디컴파일 Craft).
- 남은 시간은 '지금 타이머 + (입력 칸의 개수분 − 1) × 개당 시간'으로 잡는다. 개당 시간은 `ObjectInfo.craftingTime`이고, 레시피에 덮어쓰기 값이 있으면 그것을 쓴다.
- 화면 밖이면 마지막 관측에서 지난 시간을 빼서 추정한다. 출력 개수 추정도 함께 적는다.

## 위쪽 LLM 보고
- `can_do.craft`에서 자동 가공 시설(`!PrefabManual`)의 레시피를 뺐다.
- `can_do.smelt`: 아는 시설마다 레시피 목록. 예: `"station:Furnace@3,0": ["CopperBar: CopperOre 5개당 1개, 1개 약 4초, 지금 가방 CopperOre 27개로 최대 5개(입력 칸 한도 9999개)"]`. 보이는 것과 기억한 것을 합쳐 6곳까지다.
- `smelt_stations`(새 필드): 시설마다 한 줄. 예: `station:Furnace@3,0: 입력 CopperOre 25개(CopperBar 5개분), 출력 CopperBar 1개, 예상 남은 약 19초, 내 몫 CopperBar 6개(넣어서 6개, 꺼냄 0개), 지금 보임`.
  - 화면 밖이면 `화면 밖 — N초 전 마지막 관측 기준 추정`이라고 쓰고, `(본 때 1개, 그 뒤 나왔을 것으로 추정)`도 붙인다.
  - 재료는 있는데 타이머가 보이지 않으면 `가공 타이머가 보이지 않음(멈췄을 수 있음)`이라고 쓴다.
- craft로 가공 품목을 요청하면 거절한다. 문구는 `CopperBar은(는) Furnace(자동 가공 시설)에서 … craft로 만들지 않음. 대신 {"do":"smelt","mode":"put",…}로 넣고 … {"do":"smelt","mode":"take",…}로 꺼낸다`이다.
  - where가 자동 가공 시설인 craft도 같은 문구로 거절한다. 로그는 `PLAN: craft … refused — auto processing`이다.
- brief 지침(`Tools/ai-bridge/brief.md`)
  - 주괴·유리 같은 가공 결과는 craft로 만들지 않는다.
  - smelt 형식, 넣기 → 다른 일 → 꺼내기 계획, 곧바로 꺼내기(곁에서 기다림).
  - 입력 칸은 한 칸이고 한도가 있다. 남의 재료·결과물은 건드리지 않는다.
  - 용광로 자체는 craft·place로 만들 수 있다. 예전 '용광로로 만들 수 있는 것은 craft로 정해도 된다'는 문장은 고쳤다.
  - trigger smelt_ready, smelt_alert, smelt_stations 설명을 넣었다.

## 계기 smelt_ready — 넣었다
- 내 몫의 예상 끝 시각(넣을 때와 볼 때마다 갱신)이 지나면 위쪽 LLM을 한 번 부른다. `smelt_alert`에 어느 시설의 무엇이 몇 개인지 적는다. 우선순위는 다른 목표 사건과 같은 2다.
- 부르지 않는 경우
  - 지금 단계가 그 시설의 smelt 단계일 때(이미 다루고 있음)
  - 싸움 중일 때(끝나면 부름)
  - 지켜보는 중이 아닐 때(몫이 남아 있어 나중에 부름)
- 몫마다 한 번만 부른다. 다시 넣으면 다시 걸린다.
- 이유: 정기 점검(약 2분)만으로도 smelt_stations를 볼 수는 있다. 그러나 넣어 두고 긴 단계(예: 나무 50개)를 하는 동안에는 '지금 돌아갈지'를 정할 계기가 없다. 몫당 한 번이라 호출 비용이 작다.
- 하던 단계는 끊지 않는다. 계기는 보고일 뿐이라 keep이면 그대로 간다.

## 목표 검사와 준비 사슬
- `AICraft.KnownRecipe`: 보이는 시설은 버튼 제작대만 센다.
- `AICraft.StationFor`: 버튼 제작대만 센다.
- `AICraft.RememberedStationFor`: 자동 가공 시설을 건너뛰고 이유를 로그에 남긴다(F4).
- `AIBrain.Prep`
  - 재료나 목표 품목이 자동 가공으로만 나오면(`AISmelt.IsSmeltOnly`) craft 단계·제작대 만들기·'기억한 제작대로 가기' 선택지를 만들지 않는다.
  - 대신 준비 문구를 낸다. 예: `CopperBar 3개 더(CopperMiningPick의 재료): 자동 가공 품목 — craft로 만들지 않음. station:Furnace@3,0(보임)에 smelt put(CopperBar 3개분, 재료 CopperOre 15개(가방 n개), 1개 약 4초) → 다른 일 또는 곁에서 기다림 → smelt take; …에 내 몫 k개 있음`.
  - 그 재료(광석)가 모자라면 모으기 대상과 상자 꺼내기 대상에 넣는다.
  - `Gatherable`은 자동 가공 품목을 빼서, 주괴를 '모으기' 대상으로 삼지 않게 했다.
- smelt put 단계에서 가방 재료가 모자라면 모자란 광석을 wanted와 상자 꺼내기 대상에 넣는다. 그래서 광석 캐기 선택지가 남고 '주요 목표 재료 CopperOre 나옴' 꼬리표가 붙는다.

### F4 (review-20261108-late)
- `AICraft.LifeEntries`(where 없음): 기억한 자동 가공 시설은 이제 이유 항목과 `BRAIN: craft exact CopperBar at station:Furnace@x,y: no option — 화면 밖 기억한 자동 가공 시설 — craft 범위 밖(smelt put·take로)` 로그를 남긴다. 준비 문구의 exactWhy에도 들어간다.
- `RememberedStationFor`: 위와 같다.
- Prep의 기억 경로(`AIBrain.Prep.cs` where==null 분기)에는 자동 가공 품목이 오기 전에 위 smelt 안내로 빠진다. 그래서 'Furnace에서 만듦'이나 '기억한 제작대로 가기 — 거기서 만들 수 있음'이 생기지 않는다.

## 하위 선택지
- 넣기 단계
  - 화면 밖이면 `시설로 가기 — <시설 상태(마지막 관측 기준 추정)>. 아는 길 n칸, 예상 약 T초. 도착 뒤 넣기를 다시 고름.`
  - 보이면 `넣기 — CopperOre 25개 → CopperBar 5개, 약 20초(… 입력 칸에 내 가방 재료를 옮김; 가공은 시설이 저절로 함, 결과물 확보 아님) 이번 단계 넣음 0/25개.`
  - 한 번에 가방의 한 칸 묶음에서 넣는다. 개수분으로 딱 떨어지게 자르고, 입력 칸·출력 칸 자리 안에서 넣는다.
- 꺼내기 단계
  - `꺼내기 — 출력 CopperBar k개(…, 내 몫 m개 중; 가방 증가로 확인)`
  - 아직 나올 몫이 있으면 `시설 곁에서 기다리기(남은 약 T초) — … 이 단계는 꺼내기라 기다리는 것이 목표에 맞음 이번 선택은 s초.` (선택 종류 `smeltwait`, 한 번에 1~10초, 대략 한 개분)
- 실행 `SmeltSkill`: 멀면 걸어간 뒤 이동 요청을 한 번 보낸다. 칸 관측으로 확인하고, 6초 안에 확인되지 않으면 실패로 끝내고 다시 보내지 않는다.
- 선택지 정리(Core/AIFieldChoices.cs)와의 맞물림
  - `MenuFree`에 Smelt를 넣었다. 생활·인벤토리 메뉴는 빠진다(smelt는 트리 없이 바로 고름).
  - 일반 '기다리기'는 다른 몸 행동이 있으면 빠진다. `smeltwait`는 몸 행동 종류라 남는다.
  - 목표와 무관한 채집은 빠지고, 모자란 광석 채집은 남는다.
  - 트리 열림 규칙은 그대로다.

## 고친 파일 (겹칠 수 있는 파일 표시)
- 새 파일: `Assets/AIPlayerSpike/Scripts/AISmelt.cs`, `Core/AISmeltRules.cs`(+ .meta), `Tools/core-tests/SmeltRulesTests.cs`, `Tools/life-smelt-tests/`
- **Core/AIFieldChoices.cs**(겹침 목록): `MenuFree` 한 줄에 `|| k == GoalKind.Smelt`만 추가했다(56행).
- **AIBrain.Candidates.cs, AIAttackSkill.cs, AITunnel.cs, AICombat.cs, AIGearWear.cs**: 고치지 않았다.
- 그 밖
  - AIGoalKinds(Smelt 종류·Feature)
  - AILifeTree(`AILifeGoalForm.Check`·`CountFor`의 smelt)
  - AIPlanProtocol(smelt count를 엄격히 읽음)
  - AIGoals(SetGoal의 smelt)
  - AIGoalFeatures(선택지·완료·정체 문구)
  - AIActions(smelt·smeltwait)
  - AIDecisions(`AICandidate.smelt`)
  - AIAgents(`AISlot.Smelt=21`)
  - AIMemory(장부 저장·관측)
  - AIPlanner(형식·검사·craft 거절·TickSmelt)
  - AIPlanReport(can_do·smelt_stations·smelt_alert)
  - AIGoalText, AICraft, AIBrain.Prep
  - AIPlayerSpikeMod.Chat(`/ai smelt` 상태 보기)
  - 브리지 brief.md·protocol.mjs와 시험
  - 다른 시험 프로젝트 6곳의 csproj에 AISmeltRules.cs 링크를 추가했다. execution·life-craft의 Stubs에는 AISmelt 경계를 넣었다.

## 시험
| 시험 | 결과 |
|---|---|
| gates --unity | 실패 0 — compile-check 오류 0, core 1746(SM 48 추가), exp-sim, recall-loop 41, bridge-wiring 23, protocol 206(smelt 11 추가), fake-bridge, logshape, decisions-compare, movecheck, Unity CS 0 |
| life-smelt-tests(새, `dotnet run -c Release --project Tools/life-smelt-tests`) | 34/34. 실제 AISmelt.cs·Core 링크, Unity/ECS·게임 DB·이동 요청만 대역 |
| execution | 246 |
| life | 143 |
| life-craft | 59(F4 2건·smelt 안내 1건 추가) |
| life-cook | 73 |
| life-inventory | 253 |
| maintenance | 171 |
| gather | 62 |
| travel | 105 |
| inventory-menu | 6 |
| path-preparation | 25 |

- core SM이 다루는 것
  - 형식(put·take·잘못된 count·where·second/slot)
  - craft 거절 문구, 시간(보임·화면 밖 추정)
  - 남의 재료·결과물 규칙 6가지, 출력 가득 참, 넣을 양 자르기, 장부
  - 내 몫만 꺼내기, take 완료 5가지(count, 몫 다 꺼냄, 화면 밖은 판단 안 함, 몫 사라짐, 남의 것만 있음)
  - smelt_ready(한 번, 진행 중 시설 제외), 보고·선택지 문구, 저장
  - Refine(smeltwait 남고 일반 기다리기·메뉴·무관 채집 빠짐, 모자란 광석 채집 남음)
- life-smelt가 다루는 것
  - 카탈로그·레시피, where 자동 결정
  - 넣기 선택지→실행→입력 확인→장부→완료
  - 곧바로 꺼내기: 기다리기 → 2개 꺼내기 → 남의 4개 섞인 출력에서 내 3개만 → 완료·장부 비움
  - 다른 재료·남의 같은 재료·다른 결과물 거절, 목표 검사 거절, 바뀐 칸 stale
  - 재료 부족(묶음 자르기, 부족량), 화면 밖 걷기, smelt_stations 추정 문구, can_do.smelt
  - 준비 문구(보임·기억), 저장, smelt_ready 한 번·진행 중 제외

## 실게임에서 확인할 것
1. 시작 로그 `SMELT: catalog — … ProcessResources station(s): Furnace [CopperBar,…]; …`에 Furnace·SmelterKiln·GlassSmelter가 다 나오는가. 레시피 결과물·재료 수·시간이 실제 값과 같은가.
2. 넣기가 실제로 가공을 시작하는가. 일반 이동으로 칸 0에 넣으면 서버가 받는가(`SlotAllows`), 타이머(`CraftingTimerSlotBuffer`)가 도는가, 출력이 쌓이는가. 로그 `SMELT: put … into …`.
3. 개당 시간이 `craftingTime`과 맞는가. 보고의 '예상 남은 약 T초'와 실제 끝 시각을 비교한다.
4. 입력 칸 한도. `can_do.smelt`의 '입력 칸 한도'가 실제 칸 한도와 같은가(코드는 쌓이는 물건이면 9999로 봄).
5. 꺼내기가 가방으로 들어오는가(커서에 남지 않는가). 남의 결과물이 섞였을 때 내 몫만 오는가.
6. 화면 밖 추정. 다른 일을 하는 동안 시설이 계속 가공하는가(멀리 가면 청크가 멈추는지). smelt_ready가 한 번 오는가.
7. 실제 Gemini가 craft 거절 뒤 smelt 형식으로 다시 계획하는가. 넣기 → 다른 일 → 꺼내기를 쓰는가.
8. Celeris가 꺼내기 단계에서 '시설 곁에서 기다리기'를 고르는가. 기다리는 동안 적이 오면 전투 선택지로 바꾸는가.
9. 재료가 모자란 넣기에서 광석 캐기·상자 꺼내기 선택지가 나오는가.
10. `/ai smelt`로 시설·몫·레시피가 보이는가.

## 사용자 결정이 필요한 것
- 남이 넣은 것 꺼내기: 지금은 내가 넣은 몫만 꺼낸다. 호스트가 "용광로에서 주괴 꺼내 와"라고 할 때 남의 결과물도 꺼내게 할지 정해야 한다. 허용하려면 take에 별도 표시(예: 호스트 부탁일 때만)가 필요하다.
- 남이 넣은 같은 재료에 보태기: 지금은 보태지 않는다. 공용 용광로를 함께 쓰자면 몫 계산 규칙이 따로 필요하다.
- 넣기에 근처 상자 재료도 쓸지: 게임 버튼처럼 상자에서 끌어오게 할지. 지금은 가방만 쓰고, 모자라면 상자 꺼내기 선택지로 보낸다.

## 확인하지 못한 것
- Furnace 프리팹의 `craftingType` 값을 에셋으로 직접 보지 않았다(5번과 같음). 카탈로그 로그로 확인할 수 있다.
- 시설 인벤토리의 칸 구성. 칸 0이 입력이고 `outputSlotIndex`가 출력이라는 것은 디컴파일 CanCraft·ActivateRecipeSlot 기준이다. 칸이 더 있는 시설(예: 연료 칸)은 확인하지 않았다.
- SmelterKiln·GlassSmelter가 전기나 다른 활성 조건을 요구하는지. 타이머가 안 보이면 보고에 '멈췄을 수 있음'이 나오지만, 원인은 알려 주지 않는다.
- 화면 밖(청크 비활성)에서도 가공이 진행되는지. 진행하지 않으면 화면 밖 추정과 smelt_ready가 이르게 나온다.
- `gather CopperBar` 목표는 그대로 받는다(KnownItems에 있으면). smelt로 돌리는 거절은 넣지 않았다.
- 계획의 둘째 단계 이후에 있는 craft CopperBar는 그 단계가 시작될 때 거절된다(StepForm은 세계를 보지 않음). 그러면 plan_step_failed로 다시 묻게 된다.

## 공용 자원 반영 (2026-11-09)

커밋 `30c0093c`(브랜치 ai-celeris-decisions-20261108, 54c0e0e9 위). 설치·Unity 컴파일(--unity)은 하지 않았다.

### 사용자 결정
1. 남이 넣은 결과물: "판단하에 알아서 상자로 넣기"
2. 공용 용광로에 보태기: "그냥 넣어. 어차피 기지 자원은 공용으로 쓸거야."
3. 근처 상자 재료: "상자에서 꺼내다 쓰게 해. … 용광로에 해당 버튼이 있어."

### 바뀐 동작
- **공용 기준.** 같은 재료면 누가 넣었든 보탠다. 출력은 누가 넣었든 꺼낸다.
  - 시설별 '내 몫' 장부(나올 개수·꺼낸 개수·나머지)를 없앴다.
  - 남긴 것은 '내가 넣은 기록'(결과물, 넣은 개수분, 넣은 때, 예상 끝)뿐이다. 이 기록은 아무것도 막지 않는다.
  - 쓰임은 세 가지다: smelt_ready 계기, 보고 문구, where를 생략한 take의 우선 시설.
  - 그 시설에 그 재료도 그 결과물도 남지 않은 것이 보이면 기록이 끝난다. 누가 꺼냈든 같다.
  - memory.json은 숫자 5개 형식이다. 예전 7개 형식도 읽는다(넣은 개수 − 꺼낸 개수로 읽음).
- **완료 판정**
  - put: 입력 칸에 확인된 재료가 count개분에 닿으면 끝난다(전과 같음).
  - take(count 있음): 가방이 count만큼 늘면 끝난다.
  - take(count 없음): 보이는 시설의 출력에 그 결과물이 없고, 입력에서 더 나올 재료도 없으면 끝난다. 처음부터 아무것도 없었으면 그렇다고 적고 끝낸다.
- **넣기는 게임 레시피 버튼이다**(`CraftAction.ActivateRecipeSlot`).
  - 요청은 `Create.ActivateRecipeSlot(station, player, 1, mod1, 레시피 번호)`이다. `AICraft.QueueCraftAction`으로 UIActionBuffer에 올린다. AIMaintenance가 실제로 쓰는 경로와 같다.
  - 레시피 번호는 시설 엔티티의 CanCraftObjectsBuffer 순번이다. 서버 `GetRecipeInfo`가 이 번호로 읽는다.
  - 결과물 n개분을 넣을 때 ×10(mod1)을 먼저 누르고 나머지는 ×1로 누른다. 한 번 고를 때 100개분까지다.
  - 한 번 누를 때마다 입력 칸이 는 것(가공된 만큼은 출력 증가로 보정)을 확인한 뒤 다음 것을 누른다.
  - 6초 안에 확인되지 않으면 멈추고 다시 보내지 않는다. 일부가 들어갔으면 들어간 만큼을 진척으로 친다.
- **보고와 선택지**
  - `can_do.smelt` 예: `CopperBar: CopperOre 5개당 1개, 1개 약 4초, 지금 가방 27개·시설 근처 상자 40개로 최대 13개(입력 칸 한도 9999개)`
  - 화면 밖 시설은 '기억한 근처 상자'로 센다. 열어 본 상자의 기억 가운데 시설 10칸 안의 것이다.
  - 넣기 선택지 끝에는 `…게임 레시피 버튼으로 넣음: 가방 먼저, 모자라면 시설 10칸 안 상자 — 지금 가방 a개·상자 b개…`가 붙는다.
  - 준비 문구·PutShortage·목표 검사도 가방과 근처 상자를 합쳐 센다. 합쳐서 1개분이 안 되면 목표 검사가 거절한다.
  - smelt_stations에서는 '내 몫'을 뺐다. 대신 '출력 …(기지 공용)'과 '내가 넣은 기록 …개분(n초 전)'을 쓴다.

### 버튼 경로 확인 (디컴파일 `_ref/corekeeper-decomp/Pug.Other`)
- **근처 상자 범위**
  - `InventoryUpdateSystem.ProcessCraftingJob`(:411-431)이 재료를 찾을 곳을 정한다. 먼저 요청자의 인벤토리이고, 다음은 `GetNearbyChestsForCraftingByDistance`(시설 위치 기준)이다.
  - 이것은 `GetNearbyChestsByDistance(10f, 20)`(:3796)을 부른다. 물리 OverlapSphere 반경 10 안에서 `InventoryAutoTransferEnabledCD`가 있는 엔티티를 가까운 순으로 20개까지 쓴다. 주인은 보지 않는다.
  - `FindAndMoveTo`(:2766)는 그 순서대로 같은 ID·variation 0인 칸에서 옮긴다.
  - 모드는 기존 `AICraft.CraftChests`로 같은 상자를 센다. 다만 모드는 상자 위치로 재고, 게임은 콜라이더로 잰다. 10칸 경계에서는 조금 어긋날 수 있다(AICraft 머리말과 같은 추정).
  - 서버 확인은 `HasMaterialsInCraftingInventoryToCraftRecipe`(배수 1)이다. 1개분이 있어야 동작하고, mod1이면 있는 만큼(최대 10개분)을 옮긴다. 모드는 10개분이 다 있을 때만 mod1을 누른다.
  - 서버는 거리를 보지 않는다. 모드는 손이 닿는 거리(ChestReach)까지 걸어간 뒤 누른다.
- **입력·출력 칸의 다른 물건(가방이 찼을 때 포함)**
  - `ActivateRecipeSlot`(:3084-3095)은 다른 입력 재료와 다른 출력 결과물을 `MoveAllToOrDrop(…, playerEntity, 0, -1, …)`로 뺀다.
  - 이 함수(:3206)는 끝 번호를 그대로 반복 상한으로 쓴다. 그래서 끝 번호가 −1이면 가방으로 옮기는 두 반복(:3218, :3229)이 한 번도 돌지 않는다. 남은 물건은 `DropItem`(:3242)으로 누른 사람 자리 바닥에 떨어진다.
  - 디컴파일대로라면 가방이 비어 있든 가득 차 있든 **바닥에 떨어진다**. 행동 그래프 검수 `review/furnace/recovery/3905-chain.md`의 4번도 같은 결론이다.
  - 막는 방법: 그런 상태에서는 버튼을 누르지 않는다. 누를 때마다 직전 확인(Stale)이 이 상태를 본다.
    - 출력 칸에 다른 결과물이 있으면: 하위 선택지로 '먼저 꺼내기 — … 출력 칸의 TinBar 3개를 가방으로'를 낸다. 이 물건은 가공도 막는다.
    - 입력 칸에 가공되지 않는 다른 재료 나머지가 있으면 같은 식으로 꺼낸다. 1개분이 안 되거나 이 시설에 그 재료의 레시피가 없는 경우다.
    - 다른 재료가 가공 중이면 꺼내지 않는다. 기다리거나 다른 시설을 쓰라고 적는다(남의 가공을 끊지 않음).
    - 가방에 자리가 없으면 꺼내지도 누르지도 않는다. 이유는 '가방에 … 받을 자리 없음 — 버튼은 누르지 않음(누르면 바닥에 떨어짐); store로 가방을 비우거나 다른 시설'로 남긴다.
  - take 단계에서 다른 결과물이 출력을 막아 원하는 결과물이 나오지 못하면, 같은 '먼저 꺼내기'를 낸다.
- **보낼 수 있는가**
  - 보낼 수 있다. 클라이언트 UI(`ProcessResourcesCraftingUI.ActivateRecipeSlot` :93-109)도 같은 `UIInputAction.Craft` + `Create.ActivateRecipeSlot`을 큐에 넣는다.
  - 모드의 `QueueCraftAction`은 AIMaintenance(수리·강화)가 실제로 쓰는 같은 UIActionBuffer 경로다. compile-check(게임 DLL 기준)는 오류 0이다.
  - 실게임에서 처리되는 것은 아직 보지 않았다.

### 태그 레시피 (HEAD 전달 사항, 3905-chain.md)
- 확인한 디컴파일
  - `InventoryUtility.CanCraft`(:1653)는 `ObjectInfo.craftingSettings.canOnlyUseAnyMaterialsWithTag`가 있으면, requiredObjectsToCraft 대신 그 태그의 물건을 찾는다.
  - `Craft`(:1772-1807)는 그 경우 `ConsumeObjectWithTag`로 1개×레시피 수량을 소비한다.
  - 버튼과 UI는 requiredObjectsToCraft(예: 석탄)를 쓴다. 그래서 보고할 재료와 양이 실제 소비와 다르다. 3905에서는 석탄이 입력 칸 태그 조건에 맞지 않아 옮겨지지도 않는다.
- 처리: 이런 레시피는 smelt 지원에서 뺐다(`AISmelt.Recipe`가 null을 돌려줌).
  - 이유는 `UnsupportedWhy`에 남긴다. 목표 검사 거절 문구에 나오고, can_do.smelt에도 `<품목>: smelt 지원 안 함 — 태그 레시피(…)` 줄로 나온다. 로그 `SMELT: <시설> <품목> not supported — …`는 한 번 남긴다.
  - brief에는 "smelt 지원 안 함 항목은 smelt로 정하지 않는다"를 넣었다.
- 같은 Craft 코드에서 함께 본 것
  - 출력은 `initialAmount × 레시피 수량`이고, 재료 소비도 레시피 수량을 곱한다.
  - 지금의 개수·시간 계산은 1회 1개만 맞으므로, 레시피 수량이나 initialAmount가 1이 아닌 레시피도 같은 방식으로 뺐다.
- 시설 칸이 input0/output1인 구성은 이미 `CraftingCD.outputSlotIndex`를 읽어 다룬다. 고정 번호는 쓰지 않는다.
- 어떤 실제 시설·레시피가 빠지는지는 실게임 카탈로그 로그로 확인해야 한다. Furnace의 주괴가 태그 레시피가 아니라는 것은 에셋으로 보지 않았다.

### 결과물 보관 방식
- 정한 방식: 판단은 위쪽 LLM이 한다. 근거는 프로그램이 계산해 준다.
  - take가 끝나면 완료 근거에 판단 근거가 붙는다. 이 근거는 다음 보고 events에 실린다.
  - 남은 계획 단계가 그 결과물을 쓰면: `꺼낸 CopperBar은(는) 남은 계획 단계 'craft CopperMiningPick 1'에 쓰여 가방에 둠`
  - 쓰지 않으면: `남은 계획에 CopperBar을(를) 쓰는 단계가 없음 — 기지 공용 자원이니 기지 상자에 보관함(계획 끝에 {"do":"store","item":"CopperBar"}; 호스트가 가져오라고 했으면 give)`
- '쓴다'의 기준(`AISmeltRules.UsedBy`)
  - 남은 craft의 품목이 그것이거나, 그 재료(3단계 깊이까지)에 그것이 들어 있음
  - 같은 품목의 give·drop·hold·place·store·equip
  - maintain 단계(수리·강화 비용에 주괴가 들 수 있음)
- brief에 규칙을 넣었다: '다음 단계 재료면 가방에 두고, 아니면 계획 끝에 store 단계를 넣어 기지 상자에 보관'.
  - 예시 계획 끝에 `{"do":"store","item":"CopperBar"}`를 붙였다.
  - '먼저 꺼내기'로 가방에 들어온 남의 결과물도 같은 규칙으로 보관한다.
- 이유
  - 다음에 무엇을 할지는 위쪽 LLM의 계획에 있다. 호스트가 "가져와"라고 했는지도 위쪽 LLM만 안다.
  - 게임 쪽이 계획에 store 단계를 몰래 끼워 넣으면 두 문제가 생긴다. 계획 보고와 실제가 어긋나고, 상자를 모를 때 plan_step_failed가 날 수 있다.
  - 그래서 기존 store 목표를 그대로 쓰고, 판단 근거만 정확히 준다.
  - take 하위 선택지에 '상자에 넣기'를 두는 방식도 있었다. 그러나 그러면 take 단계의 완료 판정과 보관이 섞이고, 하위 모델이 계획을 모르는 상태로 보관을 정하게 된다.

### brief·protocol·문구
- brief
  - smelt 항목을 공용 기준으로 다시 썼다. '남의 재료나 결과물은 건드리지 않는다'를 지우고 '기지 자원은 공용'을 넣었다.
  - 레시피 버튼이 가방과 근처 상자를 쓴다는 것, 막는 물건 처리, 보관 규칙을 넣었다.
  - trigger smelt_ready, can_do.smelt, smelt_stations 설명도 고쳤다.
- protocol.mjs: 주석만 고쳤다(검사 규칙은 같음).
- AIGoalText의 take 문구, AIBrain.Prep 부족 문구, AIMemory 주석을 고쳤다.

### 고친 파일
- `AISmelt.cs`, `Core/AISmeltRules.cs`(대부분 다시 씀)
- `AICraft.cs`(`ChestMaterialCount` 추가), `AIPlanner.cs`(`PlanRest()` 추가)
- `AIBrain.Prep.cs`·`AIGoalText.cs`·`AIMemory.cs`(문구)
- `Tools/ai-bridge/brief.md`·`protocol.mjs`
- `Tools/core-tests/SmeltRulesTests.cs`, `Tools/life-smelt-tests/`(Boundary에 버튼·상자·계획 대역 추가)

### 시험
| 시험 | 결과 |
|---|---|
| gates(--unity 없이) | 실패 0 — compile-check 오류 0, core 1761(SM 51), exp-sim, recall-loop 41, bridge-wiring 23, protocol 206, fake-bridge, logshape, decisions-compare, movecheck. memgraph는 KG 데이터가 없어 건너뜀 |
| life-smelt | 47/47 |
| execution | 246 |
| life | 143 |
| life-craft | 61 |
| life-cook | 73 |
| life-inventory | 253 |
| maintenance | 171 |
| gather | 62 |
| travel | 105 |
| inventory-menu | 6 |
| path-preparation | 37 |
| restriction-tests, travel-tests/navigator, travel-tests/candidate-progress | 전과 같이 실패. 모두 smelt와 무관한 컴파일 오류다(`FoodProbe.PlantEntry`, `AIBrain.MarkAreaQuestion`, `.generated/CandidateSlices.cs` 없음) |

- life-smelt가 새로 다루는 것
  - 버튼 5번 누름(×1), 남이 넣은 같은 광석에 보태기, 남의 결과물까지 7개 꺼내기
  - 가공 중인 다른 재료면 누르지 않음, 가공되지 않는 입력 나머지·다른 출력은 먼저 꺼낸 뒤 넣기
  - 가방이 가득 찼으면 누르지 않음, 가방 7개 + 5칸 거리 상자 40개(17칸 거리 상자는 셈에서 뺌)에서 가방 먼저 쓰기
  - 12개분을 ×10 한 번 + ×1 두 번으로 누르기
  - 화면 밖에서 기억한 상자로 can_do 세기, 보관 판단 문구 두 가지, 태그 레시피 제외
- 시험의 서버 대역은 디컴파일의 ActivateRecipeSlot(다른 물건은 떨어뜨림, 가방 먼저 다음 상자 가까운 순)을 흉내 낸다. 실제 서버 처리는 아니다.

### 실게임에서 확인할 것
1. 레시피 버튼 요청이 실제로 처리되는가.
   - 로그 `SMELT: put CopperOre n into … with the recipe button`이 나오고, 입력 칸이 1개분(또는 ×10)씩 느는가.
   - ×10(mod1)이 10개분을 옮기는가.
2. 시설 10칸 안 상자의 광석이 가방 다음으로 쓰이는가. 상자가 경계 근처에 있을 때 모드 셈과 게임이 맞는가.
3. 다른 물건이 있는 채로는 버튼을 누르지 않는가. 실제 게임에서 누르면 바닥에 떨어지는지는 일부러 시험할 때만 본다(디컴파일 추정 확인).
4. '먼저 꺼내기'로 출력의 다른 결과물이 가방에 들어오는가.
5. take가 남이 넣은 결과물까지 꺼내는가. 완료 근거의 보관 문구를 Gemini가 따라 store 단계를 넣는가.
6. 카탈로그 시작 로그와 `not supported` 로그. 어떤 시설·레시피가 태그·수량 때문에 빠지는가. Furnace 주괴가 빠지지 않는가.
7. 이전 항목(타이머·화면 밖 가공·smelt_ready 한 번)은 그대로 확인할 것으로 남는다.

### 확인하지 못한 것
- 실게임 동작 전부(위 목록). Unity 컴파일(--unity)은 지시대로 돌리지 않았다. compile-check는 게임 DLL에 대해 오류 0이다.
- `MoveAllToOrDrop`가 끝 번호 −1에서 정말 바닥에 떨어뜨리는지. 디컴파일을 읽은 것뿐이다. 모드는 어느 쪽이든 누르지 않으므로 결과는 같다.
- 버튼의 `anyMaterialCostMultiplier`(모루 등 조건)와 자동 가공 Craft의 비용 배수. 용광로에서는 1로 보았고 확인하지 않았다.
- 화면 밖 '기억한 근처 상자'는 마지막으로 열어 본 내용이라 지금과 다를 수 있다. 문구에 '기억한'이라고 적었다.
- 플레이어 인벤토리의 main 범위 밖 칸은 버튼이 쓰지만 모드의 가방 셈(main 범위)에는 들어가지 않는다. 광석이 거기 있을 일은 적다고 보았다.
