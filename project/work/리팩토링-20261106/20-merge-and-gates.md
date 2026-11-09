# 본 저장소 병합과 오프라인 관문

사용자의 최종 “합쳐줘” 승인(`00-agreement.md`)과 HEAD 지시에 따라 `C:/Dev/CoreKeeperModSDK`의 `ai-character`에 검증판을 병합했다.

- 병합 전: `a07166f38086e0241c37c601c16452664236813e`
- 승인 브랜치 `refactor-maint-20261106`: `902721ec505df42d74d0b2d245ed34a049faadad`
- 실행: `git merge --ff-only 902721ec505df42d74d0b2d245ed34a049faadad`
- 병합 후: `ai-character`, `902721ec505df42d74d0b2d245ed34a049faadad`
- fast-forward 성공, 충돌 없음. 새 merge commit을 만들지 않았다.

## 로컬 자료 보존

병합 직전 브랜치/HEAD/대상 SHA, ancestry, staged 변경 없음, 기존 tracked dirty 7개가 예상 경로와 같은지 확인했다. 병합 범위의 변경 경로가 로컬 수정/미추적 파일과 겹치지 않는 것도 확인했다.

기존 tracked dirty 7개와 Git의 `--others --exclude-standard` 미추적 파일 450개를 경로와 파일 바이트 SHA-256으로 기록했다. 병합 전후 모두 동일하다. 대상은 SDK 설정 7개, .codex/.head/.vsconfig, Assets/Plugins/CoreKeeper 참조, Tools/ai-bridge의 로컬 data/log 파일이다. 원문 내용은 보고에 출력하지 않았다.

근거: [병합 전](20-before-merge.json), [병합 직후](20-after-merge.json), [Git 병합 출력](20-merge-output.txt). ShaderGraphSettings.asset은 status에서 dirty이지만 diff --name-only에서는 생략되는 기존 상태였으므로, 보존 검사는 status 기반의 7개 모두를 해시했다.

stash/reset/clean을 사용하지 않았고 로컬 자료를 이동·삭제·재작성하지 않았다. Git ignore 대상의 Library/빌드 캐시 전체를 해시했다는 뜻은 아니며, 오프라인 빌드 산출물은 기존 규칙에 따라 생성될 수 있다.

## 본 트리 오프라인 관문

본 저장소에서 격리한 TEMP/TMP로 Git Bash 절대 경로를 사용해 `Tools/core-tests/gates.sh --tag main-merged`를 한 번 실행했다. [전체 로그](20-main-gates.txt).

전체 관문 실패 0으로 종료했다.

| 관문 | 결과 |
|---|---|
| compile-check | 오류 0 |
| core-tests | 1062 통과 |
| exp-sim | ALL CHECKS OK |
| recall-loop | 41 통과 |
| bridge-wiring | 23 통과 |
| memgraph | 80 통과 |
| protocol | 106 통과 |
| fake-bridge / logshape / movecheck | 각 SELFTEST OK |

관문 종료 뒤 [최종 해시 기록](20-after-gates.json)을 다시 만들었다. HEAD/브랜치가 승인판 그대로이고 기존 tracked dirty 7개와 미추적 450개 파일의 경로·SHA-256도 병합 전과 모두 동일하다. 추가 tracked 변경이나 미추적 자료 변화가 없다.

코드 재편집이나 별도 시험 확대는 하지 않았다. Unity·설치·게임·원격 push는 이 병합 작업에서 실행하지 않았다.
