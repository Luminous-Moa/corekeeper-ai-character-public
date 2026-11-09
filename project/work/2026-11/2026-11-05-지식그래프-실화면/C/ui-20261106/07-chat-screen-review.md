# 채팅 화면 표시층 — 독립 코드 검수

검수 기준: 25db8ed 이후 후보8578950 및 수정후 고정 후보 `11eb461ae122f404acec94563c086b09e002987c`. 제품 코드 수정 없음. Unity/게임/설치/CU 실행 없음. 작업 트리의 SDK/패키지 등 다른 담당자 변경은 건드리지 않았다.

## 판정

**11eb461: 코드 검수 기준 합쳐도 됨.** 8578950에서 P2 1건을 발견해 수정 요청했고, 수정 delta와 독립 검사를 통과했다. 실제 44px 캡처 대비 약절반 크기·선명도와 입력·기호·풀 재사용은 실게임 확인이 남는다. 코드 통과와 사용자 체감 목표 달성을 구분한다.

## 발견 및 수정 확인 — 초기 fade 배율 고정 P2

8578950의 AIChatScreenLayer.FontPixelsPerUnit은 messageTextParent.lossyScale.y를 폰트 ppu에 포함하면서, 재측정 트리거는 Screen.width/height 변경뿐이었다. 원게임 ChatWindow.LateUpdate:466-470는 그 부모 배율을 매프레임 CalcGameplayUITargetScaleMultiplier로 갱신하고, UIManager.cs:633-658은 로딩/숨김 시0, 전환 fade 중0~1을 반환한다. 초기 Ensure가0에서 측정하면 ppu16 fallback이 고정되어 안정화 후 축소가 적용되지 않거나, 중간값에서 측정하면 목표크기가 달라질 수 있었다. HEAD와 구현자에게 즉시 전달했다.

11eb461은 root 자체의 애니메이션 배율을 제외하고 root.parent.lossyScale 및 정상 target1로 측정한다. 화면 크기뿐 아니라 camera instance/rect/orthographicSize/안정 parent scale 변경을 감지한다. 초기 root/camera가 없으면 pending을 유지하며, 조건이 준비되고0.25초 안정화 후 갱신한다. 같은 ppu에서는 RefreshScreenMetrics가 즉시 반환하므로 11,369개 Sprite를 다시 만들지 않는다. fade/hide/show 자체는 signature에서 제외하므로 매frame atlas sprite 재생성이 일어나지 않는다. 수정은 보고한 원인에 대응하며 코드상 해소됐다.

## 확인한 보존 경로

- 소유권은 해당 compact-font PugText의 glyph 목록으로 한정한다. 기존 forceRenderingOff를 저장하고 Clear/OnDisable prefix에서 원게임 pool 반환 전 복원한다. Release/Shutdown에서도 소유 렌더러 전체를 복원한다.
- 원게임 PugText.Clear는 glyphPool.Free 후 목록을 비우고 OnDisable은 freeResourcesOnDisable일 때 glyphsAwaitingFree로 반환한다. 두 실제 반환 경로에 prefix가 연결돼 있다.
- 기존 ChatWindow/PugText가 문자열·필터·줄바꿈·입력·메시지 풀·효과를 소유한다. 표시층은 Repaint에서 현재 glyph 위치/활성/색/알파를 읽는다. 색 태그도 실제 glyph.color를 쓰며 outline은 같은 방향 비트와 알파로 그린다.
- masked/overrideMaterial/nonpooled/dynamic text는 takeover하지 않는다. packed sprite나 glyph mask 발견 시 줄 전체를 native로 되돌리고 다시 Render해 부분 누락을 피한다. 버튼/언어 fallback sprite ppu와 새 font advance ppu 차이를 비율로 맞춘다.
- ViewportPoint → camera.rect → 실제 Screen dimensions 변환으로 저해상도 camera target pixelHeight를 화면 크기로 오용하지 않는다. 픽셀 경계 반올림과 point atlas를 사용한다. camera cullingMask·glyph/text 활성 상태도 따른다.
- world exit는 screen glyph 상태를 먼저 해제하고 info panel 경로가 compact font hook을 해제해 기존 폰트로 Render한 뒤 Sprite/Texture를 해제한다. mod Shutdown도 screen layer를 먼저 해제한다.
- 리사이즈 시 활성 메시지를 기존 y순으로 재배치하고 입력 위치는 원게임 AdjustInputFieldPosition으로 갱신한다. Enter/Tab/IME/커서 처리 메서드를 교체하지 않는다.
- 정상 frame은 기존 text/glyph 순회이며 새 전체 atlas/list를 만들지 않는다. 리사이즈용 Sprite 재생성은 안정화된 투영값이 실제로 바뀔 때다.
- mask 경로의 기존 표시 보존을 확인한 것이며, native 경로에 남는 입력창/힌트까지 화면 픽셀 축소됐다고 주장하지 않는다. 실제 입력·힌트의 크기/가독성은 테스터가 확인해야 한다.

## 장비 진단 delta

버퍼는 read-only GetBuffer를 사용하고 쓰기/추가 모델 호출은 없다. client entity는 동일 PlayerGhost.playerGuid로 검색하며 server Entity index를 재사용하지 않는다. 선택된 member만0.5초 주기로 읽고 로그는 변경 시/최소2초 간격이다. client 진단 예외는 DescribeClient 호출 주위에서 격리해 기존 스킬/장비 표시를 유지한다. 쿼리와 Temp NativeArray는 using으로 해제한다. 실제 조사 결과는 [06-equipment-bug.md](06-equipment-bug.md)의 최신 raw 대조를 따른다.

## 독립 검증

- 8578950 게임 API 참조 .NET compile 경고0/오류0, core1135개 통과.
- 11eb461 게임 API 참조 .NET compile 경고0/오류0, core build 경고0/오류0, 전체 실행 **ALL CHECKS OK (1137)**. 신규 stable projection2개 포함.
- 25db8ed..11eb461 diff whitespace 검사 통과.
- 격리 출력: `C:/Dev/_scratch/ai-ui-independent-review-11eb461/`, 실행 로그 `core-run.log`. 작성자 출력 경로와 분리했다.
- pure 테스트는 실제 Unity 렌더링/카메라 로딩 타이밍을 실행하지 않는다. 초기 로딩 후 정상 크기, 해상도 변경 후 배치, 긴 입력과 한글 IME/Tab/Enter, 색·기호·fade, 다른 UI에 pool glyph가 돌아간 뒤 표시, 월드 재입장/종료를 실게임에서 확인해야 한다. 검수자가 게임이나 CU 소유권을 가져오지 않았다.
