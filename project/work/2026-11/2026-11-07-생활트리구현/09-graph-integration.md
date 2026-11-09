# 생활 트리 공식 프로젝트 그래프 반영

공식 코드와 프로젝트 그래프의 기준을 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`로 맞췄다. [본 코드 통합](08-integration.md), [구현·자체검증](05-implementation.md), [최종 독립검수](07-review.md)를 승인된 생활 트리 기획에서 조회할 수 있게 연결했다. 제품/게임 코드를 수정하거나 설치·실게임·외부 모델 호출을 수행한 작업은 아니다.

## 원본과 보존 범위

기존 프로젝트 기획 원본58노드·75관계는 정의와 순서까지 보존했다. 최상위 P.release-goal, 동료 경험·생활·실행·원래 부탁·유효 행동과 R.common-* 공통 실행 근거를 유지했다. 생활 관련 승인8항목과 명세/실제 구현·검증·검수 참고3항목을 더해 기획은69노드·90관계·31원문이다. 상위 count/첫 네 갈래는 HEAD 승인 구현 기준으로, 탐색·직접실행은 합의로, 구현·검증은 참고로 구별한다.

새 생산 C#7개를 기존 개발 그래프 config에 등록했다. F.life-tree/F.inventory-exact/F.hand-choice와 실제 앵커를 추가하고, F.candidates/F.executor의 조회·실행 구분을 보완했다. F.craft는 실제 runtime recipe·직접/트리/Prep의 공통 입구·main 가방 수령 기준으로 설명을 갱신했다. 과거 제작 실게임 설명·확인한 선·인용은 메모와 기존 증거로 남겼으며 새 구현의 실게임 확인으로 쓰지 않는다.

갱신 전 원본·생성물은 [official-before](06-graph-stage/official-before/)에 보존했다. 가방장비 담당 stage 전체를 복사하지 않고 현재 공식 원본의 추가분만 반영했다. 진행 중 게임 행동 규칙 그래프와 게임 내 LLM의 별도 게임 지식그래프는 수정하지 않았다.

## 코드층과 제품 통합

기존 Git blob pin·Roslyn·Acorn 추출기를 사용했다. snapshot/runtime-package325개 파일은 목표 Git blob과 일치한다. 코드층은6,297노드·24,795관계이며, 과거 코드만 보고 쓴 별도 추정을 포함한 B 통합물과 구별한다. C를 새 B에서 재생성하고 실화면의 baseCommit/정적 소스 대응을 갱신했다. 제품 통합 결과는6,592노드·25,319관계다.

기획에서 실제 코드 입구까지12개의 정적 대응을 추가했다. Compose/Navigate/HandleResponse, Resolve와 네 생활 모듈의 ExactCandidate/CandidatesFor, 상위 Protocol/Remaining 및 Ended/Record를 유일한 실제 심볼로 확인한다. 함수가 실제 실행됐다는 trace 관계는 만들지 않았다.

과거 화면 관측판76563bf와 UI 관측판11eb461, 리팩토링 관측판902721e는 그대로 보존했다. UI/리팩토링의 현재 코드 동일성은 false이고 새 코드 적용 관측으로 승격하지 않는다. 과거 화면 대응51개 중37개만 같은 source segment이며 다른14개는 변경 경계를 유지한다.

## 검증 결과

| 확인 | 결과와 근거 |
|---|---|
| 공식 코드·제품 코드층 기준 | 둘 다c4a0f5e, [최종 검사](graph-integration-checks.json) |
| Git blob snapshot | 325파일 모두 일치, B/snapshot-verification.json |
| 코드 재추출 결정성 | Roslyn/Acorn 새 실행2회, 생성물7개 모두 바이트 일치, B/determinism.json |
| 기존 코드 검수 표본 | 8개 신원·줄·본문 모두 이전판과 동일, [표본 대조](graph-code-sample-recheck.json) |
| 개발 근거 그래프 | `build.mjs --strict` 통과, [생성 로그](graph-legacy-build.log), [원본·인용 확인](graph-legacy-check.log) |
| 기획 원본 | 기존 정의 보존, 근거 앵커/관계·결정성 통과 |
| C 및 화면 대응 | C build/validate 통과, 역사 관측판과 현재 코드 범위 구분 |
| 제품 검사 | ok=true, 구조 오류0, stale0, baseline warning=null, [검사 원문](graph-product-check.json) |
| 제품 재생성 | 바이트 동일, [결정성](graph-product-determinism.log) |
| 대표 실제 조회 | 최종 목표→생활 트리 첫 범위, 승인 count 원문, 본 코드/검수 원문, 실제 코드8개 source 확인, [조회 원문](graph-representative-queries.txt) |

검증은 코드 기준·원문·근거 종류·정적 연결·생성 결정성에 한정한다. 구현 자체의 어댑터/컴파일/독립검수 결과는05/07의 수행 구분을 따른다. 이 그래프 갱신이 그 검증을 새로 재실행하거나 전체 함수 coverage·실제 모델·게임 소비자의 성공을 입증하지 않는다.

## 현재 소비 경로와 남은 범위

`work/지식그래프`에서 기존과 같은 명령으로 조회한다.

```powershell
node tools/product.mjs intent P.life-tree-first-scope
node tools/product.mjs source P.life-tree-count
node tools/product.mjs source R.life-tree-implementation
node tools/product.mjs source R.life-tree-review
node tools/product.mjs show P.life-tree-direct
node tools/product.mjs check
```

실제 모델/API가 트리를 골라 실제 게임 입력/소비·가방 결과·다음 판단으로 이어지는 실게임 흐름은 보류 상태다. 수리·자동 가공/제련·강화·거래·낚시는 Q.life-tree-followup의 후속 실행 단위로 남는다. 첫 네 갈래의 구현·본 통합과 전체 시설 생활 완료를 구별한다.

이 결과는 갱신 담당의 자체 확인이며, HEAD가 별도로 배정할 독립 그래프 대표조회 검수는 아직 포함하지 않는다.
