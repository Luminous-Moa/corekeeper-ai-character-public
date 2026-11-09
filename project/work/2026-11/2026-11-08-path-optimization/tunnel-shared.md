# Tunnel shared-search 통합 결과

생산 수정 파일은 `Assets/AIPlayerSpike/Scripts/AITunnel.cs`다. 기존 `AIObjects.BlockedInViewAt`과 snapshot/unique validation을 재사용했다. 코어 검색 클래스는 별도 담당자가 수정했다.

## 구현

PlanBatch는 effectiveDamage별 SharedTrees를 보유한다. 생산의 같은 피해량 배치에서는 닫힌 경계와 열린 경계 SharedSearchJob을 각각 최대 1개 생성한다. 경계 정책이 비활성 상태면 두 모드를 하나의 열린 트리로 처리한다. TEST ONLY damageOverride는 각 피해량별 트리 쌍을 가진다.

배치 BeginPlan은 SharedPlanSearch를 사용한다. 대량 PreparePlan iterator가 처음 실제로 필요해질 때 StepSearches(32)를 반복해 기본 피해량의 두 트리를 예열한다. 예열 전 Prep/생활의 소수 동기 PlanC는 BeginSinglePlan을 사용해 배치 고정 facts에서 기존 FindWithDetourJob으로 해당 목표만 검색한다. 이후 후보 생성의 동기 PlanC 소비자도 완성된 트리에서 경로만 복원한다. 예열을 생략한 비생산 호출도 PlanJob.Step 예산 안에서 공통 트리를 완성할 수 있다. 경계벽 목표는 closed tree에서 terminal-target 예외로 해결한다. 목표 인접 4칸의 확정 최저 거리와 목표 비용으로 복원하며 목표별 재검색을 시작하지 않는다.

첫 경로 실패 시 열린 트리 fallback, 긴 우회 또는 중간 채굴벽이 있는 경로의 대안 검사, 추정 길이 차이 기준은 기존 AITunnelSearch 함수를 그대로 사용한다. target 자체 벽은 IntermediateHits에서 제외한다. 주 경로와 대안 각각 실제 선택된 타일 경로의 벽·타격·위험을 누적한다. standalone BeginPlan/Plan은 기존 FindWithDetourJob을 사용한다.

배치 Cost/HitsAt/FillNext와 tree callbacks는 같은 불변 raw tile facts를 읽는다. 각 쿼리 완료에 전체 공유검색의 관측 진단을 복사하며 TunnelPlan.sharedSearch로 해당 범위를 표시한다. 실패 reason과 경계 로그는 공유 검색 전체에서 관측한 도구 부족, 미관측 경계, 벽 정보 미확인, 탐색 상한을 구별한다. 상한으로 미증명인 목표를 도달 불가로 확정하지 않는다. 완료 트리가 상한에 도달했더라도 이미 최단거리가 증명된 목표는 유효한 결과를 낸다.

각 Step의 타일 reader는 실제 신규 tile fact를 읽을 때만 생성한다. 완료 트리의 캐시만 사용하는 경로 복원 Step은 Unity registry query를 다시 만들지 않는다.

## 외부 API

기존 BeginBatch/StepCapture/BeginPlan/PlanJob.Step/BeginValidation/StepValidation API를 유지한다. 추가 `PlanBatch.StepSearches(int)`는 기본 피해량의 closed/open 트리 예열 완료 여부를 반환한다. StepCapture 완료 뒤 소수 계획은 BeginSinglePlan을 사용할 수 있다. 대량 후보 준비는 첫 실제 경로 필요 시 StepSearches(32)를 완료한 다음 공유 BeginPlan을 사용한다. LastStepWork가 실제 소비 budget을 표시하며 비양수 예산과 완료 후 호출은 no-op이다. 모든 job 완료 후 배치 readset을 한 번 검증하며 Changed면 전송을 취소해야 한다.

추가 조회 속성은 다음과 같다.

- `PlanBatch.SearchesReady` bool, 기본 피해량의 공유 트리 예열 완료 여부
- `PlanBatch.BeginSinglePlan(...)`, 배치 facts/최종검증을 공유하며 target별 FindWithDetourJob과 job별 진단을 사용하는 소수 계획 API
- `PlanBatch.SearchCount` int, 실제 생성된 공유 검색 트리 수
- `PlanBatch.ExpandedTotal` long, 트리들의 실제 ExpandedTiles 누적값
- `PlanBatch.CapturedTileCount` int, 공유 tileFacts에 기록된 고유 타일 수
- `TunnelPlan.sharedSearch` bool, toolShortSeen/edgeClosed가 공유 검색 관측 범위임을 나타내는 진단 metadata

