# 구현 설계

## 흡수 원천
- Smaller Korean Font 배포 설명에서 Galmuri9 교체 접근을 확인했다. 모드 코드 라이선스는 확인되지 않아 복사하지 않는다.
- 원저자 quiple/galmuri 커밋 71e1cacf1437a11220307120e63e30bc275312d4의 Galmuri9.bdf와 OFL 원문을 사용한다. 작은 글꼴을 현재 모드의 script-only 배포에 포함하기 위해 필요한 글리프를 결정적으로 압축한 C# 데이터로 생성한다. 변환 폰트의 이름은 AI Compact Korean으로 구분하고 OFL/원저자/출처를 생성 데이터와 배포 소스에 보존한다.
- CombatMeter의 MIT OnGUI UI 선례는 구조 참고다. 전투 계측·동기화 등은 포함하지 않는다. 실제 패널은 현재 AI 데이터와 입력 경계에 맞춰 직접 작성한다.

## 기존 채팅 보존
PugText.SetPugFont의 폰트 인수만, 현재 ChatWindow의 출력/입력 부모 아래 인스턴스에 한정해 바꾼다. PugFont의 픽셀 텍스처·글리프 치수를 작은 글꼴로 제공하므로 Render, 줄바꿈과 dimensions.height, AddMessageHeight, fade, 네트워크, 입력 모두 기존 경로다. TextManager의 전역 폰트 자산은 바꾸지 않는다. 버튼 아이콘 글꼴은 유지한다. 한글/ASCII/한글 자모를 제공하고 기타 문자는 게임의 기존 글리프 폴백을 보존한다.

## AI 패널
- 호스트 게임에서만 표시. 기본 위치 우측 하단, 작고 어두운 AI 선택 막대. 선택 시 그 위로 장비/스킬 정보 패널을 펼친다. 닫기 버튼으로 막대만 유지한다.
- 별도 GameObject의 MonoBehaviour.OnGUI. 폰트는 채팅과 같은 동봉 비트맵으로 그려 OS 폰트/외부 모드 의존을 만들지 않는다.
- 선택한 AI의 ServerPlayerOf/IsJoined와 ECS SkillBuffer, EquippedObjectCD, ActiveEquipmentPresetCD+EquipmentPresetsBuffer+ContainedObjectsBuffer를 읽기 전용으로 조회한다. 0.5초 캐시, 항목 선택 즉시 갱신. 번역 이름은 기존 AIKnowledge의 게임 번역 경로 사용.
- 기본 메시지/LLM/봇 행동은 수정하지 않는다. 부재·퇴장 상태는 명시하고 이전 캐릭터 정보를 그대로 표시하지 않는다.

## 입력과 수명
- GUI.Event.Use만으로 끝내지 않는다. 로컬 호스트 PlayerInput의 버튼 질의 3종과 축 질의에서 UI 클릭/드래그 캡처 동안만 게임 입력을 막는다. 봇/타 플레이어 모듈은 건드리지 않는다.
- 클릭 시작 위치를 게임 입력 질의 시점에도 검사하여 프레임 순서와 무관하게 첫 공격을 막고, UI 밖으로 드래그해도 버튼을 놓은 프레임까지 캡처한다. 마우스가 단지 패널 위에 있는 동안에는 키보드 게임 입력을 막지 않는다.
- 메뉴/인벤토리/지도 열림 동안 패널은 숨겨 기존 UI를 우선한다. 월드 종료·모드 shutdown 때 캐시·UI·폰트 자원을 해제하고 기존 채팅 글꼴로 재렌더한다.

## 변경 책임
- AICompactFont / 생성 데이터 / Tools/ui-font: 동봉 폰트와 PugText 지역 적용, IMGUI 글자 그리기.
- AIInfoPanel / AIInfoSnapshot: 표시·조작, 읽기 전용 서버 스냅샷.
- Core/AIUiPointerCapture: 순수한 캡처 수명 판정, 프레임 단위 시험.
- AIPlayerSpikeMod: UI 생명주기 연결과 Harmony 패치 설치만.

## 검증
오프라인 font 재생성/글리프·코드포인트·클리핑 검증, 캡처 시작/밖 드래그/놓기/월드종료 시험, 기존 gates, Unity compile-only. 실게임은 별도 담당이 실제 작은 채팅 밀도와 입력/장비/스킬 조회를 검증한다. 공통 설치·게임은 이 구현자가 실행하지 않는다.
