# 독립 검수: `/ai hold`·`/ai tp` (3dbae75)

대상: worktree `C:/Dev/CoreKeeperModSDK-cheat-commands`, diff `8a9fab4..3dbae75`. 기준: [합의 추가 범위](agreement.md), 검증 대상으로 본 [구현 설명 추가 범위 절](implementation.md), [실게임 기록](live-validation.md). 읽기만 했고 코드·게임은 건드리지 않았다.

## 판정

**수용.** 차단할 결함은 없다. 아래 F1·F2는 작게 고칠 것을 권고한다(비차단).

- 모델 호출이 나가는 곳이 두 군데뿐이라는 주장, 두 겹 게이트, 실게임에서 호출 103회를 낸 두 경로가 막혔다는 점을 코드로 직접 확인했다.
- 스위치가 꺼져 있고 hold·tp를 쓰지 않은 기본 상태에서는 동작이 그대로다.
- compile-check(`dotnet build Tools/compile-check -c Release --no-incremental`)를 다시 돌렸다. 경고 0, 오류 0이다. HEAD는 3dbae75이고 스크립트에 커밋하지 않은 변경은 없다(환경 파일만 바뀌어 있음). compile-check csproj는 `Scripts/**/*.cs`를 포함한다. Unity는 실행하지 않았다.

## 1. hold 중 모델 호출 경로

**네트워크 호출 위치.** `Assets/AIPlayerSpike` 전체와, 저장소에 추적되는 패키지 밖 .cs 파일에서 다음을 찾았다: `UnityWebRequest`, `HttpClient`, `WebClient`, `Socket`, `SendWebRequest`, 코루틴, `Task.Run`, 스레드, `completed +=`. 결과는 세 곳이다.

- `AIDecisionsApi.Post`(AIDecisionsApi.cs:36)
  - 호출하는 곳은 `CompletePreparationSteps`(AIBrain.Loop.cs:838) 하나다.
  - 그 준비는 `Send`(:728)가 만들고, `StepPreparation`이 진행한다. `StepPreparation`을 부르는 곳은 `Send`와 `Tick`(:285)뿐이다.
  - `Send`를 부르는 곳은 `Tick`의 :208과 :296뿐이다.
- `AIPlanner.Report`의 `/v1/plan`(AIPlanner.cs:446)
  - private이고, 모든 상위 보고가 이곳을 지난다.
  - 호출하는 곳은 Enable, OnHostSaid, OnHostGoal, OnGoalEnded, StartNextStep, OnBackUp, OnFightEnded, RequestReplan, Tick(대기 trigger·periodic·plan 고아)이다.
  - 외부에서 들어오는 곳도 확인했다: 채팅 멘션(Chat.cs:114, 대상 scope 안), `/ai goal`(AIGoals:141·276), 목표 끝(AIGoals:433·437), 쓰러졌다 일어남(AICombat.cs:319, `AIVitals.Tick` 안이며 HeldTick에서도 돈다), 재계획 요청(AIBrain.Loop:1059, AIExpedition:169·267·287).
- `/v1/health`(AIPlanner.cs:947): `/ai bridge` 시험 명령에서만 나가고 모델 호출이 아니다.

**게이트 확인**
- `Tick` 첫머리 게이트(AIBrain.Loop.cs:69), `Send` 게이트(:719), `Report` 게이트(AIPlanner.cs:426)가 위 경로를 모두 덮는다.
- 재시도: 하위는 `_retryAt` 뒤 `Tick`이 다시 보내므로 게이트에 걸린다. 상위는 대기 trigger를 `Planner.Tick`이 보내는데, hold 중에는 그 tick이 돌지 않고 `Report` 게이트에도 걸린다.
- 타이머·콜백·비동기로 HTTP를 보내는 경로는 없다. `AIEdge`의 `Task.Run`은 계산만 한다.
- 멤버 범위: `Held`·`BlocksBrain`은 `AIAgent.Cur`를 읽는다. 각 Tick(AIPlayerSpikeMod.Update:234), 채팅 명령(Chat.cs:87), 멘션(:111)은 모두 그 멤버의 scope 안에서 돈다. 플래너·브레인 상태(`S`)도 같은 `Cur`로 고르므로 게이트와 상태가 어긋나지 않는다.
- 실기록 경로와 대조: `/ai stop` 뒤 103회는 `Tick → SelfDefence → Send` 경로와 상위의 fight_end·periodic 등 `Report` 경로에서 나왔다. 둘 다 막힌다.

