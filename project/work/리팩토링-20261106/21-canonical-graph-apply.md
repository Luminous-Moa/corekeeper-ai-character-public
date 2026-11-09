# 승인된 리팩토링의 공식 그래프 반영

2026-11-06. 사용자 “합쳐줘” 승인과 본 저장소 ai-character의 fast-forward를 확인한 뒤 공식 개발·제품 그래프를 **902721ec505df42d74d0b2d245ed34a049faadad**에 적용했다. 본 HEAD와 제품 baseline이 같고 baseline warning은 null이다. 제품 코드·정책 수정, 소스 병합, 설치·게임·Unity·모델 호출·push는 이 그래프 담당이 수행하지 않았다.

## 사전 확인과 보존

격리 준비 시 기록한 canonical434개 파일을 다시 해시 대조하여 차이0을 확인했다. 다른 작업을 덮어쓰지 않은 상태에서 `canonical-graph-before-apply/`에657파일99,219,218바이트를 백업했고 각 복사본 해시도 대조했다. `manifest.json`이 원본/백업 경로와 해시를 기록한다. node_modules 전체/bin/obj를 복제하지 않았다.

기존 P/B snapshot과 runtime-package는 각각 정확한 절대 원본/대상 경로, 대상 미존재, 원본 reparse point 아님을 확인한 뒤 네이티브 PowerShell Move-Item으로 `canonical-graph-before-apply/original-directories/`에 보존했다. 삭제하지 않았다. 빈 공식 출력 경로에 최종 커밋 Git blob을 다시 추출하여 이동 전 파일이 남지 않게 했다.

## 공식 경로와 적용 범위

공식 루트는 `C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work`이며 아래 경로를 사용한다.

| 구분 | 공식 루트 아래 경로 |
|---|---|
| G 개발 그래프 | `지식그래프` |
| P 코드·제품 문서 | `2026-11/2026-11-05-지식그래프-개편` |
| L 실화면·통합 제품 그래프 | `2026-11/2026-11-05-지식그래프-실화면` |

검증된 원본16개와 supplemental20개 파일(관측 원본1+증거사본19)을 적용했다. 생성물은 공식 경로에서 재생성했다. 전체 적용 원본 목록/최종 해시는 `canonical-applied-sources.json`, 명령 결과는 `canonical-graph-command-results.json`, 정합성 감사는 `canonical-graph-audit.json`에 있다.

G config는 codeRoot=`C:/Dev/CoreKeeperModSDK`, sessionRoot=`../..`, codeFiles85개다. stagedDocumentRoot를 제거했고 공식 `tools/lib.mjs`·`query.mjs`는 기존 원본 해시 그대로 유지했다. 활성 개발 source metadata와 제품 meta에 `/graph-stage/` 참조가 없다. README와 설치판 대응 문서 상단은 공식902721e 기준으로 표시하고 아래의 과거·격리 검증 기록은 이력으로 보존한다.

공식 HEAD 전환 뒤 strict 검사에서 U.test-give의 절대원문 인용이 이전 AIPlayerSpikeMod.cs를 가리키는 점을 발견했다. 같은 원문 `TEST ONLY (verification aid, not gameplay)`가 이동한 **AICharacterStore.cs:510**으로 근거 경로만 바로잡았다(기존 미결 문서의 두 위치). 제품 의미·원문·미결 판정은 바꾸지 않았다. 격리 검사 때는 본 저장소가a07166f라 옛 경로가 유효했던 차이다. 수정 뒤 개발 strict/query가 통과했다.

## 검증 결과

