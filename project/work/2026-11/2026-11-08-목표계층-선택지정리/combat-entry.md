# 전투 진입 조건과 공격 선택지의 내 길 사실 (2026-11-08)

- 사용자 결정 1: "2. 그렇게 하고 추가로 적이 날 공격대상으로 인식했을 경우로 해."
- 사용자 결정 2: "돌아가야만 닿을 수 있으면 나도 마찬가지잖아 … 정보를 충분히 줘서 공격을 고르지 않을 수 있게 할 수 있나?" → 하기로 함.
- 실게임 사례: 42503a6a 설치본, Player.log 22:43. MushroomEnemy attack:58196이 벽 너머 5.5칸에 있었다. 본 적 없는 칸을 거치는 길만 있었는데 "올 수 있음(maybe)"으로 셈해져 COMBAT #3이 시작됐다. 공격 기술은 "길 없음"으로 32번 실패했고, 그동안 모델은 계속 '공격 계속'을 골랐다.
- 커밋 **0314009a** (브랜치 ai-celeris-decisions-20261108). 설치하지 않았고(게임 실행 중) 브리지도 다시 띄우지 않았다.

## 바뀐 것

### 전투 진입
`Core/AIBaseGuard.cs`의 `IsCombatThreat`가 판정한다. combatRange 안의 적이 다음 넷 중 하나일 때만 전투로 센다.
- (가) 아는 길이 있다. 판정은 `EnemyReach.known`으로 한다.
  - 걷는 적: 본 칸·기억한 칸 중 열린 칸(GuardCell.Open, 기지 기억 포함)만 걸어서 닿는다.
  - 날기·뛰기·쏘기 적: 아는 열린 칸·울타리·물 칸으로 닿는다(`ReachMap.flyKnown`).
- (나) 칠 거리 안에서 사이를 넘어 친다. 기존 HitsAcross(touch·breaker·ranged)를 그대로 쓴다.
- (다) 다쳤다. 1.5초 안에 다쳤고 그 적이 때린 쪽일 수 있다(flyReach). 자기 방어의 다침 규칙을 유지한 것이다.
- (라) 적이 나를 공격 대상으로 잡았다. `AIEdge.TargetsMe`로 판정한다(`AIEdge.Reach.cs`).
  - 읽는 곳: 적 자신의 상태 머신. `StateInfoCD.currentState`가 전투 상태(Chase, MeleeAttack, MeleeAttackContinuous, RangeAttack, JumpAttack, Charge, BeamAttack, RayAttack, IdleInCombat)이고, 다음 중 하나가 이 AI이면 대상으로 잡은 것이다.
    - `ChaseStateCD.targetEntity`: 쫓는 적이 모두 가지는 추적 대상이고, 공격 상태 중에도 유지된다.
    - `MeleeAttackStateCD.aimingAtEntity`
    - `ChargeAttackStateCD.targetEntity`: 야수 돌진 감시(AIChargeWatch)가 읽는 것과 같다.
    - `JumpAttackStateCD.target`
  - 컴포넌트·필드 이름과 타입은 게임 DLL 메타데이터로 확인했고, 실제 게임 DLL을 참조하는 compile-check로 컴파일했다.

본 적 없는 칸을 거쳐야만 오는 적(전에는 maybe로 위협이었음)은 이제 전투가 아니다. 대신 입력에 사실로 남는다.
- 적 사실: "이 적이 나에게 올 길은 본 적 없는 칸을 거쳐야만 이어짐(아는 길 없음)."
- CloseNote: "돌아올 길은 본 적 없는 칸을 거쳐야만 이어져 싸움으로 세지 않음."
- 공격 선택지는 그대로 나오므로 모델이 직접 상대할 수는 있다.

### 같은 판정을 쓰는 곳
모두 `AssessThreatWithinRange`(ObserveThreat·LastAssessedCannotCome)를 거친다.
- 전투 재판단 `_inCombat`: `AIBrain.Loop.cs:226`, :259
- 빌드의 inCombat: `AIBrain.Candidates.cs` Enemies
- 근접전 묶음 `CloseFight`
- 자기 방어 시작·유지(:728). 다침 규칙은 그대로다.
- 요청의 적 묶음 `_callEnemies`
- 가방 메뉴 위험 판정
- 준비 중 위협 묶음(`AIBrain.Preparation.cs`)

