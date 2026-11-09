# 재검수: 후속 수정 (ae09acf)

대상: worktree `C:/Dev/CoreKeeperModSDK-cheat-commands`, diff `3dbae75..ae09acf`(AIPlanner.cs, AIPlayerSpikeMod.Chat.cs, AITestControl.cs). 기준은 [이전 검수](review-control.md)의 F1·F2와 [구현 설명 '후속 수정 (ae09acf)' 절](implementation.md)이다. 읽기만 했고 코드·게임은 건드리지 않았다.

## 판정

**수용.** F1·F2는 해소됐고, tp 거부도 설명대로 동작한다. 새로 생긴 차단 결함은 없다. 아래 참고 2건은 고치지 않아도 된다.

- compile-check(`dotnet build Tools/compile-check -c Release --no-incremental`)를 다시 돌렸다. 경고 0, 오류 0이다.
- HEAD는 ae09acf이고, 스크립트에는 커밋하지 않은 변경이 없다(환경 파일만 바뀌어 있음). Unity는 실행하지 않았다.

## F1: hold 중 호스트 보고 보존 (해소)

**수정 내용** (AIPlanner.cs:413-429)
- `DropRequestForHold`는 진행 중이던 보고가 `IsHostTrigger`(host_said·host_goal)이면 `S._chatSeen = S._reqChatSeenBefore`로 되돌린다.
  - 409(:540), AskAgain(:603), KeepForRetry(:210), :751 경로와 같은 방식이다.
  - `_reqChatSeenBefore`는 보낼 때(:465) `_chatSeen`을 올리기 직전 값으로 저장된다. 따라서 되돌리면 풀린 뒤 다시 나가는 보고에서 호스트 말이 새 말로 잡힌다.
- 이전의 `if (_pendingTrigger == null)` 조건은 없어졌다. 지금은 `HoldTrigger`가 우선순위를 비교하므로, 이미 대기 중인 낮은 trigger(fight_end·periodic)가 host_said·host_goal을 밀어내지 못한다.

**기본 상태(hold 미사용)의 상위 보고 동작은 바뀌지 않았다.**
- `HoldTrigger`를 부르는 곳은 두 군데뿐이다. 하나는 `DropRequestForHold`(:420)이고, 이 함수는 `AITestControl.Hold`의 켜기 분기(AITestControl.cs:86)에서만 불린다. 다른 하나는 `Report`의 `if (AITestControl.Held)` 분기(:436)다.
- `Report`에서 hold가 아닐 때 타는 줄(:437 `_req != null`, :439 down/retry, :442 이후 대기분 합치기·전송)은 diff에서 바뀌지 않았다.
- `_pendingTrigger`의 setter는 원래부터 `PlanInbox.Put`(Core/AIPlanInbox.cs:23-28)을 부른다. `Put`은 우선순위가 낮은 새 trigger를 버리고(`<`이면 return), 같거나 높으면 바꾼다. `HoldTrigger`의 비교식(`>=`)은 이 규칙과 같다. 그래서 hold 중 `Report` 게이트의 동작은 3dbae75와 사실상 같고, 바뀐 것은 로그 문구뿐이다. 실제로 동작이 고쳐진 곳은 `DropRequestForHold`의 null 조건 제거와 `_chatSeen` 복원이다.

**참고 1(정보, 수정 불필요)**
- 구현 설명의 "우선순위가 같으면 새 trigger가 남는다"는 `Report` 게이트에서만 맞다.
- `DropRequestForHold`에서는 진행 중이던(더 오래된) `_reqTrigger`가 같은 우선순위의 대기 trigger를 덮는다. 예를 들어 대기 host_goal을 진행 중이던 host_said가 덮고, 대기 goal_done을 진행 중이던 action_replan이 덮는다.
- 바뀌는 것은 다시 나가는 보고에 붙는 trigger 이름뿐이다. 보고 내용은 보낼 때의 상태(채팅·목표·사건)로 새로 만들어진다. 기존 409 경로(:540 `_pendingTrigger = _reqTrigger`)도 같은 동작이라 새로 생긴 문제가 아니다.

