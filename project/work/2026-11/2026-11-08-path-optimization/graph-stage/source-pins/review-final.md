# 길 계산 최적화 후속 독립 검수

## 실기 결과에 따른 재개

2026-11-08 후속 실기에서 `2a0112d`의 최종 수용 조건을 충족하지 못했다. 아래 첫 검수의 코드·오프라인 결과는 당시 범위의 기록으로 보존한다. 전체 작업은 계속 검수 중이다.

`live-evidence/ai2-wait-1.Player.log`는 frontier가 30,492개 타일을 확장해 7,526개 목적지를 찾았고, t=192.2 이후 진행한 준비가 t=340.1에 새 위협 집합으로 취소됐음을 기록한다. 해당 준비는 17,647 frames와 54,636.3ms의 누적 작업을 소비했다. 호출자가 확인한 147.9초 동안 SEND가 없었다. 평균 약 120fps 유지와 함께 전체 후보 준비 완료가 장기간 지연된 상황이다. 목적지마다 같은 출발점에서 Dijkstra를 반복하는 총 계산량과 재시작에 따른 진행 손실을 후속 결함으로 검수한다.

후속 수용에는 동일 출발 다중 목적지 검색의 실제 공유, 7,526개 frontier 조건에서의 완료·총 작업량 증거, 목적지 자체가 보호 경계인 예외와 우회·문·도구·위험 의미의 보존이 필요하다. 대부분의 목적지가 예외 fallback으로 돌아가는 구현은 이번 실기 실패의 해소로 수용하지 않는다.

2026-11-08. 검수자 `path_review_final`. 검수 대상은 `C:/Dev/CoreKeeperModSDK-path-opt`의 `7af8f91` 기반 미커밋 변경이다. 적용 지침과 agreement.md, review-criteria.md, review.md를 읽고 구현 소스와 시험을 확인했다. 검수자는 생산 소스를 수정하지 않았다.

## 판정

발견된 707회 구성의 후처리 재검색, 64개 캐시 전체 삭제, 2179개 탐험 목적지의 동기 연속 계산에 관한 코드 변경과 오프라인 검증을 수용한다. 사용자 선택지 상한을 추가하거나 위험 정보를 선택 뒤로 옮기는 변경은 발견하지 않았다. 보고한 코드 결함은 구현자 수정 후 재검토했다.

구현자가 실행한 Unity batchmode 로그에서도 `[HeadlessSdk] CompileCheck: ok`와 정상 종료 정리 기록을 직접 확인했다. 실제 게임에서 두 멈춤 상황을 재현한 프레임 시간과 LLM 요청 결과는 이번 독립 검수에서 측정하지 않았다.

## 실제 생산 경로 확인

- `Send → Preparation → BuildCandidateSteps → CompletePreparationSteps`가 프레임 단위로 진행된다. 모든 후보 조립과 JSON 생성이 끝나고 배치 검증을 통과한 뒤 요청을 보낸다.
- 광석, 기억 광석, 물체 주변 경로, 아이템, 호스트 접근, 기억 탐험 끝의 대량 반복문은 `PreparePlan`으로 검색을 완료한 뒤 `PlanC`로 같은 결과를 소비한다. 조립 후 경계·위험 설명도 build-local 결과를 재사용한다. 목록을 끝까지 처리하는 기존 순서와 필터를 유지했다.
- build-local 사전에는 64개 상한이나 TTL 만료가 없다. 일반 실행 힌트 캐시는 256개에서 가장 오래된 항목 하나를 제거한다. 공유 배치는 목적지마다 Dijkstra를 수행하며 확장, 경로 복원, 벽·위험 수집을 나눠 진행한다.
- `PlanBatch`는 최초 위치, 도구 피해, 물체 범위를 공유하고 타일의 첫 사실을 재사용한다. Cost, HitsAt, FillNext가 동일한 사실을 사용한다. 최종 검증은 유일한 read set과 경계 정보를 검사한다. 검증 시작 뒤 새 검색 추가를 거부한다.
- `Changed`는 `PreparationChanged`로 배치 전체를 취소한다. 계산 중과 실제 도달 불가가 후보 실패 캐시에서 섞이지 않는다. 실패의 `LastToolShortRed`도 build-local metadata에서 복원한다.
- `_building`을 각 Step 전후 설정·복원하며 per-agent 상태를 유지한다. Unity 접근은 메인 스레드에 남아 있다. 공유 배치는 이동만으로 취소하지 않고 최초 관측 위치 기준을 유지한다.

