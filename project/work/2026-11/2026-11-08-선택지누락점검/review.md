# 빈 선택지 3범위 변경 독립 검수 (2026-11-08)

- 대상: worktree `C:\Dev\CoreKeeperModSDK-gap-integrated`, 브랜치 `gap-integrated-20261108`, `git diff 20b1ace..068c7b5` 전체.
- 기준: [합의](agreement.md). 결함 근거: [가방·장비](bag-equipment.md), [생활](life.md), [장비 정비](maintenance.md). 구현자 보고(impl-*.md, integration.md)는 주장으로만 보고, 코드로 확인했다.
- 방법: 생산 코드를 읽어 후보 조립 → 판단 요청(공개 선택면 `AIBrain.Loop.cs:1315-1336`, 생활 `AILife.Compose`) → 실행(`AIActions`) → 결과 관측 경로를 따라갔다. 게임 원본은 `C:\Dev\_ref\corekeeper-decomp`(구현 보고서가 인용한 경로)로 대조했다. 코드는 고치지 않았다.
- 시험 재실행(Unity 미실행): maintenance-tests 144/0, life-cook-tests 70/0, life-craft-tests 38/0, life-tests 129/0, life-inventory-tests 217/0. 실행 뒤 `git status`에서 추적 파일 변경은 없었다.
- 아래 `파일:줄`은 `Assets/AIPlayerSpike/Scripts/` 기준(068c7b5).

## 결론 요약

- 차단: 없음. 합의 항목은 모두 판단 모델이 고를 수 있는 선택지로 생산 경로에 연결돼 있다. 시험에서만 통과하고 실제 경로에서 나오지 않는 항목은 찾지 못했다.
- 중요 3건:
  - I1: 상자 재료를 쓰는 제작에서, 가방만으로 충분해도 근처 상자의 변화 때문에 제작이 거절되거나 끝나지 않는다.
  - I2: 화면 밖 대장간 회수(M1)가 월드 청크 언로드 뒤 깨지고, 회수 불가 판정(M2)이 장비가 남아 있는데도 "대장간 없음(직접 보고 확인)"을 상위 LLM에 보낸다.
  - I3: 모든 본 상자를 상한 없이 기억해 상자 목록이 무한히 커진다. 255개를 넘으면 판단 요청이 아예 보내지지 않는다.
- 경미 13건, 실게임 확인 필요 19건.

## 발견

### 중요

#### I1. 제작 상자 재료: 가방만으로 충분해도 근처 상자 변화로 거절·미완료 (회귀)
- 위치: `AICraft.cs:254`, `AICraft.cs:282`, `AICraft.cs:754`, `AICraft.cs:804`.
- 무엇이 문제인가:
  - `ReadRecipe`는 가방이 충분하든 아니든, 비용 품목과 같은 품목이 든 근처 상자 칸을 모두 `ChestMaterials`에 넣는다(:254).
  - `Stale`은 그 칸 중 하나라도 바뀌면 거절한다(:282).
  - 소비 확인도 `가방+상자` 합계의 감소로 본다(:754, :804).
- 실패 시나리오 1 (자동화 기지):
  1. 드릴→컨베이어→상자로 구리 광석이 계속 들어오는 기지에서, 가방에 구리 광석이 충분한 AI가 구리 주괴 제작 후보를 받는다.
  2. 판단 모델 응답(수 초) 사이에 상자 칸이 바뀐다.
  3. 실행 시작 때 "제작대 근처 상자의 재료 슬롯이 바뀜"으로 거절된다.
  4. 같은 일이 계속 반복된다. 20b1ace에서는 가방만 봤으므로 성공하던 제작이다.
- 실패 시나리오 2:
  1. 제작 도중 상자에 같은 광석이 들어온다.
  2. `_materialBefore - MaterialCount(after) < need`가 되어 `consumed=false`가 된다.
  3. `cost-unconfirmed` 관측만 반복되고, 스킬은 시간 제한 없이 끝나지 않는다(:817-818). 판단 모델이 멈출 때까지 제작이 미확정으로 남는다.
