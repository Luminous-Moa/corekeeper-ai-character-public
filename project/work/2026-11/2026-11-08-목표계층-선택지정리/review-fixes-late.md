# 늦은 검수 지적 고침 (2026-11-08)

- 근거 검수: `review-20261108-late.md`(판정 '고친 뒤 합격', 대상 42503a6a..4171d9b8)
- worktree: `C:\Dev\CoreKeeperModSDK-reviewfix`, 브랜치 `ai-review-fixes-20261108`(4171d9b8에서 시작)
- 원래 작업트리 `C:\Dev\CoreKeeperModSDK-celeris`는 건드리지 않았다.
- Unity 컴파일, 설치, 실게임은 하지 않았다.

## 커밋
| 커밋 | 내용 |
|---|---|
| fccf5450 | F3과 작은 것 5: 칠 거리에서 휘두르면 길 찾기 실패를 되돌림. '약 N타 남음'은 길이 확인됐을 때만 보임 |
| 1db95750 | 작은 것 1·10: 장비 추적 순서, gear_alert 중복 |
| 553d922b | 작은 것 3·4: craft 로그 키, 적 대상 읽기 LogError 빈도, 전투 상태 목록 |
| 5ebc516b | F1, F2, 작은 것 2·6·7·8·9 |

## 고친 것

### F1. 물러나기는 열린 길로 간다
- **방법:** 계획기에 '걷기 전용' 계획을 더했다(`AITunnel.Plan(..., walkOnly: true)`, `PlanBatch.BeginSinglePlan(..., walkOnly)`).
  - 열린 칸만 지난다. 벽·큰 뿌리·물체는 모두 못 지나가는 칸으로 본다.
  - 경계 규칙과 우회 확인은 쓰지 않는다. 벽을 열지 않으므로 상관이 없다.
  - 공유 트리 대신 국소 탐색 하나만 한다.
  - 길이 없을 때 이유는 "열린 아는 길 없음"이다.
- **이 방법을 고른 근거**
  - '벽 0개' 판정만 고치면 부수는 길이 더 싼 경우를 막지 못한다. 계획기가 단지를 부수는 짧은 길을 고르면 열린 우회로는 결과에 나오지 않는다.
  - 걷기만 하는 쪽이 같은 기준으로 계획을 받는 것이 맞다.
  - 그러면 예전부터 있던 경우도 함께 풀린다. 벽을 캐는 지름길이 우회보다 싸서 물러나기가 빠지던 경우다.
- **물러나기**
  - `RetreatSkill.NextLeg`: 부수는 계획에 부술 것이 있으면 걷기 전용 계획을 다시 구하고, 열린 길이 있으면 그 길로 걷는다.
  - 실패 문구는 `열린 길 없음(벽 1개·물체 1개)`처럼 종류별로 센다.
  - 물러나기 선택지(`Candidates.cs` 352): `OpenPlanC`(부수는 계획에 부술 것이 없으면 그대로 쓰고, 있으면 걷기 전용 계획)를 쓴다.
  - 열린 길이 없을 때만 부수는 계획으로 막힘 문구를 쓴다.
  - 실행 중인 물러나기의 '가는 쪽'(240)도 같은 길을 쓴다.
- **같은 판정을 쓰던 곳:** 308(적 쪽 파 들어가기), 655(떨어진 아이템 파 들어가기), 522(부술 물체 옆 파 들어가기)
  - 부수는 계획이 물체·뿌리를 부수는데 열린 아는 길도 있으면 파 들어가기를 내지 않는다(`BreaksBesideOpenWay`).
  - 부술 것이 없을 때(벽만 있음)는 바꾸기 전 계획과 같으므로 걷기 전용 계획을 따로 구하지 않는다. 비용과 동작이 전과 같다.
- **캐시:** 걷기 전용 계획은 PlanC 캐시에 따로 들어간다(`WalkOnlyKey`). `ForgetPlan`이 둘 다 지운다.

### F2. 트리가 열린 채 싸울 때 회복 먹기를 남긴다
- 싸움 묶음이 만드는 회복 먹기에 `combatHeal` 표시를 달았다.
- TreeOnly에는 이 먹기를 종류 `heal`로 넘긴다.
- 규칙(`AIFieldChoices.TreeOnly`)
  - 전투 중이면 `heal`을 남긴다.
  - 배고픔 위험 구간(<25)이면 `eat`와 `heal`을 모두 남긴다.
  - 전투 밖이고 배고픔이 25 이상이면 둘 다 뺀다. 배고픔 먹기 규칙은 그대로다.