| 검사 | 결과 |
|---|---|
| 본 HEAD / 제품 sourceCommit | 모두902721e, baselineDrift.warning=null |
| G build --strict / query check / F.my-door 선 조회 | 통과, 해시·인용·앵커·ID 경고0 |
| G 소스 / 기능 / 노드·간선 | 85 /64 /2605·21380 |
| G 선 밖 / 확인 뒤 코드 바뀜 | 기존51 유지 /48 유지; 옛 확인 서명 승격 없음 |
| P/B snapshot 파일 목록 | 최종 Git tree와 정확히232파일 일치, AIBodySkills·AIExplore 옛 cs/meta 잔존 없음 |
| Git blob 검증 | snapshot+runtime-package287파일 일치 |
| B 질문 경로·정적 외부 경계 | 기존3질문 통과, health→plan 도달 없음, 외부 import는 정적 해시 경계 유지 |
| B 결정성 | Roslyn/Acorn2회 추출7생성물 동일, 추론 포함 B.graph도 동일 |
| 표본8개 | 검증된 동일 최종 SHA의 원본·새 위치 표본 기록 보존, 새 canonical Git blob 일치 확인 |
| P/C·L/C 참조 / L 질문·공식 진입점 | 모두 통과 |
| 통합 제품 그래프 | 5807노드22303간선, structuralErrors0/stale0/ok=true |
| 공식 제품 재생성 --check | byte-identical |
| 실게임 입력사본19개 | 모든 사본·기록SHA·현재 원본SHA 일치 |
| 공식 resolver / 임시 경로 | 기존 resolver원본 유지, stage override 없음, 활성 source metadata stage경로 없음 |

첫 실행에서 B추출·검증·결정성은 완료했고 이후 개발 인용 경로 정정으로 strict 단계부터 재개했다. 이미 검증된 B 결과의 해시가 결정성 기록과 같은지 다시 확인했으며 불필요하게 코드 추출을 반복하지 않았다. 감사 도구가 `baselineDrift`를 경고-null인 정상 object와 구분하지 못한 부분은 도구 조건만 `.warning` 검사로 바로잡았다. 실제 제품 check는 HEAD/baseline 일치였다.

## 증거와 남은 경계

과거 화면 관측 설치 기준76563bf와 코드 추론 e82eb87을 보존했다. 902721e 실게임 관측은 별도 `supplementalRuntimeObservations`에 있으며, 기존 화면층/미관측 범위/개발 확인한 선을 전부 승격하지 않았다. 실제 자연어 plan→place/craft→다음 목표, 다중 멤버 교체/allstop, 자기 문 통과, 완전 종료 후 동일 월드 재입장30/30비교를 능력별 범위로 유지한다. DOWN 실제 피해원 미확정과 한정 회귀 검토도 그대로다.

최종 환경 후속은17:36:28 복원verify0, MouseKeys 수집 설정4개 동일(Flags62), 버튼DOWN미실행, FoldRemote끊음이다. NumLock의 사전 실제 토글 상태는 미관측이라 동일을 주장하지 않는다. 시험 월드 rfmaint1106(슬롯7/파일인덱스6)의 게임 내·Steam Cloud 삭제는 **사용자가 별도로 요청하기로 한 후속 작업**이다. 실제 Cloud 재다운로드가 있었으며 이번 공식 그래프 적용이 삭제를 수행하거나 완료로 바꾸지 않았다.

개발 파서의 WalkCandidate expression-body 누락과 HandleCommand 배열 인자 서명 표시 한계, L/D/verify의 이전생성물불변 검사 제외는14보고와 같다. 이번 공식 경로 반영으로 파서를 수정하지 않았다.

## 최종 주요 SHA-256

| 산출물 | SHA-256 |
|---|---|
| G graph/graph.json | `7e2960ea1caba79da61c153c315731df4946b2614895de6406b55802c107515b` |
| G tools/config.json | `c946002b633794f9b6d12eba659f57cb68a0ed5805f5526e858be5b83143bae1` |
| P/B/code.graph.json | `7518faa079d066610ff41c9b5dcb2894a43cd2a4430078a1cd79aa33fafd0b2e` |
| P/B/snapshot.manifest.json | `effb5fc43ce4b928a8fb0f3c74c7bac4667da658096ecdd5729a801a4b620eba` |
| L/D/graph.json | `d40458ed7a2f5310ebeb60aed037c23ca68853508698fb8f2b36bb982c27c429` |
| L/D/validation.json | `bc656a593e62a60a3530cff3cd484345da57517c92b3e88536d6245d94f0a4f3` |
| L/C/refactor-20261106/observations.json | `b958c27b8f4428665882c952aaee4345f3047b75f44dfb5a0ca2a87bab55b40d` |

이 보고와 감사 결과를 최종 반영 독립 검토자에게 전달했다. 공식 그래프 적용은 완료했으며 최종 독립 판정·세션 저장은 HEAD 담당이다. 격리 graph-stage는 당시 검수 기록으로 보존하고 공식 진입점은 G의 product/query 도구를 사용한다.
