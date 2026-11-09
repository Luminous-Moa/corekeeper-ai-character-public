# 최종 검증 결과 — 2026-11-06 20:08 KST

고정 커밋 b25099f6fd31e4c4c3618357eb9e8101e0e17124의 Unity CompileCheck 및 BuildAndInstall exit0, C# 컴파일 오류0. 설치 C# 87개를 수정판과 줄끝 정규화 대조하여 차이0, 게임에서도 Successfully compiled AIPlayerSpike safetyCheck=False 확인. 게임19:58:58 시작,20:07:49 정상 종료, 원복20:08:16 완료. 아래 준비·대기 절은 실행 전 이력이다.

새 격리 월드 choice1106(화면 슬롯8, 파일 index7, GUID 89e71385a43c34ab93ac3039963244e4, 실제 worldinfo seedString 623288821953)에 Gemini만 활성화하고 /ai gemini goal explore를 입력했다. 모델 선택·응답을 강제하지 않았다.

- **대표 실제 흐름 통과:** 최종 INPUT #1 t197.20 → 모델 dig_12_0 선택 t199.41(probability0.92, confidence0.90) → 파들어가기14.3초 후 DONE t213.75. 벽4개를 실제 부쉈고 가방 흙+3. #2는 모델이 follow_28867을 골라2.0초 후 호스트2.4칸까지 도착했다. [원문](evidence/live-representative.txt).
- **폭발 prefab 경고 실게임 확인:** 최종 INPUT #64(t248.68)의 dig_4_-36 및 #116(t265.77)의 dig_24_-32에 실제 WallNaturalExplosiveBlock1개 포함. 두 후보 모두 새 경고 전문인 “채굴로 폭발할 수 있어 가까이 있으면 피해 위험”, “피해량·범위 미확인”, “폭발을 기다리며 물러나는 동작이 없음”을 제공했다. 모델은 각각 기존 동쪽 걷기 계속을 선택했다(1.00/0.98). 폭발 채굴 실행·피해량·생존전략 개선은 시험하지 않았다. [최종 INPUT 및 응답](evidence/live-explosive-inputs.txt).
- **ETA 표기 확인:** #2 retreat 후보 열린 길13칸→약2초는13/5.7 반올림과 일치한다. #1 굴 길16칸·32타→약19초는16/5.7+32×0.5와 일치한다. 실제 굴 실행14.3초와 표시19초가 동일하다고 주장하지 않는다. 추정치의 계산 기준 통일을 확인했다.
- **줍기 범위:** 최종 INPUT에 일반 블록·Mushroom 줍기 후보가 관측됐다. 가까운 일반 물건2개 뒤의 목표품목·필요음식 보존이라는 특정 경계 장면은 구성하지 않았다. 해당 보존/중복/먹을 필요 조건은 구현자의 오프라인 회귀 근거와 구분한다.

원본 전체 결정로그는 [live-decisions-200205.log](evidence/live-decisions-200205.log),3,687,025bytes. SHA256=6EEC30915AD4029ECD1427E0A1DA2D5FE1D74D18FB7A072F9A10CBCC45FDA6F3. 게임 중 파일목록의 길이0 표시와 달리 공유읽기로 내용이 읽혔고 종료 후 원본 복사본을 확보했다. [Player.log](evidence/live-Player-final.log)도 보존했다.

## 화면의 Gemini 호출 실패 원인

이번 하위 판단 시험에서 상위 bridge를 기동하지 않았고 TCP8331 listener0이었다. 다만 기본 auto 플래너를 미리 끄지 않아 **제가 입력한 직접 목표 명령도** 상위 요청을 깨웠다: t197.0 /ai gemini goal explore → PLANNER report #1 trigger=host_goal → t199.1 bridge not reachable (Cannot connect to destination host); t321.2에120초 재시도 실패. 별도의 자연어 사용자 채팅 때문이라고 단정하지 않는다. 시험설정이 만든 상위 연결 실패이며 하위 선택지 변경 실패가 아니다. [실패층/중지 근거](evidence/bridge-layer-and-stop.txt).