- 전투 여부는 기존처럼 빌드의 `_inCombat`(실제 위협 판정)이다.
- TREE 시험은 '전투 중 먹기가 빠짐'을 기대하고 있었다. 이를 다음으로 바꿨다.
  - 전투 중: 회복 먹기가 남고, 배고픔 60의 일반 먹기는 빠진다.
  - 전투 밖: 회복 먹기도 빠진다.
  - 배선 확인을 더했다.

### F3. 공격 계속하기 문구
- `AttackSkill.Tick`의 휘두르기 분기(1.3칸 안)에서 `_pathFails = 0`, `_pathFailSince = -1`로 되돌린다.
- 이제 붙어서 치는 중이면 '약 N타 남음'이 돌아온다.

### 작은 것
| # | 지적 | 고친 방법 |
|---|---|---|
| 2 | `BRAIN: preparation:`이 같은 이유로 반복됨 | 문구가 바뀔 때만 찍는다. 캐릭터별 `_prepLogged`를 둔다 |
| 3 | craft 로그 키에 개수가 들어감 | 키에서 이유의 숫자를 `#`으로 바꾸고 캐릭터 이름을 넣었다 |
| 4 | 적 대상 읽기 실패 LogError에 빈도 제한이 없음 | 30초에 한 줄만 찍고, 그동안 생략한 수를 함께 적는다 |
| 6 | 여러 칸 물체의 비용을 두 번 셈 | 물체 칸의 비용을 `1 + ceil(2×타격/짧은 변)`으로 나눴다(`AIPathBreak.ObjectTileCost`). 2×2를 지나면 약 한 번 부수는 값이다. 길 목록에도 같은 물체는 한 번만 넣어 타격 수를 한 번만 센다 |
| 7 | 부수기 타격 수가 실제 도구와 다름 | 물체는 BreakSkill이 휘두르는 근접 무기의 피해(`PlannedBreakDamage`: 쓸 수 있는 무기의 게임 자료 피해, 없으면 캐기 피해)로 센다. 벽·큰 뿌리는 곡괭이 캐기 피해 그대로다. 검증 단계도 이 값의 변화를 본다 |
| 8 | 걷기 선택지에서 뿌리를 '벽'이라 함 | `TunnelPlan.BlockSummary()`를 만들었다. 예: "부술 벽 2개(DirtWall 2)·뿌리 1개(BigRoot 1)·물체 1개(Pot 1)". 기지·장소·상자·제작대·밭·탐험·파 들어가기 문구가 모두 이것을 쓴다 |
| 10 | 장비 경고가 gear_alert와 events에 두 번 들어감 | events에는 넣지 않는다. 보고 전에 경고가 두 번 오면 gear_alert에 둘 다 이어 적는다 |
| 1 | 같은 종류 장비가 사라지는 순간 gear_worn을 놓침 | 같은 칸·같은 id·변형이고 조금 줄었으면(5 이하) 같은 장비로 먼저 잡는다. 그다음이 다른 칸의 같은 수량이다 |
| 5 | 아는 길 없는 적을 고른 직후 '약 N타 남음'이 보임 | 걷기가 실제로 움직였거나 휘둘렀을 때만 보인다. 길 찾기에 실패하면 다시 숨긴다. 원거리 무기는 보이기만 하면 전처럼 보인다 |
| 4b | 전투 상태 목록에 TookDamage·보스 상태가 없음 | 넣었다(아래 '영향 판단') |

- **작은 것 4b 영향 판단**
  - TookDamage: 내가 친 적이 움찔하는 짧은 순간에는 '나를 노림'이 거짓이 됐다. 대개 다른 조건(아는 길, 칠 거리)이 받쳐 주지만, 벽 너머에서 쏘거나 날아오는 적은 그 틈에 전투가 끊길 수 있다.
  - 보스: 공격·이동 상태(SlimeBossJump, ScarabBossCharge, SnakeBossMove 등)에서는 판정이 빠졌다.
  - 둘 다 대상 필드(ChaseStateCD.targetEntity 등)가 이 AI여야만 참이 된다. 그래서 넣어도 잘못 켜지는 위험은 작다고 봤다.
  - 넣지 않은 상태: Stunned, Sleep, ScarabBossBuried 등 묻힘·소환 등장 상태, Vulnerable, PhaseTransition.

