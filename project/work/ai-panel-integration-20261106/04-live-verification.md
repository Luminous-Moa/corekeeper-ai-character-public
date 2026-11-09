# 채팅 글꼴과 AI 정보 패널 — 실게임 검증

2026-11-06. 담당: ui_live_verify. **최종 후보 11eb461 실게임 검증과 시험 변경 정리 완료(21:18:50 KST).** 본문 축소·읽기 전용 정보 패널은 HEAD 수용. 공식 b25099f 모드 복원, 게임/Unity/시험 bridge 종료. 사용자 직접 진행한 aiui1106 월드와 4K 설정은 보존했다. 아래 초기 계획·중간 실패 기록은 당시 이력이며 최종 판정과 복원 정책은 마지막 절이 우선한다.

## 준비 및 경계

- `C:/Dev/AGENTS.md`, `00-agreement.md`, 이전 `리팩토링-20261106/02-environment-and-live-check.md`, `15-unity-and-live.md`, `2026-11/2026-11-05-채팅창-개선/6-실게임3.md`와 기존 `livectl.ps1`를 읽었다.
- Computer Use의 SKILL, guidance, confirmations, api를 읽었다. Windows 앱은 node_repl의 `@oai/sky`로 반환된 유일한 대상 창 선택 → 현재 관측 → 한 동작 → 즉시 재관측한다. 옛 고정 좌표/PowerShell 입력 도구는 사용하지 않는다.
- 준비 중 게임·Unity·bridge 시작, 설치, UI 입력, 태블릿 연결, 운영 파일 변경을 하지 않았다. 별도 세션 load/branch/save도 하지 않았다.
- 구현 worktree `C:/Dev/CoreKeeperModSDK-ai-ui`, branch `ai-info-ui-20261106`. 구현 완료 보고와 HEAD의 공통 환경 점유 인계를 받은 뒤 최종 커밋·UI 진입 위치·시험 경로를 고정한다.

## 인계 후 수행할 경로

1. CoreKeeper/Unity 종료, 8331 소유 상태와 다른 작업 점유 종료 확인. 새 회차 전용 backup 디렉터리와 실제 대상 경로/재분석점 유무 확인 후 `C:/Dev/_scratch/refactor-runs/livectl.ps1 backup ...`, `verify ...` 차이 0 확인.
2. 설치/Unity 담당과 역할을 확정하고, 설치 후 소스/manifest 대조 및 게임의 `Successfully compiled AIPlayerSpike`를 확인. 운영 모델·프롬프트·사용량 정책을 바꾸지 않는다.
3. 새 시험 월드의 이름·슬롯·GUID를 기록하고 사용자 월드 1~6은 보존. 실제 bridge는 worktree 실행, 회차 전용 로그 경로와 실제 health 확인. fake 응답을 실제 AI 발언의 근거로 쓰지 않는다. 필요한 경우만 `testcmds.on`, `decisionlog.on`을 켜며 원래 존재 여부와 복원을 기록한다.
4. 기본 Enter 입력 → `@G` 등 힌트/Tab → 실제 한국어 AI 짧은 발언 1건을 확인한다. 서로 다른 호스트 문장으로 줄 밀도를 확인하고, 입력 열림 동안 유지 및 닫힘 뒤 기본 7초 선명/약 1초 fade를 관측한다. 과거 동일 문장 반복+멘션 시 빈 줄 재현은 기존 문제로 분리한다.
5. 작은 AI 선택부 → AI별 상세 스킬/장비 → 닫기. 실제 저장/서버 값과 표시를 대조하고, 장비가 비어 있으면 실제 착용 장비가 있는 시험 상태를 확보해 재확인한다. 패널이 추가 AI 판단을 부르지 않는지 관련 로그를 확인한다.
6. 패널/선택부 클릭 전후 플레이어 위치·공격 모션/지형/아이템 상태를 관측한다. 새로고침 및 AI 참가/퇴장, 월드에서 메뉴 복귀/재입장의 상태 갱신을 확인한다. 전체 게임 HUD/메뉴 폰트는 채팅·새 패널 외 범위에서 유지되는지 화면으로 비교한다.
7. [초기 계획, 이후 폐기] 시험 월드 삭제와 전체 백업 복원을 예정했으나 사용자가 이 월드에서 직접 진행하고 화면 설정을 바꿨으므로 HEAD가 최종 사용자 진행·설정 보존으로 변경했다. 최종적으로 월드를 삭제하지 않았고 전체 restore를 실행하지 않았다. 시험 장비와 설치 모드만 복원했으며 실제 비용도 유지했다.

## 증거와 알려진 제약

