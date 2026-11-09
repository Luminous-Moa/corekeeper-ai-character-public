# 1단계 결과 — 위쪽 LLM 목표 조정 (2026-11-08)

기준: [design.md](design.md) 1단계. worktree `C:\Dev\CoreKeeperModSDK-celeris`, 브랜치 ai-celeris-decisions-20261108(2bc81cbb 위). 설치·병합·브리지 재기동 없음.

## 위쪽 LLM 답 형식 (brief.md에 준 그대로)
- `step`(선택, keep·ask와 함께만):
  - `{"count": N}`: 지금 gather·craft 단계의 완료 수량을 N으로 바꾼다. 늘리면 파밍이 이어진다. 이미 확보한 양(step.acquired)보다 작게는 못 하고, 같게 하면 바로 끝난다. 호스트가 정한 max_count는 넘지 못한다.
  - `{"stop": true}`: 지금 단계를 지금 끝내고 계획의 다음 단계로 넘긴다. 파밍을 멈추는 길이고, explore·follow는 이것이나 새 set으로만 끝난다. 마지막 단계면 goal_done으로 다시 묻는다.
  - set·hold에 쓴 step은 적용하지 않는다. 받지 못한 step은 다음 보고의 last_answer_rejected에 온다.
- `extra`(선택): 보조 목표 목록 전체를 바꾼다.
  - 꼴은 `[{"item": "<ObjectID>", "count": 양의 정수, "why": "짧게"}]`. `[]`는 모두 지우고, 안 쓰면 그대로 둔다.
  - 보조 목표는 지금 단계를 끝내지 않는다. 가방에 count만큼 모이면 filled가 되고, 목록에서 지울 때까지 남는다.
  - 기준: 계획의 다음 단계 재료, 기지 상자·가방에 모자란 것, 성장의 다음 단계 준비를 준다. 이미 충분한 것은 주지 않는다. 개수·수량의 정해진 한도는 없다.
- 예: `{"decision": "keep", "for": {...}, "step": {"count": 20}, "extra": [{"item": "Wood", "count": 30, "why": "횃불·널빤지"}], "why": "…"}`
- 보고 필드
  - `step`: gather·craft 단계면 `{acquired, count, left, close}`, 아니면 null. close는 남은 양이 2개 이하이거나 count의 5분의 1 이하일 때다.
  - `extra`: `[{item, count, have, filled, why}]`.
  - 기존 chests·base·me.bag는 그대로 둔다.
- 브리프 본문: 행동 판단기는 단계를 끝내지 못하며, 곁일·추가 파밍은 위쪽 LLM이 상자·가방·성장 다음 단계를 보고 정한다. 정기 점검 때 step·extra를 함께 정할 수 있다(`Tools/ai-bridge/brief.md:3,7,13-14,57-59,65-66,90`).

## 바뀐 것 (file:line)
**새 Core 파일:** `Assets/AIPlayerSpike/Scripts/Core/AIExtraGoals.cs` (+ .meta)
- `ExtraGoal`, `ExtraGoalView`
- `AIExtraGoalList`
  - `Replace`: 목록 전체를 바꾼다. 같은 item과 count면 filled를 유지한다.
  - `View`: filled는 한 번 닿으면 유지된다.
  - `ReportJson`
  - `Valid`: 모르는 품목, 양이 아닌 count, 중복을 이름으로 알려 준다. 개수 상한은 없다.
- `AIStepRules.CountCheck`, `AIStepRules.Close`

**파서:** `Core/AIPlanProtocol.cs:45-50,104-121`
- `HasStep`, `StepCount`(-2는 잘못된 값), `StepStop`
- `Extra`: null은 생략, 빈 목록은 지움.

**위쪽 LLM 적용 (`AIPlanner.cs`)**
- :96 State `Extra`(재시작 때 저장되지 않음, 계획과 같음). :159 Reset 때 비움.
- :167 `ApplyExtra`: 모르는 품목 등은 last_answer_rejected와 이벤트로 알린다.
- :182 `ExtraGoalsNow(server, player)`: 2단계용 접근자.
- :190 `ExtraReportJson`.
- :384 `ApplyStep`
  - count: `AIStepRules`로 검사한 뒤 Goal.count와 계획 현재 단계 count를 바꾼다.
  - stop: `AIGoals.EndGoal(..., "goal_done", ...)`. 완료와 같은 경로라서 몸 동작이 멈추고, 다음 계획 단계가 시작되며, 마지막 단계면 보고한다.
  - :378 `NoteRejected`
