# Tunnel PlanJob 구현 결과

수정 소유 파일: `C:/Dev/CoreKeeperModSDK-path-opt/Assets/AIPlayerSpike/Scripts/AITunnel.cs`만 수정. 커밋/메타 파일 수정 없음.

## API

- `AITunnel.BeginPlan(server, player, target, damageOverride = -1, allowEdge = false, detourCheck = false)` → `PlanJob`
- `bool Step(int workBudget)`: 완료 여부. 0 이하 예산은 no-op.
- `IsCompleted`, `LastStepWork`, `Result`, `Reason`
- `RunUntilDone()`: 동기 실행. 기존 `AITunnel.Plan`은 이 경로를 사용한다.

## 구현

생성자는 입력과 빈 job-local 컬렉션만 보유한다. Unity/게임 상태 조회는 Step에서만 수행한다. 준비를 reader/start, damage, object observation, core job 생성의 네 단계로 나눈다. `FindWithDetourJob.Step`에 남은 예산을 넘기고 LastStepWork만큼 차감한다. 탐색 후 main/alternative 경로를 각각 한 타일씩 추가하면서 wall/hits/risk를 모은다. 완료된 plan의 RiskText는 동일 World와 wall count에 대해 사전 계산한 값을 사용하므로 큰 route의 위험 정보 조회가 완료 프레임으로 몰리지 않는다.

기존 Cost/door/edge 조건 및 실패 reason/log 문구는 보존했다. Cost, wall info, tool-short, object snapshot, risk lookup은 job 수명에 한정한다. Calls는 첫 양수 Step에서 한 번 증가하고 TwoPass는 탐색 완료 후 실제 Passes==2일 때 한 번 증가한다. searchMs는 프레임 대기를 제외한 active search Step 시간이다. LastToolShortRed는 탐색 진단 및 성공 완료에서 다시 설정해 route Fill 중 다른 job이 진단값을 바꾸더라도 최종값이 일치한다.

외부 호출자는 각 Step 전에 기존 agent/edge 문맥을 복원하고 world/goal/start/damage/inventory/observation 변화를 감지하면 job을 버려야 한다. 별도 스레드에서 실행하지 않는다.

## 검증

- `dotnet build Tools/compile-check -c Release --nologo`: 성공, 경고 0 / 오류 0 (최종 수정 후 재실행).
- `git --no-pager diff --no-ext-diff --check -- Assets/AIPlayerSpike/Scripts/AITunnel.cs`: 통과.
- 비용 조건/실패 문구는 기존 본문에서 그대로 이동한 것을 점검했다.
- 게임 안에서 프레임 지연 측정 및 Unity runtime PlanJob 비교는 수행하지 않았다.
- sibling resumable_search가 보고한 core oracle 비교(135 checks), 기존 경로 테스트(489 checks)는 별도 담당 검증이다.

## 범위상 남은 제한

기존 `AIObjects.BlockedInView` cache miss는 내부 entity query 전체가 동기 실행되는 API다. AITunnel.cs만 수정하는 범위에서 그 내부를 분할하지 않았으며 초기화의 한 작업 단위로 호출한다. PlannedDamage의 기존 memory/inventory 조회 역시 한 작업 단위다. 탐색/경로 복원/벽·위험정보 마무리는 큰 경로에도 예산 단위로 중단된다.