- 사람 플레이어가 같은 상자에 넣고 빼는 기지에서도 같다.
- 고치는 방향: 상자 칸 기록과 상자 감소 확인은 가방이 부족한 품목(`ChestUse`에 들어가는 품목)에만 적용한다. 소비 확인은 가방 감소와 상자 감소를 나눠, 들어온 양에 흔들리지 않게 해야 한다.

#### I2. 청크 언로드 뒤 M1 회수 이동이 쓸모없어지고, M2가 장비가 남아 있는 대장간을 "없음"으로 판정함
- 위치: `AIMaintenance.cs:110-114`(`Station`은 저장된 Entity로만 찾음), `:370-384`(M1 이동), `:455-462`(`ObserveLoss`의 `forge = em.Exists(station) ...`).
- 게임 근거: 서버는 플레이어 근처(`KeepAreaLoadedCD`) 밖의 청크를 언로드한다. 그때 엔티티를 파괴하고 직렬화한다(`Pug.Other/UnloadToSerializeWorldSystem.cs:424, 574-586`). 다시 로드되면 같은 대장간이라도 새 Entity가 된다. 모드도 상자·시설 기억은 Entity가 아니라 타일+ObjectID로 한다(`AIBase` ChestMem, `AIMemory` Station). 그런데 정비 pending은 `Request.Station` Entity를 그대로 쓴다.
- 실패 시나리오:
  1. 업그레이드가 강화된 채 대장간에 남아 있다(pending).
  2. AI가 멀리 채집하러 간다. 호스트도 기지를 떠나 대장간 청크가 언로드된다.
  3. 돌아올 때 M1이 "자기 pending 대장간"으로 걷는 후보를 낸다(:381).
  4. 도착하면 타일은 보이지만 `em.Exists(station)`이 거짓이다. 그래서 `Station()`이 실패하고, `ObserveLoss`가 `LostNoForge`("그 자리에 대장간이 없음")와 "(직접 보고 확인)"을 붙인다(:445, :462).
  5. 판단 모델에게 '포기'가 제안된다. 포기를 고르면 상위 LLM에 "정비 장비를 잃음"이 보고되지만, 실제로는 장비가 그 대장간 입력 칸에 그대로 있다.
  6. 포기하지 않아도 회수는 영영 불가다(Stale·Post·Take가 모두 옛 Entity를 씀).
- 합의 위반 여부: 합의 M1("기억한 대장간으로 가는 후보")은 정확히 이 상황을 풀려고 넣은 것이다. M2의 "대장간 파괴·식별 불가면 모른다는 사실" 역시 사실과 다른 "없음 확인"이 된다.
- 고치는 방향: 대장간을 타일과 ObjectID로 다시 찾아 Entity를 갱신한다. 그다음 입력 칸 물품의 신원(`SameObject`)으로 pending을 재확인한다.
- 언로드가 일어나는 실제 거리는 '실게임 확인 필요'다.

#### I3. 모든 본 상자를 상한 없이 기억 → 상자 목록이 255 후보 한도를 넘으면 판단 요청이 멈춤
- 위치:
  - `AIBase.cs:125-138`: 화면 안의 모든 상자를 기억한다. 기억 해제는 그 타일이 다시 보이고 상자가 없을 때뿐이다.
  - `AIInventory.cs:325-334`: 생활 보관/꺼내기 갈래가 기억한 상자를 전부 where로 낸다.
  - `AIInventoryMenu.cs:272-281`: 가방 메뉴 chests 단계도 전부 낸다.
  - `AIInventory.cs:978`(보고 `own_chests`)와 `AIPlaces.cs:604`도 전부 싣는다.
  - `AIBrain.Loop.cs:763-775`: 후보가 255개를 넘으면 판단 요청을 보내지 않고 30초 뒤 재시도한다.
- 실패 시나리오:
  1. 큰 공용 창고(상자 100개 이상)가 있는 기지나 여러 플레이어 기지를 돌아다닌 AI가 생활 트리 inventory 갈래를 연다.
  2. 생활 트리는 열려 있는 동안 기존 실행 후보 전체를 유지한 채 항목을 덧붙인다(`AILife.cs:156-190`). 그래서 상자 where 항목과 기존 후보 합계가 255를 넘을 수 있다.
  3. 넘으면 "AI 판단 목록이 너무 큽니다"만 나오고 판단이 멈춘다. 그 단계를 벗어날 판단 자체가 나가지 않으므로 같은 상태가 반복된다.