### 보수적 maybe를 남긴 곳과 이유
- `AIBaseGuard.IsThreat`와 `CombatNow`는 그대로 두었다.
  - 기지·문 사실의 '올 수 있음'(`r.comes`)은 기지 안전 판단에 쓰인다. 문이 열리면 달라짐, 기억으로 막혔다고 본 둘레 같은 것들이다. 그래서 보수적으로 둔다.
  - 둘 다 사실 문장과 기존 시험이 쓰고, 전투 진입에는 더 쓰이지 않는다.
- 목표 없을 때의 '새 적 보임' 이슈 트리거(NoGoalWatch)는 위협 판정을 쓰지 않는다. 그래서 바뀌지 않았다.
- `/ai reach` 시험 명령 표시는 새 판정을 따른다.

### 공격 선택지의 내 길 사실
모든 비전투 일반 공격 선택지 끝에 붙는다(`AIBrain.Candidates.cs` OwnReach → `AIBaseGuard.OwnReachText`).
- 이 빌드의 보이는 범위 걷기 탐색(`_reachDist`)으로 닿으면 "내가 갈 아는 길 N칸".
  - 이것은 공격 걷기가 다니는 아는 걸을 수 있는 칸과 같은 바탕이다.
- 그렇지 않으면 위협 판정이 이미 계산한 아는 칸 도달(EnemyReach Open·Door)을 쓴다: "내가 갈 아는 길 N칸(내 문 지나서)".
- 칠 거리 안이고 사이가 막히지 않았으면 "바로 칠 거리".
- 그 밖에는 "아는 길 없음 — 벽 너머, 본 적 없는 칸을 거쳐야만 이어짐. 고르면 다가가려다 막힘."
- 새 길 찾기는 하지 않았다.
- 야수 공격 선택지(전투 단계 2 문구)는 전투 최적화를 미뤄 두었으므로 붙이지 않았다.

### 실행 중 공격의 계속하기
`AIAttackSkill.Progress`를 고쳤다.
- 길 찾기가 연달아 2번 이상 실패하면 진행 문구가 이렇게 바뀐다(`AIBaseGuard.AttackStuckText`): "대상 X 체력 h/m, d칸. 길 찾기 N번 실패(S초째), 대상 체력 변화 없음(또는 a→b)". 이때 '약 N타 남음'은 빼고, 길이 있을 때만 보인다.
- 실패 연속 시작 시각은 `_pathFailSince`에 기록하고, 다시 걷기 시작하면 지운다.
- 계속하기의 '바꾸는 비용' 문장(전투)은 전투 최적화를 미뤄 두어 그대로다.

## 시험
| 시험 | 결과 |
|---|---|
| core-tests | 1651 |
| execution | 246 |
| life | 143 |
| maintenance | 171 |
| gather | 62 |
| travel | 105 |
| life-cook | 73 |
| life-craft | 49 |
| life-inventory | 253 |
| inventory-menu | 6 |

core-tests에 새 CE 묶음 12개가 들어갔다.
- 벽 너머·본 적 없는 칸으로만 이어지는 적은 전투가 아니다. 보수적 IsThreat로는 위협이었다.
- 같은 적이 나를 노리면 전투다.
- 아는 길이 있으면 전투다.
- 다치면 전투다.
- 범위 밖이면 아니다.
- 아는 울타리를 넘어오는 나는 적은 전투다.
- 전투 상태 이름만 대상으로 친다.
- 그 밖: 내 길 문장, 막힌 공격 문장, 적 사실 문장, 배선(소스) 확인.

F6a는 새 문구와 `r.known`·전투 판정까지 확인하도록 고쳤다.

**Unity 컴파일은 아직 안 했다.** 게임이 실행 중이어서 오프라인 gates와 compile-check(실제 게임 DLL 참조, 오류 0)만 돌렸다.

