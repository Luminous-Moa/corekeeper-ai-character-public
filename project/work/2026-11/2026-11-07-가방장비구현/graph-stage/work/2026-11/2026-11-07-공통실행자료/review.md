# 공통 실행 최종 독립 상세검수

## 판정

**검수 중 발견한 R1~R8이 최종 소스에서 해소됐다. 이번 공통 실행 합의 범위에서 남은 P1/P2 차단 결함을 발견하지 못했으며 수용 가능하다.**

검수 대상은 `C:/Dev/CoreKeeperModSDK-common-execution`, 기준 `11eb461ae122f404acec94563c086b09e002987c` 대비 최종 작업 소스다. 공통 실행 합의·contract.md·기준 취소경로조사와 actual producer→consumer를 대조했다. 2026-11-07 12:44:23+09:00에 기록한 [production 파일 SHA-256](review-source-hashes.json)과 최종 파일이 모두 일치함을 확인했다. 소스 수정·게임 실행·원격 모델 호출은 하지 않았다. 검증 과정에서 전용 harness의 생성·빌드 파일만 기존 runner가 갱신했다.

최종 소스로 독립 실행한 검증:

- `dotnet run --project Tools/core-tests -c Release --no-restore`: **1,141개 통과**.
- `dotnet build Tools/compile-check -c Release --no-restore`: **경고 0·오류 0**.
- `Tools/execution-tests/run.ps1`: **실제 소스 연결 31/31 통과**. 동일 빌드의 출력도 [독립 검수 최종 실행 로그](review-execution-final.log)에 보존했다.
- `git diff --check`: 통과.

이 판정은 아래 명시한 오프라인 경계까지다. 실제 게임의 타격·경로·지연 거래와 원격 AI 선택을 검증했다고 확장하지 않는다.

## 발견과 수정 확인

| ID | 발견 당시 영향·재현 경로 | 최종 수정과 확인 |
|---|---|---|
| R1 / P1 | run X 때 만든 stop_action 답이 held 적용으로 시작한 run Y를 중단. 최초 control에는 run 바인딩·stale 검사 없었음 | `AIBrain.Candidates.cs:1197`에서 현재 run 바인딩. `AIActions.cs:76`에서 같은 run만 수락하고 idle 관측 뒤 새 run도 거절. 31개 실제 entry 검사에 same-run 허용·다른-run/idle→running 거절 포함 |
| R2 / P2 | 바깥 skill의 timeout만 바꿔도 내부 DoorPass/PlacePress/PickupStep이 시간·횟수만으로 먼저 Failed. 그 결과가 부모 skill 종료로 상승 | 문6/12초·누름2회, 놓기 설자리3초, 회수 무피해5초/수령3초가 observation으로 바뀜. `AINavigator`, `AIEdgeSkills`, `AIEdge.Guard` tail, `AIPlace`, `AIPickupSkill`이 출력 관측을 AI에 전달하면서 phase 유지. 1,141개 core 검사에서 정상 문 조합·정렬·지연 회수 완료까지 확인 |
| R3 / P2 | 하위 age는 제거했으나 상위 CheckReason의 secs>180이 유효한 set/hold를 drop/TalkOnly로 만듦 | `AIPlanner.cs:754`의 age 단독 분기 제거. 하위 `AIBrain.Loop.cs:881`도 age 대신 실제 goal/대상/run 검사. 사용자 stop·새 goal·잘못된 report echo를 무효화하는 경계는 유지 |
| R4 / P2 | ObserveOnce 사실이 일시적 needReason 한 칸에만 기대어 HTTP 실패/다른 trigger 뒤 prompt에서 사라짐 | `AISkill.ExecutionObservations`와 per-character ExecutionReason/revision 보존. 현장 BuildInput이 둘 다 읽고, `AIPlanReport.cs:48`은 durable last fact와 현재 run 목록을 함께 읽어 nested navigator/tail도 전달. 호출 중 새 관측은 revision 차이로 후속 판단을 유지. 실제 Observe/일시 이유 덮어쓰기 검증 통과 |
| R5 / P1 | 문 닫기 tail의 시간 실패 제거 후 soft Stop은 KeepTail을 반환하므로 AI 명시 stop/finish가 무기한 tail을 못 끊을 수 있었음 | `Loop.Apply:889` stop, `AIGoals.EndGoal:302` 양순서, host goal없음, upper hold가 hard Stop. 실제 `AIBody.Stop:367→OnStop→EndSkill:408`을 harness에서 실행해 tail 종료·열린 문 사실·부탁 보존·대체 tail 미시작 확인 |
| R6 / P2 | Gather 목표 중 선택한 edgefill/repair(progress=false)에 근접 적이 오면 close-fight set이 continue를 삭제. 후보 cap도 current를 자를 수 있었음 | `Core/AICandidateAssembler.Select:60`이 regroup 전 current continue를 잡아 group 탈락 후 복구. `Finish:214`가 cap 후 current를 보존하고 실제 value를 할당. 새 core 검사3개로 비목표 current·no-goal current·cap 보존 확인 |
| R7 / P2 | Goal.None 자기방어 중 upper hold는 goal 조건문에 막혀 몸을 멈추지 않았음 | `AIPlanner.cs:646` hold에서 body Stop을 goal clear 조건 밖으로 이동. 자기방어 episode도 false/revision/request 정리. real/free/none의 현재 몸 중단 의미가 일치함을 actual consumer에서 확인 |
| R8 / P1 | tailStatus=Done인 X와 과거 prefetch Y가 있을 때 stop hard→EndSkill이 Done 복원→`OnSkillEnded:410`이 Y 재예약→다음 Tick에 Y 시작. Goal.None userclear의 early return도 같은 잔존 위험 | `AIBrain.cs:296 CancelPendingChoices`가 queued/for/applyNow·held·nextHit/for/now·prefetch 및 요청을 먼저 정리. stop_action·새 goal/clear·upper hold가 Stop 전에 호출. `AIGoals.ClearGoal:56`은 None early return보다 먼저 정리하고 자기방어도 해제. actual Apply→Body.Stop→EndSkill→OnSkillEnded 회귀에서 old 선택 재예약 없음, 원래 부탁 유지 확인. ordinary Done은 이미 선택한 정상 prefetch를 계속 예약하는 positive 검사도 통과 |

