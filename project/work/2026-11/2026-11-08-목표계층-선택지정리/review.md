# 목표 계층·선택지 정리 — 독립 검수 (2026-11-08)

대상: 브랜치 ai-celeris-decisions-20261108, `git diff fb174553..1b5589ad` (2bc81cbb, 61b8abce, 1b5589ad). worktree `C:\Dev\CoreKeeperModSDK-celeris`.
기준: [design.md](design.md). 보고서 [stage1.md](stage1.md), [stage2.md](stage2.md), [../2026-11-08-코드완료판정.md](../2026-11-08-코드완료판정.md)는 확인할 주장으로만 읽었다.
코드는 바꾸지 않았다. Unity와 게임은 실행하지 않았다.

## 판정: **고친 뒤 합격**

설계의 큰 줄기는 코드에 들어가 있다: 측정 단계 자동 완료, finish_goal 제거, step·extra 프로토콜, 하위 선택지 정리.
그러나 실게임의 주 흐름을 깨는 결함이 2건(막음) 있다. 계획 단계를 넘어가는 순간에 step이 엉뚱한 단계에 적용되고, 부수기 선택지가 품목을 잘못 읽혀 목표용·음식용까지 빠진다.
이 2건과 고칠 것 1~4를 고친 뒤, 고친 부분만 다시 검수하고 설치하기를 권한다.

## 돌려 본 시험 (이 검수에서 직접 실행)
- `bash Tools/core-tests/gates.sh` (--unity 없이): 실패 0. compile-check OK, core-tests 1618, protocol 192, bridge-wiring 23, recall-loop 41, exp-sim·fake-bridge·logshape·decisions-compare·movecheck OK. memgraph는 KG 데이터가 없어 건너뜀.
- execution-tests 233/233, life-tests 136, maintenance-tests 171.
- 보고서에 적힌 시험 수는 실제와 맞다. 다만 아래 막음·고칠 것 항목은 어느 시험도 다루지 않는다.
  - FC 시험은 `products`를 손으로 넣는다. 그래서 Unity 쪽 매핑(`RefineForGoal`)을 검사하지 못한다.
  - step 시험은 `ApplyStep`을 직접 부른다. 그래서 답이 오는 사이 계획이 넘어가는 경우를 검사하지 못한다.
  - life-tests의 `GoalDone` 스텁은 늘 false를 돌려준다. 그래서 farm·cook 자동 완료를 검사하지 못한다.

---

## 막음

### 막음-1. 답이 오는 동안 계획이 다음 단계로 넘어가면 step.count·step.stop이 그 다음 단계에 적용된다
- 위치
  - `Assets/AIPlayerSpike/Scripts/AIPlanner.cs:718`: keep·ask이면 `ApplyStep` 호출
  - `AIPlanner.cs:384-421` `ApplyStep`: 현재 `AIGoals.Goal`에 적용
  - `Core/AIPlanRules.cs:47` `RevStale`: 같은 계획 출처면 오래된 답으로 보지 않음
  - `AIPlanner.cs:893` `CheckReason`
- 실패 장면
  1. 계획이 `[gather CopperOre 8, craft CopperHelm 1]`이다. 정기 점검 보고는 `step: {acquired 7, count 8, close: true}`를 보낸다. `close` 표시가 바로 이 순간에 판단하라고 위쪽 LLM을 부른다.
  2. 위쪽 LLM은 `keep + step.count 20`으로 답한다. 답은 Gemini에서 12~15초, 기억 검색이 있으면 더 걸린다.
  3. 그 사이 8번째 광석이 들어와 `CheckDone`이 1단계를 끝내고, 2단계 craft가 시작된다.
  4. 답이 도착한다. `RevStale`은 같은 계획(`_reqPlanSource == S.Plan.Source`)이라 오래된 답이 아니라고 본다. 그래서 `ApplyStep(count 20)`이 **craft 단계**에 적용된다.
  5. `CountCheck`는 통과한다: craft는 셀 수 있는 단계이고, 확보량 0 ≤ 20이며, max_count가 없다. 결국 `Goal.count`와 `Plan.Current.Count`가 20이 되어 투구 20개를 만들려 하고 광석을 다 쓴다.