- :673 모든 받은 답에 extra를 적용한다. :676 set·hold에 온 step은 거절 사유로 남긴다. :718 keep·ask에 온 step을 적용한다.

**목표 (`AIGoals.cs`)**
- :310 `StepAcquired`: gather는 원정 기록의 확보량, craft는 만든 수다. RefreshCompletion도 이 함수를 쓴다.
- :336 따르기 3분 관측 문구를 "위쪽 LLM이 정함"으로 바꿨다.

**보고:** `AIPlanReport.cs:49-53`에 `step`, `extra` 필드를 추가했다.

**finish_goal 전면 제거**
- `AIBrain.Candidates.cs:28`: 어떤 목표에도 내놓지 않는다.
- `AIBrain.Loop.cs:1526`: 작업 관측 문구를 "측정 조건이 없어 위쪽 LLM이 끝낼 때를 정함"으로 바꿨다.
- `FinishChosen` 가드는 늦게 온 답을 위해 그대로 둔다.

**브리지**
- `Tools/ai-bridge/protocol.mjs:105-128`: step과 extra 형식을 검사한다. 상한은 없다.
- `plan.mjs:99`: 브리지 로그에 step·extra를 남긴다.

## 보조 목표 진척의 기준
가방(main inventory) 수량(`AIBody.InventoryCounts`)이다. 상자 수량은 보고의 chests로 위쪽 LLM이 따로 본다.

## 시험
- bridge protocol 192(step·extra 표 21건 + null 삭제 1건 추가), bridge-wiring 23
- core-tests 1604. 새 묶음 EG 21: 파서, Valid(12개 받음·상한 없음), Replace·View의 filled 유지·초기화·비움, CountCheck(올림·같음·내림 거절·상한 없음·max_count·셀 수 없는 단계), Close
- execution 233/233(+8)
  - step.count 5→20(계획 단계도 같이 바뀜), 20에서 4로 내리기 거절, 5로 내리면 프로그램이 완료하고 다음 단계로 감
  - 2/8에서 step.stop하면 몸이 멈추고 다음 단계로 감
  - 단계가 없을 때 stop 거절, Home에 count 거절, -2 거절
  - explore·follow는 step.stop으로 끝남
- life 136(어떤 목표 종류에도 finish_goal 없음: 7종), maintenance 171, travel 105/105, gather 61, life-cook 70, life-craft 49, life-inventory 253, inventory-menu 6
- gates.sh --unity 실패 0(Unity CS 0)

## 남은 위험
- 브리지는 아직 `C:/Dev/CoreKeeperModSDK`(옛 brief·protocol)에서 돌고 있다. 설치 때 worktree의 `Tools/ai-bridge`로 다시 띄워야 step·extra를 알고 형식도 검사한다. 옛 브리지는 step·extra를 모르지만 그대로 넘겨는 준다.
- 보조 목표는 재시작하면 사라진다(계획과 같음).
- set 답의 step은 적용하지 않는다(set은 자기 count를 가짐). 지시는 "set과 함께"였으나 extra만 set과 함께 쓰고 step은 keep·ask 전용으로 정했다.
- step.count는 계획 현재 단계의 count도 바꾼다. 원래 부탁(HostOrder)의 max_count는 넘지 못한다.
- step.stop의 끝 사유는 'completed'(goal_done)라서 원래 부탁에 묶인 단계면 그 단계는 끝난 것으로 셈해진다. 원래 부탁 자체는 order=done 전까지 열려 있다.
- 실제 Gemini가 step·extra를 쓰는지, 얼마나 자주 쓰는지는 실게임에서 확인해야 한다.
- 2단계 접근자: `AIPlanner.ExtraGoalsNow(World, Entity) → List<ExtraGoalView>{Item(ObjectID 이름), Count, Have(가방), Filled, Why}`.
