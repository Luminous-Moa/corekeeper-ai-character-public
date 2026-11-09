# 목표 계층·선택지 정리 — 재검수 (2026-11-08)

대상: 브랜치 ai-celeris-decisions-20261108, worktree `C:\Dev\CoreKeeperModSDK-celeris`
- 커밋 8ec4cd41: [review.md](review.md) 지적 반영. [review-fixes.md](review-fixes.md)는 확인할 주장으로만 읽었다.
- 커밋 f5d16072: 3단계 비전투. 보수 재료는 모자랄 때만 내고, 입력을 다듬는다. [stage3-noncombat.md](stage3-noncombat.md)를 함께 보았다. 이 커밋은 처음 검수한다.
- 기준: [design.md](design.md)

코드는 바꾸지 않았다. Unity와 게임은 실행하지 않았다.

## 판정: **고친 뒤 합격**

- 막음 2건은 실제 코드 경로에서 고쳐졌다. 고칠 것 4건도 고쳐졌다.
- 고칠 것-2는 한 부분이 남았다. 고친 범위는 mode와 count가 모두 있는 단계뿐이다.
  - mode는 있고 count가 없는 farm·cook 단계는 자동 완료되지 않는다. cook wait은 count를 받지 않으므로 늘 이 경우다.
  - 그런데 판단 모델 입력과 brief는 이런 단계도 프로그램이 끝낸다고 말한다.
  - cook은 8ec4cd41에서 오히려 나빠졌다. 그전에는 솥 관측으로 끝났는데, 이제는 끝날 길이 없다.
  - 막히지는 않는다. request_plan이나 정기 점검에서 위쪽 LLM이 step.stop을 고르면 풀린다.
  - 아래 '고칠 것 A'만 고치면 설치해도 된다. 고친 부분은 단위 시험으로 확인하면 충분하고 재검수까지는 필요 없다.
- 3단계(f5d16072)에서는 판단에 필요한 사실을 지운 곳을 찾지 못했다.

## 돌려 본 시험 (이 재검수에서 직접 실행)
| 시험 | 결과 |
|---|---|
| `bash Tools/core-tests/gates.sh` (--unity 없이) | 실패 0. compile-check 오류 0, core-tests 1638, protocol 193, bridge-wiring 23, recall-loop 41. exp-sim·fake-bridge·logshape·decisions-compare·movecheck OK. memgraph는 KG 데이터가 없어 건너뜀 |
| execution-tests | 241/241 |
| life-tests | 138 |
| gather-tests | 62 |
| life-cook-tests | 73 |
| `node Tools/ai-bridge/test/*.test.mjs` | protocol 193, gather-limit 14+11, bridge-wiring 23, recall-loop 41 |

보고서에 적힌 시험 수는 실제와 맞다. 그러나 다음 셋은 소스 문자열 검사로만 확인되어 있다. 이 재검수에서 코드를 읽어 따로 확인했다(아래).
- Send·Apply의 연결
- 빌드가 후보에 gives를 싣는 배선
- AIExpedition의 goalDirect

---

## 원래 지적별

| 지적 | 판정 | 근거 |
|---|---|---|
| 막음-1 step 답이 다음 단계에 적용됨 | **고침** | 아래 1 |
| 막음-2 부수기를 물체 id로 판정 | **고침** | 아래 2 |
| 고칠 것-1 보이는 목표 광석이 목록 안에만 있음 | **고침** | 아래 3 |
| 고칠 것-2 farm·cook 완료 판정 | **일부** (mode+count는 고침. mode만 있는 경우는 새 고칠 것 A) | 아래 4 |
| 고칠 것-3 prefetch 미정리 | **고침** | 아래 5 |
| 고칠 것-4 근접전 중 step.stop | **고침** | 아래 6 |
| 작은 것 1 보조 목표 별칭 | 고침 | `AIPlanner.cs:172`: `TryItemPublic`→`id.ToString()` canonical. `Core/AIExtraGoals.cs:94-105`: 매핑 뒤 중복 판정, 새 ExtraGoal로 저장 |
| 작은 것 2 거절 사유 덮어쓰기 | 고침 | `AIPlanner.cs:457` AppendRejected. set 잘림·refusedAll·limitOwner 세 곳이 이어 붙인다. 답을 받을 때 `_rejected = null`로 비우는 것은 그대로다(답 하나 단위) |
| 작은 것 3 브리지가 답 전체를 버림 | 고침 | `protocol.mjs:112-138`. 게임 쪽은 `AIPlanner.cs:174`(모두 잘못이면 바꾸지 않음), `[]`는 지움. 양쪽이 같다. 남은 차이는 아래 작은 것 b |
| 작은 것 4 기억 광석 경로 비용 | 대체로 고침 | `AIBrain.Candidates.cs:582-584`. gather·craft 단계에서만 계산한다. 광맥 판정은 덜 됐다(아래 작은 것 a) |
| 작은 것 5 가방 가득 참 | 고침 | `AIBrain.Loop.cs:1391` bagFull, `Core/AIFieldChoices.cs:126`. `BagHasRoomFor(None)`은 빈 칸이 있으면 true라서 explore·follow에 잘못 켜지지 않는다 |
| 작은 것 6·7 | 문서 | brief.md:3·65·66 |
| 작은 것 8 explore_ended | 고침(brief) | brief.md:10·90. 주석 2곳은 남음(`Core/AIPlanInbox.cs:4`, `Core/AIPlanRules.cs:98` StepDone). 동작에는 영향 없음 |