- 결과 화면은 `evidence/live/`에 원본 PNG와 시간/단계명을 보존한다. sky 캡처는 반환 화면으로 직접 확인하며 payload 재출력은 하지 않는다. 재현 증거 저장을 위해 반환 Screenshot.url을 파일로 보존할 수 있다. 별도 원격 영상은 필요할 때만 사용한다.
- Player.log, bridge 로그, 필요한 시험 월드 AI 상태, 설치/복원 로그를 함께 보존하고 화면 판정과 로그 판정을 구분한다. 비밀 설정 원문은 보고서에 담지 않는다.
- 모니터가 없어 sky 캡처가 실패할 때만 기존 ADB 태블릿 FoldRemote의 저장된 PC 연결을 사용한다. 기존 앱/host/권한/페어링은 변경하지 않고 종료 시 정상 끊기로 디스플레이 상태를 돌린다.
- **시험 월드 삭제 위험:** 직전 회차의 `rfmaint1106`(슬롯7, GUID `97b88eb024da6d5794f65879c2592dcc`)는 유지 클릭 부재와 FoldRemote 입력 불가로 Cloud 삭제가 미완료였다. sky에는 hold/duration/down/up API가 없고 직전 단발 키/drag도 실패했다. 이전 잔여 월드와 이번 시험 월드를 혼동하지 않는다. 삭제 지연/불확실 시 추가 우회나 Cloud 직접 삭제 없이 HEAD에 즉시 보고한다.
- 로컬 restore 성공만으로 새 월드의 Cloud 삭제를 통과로 판정하지 않는다.

## 결과

최종 결과는 문서 끝의 「최종 후보 11eb461」과 「정리·복원 완료」를 참조한다. 초기 패널 가독성 실패는 정수배율 수정 후 해소됐고 장비 비어 있음/실제 착용 양쪽 원천을 대조했다.

## 구현 후보를 반영한 준비 갱신

후보 `3bb470dd6b391f9035c6d1580ffe2b8b53f4c4da`의 `01-design.md`, `02-implementation.md`, `AIInfoPanel.cs`, `AIInfoSnapshot.cs`와 기존 채팅/캐릭터 저장·시험 지급 경로를 읽었다. HEAD 인계상 비Unity 관문 core1078/게임 API 컴파일 통과, Unity compile-only는 타 대화 게임 점유 종료 뒤 구현자가 수행한다. 이 담당은 아직 어떤 실행도 하지 않았다.

HEAD 후속 인계로 **설치/실게임 대상은 `ecc8a88c1f6cacd4879225ec1967a74149f4c4b9`로 갱신**됐다. 짧은 click down/held edge 처리와 회귀3개가 추가됐으며 UI19검사/API compile 통과라는 인계다. 실제 설치 전 작업트리 HEAD와 Unity 완료 보고를 재확인한다.

20:08:16 타 작업 복원 인계: mod85/AIPlayers43/localsteam248/remote40 및 registry 원복 해시 일치, CoreKeeper/Unity0. 이후 구현자가 ecc8a88 Unity compile-only 점유 중이므로 HEAD 완료 알림까지 설치/게임 시작을 대기한다. 임시설치·백업·실게임·복원은 이 담당 소유로 인계됐다. **기존 외부 시험 월드 `rfmaint1106` 슬롯7 및 `choice1106` 슬롯8(GUID `89e71385a43c34ab93ac3039963244e4`)은 이번 대상이 아니며 삭제·변경하지 않는다.** 두 월드 모두 UI 삭제 실패/Cloud 잔여 이력이 있다. 새 회차 이름은 `aiui1106`을 우선 사용하되 생성 직전 목록 중복 부재와 빈 슬롯을 확인한다.

### 최종 기준 갱신

- 선택지 기능의 사용자 승인 병합으로 기준은 `b25099f`가 됐다. 이전 `ecc8a88`을 그대로 설치하지 않는다.
- HEAD가 확정한 실제 후보는 **`2762495663a6cfa26b7d966ad6676b53f880c73e`**, 동일 worktree/branch다. `b25099f` 위 rebase, 선택지+UI 등록 보존, core1103/.NET/font check 통과 인계. 런타임 동등한 이전 `ecc8a88`의 Unity CompileCheck exit0/CS0도 완료됐다.
- 선택지팀의 `b25099f` 최종 BuildAndInstall 완료를 HEAD로부터 받은 뒤 **새 백업 → 2762495 BuildAndInstall → 실게임 → 새 백업의 b25099f로 복원**한다. 새 설치 빌드가 재배치된 최신 후보의 Unity 컴파일도 검증한다. 완료 알림 전 백업·설치를 시작하지 않는다.
- 별도 화면 끊김 조사와 섞이지 않도록 실제 시험 시작/끝 KST, 게임 PID, Player/bridge/설치 로그 경로를 명시한다. 패널 조작 때 명백한 프레임 정지나 재생성 오류는 관측해 보고하되 독립 성능 조사로 확대하지 않는다.

### 실제 UI 관측 순서

