# 11월 8일 Gemini 응답 뒤 응답 없음 — 코드 경로 조사

읽기 전용, 현행 소스 `7af8f91e5d26e4fd9ce5996809ae41e213340118`, Unity6000.0.59f2. 코드/설치/설정 변경·게임 조작·네트워크 호출 없음. 독립 임시 PowerShell로 캐시 정책만 재생했다.

## 결론

**Gemini가 정상 응답한 뒤 하위 행동 판단의 후보 준비가 게임 메인 스레드를 수초~십여 초 막은 사실이 두 실행에서 반복 확인된다.** 최종 로그 뒤 코드에는 2,179개 탐색 후보의 길을 다시 계산하는 구간이 있다. 작은 전체삭제 캐시 때문에 같은 준비 안의 재검색도 회피되지 않는 구조가 확인됐다. 이 경로가 응답 없음의 가장 강한 설명이다.

사용자는 응답 없음 뒤 직접 종료했다. runtime 담당은 Windows1002/1001 AppHangB1 두 건을 확인했다. Native crash/OOM/무한 루프가 입증된 것이 아니다. 마지막 순간 stack과 전체 준비 완료시간이 없어 **정지한 정확한 명령은 미확정**이다. 과거 보고서434.5ms를 이번 완전정지 원인으로 재사용하지 않는다.

## 실제 타임라인

보존 원본: `evidence/20261108-080811-1-Player.log`와 `-2-Player-prev.log`. 추출본 `code-timeline-extract.txt`; 아래 t는 게임 실행 후 초다.

| 단계 | 최신 실행 | 이전 실행 |
|---|---|---|
| 상위 답변·목표 설정 | t84.4,12.9초 뒤200,give CopperBreastArmor1→Luminous | t83.2,14.0초 뒤200,give WoodBreastArmor1→Luminous |
| 첫 하위 후보 준비 | 2,269.5ms,그중길계획2,182.5ms,707검색/캐시hit0,452후보 | 2,298.8ms,길계획2,201.4ms,707검색/캐시hit0,452후보 |
| 요청 뒤 | t86.8 하위HTTP400,3초뒤재시도 | t87.4 하위HTTP400,3초뒤재시도 |
| frontier 검색 자체 | 9프레임,3,462타일,41ms합계,2,179곳 발견 | 9프레임,3,462타일,39ms합계,2,179곳 발견 |
| 마지막 대량 후보 준비 | t103.2 frontier loop12,349ms,found/offered2,179 | t104.3 frontier loop12,762ms,found/offered2,179 |
| 최종 행 | 같은 t의 `BRAIN: blocked targets: 기억한 광석 (32,8)...` | 같은 내용 |

두 실행 모두 DECISION#2 SEND와 실제 건네기 행동 APPLY가 없다. **목표를 받은 것과 물품 이동이 실행된 것은 다르다.** 최초452후보 준비가 짧은 정지, 실패 후2,179후보 준비가 더 긴 정지로 이어지는 관측 순서다.

`expanded 0`은 계산이 없다는 뜻이 아니다. 기존 frontier 결과를 사용하면서 그곳까지의 **목적지별 PlanC 계산**에12초가 쓰였다. 별도의 FrontierJob 자체가 끝나는 데 쓴39~41ms와 혼동하면 안 된다. offered는 PlanC 성공 후 added++ 수이므로2,179개 경로 준비 성공을 뜻하며 최종 HTTP choices 개수는 아직 아니다.

## 코드의 인과 경로

소스 공통 루트: `C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/`.

1. `AIPlayerSpikeMod.Update:226`→`Step3_Drive:988–990`이 Brain/Body를 순차 실행한다. `AIPlanner.Apply:680–685`가 give 목표를 설정한다. 이때 상위HTTP는 이미 완료됐다.
2. 다음 Brain.Send(`AIBrain.Loop.cs:663–717`)가 메인 스레드에서 관측/BuildCandidates를 실행한다. `BuildCandidates:1246–1254`는 give일 때도 wall/ore/items/explore를 생성한다. `CandBuild.Explore:615`의 wantExplore=true는 목표 독립성 요구를 반영한 동작이다. 단순히 give일 때 탐색을 삭제하면 기존 선택지 의미가 바뀐다.
3. `AITunnel.FrontierJob:201–207`은 발견한 각 좌표를 `_best[int2]`에 모아 모두 Result로 반환한다(227–229). 주석에 “one per direction”이 남아 있어도 현행 구현은 모든 좌표다. 이 월드에서는2,179곳이었다.
4. `AIBrain.Candidates.cs:689–700`은 각 frontier마다 `PlanC`를 동기로 호출한다(693). 최초재시도는 이 loop에12.349/12.762초를 썼고 그뒤701–702의 giant log가 남았다. 여기까지는 추론이 아닌 완료된 구간의 실측이다.
5. `Assemble:1118`의 blocked targets 로그가 마지막이다. 그뒤 Gather 전용 재귀조건1122는 이번 Give에서 실행되지 않는다. Finish→생활목록→위험문구 생성이 남아 있다.
6. `Assemble:1157–1163`은 모든 살아 있는 dig후보에 **PlanC를 다시 호출**해 위험문구를 붙인다. AIEdge.PlanActive면 앞선1146–1153도 별도의 재호출이다. 실제로 어느 재호출에서 종료됐는지는 stack이 없어서 미확정이다.
7. `AIBrain.Loop.cs:1115`는 성공캐시를 평화5초만 유지하며,1122는 `plans.Count > 64`이면 캐시 전체를 지운다.2,179개 순회 중 여러 번 전체삭제하고,12초 loop는5초TTL도 초과한다. 앞서 만든 경로를 최종 설명 단계가 다시 요청할 때 캐시가 살아 있을 것을 기대할 수 없다.
8. 캐시miss는`AITunnel.Plan:243–349`→`Core/AITunnelSearch.FindWithDetour:95–119`→`Find:26–63`의 동기 heap/Dictionary 검색이다. 검색마다 최대120,000 heap pop이며 request 전체 시간예산은 없다. 벽/타일 읽기도 EntityManager를 사용한다. 단순 Task.Run 포장으로 옮길 수 없다.

