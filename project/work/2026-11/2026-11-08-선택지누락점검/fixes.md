# 독립 검수 발견 수정 (2026-11-08)

- 대상: worktree `C:\Dev\CoreKeeperModSDK-gap-integrated`, 브랜치 `gap-integrated-20261108`. 수정 전 068c7b5, 근거 [review.md](review.md).
- 먼저 공식 ai-character `ae09acf`(시험 명령, /ai hold·tp)를 병합했다(`6b9b511`). 텍스트 충돌은 없었다. 다만 병합이 `AIBody.Tick`을 `Emit`으로 나누면서, 가방 쪽 프리셋 읽기(B3)가 없어진 지역 변수 `em0`을 참조해 compile-check가 깨졌다. `6f5a84f`에서 고쳤다.
- 새 모델 호출 경로는 만들지 않았다. 정비 포기 알림은 계속 `AIPlanner.RequestReplan` → `Report`로 가므로 /ai hold의 막는 지점을 거친다.
- 최종 커밋: `6f5a84f`. 실게임, 설치, 공식 병합은 하지 않았다.

## 커밋

| 커밋 | 내용 |
|---|---|
| `6b9b511` | ae09acf 병합 |
| `c93627d` | I1 제작 상자 재료 |
| `bdc8fee` | I2 대장간 재연결과 '없음' 판정 보류, L6·L7·L9, 정비 비용의 파우치 포함(L13) |
| `e1006e1` | I3 상자 지역 묶음, L1·L2·L3·L11·L12 |
| `6f5a84f` | 병합 깨짐 수정, L4·L5·L10, 요리·씨앗 뿌리개의 파우치 포함(L13) |

## 발견별 처리

### I1 근처 상자 재료 제작 — 고침 (`AICraft.cs`)
- `ReadRecipe`는 서버 소비 순서대로 이번 제작이 실제로 가져갈 칸만 기록한다. 순서는 플레이어 버퍼 전체(인덱스 순서: 가방, 파우치…)를 먼저 보고, 그다음 가까운 상자다(`InventoryUtility.Craft` → `ConsumeObject`). `Stale`은 그 칸만 검사한다.
- 따라서 가방만으로 충분한 제작은 상자 칸을 하나도 기록하지 않는다. 컨베이어가 상자를 채워도 거절되지 않는다.
- 새 함수는 `AICraft.PlayerItems`, `AICraft.ConsumedFromPlan`, `SlotDecrease` 셋이다.
- 소비 확인은 계획한 칸마다 그 칸의 감소량을 더해서 한다. 계획한 칸에 같은 물품이 들어와 감소가 가려졌다면, 시작 때 비어 있던 커서에 고른 출력이 나타났는지로 확정한다. 게임은 비용을 소비한 뒤에만 커서 출력을 만든다. 이때 '감소 일부를 직접 못 봄' 사실을 남긴다.
- `MaterialCount`도 플레이어 버퍼 전체를 센다(L13 제작 부분).
- 시험(`Tools/life-craft-tests/Program.cs` `PlannedTakeChecks`, 9개):
  - 가방만으로 충분한 제작은 상자 칸을 기록하지 않는다.
  - 상자가 바뀌어도 거절되지 않는다.
  - 상자가 계속 채워지는 동안에도 완료된다.
  - 가장 가까운 상자 칸만 기록한다.
  - 먼 상자가 바뀌어도 거절되지 않는다.
  - 계획한 칸에 물품이 들어와도 커서 출력으로 완료된다.
  - 파우치 칸 재료도 계산한다.
  - 커서 출력도 계획한 칸의 감소도 없으면 완료되지 않는다.
- 기존 대량 제작·상자 시험도 그대로 통과한다.

### I2 대장간 식별 — 고침 (`AIMaintenance.cs`)
- `ObservePending`은 맨 앞에서 `Rebind`를 부른다. 저장된 Entity가 그 타일의 대장간이 아니면, 같은 타일·종류의 대장간을 찾아 `Request.Station`, `InputSlot`, put/take 이동의 엔티티를 바꾼다. 이후 회수 확인은 기존처럼 입력 칸 물품의 신원으로 한다.
- '대장간 없음'은 타일이 보이는 상태에서 대장간이 없는 것이 server tick 120 동안 이어질 때만 판정한다(`NoForgeSince`, `NoForgeTicks`). 청크가 다시 로드될 때는 타일이 대장간보다 먼저 보일 수 있어서다. 그전에는 '판정 보류'만 기록한다.
- 시험(`Tools/maintenance-tests/GapCases.cs`):
  - 대장간이 없어진 직후에는 포기 선택지가 없고, 121 tick 뒤에 생긴다.
  - 같은 타일에 새 Entity로 다시 만들어진 대장간에서는 정확한 회수가 그대로 나온다. 500 tick이 지나도 '없음'으로 말하지 않는다.

### I3 상자 후보 폭주 — 고침 (새 `AIChestAreas.cs`, `AIInventory.cs`, `AIInventoryMenu.cs`)
- 후보 상한이나 페이지는 다시 만들지 않았다. 상자가 40개 이하면 지금처럼 바로 보인다.
- 40개를 넘으면 정렬된 정사각 타일 구역(`area:x,y,size`)을 보여 준다. 시작 크기는 32이고, 구역 수가 40개를 넘으면 크기를 두 배로 늘린다.
- 구역을 고르면 그 안의 상자를 보여 준다. 아직 40개를 넘으면 1/4 크기 칸(최대 16개)으로 다시 나눈다.
- 적용한 곳:
  - 생활 보관·꺼내기 갈래 (`where` 비었을 때, 구역일 때, 기지 도착 후 목록)
  - 가방 메뉴 chests 단계 (`AIInventoryMenuFrame.chestArea`)
  - 상위 보고 `ReportJson`: 키를 `own_chests`에서 `chests`로 바꿨고, 상자가 많으면 구역과 상자 수를 보낸다.
- `Core/`에 두면 Unity.Mathematics가 없는 core-tests가 깨져서 `Scripts/`에 뒀다. 메타 파일도 넣었다.
- 시험(`Tools/life-inventory-tests/GapCases.cs` `WarehouseCases`, 5개):
  - 상자 300개 창고에서 모든 단계가 40개 이하다.
  - 300개 전부에 닿을 수 있다.
  - 첫 단계는 구역만 보여 준다.
  - 보고는 `chests` 키 아래 구역으로 간다.
  - 상자 5개면 바로 보인다.