- 20b1ace에서는 자기가 놓은 상자만 기억했다. 이번 '모든 상자 공용 + 본 상자 모두 기억'으로 생긴 규모 문제다. 상위 보고(own_chests, 장소 목록)의 토큰도 같이 커진다.
- 합의상 후보 상한·페이지는 다시 만들 수 없으므로, 구조로 풀어야 한다(예: 상자를 지역·거리 묶음 단계로 나누는 트리). 구현자 보고의 "기억하는 상자 수 상한이 없다"는 언급만 있고 영향 분석은 없다.

### 경미

#### L1. 악기 '사용'은 관측 없이 성공 처리
- 위치: `AIInventory.cs:1080-1084`.
- 주 사용 입력 0.9초 뒤 `SkillStatus.Done`이 된다. 문구에 "소리 결과는 관측하지 않음"이라고 밝히지만, 판단 기준 2("완료를 실제 관측 없이 성공 처리")에 걸린다.
- 반대로 소모되지 않는 CastingItem(탐지기 등)은 실제로 쓰였어도 8초 뒤 실패로 보고된다(:1085). 문구는 정직하다.

#### L2. 일괄 정리(B2) 완료 판정이 아무 칸 변화나 받아들임
- 위치: `AIInventory.cs:1502-1518`.
- 정렬·빠른 쌓기 요청 뒤 8초 안에 플레이어나 관찰 상자에서 칸 하나라도 바뀌면 완료로 본다(자동 줍기, 다른 플레이어의 상자 조작 포함).
- 예: 이미 정렬된 가방에 정렬을 요청했다(게임은 아무것도 바꾸지 않음). 그 사이 바닥 물품을 자동으로 주우면 "가방 정렬 실제 칸 변화 1칸"으로 성공 처리된다.
- 근처 빠른 쌓기의 "가방·파우치에서 상자로 옮겨진 수량"은 실제로는 플레이어에서 빠진 수량이다. 게임은 `DropItem(..., pullTowardsEntity: chest)`로 떨어뜨린 뒤 끌어당긴다(`InventoryUtility.cs` `TryQuickStackItemIntoChest`). 상자 도착은 확인하지 않는다.

#### L3. '가방 정렬' 문구 "핫바 0~9 칸은 그대로"가 부정확
- 위치: `AIInventory.cs:1417`(`sort_bag` label).
- 게임 `Sort(player, true)`는 먼저 `QuickStack(player, player)`로 쌓을 수 있는 칸을 합친다(`InventoryUtility.cs:917-920`). 그래서 핫바 스택이 가방 스택으로 합쳐질 수 있다. 정렬 순서 변경만 핫바를 건너뛴다.

#### L4. 착용 프리셋 값이 연결 초기화에서 지워지지 않음
- 위치: `AIBody.cs:32, 306-308`, `AIBody.Reset`(:185-196).
- `_preset`은 첫 입력 때 한 번만 읽고 `Reset`에서 -1로 돌아가지 않는다. 월드 재입장이나 다른 저장 캐릭터로 바뀐 뒤에도 이전 값을 매 입력 `equipmentPresetIndex`로 보낸다. 그러면 저장된 활성 프리셋을 덮어쓴다(게임은 매 입력 그 값으로 `ActiveEquipmentPresetCD`를 설정함, `SelectedEquipmentChangeSystem.cs:583`).

#### L5. 씨앗 뿌리개: 손 칸 교환이 종자 순서를 바꿔 다른 종자를 심고 진척으로 셀 수 있음
- 위치: `AIFarm.cs:1301-1318`(SelectedHotbar의 직접 버퍼 교환, 기존 코드), `:776-781`, `:1198-1203`(plant 완료 = 아무 작물이나 생김).
- 시나리오:
  1. 핫바가 가득 찼고, 핫바 9에 다른 종자 B가 있다.
  2. 고른 종자 A(가방 앞쪽 첫 종자 판정 통과)를 위해 뿌리개를 손 칸으로 옮긴다. 이때 9번의 B가 뿌리개 자리(뒤쪽)로 간다.
  3. 게임은 버퍼 앞에서부터 첫 종자를 쓰므로(`SeederSlot.cs:71-95`) B가 앞이면 B를 심는다.
  4. `Satisfied`는 작물 존재만 보므로 A의 plant 진척으로 센다.
