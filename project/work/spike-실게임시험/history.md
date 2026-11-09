<!-- head-context branch:1 -->
# spike-실게임시험

이름표: spike-실게임시험
시작: 2026-11-02T23:10:08.162Z
갈래: 코어키퍼-AI-캐릭터/메인@2026-10-21T15:01:01.274Z
갈래-ID: %EC%BD%94%EC%96%B4%ED%82%A4%ED%8D%BC-AI-%EC%BA%90%EB%A6%AD%ED%84%B0/main@2026-10-21T15:01:01.274Z
상태: 끝남

## 지금까지 요약

AIPlayerSpike 실게임 시험을 마침. 아침에는 빔프로젝터가 꺼져 있어 HEAD가 화면을 캡처하거나 조작할 수 없었음(steam_appid.txt로 Steam 실행 옵션 창을 우회해 모드 로드까지만 확인). 저녁에 사용자가 새 월드를 켠 상태에서 Player.log와 화면으로 STEP1~3 모두 통과를 확인함: thin client 경로로 두 번째 플레이어 'Claude'가 접속하고, 이름이 붙고, 입력 명령(route A)으로 움직임. steam_appid.txt는 제거함. 다음은 이 결과로 몸 방식(thin client 진짜 플레이어)을 확정하고 구현 설계로 넘어가는 것(5c34의 다음).

## 2026-11-02T23:19:05.664Z

요약: AIPlayerSpike 직접 시험 시도: 게임은 모드를 불러온 채 실행됐으나 화면(빔프로젝터)이 꺼져 있어 캡처·입력 불가로 중단, 사용자가 귀가 후 화면을 켜면 재개

사용자 요청: HEAD가 직접 게임을 실행해 AIPlayerSpike 시험(STEP1~3)을 확인.

확인된 사실
- 이 PC에서 Core Keeper를 처음 실행. Steam 실행 시 실행 옵션 창('Core Keeper 플레이' / '모드 없음 플레이')이 뜸.
- CoreKeeper.exe를 직접 켜면 "Exiting and restarting via steam"으로 Steam을 거쳐 다시 시작함 → 게임 폴더에 steam_appid.txt(1621690)를 추가해 우회. 이 파일은 시험이 끝나면 지울 것(현재 남아 있음).
- 우회 실행 시 Player.log(%USERPROFILE%\AppData\LocalLow\Pugstorm\Core Keeper\Player.log)에 `loaded mod AIPlayerSpike`, `steam initialized successfully` 기록 → 모드 로드까지는 확인. STEP1~3은 아직 미판정(월드 진입 전).
- 화면 출력 장치가 빔프로젝터라 꺼져 있으면 화면이 잡히지 않음: 전체 화면 캡처가 검게 나오고 앞에 나온 창이 없으며, 주입한 클릭·키 입력이 먹히지 않음. 창 단위 캡처(PrintWindow)는 Steam 창에서는 되지만 게임 창은 검게 나옴.
- 조작 도우미(임시): 대화 scratchpad의 ui.ps1(캡처·클릭·키), win.ps1(창 캡처). 샌드박스 안에서는 캡처가 안 되어 샌드박스 밖에서 실행함.
- 게임은 정상 종료함. Steam 실행 옵션 창은 남아 있을 수 있음.

시험 절차(재개 시): 화면 켜짐 확인 → 게임 실행(steam_appid.txt로 직접 실행) → 버릴 새 캐릭터·새 월드 생성(Claude가 호스트 캐릭터 복사본으로 들어와 아이템이 복제·저장되므로) → 월드 진입 후 약 20초 대기 → Player.log에서 AIPlayerSpike STEP 1~3 판정.

다음: 사용자가 귀가 후 화면(빔프로젝터)을 켜면 HEAD가 게임을 직접 실행해 새 캐릭터·새 월드로 STEP1~3 판정 → 끝나면 steam_appid.txt 제거



## 2026-11-03T10:28:13.260Z

요약: AIPlayerSpike 실게임 시험 통과: STEP1(두 번째 플레이어 접속)·STEP2(이름 Claude)·STEP3(route A로 이동) 모두 OK, 화면에서도 Claude 캐릭터 확인

사용자가 귀가 후 새 월드(worlds\4.world.gzip, 11-03 19:22 생성)를 호스팅해 게임을 켬. HEAD가 Player.log와 화면 캡처로 판정.

결과(Player.log의 [AIPlayerSpike] 줄)
- STEP 1 OK: thin 월드 생성 → 127.0.0.1:7777 접속 → 서버가 PlayerConnectRequestRPC 수락 → 두 번째 플레이어 생성(playerIndex=2). 월드 진입 후 접속까지 약 0.4초.
- STEP 2 OK: 서버 플레이어 이름 'Claude', onlineName 'Claude'. 화면에도 'Claude' 이름표가 붙은 캐릭터가 코어 옆에 보임.
- STEP 3 OK: route A(thin 클라 입력 명령을 Netcode로 보냄)로 2.8초 만에 2.34칸 이동. route B(서버 입력 버퍼) 폴백은 필요 없었음. 이후 오른쪽·왼쪽·원·정지 패턴을 반복하며 50초 넘게 이동 계속.
- 해석(미확인): 입력을 주는데 위치가 멈추는 구간과 x 약 ±4~5, z 약 -3.3~1.7 범위는 시작 코어 방의 벽 충돌로 보임.
- 오류·예외 줄 없음.

판단: 게임 내장 thin client 경로로 '진짜 플레이어 몸'이 된다. 미니언 몸 폴백은 필요 없을 가능성이 높음.

정리: 게임 폴더의 임시 steam_appid.txt 제거 완료. 모드는 여전히 게임 폴더에 설치돼 있어 어느 호스팅 월드에서든 Claude가 들어옴(호스트 캐릭터 복사본 → 아이템 복제 위험).

