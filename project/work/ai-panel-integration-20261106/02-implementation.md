# 구현과 검증 인계

## 실게임 가독성 후속 25db8ed

현재 최신 후보는 `25db8ede5fa429791b01e69b51f4dbbd980679de`다. 아래 최초 구현/좌표 기록은2762495 기준이며, 실제2560×1440 화면에서 본문10px가 너무 작다는 HEAD 판정에 따라 AI 패널만 정수 배율로 확대했다. 채팅 글꼴·폰트 데이터/생성기·스냅샷·모델/게임 동작은 바꾸지 않았다.

- `AIUiLayout`이 화면 높이에 맞춘 정수배율과 화면폭/높이 내 최대배율을 함께 계산한다. 1080p/1440p는2배(본문20px),720p는1배(10px),4K는3배(30px). 좁은 창에서는 화면 안에 맞는 정수배율로 낮춘다. 최소 논리316×302조차 들어가지 않는 비정상적으로 작은 viewport에서는 UI를 그리지 않는다.
- 논리 레이아웃292×28 선택막대/292×246 상세는 유지한다. 같은 배율을 GUI.matrix, 버튼 geometry, 게임입력 캡처의 마우스 역변환에 적용한다. 다른 OnGUI에 영향을 주지 않도록 matrix/depth는 finally에서 복구한다. viewport 치수는 캐시하지 않아 창 크기를 바꾸면 다시 계산된다.
- 2560×1440: 선택막대 `(1952,1360,584,56)`, 상세 `(1952,860,584,492)`. 멤버 버튼 중심 x=2030/2172/2314/2456, y=1388. 스킬탭 중심 `(2102,946)`, 장비탭 `(2386,946)`, 닫기 `(2502,892)`.
- 1920×1080: 선택막대 `(1312,1000,584,56)`, 상세 `(1312,500,584,492)`.
- 검사: 레이아웃22개(640×480~4K,울트라와이드,좁은 창,뷰포트 축소,hit좌표 일치),기존포인터19개,게임API컴파일 경고0/오류0. `evidence/ui-layout-tests.txt`, `pointer-tests-scaled.txt`, `compile-check-scaled.txt`. 실제 확대 화면은 아직 설치 전이며 별도 실게임 검증을 기다린다.
- 새 커밋의 Unity 실행/공통 설치는 하지 않았다. 기존 독립 실게임 검증을 방해하지 않고 구현·오프라인만 수행했다.

## 고정 후보

- worktree: `C:/Dev/CoreKeeperModSDK-ai-ui`, branch: `ai-info-ui-20261106`.
- 최초 base: `902721ec505df42d74d0b2d245ed34a049faadad`. HEAD 지시에 따라 선택지 개선이 승인·병합된 `b25099f` 위로 rebase했다.
- 기능: `e5ce3095afed4bd3425aba9c9c0c2f81666f13bd`.
- 최종 후보: `2762495663a6cfa26b7d966ad6676b53f880c73e` (b25099f 위 dc523e6, f1c4b0b, 2762495). 이전 검수 대상 ecc8a88의 런타임 코드는 동일하다. 유일한 rebase 충돌은 core-tests/Program.cs에 CHOICE와 UI 그룹을 둘 다 등록해 해결했다. `evidence/rebase-range-diff.txt`.
- `f1c4b0b`는 폰트 원본/생성물 Windows 체크아웃 개행 보존 `.gitattributes`, `2762495`는 입력 폴링 사이 초단타 클릭의 down-edge도 캡처하는 세 파일 후속 수정이다.
- 공통 트리 변경·병합·push·게임 설치·실행 없음. 작업 중 공통 SDK/게임 설정은 보존.

## 구현

`AICompactFont`와 생성 `AICompactFontData`는 원저자 Galmuri9 BDF의 허용된 OFL 데이터를 동봉한다. 외부 모드 코드·DLL·구독 의존은 없다. 11,369글리프, 10px 셀, gzip 64,742바이트. 원저자·OFL 전문·원본 커밋·SHA256은 배포되는 C#에 포함된다. 재현 명령은 `node Tools/ui-font/build.mjs --check`, 원본과 생성기는 Tools/ui-font에 있다.

기본 채팅은 SetPugFont 인수만 현재 채팅 출력/입력/입력 안내/AI 멘션 힌트 인스턴스에 한정해 교체한다. 전역 TextManager 글꼴은 변경하지 않는다. 입력창과 힌트는 명시적 객체로도 판별하므로 부모 배치에 의존하지 않는다. 한국어 IME/Tab은 기존 PugText/ChatWindow/AIChatHint 실행 경로를 그대로 사용한다. 동적 글꼴 경로, 게임의 기타 문자 폴백은 유지된다. 실제 Render→dimensions.height→AddMessageHeight로 줄높이가 결정되며 축소된 치수를 쓴다. 시각적 밀도·한글 입력 실제 확인은 아직 실게임 대기다.

`AIInfoPanel`은 호스트에서 작은 선택 막대를 만든다. AI별 클릭으로 상세를 열고, 스킬/착용 장비 탭을 전환하며 X 또는 같은 AI 재클릭으로 닫는다. 스킬은 SkillBuffer의 실제 XP를 게임 SkillExtensions.GetLevelFromSkill로 변환한다. 장비는 서버의 활성 장비 프리셋과 해당 인벤토리 슬롯, 현재 손 슬롯을 읽는다. 0.5초마다 선택한 멤버만 새 스냅샷으로 교체하고, 부재 상태는 ‘미접속’으로 표시한다. 저장 데이터·AI 행동·LLM 호출·메시지 전송 경로는 호출하지 않는다.