- 호스트 월드 자동 표시. 선택 막대는 화면 `(max(8,W-304),max(8,H-40))`의 292×28, 상세는 그 위 4px 간격 292×246. 문서 좌표는 발견용이며 클릭은 새 sky 화면에서 확인한 실제 위치만 쓴다.
- 처음에는 막대만 촬영. 미접속 AI 하나를 선택해 `미접속`/빈 정보, 실제 참여 AI를 선택해 `접속 중 · 읽기 전용`과 스킬12종(2열)/착용장비10종(1열)을 확인한다. 같은 AI 재클릭과 X 각각 닫기를 확인한다.
- 장비 행은 손/머리/상의/하의/목걸이/반지1·2/보조손/가방/랜턴. 단순 인벤토리 보유와 착용을 구분하고, 이름이 긴 장비/한국어에서 잘림이나 행 겹침이 있는지 본다. 기본 1배 비트맵을 원본 해상도에서 판정한다.
- 선택 직후와 0.5초 갱신 후를 구분한다. 패널을 연 채 해당 AI off→미접속/기존 값 제거, on→실제 정보 재생성을 확인하고 메뉴/인벤토리/지도에서는 패널이 숨는지 확인한다. 재입장 때 이전 선택/다른 월드 캐릭터 데이터가 남지 않아야 한다.
- 실제 짧은 한국어 멘션 답을 받은 뒤 서로 다른 문장 몇 개로 한글 선명도/줄 밀도/자모·문장부호를 확인한다. Enter/Tab/멘션 경로는 기존 채팅 그대로인지 본다. `sky.type_text`로 완성 한글을 넣는 것과 실제 OS IME 조합 입력은 구분하며, 직접 조합을 확인하지 못하면 IME까지 통과라 쓰지 않는다.
- UI 선택/탭/닫기 클릭 때 호스트의 공격 또는 이동이 없는지 전후 장면과 로그로 확인한다. 패널 안에서 밖으로 drag 후 정상 게임 입력 복귀를 확인한다. `sky`의 문서화 API에는 개별 down/up·duration·동시 버튼 유지가 없으므로 **다중 버튼 동시 유지 실험은 별도 지원 입력경로가 확인되지 않으면 미실행 한계로 남긴다**. 단발 클릭 성공으로 이를 통과 처리하지 않는다.

### 로그·독립 데이터 대조

- 시작 `AI-UI font ready`, `AI-UI chat metrics`의 원래/교체 셀 및 간격, 선택 `AI-UI selected <name>: <status>; skills=N; equipment=N`, snapshot 오류 여부를 수집한다. 선택 로그는 값 전체를 제공하지 않으므로 로그 행 수만으로 정확성을 주장하지 않는다.
- 스킬 원천은 서버 `SkillBuffer.Value` XP를 게임 `SkillExtensions.GetLevelFromSkill`로 환산한 값이다. 정상 캐릭터 저장의 `skills` XP와 실제 보이는 레벨을 대조하며, 환산 근거가 없으면 XP를 레벨로 오인하지 않는다.
- 착용 장비 원천은 서버의 활성 프리셋 `ActiveEquipmentPresetCD` + `EquipmentPresetsBuffer`, 인벤토리 `ContainedObjectsBuffer`, 손 `EquippedObjectCD.equippedSlotIndex`. 기존 save의 인벤토리와 서버 인벤토리 로그를 보조 근거로 쓴다. 시험 지급 파일은 해당 시험 월드 AI 캐릭터 경로의 `.testgive`이며 **지급만으로 착용됐다고 보지 않는다**.
- 구현자 추가 인계: 기존 `AIInspectSkills.Inventory` Facts에 `equipped={slot}번 칸 {objectID}`, AIBody에 `slot=... held=...`가 있어 현재 손의 독립 대조에 쓸 수 있다. `/ai inv`는 현재 기술을 중단할 수 있으므로 자연 발생 observation/inventory 로그를 우선한다. 활성 프리셋 직접 로그는 없고 `SaveFromServer`도 active preset을 새로 저장하지 않으므로 저장 파일만으로 프리셋 실시간 일치까지 증명했다고 쓰지 않는다.
- UI 조회만 하는 구간에서는 AI를 stop 상태로 안정화한 뒤 실제 bridge 요청/결정 로그 전후 수를 비교한다. 자연어 멘션과 기존 AI 자율 판단 요청을 UI가 유발한 호출로 섞지 않는다.

## 1차 실게임 실행 — 2762495

### 실행 경계와 보존

- 20:20:44 HEAD가 선택지 b25099f 정식 설치 완료와 독점 점유를 인계했다. 우리 새 백업은 20:21:47 생성. `evidence/live/backup/`에 mod88파일·AIPlayers43·localsteam248·remote40 및 registry를 보존했고 verify 차이 0. 기준 manifest SHA256 `E1E4D51294F8466C685D08D1A4222AA7484945688DDEB25E38C2E85B854D4F9B` 일치. 대상 경로 재분석점 0 확인.
- 2762495 BuildAndInstall은 20:23:57 exit0, C# error0. 설치된 92 C#의 원시 바이트 해시 차이0, manifest92 경로 누락0, fontData 존재. `build-install.log`, `build-result.json`, `installed-source-check.json`, `manifest-check.json` 참조.
- 게임 PID4124 시작 **20:24:16 KST**. 게임 자체 `Successfully compiled AIPlayerSpike safetyCheck=False` 확인. 실험 Player.log 사본은 `Player-first-trial-readonly.log`(20:36경 중간 사본); 종료본은 나중에 추가한다.
- 실제 bridge PID41456, 20:23:42 시작, 포트8331. 후보 worktree `bridge.mjs`, 별도 `bridge-logs/`, 실제 Gemini/GPT 로그인 health 확인. fake 응답/모델 설정 변경 없이 기존 하위 gpt-6-luna 경로 사용. `bridge-process.json`, `bridge-health.json`, `bridge-out.log`, `bridge-logs/plan.jsonl`.
- 우리 시험 월드는 **aiui1106 슬롯9 / GUID a66565fae4bf489b0ec9c119ec31add3**, 기존 character4 Luminous. 생성 화면 seed966095607185. 기존 슬롯7 rfmaint1106과 슬롯8 choice1106은 Cloud에서 보이며 삭제하지 않았다. `02-worlds-before.png`, `03-create-world.png` 참조.
- 시험 전용 `testcmds.on`, `decisionlog.on`만 추가했고 testgive/arena는 사용하지 않았다. 비용 usage 파일은 복원 제외로 유지한다. 실제 삭제/원복은 아직 미실행.

