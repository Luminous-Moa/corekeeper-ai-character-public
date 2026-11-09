# Core resumable tunnel search

작업 위치: C:/Dev/CoreKeeperModSDK-path-opt
커밋: 하지 않음.

## 변경 파일
- Assets/AIPlayerSpike/Scripts/Core/AITunnelSearch.cs
- Tools/core-tests/TunnelSearchJobTests.cs (새 파일; Tools 아래이므로 Unity .meta 불필요)
- Tools/core-tests/Program.cs (부모 승인한 TUNNEL-JOB 등록 한 줄)

## 공개 API
- new AITunnelSearch.FindJob(start, target, lo, hi, cost, guardMax = 120000)
- new AITunnelSearch.FindWithDetourJob(start, target, lo, hi, cost, opensEdge, hitsOf, detourCheck, guardMax = 120000)
- 공통: bool Step(int workBudget), bool IsCompleted, List<(int x, int y)> Result, int LastStepWork
- FindJob: bool Capped, double ElapsedMilliseconds
- FindWithDetourJob: UsedEdge, EdgeClosed, Detour, FirstCapped, AllCapped, FirstEst, DetourEst, Passes, Ms1, Ms2
- Step은 완료 여부를 반환. 0 이하 budget은 no-op. 완료 후 Step도 no-op이며 LastStepWork는 0.
- 결과/판정은 IsCompleted 이후 사용. 콜백/지도 데이터는 job 동안 안정적이어야 하며 단일 thread에서 호출.

## 보존 및 프레임 분할
- 기존 heap 구현, 동점 처리, 이웃 +x/-x/+y/-y 순서, guard cap, target edge 예외 유지.
- 기존 Find/FindWithDetour/FindAvoidingEdge API와 결과 의미 유지. 동기 API는 job을 완료까지 실행.
- 한 work는 heap pop 및 최대 네 이웃 처리, 경로 역추적 하나, reverse swap 하나, hitsOf 호출 하나 또는 상수시간 phase 전이.
- 경로 복원/뒤집기, 첫 경로 hits 전체/마지막 제외 집계, 두 번째 경로 hits 집계까지 분할.
- 두 pass가 동일 budget을 공유. cost/opensEdge/hitsOf 호출 순서까지 원본과 동일.
- Ms1/Ms2는 각 pass의 실제 Step 탐색/경로복원 실행시간. 프레임 사이 대기 및 hits 집계는 제외. 생성자 상수시간 초기화는 포함하지 않음.
- budget은 work 개수 제한이며 콜백 개별 실행시간이나 dictionary/list 확장·GC까지 millisecond hard cap으로 제한하지 않음.

## 검증
- dotnet run -c Release --project Tools/core-tests -- TUNNEL-JOB: ALL CHECKS OK (135)
- 새 테스트는 수정 전 AITunnelSearch 전체를 TunnelSearchOriginal로 보존한 독립 oracle과 비교. 새 동기 wrapper를 oracle로 쓰지 않음.
- budgets 1 / 7 / 1000000: weighted tie, zero costs, no way, empty route, int boundary, cap 0/1/경계, target edge 예외, fallback, offered detour, detour 미요청, wall-trigger, skipLast target hits, 양쪽 no-way/cap, seeded map 20개.
- 비교 범위: 경로/우회 경로, 모든 결과 flags/estimates/pass 수, cost/edge/hits callback 전체 trace, 매 Step work 제한, 완료 후 멱등/시간 안정.
- 기존 관련 회귀: PG 128, G3 118, G3f 44, G3r 34, G3rr 40, G3f3 28, G3m 19, G3d 15, G3md 28, E2 35 = 총 489 checks 통과.
- dotnet build Tools/compile-check -c Release -nologo: warnings 0 / errors 0 (C# 8, netstandard2.1).
- git diff --check (소유 파일): 통과.
- 전체 core-tests 실행은 당시 병렬 변경 중인 AIBrain.Candidates.cs의 Items 메서드 변경으로 ChoiceFactsTests.cs:60 문자열 검색이 -1을 반환하여 ArgumentOutOfRangeException 중단. 부모에게 전달했으며 본 담당 범위 밖 파일은 수정하지 않음. 전체 통합 회귀 재실행은 부모가 수행해야 함.

## 통합 안내
AITunnel.PlanJob 담당자에게 API/예산 차감 방식을 전달함. 별도 독립 검수는 부모 통합 검수에 포함 필요.
