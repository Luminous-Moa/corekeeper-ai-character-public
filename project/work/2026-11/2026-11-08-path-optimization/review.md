# 길 계산 최적화 독립 검수

검수 위치 `C:/Dev/CoreKeeperModSDK-path-opt`, branch `ai-path-optimization-20261108`. 독립 검수자는 구현 소스를 수정하지 않는다. [기준](review-criteria.md)을 적용하며 구현자와 직접 발견을 교환하고 수정 후 다시 읽는다.

## 상태

초안 검토 중. 아래 수용 전 보완 사항을 owner에게 전달했다. 최종 diff·생산 통합 시험·컴파일 증거를 검토하기 전 통과로 판정하지 않는다.

## 최초 초안의 구체 발견

1. **여러 프레임에 걸친 tile 사실 혼합(P1).** AITunnel.PlanJob.Cost가 tileCost와 wallInfo를 만들지만 HitsAt/FillNext는 live AITileReader로 벽을 다시 읽는다. 검색 때 바닥이던 칸에 yield 뒤 새 tileset 벽이 생기면 FillNext의 `wallInfo[w.tileset]`가 없거나, 변경된 벽의 피해/위험을 예전 경로 비용과 섞을 수 있다. 완료 시 자기 주변 AIObservation 비교와 실행 시 재검증만으로 이미 제공한 경로 비용/위험의 일관성이 보장되지 않는다. Cost/HitsAt/FillNext/edge의 불변 사실 공유와 해당 입력의 변경 검증을 요청했다.
2. **pending 준비가 기존 위협 관측을 건너뜀(P1).** Loop133~136의 Preparation 분기 return이 기존183~230의 실행 중 새 적/체력, idle전투 범위, breach watch보다 앞이다. 긴 준비가 진행되는 동안 기존 행동은 계속돼도 새 위협 입력 반영이 정체될 수 있다. 최초 `_inCombat=false`이면 위쪽 EmergencyNow도 실행되지 않는다. 관측을 유지하고 준비 취소/우선화로 연결하도록 요청했다.
3. **워밍되지 않은 PlanC의 동기 fallback(P1).** 직접 분산한 벽/기억/탐험 이외에 Prep/Enemies/Combat/AIApproach/AIBase/AIPlaces 등에서 PlanC miss가 나면 AITunnel.Plan→RunUntilDone으로 동기 완료한다. 생산 candidate 흐름의 나머지 경로도 예산을 우회하지 않게 할 것을 요청했다. 모든 소규모 작업을 무조건 새로 분산하는 범위 확대와 구별한다.
4. **3ms 체크 경계 밖의 큰 작업(검증 필요).** MoveNext 사이에서는 경과시간을 보지만 frontier의 거대 string.Join/로그, 완성된 목록의 위험·경계 설명 반복, CompletePreparation의 BuildInput/BuildJson/로그는 한 작업으로 실행한다. 개별 검색과 경로 복원 분산은 확인했으나 이를 전체 프레임3ms hard guarantee로 표현할 수 없다. 실제2179급 입력의 최대 작업 조각과 남은 비용을 측정하고 큰 잔여 경로를 해소하도록 요청했다.
5. **실패 결과의 tool-short 진단 수명(P2).** PreparePlan이 실패 결과를 기록하고 yield한 뒤, body의 다른 Plan 호출이 per-agent LastToolShortRed를 바꿀 수 있다. 다음 프레임 PlanC의 build cache hit가 plan=null/why만 반환하면 원래 실패의 red가 복구되지 않는다. 실패 metadata도 해당 결과와 함께 보존하는지 확인을 요청했다.

## 확인한 좋은 경계와 미확인 범위

- Core FindJob/FindWithDetourJob은 heap pop, 경로 역추적·뒤집기, hit 집계를 work unit으로 나눴다. 기존 동점/이웃 순서·두 pass 의미를 frozen 이전 구현과 비교하는135개 검사는 새 wrapper 자기 비교가 아니다. 기존 수제 경계/문 테스트도 함께 필요하다.
- PlanJob은 작업 스레드를 쓰지 않고 caller의 메인 스레드에서 이어서 실행한다. 경로 복원/벽·위험 수집도 타일 단위로 나눈 부분을 확인했다.
- build-local 계획은 후처리 중복을 줄이는 방향이며, global64 전체 Clear 제거는 적절하다. 이 사실만으로 입력 일관성·생산 흐름 전체 예산 통합이 완료됐다고 보지 않는다.
- 목표/실행 revision·world/player 수명 취소 초안은 있으나 지도/문/도구/이동 입력과 반복 취소 기아의 균형은 owner 수정 중이다. 실제 게임/서비스/LLM 요청은 검수자가 실행하지 않았다.

## 검수자 직접 실행

- `dotnet run -c Release --project Tools/core-tests -- TUNNEL-JOB`: 종료0, `ALL CHECKS OK (135)`. weighted ties/zero costs/no-way/start==target/정수 경계/cap, target edge/fallback/detour/hits, seeded20지도, budget1/7/1000000, 복원 예산·완료 멱등을 확인했다. 이는 순수 Core의 이어 실행 의미/경계 회귀이며 위 생산 통합 발견을 닫는 시험은 아니다.