### 확인된 동작과 증거

- 월드에 선택 막대가 자동 표시됨. ChatGPT 선택 시 `미접속`, 0행. Gemini 참가 직후→약0.5초 뒤 `접속 중 · 읽기 전용`, 스킬12/장비10으로 갱신됐다. `05-panel-offline.png`, `06-gemini-skills.png`, `07-gemini-skills-ready.png`.
- 첫 실제 스킬은 달리기 Lv.3, 나머지11종 Lv.0. 장비 초기에는 모두 없음. 목검/랜턴을 단순 소지한 것과 착용을 구별했다. 독립 XP→레벨 환산 대조는 아직 미완료.
- Enter 입력, 완성 한국어 입력, `@Ge` 힌트와 Tab→`@Gemini ` 자동 완성 성공. `09-mention-prefix.png`, `10-tab-completed.png`, `11-korean-mention-input.png`.
- 실제 상위 Gemini 호출 report1: t231.4 요청→t245.5 응답, 14.1초. 답변 `안녕하세요! 반갑습니다. 목검을 쥐고 대기할게요.`가 채팅에 표시됨(`14-actual-ai-korean-reply.png`). 이는 목검 착용 실행의 증거가 아니며 당시 손은 None이었다.
- 다음 실제 요청 `나무 두 개만 모아 줘.`는 상위 report2가 gatherWood2를 지시하고 하위 DECISION1이 실 API 응답0.98초 후 root_2_1을 실행했다. 이후 자연 수집/제작으로 나무 곡괭이를 들었고 패널 손 행이 `나무 곡괭이`로 갱신. t389.7 AIBody held=WoodMiningPick, t394.3 planner me.hand=WoodMiningPick와 일치(`17-state-after-input-notice.png`). 주입 아이템이나 fake 응답이 아니다.
- AI는 추가 작업대/나무 갑옷을 제작하며 자율 판단을 계속했다. 모델이 갑옷을 입었다고 말한 것만으로 실제 착용을 인정하지 않았다. 패널에서 머리/상의/하의 없음이 보였으므로 이 범위의 독립 착용 대조는 추가 확인 필요.
- 입력 열린 상태에서 10초 넘게 채팅 유지(`14`, `15-chat-open-stable.png`). Escape로 닫은 뒤 fade 시계열438/3221/6687/7254/7760/8458ms 캡처 저장(`fade-*.png`, `fade-times.json`), 8458ms에 완전히 사라짐. 시작 로그 CHAT-UI는 7초 유지+1초 fade. 정량 밝기 대조는 아직 정리 중.
- X로 상세 닫기와 다시 Gemini 선택은 성공(`16-panel-close-x.png`). 다중 버튼 유지·drag 밖 해제·명확한 공격/사용 누출 확인은 아직 미실행. 단발 클릭 후 호스트 버섯25 유지 관측만으로 전체 누출 통과라 판정하지 않는다.

### 글꼴과 가독성 판정

- t113.6 글꼴 생성 11369glyph / cell10 / atlas1792×1157 / **33ms**, t141.3 원래 Font8B cell12 spacing-2 ppu16 → compact cell10 spacing1 ppu16. 채팅 전용 로그 확인.
- 2560×1440의 `14-actual-ai-korean-reply.png`에서 x175~899 흰 픽셀(RGB>240) 행 투영 기준, 일반 한글 글자 높이43~44px. 줄 시작 y604→657→710 간격53px, y758→806→854 간격48px. 게임 UI 배율 때문에 cell10이 최종 약44px로 표시된다. 사용자가 채팅 글자를 더 작게 원해 원본 약절반 축소안을 구현자가 준비 중이다.
- **패널 가독성 실패:** 1440p에서 본문10px가 지나치게 작다. HEAD가 `07` 실제 화면으로 확인하고 고해상도 정수배율 패치25db8ed를 준비했다. 현행 패널 크기를 통과로 처리하지 않는다. 새 패치 설치는 아직 안 했다.
- 현재 메뉴/HUD 일반 폰트의 정식 전후 비교와 실제 OS IME 조합 입력은 미완료. type_text 완성 한글 성공을 IME 통과로 확대하지 않는다.