- 구현자가 '추정'으로 남긴 위험이 실제 경로로 확인된다.

#### L6. 회수 불가 판정 중 자기 회수를 남의 탓으로 볼 수 있음
- 위치: `AIMaintenance.cs:463-467`, `:481-485`.
- `MainHolds`는 가방과 파우치만 본다.
- 시나리오:
  1. 회수 take가 서버에서 처리됐지만 `TakeConfirmed` 기록 전에 판단 모델이 행동을 바꾼다(MaintenanceSkill receive 단계의 `maintenance-transfer-processing` 대기 중 재판단).
  2. 판단 모델이 그 강화 장비를 착용하거나 상자에 넣는다.
  3. 다음 관측에서 입력 칸은 비었고 가방·파우치에 없다. 그래서 "대장간 입력 칸이 비어 있음"과 함께 근처에 있던 호스트가 "가져간 것으로 보이는 사람(추정)"으로 상위 LLM에 보고된다.
- 착용 칸까지 확인해야 한다.

#### L7. "그때 대장간 근처에 다른 플레이어가 없었음" 문구가 관측 범위를 넘어 단정
- 위치: `AIMaintenance.cs:451`.
- 근처 플레이어는 AI가 대장간을 본 두 시점(마지막으로 장비를 본 때, 사라진 것을 본 때)에만 수집된다(:470-479). 그 사이에 왔다 간 사람은 알 수 없다.
- "관측한 두 시점에는 근처에 플레이어가 없었음; 누가 가져갔는지 모름"처럼 관측 범위를 밝혀야 한다. 사람 이름이 나오는 경우의 '추정' 표시는 합의대로다.

#### L8. 경로를 찾을 수 없는 화면 밖 대장간 pending은 포기 선택지가 없음
- 위치: `AIMaintenance.cs:378`, `:455-456`.
- 포기는 대장간 타일을 직접 볼 때만 생긴다. 경로 미확인이면 사실 문장만 남고, 새 업그레이드는 연결 초기화 전까지 계속 막힌다. 합의 M2의 두 경우(대장간 없음/칸 빔)는 관측이 전제이므로 범위 밖으로 볼 수도 있지만, 막힘 자체는 남는다.

#### L9. 업그레이드 재료 사실에 사이 수리의 재료 소비가 섞일 수 있음
- 위치: `AIMaintenance.cs:607`.
- `MaterialFact`는 레벨 변화를 처음 본 순간에 고정된다. 대장간이 시야 밖이라 레벨 변화를 늦게 보면, 그 사이 M3로 허용된 일반 수리의 재료 감소가 업그레이드 재료 보고에 들어간다.

#### L10. 수면: 체력이 가득하면 잠든 즉시 끝나 수면 버프를 받을 수 없음
- 위치: `AIBed.cs:181`.
- 게임은 잠든 뒤 2초가 지나야 수면 품질 버프를 줄 수 있다(파일 머리 주석 :10-11). 체력이 가득한 상태로 고르면 잠든 첫 틱에 "체력 가득"으로 끝난다. 선택지 문구는 "체력이 차거나 60초"라고 밝힌다.

#### L11. 물주기 '전체'의 의미가 바뀌어 상위 목표 진척에 빈 땅이 섞임
- 위치: `AIFarm.cs:618-638`.
- 상위 LLM의 `{"do":"farm","mode":"water","count":N}`(item 없음)은 이제 빈 판 땅도 대상이다. 그래서 작물에 물을 주지 않고도 count를 채울 수 있다. 판단 모델이 고르므로 큰 문제는 아니지만, brief.md의 farm 설명("water/harvest의 작물 필터")은 갱신되지 않았다.