- `step.stop`이면 피해가 더 크다. 6/8에서 "충분하니 멈춤"이라고 답했는데 그 사이 8/8로 완료되면, 시작만 한 craft 단계가 `goal_done`(completed)으로 끝난다. 계획은 3단계로 넘어가거나 plan_done을 보고하고, 실제로는 만들지 않은 투구가 완료로 셈해진다.
- 호스트 발화(host_said)라면 오래된 keep도 `OnStale`이 적용하므로, 그 사이 걸린 다른 호스트 목표에도 같은 일이 생긴다.
- 고칠 방법
  - `ApplyStep`에 "보고 때의 단계와 지금 단계가 같은가"를 넣는다. 예: `AIBrain.GoalRevision == _reqRev`를 확인하거나, 보고 때 `Goal` 참조나 계획 index를 저장해 비교한다.
  - 다르면 적용하지 말고 `NoteRejected("보고 뒤 단계가 바뀌어 step 적용 안 함: …")`를 남긴다.
  - extra는 단계와 무관하니 그대로 적용해도 된다.
  - 시험: `Apply` 경로에서 보고 → 단계 완료·다음 단계 시작 → keep+step.count / step.stop 답 순서로 둘 다 거절되는지 확인한다.

### 막음-2. 부수기 선택지가 '나오는 것'이 아니라 '물체 자신'의 ID로 판정된다 — 나무·음식·씨앗을 주는 부수기가 목표가 있으면 모두 빠진다
- 위치
  - `AIBrain.Loop.cs:1393-1403`: `RefineForGoal`의 품목 매핑. resources가 없으면 `c.itemId`, 그다음 `ProductOf`
  - `AIGathering.cs:89-94, 104`: break 대상의 `Item = data.objectID`, 즉 물체 자신. `candidate.itemId = target.Item`
  - `AIGathering.cs:39-47`: `ProductOf`는 break이면 그냥 `itemId`
  - `AIBrain.Candidates.cs:515`: break 후보에 산출 품목이 없음. 빌드는 `forGoal`·`foodGoal`·`foodHunger`를 이미 알고 있으면서 버린다
  - `AIFarm.cs:1013`: 씨앗 얻으려 부수기
  - `Core/AIFieldChoices.cs:96`: 무관 채집으로 Drop
- 실패 장면
  - **나무**: gather Wood 단계, 보조 목표 Wood(brief.md에 적힌 예 그대로), 나무 재료가 모자란 craft 단계가 모두 해당한다. 빌드는 Root 물체(RootCD)·나무 물체를 `forGoal`(나무 나옴)로 만든다. 그런데 Refine에서는 products가 `[Root 물체 ID]`라서 Wood와 맞지 않아 빠진다. 남는 건 큰 뿌리 타일(`root`)뿐이다. 나무 단계가 크게 느려지거나 '진척 후보 없음'으로 막힌다.
  - **배고픔**: 가방에 음식이 없고 배고픔이 50 미만이다. 빌드는 익은 식물(HeartBerryPlant 등)을 `foodHunger`로 내놓는다. 그런데 물체 ID는 먹을 수 없으니 `HungerAdd`가 0이고 `food=false`라서 빠진다. 설계의 "배고픔 시 음식 남김"이 실제로는 지켜지지 않는다. FC 시험은 `food=true`를 손으로 넣어 통과했다.
  - **농사**: 씨앗 없는 farm plant 단계에서 진척 길은 `AIFarm`의 "씨앗 얻으려 부수기"(progress=true)뿐이다. 이것이 빠져 선택지가 남지 않는다. request_plan만 남는다.
  - **요리 재료**: cook 단계의 `foodGoal` 부수기도 같은 이유로 빠진다.
- 고칠 방법
  - 빌드가 아는 산출 품목을 후보에 실어 Refine이 그것을 쓰게 한다. 예: `AICandidate`에 `products` 필드를 두고, 학습한 drop(`AIDrops.Gave`), 나무 물체·RootCD면 Wood, `AIFoodSource.food`를 넣는다.
  - 최소한 `c.progress == true`인 채집 후보, 그리고 `foodHunger`인 후보는 Refine에서 빼지 않는다.
  - gather_group의 resources(`item@x,y`)도 break 묶음이면 물체 ID이니 같은 방식으로 고친다.
  - 시험: RefineForGoal 매핑 단위 시험. 나무 물체 break + gather Wood → 남음, 익은 식물 break + foodNeed → 남음, farm 씨앗 부수기 → 남음.

---

## 고칠 것

### 고칠 것-1. 기억한 목표 광석 직접 선택지가 있으면 '채집·이동 목록 펼치기'가 빠져서, 화면에 보이는 목표 광석 벽을 고를 길이 없어진다
- 위치
  - `Core/AIFieldChoices.cs:104, 107`: `directGoal`에 화면 밖 기억 광석 dig도 셈한다
  - `AIExpedition.cs:230`: 보이는 단일 `mine`, 보이는 광석까지 파 들어가는 `dig`는 목록 안(browseable)으로 옮겨진다
  - `AIMemory.RememberedOres`: 보이는 것은 뺀다
  - `AIGathering.cs:110`: 2개 이상 붙어 있어야 묶음이 된다
