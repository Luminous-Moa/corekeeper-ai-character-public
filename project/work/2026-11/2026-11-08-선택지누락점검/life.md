# 생활 범위 선택지 누락 점검

2026-11-08. 읽기 전용 점검이다. 기준은 공식 `C:/Dev/CoreKeeperModSDK` ai-character `20b1ace`의 생산 코드다(아래 `파일:줄`은 `Assets/AIPlayerSpike/Scripts/` 기준). 합의·설계 문서와 프로젝트 조회(`work/지식그래프/tools/product.mjs show`), 게임 데이터 그래프(`Tools/knowledge/kg.mjs station`), 로컬 디컴파일(`C:/Dev/_ref/corekeeper-decomp`)을 대조했다. 코드·문서·그래프는 수정하지 않았고 게임·모델도 실행하지 않았다.

범위: 시설을 이용하는 기지 생활(제작·요리·농사·보관/꺼내기·제련 등). 장비 정비(수리·레벨 업그레이드·회수), 가방·장비 직접 조작, 전투, 채집·이동·귀환은 제외하고 겹치는 곳은 "경계"로 남겼다.

## 1. 현재 제공되는 선택지

### 진입과 조립

- 생활 트리는 닫혀 있으면 기존 후보 끝에 `life_open` 하나만 붙는다(`AILife.cs:155`). 열리면 기존 후보 전체를 유지한 채 현재 깊이의 항목 전부, `back/root/close`를 붙인다(`AILife.cs:156-190`). 5개 페이지 제한은 제약 해제로 없어졌다(`AILife.cs:171`, 제약해제 coverage C01).
- 조립 호출은 `AIBrain.Candidates.cs:1211`(`AssembleLifePage`→`AILife.Compose`)이다. 상위 목표와 완전히 일치하는 직접 실행 후보 하나를 트리와 별도로 보존한다(`AIBrain.Candidates.cs:1200`, `AILife.cs:158-161`).
- 상위 계획의 생활 목표 형식: farm mode `hoe/plant/water/fill/harvest`, cook mode `put/fill/take/wait`, craft는 mode 없음(where `hand`/`station:`), store/take는 기존 가방 계약, maintain은 정비 규칙(`Core/AILifeTree.cs:116-149`).
- 목표 경로(트리 밖)의 후보: 농사 `AIFarm.Candidates`, 요리 `AICook.Candidates`, 정비 직접 후보, 보관 정확 슬롯 후보(`AIGoalFeatures.cs:8-43`). 실행 kind 연결은 `AIActions.cs:32,45,48-50`(craft/inventory/farm/cook/cookwait).

### 루트 갈래와 말단(`AILife.cs:126-131`)

| 갈래 | 단계 | 말단 행동 | 코드 |
|---|---|---|---|
| 제작 craft | 품목(손 레시피 + 보이는 수동 제작대 레시피 + 기억한 제작대의 prefab 레시피) → 장소(hand / 보이는 제작대 / 기억한 제작대 "가서 확인") → 실행 | `craft`: 정상 Craft 요청, 회수 = ceil(잔량/1회 출력), 커서 잔류·main 가방 수령 구분 | `AICraft.cs:51-87`, `:89-124`, `:127-189`, `CraftSkill :638-737` |
| 요리 cook | mode(put/fill/take/wait) → put: 입력0 재료 → 입력1 재료(이때만 예측) / fill: 넣을 재료 / take: 출력 종류(전체 포함) → 자기 솥(보임/기억 "가서 확인") → 실행 | `cook` 투입(1회 최대 5짝)·수령, `cookwait` 대기 | `AICook.cs:544-604`, `:605-672` |
| 농사 farm | mode(hoe/plant/water/fill/harvest) → plant: 확인된 종자 / water·harvest: 작물 종류(전체 포함) → 영역(자기밭 전체·연결구역·특정 칸·기억한 칸/물) → 실행 | `farm`: 1회 실행 최대 6칸, 실제 바뀐 칸만 확인량 | `AIFarm.cs:639-679`, `:680-730`, `Eligible :607-625` |
| 보관/꺼내기 inventory | store/take → 자기 상자(base 기준점 포함) → 미관측이면 다가가 열기만 → 출발 슬롯 → 실행 | `inventory` 정확 슬롯 이동 | `AIInventory.cs:241-307`, `:308-328` |
| 장비 정비 maintenance | repair/upgrade/recover(다른 점검자 범위) | — | `AIMaintenance.cs:242-252` |