#### L12. 상위 LLM 안내와 보고 이름이 '자기 상자' 그대로
- 위치: `Tools/ai-bridge/brief.md:60`("자기 상자의 보관/꺼내기"), 같은 파일 store/take 줄("트리에서 자기 상자→"), `AIInventory.cs:978`(`own_chests`), `AIPlaces.cs:604`(Group "mine").
- 공용 상자가 "own_chests"·"mine"으로 상위 LLM에 간다. 동작은 공용이지만 상위 판단은 자기 상자로 오해할 수 있다.
- `AIBase.Usable`(:167)은 정의만 있고 호출하는 곳이 없다. integration.md의 "접근 판정 = AIBase.Usable"은 사실과 다르다. 실제 판정은 기억 기반의 `AIBase.Own`/`AIInventory.OwnChest`다. 동작은 공용 합의와 맞는다.

#### L13. 파우치 안 재료·수령은 생활 쪽 계산에서 빠짐
- 위치: `AICraft.cs:251`, `AIMaintenance.cs:85-92`(Main), `AICook.cs` MainCount, `AIFarm` 종자 계산.
- B1로 파우치를 다루게 됐지만, 제작·요리·농사·정비 비용은 여전히 main만 센다. 씨앗·작물 파우치를 착용하면 다음 문제가 생긴다.
  - 줍거나 꺼낸 작물·씨앗이 파우치로 먼저 들어간다(`InventoryUtility` 슬롯 탐색).
  - plant 후보에서 그 씨앗이 빠진다.
  - 솥 `clear`·`take` 수령 확인(`AICook.cs:947` MainCount)이 증가를 못 봐 미확인으로 남는다.
- 제작은 게임이 파우치 재료를 먼저 쓰면 `consumed=false`가 된다(I1과 같은 미완료).
- 기존부터 있던 한계지만, 이번에 파우치를 정식 대상으로 넣으면서 드러났다.

### 참고(결함 아님, 확인 결과)
- 서버 `InventoryUtility.Swap`은 칸 요구 조건을 검사하지 않는다(`InventoryUtility.cs:668-693`). 모드가 `SlotAllows`(양쪽)와 `CapacityAfterSwap`(플레이어 쪽만)으로 미리 막는 구조는 맞다.
- MoveAmount의 `endIndex`를 그 칸이 속한 인벤토리 끝으로 둔 것(`InventoryEnd`)은 `TryFindSlotToAddTo`의 범위 규칙상 지정 칸(hint)이 먼저 선택되므로 맞다.
- 상위 LLM 알림은 `AIPlanner.RequestReplan` → `AddEvent(force)` → 보고 `events`로 실제 전달된다(`AIPlanner.cs:230-236, 402-407`, `AIPlanReport.cs:60`). 문장에는 따지라는 지시가 없다. 이름을 댈 때는 "추정 — 게임은 꺼낸 사람을 기록하지 않음"을 붙인다. 상위 LLM은 `say`로 말할 수 있다(brief.md:51, 70).
- 기억 시설 레시피 누출 수정(`AICraft.cs:67-83`, 세 곳 적용)과 가축 제외는 코드상 맞다.
- 게임 서버 제작의 상자 집합(`InventoryUpdateSystem.cs:423-431`)은 손 제작에서도 craftingEntity=player 위치를 기준으로 한다. 모드의 `QueueCraft(... player)` + `CraftChests(player)`와 일치한다.

## 합의 항목별 연결 확인표

연결됨 = 후보 조립 → 판단 요청 → 실행 → 결과 관측이 모두 생산 코드에 있음. 부분 = 연결은 됐으나 위 발견 때문에 일부 상황에서 실패함.

