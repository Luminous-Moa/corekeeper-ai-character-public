# 장비 정비 범위 선택지 누락 점검

읽기 전용 점검이다. 기준은 공식 `C:\Dev\CoreKeeperModSDK`(브랜치 ai-character, 커밋 20b1ace)이며, 미커밋 환경 파일과 다른 worktree는 보지 않았다. 코드 위치는 `Assets/AIPlayerSpike/Scripts/` 기준이다. 게임 근거는 로컬 디컴파일 `work/게임원본-상호작용/code`(아래 "게임:"), 행동 판단 그래프(`work/지식그래프/tools/product.mjs game ...`), 게임 지식 그래프(`Tools/knowledge/kg.mjs`)에서 확인했다. 근거 없이 판단한 내용에는 "추정"을 붙였다.

## 1. 현재 제공되는 선택지

### 트리 경로(생활 트리의 maintenance 갈래)

```
생활 선택지 열기 (life_open, AILife.cs:155)
└ 루트: 장비 정비 (AILife.cs:131 → AIMaintenance.LifeEntries, AILife.cs:139)
  └ mode (AIMaintenance.cs:245-251)
     ├ repair  일반 수리
     ├ upgrade 레벨 한 단계
     └ recover 자기 pending 회수 (pending.PutConfirmed일 때만, :249)
     └ slot (:254-269)
        · repair/upgrade: main(핫바+가방, AIBody.cs:515) + 활성 프리셋 장비 슬롯(AIInventory.cs:113-124) 중
          게임 CanBeRepaired/CanBeUpgraded를 통과한 비중첩 물품 (:193-206, :235-241)
        · recover: pending의 원래 슬롯 하나 (:258)
        └ where (:271-292)
           · 현재 보이는 SalvageAndRepairStation(4025)/UpgradeForge(4046) (:279-283)
           · 화면 밖 기억 시설(AIMemory) "가서 확인" (:284-289)
           · recover: pending 위치 하나 (:273-276)
           └ 말단 (:293-296 → ExactCandidate :298-340)
              · kind=maintain 실행 후보: 수리=내구도 before→max, 업그레이드=레벨 n→n+1, 실제 비용, 거리 (:335-339)
              · 기억 시설이면 걷기 후보(도착 뒤 다시 선택) (:313-321)
              · recover 말단 = Recovery (:342-357)
              · 조건 미충족이면 실행 후보 없이 사실 한 줄(LastResult)만 표시 (:295)
```

탐색 단위로 back, root, close가 붙는다(AILife.cs:186-188). 페이지 상한은 제약 해제 때 없어졌다.

### 직접 실행 경로

- 상위 계획 `do=maintain`의 형식은 mode가 repair/upgrade/recover 중 하나, count는 1(누락 시 1), item은 필터, slot은 정확 절대 슬롯, where는 `station:<ObjectID>@x,y`다(Tools/ai-bridge/protocol.mjs:32-40, Core/AIMaintenanceRules.cs:7-18, brief.md:66).
- 목표 kind가 Maintenance이면 `AIGoalFeatures.cs:37-40`이 `ExactCandidate(AILife.ForGoal(goal))`을 직접 후보로 넣는다. 다만 slot과 where가 모두 지정된 경우에만 후보가 생기고(:307), 나머지는 트리로 좁힌다.
- 목표 성립 검사는 `AIMaintenance.CheckGoal`(:208-234)이 맡는다.

### 실행

`AIActions.cs:33` → `MaintenanceSkill`(:656-779).

- repair: 접근 → 재검증 → `Create.RepairOrReinforce(..., reinforce:false)` 1회 → 처리 확인 → 내구도 변화 확인.
- upgrade: 접근 → forge local0 투입(MoveAmount) → `Create.Upgrade` → 레벨 변화 확인 → main 회수.
- recover: 처리 장벽을 통과한 뒤 회수(take).

### 현재 고정 조건

- 정비 1회 = 장비 1개, 단계 1개.
- 캐릭터당 pending은 최대 1개다(:540-547). pending이 있으면 새 repair/upgrade를 막는다(:302-306, :371).
- 재료는 자기 main 가방에 있는 것만 가용으로 본다(:76-83, :327).
- 대장간 입력이 비어 있어야 한다(:330). main에 회수 공간이 있어야 한다(:331).
- 시설은 현재 시야 안에 있어야 비용·상태를 읽는다(:106).