수량: 현재 목표와 일치하는 말단은 목표 잔량을 쓰고, 일치하지 않는 말단(자유 시간 포함)은 "원래 단계 밖의 별도 행동"으로 1회/1칸/1개만 제시한다(`AILife.cs:78-84,181`). 명세 §6에 따른 설계다.

## 2. 빠진 선택지

중요도 표기: **막음** = 실제 플레이 흐름이 그 지점에서 진행되지 않음, **좋음** = 있으면 좋음(우회 가능하거나 흐름 밖).

### M1. 가축 기르기(먹이 주기·생산물 수령·번식·우리/목줄/안장)

- 빠진 것: 생활 트리와 목표 경로 어디에도 가축 관련 후보가 없다. `Scripts/` 전체에 Cattle·feed 처리 코드가 없다.
- 게임 근거: 프로젝트 조회 `R.game-topic.animals`(라벨 feed, feed_matching_food, cage_cattle, leash_cattle, saddle_cattle, open_cattle_window, breed_downstream_child_request). `show game:animal-feed`는 `accepted_by acceptance:B-animal175`다. 게임 데이터 그래프의 `crafted_at`에서 음매린(Cow)→초원 우유, 도도(Dodo)→도도 알이 확인된다(`kg.mjs station Cow`, `station Dodo`). 디컴파일 `Pug.Base/CraftingType.cs`에 `Cattle` 제작 종류가 있고 `InventoryUtility.cs:1810`에 그 분기가 있다. 생산물이 바닥에 떨어지는지 가축 창으로 받는지 같은 정확한 수령 경로는 확인하지 못했다(추정).
- 합의: 시설생활 계획(02-plan)의 여섯 묶음과 후속 방향(next/01-direction)에 가축이 없다. 제외 기록도 미룸 기록도 없다.
- 코드 확인: 가축 엔티티는 `AIBrain.Candidates.cs:459`에서 CanCraftObjectsBuffer가 있다는 이유로 부수기 대상에서 빠진다. 생활 갈래 화이트리스트(`Core/AILifeTree.cs:40`)에도 해당 갈래가 없다.
- 중요도: **좋음**. 우유·알 같은 요리 재료 공급 루프를 넓히지만 지금의 흐름을 막지는 않는다.

### M2. 침대에서 자기(claim_and_sleep)

- 빠진 것: 침대를 차지하고 잠드는 선택지가 없다. 코드는 `PlayerSleepStateCD`를 읽기만 한다(`AIBody.cs:141-151`).
- 게임 근거: `R.game-topic.food`(섭취·휴식) 라벨 claim_and_sleep, sit. `show game:bed-sleep`(Bed 4760 variation 0, `accepted_by acceptance:B-world-tools122`). 수면이 주는 실제 효과(버프 등)는 확인하지 못했다(추정).
- 합의: 생활 계획·트리 합의에 언급이 없다. 제외 기록도 없다.
- 중요도: **좋음**.

### M3. 요리 솥 입력 정리: 입력0만 채우기, 조리 중 보충, 남은/잘못된 재료 빼기

- 빠진 것: put은 두 입력이 모두 비어 있을 때만, fill은 "입력0 있음 + 입력1 빔"일 때만 나온다(`AICook.cs:641`, 상태 정의 `:248-249`). 입력0이 비고 입력1만 남은 비대칭 상태, 짝이 맞지 않는 재료나 상대방이 넣은 재료가 남은 상태를 풀 선택지(입력 빼기/입력0 채우기)가 없다. 조리 중 같은 재료를 더 쌓는 보충도 없다.
- 게임 근거: 솥 입력은 일반 인벤토리 이동으로 넣고 뺀다(`AICook.cs:11-16` 디컴파일 요약, `R.game-topic.cook`). 슬롯 이동 자체는 보관 갈래와 같은 정상 InventoryChange 경로다.
- 합의: 02-plan 첫 단위 요리 흐름은 "한쪽만 들어가면 그 상태를 전달"까지만 요구한다. 03-spec §7.2는 "일부만 들어갔으면 빈 입력을 보고하고 다음 fill 선택으로 이어진다"고 했다. 입력0 쪽 빈칸과 회수는 다루지 않았다.
- 중요도: 비대칭 잔여가 생긴 솥에서는 put/fill이 둘 다 막힌다(그 솥 한정 **막음**). 보충은 **좋음**(5짝씩 비울 때마다 재투입으로 우회 가능).

### M4. 제작 재료를 근처 자기 상자에서 바로 쓰기