### 1. 막음-1 — 고침
- 보고 때 단계를 기록한다: `AIPlanner.cs:554` `S._reqGoal = AIGoals.Goal`.
  - 이 줄은 `/v1/plan`을 보내는 유일한 경로(`:555`) 바로 앞에 있다. 재전송·owed 보고도 같은 함수를 거친다.
- keep·ask 답은 그 기록에 묶어 적용한다: `:768` `ApplyStep(..., S._reqGoal, true)`.
- `:395-400`: 참조가 다르면 적용하지 않고 NoteRejected한다.
- 거짓 거절이 생기는지 확인했다.
  - `AIGoals.Goal` 객체는 `SetGoal`(`AIGoals.cs:231`)과 비우기(`:67`, `AIBrain.cs:309`)에서만 바뀐다. 둘 다 GoalRevision이 오르는 때다.
  - 같은 단계를 진행하는 동안에는 참조가 유지된다. 따라서 정상 답이 거절되는 경로는 없다.
- 리뷰의 장면을 다시 따라갔다.
  - 장면: 7/8에서 보고 → 8/8이 되어 `CheckDone`이 단계를 끝냄 → craft 시작 → 답 도착.
  - 결과: reported(gather) ≠ 지금(craft)이므로 거절된다. `step.stop`도 같은 줄에서 먼저 걸러진다.
  - 마지막 단계가 끝나 Goal이 비었을 때도 '단계 바뀜'으로 거절된다.
- 시험: `GoalCompletionTests`의 두 갈래가 craft count 1 유지와 Plan.Index 1을 확인한다.

### 2. 막음-2 — 고침
- 빌드가 아는 산출 품목을 후보에 싣는다.
  - `AIBrain.Candidates.cs:488-491`: 학습한 drop(`AIDrops.Items`, `AIDrops.cs:49`). 학습 전 나무 물체·RootCD는 Wood. 익은 음식원은 그 음식.
  - hungerFood = foodHunger && 익음.
  - break(`:519`)와 갇힌 물체로 파 들어가기(`:514`)에 실린다.
  - root: `:547`.
  - 묶음: 구성원 gives의 합집합(`AIGathering.cs:120-130`).
- Refine 쪽
  - `Core/AIFieldChoices.cs:68-84` Describe: gives → 묶음 resources → item/ProductOf 순으로 본다.
  - `:121`: progress 채집은 빼지 않는다. `:122`: 배고픔 음식은 남긴다.
  - `AIBrain.Loop.cs:1396-1413`이 이 경로를 실제로 쓴다.
- 장면별로 확인했다.
  - gather Wood 단계와 보조 목표 Wood: 나무 물체·RootCD 부수기의 products가 `[Wood]`이므로 Serves로 남는다.
  - 배고픔(음식 없음): 익은 식물은 hungerFood=true이고 products에 음식이 있어 남는다.
  - farm 씨앗 부수기(`AIFarm.cs:1015`, `progress = true`): 남는다.
    - 같은 엔티티에 브레인의 break(같은 key)도 함께 나온다. 이것은 farm 단계에서 빠진다. 어셈블러는 key로 중복을 지우지 않고(`Core/AICandidateAssembler.cs:216-222`) 값에 _2를 붙인다. 그래서 진척 쪽 하나가 남고 문제는 없다.

### 3. 고칠 것-1 — 고침
- 보이는 목표 대상을 직접 선택지로 남긴다.
  - `AIExpedition.cs:234-244`: gather·craft 단계에서 progress인 mine·break·root·pickup 중 가까운 3개는 목록으로 옮기지 않는다.
  - 원래 장면에서 구리 벽 하나는 이제 최상단 직접 선택지가 된다.