- 남은 것: 생활 트리가 열려 있는 동안 기존 실행 후보 전체를 유지한 채 항목을 덧붙이는 구조(검수 `AILife.cs:156-190`, 기존 트리 감사의 '입구' 문제)는 이번 범위가 아니라 그대로다. 상자 단계 자체는 40개 이하로 묶였다.

### 경미

| 발견 | 처리 | 위치 |
|---|---|---|
| L1 악기를 관측 없이 성공 처리 | 고침. 연주 입력은 보내지만 `ConfirmedWork=0`, '결과 미확인'이고 `Completed`를 기록하지 않음 | `AIInventory.cs` UseTick |
| L2 일괄 정리가 아무 칸 변화나 받아들임 | 고침. 정렬은 대상 인벤토리의 순서만 바뀌고 종류별 총량이 그대로일 때만 완료. 빠른 쌓기는 플레이어에서 빠진 수량이 있어야 완료하고, 지켜본 상자 쪽 증가를 따로 보고하며 근처 빠른 쌓기는 끌려가는 중일 수 있음을 밝힘. 시간 초과 문구에 '다른 변화가 섞임' 추가 | `AIInventoryBulk.Diff` |
| L3 '핫바 그대로' 문구 | 고침. 같은 물품 합치기로 핫바 스택이 합쳐질 수 있음을 밝힘 | `sort_bag` label |
| L4 프리셋 값이 초기화 때 남음 | 고침. `AIBody.Reset`에서 `_preset=-1` | `AIBody.cs` |
| L5 씨앗 뿌리개가 다른 종자를 심고 진척으로 셈 | 고침. 대상 칸 작물의 수확물이 고른 종자의 수확물과 다르면 사실로 알리고 작업으로 세지 않음(`WrongSeed`). 첫 종자 탐색과 종자 수는 가방 다음 파우치 순서로 셈 | `AIFarm.cs` |
| L6 자기 회수를 남의 탓으로 봄 | 고침. 플레이어 버퍼 전체(착용·커서 포함)에서 장비를 찾음. 자기 회수 요청이 처리된 뒤 입력 칸이 비었으면 아무도 지목하지 않음. 시험 2개 | `AIMaintenance.cs` MainHolds/ObserveLoss |
| L7 '근처에 아무도 없었음' 단정 | 고침. "마지막으로 장비를 본 때와 사라진 것을 본 때 6칸 안에 없었음(그 사이는 보지 못함)" | `LostWho` |
| L8 경로 없는 화면 밖 대장간은 포기 불가 | 고치지 않음. 합의 M2의 두 경우(대장간 없음, 칸 빔)는 직접 본 것이 전제다. 보이지 않는 곳을 잃었다고 판정하면 사실이 아니게 된다. 막힘 사실은 그대로 보고됨 | — |
| L9 업그레이드 재료 사실에 수리 소비가 섞임 | 부분 고침. 레벨 변화를 보기 전에 수리를 게시했다면 재료 사실에 '섞였을 수 있음'을 붙임. 수량을 분리하지는 않음(수리 비용 감소 시점을 따로 관측하지 않아서) | `RepairBeforeLevel` |
| L10 체력이 가득하면 즉시 깸 | 고침. 체력이 가득해도 잠든 뒤 2초(품질 수면 버프 조건)는 유지. 선택지 문구도 고침 | `AIBed.cs` |
| L11 물주기 '전체'의 의미 | 문서로 고침. brief의 farm 설명에 "item 없는 water는 빈 판 땅도 대상"을 추가. 동작은 판단 모델 선택이라 그대로 | `brief.md` |
| L12 '자기 상자' 표현, 쓰이지 않는 Usable | 고침. brief 세 곳을 '상자(모두 공용)'로, 보고 키를 `chests`로, 장소 목록에서 남의 상자는 "other" 그룹으로. 호출처 없는 `AIBase.Usable`은 지우고 실제 판정(`Observe`/`NoteOpened`→`Own`/`OwnChests`, 제작은 `CraftChests`)을 주석으로 적음 | `brief.md`, `AIInventory.cs`, `AIPlaces.cs`, `AIBase.cs` |
| L13 파우치 계산 누락 | 고침. 제작은 플레이어 버퍼 전체(I1). 요리 수령·제거 셈(`AICook.MainCount`), 씨앗 뿌리개 첫 종자, 정비 비용(`AIMaintenance.Main`)은 가방+착용 파우치. 손 심기 후보는 핫바로 들어야 하므로 가방만 그대로이고, 파우치 씨앗은 B1 옮기기로 꺼냄 | 각 파일 |

## 시험 결과 (최종 6f5a84f)

| 시험 | 결과 |
|---|---|
| life-craft-tests | 47/0 (기존 38 + 새 9) |
| life-inventory-tests | 222/0 (217 + 5) |
| maintenance-tests | 149/0 (144 + 5) |
| life-cook-tests | 70/0 |
| life-tests | 129/0 |
| execution-tests | 181/181 |
| restriction-tests (prepare.mjs 먼저) | ALL PASS 47 |
| gather 61, travel 51, path-preparation 25, action-input 43, info | 모두 통과 |
| gates.sh compile-check | 오류 0 |
| gates.sh core-tests | 1571 OK |
| recall-loop / protocol / fake-bridge / logshape / movecheck | OK |
| bridge-wiring | 단독 실행 23/23 OK. 다른 시험과 동시에 돌렸을 때 한 번 실패, 다시 돌리면 통과 |
| exp-sim | 개별 작업 폴더를 주면 ALL CHECKS OK. gates.sh는 공용 `%TEMP%\exp-sim\exp_state.json.tmp`가 다른 프로세스에 잡혀 있어 정리 단계에서 실패함(exit 127, 코드와 무관) |
| memgraph | 이 worktree에 KG 데이터가 없어 건너뜀 |
| Unity 컴파일 (`gates.sh --unity --tag gap-fixes`) | exit 0, error CS 0. Unity가 꺼질 때까지 약 150초 기다린 뒤 실행 |

