# 단계 0~2 구현 결과

2026-11-06. 승인 명세 `01-spec-goals-candidates.md`에 따라 `C:/Dev/CoreKeeperModSDK-refactor-20261106`, `refactor-maint-20261106`에서 구현했다. 기준 `a07166f38086e0241c37c601c16452664236813e`.

| 경계 | 커밋 | 책임과 변화 | 오프라인 관문 |
|---|---|---|---|
| R1 | `33f477b` | 목표 enum/읽기 전용 계약, 목표 문장, 기능 연결, 후보 후 목표 종료 책임 분리. C#/JS 공유 계약 fixture 추가 | 실패 0, core 1043, protocol 106 |
| R2a | `bcdec88` | 후보 인자를 실제 뜻에 맞게 분리하고 생산자→Make/Stale를 함께 변경. 문/문 수리/횃불의 명시적 역할 전달 | 실패 0, core 1045, protocol 106 |
| R2b | `f512924` | 호출 이유의 부서짐 의미와 벽의 위험 사실을 코드값으로 전달 | 실패 0, core 1048, protocol 106 |

전체 관문은 각 경계마다 Git Bash 절대 경로와 작업 전용 TEMP/TMP로 `Tools/core-tests/gates.sh`를 실행했다. R1/R2a/R2b 모두 compile-check, core-tests, exp-sim, recall-loop(41), bridge-wiring(23), memgraph(80), protocol(106), fake-bridge, logshape, movecheck 통과했다. 근거는 [R1 로그](r1-gates.txt), [R2a 로그](r2a-gates.txt), [R2b 로그](r2b-gates.txt). 기존 기대 로그와 golden 문구는 바꾸지 않았다.

## 구현 및 보존 근거

- R1: 새 `Core/AIGoalKinds.cs`에서 기존 enum 순서와 외부 10개 목표, None/Free 구분, host 별칭, 각 경계의 count 차이, 경험 key fallback을 보존한다. `AIGoalText`는 원래 삼항식 표시 본문을 옮겼다. `AIGoalFeatures`는 기존 네 후보 try/catch의 순서와 기능별 예외 경계, 완료/막힘 연결만 가진다. `AIGoals.CheckAfterBuild`는 기존 세 종료 블록을 옮겼고 only-wait는 Brain에 남겼다. 새 상태 저장소·위임 테이블·플러그인 등록은 없다.
- R1 계약 시험: `GoalContractTests`와 JS protocol 시험이 동일 fixture를 읽는다. bridge가 count 범위를 mod보다 느슨하게 받는 현재 경계, place의 StepForm 하한 부재, host gather fallback, None/Free 및 unknown enum 경험 key를 확인한다. 기존 brief/프롬프트/GOAL_DO는 변경하지 않았다.
- R2a: itemVariation/itemCount/edgeGap/waitSeconds, targetLabel/actionMode/exploreDirection, placeStandOnly/pickupStandOnly/retreatInside, placeStand/pickupStand를 실제 행동 의미에 맞췄다. 생성자 인자 순서·기본값·key override·Stale 경계는 유지한다. 생산 코드에서 eatId/eatVariation/item2/digWhat 참조는 없다.
- R2a: `EdgeCandidateRole`은 두 문 놓기 생산자, 두 문 재설치 생산자, 횃불 생산자에서 정해지고 AsmCand로 복사된다. IsRepair와 EdgeKindWord 및 기존 테스트 명령은 role을 읽는다. production에 문장 fallback은 없다. 과거 ASM fixture의 role 복원은 테스트의 `LegacyEdgeRole`에만 있다. 기존 replay 기대 로그는 그대로이며, desc를 모두 바꿔도 순서/할당량/cut이 같은지 확인한다.
- R2b: `_needBreach`는 캐릭터별 State에 있다. reason setter가 지우고 NewBreach 성공 직후에만 true로 설정한다. 일반 Tick→Send는 flag를 전달하고 prefetch 호출은 기본 false다. `_buildBreach`는 flag 또는 현재 plan의 pending을 읽는다. NewBreach/WithPendingBreach/TakePending 호출 시점과 planVersion 처리는 그대로다. 알려진 코드의 해당 문구 생산자는 Core의 BreachTrigger 한 곳이다.
- R2b: `EdgeWallFact`/`EdgeWallKind`는 기존 `Core/AIBaseGuard.cs` 안 순수 값이다. null→걸린 문 옆→문 자리 옆→안쪽이 아닌 경계→None의 기존 분기 순서를 유지한다. OpensEdge는 enum에서 계산하며 표시 문장을 읽지 않는다. Text는 기존 상수/formatter를 그대로 쓴다. 경로 벽 수집의 ok 제외/순서, MineEdgeFact의 문장, WithEdgeFact의 출력 중복 방지는 보존한다. GuardTests/GuardLiveTests/PlaceGoalTests에서 null·안쪽·무관한 칸·문 옆·걸린 문 옆·경계 벽 및 route ok 제외를 확인한다.

## 독립 검수와 미확인 범위

R1은 HEAD가 독립 검수 `05-review-r1.md`의 must-fix 0을 수용했다. R2a/R2b는 HEAD 검수 대상이다. 특히 R2a의 모든 생산자→실제 skill 생성자 인자 의미와 R2b의 reason 재설정·prefetch·pending wiring은 diff 대조 대상이다. 이를 시험하려고 새 실행 추상화나 정책을 넣지 않았다. 기존 GuardLive L7 및 FixMergeReview S5 시험은 이유 덮어쓰기 뒤 pending 복원/한 번 소비/plan 교체를 계속 검증한다.

의도한 게임 정책 변경은 없다. 승인된 명세대로 자유 문자열에 우연히 부서짐 문구가 들어 있다는 이유만으로 수리 우선권을 주던 의존은 제거했다. 알려진 생산자의 의미·표시 문장·후보 key와 순서·상한·경로 계산 시점·외부 JSON/저장은 보존한다.

새 `.cs` 세 개에 `.meta` 세 개를 추가했다. HEAD의 명시적 허용에 따라 기존 `.cs.meta`와 같은 형식에 새 고유 GUID를 생성했다. 기존 GUID는 수정하지 않았고 신규 source/meta 짝과 GUID 중복 없음을 검사했다. Unity import 보존 및 실제 설치 목록 포함은 이후 HEAD 게임 준비 단계에서 확인해야 한다.

SDK 로컬 설정 7개, Assets/Plugins/CoreKeeper와 지식 데이터는 준비된 환경으로만 사용했으며 커밋하지 않았다. Library를 생성하지 않았다. 본 저장소/다른 worktree/운영 AIPlayers/bridge logs/data를 수정하지 않았다. Unity·게임·설치·모델 호출은 실행하지 않았다. 실제 장면과 Unity import/게임 컴파일은 아직 확인되지 않았다. 단계 3~4는 적용하지 않았다.