## 재생 (Celeris 2회, 동시 3)
- 대상: 실게임 기록 `decisions-224158.log` #105–#131(27건). 모두 58196 '공격 계속'이 고른 답이었다.
- V0는 기록 그대로다.
- V1은 새 문구다.
  - 아는 길이 없는 적(그 빌드에 그 적 칸으로 '파 들어가기'가 있던 적)의 공격 선택지에 NoKnownWay를 붙였다.
  - 계속하기에는 Player.log의 실패 줄로 길 찾기 N번 실패·S초째를 넣었다. 예: #105는 14번, 26초째다.
- 자료: `replay/ce/`(build.mjs, V0/V1.jsonl, out-*, tally.txt).

| 변형 | 결과(54) | 닿을 수 없는 적에게 공격·계속 |
|---|---|---|
| V0 | 계속 54 | 54/54 |
| V1 | 계속 24, 중단 17, 따라가기 7, 파 들어가기 4, 물러나기 2 | 24/54 |

- 한계: 새 진입 규칙에서는 이 장면이 처음부터 전투가 아니었을 수 있다. 그러면 선택지 묶음 자체가 달라지는데, 재생은 기록된 근접전 묶음에 문구만 바꿔 쟀다.
- 남은 24번 계속하기에는 '바꾸는 비용' 설득 문장이 여전히 붙어 있다(전투 최적화로 미룸).

## 추가: 길에 막힌 부술 수 있는 자연물 (2026-11-08, 커밋 d6d03f88)

- 사용자 결정: "그것도 고치고". 큰 뿌리가 1칸 통로를 막아 길 계획이 '길 없음'이 됐는데, 사용자가 뿌리를 부수자 AI가 바로 지나갔다.
- 판정은 `Core/AIPathBreak.cs`가 한다.

### 무엇이 바뀌었나
**길에 넣는 것(부수고 지나감)**
- 큰 뿌리 타일(TileType.bigRoot): 걸을 수 없는 칸이었다. 체력·감소는 게임 데이터에서 읽는다(`AIWallData.TryGetTile`, `AITileReader.cs`).
- 자연 물체: RootCD 또는 DestructibleObjectCD가 있고 체력이 있는 물체.
- 비용은 벽과 같다: 1 + 2 × 타격 수. 계획 피해로 못 부수면 -1(못 지나감)이다.

**길에 넣지 않는 것(그대로 못 지나감)**
- PlaceablePrefab(상자·제작대·횃불·울타리·석상 등 놓은 모든 것), 문, 게이트, 제작대(CanCraftObjectsBuffer), 작은 생물.
- 문은 지금의 문 규칙을 그대로 따른다.
- 자연 물체 판정은 `AIObjects`가 막힌 칸을 읽는 같은 순회에서 한다(`BreakableInView`·`BlockedInViewAt(..., breakable)`).

**실행**
- 계획은 그런 칸을 `TunnelPlan.breaks`에 싣는다. `TunnelSkill`은 벽을 캐는 자리에서 그 칸을 부순다. 큰 뿌리는 RootSkill, 물체는 BreakSkill로 한다. 벽과 같은 방식이다.
- 명시 상한이 있으면 따른다: 뿌리는 RootLimit, 벽은 WallLimit.

### 적용 범위
- 적용함: 터널 계획기(PlanBatch). 이를 쓰는 기능은 다음과 같다.
  - 파 들어가기
  - 기억한 광석 쪽 이동
  - 적·물체 쪽으로 파 들어가기
  - 긴 걷기 구간
- 바꾸지 않음
  - 걸어가는 A*(AINavigator): 공격 기술의 다가가기, 통로 탐색 걷기, 채집 걷기가 쓴다. 이것은 걷기만 하고 부수기를 실행하지 않아서 그대로 막힘으로 본다. 이런 경우 막힌 물체·뿌리를 부수는 선택지는 지금처럼 따로 나온다.
  - 탐험 끝 찾기(FrontierJob)

