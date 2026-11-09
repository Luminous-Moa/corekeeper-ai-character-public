# 제련 검수 수정 — 고칠 것 2·작은 것 11 (2026-11-09)

- 검수: [review-smelting.md](review-smelting.md) (판정 '고친 뒤 합격')
- 설계·구현: [smelting.md](smelting.md) '공용 자원 반영'까지
- worktree `C:\Dev\CoreKeeperModSDK-celeris`, 브랜치 ai-celeris-decisions-20261108, 30c0093c 위 커밋 `c84feb4f`
- 게임 설치와 브리지 재기동은 하지 않았다. Unity가 바꾼 파일(AIPlayerSpike.asset, GameVersionRegistry.asset, ConditionsTable.asset(.meta), Packages/manifest.json·packages-lock.json, ProjectSettings.asset, .vsconfig, Assets/Plugins/CoreKeeper/)은 커밋하지 않았다.

## 고칠 것

### 고칠 것 1. 지원 안 함 레시피로 가공 중인 재료를 빼 오지 않음
- **바꾼 것**
  - `AISmelt.InputUse`(새로 만듦): 입력 칸 재료가 그 시설에서 무엇이 되는지를 게임 CanCraft(InventoryUtility.cs:1640-1700) 기준으로 본다. 시설의 모든 레시피를 보고, smelt가 지원하지 않는 레시피도 포함한다.
    - smelt 레시피를 먼저 본다.
    - 태그 레시피: 재료 프리팹의 `ObjectCategoryTagsCD`에 그 태그가 있으면 가공 대상이다. 1개만 있어도 된다(:1653-1670).
    - 그 밖의 재료 하나 레시피: 재료 × 레시피 수량(`CanCraftObjectsBuffer.amount`)이 있어야 1회분이다(:1672-1700).
  - `AISmeltRules.SmeltInputUse`(Output·Enough·Unsupported)를 새로 두었다. 판정 함수 `Stuck`·`ClearFirst`·`PutBlock`은 이것을 받는다.
    - '가공되지 않는 나머지'는 어느 레시피도 받지 않거나 1회분이 안 될 때만이다.
    - 지원 안 함 레시피로 가공 중이면 이렇게 적는다: `다른 재료 X n개가 가공 중(Glass 레시피 — smelt가 다루지 않는 레시피지만 시설은 가공함)`. 꺼내지 않고, 기다리거나 다른 시설을 쓴다.
- 목표 검사(CheckGoal), 선택지(Candidates), where 고르기(작은 것 7)가 모두 같은 판정을 쓴다.

### 고칠 것 2. 멈춘 시설에서 꺼내기가 끝없이 기다리던 것
- **진척 관측**
  - `SmeltWatch`와 `AISmeltRules.Watch`가 보이는 시설마다 진척을 본다. 입력·출력 내용이 바뀌거나 타이머가 움직이면 진척이다.
  - `InView`가 관측할 때마다 갱신한다(캐릭터별 상태).
  - 3초 넘게 안 보였다가 다시 보이면 처음부터 다시 잰다. 이유: 화면 밖에서는 지켜보지 않았고, 로드되지 않은 시설은 돌지 않았을 수 있다.
- **멈춤 기준:** `StallAfter(1개 시간) = 1개 시간 + max(5초, 1개 시간의 절반)`. 구리 주괴(4초)는 9초다. 피스 사이에 타이머가 한 틱 비는 것은 이 여유 안에 들어간다.
- **멈췄을 때 하는 일**
  - 기다리기 선택지는 남기되 `progress=false`로 낸다(진척 표시를 거둠). 그러면 다른 진척 선택지가 없을 때 단계가 정체 문구로 올라간다(AIGoals.CheckAfterBuild).
  - 이유 문구를 낸다: `station:…이(가) 멈췄을 수 있음 — 전기·자동화 조건 확인(입력 CopperOre 25개(CopperBar 5개분)가 10초째 그대로, 1개 약 4초; 가공 타이머가 보이지 않음 | 가공 타이머가 N초에서 멈춤) …`. 이 문구는 선택지 설명과 StallNote(정체 문구)에 나온다.
    - 예전 '타이머가 보이지 않음' 문구는 `res.Count == 0`에서만 나와 사실상 닿지 않았다. 지운 뒤 이 멈춤 문구 안으로 옮겼다.
  - 위쪽 LLM 보고: 멈춤마다 한 번, `AIPlanner.TickSmelt`(2초 주기)가 `AISmelt.TakeStallReport()`를 받아 `RequestReplan`(trigger action_replan, events에 이유)으로 보낸다.
    - 후보를 만드는 도중에는 보고를 보내지 않게 했다. 그래서 상태에 담아 두었다가 planner 틱에서 보낸다.
    - goal_stalled는 지금 코드에서 보내는 곳이 없다(brief에만 있음). 그래서 다른 기능과 같은 action_replan을 썼다.
  - 다시 움직이면 진척으로 돌아가고, 다음에 멈추면 다시 한 번 알린다.