**"모델 호출 0"의 한계(F3).** hold를 켤 때 이미 나가 있는 요청은 클라이언트 쪽에서 `Abort`만 한다. 제공자나 브리지 쪽 실행은 멈추지 않을 수 있다. 상위 요청은 버려진 trigger가 풀린 뒤 다시 나가므로 같은 보고가 사실상 두 번 처리될 수 있다. 기존 `/ai stop`도 같은 방식으로 Abort하므로 새로 생긴 문제는 아니다. 실게임에서 사용량으로 확인할 항목이다.

## 2. hold 중 자동 행동과 켜고 끄기

- **AI가 실제로 멈추는가**
  - `Step3_Drive`가 입력을 만드는 유일한 경로다(AIPlayerSpikeMod.cs:989-991).
  - `HeldTick`은 실행 중 기술을 hard Stop한다. `AIBaseGuard.OnStop(hard)`는 End 또는 CutTail이므로 문 닫기 꼬리를 남기지 않는다. `EndSkill`·`Stop`이 `DropPress`를 하므로 기다리던 상호작용 입력도 나가지 않는다.
  - 입력은 `BodyInput.Idle`로 만든다. 이동 0, 버튼 없음, nudge 0이다.
  - 문 추적, 꼬리, 커서 정리, 시험 지급, `AIPlanner.Tick`(`AIExperience` 포함)은 건너뛴다.
  - `AIVitals.Tick`·`AIMemory.Observe`는 읽기와 기록만 한다. 쓰러지면 `AddEvent`, 일어나면 `OnBackUp → Report`(보류)로 간다.
- **켤 때 순서.** `Hold = true`를 먼저 세우고 나서 Stop, `CancelPendingChoices`(대기 선택·`_held`·다음 타격·미리 묻기·준비·HTTP·반사), `DropRequestForHold` 순으로 정리한다. 중간에 예외가 나도 게이트가 먼저 선다.
- **목표 유지.** hold on·off 모두 `Goal`을 건드리지 않는다. 기술 취소는 `_fails`에 들어가지 않는다(Failed만 들어감). `/ai stop`은 hold 중에도 `Disable`로 보류 보고를 지운다. 풀린 뒤 목표가 없으면 `SelfDefence`가 거짓이라 호출하지 않는다.
- **풀린 뒤 상위 호출 횟수**
  - 보류 trigger는 `PlanInbox.Put`이 우선순위를 지켜 하나만 남는다.
  - 풀린 다음 상위 tick에서 `_retryAt`가 지났으면 한 번 나간다. 그 뒤로는 `_req`가 있어 periodic도 겹치지 않는다.
  - 하위는 `ForgetPlace`로 판단을 한 번 부른다(간격·비용 규칙은 그대로).
  - 보류 중에 periodic 주기가 지났더라도 보류 trigger가 있으면 그 보고 하나만 나간다. 보류 trigger가 없으면 periodic이 한 번 나갈 수 있다(기존 규칙).