| 항목 | 연결 | 경로 근거 | 관련 발견 |
|---|---|---|---|
| B1 파우치 보기·옮기기 | 연결됨 | `AIInventoryMenu.cs:148` 물품 목록 → MenuMoves move/store/drop/give/hold/use(`AIInventory.cs:556-640`) → MoveAmount/Swap → 양쪽 칸 관측 | L13 |
| B2 빠른 쌓기·정렬 | 연결됨 | 분류 단계 "가방 전체 조작"/"열린 상자 전체 조작"(`AIInventoryMenu.cs:250-256, 268-271`) → `AIInventoryBulk.Queue` → `BulkTick`/`Diff` | L2, L3 |
| B3 프리셋 전환·비활성 보기 | 연결됨 | presets 단계 → `BodyInput.presetAsk` → `ClientInput.equipmentPresetIndex`(`AIBody.cs:306-308`) → 활성 프리셋 관측 | L4 |
| B4 사용(물약·사용 물품·악기) | 연결됨(악기는 관측 없음) | 조작 use → 핫바 Swap → `UseTick`(보조/주 사용) → 수량 감소 | L1 |
| B5 펫 칸 | 연결됨 | `GearSlots` pet(`AIInventory.cs:127`) → equip/unequip | 실게임 |
| B6 꾸미기 칸 | 연결됨 | `GearSlots` vanity, 명시하지 않은 착용 목표에서는 제외(`WantsSlot`) | 실게임 |
| B7 착용 장비 직접 보관·내려놓기·전달, 해제 목적지 확대, 상자 장비 바로 착용·들기 | 연결됨 | store whole CrossSwap / drop·give gear / unequip→CarriedSlots / 상자 equip·hold CrossSwap, `Stale` chestSide | 실게임 |
| B8 자리 옮기기·나누기·합치기 | 연결됨 | 조작 move → MoveAmount(빈 칸·같은 스택) 또는 Swap | — |
| B9 정보 없는 물품 기본 조작 | 연결됨 | store/take/move 칸 전체 Swap | — |
| 상자 공용(호스트·다른 AI 포함) | 연결됨 | `AIBase.Observe`/`NoteOpened`가 주인 검사 없이 기억 → `OwnChest`/`Own` | I3, L12 |
| 공용: 제작 근처 상자 재료 | 부분 | `CraftChests`/`MaterialCount` → 가능 판정·Stale·소비 확인 | I1, L13 |
| M3 솥 입력 정리(비대칭 채우기·보충·빼기) | 연결됨 | cook fill(TargetSlot)·clear 모드(`AICook.cs:551-560, 649-693`) → `CookSkill` _sentClear/fill 확인 | L13 |
| M5 빈 판 땅 물주기 | 연결됨 | water item ground/all(`AIFarm.cs:618-638, 684-688`) → `Satisfied`=IsWatered | L11 |
| M6 괭이 없이 수확 | 연결됨 | 괭이 없을 때 `AttackHarvestCandidate` → FarmSkill primary 조준 → 작물 소멸/미성숙 확인 | 실게임 |
| M7 씨앗 뿌리개 | 연결됨 | plant second `seeder:` → `SeederCandidate` → 도구 들고 범위 누름, 범위로 심긴 대상 칸 셈 | L5 |
| 기억 시설 레시피 누출 수정 | 연결됨 | `PrefabManual` 세 곳 | — |
| M2 침대에서 자기 | 연결됨 | 루트 rest(`AILife.cs:132, 141`, `AILifeTree.cs:40`) → kind sleep(`AIActions.cs:51`) → `SleepSkill`, PlayerStateEnum.Sleep 관측 | L10 |
| 정비 M1 화면 밖 대장간 회수 | 부분 | `Recovery` 이동 후보(`AIMaintenance.cs:370-384`) → 도착 뒤 정상 recover | I2 |
| 정비 M3 pending 중 일반 수리 | 연결됨 | Pending/Repair 분리, `RepairBlockedBy` | L9 |
| 정비 M4 파우치 장비 | 연결됨 | `ActiveSlot`/`SourceSlots`가 CarriedSlots 사용, `maintenance_put` Stale 확장 | L13 |
| 정비 M2 회수 불가 포기 + 상위 LLM 알림 | 부분 | `ObserveLoss` → `AbandonCandidate`(정비 루트·recover 말단) → `MaintenanceAbandonSkill` → `RequestReplan` | I2, L6, L7, L8 |

합의 공통 조건 확인:
- 판단 모델 선택: 새 선택지는 모두 트리 자식이나 말단이다. 코드가 대신 실행하는 경로는 없다. 유효 후보를 지우는 곳은 게임 규칙상 실행 불가한 경우뿐이다(남이 차지한 침대는 사실로만 표시, 뿌리개 첫 종자 불일치, 괭이가 있을 때 쳐서 수확은 내지 않음).
- 후보 상한·페이지: 새로 생긴 수치는 게임 규칙을 그대로 옮긴 것(상자 10칸/20개)과 기존 농사 실행 묶음 6칸뿐이다. 후보 상한·페이지 재도입은 없다. 다만 I3처럼 한도가 없어 생기는 규모 문제가 있다.
- 상자 공용: 보관·꺼내기·빠른 쌓기·제작 모두 주인 검사가 없다. 합의와 맞다.
- 정비 포기 뒤 상위 LLM: 상황만 알리고 따지라는 지시가 없다. 추정임을 밝힌다. 대장간이 없을 때는 모른다고 쓴다. 합의와 맞지만 I2의 잘못된 "없음 확인"과 L6·L7의 문구 문제가 있다.

