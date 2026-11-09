# 독립 코드 검수

2026-11-06. 검수자는 제품 코드를 수정하지 않았고 게임·Unity·설치를 실행하지 않았다. 지정된 이 보고서만 작성했다. 세션 load/branch/save 도구는 호출하지 않았다.

**후속 실게임 blocker:** 사용자가 Gemini의 실제 착용과 패널 표시 불일치를 보고했다. 아래 ‘합쳐도 됨’은 당시 정적 코드/delta 검수 기록이며 이 미해결 현상을 통과시킨 판정이 아니다. 현재 장비 기능 완료 및 최종 병합 판단은 보류한다. [독립 조사 06-equipment-bug.md](06-equipment-bug.md)에 실제 서버 저장/제작 로그, 원게임 장비 경로, raw ECS 증거 부족과 다음 검증 조건을 기록했다. 원인은 아직 단정하지 않았다.

## 대상과 판정

- 작업 트리: `C:/Dev/CoreKeeperModSDK-ai-ui`, `ai-info-ui-20261106`.
- 현재 기준: 승인된 main `b25099f`. 초기 검수 기준은 `902721ec505df42d74d0b2d245ed34a049faadad`였다.
- 최종 검수 후보: `25db8ede5fa429791b01e69b51f4dbbd980679de`. rebase 후보 `2762495663a6cfa26b7d966ad6676b53f880c73e`의 실게임 가독성 문제를 패널 정수 배율로 보완한 변경이다. rebase 전 검수 후보는 `ecc8a88c1f6cacd4879225ec1967a74149f4c4b9`였다.
- 먼저 `3bb470dd6b391f9035c6d1580ffe2b8b53f4c4da` 전체 변경을 검수한 뒤, `ecc8a88`의 추가 3파일(AIInfoPanel, AIUiPointerCapture, UiPointerTests)을 대조했다. 이후 `2762495`로의 rebase는 아래 동등성/충돌 보존 검사로 별도 확인했다. SDK 환경 설정 변경은 제품 검수 대상에서 제외했다.
- **코드 검수 판정: 합쳐도 됨. 현재 후보에서 수정을 요구할 중요한 결함은 발견하지 못했다.** 이는 병합 실행 승인이나 기능 완료 판정이 아니다. rebase 전 `ecc8a88`의 Unity CompileCheck 성공 로그는 후속 확인됐으며, `2762495`의 실제 화면에서 작은 패널 본문 문제가 관측됐다. 최신 `25db8ed`의 설치 빌드 및 개선된 화면/클릭 흐름 재확인이 남아 있다. 기존 최종 병합·push 승인 조건도 유지된다.

검수 기준은 `00-agreement.md`, `01-design.md`, `02-implementation.md`, `03-graph-handoff.md`다. 게임 사실은 기존 프로젝트 소스와 `C:/Dev/_ref/corekeeper-decomp`의 PugFont/PugText/ChatWindow/PlayerInput/SendClientInputSystem/SkillID/SkillExtensions/EquipmentHandler를 대조했다. `Tools/knowledge/README.md`의 게임 데이터 원천과 기존 세션 참조를 확인했으며 .head 인덱스를 제품 지식 원천으로 사용하지 않았다.

## 선행 발견과 해소

1. **P2, UI 밖에서 시작한 드래그를 가로챔 — 해소.** 최초 코어는 유지 상태와 현재 inside만으로 캡처했다. 실제 C#을 Add-Type으로 실행하여 밖에서 누름→패널 진입에서 false→true를 재현했다. 최종 구현은 버튼별 시작 비트만 소유하므로 기존 월드 드래그의 진입은 캡처하지 않는다.
2. **P2, 다중 버튼의 새 UI 클릭 누락 — 해소.** 첫 수정은 버튼 유지 상태를 OR한 bool의 상승 에지만 사용했다. 월드 우클릭 유지→패널 안 새 좌클릭에서 캡처가 false인 것을 재현했다. 최종 구현은 버튼별 비트마스크로 새 좌클릭을 잡으며, 소유한 버튼의 해제 프레임까지 차단하고 기존 월드 버튼은 다음 프레임에 복귀한다.
3. **월드 종료 수명 설계 불일치 — 해소.** 선행 코드의 !hosted 경로는 선택·캡처만 초기화했다. 최종 `AIInfoPanel.Frame`은 hosted 상태를 내리고 UI GameObject를 파괴하며 정적 참조를 제거한다. 이어 `AICompactFont.Shutdown`이 후킹을 먼저 비활성화하고 기존 텍스트를 재렌더한 뒤 글리프 Sprite·PugFont·Texture·사전을 해제한다. 새 월드에는 새 패널/스냅샷을 만든다. 모드 shutdown에서는 Harmony 제거 후 폰트 원복도 수행한다.