- brief에 이 동작을 한 문장으로 넣었다.

## 작은 것

| # | 처리 |
|---|---|
| 1 | 진척을 GoalRevision이 아니라 단계 정체성(mode·item·해석된 where)으로 묶었다. `OnGoalSet`이 바로 앞 목표와 같은 smelt 단계이고, 끝나지 않았고, 진척이 있으면 넣은·꺼낸 양(과 먼저 꺼낸 것)을 이어서 센다(로그 `SMELT: the same step set again …`). 끝난 단계(GoalDone이 참)를 다시 받으면 새로 센다. 다른 단계가 사이에 끼면 새로 센다. 확인 반영(Confirmed)도 revision이나 같은 정체성이면 받는다. |
| 2 | 보낸 누름·이동을 스킬이 아니라 캐릭터 상태(`Sent`)에 둔다. 스킬이 확인 전에 끊기거나 확인이 빗나가도 남는다. 그 시설에서는 관측으로 확인되거나 `SentWindow`(10초, 스킬 확인 6초보다 김)가 지날 때까지 짧은 '확인 기다리기'(progress=true)만 내고, 새 누름·이동은 Stale로 막는다. 나중에 관측되면 그만큼 진척으로 친다. 끝까지 안 보이면 세지 않고 로그를 남긴다. 확인은 `AISmeltRules.PutObserved`로 예상치(그 누름이 옮길 양)와 관측치를 비교한다. 관측치는 둘 중 큰 쪽이다: 입력 증가(출력 증가분을 되돌려 셈), 또는 가방+근처 상자 재료 감소. 남이 그 사이 출력을 꺼내도 확인된다. 예상치를 넘지 않는다. take는 가방 증가를 요청량까지만 센다. count에 닿으면 더 꺼내는 선택지를 내지 않는다(예전에는 남은 양 0이 '제한 없음'으로 읽혔다). |
| 3 | `AICraft.CraftChests`에서 `e == station`을 뺀다. 제작대 craft에도 같은 함수가 쓰이며, 시설 자신은 재료 상자가 아니므로 무해하다. 시설 프리팹에 `InventoryAutoTransferEnabledCD`가 실제로 있는지는 여전히 모른다. |
| 4 | clear 확인을 단계 진척에 '먼저 꺼낸 것'으로 기록한다. put·take 완료 근거와 정체 문구 끝에 `먼저 꺼낸 TinBar 3개(가방): <보관 판단 근거>`를 붙인다. 보관 판단 근거는 AfterTake와 같다. 비우기 스킬의 완료 문구에도 같은 판단을 붙였다. |
| 5 | `TickSmelt`가 gear_alert처럼 smelt_alert를 ` / `로 이어 붙이고, events에는 같은 문장을 넣지 않는다(`AddEvent` 제거). brief에도 적었다. |
| 6 | memory.json 기록을 숫자 6개(`…, ready, alerted`)로 바꿨다. 읽기는 30c0093c의 5개 형식(alerted 없음 → 안 보냄으로 읽음)과 2026-11-08의 7개 형식을 모두 받는다. 형식은 기록당 숫자 개수로 구별한다. |
| 7 | where 없는 put의 Resolve: 레시피가 있는 아는 시설 중 버튼을 누를 수 있는 곳(또는 먼저 꺼내면 되는 곳)을 먼저 고른다. 기준은 보이면 지금 관측, 화면 밖이면 마지막 관측이고, 본 적 없는 시설은 열린 것으로 본다. 모두 막혔으면 가장 가까운 곳으로 정하고, 목표 검사는 '(그 레시피가 있는 다른 아는 시설도 지금 넣을 수 없음)'을 붙여 거절한다. |
| 8 | `OutputStops`: 가공될 입력인데 출력 칸에 다른 결과물이 있거나 한도면 `입력 칸의 다른 재료 … 출력이 막혀 멈춤 — 출력을 꺼내면 이어짐(출력 칸 CopperBar 3개라 TinBar이(가) 나올 자리 없음; smelt take로 꺼낸 뒤 …)`로 적는다. smelt_stations의 상태 문구도 `출력이 막혀 멈춤 — 출력을 꺼내면 이어짐(…)`이다. put 단계에서 출력을 자동으로 꺼내지는 않는다. 다음 선택(smelt take)은 위쪽 LLM에 맡긴다. |
| 9 | 화면 밖 시설의 입력 칸 한도를 `RememberedInputRoom`으로 센다. 마지막 관측 입력에 다른 재료가 있으면 0, 같은 재료면 9999 − 개수, 비었으면 9999다. |
| 10 | 준비 사슬(AIBrain.Prep)의 광석 부족을 `AISmelt.PrepMaterial`로 센다. 준비 문구가 가리키는 시설 기준으로 가방 + 그 시설 10칸 안 상자를 센다. 보이면 버튼이 쓰는 상자, 화면 밖이면 기억한 상자이고, 아는 시설이 없으면 가방만 센다. PutShortage와 같은 셈이다. |
| 11 | 아래 기준으로 처리했다. |

