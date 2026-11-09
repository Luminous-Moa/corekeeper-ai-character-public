# 정식 설치 결과

2026-11-06. 담당 ui_live_verify. 사용자 본병합·정식설치 승인과 HEAD 점유 인계에 따라 완료했다. 원격 push, 게임 재실행, 새 fixture는 하지 않았다.

## 공식 소스와 실행

- 소스: C:/Dev/CoreKeeperModSDK, ai-character, SHA `11eb461ae122f404acec94563c086b09e002987c`. 실게임 검증 후보와 동일하므로 04의 증거를 재사용했다.
- 다른 작업의 Unity57188 CompileCheck 정상 종료 및 CoreKeeper/Unity0을 확인한 뒤 실행.
- Unity55988: **21:26:58~21:28:02 KST**, HeadlessSdk.BuildAndInstall, exit0, C# 오류0, BuildAndInstall done 확인.
- 설치 위치: C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike.
- 상세 로그·실행 결과: evidence/official/build-official.log, build-process.json, build-result.json.

## 설치 검증

- 설치 C#95개와 공식 소스95개 원시 SHA256 차이0.
- manifest95개 경로 존재, 원본 .meta GUID 차이0, 설치 C# 수95로 일치.
- manifest SHA256: `D2D72FEB490B1C6365025A9A06981C543ABD5D14EA719E165DDF48A7E4782370`.
- 배포 AICompactFontData.cs에 OFL 전문, Lee Minseo 저자 고지, 원본 고정 커밋71e1cacf1437a11220307120e63e30bc275312d4 포함. `node Tools/ui-font/build.mjs --check` exit0: 11,369글리프/10px셀/64,742압축바이트, deterministic artifact.
- evidence/official/installed-source-check.json 및 verification-summary.json 참조.

## 사용자 자료·설정 보존

설치 직전 before-install 백업 및 verify 차이0. 설치 후 AIPlayers52파일, localsteam251파일, remote49파일, registry, 시험 스위치 상태가 모두 그대로다. 실제 비용 decisions-usage.json도 전체 SHA256 동일. aiui1106의 사용자 진행과4K 설정을 유지했고 과거 자료로 되돌리지 않았다.

after-data-verify.txt의 차이1그룹은 승인된 모드 설치 변경뿐이며 사용자 데이터 차이는0이다. 로컬 SDK 실질 dirty6파일의 원시 해시도 전후 동일(dirty-before.json, dirty-after-check.json). git status의7번째 ShaderGraphSettings.asset은 diff/numstat가 비었고 현재 원시 blob 및 HEAD blob 모두 `ce8c2432884fcc0d627b3cef54a6ea51d4f766e4`로 같다. 통합 전후 기존 해시 기록도 실질6파일을 담는다.

## 종료·인계

21:28:26 CoreKeeper/Unity0 확인 후 HEAD에 슬롯 해제를 통보했다. 게임·bridge는 이 정식 설치 단계에서 실행하지 않았다. 기존 UI 실게임 한계(OS IME 조합, 다중 마우스 버튼 유지, 피해 정량 등)는04에 그대로 기록하며 정식 설치로 추가 통과를 주장하지 않는다.

현재 설치본은 승인된11eb461 정식 버전이다. 앞선04의 b250 복원 상태는 임시 시험 종료 당시 이력이며 이 정식 설치로 대체됐다.
