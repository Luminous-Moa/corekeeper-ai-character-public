# 공개 Core Keeper 오버레이 소스 조사

2026-11-06. GitHub 원본 README·C#·라이선스를 읽었다. 설치/실행/제품 코드 수정은 하지 않았다.

## 결론

공개 구현은 있다. 채팅 완제품은 아니지만 작은 글자·버튼·드래그 패널의 구체적인 구현 참고가 된다. 다만 실게임 검증이나 게임 공격 입력 차단까지 해결됐다는 근거는 확보하지 못했다.

## CoreKeeperCombatMeter

- [저장소/README](https://github.com/Shiroko7/CoreKeeperCombatMeter): 코어키퍼 협동 전투 통계 패널. Unity 6000.0.58f2 SDK에서 컴파일 성공, 실제 실행 게임은 미시험이라고 명시한다. 바로 설치해서 작동이 보장되는 완제품으로 보지 않는다.
- [실제 UI 소스](https://github.com/Shiroko7/CoreKeeperCombatMeter/blob/main/UI/CombatMeterOverlay.cs): MonoBehaviour·DontDestroyOnLoad·OnGUI·GUI.Window. 380×370 창, 글자 10~13px, 제목줄 드래그, Dealt/Received/Healing/History 탭, 닫기·리셋·기록 선택 버튼이 구현돼 있다. 채팅·한글 폰트 교체·장비 조회 기능은 아니다.
- 소스에서 확인한 버튼 처리는 GUI.Button, 드래그는 GUI.DragWindow다. 해당 파일에는 Core Keeper의 게임 행동 입력을 차단하는 처리가 보이지 않는다. GUI 이벤트 소비가 게임 공격 입력까지 막는다고 단정할 수 없다.
- [라이선스](https://github.com/Shiroko7/CoreKeeperCombatMeter/blob/main/LICENSE): MIT, 2026 shiro. 코드 재사용 시 저작권/허가문 보존 조건. 공식 SDK 내로 넣어 빌드하는 구성이고 별도 standalone 프로젝트가 아니다.
- GitHub API의 마지막 push 시각: 2026-09-29T19:32:05Z. 최신 사용자 게임 버전 호환은 이번에 검증하지 않았다.

## germanoeich/CoreKeeperMods DebugMod

- [저장소](https://github.com/germanoeich/CoreKeeperMods)
- [FPS 오버레이 소스](https://github.com/germanoeich/CoreKeeperMods/blob/main/DebugMod/Script/Debug/Overlay/DebugFpsOverlay.cs): MonoBehaviour·DontDestroyOnLoad·OnGUI로 화면 왼쪽 위에 380×356 반투명 패널, FPS/프레임 통계·그래프를 그린다. 기본 글꼴 14px, 범례 원 표시 12px. 고정 표시이며 이 파일에는 드래그·정보 버튼이 없다.
- 같은 소스에 게임 입력 차단은 없다. 표시 전용이므로 클릭형 AI 정보 패널 문제의 해결책은 아니다.
- 조사한 재귀 트리에 LICENSE 파일이 없고 GitHub API의 license 값도 null이다. 소스가 공개됐다는 이유만으로 복사·재배포 가능한 라이선스가 있다고 보지 않는다. 구조 참고와 코드 재사용 판단은 구분한다.
- GitHub API 마지막 push 시각: 2026-09-24T17:36:33Z. CoreKeeperCombatMeter README는 이 패턴을 proven으로 표현하지만, 독립적인 실게임 실행 증거를 이번 조사로 확인하지는 않았다.

## 이번 프로젝트에 대한 의미

- 이전 판단의 '기술 경로가 있다'를 실제 공개 C# 구현이 뒷받침한다.
- 작은 채팅 출력·AI 스킬/장비 버튼을 제작할 경우 CombatMeter의 표시/드래그/탭 구조를 참고할 수 있다.
- 채팅 수신 경로 연결, 한국어 글꼴, 버튼 클릭의 공격 유출 방지는 여전히 우리 게임에서 검증해야 한다.