### 작은 것 11의 기준과 근거
- **바꾼 것**
  - `AIMoveEstimate.OpenWayFarLonger(open칸, dig칸, dig타격)`: 열린 우회로의 예상 시간이 파 들어가는 길의 2배 이상이고 5초(걷기 약 28칸) 이상 길면 '훨씬 길다'로 본다. 예상 시간은 표시에 쓰는 것과 같다(5.7칸/초, 타격당 0.5초).
  - `BreaksBesideOpenWay`가 이때 거짓을 돌려준다. 그래서 적·물체 옆·떨어진 아이템 쪽 파 들어가기 선택지가 남는다.
- **문구**
  - 남은 선택지는 `열린 우회로도 있으나 80칸·약 14초로 이 길(약 2초)보다 훨씬 김.`을 붙인다(`OpenWayNote`).
  - 아이템 쪽의 '열린 길 없음'은 열린 길이 없을 때만 쓴다.
- **근거**
  - 2배: 먼 길에서 열린 길이 조금 더 긴 정도로 물건을 부수는 쪽으로 바뀌지 않게 한다.
  - 5초: 몇 칸 차이는 따지지 않게 한다. 부수는 것이 남의 단지일 수 있고, 타격 수는 추정이다.
  - 예: 검수의 3칸+단지(약 1.5초) 대 80칸(약 14초)은 남는다. 5칸+단지 대 9칸은 빠진다(기존 F1 시험 그대로).
- **물러나기:** 바꾸지 않았다. RetreatSkill과 물러나기 선택지는 OpenPlanC(열린 길 우선)를 그대로 쓴다.

## 고친 파일
- `Assets/AIPlayerSpike/Scripts/AISmelt.cs`, `Core/AISmeltRules.cs`(고칠 것 1·2, 작은 것 1·2·4·6·7·8·9·10)
- `AIPlanner.cs`(TickSmelt: 멈춤 보고, smelt_alert 이어 붙임)
- `AICraft.cs`(작은 것 3), `AIBrain.Prep.cs`(작은 것 10)
- `AIBrain.Preparation.cs`·`AIBrain.Candidates.cs`·`Core/AIMoveEstimate.cs`(작은 것 11)
- `Tools/ai-bridge/brief.md`(smelt 멈춤·같은 단계 다시 set·지원 안 함 레시피 가공 중·출력 막힘·where 고르기·smelt_alert 이어 붙임)
- 시험: `Tools/core-tests/SmeltRulesTests.cs`, `Tools/life-smelt-tests/`(Boundary에 태그 컴포넌트·Mushroom/Clay/Brick), `Tools/life-craft-tests/`(Stubs에 PrepMaterial 경계), `Tools/path-preparation-tests/`(AIMoveEstimate.cs 링크, 대역 TunnelPlan에 totalHits)

