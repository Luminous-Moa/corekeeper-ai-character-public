# 생활 트리 프로젝트 그래프 반영 stage

> 후속 상태: HEAD의 최종 수용·본 코드 반영 뒤 공식 프로젝트 그래프도c4a0f5e로 갱신했다. 현재 결과는 [09 공식 반영](../09-graph-integration.md)을 따른다. 아래 stage의 pending·공식 미수정 문구와 부분 후보 색인은 당시 준비 기록으로 보존하며 정식 그래프의 현재 상태를 뜻하지 않는다.

현재 지위는 **승인된 기획/구현 기준과 격리 중간 후보의 stage**다. 공식 `work/지식그래프` 및 `2026-11-07-기획연결`을 수정하지 않았다. 중간 후보24c61c2의 실제 코드 앵커는 추가했지만 최종 구현·검증 보고가 아직 없으므로 최종 수용·정상 AI 흐름·실게임 관측을 추가하지 않았다.

## 준비된 원본 패치

- `source.mjs`: 공식 기획연결 편집 원본 전체를 바이트 보존한 접두부에 생활 트리 선언만 덧붙인 사본.
- `source-addition.txt`: 위 사본에서 추가된 문서/노드/관계 선언만 분리한 내용. generated graph.json 패치가 아니다.
- `stage-inputs.json`: 공식 편집 원본·컴파일러의 고정 해시, 추가 ID, 현재 지위와 근거 경계.
- `prepare.mjs`: 위 사본과 stage 전용 컴파일러를 다시 준비한다. 출력은 이 폴더 안으로 한정한다.
- `compile-stage.mjs`: 정식 compile.mjs의 사본. 편집 원본만 stage에서 import하고 근거 문서는 원래 위치에서 읽는다. 정책·기획 승격 판단을 바꾸는 별도 컴파일러가 아니다.
- `stage.mjs`: 이 폴더 안의 planning-preview.json만 생성하고 원본 해시·기존 선언 보존·결정성·근거 앵커와 새 지위를 검사한다.

기존58노드·75관계는 변경하지 않았고 생활 승인8노드·12관계와 격리 후보 참고2노드·2관계를 더해68노드·89관계·29문서가 됐다. 진입점은 P.release-goal을 유지한다. P.experience/P.life/P.execution/P.original-request/P.valid-action 및 R.common-* 구현·검증 노드는 그대로다.

추가 항목:

- P.life-tree: 필요한 수준만 펼치는 선택 탐색 합의.
- P.life-tree-readonly: 탐색은 현재 몸·대기 선택·목표를 바꾸지 않는 조회.
- P.life-tree-direct: 이미 지정된 대상의 직접 실행과 공통 재검증 입구.
- P.life-tree-count: HEAD가 승인한 상위 작업량 전달. 사용자 직접 API 결정으로 표기하지 않는다.
- P.life-tree-first-scope: HEAD 승인 첫 네 갈래 범위이며 구현 완료가 아니다.
- P.life-tree-result: 실제 실행/확보·목표 밖 행동·원래 부탁의 진척 구분 기준.
- Q.life-tree-followup: 수리·자동 가공·강화·거래·낚시의 후속 실행 범위.
- R.life-tree-spec: 승인 명세의 참고 입구. 테스트 성공 증거로 쓰지 않는다.
- R.life-tree-candidate / R.life-tree-candidate-code: 격리 구현 후보24c61c2와 실제 소스의 부분 정적 추출 참고. 최종 수용 지위는 pending이다.

## 중간 후보의 실제 코드 앵커

후보 `24c61c246053237784c057cf97282b9ca0c5d564`, 기반 `1fc89b93edbee31a9001a174581e32568119efc1`을 Git blob으로 고정했다. 관련19개 생산소스만 기존 제품 코드층의 Roslyn Extract.dll로 분석했고, 실제 함수33개의 신원·줄 범위를 선택했다. 이들 사이 정적 관계18개와 기획→후보 코드 대응12개를 `candidate-project-preview.json`에 넣었다(부분색인101노드·119관계).

`candidate-implementation.md`와 `candidate-code-current.json`에서 후보 지위·snapshot 경로·기존 추출기 해시와 입력 Git blob 해시를 확인한다. **부분 정적 추출이며 전체 제품 코드층 재생성도, 함수 실행 trace도 아니다.** 미완료 전체 하네스·Unity·최종 독립검수와 실제 게임 검증을 통과로 표기하지 않았다. 이 판 뒤 결함 수정이 진행 중이므로 최종 커밋/05 보고 뒤 필요한 변경분을 반영해야 한다.

이 중간판을 지금 다시 추출할 필요는 없다. 최종 고정판을 HEAD에게 받은 뒤에만 다음 후보를 준비한다. 재현 명령은 다음과 같다.

