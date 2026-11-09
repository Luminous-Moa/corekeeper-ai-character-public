# 공통 실행의 개발 지식그래프 반영

새 기획안은 이번 작업 전에 이미 프로젝트 그래프에 통합돼 있었다. 기존 최종 목표·경험·생활·계층·원래 부탁·유효 행동 합의를 유지하고, 공통 실행의 코드 기준과 구현·검증 근거를 추가했다.

## 코드 기준과 조회 연결

코드 기준을 11eb461에서 685dde4d27dd91a782a14d8ae33fb576160fa578로 갱신했다. 고정 snapshot과 runtime-package 309개 파일은 정확한 Git blob 바이트와 일치한다. 두 번의 Roslyn·Acorn 재추출에서 코드 생성물 7개가 바이트 단위로 같았다. 코드 층은 5,859노드·22,395간선, 요청 입구 기능은 24개다. 기존 검수 표본 8개는 심볼이 모두 존재하고 줄 번호도 같았다.

제품 그래프는 6,052노드·22,695간선이다. 기존 기획의 P.original-request와 P.valid-action에서 새 참고 구현 항목, 정책, 실제 코드 입구로 이어진다.

- R.stop-action → AIBrain.Apply, CancelPendingChoices, AIActions.Stale
- R.finish-goal → AIGoals.FinishChosen, AIHostOrders.GoalEnded
- R.request-plan → AIPlanner.RequestReplan
- R.common-implementation → AIGoals.CheckDone, AIBrain.ObserveExecution
- R.common-offline → 확인 결과와 실제 실행 소스·경계 원문
- R.common-review → 독립 검수 판정과 검증 증거·경계 원문
- pol.ai-action-control, pol.original-request-completion → 합의 원문과 구현 계약

## 기존 능력 색인

근거 그래프의 F.action-select, F.candidates, F.skill-attack, F.skill-dig, F.charge-urgent, F.place-grid, F.host-orders, F.plan-queue, F.executor의 현재 한 줄 설명을 공통 실행에 맞췄다. 이전 구현과 실게임 근거는 과거 메모로 보존했다. F.common-execution을 추가해 명시 제어와 실제 코드 앵커를 한 입구에서 조회한다.

과거 3회 호출 실패 중지와 시스템 종료 보고의 미관측 항목은 새 실게임에서 행동 유지·명시 종료를 확인할 항목으로 정정했다. 실패 횟수로 후보를 잠시 제외하던 cand.fail-memory는 과거 구현 제안임을 표시했다.

## 검증

- product.mjs check — ok=true, 구조 오류 0, stale 0, baselineDrift warning=null
- product-build.mjs --check — 재생성 바이트 동일
- legacy build.mjs --strict — 인용·앵커·참조 검사 통과
- 기획 부분 graph.mjs check — 58노드·75관계·24원문, 결정성·관계목록 일치
- 원래 부탁 intent, 새 제어 3종, 정책 및 코드 source, 오프라인 근거 source를 실제 CLI로 조회했다.

[제품 검사](graph-check.json), [대표 조회](graph-queries.txt), [코드 표본 대조](graph-sample-check.json), [최종 해시](graph-final-hashes.json), [근거 그래프 생성](legacy-build.log)을 남겼다. 원본과 생성물의 이전 상태는 graph-before에 보존했다.

## 증거 범위

공통 실행의 현재 근거는 전체 컴파일 경고0·오류0, 회귀1,141개, 실제 코드 입구 오프라인31개와 기준판 취소 재현3개다. 새 게임 물리·원격 모델 시험과 설치본 배포는 수행하지 않았다. 기존 11eb461 UI 실게임 관측은 역사 관측으로 유지하며 현행 코드와 sameRevision=false로 표시했다. 이전 화면·리팩토링 관측과 새로운 코드 연결은 함수 실행 trace를 뜻하지 않는다.

게임 내 LLM의 게임 지식그래프와 별도 수집 중인 게임 행동 그래프는 이번 반영 범위 밖이다.
