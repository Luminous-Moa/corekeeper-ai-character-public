# 1~2단계 독립 검수 반영 (2026-11-08)

- 근거: [review.md](review.md) (판정 '고친 뒤 합격'). 기반 f5d16072(3단계 비전투) 위.
- 커밋 **8ec4cd41**: 브랜치 ai-celeris-decisions-20261108, worktree `C:\Dev\CoreKeeperModSDK-celeris`. 설치·병합·브리지 재기동은 하지 않았다.
- 전투 최적화는 미룬 그대로다. 이번 범위는 고칠 것-4의 근접전 보류뿐이다.

## 항목별

| 항목 | 상태 | 바뀐 곳 | 시험 |
|---|---|---|---|
| 막음-1: step 답이 다음 단계에 적용됨 | 고침 | 보고 때 단계를 `S._reqGoal`(AIGoal 참조)에 저장(`AIPlanner.cs` Send). `ApplyStep(..., reported, bound)` `AIPlanner.cs:392`에서 단계가 다르면 적용 안 하고 NoteRejected("보고 뒤 단계가 바뀌어 적용하지 않음 …"). keep·ask 답 경로가 `S._reqGoal, true`로 부름 | execution GoalCompletionTests: 보고(7/8) → 완료 → craft 시작 → step.count 20 / step.stop 둘 다 "refused: step changed"이고 craft count 1 유지. 같은 단계면 적용. Send·Apply 연결은 소스로 확인 |
| 막음-2: 부수기를 물체 id로 판정 | 고침 | `AICandidate.gives`·`hungerFood`(`AIDecisions.cs`). break는 학습한 drop(`AIDrops.Items`, `AIDrops.cs`), 나무 물체(학습 전)는 Wood, 익은 음식원은 그 음식(`AIBrain.Candidates.cs` brk 튜플, :514·:519). root는 Wood 또는 학습 drop(:547). 묶음은 구성원 gives의 합집합(`AIGathering.cs:122-130`). Refine 쪽 `AIFieldChoices.Describe` `Core/AIFieldChoices.cs:68`는 gives → 묶음 resources → item 순으로 본다. progress 채집은 빼지 않는다(:121). RefineForGoal은 Describe를 쓴다(`AIBrain.Loop.cs:1412`) | core FC 막음-2 4개: Wood 단계·Wood 보조 목표에서 나무 물체·뿌리 남음, 배고픔 음식 남음, farm 씨앗 부수기 남음. gather-tests: 실제 AIGathering이 break 묶음에 gives·hungerFood를 싣는다. 배선은 소스로 확인 |
| 고칠 것-1: 보이는 목표 광석이 목록 안에만 있음 | 고침 | gather·craft 단계의 progress 대상 중 가까운 3개는 direct로 남김(`AIExpedition.cs:234-244`). 목록에만 남은 보이는 대상의 품목은 `HiddenVisible`(gather 잎과 "보이는 채집 대상 X" 파기)에 모음. Refine에서 목표 품목이 거기 있으면 '펼치기'를 남기고 "· 목표 품목 X 보이는 대상 포함"을 붙이며, 목표가 보이는 것으로 셈해 '화면에 목표 없음'을 쓰지 않음(:138, :143). 보이는 목표 광석으로 파 들어가기도 goalTarget으로 보는 것으로 셈함 | core FC 고칠 것-1 2개 |
| 고칠 것-2: farm·cook 완료 판정 | 고침 | `AILife.ExplicitDone`(`AILife.cs:142`, RequestSatisfied(ForGoal)). mode 있는 farm(`AIFarm.cs:513`)과 cook(`AICook.cs:382`)이 이것으로 완료. mode 없는 cook은 솥 관리 관측을 유지하고 깨진 "??" 문구를 고침 | life-tests: farm plant 6이 4/6에서는 미완료이고 6/6에서 CheckDone이 완료(GoalDone 스텁이 실제 AIFarm.GoalDone으로 감). life-cook-tests: cook put 8은 시작 때 미완료, 충족 때 완료하고 문구를 읽을 수 있음. mode 없는 cook은 명시 판정을 부르지 않음 |
| 고칠 것-3: prefetch 미정리 | 고침 | `AIBrain.Loop.cs:1373` 무조건 RefineForGoal. `_wantedNow`·`_foodNeedNow`도 prefetch 때 갱신(`AIBrain.Candidates.cs:163, :381`) | core FC review wiring(소스) |
| 고칠 것-4: 근접전 중 step.stop | 고침 | `InCombatNow`이면 `S._pendingStop`에 보류(`AIPlanner.cs:392~`). `TickPendingStop`(:441)은 매 틱 CheckDone 옆(`AIBrain.Loop.cs:153`)에서 불리며, 전투가 끝나면 적용하고 단계가 바뀌면 버림 | execution: 싸움 중 보류·휘두름 유지 → 끝나면 적용·다음 단계 / 보류 중 단계가 바뀌면 버림 |
| 작은 것 1: 보조 목표 별칭 | 고침 | `AIExtraGoalList.Valid(…, canonical)`(`Core/AIExtraGoals.cs`)로 ObjectID 이름을 저장하고 매핑 뒤 중복을 판정. `ApplyExtra` `AIPlanner.cs:169` | core EG 작은 것 1 |
| 작은 것 2: 거절 사유 덮어쓰기 | 고침 | `AppendRejected` `AIPlanner.cs:457`, 덮어쓰던 3곳을 이어 붙임 | execution 작은 것 2(함수와 소스) |
| 작은 것 3: 브리지가 답 전체를 버림 | 고침 | `protocol.mjs` shape: step은 모르는 키·stop:false·잘못된 count만 빼고, count+stop이거나 남은 게 없으면 step만 뺌. extra는 모양이 틀린 항목만 빼고, 모두 틀리면 extra를 빼서(바꾸지 않음) 지우지 않음. 게임도 같다: 모두 잘못이면 바꾸지 않음(ApplyExtra) | protocol.test 22건 표 + 'set·say를 버리지 않음' 1건(193 통과) |
| 작은 것 4: 기억 광석 경로 비용 | 고침 | 직접 경로는 gather·craft 단계에서만, 광맥(붙은 칸)당 하나(`AIBrain.Candidates.cs:582-584`) | core FC 작은 것 4(소스). 재생 아래 |
| 작은 것 5: 가방 가득 참 | 고침 | `FieldGoal.bagFull`(`!AIBody.BagHasRoomFor(목표 품목)`)이면 메뉴 없는 목표에서도 인벤토리 메뉴를 남김(`Core/AIFieldChoices.cs:126`). 보관·버리기는 그 메뉴 안에 있음 | core FC 작은 것 5 |
| 작은 것 6: 측정 안 되는 단계 | 문서 | brief: explore·follow 같은 측정 안 되는 단계는 step.stop이나 새 set으로만 끝나고, 현장 판단기는 끝내지 못함. 위쪽 LLM이 쉬는 동안에는 끝나지 않는다(설계대로). | — |
| 작은 것 7: 재시작 시 보조 목표 | 문서 | brief: 다시 시작하면 보조 목표는 비어 있음(보고의 extra: []) | — |
| 작은 것 8: explore_ended | 고침 | brief에서 trigger 설명과 규칙의 explore_ended를 지움 | — |