## 남은 것과 통합 주의
- 실게임에서 확인할 것: 청크 언로드 거리와 대장간 재생성(I2), 상자 유입 중 제작, 대형 창고 구역 탐색의 실제 판단, 씨앗 뿌리개 작물 수확물 비교, 정비 비용을 파우치·상자에서도 내는지(상자 쪽은 미확인이라 세지 않음). 검수 '실게임 확인 필요' 19개 목록은 그대로 유효하다.
- 이동전달-개선4건(move-deliver)을 합칠 때 `AIInventory.cs`·`AIInventoryMenu.cs`에서 충돌이 예상된다. 이번에 frame 필드 `chestArea`, `Diff`, UseTick이 더 바뀌었다.
- 사용자 미커밋 환경 파일(Packages, ProjectSettings 등)은 Unity 컴파일용으로 남겨 두었고 커밋하지 않았다.

## 2차 (재검수 [review-2.md](review-2.md) 반영, 6f5a84f 위)

최종 커밋 `c8dee71`. 실게임, 설치, 공식 병합은 하지 않았다. L8은 할 일로 남기기로 해서 손대지 않았다.

| 커밋 | 내용 |
|---|---|
| `4fdd253` | N3 |
| `d7c6f85` | N4, N5 |
| `c8dee71` | N1, N2, N6, N7, L12 남은 부분 |

### 발견별 처리
- **N1 기지 보관 평면 후보 (중요)** — 고침 (`AIBase.Candidates` Store 절)
  - 목표 where가 "base"면 기지에 도착했을 때 보이는 상자만 대상으로 한다(`ArrivedAtBase`).
  - 대상이 40개 이하면 지금처럼 상자마다 후보를 낸다. 넘으면 `AIChestAreas.Split`으로 구역마다 가장 가까운 상자 하나를 후보로 낸다. 나머지 상자는 생활 보관 트리(구역 → 상자)에서 고른다. 상한·페이지는 없다.
  - 문구는 실제 사실로 바꿨다: "{이름}가 놓은 상자" 또는 "공용 상자(다른 플레이어가 놓았거나 원래 있던 것)", "기지에서 보이는" 또는 "기지 밖". 구역 후보에는 "구역의 상자 N개 중 가장 가까운 것"을 붙인다.
  - 상자 놓기 까닭 문구의 "다른 플레이어의 상자는 쓰지 않음"도 고쳤다.
  - 이 경로는 production `AIBase`를 링크하는 시험 harness가 없어 compile-check와 Unity 컴파일로만 확인했다.
- **N2 상위 보고** — 고침
  - 최상위 `chests`(`AIBase.ReportFields`): 상자가 40개 이하면 모두 상세히 싣는다. 넘으면 직접 놓은 상자만 상세히 싣고, 나머지는 구역별 개수·열어 본 수·가장 가까운 거리로 요약한다.
  - 장소 목록(`AIPlaces`): 상자가 많으면 남의 상자는 구역마다 가장 가까운 상자 한 줄로 싣는다. 식별자는 goto에 그대로 쓸 수 있다.
  - 이름이 겹치던 `inventory_actions.chests`는 `chest_ids`로 바꿨다. 구역 id를 store/take의 where로 써도 되게 했다(`AIInventory.ChestWhere`, `AILife` 목표 일치 판정).
  - brief 40·45줄을 '모든 상자 공용, 많으면 구역 요약', 'chest_ids는 where용 식별자'로 고쳤다.
- **N3 부분 유입** — 고침
  - 계획한 칸이 줄었지만 계획량보다 적게 줄어든 경우도, 시작 때 비어 있던 커서에 고른 출력이 나오면 소비로 확정한다. 이때 '감소를 다 보지 못함' 사실을 남긴다.
  - 시험 2개 추가(`life-craft-tests`): 상자에서 3개를 가져가는 제작 중 유입 1개로 상자가 10→8이 되는 경우. 이전 코드였으면 확정되지 않는다.
- **N4 자기 회수 뒤 pending 남음** — 고침
  - 자기 take가 처리됐고 장비가 이 플레이어에게 온 경우(어디든 지니고 있거나, 그 take로 입력 칸이 빈 경우)에는 `OwnTakeDone` 사실을 남기고 pending을 끝낸다. 그 pending을 회수하는 스킬이 아직 돌고 있으면 그 스킬이 직접 수령을 확인하게 둔다(`MaintenanceSkill.PendingRun`).
  - 끝낸 뒤에는 새 업그레이드를 고를 수 있다. 레벨 변화를 확인했는지 여부도 사실로 남긴다.
  - 시험 2개(`maintenance-tests`): 장비를 옮긴 경우와 착용한 경우 모두 pending이 정리되고 새 업그레이드가 가능하다.
- **N5 시야 밖에서 NoForgeSince 유지** — 고침
  - 타일이 안 보이면 `NoForgeSince`를 지운다.
  - 시험 3개: 보였다가 안 보인 뒤 500 tick이 지나 다시 보면 판정을 보류하고, 121 tick 뒤에 포기 선택지가 나온다.
- **N6 음식 꺼내기 후보** — 고침
  - (상자, 음식) 출처가 40개 이하면 모두 후보로 둔다. 제약 해제 합의에 따라 공급 대안을 자르지 않는다. 처음에는 종류별로 하나만 남겼는데, restriction-tests "all supply alternatives"가 실패해서 이렇게 바꿨다.
  - 40개를 넘으면 음식 종류마다 하나(배고픔 효과가 크고 가까운 상자)만 후보로 내고, 같은 음식이 든 다른 상자 수와 '생활 꺼내기에서 고름'을 알린다.
- **N7 기지 구역의 범위 필터** — 고침
  - 기지 도착 목록의 구역 id를 `base:area:x,y,s`로 바꿔, 구역을 골라도 기지 범위 필터가 유지되게 했다. 생활 목표 일치 판정도 `area:`와 `base:area:` 단계를 받아들인다.
- **L8** — 사용자 결정에 따라 손대지 않음(할 일로 남김).