```powershell
node candidate-code.mjs 24c61c246053237784c057cf97282b9ca0c5d564
node candidate-reference.mjs
node prepare.mjs
node stage.mjs build
node stage.mjs check
node candidate-project.mjs
```

최종 구현에서 함수 신원/파일이 바뀌면 candidate-code.mjs의 실제 선택 심볼 목록을 그 고정판에 맞춰 고친다. 없는 함수를 추정으로 통과시키지 않는다. 후보마다 snapshot·원자료·manifest를 별도 SHA 하위 폴더에 보존하고 이전 판은 중간 참고로 남긴다.

## stage 재생성·조회

이 폴더에서 실행한다.

```powershell
node prepare.mjs
node stage.mjs build
node stage.mjs check
node stage.mjs find 생활
node stage.mjs path P.release-goal P.life-tree-first-scope
node stage.mjs show P.life-tree-readonly
node stage.mjs source P.life-tree-count
node stage.mjs show Q.life-tree-followup
```

prepare는 명세의 새 상태를 자동 승인하지 않는다. 공식 원본이 그 사이 바뀌면 기존 stage check의 baselineStable/ compilerStable이 false가 되어 HEAD가 새 원본과 재대조해야 한다. 기존 수정이 있다고 사본으로 공식 원본을 덮어쓰지 않는다.

현재 check의 성공은 **편집 원본에서 재생성한 기획 stage의 근거·구조·결정성**에 한정한다. 제품의 C# 코드 재추출·최종 제품 그래프 통합·실제 모델/게임 검증을 수행한 결과가 아니다.

## 구현 결과가 도착한 뒤 보완할 것

1. `../05-implementation.md`의 실제 고정 커밋·범위·로그를 읽고 구현자에게 현재 후보 소스/앵커를 확인한다. 타담당 가방장비 기반과 생활 변경 커밋을 구분한다.
2. R.life-tree-implementation, R.life-tree-offline 등 참고 항목은 실제 보고/검증이 생긴 뒤 그 범위대로 추가한다. 성공하지 않은 검증, 공식 반영되지 않은 후보, 수행하지 않은 실게임을 승격하지 않는다.
3. 실제 code anchor는 생산 함수의 신원을 확인한 후 제품 코드 추출 기준에 맞춰 연결한다. 아직 없는 심볼을 문서에 만들어 넣지 않는다.
4. 개발 근거 그래프의 F.life-tree와 관련 기능의 현행 설명/앵커가 필요하면 `기획/기능.md` 사본과 입력 config를 stage에서 준비한다. 정책 원문은 기존00/02/03 문서이며 generated JSON을 직접 수정하지 않는다.
5. 본 코드 반영/코드 추출을 수행할 HEAD가 기준 커밋과 stage를 수용한 뒤에만 공식 편집 원본·코드 고정 snapshot을 갱신한다. 이전의 UI/실게임 관측은 당시 기준으로 보존한다.

실제 제품 코드 기준 갱신 시에는 `work/지식그래프/제품그래프-사용법.md`의 B pin→추출→build/verify/결정성→C 재생성→실화면 대응→제품 통합 절차를 따른다. 현재 stage가 과거 코드 그래프에 새 구현 연결을 추가했다고 주장하지 않는다.

## HEAD 수용 후 정식 기획 재생성 순서

이 명령들은 **실행한 것이 아니라 HEAD가 수용한 뒤 사용할 명령**이다. 현재 source.mjs 사본은 baseline hash가 맞는 공식 편집 원본에 대한 추가 패치이고, 동시에 진행된 다른 수정이 있으면 추가 부분만 병합한다.

1. 승인된 추가 선언을 공식 `2026-11-07-기획연결/source.mjs`에 병합.
2. 그 폴더에서 `node graph.mjs build`, `node graph.mjs check`.
3. `work/지식그래프`에서 `node tools/product-build.mjs`, `node tools/product.mjs check`, `node tools/product-build.mjs --check`.
4. `intent P.life-tree-first-scope`, `source P.life-tree-count`, `show Q.life-tree-followup`를 조회해 의도·첫 범위·후속 지위를 확인.

기존 addPlanning은 편집 원본의 새 항목을 제품 그래프에 자동 투영한다. 기획만 추가하는 이번 stage에는 제품 통합 스크립트 변경이 필요하지 않다. 코드/검증 참고 앵커를 추가할 때는 실제 기준판과 심볼 신원에 맞춘 별도 대응이 필요하다.

게임 행동 그래프 및 게임 내 LLM 게임 지식그래프는 이번 stage의 수정 대상이 아니다. 두 그래프를 합치거나 게임 원본의 사실을 모드 지원 능력으로 바꾸지 않는다.