## 2. 빠진 선택지

### M1. 화면 밖 대장간에 남은 자기 pending을 회수하러 가는 후보가 없음
- **빠진 것:** upgrade 도중 전투, stop_action, 목표 변경 등으로 대장간에서 멀어져 시야를 벗어나면 recover 말단이 비고, 그 대장간으로 가는 걷기 후보도 나오지 않는다. 트리에는 mode·slot·where 항목만 보이고, 말단에는 "선택 정비 시설이 현재 관측 밖"이라는 사실 한 줄만 남는다.
- **합의 근거:** 00-agreement는 "중단 뒤 자신의 정확 투입물 회수", 02-spec §8은 "중단 후에도 다음 후보에서 pending을 관측… 새 스킬로 회수를 이어갈 때"라고 정했다. §4의 "기억 위치로 가서 확인하는 기존 이동 후보"는 새 정비에만 구현됐다.
- **코드 확인:**
  - `Recovery`는 먼저 `Station()`을 호출하고, 그 안의 CanSee 검사(:106)에 실패하면 null을 돌려준다(:346).
  - 기억 시설 걷기 경로(:313-321)는 pending이 없을 때만 지나간다. pending이 있으면 :302-306에서 먼저 return한다.
  - 정작 MaintenanceSkill은 접근(nav) 단계를 갖고 있어(:671-673), 시야 안이기만 하면 걸어가서 회수할 수 있다.
- **중요도:** 정비 흐름을 막는다. 회수 전까지는 다른 장비의 수리·업그레이드도 모두 막히고(M3), 강화된 장비가 대장간에 남는다. 다른 범위의 일반 이동으로 시야에 다시 들어가면 풀린다(경계).

### M2. 회수가 불가능해진 pending을 정리하거나 포기하는 선택지가 없음
- **빠진 것:** 다음 경우 회수 조건이 영구히 깨진다.
  - 대장간이 철거되거나 옮겨졌다.
  - 입력 물품을 사용자나 다른 플레이어가 꺼냈다.
  - snapshot이 어긋났다.
  - 게시 요청 소비를 끝내 확인하지 못했다.
  
  이때 pending이 연결 리셋 전까지 남아, 그 캐릭터의 새 수리·업그레이드가 전부 막힌다. 판단 모델이 고를 수 있는 "포기·정리" 후보도 없다.
- **합의 근거:** 02-spec §6은 "world/캐릭터 reset 경계에서 임시 기록을 지운다", §8은 "pending은 실제 회수 확인 뒤에만 지운다"고 정했다. 거짓 성공을 막는 의도는 기록돼 있지만, 회수가 불가능할 때의 출구는 합의에 없다(합의 공백). 게임 쪽에서는 forge 철거 시 내용물이 떨어지는 등 물품이 사라지는 정상 경로가 있다고 본다(추정, 원문 미확인).
- **코드 확인:**
  - `Pending = null`이 되는 곳은 네 군데뿐이다. 수리 관측 완료(:463), Complete(:641), 게시 전 intent 정리(:652), Reset(:66 ← AIBrain.cs:283 ← AIPlayerSpikeMod.cs:400 연결 리셋).
  - forge가 사라져도 관측만 보존한다(:466-468).
  - 입력이 바뀌면 ObserveExactInput(:440-443)이 실패하고, 그 결과 QueueReady → Recovery가 null을 돌려준다.
  - 수리 pending도 소비 확인을 못 하면(:456) 그대로 남는다.
- **중요도:** 흐름을 막는다. 드물지만 일어나면 그 접속 동안 정비 범위 전체가 멈춘다. 정리 선택지를 추가하려면 "물품 결과 미확인 상태로 끝내기"를 어떻게 보고할지 결정이 필요하다.

### M3. 업그레이드 pending이 있는 동안 다른 장비를 일반 수리할 수 없음
- **빠진 것:** 강화된 물품이 대장간에 남아 회수를 기다리는 동안, 또는 M1·M2 상태에서, 수리대에서 하는 일반 수리도 후보가 나오지 않는다. 수리는 대장간 입력이나 투입과 무관한데도 막힌다.
- **합의 근거:** 02-spec §6은 "pending이 있으면 **새 물품 투입**을 시작하지 않고 기존 건의 상태/명시 회수 선택을 제공"한다고 했다. 투입이 없는 수리까지 막으라는 문구는 없다. 다만 같은 절의 "최대1개의 pending" 상태 모델 때문에 수리도 함께 막힌 것이다.
- **코드 확인:** ExactCandidate :302-306이 mode와 상관없이 null을 돌려주고, Stale :371과 Begin :544도 거절한다.
- **중요도:** 있으면 좋다. 회수하면 풀린다. 다만 M1·M2와 겹치면 수리까지 막혀 흐름 차단으로 커진다.