R5와 R8은 최종 상세검수 중 추가 발견하여 main/root에 즉시 전달했고, 수정 후 actual callback 검사까지 확대해 다시 확인했다. R8은 몸 Stop만 stub으로 대체한 최초19개 검사로는 검증되지 않으므로 최종31개 검사의 실제 callback 연결을 별도로 읽었다.

## 합의의 실제 소비 흐름

### 관측은 목표·몸 실행을 자동 종료하지 않는다

`AIGoals.CheckDone:248`은 수량·feature·follow 경과 조건을 Completion에 보존하고 false를 반환한다. `CheckAfterBuild:272`도 진행 후보 부족을 Blocked 관측으로 보존한다. 기준 코드의 `EndGoal→ClearGoal→AbortRequest→Stop` 연결로 즉시 흐르지 않는다. 동일10/10은 반복 관측을 발행하지 않으며 선택한 다음 채굴 run이 다음 Tick의 primary 입력까지 유지되는 것을 actual entry 검사로 확인했다.

연속3회 호출 실패·월 비용 상한·API key 없음은 새 호출을 제어하면서 goal/유효한 current를 지우지 않는다. 일반 timeout과 새 관측은 `AIRunGate.AbortDropsWaiting`에서 waiting 취소 사유가 아니다. 대신 직접 AI 중단·새 goal·사용자 stop은 CancelPendingChoices로 과거 선택까지 취소한다. 관측 보존과 명시 취소의 소유권이 구분됐다.

### 단계 완료와 원래 부탁 완료가 구분된다

`finish_goal→AIGoals.FinishChosen:292→EndGoal:302`은 AI가 현재 단계 완료를 명시했을 때만 실행된다. contract는 이 경우 이미 세운 다음 plan 단계로 이어갈 수 있다고 정했으므로 `OnGoalEnded→_planNext→StartNextStep` 자체는 이번 검수의 결함이 아니다.

`Core/AIHostOrders.GoalEnded:132`는 done=true에서 같은 source의 원래 부탁을 끝남으로 바꾸지 않는다. 구리10/10 관측과 명시 구리 단계 완료 양쪽에서 '구리 갑옷 만들어줘'가 열린 상태이며, actual `AIHostOrders.ApplyAnswer(order=done)`이 와야 닫히는 것을 확인했다.

