# 공식 코드 반영 뒤 전체 프로젝트 그래프 갱신 순서

이 문서는 HEAD가 c4a0f5e 고정판의 독립 수용과 공식 코드 반영을 결정한 뒤 사용할 절차다. **현재 이 절차로 공식 파일을 수정하지 않았다.** 부분 후보 색인을 전체 제품 코드층의 대체물로 쓰지 않는다.

목표 코드: `c4a0f5e38016c79567f880bbaea5e56e0eeae347`. 기존 정식 그래프 코드: `685dde4d27dd91a782a14d8ae33fb576160fa578`. 기획 원본에는 P.release-goal·P.life 등 새 제작 의도와 R.common-* 공통실행이 이미 연결돼 있으므로 보존한다. 설치/실게임은 계속 별도 보류다.

## 1. 수용 전제와 기존 원본 보존

- HEAD의 최종 독립검수 수용과 공식 `C:/Dev/CoreKeeperModSDK`의 실제 HEAD를 확인한다. 실제 HEAD가 목표 SHA와 다르면 그것을 목표판으로 조용히 바꾸지 말고 의도를 다시 맞춘다.
- work/지식그래프, 기획연결 편집 원본, 개편 B/C와 실화면 B/C/D의 기존 갱신 대상·생성물·관련 source 해시를 이번 작업의 backup 폴더에 남긴다. 기존05/07 결과와 UI/실게임 역사 증거는 덮어쓰지 않는다.
- 기존 미커밋 설정·데이터/게임 설치본을 그래프 재추출의 입력/출력으로 변경하지 않는다. Git blob의 제품 코드만 pin한다.

## 2. B 전체 코드 snapshot과 코드층

대상 폴더는 `work/2026-11/2026-11-05-지식그래프-개편/B`다. 먼저 사본에서 준비하거나 HEAD 승인 범위에서 갱신한다.

1. `extractor/pin-snapshot.mjs`, `extractor/verify.mjs`의 repo/commit 및 `extractor/build.mjs`의 graph.meta.sourceCommit을 목표 SHA에 맞춘다. Program.cs/Acorn의 추출 방식은 바꾸지 않는다.
2. 기존 snapshot/runtime-package는 앞 단계 backup에 보존하고 빈 대상 디렉터리에서 새로 pin한다. 새 pin이 과거에 삭제된 파일까지 그대로 남기는 방식이 되지 않게 한다. 경로를 실제 확인한 뒤 파일 작업을 수행한다.
3. 아래 명령으로 Git blob snapshot·runtime-package와 전체 C#/JS 코드층을 재생성한다. 기존 추출기/의존성은 재사용하며 재설치할 필요가 없으면 npm ci를 반복하지 않는다.

```powershell
node extractor/pin-snapshot.mjs
dotnet run --project extractor/Extract.csproj -- snapshot csharp.raw.json
node extractor/build.mjs
node extractor/verify.mjs
node extractor/determinism.mjs
```

Git blob 일치와 두 차례 재추출 결정성을 확인한다. 이 전체 재추출은 **정식 기준판을 한 번 갱신하기 위한 필수 단계**이며, 진행중 후보마다 반복하는 작업이 아니다. UI/게임 실행 trace나 실제 모델 판단 성공으로 해석하지 않는다.

기존 validation-review.json의 검수 표본8개는 새 고정 snapshot의 실제 심볼·소스 범위와 다시 대조한다. 같은 본문이면 그대로 보존하고 달라졌으면 새 위치와 범위를 기록한다. 이전 판 관측을 새 함수 실행 증거로 바꾸지 않는다.

## 3. 기존 개발 근거 그래프의 새 소스·기능 등록

`work/지식그래프/tools/config.json`은 전체 codeDirs를 읽으면서 codeFiles 누락을 검사한다.685dde4→c4a0f5e에서 새로 생긴 생산 C#7개를 등록해야 한다.

- `Assets/AIPlayerSpike/Scripts/AIHandLease.cs`
- `Assets/AIPlayerSpike/Scripts/AIInventory.cs`
- `Assets/AIPlayerSpike/Scripts/AILife.cs`
- `Assets/AIPlayerSpike/Scripts/Core/AIHeldChoice.cs`
- `Assets/AIPlayerSpike/Scripts/Core/AIInventoryGoalForm.cs`
- `Assets/AIPlayerSpike/Scripts/Core/AIInventoryRules.cs`
- `Assets/AIPlayerSpike/Scripts/Core/AILifeTree.cs`