- 빠진 것: 제작 가능 판정과 비용 확보를 main 가방만으로 계산한다(`AICraft.cs:172`). 게임은 제작대 근처 상자의 재료도 사용하지만, 해당 선택지는 "재료 부족"으로 막힌다.
- 게임 근거: `AICraft.cs:5-6` 디컴파일 요약(InventoryUpdateSystem이 플레이어 인벤토리와 제작대 근처 상자를 함께 사용), next/01-direction 71행(ProcessCraftingJob이 근처 자동이체 상자를 거리순으로 더함).
- 합의: 수리 방향에서는 "자기 가방에 먼저 준비, 새 공유상자 정책 불허"로 정했다. 일반 제작은 명시 결정이 없다. 자기 상자에 한정할지 권한 정책이 필요하므로 **경계** 성격이다.
- 중요도: **좋음**. 보관 갈래의 take로 먼저 꺼내면 우회할 수 있다.

### M5. 농사 물주기 대상 제한(작물 없는 판 땅 미리 물주기)

- 빠진 것: water 후보는 "자라는 중이고 물이 없는 칸"만 대상으로 하고 작물이 있어야 한다(`AIFarm.cs:613,623`, Snap.dry 정의 `:466-470`). 빈 판 땅을 먼저 적셔 두는 선택지가 없다.
- 게임 근거: 물뿌리개를 판 땅에 쓰면 wateredGround가 된다(`AIFarm.cs:15-16` 디컴파일 요약, `R.game-topic.farm` water_ground).
- 중요도: **좋음**. 심은 뒤 물을 주는 순서로 우회할 수 있다.

### M6. 괭이 없이 수확(트리 말단)

- 빠진 것: harvest 말단은 부서지지 않은 괭이가 있어야만 나온다(`AIFarm.cs:704`).
- 게임 근거: 공격 경로에서도 `PlayerController.OnHarvest`가 호출된다(디컴파일 `Pug.Other/PlayerController.cs:5922`). 현재 코드는 다 자란 자기 작물을 일반 부수기 후보로는 남긴다(`AIBrain.Candidates.cs:465`). 다만 그 경로는 농사 목표의 확인량에 들어가지 않는다.
- 중요도: **좋음**. 준비 경로가 괭이 제작을 유도한다(`AIFarm.cs:498`).

### M7. 범위 농사 도구(씨앗 뿌리개 seeder, 범위 물주기)

- 빠진 것: plant는 씨앗 자체를 칸마다 놓고, water는 칸마다 한 번씩 쓴다. 씨앗 뿌리개 도구를 고르는 선택지가 없다.
- 게임 근거: `show game:seeder`(정확한 seeder 도구 2종, 2×2/5×5 prefab, `accepted_by acceptance:B-world-tools122`). 큰 물뿌리개의 범위 효과는 확인하지 못했다(추정).
- 중요도: **좋음**(효율 문제).

## 3. 알려진 후속과 경계

### 알려진 후속(기록상 미룸)

| 항목 | 현재 코드 상태 | 기록 근거 |
|---|---|---|
| 자동 가공·제련(용광로·용광로 가마·유리 용광로 등 ProcessResources) | 제작 갈래가 자동 가공 시설을 "일반 제작 실행 범위 밖"으로 표시만 함(`AICraft.cs:49,72`) | todo.md 58행("남은 범위는 자동 가공·내구 강화·거래·낚시"), next/01-direction 표, 생활선택-트리구현 history 2026-11-07T06:44 "다음: 자동 가공·제련" |
| └ 같은 자동 가공 계열 중 이름이 따로 없는 것: 씨앗 추출기(Extract), 소각(Incinerate), 벌레 포획(CritterCatching), 알 부화기 | 같은 `IsProcessAutoCrafter`로 제외됨(디컴파일 `Pug.ECS.Components/CraftingCD.cs:22-29`) | 기록은 "자동 가공/제련"만 말한다. 씨앗 추출기(작물→씨앗)는 농사 순환과 직결되므로 자동 가공 단위 명세 때 범위에 넣을지 명시가 필요하다 |
| 거래(구매·판매) | 후보 없음 | todo.md 58행, next/01-direction 표·본문 |
| 낚시 | 후보 없음 | todo.md 58행, next/01-direction 표 |
| 내구 강화 | 정비 갈래에 repair/upgrade/recover만 있음(`AIMaintenance.cs:246-248`) | todo.md 58행. 정비 담당 점검자 범위 |
| 분해(salvage), 일반 시설 회수/재배치, 자동화 공사(농사팔·스프링클러·회로·어망) | 후보 없음 | 02-plan 37행("자동 농사팔·어망·회로 등 설비 운영은 … 별도의 단위"), 67행("분해·일반 시설 회수/재배치, 자동화 공사와 기지 배치 설계는 … 별도로 정한다") |

