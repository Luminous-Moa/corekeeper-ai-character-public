# 채팅 화면 표시층 구현 후보

최신 고정 커밋 `11eb461ae122f404acec94563c086b09e002987c` (초기구현8578950 + 독립검수P2수정), `C:/Dev/CoreKeeperModSDK-ai-ui`, branch ai-info-ui-20261106. 제품 파일은 커밋 완료. 장비 진단만 먼저 시험할 별도 snapshot은 `C:/Dev/CoreKeeperModSDK-ai-ui-diagnostic`의134b1c3이며 이 채팅 구현이 들어 있지 않다.

## 크기 기준 보정

실제 게임은3840×2160이고2560×1440 캡처는2/3축소본이라는 테스터 확인을 반영했다. 이전12px 로그는Font8B이므로 한글17%축소로 단정하지 않는다. 초기 목표는 **현재 동봉10px 채팅이 차지하는 실화면 높이의 약절반**이며, 비트맵 한 점을최소1실화면픽셀로 보존하는 가장 가까운 정수 배율이다. 예를들어 현재native66px(축소캡처44px)라면30px(캡처20px)을 선택한다. 이는 순수계산 예시이며 아직 새 설치본에서 관측한 크기가 아니다.

## 동작 경계

- 기본 ChatWindow/PugText의 문자열처리·번역·색태그·아이콘·줄바꿈·메시지큐·AddMessageHeight·입력/IME·Tab·fade는 그대로 실행한다. AI 메시지 전송이나 모델 호출 경로를 추가하지 않는다.
- UI 카메라의 normalized viewport를 실제 Screen.width/height에 대응한다. 내부 render target의 pixelHeight를 실제 화면높이로 오해하지 않는다. `CHAT-SCREEN metrics`에실제해상도·native점크기·이전compact높이·새목표px·ppu·viewport를남긴다.
- `AICompactFont`의 PugFont ppu와생성sprite치수를함께바꾸므로 기존 PugText.dimensions/메시지높이/입력스크롤이 작은크기를사용한다. 폰트비트맵 자체는그대로다.
- `AIChatScreenLayer`는기존 glyph SpriteRenderer의 sprite/위치/color/alpha를수동문자열재파싱없이그린다. native glyph만 forceRenderingOff로감추며 기존effects는계속원본색/alpha를갱신한다. UI panel과별개인수동표시층이며입력을먹지않는다.
- PugText.Clear/OnDisable **이전**에저장한renderflag를복원해global glyphpool 재사용에누출되지않게한다. hosted world종료/모드shutdown/표시실패도복원한다.
- 기존SpriteMaskinteraction/특수overrideMaterial/packed sprite/동적텍스트같은미지원경로는독자적으로불완전복제하지않고원래게임표시를유지한다. 그런경로에서는새축소가적용되지않을수있다. 과거대표chat/input조사는maskNone이었다. 실제현재상태는실게임확인이필요하다.
- 화면크기가바뀌면새ppu로다시렌더하되기존효과진행을리셋하지않는다. 실제메시지는기존높이순서를유지하며새dimensions로간격을재계산하고기존AdjustInputFieldPosition으로입력스크롤을맞춘다. 메시지/입력문자열을수정하지않는다.
- 독립검수P2에따라 ChatWindow가매LateUpdate설정하는root.localScale(loading/hide/fade의0..1)을폰트측정에서제외했다. 완료목표1을기준으로root.parent의안정배율을사용한다. Screen뿐아니라camera교체/viewport/ortho/부모배율도감지하며0.25초안정후재계산한다. ppu가같으면sprite재생성은없고,단순loadingfade때11369sprite를매프레임재생성하지않는다.

## 확인된 것과 남은 것

오프라인 core1135와게임API참조컴파일경고0/오류0통과. 새10검사는실화면projection/부분viewport/좌상단GUI변환·반크기정수픽셀·최소1px·잘못된측정fallback을검사한다. 근거는 `evidence/core-tests-screen-layer.txt`, `compile-check-screen-layer.txt`.

11eb461 수정뒤에는CHAT-SCREEN12검사(안정부모기하와동일해상도viewport/ortho변경2검사추가),게임API컴파일경고0/오류0을재확인했다. `evidence/chat-screen-stable-metrics.txt`. 독립 검수자는 재검수에서 P2 해소와 동일ppu 재생성0, 독립 컴파일0/0·core1137·diff 검사 통과를 확인했다. 실제 화면 합격과는 구별한다.

아직 이후보의Unity검사·실게임은실행하지않았다. 특히 실제viewport/앵커정렬, 한글획과실제높이, 입력과긴문장/IME/Tab, 다양한시스템색·아이콘/외곽선,7초fade,메뉴/인벤토리등과의가림,창크기변경,퇴장/재입장/글리프풀복원,프레임비용을확인해야한다. API컴파일·순수계산통과를이화면품질의증거로승격하지않는다. 독립검수에고정커밋을전달했다.

## 장비진단 분리

134b1c3진단판은server playerGuid와동일한clientghost를찾아(서버Entity번호재사용안함) 양쪽world/entity/currentEquipment/activepreset/preset/vanity슬롯→objectID+amount,client실제렌더핸들러의머리/가슴/다리항목,activeCustomization armor주소를읽는다. 패널선택/값변경시에만최대2초1회기록한다. client진단오류는실제정보표시를중단시키지않는다. 착용표시를가방보유또는외형으로추정대체하지않았다. 원인과UI정책판단은테스터의동일월드실제데이터를기다린다.