- 목록에만 남은 대상을 알린다.
  - `:249-260`: 목록 안에만 있는 보이는 gather 대상과 '보이는 채집 대상 X' 파기를 HiddenVisible로 모은다.
  - `AIFieldChoices.cs:135-143`: 그 품목이 목표이면 '펼치기'를 남기고 표시를 붙이며, 목표가 보이는 것으로 셈한다.
- 원래 장면의 거짓 사실이 사라졌다. "화면에 목표 없음"(`:145-153`)은 이제 붙지 않는다.
- 막힌(StandReachable 아님) 보이는 광석은 지연 경로로만 있다. 이 경우 '펼치기'가 남고 '목표 품목 X 보이는 대상 포함' 표시가 붙는다.

### 4. 고칠 것-2 — 일부
- mode+count는 고쳐졌다.
  - farm(`AIFarm.cs:513`)과 cook(`AICook.cs:382`)이 `AILife.ExplicitDone`(`AILife.cs:142-148`)을 쓴다. 판정은 `RequestSatisfied(ForGoal(g))`이다.
  - `farm plant 12`는 12/12에서 끝나고, 시작하자마자 끝나지 않는다. `cook put 8`도 같다.
  - 깨졌던 "??" 문구는 고쳐졌다(`AICook.cs:386`).
- mode는 있고 count가 없는 단계는 남았다. → 새 고칠 것 A.

### 5. 고칠 것-3 — 고침
- `AIBrain.Loop.cs:1373`: Refine을 조건 없이 적용한다.
- `AIBrain.Candidates.cs:163, :381`: prefetch 때도 `_wantedNow`·`_foodNeedNow`를 갱신한다.
- 부작용을 확인했다.
  - prefetch에는 기억 광석 직접 선택지가 여전히 없다(`:584 !prefetch`).
  - 그래서 '보이는 목표 없음 + 기억 광석만' 상황의 prefetch는 진척 후보가 0개다. 이때 `AIBrain.Loop.cs:787` 규칙(NeedsProgress && 진척 후보 없음 → prefetch 건너뜀)으로 묻지 않고, 동작이 끝난 뒤 정식 빌드로 묻는다.
  - 따라서 "탐험에 '목표 탐색 이동'만 붙고 기억 광석은 없는" prefetch 답이 생기지 않는다.

### 6. 고칠 것-4 — 고침
- `AIPlanner.cs:404-411`: InCombatNow이면 보류한다.
- `:441-454` TickPendingStop
  - 단계 참조가 바뀌면 버린다.
  - 전투가 끝나면 EndByStop을 부른다.
  - 부르는 곳은 `AIBrain.Loop.cs:153`, CheckDone 바로 뒤다. 그래서 전투 뒤에 측정 완료가 먼저 일어나면, 보류한 stop은 '단계 바뀜'으로 버려진다.
- reset(`AIPlanner.cs:161`)에서 함께 비운다.
- 쓰러짐(hp ≤ 0)에서는 틱이 일찍 return한다. 그 동안 보류는 유지되고 일어난 뒤 적용된다. 허용할 만하다.

---

## 새 발견

### 막음
없음.

### 고칠 것

**A. mode는 있고 count가 없는 farm·cook 단계는 끝날 길이 없다. 판단 모델 입력과 brief는 프로그램이 끝낸다고 말한다**
- 위치
  - `AIFarm.cs:513`, `AICook.cs:382`: mode가 있으면 `ExplicitDone`만 본다.
  - `AILife.cs:138`: `RequestSatisfied`는 `g.count > 0`을 요구한다. 그래서 count 없는 단계는 늘 false다.
  - `Core/AILifeTree.cs:129-146`: count 없는 mode 단계는 형식상 받는다.
    - farm hoe/plant/water/harvest
    - cook put/fill/take
    - cook wait: count를 아예 받지 않는다(`:145`).
  - Farm·Cook은 `Measured`(Feature)다(`Core/AIGoalKinds.cs:44-45`). 그래서 입력은 "완료 조건이 충족되면 프로그램이 완료함"이라고 쓴다(`AIBrain.Loop.cs:1576`).
  - brief.md:3·65는 "explore·follow 같은 측정 안 되는 단계"만 step.stop으로 끝낸다고 가르친다.
  - `AIGoals.CheckAfterBuild`(`:372-392`)는 막힘을 적어 둘 뿐 단계를 끝내지 않는다.