## 시험
| 시험 | 결과 |
|---|---|
| gates `--unity` | 실패 0 — compile-check 오류 0, core 1769, exp-sim, recall-loop 41, bridge-wiring 23, protocol 206, fake-bridge, logshape, decisions-compare, movecheck, **unity-compile exit 0·error CS 0**. memgraph는 KG 데이터가 없어 건너뜀 |
| core-tests | ALL CHECKS OK (1769) — SM52~59 추가 |
| life-smelt | ALL 77 CHECKS OK (47 → 77; R1~R11, S1~S15 추가) |
| life-craft | 63, 실패 0 (작은 것 3·10 추가) |
| path-preparation | ALL SCHEDULER CHECKS OK (43) (작은 것 11 추가) |
| execution | 246/246 |
| life | 143, 실패 0 |
| action-input(`node run.mjs`) | 52 통과 |
| maintenance / gather / life-cook / life-inventory / inventory-menu / travel | 171 / 62 / 73 / 253 / 6 / 105, 모두 실패 0 |
| restriction-tests, travel navigator, candidate-progress | 원래 실패하던 것이라 돌리지 않음 |

### 새 시험이 보는 것
- **life-smelt**(실제 AISmelt.cs와 Core)
  - R1~R4(고칠 것 1): 음식 태그 재료(태그 레시피 Glass), Clay 8(2개짜리 Brick 1회분)은 가공 중이라 꺼내지 않는다. Clay 3과 Wood는 꺼낸다.
  - R5~R11(고칠 것 2): 시계를 움직여 10초 무변화면 기다리기의 progress가 거짓이 된다. 멈춤 문구에 '전기·자동화 조건 확인'과 '타이머가 보이지 않음'이 들어간다. 보고는 한 번만 간다. 다시 움직이면 진척으로 돌아간다. 같은 값에 멈춘 타이머는 멈춤으로, 도는 타이머는 진척으로 본다.
  - S1~S3(작은 것 1), S4~S8(작은 것 2): 끊긴 누름, 잃어버린 누름, 남이 출력을 꺼낸 경우, take 이동.
  - S9(4), S10(6), S11~S12(7), S13(8), S14(9), S15(10)
- **core SM52~59:** 같은 규칙을 함수 단위로 본다.
- **life-craft:** 시설 자신이 근처 상자에서 빠지는지, 준비 사슬이 상자까지 세는지(대역 경계).
- **path-preparation:** 실제 `AIBrain.Preparation.cs`로 80칸 우회 대 3칸+단지에서 파 들어가기가 남는지, 9칸 대 5칸에서 빠지는지, 물러나기가 열린 길을 쓰는지 본다.

## 확인하지 못한 것
- 실게임 동작 전부. 특히 다음을 보지 못했다.
  - 멈춘 시설(전기 없음·자동화 꺼짐)에서 실제로 타이머가 -1인지 멈춘 값인지, 멈춤 기준(1개 시간+5초 이상)이 실제 피스 간격과 맞는지.
  - action_replan 보고를 Gemini가 받아 단계를 바꾸는지.
- 작은 것 3: 시설 프리팹에 `InventoryAutoTransferEnabledCD`가 있는지는 여전히 모른다(빼는 쪽으로 처리만 함).
- 고칠 것 1의 태그 판정은 재료 프리팹의 `ObjectCategoryTagsCD`를 읽는다. 게임 CanCraft와 같은 컴포넌트지만 실제 에셋으로 확인하지 않았다. 여러 재료 레시피(재료 2개 이상, 태그 없음)는 입력 칸 하나로 성립하지 않는다고 보고 가공 대상으로 세지 않았다.
- 작은 것 2: 확인 창 10초가 지난 뒤 늦게 처리된 누름은 세지 않는다. 그 뒤 다시 누르면 1회분이 더 들어갈 수 있다(드문 경우로 보았다). take의 가방 증가 셈은 같은 품목을 그 사이 다른 경로로 얻으면 섞일 수 있다(요청량까지만 셈).
- 작은 것 7: 화면 밖 시설의 '막힘'은 마지막 관측 기준이다. 그 사이 끝났을 수 있다. 본 적 없는 시설은 열린 것으로 본다.
- brief의 goal_stalled 설명은 그대로 두었다. 지금 코드에는 이 trigger를 보내는 곳이 없어 보인다(grep 기준). 별도 확인이 필요하다.