/ai all stop은 상위 auto와 목표를 껐지만 근처 적으로 자기방어 하위 판단이 시작되어 /ai off gemini로 멤버를 비활성화했다. 이후 members[]/aiEnabled=false, 게임 정상 종료. 추후 같은 하위-only 시험은 상위 auto 상태와 자기방어를 포함한 호출 종료 조건을 먼저 확정해야 한다.

## 복원과 남은 정리

게임·Unity 프로세스0과 원복 대상 실제 절대경로·reparse부재를 재확인한 뒤 기존 livectl restore 수행. **mod85/AIPlayers43/localsteam248/remote40 파일 모두 사전 해시와 동일, registry 동일, 시험 스위치 포함0차이**. [복원 검증](evidence/restore-verify.txt). 공유 슬롯은 HEAD에 즉시 반환했다. 누적 실제 사용량은 원복하지 않았으며 이번 증가238호출·402,880입력토큰·$0.040288을 보존했다([전](evidence/usage-before.json),[후](evidence/usage-after.json)). 새 결정로그는 보존 예외로 남겼다.

**남은 정리: 이번 choice1106의 Steam Cloud 삭제.** 슬롯8 삭제 확인창에서 문서화된 클릭1회를 안전하게 시도했으나 삭제되지 않았다. 추가 입력 우회 없이 중단했다.20:07:52 worldinfos/7.worldinfo, worlds/7.world.gzip, worldgenparams/7.json의 Upload OK 및20:07:53 Upload complete 확인, 삭제 증거는 없다([Cloud 근거](evidence/cloud-choice1106-upload.txt),[월드 식별](evidence/choice1106.worldinfo.json)). 로컬 원복은 완료됐지만 Cloud 삭제 완료로 보고하지 않는다. **기존 rfmaint1106(슬롯7)과 사용자 월드는 삭제 시도하지 않았다.** 시험 전 백업/설치 대조 및 추가 산출물은 C:/Dev/_scratch/choice-live-20261106-1956/에 있다.

---
# 당시 기록: 실게임 검증 준비

2026-11-06. 담당 범위는 준비 조사다. 부모의 검증판 커밋 및 시작 지시 전까지 게임·Unity·브리지·모델 실행, 공통 설치 변경, 프로세스 중지, UI 입력을 하지 않았다. start-job 및 computer-use의 SKILL/guidance/api/confirmations를 읽었다. 세션 기록은 부모가 저장한다.

## 현재 확인

- 준비 시점 CoreKeeper/Unity 프로세스 0, TCP 8331 listener 0. 시작 직전 다시 확인해야 한다.
- Unity 6000.0.59f2, CoreKeeper.exe, 기존 livectl.ps1 존재 확인.
- 원본 ai-character 902721e, 수정 작업트리는 CoreKeeperModSDK-choice-fixes(구현 중). 게임 설치 대상 커밋은 아직 미지정이다.
- 기지 담당의 당분간 공통 설치/게임 미사용 회신은 부모가 전달했다. 이 문서는 잠금이나 이후 시간의 사용 허가를 대신하지 않는다.

## 최소 대표 흐름

수정 대상은 하위 행동 선택지이므로 상위 LLM bridge를 추가로 실행할 필요가 없다. 격리된 새 시험 월드에서 하위 판단 모델 한 명만 켜고 기존 명시 목표 명령으로 입력→실제 모델 선택→기술 시작→실행 결과를 확인한다. 명시 목표는 모델 답을 가짜로 만드는 것이 아니지만 상위 자연어 판단까지 검증한 것으로 쓰지 않는다.