- 실패 장면
  - 구리 벽 하나(또는 떨어진 두 개)가 화면에 보이고, 기억한 구리 광석이 화면 밖에 있다. 위쪽 최상단에는 기억 광석 dig만 남고 '목록 펼치기'는 빠진다. 바로 옆 구리 벽은 목록 안에만 있어 고를 수 없다. 그래서 먼 기억 광석 쪽으로 간다.
  - 탐험 선택지에는 `goalInView=false` 때문에 "화면에 목표 CopperOre 없음"이라는 틀린 사실이 붙는다.
  - 막히지는 않지만 주 흐름(CopperOre 8)이 눈에 띄게 비효율이 되고, 모델에게 거짓 사실을 준다.
- 고칠 방법(둘 중 하나)
  - (가) 이번 빌드 catalog(`query`)에 목표 품목의 gather 대상이 있으면 '목록 펼치기'를 남기고, `goalInView`도 catalog의 보이는 대상을 셈한다.
  - (나) 보이는 목표 품목 mine·dig를 browseable에서 빼서 최상단 직접 선택지로 둔다. 설계의 "숨은 하위 목록 대신 직접 선택지"와 맞는 쪽은 이것이다.

### 고칠 것-2. 요리(cook)·농사(farm)에 mode와 count를 준 단계가 프로그램 완료와 맞지 않는다 — 농사는 끝나지 않고, 요리는 엉뚱한 때 끝난다
- 위치
  - `AIFarm.cs:511`: mode가 있으면 언제나 false("finish is an AI choice")
  - `AICook.cs:376-384`: mode·count를 보지 않고 '요리할 짝이 없음' 같은 관리 관측으로 완료
  - `AIGoals.cs:300` `CheckDone`: Feature이면 측정 단계로 자동 완료
  - `AILife.cs:198`: "명시 요청량의 확인된 작업 충족; 프로그램이 이 단계를 완료함"
- 실패 장면
  - `farm plant 12` 단계에서 12칸을 심어 `AILife.RequestSatisfied`가 참이 되어도 끝나지 않는다. 같은 목표의 생활 선택지는 사라지고(`AILife.cs:172`), finish_goal도 없다. 모델에게는 "프로그램이 완료함"이라는 거짓 사실이 보인다. 계획은 정기 점검(약 2분) 때 위쪽 LLM이 step.stop을 고르기 전까지 멈춘다. brief는 측정 단계는 자동 완료된다고 가르치니, 그 step.stop도 기대하기 어렵다.
  - `cook put 8` 단계는 넣고 나면 솥이 바쁘거나 출력이 있어 끝나지 않는다. 반대로 가방에 짝이 없고 솥이 비어 있으면, 시작하자마자 "완료"로 끝난다.
  - 이전에는 모델 선택이 이 엉성한 관측을 걸렀는데, 지금은 그대로 완료가 된다. `AICook.cs:382`의 완료 문구는 `??`로 깨져 있다(기존 문제). 그 깨진 문구가 이제 goal_done 근거로 위쪽 LLM에 간다.
- 고칠 방법
  - mode와 count가 있는 farm·cook은 `AIMaintenance.GoalDone`처럼 `AILife.RequestSatisfied(AILife.ForGoal(Goal))`로 완료를 판정한다.
  - mode가 없는 cook의 관리 관측은 자동 완료에서 빼거나(측정 아님), 조건을 다시 정의한다.
  - life 시험에 farm·cook 자동 완료와 즉시 완료 금지 시험을 더한다.

### 고칠 것-3. prefetch 빌드에는 정리가 적용되지 않는다 — 뺀 선택지가 '곧 끝남' 질문으로 계속 나온다
- 위치: `AIBrain.Loop.cs:1371` (`if (!prefetch) candidates = RefineForGoal(...)`). `_wantedNow`·`_foodNeedNow`도 prefetch에서는 갱신하지 않는다.
- 실패 장면
  - 캐기·걷기 같은 실행이 0.4초 안에 끝날 때마다 prefetch로 묻는다. 이때는 무관 채집(버섯·흙벽), 기다리기, 생활·인벤토리 메뉴, 목록 펼치기가 그대로 나온다. 고른 답은 다음 행동으로 적용된다.
  - 실행마다 끝에 prefetch가 붙는 채집 흐름에서는 정리 효과가 크게 줄어든다. stage2 재생은 prefetch를 구분하지 않아 이 효과를 재지 못했다.