가방장비 담당의 graph-stage에 준비된 기능/원본 등록은 현재 그래프와 대조해 필요한 추가분만 병합한다. 그 사본 전체로 최신 P.release-goal·공통실행 원본을 덮어쓰지 않는다. 생활 기능에는 F.life-tree와 실제 생산 앵커(Compose/Navigate/HandleResponse/ExactCandidate/Ended 등)를 추가하고 기존 F.candidates/F.craft/F.executor 등 설명을 현재 구현에 맞춘다. 새 상태는 구현·오프라인 검증/Unity 확인이며 새 실게임 확인이 아니다. game graph에 제품 능력을 쓰지 않는다.

그다음 `node tools/build.mjs --strict`, `node tools/query.mjs check`를 수행한다. 정규식 parser의 known 한계를 함수가 없다는 뜻으로 해석하지 않는다. 제품 Roslyn 코드층의 실제 심볼과 대조해 정직한 경계를 기록한다.

## 4. C와 과거 화면의 새 코드 대응

개편 C에서 `node integrate-c.mjs`, `node build-graph-C.mjs`로 새 B 해시와 기획 대응을 다시 만든다. 기존 정책·활동 지위/역사 관측을 보존한다. C의 연결 목록이 미확인 심볼을 참조하면 실제 새 B 심볼에 맞춰 수정한다.

실화면 B에서 `node remap-base.mjs --base c4a0f5e38016c79567f880bbaea5e56e0eeae347`를 실행한다. 기본 과거 화면 관측판76563bf는 그대로 두며 같은 source segment만 연결한다. 함수 본문이 바뀐 화면 대응은 불일치를 그대로 남긴다.

실화면 `C/overlay-source.json`의 baseCommit만 목표 SHA로 맞추고 installedSourceCommit, historicalUiObservationCommit 및 historicalRefactorObservationCommit는 그 관측 당시 값으로 유지한다. 해당 C validate를 수행한다. UI/리팩토링 supplementary runtime evidence가 같은 코드판이 아니면 sameRevision=false/evidenceAppliesToCurrentRevision=false 경계를 유지한다.

## 5. 승인·구현·검증 기획 원본 추가

현재 stage-inputs.json의 canonicalSourceSha256와 공식 기획연결 source.mjs를 비교한다. 중간 다른 수정이 있으면 현재 공식 원본을 먼저 읽고 **source-addition.txt의 추가분만** 병합한다. 기존 선언58노드75관계와 진입점 P.release-goal은 삭제/재정의하지 않는다.

지위는 구별한다.

- P.life-tree 및 읽기전용/직접실행: 승인된 방향.
- 상위 count/첫 네 갈래/결과 기준: HEAD가 승인한 구현 기준.
- 후보·자체검증 참고: 정확한 SHA와05/JSON의 범위.
- 독립 최종 수용: 실제 최종07 검수와 HEAD 수용 기록이 생긴 뒤에만 그 지위 추가.
- 공식 반영: 실제 공식 HEAD·반영 기록으로 확인한 뒤에만 추가.
- 설치·실게임: 수행하지 않았으면 미수행 유지.

그 폴더에서 `node graph.mjs build`, `node graph.mjs check`를 수행한다. 생활 기획에서 실제 최신 코드까지의 연결은 정식 제품 코드층이c4a0f5e가 된 뒤 실화면 D/planning.mjs에 실제 sourceCommit과 유일 심볼을 검사하는 정적 대응을 추가한다. 현재 stage의 `candidate-project-preview.json`에 들어 있는12개 대응은 그 매핑 근거이며 전체 JSON을 정식 그래프에 복사하지 않는다.

## 6. 최종 제품 통합과 조회

`work/지식그래프`에서 다음을 수행한다.

```powershell
node tools/product-build.mjs
node tools/product.mjs check
node tools/product-build.mjs --check
node tools/product.mjs intent P.life-tree-first-scope
node tools/product.mjs source P.life-tree-count
node tools/product.mjs show R.life-tree-self-validation
node tools/product.mjs show Q.life-tree-followup
```

새 기획부터 Compose/Navigate/HandleResponse/ExactCandidate/Ended와 상위 count·결과 계약의 실제 source 조회까지 확인한다. 같은 sourceCommit 여부·snapshot 해시·기존 기획/공통실행 보존·원문 앵커·새 코드 참조 끝점·재생성 바이트 일치를 최종 검수한다.

현재의 게임 행동/게임 지식 그래프는 이 절차의 쓰기 대상이 아니다. 전체 시설 생활 완료·정상 AI 실게임 흐름·정식 배포로 보고하지 않는다.
