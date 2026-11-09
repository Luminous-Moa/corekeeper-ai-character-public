# 독립 검수 — 목표계층·선택지 정리 후반 5커밋 (2026-11-08 늦은 검수)

- 대상: worktree `C:\Dev\CoreKeeperModSDK-celeris`, 브랜치 ai-celeris-decisions-20261108, `git diff 42503a6a..4171d9b8`
  - 0314009a 전투 진입 / d6d03f88 뿌리·물체 부수고 지나가기 / a6320486 트리 열림 선택지 / d3fcf40f 제작 실패 이유 / 4171d9b8 장비 상태·계기
- 방법: 작업자 보고는 근거로 쓰지 않고 실제 코드 경로를 읽었다. 게임 규칙은 `C:\Dev\_ref\corekeeper-decomp`로 확인했다(StateID 이름, ChaseStateCD.targetEntity, MeleeAttackStateCD.aimingAtEntity, DestructibleObjectCD/RootCD, ObjectType).
- 돌린 시험: core-tests `ALL CHECKS OK (1698)`, life-craft-tests `CRAFT CHECKS 56, FAILED 0`, compile-check `오류 0개`. Unity 컴파일과 실게임은 돌리지 않았다.
- 코드는 고치지 않았다.

## 판정: 고친 뒤 합격

다섯 동작의 큰 줄기는 코드 경로에서 지켜진다. 다만 길 계획 변경이 **물러나기(생존 이동)를 끊는 회귀**가 있고, 트리 정리가 **전투 중 회복 먹기**를 빼며, 막힌 공격의 문구가 **실제로 치고 있을 때도 '길 찾기 실패'로 남는다.** 이 셋과 제작 이유의 빈 경로 하나를 고치면 합격이다.

---

## 막음
없음.

## 고칠 것

### F1. 길 계획이 자연물을 부수는 길을 고르면 물러나기가 사라지거나 실패한다 (d6d03f88)
- 파일:행
  - `Assets/AIPlayerSpike/Scripts/AITunnel.cs:610`, `:875`: 부술 칸 비용이 `1 + 2 × 타격 수`다.
  - `AITunnel.cs:485-489`: 자연 물체도 부술 칸이 된다.
  - `AITunnel.cs:966-970`: 부술 칸이 `plan.walls`에 들어간다.
  - `Assets/AIPlayerSpike/Scripts/AICombat.cs:699`: `if (plan == null || plan.walls.Count > 0) { Finish(Failed, "열린 길 없음(벽 N개)") }`
  - `Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs:353`: `openWay = rp != null && rp.walls.Count == 0`. 이것이 거짓이면 물러나기 선택지를 만들지 않고 `blocks`에 "열린 길 없음(벽 1개)"만 남긴다.
- 실제 상황
  - 싸움 중 코어 쪽으로 물러날 길에 부서지는 단지·자연 뿌리 물체가 하나 있다.
  - 1타면 부서지는 물체라 비용은 3이다. 2칸보다 긴 우회로가 열려 있어도 계획기는 부수는 길을 고른다.
  - 그 결과 `walls.Count = 1`이 되어 물러나기 선택지가 빠지거나, 진행 중인 RetreatSkill이 다음 구간에서 "열린 길 없음(벽 1개)"로 실패한다.
  - 바뀌기 전에는 우회로로 물러날 수 있었다. 싸움에서 빠져나오지 못하고 맞게 된다.
- 근거
  - `RetreatSkill.NextLeg`와 물러나기 후보는 여전히 "부술 것 0개 = 열린 길"로 판정한다. 이 판정이 기대는 계획기는 이제 자연물도 부술 대상으로 넣는다.
  - TunnelSkill은 부수기를 실행하지만, RetreatSkill은 GotoSkill만 쓴다(`AICombat.cs:700-704`).
  - 작업자 문서도 적용 범위를 "PlanBatch를 쓰는 기능"으로만 적었고, `plan.walls.Count == 0`을 열린 길 판정으로 쓰는 곳은 점검하지 않았다.
- 고칠 방향(참고)
  - 물러나기처럼 걷기만 하는 소비자는 부수기 없는 계획을 받게 한다(계획기에 부수기 끄기 옵션).
  - 또는 `walls`에서 부술 칸만 따로 세어 '벽 0개 + 부술 물체 n개'를 처리한다.
  - `AIBrain.Candidates.cs:655`(떨어진 아이템 파 들어가기)와 `:308`(적 쪽 파 들어가기)도 같은 판정을 쓰니 함께 본다.

