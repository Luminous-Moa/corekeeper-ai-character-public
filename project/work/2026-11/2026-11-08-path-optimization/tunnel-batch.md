# Tunnel batch 공유 완료

생산 수정은 `Assets/AIPlayerSpike/Scripts/AITunnel.cs`에 적용했다. 기존 `AIObjects.BlockedInViewAt`을 읽고 재사용했다. 이번 인계에서는 AIObjects를 추가 수정하지 않았다.

## 통합 API

- `AITunnel.BeginBatch(World, Entity, float3? capturedPos = null)`
- `PlanBatch.StepCapture(int)`는 캡처 완료 여부를 반환하며 `Ready`, `Position`, `Start`, `LastStepWork`를 제공한다.
- `PlanBatch.BeginPlan(int2, int damageOverride = -1, bool allowEdge = false, bool detourCheck = false)`는 기존 형태의 PlanJob을 반환한다.
- 모든 job 완료 후 `BeginValidation()`을 호출하고 `StepValidation(int)`가 true가 될 때까지 진행한다. `ValidationCompleted && !Changed`를 확인한 뒤 결과를 사용한다.
- `Changed`와 `Reason`은 배치에서 각 job으로 전파된다. 변경된 배치의 job.Result는 null이다. 이 null을 도달 불가 경로로 채택하면 안 되며 caller가 배치 송신을 취소해야 한다.
- 검증 시작 후 새 job 추가는 거부한다. 완료 전 job이 있으면 BeginValidation은 예외를 발생시킨다.

## 동작

최초 위치/start/기본 피해/miningEffect/note/객체 footprint를 한 번 캡처한다. 각 타일·경계 사실은 첫 조회 값을 공유하고, 피해 override에 따른 hits/cost 및 visited/unknown/toolShort 진단은 job별로 계산한다. Cost/HitsAt/FillNext는 같은 불변 raw fact를 사용한다. 타일 reader는 검색 Step마다 갱신하며 타일마다 새 Unity query를 생성하지 않는다.

공유 job은 개별 검증을 생략한다. 마지막 배치 검증은 유일한 readTiles 전체와 읽은 edgeFacts를 한 번 검사한다. 객체 검증은 초기 Position에서 BlockedInViewAt으로 다시 조회한다. 현재 위치가 바뀌어도 공유 배치를 취소하지 않는다. standalone BeginPlan/Plan은 자기 배치를 소유하고 위치 변경까지 검증하며, 기존 BlockedInView 캡처를 유지한다. allowEdge만 사용하는 배치는 사용하지 않은 경계 정책 토글로 취소하지 않는다.

벽·위험·도구 선택 정보, 목표 자체의 edge 예외, 두 pass/우회 의미는 유지한다. 모든 Unity 접근은 호출자 메인 스레드에서 수행하며 caller가 기존 agent/edge context를 유지해야 한다.

## 검증

- 실제 생산 AITunnel의 TunnelPlan/Plan/BeginPlan/PlanBatch/PlanJob을 추출한 `tunnel-batch-harness`에서 `PASS 13926 checks`를 확인했다. 이전 동기 구현 Legacy.cs와 결과를 비교한다.
- budget 1/7/4096, 완료 멱등, standalone 실패 이유/벽·위험 parity, 벽 생성·tileset·객체·피해·경계·문·위험 변화, 이동 중 배치 유지, 명시 위치 캡처, 피해 override별 진단 독립, 경계 목표 예외의 allowEdge/detour 조합을 검사했다.
- 계수 검사에서 두 동일 job은 타일 fact를 한 번 읽고 배치 검증에서 유일한 타일을 한 번 더 읽었다. 공유 배치 객체 조회는 캡처 1회와 검증 1회였다.
- 실행은 `$env:DOTNET_ROLL_FORWARD='Major'; dotnet run -c Release --project <tunnel-batch-harness>`를 사용했다. .NET8 런타임이 없어 설치된 .NET10으로 실행했다. stub math 이름에 관한 CS8981 경고 1개가 있다.
- 최종 `dotnet build Tools/compile-check -c Release --no-restore` 성공, 경고 0개/오류 0개.
- 소유 생산 파일 `git diff --check` 통과.

## 검증 범위

lazy fact 캡처와 마지막 검증 모두 시간 구간에 걸친 관측이다. 변경 검출은 읽는 시점의 비교이며 원자 snapshot이나 epoch 일관성을 보장하지 않는다. 객체 재조회도 검증 구간에서 1회 수행한다. 이미 검증한 타일 또는 객체가 이후 바뀌는 경우까지 원자적으로 포착한다고 주장하지 않는다. caller가 최초 관측 위치와 입력 age를 표시하고 실행 시점 재검증을 유지해야 한다.

PlannedDamage, 최초/검증 객체 entity query, AITileReader 생성은 나눌 수 없는 기존 연산이다. work unit은 3ms hard guarantee가 아니다. 게임 runtime 프레임 측정 및 실제 AI/LLM 요청은 이번 단위검증에 포함하지 않았다.