- 실패 장면
  1. 위쪽 LLM이 `[cook put 2, cook wait, cook take 2]` 계획을 세운다. 또는 `{"do":"farm","mode":"water"}`를 건다.
  2. wait 단계에서 요리가 끝나면 `cookwait` 선택지가 사라진다(`AICook.cs:632-637`).
  3. 단계는 끝나지 않고, 하위 모델에게는 request_plan·생활 메뉴 정도만 남는다.
  4. 위쪽 LLM은 '측정 단계는 자동으로 끝난다'고 배웠다. 그래서 정기 점검(약 2분)에서 keep할 수 있다.
- 회귀인 부분
  - 8ec4cd41 전에는 cook이 mode를 보지 않고 솥 관측(`CanCook == null`)으로 끝났다. 그래서 count 없는 cook 단계도 끝났다(정확하지는 않았다).
  - 지금은 끝나지 않는다.
  - farm 쪽은 1단계(finish_goal 제거)부터 같은 상태였다. 원래 검수가 'mode와 count가 있는 경우'만 짚어서 남았다.
- 고칠 방법(하나 고르기)
  - (가) count 없는 mode 단계는 측정 안 되는 단계로 본다.
    - 입력 문구를 그에 맞게 바꾼다(`Measured` 판단에 `count > 0` 조건).
    - brief의 step.stop 설명에 "count 없는 farm·cook mode(cook wait 포함)"를 넣는다.
  - (나) mode별 관측 완료를 정의한다.
    - wait: 자기 솥이 요리 중이 아님
    - take: 출력 칸이 빔
    - water·harvest: 대상 칸이 남지 않음
  - (가)가 작고 brief와 맞추기 쉽다. 어느 쪽이든 life 시험에 'count 없는 mode 단계' 한 건을 더한다.

### 작은 것

a. **'광맥당 하나'가 고른 칸의 이웃만 거른다**
- 위치: `AIBrain.Candidates.cs:583`
- 무엇이 일어나나
  - 이미 직접으로 만든 칸과 맞닿은 칸만 같은 광맥으로 본다. 1×3 광맥이면 1번째와 3번째 칸이 둘 다 직접 경로가 된다.
  - 가장 가까운 칸의 경로가 실패(plan==null)해도 그 칸은 슬롯을 쓰고 이웃을 막는다. 갈 수 있는 이웃 칸은 목록으로 밀린다.
  - 영향은 경로 계산 1회 더, 또는 직접 선택지 1개 덜이다.
- 고칠 방법: 기억 광석을 연결 성분(광맥)으로 묶은 뒤, 광맥마다 가장 가까운 칸 하나를 쓴다. 실패하면 같은 광맥의 다음 칸을 쓴다.

b. **브리지가 뺀 extra 항목은 위쪽 LLM에 알려지지 않는다**
- 위치: `protocol.mjs:123-138`
- 무엇이 일어나나: 게임이 거른 항목만 last_answer_rejected에 실린다. 브리지는 regex에 맞지 않는 형식(한글 별칭, 공백 등)을 조용히 뺀다. 이 한계는 review-fixes.md에도 적혀 있다.
- 고칠 방법: 브리지 로그에 남기거나, 뺀 항목을 다음 보고의 거절 사유에 합친다.

c. **보류된 step.stop이 보고에 보이지 않는다**
- 무엇이 일어나나
  - 근접전이 길면 그동안의 정기 보고에서 위쪽 LLM은 단계가 그대로 도는 것만 본다. 같은 stop을 다시 보내거나 keep+count를 줄 수 있다.
  - count는 적용되지만 보류된 stop이 결국 단계를 끝낸다.
  - 해는 작다.
- 고칠 방법: 보고 events에 '보류 중' 한 줄을 넣는다(지금은 AddEvent가 보류 시점에 한 번만 남는다).

d. **입력 다듬기의 MenuFree가 Refine과 조금 다르다**
- 위치: `Core/AIInputTrim.cs:16`, `Core/AIFieldChoices.cs:56`
- 무엇이 일어나나
  - AIInputTrim.MenuFree는 GoalKind.None을 포함하고, AIFieldChoices는 None에서 정리를 하지 않는다. 그래서 목표 없음일 때 생활·가방 메뉴 선택지는 나오지만 그 상태 줄은 빠진다.
  - 가방이 가득 차 Refine이 가방 메뉴 선택지를 남겨도, 다듬기는 '가방 선택 메뉴: 닫힘' 줄을 뺀다.
  - 선택지 설명만으로 고를 수 있어 해는 없다. 규칙을 한 곳에 두면 앞으로 어긋날 일이 줄어든다.