## 발견과 재검수

1. 이전 검수의 프레임 간 벽·타일 혼합 문제는 불변 TileFact와 공유 배치 검증으로 수정됐다. 실제 PlanBatch/PlanJob 추출 시험에서 벽, tileset, 물체, 도구 피해, 경계, 문, 위험 변화와 이동 중 배치를 검사했다.
2. 기존 적·체력·breach 감시가 준비 분기의 return에 가려지던 문제는 감시 뒤 준비 진행으로 이동했다. 후속 검수에서 idle 상태에 이미 전투가 진행 중이면 새 적 추가가 bool 변화 없이 지나감을 발견했다. 현재 idle은 위협 Entity 집합을 비교하고 running은 새 적·소멸을 감지해 준비를 취소한다. HP, 긴급 돌진, 새 emergency 사유도 취소에 연결됐다.
3. 예산을 AI 수로 나누는 것만으로 큰 작업 단위를 가진 앞 AI가 후순위 AI를 계속 밀어낼 수 있음을 전달했다. 현재 예산을 받지 못한 준비를 다음 프레임 우선 처리한다. 고정 a→b 호출 순서와 분리 불가능한 5ms 단위를 사용하는 실제 스케줄러 시험이 통과했다.
4. JSON iterator가 yield한 뒤 오래된 now를 요청 시작·timeout·호출률 계수에 사용하던 문제를 전달했다. 현재 배치 검증이 끝난 직후 실제 시간으로 갱신하고 비용과 호출률을 재검사한다.
5. 준비 중 prefetch 대상 기술의 종료·실패가 HTTP 요청에만 연결되던 문제를 전달했다. 정상 종료는 `PrefetchCompleted`로 보존하고 실패는 준비를 취소한다. 요청 생성 시 완료 상태를 이어받는다.
6. player entity 또는 thin world가 사라져 Tick 자체가 중단되면 준비와 전역 활성 수가 남던 경로를 전달했다. `Step3_Drive`와 Brain의 entity 검사에서 `CancelPendingChoices`를 호출하도록 수정됐다. 일반 off/reset의 기존 취소 경로도 확인했다.
7. SEND 로그 요약 변경으로 Core 소스 계약 검사 하나가 실패했다. 구현자가 실제 로그 계약에 맞춰 검사를 고친 뒤 전체 1553개가 통과했다. `Follow()` 이름을 찾던 restriction 시험 추출기도 `FollowSteps()`로 갱신됐고 47개가 통과했다.

## 검수자 직접 실행 결과

| 실행 | 결과 | 확인 범위 |
| --- | --- | --- |
| `dotnet run -c Release --project Tools/core-tests` | ALL CHECKS OK 1553 | 순수 Core, 기존 경계·문·후보 회귀, 이어 실행 탐색 135개 비교 포함 |
| `dotnet run -c Release --project Tools/path-preparation-tests` | ALL SCHEDULER CHECKS OK 25 | 실제 Preparation.cs 전체 링크, context 복원, 완료·취소·오류·공정성, 실제 PreparePlan→PlanC 캐시 |
| `dotnet run -c Release --project Tools/restriction-tests` | ALL PASS 47 | 기존 선택권 및 실제 생산 코드 추출 회귀 |
| `dotnet build Tools/compile-check -c Release -nologo` | 경고 0, 오류 0 | 실제 모드 전체 C#8/netstandard2.1 소스 컴파일 |
| `tunnel-batch-harness` | PASS 13926 | 생산 Plan/PlanBatch/PlanJob 추출, stub world, 기존 동기 구현과 비교 및 변경 검증 |
| `dotnet run -c Release --project Tools/path-search-benchmark` | 네 시나리오 모두 통과 | 707/2179 요청의 pure Core 경로·상한·우회 동등성과 Step 작업량 |
| `git diff --check` | 종료 0 | 최신 변경 공백 검사 |

