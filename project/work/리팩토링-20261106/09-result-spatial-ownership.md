# 단계 3~4 구현 결과

작업 경로 `C:/Dev/CoreKeeperModSDK-refactor-20261106`, 브랜치 `refactor-maint-20261106`. 기준은 단계 0~2 검수 완료 `f512924`이고, 최신 `03-spec-spatial-ownership.md`를 적용한다. 코드·시험만 이 작업에서 실행하며 Unity/게임/설치는 HEAD의 별도 실행 담당이 확인한다.

| 단위 | 커밋 | 검증 |
|---|---|---|
| M 파일 분리 | `fbb98b7` | 최종 전체 gates 실패 0, core 1048, protocol 106, compile 오류 0 |
| Q 공간 사실 | `3c31536` | 전체 gates 실패 0, core 1048, protocol 106, compile 오류 0 |
| R 이동 절차 | `a760da1` | 전체 gates 실패 0, core 1054, protocol 106, compile 오류 0 |
| O 준비 인자·위협 기록 | `902721e` | 전체 gates 실패 0, core 1062, protocol 106, compile 오류 0 |

## M

몸·입력/관측/개별 기술, 제작/놓기/파괴, 기억/물체/터널, 모드/패치/저장을 기존 형식 경계에서 분리했다. 몸 명령과 모드 채팅 method/property만 partial로 옮겼고 정적 상태는 기존 본체에 남겼다. AIEdge의 reach부터 CannotCome까지도 같은 partial로 옮겼다. [이동 manifest](09-move-manifest.md)에 원본→대상 형식/멤버를 기록했다.

Roslyn으로 직접 형식 멤버의 토큰을 비교한 멀티셋 차이는 0(`m-token-check.txt`)이다. 초기화 순서는 별도로 이동 대상 38개 형식의 field/property initializer 선언 토큰 시퀀스를 비교해 동일 순서를 확인했다(`m-field-order.txt`). 이 검사는 원본 f512924와 현재 분리된 소스를 읽어 비교했으며 게임 상태를 쓰지 않았다. `m-movecheck.txt`의 실제 잔여는 using/주석/허용 partial 선언/신규 meta다. `git diff --check`도 통과했다.

최초 전체 관문에서 source-wiring 단언 한 개가 옮겨진 AIChargeWatch.Reset 위치를 못 찾았다. CombatS2Tests가 AIObjects와 모드 본체+Patches의 정확한 새 소유 파일을 읽도록 바꾸고 단언/기대값은 유지했다. 최종 [전체 로그](m-gates-final.txt)는 모두 통과했다. compile-check, core-tests, exp-sim, recall-loop 41, bridge-wiring 23, memgraph 80, protocol 106, fake-bridge/logshape/movecheck selftest 포함이다.

신규 소스 23개에 meta를 만들고 기존 두 파일은 이름 변경과 함께 GUID를 보존했다(AIBodySkills→AIBody, AIExplore→AIMemory). source/meta 짝 누락 0, GUID 중복 0. 새 Unity import와 설치 manifest 포함은 실제 게임 준비 때 확인한다. 기존 실게임 로그/미관측 연결과 최종 E2E 요구는 `08-live-evidence-plan.md`를 따른다.

SDK 로컬 설정 7개 및 게임 참조는 준비된 환경으로만 쓰고 커밋에서 제외했다. 본 저장소·다른 worktree·운영 데이터는 수정하지 않았다.

## Q

`AIDoorFacts`는 DoorCD→GateCD 순서의 칸별 마지막 문, 전체 ObjectData/LocalTransform 쿼리의 첫 문, 한 칸에 하나라도 열린 문이라는 서로 다른 계약을 유지한다. `AIEdge.DoorAt`은 기존 DoorView를 계속 반환한다. 범위→제외→variation 순서를 보존하기 위해 OpenTilesExcept의 제외 인자는 `t => S.doors.ContainsKey(t)`로 넘겼다. query 실패에 새로운 catch나 fallback을 넣지 않았다.

`AIWorldFacts`는 생물/타일 제외 순서와 prefab/문 판정을 공유한다. ObjectsInWindow의 variation fallback/collider/TheCore 및 PlacedAt 첫 선택/PlacedOn 칸별 첫 선택의 차이는 남겼다. IsKnown은 BuildGrid와 AIPlaceSpot.Grid에만 적용했다. GuardGridAround는 inView를 g.view와 DoorCell에도 재사용하므로 그 식을 유지하는 HEAD 보정을 반영했다.

새 두 파일의 meta와 source 짝/중복 GUID를 검사했고 [Q 전체 로그](q-gates.txt)가 모두 통과했다. query 순서와 필터 순서는 diff 대조 근거이며, 게임 API를 흉내 내는 새 순수 시험은 만들지 않았다. 겹친 엔터티·자기/남의 문·관측/기억 재전환 장면은 실제 실행 담당의 확인 범위다.