`request_plan→AIPlanner.RequestReplan:411`은 현재 goal/current를 유지하고 요청·후속 trigger를 합친다. 실검증의 planner/network 경계는 stub이므로 실제 HTTP 대기 중 몸 입력은 정적 호출 흐름과 probe 유지까지 확인한 범위다.

### 후보 실패 기록과 기계적 전제가 구분된다

반복2회/30초 Mark 삭제, 생성 전 LeftOut, frontier skipF, noPath 주변90초, NoDamage/농사600초 등의 역사 기반 차단은 제거되거나 설명으로 바뀌었다. current continue는 set/cap 이후에도 보존된다.

손 칸·가방 공간·현재 점유·실제 대상 health/존재·입력 buffer·알 수 없는 kind/item/where 같은 실제 실행·프로토콜 전제는 남긴다. 길찾기의 탐색 상한·피해 추정은 실제 물리 불가의 완전한 증명이 아니며 보고 문구에 미발견/추정 이유가 남는지 함께 살폈다. 모든 후보 생성 예산과 제품의 지원 행동 목록을 이번 작업에서 무제한으로 확장하지 않았다.

### 정책 포기와 실제 operation 결과가 구분된다

공격45초·채굴6초 무변화/60초·터널30초 무진척/300초·이동예산·짧은 이동 무진척은 observation이다. actual 대상 소실, 선택한 손의 물건 변경, input buffer 없음, 실제 path 실행 실패, 예외 등은 해당 실행 회차 결과로 종료할 수 있다. 실패한 tunnel child를 횟수로 자동 재시도하는 대신 실제 실패와 진척을 부모 결과로 전달하는 변경도 확인했다.

Follow 후보의 '최대30초', Hold 최대2초, 거리벌리기3초, 정한 좌표 도착, 한 번 휘두르기는 AI가 선택한 유한 동작의 endpoint로 분류하여 결함에서 제외했다. 실제 후보 설명과 달리 숨은 시간·횟수로 포기하는 내부 단계가 R2의 대상이다.

Craft/Take/Cook은 보낸 요청의 `_sent` 상태를 유지하며 실제 수량/입력 결과를 확인한다. timeout을 무한 재주문으로 바꾸지 않았다. Eat도3초 미확인 뒤 소비 버튼을 놓고 수량을 확인하므로 같은 실행에서 계속 먹는 입력을 만들지 않는다. 실제 게임의 늦은 ACK·동일 품목 외부 변화·새 AI 회차 재시도 시 중복 거래 여부는 오프라인 harness에서 입증하지 않은 동적 검증 범위다.

## 검증 증거와 경계

[기준 검증 로그](offline-baseline.log)는 기준11eb461의 실제 Goal/HostOrders 소스를 연결해 수량 충족→현재 선택 취소→원래 갑옷 부탁 완료를3/3 재현한 자료다.

최종 [31개 실행 로그](review-execution-final.log)는 actual Goal/Skill/HostOrders/RunGate와 생산 소스에서 추출한 Apply·ObserveExecution·AIActions.Stale·Body.Stop·Body.EndSkill·Brain.OnSkillEnded·CancelPendingChoices/AbortRequest/DropQueued를 실행한다. 임의로 재구현한 취소 정책에 기대지 않는다. state/world/network·실제 행동 factory와 물리 입력의 경계는 stub이고 probe를 사용한다. 전체 Brain.Tick scheduling/응답 parser/actual candidate 생성과 실제 게임 transaction을 통째로 실행한 검사는 아니다.

core1,141개에는 정상 문 통과420조합 등 기존 정상 실행과 새 내부 관측 경계, current continue 보존 회귀가 포함된다. 생산 모드 compile-check는 설치 게임 DLL에 대조한 전체 C# 빌드이며 경고0·오류0이었다. 검수 컨텍스트에서도 최종 소스 변경 후 core·compile을 재실행했다.

전체 원격 모델 판단과 실게임은 수행하지 않았다. 이 제한을 제외하고 이번 합의의 관측 유지·명시 계속/중단/전환·원래 부탁 보존·과거 선택 경합 처리에 대한 소스 및 오프라인 수용 근거는 확보됐다.
