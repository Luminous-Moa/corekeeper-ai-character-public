# 최종 공통 설치본 반영

2026-11-06 17:44:35~17:46:04 KST. 사용자의 “합쳐줘” 승인과 HEAD 지시에 따라 본 저장소 병합 확인 후 공통 설치본을 최종판으로 반영했다. **설치 완료, 원래 a07166f 설치본으로 되돌리지 않았다.**

- 본 저장소 `C:/Dev/CoreKeeperModSDK`, branch `ai-character`, HEAD `902721ec505df42d74d0b2d245ed34a049faadad`. 병합/offline gates는 별도 담당이다.
- 실행 전 CoreKeeper/Unity 0 확인. 다른 코어 작업 대화의 사용 보류를 HEAD가 확인했다. 이번에 게임 실행·실게임·모델·bridge·시험 월드 삭제·FoldRemote 연결은 하지 않았다.
- 실행: Unity `C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Unity.exe`, `-batchmode -quit -projectPath C:/Dev/CoreKeeperModSDK -executeMethod HeadlessSdk.BuildAndInstall -logFile C:/Dev/_scratch/refactor-maint-final-install-20261106/build-install.log`. 숨김 실행 후 종료 대기.
- **exit 0, BuildAndInstall done 1, error CS 0, HeadlessSdk FAILED 0**. 게임 자체의 런타임 컴파일은 이번에 재실행하지 않았다. 같은 커밋의 앞선 실제 게임 검증은 15 보고서에 있다.
- Unity 로그에는 앞선 검증과 같은 headless entitlement 404/access-token 진단이 있다. 설치는 성공했지만 로그의 모든 error 문자열이 0인 것으로 표현하지 않는다.

## 설치 동일성과 사용자 자료 보존

설치 경로는 `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike`, 소스는 `Scripts/Scripts/`다.

| 검증 | 결과 |
|---|---|
| 본 repo C# 소스 / 설치 C# 소스 | 84 / 84 |
| 경로별 LF 정규화 SHA256 차이 | 0 |
| ModManifest 파일/C# 목록 | 84 / 84 |
| manifest 누락 파일 / 소스 경로 집합 차이 | 0 / 0 |
| Unity 실행 전후 제품 Scripts 및 meta SHA256 차이 | 0 |
| 기존 SDK 7파일 SHA256 차이 | 0 |
| AIPlayers 전체 65파일(usage·기존 로그 포함) 차이 | 0 |
| 사용자 localsteam 248 / remote 40 / registry | 모두 백업과 동일 |
| 종료 후 게임 / Unity 프로세스 | 0 / 0 |

기존 `livectl verify` 결과는 **mod만 예상된 1개 차이 그룹**이다. 이는 최종 배포의 목적에 맞는 설치 변경이며, 사용자 AIPlayers/저장/registry/switches 차이는 없다. 전체 verify0이라고 기록하지 않는다.

BuildAndInstall이 `Assets/AIPlayerSpike/AIPlayerSpike.asset`를 갱신할 수 있으므로 확인했다. HEAD/index/현재 정규화 blob은 모두 `6c3af303677ca4e3f3e745b352943c8db483bd20`으로 같고, 본 repo 최종 status에는 이 asset 변경 표시도 없다. 제품 소스를 수정하거나 강제 asset 복원/스테이징하지 않았다. 기존 SDK7·미추적 로컬 자료는 보존했다.

## 근거와 되돌림 자료

증거 루트: `C:/Dev/_scratch/refactor-maint-final-install-20261106/`.

- `install-result.json`, `build-install.log`, `build-log-check.json`
- `installed-source-compare.json`, `verification-summary.json`
- `source-before.json`, `sdk7-before.json`, `asset-before.json`, `asset-after.json`
- `AI-all-before.json`, `AI-all-after.json`, `user-state-verify.log`
- `git-status-before.txt`, `git-status-after.txt`
- `backup/`: 설치 전 공통 mod54파일 및 사용자 자료/registry를 기존 검증된 절차로 별도 보존. 이 백업을 자동 복원하지 않았다.

시험 월드 Cloud 삭제는 사용자의 “따로 시킬게. 마무리해” 결정에 따라 별도 후속으로 남는다. 이번 설치 과정에서는 해당 월드나 사용자 저장을 열지 않았다. 본 기록은 최종 설치 반영만 판정하며 push/그래프 갱신은 다른 담당 범위다.