## 실게임 확인 필요

1. 착용 칸↔상자 칸, 상자 칸→착용 칸 Swap 뒤 게임의 장착 효과(EquipmentHandler, 장비 엔티티 갱신)가 정상 적용되는지. 서버 Swap 자체는 교환만 한다.
2. 착용 칸에서 `DropItem`으로 바로 내려놓기·전달(서버 DropItem은 아무 칸이나 받음). 장착 해제 효과가 따라오는지.
3. 펫 칸에 Swap으로 펫을 넣었을 때 펫 소환·해제가 되는지. 꾸미기 칸 표시 반영.
4. 파우치에 실제로 들어가는 품목. 플레이어 버퍼에서 파우치 칸과 커서 칸(`CraftingCD.outputSlotIndex`)의 순서. 솥 take·clear의 `MoveOrDropAmount(..., -1, cursor)`가 파우치로 넣는지(L13).
5. `QuickStackToNearbyChests`의 실제 상자 집합(콜라이더 OverlapSphere, 시야 무관)과 모드 근사(보이는 상자 10칸/20개)의 차이. 끌려간 물품이 실제로 상자에 도착하는지.
6. `Sort(player, true)`의 핫바 합치기 결과(L3).
7. 악기 주 사용 1회가 소리를 내는지. CastingItem(선물 상자·탐지기·소환·귀환 우상)이 보조 사용 유지로 쓰이는지. 귀환 우상 사용 뒤 이동 상태.
8. AI 플레이어의 `equipmentPresetIndex` 입력으로 프리셋이 바뀌는지. `EquipmentPresetsBuffer.Length`가 해금된 프리셋 수인지(항상 3이면 모든 캐릭터에 메뉴가 나옴).
9. 씨앗 뿌리개의 범위 크기·조준점 배치. 첫 종자 탐색에 파우치·장비 칸이 들어가는지(L5).
10. 쳐서 수확: 무기 휘두름 범위에 닿은 옆의 미성숙 작물이 함께 부서지는지(빽빽한 밭에서 피해 위험). 무기별 타격 수. 수확 산물 드롭.
11. 침대: AI 플레이어에게 ClaimBedSystem이 도는지. 잠자리 좌표에 올라설 수 있는지(충돌). `move` 정규화 입력으로 0.35칸 안에서 멈추는지. 호스트가 아직 안 쓴 침대를 AI가 영구 차지하는 영향.
12. 제작 상자 집합: 게임은 콜라이더 거리와 충돌 필터(CollidesWith=1)를 쓰고, 모드는 중심 거리를 쓴다. 솥 같은 자동 이체 시설 인벤토리가 실제로 포함되는지.
13. 청크 언로드 거리와, 대장간 Entity가 다시 로드될 때 바뀌는지(I2).
14. 대장간이 부서질 때 입력 칸 물품이 떨어지는지.
15. 작물 없는 판 땅에 물뿌리개를 쓰면 wateredGround가 되는지.
16. 정비 시설(SalvageAndRepairStation, UpgradeForge)이 `AIBase.View`에서 상자로 잡히지 않는지(CanCraftObjectsBuffer 보유 여부). 잡히면 공용 상자로 기억되고 꺼내기 대상이 된다.
17. 파우치 칸 장비에 대한 `RepairOrReinforce`·업그레이드 투입 처리. 정비 비용을 게임이 파우치·상자에서도 쓰는지.
18. 솥 fill 보충(두 입력이 모두 찬 상태에서 같은 재료 추가) 중 자동 생산과 겹칠 때의 실제 수량.
19. 실제 판단 모델이 새 단계(bulk/presets/rest/clear/second=seeder/abandon)를 문구만 보고 올바르게 고르는지.
