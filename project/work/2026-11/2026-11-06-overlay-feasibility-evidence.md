# 채팅 오버레이 가능성 — 코드 조사 근거

2026-11-06. 대상 `C:/Dev/CoreKeeperModSDK`, `ai-character` HEAD `902721e`. 조사만 수행했으며 제품 코드·설치·게임 실행 상태는 변경하지 않았다. 작업 트리에 기존 SDK 설정 변경 및 미추적 파일이 있었고 그대로 두었다. 빌드·실게임 검증을 하지 않았으므로 아래 판단은 기술 가능성 평가다.

## 결론

게임 모드 안에서 화면 해상도로 별도 채팅 UI를 그리는 것은 현실적인 경로다. 기존 픽셀 글꼴을 축소하는 방식과 달리 작은 한글을 별도 폰트로 그릴 수 있다. **출력 영역을 대체하고 기존 입력·전송을 보존하는 방식은 중간 난도**, 입력·IME·Tab까지 전부 바꾸는 방식은 그보다 큰 작업이다. AI별 스킬·장비 조회 버튼은 같은 UI에 자연스럽게 추가 가능하다. 호스트 PC에는 실제 데이터가 이미 있다.

관찰: 게임 내부에 일반 Unity IMGUI 화면 렌더링 선례가 있고, SDK 및 설치 게임에 UGUI·텍스트 렌더링 모듈도 있다. 추정: 화면 해상도의 루트 Canvas가 장기 UI(패널·버튼·스크롤)에 적합하다. 미검증: 이 게임의 실제 렌더 순서·스케일·한글 폰트·입력 차단을 결합한 작은 프로토타입의 성공.

## 1. 화면 해상도 렌더 경로