## R

`AIApproachSearch.FirstOfThree`와 게임 연결 `AIApproach`를 추가했다. 명세의 다섯 루프만 처음 세 설 칸 중 첫 성공을 고르는 공통 절차로 바꿨다. 기억 상자 두 곳/솥/제작대는 PlanC, 밭은 직접 AITunnel.Plan을 그대로 호출한다. 설 칸 생성·정렬·점유 사본·현재 key·중복·실패 로그와 재료 확인 순서는 호출부에 남아 있다.

보행 후보 공통 필드 여덟 곳을 WalkCandidate로 만들고 설명은 호출부에서 그대로 계산한다. 원본 `3c31536`의 여덟 desc 표현식과 새 helper의 인자 토큰이 전부 같음을 Roslyn으로 확인했다(`r-description-tokens.txt`). Home/Place의 detour=true와 alt/current key 비교, RouteFact, ConvertAll→DoorsOn→alt 순서는 유지했다. digEdgeOk 목록을 먼저 계산하고 같은 후보에 대입하는 지역 변수만 추가했다.

새 순수 시험 6개는 빈 목록, 첫 성공, 세 번째 성공, 네 번째 미시도와 마지막 결과, 두 칸 목록, 동일 예외 전파를 호출 기록/반환값으로 확인한다. [R 전체 로그](r-gates.txt)는 모두 통과했다. 새 두 파일의 source/meta 짝과 GUID 중복도 문제없다. 후보 문구/key/순서와 PERF 호출 수의 실제 장면 대조는 HEAD 실행 담당 범위다.

## O

Send에서 같은 시점에 계산한 prefetch/breachCall을 BuildCandidates→CandBuild와 도구 재귀로 직접 전달한다. State의 _buildPrefetch/_buildBreach와 forwarding property를 제거했다. _needBreach, _reqPrefetch, _ctx/_building, 준비 결과/카운터 수명은 그대로다. AsmIn의 prefetch는 마지막 인자로 받아 기존 로그 형식을 유지한다. 원래 여섯 후보 읽기와 AsmIn 한 곳, breach 조립 한 곳 외 다른 사용처는 없었다.

위협 판단은 AssessThreatWithinRange→ObserveThreat→AIThreatHistory.Record로 구분했다. 범위 밖 null은 Record를 호출하지 않고 false로 돌아간다. ObserveAnyThreat 전체 열거와 Tick Exists/자기방어 Find의 단락 평가는 그대로다. CloseFight/SEND 목록은 마지막 기록만 읽고 새 refresh나 snapshot을 만들지 않았다. 이전 공개 Threat/AnyThreat/CannotCome는 호환 위임으로 남았다. ClearMemory/Import에 초기화는 추가하지 않았다.

새 순수 시험 8개는 미판정, false 기록, 반복 key, true 제거, 인스턴스 격리, 64/65개 경계와 다음 Record 시 clear(반복 key 및 true 입력 포함)를 확인한다. 의미와 제한은 기존과 같으며 이전 기록을 항상 최신 snapshot으로 바꾸는 기능은 추가하지 않았다.

[O 전체 로그](o-gates.txt)는 모두 통과했다. source/meta 짝 누락과 GUID 중복은 0이고, `git diff --check`도 통과했다. 제품 작업 변경은 모두 커밋했으며 마지막 status는 준비된 SDK 설정 7개와 Assets/Plugins/CoreKeeper만 남았다.

## 독립 검수·실제 실행 인계

각 커밋을 HEAD에 즉시 전달했다. 독립 검수 수용 여부는 HEAD 기록이 정본이다. 코드에서 별도 정책 변경은 하지 않았고 source query/후보·경로 호출/정적 초기화·상태 수명의 차이를 위와 같이 보존했다. 최종 `902721e`와 [이동 manifest](09-move-manifest.md)를 개발 지식그래프 담당에 전달하면 source 앵커와 config를 갱신할 수 있다. 그래프 결과물은 이 작업에서 수정하지 않았다.

Unity import/설치 manifest/게임 자체 컴파일, 실제 문 통과·닫기/stop, 저장 후 완전 종료·재입장 복원, 시설 재접근·밭, 두 캐릭터와 늦은 답 폐기 등은 아직 이 코더가 실행하지 않았다. 실제 장면 확인 여부는 오프라인 관문과 구별하며 `08-live-evidence-plan.md`에 따른 HEAD의 실행·복구 절차가 남아 있다. 이 보고는 실제 게임 동작까지 검증했다고 주장하지 않는다.