### 확인만 한 것: 심은 작물
- 확인 결과
  - 부수기 선택지 쪽은 원래 자기 밭의 덜 자란 작물과 남의 작물을 뺀다(`Candidates.cs` 484-488).
  - 이 코드는 작물이 `DestructibleObjectCD`·체력을 가질 수 있다고 전제한다.
  - 길 계획 쪽 `AIObjects`의 자연물 판정에는 작물 검사가 없었다. 충돌체가 있고 PlaceablePrefab이 아닌 작물은 부술 대상이 될 수 있었다.
- 막은 방법: `PlantCD`나 `GrowingCD`가 있으면 길에서 부수지 않는다(`AIPathBreak.NaturalBreakable(..., plant)`).
  - 야생 식물도 지나가지 않는다. 보수적으로 둔 것이다.

## 고치지 않은 것
- F4(기억한 용광로 경로): 제련 작업자가 맡았다.
- 트리가 열린 채 비전투 적이 보이면 선제공격 선택지가 빠지는 것: 사용자 기준대로 그대로 두었다.

## 시험
| 시험 | 결과 |
|---|---|
| gates(--unity 없이) | 실패 0 |
| compile-check | 오류 0개 |
| core-tests | 1710(전 1698, +12) |
| exp-sim | 통과 |
| recall-loop | 41 |
| bridge-wiring | 23 |
| protocol | 193 |
| execution | 246 |
| life | 143 |
| life-craft | 58(+2) |
| maintenance | 171 |
| gather | 62 |
| travel | 105 |
| path-preparation | 37(+12) |
| life-cook | 73 |
| life-inventory | 253 |
| inventory-menu | 6 |
| info | 70 |
| action-input | 52(+8) |

### 새 시험
- **core PB7:** 짧은 길에 단지가 있고 열린 우회로가 있으면, 부수는 계획은 단지를 지나고 걷기 전용 계획은 우회로로 간다.
- **core PB8:** 물러나기, 파 들어가기 배선(소스 확인)
- **core PB9-13:** 물체 칸 비용, 한 번만 셈, 무기 피해, 작물 제외, 종류별 문구
- **core TREE:** 전투 중 회복 먹기
- **core GW24b·24c:** 장비 추적
- **core CE8b:** 전투 상태
- **path-preparation:** 실제 `AIBrain.Preparation.cs`의 `OpenPlanC`·`BreaksBesideOpenWay`·걷기 전용 캐시 칸. 빌드 안과 밖에서 각각 6개를 본다.
- **action-input:** 실제 `AIAttackSkill.cs`의 Progress가 실패, 실패, 휘두르기, 걷기 순서에서 어떤 문구를 고르는지 본다(F3·작은 것 5).
  - 이 시험은 0314009a 이후 컴파일되지 않고 있었다(AIBaseGuard 대역 없음). 대역을 넣어 되살렸다.
- **life-craft:** 개수만 바뀐 재료 부족은 craft 로그를 다시 찍지 않는다. 같은 준비 문구는 한 번, 바뀐 문구는 다시 찍는다.

### 시험 대역 변경
- life-tests FarmRoute, life-craft·maintenance AIApproach.Route에 `BlockSummary`를 넣었다.
- life-craft State에 `_prepLogged`를 넣었다.

### 이번 변경과 무관하게 원래 실패하는 시험
- restriction-tests: `FoodProbe.PlantEntry` 없음
- travel-tests/navigator: `AIBrain.MarkAreaQuestion` 없음
- travel-tests/candidate-progress: `.generated/CandidateSlices.cs` 없음

## 확인하지 못한 것
- Unity 컴파일, 실게임: 병합 뒤 HEAD가 한다.
- 걷기 전용 계획(PlanJob)과 물러나기 선택지·RetreatSkill의 실제 경로는 런타임 시험이 없다.
  - 계획기 비용 규칙은 합성 비용 함수(PB7)로 본다.
  - 고르는 규칙은 path-preparation의 실제 OpenPlanC로 본다.
  - 나머지 배선은 소스 확인뿐이다.
- `PlannedBreakDamage`가 쓰는 무기 기본 피해(`AIVitals.WeaponData`)는 게임의 실제 근접 피해(`PlayerAttackCD.meleeDamage`, 장비·버프 포함)와 다를 수 있다. 타격 수 추정만 바뀐다.
- 보스가 해당 상태에서 ChaseStateCD 등의 대상 필드를 실제로 유지하는지는 보지 못했다. 유지하지 않으면 전처럼 거짓이 된다.
- 작물이 실제로 이동을 막는 충돌체를 가지는지는 실게임에서 보지 못했다. 막도록 고쳤으므로 동작에는 영향이 없다.