### F2. 트리가 열린 채 싸우면 회복 먹기 선택지가 빠진다 (a6320486)
- 파일:행
  - `Assets/AIPlayerSpike/Scripts/Core/AIFieldChoices.cs:93`: `CombatKinds`에 `eat`가 없다.
  - `AIFieldChoices.cs:107`: `keep[i] = mine || inCombat && IsCombat(...) || hunger < 25 && c.kind == "eat"`
  - 회복 선택지 생성: `AIBrain.Candidates.cs:373`. 싸움 묶음 `lCombat`에 `kind = "eat"`, 키 `eat:<회복 아이템>`으로 들어간다.
- 실제 상황
  - 채집·이동 목록이나 생활 목록이 열린 채 싸움이 시작되고, 체력이 떨어져 가방의 회복 물약·음식이 선택지로 만들어진다.
  - 배고픔이 25 이상이면 TreeOnly가 이 먹기를 뺀다. 남는 것은 공격·물러나기와 트리 항목뿐이라 모델은 회복을 고를 수 없다.
  - 트리는 싸움에 반응해 닫히지 않는다(`AIExpedition`·`AILife`·`AIInventoryMenu`에 전투 처리가 없다). 목록을 열어 둔 채 일하는 동안 싸움이 나면 그대로 이 상태가 된다.
- 근거
  - 사용자 기준은 '예외는 생존뿐: 전투 중이면 전투 선택지'다. 싸움 묶음이 만든 회복 먹기는 전투 생존 선택지인데, 종류 이름(`eat`) 때문에 배고픔 규칙으로만 판정된다.
  - TREE 시험(FieldChoiceTests TreeOnlyRule)도 전투 중 먹기는 빠져야 한다고 고정했다(`!Kept(k2).Contains("eat")`).
- 고칠 방향(참고)
  - 싸움 묶음에서 온 먹기(회복 아이템)는 전투 중 남긴다. 예: 후보에 `combat` 표시를 달거나, `inCombat && kind=="eat"`이고 체력이 최대 미만이면 남긴다.

### F3. '길 찾기 N번 실패' 문구가 실제로 치고 있는 동안에도 남는다 (0314009a)
- 파일:행
  - `Assets/AIPlayerSpike/Scripts/AIAttackSkill.cs:87-88`: `_pathFails >= 2`이면 막힌 문구를 쓴다.
  - `AIAttackSkill.cs:161-165`: 1.3칸 안에서 휘두르는 경로에서는 걷기를 정리할 뿐, `_pathFails`를 되돌리지 않는다.
  - `AIAttackSkill.cs:195`: 걷기가 실제로 움직일 때만 `_pathFails`가 0으로 돌아간다.
- 실제 상황
  - 벽 너머에서 길 찾기가 두 번 이상 실패했다. 그 뒤 적이 돌아와 붙어서 AI가 바로 치고 있다.
  - 이때 '계속하기' 문구는 여전히 "길 찾기 N번 실패(S초째), 대상 체력 변화 a→b"이고, S는 계속 커진다. '약 N타 남음'도 빠진다.
  - 재생에서 새 문구가 중단·전환을 늘렸으므로(24/54), 이기고 있는 근접전에서 모델이 공격을 멈출 위험이 있다.
- 근거
  - 사용자 기준은 "계속하기는 길 찾기가 반복해서 실패하면 그 사실"을 적는 것이다. 지금 칠 거리 안에서 치는 중이면 '길이 있는' 상태다. '약 N타 남음'은 길이 있을 때만 빼야 하는데, 이 경우에도 빠진다.
- 고칠 방향(참고)
  - 휘두르기 경로(`d <= 1.3f` 분기)에 들어가면 `_pathFails = 0; _pathFailSince = -1f`로 되돌린다.
  - 또는 Progress에서 `d <= 1.3f`이면 막힌 문구를 쓰지 않는다.

### F4. 화면 밖 기억 용광로만 있을 때 제작 실패 이유가 남지 않는다 (d3fcf40f)
- 파일:행
  - `Assets/AIPlayerSpike/Scripts/AICraft.cs:166`(LifeEntries, where 없음): 기억한 자동 가공 시설은 `!PrefabManual`이면 아무 말 없이 건너뛴다.
  - `AICraft.cs:587-591`: `RememberedStationFor`가 자동 가공 여부를 거르지 않는다.
  - `Assets/AIPlayerSpike/Scripts/AIBrain.Prep.cs:126-150`(where == null 분기)