부모에게 Candidates.cs의 성공 후보 도구 부족 설명도 sharedSearch일 때 공유 관측 범위를 명시하도록 전달했다.

## 검증

`tunnel-shared-harness`는 현재 생산 TunnelPlan/PlanBatch/PlanJob과 실제 Core/AITunnelSearch.cs를 추출한다. Unity world 조회만 stub으로 치환한다. 이전 동기 LegacyAITunnel 구현을 비교 대상으로 유지한다.

- 최종 실행 `PASS 72084 checks`
- 150,000 알려진 타일에서 5칸 목표 BeginSinglePlan은 SearchCount 0, ExpandedTotal 0, CapturedTileCount 50 미만으로 완료한다. single 이후 shared를 쓰는 혼합 배치와 초기 위치 기준 최종 검증도 통과했다.
- 기존 standalone route/diagnostic/risk parity, snapshot 변화 검출, 예산 1/7/4096, 완료 멱등, 이동 중 초기 scope 유지, damageOverride 분리, target 경계벽 예외를 확인했다.
- 닫힌 경계의 긴 우회와 짧은 채굴 대안을 함께 제공하는 사례에서 기존 결과와 같고, 주 경로 무위험/대안 explosive 위험이 분리되는 것을 확인했다.
- 목표 자체 벽만 채굴하는 짧은 경로는 다른 경계벽이 관측돼도 추가 우회 검사를 시작하지 않는다.
- 150,000타일 사례에서 공유 트리의 120,000 확장 상한 뒤 먼 미증명 목표는 상한/미확정으로 보고하고, 확정된 가까운 목표는 같은 트리로 성공한다.
- 독립 검수가 지적한 closed graph 완전 소진 / open graph capped 조합을 수정했다. 최종 열린 query가 capped이면 첫 query의 cap 여부와 관계없이 미확정이다. closed 50칸 소진 + open 120,000 상한 회귀검사에서 상한/미확정 reason을 확인했다. standalone legacy의 두-pass 상태 정의는 유지했다.

요청한 규모의 생산 통합 스트레스 결과는 다음과 같다.

| 항목 | 결과 |
| --- | --- |
| 알려진 타일 | 30,492 |
| 목표 | 7,526, 모두 경계벽 |
| 생성 공유 검색 | 2 |
| 총 확장 타일 | 30,666 |
| 캡처 고유 타일 | 31,191, 미관측 경계 포함 |
| 전체 복원 경로 타일 | 1,291,721 |
| query와 마지막 검증 완료 시간 | 312.1ms |

StepSearches(32) 예열의 매 Step 예산을 검사했다. 이후 모든 목표에서 ExpandedTotal이 예열 완료값에서 전혀 증가하지 않음을 확인했다. 모든 목표 경로 성공, 끝 타일 보존, 선택된 경로의 실제 hits/risk, 최대 2×map 확장 이내 조건도 매 목표마다 확인했다. 마지막 검증의 타일 읽기 수는 최초 고유 fact 읽기 수와 동일했다. 이 시간은 .NET10 stub 실행 단일 측정이며 게임 프레임/CPU 측정값은 별도 실기에서 확인해야 한다.

실행은 `$env:DOTNET_ROLL_FORWARD='Major'; dotnet run -c Release --project <tunnel-shared-harness>`를 사용했다. stub math 타입 이름 경고 CS8981 1개 외 테스트 오류는 없다. 최종 생산 `dotnet build Tools/compile-check -c Release --no-restore`는 경고 0개/오류 0개로 성공했다. AITunnel/AIObjects 소유 파일의 diff --check도 통과했다.

## 범위

동률 최적 경로의 타일 배열은 target별 검색과 달라질 수 있다. HEAD가 허용한 동일 최단비용과 선택권, 실제 선택 경로의 위험 정보 조건을 적용한다. 코어 담당자는 별도로 8,640 all-target oracle 비용/도달성 비교와 실제 hits 검증을 보고했다.

초기 snapshot과 최종 unique validation은 시간 구간에 걸친 관측이다. epoch 원자성 보장은 기존과 동일하게 없다. 객체 entity query, 피해량 조회, 타일 reader 생성은 개별 기존 Unity 연산이며 3ms hard guarantee로 표현하지 않는다. JSON/API 후보 개수 정책은 이번 수정 범위에 포함하지 않았다.