### 입력 일시 정지

- t535.7 `/ai gemini stop` 전송, planner stopped/goal cleared/skill cancelled 로그 확인. 추가 UI 조회만으로 호출이 증가하지 않는 안정 구간 시험을 준비했다.
- 20:33~20:34 패널 우클릭 시 sky가 `user input was detected in this window; call get_window_state before continuing`을 반환. 재관찰하니, 테스터가 이동을 입력하지 않았는데 호스트가 코어에서 Gemini 옆 광산으로 이동해 있었다. 우클릭은 성공했다고 기록하지 않았다.
- HEAD에 즉시 전달. HEAD는 사용자 직접 조작 여부를 확인하며 답 전까지 자동 입력·게임 종료·설치 변경을 멈추라고 지시했다. 현재 읽기 전용 로그/증거 정리만 한다. 시험 취소나 원복 완료를 의미하지 않는다.

### 사용자 직접 시험에서 보고된 장비 결함 — blocker

사용자는 직접 조작 중임을 확인했고, **Gemini가 장비를 입었는데 정보창에 제대로 표시되지 않는다**고 보고했다. 합의의 실제 착용 장비 표시 미충족으로 취급한다. 앞의 모델 발언/소지 구별 메모는 테스터가 확보한 증거 범위에 관한 것이며 사용자 관측을 착각으로 결론내린 것이 아니다.

- 20:33 stop 직후 및 20:34 입력 충돌 재관측 화면에서 손은 나무 곡괭이, 머리/상의/하의는 없음이었다. 해당 tool 화면이 남아 있으나 이 순간을 별도 PNG로 저장하지 못했다.
- 새 읽기 전용 `19-user-observed-equipment-mismatch.png`는 사용자가 이미 상세를 닫은 장면이다. 파일명과 달리 한 화면의 장비 불일치를 증명하는 캡처는 아니며 현재 상태 기록이다.
- t578.4~698.4 실제 저장 로그와 현재 저장은 inventory4 WoodBreastArmor, 5 WoodHelm, 7 WoodPantsArmor를 갖는다. `Gemini-user-equipment-observation.json`, `Player-user-equipment-observation.log`에 사본 보존. 저장이 active preset을 새로 저장하지 않으므로 이 자료만으로 정확한 장착 슬롯 판정을 끝내지 않는다.
- 독립 검수자 ui_independent_review에 경로와 관측을 전달했다. 사용자 조작 중이므로 추가 명령·정보창 클릭·게임 종료·설치 없이 읽기 조사만 계속한다.

### 읽기 전용 추가 정량

- fade 최초 캡처의 흰 픽셀20,859개를 같은 좌표로 추적했다. 평균 RGB는 438/3221/6687ms 모두253.82, 7254ms253.68, 7760ms232.46, 8458ms0.93(흰 픽셀0). 약7초 유지 후8초에 소멸하는 기본 동작과 일치한다. `fade-pixel-measurement.json` 참조.
- 게임 decompile `Pug.Other/SkillExtensions.cs:GetLevelFromSkill` 공식 floor(log(1−XP(1−factor)/base)/log(factor))를 독립 적용하면 현 저장 XP 채광86→Lv.1, 달리기675→Lv.3, 제작10→Lv.0이다. 이후 재검증에서 이 실제 값과 스킬 화면을 대조할 예정이며 현재 화면 대조 완료로는 쓰지 않는다. 시험은 gainOneSkillPointPerLevel 명령을 사용하지 않았다.

## 사용자 재개 후 독립 동작 확인

- 사용자가 `이제 pc 다시 사용해도 돼`라고 명시하여 Computer Use를 재개했다. PID4124/시험 월드는 동일하며 아직 코드2762495다. 사용자 진행 뒤 코어 옆 제작대·상자와 Gemini 외형을 보존했다.
- `20-resumed-current-state.png`, `21-resumed-gemini-panel.png`(실제로 ChatGPT 선택), `22-gemini-equipment-and-avatar.png`(Gemini 선택)을 저장했다. 22는 Gemini의 화면 외형과 손 나무 곡괭이/머리·상의·하의 없음 표시를 함께 담는다. 외형 원천 vanity/equipment/skin 구분은 새 계측으로 확인할 예정이다.
- 20:45:41 `user-progress-live-snapshot/`에 AIPlayers/localsteam/remote 총375파일, registry, Player.log와 파일해시를 보존. 라이브 복사라 동시성 한계가 있다: exp_state.json 및 bak2개는 복사 뒤 갱신됐고, decisions-202704.log1개는 활성쓰기 때문에 원본 해시를 못 읽었다. 나머지 복사본 원본해시 일치. 이는 원래 b25099f 복원용 `backup/`과 별개다. 업그레이드 직전 정상 종료 후 완결된 저장 백업을 추가한다.
- 아이템 사용 입력의 실제 대조: 슬롯1 버섯23개 선택→장비 패널 내부 우클릭 후23개 유지(`23-panel-rightclick-food23.png`)→게임 지형에 우클릭하면22개로 감소하고 음식 효과가 나타남(`24-world-rightclick-food-control.png`). 우클릭의 패널 차단과 밖에서 정상 복귀 확인. 좌클릭 공격·다중 버튼·드래그 누출은 아직 이 결과에 포함하지 않는다.
- 기본 인벤토리(Tab), 지도(M), 메뉴(Escape)에서 AI 막대/상세가 숨었고 인벤토리·지도 닫으면 같은 Gemini 정보가 복귀했다. `25-inventory-panel-hidden.png`, `26-map-panel-hidden.png`, `27-menu-panel-hidden.png`. 일반 게임 메뉴 글꼴이 작아지는 현상은 관측되지 않았다.
- 사용자가 보낸 `/ai stop` t1152.4 이후 우리 ChatGPT선택t1211.3/Gemini선택t1226.9 및 메뉴/아이템입력 시험 동안 상위PLANNER SEND/하위DECISION SEND 추가 없음. 사용자 이전 실행과 UI조회 비용을 섞지 않는다.
- AI off/on은 현재 외형을 바꿀 수 있어 새 진단 설치 후의 재접속 검사로 미뤘다. HEAD에 현재 메뉴에서 정상 종료 가능한 경계를 통보했으며 설치 지시 대기다. 삭제/복원은 여전히 미실행.