- 고칠 방법: prefetch에도 같은 Refine을 적용하고 wanted·foodNeed를 갱신한다. 제외해야 할 이유가 있으면 주석과 시험으로 근거를 남긴다.

### 고칠 것-4. step.stop은 근접전 중에도 바로 몸을 멈추고 다음 단계를 시작한다
- 위치: `AIPlanner.cs:390-394` → `AIGoals.EndGoal` → `AIBody.Stop`(`AIGoals.cs:433`), 그리고 `CancelPendingChoices`가 근접전 판단 요청을 버린다.
- 실패 장면: 정기 점검의 stop이 물러나기·휘두르기 중에 오면 그 동작이 끊기고, 다음 단계(제작대로 걷기 등)가 시작된다. 측정 단계 자동 완료는 근접전 중 미루는데(2bc81cbb), 위쪽 LLM의 stop만 그 보호를 받지 않는다.
- 고칠 방법: `InCombatNow`이면 stop을 "보류됨"으로 두었다가 전투가 끝난 뒤 적용한다. CheckDone과 같은 규칙이다. 아니면 목표만 끝내고 몸 동작은 멈추지 않는다.

---

## 작은 것

1. **보조 목표 이름을 정규화하지 않는다**
   - 위치: `AIPlanner.cs:167` `ApplyExtra`, `AIExtraGoals.Valid`
   - 무엇이 일어나나: `TryItemPublic`은 `copper`·`copperore`·`구리` 같은 별칭과 대소문자 무시를 받아 주는데, 원래 문자열 그대로 저장한다. 그 뒤 `Serves`·`extraItems.Contains(r.o.ore.ToString())`는 ObjectID 이름으로 정확히 비교하므로, `"copperore"` 보조 목표는 have만 셀 뿐 꼬리표·채집 유지·기억 광석 직접 선택지가 모두 안 된다. 브리지 정규식은 영문 별칭을 통과시킨다.
   - 고칠 방법: 받을 때 `ObjectID.ToString()`으로 바꿔 저장한다.
2. **step·extra 거절 사유가 덮어쓰기로 사라진다**
   - 위치: `AIPlanner.cs:700, 750, 758`
   - 무엇이 일어나나: set이나 거절 경로에서 `_rejected = ...`로 덮어써서, 앞서 `NoteRejected`로 남긴 "set의 step 적용 안 함"과 "extra에서 뺀 항목"이 사라진다.
   - 고칠 방법: 덮어쓰지 말고 이어 붙인다.
3. **브리지가 형식 하나 틀린 답 전체를 버린다**
   - 위치: `Tools/ai-bridge/protocol.mjs:864-886`
   - 무엇이 일어나나: `step: {"count":20,"stop":false}`나 extra 한 항목의 형식 오류만으로 답 전체(set·say·order 포함)가 parse 오류로 버려지고 다시 묻는다. 게임 쪽은 잘못된 extra 항목만 빼므로 기준이 다르다.
   - 고칠 방법: `stop:false`는 무시하고, 잘못된 extra 항목은 빼고 받게 맞춘다. 아니면 지금처럼 두되 brief에 명시한다.
4. **기억 광석 직접 경로 3개를 빌드마다 계산한다**
   - 위치: `AIBrain.Candidates.cs:574-583`
   - 무엇이 일어나나: 가까운 3곳은 같은 광맥의 이웃 칸인 경우가 많아 사실상 같은 경로를 세 번 계산한다. PlanC 캐시는 5초(전투 2초)이고 이동 거리에도 묶여 있어, 걷는 중에는 빌드마다 다시 계산된다. follow·explore 단계에서도 보조 목표 광석이면 계산한다.
   - 고칠 방법: 광맥(붙은 칸)마다 하나로 줄이고, 실게임 PERF·PREP로 시간을 확인한다.
5. **가방이 가득 차면 gather 단계에서 비울 길이 없다**
   - 위치: `AIFieldChoices.cs:51` `MenuFree`
   - 무엇이 일어나나: gather·goto·home 단계에서 인벤토리 메뉴를 빼므로, 가방이 차서 줍지 못할 때(`AIBrain.Candidates.cs` "가방을 비우면 됨") 하위 모델이 비울 길이 없다. request_plan만 남는다.
   - 고칠 방법: 가방이 가득 찼다는 관측이 있으면 메뉴를 남긴다.