1. `decisionlog.on`으로 전체 INPUT/응답 기록을 켠다. 기존 모델·프롬프트·사용량 정책은 유지한다.
2. 한 멤버의 `goal explore`로 실제 관측 지형에서 걷기/파기 후보를 받는다. 후보에 표시된 열린 길 거리와 ETA를 수정판의 동일 속도 기준으로 대조하고, 모델이 고른 행동의 key와 기술 시작/완료를 연결한다. 실제 경로의 문·벽·장애물이 있다면 순수 걷기 시간과 총 실행 시간을 같은 값이라고 주장하지 않는다.
3. 장면에 필요한 물건이 자연스럽게 보이면 `goal Wood 2` 등 실제 품목 목표로 줍기 선택지를 관측한다. 거리순 일반 물건 두 개 뒤의 목표 품목/음식이라는 엄밀한 경계는 구현자의 오프라인 회귀시험으로 확인하고, 실게임에 그 장면이 없으면 관측하지 못했다고 남긴다. 임의 인벤토리 지급을 '줍기' 검증으로 대신하지 않는다.
4. 폭발성 벽이 실제 탐험 후보에 들어오면 전체 INPUT에서 위험 문구와 다른 후보의 구분을 확인한다. 고른 위험 경로에 따라 실행 여부/결과를 관측하되, 사망 재현이나 위험 선택을 강제하지 않는다. 우연히 벽이 안 보이면 고정 로그 장면/오프라인 경계 확인을 사용하고 실게임 미관측으로 구분한다.
5. `/ai all stop` 후 추가 판단이 멈추는지 확인하고, 로그/월드 ID/캐릭터 상태 및 이번 비용 차이를 보존한다.

대표 실제 모델 흐름 한 건과 수정 세 조건의 오프라인 경계 근거가 결합되면 이번 작은 변경에 비례한 검증이다. 모든 지형·행동 조합이나 모델의 생존 전략 개선까지 성공 기준을 넓히지 않는다.

## 보존·설치 순서

1. 현재 게임/Unity/8331, 사용자 게임 사용 여부 및 다른 대화의 시험 사용을 재확인한다. 실행 중인 타 작업을 임의 종료하지 않는다.
2. 새 회차 절대 백업 폴더를 만든다. livectl.ps1의 대상은 mod, AIPlayers, LocalLow Steam/435751233, Steam userdata/435751233/1621690/remote 및 HKCU 게임 레지스트리다. 대상의 실제 절대 경로·일반 디렉터리 여부·사용자 경로 일치를 검증하고 backup/verify 차이 0 뒤에만 변경한다. `/MIR`가 있는 restore는 같은 검증을 다시 거친다.
3. 수정판에서 Unity CompileCheck를 통과한 뒤, HEAD가 시작을 지시한 고정 커밋의 BuildAndInstall을 실행한다. 설치 전체 .cs와 manifest를 수정판에 대조하고 게임 시작 뒤 `Successfully compiled AIPlayerSpike`를 확인한다.
4. UI는 현재 sky 반환 창 한 개를 선택해 관측→한 행동→새 관측으로 조작한다. 과거 PowerShell UI 스크립트/좌표를 재사용하지 않는다. 화면이 없으면 현재 가능한 문서화된 화면 경로를 HEAD에 보고한다. 기존 FoldRemote 자료의 승인/프로세스 핸들을 현재 작업 승인이나 핸들로 가정하지 않는다.
5. 새 시험 월드만 사용하며 기존 rfmaint1106 및 사용자 월드는 선택/수정/삭제하지 않는다. 실제 생성 이름·슬롯·GUID를 기록한다.

## 정리의 알려진 제한

기존 rfmaint1106은 game UI의 삭제 버튼을 일정 시간 유지해야 해서 삭제하지 못했고 Steam Cloud에서 재다운로드됐다. 현행 sky API에도 hold/down/up/duration이 문서화되어 있지 않다. 과거 FoldRemote는 화면은 제공했지만 입력 시 '지금은 PC로 입력을 보낼 수 없습니다'를 보여 유지 입력도 실패했다. 이전 기록의 미해결을 이번 준비가 해결한 것으로 표시하지 않는다.

HEAD 후속 결정: 이번 수정 검증에 필요한 새 시험 월드 생성은 가능하다. 정리 때 현재 UI와 문서화된 입력으로 최소 1회 안전하게 삭제를 시도하고 제한되면 즉시 기록·보고한다. 삭제 가능성 자체로 검증을 막거나 별도 사용자 승인 흐름을 만들지 않는다. 장시간 원격 입력 디버깅·미지원 API 호출은 하지 않는다. 기존 rfmaint1106 삭제는 이번 범위가 아니다.

