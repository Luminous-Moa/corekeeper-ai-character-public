# 공통 실행의 오프라인 진입 경로 검증

2026-11-07. 구현 담당과 별도 검증 컨텍스트에서 `Tools/execution-tests`를 만들고 실행했다. 검증 대상은 `C:/Dev/CoreKeeperModSDK-common-execution`이다.

## 확인 결과

기준 커밋 11eb461의 실제 `AIGoals.cs`와 `AIHostOrders.cs`로 구리 10/10 장면을 재현했다. 목표량 도달이 목표를 지우고 이미 고른 다음 채굴을 취소했으며, 같은 source를 가진 갑옷 부탁도 완료 처리했다. 세 확인이 모두 통과했다. 이 기대 결과는 보존 실게임 `Player.log` 750·755·759행의 문제와 대응한다. [기준 실행 로그](offline-baseline.log)

수정한 소스에서는 31개 확인이 통과했다. 실제 `AIGoals.CheckDone`의 수량 충족·feature 도착과 `CheckAfterBuild`의 무진척이 선택한 몸 동작과 목표 revision을 유지한다. 실제 `Apply` 분기로 재계획 요청, 몸만 중지, 다른 행동 시작, 현재 단계 완료를 실행했다. 현재 단계 완료 뒤 원래 갑옷 부탁은 열려 있으며 실제 `AIHostOrders.ApplyAnswer`의 `order=done`이 와야 닫혔다. 실제 `AISkill.ObserveOnce`와 `ObserveExecution`을 통해 조건을 한 번 보고하며, API retry가 일시적 호출 이유를 바꿔도 몸의 실행 관측 목록과 최근 관측·revision이 보존됐다. 사용자 직접 목표 해제는 몸 동작을 중단한다. [수정본 실행 로그](offline-changed.log)

추가 회귀에서 실제 `AIActions.Stale`와 `AIRunGate`가 관측한 실행41의 stop은 같은 실행에서 수락하고 실행42가 시작됐으면 거부했다. idle 당시의 stop도 새 실행을 중단하지 않는다. 실제 `Apply → AIBody.Stop → AIBaseGuard.OnStop → AIBody.EndSkill` 경로에서 AI 명시 stop, 단계 완료, 사용자 직접 해제는 문 닫기 tail을 즉시 끝냈다. TailAbort는 호출되고 새 tail은 시작하지 않으며, 실행 결과에는 문이 열린 채일 수 있다는 이유가 전달됐다.

## 실제 실행한 소스와 경계

최종 검수에서 문 닫기 tail의 원래 완료 상태가 복구되면서 과거 예약 선택이 다시 시작될 수 있는 경합을 추가 검증했다. 실제 `AIBrain.OnSkillEnded`와 `CancelPendingChoices`, `AbortRequest`, `DropQueued`, `SetReflex`, 판정 helper도 원문 추출해 실행한다. ordinary 완료 때는 기존에 선택한 다음 행동이 정상 예약되고, 명시 stop 때는 old queued·held·nextHit와 재실행 flag가 모두 사라진다. Goal.None 자기방어 중에도 사용자 해제는 tail과 과거 선택을 정리한다. 이 경로에서 OnSkillEnded는 stub을 사용하지 않는다.

프로젝트는 production `AIGoals.cs`, `AISkill.cs`, `AIGoalText.cs`와 Core 목표·원부탁·계획·실행회차 규칙을 직접 링크한다. runner는 production `AIBrain.Loop.cs`의 `Apply` 및 `ObserveExecution`, `AIActions.Stale`, `AIBody.Stop`와 `EndSkill` 전체 메서드, `AIBaseGuard.OnStop`과 enum, `AIDecisions.cs`의 데이터 클래스를 원문 그대로 뽑아 컴파일한다. 실행 정책을 검증 코드에서 다시 구현하지 않았다. [연결한 소스의 실행 시점 SHA-256](offline-source-hashes.json)

Unity 월드·가방 관측·물리 입력·네트워크/상위 planner 경계는 stub이다. 몸 동작에는 primary 입력을 내는 ProbeSkill을 사용했다. planner adapter는 실제 host-order 규칙에 source·완료 통지를 전달한다. tail 상태는 fixture에서 구성하며 실제 stop/end 로직이 읽고 갱신한다. 전체 `Tick` scheduling, candidate 생성·응답 parsing, stop 이외의 stale 판정, 실제 경로 탐색·채굴 타격·문 상호작용·인벤토리 트랜잭션·원격 모델 선택은 이 harness의 검증 범위 밖이다. 이 자료는 공통 정책과 명시 제어의 source-level 연결 검증이며 실게임 재실행을 대신하지 않는다.

빌드는 오류 0이었다. 테스트 경계 stub의 소문자 math 형식명과 쓰이지 않은 필드에 대한 컴파일 경고가 있었다. main 담당의 전체 제품 compile-check 결과와 별도로 취급한다.

## 재현

저장소 루트에서 다음을 실행한다. runner가 실행마다 production source를 갱신해 읽는다.

```powershell
./Tools/execution-tests/run.ps1 -Baseline
./Tools/execution-tests/run.ps1
```

첫 명령은 Git에서 11eb461의 원문 두 파일을 생성 폴더에 읽어온다. 두 번째 명령은 현재 소스를 실행한다. 생성 파일·bin·obj는 전용 `.gitignore`에 포함돼 있다. 게임·설치·모델 호출은 수행하지 않았다.

