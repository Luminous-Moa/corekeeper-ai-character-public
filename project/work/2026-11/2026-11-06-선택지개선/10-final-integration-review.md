# 최종 통합 독립 확인

2026-11-06 20:21~20:22 KST. 08 병합/공식 그래프와 09 정식 설치 결과를 원자료·현 상태로 좁게 대조했다. 전체 코드/시험을 다시 검수하지 않았으며 제품·그래프 수정, 설치, 게임/모델 실행 없음.

## 결론

**추가 차단 사항 없음.** 승인·독립 검수한 `b25099f6fd31e4c4c3618357eb9e8101e0e17124`가 본 `ai-character`, 공식 코드 그래프, 실제 설치 소스에 일관되게 연결된다. 기존 로컬 변경과 설치 전후 사용자 데이터의 보존 근거도 일치한다.

## 직접 확인한 연결

| 경계 | 직접 대조한 내용 |
|---|---|
| 후보→본 브랜치 | 본 `git rev-parse HEAD`=b25099f, branch=ai-character, candidate worktree HEAD와 동일. index 비어 있음. `evidence/merge/result.json`은 FF=true, 21파일 반영을 기록한다. |
| 로컬 보존 | `evidence/merge/before.json`의 기존 수정/미추적 **457파일 SHA256**를 현재 본 파일에 다시 계산해 **차이0** 확인. SDK7개와 브리지/로컬 미추적 파일을 제품 커밋에 섞지 않았다. |
| 공식 그래프 | 공식 config의 codeRoot는 `C:/Dev/CoreKeeperModSDK`, stagedDocumentRoot는 없음. 공식 실화면 D graph meta.sourceCommit=b25099f. 공식 코드 추출 **7파일을 검수된 stage 파일과 직접 원바이트 비교해 모두 동일**. |
| 실제 설치 | 20:21~20:22 읽기 시 설치된 C# **87개와 본 소스 원바이트 비교 모두 동일**. manifest의 .cs87개 각각 경로1개·GUID가 소스 .meta와 일치, 차이0. manifest SHA256도 09의 E1E4D51294F8466C685D08D1A4222AA7484945688DDEB25E38C2E85B854D4F9B와 같다. |

설치 실제 경로: `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike`.

manifest 직접 대조 시각은 **2026-11-06 20:21:54 KST**이다. 이후 공유 슬롯을 넘겨받은 다른 작업이 설치를 바꿀 수 있으므로 이 결과를 이후 모든 시각의 설치판 보증으로 쓰지 않는다. 완료 당시 20:20:44 증거 스냅샷과 현재 읽기 시점의 일치로 기록한다.

## 설치 전후 사용자 데이터 보존

`evidence/final-install-before.json`과 `final-install-after.json`을 Group/Path로 맞춰 직접 비교했다. 두 쪽 모두 **529항목**, SHA 차이0이다.

- AIPlayers 66파일: 설정·실제 사용량·기존 로그 포함.
- localsteam 248파일, remote 40파일.
- Scripts 소스·meta 175파일.

`final-install-before.reg`와 `final-install-after.reg`도 직접 원바이트 동일성을 확인했다. 이 검사는 설치 전후 스냅샷을 대조한 것이며 사용자의 이후 게임 활동까지 불변이라고 주장하지 않는다.

`final-install-verification.json`은 Commit=b25099f, 설치87/소스87/manifest87, ByteOrManifestDifferences=0, DataAndSourceHashDifferences=0, RegistryBytesSame=true, C#오류0, BuildAndInstall완료1을 기록한다. 09의 결과와 위 독립 대조가 일치한다. Unity compile/CHOICE/graph 전체 명령은 이번에 재실행하지 않았고 기존 실행 결과를 참조했다.

## 증거의 판과 한계 유지

공식 graph의 과거 화면 `installedSourceCommit=76563bf`는 당시 관측판을 뜻하며 현재 설치판이 그 커밋이라는 뜻이 아니다. supplemental 관측은 sourceCommit=902721e, currentSourceCommit=b25099f, evidenceAppliesToCurrentRevision=false로 유지됨을 실제 공식 graph에서 확인했다. 검수한 stage 추출7파일과 공식본이 같고, 새 실게임 참조는 03/06의 제한된 관측 범위를 따른다. 과거 실행·추론 전체를 이번 코드의 새 검증으로 승격하지 않았다.

이번 확인으로 이미 정리되지 않은 choice1106의 Steam Cloud 삭제가 해결되지는 않는다. 폭발 채굴 안전성·생존 개선, 엄밀한 목표/음식 뒤쪽 드롭 경계의 실게임 관측, 상위 모델 전체 흐름은 기존 검수에서 남긴 범위를 그대로 따른다. 이는 정식 설치 통합의 새 차단 사항이 아니다.

본 보고서의 근거: 08-merge-and-graph.md, 09-final-install.md, evidence/merge/{before,result,final-verification}.json, evidence/final-install-{before,after,verification,source-manifest-check}.json, 전후 .reg 및 검수 시 실제 본/공식 그래프/설치 파일.