- 실제 상황
  - 단계가 'CopperBar 만들기'(Goal.where 없음)이고, 용광로는 전에 본 것이라 지금 화면 밖이다.
  - LifeEntries가 빈 목록을 돌려주므로 `exactWhy`가 없다. `craft exact` 로그도 찍히지 않는다.
  - 준비 문구는 "CopperBar: Furnace에서 만듦 — 지금 안 보이지만 기억한 …"이 되고, '기억한 제작대로 가기 — 거기서 CopperBar를 만들 수 있음' 걷기 선택지까지 생긴다.
  - 도착하면 ReadRecipe가 자동 가공으로 거절하는데, 그 전까지 이유는 어디에도 없다.
- 근거
  - 사용자 기준은 "정확한 제작 선택지가 안 만들어지는 모든 경로에서 이유가 로그와 준비 문구에 남는다"이다.
  - 화면 안의 용광로 경로(`AICraft.cs:160`)만 이유를 남기고, 기억 경로는 빠졌다.
  - 작업자 문서(craft-diagnosis.md '바꾸지 않음' 3번)가 같은 지점을 결정 사항으로 남겼다. 하지만 이유를 남기는 일(5번 기준)은 그 결정과 상관없이 할 수 있다.
- 고칠 방향(참고)
  - 기억 경로에서 자동 가공 시설을 만나면 `NoExact`와 같은 방식으로 '기억한 {id}: 자동 가공 시설로 봄'을 로그와 준비 문구에 남긴다.
  - 걷기 선택지 문구가 "만들 수 있음"이라고 말하지 않게 한다.

## 작은 것

1. **`AIGearWear.cs:142-146` 장비 추적의 잘못된 대응.**
   - 둘째 단계(같은 id·변형·수량으로 다른 칸 찾기)가 셋째 단계(같은 칸)보다 먼저 돈다.
   - 예: 같은 종류 곡괭이 두 자루가 있다. 이미 닳음 표시된 B(35)가 상자로 가서 사라진 그 1초 안에, 쓰던 A가 36→35로 30% 선을 넘는다.
   - A가 B의 표시를 물려받아 gear_worn이 안 걸린다. 드물지만 한 번 놓치면 수리 전까지 다시 걸리지 않는다.
   - 같은 칸에 같은 id가 있으면 그것을 먼저 잡게 하면 된다.
2. **`AIBrain.Prep.cs:204` 같은 이유의 반복 로그.**
   - 새 `craft exact` 로그는 한 번만 남는다(`AICraft.cs:185`).
   - 하지만 준비 문구 전체를 찍는 `BRAIN: preparation:` 줄은 빌드마다 찍힌다. 전투 중에는 약 0.3초마다다. 이제 이유가 붙어 같은 이유가 계속 반복된다(줄 자체는 원래 있던 것).
3. **`AICraft.cs:184-185` 중복 막기 키에 바뀌는 숫자가 들어간다.**
   - 재료 부족 이유("CopperOre 3/4 …")는 채집으로 개수가 바뀔 때마다 새 줄이 된다.
   - `_exactLogged`는 캐릭터 구분 없이 하나로 공유되고, 256개가 넘으면 통째로 비워진다.
4. **`AIEdge.Reach.cs:204` 적 대상 읽기 실패 로그가 빈도 제한 없이 찍힌다.** 적마다, 0.25~0.5초마다 LogError가 나간다. `AIBaseGuard.CombatStateName`에는 `TookDamage`와 보스 전용 상태(ScarabBossCharge, SnakeBossMove 등)가 없다. 그 순간에는 '나를 노림'이 거짓이 되지만, 다른 조건(아는 길, 칠 거리, 다침)이 대개 받쳐 준다.
5. **`AIAttackSkill.cs:87`: 아는 길이 없는 적을 고른 직후** 첫 실패 2번까지(약 2~4초)는 '약 N타 남음'이 나온다. 기준은 '길이 있을 때만'이다.
6. **`AIObjects.cs:197` 여러 칸 물체의 비용을 두 번 센다.** 2×2 자연물의 각 칸이 부술 칸으로 따로 값이 매겨져 비용이 과대해진다. 실행은 칸마다 다시 계획하므로 동작 문제는 아니다.
7. **`AITunnel.cs:1112` 부수기 무기와 계획 피해가 다르다.** 물체 부수기는 BreakSkill이 공격 무기(`WeaponSlotForAttack`)로 하는데, 계획 피해는 곡괭이의 캐기 피해다. 타격 수 추정만 어긋난다.
8. **문구.** 기지로 가기·장소로 가기 같은 걷기 선택지에 "부술 벽 1개(Root)"처럼 뿌리·물체가 '벽'으로 나온다(`AIBase.cs:501`, `AIPlaces.cs:567`, `WallSummary`).
9. **확인 필요(실게임).**
   - 심은 작물·식물이 `DestructibleObjectCD`와 충돌체를 가지면서 `PlaceablePrefab`이 아니면 부술 대상이 된다. 호스트의 밭을 부술 수 있다.
   - 디컴파일로는 어떤 프리팹이 `DestructibleObjectCD`를 갖는지 알 수 없다. 첫 실게임에서 `SKILL 파들어가기: … mine <id>` 줄의 id를 확인한다.
