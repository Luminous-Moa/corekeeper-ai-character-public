# 최종 반영 한정 독립 검수

2026-11-06. 사용자 병합 승인 뒤 20 병합/게이트, 21 공식 그래프, 22 최종 설치 결과를 독립 대조했다. 승인 기준은 `902721ec505df42d74d0b2d245ed34a049faadad`다. 코드 자체와 실게임의 전수 검수는 반복하지 않았다. 제품/설치/그래프 수정, 게임 실행, push도 하지 않았다.

## 판정

**최종 반영 검수 통과. Must-fix 0.** 본 저장소·설치 소스·공식 그래프가 승인된 같은 코드판에 연결된다. 기존 SDK/미추적 자료와 사용자 저장 보존을 확인했으며, 활성 그래프에 격리 stage 경로나 삭제된 옛 소스가 남아 있지 않다.

## 직접 확인한 일치와 보존

| 대상 | 독립 확인 결과 |
|---|---|
| 본 저장소 | `C:/Dev/CoreKeeperModSDK`, 실제 branch `ai-character`, HEAD=승인902721e. 20의 fast-forward 결과와 같다. |
| 본 소스/설치/manifest | Git 902721e의 모드 C# 84개, 본 트리84개, 설치84개, manifest84개. 경로 집합 누락/여분0, manifest 누락0. 각 소스를 Git blob과 LF 정규화해 직접 비교하여 본 트리/설치 차이0. |
| 기존 SDK 변경 | 20-before/after-gates의 7개 기록 동일뿐 아니라 현재 파일 SHA-256도 병합 전과 모두 같다. |
| 기존 미추적 자료 | 같은 방법으로 450개 경로/기록/현재 SHA 모두 같다. 로컬 data/log, .codex/.head 및 참조 자료를 없애거나 병합 대상으로 바꾸지 않았다. |
| AIPlayers | 설치 전 기록65개를 실제 현재 파일 SHA와 직접 대조하여 변경0. 실제 비용 기록을 포함한다. |
| 사용자 로컬 저장 | 최종 설치 전 backup의 localsteam248개와 현재 경로를 직접 바이트 해시 대조하여 변경/누락/여분0. |
| Steam remote 로컬 사본 | backup40개와 현재 remote 파일을 같은 방식으로 대조하여 변경/누락/여분0. 이것은 Cloud 서버 삭제 증명이 아니다. |
| 게임/Unity | 마지막 프로세스 조회에서 둘 다 없음. 이 검수는 실행하지 않았다. |

설치 위치는 `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike`, 소스 위치는 `Scripts/Scripts/`다. 전체 경로 집합 일치로 AIBodySkills.cs/AIExplore.cs 등 삭제된 옛 파일 잔존도 배제된다. 최종 설치가 의도한 변경이므로 mod 백업과의 차이는 정상이다. 22의 user-state-verify는 **mod만 차이1그룹**, AI43/local248/remote40/registry 동일이다. registry 자체 재수집은 하지 않았으며 그 부분은 보존된 verify 로그 근거로 구분한다.

## 공식 그래프

공식 G=`work/지식그래프`, P=`work/2026-11/2026-11-05-지식그래프-개편`, L=`work/2026-11/2026-11-05-지식그래프-실화면`이다.

- G config는 codeRoot=`C:/Dev/CoreKeeperModSDK`, sessionRoot=`../..`, codeFiles85다. stagedDocumentRoot가 없다. G/B/D의 활성 source metadata에서 graph-stage 또는 후보 worktree 절대경로를 직접 검색하여 0건이었다. 보존된 검수 보고의 역사적 stage 언급은 활성 경로와 구분한다.
- P/B와 L/D sourceCommit, 추가 실행 관측의 sourceCommit은 모두 승인902721e다. 과거 화면 설치76563bf와 코드 추론e82는 원래 역사적 기준으로 남아 있다.
- 공식 P/B snapshot232개와 runtime-package55개를 현재 Git902721e tree/blob에 직접 대조했다. **287개 모두 일치, 누락/변경/여분0**이다. 이전 snapshot 폴더 위에 삭제 파일이 섞이지 않았다.
- 공식 읽기 전용 product check 직접 실행: exit0, ok=true, structuralErrors0, stale0. baseline=본HEAD=902721e이며 baselineDrift.warning=null이다.
- 공식 반영 중 바꾼 U.test-give의 절대 원문 인용은 현재 `AICharacterStore.cs:510`의 같은 `TEST ONLY (verification aid, not gameplay)` 문장으로 이어짐을 직접 확인했다. 과거 정책이나 시험용 기능 지위를 새로 결정한 수정이 아니다.

공식 최종 hash를 직접 계산하여 21 통보와 일치했다.

| 생성물 | SHA-256 |
|---|---|
| G graph/graph.json | `7e2960ea1caba79da61c153c315731df4946b2614895de6406b55802c107515b` |
| P/B/code.graph.json | `7518faa079d066610ff41c9b5dcb2894a43cd2a4430078a1cd79aa33fafd0b2e` |
| L/D/graph.json | `d40458ed7a2f5310ebeb60aed037c23ca68853508698fb8f2b36bb982c27c429` |

## 검사 근거와 남는 경계

20-main-gates의 마지막 `failed gates: 0`과 protocol106 등 결과를 읽었다. 해당 전체 게이트를 재실행하지 않았다. 21의 strict/추출/결정성 및 공식 source 재생성 결과, 22의 BuildAndInstall exit0/C#오류0은 담당자의 실행 증거이며 이 검수에서는 최종 동일성과 읽기 전용 check를 독립 확인했다. 본 트리 설치 이후 게임 런타임 컴파일을 새로 관측했다고 하지 않는다. 같은 SHA의 실제 실행 증거는 15/18/19에 있다.

시험월드 `rfmaint1106`의 게임 내/Steam Cloud 삭제는 사용자가 별도로 요청하기로 한 후속이다. 이번 병합/설치/그래프 반영이 삭제 완료를 의미하지 않는다. DOWN 원인 미확정, NumLock 사전 토글 미관측, 개발 파서의 기존 한계도 앞선 보고대로 남는다. 새 반영 차단 사항이나 제품 수정 요청은 없다.
