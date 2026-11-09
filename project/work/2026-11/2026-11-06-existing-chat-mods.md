# 공개 채팅·폰트·오버레이 구현 조사

2026-11-06. 사용자 요청: GitHub와 공개 모드에서 이미 구현된 사례를 찾아 기존 것을 활용할 수 있는지 판단. 설치·제품 변경은 하지 않는다.

## 확인한 후보

1. Smaller Korean Font (urakamza)
   - 공개 모드: https://mod.io/g/corekeeper/m/smaller-korean-font
   - mod.io 공식 목록의 기능 근거: https://mod.io/g/corekeeper/m?tags=Language
   - 기본 한글 폰트를 작은 픽셀 폰트 Galmuri9로 교체한다고 설명한다. 1.3.0·1.2.1.5 태그, Client, Script (Elevated Access). 폰트는 SIL OFL 1.1로 표시된다.
   - 직접 상세 페이지는 JS 요구로 본문 수집되지 않았고, 공식 모드 목록에 있는 작성자 설명을 확인했다. 이번 조사에서 GitHub 소스는 찾지 못했다. 사용자 게임·현재 AI 모드와의 호환 및 실제 채팅 축소 정도는 미시험이다.
   - 폰트 라이선스와 모드 코드의 재사용 허용은 구분한다. 폰트 라이선스만으로 모드 코드 재사용이 허용된다고 단정하지 않는다.

2. BetterChat (limoka8)
   - https://mod.io/g/corekeeper/m/betterchat
   - 작성자 기능 설명은 채팅 폭 확대, 배경 추가, 체력·허기 등 UI 숨김이다. 배포 버전 1.0.2, 오래된 1.0 계열 호환 태그. 최신 게임과의 호환은 미시험이다.
   - 글자 크기를 줄인다는 제3자 소개가 있었으나 작성자 설명에서는 확인되지 않아 채택하지 않는다. 완전 교체형 채팅·AI 정보 버튼을 제공한다고 소개하지 않는다. 이번 GitHub 검색에서 해당 모드 소스를 찾지 못했다.

3. CoreKeeperCombatMeter (Shiroko7)
   - https://github.com/Shiroko7/CoreKeeperCombatMeter
   - 실제 소스에 작은 글자, 드래그 창, 탭·버튼이 있다. MIT 라이선스. 새 AI 정보 패널 UI의 참고 후보다.
   - README가 컴파일만 확인했고 실게임 시험은 아직이라고 명시한다. 실행 검증된 완성 모드로 취급하지 않는다. 채팅 교체 모드가 아니다.
   - 상세 코드 조사: [공개 오버레이 소스 근거](2026-11-06-public-overlay-sources.md)

4. CK Hud Reworked
   - 배포 설명: https://mod.io/g/corekeeper/m/ck-hud-reworked
   - 소스: https://github.com/Subbok/CKHud
   - F1 토글, 화면 상태 표시, 줄 간격·줄 수·위치 설정. Core Keeper 1.2.0.6 / CoreLib 4.0.0+ 요구를 표시한다. DPS/MaxDPS 기능은 API 변경으로 비활성이라고 명시한다.
   - HUD 구현 선례다. 현재 AI 스킬·장비 패널이 포함돼 있다는 근거는 없다.

5. germanoeich/CoreKeeperMods DebugMod
   - https://github.com/germanoeich/CoreKeeperMods
   - 일반 IMGUI로 작은 FPS 텍스트를 표시하는 소스 선례. 상세 조건·라이선스 확인 범위는 위 공개 오버레이 소스 근거 파일에 있다.

## 판단 갱신
- 앞선 기존 글꼴 축소 실패는 유효한 관찰이다. 그러나 게임 내부에서 작은 한글을 얻는 모든 방법이 막혔다고 일반화해서는 안 된다. 더 작게 설계된 한글 폰트로 교체하는 공개 모드가 확인됐다.
- 글자 크기 문제만 해결하려면 Smaller Korean Font의 실제 채팅 표시를 먼저 평가하는 것이 가장 직접적이다.
- 채팅 + AI별 장비·스킬 조회까지 한 창에 묶은 기성 Core Keeper 모드는 이번 검색에서 찾지 못했다. 존재하지 않는다는 단정은 아니다.
- 통합 UI를 만들 경우 공개 오버레이 소스를 참고할 수 있다. 현재 프로젝트 연결·한글·게임 입력 충돌·실제 호환은 별도 검증이 필요하다.