10. **`AIPlanner.cs:520` 같은 경고가 두 번 들어간다.** 경고 문구가 보고의 `gear_alert`와 `events`('장비: …')에 같이 들어간다.
11. **트리가 열린 채 비전투 적이 보일 때.** 선제공격 선택지는 트리 규칙 때문에 빠진다. 사용자 기준 4(트리 열림 → 트리 항목만, 위협일 때만 전투 선택지)에 맞는 동작이지만, 기준 1의 '선제공격 가능'과 겹치는 지점이다. 의도한 결과인지 사용자에게 한 줄 확인하면 좋다.

---

## 기준별 확인 결과

### 1. 전투 진입 — 지켜짐
- 판정은 `IsCombatThreat`(`Core/AIBaseGuard.cs:683`)가 한다. 다음 넷 중 하나면 전투다.
  - 아는 길(`r.known`: m.open의 열린 칸 BFS, 또는 걷기 아닌 적의 flyKnown)
  - 칠 거리 + HitsAcross
  - 1.5초 안에 다침 + flyReach
  - 나를 노림(TargetsMe)
- 전투 판정 경로는 모두 이 함수를 거친다.
  - 재판단: `Loop.cs:226`, `:259`
  - 빌드: `Candidates.cs:328`
  - 자기 방어: `Loop.cs:728`
  - 근접전 묶음, 호출 적 묶음, 가방 메뉴 위험, 준비 위협: `LastAssessedCannotCome`
- 보수적 판정 `IsThreat`는 이제 사실 문장에만 남았다(grep 확인).
- TargetsMe의 컴포넌트·필드 이름과 상태 이름은 디컴파일(StateID, ChaseStateCD.targetEntity, MeleeAttackStateCD.aimingAtEntity)과 맞는다. 엔티티가 사라지면 `Exists` 검사로 거짓이 된다.
- 선제공격: `Candidates.cs:276-286`이 보이는 모든 적에 공격 선택지를 만든다. 본 적 없는 칸으로만 이어지는 적은 `er.kind == Unseen`이라 cannot에 걸리지 않는다. 선택지 끝에 `NoKnownWay` 문구가 붙는다.
- 전투가 늦게 시작될 위험
  - 어둠은 반영하지 않으므로, 화면 안의 칸은 본 칸이다. 그래서 '본 적 없는 칸만으로 이어짐'은 사실상 벽 너머일 때다.
  - 쫓아오기 시작하면 Chase 대상이 AI가 되므로 바로 전투가 된다.
  - 큰 회귀는 찾지 못했다(작은 것 4 참고).

### 2. 공격 선택지 — 대체로 지켜짐
- 길이 있으면 'N칸', 없으면 NoKnownWay를 적는다(`AIBaseGuard.OwnReachText`).
- 계속하기에 실패 횟수·시간·체력 변화를 적는다. 문제는 F3과 작은 것 5다.

### 3. 뿌리와 물체 — 동작은 맞음, 회귀 있음
- 큰 뿌리 타일과 자연 물체(RootCD/DestructibleObjectCD, 체력 > 0)는 부수는 비용으로 지나간다.
- 제외 대상: `PlaceablePrefab`, 문, 게이트, 제작대(CanCraftObjectsBuffer), 작은 생물. 호스트·내가 놓은 물건은 PlaceablePrefab이라 제외된다.
- 실행은 부술 칸마다 RootSkill·BreakSkill로 부수고, 다 부수면 다시 계획한다(`AITunnel.cs:1103-1112`, `NextStep(null)`).
- 물체가 그 사이에 사라지면 BreakSkill이 Done으로 끝나고, 다음 계획에서 빠진다.
- 명시 상한은 RootSkill·BreakSkill 안에서 다시 검사한다(`AIBreakSkills.cs:31`, `:132`).
- 회귀는 F1이다. 걷기 A\*(AINavigator)와 탐험 끝 찾기는 바뀌지 않았다(문서에 적힌 범위와 같다).