작성자가 추가한 짧은 클릭 보완도 확인했다. `GetMouseButton`이 이미 0이어도 같은 프레임의 `GetMouseButtonDown` 비트를 캡처에 넘기므로 누름/놓음이 폴링 사이에 끝난 클릭 프레임을 막고 다음 프레임에 해제한다.

## 기능·경계 확인

| 항목 | 코드 검수 결과와 근거 |
|---|---|
| 기본 채팅 보존 | `AICompactFont.SetPugFontPrefix`는 현재 채팅 부모 아래 텍스트, 입력/프롬프트, 기존 AIChatHint 소유 객체만 바꾼다. TextManager 전역 자산과 버튼 글꼴은 변경하지 않는다. PugText Render→SetFont→PugFont 측정/줄바꿈과 ChatWindow AddMessageHeight 경로가 유지된다. 메시지 전송·기존 7초 fade·멘션/Tab 동작 구현은 변경하지 않았다. |
| 한글·글리프 | 11,369개 고유 글리프, 10px 셀. 현대 완성형 한글 11,172개 전체, ASCII 95개, 호환자모 94개 포함을 독립 payload 해제로 확인했다. 조합형 자모 U+1100–11FF는 원본에 없어 0개다. 채팅의 기타 문자는 원게임 GetGlyphData 폴백을 유지한다. 패널의 미지원 문자는 `?`로 표시한다. |
| 비트맵/라이선스 | 생성기의 각 비트 범위와 재생성 일치 검사 통과. InitCodePoints의 하단 행 제외 및 외곽 여백 방식과 atlas 좌표가 일치한다. 생성 소스에 원저자, OFL 전문, 원본 커밋/해시, 변환 이름 AI Compact Korean이 보존된다. |
| 배포 포함 | 모드 설정은 `Assets/AIPlayerSpike`의 script-only 배포이며 외부 의존 목록은 비어 있다. SDK `Packages/dev.pugstorm.mod/SDK/Editor/ModBuilder.cs`의 BuildScripts는 비Editor `.cs`를 원문 `File.Copy`하고 manifest에 등록한다. 따라서 AICompactFontData.cs의 payload와 고지 주석은 함께 배포되는 구조다. 외부 폰트 설치나 외부 모드 DLL 없이 동작하도록 구현했다. 실제 산출물 확인은 설치 담당의 남은 확인 항목이다. |
| 읽기 전용 실시간 정보 | AIInfoSnapshot.Read는 선택된 멤버의 IsJoined/ServerPlayerOf, 서버 EntityManager의 읽기 전용 buffer와 component 조회를 사용한다. SkillID 12개의 순서와 GetLevelFromSkill 적용을 원게임과 대조했다. 활성 프리셋의 장비 슬롯 및 손 슬롯 조회도 기존 게임 데이터 경로와 일치한다. 장비 변경·특성 배분·파일 저장·네트워크 전송·추가 모델 호출이 없다. |
| 선택/부재 상태 | 작은 선택부→AI 선택→스킬/착용 장비 탭→X 또는 같은 AI 재클릭으로 닫기 흐름이다. 선택 시 이전 스냅샷을 지우고 다음 Frame에서 즉시 읽으며, 이후 0.5초 주기로 교체한다. 미접속/없어진 엔티티는 빈 새 스냅샷을 반환한다. |
| 로컬 입력 차단 | PlayerInput 버튼 질의 3종과 축 질의 4종의 prefix는 로컬 플레이어 inputModule 참조가 일치할 때만 캡처를 평가한다. 원게임 InputManager가 이 모듈을 PlayerInput으로 생성하고 SendClientInputSystem의 이동·공격·해제 명령이 해당 질의를 사용하는 것을 확인했다. 타 플레이어/AI 모듈이나 전역 EnableInput 상태는 변경하지 않는다. 입력 hook 하나라도 설치되지 않으면 패널을 만들지 않는다. |
| 기존 UI 우선 | 메뉴·인벤토리·장비·외형·지도 동안 패널이 숨고 캡처가 초기화된다. 단순 hover는 게임 입력을 차단하지 않는다. Unity IMGUI와 게임 입력의 실제 프레임 순서는 실게임 확인이 남는다. |

## 승인 main 위 rebase 동등성 확인

최신 후보 `2762495`는 UI 3개 커밋을 `902721e`에서 승인 main `b25099f` 위로 옮긴 결과다. 전체 기능을 재검수하거나 게임/Unity를 다시 실행하지 않고, 기존 검수 결과가 유지되는지 독립 확인했다.