UI 클릭은 로컬 호스트 inputModule에 한정한 PlayerInput 버튼 3종과 축 4종 prefix로 차단한다. GUI 이벤트 소비만 의존하지 않는다. 버튼별 시작 에지를 기록해 UI 밖에서 시작한 드래그는 가로채지 않고, 다른 버튼을 누르고 있는 동안 새 UI 클릭은 캡처한다. UI 밖으로 끌어서 놓아도 놓은 프레임의 모든 게임 질의까지 차단하고 다음 프레임에 해제한다. 후킹 실패 시 패널은 만들지 않는다. 인벤토리/장비/외형/지도/메뉴 중에는 숨고, 월드 종료는 UI·스냅샷·폰트/스프라이트를 해제한다. 해제 전에 폰트 후킹을 비활성화해 채팅 원본 글꼴로 재렌더한다.

## 실행/관측 방법

별도 설정이나 키 바인딩 없이 호스트 월드에서 자동 표시된다. 기존 모드의 멤버 참여 설정을 그대로 따른다. 미접속 멤버도 선택해 부재 상태를 볼 수 있다.

- 화면 좌표는 Unity GUI 기준 좌상단 원점. 선택 막대는 `(max(8,W-304), max(8,H-40),292,28)`.
- 1920×1080 예: 막대 `(1616,1040)`~`(1908,1068)`, 멤버 버튼 중심 x=1655/1726/1797/1868, y=1054.
- 상세 패널은 막대 위 4px, 292×246: 위 예 `(1616,790)`~`(1908,1036)`.
- 스킬 탭 중심 `(1691,833)`, 장비 탭 `(1833,833)`, 닫기 `(1891,806)`.
- 다른 해상도에서는 위 계산식을 적용. 글자는 비트맵 원본 픽셀배율1이며 검수에서 가독성을 판단한다.
- 관측 로그: `AI-UI font ready`에 생성시간/아틀라스 크기, `AI-UI chat metrics`에 원래/교체 셀과 줄간격, `AI-UI selected <name>`에 상태와 스킬/장비 행 수.
- 실제 상태 원천: `AIPlayerSpikeMod.ServerPlayerOf(AIAgent.All[index])`의 서버 EntityManager `SkillBuffer`, `ActiveEquipmentPresetCD`, `EquipmentPresetsBuffer`, `ContainedObjectsBuffer`, `EquippedObjectCD`. 모델이나 저장 파일을 통해 값을 만들지 않는다.

실게임 담당은 실제 AI 한국어 발언/다중줄 밀도, 입력·IME 조합·멘션/Tab·Enter, 각 멤버의 스킬과 현재 장비, 닫기, UI 클릭/다중버튼/외부드래그 시 공격·이동 누출, 메뉴/지도 숨김, 월드 재입장을 확인한다. 추가 모델 호출0은 기존 호출과 UI 조회를 구분해 관측한다. 새 게임 실행은 HEAD의 점유 순서에 따른다.

## 검증 상태

- .NET 게임 DLL 참조 컴파일: 경고0/오류0.
- 순수 포인터 회귀19개 통과. `evidence/pointer-tests-final.txt`. 초단타 클릭 down-edge 후속 수정 뒤 재확인했다.
- 폰트 재생성 일치/한글·자모 포함/클리핑 검사 통과. `evidence/font-check.txt`.
- 전체 비Unity 관문: ecc8a88에서 core1081과 모든 비Unity 관문 통과(`evidence/unity-gates.txt`; 마지막 Unity 시작만 다른 작업자 점유 보호로 보류됐고 아래 별도 실행에서 성공). rebase 후보2762495는 전체 core1103·게임 DLL 참조 컴파일 경고0/오류0·폰트 재현검사를 재실행했다(`evidence/core-tests-rebased.txt`, `compile-check-rebased.txt`, `font-check-rebased.txt`).
- 초기 exp-sim은 공통 `%TEMP%/exp-sim` 병렬 작업 충돌로 실패했다. 이 프로세스의 TEMP/TMP만 worktree Logs/isolated-tests로 바꾼 재실행은 통과했다. 전역 환경·제품·시험 구현은 바꾸지 않았다.
- Unity compile-only: ecc8a88에서 exit0, error CS0, `[HeadlessSdk] CompileCheck: ok` 성공. `evidence/unity-compile-ecc8a88.log`. b25099f rebase 뒤 결합본의 Unity 재컴파일은 아직 하지 않았으며 임시설치 빌드에서 최종 소스를 컴파일해야 한다.
- Library는 main에서 독립 복사(25,924파일, 약2.5GiB, 누락/실패0). 공유 writable 링크 없음. `evidence/library-copy.log`.
- 실제 플레이/화면 검증은 이 구현자가 수행하지 않았다. 글꼴 생성시간·밀도·입력경계의 최종 판정은 실게임 담당 근거를 추가해야 한다.

로컬 SDK용 설정과 미추적 Plugins/CoreKeeper·Unity 생성 .vsconfig는 커밋에서 제외했다. Unity/rebase 후 표시되는 로컬 SDK 변경은 6개이며 제품 변경은 모두 커밋됐다. rebase 적용 중 남은 CRLF 작업 파일은 HEAD의 exact blob으로 복원하고 동일 해시/폰트 재현 검사를 확인했다. 독립 검수는 HEAD가 별도 소유한다. 공식 그래프 적용은 승인된 최종 병합 뒤이며 준비 인계는 03-graph-handoff.md를 따른다.