## 오프라인 확인

`replay-cache.ps1`은 두 보존 로그의 실제 마지막 frontier 좌표 순서 2,179개를 파싱해 `Count>64→Clear` 정책을 두 번 순회한다. Unity/API/게임을 호출하지 않고 로컬에서 정상 종료했다. 결과는 `cache-replay.json`.

| 재생 조건 | 첫 순회 | 같은순서 두번째 순회 |
|---|---|---|
| 빈캐시 시작,모든TTL이 영원히 유효하다고 낙관 | 2,179miss/0hit,33회Clear | 2,179miss/0hit,34회Clear |
| 같은build의 첫결과를별도로전부보존 | 2,179개보존 | 2,179hit |

두 로그의 좌표는 각각 2,179개 모두 유일했다. 재생은 캐시 효율 붕괴를 확인한 것이며, 실제 타일 비용/검색 CPU/마지막 hang을 완전히 재현한 것은 아니다. 실제 시작 캐시·추가 dig 후보·edge pass·시간 만료는 생략했다. 게임 snapshot에 전체 타일별 비용이 남아 있지 않아 실제 검색의 정확한 무게임 재현은 이번 자료만으로 불가능하다.

## HTTP400와 다른 원인 범위

상위Gemini200과 하위Decisions400은 별도 요청이다. 하위BuildJson(`AIBrain.Loop.cs:1459–1493`)은 전체 choices를 보내며 quote/backslash/control문자를 escape하지만 개수/길이의 client 사전검사는 없다.452choices·약19,922 inputChars가 전송됐다는 사실까지 확인된다. 서버의 개수/스키마/모델/총길이 제한 또는 다른400사유는 raw body 미보존으로 확정할 수 없다. inputChars는 후보description을 포함한 요청 전체byte수가 아니다.

`AIDecisionsApi.Read:51–64`는 응답text를 읽지만400에서 error.message만 노출하고,`AIBrain.Loop:789–791`은 빈message를 남겼다. 요청전체와 원문오류를 보존하지 않아 사유를 복구할 수 없다. 400뒤 새전체준비를 돌린 것이 대량계산의 두번째 방아쇠라는 로그 순서는 확인된다.

무한loop/lock/native crash/OOM 증거는 발견하지 못했다. Find의heaploop는guard,FrontierJob은유한기억그래프,Finish의value충돌loop는이번수천개로 intoverflow에 이를 규모가 아니다. GC/엔진jobs대기/거대로그/위험prefab조회는 추가비용일 수 있으나 마지막stack 없이 주원인으로 확정하지 않는다.

## 우선 수정 제안 — 이번에는 미적용

1. **같은 후보 준비에서 생성한 경로 객체를 보존해 최종 설명에 재사용한다.** AICandidate 또는 build별 Dictionary에 primary/alt/위험 사실을 연결한다. 전역의 짧은 TTL·64개 캐시를 재통과하지 않게 한다. 캐시 용량만 올려도 5초 TTL 문제가 남으므로 그것만으로 끝내지 않는다.
2. **목적지별 검색과 전체 후보 준비를 취소 가능한 단계 작업으로 분할한다.** 모든 AI의 합산 프레임 예산, 시간 검사/검색 continuation, revision 검증이 필요하다. frontier 찾기만 분할한 현재 상태로는 이후 2,179개 Plan 검색이 다시 한 프레임에 모인다. 선택지 의미를 보존하며 계산 방식을 바꾼다.
3. **동일 관측의 여러 목적지는 공유 최단경로 탐색/immutable 비용 snapshot을 재사용한다.** FrontierJob가 이미 경로 비용을 계산한 점을 활용할 수 있지만 문/경계 회피·우회 대안·위험 정보의 의미를 유지해야 한다. worker 전환 시 Unity 읽기는 메인에서 완료한다.
4. **400 재시도마다 무조건 새 대량 후보를 계산하지 않는다.** 관측/목표 revision이 유효하면 완성된 snapshot/경로를 재사용하고, 요청 전 유효성 검사 및 원문 error body의 안전한 로깅으로 서버 거절 사유를 분리한다. 오래된 행동 실행은 기존 stale 검증을 유지한다.
5. 재현 검증은 동일 보존 월드와 Give 목표에서 후보 수/내용을 유지한 채 구간별 시간·cache hit·총 Plan 횟수·메인 thread 최장 시간을 비교한다. 400 실패 주입은 로컬 mock으로 가능하며 실제 요청 없이 재준비 폭증을 검증할 수 있다. 실게임 재현은 후속 수정·검증 단계로 남긴다.

현 증거는 **응답 후 로컬 판단 준비의 대량 동기 검색+재검색**을 구체화한다. 마지막 stack 부재로 특정 줄의 무한 실행이나 영구 정지를 확정하지 않는다.