- `git range-diff 902721e..ecc8a88 b25099f..2762495`: 첫 기능 커밋의 유일한 차이는 Program.cs 등록 위치에 기존 CHOICE가 추가된 주변 문맥이다. 폰트 개행 보존 및 짧은 클릭 보완의 뒤 2개 커밋은 `=`로 일치한다.
- 두 범위의 변경 파일 목록은 같은 19개다. `Tools/core-tests/Program.cs`를 제외한 **18개 파일은 old/new Git blob이 모두 완전히 동일**하다. 런타임 코드·생성 폰트·원본/고지·포인터 테스트는 바뀌지 않았다.
- 새 Program.cs의 UI 등록 1줄만 제거한 내용은 `b25099f:Tools/core-tests/Program.cs`와 정확히 같다. **CHOICE와 UI 등록은 각각 1회이며 둘 다 보존**됐다.
- `git diff --check b25099f..2762495`: 통과.
- 제공된 rebase 후 증거를 직접 읽어 확인: `evidence/core-tests-rebased.txt` **1,103개 통과**, `compile-check-rebased.txt` **경고 0/오류 0**, `font-check-rebased.txt` **재생성 검사 통과**. 이 실행들은 구현자가 수행했으며 검수자의 중복 실행으로 표시하지 않는다.
- `evidence/unity-compile-ecc8a88.log`에서 `[HeadlessSdk] CompileCheck: ok`를 확인했다. HEAD는 해당 실행 exit 0/CS 오류 0을 보고했다. `evidence/unity-gates.txt`에 남은 Unity 점유로 인한 미실행 FAIL은 선행 시도이며 이 후속 성공과 구별한다. Unity 증거의 SHA는 **ecc8a88**로 보존하고 최신 후보의 실행 증거라고 바꿔 쓰지 않는다.

따라서 rebase 후보에도 기존 코드 검수 판정을 유지했다. 해당 단계의 설치 빌드와 실게임 검증 대상은 `2762495`였으며, 아래 가독성 보완 뒤 대상은 `25db8ed`로 갱신됐다.

## 실게임 가독성 보완 delta 검수 — 25db8ed

HEAD가 전달한 `2762495`의 2560×1440 실게임 결과는 패널 본문 10px가 너무 작다는 판정이다. 검수자도 `evidence/live/07-gemini-skills-ready.png`를 직접 열어 게임 HUD와 비교해 작은 패널을 확인했다. 이 화면은 이전 후보의 문제 근거이며 개선된 결과의 증거로 사용하지 않는다.

`2762495..25db8ed` 변경은 AIInfoPanel, 순수 코어 AIUiLayout와 meta, UiLayoutTests 및 Program 등록의 **5파일**뿐이다. 채팅·동봉 폰트/데이터·스냅샷·AIUiPointerCapture 구현은 변경되지 않았다. 이번 검수는 이 delta에 한정한다.

- AIUiLayout은 화면 크기에서 정수 배율을 구하고, 선택 막대와 상세 패널 전체가 여백을 포함해 들어가는 배율로 제한한다. 720p 1배/10px, 1080p·1440p 2배/20px, 4K 3배/30px다. 360×800처럼 폭이 좁은 화면은 들어가는 1배로 낮춘다. 300×240처럼 최소 영역도 들어가지 않으면 표시·캡처를 비활성화한다.
- OnGUI는 `GUI.matrix = Scale(scale,scale,1)`로 패널·버튼·비트맵을 함께 확대한다. 글꼴 데이터 자체의 10px 셀과 point filtering은 유지되며 `AICompactFont.Draw(...,1,...)`를 추가로 확대하지 않으므로 배율이 중복 적용되지 않는다. 정수 논리 좌표와 정수 배율을 사용한다.
- SampleCapture는 같은 Layout.Scale로 `(mouse.x / scale, (Screen.height - mouse.y) / scale)`를 만든 뒤 같은 Bar/Panel rect에 검사한다. 그리기의 화면 좌상단 좌표에 대한 역변환이 일치한다. 기존 버튼별 down/held, 외부 드래그, 해제 프레임 보존과 로컬 플레이어 구분은 그대로다.
- Layout은 각 조회에서 현재 Screen.width/height로 계산하므로 해상도 변경 후 낡은 배율/좌표를 캐시하지 않는다. GUI.matrix와 GUI.depth는 try/finally로 이전 값에 복원해 다른 OnGUI 경로에 확대 상태를 남기지 않는다.
- 2560×1440에서 물리 화면 영역은 선택 막대 `(1952,1360,584,56)`, 상세 `(1952,860,584,492)`이며 오른쪽/아래 여백 24px, 두 영역 간격 8px다. 이는 코드 계산값으로, 실게임 클릭은 새 화면 관측으로 위치를 확인해야 한다.
- 검수자가 별도 `C:/Dev/_scratch/ai-ui-independent-review-25db8ed/` obj/bin에서 컴파일/실행했다. **레이아웃 22개와 기존 포인터 19개 통과**, 게임 DLL 참조 컴파일 **경고 0/오류 0**, `git diff --check 2762495..25db8ed` 통과. 640×480, 720p, 1080p, 1440p, 4K, 울트라와이드, 좁은 화면, 비정수 나눗셈 화면과 크기 축소를 검사한다. 제품 코드는 수정하지 않았고 게임·Unity·설치를 실행하지 않았다.

