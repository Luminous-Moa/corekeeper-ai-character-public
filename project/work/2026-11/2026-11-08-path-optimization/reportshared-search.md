# 공유 터널 검색 코어

## 변경 파일

- `Assets/AIPlayerSpike/Scripts/Core/AITunnelSearch.cs`에 `SharedSearchJob`, `SharedQueryJob`을 추가했다.
- `Tools/core-tests/SharedTunnelSearchTests.cs`와 `Program.cs`의 `TUNNEL-SHARED` 그룹을 추가했다.

## API와 처리 방식

`SharedSearchJob(start, lo, hi, cost, opensEdge = null, guardMax = 120000)`을 시작점·비용 그래프마다 생성한다. `opensEdge`를 전달하면 경계를 닫은 트리, 생략하면 경계를 허용한 트리다. 호출자가 `Step(workBudget)`을 반복하여 완료한다. 비용과 경계 콜백은 타일마다 메모하며 음수 비용은 통과할 수 없다. 하나의 작업 단위가 최대 네 이웃의 비용을 평가한다.

완료 후 `CreateQuery(target, hitsOf = null)`로 목적지별 `SharedQueryJob`을 만들고 `Step(workBudget)`으로 복원한다. 결과에는 `Result`, `TotalCost`, `Hits`, `IntermediateHits`, `Capped`, `EdgeClosed`가 있다. `IntermediateHits`는 목적지 타일의 채굴 횟수를 제외한다. 쿼리는 최단경로 검색을 실행하지 않는다. 네 이웃을 확인하는 준비 단계, 타일 하나의 부모 추적·타격 횟수 누적, 순서 뒤집기의 한 번 교환을 각각 작업 단위로 센다.

닫힌 경계 목적지는 네 이웃의 확정 최단거리 중 최소값에 목적지 진입 비용을 더하여 마지막 한 타일로 연결한다. 이 타일을 통과하여 다른 목적지로 가는 부모 경로는 생성되지 않는다.

검색 및 쿼리는 `IsCompleted`, `LastStepWork`, `ElapsedMilliseconds`를 제공한다. 검색은 `HeapPops`, `ExpandedTiles`, `CostCalls`를 추가로 제공한다. 완료 후 재호출과 0 이하 예산은 작업량이나 실행 시간을 늘리지 않는다. 모든 콜백과 그래프는 작업 수명 동안 안정적이어야 하며 호출은 한 스레드에서 진행한다.

## 기존 동작과의 차이

- 기존 `FindJob`, `FindWithDetourJob`은 그대로 유지했다.
- 같은 비용의 여러 최적 경로 중 선택되는 경로는 달라질 수 있다. 반환한 실제 경로에서 타격 횟수를 누적한다. 통합 계층도 그 경로의 위험 사실을 계산해야 한다.
- `EdgeClosed`는 공유 트리 전체에서 관측한 고유 경계 타일 수에서 현재 목적지만 제외한다. 목적지별 검색이 종료되기 전까지 관측한 기존 수치와 달라질 수 있다.
- 트리가 cap에 걸려도 확정된 목적지 또는 확정 이웃으로 최적성이 입증된 경계 목적지의 쿼리는 정상 결과를 반환한다. 미확정 경로는 `Capped=true`다. 차단된 목적지와 범위 밖 목적지처럼 도달 불가가 직접 입증되는 경우는 `Capped=false`다. `guardMax=0`의 `start==target`은 capped를 유지한다.
- 누적 비용은 `long`을 사용한다. 큰 양수 타일 비용이 음수로 넘치지 않는다. 좌표 이웃 계산도 `long`에서 범위를 검사한다.

## 검증 결과

- `dotnet run -c Release --project Tools/core-tests -- TUNNEL-JOB`에서 기존 135개 검증이 통과했다.
- `dotnet run -c Release --project Tools/core-tests -- TUNNEL-SHARED`에서 신규 18개 검증이 통과했다.
- `dotnet run -c Release --project Tools/core-tests`에서 전체 1571개 검증이 통과했다.
- 신규 그룹은 고정된 기존 구현 `TunnelSearchOriginal`과 무작위 지도 12개, 목적지 120개, 열린·닫힌 두 정책, 예산 1·7·1000000을 조합한 8640개 목적지 비교를 수행했다. 도달 가능성, 최적 비용, 경로 유효성, 실제 타격 횟수, 예산 상한을 확인했다.
- 목적지 경계 예외, 경계 뒤의 다른 목적지 차단, cap, 시작점과 목적지 일치, 범위 밖 시작점, 0 비용, 좌표와 비용 overflow, 긴 경로 분할 복원, 우회 판단과 목적지 타격 제외를 검증했다.
- 알려진 타일 30492개와 보호 경계 목적지 7526개의 합성 지도에서 두 트리와 15052개 경로 쿼리를 약 272ms에 완료했다. heap pop은 53458회로 `2 × 30492` 이하였고 비용 콜백은 61152회로 두 그래프의 전체 사각형 크기 이하였다. 목적지 쿼리 중 heap pop과 비용 콜백은 추가되지 않았다. 시간은 이 실행 환경의 합성 측정치다.

실제 게임의 배치 통합과 최종 실기 측정은 부모 작업 및 AITunnel 통합 담당자의 범위다. 커밋은 생성하지 않았다.