- `C:/Dev/_ref/corekeeper-decomp/Pug.Other/BenchmarkRunner.cs:557` — 게임 자체 `OnGUI()`가 `GUIStyle(GUI.skin.textArea)` 및 `fontSize = 12`로 `GUI.TextArea(new Rect(10,50,600,...))`를 그린다. PugText 및 게임 픽셀 글꼴을 거치지 않는 직접적인 선례다. 현재 일반 플레이에서 활성화됐다는 증거는 아니다.
- `C:/Dev/CoreKeeperModSDK/AIPlayerSpike.csproj:241`, `:337`, `:341`, `:345`, `:1869` — IMGUI, TextRendering, UIModule, UIElementsModule 및 `UnityEngine.UI` 참조가 있다. `UnityEngine.UI.dll` 참조는 `Library/ScriptAssemblies`다.
- 설치 게임 `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/Managed/`에서 `UnityEngine.UI.dll`, `UnityEngine.UIModule.dll`, `UnityEngine.IMGUIModule.dll`, `UnityEngine.TextRenderingModule.dll`, `UnityEngine.UIElementsModule.dll` 존재를 확인했다.
- `Packages/manifest.json`은 imgui/ui/uielements 모듈을 선언하지만 ugui를 직접 선언하지 않는다. 따라서 위 어셈블리 존재와 참조는 확인됐지만 새 UGUI 프리팹을 SDK에서 빌드·패키징해 게임에서 로드하는 전체 흐름은 미검증이다.
- Unity 공식 [Canvas 문서](https://docs.unity3d.com/2021.3/Documentation/Manual/class-Canvas.html): Screen Space Overlay는 카메라 없이 화면에 직접 UI를 렌더하며 최상위 루트에 있어야 한다. 게임의 PugText 부모·픽셀 UI 카메라에 넣지 않는 것이 핵심이다.
- 한글은 별도 한글 지원 폰트가 필요하다. Windows 전용 첫 확인에는 [Font.CreateDynamicFontFromOSFont](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/font/createdynamicfontfromosfont) 경로가 있다. 배포용 폰트 포함/폴백은 별도 결정 및 런타임 확인이 필요하다. 글자 축소율만 바꾸는 이전 실패 코드를 재사용하는 해결이 아니다.

판단: 첫 기술 확인은 IMGUI도 충분하다. 게임 내 선례가 있고 추가 UI 에셋 없이 작은 한글·버튼·입력 차단 여부를 볼 수 있다. 사용자용 UI는 루트 Canvas/UGUI가 더 적합한 후보지만, 아직 확정 구현 선택으로 삼지는 않는다. 외부 최상위 Windows 창은 화면 모드·포커스·별도 프로세스와 데이터 전달 문제가 추가되므로 현재 모드 내 경로보다 우선할 근거가 없다.

## 2. 채팅 연결과 출력만 교체하는 경계

- `Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.Chat.cs:249` — AI `Reply`는 표시 정책·중복 제한을 거쳐 큐에 넣는다. `:263`의 `FlushReplies`는 `ChatWindow.RenderText`를 호출한다. 기존 AI 발화 규칙 및 80자 정책을 바꾸지 않아도 표시만 교체할 연결점이 있다.
- `C:/Dev/_ref/corekeeper-decomp/Pug.Other/ChatWindow.cs:217` — `RenderText`가 수신 및 AI 문장을 PugText로 그린다. 이곳만 가로채면 보낸 자기 말과 시스템 알림은 빠진다.
- 같은 파일 `:361` `Deactivate(commit)` — 보내는 자기 말은 필터링 뒤 `SendChatMessage`를 호출하고 `MessageTextType.Sent`를 직접 생성한다. `:425` `AddInfoText`는 시스템 알림을 별도로 생성한다. 모든 채팅 출력의 포착은 `:313` `AddPugText(type,text)` 같은 공통 경계가 유력하다. 텍스트 포착은 번역·서식 처리가 끝난 표시 문자열과 색/유형을 함께 다루어야 한다.
- 같은 파일 `:62`~`:70`, 기존 실게임 기록: 입력 칸·프롬프트 등도 `messageTextParent`에 연결돼 있다. 부모 통째 숨김은 입력까지 숨기므로 출력 메시지 객체만 숨기는 처리가 필요하다. 단순히 불투명 사각형으로 기존 글을 가리는 방식보다 출력만 교체하는 경계가 낫다.
- `AIChatHint.cs:21`, `:31`, `:92`: 채팅 활성 상태를 읽고 기존 입력 문자열로 멘션 힌트를 만들며, `ChatWindow.Deactivate`를 패치해 Tab 자동완성한다. `:120`부터 한글 조합 상태 처리도 있다. 입력을 유지하면 이 기능을 재작성할 필요가 줄어든다.

판단: 첫 완성 범위로 출력 교체 + 작은 글자 + 원래 7초 흐려짐 + 기존 입력/Tab 유지가 합리적이다. 스크롤·필터·이름 버튼은 같은 UI 위의 확장이다. 출력만 바꿔도 자기 말/AI 말/타인 말/시스템 알림을 빠짐없이 보여야 하므로 단순한 폰트 옵션 한 개 변경은 아니다. 기존 `ClearAll`, 월드 전환, 메뉴에서 숨김도 함께 맞춘다.

## 3. AI 스킬·장비 데이터

- `AIPlayerSpikeMod.cs:190`~`:191`: AI별 `IsJoined(a)`, `ServerPlayerOf(a)`가 이미 존재한다. `:282`의 `InHostedGame`은 `Manager.ecs.ServerWorld`와 로컬 클라이언트·플레이어가 있는 호스트 게임을 요구한다.
- `AICharacterStore.cs:298`~`:357`: 서버의 해당 AI 엔티티에서 `ContainedObjectsBuffer`(가방/장비 물건)와 `SkillBuffer`(스킬 값)를 읽어 캐릭터 파일에 저장한다. 실시간 UI는 저장 JSON 대신 해당 서버 상태를 읽는 편이 맞다. 파일은 저장 시점 자료다.
- `AIInspectSkills.cs:65`~`:89`: 이미 가방 물건과 `EquippedObjectCD`의 손에 든 물건·슬롯을 읽는다. `AIObservation.cs:50`에도 손 장비 읽기 코드가 있다.
- `C:/Dev/_ref/corekeeper-decomp/Pug.Other/SkillExtensions.cs:142`: `GetLevelFromSkill(skillID,skillValue)`로 원시 스킬 값을 표시 레벨로 변환한다. 스킬 경험치를 곧바로 레벨처럼 표시하면 안 된다.
- `C:/Dev/_ref/corekeeper-decomp/Pug.Other/EquipmentHandler.cs:39`~`:74`: 현재 장비 프리셋은 `ActiveEquipmentPresetCD`와 `EquipmentPresetsBuffer`로 고른다. 그 안 `EquipmentCD`가 머리/가슴/바지/목걸이/반지/보조손/가방/랜턴 슬롯을 가리킨다. 원시 장비 슬롯을 고정 숫자로 추정할 필요가 없다.
- `AICharacterStore.cs:298` 주석은 특성(talents)은 파일에서 유지한다고 명시한다. 현재 자동 특성 배분 동작이나 특성 트리의 정확한 실시간 권위 경로를 이 조사에서 확인하지 않았다. 채광/달리기 등 스킬 레벨 조회와 특성 트리·포인트 선택 기능은 구분해야 한다.

판단: 호스트 화면에서 AI 선택 → 스킬 레벨·손 장비·착용 장비/가방 조회는 데이터 연결 난도가 낮거나 중간이다. 읽기 전용 버튼 추가는 봇 사고방식이나 행동 루프를 바꿀 필요가 없다. 아이템 한국어 이름·아이콘·수치 설명의 완성도는 추가 UI 작업이다. **다른 접속자의 화면까지 지원하는 것은 별도 범위**다. 이 모드는 호스트 서버 상태에 의존하며 AI 답도 로컬 `RenderText`로 보낸다. 원격 사용자에게 전체 AI 인벤토리/스킬이 이미 전달된다고 단정할 수 없다. 원격 지원 시 해당 데이터 동기화와 UI 설치 범위를 먼저 정해야 한다.

## 4. 실제 어려운 부분과 확인 기준

- 입력 충돌: `SendClientInputSystem.cs:371`의 `PlayerInputBlocked()`는 게임 메뉴·인벤토리·UIInputAction을 본다. Unity EventSystem의 새 버튼에 마우스가 올라갔다고 자동으로 차단되는 경로는 확인되지 않는다. 버튼을 눌렀을 때 동시에 공격/채광하지 않아야 한다.
- `InputManager.cs:678`~`:685`에는 DisableInput/EnableInput이 있지만 게임 채팅도 이를 사용한다. 새 패널이 닫힐 때 채팅/메뉴가 아직 열려 있으면 무조건 EnableInput을 호출하는 방식은 잘못이다. 입력 소유권을 연결해야 한다.
- 화면 크기: Canvas를 화면 기준으로 그리더라도 창 크기/해상도 변경 시 위치·폭·줄바꿈·클릭 좌표를 함께 확인해야 한다. 기존 저해상도 UI 좌표와 새 UI 좌표가 다르다.
- 완전 입력 교체: 한글 IME 조합, Enter/Esc/Tab, 멘션 힌트, 전송 정책 연결까지 생긴다. 출력만 교체보다 명확히 큰 작업이다.

대표 확인 흐름: 실제 AI 발화 수신 → 작은 한글로 선명한 출력 → 기존 입력으로 @멘션/Tab/Enter → AI 답 표시 → AI 버튼 클릭 → 실제 스킬/착용 물건 표시 → 버튼 클릭 동안 공격 안 나감 → 패널 닫은 뒤 정상 조작 복귀. 이 흐름과 창 크기 변경을 실게임에서 확인해야 완료라고 할 수 있다. 이번 조사는 이 실행을 하지 않았다.