### 4. 트리가 열렸을 때 — 대체로 지켜짐
- 닫기·뒤로·처음 항목은 모두 nav 종류라 남는다. 트리를 닫을 수 없게 되는 경우는 없다.
- 남는 것이 하나도 없으면 원래 목록으로 돌아간다(`Loop.cs:1407`).
- nav를 고르면 몸 행동을 시작하지 않는다(`AIActions.cs:92`, `AIExpedition.Navigate`). 진행 중인 행동은 끊기지 않는다.
- RefineForGoal은 트리가 열려 있으면 바로 돌아가므로(`AIFieldChoices.cs:137-139`) 두 규칙이 부딪치지 않는다.
- 전투 여부는 빌드가 막 정한 `_inCombat`이다(`Candidates.cs:382`가 TreeOnly보다 먼저 돈다). 트리가 열린 채 적이 나타나도 전투 선택지는 남는다.
- 문제는 F2와 작은 것 11이다.
- 참고: 채집·이동 목록에서 잎(몸 행동)을 골라도 목록은 닫히지 않는다(`ExpeditionCursor.Apply`는 close에서만 닫음). 그래서 이후 판단도 계속 트리 항목만 받는다. 기준에 맞는 동작이다.

### 5. 제작 실패 이유 — 대부분 지켜짐
- ExactCandidate·ReadRecipe의 거절 경로마다 이유가 붙는다. DB 실패, 예외, 재료 부족, 커서 칸, where 어긋남(보이는 제작대 id·타입 포함), 자동 가공이 모두 해당된다.
- where 없이 화면 안의 자동 가공 시설도 준비 문구에 이유가 들어간다.
- 빠진 경로는 F4다. 반복 문제는 작은 것 2·3이다.

### 6. 장비 상태 — 지켜짐
- 문구: 부서짐(0), 닳음(30% 아래), 쓸 만함, 새것, 보강됨. 경계는 36/120이 쓸 만함, 35/120이 닳음이다.
- 내구도가 없는 물건(쌓이는 물건, DurabilityCD 없음)에는 상태를 쓰지 않는다.
- 문구가 들어가는 곳: `bag_slots.state`, `me.hand`, `me.gear`, `gear_alert`, 하위 입력의 '손에 든 것'과 '장비 상태'(닳거나 부서졌을 때만).
- 계기
  - 30% 아래로 처음 내려가면 gear_worn 1회, 0이 되면 gear_broken 1회. 30% 이상이 되면 다시 걸린다.
  - 처음 보는 장비는 부르지 않는다.
  - 두 장비가 같이 걸리면 한 번만 부르고, 하나라도 부서졌으면 gear_broken이다.
  - 싸움 중이면 미루고 끝난 뒤 부른다. 지켜보는 중이 아니면 버린다.
- 보고가 이미 가는 중이면 계기는 대기열로 간다. 경고 문구는 `S._gearAlert`에 남아 다음 보고에 실린다. 더 급한 계기가 대기열을 덮어써도 문구는 사라지지 않는다.
- 브리지(protocol.mjs)는 계기 이름을 검사하지 않고, bag_slots의 state를 숫자로 읽는 곳도 없다(grep 확인).
- 문제는 작은 것 1·10이다.

## 시험이 무엇을 시험하나
- **core-tests**: `Assets/.../Scripts/Core/**`를 링크해 실제 코드를 시험한다.
  - CE1–11(IsCombatThreat·ReachOf·문구), TREE(TreeOnly), GW(GearWear·GearWatch), PB1–3(분류·비용)이 여기에 든다.
  - CE12, TREE 배선, PB6, GW 배선은 소스 문자열 검사일 뿐, 동작은 시험하지 않는다.
  - PB4·5는 범용 `AITunnelSearch.Find`에 합성 비용 함수를 넣은 시험이다. 실제 계획기의 칸 읽기(`ReadFact`의 뿌리·물체 분기)와 AIObjects 분류는 런타임 시험이 없다.
  - TargetsMe와 AIGearState(Unity 쪽)도 런타임 시험이 없다.
- **life-craft-tests**: 실제 `AICraft.cs`와 `AIBrain.Prep.cs`를 링크하고, 가짜 ECS·DB를 쓴다.
  - AILife만 추출 복사본(`LifeBindingSource.cs`, `ExtractLifeBinding.ps1`)이다. 제작 이유 시험은 실제 코드를 시험한다.
- **F1(물러나기)과 F2(전투 중 회복)를 잡는 시험은 없다.** TREE 시험은 오히려 전투 중 먹기가 빠지는 것을 기대값으로 고정했다.
