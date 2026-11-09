# 가방·장비 1차 고정판

격리 worktree C:/Dev/CoreKeeperModSDK-bag-equipment, Git bag-equipment-20261107의 d8f2084. 출발은11eb461이다. 공식 본 저장소·설치·게임은 변경하지 않았다.

## 연결된 결과

- equip/unequip/hold 목표와 실제 개별 가방/현재 활성 장비 슬롯 후보를 연결했다. 반지·파우치 목적지를 구별한다.
- 후보의 출발·도착 물품 스냅샷을 적용 직전에 대조하고 정상 UI Swap 요청을 보낸다. 큐접수만으로 성공 처리하지 않는다. 양 슬롯 교환, 현재 활성 장비 슬롯과 실제 손 물품 관측을 결과로 확인한다.
- 배낭/파우치 용량·태그 변경에 따른 자동 드롭을 예방하는 현재 조건 검사를 둔다. guest·커서·물품 파손·슬롯 요구조건을 대조한다.
- 명시 손선택을 도구/근접무기별로 유지하고 공격·짧은공격·채굴의 기존 AIBody 준비 입구에서 우선한다. 선택된 물건 소실·파손은 이유로 보고하며 자동 대체하지 않는다.
- 상위 계획 keep/slot와 새 목표 어휘를 전달하는 공유계약을 추가했다. 이 고정판의 실제 operations는 equip/unequip/hold만이며 Store/Take/Drop/Give는 아직 구현되지 않아 현재 CheckGoal이 거절한다. 나머지는 후속 확장용 연결 입구다.
- 새 목표의 성공·막힘은 note로 보고하며 자동 목표종료를 우회한다. 단계 완료는 별도 공통실행 finish_goal에 의존한다. 공통실행을 합치기 전 독립판은 전체 계획단계 진행의 완료판이 아니다.

## 실제 실행한 검증

- [빠른 컴파일](stage1-compile.log): dotnet build Tools/compile-check -c Release -nologo, 경고0/오류0.
- [Core 전체 회귀](stage1-core-tests.log):1177건 통과. 새 INV-GOAL11건은 실제 응답 파싱·계획큐의 keep/slot 보존과 형태 경계를 검사한다. INV-FLOW16건은 실제 제품 경로가 사용하는 Core helper의 요청·양슬롯 관측·비중첩상태·reserve/partial/false-success 경계를 검사한다.
- [bridge protocol](stage1-protocol.log):118건 통과. 기존 envelope와 새 공유 goal계약을 함께 확인했다.
- git diff --check 통과.

Unity 컴파일·설치·게임·외부 모델 호출은 이 검증에 포함하지 않는다. d8f2084의 독립 검수는 HEAD가 별도로 맡겼다. 후속판에서 일반목표의 장비후보 다양성과 가방 이동·전달을 이어서 구현한다.