### 해상도 해석 보정

sky PNG 및 metadata는2560×1440이지만 이것이 게임 내부 Screen 해상도와 같다는 보장은 없다. 사용자 진행 후 읽은 registry Screenmanager Resolution은 **3840×2160**, native=true, fullscreen1이며 현재 292폭 패널이 캡처상 약196폭으로 보이는 비율과 일치한다. 원래 b25099f 백업 registry는2800×1752였고 이번 테스터가 해상도를 바꾼 적은 없다. 따라서 앞의 `1440p` 표기는 캡처 크기이며 실제 게임 해상도로 단정한 표현은 정정한다. 가독성 실패 판정과 채팅43~44px 측정은 **캡처상** 사실로 유지한다. 정확한 게임 Screen.width/height는 새진단 또는 설정화면과 함께 대조한다. `display-current-registry.json` 보존.

### 진단 전용 후보로 업그레이드 준비

HEAD 지시로 채팅 미완 코드를 제외한 detached snapshot `C:/Dev/CoreKeeperModSDK-ai-ui-diagnostic` / `134b1c3248b635fc98fc6a766bbe25bcb37494ab`를 사용한다. 포함은 패널 정수배율25db8ed와 서버/client GUID·vanity·renderer 진단이며 채팅 절반 축소 코드는 없다. 게임은 저장 후 메인 화면으로 복귀했고 정상 종료했다. 종료 중 패널이 잠깐 black transition에 남았으나 최종 타이틀에서는 사라졌다(`28`은 black transition, `29`는 타이틀).

20:52:13 `user-progress-closed-checkpoint/` 완결 백업: mod93, AIPlayers54, localsteam251, remote49와 registry/switch 전부 verify0. `Player-first-trial-final.log`와 실제 사용량/현재 결정 로그도 별도 보존했다. 원복 기준 `backup/`(b25099f)은 덮어쓰지 않았다. Unity PID55496로 이 고정 snapshot의 BuildAndInstall을 시작했다. 재입장 전 AI off/on이나 시험 월드 삭제를 하지 않았다.

## 진단 후보 134b1c3 결과