- **F1 (낮음~중간)** `AIPlanner.DropRequestForHold`, AIPlanner.cs:411-419
  - :417의 `if (_pendingTrigger == null)` 조건 때문에, 이미 대기 중인 낮은 우선순위 trigger(fight_end·periodic 등)가 있으면 진행 중이던 `host_said`/`host_goal`이 버려진다. `Put`은 원래 우선순위를 비교하므로 이 조건이 오히려 그 비교를 막는다.
  - 보낼 때 `S._chatSeen = Chat.LastSeq`로 올린 값을 되돌리지 않는다. 그래서 풀린 뒤 다시 나가는 보고에서 호스트 말이 "새 말"로 표시되지 않는다.
  - 기존 재전송 경로(409 :530, AskAgain :593, AskAgainNow :741, KeepForRetry :210)는 모두 `IsHostTrigger`일 때 `_chatSeen = _reqChatSeenBefore`로 되돌린다. "호스트 말은 잃지 않는다"(B2) 규칙이 hold 경로에서만 깨진다.
  - 권고 수정:
    ```
    if (AIPlanRules.IsHostTrigger(_reqTrigger)) S._chatSeen = S._reqChatSeenBefore;
    _pendingTrigger = _reqTrigger;
    ```
  - 참고: 보낼 때 `_events.Clear()`로 지운 사건도 사라진다. 다만 기존 실패·재시도 경로도 사건을 되돌리지 않으므로 같은 동작이다.
  - 참고: 계획이 보고 진행 중에 끝나 생긴 owed 상태(`_owedPlanTrigger`)는 다시 나간 보고의 답이 정리한다. 상위 호출이 한 번 더 늘 수는 있지만 꼬이지는 않는다.
- **F2 (낮음)** hold가 시험 스위치와 묶여 있지 않다.
  - `Held`(AITestControl.cs:30)는 `testcmds.on`을 보지 않는다. 그런데 `hold off`는 `Dispatch`의 시험 명령 검사(AIPlayerSpikeMod.Chat.cs:138)에 걸린다.
  - 그래서 hold 중에 스위치 파일이 사라지면 AI는 게임을 재시작할 때까지 멈춘 채 풀 수 없다. 스위치가 꺼진 상태에서 제품 동작이 달라지는 유일한 경우다.
  - `Hold`는 `AIAgent` 필드(AIAgents.cs:64)라 같은 프로세스에서 다른 월드(예: 사용자 월드)를 열어도 유지된다. `/ai` 상태 표시에도 나오지 않는다.
  - 권고: `hold off`는 스위치 없이도 받는다. 또는 게이트를 `AITestSwitches.Test && Hold`처럼 스위치와 함께 묶는다. 상태 문구에 "정지(hold)"를 표시한다. 월드를 내릴 때(`DisposeAll`) 푸는 것도 검토할 만하다.
- **참고:** hold·tp 취소가 `_recent`에 "… 취소: /ai hold"처럼 남아 풀린 뒤 첫 판단 입력에 들어간다. 해롭지는 않다.

## 3. tp

- **이동 방식.** `AIArena.Go`(AICombat.cs:955-965)와 같다: `SetNextState(Teleporting, true)`, `TeleportingStateCD.targetPosition = (x,0,z)`. 걷기 칸 찾기도 같은 고리 탐색이다. host일 때만 가운데 칸을 뺀다. 적 제거·회복·무기 교체를 하지 않는 점은 합의와 맞다. 쓰러진 상태는 거부한다.
- **도착 전 차단**
  - `BlocksBrain`(AITestControl.cs:34-55)은 Teleporting 상태이거나 다음 상태가 Teleporting인 동안, 또는 목표 칸에서 2칸보다 멀면 하위 tick을 막는다.
  - 명령 순간에 Stop과 `CancelPendingChoices`를 한다.
  - 도착하거나 20초가 지나면 `ForgetPlace`로 대기 선택·요청·탐색 끝 찾기 작업을 버리고 그 프레임에 새로 판단한다.
  - hold 중 tp는 hold 판정이 먼저라 풀릴 때 처리된다. `arrived`를 먼저 계산하므로 20초가 지난 뒤 풀어도 실제로 도착했으면 "도착"으로 판정한다.
  - 다시 참가해도 `TpPending`은 `late`로 바로 해소된다.