e. **보수 재료 판정이 열린 칸을 덜 셀 수 있다**
- 위치: `AIEdge.Guard.cs:489-490, :624`
- 무엇이 일어나나: 열린 칸 수는 `RepairBatches`·`Batches`에 들어간 칸만 센다. 묶음에 들지 못한 열린 칸(닿지 않는 칸 등)이 있으면, 블록이 실제로는 모자라도 재료 캐기가 나오지 않을 수 있다.
- 블록이 넉넉하면 미리 쌓아 두지 않는 것은 stage3 문서에 적힌 대로다.
- 실게임 로그 `EDGE materials: … not offered`로 확인하면 된다.

f. **보조 목표의 보이는 대상은 직접 선택지가 되지 않는다**
- 위치: `AIExpedition.cs:236`
- 무엇이 일어나나
  - goalDirect는 progress(주요 목표·재료)만 고른다. 보이는 보조 목표 광석은 목록 안에 남고, '펼치기'의 표시로만 알려진다.
  - explore·follow 단계에서는 기억한 보조 목표 광석도 직접 선택지가 아니다(작은 것 4의 결정).
  - design.md 표는 "기억한 주요·보조 목표 광석 → 직접 선택지"라고 적는다. 의도한 축소라면 design.md에 한 줄 남긴다.

## 3단계(f5d16072) 확인

- **보수 재료**
  - 판정: `Core/AIBaseGuard.cs:1372` `MaterialsShort(have, repairLeft + gapLeft)`
  - repairLeft는 `have == 0`일 때 batches를 비우기 전에 센다(`AIEdge.Guard.cs:490`, 비우기는 `:495`). 그래서 블록 0개에 깨진 곳이 있으면 '모자람'이 맞게 나온다.
  - 문은 블록 수에 넣지 않는다.
  - 막힘 문장도 모자랄 때만 붙는다(`:636`).
- **입력 다듬기** (`Core/AIInputTrim.cs`, `AIBrain.Loop.cs:1565-1612`)
  - 정비 줄
    - `IdleMaintenanceHead`는 `AIMaintenance.Report`(`AIMaintenance.cs:444`)에서 Pending·Repair·LastResult가 모두 없을 때의 머리와 글자까지 같다.
    - 그래서 pending·수리 중·최근 결과가 있으면 줄이 남는다.
  - 생활 줄
    - `AILife.Report`의 Result·Facts는 목표가 바뀌면 비워진다(`AILife.cs:36`).
    - 그래서 MenuFree 목표에서 빠지는 것은 닫힌 커서 정보와 목표 mode 칸뿐이다.
  - 가방 메뉴 줄
    - 닫힌 줄의 notice는 탐색 결과 문구다.
    - 상자 열림 모드는 path가 있어 `Active`가 참이다(`AIInventoryMenu.cs:63, :236-238`). 그래서 상자 모드에서는 줄이 남는다.
  - 원정 줄
    - 빈 칸 지우기는 하위 모델 입력(`PromptReport`)에만 적용된다.
    - 위쪽 LLM 보고(`Report`)는 그대로다. execution-tests TRACE에서 위쪽 보고에 '마지막 결과 ;' 등이 남은 것을 보았다.
  - 작업 관측·다시 판단하는 이유
    - 지우는 것은 '최근 실행 관측' 줄과 같은 내용뿐이다. 그 줄은 그대로 있어 정보가 사라지지 않는다.
  - 이 커밋은 전투 선택지·I3 문장을 건드리지 않았다(diff 확인).

## 브리지·게임 형식 일치
- step
  - 브리지(`protocol.mjs:112-121`): 모르는 키, stop:false, 잘못된 count만 뺀다. count+stop이거나 남은 게 없으면 step을 뺀다.
  - 게임(`Core/AIPlanProtocol.cs:104-109`): count는 ExplicitPositive로 읽고, stop은 bool true만 받는다.
  - 브리지를 거친 답이면 양쪽 해석이 같다.
- extra
  - 브리지: 모양이 틀린 항목은 빼고, 모두 틀리면 extra를 뺀다. `[]`는 그대로 둔다(given 0).
  - 게임: 모르는 품목·중복을 빼고, 모두 틀리면 바꾸지 않으며, `[]`는 지운다.
  - 기준이 같다.
- brief: step(:65), extra(:66), 보고 step·extra(:13-14)는 게임 동작과 맞는다. 예외는 위 고칠 것 A의 '측정 단계' 설명이다.

## 실게임에서 볼 것(설치 뒤)
- 직접 목표 대상 3개와 광맥 경로의 준비 시간(PREP/PERF)
- farm·cook mode+count 단계의 실제 완료
- 근접전 뒤 stop 적용
- `EDGE materials: not offered` 로그
- count 없는 cook/farm 단계(고칠 것 A를 고친 뒤)