- BuildAndInstall20:54:42 exit0/CS0, 설치93cs 원시해시 차이0. `build-diagnostic.log`, `build-diagnostic-result.json`, `diagnostic-installed-source-check.json`. 새 게임PID19876, 20:55경 시작, 게임 자체 compile성공.
- 같은 aiui1106/캐릭터4에 재입장했고 AI off/on 명령을 넣기 전에 Gemini 자동 참가→선택→장비탭으로 확인했다. `30-diagnostic-first-gemini.png`는 자동 참가 진행 중의 미접속 순간, `31-diagnostic-equipment-avatar.png`는 참가 후 실제외형·장비동시 캡처다.
- **t97.4의 실제 원천:** server currentEquipment와selectedPreset0 모두head51/chest53/legs54=None, serverVanity66/67/68=None. 서버playerGuid `8bc92592511c027f446f91659fd21e7d`로 매칭한 client entity도current/preset/vanity 모두None. `renderedEquipmentHandlers`와`renderedVanityHandlers`도head/chest/legs=None, `renderedArmorAddresses` 3개 모두m_low0/m_high0. 손slot0WoodMiningPick114는heldContained 및UI와일치.
- 원문은 `equipment-source-raw.log`, 전체는 `Player-diagnostic-equipment.log`, 그시점 저장은 `Gemini-diagnostic-equipment.json`. HEAD/작성자/독립검수자에게즉시전달. 이전 사용자관측을 무시하지 않고 재입장후동일해보이는외형과동시원천자료로원인을확정하도록 했다. 진단의범위는재입장후이며 이전프로세스의메모리를소급증명하지않는다.
- `32-diagnostic-skills.png` 채광Lv1/달리기Lv3/나머지0. 저장XP86/675/제작10의게임환산공식과일치한다. 사용자진행으로새저장XP가달라졌는지도별도사본으로확인한다.
- 새정수배율패널은4K게임에서선택막대·정보본문을읽을수있게개선됐고 탭클릭과닫기영역을현재관측좌표로조작가능하다. 캡처기준약20px 글자이며 글자겹침/잘림은보이지않는다. 기존 HUD아이콘뒤를덮는위치는유지되므로패널닫기로복귀시킨다.
- 비어있지않은방어구표시는아직미검증. 기존testgive는가방삽입일뿐장착하지않으므로이를통과근거로쓰지않는다. 사용자진행checkpoint를보존한시험전용장착fixture의필요성을HEAD에알렸으며아직원상태를변경하지않았다.
- 최신 진단 사본의 XP는 채광86, 달리기729, 제작16으로 확인됐다. 사용자 진행 이전675/10과 달라졌지만 공식환산은 여전히Lv1/Lv3/Lv0이며 화면32와일치한다.
21:03 정상 저장/종료 완료. pre-fixture-closed-checkpoint verify0: mod94, AIPlayers54, localsteam251, remote49, registry 일치.
Fixture: 우리 aiui1106의 Gemini.json만 5→51 WoodHelm45,4→53 WoodBreastArmor75,7→54 WoodPantsArmor70을 빈칸과 교환. inventoryObjectNames 동일 이동; aux/locked는 원본 동일. item multiset 및 기타 필드 불변. 제품 자동착용이 아닌 표시 검증 fixture.
originalSHA d797d5c1b7074e5504f681a59b1cf33a1068a5f2c0ba5197f749a326289799cb; fixtureSHA012a8374d85fff75bd7abcb4a9f01817e346b8ddf98bafc53833c918c6e7b9d2. 원본 fixture-Gemini-original.json과 pre-fixture-closed-checkpoint에 중복 보존.
21:03:50 11eb461 BuildAndInstall 시작 Unity PID70256, log build-chat-final.log. 최종 검수 승인 SHA를 HEAD로 확인한 뒤 실행. 자동착용은 사용자 결정대로 별도 작업 범위.


## 최종 후보 11eb461 — 21:05~21:14 실게임 결과

고정 SHA `11eb461ae122f404acec94563c086b09e002987c`의 BuildAndInstall은 21:05:22 exit0/CS0. 설치 C#95개 원시 해시가 소스와 일치했다(`chat-installed-source-check.json`). 게임 PID63960은 21:05:48 시작하여 같은 aiui1106/캐릭터4에 재입장했다. 이번 로그는 `Player-final-chat-trial.log`, 실제 bridge 기록은 `bridge-logs/`이다. 다른 사용자 회차 로그와 구분한다.

- **실제 방어구 양성 표시:** 시험 fixture에서 서버 entity79051:1 및 GUID로 맞춘 client24593:2의 current/preset0 모두 head51=WoodHelm45, chest53=WoodBreastArmor75, legs54=WoodPantsArmor70이었다. vanity는 모두 None, renderer 장비도 같은3종이며 armor addresses는 비어 있지 않았다. `34-fixture-equipped-panel-avatar.png`의 나무 투구/몸통 방어구/바지 표시 및 실제 외형과 일치한다. t89.1 원문은 `fixture-equipment-source-raw.log`. 이는 UI 표시 검증용 저장 fixture이며 제품 자동 착용 성공이 아니다. 사용자가 자동 착용은 별도 작업으로 정했다.
- **작은 채팅 본문:** 실제 Screen3840×2160, sky 캡처2560×1440을 구분했다. CHAT-SCREEN metrics의 기존 compact 높이80·target40·ppu32와 함께 실제 한글 본문 높이24 캡처px를 측정했다. 이전43~44px 대비 약55%, 줄 간격은 약26~29px로 이전48~53px보다 촘촘하다. `38-final-chat-real-ai-reply.png`, `final-chat-row-measurement.json`. 한글 문장 전체가 큰 fallback으로 되돌아가지 않았으며 글자 겹침/잘림은 관측되지 않았다.
- **실제 AI 발언:** report1 t145 요청→t164.2 응답,19.2초,code200,gemini-3.8-flash-low. “안녕하세요! 반갑습니다, 무엇을 도와드릴까요?”가 작은 본문으로 표시됐다. 최종 요청은 keep 결정이며 불필요한 하위 호출은 없었다. 상위+하위 실제 실행은 앞 회차 gatherWood2 로그로 따로 입증했다.37번 PNG는 응답 직전이므로 AI 발언 증거로 사용하지 않는다.
- **입력 행동:** Enter 열기, 완성 한글 입력과 caret,@Ge 힌트,Tab의 @Gemini 완성,Enter 전송 성공(35/36/38). 입력행·멘션 힌트는 기존 큰 글꼴을 유지한다. HEAD는 이 구분과 본문 축소 결과를 수용했고 추가 구현/재시험을 요구하지 않았다. OS IME 조합 자체는 실행하지 않았다.
- **유지/fade:** 입력 닫기 뒤 final-fade-6400은 선명,7400은 흐려짐,8500은 사라짐. 기존7초 유지+1초 fade 동작과 일치한다. 최초000 프레임은 잠깐 비어 있었으나 후속6400은 정상 표시였다. 한 장의 전환 프레임을 지속 결함으로 판단하지 않았다.
- **정보 화면과 기본 UI:** 새 패널은 캡처 기준 약20px로 읽을 수 있다. 스킬 화면43은 채광1/달리기3/나머지0이고 XP86/729/제작16 환산과 일치. X 닫기44,선택 재열기,탭 hit 성공. 기본 인벤토리42에서 막대/상세는 숨고 게임 전체 글꼴은 원래 크기였다. 이전 회차 지도/메뉴 숨김도 확인했다.
- **참가 갱신:** /ai off gemini 후45에서 미접속·표시 정보 없음으로 바뀌며 오래된 레벨이 지워졌다. /ai on gemini 후46에서 접속 중과 실제 스킬 값이 회복됐다. 종료 전 Gemini를 원래 활성 상태로 돌렸다.
- **클릭 누출:** 앞 회차 패널 우클릭의 버섯23 유지, 바깥 우클릭22와 buff라는 양성 대조로 사용 차단/복귀를 확인했다. 최종 패널 안→밖 좌드래그에서는 관측 가능한 공격/회전이 없고 같은 바깥 좌클릭에서는 횃불 휘두름/회전이 나타났다. 공격 피해량까지 계측하지 않았다.39/40 파일명의 torches42는 잘못 붙인 이름이며 실제 수량 감소를 주장하지 않는다(화면은 약92,동일). 다중 버튼 동시 누름/유지는 도구에서 지원하지 않아 미시험이다.

