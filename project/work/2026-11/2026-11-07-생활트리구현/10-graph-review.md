# 생활 트리 공식 프로젝트 그래프 독립 검수

## 판정

공식 코드 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`에 대한 이번 프로젝트 그래프 반영은 **수용 가능**하다. 차단 0건, 중요 0건이며 작은 문서 지적 D1은 해소를 확인했다. 문구 수정 뒤 발견한 공유 `todo.md`의 동시 입력 변화도 담당자가 최신 원문을 보존해 파생물에 반영했고, 검수자의 최종 check·재생성 재확인은 모두 통과했다. 미해소 지적은 없다. 실제 모델/API·실게임 성공이나 전체 시설 생활 완료 판정은 아니다.

검수 입력은 [합의](00-agreement.md), [방향 판단](02-direction-decision.md), [가방 의존](04-bag-dependency.md), [최종 구현](05-implementation.md), [최종 독립 검수](07-review.md), [공식 코드 반영](08-integration.md), [그래프 반영](09-graph-integration.md)과 실제 공식 그래프·원본이다. 프로젝트 AGENTS, head-context start-job, 그래프 README/사용법과 관련 검수 지침을 적용했다. 갱신 중 파일을 최종 결함으로 판단하지 않았고, HEAD의09완료 통보 뒤 최종 check·대표 조회를 실행했다.

이번 검수는 그래프 연결·근거 지위·원본 재생성의 독립 검수다. 제품 구현 전체나 모든 역사적 그래프를 다시 검수하지 않았다. 제품·원본·그래프를 수정하지 않았으며 이 보고서만 작성했다.

## 발견과 수정 확인

### D1 — B 안내문의 과거 커밋이 현재 공식판으로 표시됨: 작은 지적, 해소 확인

`2026-11-05-지식그래프-개편/B/README.md` 첫 두 문단이11eb461/b25099f를 “공식 코드 기준”, “본 ai-character와 일치/기준이 같다”라고 현재형으로 표시한다. 실제 B manifest·code.graph·최종 제품meta와 공식 HEAD는 모두c4a0f5e다. 최상위 프로젝트 README/사용법은 이미 현재c4와 과거판을 구별한다.

HEAD에게 전달한 뒤 작성 담당이 현재형 기준을c4와08/09근거로 수정하고11eb461/b25099f는 과거 기준으로 구별했다. 검수자가 수정된 첫 세 안내 문단을 직접 읽어 해소를 확인했다. 구조·원본 일치나 현재 생성물의 blocker는 아니었다.

문장 수정 후 최종 check에서 `../../../todo.md`의 동시 변경으로 stale1/재생성 불일치가 새로 발생했다. README 수정은 이 stale의 대상이 아니다. 정확한 대상을 HEAD에게 전달했고, 작성 담당이 최신 todo를 변경하지 않고 제품 파생물만 재생성했다. [동시 입력 반영 기록](graph-final-todo-refresh.json)의 todo 전후 해시 보존을 읽고, 검수자가 `product.mjs check`와 `product-build.mjs --check`를 다시 직접 실행했다. 최종 ok=true/구조0/stale0/기준불일치없음과 `regeneration byte-identical`을 확인했다. 이 일시적 입력 변화를 생활 연결의 코드/기획 결함으로 분류하지 않는다.

## 실제 독립 확인

| 확인 | 수행과 실제 결과 |
|---|---|
| 공식/코드/제품 기준 | 공식 `git rev-parse HEAD`, B/code.graph meta.sourceCommit, 최종 제품meta.sourceCommit, check의 baseline/workingTreeHead 모두c4a0f5e 일치 |
| 제품 check | `node tools/product.mjs check` 직접 실행: ok=true, structuralErrors=[], stale=[], baselineDrift.warning=null |
| 제품 재생성 | `node tools/product-build.mjs --check` 직접 실행: `regeneration byte-identical` |
| legacy 원본·인용 | `node tools/query.mjs check` 직접 실행: 원본 해시 일치, 인용 모두 현행 원문에 존재, 인용/앵커/ID 경고 없음 |
| 기존 기획 보존 | 갱신 전 `06-graph-stage/official-before/기획연결/source.mjs`와 현재 편집 원본을 독립 import하여 비교: 기존58노드·75관계의 정의와 순서 전부 동일. 현재69노드·90관계, entry=P.release-goal |
| B 고정 원본 | snapshot manifest/verification의c4·325파일 allMatch를 읽고, 생활 핵심5파일의 실제 Git blob/SHA와 snapshot 및 manifest를 직접 재계산: 불일치0 |
| B 결정성 | 기존 두 Roslyn/Acorn 실행 결과7개 equal을 읽고 현재7파일의 SHA가 second 기록과 모두 일치함을 독립 계산. 전체 추출 두 회를 새로 반복한 것은 아님 |
| 새 코드 대응 | 제품에서 P.life-tree 계열의 코드 대응12개를 독립 추출: target 모두 존재, sourceCommit 모두c4, runtimeTrace 모두false |

B 표본은 `AILife.cs`, `Core/AILifeTree.cs`, `AIInventory.cs`, `AIBrain.Loop.cs`, `Tools/ai-bridge/protocol.mjs`다. B/code.graph SHA256은 `2a58554157dca88413f6373375035b323fcc07949b532d5ddbf8f7cfd96a7fa7`로 결정성 기록과 같다. 코드층6,297노드/24,795관계와 별도 과거 추정을 포함한 B 통합물은 구별했다. 제품6,592노드/25,319관계는 담당 결과와 일치한다. 최신 todo 반영 뒤 최종 제품 graph.json SHA256은 `8c0f885e6842d5336ad3d1653a0679c3388992279865df07cb6860dff4292d01`이다.

## 대표 소비 흐름과 원문 대조

기존 `work/지식그래프`의 CLI로 다음을 직접 실행했다.

1. `intent P.release-goal`, `intent P.life-tree`: Core Keeper 모드 제작·배포→동료 경험→생활→필요한 단계만 탐색으로 이어진다. 원래 제작 의도를 생활 편의 기능으로 대체하지 않았다.
2. `source P.execution`, `source R.common-implementation`, `source R.stop-action`: LLM의 대상·수량·순서 선택과 코드의 몸 실행, 중간 단계/원래 부탁, 실행 회차 중단과 대기 중 유효 실행 보존이 기존 원문으로 조회된다.58/75 보존 비교에도 R.common-* 정의가 포함된다.
3. `source P.life-tree-count`, `source P.life-tree-first-scope`, `source P.life-tree-readonly`: HEAD의 수량/첫 네 갈래 승인과 사용자의 탐색·유효 행동 보존 합의를 각 원문에서 구별한다. 다른 생활 작업·수리/가공/강화/거래/낚시가 첫 구현 완료로 바뀌지 않았다.
4. `show P.life-tree-readonly`, `show P.life-tree-result`: 합의/승인 원문에서 Navigate/HandleResponse와 Ended/Record의 정적 코드 대응으로 이어지며 trace=false다. 실제 source 조회로 Compose136~180행의 페이지·back/root/close/직접실행 보존, Ended215~225행의 작업량/확보량 분리, Resolve76행과 Inventory.CandidatesFor217~232행의 정확 payload 연결을 읽었다. 앞의 Git blob 대조로 이 source가 공식c4 원바이트임도 확인했다.
5. `source R.life-tree-implementation`, `source R.life-tree-review`, `show R.life-tree-offline`: 공식 반영08, 수정 후 독립 검수07, 생산 자체검증05로 연결된다.05/07의 실제 수행 구분과 `logs/final-validation.json`·`logs/unity-summary.json`, 독립96/0 로그 말미도 대조했다. 기존 검증을 이 그래프 검수가 새로 실행한 것처럼 기록하지 않았다.
6. legacy `show F.inventory-exact`, `show F.hand-choice` 및 생성 원본/노드 표본: 정확한 InventorySkill·Stale/Start/Tick, AIHandLease/AIHeldChoice 앵커와 가방 구현 근거가 남는다. F.life-tree/F.craft와 함께 상태는 `구현됨·새 실게임 미확인`이다. 기존 가방 담당 staging 전체를 공식에 덮어쓴 결과로 읽지 않았다.
7. `source Q.autonomy`, `show Q.life-tree-followup`: 개인 목표·요청 거절·호출 비용의 세부 미정과 후속 실행 범위가 보존된다. 공유 자원/소비 정책이나 후속 몸 실행이 새로 확정되지 않았다.

## 과거 관측과 현재 미확인 경계

최종 제품 JSON을 직접 읽어 다음을 확인했다.

- meta.sourceCommit=c4a0f5e와 installedSourceCommit=76563bf를 구별한다.
- UI 관측 sourceCommit=11eb461, currentSourceCommit=c4, sameRevision=false, evidenceAppliesToCurrentRevision=false, functionTraceObserved=false다.
- 리팩토링 관측 sourceCommit=902721e, currentSourceCommit=c4, sameRevision=false, evidenceAppliesToCurrentRevision=false다. 과거 수용 결과를 삭제하거나 현재 코드 성공으로 승격하지 않았다.
- 새 생활12연결은 정적 대응이고 runtimeTrace=false다. 오프라인/Unity/독립검수 참고에는 어댑터 경계와 전체 CandBuild/Tick·실제 모델/게임 소비자 미확인이 적혀 있다.
- Q.life-tree-followup과 F.life-tree/F.inventory-exact/F.hand-choice/F.craft의 미확인 상태가 실제 구현 보고의 범위와 맞는다.

따라서 원래 합의→새 생활 탐색 합의/HEAD 승인→현행 코드와 실제 검증 문서로 이어지는 대표 개발 조회는 끝까지 동작한다. 여기서 확인한 재생성/정적 연결은 실제 모델이 트리를 고르고 게임 입력·소비·가방 결과·다음 판단을 수행했다는 증거를 대신하지 않는다. 그 실게임 흐름은 보류 해제 뒤 별도 확인해야 한다.