### 시험
- core PB 묶음 6개
  - 자연 뿌리·물체는 지나갈 수 있음
  - 놓은 것·문·게이트·제작대·작은 생물·자연물 아님·체력 0은 지나갈 수 없음
  - 비용은 벽과 같고, 못 부수면 -1
  - 1칸 통로의 뿌리를 뚫고 계획함
  - 같은 통로의 놓은 물체는 길 없음
  - 배선 확인

| 시험 | 결과 |
|---|---|
| core | 1657 |
| execution | 246 |
| gather | 62 |
| life | 143 |
| maintenance | 171 |
| travel | 105 |
| path-preparation | 25 |
| gates --unity | 실패 0(Unity CS 0) |

- 게임이 꺼진 뒤 돌린 Unity 컴파일로 앞 커밋(전투 진입)의 Unity 컴파일도 확인됐다.

## 추가: 트리가 열리면 그 트리 항목만 (2026-11-08, 커밋 a6320486)
- 사용자 결정(설계 정정): 다음 셋 중 하나가 열려 있으면 하위 선택지는 그 트리의 항목만 남긴다.
  - 채집·이동 목록(`AIExpedition.Active`)
  - 생활 목록(`AILife.Cursor.Open`)
  - 가방 메뉴(`AIInventoryMenu.Active`)
- 그 트리의 항목은 다음과 같다.
  - 목록 이동 항목, 범위·품목 항목, 닫기·뒤로·처음으로
  - 몸 행동을 시작하는 잎: 채집·이동 목록이 넣은 잎은 `AICandidate.treeEntry`로 표시하고, 생활 잎은 `c.life`, 가방은 `inventory_menu`·`c.inventory`로 알아본다.
- 빠지는 것: 묶음 채집, 기다리기, 다른 메뉴, 탐험, 실행 중인 일의 계속하기, 무관 채집.
- 실행 중인 몸 행동은 멈추지 않는다. 트리 둘러보기는 조회일 뿐이다.
- 예외는 생존뿐이다.
  - 전투 중(새 전투 진입 규칙)의 전투 선택지: 공격·물러나기·짧은 이동 등과 그 계속하기
  - 배고픔 25 미만일 때 먹기
- 규칙은 `Core/AIFieldChoices.TreeOnly`, 연결은 `AIBrain.Loop.cs` TreeOnly(RefineForGoal 뒤)다. 뺀 것은 "BRAIN: tree open … left out" 로그에 남는다.
- 시험(core TREE 7개)
  - 채집 목록이 열리면 그 목록 항목과 잎만 남는다.
  - 전투 중이면 전투 선택지가 더해진다.
  - 배고픔이 위험 구간이면 먹기가 더해진다.
  - 생활 목록이 열리면 생활 항목만 남는다.
  - 가방 메뉴가 작동하면 가방 항목만 남는다.
  - 아무 트리도 열리지 않으면 그대로다.
  - 배선을 소스로 확인했다.
- 전체 확인: core 1664, execution 246, life 143, maintenance 171, gather 62, inventory-menu 6, travel 105, gates --unity 실패 0.
- 재생(Celeris 2회; `replay/tree/`)

| 요청 | 열린 트리 | 선택지 수 | 이전 답 | 바꾼 뒤 답 |
|---|---|---|---|---|
| #641 | 채집·이동 목록 | 17 → 3 | 흙벽 묶음 채집 | 목록 이동 |
| #654 | 채집·이동 목록(전투) | 22 → 8 | 흙벽 묶음 채집·물러나기 | 목록 이동 |
| #661 | 채집·이동 목록 | 25 → 3 | 흙벽 묶음 채집·목록 이동 | 목록 이동 |
| #700 | 생활 목록 | 85 → 8 | 흙벽 묶음 채집 | 생활 목록 이동 |
| #710 | 생활 목록 | 95 → 8 | 흙벽 묶음 채집 | 생활 목록 이동 |

- 합계: 이전 10번 중 묶음 채집 9번, 지금 10번 모두 트리 이동.
- 재생에서는 트리가 열렸는지를 기록의 nav 항목으로, 전투 여부를 근접전 종류로 근사했다.
