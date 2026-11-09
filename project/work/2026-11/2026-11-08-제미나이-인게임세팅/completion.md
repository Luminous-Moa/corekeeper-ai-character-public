# 제미나이 수동 인게임 확인 준비

2026-11-08. 사용자의 최신 모드 설치·제미나이만 연결 준비 요청에 따라 완료했다. 게임 실행, 월드 진입, AI 작업 명령 또는 모델 생성 요청은 수행하지 않았다.

## 준비 상태

- 본소스 `7af8f91e5d26e4fd9ce5996809ae41e213340118`을 같은 커밋의 격리 작업트리에서 공식 `HeadlessSdk.BuildAndInstall`로 설치했다. `unity-install.log`의 `BuildAndInstall done` 및 `Exiting batchmode successfully now!` 확인. script-only 모드이며 assetbundle은 필요 없다.
- 설치 위치: `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike`. 설치 C# 115개 모두 빌드 작업트리와 SHA256 일치. 본소스 115개와도 텍스트 일치하며 4개는 체크아웃 개행만 다르다. manifest SHA256 `753BD026EBB5CA1F43183BF08EED44F88EA265AC21E923D4F78238FB4EE2C145`. 상세는 `installation.json`, `installed-script-hashes.json`.
- 활성 월드 2개의 참가 멤버가 모두 `["gemini"]`이다. 최근 월드 `a66565fae4bf489b0ec9c119ec31add3`는 이미 이 설정이라 그대로 뒀다. legacy 활성 월드 `c48cf4bbd414b29b047a91b9620b29b4`만 Claude에서 Gemini로 변경했다. 비활성 월드 `b39072f4c49d28bf6ab43b7b6a702d7f`는 그대로 비활성이다. 새 월드는 기본 비활성이다.
- 브리지 `http://127.0.0.1:8331` 실행 중. node PID `51816`, 숨김 PowerShell launcher PID `53836`. 정확 명령은 본소스 `Tools/ai-bridge/bridge.mjs`이다. `start-bridge.ps1`에서 `AI_BRIDGE_MEMBERS=gemini`로 Gemini만 기동 인증 검사한다. 이 환경변수는 기동 검사 범위이며 API의 강제 allowlist는 아니다. 실제 게임 캐릭터 연결은 위 월드 참가 설정으로 Gemini만 제한했다.
- `GET /v1/health`: Gemini `found/login/subscriptionOk/ok=true`, `skip=false`, 사용량 5시간 0%, 주간 0.2%, busy 없음. 다른 멤버는 기동 검사·연결하지 않았다. `bridge-health.json`, `bridge-stdout.log` 참조. 로그인/사용량 조회만 수행했고 모델 작업 요청은 하지 않았다. 추가 로그인은 현재 필요 없다.
- 게임 지식 데이터 3190 노드/build 26332184 정상 로드. 기존 Decisions 공통 모델 `gpt-6-luna`, 월 예산 $200, 누적 약 $22.62를 변경하지 않았다. 사용자 환경의 기존 API 키 존재만 확인했고 키 값을 출력·저장하지 않았다. 상위 자동 모드와 작업 목표는 새 연결 시 기본 꺼짐/없음이다.

## 사용자 확인 순서

1. 평소처럼 Core Keeper를 실행하고 기존 월드에 들어간다. 최근 활성 월드는 Gemini가 자동 참가한다.
2. Gemini가 없는 비활성/새 월드라면 게임 채팅에 `/ai on gemini`를 입력한다.
3. 게임 채팅에 `@제미나이 구리 10개 모아줘`처럼 부탁한다. 자연어 멘션은 `@`를 포함해야 한다. 이동부터 보고 싶다면 `@제미나이 나를 따라와`도 가능하다. 이 문서의 명령들은 안내 예시이며 실제 전송하지 않았다.
4. 멈추려면 `/ai gemini stop`, 참가를 끄려면 `/ai off gemini`를 쓴다. 상태는 `/ai`로 확인한다.

채팅 키 자체는 이번 준비에서 확인·변경하지 않았다. 게임의 기존 채팅 입력을 사용한다.

## 백업 및 복구

변경 전에 이 폴더의 `backup/`에 기존 설치 모드 전체, `Steam-435751233` 세이브 관련 전체, `AIPlayers` 설정·캐릭터·기억 전체를 복사했다. 세이브 원본 252개가 백업과 SHA256 일치하고, AIPlayers는 변경한 legacy 참가 설정 1개를 제외한 77개가 일치한다(`backup-verification.json`). 세이브 원본은 직접 변경하지 않았다. 인증 계정·자격정보 및 기억을 삭제하지 않았다.

되돌리기가 필요하면 먼저 게임을 종료한다. 참가 설정은 `backup/AIPlayers/<world>/settings.json`의 해당 원본을 복원한다. 설치본은 `backup/installed-AIPlayerSpike`에 있다. 세이브 백업은 보관용이며 이번 작업에서 복원하지 않았다. 브리지를 중단하려면 위 PID의 실제 명령이 여전히 해당 bridge인지 확인한 뒤 그 프로세스만 종료한다. 다시 실행하려면 이 폴더의 `start-bridge.ps1`을 숨김 PowerShell로 실행한다(이미 8331 사용 중이면 중복 실행하지 않는다).

## 경계

이 기록은 설치·연결 준비 완료 근거다. 게임 내부 스크립트 로드, Gemini 자연어 응답, 실제 채집·이동·귀환은 사용자의 이번 수동 확인에서 관찰할 단계이며 통과로 주장하지 않는다. 조명·어둠과 실제 인간 시야 동일성은 사용자가 분리한 후속 과제 그대로다. 제품 코드 변경은 없으며 본소스의 기존 dirty Unity 설정/자산을 건드리지 않았다. 격리 작업트리의 설치 중 생성된 tracked 변경만 복구했고 기존 SDK DLL 폴더는 유지했다.