### 시험 결과 (최종 c8dee71)
- 오프라인 시험:
  - life-craft 49/0, life-inventory 222/0, maintenance 154/0, life-cook 70/0, life 129/0
  - execution 181/181, restriction ALL PASS 47
  - gather 61, travel 51, path-preparation 25, action-input 43, info 모두 통과
- gates.sh: 실패 0
  - compile-check 오류 0, core-tests 1571
  - exp-sim, recall-loop, bridge-wiring, protocol, fake-bridge, logshape, movecheck 모두 OK
  - memgraph는 데이터가 없어 건너뜀
- Unity 컴파일(`gates.sh --unity --tag gap-fixes2`): exit 0, error CS 0
- 함께 바꾼 시험 파일
  - AILife 바인딩 추출본(`LifeBindingSource.cs`, life-craft·life-inventory)을 `ExtractLifeBinding.ps1`로 다시 만들었다.
  - `AIChestAreas.cs`를 life-tests와 restriction-tests 프로젝트에 링크했다.

## 3차 (재검수 [review-3.md](review-3.md) 반영, c8dee71 위)

최종 커밋 `b17969b`. 실게임, 설치, 공식 병합은 하지 않았다. L8은 손대지 않았다.

| 커밋 | 내용 |
|---|---|
| `ef2541d` | R1 |
| `fe074d3` | R2 |
| `b17969b` | R3~R6, L12 남은 문구 |

### 발견별 처리
- **R1 구역 id를 where로 (중요)** — 고침
  - `AIInventory.CheckGoal`(상위 계획과 호스트 명령이 같이 씀)이 기억 상자를 하나 이상 품은 `area:x,y,s`와 `base:area:x,y,s`를 받는다(`KnownChestWhere`).
  - `ChestWhere`와 기지 검사(`IsBaseWhere`: 도착 확인, `Stale`, `StallNote`, 기지 미도착 이동)도 같은 판정을 쓴다.
  - `inventory_actions.chest_ids`는 `[{"id","desc"}]`다. id는 where에 그대로 쓰는 `chest:x,y` 또는 구역 id다. brief 45줄도 이 형식으로 설명했다.
  - 시험 8개(`life-inventory-tests` `WarehouseCases`):
    - chest_ids의 순수 id
    - 구역 where가 take·store 모두 통과
    - 상자가 없는 구역은 거절
    - 구역 꺼내기 목표 → 그 구역 상자 열기 → 정확한 꺼내기 3개 → 정상 move 요청 1회
  - 상위 계획 객체 자체는 이 harness에 링크되지 않는다. 그래서 계획 경로가 공통으로 부르는 `CheckGoal`과, 그 목표의 실행 후보·요청을 시험했다.
- **R2 남이 먼저 꺼낸 경우의 경합** — 고침
  - take를 게시할 때 그 정확한 장비(종류·레벨·aux)를 지닌 수를 기록한다(`HeldBeforeTake`).
  - 이후 관측에서 하나라도 늘어난 것을 봤을 때만 회수 완료로 기록한다(`TakeArrivedSeen`, `HeldCount`). 지금 지니고 있는지, 봤지만 그 뒤 옮겼는지에 따라 문구를 나눈다.
  - 처리됐는데 들어온 것을 보지 못했으면 M2의 잃음 흐름으로 간다. 문구는 "들어온 것을 보지 못함, 남이 먼저 꺼냈을 수 있음"이고, 포기 선택지를 거쳐 상위 LLM에 알린다. 쓰이지 않게 된 `MainHolds`는 지웠다.
  - 시험: 남이 먼저 꺼낸 경우 포기 선택지가 나오고, 근처 플레이어를 추정으로 밝히고, 회수 완료로 기록하지 않고, 상위 LLM에 알린다. 들어온 것을 본 뒤 옮긴 경우와 착용한 경우는 회수로 끝난다.
  - 잠깐 시도했다가 되돌린 것: 정확한 수령까지 확인됐는데 행동이 멈춘 pending을 관측 때 끝내 봤다. 하지만 기존 'pending identity regression' 시험이 깨졌다. 그 pending은 회수 선택으로 업그레이드 단계를 마무리하는 기존 설계였다.
- **R3 평면 문구의 구역 id** — 고침. 평면 보관 후보는 생활 트리 첫 단계와 같은 구역(기억한 모든 상자로 Split)을 쓰고, 그 구역 id와 기억 상자 수를 밝힌다.
- **R4 상위 LLM의 음식 정보** — 고침
  - 최상위 `chests` 구역 요약에 마지막으로 열었을 때 음식이 든 상자 수와 음식 종류를 넣었다.
  - brief 21·22줄은 '기억한 상자(모두 공용)', '구역으로 묶였으면 내용은 안 보이지만 행동 판단기가 음식 상자 꺼내기 선택지를 받음'으로 고쳤다.
- **R5 자유 시간 보관이 직접 놓은 상자만 봄** — 맞춤
  - 진입 조건은 기억한 상자 가운데 자리가 있을 수 있는 아무 상자로 바꿨다.
  - 화면 밖 상자로 걸어가는 후보도 기억한 모든 상자에서 고른다. 직접 놓은 상자를 먼저 보고, 그다음 가까운 순서다.
  - 경로 탐색은 판단 한 번에 최대 8회로 둔 처리 예산이다. 선택지 수 상한이 아니고, 상자가 많을 때 길 계산이 폭증하는 것을 막는다.
- **R6 음식 종류가 40을 넘을 때** — 고침. 출처가 40개를 넘고 종류도 40개를 넘으면 배고픔 값이 같은 종류를 한 묶음으로 보고, 묶음마다 가장 나은 출처 하나를 후보로 낸다. 같은 묶음의 다른 종류는 이름을 밝히고, 생활 꺼내기 트리로 고른다고 알린다. 출처가 40개 이하면 지금처럼 모두 후보로 둔다(restriction 시험 유지).
- **L12 '놓은 상자' 문구 4곳** — 고침: 꺼내기 후보 문구(`AIBase` TakeOrGo), hunger_note, brief 21줄, brief 85줄('where 생략 = 기억한 모든 상자').

### 시험 결과 (최종 b17969b)
- 오프라인 시험:
  - life-craft 49/0, life-inventory 230/0, maintenance 157/0, life-cook 70/0, life 129/0
  - execution 181/181, restriction ALL PASS 47
  - gather 61, travel 51, path-preparation 25, action-input 43, info 모두 통과
