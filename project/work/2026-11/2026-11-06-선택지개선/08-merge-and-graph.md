# 승인된 병합과 공식 그래프 반영 — 2026-11-06

사용자의 **“합쳐줘”** 승인에 따라 본 저장소 `C:/Dev/CoreKeeperModSDK`의 `ai-character`를 `902721ec505df42d74d0b2d245ed34a049faadad`에서 검수 완료 후보 **`b25099f6fd31e4c4c3618357eb9e8101e0e17124`**로 `git merge --ff-only` 했다. 새 코드 변경·병합 커밋·원격 push는 없다. 검수한 두 커밋(`85f4814`, `b25099f`)과 21파일이 그대로 반영되었다.

## 저장소 보존

- 병합 전 HEAD/브랜치/index/worktree 상태와 수정·미추적 파일 SHA-256을 저장했다. HEAD, 후보 브랜치 SHA, 조상 관계, 빈 index, 후보 변경과 dirty 파일의 비중첩을 확인한 뒤 병합했다.
- 병합 직후와 공식 그래프 검사 후 모두 기존 로컬 파일 **457개 전부 동일**, index 비어 있음. 기존 SDK 관련 수정 7파일, CoreKeeper 로컬 DLL·메타, `.codex`, `.head`, 브리지 데이터·로그 등 미추적 파일을 보존했다.
- 마지막 HEAD는 `b25099f6fd31e4c4c3618357eb9e8101e0e17124`, 브랜치는 `ai-character`다.
- 근거: [병합 전 기록](evidence/merge/before.json), [병합 출력](evidence/merge/git-merge.txt), [병합 결과](evidence/merge/result.json), [최종 보존 검사](evidence/merge/final-verification.json).

## 공식 그래프 반영

격리 검수 당시 공식 파일 454개의 해시를 다시 대조해 타 작업 변경이 없음을 확인했다. 공식 개발·제품·실화면 그래프 트리 **732파일**을 `evidence/merge/graph-before/`에 원바이트 백업하고 [manifest](evidence/merge/graph-before/manifest.json)를 남겼다. 생성물은 기존 공식 스크립트로 재생성했다.

승인된 입력/안내 파일 12개를 반영했다. 아래 경로는 세션의 `work/` 기준이다.

| 영역 | 반영 파일 |
|---|---|
| 개발 그래프 | `지식그래프/tools/config.json`, `지식그래프/기획/기능.md`, `지식그래프/README.md`, `지식그래프/제품그래프-사용법.md` |
| 제품 코드 추출 | `2026-11/2026-11-05-지식그래프-개편/B/extractor/{pin-snapshot,build,verify}.mjs`, `B/inference.graph.json`, `B/README.md` |
| 실화면 연결 | `2026-11/2026-11-05-지식그래프-실화면/C/overlay-source.json`, `D/integrate.mjs`, `B/code-version-mapping.md` |

공식 `codeRoot`와 Git blob 핀 저장소는 본 저장소이며 기준 SHA는 `b25099f`다. 격리용 `stagedDocumentRoot`는 제거했다. 공식 `tools/lib.mjs`와 `tools/query.mjs`는 원바이트 보존했다. F.skill-goto에 잘못 붙었던 `AIMoveEstimate.Seconds` 함수 앵커는 독립 검수 정정판대로 포함하지 않았다.

공식 추출 결과 7파일이 검수된 격리 추출 결과와 전부 SHA-256 동일함을 확인한 다음, 해당 두 번 추출 동일성 증명 `B/determinism.json`도 공식본에 옮겼다. 이 파일은 추가 증명 산출물이며 이번 병합에서 두 번의 Roslyn 전체 추출을 새로 실행했다고 주장하지 않는다. 전체 변경 목록/해시는 [반영 입력](evidence/merge/applied-graph-sources.json)과 [최종 검사](evidence/merge/final-verification.json)에 있다. 백업된 기존 파일 중 44파일이 입력·안내·생성물·증명 갱신으로 달라졌다.

## 증거의 판 구분

- 과거 `observations.json` 2개는 원바이트 동일하다. 기존 76563bf 화면 관측과 e82eb87 추론 원본을 보존했다.
- 902721e의 실게임 보충 관측은 원래 `sourceCommit`을 유지하며 `currentSourceCommit=b25099f`, `sameRevision=false`, `evidenceAppliesToCurrentRevision=false`로 표시한다. 통합기는 검수된 명시적 과거 기준만 허용하며 기존 스키마·참조·증거 해시 검사를 유지한다.
- 이번 [03 실게임 검증](03-live-validation.md)과 [06 독립 증거 검수](06-live-evidence-review.md)를 기능 근거 및 실화면 overlay 참조에 연결했다. b25099f INPUT #64/#116의 자연 폭발벽 경고 전달, 일반 흙 경로와 구분, ETA 계산 기준, 대표 선택→실행만 뒷받침한다.
- 목표 재료/필요 음식의 뒤쪽 후보 경계, 직접 캐기·계속하기·본/대안 위험의 모든 경우는 오프라인 시험·코드 검수 범위다. 실게임 생존 개선이나 전체 상위 모델 흐름을 입증하지 않으며 과거 기능 전체를 현재판 검증으로 승격하지 않는다. 시험 설치는 복원되었으므로 이번 소스 병합 자체가 현재 게임 설치판 변경을 뜻하지 않는다.

## 병합 후 검사

| 검사 | 결과 |
|---|---|
| 본 저장소 CHOICE 의미 회귀 | **22/22 통과** |
| 개발 그래프 strict build / check | 통과, **2,617 nodes / 21,426 edges** |
| 승인 Git blob 핀 / Roslyn / B build / verify | 모두 통과 |
| 실화면 과거 기준 재매핑 / P C 통합·그래프 / L C 검증 | 모두 통과 |
| 제품 build / 질문 / check | 통과, **5,831 nodes / 22,379 edges** |
| 제품 동일 재생성 | byte-identical |
| 공식 추출과 검수된 격리 추출 | 7파일 전부 동일 |
| 공식 기준과 본 HEAD | 모두 b25099f, baseline drift 경고 없음 |
| 기존 로컬 457파일·과거 관측 2파일 | 모두 동일 |

15개 실행 단계의 명령·종료 코드·출력은 [graph-commands.json](evidence/merge/graph-commands.json)에 있다. 이미 검수한 후보의 동일 바이트 FF이므로 전체 비Unity gates·Unity compile을 반복하지 않았다. 기존 전체 검사, 최종 위험 분기 수정의 CHOICE/compile-check 및 실게임 검증은 01/02/03/06 보고서 범위를 그대로 따른다.

실행 스크립트: `merge-approved.mjs`, `apply-approved-graphs.mjs`, `finalize-merge.mjs`. 처음 두 스크립트는 예상 HEAD·공식 원본 해시·백업 존재 여부를 보호 조건으로 사용하므로 현재 상태에서 재실행할 스크립트가 아니다.

## 실행 제한

이 단계에서는 게임, 공통 설치, 실행 중인 Unity 및 다른 프로세스를 조작하지 않았다. 새 제품 기능·자동 안전 행동·모델 설정 변경·원격 push도 없다. 다른 작업/대화의 조율과 게임 설치 여부는 HEAD가 담당한다.