### M4. 정비 대상 슬롯 범위: 파우치 안의 장비(추정, 영향 작음)
- **빠진 것:** 대상 슬롯은 main(핫바+가방)과 활성 장비 슬롯뿐이다. 장착된 파우치 자체는 대상이지만, 파우치 내부 인벤토리의 물품은 대상이 아니다.
- **게임 근거:** 수리 마우스 모드는 UI에 보이는 아무 InventorySlotUI에나 `RepairOrReinforce`를 보낸다(UIMouse.cs:795-799). 슬롯 유형에는 PouchInventorySlot도 있다(InventorySlotUI.cs:883). 파우치에 내구도가 있는 장비가 실제로 들어가는지는 확인하지 못했다(추정).
- **코드 확인:** SourceSlots(:235-241)과 ActiveSlot(:71-75)는 main 범위와 GearSlots만 본다.
- **중요도:** 있으면 좋다. 파우치에 장비가 들어가지 않는다면 빠짐이 아니다.

## 3. 알려진 후속과 경계

### 알려진 후속(기록상 미룬 것)

| 항목 | 게임 근거 | 기록 근거 |
|---|---|---|
| 내구 강화(reinforce, 최대 내구도의 2배) | SalvageAndRepairUI.ToggleReinforce(SalvageAndRepairUI.cs:73), InventoryUtility.RepairOrReinforce의 2×max(InventoryUtility.cs:2181), CanBeRepaired isReinforcing(:2191) | 장비정비-트리/00-agreement.md("내구 강화…는 별도 후속 단위"), 10-main-integration.md, todo.md:58("내구 강화(true 경로 별도 근거 검수)"), next/01-direction.md "내구 강화" 절, 11-graph-integration.md:11. 코드는 mode에서 거절(AIMaintenanceRules.cs:4,11)하고 비용 조회에서 false 고정(AIMaintenance.cs:155) |
| 분해(Salvage): 수리대 6칸에 넣고 일괄 분해 → 폐품 조각(수리 재료)과 일부 재료 | SalvageAndRepairUI.Salvage(:47-58) → Create.SalvageAll(Create.cs:510) → InventoryUtility.SalvageAll/TrySalvageObject(:1205, :1267). 행동 그래프 `partial-source-role:A-scrap-salvage-consumer:0:roles:0`(independent_pass). 수리대 claim의 label은 "수리·분해 UI 열기". ScrapPart는 수리 비용 재료(InventoryUtility.cs 비용식의 ScrapPart 분기) | 2026-11-07-시설생활계획/02-plan.md:67 "아이템을 없애는 분해…는 현재 첫 구현 범위에 섞지 않고 **별도로 정한다**". **todo에는 없음.** 정비 범위로 넣을지, 몇 개·무엇을 분해할지는 아직 정해지지 않았다 |
| 재입장 전 투입물, 대장간에 원래 있던 물품 꺼내기 | UpgradeForgeUI 입력 local0(UpgradeForgeUI.cs:82) | 02-spec §9 "재입장 뒤 임의 forge 물품 회수까지 지원한다고 주장하지 않는다", brief.md:66, 05-review.md:92. **실제 영향:** 연결 리셋으로 pending을 잃으면 자기 장비가 대장간에 남고, 그 대장간은 이후 업그레이드에도 쓸 수 없다(:330). 보관/꺼내기 갈래도 InventoryAutoTransferEnabledCD가 있는 자기 상자만 다룬다(AIInventory.cs:379-383). 대장간에 이 컴포넌트가 없다고 보면 다른 범위로도 꺼낼 수 없다(추정) |

### 합의에서 명시적으로 제외한 것(빠짐 아님)