- **옛 위치 기준이 남는지**
  - 경로 캐시 `PlanC`는 출발 칸을 비교하고, 준비별 `Plans`는 준비마다 지운다. 문 단계 상태(`navStep`)는 기술 실행 중이고 0.5초 안일 때만 쓰인다. 옛 위치에서 실행이 이어지는 경로는 찾지 못했다.
  - 상위 계획은 이동 중에도 돈다(설계 선택). 약 6초 동안 만든 보고는 옛 위치나 이동 중 위치를 담는다.
- **걸을 수 없는 칸**
  - 반경 3 안에 걷기 칸이 없거나 읽지 않은 영역이면 지정 칸 그대로 보내고 응답에 그 사실을 붙인다.
  - 게임이 다른 곳에 내려놓아 2칸 밖이 되면 하위 판단이 최대 20초(이동 약 6초 포함) 멈췄다가 "도착 확인 못함"으로 다시 판단한다. 결함은 아니고 허용 가능한 저하다.
  - `HostPosition`은 호스트를 못 찾으면 (0,0)을 준다(기존 함수).
- **여러 AI:** `/ai all tp host`·`/ai all tp x z`는 모든 AI를 같은 칸으로 보낸다. 시험할 때는 멤버를 하나씩 지정한다.

## 4. 기본 상태(스위치·hold·tp 꺼짐)

- `Held`·`BlocksBrain`이 거짓이면 `Tick`·`Send`·`Report`·`Body.Tick` 흐름은 원래와 같다.
- `Body.Tick` 끝부분을 `Emit`으로 떼어 낸 것은 문장 순서가 같다. 앞쪽 지역 변수(`em0`·`myHp`)를 쓰지 않고, 그랬다면 컴파일이 실패했을 것이다.
- 비용은 캐릭터당 프레임마다 `Cur` 읽기와 bool 확인 몇 번, 함수 호출 하나뿐이다.
- 이 함수들은 모두 scope 안에서 불리므로 `Outside()` 계측 수치도 늘지 않는다.

## 5. 예외와 여러 AI

- `Brain.Tick`은 try 안에서 돈다. `BlocksBrain`은 `player`가 있는지 확인한 뒤에 불린다.
- 명령은 `Dispatch`의 try 안에서 돈다. hold를 켜는 중에 예외가 나도 게이트가 먼저 선다.
- hold 중에는 `AIChargeWatch.Frame`·`AITestSwitches.Refresh`가 그 멤버 tick에서 빠진다. 다른 멤버가 tick하면 그대로 돈다. 모든 멤버가 hold면 스위치 상태 갱신이 멈추지만 영향은 없다.
- `/ai all hold`(인자 없음)는 멤버마다 따로 켜고 끈다. 상태가 섞여 있으면 `hold on`·`hold off`를 명시해야 한다.

## 실게임에서만 판정할 항목

1. hold 중 decisions 사용량, `DECISION … SEND`, `PLANNER: report … sent`가 0인지. 적 접근·피격·쓰러짐 뒤 일어남, 채팅 멘션, `/ai goal` 조건에서 본다. hold를 켠 직후 이미 나가 있던 요청 몫이 과금되는지(F3)도 본다.
2. hold 중 실제로 서 있는지, 문 앞이나 상호작용 중에 바로 멈추는지.
3. `hold off` 뒤 목표가 그대로이고, 하위 1회·상위(보류 trigger) 1회만 나가는지. 멘션이 진행 중일 때 hold를 켰다면 풀린 뒤 보고에 호스트 말이 남는지(F1 재현).
4. `tp x z`·`tp host`가 약 6초 뒤 2칸 안에 도착하는지. `IsNextState`·`HasAnyState`로 이동 중을 끊김 없이 잡는지(이동 전후 한 프레임 빈틈 여부). 도착 전 하위 SEND가 없고 도착 뒤 새 위치 후보로 판단하는지.
5. 벽 속이나 읽지 않은 영역으로 tp했을 때 착지 위치와 20초 대체 처리.
6. hold 중 tp한 뒤 hold off 하면 새 위치에서 판단하는지. 원정 중 tp 뒤 `AIExpedition`의 현장 이탈 재계획이 과하게 나오지 않는지.