### 경계 항목

- **현장 수량 지정**: 현재 목표와 맞지 않는 생활 말단은 1단위만 실행하고, 더 많은 양은 request_plan으로 요청한다(`AILife.cs:82`, 명세 §6 83행). 합의된 설계이므로 빠짐으로 세지 않았다. 다만 자유 시간에는 모든 생활 행동이 1개/1칸/1짝이 된다는 점을 사용성 측면에서 기록해 둔다.
- **자기 시설만 사용**: 호스트·다른 AI의 솥·상자·밭은 쓰지 않는다(제약해제 coverage C18 유지). 빠짐이 아니다.
- **시설 설치(제작대·솥·상자 놓기)**: 생활 트리에는 없고 place/goto 목표와 요리 목표의 첫 솥 놓기로 제공된다(`AIGoalFeatures.cs:43`, `AICook.cs:718`). 기지 배치 범위와 겹친다.
- **먹기**: `AIFood.Candidates`(lLife)로 트리 밖에서 제공된다(`AIBrain.Candidates.cs:370`). 시설 행동이 아니다.
- **칠하기(PaintersTable·paint_entity), 지붕, 악기·거울 등 꾸미기/상호작용, 물 양동이 채우기/붓기, 펫**: 게임 근거는 있다(`R.game-topic.place`, `interact`, `farm` bucket, `animals` feed_pet_candy). 생활 합의에 언급이 없고 기지 꾸미기·자동화 성격이라 경계로 둔다.
- **장비 정비 갈래**: 다른 점검자 범위.

### 부수 관찰(누락은 아니지만 선택지 품질 문제)

- **기억한 자동 가공 시설이 제작 목록에 섞임**: 화면 안 제작대는 `Manual` 검사로 자동 가공 시설을 거르지만(`AICraft.cs:58,72`), 기억한 시설은 prefab 레시피를 검사 없이 품목 목록(`:59`)과 장소 목록(`:75-80`)에 넣는다. 그래서 기억한 용광로의 주괴나 기억한 솥의 수프가 "만들 품목"으로 나오고, "가서 확인"하러 간 뒤에야 실행할 수 없는 시설로 표시된다. 기억 대상은 `AICraft.StationsInView` 전체다(`AIMemory.cs:299-302`). 합의의 "이름만 있는 실행 선택지를 지원 능력으로 노출하지 않는다"와 부분적으로 어긋난다.
- **가축이 제작대로 잡힐 가능성(추정)**: `Cattle`은 `IsProcessAutoCrafter`에 포함되지 않아(`CraftingCD.cs:22-29`) `AICraft.Manual`이 참이 된다. 가축 엔티티에 실행 중에도 CanCraftObjectsBuffer가 있다면(게임 데이터 그래프의 crafted_at Cow/Dodo로 보아 그럴 가능성이 높다) 보이는 소가 우유 "제작대"로 목록에 나오고 위치도 기억된다. 실행은 태그 재료 레시피여서 `AICraft.cs:143`에서 거절될 것이다. 실제 실행 상태는 확인하지 못했다.

## 4. 확인하지 못한 것

- 실게임·실제 모델 실행. 모든 판단은 정적 코드와 그래프 근거에 따른 것이다.
- 가축 생산물의 정확한 수령 경로(바닥 드롭인지 가축 창인지)와 먹이 슬롯 조작 방식. 수면의 효과.
- 큰 물뿌리개의 범위 물주기 여부와 seeder의 실제 사용 조건(seeder 노드는 수용됐지만 실행 시험은 하지 않음).
- `CanCraftObjectsBuffer.entityAmountToConsume != 0`이라 제작 말단에서 빠지는 레시피 목록(`AICraft.cs:135`)과 식물 배양기(GreeneryPod) 등 특수 시설의 제작 종류.
- 생활 트리 입구가 255개 상한 때문에 실제 요청에서 열리지 못하던 문제는 path-optimization 감사(existing-tree-audit.md)의 범위라 이번에 다시 보지 않았다. 20b1ace에 반영된 연결의 실기 상태도 확인하지 않았다.
- 다른 worktree(`CoreKeeperModSDK-*`)에서 진행 중인 자동 가공 등 구현은 기준으로 삼지 않았다.
