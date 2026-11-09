# 공통 실행 개발 지식그래프 독립 검수

2026-11-07. 최종 판정은 **수용**이다. 새 기획 통합을 유지하면서 공통 실행 `685dde4d27dd91a782a14d8ae33fb576160fa578`의 코드·정책·구현 계약·오프라인 검증을 연결한 범위에서 남은 필수 차단 결함을 발견하지 못했다.

새 기획안은 이번 반영 전부터 제품 그래프에 통합되어 있었다. 최초 README의 이전 코드 기준만 보고 새 기획 통합도 누락됐다고 해석하면 틀리다. 기존 새 기획 통합 여부와 공통 실행 코드층의 최신화 여부를 분리하여 검수했다.

## 범위와 방법

README와 제품그래프-사용법, 행동취소 판단주체·공통 실행 합의, contract.md·implementation.md·review.md·offline-execution.md를 읽고 실제 소비 CLI에서 대표 경로를 조회했다. 그래프 원본·생성물·제품 코드를 수정하지 않았다. 제품 코드·기존 테스트·게임·외부 모델은 실행하지 않았다. 실행한 것은 읽기 전용 그래프 조회와 `product-build.mjs --check`의 메모리 재생성 비교다.

게임 내 LLM 게임 지식그래프와 별도 게임 행동 추출 그래프는 검수 범위 밖이다. 정적 호출 연결을 실제 런타임 trace로 간주하지 않았다.

## 실제 조회에서 확인한 연결

| 소비 입구 | 원문·소스까지 확인한 내용 |
|---|---|
| `product.mjs intent P.original-request` | 최종 모드 배포 목표 → 동료 경험 → 자율 생활 → 목표 계층 → 원래 부탁 보존의 합의가 유지된다. P.valid-action, 신규 정책, R.finish-goal, 역사 관측 R.copper로 이어진다. |
| `source pol.ai-action-control`, `source pol.original-request-completion` | 판단주체 문서 3·5·7행과 공통 합의 10·11·13행을 실제 출력한다. AI의 계속·중단·전환, 수량의 관측 지위, 원래 부탁 완료의 상위 판단, 현재 대상·실행 회차 대조를 확인했다. |
| `show R.stop-action` → 실제 코드 `source` | `AIBrain.Apply:889`의 stop 분기는 CancelPendingChoices 후 hard Stop을 호출한다. `AIActions.Stale:76`은 관측한 run 및 idle 관측 뒤 새 run을 검사한다. `AIBrain.CancelPendingChoices:296`은 queued·held·nextHit·prefetch·요청을 정리한다. 계약·정책·코드 연결은 sourceCommit=685dde4, runtimeTrace=false로 표시된다. |
| `source R.finish-goal` 및 연결 코드 | contract 10행에서 현재 단계와 원래 부탁 완료를 구분한다. `AIGoals.FinishChosen`과 `AIHostOrders.GoalEnded:132`로 연결되며 GoalEnded는 done=true만으로 원래 부탁을 끝내지 않는다. |
| `show R.request-plan` → 코드 `source` | `AIPlanner.RequestReplan:412`가 현재 요청이 있으면 후속 trigger를 합치고 중복 요청을 내지 않는다. 현재 몸 실행을 지우는 호출이 없다. |
| `R.common-implementation` → 코드 `source` | `AIGoals.CheckDone:248`이 수량·시설·따르기 조건을 관측으로 보존하며 false를 반환한다. ObserveExecution 입구도 연결된다. |
| `query.mjs why F.action-select`, `why/code F.common-execution` | F.action-select의 이전 8초 폐기·연속 3회 실패 중지 설명이 최신 관측·명시 제어 설명으로 바뀌었다. 신규 F.common-execution에서 정책·구현 계약·실제 앵커를 조회할 수 있다. 관련 F 9개의 현재 설명과 미관측 상태를 정적 JSON으로 대조했다. |
| `source R.common-offline`, `source R.common-review` | 기준판 3/3 재현, 수정본 실제 입구 31/31, 최종 수용, core 1,141개·제품 컴파일 경고0/오류0 및 검증 경계가 실제 원문 조회 결과에 함께 나온다. |

## 검수 중 보완과 재확인

첫 안정본에서는 R.common-offline의 source가 문서 제목·날짜만, R.common-review의 source가 제목만 출력했다. 노드 요약에 적힌 결과와 경계를 같은 소비 명령으로 확인할 수 없으므로 인용 범위 보완을 요청했다.

최종본에서 offline-execution.md의 `확인 결과`(5~11행)와 `실제 실행한 소스와 경계`(13~21행), review.md의 `판정`(3~16행)과 `검증 증거와 경계`(63~71행)가 출력됨을 재확인했다. **해소 완료**다.

## 기준판·해시·재생성

- 최종 제품 그래프: 6,052노드·22,695간선. 기획은 58노드·75관계이며 기존 합의의 지위를 유지한다.
- 독립 실행한 `product.mjs check`: ok=true, structuralErrors=[], stale=[], baseline과 실제 SDK HEAD가 모두 685dde4, drift warning=null.
- 독립 실행한 `product-build.mjs --check`: `regeneration byte-identical`.
- 독립 실행한 `query.mjs check`: 원본 해시 일치, 인용 유효, 인용·앵커·ID 경고 없음.
- offline-source-hashes.json의 실제 검증 소스 11개를 본 SDK 현재 파일 SHA-256과 직접 대조해 불일치 0개를 확인했다.
- 코드 추출 전체의 309개 Git blob 일치·두 번 추출 결정성은 반영 담당의 graph-update.md 및 관련 검증 산출물로 확인했다. 독립 검수에서 전체 추출을 재실행한 것은 아니다.

## 역사 관측과 현재 검증의 구분

R.copper는 11eb461 및 보존 게임 로그를 대조한 역사 관측이며 개선 후 실게임 미확인을 명시한다. UI 관측의 sourceCommit은 11eb461, currentSourceCommit은 685dde4이고 sameRevision/evidenceAppliesToCurrentRevision은 false다. 리팩토링 902721e 관측도 sameRevision=false다.

새 근거 R.common-offline은 오프라인 실행, R.common-review는 독립 검수로 표시된다. 실제 world·가방·물리·네트워크/planner 경계는 fixture/stub이고 전체 Tick scheduling·candidate 생성·응답 parsing·게임 transaction·원격 모델 선택은 검증 밖이라는 원문을 함께 조회할 수 있다. 중간 offline-final.log의 19/19와 최종 offline-changed.log·review-execution-final.log의 31/31을 구별했다. 기존 실게임 성공을 현재 공통 실행 성공으로 승격한 연결은 발견하지 못했다.

검수 대상은 [반영 보고](graph-update.md), [제품 검사](graph-check.json), [대표 조회](graph-queries.txt), [최종 해시](graph-final-hashes.json)와 현재 개발/제품 그래프다. 이 보고는 그래프 연결과 증거 범위의 수용이며 새 실게임 동작 수용을 뜻하지 않는다.