## F2: 스위치 없이 hold off (해소)

**게이트 예외** (AIPlayerSpikeMod.Chat.cs:139)
- 조건은 `IsTestCommand(arg) && !TestCommandsOn && !(arg == "hold" && AIAgent.Cur.Hold)`다.
- 이 예외는 명령어가 정확히 `hold`(소문자로 바꾼 값)이고, scope 안의 멤버가 지금 hold 중일 때만 열린다. `tp`와 다른 시험 명령, hold 중이 아닌 멤버의 `hold`(켜기)는 그대로 거부된다.
- `Dispatch`는 멤버마다 `AIAgent.Enter(a)` 안에서 불린다(:85-89). 그래서 `/ai all hold`를 보내면 hold 중인 멤버만 풀리고 나머지는 거부 응답을 받는다.
- 스위치가 없을 때 예외로 들어갈 수 있는 입력은 `hold`, `hold off`, `hold on`이다. 앞의 둘은 hold를 푼다. `hold on`은 이미 켜져 있으므로 `on == a.Hold`에 걸려 "이미 켜짐"만 답한다(AITestControl.cs:80). 잘못된 인자는 사용법만 답한다. 그 밖에 열리는 경로는 없다.
- `TestCommandsOn`을 보는 다른 곳(AICharacterStore.cs:514, 도움말 문구 :207)은 바뀌지 않았다.
- 원래부터 있던 제약: 예외로 받은 hold도 `IsSkillCommand` 분기에서 `_stage == Walking`이어야 한다. 월드에 들어와 있지 않은 멤버는 들어온 뒤에 풀 수 있다. hold는 들어와 있을 때만 효과가 있으므로 문제가 되지 않는다.

**상태 표시**
- `/ai <멤버>`(:239)에는 `· 정지(hold) 켜짐: /ai <id> hold off`가, `/ai`(:249)에는 `/정지(hold)`가 붙는다. `/ai all` 상태는 `a.Hold`를 그 멤버의 scope 안에서 읽는다.

## tp: 걸을 수 있는 칸이 없으면 거부 (확인)

- `NearestWalkable`이 `found == false`를 돌려주면 Log와 Reply만 하고 `return`한다(AITestControl.cs:131-138).
- 이 return은 `AIBody.Stop`, `CancelPendingChoices`, 상태 설정, `TpPending` 설정보다 앞에 있다. 그래서 행동·대기 선택·요청·`TpPending`이 그대로 남는다. `tp host`도 같은 함수를 지나므로 같은 규칙을 따른다.
- 칸을 찾았을 때의 경로는 그대로이고, 응답과 로그에서 "확인 못함" 꼬리만 빠졌다.

**참고 2(표시만, 수정 불필요)**
- 좌표로 tp를 거부할 때 응답에 좌표가 두 번 나온다(`what` = `"(x,z)"` 뒤에 ` (x,z)`가 또 붙어, 예: `(5,3) (5,3) 주변 3칸 …`). `tp host`일 때는 `호스트 옆 (x,z)`로 자연스럽다.

## 기타

- 파일 머리 주석에 부활 무적 주의사항을 넣었다(AITestControl.cs:18-20). 주석만 바뀌었다.
- 이전 검수의 F3(진행 중 요청은 클라이언트에서 Abort만 함)는 이번 범위가 아니며 그대로다.
- hold로 요청을 버렸을 때 owed plan trigger(`_owedPlanTrigger`)는 다시 나가는 보고의 답이 정리한다. 이전 검수의 판단과 같고 이번에 바뀌지 않았다.

## 실게임에서만 판정할 항목

구현 설명의 '실게임 확인 항목(ae09acf)' 네 가지를 그대로 따른다. 코드상 기대 결과는 다음과 같다.
- host_said가 진행 중일 때 hold를 켜고 그 사이 fight_end가 생기면, 풀린 뒤 `PLANNER: report … sent trigger=host_said`가 1회 나가고 보고의 새 호스트 말에 그 멘션이 들어 있어야 한다.