- **일괄 수리:** 게임의 RepairAllItems는 디버그 명령 전용이고 비용을 면제한다(ServerSystem.cs:1169-1175, InventoryUtility.cs:2122-2133). 합의(00-agreement, next/01-direction)도 제외했다.
- **count가 2 이상이거나 최대 레벨까지 자동 반복:** 00-agreement, 02-spec §3, 제약해제 coverage.md P06에서 제외했다. count 1 단계를 반복 계획하면 여러 단계 업그레이드가 가능하다. 다만 단계마다 투입과 회수를 왕복하며, "대장간에 둔 채 연속 업그레이드"는 제공되지 않는다(:304에서 강화된 pending은 회수만 가능). 효율 문제일 뿐 흐름을 막지는 않는다.
- **다른 사람이 대장간에 넣어 둔 물품 강화나 채택:** 02-spec §9로 제외했고 코드는 :330에서 거절한다.

### 경계(다른 범위와 겹침)

- **재료가 주변 상자에 있는 경우:** 게임 소비자는 main 다음으로 주변 자동이체 상자에서도 재료를 꺼낸다(CraftingHandler 비용 조회의 nearbyChests). 모드는 main에 전량이 있어야 후보를 낸다(:327). 부족분 준비는 보관/꺼내기, 채집, 제작 범위다. 02-spec §5와 §9에서 의도한 제한이다.
- **업그레이드한 착용 장비 다시 착용:** 회수는 main 가방까지만 하고 원래 착용 슬롯으로 되돌리지 않는다(02-spec §8). 착용은 가방·장비 범위다.
- **동료 장비 정비:** 게임의 수리·업그레이드 대상은 자기 UI에 보이는 인벤토리와 대장간 입력이다. 동료 장비는 전달(가방·장비 범위)로 받은 뒤 자기 장비로 정비하고 다시 전달하는 조합이 된다. 정비 쪽에 별도 선택지가 필요하다는 근거는 찾지 못했다.
- **정비 시설 제작·설치, 시설 찾아가기:** 제작·설치, 이동 범위다. M1의 "기억 위치로 가기"는 정비 트리 안에서 빠진 것으로 분류했다.
- **판단 근거:** 슬롯 목록 설명은 `상태 {amount}`만 보여 주고(AIInventory.cs:84) 최대 내구도나 비율은 시설을 고른 뒤 말단에서야 보인다. 선택지 누락은 아니지만 어떤 장비를 고칠지 고르는 근거가 약하다.
- **전투 중 부서진 무기 수리:** todo.md:14가 전투 작업과 함께 묶어 두었다. Compose에는 전투 필터가 없어 생활 트리를 열 수는 있다(AILife.cs:148-191).

## 4. 확인하지 못한 것

- **기억 시설 기록:** SalvageAndRepairStation과 UpgradeForge가 AIMemory 시설 기억에 실제로 기록되는지 확인하지 못했다. 기록 조건은 `AICraft.StationsInView`의 CanCraftObjectsBuffer 쿼리다(AICraft.cs:217). 두 시설은 CraftingAuthoring의 recipe_configuration claim이 있고, InventoryConverter.SetupCrafting이 일반 craftingType이면 빈 버퍼라도 만든다(InventoryConverter.cs:317, 332). 그래서 기록될 가능성이 높다(추정). 런타임에서는 확인하지 않았다. 기록되지 않으면 화면 밖 시설로 가는 후보(:284-289, :313-321)가 사람이 직접 놓은 시설에는 나오지 않는다.
- **forge 철거 시 내용물 처리:** 떨어지는지 사라지는지 원문을 확인하지 않았다(M2의 게임 근거는 추정).
- **파우치 내부의 내구도 장비 수납 가능 여부(M4).**
- **업그레이드 비용표의 실제 재료 종류:** 희귀 재료 포함 여부를 보지 않았다. 지식 그래프에는 레시피·비용 dump가 없다("레시피 없음 — 뽑기 전"). 판단 근거로 필요한지는 정하지 않았다.
- **행동 판단 그래프 조회의 낡음:** `product.mjs`가 "입력 원문 2개 변경/없음" 경고를 냈다. 정비 주제 자체(R.game-topic.maintenance: open_repair/repair/open_upgrade/upgrade_equipment/use_upgrade_forge)는 조회됐지만, 분해·강화는 이 주제 label에 없어 정본 전체 검색(`game find`, published graph grep)으로 따로 확인했다.
- **실게임:** 실제 모델·게임에서의 실행은 미확인이다(기존 보류 유지). M1~M3는 코드 경로를 읽어 판단한 것이며 실행 재현은 하지 않았다.