초기 패널 크기 결함은 배율 수정 후 해소. 장비 이슈는 재입장 시 서버/client/vanity/renderer 모두 빈 상태였으며, 별도 양성 fixture의 실제 장비는 정확하게 표시됐다. 이 결과를 이전 프로세스 메모리까지 소급한 증명으로 쓰지 않는다. 최종 기능 blocker는 없고 HEAD의08-acceptance.md 수용을 받았다. 뚜렷한 지속 UI 정지/반복 재생성은 보이지 않았지만 별도 성능 시험은 하지 않았다.

## 정리·복원 완료 — 사용자 진행 보존

최종 정상 저장 후 종료하고 final-fixture-closed-checkpoint를 만들었다. mod96/AIPlayers54/localsteam251/remote49/registry/switch 검증 차이0. 원래 b250 백업과 prefixture 사용자 진행 checkpoint를 덮어쓰지 않았다.

1. 최신 Gemini 파일에서 fixture51↔5,53↔4,54↔7 및 대응 이름만 역교환했다. 장비로 증가한 maxHealth/health116→100도 HEAD 승인대로 복구. 다른 필드는 그대로 유지했다. 결과 SHA는 prefixture 원본과 완전히 동일한 d797d5c1b7074e5504f681a59b1cf33a1068a5f2c0ba5197f749a326289799cb. fixture-undo-verification.json,fixture-Gemini-before-undo.json,fixture-Gemini-restored.json 참조. 검증 후 쓰기이며64비트 식별자를 JSON 재직렬화하지 않았다.
2. 공식 b25099f 백업의 모드 폴더만 복원했다.21:18:50 기준88파일의 상대 경로와 전체 SHA256 차이0. manifest SHA는 E1E4D51294F8466C685D08D1A4222AA7484945688DDEB25E38C2E85B854D4F9B. final-mod-restore-check.json 참조. 후보 코드를 공식 병합/설치한 것이 아니다.
3. 우리 시험 스위치 testcmds.on/decisionlog.on만 제거. 실제 usage 누적과 사용자 세계 aiui1106은 유지했고 기존 슬롯1~8도 삭제하지 않았다. 최초 AIPlayers/world/registry 전체 restore는 실행하지 않았다.
4. 최종 저장 대비 localsteam251파일/remote49파일 및 registry가 전부 동일하다. AIPlayers 차이는 Gemini fixture 역복구와 두 시험 스위치뿐이다. final-post-cleanup-diff.txt의4개 차이 그룹은 의도된 모드/fixture/스위치이며 실패가 아니다. 사용자 직접 진행과4K 설정은 최종 저장 그대로다.
5. CoreKeeper/Unity 프로세스0,우리 bridge PID41456 종료,8331 listen 없음.21:18:50 HEAD에 게임/Unity 슬롯을 해제한다고 통보했다. 이후 게임/Unity 실행은 하지 않았다.

주요 미시험 한계: 실제 OS IME 조합,다중 마우스 버튼 동시 유지,공격 피해 정량,다른 화면 해상도에서의 추가 실게임. 이 범위를 통과로 과장하지 않는다. 대표 화면은34(장비),38(작은 실제 AI 채팅),43(스킬),45/46(퇴장·복귀)이다.