- gates.sh(`--unity --tag gap-fixes3`): 실패 0
  - compile-check 오류 0, core-tests 1571, exp-sim OK
  - Unity 컴파일 exit 0, error CS 0
  - exp-sim은 앞선 실행 한 번에서 공용 임시 폴더가 잡혀 exit 127이었다. 개별 폴더로 돌리면 OK였고, 다시 돌린 gates에서도 OK였다.

## 4차 (재검수 [review-4.md](review-4.md) 반영, b17969b 위)

최종 커밋 `a523367`(`ef0abda` → `a523367`). 실게임, 설치, 공식 병합은 하지 않았다. L8은 손대지 않았다.

### 공통 규칙
`AIChestAreas.Step(items, tile, parent, rank)`: 상자가 40개 이하면 모두 선택지로 낸다. 넘으면 생활 트리와 같은 `Split(items, parent)` 구역마다 하나를 낸다. 순서는 열린 상자가 먼저, 그다음 가까운 상자다.
- 한 단계의 상자 선택지는 40개 이하다. 상한도 페이지도 없다.
- 나머지 상자는 생활 트리(구역 → 작은 구역 → 상자)로 모두 닿는다.
- 경로 탐색은 그대로 나열하는 상자(40개 이하일 때)와 사람이 고른 상자에만 한다. 구역 대표의 걷기 후보는 경로를 미리 찾지 않고 "길은 고른 뒤 찾음"으로 낸다.

### 전수 목록: 기억한 상자를 도는 생산 코드 (grep `OwnChests()|S.chests.Values|BaseChests()|ChestsInView(`)

| # | 생산자 | 하는 일 | 처리 |
|---|---|---|---|
| 1 | `AIInventory.ChestCandidates` (take/store 목표: 구역 where, where 생략, base) | 상자마다 걷기·열기 후보 | **F2 고침**: `Step` 적용(부모 구역 = 목표 where의 구역), 대표 걷기는 경로 미탐색 |
| 2 | `AIInventory.Candidates` slot 분기 | where 상자로 정확 이동 | **F1 고침**: 구역 where면 그 구역 안의 열린 상자로 `ExactCandidate` |
| 3 | `AIInventory.LifeEntries` 보관·꺼내기 (`""`/`area:`/`base`/`base:area:`) | 트리 단계 | 이미 `Split` (I3, N7) |
| 4 | `AIInventoryMenu` chests 단계 | 가방 메뉴 상자 목록 | 이미 `Split` (I3) |
| 5 | `AIBase.Candidates` 평면 보관(자유 시간·일반 보관) | 화면 안 상자마다 넣기 | 이미 트리 첫 단계 구역마다 대표(N1, R3). 같은 규칙 |
| 6 | `AIBase.Candidates` 화면 밖 상자로 가기 | 후보 1개 | 원래 하나. 경로 예산 8 (R5) |
| 7 | `AIBase.Candidates` 제작 재료 꺼내기(`S.missing`) | 재료마다 상자를 찾아 1개 | **고침**: 가까운 순으로 돌고 경로 예산 8 |
| 8 | `AIBase.FoodTakeCandidates` | (상자, 음식) 출처 | 40개 이하면 모두, 넘으면 종류마다, 종류도 넘으면 배고픔 값마다(N6, R6). **F3 고침**: 실제로 후보가 됐을 때만 묶음을 소비하고, 실패하면 같은 묶음의 다음 출처를 시도. 경로 예산 8 |
| 9 | `AIBase.ItemTakeCandidates` (씨앗) | 후보 1개 | 원래 하나. **고침**: 경로 예산 8 |
| 10 | `AIBase.ReportFields` 최상위 `chests` | 상위 보고 | 이미 놓은 상자는 상세, 나머지는 구역 요약(N2, R4) |
| 11 | `AIInventory.ReportJson` `chest_ids` | 상위 보고 | 이미 `{id, desc}`, 많으면 구역(R1) |
| 12 | `AIPlaces` 장소 목록 | 상위 보고 | 이미 놓지 않은 상자는 구역마다 가장 가까운 한 줄(N2) |
| 13 | `AIBase.StallNote`·`PromptLine`·`/ai base` 상태 | 직접 놓은 상자(`BaseChests`)만 나열 | 놓은 상자는 수가 작다. 선택지가 아님(유지) |
| 14 | `AIBase.ChestItem`·상자 놓기 까닭 | `BaseChests` 존재 확인 | 기지 상자 제작 판단(HEAD 결정 유지) |
| 15 | `AIInventory.NearbyChests` (근처 빠른 쌓기) | 10칸 안 최대 20개 | 게임 규칙 근사(유지) |
| 16 | `AIFarm` seeds_in_chests | 합계 | 선택지 아님 |
| 17 | `AIInventory.KnownChestWhere`·`CheckGoal`·`ChestWhere`·`ExactCandidate`·`MenuVisibleChest`·`AIPlaces` 존재 확인 | 찾기·검사 | 선택지 생산 아님 |
| 18 | `AIBase.Observe`(기억 갱신), `AIPlace`(놓은 상자 확인) | 관측 | 선택지 아님 |

### 발견별 처리
- **F1** — 고침. 위 #2. 시험: 상자가 한 구역에 몰린 경우와 퍼진 경우 각각에서 `store slot=12 where=<구역>`이 열린 상자로의 정확 보관을 낸다.
- **F2** — 고침. 위 #1. 시험 결과(상자 300개):
  - 몰린 경우: 구역 꺼내기 16, where 생략 꺼내기 4, 보관 4
  - 퍼진 경우: 1 / 19 / 19
  - 모두 40 이하다. 이전에는 300·360개였다(review-4 탐침).
  - 구역 안의 실제 상자를 열면 실제 꺼내기가 나오고, 생활 트리는 두 경우 모두 300개 전부에 닿는다(가장 넓은 단계 16·36).
- **F3** — 고침. 위 #8.
- **F4** — 고침
  - 장비에 aux 신원이 있으면 레벨(variation)과 관계없이 aux로 센다(`HeldCount`).
  - 도착을 못 본 경우 문구에 "받은 직후 관측 전에 몸에서 나갔을 수 있음"을 더해, 남이 꺼냈다고 단정하지 않는다.
  - 관측 시점 자체는 판단 구성 때라 그대로다.