추가 문서(brief): step이 보고 때 단계에 묶인다는 것, 근접전 중 stop은 싸움 뒤 적용된다는 것, 잘못된 extra 항목만 빠진다는 것.

## 확인
- gates --unity 실패 0: core 1638, protocol 193, bridge-wiring 23, Unity CS 0.
- execution 241, life 138, life-cook 73, gather 62, maintenance 171, travel 105, life-craft 49, life-inventory 253, inventory-menu 6.
- 재생(Celeris 2회, T 입력 다듬기 + 광맥당 하나 V; `replay/s3/out/*-TV`):
  - 구리 갇힘 66건은 기억한 목표 광석 132/132(2단계 131/132)이고, 선택지는 평균 12.3개.
  - 멈춘 탐색 82건은 탐험 67, 목표 광석 58, 공격 36, 따라가기 2, 중단 1이다. 2단계(64/58/40)와 같은 모양이고 서 있기는 0.

## 한계
- 재생은 옛 기록 글에 규칙을 적용한 근사다. gives·숨은 목록 내용은 기록에 없어서 막음-2와 고칠 것-1의 효과는 단위 시험으로만 확인했다.
- 실게임은 확인하지 않았다. 확인할 것:
  - 직접 목표 대상 3개와 광맥당 경로 1개의 준비 시간(PREP/PERF)
  - farm·cook 명시 단계의 실제 완료
  - 근접전 뒤 stop 적용