실행했다면 원본 로그를 먼저 보존하고, 이번 월드만 게임 UI에서 삭제한 후 정상 종료·Steam Cloud 삭제 반영을 확인한다. 이후 프로세스 0 상태에서 기존 백업 restore/verify를 수행한다. 실제 usage 누계는 원복하지 않는다. trial log/knowledge dump 제외 항목은 별도 기록한다. 게임 삭제 없이 파일 복원만 했다는 이유로 Cloud 정리가 끝났다고 쓰지 않는다.

## 원본 근거

- [이전 환경·복원 절차](../../리팩토링-20261106/02-environment-and-live-check.md)
- [이전 Unity와 실게임·삭제 실패](../../리팩토링-20261106/15-unity-and-live.md)
- [이전 폭발벽 인근 쓰러짐의 한정 해석](../../리팩토링-20261106/18-review-live-observation.md)
- C:/Dev/_scratch/refactor-runs/livectl.ps1 (이번에는 읽기만 함)
- C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIGoals.cs 및 AIBody.Commands.cs (명시 목표/testcmd 의미 확인)

## 준비 완료 조건 / 대기

검증판 커밋·Unity 통과 근거와 HEAD의 게임 검증 시작 지시를 기다린다. 제품·게임·설치 상태 변경 없음. 이 파일은 준비 계획이며 실게임 통과 보고서가 아니다.


## Unity 준비 후속

19:49~19:51 KST. HEAD가 후보 85f48145b68d02be434dbef9a6c76247b9b91277의 compile-only를 맡겼다. 해당 트리는 clean이었지만 Unity용 게임 참조·Library·로컬 SDK 설정이 없어, 원본 SDK의 기존 로컬 설정 7개 및 기존 refactor worktree의 Plugins/CoreKeeper와 독립 Library를 복사했다. 제품 소스와 공통 설치본은 바꾸지 않았다. 게임 참조 440파일 SHA256 차이 0, Library reparse point 0. 환경 로컬 변경은 구현 커밋에 포함하지 않는다. 근거 evidence/unity-copy-plugins.log, unity-copy-library.log, unity-environment-check.json, unity-source-before.json.

첫 복사 명령은 PowerShell의 /LOG 인자 구성 오류로 파일 복사 전 실패했고, 명시적 문자열 인자로 바로잡아 성공했다. 삭제·MIR·원본 수정은 하지 않았다.

CompileCheck 전용 진입점이 설치하지 않음을 소스로 확인했다. 실행 직전 CoreKeeperModSDK-base의 타 작업 Unity CompileCheck PID73160이 감지되어 이쪽 Unity를 실행하지 않았다. 이후 그 프로세스는 정상 종료된 것으로 관측했다. HEAD가 독립검수 수정판을 기다려 한 번만 검사하도록 지시하여 현재 compile-only도 대기 중이다. 아직 Unity 통과 결과는 없다.

## 최종 후보 Unity 검사

최종 고정 커밋 b25099f6fd31e4c4c3618357eb9e8101e0e17124로 19:53~19:56 KST CompileCheck 실행. exit0, CompileCheck: ok 1건, error CS 0. 새 AIMoveEstimate.cs/AIPickupOptions.cs/AIWallRisk.cs가 실제 Library/Bee compiler rsp에 포함되었고 Scripts .cs/.meta 전후 SHA256 차이0. evidence/unity-result.json, unity-verification.json, unity-compile-b25099f.log가 근거다. 알려진 headless entitlement 404 진단은 있었으나 C# 컴파일/최종 종료 성공과 구분한다.

HEAD 독립 재검수 통과 및 임시 설치·짧은 실게임 시작 지시를 받았다. 회차 루트 C:/Dev/_scratch/choice-live-20261106-1956/에 새 백업 생성. 실제 경로 및 reparse 부재 확인 후 기존 livectl backup/verify 수행, mod85/AIPlayers43/localsteam248/remote40 및 registry 차이0. 실제 사용량 JSON은 별도 usage-before.json으로 보존하고 원복 대상에서 제외한다. 이 시점까지 사용자 월드·캐릭터는 수정하지 않았다.