- **F5** — 고침. `AILife.Matches`가 `base:area:` 목표 아래의 작은 구역과 상자를 진척으로 묶는다(상자는 기지 범위일 때만).

### 시험 결과 (최종 a523367)
- 오프라인 시험:
  - life-inventory 242/0 (몰린·퍼진 창고 각 5개 추가)
  - maintenance 157/0, life-craft 49/0, life-cook 70/0, life 129/0
  - execution 181/181, restriction ALL PASS 47 (재현 stub에 예산 상수를 더함)
  - gather 61, travel 51, path-preparation 25, action-input 43, info 모두 통과
- gates.sh `--unity`: 실패 0
  - compile-check 오류 0, core-tests 1571, exp-sim OK
  - Unity 컴파일 exit 0, error CS 0 (`ef0abda` 뒤와 `a523367` 뒤 두 번)
- 시험하지 못한 것
  - 전체 후보 조립(`CandBuild.AssembleSteps`/`BuildJsonSteps`)을 거친 총 개수는 오프라인 harness에 그 조립 경로가 링크돼 있지 않아 재지 못했다. 이번 시험은 생산자별 개수(목표 경로, 생활 트리 단계)로 확인했다.
  - 자유 시간 평면 보관(`AIBase.Candidates`, #5)은 production `AIBase`를 링크하는 harness가 없다. 코드상 트리 첫 단계 구역 수(40 이하) 이하이고, compile-check와 Unity 컴파일만 거쳤다.

## 5차 (재검수 [review-5.md](review-5.md)와 사용자 결정 '상자 열림 모드' 반영, a523367 위)

최종 커밋 `1e3ac43`. 실게임, 설치, 공식 병합은 하지 않았다. G3은 지시대로 고치지 않고 측정만 했다(사용자에게 올림). L8은 손대지 않았다.

### 상자 열림 모드 (사용자 결정, agreement.md '상자 열림 모드' 절 추가)
- 판단 요청을 만드는 맨 앞(`AIBrain.BuildCandidateSteps`)에서 `AIChestMode.Active`가 참이면, 그 요청의 선택지는 `AIInventoryMenu.ChestModeCandidates`만 쓴다. 벽·탐험·생활 트리·다른 상자 걷기 등 다른 생산자는 돌지 않는다.
- 메뉴는 연 상자에 뿌리를 둔다(`chestroot`). 단계는 다음과 같다.
  - 상자 안 물건 꺼내기: 분류 → 칸 → 조작은 take만 → 목적 칸·수량
  - 내 가방·파우치·착용 물건 넣기: 분류 → 칸 → 조작은 store만 → 이 상자의 칸·수량. 다른 상자 목록은 거치지 않는다.
  - 이 상자 빠른 쌓기·정렬
  - 상자 닫기(`chest-close`): 열린 상태와 메뉴를 지우고 원래 선택면으로 돌아간다. 모든 단계에 붙는다.
- 모드가 갇히지 않게: 상자가 없어졌을 때, 보이지 않을 때, 2칸 밖으로 멀어졌을 때 `AIInventory.ValidOpenChest`가 열린 상태를 풀고, 그 상자에 뿌리를 둔 메뉴도 닫는다.
- 전투 시작 신호(fixes용 기록): **기존 피격 신호만** 쓴다.
  - 모드 동안의 최고 체력에서 `AIVitals.LostSince(최고, 지금)`가 판단 호출의 기존 기준 `Settings().hpDropTrigger`에 닿으면 전투 시작으로 본다. `AIBrain.Loop` 행동 중 감시의 "체력 감소" 호출과 같은 기준이다.
  - 그러면 상자를 닫고 모드를 끝낸다. 같은 판단부터 일반 선택면(전투 후보 포함)이 나온다.
  - '적 인식'(`_inCombat` = 올 수 있는 적이 전투 거리 안)만으로는 풀지 않는다. 도주 가능성 판단은 만들지 않았다(합의 '전투 상태 진입 기준').
  - 판정은 `AIChestMode.FightStarted` 한 곳에 있어, 나중에 전투 모드 진입점이 그 자리를 대신한다.
- 시험(`maintenance-tests` `ChestModeCases`, 10개):
  - 열면 꺼내기·넣기·빠른 쌓기·뒤로·취소·닫기만 나온다.
  - 상자 물건 조작은 take 하나, 내 물건 조작은 store 하나다.
  - 닫으면 모드와 메뉴가 끝난다.
  - 작은 체력 감소로는 유지되고, 피격하면 끝나며 상자가 닫힌다.
  - 상자가 파괴되거나 멀어지면 풀린다.
  - 생산 `AIBrain` 조립 경로 자체는 harness에 없다. 모드 판정과 메뉴 선택면을 시험했다.

### 발견별 처리
- **G1** — 고침
  - 구역 대표의 순위를 기억 내용으로 정한다(`ChestCandidates` `Use`).
    - 꺼내기: 그 물건이 있다고 기억한 상자
    - 보관: 빈 칸이 있거나 같은 물건이 든 상자
    - 그다음 안 열어 본 상자, 마지막이 '열어 보니 쓸모없음'
  - 열린 상자는 쓸모없음이 아닐 때만 맨 앞이다.
  - 시험(몰린·퍼진 창고 각각):
    - 열기 전에는 살아 있는 상자가 대표다.
    - 가득 찬 것을 본 뒤에는 그 상자가 대표에서 빠지고, 같은 작은 구역의 다른 상자가 나온다.
  - 상자 열림 모드에서는 닫기 → 다음 판단의 대표로 이어진다.
  - 시험 중 발견해 고친 것: life-inventory 시험 stub의 걷기 후보 종류가 생산 코드와 달랐다(`walk` 대 생산 `dig`). 그래서 slot+구역 경로의 걷기가 시험에서 걸러지고 있었다. 생산과 같게 맞췄고, F1·F2 시험도 그대로 통과한다.
- **G2** — 고침
  - `TakeOrGo`가 `noRoute`를 돌려주고, 경로 없음만 예산에 센다.
  - 이미 후보로 나온 상자의 다른 음식은 그 걷기 문구에 "이 상자에 X도 있었음"으로 합친다(성공으로 침).
  - 예산을 다 쓴 뒤에도 화면 안 출처(경로 탐색이 필요 없음)는 계속 낸다. 재료·씨앗 반복도 같다.
  - 시험(restriction harness, `TakeOrGo`를 생산 코드에서 그대로 추출):
    - 화면 밖 상자 하나에 음식 9종 + 화면 안 출처: 걷기 1개에 다른 음식 이름이 합쳐지고, 화면 안 출처가 나온다.
    - 그 상자로 걷는 중이어도 화면 안 출처가 나온다.
    - 경로 없음 9회 뒤에도 화면 안 출처가 남는다.
  - 기존 restriction 시험 "세 출처 보존"은 지금까지 stub 기준(출처마다 `inventory` 후보)이었다. 생산 동작으로 바꿨다: 걷기 2개, 상자 (1,0)의 두 번째 음식은 그 걷기 문구에 합침.
- **G3** — 측정만(사용자 결정 대기)
  - 실제 `AICandidateAssembler.Select`+`Finish`에 상자 쪽 최악을 넣었다: 평면 보관 41 + 음식 40. 생활 단계 43은 `Compose`가 더하는 수라 더해 계산했다.
  - 상자 쪽만으로 **125**다.
  - 벽 후보를 더하면: 벽 120 → 245, 벽 130 → 255(한도 255에 닿음).
  - 시험이 이 수치를 MEASURE 줄로 남긴다.
- **G4** — 사실에 맞춤
  - 판단 조립(`AIBrain.Candidates`)은 모든 걷기 후보의 경로를 판단 전에 준비한다. 그래서 '길은 고른 뒤 찾음' 문구를 지웠다.
  - 경로가 없는 구역 대표 걷기는 조립 단계에서 빼고(`AIInventory.IsAreaRepresentativeWalk`), 그 구역 상자는 생활 트리로 고른다.
  - 대표 문구는 "구역의 대표(기억 내용상 쓸모 있어 보이는 것 먼저, 그다음 가까운 것)"로 바꿨다.
- **G5** — 시험 추가
  - F4: aux가 같은 장비가 다른 레벨로 돌아와도 자기 도착으로 센다.
  - F5: `base:area:` 목표 아래에서 기지 맥락의 상자와 작은 구역은 진척이고, 기지 밖 상자는 별도 행동이다.
- **G6** — 고침. inspect 문구의 마침표 중복.

### 시험 결과 (최종 1e3ac43)
- 오프라인 시험:
  - life-inventory 253/0, maintenance 168/0, restriction ALL PASS 54
  - life-craft 49/0, life-cook 70/0, life 129/0, execution 181/181
  - gather 61, travel 51, path-preparation 25, action-input 43, info 모두 통과
- gates.sh `--unity --tag gap-fixes5`: 실패 0
  - compile-check 오류 0, core-tests 1571, exp-sim OK
  - Unity 컴파일 exit 0, error CS 0

## 공식 병합 (공식 ai-character 61aa67b → gap-integrated-20261108)

- 시작 직전에 `git -C C:\Dev\CoreKeeperModSDK rev-parse ai-character`로 확인한 판은 `61aa67b`이고, 우리 쪽보다 25커밋 앞서 있었다.
  - 이동전달 1/4~4/4
  - 호출 간격·이동 중 호출 계기
  - 자유 시간 제거 0c8a3e8
  - 토큰 절감 1~4
  - 지역·출구 이동 91445e2
  - /ai hold 자체 경로 정지 092c692
  - 최종 검수 수정
- 병합 커밋은 `8ba7c1a`다. 실게임, 설치, ai-character 이동, push는 하지 않았다.

### 충돌과 의미 조정
| 곳 | 내용 | 처리 |
|---|---|---|
| `AIBase.cs` (글자 충돌) | 공식은 자유 시간을 지우며 `FreeStoreCandidates`를 삭제. 우리는 그 함수를 공용 상자로 넓혔음(R5) | **공식을 따라 삭제**. 함수 아래에 둔 공용 도우미(`RouteTriesPerDecision`, `InViewChest`, `NearestFirst`)는 보관 목표·재료·음식 경로가 쓰므로 유지 |
| 자유 시간 의도 | "목표 없을 때 가방 정리" | 사용자 결정(목표가 없으면 문제 발생을 기다림)으로 사라진 의도라 다른 경로로 옮기지 않음. 상자 보관은 보관 목표의 평면 후보(구역 대표, N1)와 생활 트리 보관 단계로 그대로 할 수 있음 |
| 토큰 절감 ① (`AIExpedition.PromptReport`) | 현장 관측이 직전 선택지(`LastChoices`)를 가리킴. 상자 열림 모드는 `AIExpedition.Compose`를 거치지 않으므로, 이전 판단의 선택지를 가리키게 됨 | `AIExpedition.NoteChoices`를 더하고, 상자 열림 모드 선택면을 직전 선택지로 기록 |
| 토큰 절감 ②~④ (gather 묶음 `resources`, 원정 기록, 결과 이력) | 우리 상자 선택지·문구·보고와 겹치는 형식 변경 없음 | 조정 없음. `chests`/`chest_ids`/구역 요약은 상위 보고(변경 없음)에 그대로 있음 |
| 가방 메뉴 (자동 병합) | 공식 4/4: 목표 품목 표시와 완료 거절 문구. 우리: 파우치·장비 칸·빠른 쌓기·상자 모드(`chestroot`)·상자 구역 | 양쪽 보존. inventory-menu-tests(공식 새 시험) 6/0 |
| /ai hold (092c692) | 공식이 자기 호출 경로를 막음 | 우리 변경에는 새 모델 호출 경로가 없음. 정비 포기 알림은 `AIPlanner.RequestReplan`→`Report`(hold 막힘)를 거침. 상자 열림 모드 판정과 닫기는 판단 조립 안에서만 돌고, 그 조립은 hold 동안 멈춤 |
| 시험 harness | inventory-menu-tests가 `AIInventory`를 링크해 `AIChestAreas`가 필요해짐. restriction 추출이 자유 시간 주석을 끝 표식으로 쓰고 있었음 | csproj에 `AIChestAreas.cs` 링크. 추출 끝 표식을 follow 블록 자신의 `yield return 0`으로 바꿈 |

### 뺀 것
- `AIBase.FreeStoreCandidates`(자유 시간 보관). 위 표의 이유대로다.

### 시험 (병합 뒤, 생성 스크립트 경로로 실행)
- 생성 스크립트(run.ps1)로 돌린 harness:
  - execution 217/217
  - life 129/0, maintenance 168/0
  - gather 61, inventory-menu 6/0
- 그 밖의 harness:
  - life-inventory 253/0, life-craft 49/0, life-cook 70/0
  - travel 105/105, path-preparation 25, action-input 43, info
  - restriction ALL PASS 54 (G3 측정 125/245/255 그대로)
- gates.sh `--unity --tag official-merge`: 실패 0
  - compile-check 오류 0, core-tests 1581, protocol 190
  - Unity 컴파일 exit 0, error CS 0
- 정정
  - 이전 회차의 life-tests·maintenance-tests·execution-tests 수치는 run.ps1을 거치지 않은 `dotnet run`이어서, 생산 코드를 그대로 뽑아 넣는 `.generated` 추출본이 갱신되지 않은 채로 돌았을 수 있다.
  - 이번 병합 뒤 수치는 모두 run.ps1(추출 재생성)로 돌린 것이다.
  - 그 과정에서 공식 판 자체의 시험 stub 문제(`RecentResults`, `AICandidate.resources`)는 추출 재생성으로 해소됨을 확인했다.

## 병합 검수 ([review-merge.md](review-merge.md) 반영, 8ba7c1a 위)

최종 커밋 `49bd748`. 실게임, 설치, ai-character 이동, push는 하지 않았다.

### 항목별 처리
- **G-1 (중요)** — 고침
  - 공식 대기 문 `AIRunGate.IdleHold`에 넘기는 '열린 선택 화면' 판정을 한 곳 `AIBrain.ChoiceViewOpen`으로 모았다. 판정은 `AIInventoryMenu.Active || AILife.Cursor.Open || AIExpedition.Active || AIChestMode.Open`이다.
  - `AIChestMode.Open`은 실제 열린 상자(`AIInventory.ChestIsOpen`, 읽기 전용)다. 그래서 상자 모드에서 마지막 조작으로 메뉴 경로가 비어도, 상자가 열려 있는 한 목표 없는 상태에서도 닫기까지 판단이 이어진다.
  - 상위가 hold나 무목표를 답해 목표가 없어진 경우도 같은 문을 지나므로 같다.
  - 시험(maintenance `ChestModeCases`, 3개):
    - 메뉴 경로가 빈 뒤에도 문이 호출을 막지 않는다.
    - 상자 판정을 빼면 공식 문만으로는 막혔을 것이다.
    - 다음 판단은 다시 상자 선택면(닫기 포함)이다.
  - core-tests의 소스 형태 검사(`ChoiceFactsTests`)는 새 판정 형태로 고쳤다.
- **L-1** — 고침. 목표가 없고 상자가 열려 있으면 '자기 방어 중' 문구 대신 "목표 없음: 열린 상자 안의 일을 고르거나 상자를 닫는다(닫으면 원래 선택면)."를 싣는다.
- **L-2** — 고침. 상자 모드가 `AIExpedition.NoteChoices`로 직전 선택지를 기록하면 `FieldPaused`가 켜진다. 행동 모델용 간이 현장 관측은 "상자 열림 중이라 현장 자원 목록은 싣지 않음"으로 바뀌고, 다음 일반 조립(`Compose`)에서 꺼진다. 상위 LLM 보고(전체판)는 그대로다.
- **L-3** — 고침. 메뉴 경로 문구(`PathText`)가 상자 모드에서 "열린 상자 chest:x,y(상자 열림 모드) → 상자 안 물건 꺼내기/내 물건 넣기 → …"로 나온다. 공식 4/4 완료 거절 안내도 이 문구를 쓴다.
- **L-4** — 고침. 시험 고정값의 `GoalKind.Free`를 `GoalKind.None`으로 바꿨다(life-craft 8곳, life Bed·Farm 3곳, restriction G3 측정 입력 1곳). 모두 통과한다.
- **L-5** — 고치지 않음(검수 사실 확인).
  - `AIBase.Candidates`의 `asGoal`은 살아 있다. `AIInventory.ChestCandidates`가 명시 기지에 아직 도착하지 않았을 때 `new AIGoal { kind = GoalKind.Home }`으로 부른다(AIInventory.cs 기지 미도착 분기).
  - 재료 분기도 그 호출에서 쓰인다. 주석으로 밝혔다.
- **L-6** — 고침
  - `AreaRepWalks`를 `AIInventory`의 캐릭터별 상태(`State.areaRepWalks`)로 옮겼다.
  - `AIChestMode.Reset`을 캐릭터 초기화 경로(`AIBody.Reset`, `AIInventory.Reset` 다음)에 연결했다.
  - `LastEnded`는 시험과 로그·notice가 읽는다(유지).
- **L-7** — 고침. brief 42줄: "seeds_in_chests 기억한 상자(모두 공용)에 마지막으로 열었을 때 든 씨앗".

### 시험 (정규 생성 스크립트 경로)
- run.ps1:
  - execution 217/217, life 129/0
  - maintenance 171/0 (G-1 3개 추가)
  - gather 61, inventory-menu 6/0
- Extract*.ps1 + 실행:
  - life-inventory 253/0, life-craft 49/0
  - 재추출본은 커밋본과 같다(줄 끝 차이뿐이라 되돌림).
- prepare.mjs + 실행: restriction ALL PASS 54
- run.mjs: action-input PASS 43
- 그 밖: life-cook 70/0, travel 105/105, path-preparation 25, info 통과
- gates.sh `--unity --tag merge-review`: 시험 형태 검사 수정 뒤 실패 0
  - compile-check 오류 0, core-tests 1581, protocol 190
  - Unity 컴파일 exit 0, error CS 0
- 병합과 무관한 기존 실패(검수가 확인): travel navigator 컴파일, candidate-progress 8/11. 두 부모에서도 같다.