구현자의 `unity-compile.log` 11817행은 `[HeadlessSdk] CompileCheck: ok`를 기록한다. Unity 로그에는 SDK 참조 및 APIUpdater 경고가 함께 있다. 위 표의 경고 0개는 dotnet 전체 소스 컴파일 결과다. Unity 실행으로 생성된 패키지·프로젝트 설정·참조 DLL 변경은 구현자가 최종 작업 범위에서 정리해야 한다.

`tunnel-batch-harness/Extracted.cs`의 Plan 시작부터 PlanBatch/PlanJob 끝까지를 현재 AITunnel.cs와 문자열 비교해 완전 일치를 확인했다. 설치된 .NET8 런타임이 없어 `DOTNET_ROLL_FORWARD=Major`로 .NET10에서 실행했다. World, Unity math, 도구 피해 조회 등 주변 의존성은 대역이다.

실제 PreparePlan/PlanC를 링크한 시험은 357개 생성 후 350개 설명 조회에서 실제 계획 357회, cacheReads 707회를 기록했다. 2179개 생성과 2179개 설명 조회에서는 실제 계획 2179회, cacheReads 4358회였다. 생성 직후의 PlanC 조회도 cacheReads에 포함한다. 60초를 진행한 뒤에도 살아 있는 build-local 결과를 보존했다. 이 시험의 탐색 backend는 호출 계수용 대역이다.

pure Core 합성 지도에서 Step 최대 작업량은 모두 32였다. 검수자 실행의 최장 Step은 Find/707 0.9722ms, Detour/707 1.7708ms, Find/2179 1.3730ms, Detour/2179 0.6299ms였다. 기존 경로와 새 경로의 타일 순서, 상한 결과, 우회 결과를 비교했다. 707은 고유 목적지 357개와 재조회 350개의 구성으로 시험했다.

## 남은 범위와 증거 한계

- 전체 모드나 모든 프레임의 3ms 상한을 보장하지 않는다. object entity query, 초기 관측, 일부 후보 준비, BuildInput, 최종 문자열·HTTP 버퍼, 상세 진단 로그는 분리 불가능한 비용을 남긴다.
- Prep의 생활·기지·장소 접근, 일부 적 접근 등 워밍되지 않은 PlanC는 공유 사실을 사용하며 `RunUntilDone`으로 동기 완료한다. 이번 수정으로 확인된 광석·기억 탐험 끝 대량 반복과 후처리 재검색 경로는 분산됐다. 남은 호출의 실게임 최대 비용은 측정하지 않았다.
- 관측과 검증은 시간 구간에 걸친다. captured Position을 기준으로 lazy fact를 읽고 마지막에 다시 비교한다. 원자 snapshot/epoch 보장이 없으며 이미 검증한 타일·물체가 뒤에서 다시 변할 수 있다. 입력에는 최초 위치 기준과 관측 시각·준비 경과를 표시하고 기존 실행 시 재검증을 유지한다. 계속 변하는 지형·도구·위협은 준비를 다시 시작할 수 있다.
- 2179개 전체 Unity 후보 조립·JSON·전송까지를 실제 게임 지도에서 재현하지 않았다. pure Core 시간과 대역 시험은 실게임 최장 프레임, 전체 준비 지연, GC·메모리 영향에 대한 측정값을 제공하지 않는다. 모든 실제 후보 key와 설명을 실게임 전후 덤프해 비교한 증거도 없다. 선택지 보존은 변경 diff, 기존 회귀와 대량 캐시 시험 범위에서 확인했다.
- Unity 컴파일 로그는 확인했다. 검수자는 게임 실행, 외부 LLM 요청, 모드 설치를 수행하지 않았다. 최종 배포·설치 완료 여부는 이 검수 판정에 포함하지 않는다.