**Delta 판정: 합쳐도 됨. 추가 중요 결함은 발견하지 못했다.** 다만 이 검사는 좌표/배율 계산과 입력 상태 보존의 근거다. 새 20px 본문의 실제 가독성, Unity IMGUI 버튼 hit, 확대 영역의 HUD 간섭, 탭/닫기/외부 드래그/해상도 변경 실제 동작은 `25db8ed`의 새 실게임 증거로 확인해야 한다. 기존 작은 화면의 성공 동작을 확대 UI의 실게임 통과로 승격하지 않는다.

## 독립 실행과 제공 증거

- rebase 전 후보 `ecc8a88`의 .NET 게임 DLL 참조 컴파일: **경고 0, 오류 0**. 작업자와 충돌하지 않도록 `C:/Dev/_scratch/ai-ui-independent-review-ecc8a88/compile-obj/`와 `compile-bin/`을 사용했다.
- `ecc8a88`의 `AIUiPointerCapture.cs`와 `UiPointerTests.cs`를 PowerShell Add-Type으로 독립 실행: **19개 통과**. 시작/동일 프레임 반복/외부 드래그/해제/hover/숨김/다중버튼/짧은 클릭 포함. 제품 파일이나 작업자 시험 출력 경로에 쓰지 않았다. 두 파일은 rebase 후에도 동일하다.
- `node Tools/ui-font/build.mjs --check`: **통과**, 11,369 glyphs, 10px cell, gzip 64,742 bytes.
- 독립 payload 해제: 실제/예상 길이 **261,495바이트 일치**, 중복 코드포인트 없음, 각 행 비트가 advance 폭을 넘지 않음.
- `git diff --check 902721e..ecc8a88`: 통과.
- 작업자 제공 `evidence/offline-gates.txt`를 읽어 기존 비Unity 전체 관문 failed gates 0 및 core 1,078 통과를 확인했다. 이 수치는 16개 UI 검사가 포함된 이전 후보 실행 결과이며, 마지막 3개 검사를 추가한 전체 관문의 최신 실행 결과와 혼동하지 않는다. 검수자가 전체 관문을 중복 실행하지는 않았다.

## 완료 전에 남은 실행 관문

검수자가 Unity나 실제 게임을 실행하지는 않았다. 별도 담당의 `ecc8a88` Unity CompileCheck 성공은 위에 기록했다. 코드 검수·rebase 동등성·가독성 보완 delta 통과만으로 아래 최신 후보의 설치/실게임 항목을 통과한 것으로 기록하면 안 된다.

1. 최신 후보 `25db8ed`가 일치하는 설치 빌드/컴파일 결과와 설치 산출물의 AICompactFontData.cs payload/고지 포함 확인.
2. 실제 AI 한국어 발언·다중줄·시스템 알림의 가독성/줄간격/배치와 채팅 밖 기존 UI 폰트 유지. 입력 IME 조합·멘션·Tab·Enter 및 7초 흐려짐 확인.
3. AI 선택/탭/닫기와 실제 서버 스킬 레벨·현재 장비 대조, 부재/퇴장 시 정보 갱신, UI 조회로 추가 모델 호출이 생기지 않음 확인.
4. 첫 클릭·UI 밖 드래그/해제·월드에서 시작한 드래그 진입·우클릭 유지 중 새 좌클릭·짧은 클릭에서 공격/이동 누출 없음 및 이후 정상 조작 복귀 확인.
5. 메뉴/지도/인벤토리 숨김과 월드 종료·재입장, 해상도 변경 후 좌표·가독성·자원 재생성 확인.

실게임 결과는 `04-live-verification.md`와 연결하고, 최종 병합·원격 push 및 공식 지식그래프 적용은 기존 승인 절차를 따른다.

## 최신 채팅 표시층 검수

후보11eb461 독립 검수는 [07-chat-screen-review.md](07-chat-screen-review.md). 8578950의 초기 fade 배율 고정 P2는11eb461에서 수정되어 코드 기준 합쳐도 됨. 독립 core1137개 및 게임 API compile0/0 통과. 화면 크기/선명도·기본 채팅 실게임 검증은 별도다. 장비 raw 재평가는 [06-equipment-bug.md](06-equipment-bug.md)의 최신 절을 따른다.