- 브리지가 모양 오류로 뺀 extra 항목은 게임에 알려지지 않는다. 게임이 거른 항목만 last_answer_rejected에 실린다.

## 재검수 반영 (re-review.md, 2026-11-08)

- 근거: [re-review.md](re-review.md) (판정 '고친 뒤 합격'). 기반 8ec4cd41.
- 커밋 **42503a6a**. 설치·병합·브리지 재기동은 하지 않았다.

| 항목 | 상태 | 바뀐 곳 | 시험 |
|---|---|---|---|
| A: count 없는 farm·cook mode 단계(cook wait은 늘 이 경우)가 끝날 길 없음 | 고침 | 아래 3곳 | 아래 |
| f: 보조 목표의 보이는 대상이 직접 선택지가 아님 | 고침 | 아래 3곳 | 아래 |
| a: 광맥당 하나가 이웃 칸만 봄, 실패 경로가 광맥을 막음 | 고침 | 아래 2곳 | 아래 |

**A — 바뀐 곳**
- 측정 안 되는 단계로 본다(가). `AIGoals.CountlessLifeMode`·`MeasuredStep`이 `AIGoals.cs:330-332`에 있고, `CheckDone`이 이를 쓴다(:300).
- 판단 모델 입력은 이런 단계에 "측정 완료 조건이 없어 위쪽 LLM이 끝낼 때를 정함"이라고 쓴다(`AIBrain.Loop.cs:1579`).
- brief의 3행과 65행(step.stop 설명)에 "count 없이 mode만 준 farm·cook 단계(cook wait 포함)"를 넣었다.

**A — 시험**
- life-tests 5개
  - count 없는 farm water와 cook wait는 측정 안 되는 단계이고, 프로그램이 완료하지 않는다.
  - 입력 문구와 brief 문구를 확인한다.
- execution-tests 4개: 두 종류 모두 자동 완료되지 않고, 위쪽 LLM의 step.stop으로 끝나 계획이 이어진다.

**f — 바뀐 곳**
- 채우지 않은 보조 목표 품목이 나오는 보이는 대상(mine·break·root·pickup) 중 가까운 3개를 직접 선택지로 남긴다(`AIExpedition.cs:244-251`).
  - 판정은 gives가 있으면 그것으로, 없으면 item으로 한다.
  - 상한은 주요 3 + 보조 3(`DirectExtraTargets`, :453)이다.
- 품목은 빌드가 Compose 직전에 `AIExpedition.ExtraGoalItems`로 넘긴다(`AIBrain.Loop.cs:1371`).

**f — 시험**
- execution-tests 1개
  - 주요 목표 구리 대상은 가까운 3개만, 보조 목표 주석 대상도 가까운 3개만 직접 선택지로 남는다.
  - 나머지와 무관한 나무 벽은 목록으로 간다.

**a — 바뀐 곳**
- 기억 광석을 `AIGatheringRules.Components`로 광맥(같은 광석이 이어진 무리, 대각 포함)으로 묶는다. 직접 경로는 광맥마다 하나만 둔다(`AIBrain.Candidates.cs:568-600`).
- 가장 가까운 칸의 경로가 실패하면 그 칸은 막힘 사실로 남기고, 같은 광맥의 다음 칸이 시도한다. 시도 횟수는 직접 개수의 두 배까지다.

**a — 시험**
- core: 1×3 광맥은 양 끝까지 한 광맥으로 묶이고, 다른 광석과 먼 칸은 다른 광맥이다.
- 소스 배선은 소스 검사로 확인했다.

- 확인: gates --unity 실패 0. 수치는 다음과 같다.

| 시험 | 결과 |
|---|---|
| core | 1639 |
| protocol | 193 |
| bridge-wiring | 23 |
| Unity | CS 0 |
| execution | 246 |
| life | 143 |
| life-cook | 73 |
| gather | 62 |
| maintenance | 171 |
| travel | 105 |
| life-craft | 49 |
| life-inventory | 253 |
| inventory-menu | 6 |
| 브리지 node 시험 | gather-limit 11, recall-loop 41 |

- 남긴 작은 것(재검수 b·c·d·e): 이번 지시 범위 밖이라 그대로 두었다.
  - b: 브리지가 뺀 extra를 위쪽 LLM에 알리지 않음
  - c: 보류된 stop이 보고에 보이지 않음
  - d: AIInputTrim·AIFieldChoices의 MenuFree가 조금 다름
  - e: 보수 재료 열린 칸을 덜 셀 수 있음
