# 본 브랜치 정식 설치 결과

2026-11-06 KST. 사용자 “합쳐줘” 및 HEAD의 공통 설치 시작 지시에 따라 본 `C:/Dev/CoreKeeperModSDK`의 `ai-character`, 커밋 `b25099f6fd31e4c4c3618357eb9e8101e0e17124`를 설치했다. 시험용 설치 원복 이후의 기존 설치본을 승인된 본 소스로 갱신한 최종 설치다. 새 게임·월드·모델 호출·백업 원복 회차는 만들지 않았다.

## 결과

- `HeadlessSdk.BuildAndInstall` 20:20:05 exit0, 완료 기록1건, C# 오류0. [실행 결과](evidence/final-install-result.json), [Unity 로그](evidence/final-build-install.log).
- 설치 C# **87개 모두 본 소스의 SHA256와 바이트 단위 동일**. manifest87개 경로 각각 정확히1건, 모든 GUID가 해당 소스 `.meta`와 동일. 누락·추가 C#·내용·manifest 차이0. [파일별 대조](evidence/final-install-source-manifest-check.json).
- 최종 manifest SHA256 `E1E4D51294F8466C685D08D1A4222AA7484945688DDEB25E38C2E85B854D4F9B`. [설치 manifest](evidence/final-installed-ModManifest.json).
- AIPlayers66파일(설정·실제 사용량·기존 로그 포함), localsteam248파일, remote40파일, Scripts 소스·meta175파일의 설치 전후 SHA256 차이0. 게임 레지스트리 export도 바이트 동일. [종합 검증](evidence/final-install-verification.json), [전](evidence/final-install-before.json), [후](evidence/final-install-after.json).
- 검증 완료20:20:44에 CoreKeeper/Unity 프로세스0 확인 후 HEAD에 공유 슬롯 반환. 이후 UI 담당은 이 설치본을 새 백업 기준으로 사용할 수 있다.

## 진행 중 확인한 경합과 처리

20:15 읽기 확인 시 본 HEAD가 승인 커밋과 같았으며 공통 설치에는 이전84개 C#가 있었다. 시작 직전20:16 재검사에서 다른 UI 작업의 Unity65240 CompileCheck를 발견해 **설치나 데이터 기록 전에 중단**했다. 해당 프로세스를 종료하지 않았다.20:18:46 종료 및 게임·Unity0을 확인하고 HEAD 재지시 후 우리 Unity18232만 실행했다.

설치 후 검증 스크립트 첫 호출은 Windows PowerShell의 UTF-8 BOM 없는 한글 경로 해석으로 증거 파일 쓰기 단계에서 실패했다. 스크립트를 UTF-8 BOM으로 저장하여 재실행했고 위 모든 대조가 통과했다. 제품·게임 데이터는 이 오류로 변경되지 않았다.

이번 설치는 앞선 실게임 보고서의 Cloud 잔여를 해소하는 작업이 아니다. `choice1106`의 Cloud 삭제 미확인 및 기존 `rfmaint1106` 정리 분리는 [03 실게임 결과](03-live-validation.md)에 그대로 남긴다.