6. **측정 안 되는 단계는 위쪽 LLM이 없으면 끝나지 않는다**
   - 무엇이 일어나나: explore·follow는 위쪽 LLM의 step.stop이나 set으로만 끝난다(설계대로). 브리지가 꺼졌거나 사용량 한도로 쉬는 동안에는 끝낼 길이 없다. 탐색할 곳이 없으면 기다리기나 request_plan만 되풀이한다.
   - 할 일: 의도한 동작이라면 문서에 남긴다.
7. **보조 목표는 재시작하면 사라진다**
   - 무엇이 일어나나: 재시작 때 저장하지 않는다(보고서에 적혀 있고 계획과 같음).
   - 할 일: 위쪽 LLM은 다음 보고의 `extra: []`로 이를 알게 되니 큰 문제는 아니다.
8. **brief가 만들어지지 않는 trigger를 설명한다**
   - 위치: brief.md:10
   - 무엇이 일어나나: brief는 `explore_ended`(탐험할 곳 없음)를 trigger로 설명하지만 이를 만드는 코드는 없다. 이번 변경 전부터 그랬다. 이제 explore가 끝나는 길이 step.stop뿐이니 문장을 맞추는 것이 좋다.

## 확인한 것(문제 없음)
- **finish_goal**
  - 어떤 목표에도 나오지 않는다(`AIBrain.Candidates.cs:25-31`, life 시험 7종).
  - 하위 모델에게 "완료를 고르라"는 문구는 거절 경로 안내(`AIGoals.cs:425`)뿐이며, 그 문구도 "고를 필요 없음"이다.
  - 작업 관측 줄·수량 관측·원정 줄은 모두 새 계약 문구로 바뀌었다. 예외는 위 고칠 것-2의 AILife 문구다.
- **자동 완료 경로**
  - `CheckDone`은 목표 하나에 한 번만 `EndGoal`을 부른다. 뒤이어 Goal이 비므로 같은 프레임의 step.stop은 "지금 단계 없음"으로 거절된다.
  - 끝날 때 하위 모델의 진행 중 요청과 준비는 `ClearGoal`→`CancelPendingChoices`로 버려진다.
  - 근접전 보류는 `_inCombat`을 보는데, 이 값은 요청이 날아가는 동안 갱신되지 않아 최대 답 한 번만큼 늦게 풀린다. 그래도 요청과 요청 사이에 감시가 갱신하므로 영영 풀리지 않는 경우는 찾지 못했다.
- **계획 마지막 단계가 끝날 때 오는 답**
  - 정기 보고가 날아가는 동안 마지막 단계가 끝나면, `OnGoalEnded`가 goal_done을 보내지 않고 '빚진' 보고로 남긴다.
  - 그 답이 keep이면 그 보고를 다시 보내고, 답의 step은 "지금 단계 없음"으로 거절된다.
  - 계획 없이 하나뿐인 목표라면 같은 상황의 답은 오래된 답으로 버려진다.
- **step.count 검사**
  - gather·craft만 받는다. 양의 정수만 받는다. 이미 확보한 양보다 작게는 못 한다. 같으면 다음 0.5초 안에 완료된다.
  - 호스트가 정한 max_count를 넘지 못한다.
  - 계획의 현재 단계 count도 함께 바뀐다.
- **보조 목표**
  - 가방 기준이고 filled는 한 번 닿으면 유지된다. Replace는 같은 item·count면 filled를 유지한다.
  - 보고의 `step`·`extra` 필드는 brief 설명과 이름·뜻이 맞는다.
- **프로토콜**
  - 게임 파서(`AIPlanProtocol.cs:104-121`)와 브리지 `shape()`의 필드 이름이 같다.
  - step은 keep·ask에만 쓰고, set·hold의 step은 거절 사유로 남긴다.
  - `plan.mjs` 로그에도 step·extra가 남는다.
- **효과 없는 계속하기 제거**: 멈춘 탐색의 '제자리에 서 있기', 답 전에 끝나는 근접전 짧은 행동의 계속하기를 지웠다. `lCont`를 비운 뒤 `lBrute`에서 빼므로 다른 목록으로 다시 들어오지 않는다.
- **문·기지 둘레·전투·먹기·제어**: kind가 채집 종류나 메뉴가 아니므로 Refine이 건드리지 않는다.
- **목록이 이미 펼쳐졌을 때**: 생활 화면이 열렸거나 가방 메뉴가 작동 중이면 아무것도 빼지 않는다.
