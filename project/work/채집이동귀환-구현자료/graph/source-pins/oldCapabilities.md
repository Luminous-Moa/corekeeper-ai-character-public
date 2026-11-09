# 기능 — 관측할 수 있는 능력

기능은 사람이 보거나 확인할 수 있는 능력 하나다. 모드는 작은 AI 정보 패널과 기본 채팅의 표시층을 포함한다. 관측 지점은 해당 화면과 Player.log 줄이다(`[AIPlayerSpike]` 접두).

기능은 코드를 소유하지 않는다. 기능은 "코드 앵커"에서 시작하는 선이다. 앵커에서 모드 안 호출을 따라 닿는 코드가 그 선이다. 선은 도구가 계산한다. 같은 코드가 여러 선 위에 있어도 된다.

이 문서에서 사람이 판단해 쓰는 것:
- 기능의 이름과 한 줄
- 코드 앵커(어느 함수에서 선이 시작하나)
- 적용되는 정책
- 상태와 그 근거

필드:
- 상태: 실게임 확인 / 사용 확인(개발 도구) / 구현됨·미확인 / 구현 중 / 설계만 / 미설계
- 종류: 제품 / 개발 도구
- 정책: 쉼표로 나눈 정책 ID. 후보(cand.*)도 적을 수 있다. 후보는 정책이 아니라 "관련"으로만 이어진다.
- 코드 앵커: 쉼표로 나눈 함수 ID(`형식.함수`). 게임이 직접 부르는 함수(Harmony 패치 함수, 인터페이스 구현)는 따로 앵커로 적어야 선에 들어온다.
- 게임 지점: 모드가 직접 부르지 않지만 이 기능이 기대는 게임 코드 자리. `디컴파일 상대 경로:줄 찾을 글` 꼴. 도구가 그 줄에 글이 있는지 확인한다.
- 확인한 선: 실게임에서 통과한 코드의 선 서명(12자). `node tools/query.mjs code F.x`가 지금 서명을 보여 준다. 시험이 통과하면 그 서명을 여기 적는다. 그 뒤 선 위 코드가 바뀌면 도구가 "확인 뒤 코드 바뀜"을 낸다. 없으면 "확인 시점 코드 모름"을 낸다.

`확인한 선`은 실제로 그 코드로 실게임 확인한 기능에만 적는다. 2026-11-03 몸 기본 기술 최종 빌드(AIBodySkills.cs `89074ee3e860`, AIPlayerSpikeMod.cs `ba1c568c46d7`)에서 다시 본 기능(접속·이름·켜기끄기·떠나기·입력 실행부·보기·가방·걷기·캐기·공격)은 그 서명으로 적었다.

## 만든 것

### F.thin-connect — 두 번째 플레이어로 접속
- 상태: 실게임 확인
- 확인한 선: dd009a955883
- 종류: 제품
- 한 줄: AI 참여가 켜진 호스팅 월드에서(F.ai-toggle) thin client 월드를 만들어 127.0.0.1로 접속하고, 서버에 연결 있는 두 번째 플레이어를 만든다. thin 월드 이름이 월드·AI마다 고정이라 playerGuid도 고정이고, 같은 월드에서는 같은 엔티티로 다시 들어온다.
- 관측 지점: Player.log "STEP 1 OK"; 화면에 두 번째 캐릭터
- 정책: pol.body-thinclient, pol.body-visible, pol.multi-ai, pol.join-toggle
- 코드 앵커: AIPlayerSpikeMod.Step1_CreateThinWorld, AIPlayerSpikeMod.Step1b_TrackHandshake, ThinClientDriverConstructor.CreateClientDriver, AIPlayerSpikePatches.ServerNetworkUpdatePrefix
- 게임 지점: Unity.NetCode/Unity.NetCode/ClientServerBootstrap.cs:189 CreateThinClientWorld
- 근거: work/spike-실게임시험/history.md "STEP 1 OK: thin 월드 생성 → 127.0.0.1:7777 접속"
- 근거: work/AI-전용-캐릭터/history.md "월드 ai(c48cf4bb…) 재접속 2·3번 모두 'Enabling player entity'(같은 엔티티)"
- 근거: work/2026-11/2026-11-03-ai-참여-켜기끄기.md "t=227.2 AI identity: spawned AI player guid=c7ddd1ca7a22e19f36af22ac2bfadd72 expected=c7ddd1ca7a22e19f36af22ac2bfadd72 match=True"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`AI identity: spawned AI player guid=4bc3c25898bea1cd862846508390a3f1 expected=4bc3c25898bea1cd862846508390a3f1 match=True`"
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "STEP 1 OK: SECOND PLAYER SPAWNED on server ... playerIndex=2"

### F.naming — 이름 'Claude'
- 상태: 실게임 확인
- 확인한 선: faf8d274901e
- 종류: 제품
- 한 줄: 서버 쪽 플레이어 이름과 onlineName을 'Claude'로 맞춘다. AI 전용 캐릭터를 쓰면 이름은 캐릭터 파일에서도 온다.
- 관측 지점: Player.log "STEP 2 OK"; 화면 이름표 'Claude'
- 정책: pol.body-visible, pol.multi-ai
- 코드 앵커: AIPlayerSpikeMod.Step2_NameAndPrepareInput
- 근거: work/spike-실게임시험/history.md "STEP 2 OK: 서버 플레이어 이름 'Claude', onlineName 'Claude'."
- 근거: work/2026-11/2026-11-03-ai-참여-켜기끄기.md "`2-켜기-들어옴.png`: `/ai on` 뒤 'Claude'가 보임"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`STEP 2 OK: server player name='Claude' onlineName='Claude'`. 다시 들어온 자리"

### F.move-input — 입력 실행부(한 입력, 기본 정지)
- 상태: 실게임 확인
- 확인한 선: 33ddaf97f459
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "server PlayerGhost.cameraPosition ai=float2(-101.0003f, 46.57286f) host=float2(0f, -0.138f)"
- 종류: 제품
- 한 줄: 실행부 하나(`AIBody.Tick`)가 매 틱 AI의 입력 하나(걷기 방향·바라보는 방향·주 사용 버튼·손에 든 칸)를 만들고, thin client가 Netcode 입력 명령으로 보낸다(route A). 하던 기술이 없으면 움직이지 않는다. 예전 걷기 무늬는 `/ai walktest`로만 돈다. route B(서버 입력 버퍼 직접 쓰기)는 `/ai route b` 진단용으로만 남았다. AI가 쓰러지면(체력 0) 하던 기술을 실패로 끝낸다. 입력의 카메라 위치(cameraPosition)에 AI 자기 위치를 넣는다(진짜 클라이언트의 FollowPlayer 카메라와 같음). 서버는 이 값으로 엔티티를 켜고 끄고 고스트를 보낸다. 비어 있으면 (0,0)=코어로 여겨져, 코어·호스트에서 약 100칸 넘게 떨어진 AI 주변이 꺼졌다(먼 곳 캐기 실패의 원인).
- 관측 지점: Player.log "STEP 3 OK (walktest)", "SKILL <이름> START|DONE|FAILED|CANCELLED"; 화면에서 기술이 없을 때 서 있음
- 정책: pol.realtime-by-program, pol.behavior-roles, pol.idle-stand
- 코드 앵커: AIPlayerSpikeMod.Step2_NameAndPrepareInput, AIPlayerSpikeMod.Step3_Drive, AIPlayerSpikeMod.SetRoute, AIBody.Tick, WalkTestSkill.Start, WalkTestSkill.Tick
- 게임 지점: Pug.Other/SetPlayerDirectionSystem.cs:148 clientInput.facingDirection;
- 근거: work/spike-실게임시험/history.md "STEP 3 OK: route A(thin 클라 입력 명령을 Netcode로 보냄)로 2.8초 만에 2.34칸 이동."
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`STEP 3 OK (walktest): player moved 0.93 units after 2.8s via route A (thin-client input commands over Netcode)`"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "정지 뒤 두 번의 `look`(4초 간격)에서 `ai=(-2.00,-1.16)` 같음."
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "| 목표 없음이면 호출 없음 | 통과. 입장 후 12초 동안 `DECISION` 0줄. `/ai on`으로 다시 들어온 뒤에도 0. |"
- 메모: 쓰러짐 처리("AI가 쓰러짐(체력 0)")는 행동 판단 연결 1차 시험에서 실게임으로 봤다.
- 메모: 시험 전용 움직임 기록 `AIMoveTrace`(스위치 파일 `AIPlayers/movetrace.on`이 있을 때만, 프레임마다 CSV)가 Step3_Drive 끝에서 돈다. 서버 틱 20/초, 입력은 틱마다 고르게 전달됨을 이것으로 쟀다.
- 근거: work/2026-11/2026-11-04-이동-드득거림.md "| 서버가 실제로 쓴 입력(ClientInput) | 같은 모양: 뒤집힘 347번(6.0/초). 입력이 틱마다 고르게 전달됨 — 가설 (b) 아님 |"

### F.skill-look — 보기
- 상태: 실게임 확인
- 확인한 선: 17801b660da2
- 종류: 제품
- 한 줄: AI 자리를 중심으로 사람 화면 크기(호스트 카메라에서 잰 사각형)만큼의 타일(벽·광석 겹침·걸을 수 있는 칸), 벽 종류별 체력·감소량, 적, 떨어진 아이템, 부딪히는 물체, 자기 체력·배고픔·든 물건을 모은다. 빛·어둠은 아직 안 따진다. 보는 범위 규칙은 `AIVision` 한 곳에 있다.
- 관측 지점: 채팅 "보기: 범위 …"; Player.log "SKILL 보기 DONE … facts=[rule=…]"
- 정책: pol.vision-human-view
- 코드 앵커: LookSkill.Start, LookSkill.Tick, AIObservation.Take, AIVision.HalfExtents, AIVision.CanSee, AITileReader.AITileReader, AITileReader.Layers, AITileReader.Top, AITileReader.Walkable, AITileReader.HasWall, AIWallData.TryGet, AIBody.HostPosition
- 게임 지점: Pug.Other/CameraManager.cs:244 gameCamera.orthographicSize
- 게임 지점: Pug.Other/PathFindSystem.cs:1321 GetPrimaryPrefabEntity
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`chat reply: 보기: 범위 30x24칸(사람 화면, 빛 미반영) · Claude (2.3,0.3) · 호스트 (0.0,-0.3) · 벽 431(광석 8) · 적 0 · 아이템 0`"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "이 PC(3840x2160, 16:9)에서 **30.0 x 23.9칸**"

### F.skill-inv — 가방 확인
- 상태: 실게임 확인
- 확인한 선: 36c3e694e590
- 종류: 제품
- 한 줄: 서버의 AI 가방을 읽어 칸 번호·물건·개수, 든 칸, 빈 칸 수를 알린다. 읽기만 한다.
- 관측 지점: 채팅 "가방: …"
- 정책: pol.body-progress
- 코드 앵커: InvSkill.Start, InvSkill.Tick
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`chat reply: 가방: 1:SmallLanternx1, 2:FoodRationx3, 3:Woodx7 · 든 것 0번 칸 빈손 · 앞 40칸 중 빈 칸 37`"

### F.skill-goto — 지정한 곳까지 걷기
- 상태: 실게임 확인
- 확인한 선: 156e8e7dc26d
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "nav: switching to mod A* (game pathfinding found no path; retrying with tile A* on known tiles only)"
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "LEAK-CHECK walk to (-103,48) Arrived mode=mod-astar: knownPathAtStart=True tilesWalked=23 unseenAtStartWalked=0"
- 종류: 제품
- 한 줄: 게임 길찾기를 빌려 걷는다(도우미 엔티티의 PathFindCD·PathFindBfsCD·노드 버퍼 5칸). 0.5칸 안이면 도착, 2초 동안 0.4칸도 못 가면 막힘(3번이면 실패), 시간 제한 있음. 게임이 처리했는데 길이 없으면 "길 없음"으로 실패한다. 단 3칸 이하 짧은 거리면 모드 A*(타일만 봄)로 한 번 더 해 본다(AI가 게임이 막혔다고 보는 자리에 서 있을 때, 예: 코어 스폰 자리·식물 사이). 게임이 처리조차 안 했을 때도 모드 A*로 넘어간다. 생물을 쫓을 때는 그 엔티티를 목표로 준다. 길은 AI가 지금 있는 선분부터 따라가고 뒤 노드는 고르지 않으며, 몸 너비(반폭 0.3칸)로 곧게 갈 수 있는 가장 먼 노드를 향한다(모퉁이는 열린 칸에서만 깎음, 게임 길과 모드 A* 길 같음). 걸어가며 손 칸을 미리 들 수 있다(굴 입구로 갈 때 곡괭이). 남은 거리로 남은 시간(5.7칸/초)과 진행 설명을 판단 층에 준다.
- 관측 지점: 채팅 "AI 걷기 완료: 도착 (x,z), 길찾기 game-bfs"; Player.log "SKILL 걷기"
- 정책: pol.realtime-by-program
- 코드 앵커: GotoSkill.Start, GotoSkill.Tick, GotoSkill.Cleanup, GotoSkill.Progress, GotoSkill.EtaSeconds, AINavigator.Start, AINavigator.Tick, AINavigator.Retarget, AINavigator.Dispose, AINavigator.CreateHelper, AINavigator.SwitchToAStar, AINavigator.AStar, AIObjects.StuckAt
- 게임 지점: Pug.Other/PathFindSystem.cs:1484 AddStartingNodesToBuffer
- 게임 지점: Pug.Other/PathFindSystem.cs:1408 pathFind.targetEntity
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`SKILL 걷기 DONE after 2.8s: reason='도착 (-4,-3), 길찾기 game-bfs'`"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "최종 빌드: `SKILL 걷기 DONE after 0.9s: reason='도착 (-14,-4), 길찾기 game-bfs' ... target=호스트`"
- 근거: work/2026-11/2026-11-04-이동-드득거림.md "| 후: 탐험 (`jitter4`, 최종) | 143초 | 46.8초 | 0.73 | 0.00 | 0.00 | 0.13 | 5.74 | 0% |"
- 근거: work/2026-11/2026-11-04-이동-드득거림.md "게임 길 프레임 3945 중 **1347(34%)이 `nodes[0]`(길을 계산했던 칸 = 방금 떠난 칸)을 향함**"

- 메모: 2026-11-04 막힘-멈춤-정리(커밋 039d8b5): 모드 A*가 지금 보이는 물체·시작 때 물체·이 걷기에서 멈춘 칸을 피하고, 멈추면 밀고 들어가던 칸을 120초 막힘으로 적는다(AIObjects.NoteStuck, 모든 계획에 적용). 물체에 붙은 AI를 "위에 서 있음"으로 보던 결함을 고쳤다(안쪽 0.35칸 넘을 때만).
- 근거: work/2026-11/2026-11-04-막힘-멈춤-정리.md "멈춘 칸 막힘이 그 진짜 장애물을 막아 준 것"

### F.skill-mine — 벽·광석 캐기
- 상태: 실게임 확인
- 확인한 선: abd1a6057bd0
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "SKILL 캐기 DONE after 2.4s: reason='벽 (-100,49) 부숨 · 가방 WallDirtBlock +1'"
- 종류: 제품
- 한 줄: 대상 벽 옆 걸을 수 있는 칸으로 가서 벽 쪽을 보고, 곡괭이가 손 칸에 있으면 그것을, 없으면 빈 칸(맨손)을 든다. "추정 피해(20 + 캐기 효과) > 벽 감소량"이 아니면 시작 전에 "도구 부족"으로 실패한다. 주 사용을 누르고 있다가 타일이 없어지면, 3초 안에서 가방이 늘 때까지 기다려 가방 차이와 땅에 남은 것을 알린다(굴 중간 벽은 기다리지 않고 바로 끝, `NoPickupWait`). 6초 동안 타일 체력이 안 변하면 실패. 도구 바꾸기 0.6초 기다림은 그 칸을 이미 0.6초 넘게 들고 있었으면 0.1초(벽 쪽으로 돌기)만. 남은 벽 체력이 한 번 칠 피해 이하이면 남은 시간 0.3초로 판단 층에 알린다.
- 관측 지점: 채팅 "AI 캐기 완료: 벽 (x,z) 부숨 · 가방 …"; Player.log "SKILL 캐기"
- 정책: pol.body-progress, pol.realtime-by-program, pol.test-tools-ok
- 코드 앵커: MineSkill.Start, MineSkill.Tick, MineSkill.Done, MineSkill.TryStandFor, MineSkill.Cleanup, MineSkill.Progress, MineSkill.EtaSeconds, AIWallData.EstimateMiningDamage, AIWallData.TileEntityHealth, AIBody.ChooseSlot, AIBody.InventoryCounts, AIBody.InventoryDiff, AIBody.DirTo, AIBody.TileOf, AIWallData.RiskText, AIWallRisk.Text
- 게임 지점: Pug.Other/EntityUtility.cs:1561 componentData.reduction
- 게임 지점: Pug.Other/EntityUtility.cs:1536 20;
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "tool=맨손 slot=0; precheck: est damage 20 (20 + Mining effect 0) vs reduction 1; holding None; tile gone after 2.9s of swinging"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "tool=WoodMiningPick slot=4; precheck: est damage 35 (20 + Mining effect 15) vs reduction 1; holding WoodMiningPick; tile gone after 1.3s of swinging"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`SKILL 캐기 DONE after 4.2s: reason='벽 (1,-4) 부숨 · 가방 WallDirtBlock +1' ... tool=맨손 slot=0"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "tool=WoodMiningPick slot=4; precheck: est damage 36 (20 + Mining effect 16)"
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "SKILL 캐기 DONE after 2.6s: reason='벽 (-4,-3) 부숨 · 가방 WallDirtBlock +1'"
- 근거: work/2026-11/2026-11-04-이동-드득거림.md "| 모으기 | `GOAL done rev=1: 구리 광석(CopperOre) 5개 모으기 (5/5) after 20.4s, calls this session 8`(파들어가기 4번·캐기 2번)."
- 메모: 최종 빌드에서 맨손(월드 ai2)·곡괭이(월드 ai) 모두 다시 확인했다. "도구 부족" 사전 실패는 맨손으로 못 파는 벽이 보이지 않아 아직 실게임으로 못 봤다(U.mine-toolshort-untested).

### F.skill-attack — 적 공격
- 상태: 구현됨·미확인
- 근거: work/2026-11/2026-11-04-전투-선택지.md "#29,#30 -> 계속하기 — 지금 하는 '공격'(attack:63129)을 2초째 진행 중 (대상 MushroomEnemy 체력 75/105, …)"
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "SKILL 공격 DONE after 5.4s: reason='MushroomEnemy 처치 (체력 105→0)'"
- 종류: 제품
- 한 줄: AI가 고른 적에게 접근해 근접 타격한다. 45초 경과·무진척은 현재 실행의 관측으로 전달한다. 실제 적 처치·소실·도달 불가 결과는 보고하며, 새로운 AI 선택이 행동을 중단·전환한다.
- 메모: 이전 구현 설명은 당시 기록이다. 보이는 가장 가까운 적 하나만 친다. 1.3칸 안이면 바라보고 주 사용, 아니면 게임 길찾기(대상 엔티티)로 다가간다. 피하기·후퇴·대상 바꾸기는 없다. 적이 죽거나 사라지면 완료, 3초 넘게 안 보이거나 45초 넘거나 길이 세 번 연달아 없으면 실패.
- 관측 지점: 채팅 "AI 공격 완료: <적> 처치"; Player.log "SKILL 공격: … hp a->b"
- 정책: pol.body-fight, pol.realtime-by-program
- 코드 앵커: AttackSkill.Start, AttackSkill.Tick, AttackSkill.Alive, AttackSkill.Cleanup
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`SKILL 공격 DONE after 17.5s: reason='MushroomEnemy 처치 (체력 33→0)'`"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`SKILL 공격 DONE after 4.6s: reason='MushroomEnemy 처치 (체력 105→0)'`"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`SKILL 공격 FAILED after 7.0s: reason='대상에게 갈 길 없음 (길 없음(게임 길찾기: 타일·물체 고려), 체력 105→105)'`"
- 메모: 최종 빌드에서 처치와 "길 세 번 연달아 없음" 실패를 모두 봤다. 쓰러짐 처리는 행동 판단 연결 1차 시험에서 봤다.
### F.ai-char-load — AI 전용 캐릭터로 들어오기
- 상태: 실게임 확인
- 확인한 선: f20de249ec19
- 종류: 제품
- 한 줄: 월드마다 AI 캐릭터 파일(`<persistentDataPath>/AIPlayers/<월드 ServerGuid>/Claude.json`)을 읽거나, 없으면 새 캐릭터 상태(직업 기본 지급품·스킬만)·호스트와 다른 외형으로 만든다. 서버의 스폰 처리(StartGameRPCSystem의 람다)가 AI의 playerGuid 요청을 풀 때만 캐릭터 정보를 AI 것으로 바꾼다. 다른 playerGuid(호스트·사람)는 그대로 둔다. 파일을 못 만들면 접속하지 않는다.
- 관측 지점: Player.log "AI character: server received character data for the AI's playerGuid"; 화면에서 호스트와 다른 외형
- 정책: pol.ai-own-character, pol.ai-start-new, pol.ai-look-distinct, pol.multi-ai, pol.ai-per-world, pol.start-kit-role-only
- 코드 앵커: AICharacterStore.Prepare, AIPlayerSpikePatches.CharacterDataPostfix, AIPlayerSpikePatches.SpawnLambdaPrefix, AIPlayerSpikePatches.SpawnLambdaPostfix
- 게임 지점: Pug.Other/ThinClientConnectSystem.cs:412 GetStrippedAndSerializedCharacterData
- 게임 지점: Pug.Other/StartGameRPCSystem.cs:428 GetCharacterDataFromSerialized
- 게임 지점: Pug.Other/StartGameRPCSystem.cs:906 PlayerWasLastConnectedToThisServer
- 근거: work/AI-전용-캐릭터/history.md "AI 전용 캐릭터 구현·실게임 확인 통과: 새 캐릭터 시작(탐험가 기본 지급품), 호스트와 다른 외형, 전용 파일 저장·재접속 유지, 호스트 아이템 복제 없음"
- 근거: work/AI-전용-캐릭터/history.md "새 월드 ai2(b39072f4…)는 새 캐릭터로 시작."
- 근거: work/2026-11/2026-11-03-ai-참여-켜기끄기.md "t=322.6 AI character: server deserialised character data for playerGuid 8021eed84fc52ef81041b0a127a89663 (not the AI's); left untouched"

### F.ai-char-save — AI 캐릭터 저장과 이어하기
- 상태: 실게임 확인
- 확인한 선: 40b8befd3ed1
- 종류: 제품
- 한 줄: 서버의 AI 플레이어 개체에서 가방·스킬·상태·코인·체력·영혼을 읽어 그 월드의 AI 캐릭터 파일에 쓴다. 들어와 있는 동안 60초마다, 그리고 떠날 때(월드를 내릴 때, `/ai off`) 저장한다.
- 관측 지점: Player.log "AI character: saved"; 재접속 때 아이템 유지
- 정책: pol.ai-own-character, pol.body-progress, pol.ai-per-world, cand.save-timing
- 코드 앵커: AICharacterStore.SaveFromServer
- 메모: 2026-11-06 후보902721e 추가 실게임: 정상 저장→완전 process 종료→같은 시험 월드 재입장, 두 캐릭터의15필드씩30/30과 참여 설정 일치 및 실제 문 사용을 확인했다. 근거 work/리팩토링-20261106/15-unity-and-live.md §3.2. 모든 저장 필드/상황 검증이나 시험 월드 Cloud 삭제 완료를 뜻하지 않는다.
- 게임 지점: Pug.Other/PlayerController.cs:2832 SetInventory
- 근거: work/AI-전용-캐릭터/history.md "사람 플레이어는 화면 쪽 게임이 자기 가방을 읽어 저장(PlayerController.cs:2832 → SaveManager.SetInventory)."
- 근거: work/AI-전용-캐릭터/history.md "Player.log에 교체(호스트 guid 0812… → AI c1a0…a001), 나갈 때 저장, 재접속 시 AI 가방 [랜턴, 식량x3, 나무x7]·호스트 가방 [랜턴, 식량x3] 기록 확인."
- 근거: work/2026-11/2026-11-03-ai-참여-켜기끄기.md "t=249.3 AI character: saved (leaving (turned off by host (/ai off))) to ...\b39072f4...\Claude.json"

### F.leave — AI가 떠날 때 정리
- 상태: 실게임 확인
- 확인한 선: 44eb32d66138
- 종류: 제품
- 한 줄: 호스트가 월드를 떠나거나 게임이 월드를 내릴 때, 또는 `/ai off`일 때 AI 캐릭터를 저장하고 thin client 월드를 없앤다. `/ai off`일 때는 먼저 서버에서 AI 연결을 끊어 게임이 사람처럼 "접속 종료"로 처리하게 한다.
- 관측 지점: Player.log "thin world disposed"; 채팅 "Claude 님이 접속을 종료했습니다."
- 정책: pol.join-toggle, pol.ai-own-character
- 코드 앵커: AIPlayerSpikeMod.DisposeThinWorld, AIPlayerSpikePatches.UnloadWorldsPrefix, AIPlayerSpikeMod.RequestServerDisconnect
- 근거: work/2026-11/2026-11-03-ai-참여-켜기끄기.md "MESSAGE: Claude 님이 접속을 종료했습니다."
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`AI character: saved (leaving (turned off by host (/ai off)))` → `thin world disposed`"
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "| `/ai off` | 통과. 목표가 있는 중에 끄자 진행 요청을 버리고 나감, 10초 동안 새 호출 0. `/ai on`으로 다시 들어오면 목표 없음·호출 없음. |"

### F.ai-toggle — 월드마다 AI 참여 켜기·끄기(기본 꺼짐)
- 상태: 실게임 확인
- 확인한 선: 22622c649e81
- 종류: 제품
- 한 줄: 호스트가 채팅에 `/ai on`, `/ai off`, `/ai`(상태)를 치면 그 월드의 AI 참여를 켜고 끄고 알려 준다. 설정은 월드마다 `AIPlayers/<월드 ServerGuid>/settings.json`에 남고, 없으면 꺼짐이다. 명령은 다른 사람에게 보내지 않고, 답은 호스트 화면에만 보인다.
- 관측 지점: Player.log "AI 'Claude' in this world: ON|OFF", "chat command from host"; 채팅 "AI Claude: 이 월드에서 …"
- 정책: pol.join-toggle, cand.toggle-chat
- 코드 앵커: AIPlayerSpikePatches.ChatSendPrefix, AIPlayerSpikeMod.FlushReplies, AIWorldSettings.Members, AIWorldSettings.Exists, AIWorldSettings.SetMember
- 근거: work/2026-11/2026-11-04-멤버넷-캐릭터.md "chat command from host: '/ai goal dirt 15'      ← 멤버 없이 → 기본 캐릭터(Claude)"
- 메모: 2026-11-04 멤버넷 작업에서 설정이 멤버 목록(`members`)이 됐다. 옛 `{"aiEnabled": true}`는 `["claude"]`로 읽는다. `/ai on|off <멤버>`, `/ai off`(멤버 없이)는 모두 끔. 여러 캐릭터 자체는 F.multi-ai.
- 게임 지점: Pug.Other/ChatWindow.cs:368 commSystem.SendChatMessage(filteredText);
- 게임 지점: Pug.Other/ChatWindow.cs:217 RenderText(string
- 근거: work/2026-11/2026-11-03-ai-참여-켜기끄기.md "| 월드에 들어가면 Claude가 들어오지 않는다(기본 꺼짐) | 통과 |"
- 근거: work/2026-11/2026-11-03-ai-참여-켜기끄기.md "| 나갔다 다시 들어와도 마지막 설정 유지(켜짐) | 통과 |"
- 근거: work/2026-11/2026-11-03-몸-기본-기술-구현.md "`/ai` → `AI Claude: 이 월드에서 꺼짐 · 지금 들어와 있지 않음`"
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "| `/ai off` | 통과. 목표가 있는 중에 끄자 진행 요청을 버리고 나감, 10초 동안 새 호출 0. `/ai on`으로 다시 들어오면 목표 없음·호출 없음. |"

### F.dev-build — 헤드리스 빌드·설치
- 상태: 사용 확인
- 확인한 선: 190cf3bc4a4c
- 종류: 개발 도구
- 한 줄: Unity를 배치 모드로 띄워 게임 어셈블리를 들여오고, 모드를 만들고, 빌드해 게임 폴더에 설치한다.
- 관측 지점: Logs\headless-*.log "[HeadlessSdk]"; 게임 폴더 Mods\AIPlayerSpike
- 정책: pol.modsdk, pol.mod-source-no-dll
- 코드 앵커: HeadlessSdk.ImportGameFiles, HeadlessSdk.CreateMod, HeadlessSdk.BuildAndInstall, HeadlessSdk.CompileCheck
- 근거: work/branch-20261021T092745-5c346ae4/history.md "실험1 모드 AIPlayerSpike 빌드·설치 완료(헤드리스 SDK, thin client 경로)"
- 근거: work/2026-11/2026-11-03-ai-참여-켜기끄기.md "`HeadlessSdk.BuildAndInstall`로 설치, 설치본 해시 일치"

### F.goal-cmd — 호스트 채팅으로 목표 주기
- 상태: 실게임 확인
- 확인한 선: 7d48068b8ca9
- 종류: 제품
- 한 줄: 호스트가 `/ai goal <품목> N`(흙·구리·주석·나무 또는 ObjectID 이름), `탐험`, `따라와`, `없음`을 치면 목표를 세우고 수정 번호를 올린다. 모으기 완료는 목표 시작 뒤 가방 속 그 품목이 N개 늘었는지 관찰로 판정한다. `/ai goal`은 진행·호출 수·지연·이달 비용을 보여 준다. `/ai stop`·`/ai off`·월드 나가기는 목표를 지운다. 목표가 없으면 판단 호출이 없다. /ai goal 알아서는 위쪽 LLM 자동 모드를 켠다(F.llm-goal). 호스트 목표와 LLM 목표는 같은 `AIGoals.SetGoal`(옛 이름 `AIBrain.SetGoal`은 전달만)로 들어가고 출처(`host`/`llm:claude#n`)가 붙는다.
- 관측 지점: Player.log "GOAL set|done|cleared"; 채팅 "AI 목표: …", "AI 목표 완료: …"
- 정책: pol.selector-openai, pol.fewer-calls, pol.idle-stand, cand.goal-end-order-kept
- 코드 앵커: AIGoals.HandleGoalCommand, AIGoals.SetGoal/8, AIGoals.ClearGoal, AIGoals.CheckDone, AIGoals.EndGoal, GoalSource.IsHost, AIBrain.StatusText, AIGoalKindInfo.AIGoalKindInfo, AIGoalKinds.Of, AIGoalKinds.IsHostWord, AIGoalFeatures.TryDone, AIGoalFeatures.StallNote
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "GOAL done rev=1: 흙벽 블록(WallDirtBlock) 5개 모으기 (5/5) after 20.1s, calls this session 7"
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "| 목표 없음이면 호출 없음 | 통과. 입장 후 12초 동안 `DECISION` 0줄. `/ai on`으로 다시 들어온 뒤에도 0. |"
- 근거: work/2026-11/2026-11-04-위쪽LLM-첫단계.md "GOAL set rev=4 source=host: WallDirtBlock(WallDirtBlock) 2개 모으기"
- 메모: 2026-11-04 리팩토링(3c3adbe→e82eb87, 동작 유지): 목표 상태·설정·끝이 `AIGoals.cs`로 옮겨졌다. 목표 끝 7곳이 `AIGoals.EndGoal` 한 경로(순서는 `EndOrder`로 예전 그대로)가 되고, 출처 해석은 `GoalSource.IsHost/IsLlm`. `AIBrain.SetGoal/ClearGoal/HandleGoalCommand`는 전달만 한다. 시스템 끝 셋(비용 상한·키 없음·연속 실패)은 아직 `ClearGoal`로 바로 끝낸다(U.goal-system-ends). 확인한 선은 리팩토링 전 서명이다(L2 실게임은 선 서명을 남기지 않았다).

### F.candidates — 실행 가능한 행동 후보 만들기
- 상태: 구현됨·미확인
- 근거: work/2026-11/2026-11-04-전투-선택지.md "#1 -> 먹기 — HealingPotion(즉시 체력 +30%), 가방에 2개. …        먹기 DONE 1.4s HealingPotion 먹음 · 체력 2→31"
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "chat reply: AI 탐험 끝: 걷거나 파서 새 구역이 보일 곳이 없음 (본 구역 522칸). 제자리에 섭니다."
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "objects 10 (+35 trigger-only, 1 under the AI), blocked tiles 42, large: SlimeBossStatue 3x3 at (-1,9); TheCore 5x5 at (-2,2)"
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "c4 탐험 이동(파 들어가기) — 동쪽 화면 끝 (13,0). 길 17칸, 부술 벽 5개(WallDirtBlock 5), 추정 타격 25회, 예상 약 20초."
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "GOAL done rev=1: 구리 광석(CopperOre) 1개 모으기 (1/1) after 19.2s, calls this session 4"
- 종류: 제품
- 한 줄: 현재 관측·목표·대상에서 실행 후보를 만들고 실행 가능한 현재 계속하기는 근접전 묶음과 개수 상한 뒤에도 보존한다. 실패 횟수는 이유와 함께 설명하고 횟수만으로 후보를 숨기지 않는다. 진행 후보 없음과 반복 대기는 목표를 지우지 않고 AI에게 관측으로 전달한다. 생활 탐색이 열리면 현재 깊이의 목록을 페이지로 조립하고 back/root/close와 공통 제어를 보존한다.
- 메모: 이전 구현 설명은 당시 기록이다. 보는 범위(AIVision) 안의 관찰, 탐색 기억, 목표로 후보를 최대 15개 만든다(순서: 계속하기, 호스트에게 가기, 적 공격 3, 목표 품목 벽 캐기 3, 파 들어가기, 기억한 목표 광석 쪽으로 2, 아이템 줍기 2, 탐험 걷기 3, 탐험 파기 2, 다른 벽 2, 기다리기). 시야 안 물체(충돌체가 있는 것)의 칸은 막힌 칸으로 친다. 탐험 목표에서는 다른 벽 캐기를 내지 않고, 탐험 후보가 없으면 목표를 끝내고 채팅으로 알린다. 광석 후보는 덩어리(3칸 안)마다 하나, 돌고 있는 굴의 목표 근처는 새 굴로 내지 않는다. 탐험은 걷기와 파기를 늘 같이 내고 둘 다 길 칸 수·부술 벽·추정 타격·예상 시간·새로 보일 구역을 적는다(판단 모델이 고름, 진행 중 굴이 있으면 새 탐험 굴은 안 냄). 벽 캐기는 시야 안 걸어서 닿는 칸(ReachableInView) 옆 벽만 내고, 닿지 않는 목표 광석은 파 들어가기로 낸다. 후보 설명에 대상·거리·체력·추정 피해와 감소·나올 품목·모르는 것을 적는다. 진행 중 기술과 같은 대상은 빼고, 30초 안에 두 번 실패한 대상은 30초 동안 빼며 한 번 실패한 대상에는 실패 이유를 붙인다. 모으기 목표인데 진행시킬 후보가 하나도 없으면 이유를 붙여 목표를 멈춘다. 기다리기만 남으면 호출 없이 기다리고 5번 연속이면 목표를 멈춘다. 화면 안에 새로 보일 곳이 없으면 탐색 기억에서 아는 길로 가장 싼 "기억한 탐색 끝"을 방향마다 찾아 둘까지 낸다(AITunnel.FrontierTargets). 계속하기 설명에 기술의 진행(`Progress`: 남은 거리·벽·체력)과 "다른 행동을 고르면 이 일은 여기서 멈추고 새 행동을 처음부터 시작함"을 적는다. 새 적 계기에는 거리와 "아직 8칸 밖"을 적는다. 줍기 후보에는 게임 줍기 상태(Claude 쪽으로 날아오는 중)와 자동 줍기 거리(AutoPickUpItemCD.pickupDistance, 기본 1.4칸) 안인지를 적고, 이미 다 주워진 아이템은 땅에 없는 것으로 본다.
- 관측 지점: Player.log "DECISION #n SEND … candidates=k | c1 … | c2 …", "BRAIN: left out after repeated failures"
- 정책: pol.behavior-roles, pol.combat-options, pol.vision-human-view, pol.explore-dig-by-model, pol.explore-end-when-nothing, cand.v2-design, cand.fail-memory
- 코드 앵커: AIBrain.BuildCandidates, AIBrain.ExploreTargets, AIBrain.ReachableInView, AIBrain.TryOreObject, AIBrain.BuildInput, AIBrain.PlanC, AIBrain.FrontierJobDone, AICandidateAssembler.Select, AICandidateAssembler.KeyLabel, AICandidateAssembler.EntityOrder, AICandidateAssembler.CombatOrder, AIBrain.CandBuild.Prep, AIBrain.CandBuild.Continue, AIBrain.CandBuild.Enemies, AIBrain.CandBuild.CombatOptions, AIBrain.CandBuild.Food, AIBrain.CandBuild.Walls, AIBrain.CandBuild.DigOre, AIBrain.CandBuild.Breakables, AIBrain.CandBuild.Roots, AIBrain.CandBuild.RememberedOres, AIBrain.CandBuild.Items, AIBrain.CandBuild.Follow, AIBrain.CandBuild.Help, AIBrain.CandBuild.Explore, AIBrain.CandBuild.Assemble, AIBrain.FightNote, AIBrain.KillSeconds, AITunnel.FrontierJob.FrontierJob, AITunnel.FrontierJob.Step, AITunnel.FrontierJob.Cost, AIGoalFeatures.AddCandidates, AIPickupOptions.Select, AIWallData.RiskText, AIWallRisk.Text, AIWallRisk.RouteText, AIMoveEstimate.Seconds
- 근거: work/2026-11/2026-11-06-선택지개선/06-live-evidence-review.md "제공된 대표 증거는 수정판의 폭발 위험 경고 전달과 정상 선택→실행 연결을 뒷받침한다."
- 메모: b25099f의 INPUT #64/#116에서 자연 폭발벽 위험문구가 모델에 도달했고 일반 흙 경로와 구분됨. ETA 계산 기준 및 대표 선택→실행을 확인. 필요한 줍기 경계와 직접 캐기/계속하기/본·대안 위험은 오프라인·코드 검수 범위다. 과거 확인한 선 전체를 승격하지 않는다.
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "`BRAIN: left out after repeated failures (for 30s): mine:-30,-14(다가가기 실패: 길 없음…)`"
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "DECISION #9 SEND rev=3 trigger='새 적 보임(MushroomBrute 11.0칸)' ... | c1 계속하기 — 지금 하는 '캐기'(mine:-13,-25)을 3초째 진행 중. 그대로 계속."
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "#2 -> 계속하기 — 지금 하는 '파들어가기'(dig:-80,24)을 2초째 진행 중. 그대로 계속."
- 근거: work/2026-11/2026-11-04-이동-드득거림.md "고친 뒤 `jitter2`~`jitter4` 20번 중 19번 계속하기"
- 근거: work/2026-11/2026-11-04-견고성-정리.md "t=155.1 BRAIN: nothing new within one screen; frontier search: expanded 3073 tiles in 42 ms"
- 근거: work/2026-11/2026-11-04-견고성-정리.md "결과: 전 2번 중 2번 고르고 버려짐. 후(robust5 빌드) 날아오는 아이템이 후보에 보인 판단 3번 모두 고르지 않음"
- 메모: 2026-11-04 전투안정 작업: 선택값이 자리 번호(c1…)에서 대상 키(attack_123, retreat_0_-1 …)로 바뀌었고, 돌고 있는 행동은 따로 첫 줄이 아니라 제자리에서 "계속하기"로 나온다. 탐험 걷기는 같은 방향이면 계속하기로만, 탐험 파기 목표는 4칸 격자, 기억한 탐색 끝 찾기는 프레임마다 나눠 도는 작업(AITunnel.FrontierJob)과 결과 재사용, 굴 계획은 1.5~2초 재사용(PlanC). 공격 후보에 FightNote(처치 시간, 받은 피해·간격, 한 번에 쓰러진 기록, 근처에서 쓰러진 수, 처치 대 쓰러짐 시간 비교). 확인한 선은 이 변경 뒤로 다시 적지 않음.
- 메모: 파 들어가기·탐색 기억 후보는 F.skill-dig, F.explore-memory에서 만든다. 2026-11-03 저녁 최종 빌드로 다시 확인했다. 2026-11-04 견고성 정리에서 기억한 탐색 끝·줍기 사실·기억한 제작대로 가기를 더해 다시 확인.
- 근거: work/2026-11/2026-11-04-다수적-긴급전환.md "같은 움직임의 다른 키 11 → 최종 0"
- 메모: 2026-11-04 다수적-긴급전환: 탐험 걷기가 도는 중에는 기억한 탐색 끝 후보를 내지 않음("새로 보일 곳 없음"이 틀린 정보였음), 도는 걷기 목표와 4칸 안의 다른 걷기 후보도 내지 않음.


- 메모: 2026-11-04 막힘-멈춤-정리(커밋 039d8b5): 몸이 못 하는 후보는 내지 않고 까닭을 막힌 대상에 적는다 — 곡괭이·빈손을 만들 수 없으면 캐기·벽 있는 파기·뿌리(무기는 공격·부수기), 가방에 자리가 없으면 떨어진 물건 줍기. 곡괭이가 가방에만 있으면 손 칸으로, 손 칸이 차면 하나를 가방으로 옮겨 빈손(AIBody.HandSlot). 싸움을 끊는 하나는 progress·할 수 있음·곧 빠지지 않음인 목표 일만.
- 근거: work/2026-11/2026-11-04-막힘-멈춤-정리.md "캐기·파기 8개 뺌: 곡괭이가 없고 손 칸 10칸이 모두 차 있으며 가방에도 빈 칸이 없어 맨손으로도 캘 수 없음(가방이나 손 칸을 비우면 됨)"
- 근거: work/2026-11/2026-11-04-막힘-멈춤-정리.md "`줍기 — 떨어진 CopperMiningPick` **0번**"
- 근거: work/2026-11/2026-11-04-막힘-멈춤-정리.md "**줍기 0**, 목표 아닌 채우기 캐기 0"
- 메모: 2026-11-04 리팩토링(3c3adbe→e82eb87, 동작 유지): 후보 생성은 `AIBrain.Candidates.cs`의 중첩 생성기 `CandBuild`(빌드마다 새로, 블록 함수 Prep·Continue·Enemies·CombatOptions·Food·Walls·DigOre·Breakables·Roots·RememberedOres·Items·Follow·Help·Explore·Assemble)로, 마지막 조립(묶기·선택값·몸 거르기·실패 표시·개수 제한·기다리기·값)은 Unity 없는 `Core/AICandidateAssembler.cs`(`Select`, `KeyLabel`, `EntityOrder`, `CombatOrder`)로 옮겼다. `BuildCandidates`가 `b.X()`로 블록을 부르는데 도구가 인스턴스 호출을 따라가지 못해 블록을 앵커로 적었다. 조립기는 옛 코드 기록 854/854와 같다(core-tests 재생). 판단 준비 일부는 `AIBrain.Prep.cs`(WantedItems·NextPickTier).
- 메모: 2026-11-05 전투 단계 2(fc8161b): 조립기의 개수 상한이 `AICandidateAssembler.LimitFor(combat, combatMax)`로 바뀌었다. 전투(적이 8칸 안)는 자르지 않고(설정 `combatMaxCandidates`, 기본 0 = 무제한) 비전투는 14개+기다리기 그대로다. 조립기 재생 시험은 상한 14로 옛 기록 854/854, 기본 설정에서 비전투 418건이 옛 기록과 같다. 버섯 야수 근접전 후보 블록은 `CandBuild.Brute`(F.combat-short-options). 상한 없음은 pol.combat-cap-none.
- 근거: work/2026-11/2026-11-05-전투-단계2/7-R1.md "조립기 재생: 상한 14로 옛 기록 **854/854** 같음, 기본 설정 비전투 **418** 같음."

### F.action-select — 현장 행동 고르기
- 상태: 구현됨·미확인
- 근거: work/2026-11/2026-11-04-전투-선택지.md "GOAL done rev=3: 구리 광석(CopperOre) 3개 모으기 (4/3) after 41.4s, calls this session 45"
- 종류: 제품
- 한 줄: 685dde4의 현장 판단은 수량 충족·실행 시간·무진척·호출 실패를 관측으로 전달하면서 유효한 현재 행동을 유지한다. AI가 stop_action·finish_goal·request_plan을 명시 선택한다. 응답 나이만으로 버리지 않고 목표·대상·실행 회차에 맞는지 검사한다. 명시 중단은 문닫기 후속 동작과 과거 예약까지 정리한다. 호출·비용 상한은 새 호출에 적용한다.
- 메모: 이전 구현 설명은 당시 기록이다. OpenAI Decisions(`POST /v1/decisions`, gpt-6-luna, choice 질문 하나)가 후보 하나를 고르고 맞는 기술을 시작한다. 요청은 캐릭터당 하나, `UnityWebRequest`로 게임 스레드를 막지 않는다. 기술이 없을 때(끝난 직후) 바로 부르고, 기술이 도는 중에는 새 적이 보이거나 체력이 15 넘게 줄 때만 최소 2.5초 간격으로 부른다. 분당 30회 상한. 응답은 목표 수정 번호·대상 유효성·나이(8초)를 보고 어긋나면 버린다. 실패는 3초 뒤 다시, 3번 연속이면 목표를 멈춘다. AI가 쓰러져 있으면 부르지 않는다. 비용은 응답 토큰으로 세어 월 상한(설정 파일 200달러, 코드 기본 70달러)에 닿으면 멈춘다. 후보가 기다리기 하나뿐이면 부르지 않는다(choice 질문은 선택지가 둘 이상이어야 해서 400이 난다). 키는 OPENAI_API_KEY(프로세스 또는 사용자 환경 변수)에서 읽고 쓰지 않는다. 기술이 스스로 끝나기 0.4초 전(`EtaSeconds`)이면 "곧 끝남"으로 한 번 미리 묻고(계속하기·곧 부술 벽은 빼고), 답은 그 기술이 완료로 끝날 때만 다시 유효성을 보고 쓴다(실패·취소·다른 판단이면 버림). 고를 후보가 없으면 미리 묻지 않는다. 전투 기술(공격·거리 벌리기·물러나기·먹기)도 관찰로 잴 수 있는 끝(마지막 한 대, 시간 상한, 도착, 먹은 뒤 1초)이면 미리 묻는다. 공격의 미리 묻기에서는 곧 죽을 그 적을 후보에서 뺀다.
- 관측 지점: Player.log "DECISION #n SEND|RESULT … APPLY|DISCARD", "DECISION #n SEND (prefetch)", "… QUEUED", "DECISION #n queued answer APPLY at skill end", "COST cap reached", "BRAIN: AI is down|back up"; AIPlayers/decisions-usage.json
- 정책: pol.selector-openai, pol.behavior-roles, pol.fewer-calls, pol.call-interval, pol.cost-ceiling, pol.cost-cap-200, pol.decision-chat-off, pol.combat-options, cand.score-first, cand.v2-design, cand.cost-cap-70, pol.choice-no-cut
- 코드 앵커: AIBrain.Tick, AIBrain.Send, AIBrain.HandleResponse, AISkill.EtaSeconds, AIDecisionsApi.Post, AIDecisionsApi.Read, AIDecisionsApi.ParseAnswerFallback, AIBrain.StaleReason, AIBrain.Apply, AIActions.Make, AIActions.Stale, AIActions.MineStale, AIActions.AttackStale, AIActions.PickupStale, AIActions.AwayStale, AIActions.EatStale, AIActions.DigStale, AIActions.ContinueStale, AIBrain.CallFailed, AIBrain.OnSkillEnded, AIBrain.Reset, AIBrain.BuildJson, AIDecisionsApi.ApiKey, AIDecisionsApi.Settings, AIDecisionsApi.AddUsage, WaitSkill.Start, WaitSkill.Tick, AIInventory.Stale, AIInventoryMenu.Candidates, AIInventoryMenu.Stale, AIBrain.InventoryChoiceSurface, AIGoalFeatures.AddCandidates, AIInventory.Candidates, AIInventory.CandidatesFor
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "DECISION #1 RESULT choice=c3 (캐기 — 벽 WallDirtBlock (0,-18) 1.3칸 ...) top=[c3=0.93, c4=0.03, c5=0.02] confidence=0.92 latency=0.27s tokens=1085 cached=0 cost=$0.000109 APPLY"
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "| 지연 | 중앙값 **0.22초**, p95 0.27초(1차, 74회) · 0.63초(2차, 49회), 최대 0.81초. 첫 빌드에서 첫 호출 하나 2.04초. |"
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "SKILL 캐기 DONE after 12.8s: reason='벽 (-13,-25) 부숨 · 가방 CopperOre +1 · 땅에 남은 것 WallDirtBlockx1 d=0.6'"
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "상태: 이번 접속 호출 16(적용 16, 버림 0, 실패 0) · 지연 중앙값 0.22초 p95 0.34초 · 이달 298회 $0.0257 / 상한 $200"
- 근거: work/2026-11/2026-11-04-이동-드득거림.md "미리 묻기: 최종 빌드 탐험에서 15번 모두 사용(`DECISION #13 queued answer APPLY at skill end (0.51s after asking)` 등)"
- 근거: work/2026-11/2026-11-04-견고성-정리.md "| 후(robust4·robust5 빌드 합) | 29 | 0.2초 | 0.21초 |"
- 메모: 2026-11-04 리팩토링(3c3adbe→e82eb87, 동작 유지): `AIBrain`이 partial 넷(AIBrain.cs 상태·전달, AIBrain.Loop.cs Tick·Send·HandleResponse·ApplyOrHold·BuildInput, AIBrain.Candidates.cs, AIBrain.Prep.cs)으로 나뉘었다. Decisions 호출·응답 읽기·설정·사용량·키는 `AIDecisionsApi.cs`. 행동 종류별 기술 만들기와 오래됨 검사는 `AIActions.cs` 표 한 줄(Make·Stale, 21종). 표의 `*Stale` 함수는 대리자로 불려 앵커로 적었다. 보류·긴급 규칙(ApplyOrHold·TryApplyHeld)과 `IsMove/IsMoveDesc` 문장 판정은 아직 표 밖이다(U.combat-rules-outside-table).
- 메모: 응답은 지금 모두 대체 리더로 읽는다(U.json-fallback). 오래된 응답 버림, 쓰러진 동안 멈춤, 비용 상한 멈춤은 실게임으로 아직 못 봤다(U.decisions-unseen).

- 메모: 2026-11-04 전투 다듬기 2(커밋 15e3744): 평소 분당 상한(30)은 평소에 한 호출만 센다(AIBrain.CallRateOk, _peaceCallTimes). 전에는 싸움 중 호출까지 세어 싸움 직후 서 있던 AI가 최대 1분 판단을 못 했다. 아무 기술도 돌지 않을 때 0.25초마다 8칸 안 적을 봐서 _inCombat을 바로잡는다. 새 탐험 목표의 탐색 끝 찾기가 끝나면 자리 채움 기다리기를 멈추고 바로 묻는다.
- 근거: work/2026-11/2026-11-04-전투-다듬기/2-측정-원인-동료.md "목표 → 첫 판단 0.1초·0초"
- 근거: work/2026-11/2026-11-04-전투-다듬기/2-측정-원인-동료.md "goal@1127.4 DOWN before any SEND @1139.4"
- 메모: 2026-11-04 넷이 함께 플레이(커밋 44d21d0): 모으기·제작 멈춤 판정이 뒤에서 도는 탐색 끝 찾기(S._fPending)를 기다린다. 굴 계획 캐시(AIBrain.PlanC)를 상자·기지·기억한 제작대 길도 쓰고, 싸움 밖에서는 5초 둔다(싸움 중 2초).
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "frontier search: expanded 0 tiles in 1 ms, found 0"
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "굴 계획 하나 약 16 ms, 한 번에 8~10개."
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "trigger='탐색 끝 찾기 끝남(기억한 탐색 끝 후보가 생김)'"
- 메모: 2026-11-05 전투 단계 2(fc8161b): '계속' 답은 요청 때 돌던 실행 번호에만 적용한다(F.short-actions). 버린 답(늦음·이미 거리 확보·끝난 실행에 대한 답·대상 없음)은 실패 기억에 넣지 않는다(비전투에도 적용되는 동작 변화). 호출 실패는 전투 중에만 원인별로 다시 묻고(거절·빈 답 0.25초, 네트워크·서버·시간 초과 1초, 속도 제한·요청 오류 3초) 거절·빈 답은 3번 연속 멈춤에 세지 않는다. 비전투는 3초·3번이면 멈춤 그대로이며 실게임으로 재현하지 못했다(U.fail-stop-3-live). 답 집계는 받음·시작·계속·다음 휘두르기 대기·보류(대체·폐기)·버림·호출 실패로 나눴고 `Applied`는 실제로 시작·계속한 답만 센다.
- 근거: work/2026-11/2026-11-05-전투-단계2/5-검수반영·회귀.md "목표 구간 버린 답 36(계속할 기술이 이미 끝남 28, 계속하라던 행동이 이미 바뀜 5, 이미 거리 확보 3)"

- 메모: 2026-11-07 가방·장비 격리 후보: 가방·장비 후보는 구체 슬롯/개체 스냅샷을 담고, 적용 시 재검사한다. 이번 추가 경로는 실게임 미확인이다.

### F.skill-dig — 막힌 대상 쪽으로 파 들어가기
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 관측하거나 기억한 대상까지 기존 길 찾기와 벽 채굴을 연결한다. 30초 무진척·300초 경과는 관측으로 전달하고 진행 가능한 선택을 유지한다. 실제 하위 실행 실패는 위치·이유·진척을 포함해 모델에 반환한다.
- 메모: 이전 구현 설명은 당시 기록이다. 보이는(또는 기억한) 대상까지 걸을 길이 없을 때, 지금 보이거나 전에 본 칸만 써서 가장 싼 길을 찾는다(걷는 칸 1, 벽은 1 + 2×추정 타격 수, 추정 피해가 벽 감소량 이하인 벽은 피하고 "도구 부족"으로 센다). 그 길의 다음 벽까지 걸어가 캐고, 한 걸음마다 다시 계획한다. 광석 벽이면 그 벽을 부수면 끝, 걸을 칸이면 도착하면 끝, 적이면 길이 열리면 끝(공격은 따로 고른다). 같은 칸에서 두 번 실패, 30초 진전 없음, 300초면 실패. 판단 층은 "파 들어가기"(보이는 목표 광석 2·떨어진 아이템 1·적 1), "기억한 목표 광석 쪽으로"(2), "파 들어가며 탐험"(1)을 후보로 낸다. 시험 명령 `/ai dig x z`. 굴 입구로 걸을 때 곡괭이를 미리 들고, 중간 벽은 떨어진 것을 기다리지 않는다. 진행 설명(대상까지 거리·남은 벽·뚫은 벽·지금 단계)과 마지막 단계의 남은 시간을 판단 층에 준다.
- 관측 지점: 채팅 "AI 파들어가기 완료|실패: …"; Player.log "SKILL 파들어가기: plan|step|child", "SKILL 파들어가기 DONE|FAILED"
- 정책: pol.dig-and-explore-first, pol.body-progress, pol.vision-human-view, pol.realtime-by-program
- 코드 앵커: TunnelSkill.Start, TunnelSkill.Tick, TunnelSkill.Cleanup, TunnelSkill.Progress, TunnelSkill.EtaSeconds, AITunnel.Plan, AITunnel.PlannedDamage, AITunnel.RecordHeld, AITunnel.Heap.Push, TunnelPlan.WallSummary, TunnelPlan.RiskText, AIWallData.RiskText, AIWallRisk.Text
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "SKILL 파들어가기 DONE after 10.6s: reason='목표 벽 (-26,-21) 부숨 · 벽 5개 뚫음 · 가방 WallDirtBlock +5, CopperOre +1'"
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "GOAL done rev=1: 구리 광석(CopperOre) 2개 모으기 (2/2) after 16.0s, calls this session 3"
- 근거: work/2026-11/2026-11-04-이동-드득거림.md "다음 휘두르기 전 서 있음 127번 평균 0.11초"
- 메모: 계획은 타일만 본다(물체·문은 모른다, U.astar-objects와 같은 한계). 곡괭이를 아직 한 번도 들지 않았으면 캐기 보너스를 몰라 맨손 기준으로 추정한다.
- 메모: 2026-11-04 넷이 함께 플레이: 걷기 단계가 같은 곳에서 두 번 실패하면 실패 글에 AI가 서 있던 칸을 적는다(전에는 걷기 구간 끝점만 적어 AI가 거기 있는 것처럼 읽혔다).
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "(-61,52)에 서서 (-35,50) 쪽으로 가다가 같은 곳에서 두 번 실패"

### F.explore-memory — 탐색 기억(본 곳·서 본 곳·본 광석·제작대·배운 떨굼)
- 상태: 실게임 확인
- 확인한 선: b4dbe8076311
- 종류: 제품
- 한 줄: 1초마다 시야(AIVision) 안 4×4칸 구역을 "본 곳", 서 있던 구역을 "서 본 곳"으로 적고, 시야 안 광석 벽을 시각과 함께 적는다. 본 광석 칸을 다시 봤는데 광석이 없으면 지운다. 월드마다 `AIPlayers/<월드 ServerGuid>/memory.json`에 30초마다·떠날 때 저장하고 다시 들어오면 읽는다. 탐험 후보는 시야 안 걸어서 닿는 칸 중 주변에 아직 못 본 구역이 있는 칸만, 방향마다 가장 많은 곳으로 낸다(못 본 구역이 없으면 내지 않음). 게임 길찾기가 길이 없다고 한 탐험 목표 주변 5칸은 90초 동안 뺀다. 기억한 광석은 "N분 전에 봄, 지금 상태는 모름"으로만 후보에 넣는다. 시야 안 제작대(종류·칸·본 때·Claude가 놓았는지)도 적고, 기억한 칸이 보이는데 제작대가 두 번 연속 없으면 잊는다. 뿌리·부술 물체에서 나온 것(품목별 횟수·합)과 안 부서진 물체(종류·칸·시각)도 같은 파일에 남겨 다시 켠 뒤에도 쓴다. 화면 안에 새 곳이 없을 때 쓸 "기억한 탐색 끝"을 이 기억에서 찾는다. 시험 명령 `/ai mem`.
- 관측 지점: Player.log "MEMORY loaded|summary|… seen gone", "BRAIN: no path to … left out"; 판단 입력의 "탐색 기억(이 월드): …"; `AIPlayers/<월드>/memory.json`
- 정책: pol.dig-and-explore-first, pol.vision-human-view, pol.ai-per-world
- 코드 앵커: AIMemory.Observe, AIMemory.Save, AIMemory.Novelty, AIMemory.RememberedOres, AIMemory.CountOres, AIMemory.Summary, AIMemory.Ago, AIMemory.SeenTile, AIBrain.ExploreTargets, AIBrain.ReachableInView, AIBrain.CandBuild.Explore, AIBrain.CandBuild.RememberedOres
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "MEMORY loaded for world b39072f4c49d28bf6ab43b7b6a702d7f: seen cells 192, visited cells 35, remembered ores 6"
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "c1 기억한 목표 광석 쪽으로 — CopperOre (4,-16) 16.4칸, 1분 전에 봄(지금은 화면 밖이라 지금 상태는 모름)."
- 근거: work/2026-11/2026-11-04-견고성-정리.md "t=702.1 MEMORY: station WoodenWorkBench at (2,-4) seen gone; forgotten (last seen 15초 전, placed by the AI)"
- 근거: work/2026-11/2026-11-04-견고성-정리.md "다시 켠 뒤 `MEMORY loaded … learned drops [bigRoot: 관찰 5번: Wood 5번(합 8개)]`"
- 메모: 기억은 4×4칸 구역 단위다. 본 곳의 지금 타일 상태는 계획할 때 서버에서 다시 읽는다(본 뒤 바뀐 것을 안다는 근사, U.memory-tiles-live).

### F.ai-weapon — 기본 무기를 갖고 무기를 들고 싸운다
- 상태: 실게임 확인
- 확인한 선: 71ed72e36b9f
- 종류: 제품
- 한 줄: 기본 무기는 게임 자료에서 고른다(초반 검 가운데 근접 무기·제작법 있음·레벨 최저·재료 최소 = WoodSword). 새 AI 캐릭터의 시작 지급품에 넣고, 무기가 없던 기존 AI에게는 들어온 뒤 한 번 서버 가방에 넣는다(표시 파일 `<캐릭터>.weapon-granted`). 공격 기술은 손 칸의 무기를 들고, 가방에만 있으면 손 칸으로 맞바꿔 넣는다(빈 손 칸, 없으면 곡괭이·무기가 아닌 마지막 손 칸). 무기가 없을 때만 맨손. 공격 후보 설명에 무기를 적는다.
- 관측 지점: Player.log "AI character: starter weapon choice|granted starter weapon|has a weapon already", "SKILL 공격: swapped …"; 채팅 "AI: 기본 무기 …"
- 정책: pol.weapon-default, pol.body-fight, pol.start-kit-role-only
- 코드 앵커: AICharacterStore.ChooseStarterWeapon, AICharacterStore.EnsureWeaponOnce, AICharacterStore.CraftableAt, AIBody.WeaponSlotForAttack, AIBody.FindWeapon/2, AIBody.FindWeapon/3, AIBody.SlotBroken, AIVitals.HitKey
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "starter weapon choice WoodSword (WoodSword: type=MeleeWeapon level=0 rarity=Common recipe=[Woodx5];"
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "granted starter weapon WoodSword (amount 88) into slot 10 (one time; marker …\Claude.weapon-granted)"
- 근거: work/2026-11/2026-11-03-파들어가기-탐색기억.md "`/ai attack`(기술 직접, 판단 모델이 그때 공격 대신 탐험을 골라서): `swapped WoodSword from bag slot 10 into hotbar slot 9 (moved Mushroom to slot 10)`"
- 메모: 새 캐릭터 시작 지급품의 무기는 실게임으로 아직 못 봤다(F.ai-char-load가 "확인 뒤 코드 바뀜"). 무기를 잃었을 때 다시 주지 않는다(제작은 나중 기능).
- 근거: work/2026-11/2026-11-04-다수적-긴급전환.md "관찰한 WoodSword(부서진 상태) 피해 약 9/타, 타격 간격 약 0.4초 → 약 12타"
- 메모: 2026-11-04 다수적-긴급전환: 내구도 0(부서진) 무기는 성한 무기보다 뒤에 고르고(가방의 성한 무기를 그 손 칸으로), 타격 관찰값을 부서진 상태와 따로 모으며(HitKey), 후보에 "내구도 0(부서짐)"을 적는다. 성한 무기를 가방에서 꺼내는 경로는 실게임에서 안 봤다.


### F.combat-options — 전투 선택지(거리 벌리기·물러나기·먹기)와 전투 중 다시 판단
- 상태: 실게임 확인
- 확인한 선: b587fa481dfb
- 근거: work/2026-11/2026-11-04-이동-드득거림.md "거리 벌리기 여러 번(최종 빌드)"
- 종류: 제품
- 한 줄: 적이 보이면 판단 후보에 공격(적별) 말고도 거리 벌리기(4.5칸 안의 적에게서 5칸, 최대 3초), 코어 스폰 자리로 물러나기(열린 아는 길, 멈춤 조건 있음), 먹기(게임 자료로 확인한 회복 아이템, 체력이 가득이 아닐 때)를 낸다. 후보마다 적 종류·체력·거리, 내 체력, 관찰한 처치 추정·받은 피해·거리 벌리기 성공률, 모르는 것은 모른다고 적는다. 적이 8칸 안이면 기술이 도는 중에도 앞 답이 오자마자 다시 판단(바닥 0.25초, 분당 240회, 2026-11-04 전에는 0.8초·60회), 체력 5 감소·새 적(공격 중에도)·싸우던 적 사라짐에 다시 판단한다. 같은 행동을 다시 고르면 이어서 계속한다. 프로그램은 스스로 피하거나 물러나지 않는다. 목표가 없어도 맞거나 적이 8칸 안에 오면 "자기 방어" 일화로 같은 판단을 하고(전투 후보와 기다리기만), 위협이 3초 없으면 호출 없이 제자리로 돌아간다.
- 관측 지점: Player.log "DECISION #n SEND … 거리 벌리기|물러나기|먹기 …", "SKILL 거리 벌리기|물러나기|먹기 DONE|FAILED", "BRAIN: chose the running action again"
- 정책: pol.combat-options, pol.behavior-roles, pol.reflex-minimal-now, pol.vision-human-view, pol.self-defence, pol.combat-rate-max, pol.combat-choice-design, cand.v2-design, pol.combat-no-auto-dodge, pol.combat-kill-brute
- 코드 앵커: AIBrain.SelfDefence, AIBrain.CandBuild.CombatOptions, AIBrain.CandBuild.Food, EatSkill.Start, EatSkill.Tick, EatSkill.Count, EatSkill.EatSkill, KeepAwaySkill.Start, KeepAwaySkill.Tick, KeepAwaySkill.Cleanup, KeepAwaySkill.Progress, KeepAwaySkill.KeepAwaySkill, KeepAwaySkill.EtaSeconds, AttackSkill.EtaSeconds, EatSkill.EtaSeconds, RetreatSkill.EtaSeconds, RetreatSkill.Start, RetreatSkill.Tick, RetreatSkill.NextLeg, RetreatSkill.Cleanup, RetreatSkill.RetreatSkill, AIItems.ToHotbar, AISkill.Progress, AttackSkill.Progress
- 근거: work/2026-11/2026-11-04-전투-선택지.md "#3 -> 먹기 — HealingPotion(즉시 체력 +30%), 가방에 1개. …        먹기 DONE 1.5s HealingPotion 먹음 · 체력 31→60"
- 근거: work/2026-11/2026-11-04-전투-선택지.md "#2 -> 거리 벌리기 — 적 SlimeBlob(1.0칸)에게서 5칸 이상 …          거리 벌리기 DONE 3.0s 3초 끝, SlimeBlob와 3.7칸"
- 근거: work/2026-11/2026-11-04-전투-선택지.md "SELF-DEFENCE #1 end: no enemy within 8 tiles and no damage for 3s; 6.5s, calls 4, hp 60->46, downs 0"
- 근거: work/2026-11/2026-11-04-견고성-정리.md "실게임: 네 기술 모두 미리 묻기가 나감"
- 근거: work/2026-11/2026-11-04-멤버넷-캐릭터.md "@Claude COMBAT #2 end (적이 범위 밖): 2.1s, calls 6 (172/min), answers 6, latency p50 0.30s p95 0.65s max 0.65s, tokens 6208 ($0.00062, $0.01776/min)"
- 메모: 2026-11-04(사용자 요청, HEAD 전달) 전투 중 다시 판단을 최대 속도로: 적이 8칸 안이고 기술이 도는 중이면 앞 답이 오자마자 다시 묻는다(캐릭터마다 한 번에 하나, 바닥 0.25초, 분당 240). 관측 지점에 Player.log "COMBAT #n start|end"와 trigger "전투 중 계속 다시 판단"이 더해졌다. 정책은 pol.combat-rate-max(2026-11-04 승격). 바꾸기는 동작 경계에서(F.motion-boundary). 2026-11-04 전투안정 작업: 선택값을 대상 키로, 돌고 있는 행동은 제자리 계속하기, 거리 벌리기·물러나기에 언제 좋은지, 공격 후보에 FightNote(F.candidates 메모).
- 메모: 관측 지점에 Player.log "SELF-DEFENCE #n start|end"와 채팅 "AI 자기 방어 시작|끝"이 더해졌다. 2026-11-04 견고성 정리에서 전투 기술의 남은 시간(EtaSeconds)을 넣음.
- 근거: work/2026-11/2026-11-04-다수적-긴급전환.md "보이는 적 모두에게서 5칸 이상 떨어지는 칸으로. 3초 동안 거리를 유지하며 적이 다시 다가오면 다른 칸을 고른다"
- 메모: 2026-11-04 전투 다듬기(다수적-긴급전환 2부 14장, 커밋 2678a64): 목표가 있고 가까운 싸움(4.5칸 안 적 또는 6칸 안 2마리 이상, AIBrain.CloseFight)이면 전투 묶음 + 목표 쪽 선택지 하나 + 기다리기만 낸다. 시험 스위치 `closefight.off`로 끔(F.test-switch). 효과 측정(A/B)은 아직.
- 근거: work/2026-11/2026-11-04-전투-다듬기/1-정리-계속불일치.md "| 켬(t≈165~215) | 45 | 26 | 0 | 1 |"
- 메모: 2026-11-04 다수적-긴급전환: 거리 벌리기는 싸움 하나에 후보 하나(away:near, 적마다 아님), 3초 유지. 움직이는 후보에 적과의 방향(AIBrain.Toward)과 지금 움직임과 같은 쪽인지(AIBrain.SameWay), 입력에 여러 적 요약(AIBrain.SwarmNote), 싸움 중 20초마다 판단 입력 전문 로그(DECISION #n FULL).


- 메모: 2026-11-04 전투 다듬기 2 A/B(시험장 24판): 가까운 싸움 묶음 켬/끔의 맞지 않는 바꾸기 1.84 대 1.90/20초로 구별 안 됨, 기본 켬 유지(코드 주석). 목표 쪽 하나를 진척 후보로 바꾸는 고침은 2.50 대 2.29로 나아지지 않아 되돌림(U.breakoff-pickup).
- 근거: work/2026-11/2026-11-04-전투-다듬기/2-측정-원인-동료.md "| 켬 | 12 | 38 | 239s | 6.6 |"
- 메모: 2026-11-04 넷이 함께 플레이(커밋 44d21d0): 가까운 싸움의 '싸움을 끊고 목표 계속하기' 하나에서 게임이 이미 끌어오는 줍기(날아오는 중·자동 줍기 거리 안, AICandidate.comesByItself)를 뺐다(U.breakoff-pickup 닫음, A/B 없이 정보 정정). 멀리 걸어가 줍는 것은 그대로.
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "저절로 들어오는 줍기(날아오는 중·자동 줍기 거리 안)는 0개."
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "| 싸움을 끊는 하나에 저절로 들어오는 줍기 | 0/1152 | 0/1035 |"
- 메모: 2026-11-05 전투 단계 2(ai-character fc8161b): 버섯 야수가 8칸 안에 보이는 근접전에서는 이 후보들에 돌진 관측(F.charge-watch)과 짧은·복합 후보(F.combat-short-options)가 더해지고, 공격·거리 벌리기 후보의 FightNote·AwayNote·TakenNote 대신 지금 한 타 예상 피해·시간·예상 결과 사실을 쓴다. 다른 적과의 싸움은 글자까지 그대로다. 전투 판단은 후보 수를 자르지 않는다(기본 무제한). 거리 벌리기는 그대로 `KeepAwaySkill(5f,3f)`이고 후보 목적지를 넘기지 않는다.
- 근거: work/2026-11/2026-11-05-전투-단계2/5-검수반영·회귀.md "새 관측·후보 0(10:13까지 비전투·약한 적 INPUT 499개 중"

### F.vitals — 쓰러짐 기록과 전투 관찰값
- 상태: 실게임 확인
- 확인한 선: 8e9fdb7add38
- 종류: 제품
- 한 줄: 목표와 상관없이 늘 체력·배고픔·위치·보이는 적을 0.5초마다 12초 동안 적어 두고, 쓰러지면 위치·체력 기록·근처 적·마지막 선택·원인을 로그·채팅·월드별 `deaths.log`에 남긴다. 무기별 타격 피해·간격, 적 종류별 받은 피해, 거리 벌리기 결과를 관찰해 월드별 기억 파일에 남긴다.
- 관측 지점: Player.log "VITALS: DOWN #n …", "VITALS: stood up again"; 채팅 "AI 쓰러짐 #n"; `AIPlayers/<월드>/deaths.log`
- 정책: pol.combat-options, pol.vision-human-view
- 코드 앵커: AIVitals.Tick, AIVitals.RecordHit, AIVitals.HitStats, AIVitals.TakenStats, AIVitals.HpChange, AIVitals.NoteChoice, AIVitals.NoteAte, AIVitals.NoteAway, AIVitals.ExportLearned, AIVitals.ImportLearned, AIVitals.Reset, AIItems.HealInfo, AIItems.HealingInInventory
- 메모: 2026-11-06 후보902721e 탐험 중 DOWN1/5.1초 뒤 부활을 관측했다. 피해원은 미확정이며 18-review-live-observation.md 한정 소스검토에서 새 리팩토링 회귀blocker 식별 근거를 찾지 못했다. 정상 결과/폭발 원인/안전 정책 통과로 판정하지 않는다.
- 근거: work/2026-11/2026-11-04-전투-선택지.md "VITALS: DOWN #1 at (-4.6,-19.1) hunger 11; health [12s ago 52, … 1s ago 49]; cause: 적 근처(마지막으로 본 적: MushroomEnemy 0.8 hp105, MushroomEnemy 0.8 hp105, SlimeBlob 9.0 hp105)"
- 메모: 쓰러짐 기록의 "running skill"은 몸 실행부가 먼저 기술을 끝내서 늘 "없음"이다.
- 근거: work/2026-11/2026-11-04-위쪽LLM-첫단계.md "VITALS: DOWN #1 at (-77.0,47.1) … cause: 적 근처(마지막으로 본 적: CavelingSkirmisher 4.3 hp243, …)"

### F.llm-goal — 위쪽 LLM(구독 CLI)이 다음 목표를 정함(첫 조각)
- 상태: 실게임 확인
- 확인한 선: 2d7a1083de58
- 종류: 제품
- 한 줄: `/ai goal 알아서`로 켜면 목표 완료·진행 불가·탐험 끝·쓰러졌다 일어남(목표 없거나 LLM 목표일 때)마다 짧은 구간 보고를 로컬 브리지(`Tools\ai-bridge`, 127.0.0.1:8331)로 보내고, 구독 Claude CLI가 답한 목표 하나를 모드가 검사해(보고 번호·목표 수정 번호·나이·품목·개수·호스트 목표 없음) `AIGoals.SetGoal`로 넣는다. 행동은 그대로 OpenAI Decisions가 고른다. 한 번에 하나, 기다리는 동안은 지금 목표·제자리·자기 방어. 호스트 `/ai goal`이 오면 늦은 답은 버린다. 브리지가 꺼져 있으면 한 번 알리고 자동을 끈다. 답의 say는 채팅에 `[Claude] …`.
- 관측 지점: Player.log "PLANNER: report #n sent|answer|APPLY|DROPPED", "bridge not reachable"; 채팅 "[Claude] …", "AI 목표(위쪽 LLM): …"; 브리지 `Tools/ai-bridge/logs/plan.jsonl`
- 정책: pol.llm-subscription, pol.behavior-roles, pol.vision-human-view, pol.fewer-calls, pol.refactor-json-fix-accepted, cand.refactor-head-accepted-diffs, cand.goal-end-order-kept
- 코드 앵커: AIPlanner.Enable, AIPlanner.Disable, AIPlanner.Tick, AIPlanner.Apply, AIPlanner.CheckReason, AIPlanProtocol.Read, AIPlanReport.ReportJson, AIPlanReport.Esc, AIPlanner.OnGoalEnded, AIPlanner.OnBackUp, AIPlanner.OnHostGoal, AIPlanner.AddEvent, AIPlanner.StatusText, AIPlanner.HealthProbe, AIPlanner.Post, PlanInbox.Put, PlanInbox.TryTake, PlanInbox.Clear, AIGoals.SetGoal/8, AIGoals.EndGoal, GoalSource.IsHost, GoalSource.IsLlm, AIGoalKinds.TryWire
- 근거: work/2026-11/2026-11-04-위쪽LLM-첫단계.md "PLANNER: report #3 answer DROPPED: 오래된 답: 목표 수정 번호가 바뀜(3→4)"
- 근거: work/2026-11/2026-11-04-위쪽LLM-첫단계.md "PLANNER: report #2 APPLY decision=set goal=follow  -1 why='주석 쪽(-77~-92,47~56)에서 강한 Caveling에게 3연속 쓰러짐. 호스트가 바로 옆이니 따라가며 안전 확보' after 4.3s"
- 근거: work/2026-11/2026-11-04-위쪽LLM-첫단계.md "PLANNER: bridge not reachable (Cannot connect to destination host) after 2.0s; auto off"
- 메모: 브리지(Node, `bridge.mjs`)는 그래프 코드 층 밖이다(C#만 읽음). 단톡방 adapters를 읽기 전용으로 import한다. LLM이 정한 따라가기는 3분이면 완료로 친다(재점검 전 임시).
- 메모: 2026-11-04 리팩토링(3c3adbe→e82eb87, 동작 유지), 브리지 S8: `bridge.mjs`는 설정·좌석·서버·시작 검사만 남고 셋으로 나뉘었다. `protocol.mjs`(응답 계약: 결정 형식 검사 `shape`, CLI 오류 낱말, 200 봉투, 오류 봉투 400·409·503·429·404·500), `preflight.mjs`(멤버마다 CLI·구독 로그인·사용량 한도·`/v1/health`), `plan.mjs`(`POST /v1/plan` 한 판단: busy → preflight → 기억 맥락 → 검색 여부 → recallLoop → 봉투 → 기록). 모두 그래프 코드 층 밖이다(위치만 적음). 모드 쪽 답 판독은 `Core/AIPlanProtocol.cs`(정식 JSON, `ExpJson.Parse`), 보고 JSON은 `AIPlanReport.cs`(Esc는 제어 문자를 공백으로, B1), 계기 받은편지함은 `Core/AIPlanInbox.cs`(`PlanInbox` 한 칸, 마지막 것이 이김). `AIPlanner.Post(owner, …)`와 `PlanInbox.TryTake`는 모드 안에서 부르는 곳이 없는 이음새다(자연어 목표 작업용, TryTake는 core-tests만 부름). 그래도 앵커로 둔 것은 그 작업에서 바뀔 자리를 이 선에 묶기 위해서다. 결정 종류 표가 모드 `CheckReason`·브리지 `shape`·`brief.md` 세 곳에 있다(U.decision-kinds-three).
- 근거: work/2026-11/2026-11-04-멤버넷-캐릭터.md "@Gemini GOAL set rev=7 source=llm:gemini#3: CopperOre(CopperOre) 5개 모으기 (start count 0)"
- 메모: 2026-11-04 멤버넷 작업부터 캐릭터마다 따로 돈다(보고 번호·요청·이벤트가 캐릭터별, `member`·`llm:<id>#n`·`[이름]`). 브리지는 멤버 넷(claude·codex·agy·grok)을 받고 멤버마다 구독 확인·사용량 85% 문턱·한 번에 하나. Gemini(agy) 세 바퀴를 실게임으로 봤고, GPT·Grok는 사용량 초과로 거절 경로만 봤다.
- 메모: 2026-11-04 넷이 함께 플레이(커밋 44d21d0): 보고에 hp_note(AIPlanner.HpNote: AI 플레이어 개체에 HealthRegenerationCD가 없으면 '저절로 차지 않음', 차는 길, 가방의 회복 음식, N분째 그대로). 위쪽 LLM 답의 오래됨은 AIBrain.GoalRevision(목표를 정하거나 지울 때만 오름)으로 판단 — 자기 방어 시작·끝의 Revision으로 답이 버려지던 것을 고침.
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "앞 시험의 hold 9번 되풀이는 다시 나오지 않았다."
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "Gemini 19번 중 6, Claude 13번 중 2"
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "Gemini 13번 보냄·13번 적용·버림 0"
- 메모: 2026-11-04 막힘-멈춤-정리(커밋 039d8b5): GPT 위쪽 LLM을 처음 실제로 불렀다(codex gpt-6.1-sol low, 7번 모두 적용, 답 6.7~11.4초, 보고에 hp_note·experience). 위쪽 LLM을 지금 못 쓰면 알아서 모드를 끄지 않고 다시 묻기를 잡는다 — 그 사이는 F.free-time.
- 근거: work/2026-11/2026-11-04-막힘-멈춤-정리.md "판 수: 두 세션에서 GPT 답 7번, **모두 적용, 버림 0**"
- 근거: work/2026-11/2026-11-04-막힘-멈춤-정리.md "GOAL free time ends after 130s: a goal came (llm:gpt#6)"
- 메모: 2026-11-05 자연어-명령(b2fa4cc): 알아서 모드만이 아니라 목표·계획이 있으면 늘 지켜보고(2분 주기·멘션·싸움 끝·쓰러짐·목표 끝, F.upper-watch), 호스트 목표 중에도 부르며 그 답을 버리지 않는다(pol.nl-host-order-defer). 답은 계획(F.plan-queue)·지시 상태(F.host-orders)·problem 칸(F.problem-report)을 담고, 결정 종류에 keep·talk-only가 더해졌다. 위 한 줄의 "호스트 목표 없음" 검사와 "늦은 답은 버린다"는 이제 맞지 않는다. 답 검사는 AIPlanner.CheckReason이다.

### F.free-time — 위쪽 LLM을 지금 못 쓸 때의 자유 시간
- 상태: 실게임 확인
- 확인한 선: 597afc7bece2
- 종류: 제품
- 한 줄: 알아서 모드가 켜져 있고 목표가 없는데 위쪽 LLM을 지금 못 쓰면(사용량 한도·login·auth·브리지 끊김·세 번 실패, 답을 20초 넘게 기다림, 다시 묻기 대기) 알아서 모드를 끄지 않고 다시 묻기를 잡으며, 그동안 목표 종류 Free로 자유 시간 후보 묶음(호스트·동료 따라가기, 호스트 쪽으로 가기, 동료 돕기, 가까운 곳 탐험, 기지에 가방 정리, 배고프면 먹기·배고픔 음식 줍기, 싸움, 기다리기)을 행동 판단기에 준다. 위쪽 LLM·호스트 목표가 오면 바뀌고, 목표 수정 번호는 오르지 않는다.
- 관측 지점: Player.log "GOAL free time rev=…", "GOAL free time ends after …", "PLANNER: upper LLM unavailable (…); auto stays on, asking again in …s", "PLANNER: report '…' kept for the retry"; 판단 기록 "set=free-time"; 채팅 "[이름] 위쪽 LLM을 지금 쓸 수 없음(…)"
- 정책: pol.behavior-roles, pol.llm-subscription
- 코드 앵커: AIGoals.FreeTime, AIPlanner.FreeTimeReason, AIPlanner.Down, AIBase.FreeStoreCandidates
- 근거: work/2026-11/2026-11-04-막힘-멈춤-정리.md "GOAL free time rev=2: 위쪽 LLM을 지금 쓸 수 없음(limit: usage 99% > 85%; 1800초 뒤 다시 물음)"
- 근거: work/2026-11/2026-11-04-막힘-멈춤-정리.md "t=214~287 판단 82번, 간격 평균 0.66초·최대 8초(서 있는 시간 없음)"
- 근거: work/2026-11/2026-11-04-막힘-멈춤-정리.md "상자에 넣기 DONE: 넣을 것을 모두 넣음 · 가방 WallDirtBlock -22, Wood -10"


### F.multi-ai — AI 캐릭터 여럿(단톡방 멤버마다 하나)
- 상태: 실게임 확인
- 확인한 선: 5c7f6c1e061e
- 종류: 제품
- 한 줄: 멤버(claude·gpt·gemini·grok)마다 자기 thin 월드·접속(127.0.0.1:7778+i)·캐릭터 파일·playerGuid·외형·뇌·몸·탐색 기억·쓰러짐 기록·위쪽 LLM 상태를 갖고 한 월드에서 동시에 움직인다. 모드는 캐릭터마다 `AIAgent.Enter` 범위 안에서 같은 static 코드를 돌리고, 캐릭터별 필드는 그 캐릭터의 State에서 읽는다. 채팅 명령은 `/ai <멤버> <명령>`, 멤버 없으면 기본 캐릭터(Claude가 들어와 있으면 Claude). 패킷 펌프는 목적지 포트로 캐릭터를 나눈다. 호스트 찾기는 어떤 AI도 아닌 플레이어.
- 관측 지점: Player.log 줄마다 "@이름"; "STEP 1 OK: … thinEndpoint=127.0.0.1:778x", "playerIndex=n"; 채팅 "[이름] …", "/ai" 한 줄 상태; `AIPlayers/<월드>/<id>/memory.json`·`deaths.log`; `decisions-usage.json`의 who 배열; "PERF 30s: AIs in world n"
- 정책: pol.ai-own-character, pol.ai-per-world, pol.ai-look-distinct, pol.vision-human-view, pol.body-thinclient, pol.multi-ai, cand.memory-per-character
- 코드 앵커: AIAgent.Enter, AIAgent.Find, AIAgent.ById, AIAgent.Get, AIAgent.Outside, AIPlayerSpikeMod.Update, AIPlayerSpikeMod.DisposeAll, AIPlayerSpikeMod.IsAiPlayer, AIPlayerSpikeMod.Pump, AIPlayerSpikeMod.TryHandleChatCommand, AIPlayerSpikeMod.DefaultAgent, AIPlayerSpikePatches.CharacterDataPostfix, AIMemory.CharDir
- 메모: 2026-11-06 후보902721e 추가 실게임: GPT/Gemini 목표 교체와 allstop을 관측했고 t659.4 이후 첫 process 끝까지 DECISION SEND0을 대조했다. 재입장 때 두 캐릭터가 기존 GUID로 자동 참여했다. 근거 work/리팩토링-20261106/15-unity-and-live.md §3.1–3.2. 모든 늦은 답/중단 경우의 통과는 아니다.
- 메모: 2026-11-04 리팩토링(3c3adbe→e82eb87, 동작 유지) S7: 캐릭터별 상태는 `AIAgent.Get<T>(AISlot.x)`로 꺼낸다(슬롯 16개, 클래스마다 하나, `AIAgents.cs`). 범위 밖 `AIAgent.Cur` 셈(`Outside`→`NoteOutside`)은 스위치 `testcmds.on` 뒤에서만 돈다(L2 0건). 새 상태 칸 하나에 State 필드와 전달 속성 두 곳을 고쳐야 한다(U.partial-forwarders).
- 게임 지점: Pug.Other/StartGameRPCSystem.cs:496 connectedPlayersLocal.Add(true);
- 게임 지점: Pug.Other/StartGameRPCSystem.cs:425 __this.characterDataMap[req.playerGuid];
- 근거: work/2026-11/2026-11-04-멤버넷-캐릭터.md "@Gemini AI identity: spawned AI player guid=3d1415c35a9774b67bec4596469f8b74 expected=3d1415c35a9774b67bec4596469f8b74 match=True"
- 근거: work/2026-11/2026-11-04-멤버넷-캐릭터.md "@Claude GOAL done rev=1: 구리 광석(CopperOre) 3개 모으기 (4/3) after 22.3s, calls this session 6"
- 근거: work/2026-11/2026-11-04-멤버넷-캐릭터.md "@Grok STEP 1 OK: SECOND PLAYER SPAWNED on server: entity=Entity(99388:153) … playerIndex=5 playerGuid=375e1b74c8ed8d179fb490083b4b6074 name='Grok'"
- 메모: 넷이 함께 약 5분, 둘 이상 약 10분 30초(ai2). 서버 20틱/초 유지, AI 하나 들어와 있을 때 프로세스 CPU +4~9%(코어 하나 기준), 넷이 일할 때 0명보다 +31~33%. 전투 다시 판단 속도는 F.combat-options. 확인한 선을 적지 않음: 넷 시험은 빌드 membernet1이었고 그 뒤 선 위의 AIDecisions.cs가 두 번 바뀌었다(why_ended 문구, 전투 속도). 마지막 빌드에서는 Claude 하나만 다시 봤다.
- 메모: 2026-11-04 넷이 함께 플레이 1차 17.4분(ai2, 단체 싸움 3판 포함): fps 중앙값 109.7(최저 102.8), 프레임 p99 중앙값 25 ms, 최장 308 ms, 서버 틱 최저 18.9, 판단 3104번 $0.6025. 동료를 공격 대상으로 한 후보 0.
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "판단 입력 3172개 모든 공격 후보의 대상 개체 번호를"
- 메모: 2차 13.6분(최종 코드와 문구 한 줄 차이): 서버 틱 최저 19.2, Claude 판단 준비 p99 155.7 ms, 판단 3454번 $0.6513.
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "0번(입력 6658개 모든 공격 후보를 플레이어 개체 번호와 대조)"

### F.motion-boundary — 바꾸기는 동작 경계에서(판단은 최대 속도 그대로)
- 상태: 실게임 확인
- 확인한 선: 9bda89ab7357
- 종류: 제품
- 한 줄: 판단은 전투 중 최대 속도로 계속 묻는다. 다른 행동으로 바꾸는 답이 동작 중간(게임 휘두르기 쿨다운이 한 틱보다 많이 남음, 한 칸 걷는 중, 먹는 중)에 오면 기다렸다가 동작 경계에서 시작하고, 요청 계기가 긴급(체력 5 이상 감소, 1.5칸 안 새 적)이거나 기술이 끝났으면 바로 시작한다. 기다리는 동안 새 긴급이 생기면 그 답은 버리고 바로 다시 묻는다. '계속'이나 같은 행동을 고르면 기다리던 바꾸기는 취소한다. 휘두르기 시간은 게임이 그 플레이어에게 쓰는 타이머(SyncedPlayerSharedCooldownTimersCD[MeleeWeaponSlot], PlayerAttackCooldownCD)를 매 프레임 읽어 무기·장비·버프·음식이 저절로 반영되고, 자기 휘두르기 간격 관찰값(최근 중앙값)을 함께 기록한다. 전투 한 번마다 바꾸기 수(경계·긴급·동작 중간)·되돌림·처치·체력·프레임 튐을 남긴다.
- 관측 지점: Player.log "DECISION #n RESULT … HOLD until the motion boundary", "held switch APPLY (boundary|idle|timeout)", "SWITCH … at boundary|emergency|idle", "REVERSAL", "MID-MOTION", "MOTION swing #n item=… game cooldown timer …s … observed interval median …", "COMBAT #n result: switches …"
- 정책: pol.combat-rate-max, pol.combat-choice-design, pol.motion-real-speed, cand.motion-boundary
- 코드 앵커: AIMotion.Frame, AIMotion.Swing, AIMotion.TickRate, AIMotion.ServerTick, AIMotion.IntervalNote, AIMotion.SpeedStats, AIMotion.CooldownOf, AIMotion.StateOf, AIBrain.ApplyOrHold, AIBrain.TryApplyHeld, AIBrain.Switch, AIBrain.EmergencyNow, AIBrain.EnemiesWithin, AIBrain.IsEmergencyTrigger, AIBrain.SwitchHistory, AIBrain.CombatTrack
- 게임 지점: Pug.Other/EquipmentSlot.cs:248 0.4f;
- 게임 지점: Pug.Other/EquipmentSlot.cs:253 cooldownTime
- 게임 지점: Pug.Other/EquipmentSlot.cs:296 syncedSharedCooldownTimersCD.ElementAt(index).cooldown.Start(currentTick,
- 게임 지점: Pug.Other/EquipmentSlot.cs:297 playerAttackCooldownCD.cooldown.Start(currentTick,
- 근거: work/2026-11/2026-11-04-전투안정-동료인식.md "MOTION swing #1 item=WoodSword: game cooldown timer 0.250s (5 ticks) (new value)"
- 근거: work/2026-11/2026-11-04-전투안정-동료인식.md "동작 중간 바꾸기는 모든 빌드·모든 전투"
- 근거: work/2026-11/2026-11-04-다수적-긴급전환.md "긴급 바꾸기(20초당): 처음 2.2(새 적 26·체력 6) → swarm1 0.0, swarm2 0.5, swarm3 0.7, swarm4 0.1(새 적 0·체력 2)."
- 메모: 2026-11-04 다수적-긴급전환: "1.5칸 안 새 적" 긴급은 그 적이 요청 입력 때 4칸 안에 없었을 때만(AIBrain.KnownNearTiles). 무리 싸움에서 같은 적이 드나들며 다시 긴급이 되던 것을 막음.
- 메모: 2026-11-04 전투 다듬기 1: "계속" 답은 요청 때 돌던 행동의 키에 대한 것. 그 사이 기다리던 바꾸기·긴급으로 다른 행동이 시작됐으면 그 답은 버리고 바로 다시 묻는다(AIBrain.StaleReason, Player.log "DISCARD: 계속하라던 행동이 이미 바뀜(<그때 키>→<지금 키>)"). 전에는 새 행동이 그대로 이어졌다(전투로그-판단검토 #827).
- 근거: work/2026-11/2026-11-04-전투-다듬기/1-정리-계속불일치.md "DISCARD: 계속하라던 행동이 이미 바뀜(attack:98284→attack:89540)"
- 메모: 2026-11-05 전투 단계 2(fc8161b): 답 처리 규칙이 `Core/AIRunGate.cs`로 갔다(`ApplyOrHold`가 `AIRunGate.Decide`를, `TryApplyHeld`가 `AIRunGate.HeldApplyNow`를 쓴다). 짧은·복합 동작 답은 경계를 기다리지 않고 바로 적용하며(`AIRunGate.IsShort`), 전환은 `short`로 따로 세어 MID-MOTION은 긴 동작의 2초 강제 적용에만 남는다(F.short-actions). 새 답이 온 프레임에는 보류 적용을 하지 않고 새 답을 먼저 처리하며, 새 답이 적용·계속·보류·대기열이면 옛 보류를 대체한다. 버섯 야수 근접전 입력의 전환 이력 줄은 긴 동작이 걸린 전환만 다시 시작 비용과 함께 적고 짧은 동작끼리는 수만 적는다(`AIRunGate.SwitchHistoryText`, HEAD 결정 2). 전조로 요청을 끊어도 보류 답은 남는다(F.charge-urgent).
- 근거: work/2026-11/2026-11-05-전투-단계2/5-검수반영·회귀.md "R3 SWITCH MID-MOTION 0줄(①58, ②54)"

### F.teammates — 화면에 보이는 동료(호스트·다른 AI)
- 상태: 실게임 확인
- 확인한 선: c2b9ca5b7c78
- 종류: 제품
- 한 줄: 캐릭터마다 자기 사람 화면 안의 다른 플레이어를 이름표·방향·거리·체력 바·보이는 행동(걷는 방향·휘두르는 중·쓰러짐·옆 3칸 안의 적)으로 본다. 판단 입력에 "화면에 보이는 다른 플레이어 … 공격 대상이 아니다" 한 줄, 공격 후보에 "동료 X와 N칸 — 돕는 셈", 동료 옆 적이 가까운 세 적에 없으면 "동료 돕기(공격)" 후보, 탐험 목표면 "동료 따라가기"(따라가기 기술, 움직이면 따라 방향을 바꿈, 2.5칸 안이면 끝), 따라가기 목표의 호스트도 같은 기술. 위쪽 LLM 보고에 visible_players. 기억·목표·말은 나누지 않는다.
- 관측 지점: Player.log "DECISION #n SEND … players=[…]", "| follow_n 동료 따라가기 —", "| attack_n 동료 돕기(공격) —", "SKILL 따라가기 DONE|FAILED"; 위쪽 LLM 보고 "visible_players"
- 정책: pol.multi-ai, pol.vision-human-view, cand.teammates-on-screen, cand.memory-per-character
- 코드 앵커: AITeammates.Visible, AIBrain.CandBuild.Follow, AIBrain.CandBuild.Help, AITeammates.NearEnemyNote, AITeammates.Summary, AITeammates.StateOf, FollowSkill.FollowSkill, FollowSkill.Start, FollowSkill.Tick, FollowSkill.Where, FollowSkill.Cleanup, FollowSkill.Progress
- 근거: work/2026-11/2026-11-04-전투안정-동료인식.md "players=[Luminous(호스트) 동쪽 1.0칸, 체력 바 100/100, 서 있음; Claude(AI 동료) 동쪽 2.6칸, 체력 바 100/100, 서 있음]"
- 근거: work/2026-11/2026-11-04-전투안정-동료인식.md "그때 Gemini는 Claude 화면 밖이라 들어가지 않았다(맞음)."
- 메모: "동료 돕기(공격)" 후보와 "돕는 셈" 설명은 2026-11-04 다수적-긴급전환에서 실게임으로 나왔다(고른 적은 없음).
- 근거: work/2026-11/2026-11-04-다수적-긴급전환.md "동료 돕기(공격) — 호스트 Luminous(16.6칸, 체력 바 100/100) 옆 2.8칸의 적 MushroomEnemy, 나와 14.7칸, 적 체력 105/105."
- 메모: 2026-11-04 넷이 함께 플레이: 넷이 함께 있을 때 돕기 정보 241번, 따로 '동료 돕기' 63번 중 3번 고름, 돕는 셈인 공격 40번, 동료 따라가기 105번 중 6번 고름. 고른 것·안 고른 것 모두 상황에 맞음.
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "동료 돕기(공격) — AI 동료 Gemini(7.0칸, 체력 바 77/100) 옆 1.1칸의 적"
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "| 돕기 정보 / 따로 돕기(고름) / 돕는 셈 공격 고름 / 동료 따라가기(고름) | 241 / 63(3) / 40 / 105(6) | 97 / 21(1) / 9 / 126(2) |"


### F.test-switch — 시험 전용 명령은 스위치 파일 뒤에
- 상태: 실게임 확인
- 확인한 선: 2bc6c0022fa9
- 종류: 개발
- 한 줄: goal·stop·on·off·상태를 뺀 `/ai` 명령(직접 명령과 시험·진단 도구 32개)과 `.testgive` 파일은 `AIPlayers\testcmds.on` 파일이 있을 때만 동작한다. 없으면 채팅에 꺼져 있다고 답하고 로그에 남긴다. 지우지 않는다. 같은 파일이 있을 때만 시험 스위치 파일 둘을 1초마다 본다: `closefight.off`(가까운 싸움 후보 묶음 끔, 기본 켬), `decisionlog.on`(판단 입력·선택지·답 확률 전문을 `AIPlayers\decisions-<시각>.log`에).
- 관측 지점: Player.log "chat command '/ai …' refused: test-only commands are off"; 채팅 "…은(는) 시험용 명령이라 꺼져 있습니다"; Player.log "TEST-ONLY close-fight candidate set: OFF|ON", "TEST-ONLY decisionlog: ON|OFF", SEND 줄 "set=close-fight|set=all(close-fight off)"; `AIPlayers/decisions-*.log` "DECISION #n INPUT|ANSWER"
- 정책: pol.behavior-roles
- 코드 앵커: AIBody.IsTestCommand, AIPlayerSpikeMod.Dispatch, AITestSwitches.Refresh, AITestSwitches.Write
- 근거: work/2026-11/2026-11-04-전투안정-동료인식.md "refused: test-only commands are off"
- 근거: work/2026-11/2026-11-04-전투-다듬기/1-정리-계속불일치.md "| 끔(t≈253~316, `closefight.off`) | 36 | 0 | 15 | 0 |"
- 근거: work/2026-11/2026-11-04-전투-다듬기/1-정리-계속불일치.md "켬 판 SEND 45 = INPUT 45, 두 판 합 INPUT 81·ANSWER 79"
- 메모: 2026-11-04 전투 다듬기 1에서 시험장 명령 `arena go|spawn`(AIArena)과 두 스위치를 더함. decisionlog는 AI가 월드에 있을 때만 닫힌다(Refresh가 AIBrain.Tick에서 돈다) — 파일을 먼저 지우고 AI를 끈다.

- 메모: 2026-11-04 전투 다듬기 2: `/ai <멤버> arena spawn [수] [종류] [반지름] explore`는 그 멤버에게 탐험 목표를 적과 같은 프레임에 먼저 준다(AIArena.Spawn). `/ai all arena spawn … explore`면 모든 멤버. 측정 스크립트는 대화 scratchpad csab.ps1·help.ps1.
- 근거: work/2026-11/2026-11-04-전투-다듬기/2-측정-원인-동료.md "같은 136.9에"
- 메모: 2026-11-05 전투 단계 2(fc8161b): 돌진 측정 줄 `CHARGE`(감시를 벗어난 야수의 `left the watch` 포함)·`BRUTE-HP`는 `testcmds.on`일 때만 쓴다(`AIChargeWatch.MLog`). 시험장 `/ai arena spawn 1 MushroomBrute 4 kill`은 근접전 입력에 "이번 교전 목표: 이 버섯 야수 처치(호스트 지시)." 한 줄을 넣는 시험 표시를 켠다(②에서 씀, 제품 목표 종류가 아님, 다음 생성 때까지 켜진 채). 제품에서 처치 목표를 어떻게 줄지는 U.kill-goal-product.
- 근거: work/2026-11/2026-11-05-전투-단계2/4-독립검수.md "kill 표시는 시험 명령 안에서만 켜진다"

### F.craft — 기본 제작과 준비 경로(모으기 → 만들기 → 놓기 → 계속)
- 상태: 구현됨·새 실게임 미확인
- 확인한 선: c0e4e4d80fce
- 근거: work/2026-11/2026-11-04-이동-드득거림.md "`GOAL done rev=5: Torch(Torch) 2개 만들기 (2/2) after 5.5s`"
- 종류: 제품
- 한 줄: 알려진 품목과 실제 제작대를 선택하고 runtime 레시피 비용·출력량으로 요청 횟수/초과량을 계산한다. 트리 말단과 직접 지정·Prep은 같은 ExactCandidate 입구를 쓰며 자동 가공은 별도다. 커서·시설 잔류와 main 가방 수령을 구분하고 원래 부탁의 단계 완료는 AI가 선택한다.
- 관측 지점: Player.log "BRAIN: preparation:", "SKILL 만들기|놓기|뿌리 캐기|부수기 DONE|FAILED", "SKILL 만들기: craft request … (via UIActionBuffer…)", "BODY: item on the cursor"; 채팅 `/ai craftinfo`(시험용)
- 정책: pol.behavior-roles, pol.vision-human-view, cand.v2-design
- 코드 앵커: AICraft.QueueCraft, AICraft.CraftBufferEntity, AICraft.CursorItem, AICraft.HotbarSlotFor, CraftSkill.CraftSkill/1, CraftSkill.CraftSkill/4, CraftSkill.Start, CraftSkill.Tick, CraftSkill.Cleanup, PlaceSkill.PlaceSkill/1, PlaceSkill.PlaceSkill/5, PlaceSkill.Start, PlaceSkill.Tick, RootSkill.RootSkill, RootSkill.Start, RootSkill.Tick, RootSkill.Cleanup, BreakSkill.BreakSkill, BreakSkill.Start, BreakSkill.Tick, BreakSkill.Alive, BreakSkill.Cleanup
- 게임 지점: Pug.Other/InventoryHandler.cs:211 CraftItem(PlayerController
- 게임 지점: Pug.Other/ReceiveUIInputActionsRPCSystem.cs:136 UIInputActionDataRPC
- 게임 지점: Pug.Other/TriggerUIActionsSystem.cs:218 UIInputAction.Craft:
- 게임 지점: Pug.Other/Inventory/InventoryUpdateSystem.cs:440 InventoryUtility.Craft(in
- 게임 지점: Pug.Other/Inventory/InventoryUtility.cs:1709 Craft(in
- 게임 지점: Pug.Other/PlayerEquipment/EquipmentUpdateSystem.cs:147 containedObjectsBufferLookup.TryGetBuffer(equipmentUpdateAspect.entity,
- 게임 지점: Pug.Other/PlaceObjectSlot.cs:45 PlaceItem(in
- 근거: work/2026-11/2026-11-04-기본-제작.md "SKILL 만들기 DONE after 0.1s: reason='Torch 1개 만듦 · 가방 Torch +1, Wood -1'"
- 근거: work/2026-11/2026-11-04-기본-제작.md "GOAL done rev=1: Furnace(Furnace) 1개 만들기 (1/1) after 11.7s, calls this session 9"
- 근거: work/2026-11/2026-11-04-기본-제작.md "GOAL done rev=1: CopperMiningPick(CopperMiningPick) 1개 만들기 (1/1) after 27.3s, calls this session 15"
- 근거: work/2026-11/2026-11-04-기본-제작.md "PLANNER: report #2 APPLY decision=set goal=craft CopperBar 4"
- 근거: work/2026-11/2026-11-04-견고성-정리.md "t=808.5 GOAL done rev=19: WallStoneBricksBlock(WallStoneBricksBlock) 1개 모으기 (1/1) after 13.7s, calls this session 64"
- 근거: work/2026-11/2026-11-04-견고성-정리.md "t=668.4 GOAL done rev=15: Furnace(Furnace) 1개 만들기 (1/1) after 9.9s, calls this session 55"
- 메모: 실게임 확인은 빌드 craft14~20에서, 마지막 craft21은 부수기 진단 로그·주석만 지운 것(그 뒤 설치·게임 컴파일 확인, 서명은 craft21 기준). "도구 부족" 모으기 목표의 준비 경로와 기억한 제작대로 가기는 2026-11-04 견고성 정리에서 실게임 확인. 가방→손 칸 옮기기는 서버 버퍼 직접 변경(무기 바꾸기와 같은 방식).
- 메모: 2026-11-04 넷이 함께 플레이: 제작 목표 멈춤 글에 되풀이 실패로 뺀 행동을 붙인다('만들 방법이 없음'은 그대로 → 막힘 분류 cannot_craft 그대로). 기억한 용광로 45칸인데 멈춘 일(기억 시험)은 그 길을 걷다 (-61,52)에서 두 번 막힌 것이었다.
- 근거: work/2026-11/2026-11-04-넷이-함께-플레이.md "만들 방법이 없음 — 되풀이 실패로 30초 동안 뺀 행동"

- 메모: 이전 구현의 한 줄 설명과 실게임 확인 지위는 당시 판의 기록이다: `/ai goal 만들기 <ObjectID> [N]` 또는 LLM의 craft 목표를 받으면 판단부가 준비 경로를 만든다: 목표 품목 → 손 제작/보이는 제작대/만들 수 있는 제작대(나무 작업대 목록까지) → 모자란 재료. 모을 재료는 모으기 후보(큰 뿌리 캐기·나무 물체 부수기·벽 캐기, 갇힌 물체는 파 들어가기), 만드는 재료와 제작대는 각각 한 단계, 가방의 제작대는 놓기 후보. 제작은 클라이언트 `UIInputAction.Craft`와 같은 UIActionBuffer 항목, 결과는 가방 변화로 판정, 커서 칸(제작 출력 칸)의 물건은 가방에 넣는다. 곡괭이 없음·부서짐이면 나무 곡괭이, 목표 벽이 "도구 부족"이면 기억한 캐기 보너스로 그 벽을 넘는 첫 곡괭이 단계를 준비 대상으로. 보이는 제작대가 없고 그것을 만드는 기억한 제작대가 있으면 새로 만들지 않고 "기억한 제작대로 가기"(재료가 있을 때)를 낸다. 보고에 can_do.craft(재료 가진 것/필요한 것)·stations_in_view·known_stations·craft_needed.
- 근거: work/2026-11/2026-11-07-생활트리구현/05-implementation.md "실제 runtime recipe 출력량으로 횟수와 초과량을 계산하고"
- 메모: c4a0f5e의 제작·Prep 연결은 생산코드 어댑터·독립 표본·Unity로 검증했으며 새 실게임 성공으로 승격하지 않는다. 위 확인한 선과 과거 실게임 인용은 그때 판에 한정한다.

### F.exp-memory — 위쪽 LLM의 경험 기억(목표 시도·결과·까닭별 막힘 판정)
- 상태: 실게임 확인
- 종류: 제품
- 한 줄: 캐릭터·월드마다 사건 로그(`events.jsonl`, 덧붙이기만)와 끝난 목표 시도(`goals.jsonl`), 상태(`exp_state.json`)를 쌓는다. 목표 시도 id는 목표 수정 번호(rev)와 별도이고 재시작·다시 읽기·같은 끝 두 번에 중복 집계하지 않는다. 결과 코드는 완료·막힘·취소·교체·실행 장애·결과 모름으로 나누고 쓰러짐은 시도에 붙는 사건이다(목표 유지). 막힘은 끝날 때 조건으로, 그 까닭을 풀 수 있는 변화만 본다(도구 부족→곡괭이 레벨, 광석 모을 곳 못 찾음→그 광석을 더 기억함, 만들 방법 없음→그 품목의 모자란 재료·제작대, 그 밖→새 구역). 목표마다 마지막 완료 뒤 가장 늦은 막힘 하나만 "풀리지 않음 / 다시 해 볼 만함 / 오래돼 모름"으로 판정하고, 바뀐 것 없이 이어진 막힘 수(까닭과 무관)에 따라 다시 볼 때까지 대기를 늘린다. 위쪽 LLM 보고에 `experience` 칸(지금 조건, 진행 중 시도, 최근 결과, 관련 목표 키별 판정, 상한 2,200자)을 붙인다. LLM 호출 수는 그대로다. 상태는 임시 파일에 쓴 뒤 바꿔 넣고, 망가지면 `.bak`이나 사건 로그에서 되살린다. "함께 한 일" 재료로 fight(동료와 함께 여부)·boss_seen/boss_kill·area_enter(첫 진입)·item_first·base_set 사건을 남긴다(장 요약은 아직 없음).
- 관측 지점: Player.log "EXP: goal run … ended", "EXP: report part"; `AIPlayers/<월드>/<id>/events.jsonl`·`goals.jsonl`·`exp_state.json`; 브리지 `plan.jsonl`의 보고 `experience` 칸; 오프라인 시험 `Tools/exp-sim`(`dotnet run -c Release`)
- 정책: pol.exp-no-repeat-misfit, pol.exp-retry-allowed, pol.exp-no-extra-calls, pol.exp-keep-world-memory, pol.exp-link-by-id, pol.combat-fit-criterion, pol.llm-subscription, cand.exp-cause-rules, cand.exp-wait-backoff, cand.memory-per-character
- 코드 앵커: AIExperience.GoalStarting, AIExperience.GoalEnding, AIExperience.Tick, AIExperience.ReportPart, AIExperience.PlanResult, AIExperience.BaseSet, AIExperience.OnReset, ExpRecorder.ExpRecorder, ExpRecorder.Load, ExpRecorder.GoalStart, ExpRecorder.GoalEnd/5, ExpRecorder.GoalEnd/8, ExpRun.Classify, ExpRecorder.Down, ExpRecorder.Event, ExpRecorder.Report, ExpRecorder.SaveState, AIGoalKinds.ExperienceKey
- 메모: 2026-11-04 리팩토링(3c3adbe→e82eb87, 동작 유지) S2·S2b: 목표 끝 값 `GoalEnd`(까닭·결과·막힘 분류, `Core/AIOutcomes.cs`)가 생겨 `ExpRecorder.GoalEnd/8`이 결과를 받아 쓴다. 자리로 정해지지 않는 막힘은 예전처럼 문장 분류(`ExpRun.Classify`). `GoalEnd/5`는 문장만으로 분류하는 옛 꼴(시험 비교용). 기술 끝 값(`SkillEnd`, `AISkill.Finish/3`)도 코드값이다. 길 없음 규칙은 아직 문장으로 판정하고 불일치만 로그로 남긴다(U.transition-code).
- 근거: work/2026-11/2026-11-04-LLM-기억/3-실게임.md "두 craft는 "풀리지 않음"이라 되풀이하지 않았다"
- 근거: work/2026-11/2026-11-04-LLM-기억/3-실게임.md "**8초 만에 완료했고**, 다음 보고 note는 "막힘 1번 뒤에 완료함""
- 근거: work/2026-11/2026-11-04-LLM-기억/3-실게임.md "보고 조각(모드 로그 `EXP: report part`, n38): 중앙값 960자, 최대 1,469자."
- 근거: work/2026-11/2026-11-04-LLM-기억/1-구현.md "목표 시도 id는 `"<sess>.<goal_start 사건 id>"`다. rev와 무관하다(review 1)."
- 근거: work/2026-11/2026-11-04-LLM-기억/1-구현.md "기다리는 시간은 연속 횟수에 따라 30분 → 1시간 → 2시간 → 4시간 → 8시간(상한)으로 는다."
- 근거: work/2026-11/2026-11-04-LLM-기억/2-검수.md "N2: 같은 실패를 고른 횟수가 20번에서 5번으로 줄었다"
- 메모: 실게임은 2026-11-04 15:05~15:50 ai2, 시험 빌드 test-memory-kg 0fa85d2(기억 + 게임 지식그래프 ai-knowledge 9b8690a)였다. 기억 파일(AIExpCore.cs·AIExperience.cs)은 ai-character 05df50d와 같지만, 그 빌드의 AIExplore.cs에는 지식그래프 훅이 더 있어 확인한 선을 적지 않는다. 실게임으로 본 흐름은 만들기 막힘 → 풀리지 않음 동안 되풀이 안 함 → 재료가 생겨 다시 해 볼 만함 → 완료. 도구 부족 판정과 보스 사건은 오프라인 시험에서만 확인(U.exp-toolweak-live, U.exp-boss-live). 입력 토큰 중앙값 +43%(4.6k→6.6k), 응답 시간 중앙값 그대로(5.46초). 같은 시험에서 ObjectInfo.level이 모든 아이템에서 0이라 레벨은 variation 또는 프리팹 LevelCD에서 읽는다(`AIExperience.ItemLevel`). 게임 지식그래프(아이템·레시피)와는 게임 ID로만 잇는다. 계획·교훈 메모, 장 요약, 관계 기억, 월드를 넘는 교훈은 다음 단계.

### F.memory-recall — 위쪽 LLM의 기억 그래프 검색(목차 + 필요할 때 질의)
- 상태: 실게임 확인(기반 동작) · 모델의 자발적 검색은 미확인
- 종류: 제품
- 한 줄: 브리지가 캐릭터·월드별 사건 로그(F.exp-memory의 events)를 원본으로 메모리 색인(기억 그래프)을 만들고, 새 줄만 이어 붙인다. 게임 ID 노드는 게임 지식그래프와 같은 키로 잇는다. 보고 턴마다 목차 `memory_index`(적·장소·동료·본 게임 지식 요약, "상위 n/전체 m", 최대 1,000자)를 붙인다. LLM은 목표 대신 `{"decision":"recall","recall":[질의 ≤3]}`로 답해 기억을 찾을 수 있다. 질의는 7개다: about, goal, near, when, follow, game(그 캐릭터가 본 범위의 게임 지식), find. 브리지가 찾아 결과를 붙여 다시 묻고, 모드에는 set/hold/ask 하나만 간다. 한 판단 검색은 최대 2번(멤버별 recallBudget)이고, 상한에 닿으면 "이제 답하라"로 한 번만 더 묻는다(새 고리 없음). 마감은 브리지가 받은 때부터 120초이고, 요청 뒤 30초가 지나거나 남은 시간이 모자라면 더 찾지 않는다. 결과는 질의당·검색당 상한이 있다. set/hold/ask에 실은 prefetch는 다음 보고에 한 번만 붙인다. 다른 캐릭터·개발자 전체 지식은 보이지 않는다. 모드는 기다림을 150초로 두고, 시간 초과는 AI를 끄지 않고 다시 묻는다. 기다리는 동안 쓰러지면 그 답을 버린다. 보고에 `me.tile`, fight 사건에 조건·장소를 더했다. MCP 없이 브리지 안에서 처리해 작업용 CLI 설정과 무관하고 추가 설치가 없다.
- 관측 지점: 브리지 `logs/plan.jsonl`(rounds·recall·calls·index_chars·deadline_hit·cap_hit), `logs/recall.jsonl`(질의·결과 전문); 브리지 시작 줄의 게임 그래프 위치; Player.log "answer DROPPED: 오래된 답: 기다리는 동안 쓰러짐"; 시험 `node Tools/ai-bridge/test/{memgraph,recall-loop,bridge-wiring}.test.mjs`, 실제 CLI 재생 `Tools/ai-bridge/replay.mjs`
- 정책: pol.recall-on-demand, pol.recall-index-then-query, pol.recall-in-bridge, pol.recall-budget, pol.recall-game-by-query, pol.recall-facts-only, pol.recall-prompt-cost, pol.exp-link-by-id, pol.llm-subscription, cand.recall-timing
- 코드 앵커: AIPlanReport.ReportJson, AIPlanner.Report, AIPlanner.Tick, AIPlanner.Apply, AIPlanner.CheckReason, AIExperience.PlanResult, AIExperience.Tick
- 근거: work/2026-11/2026-11-04-기억-그래프-검색/1-설계.md "질의 7개, 필요할 때 검색, 한 판단 검색 최대 2번."
- 근거: work/2026-11/2026-11-04-기억-그래프-검색/4-구현검수.md "가장 늦은 끝은 116.8초(< 120)였다."
- 근거: work/2026-11/2026-11-04-기억-그래프-검색/7-실게임.md "목차(memory_index)는 판단 42번 모두 붙었다. 중앙값 973~985자, 최대 1,000자로 상한 안이다."
- 근거: work/2026-11/2026-11-04-기억-그래프-검색/7-실게임.md "쓰러짐 뒤 늦은 답 버리기가 실제로 동작했다."
- 근거: work/2026-11/2026-11-04-기억-그래프-검색/7-실게임.md "브리지가 그 멤버 호출을 멈췄다(12.4초 뒤). 의도한 끊김 처리다."
- 근거: work/2026-11/2026-11-04-기억-그래프-검색/7-실게임.md "recall 0번 / 42 판단."
- 근거: work/2026-11/2026-11-04-기억-그래프-검색/8-질문시험.md "recall은 35번 판단에서 0번이었다."
- 근거: work/2026-11/2026-11-04-기억-그래프-검색/9-검색안한이유.md "강도가 높아져도 나아지지 않았다."
- 메모: 코드는 대부분 브리지 JS다: `Tools/ai-bridge/memgraph.mjs`(그래프·목차·질의), `recall-loop.mjs`(고리·마감·상한), `plan.mjs`(한 판단의 단계: 기억 맥락·목차·prefetch 보관·검색 여부·고리 뒤 처리, 2026-11-04 리팩토링 S8에서 bridge.mjs에서 나옴), `protocol.mjs`(모드로 가는 봉투·오류 봉투), `preflight.mjs`(멤버 상태·한도), `bridge.mjs`(설정·좌석·서버·끊김), `replay.mjs`(재생 비교). 이 그래프의 build.mjs는 C#만 읽으므로 JS는 코드 원본에 넣지 않고 위치만 적는다. 실게임은 2026-11-04 21:17~21:40 ai2이고 시험 빌드 ai-recall 1c2a94c였다(멤버 자리 claude=Opus, gpt=Haiku, gemini=Flash Low, grok=GPT-OSS, 사용자 함께). 그 뒤 브리지만 7ae4e08·3c3adbe(시험 seats)가 바뀌었다. C# 앵커는 ai-character 3c3adbe 기준이고, 실게임 빌드와 C#이 같은지는 따로 대조하지 않아 확인한 선을 적지 않는다. 재생 비교(128판단)·실게임(42)·질문 시험(35)·이유 묻기·추론 강도 비교 모두 모델이 스스로 recall을 쓴 적이 없어, 실제 CLI에서 recall 고리 완주는 미확인이다(U.recall-unused, U.recall-loop-live). 목차와 experience는 판단·답에 쓰였다. 사용자 결정(2026-11-04): 리팩토링·전투 개선 뒤 실게임에서 보완한다. 사용자가 목표를 정해 주며 플레이가 길어지면 목차가 다양해져 자연스럽게 검색할 것으로 본다. 모델 비교에서 Opus·Flash Low가 목표 적합성이 가장 좋았고, Haiku는 Opus와 비슷한 시간·토큰이었으며, GPT-OSS는 가장 느리고 수치를 자주 틀렸다.

### F.charge-watch — 버섯 야수 돌진 감시와 예측(근접전 관측)
- 상태: 실게임 확인
- 확인한 선: 859cbdd02718
- 종류: 제품
- 한 줄: 켜진 AI의 판단 고리에서 프레임마다 한 번, 켜진 AI 24칸 안의 돌진 적(MushroomBrute, `EnemyCD`+`ChargeAttackStateCD`)을 서버 월드에서 읽기만 한다(AI가 없으면 읽지 않음). 단계(평소·돌진 준비·돌진 중·취약·헛돌진 회복)와 그 경과(감시가 본 서버 틱), 조준 대상·방향, 다음 돌진까지를 잰다. 버섯 야수가 8칸 안에 보이면 근접전 판단 입력에 적 관측(단계·조준 고정까지·출발까지·겨누는 대상·내가 돌진선 위인지·도착 예상·취약 남은 시간·다음 돌진), 나(돌진 한 번 피해 약 58, 지금 체력이면 몇 번에 쓰러지는지), 주변(적 기준 방향별 열린 거리·내 뒤 벽), 진행 중 이동, 시간 기준(답·출발 지연), 게임에서 잰 지식 한 줄을 넣는다. 후보마다 같은 꼴의 시간·예상 결과 사실(돌진선에서 벗어남/못 벗어남/나에게 닿기 전에 먼저 멈춤과 멈추지 않으면 언제 맞는지, 취약 창, 예상 피해)을 붙이고 권유하는 말은 넣지 않는다. 예측(`AIChargeMath.Predict`·`PredictChase`)은 0.02초 궤적에서 두 중심이 1.05칸 안에 드는 첫 시각을 맞음으로 본다. 벌레(CritterCD)만 있는 칸은 돌진 막힘에서 빼고(걷기 판단은 그대로), 벽이 아닌 멈춤에는 "확인 못 함"을 붙이며 "부딪힌 둘레 벽은 부서짐"은 벽 멈춤에만 붙인다. 이 근접전 입력에서는 "쓰러짐 n번"·관찰 처치 시간·받은 피해 기록 줄 대신 지금 사실을 쓰고, 최근 결과는 직전 1개만 쓴다(단계 1 조각). 다른 적·비전투 입력은 그대로다. 측정 줄 `CHARGE`·`BRUTE-HP`는 `testcmds.on`일 때만 쓴다. 월드가 바뀌거나 내려가면 감시 상태를 비운다.
- 관측 지점: 판단 입력(`AIPlayers/decisions-*.log`, 스위치 `decisionlog.on`) "적 관측(서버 상태, 이 요청을 만들 때): 버섯 야수 …", "알려진 사실(버섯 야수, 게임에서 잰 값): …", 후보의 "예상(가정: …)"; Player.log "DECISION #n PICK <키> run #r pred=<예측 코드>"; 시험 스위치 켬일 때 Player.log "CHARGE …", "BRUTE-HP …"
- 정책: pol.combat-no-auto-dodge, pol.combat-kill-brute, pol.combat-choice-design, pol.vision-human-view
- 코드 앵커: AIChargeWatch.Frame, AIChargeWatch.View, AIChargeWatch.NewAnticipations, AIChargeWatch.ChargeStillOn, AIChargeWatch.NotePick, AIChargeWatch.NoteMove, AIChargeWatch.Reset, AIBrain.CandBuild.FindBrute, AIBrain.CandBuild.BruteObs, AIBrain.CandBuild.BruteAttackDesc, AIBrain.CandBuild.BruteFacts, AIChargeMath.Predict, AIChargeMath.PredictChase, AIChargeMath.OutcomeText, AIChargeMath.TimeText, AIChargeMath.NextChargeText
- 근거: work/2026-11/2026-11-05-전투-단계2/1-구현.md "답(0.36초, 5,886토큰): `step_left_3` 0.53, `step_left_2` 0.40, `step_right_2` 0.03."
- 근거: work/2026-11/2026-11-05-전투-단계2/5-검수반영·회귀.md "돌진 6번 모두 AI에 맞지 않음"
- 근거: work/2026-11/2026-11-05-전투-단계2/5-검수반영·회귀.md "새 관측·후보 0(10:13까지 비전투·약한 적 INPUT 499개 중"
- 근거: work/2026-11/2026-11-05-전투-단계2/7-R1.md "설치본 .cs 30개가 fc8161b와 같음(다름 0)."
- 메모: 2026-11-05 전투 단계 2(ai-character fc8161b, 부모 e82eb87). 감시 방법은 단계 0 탐침(combat-s0 `AIBruteProbe.cs`)에서 가져왔다. 실게임 판: ①(93ad7d8)·②(d35c9e9, 시험 kill 표시)·R3(76563bf)·R1 확인 판(fc8161b, 버섯 야수 2판: 쓰러짐 1·처치 1). 예측이 "벗어남"인데 맞은 경우가 판마다 몇 번 있고 원인을 다 가르지 않았다(계속 공격이 대상을 따라가는 차이 등, 1-구현 §5). 놓은 블록 충돌, 1 피해와 재생 시계, 물·뿌리 칸 멈춤은 잰 적이 없다(U.brute-placed-block, U.brute-regen-clock, U.charge-stop-tiles). 감시는 걷는 AI의 판단 고리에서만 돌아 쓰러진 동안은 단계 추적이 멈춘다(6-재검수 메모).

### F.combat-short-options — 버섯 야수 근접전의 짧은·복합 후보
- 상태: 실게임 확인
- 확인한 선: c1e204b54ef6
- 종류: 제품
- 한 줄: 버섯 야수가 8칸 안에 보이면 기존 전투 후보(계속 공격·거리 벌리기·물러나기·먹기·기다리기)에 더해 고정 순서·키로 낸다(좌우는 늘 왼쪽 먼저): 한 번 치기(`hit:<적>`, 휘두르는 거리 1.3칸 안일 때), 한 번 더 치기(`hitnext:<적>`, 한 번 치기가 도는 중), 사거리까지 접근(`approach:<적>`), 제자리 유지(`hold`, 최대 1초), 적 기준 8방향 1칸 이동과 긴 이동(`step:<방향>:<칸>`), 복합 후보 치고 이동(`combo:hit-<left|right>:<2|3>`, `combo:hit-away:2`)·접근 후 치기(`combo:approach-hit`)·이동 후 치기(`combo:<방향>:1-hit`, 도착점이 사거리 안인 1칸 이동만). 빼는 경우는 가는 길·도착점이 벽·물체에 막힘(직선을 0.25칸마다 확인), 도착점이 적 몸(0.9칸 안), 같은 출발·직선·도착인 이동뿐이고 위험하다는 이유로 빼지 않는다. 후보는 도착점·대상·관측 시각을 실어 실행기에 넘긴다(F.short-actions). 전투(적이 8칸 안) 판단은 조립기가 후보 수를 자르지 않는다(`decisions-settings.json`의 `combatMaxCandidates`, 기본 0 = 무제한). 비전투는 14개+기다리기 그대로다. 다른 적과의 싸움에는 새 후보가 나오지 않는다.
- 관측 지점: Player.log "DECISION #n SEND … candidates=k", "DECISION #n PICK <키> run #r"; 판단 입력 후보 문장 "짧은 이동: 적 기준 …", "긴 이동: …", "복합(치고 이동): 1) … 2) …", "사거리까지 접근 …"
- 정책: pol.combat-composite, pol.combat-no-auto-dodge, pol.combat-choice-design, pol.combat-rate-max, pol.combat-kill-brute, pol.combat-cap-none
- 코드 앵커: AIBrain.CandBuild.Brute, AIBrain.CandBuild.Combos, AIBrain.CandBuild.ClearWay, AIBrain.CandBuild.ExitText, AIBrain.CandBuild.DmgAt, AIBrain.CandBuild.DirOf, AICandidateAssembler.LimitFor
- 근거: work/2026-11/2026-11-05-전투-단계2/1-구현.md "상한: 전투(적이 8칸 안)에서는 조립기가 자르지 않는다(사용자 결정)."
- 근거: work/2026-11/2026-11-05-전투-단계2/1-구현.md "답(0.36초, 5,886토큰): `step_left_3` 0.53, `step_left_2` 0.40, `step_right_2` 0.03."
- 근거: work/2026-11/2026-11-05-전투-단계2/5-검수반영·회귀.md "새 관측·후보 0(10:13까지 비전투·약한 적 INPUT 499개 중"
- 근거: work/전투-단계2/history.md "복합행동을 무조건 넣어야 할 것 같아"
- 메모: 2026-11-05 전투 단계 2(fc8161b). `BuildCandidates`가 `b.Brute()`로 부르는데 도구가 인스턴스 호출을 따라가지 못해 블록과 그 보조 함수를 앵커로 적었다. R1 확인 판(fc8161b) 두 판에서 고른 짧은·복합 후보: step 22, hitnext 15, combo 10, hit 4, approach 3(판단 71번 중, 7-R1 원자료 runs/R1/analysis.txt의 choices). 전투 후보 상한 없음은 사용자 결정(work/전투-단계2/history.md 2026-11-04T22:41·22:42)(pol.combat-cap-none). 좌우 동률이면 먼저 나오는 왼쪽이 골라지는 쏠림은 순서 고정 원칙 안이라 그대로 두고 단계 5에서 다시 본다(HEAD 결정, 4-독립검수 반영).

### F.short-actions — 짧은 동작·복합 동작 실행과 실행 회차
- 상태: 실게임 확인
- 확인한 선: 360050ebf9ed
- 종류: 제품
- 한 줄: 짧은 동작(짧은·긴 이동 `StepSkill`, 한 번 치기 `HitOnceSkill`, 사거리까지 접근 `ApproachSkill`, 제자리 유지 `HoldSkill`)과 복합 동작(`ComboSkill`)은 후보가 정한 도착점·대상·관측 시각을 받아 길찾기 없이 직선 이동 입력으로 실행하고, 못 가면 이유를 남기고 끝난다(도착 0.2칸, 0.25초 무진척이면 막힘). 한 번 치기는 게임이 휘두르기를 시작하면 누르기를 놓고 그 휘두르기가 끝나면 끝나며(빗나가도), 고른 손 칸 무기가 없어지거나 바뀌면 휘두르기 전이면 실패, 도중에 부서지면 한 번 알리고 1회로 끝낸다. 복합은 한 실행 안에서 단계를 고정 순서로 돌고 조건 분기가 없다(치고 이동은 휘두르기 시작 0.12초 뒤 이동, 끊기면 그 단계에서 끝). `AIBody.StartSkill`마다 캐릭터별 실행 번호(run)를 붙이고, '계속' 답은 요청 때 돌던 그 실행에만 적용한다(그 실행이 끝났거나 같은 키의 새 실행이면 버리고 다시 묻는다). 짧은·복합 동작 답은 동작 경계를 기다리지 않고 바로 적용하고 전환 지표에서 `short`로 따로 센다. 버린 답은 실패 기억(2번이면 30초 제외)에 넣지 않고 실제 실행 실패만 기억하며(비전투에도 적용), 짧은·복합 동작의 실패는 실패 기억에 넣지 않는다. 처치는 대상 체력 0 이하를 읽을 때만 센다(기존 공격 기술도). 이동 통계는 후보 종류 코드(`AIRunGate.IsMoveKind`)로 가른다.
- 관측 지점: Player.log "DECISION #n PICK <키> run #r pred=…", "EXEC #r …", "EXEC #r combo <이름> stage i/n start|DONE|FAILED", "cut at stage i/n", "EXEC #r step move-start", "SKILL 짧은 이동|한 번 치기|사거리까지 접근|복합 행동 DONE|FAILED|CANCELLED", "SWITCH … (short action at once by design: …)", "COMBAT #n result: … short actions at once n", "COMBAT #n answers (session totals): …", "DISCARD: 계속하라던 실행 #r이 이미 끝남"
- 정책: pol.combat-composite, pol.combat-no-auto-dodge, pol.combat-choice-design, pol.motion-real-speed, cand.motion-boundary, cand.fail-memory
- 코드 앵커: AIActions.MakeCombo, StepSkill.StepSkill, StepSkill.Start, StepSkill.Tick, StepSkill.Cleanup, StepSkill.Progress, StepSkill.RemainingSeconds, HitOnceSkill.HitOnceSkill, HitOnceSkill.Start, HitOnceSkill.Tick, HitOnceSkill.Alive, HitOnceSkill.Progress, HitOnceSkill.RemainingSeconds, ApproachSkill.ApproachSkill, ApproachSkill.Start, ApproachSkill.Tick, ApproachSkill.RemainingSeconds, HoldSkill.HoldSkill, HoldSkill.Start, HoldSkill.RemainingSeconds, ComboSkill.ComboSkill, ComboSkill.Start, ComboSkill.StartStage, ComboSkill.Tick, ComboSkill.Cleanup, ComboSkill.Progress, ComboSkill.RemainingSeconds, HitOnceLogic.Step, HitOnceLogic.Finish, HitOnceLogic.WeaponCheck, AITargetEnd.How, AIRunGate.Decide, AIRunGate.ContinueStale, AIRunGate.IsShort, AIRunGate.SwitchClass, AIRunGate.KilledSeen, AIRunGate.IsMoveKind, MoveStart.Along, MoveStart.Prior, AIBody.SlotItem, AISkill.RemainingSeconds
- 근거: work/2026-11/2026-11-05-전투-단계2/1-구현.md "짧은 동작·복합 동작은 바로 적용한다."
- 근거: work/2026-11/2026-11-05-전투-단계2/5-검수반영·회귀.md "R3 SWITCH MID-MOTION 0줄(①58, ②54)"
- 근거: work/2026-11/2026-11-05-전투-단계2/5-검수반영·회귀.md "목표 구간 버린 답 36(계속할 기술이 이미 끝남 28, 계속하라던 행동이 이미 바뀜 5, 이미 거리 확보 3)"
- 근거: work/2026-11/2026-11-05-전투-단계2/7-R1.md "설치본 .cs 30개가 fc8161b와 같음(다름 0)."
- 메모: 2026-11-05 전투 단계 2(fc8161b). 실행 규칙(답 결과·continue 오래됨·짧은 동작·전환 분류·처치 판정)은 Unity 없는 `Core/AIRunGate.cs`에 두고 core-tests C2(`Tools/core-tests/CombatS2Tests.cs`)가 비동기 조건별로 시험한다(fc8161b에서 core-tests 297). 무기 파손 중 한 번 치기는 단위 시험만 있고 실게임에서 나오지 않았다. 휘두르기 피해 예상은 WoodSword 게임 자료 기본 피해 30이다(실측 30~36).

### F.charge-urgent — 전조 긴급 재요청과 보류 답 유지, 원인별 호출 실패
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 위협 관측은 판단 요청과 실행 시점을 갱신한다. 호출 실패가 현재 목표와 유효한 몸 동작을 자동 취소하지 않으며, 비상 관측만으로 이전 AI 선택을 폐기하지 않는다.
- 메모: 이전 구현 설명은 당시 기록이다. 버섯 야수가 이 캐릭터를 겨눈 돌진 전조가 새로 보이면(전조 하나 = 적+시작 사건 하나, 한 번만 처리) 야수가 8칸 안일 때만, 진행 중 요청이 전조 전에 만들어졌고 그것이 긴급 재요청이 아니면 그 요청을 끊고 0.25초 바닥과 재시도 대기를 건너뛰어 바로 다시 묻는다. 8칸 밖이면 그 돌진이 진행 중인 동안 기다렸다가 범위에 들면 묻고, 끝나면 버린다. 이미 긴급 재요청이 가는 중이면 끊지 않고 그 답이 오자마자 다음을 묻는다. 전조로 끊을 때는 요청만 끊고 보류 답(동작 경계를 기다리는 모델 선택)과 대기 답은 남긴다. 다른 끊는 이유(목표 바뀜·쓰러짐·시간 초과·리셋·자기 방어 끝·미리 묻기)는 예전처럼 버린다. 남은 보류 답은 자기 경계(멈춤·경계·2초)에서 적용되고, 긴급 답이 오면 실행 소유자 규칙으로 대체된다. 끊은 요청의 사용량은 직전 응답 토큰 수로 추정해 더한다. 전투 중 호출 실패는 원인별로 다시 묻는다(거절·빈 답 0.25초 뒤 새 관측, 네트워크·서버·시간 초과 1초, 속도 제한·요청 오류 3초)이고 거절·빈 답은 "3번 연속 실패면 목표 멈춤"에 세지 않는다. 비전투는 예전대로 3초·3번이면 멈춤이다. 코드는 전조를 보고 스스로 움직이지 않는다.
- 관측 지점: Player.log "DECISION #n dropped (charge anticipation)", "DECISION urgent: charge anticipation e=… #n seen …", "DECISION urgent send", "DECISION urgent waits|dropped", "DECISION #n held switch kept (charge anticipation: only the request in flight is dropped)", "queued answer kept", "DECISION #n failed (<원인>: …); in combat: ask again in …", "DECISION #n unblocked", "COMBAT #n answers (session totals): … charge anticipation triggers n, requests dropped for one n"
- 정책: pol.combat-no-auto-dodge, pol.combat-rate-max, pol.combat-choice-design, cand.motion-boundary
- 코드 앵커: AIBrain.ChargeTrigger, AIBrain.AbortRequest, AIBrain.TryApplyHeld, AIBrain.CallFailed, AIRunGate.ChargeAsk, AIRunGate.AbortDropsWaiting, AIRunGate.HeldApplyNow, AIRunGate.RetryDelay, AIRunGate.CountsTowardStop, AIRunGate.UrgentMayBypassRetry
- 근거: work/2026-11/2026-11-05-전투-단계2/7-R1.md "전조 긴급 5번 모두 진행 중 요청을 끊었다."
- 근거: work/2026-11/2026-11-05-전투-단계2/7-R1.md "`held switch dropped (charge anticipation)` 0번."
- 근거: work/2026-11/2026-11-05-전투-단계2/7-R1.md "확인하지 못했다(해당 경우 0번)."
- 근거: work/2026-11/2026-11-05-전투-단계2/5-검수반영·회귀.md "HTTP 503 1번(t=9837.8) — 1초 뒤, 연속 1."
- 근거: work/전투-단계2/history.md "HEAD 결정(정책 긴장 메모): ① 전조가 와도 보류 중인 모델 선택을 버리지 않고 긴급 답이 실행 소유자 규칙으로 대체"
- 메모: 2026-11-05 전투 단계 2(fc8161b). 보류 답 유지는 HEAD 결정(사용자 결정 아님)이고 정책 pol.combat-choice-design("모델의 선택을 버리거나 덮어쓰지 않고")에 기대어 정했다. R3(76563bf)에서는 끊을 요청이 없던 전조 5번만 보류 답이 남았고 요청을 끊은 6번은 버려졌다. fc8161b가 이것을 고쳤고 R1 확인 판에서 버려짐 0번이지만, 그 순간 보류 답이 있던 경우가 없어 남는 경로는 core-tests 동작 시험으로만 확인했다(U.charge-held-kept-live). 8칸 밖 전조의 기다림·버림(`DECISION urgent waits/dropped`)은 실게임 0줄이다. 감시가 아는 야수 위치로 8칸을 재므로 벽 너머 안 보이는 야수면 야수 사실 없이 다시 물을 수 있다(U.brute-unseen-urgent). 비전투 "3번 연속 실패 멈춤"은 실게임으로 재현하지 못했다(U.fail-stop-3-live).

### F.chat-mention — 채팅 @멘션과 자동완성
- 상태: 실게임 확인
- 확인한 선: c83a9fcbdc69
- 종류: 제품
- 한 줄: 호스트가 채팅 입력 중 `@` 뒤에 한 글자(영어 id·게임 속 이름·한글 이름, 한글은 초성·음절 앞부분도)를 치면 입력 칸 바로 아래 한 줄에 맞는 멤버 후보가 뜨고, Tab이 채팅을 닫는 대신 첫 후보로 채운다(Tab 뒤 IME는 이전 상태로). 보낸 호스트 줄에 `@이름`·`@모두`가 있으면 채팅에는 평소처럼 보이고, 월드 대화 기록에 남긴 뒤 지목된(모두면 월드에 있는) AI마다 '호스트가 말함'(host_said)으로 위쪽 LLM에 바로 전한다. 멘션 없는 줄은 AI에게 가지 않는다. 호스트가 친 줄만 받는다.
- 관측 지점: 채팅 입력 칸 아래 힌트 `@Claude @ChatGPT (Tab)`; Player.log "MENTION from host to [...]", "PLANNER: report #n sent trigger=host_said"
- 정책: pol.nl-mention, pol.nl-host-order-defer, pol.nl-watch-period
- 코드 앵커: AIPlayerSpikeMod.TryHandleMention, AIMention.Members, AIMention.Parse, AIMention.Typing, AIMention.Candidates, AIMention.Complete, AIMention.Hint, AIMention.Prefix, MentionName.MentionName, AIChatHint.Frame, AIChatHint.DeactivatePrefix, AIChatHint.ClearIme, AIPlanner.OnHostSaid
- 근거: work/2026-11/2026-11-05-자연어-명령/11-실게임4.md "멘션 대답 19/19(@모두 두 번 포함, 한글 이름 `@제미나이`·`@그록` 포함)"
- 근거: work/2026-11/2026-11-05-자연어-명령/7-실게임3.md "입력 칸 바로 아래 한 줄에"
- 메모: 2026-11-05 자연어-명령(b2fa4cc). 힌트 줄 위치(-0.85)는 화면 줄 높이로 셈한 값이다. 채팅 글자 크기·보이는 줄 수 개선은 할 일(스크롤은 보류). 한글 조합 중 글자 처리와 Tab 뒤 한/영은 2·3판에서 실제 IME로 봤다.

### F.chat-output — 채팅 출력 분류
- 상태: 실게임 확인
- 확인한 선: 85a5a8b2c6d5
- 종류: 제품
- 한 줄: 모드가 채팅에 띄우는 줄마다 종류(Status/Command 등)를 붙여, 상태 줄(기술 끝, 목표 받음·완료·멈춤, 쓰러짐, 자기 방어, 생각하는 중, 자유 시간)은 로그에만 남기고 `/ai verbose`일 때만 띄운다. say·오류·한도 알림·`/ai` 명령 응답은 띄운다. 띄우는 글은 게임 글꼴에 없는 글자(화살표 등)를 ASCII로 바꾼다. 되풀이되는 오류 알림(아래 판단기 재시도 등)은 캐릭터마다 같은 종류를 5분에 한 번만 띄운다.
- 관측 지점: Player.log "chat reply: …", "chat hidden (status): …", "chat hidden (same notice within 5 min)"
- 정책: pol.nl-chat-output, pol.decision-chat-off
- 코드 앵커: AIPlayerSpikeMod.Reply, ChatPolicy.Show, ChatPolicy.FontSafe, ChatRepeatGate.Allow
- 근거: work/2026-11/2026-11-05-자연어-명령/7-실게임3.md "뜬 줄은 66줄이고 모두 say, `/ai` 응답(켜짐·꺼짐·해제), 오류 알림 2줄이다."
- 메모: 2026-11-05 자연어-명령(b2fa4cc). 5분 한 번 숨김 경로는 실게임에서 같은 캐릭터에 두 번째 알림이 없어 미확인(U.nl-live-unseen).

### F.world-talk — 월드 대화 기록과 보고의 chat 칸
- 상태: 실게임 확인
- 확인한 선: 40c445328afc
- 종류: 제품
- 한 줄: 호스트의 멘션 줄과 AI들의 say를 월드마다 하나의 대화 기록에 쌓고(월드가 바뀌면 비움), 위쪽 보고의 `chat` 칸에 최근 줄을 싣는다. 이 캐릭터가 아직 못 본 줄은 new로 표시한다. AI의 say는 다른 AI를 바로 부르지 않고 그 AI의 다음 판단 보고에서 읽힌다.
- 관측 지점: Player.log "PLANNER: report #n sent …" 보고 JSON의 `chat`
- 정책: pol.nl-one-shot-context, pol.nl-ai-talk-next
- 코드 앵커: AIChatLog.Add, AIChatLog.Clear, AIChatLog.ReportJson
- 근거: work/2026-11/2026-11-05-자연어-명령/2-실게임.md "chat·recent_fights·host_order가 늘었다."
- 근거: work/2026-11/2026-11-05-자연어-명령/1-구현.md "AI의 say는 다른 AI를 바로 부르지 않는다. 다른 AI는 다음 보고에서 읽는다."
- 메모: 2026-11-05 자연어-명령(b2fa4cc). AI끼리 멘션 호출 규칙은 할 일. CLI 세션은 이어 가지 않는다(pol.nl-one-shot-context).

### F.upper-watch — 위쪽 LLM 호출 계기(2분 주기·멘션·싸움 끝·쓰러짐·목표 끝)
- 상태: 실게임 확인
- 확인한 선: a542ee16b5d8
- 종류: 제품
- 한 줄: 알아서 모드와 상관없이 목표·계획·곧 시작할 단계·hold 지켜보기 중 하나가 있으면 '지켜보는 중'이고, 그동안 2분(설정 값)마다 periodic 보고를 보낸다. 어떤 계기로든 보고를 보내면 주기 시계를 다시 센다. 바로 보내는 계기: 호스트 멘션(host_said, 알아서 모드 아니어도), 싸움 끝(절반 아래 또는 쓰러짐, 쓰러짐은 back_up 한 번만), 쓰러졌다 일어남(호스트 목표 중에도), 목표 끝·계획 끝·단계 실패·막힘. 호스트 목표가 끝나도 부르고 LLM이 새 목표를 걸면 이어 간다. 진행 중 요청이 있으면 계기를 보류하고, 실패 재시도는 지켜보는 중이거나 호스트 계기일 때만 돈다. 오래된 답은 같은 계획 안의 단계 넘김·계획 끝으로는 버리지 않고, 호스트 계기 답은 한 번 다시 묻는다. `/ai stop`은 요청·빚진 호출·계획·지켜보기를 모두 지운다.
- 관측 지점: Player.log "PLANNER: report #n sent trigger=periodic|host_said|fight_end|back_up|goal_done|plan_done|plan_step_failed|goal_stalled|retry", "fight_end not reported", "request dropped", "answer DROPPED"; 브리지 `logs/plan.jsonl`
- 정책: pol.nl-watch-period, pol.nl-fight-end-call, pol.nl-always-continue, pol.nl-host-order-defer, pol.llm-subscription
- 코드 앵커: AIPlanner.Tick, AIPlanner.Answer, AIPlanner.TurnOn, AIPlanner.OnFightEnded, AIPlanner.OwedSettle, AIPlanner.OwedAfter, AIPlanner.OwedRides, AIPlanner.AskAgainNow, AIPlanRules.Watching, AIPlanRules.MayRetry, AIPlanRules.OnStale, AIPlanRules.RevStale, AIPlanRules.PlanNow, AIPlanRules.OwedCallGoes, AIPlanRules.EventKept, AIPlanRules.DownTold, AIPlanRules.IsHostTrigger, AIPlanRules.Text, AIPlanStop.Apply
- 근거: work/2026-11/2026-11-05-자연어-명령/7-실게임3.md "싸움 82번 중 5번 불렸다."
- 근거: work/2026-11/2026-11-05-자연어-명령/5-실게임2.md "Gemini가 싸움 중 쓰러짐(DOWN #2) → back_up만"
- 메모: 2026-11-05 자연어-명령. 실게임 확인은 4판(7fcef73)까지이고, b2fa4cc의 K1·K2는 core-tests만이다. 시간당 캐릭터당 호출은 1판 약 113 → 2판 약 66 → 3판 50.7 → 4판 58.5(사용자 멘션 19 포함). hold 중 2분 주기 유지는 HEAD 결정(R4). 규칙은 Core/AIPlanRules.cs로 시험하지만 AIPlanner 안에서 부르는 순서는 시험이 없다(U.planner-policy-scattered).

### F.host-orders — 호스트 지시 상태와 목록
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 원래 사용자 부탁과 현재 계획 단계를 별도로 추적한다. 중간 목표가 성공해도 원래 부탁은 유지하며 상위 응답 order=done 또는 명시 orders 완료가 부탁을 끝낸다.
- 메모: 이전 구현 설명은 당시 기록이다. 호스트의 멘션·`/ai goal`마다 지시(번호·글·상태)를 만들고, 열린 지시(받음·따름·미룸·막힘)를 최대 3개까지 목록으로 들어 보고에 `host_order`(최근)와 `host_orders_open`(앞의 열린 것)으로 싣는다. 상태는 답의 `order`(host_said 답에 반드시)와 `orders`({번호: 값}) 칸으로만 바뀐다(follow·defer·drop·answered·done, 문장으로 판정하지 않음). order가 빠진 host_said 답은 상태를 그대로 둔다. 지시는 그것을 실은 목표·계획 단계와 함께 가고, 목표가 끝나면 끝남, 막혀 끝나면 막힘(열린 채), hold로 내려놓으면 미룸(기다리라는 지시를 follow로 따른 경우는 따름), 새 `/ai goal`이 바꾸거나 `/ai stop`이면 멈춤이 된다. 넘치면 가장 오래된 열린 지시가 밀려남으로 빠진다. 바뀔 때마다 경험 사건 order를 남긴다.
- 관측 지점: Player.log "ORDER #n 받음→따름 …", "ORDER … not applied (saw #a, now #b)", 보고 JSON `host_order`·`host_orders_open`; 경험 사건 `order`
- 정책: pol.nl-host-order-defer, pol.nl-mention
- 코드 앵커: AIHostOrders.Add, AIHostOrders.ApplyAnswer, AIHostOrders.GoalEnded, AIHostOrders.Carried, AIHostOrders.PutOff, AIHostOrders.CloseAll, AIHostOrders.ReportFields, AIHostOrders.Find, AIHostOrders.EarlierOpen, AIHostOrders.OpenAll, AIHostOrders.Prune, AIHostOrders.Clear, AIHostOrders.IsOpen, AIHostOrders.Change, AIPlanRules.OrderState, AIPlanRules.OrderApplies, AIPlanRules.EarlierOrderApplies, AIPlanRules.HoldPutsOff, AIPlanner.OnOrderChanged, AIPlanner.SetOrder, AIPlanner.OnOrder, AIPlanner.OnHostGoal, AIExperience.Order
- 근거: work/2026-11/2026-11-05-자연어-명령/11-실게임4.md "적용된 host_said 답 18개 모두 order가 있다"
- 근거: work/2026-11/2026-11-05-자연어-명령/11-실게임4.md "ChatGPT #1(미룸)이 질문(#2) 뒤 `host_orders_open`에 남았다."
- 메모: 2026-11-05 자연어-명령. 4판(7fcef73)에서 확인했고, b2fa4cc의 막힘 상태(K2)는 core-tests만이다. 목록 상한 3과 orders 칸은 작업자가 덧붙이고 HEAD가 받아들였다. 지시 상태는 게임을 다시 켜면 사라지고 경험 사건만 남는다.

### F.plan-queue — 위쪽 LLM 계획(여러 단계 목표)
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 상위 LLM이 정한 계획을 유지하고 Decisions가 finish_goal로 현재 단계를 완료하면 다음 단계를 이어 간다. 수량 충족만으로 자동으로 단계를 넘기지 않는다. 명시 재계획은 현재 관측을 전달하며 실행 가능한 몸 동작을 유지한다.
- 메모: 이전 구현 설명은 당시 기록이다. 위쪽 LLM의 set 답에 목표 단계를 여러 개(설정 upperPlanMaxSteps, 기본 5) 받아 대기열로 들고, 지금 단계 하나만 목표로 건다(행동 판단기는 그 목표만 본다). 단계가 기존 목표 끝 판정으로 끝나면 다음 틱에 호출 없이 다음 단계로 넘어가고, 다 끝나면 plan_done을 한 번 부른다(그때 가던 답이 있으면 그 답을 보고 정함). 단계 실패·막힘이면 남은 단계를 멈추고 실패한 단계와 남은 계획을 보고에 실어 부른다. 새 계획은 남은 계획을 대체하고 keep은 이어 간다. 쓸 수 없는 단계(모르는 품목·놓이지 않은 제작대 등)가 있으면 답을 버리지 않고 그 앞 단계까지만 걸며 까닭을 다음 보고의 last_answer_rejected에 싣는다. 보고에 지금·남은 단계와 계획 사건(단계 끝·멈춤·모두 끝)을 싣는다.
- 관측 지점: Player.log "PLAN <이름>: …", "plan of n steps done", "owed …", "step k/n '…' not taken"; 보고 JSON `plan`·`plan_max`·events "계획 k/n단계 끝"
- 정책: pol.nl-plan-steps, pol.nl-watch-period, pol.behavior-roles
- 코드 앵커: AIPlanQueue.Set, AIPlanQueue.Advance, AIPlanQueue.Rest, AIPlanQueue.Stop, AIPlanQueue.Clear, AIPlanQueue.ForgetStopped, AIPlanQueue.Owns, AIPlanQueue.ReportJson, PlanStep.PlanStep, AIPlanRules.PlanCut, AIPlanRules.RefusedAskAgain, AIPlanRules.RefusedNote, AIPlanRules.StepDone, AIPlanRules.PlanOrphaned, AIPlanner.StartNextStep, AIPlanner.Apply, AIPlanner.CheckReason, AIPlanner.StepForm, AIPlanner.SetGoalFrom, AIInventory.CheckGoal, AIInventoryGoalForm.Check, AIGoalText.InventoryText
- 메모: 2026-11-06 후보902721e 추가 실게임: 실제 Gemini가 place CopperAnvil→craft CopperSword를 정하고 두 목표 완료·경험 기록 뒤 plan_done에서 다음 gather를 선택했다. 재료/시설은 기존 testcmd로 지급했으며 모든 목표·제작법 확인은 아니다. 근거 work/리팩토링-20261106/15-unity-and-live.md §3.1. 기존 확인한 선은 승격하지 않는다.
- 근거: work/2026-11/2026-11-05-자연어-명령/7-실게임3.md "단계 넘김 50번(Claude 22, ChatGPT 10, Gemini 10, Grok 8)이 모두 호출 없이 이어졌다."
- 메모: 2026-11-05 자연어-명령. 4판(7fcef73)까지 확인. 계획 끝 순간 가던 답이 set인 경우와 b2fa4cc의 단계 자르기(K1)는 실게임 미확인(U.nl-live-unseen). 놓이지 않은 제작대 단계(구리 모루)가 되풀이해 막히던 일은 2026-11-06 제작대-놓기의 place 목표로 풀렸다(F.place-goal, U.nl-place-station 닫힘).

- 메모: 2026-11-07 가방·장비 격리 후보: 가방 equip/unequip/hold/store/take/drop/give 계획은 keep/slot/where 계약을 전달한다. 공통 finish_goal의 단계 종료와 개별 물품 관측을 구별한다.

### F.fight-summary — 싸움 요약과 싸움 끝 계기
- 상태: 실게임 확인
- 확인한 선: 483b53ae46cc
- 종류: 제품
- 한 줄: 경험 기억이 싸움 끝(화면 7칸 안 적이 없어짐)을 기록할 때 끝 체력/조건 반영 최대와 그 싸움에서 받은 피해를 남기고, 캐릭터·월드마다 적 종류별 전적(싸움 수·쓰러짐)을 센다. 보고 `recent_fights`에 최근 10분 싸움을 한 줄씩(상대·처치/쓰러짐·받은 피해·끝 체력·전적, 사실만) 싣는다. 끝 체력이 절반 아래였거나 쓰러짐이 섞였으면 위쪽 LLM을 부른다(쓰러짐은 back_up이 부르면 fight_end는 안 보냄).
- 관측 지점: 경험 사건 `fight`(hp_end·took), 보고 JSON `recent_fights`, Player.log "PLANNER: report #n sent trigger=fight_end", "fight_end not reported"
- 정책: pol.nl-fight-end-call, pol.exp-no-extra-calls
- 코드 앵커: AIFightLog.Add, AIFightLog.Count, AIFightLog.Line, AIFightLog.ReportJson, AIExperience.RecentFightsJson
- 근거: work/2026-11/2026-11-05-자연어-명령/7-실게임3.md "싸움 줄 82개 중 끝 체력이 최대를 넘는 줄 0"
- 메모: 2026-11-05 자연어-명령. 관측 o.maxHealth는 기본 최대라 보고 me.hp와 행동 판단기 입력에는 105/100 같은 값이 남는다(U.nl-max-health). '쓰러진 적'은 화면에서 사라졌거나 체력 0인 적이고 누가 쳤는지는 모른다.

### F.problem-report — 문제 보고
- 상태: 실게임 확인
- 확인한 선: aa9503b075f2
- 종류: 제품
- 한 줄: 위쪽 LLM 답의 `problem` 칸(같은 적에게 계속 짐·목표 반복 막힘·자기 정보가 실제와 다름)을 경험 사건 '문제 보고'로 남기고, 내용은 say로 호스트에게 알린다. 사건으로 남는 길은 problem 칸 하나다.
- 관측 지점: 경험 사건 `problem`, 채팅 say
- 정책: pol.nl-problem-report
- 코드 앵커: AIExperience.Problem
- 근거: work/2026-11/2026-11-05-자연어-명령/7-실게임3.md "**문제 보고:** 11번이다."
- 메모: 2026-11-05 자연어-명령. 3판 문제 보고 11번 중 8번이 솥·상자 놓기·요리·보관 막힘(원인 둘 — 스폰 받침 칸 고름은 코어-안전지대 놓기 선택지로, 먼 곳의 기지 설 칸 없음은 2026-11-06 제작대-놓기의 설 칸 찾기 보강으로 다룸 — 같은 장면 재현은 못 봄), 2번이 구리 곡괭이를 곡괭이로 안 치는 보고였다(할 일). 2판 Gemini는 한 번 쓰러진 일을 보고해 기준보다 넓게 썼다.

### F.place-grid — 놓을 자리 후보와 지정 칸 놓기(사실을 붙이고 판단기가 고름)
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 선택한 물건·놓을 칸·설 칸에 접근해 입력하고 실제 가방 감소와 새 물체로 결과를 확인한다. 시간 경과·자리 정렬 지연·요청 반영 지연은 관측으로 전달한다. 실패 횟수만으로 자리를 숨기지 않으며 pending 입력은 중복 전송하지 않는다.
- 메모: 이전 구현 설명은 당시 기록이다. 상자·솥·제작대·벽·문·횃불을 놓을 때, 창 안 칸 가운데 물리적으로 불가능한 칸(차 있음·스폰 받침 3×3·내가 선 칸·모르는 칸·물 위에 못 놓는 아이템의 물 칸)만 빼고, 칸마다 결과 사실("여기 놓으면 X로 가는 길이 막힘" 등)과 결과 종류를 붙여 결과 종류마다 가장 좋은 자리 하나씩 최대 3개를 후보로 낸다(깨끗한 자리가 맨 앞). 판단기가 고른 칸은 `PlaceSkill(item, tile)`로 놓고, 끝은 타일 아이템이면 그 칸의 타일 층, 물체면 그 칸의 새 엔티티로 판정한다. 게임이 받지 않은 칸은 300초 동안 뺀다. 놓기는 옆 설 칸(대각 제외)에 가서 멈춰 선 뒤에만 누르고(놓을 칸에서 0.85~1.2칸 띠), 놓을 칸 가운데 0.9칸 상자에 물체(무덤 등)가 겹치면 '놓을 수 없음'을 사실로 준다. '한쪽에서만 놓기'는 그쪽 설 칸에서만 놓는다.
- 관측 지점: Player.log "SKILL 놓기 DONE|FAILED", "PLACE press #n … body (x,y) dist d", "설 칸에 왔지만 닿지 않음"; 판단 입력의 놓기 후보 문장
- 정책: pol.place-facts-not-filter, pol.behavior-roles
- 코드 앵커: AIPlaceGrid.All, AIPlaceGrid.Pick, AIPlaceGrid.Best, AIPlaceGrid.StandFor, AIPlaceGrid.PressStep, AIPlaceGrid.OverlapsPlaceBox, AIPlaceGrid.DiagonalStand, AIPlaceSpot.AllOptions, AIPlaceSpot.Candidates, AIPlaceSpot.ForTile, AIPlaceSpot.PlacedAt, AIPlaceSpot.EntityOn, AIPlaceSpot.CanGoOnWater, AIPlaceSpot.NoteRefused, AIPlaceSpot.OverlapAt, PlaceSkill.PlaceSkill/5, PlaceSkill.Start, PlaceSkill.Tick, PlaceSkill.Retry, PlaceSkill.Pick, AIWorldFacts.IsKnown, AIWorldFacts.IsExcludedPlacedEntity, AIWorldFacts.IsPlacedObjectOrDoor
- 근거: work/2026-11/2026-11-05-코어-안전지대/5-단계1-측정.md "상자·제작대 모두 DONE으로 잡힘"
- 근거: work/2026-11/2026-11-05-코어-안전지대/22-짧은확인.md "벽 5칸은 모두 옆 설 칸에서 첫 누름에 놓임(대각 0)"
- 근거: work/2026-11/2026-11-05-코어-안전지대/23-합치기전수정.md "어느 출발점에서도 멈춰 설 자리가 있다(시험 M1c 56/56)"
- 근거: work/2026-11/2026-11-05-코어-안전지대/29-문단계-실게임.md "모두 첫 누름이었고, 놓을 칸 중심에서 0.94~1.13칸이었다"
- 메모: 2026-11-05~06 코어-안전지대 단계 1~3(base-s1~s3, ai-character 0869165)과 문 단계 멈춰 서기(4238cce). 확인한 선은 29-문단계-실게임 빌드 4238cce 기준이다(문 다시 달기 6번 모두 첫 누름, 버프·600fps 포함). 멈춰 서기는 한 서버 틱 이동(nudge)과 걸음 배우기를 문 단계와 같이 쓴다(Core/AISettle.cs). 물체 겹침 사실·'지나서 가는' 바깥 설 칸 확인은 아직 실게임에서 보지 못했다(U.base-0869165-unseen). 대각 설 칸 제외는 실측 거절 2건에서 온 규칙이고 게임 범위로는 닿는다(U.base-diagonal-stand).

### F.edge-plan — 기지 둘레 후보·계획·둘레 기억
- 상태: 실게임 확인
- 확인한 선: baa5a5cb9a08
- 종류: 제품
- 한 줄: 기지 점(코어 등)과 품을 것에서 지형을 따르는 둘레 후보를 계산한다(반경 값 없음). 걷기 거리마다 안쪽을 열린 바깥에서 떼어 놓는 가장 적은 걸을 수 있는 칸 집합(최소 정점 절단)을 찾고, 지배당한 후보를 빼 최대 4개를 낸다. 후보마다 틈 수, 막을 칸 수(가장 긴 틈), 필요한 블록과 가진 양, 물 칸, 둘러싸이는 넓이, 확인 못 한 경계 칸, 밖에 남는 코어·받침·제단 같은 사실을 붙인다. 틈은 8방향으로 이어진 끊는 칸 목록이고 7칸씩 묶음으로 나눈다. 고른 계획(기준점·안쪽 칸·틈 칸·문 자리)과 놓은 것(벽·문·횃불)은 캐릭터·월드별 memory.json에 둘레 기억으로 남고, 새 계획의 틈 위에 AI가 놓은 문이 있으면 둘레 문으로 받아들인다. 지도 자료(basemap.json: 물체·타일 격자·미리보기/확정 계획)를 만든다. 지금은 시험 명령(`/ai edge`, `pick N [문틈]`)으로 고른다(위쪽 LLM 연결은 단계 4).
- 관측 지점: Player.log "EDGE option [n] …", "EDGE plan …"; 채팅 `/ai edge`; AIPlayers basemap.json, memory.json `edge*`
- 정책: pol.base-edge-facts, pol.base-llm-decides-condition, pol.base-gap-batch, pol.base-geo-tool, pol.base-safe-meaning
- 코드 앵커: AIEdge.RequestOptions, AIEdge.Pick, AIEdge.BuildGrid, AIEdge.List, AIEdge.Import, AIEdge.Export, AIEdge.NotePlaced, AIEdge.ClearMemory, AIEdge.WriteMap, AIEdge.ObjectsInWindow, AIBaseEdgeCore.Options, AIBaseEdgeCore.Group, AIBaseEdgeCore.Batches, AIBaseEdgeCore.AdoptDoors, AIBaseEdgeCore.Annotate, EdgeOption.Facts, AIBaseMap.Json, AIBaseMap.Overlay
- 근거: work/2026-11/2026-11-05-코어-안전지대/20-실측.md "더 놓을 블록 없음(틈의 내 문 1칸은 그대로 둘레 문)"
- 근거: work/2026-11/2026-11-05-코어-안전지대/34-내문-실게임.md "`/ai edge`(t=1046.6)는 후보 4개를 냈다"
- 메모: 2026-11-05 코어-안전지대 단계 2. 씨앗 3×3 최소 조건은 사용자 결정으로 뺐고(pol.base-llm-decides-condition), 블록 60 상한은 남았다. 모르는 칸은 열린 것으로 친다. 지리 정보 조회 도구(글자 격자·물체 좌표·계획 겹치기·그린 지도)는 단계 4다. 2026-11-06 a07166f부터 둘레를 다시 고르면(pick) 새 틈 위가 아닌 옛 둘레 문을 둘레 문에서 빼고, 서 있으면 내 문으로 옮긴다(RepickOldDoors, 34-내문-실게임에서 확인). 확인한 선은 34-내문-실게임 빌드 a07166f 기준이다. 옛 계획 벽이 남는 것 등은 U.repick-leftovers.

### F.edge-fill — 둘레 메우기 묶음·문 달기·안쪽 횃불
- 상태: 실게임 확인
- 확인한 선: 62756d785f96
- 종류: 제품
- 한 줄: 계획이 있고 목표가 돌면 판단기에 둘레 메우기 묶음(틈마다 7칸까지, 마지막 묶음이 틈을 닫음)·문 달기(양옆 벽이 선 뒤에만, 대각 틈이면 필요한 옆 벽을 묶음에 더함)·안쪽 횃불(이미 놓은 횃불에서 먼 순 2개) 후보를 낸다. 묶음은 다 메움·재료 떨어짐(진행 사실)·막힘으로 끝나고 안에 코드 반응 분기가 없다. 틈을 닫는 칸과 문은 안쪽 자리와 바깥 자리 후보를 함께 내고, 바깥 자리에는 "바깥에서 닫음, 둘레 문 (x,y)로 드나듦" / "둘레에 문 없음, 들어가려면 벽 하나를 부숴야 함" / "둘레에 아직 열린 곳 N곳 있음" 중 맞는 사실을 붙인다. 둘레 후보는 목표의 진행 후보를 밀어내지 않게 목표 후보 뒤에 자기 몫으로 들어간다.
- 관측 지점: Player.log "SKILL 둘레 메우기 DONE|FAILED", "EDGE door: … not offered: 양옆 벽 대기"; 판단 입력의 둘레 후보 문장
- 정책: pol.base-gap-batch, pol.edge-close-outside, pol.base-safe-meaning, pol.behavior-roles
- 코드 앵커: AIEdge.Candidates, AIEdge.Batches, AIEdge.BlockFor, AIEdge.IsLastOpen, AIEdge.SidesReady, EdgeFillSkill.EdgeFillSkill, EdgeFillSkill.Start, EdgeFillSkill.Tick, EdgeFillSkill.Cleanup, AIBaseEdgeCore.DoorSides, AIBaseEdgeCore.SidesReady, AIBaseEdgeCore.IsLastOpen, AIBaseGuard.OutsideCloseText/6
- 근거: work/2026-11/2026-11-05-코어-안전지대/14-단계3-측정.md "묶음을 다 메운 뒤: 문 달기 DONE"
- 근거: work/2026-11/2026-11-05-코어-안전지대/14-단계3-측정.md "6칸 중 4칸 메움, 남은 2칸"
- 근거: work/2026-11/2026-11-05-코어-안전지대/34-내문-실게임.md "baseprobe로 본 실제(두 칸 wall, 문 물체)와 같다"
- 메모: 2026-11-05 코어-안전지대 단계 2·3. 바깥 닫기(사용자 결정 A)와 '아직 열린 곳 N곳' 문장(HEAD 결정)은 94e1f1a·8faa400에서 들어왔고 실게임에서 따로 보지 않았다. 물 위 흙 블록 메우기는 시험 월드에 물이 없어 못 쟀다(U.base-0869165-unseen). 확인한 선은 34-내문-실게임 빌드 a07166f 기준이다(벽 2칸 2.4초 → 문 첫 누름 1.0초, 9df72b7 놓기·이동 공용 코드 변경 뒤 다시 봄).

### F.door-pass — 자기 문 지나가기(열기→지나가기→닫기)와 열린 문 닫기
- 상태: 실게임 확인
- 확인한 선: 206bb61109d3
- 종류: 제품
- 한 줄: AI 입력에 상호작용(E, 0x20)을 한 ServerTick 동안 누르는 비트를 두고, 문을 보고 문 중심에서 누르는 창(0.55~0.70칸, 넘으면 0.85)에 멈춰 선 뒤 게임이 받는 상태일 때 누른다. 문 지나가기는 열기→지나가기→닫기 한 묶음(문 단계 12초 상한)이고, 걷기(게임 길찾기가 길 없음이거나 끼면 자기 문을 지날 수 있는 칸으로 보는 A*)·공격·물러나기·치우기가 자기 문을 지날 때 같은 단계 기계를 쓴다. 동작이 문 옆에서 끝나면 지나온 자기 문을 닫고 끝낸다(꼬리 닫기). 문 단계 중 다른 후보에는 "고르면 지금 지나는 문 (x,y)이 열린 채 남음"이 붙고 따로 닫기 후보는 없다. 열려 있는 자기·호스트 문에는 '문 열려 있음 — 닫기' 후보를 낸다. 보내지 못한 누름은 기술이 끝나거나 바뀔 때·2.5초 뒤 버리고 그 사실을 실패 문장에 남긴다. 자동문(전기)은 누르지 않고 지나가기만 하는 자리만 있다.
- 관측 지점: Player.log "DOOR step …", "press #n … dist", "BODY: … dropped unsent: …", "SKILL 문 지나가기|문 닫기 DONE|FAILED"
- 정책: pol.door-pass-bundle, pol.base-safe-meaning
- 코드 앵커: AIDoorPass.Step, AIDoorPass.Press, AIDoorPass.HoldsDoor, AIDoorPass.GetsLeftOpenNote, DoorPassSkill.DoorPassSkill, DoorPassSkill.Start, DoorPassSkill.Tick, DoorPassSkill.Cleanup, DoorCloseSkill.DoorCloseSkill, DoorCloseSkill.Setup, AIEdge.DoorCloseCandidates, AIEdge.TailStart, AIEdge.TailTick, AIEdge.TailAbort, AIBaseGuard.TailStep, AIBaseGuard.StartTail, AINavigator.DoorStep, AINavigator.OwnDoorToward, AIBody.NavDoorStep, AIBody.DoorStepOn, AIBody.DoorStepNow, AIBody.UseBitFor, PressQueue.Ask, PressQueue.Drop
- 근거: work/2026-11/2026-11-05-코어-안전지대/20-실측.md "누른 거리 열기 0.60~0.79칸, 닫기 0.62~0.81칸"
- 근거: work/2026-11/2026-11-05-코어-안전지대/20-실측.md "걷기·공격·문 지나기 기술 1.5~1.9초(문 단계 시작→닫힘)"
- 근거: work/2026-11/2026-11-05-코어-안전지대/22-짧은확인.md "공격 끝 꼬리 닫기가 처치 1.7초 뒤 누름 1번(0.60칸)에 닫음"
- 근거: work/2026-11/2026-11-05-코어-안전지대/29-문단계-실게임.md "들어오기 6번, 안→밖 3번, 밖→안 3번이 모두 성공했다"
- 메모: 2026-11-05~06 코어-안전지대 단계 2·3과 문 단계 멈춰 서기(door-settle, 4238cce). 짧은 확인의 걸어 들어오기 1/4 실패(누르는 창이 몸 한 걸음 0.28칸보다 좁음)는 4238cce에서 고쳤다: 멈춰 서기 보정을 서버 틱 한 번 이동(nudge, BodyInput.nudge·AIBody.NudgeFor·TickNudge)으로 보내고, 72방향 중 한 걸음이 창(축 방향 0.50 초과, 옆 0.30 미만)에 떨어지는 쪽을 겨누며, 실제 걸음 길이를 배운다(Core/AISettle.cs, 놓기와 같이 씀). 확인한 선은 29-문단계-실게임 빌드 4238cce 기준이다(문 지나기 32번·누름 72번 모두 첫 누름, 30·560~600fps, 버프 ×1.3). 그 뒤 a07166f가 지나간 문 닫기(owe)를 자기 문 전부로 넓히고 계획 없을 때 가던 쪽에서 닫게 했다(34-내문-실게임에서 내 문 닫기 2번 0.1·0.7초, 이 선의 확인은 옮기지 않음). 누름 대기열 버림(Core/AIPressQueue.cs)은 실게임에서 보지 못했다(U.base-0869165-unseen). 남은 것은 U.door-settle-leftovers. "적은 닫힌 문을 못 연다"는 게임 규칙 door-enemy(추정)에 기댄다.

### F.base-reach — 적마다 닿는 길과 위협 판정(닿지 않는 적은 판단을 부르지 않음)
- 상태: 실게임 확인
- 확인한 선: c1453ec74a43
- 종류: 제품
- 한 줄: AI 둘레 49×49 걷기 격자(열림·자기 문·모름·울타리/물·막힘)로 적마다 '나에게 걸어올 길'을 계산하고 0.4초 보관한다(닫힌 문은 못 지남, 나는 적은 울타리·물을 넘음, 모르는 칸은 열림, 시야 밖 둘레 칸은 마지막 본 상태). 걸어올 길이 없고 1.5칸 밖이거나 사이에 닫힌 문이 있으면 그 적은 자기 방어·전투 중 재판단·가까운 싸움 묶음·'적 사라짐' 계기를 일으키지 않는다. 맞으면(피해 판정 한 곳: 새 최대보다 위였던 체력이 함께 내려간 것과 굶주림은 맞음 아님) 칠 수 있는 적까지 위협으로 넓힌다. 공격 후보에는 기지 안/바깥·둘레 거리, 닿는 길(열린 길 N칸 / 문 (x,y)을 지나 / 보이지 않는 곳을 지나야 / 벽 너머라 지금 닿을 수 없음), 벽·문을 부수는 종류(게임 데이터의 타일·물체 피해 키, 다음 실측, 다음 규칙) 사실을 붙인다. 어떤 길로도 닿지 않는 적에게는 공격 후보를 내지 않고 막힌 대상 메모에 적는다. 기지가 있으면 대상은 기지 안의 적 먼저, 합쳐 4마리까지다.
- 관측 지점: Player.log "SELF-DEFENCE …", "EDGE tile damage <적>: bump b melee m charge c (game data; breaker=…)"; 채팅 `/ai edge reach`; 판단 입력의 공격 후보 사실
- 정책: pol.base-keep, pol.combat-options, pol.behavior-roles
- 코드 앵커: AIEdge.ReachNow, AIEdge.ReachOf, AIEdge.Threat, AIEdge.AnyThreat, AIEdge.KillFacts, AIEdge.GuardGridAround, AIEdge.TileBreakOf, AIEdge.CannotCome, AIBaseGuard.Reach, AIBaseGuard.IsThreat, AIBaseGuard.DefenceStep, AIBaseGuard.KillFacts, AIBaseGuard.HurtAmount, AIBaseGuard.HurtBy, AIBaseGuard.IsBreaker/1, AIBaseGuard.NoDataBreakNote, AIBrain.SelfDefence, AIBrain.CloseFight, AIEdge.AssessThreatWithinRange, AIEdge.ObserveThreat, AIEdge.ObserveAnyThreat, AIEdge.LastAssessedCannotCome, AIThreatHistory.Record, AIThreatHistory.LastAssessedCannotCome
- 근거: work/2026-11/2026-11-05-코어-안전지대/20-실측.md "닫힌 문 너머 적 1마리(8.6칸)·4마리(2.9~10칸) 60초+60초: SELF-DEFENCE 0, SEND 0"
- 근거: work/2026-11/2026-11-05-코어-안전지대/22-짧은확인.md "EDGE tile damage Caveling: bump 198 melee 0 charge 0 (game data; breaker=True)"
- 메모: 2026-11-05~06 코어-안전지대 단계 3. 단계 1 측정의 울타리 너머 적 100초 287회가 0회가 됐다(14-단계3-측정). 기억으로 판단한 '못 옴'에는 그 전제가 사실로 붙는다. 시야 밖에서 둘레가 부서지면 AI가 그곳을 보기 전까지 그쪽 적은 '못 옴'으로 계산된다(HEAD 보고, 남은 빈틈). 0869165에서 문을 물체 피해 키로도 판단하고 못 읽은 키는 '모름'으로 둔다(실게임 전). 피해 판정 한 곳(HurtAmount)은 F.vitals의 로그·맞은 합계와 체력 감소 계기도 쓴다.

### F.base-repair — 부서진 둘레 찾기·보수 묶음·문 다시 달기·보수 재료
- 상태: 실게임 확인
- 확인한 선: 8e8596ba9f67
- 종류: 제품
- 한 줄: 둘레(계획 틈 칸 + 안쪽의 4방향 이웃 중 안쪽이 아닌 칸, 문 옆 자연 벽 포함)의 칸마다 지금 상태를 둘레 기억과 비교해 놓았던 벽 없어짐·달았던 문 없어짐·자연 벽/물체가 있던 칸 열림으로 나눈다. 보이는 칸은 실제 상태, 안 보이는 칸은 마지막 본 상태다(memory.json `edgeSeen`). 부서진 칸을 구멍별로 7개씩 묶은 보수 묶음(메우기와 같은 기술, 이름 '둘레 보수')과 '기지 문 다시 달기'(양옆 벽이 있을 때만)를 낸다. 새로 부서진 곳이 보이면 계기 '기지 둘레 부서짐 N곳'으로 판단을 한 번 부르고, 그 계기는 다른 계기에 덮이지 않고 다음 판단에 붙는다. 재료 사실(가진 벽 블록·문, 둘레 칸 수, 부서진 곳, 확인 못 한 둘레, 적이 부순 벽은 떨구지 않음)을 붙이고, '보수 재료 모으기'는 둘레 칸이 아닌 가장 가까운 벽 하나를 캔다. 둘레 벽·뿌리·문 옆 벽을 캐는 일반 후보에는 "캐면 둘레가 열림"·"캐면 문이 떨어짐"을 붙인다. 기지 후보는 목표 진행으로 세지 않는다.
- 관측 지점: Player.log "EDGE breaches …", 판단 계기 "기지 둘레 부서짐 N곳(…)"; 채팅 `/ai edge breaks`, `/ai edge repair K`
- 정책: pol.base-keep, pol.base-gap-batch, pol.behavior-roles
- 코드 앵커: AIEdge.BreachesNow, AIEdge.RepairBatches, AIEdge.RepairCovers, AIEdge.NewBreach, AIEdge.WithPendingBreach, AIEdge.GuardCandidates, AIEdge.MemoryNow, AIEdge.OpensEdge, AIEdge.EdgeWallNote, AIEdge.EdgeTileNote, AIEdge.OwedDoor, AIBaseGuard.Breaches, AIBaseGuard.RepairBatches, AIBaseGuard.MaterialFacts, AIBaseGuard.MaterialSpots, AIBaseGuard.BreachTrigger, AIBaseGuard.TakePending, AIBaseGuard.CountsAsGoalProgress, EdgeLastSeen.Note, EdgeLastSeen.Load, AIEdge.WallFact, AIBaseGuard.WallFact, EdgeWallFact.EdgeWallFact
- 근거: work/2026-11/2026-11-05-코어-안전지대/22-짧은확인.md "3번 모두 성공: (2,1)·(3,0)은 설 칸 (2,0)"
- 근거: work/2026-11/2026-11-05-코어-안전지대/22-짧은확인.md "판단 입력에 보수 후보와"
- 메모: 2026-11-05~06 코어-안전지대 단계 3. 둘레 뿌리 캐기는 "캐면 둘레가 열림" 사실이 붙어도 판단 모델이 고른다(U.base-root-edge). 보수 재료 대상을 둘레 칸이 아닌 벽으로 정한 것은 작업자 정의를 HEAD가 받아들인 것이다.

### F.base-pickup — 놓은 물체 치우기·다시 놓기
- 상태: 실게임 확인
- 확인한 선: 4fbc18864ce5
- 종류: 제품
- 한 줄: 곡괭이나 빈손을 들고(근접 무기뿐이면 실패) 옆에 서서 치며, 칠 때마다 5초 안에 체력이 줄어야 하고, 없어지면 3초 안에 가방 +1을 기다린다. 새 칸이 있으면 이어서 놓는다(문 자리면 안쪽에서만). 둘레 기억에서 옛 칸을 지운다. 후보 `edgepickup`.
- 관측 지점: Player.log "SKILL 치우기 DONE|FAILED"; 채팅 `/ai edge pickup x z [nx nz]`
- 정책: pol.base-keep, pol.behavior-roles
- 코드 앵커: PickupSkill.PickupSkill, PickupSkill.Start, PickupSkill.Tick, PickupSkill.Alive, PickupSkill.Cleanup, AIBaseGuard.PickupStep, AIBaseGuard.PickupHoldChoice, AIEdge.ForgetPlaced
- 근거: work/2026-11/2026-11-05-코어-안전지대/14-단계3-측정.md "DONE 2.9초, 체력 2→1→0(두 번), 가방 +1"
- 근거: work/2026-11/2026-11-05-코어-안전지대/22-짧은확인.md "붙은 자리에서 치우기 1.8초, 캐기 1.7초, 걷기 1.9초(모두 누름 1번)"
- 메모: 울타리 타일은 물체가 아니라 AI가 치울 수 없다(14-단계3-측정). 게임 규칙 '놓은 물체 회수'가 rules.json에 있다.

### F.base-retreat — 기지 안으로 돌아가기
- 상태: 실게임 확인
- 확인한 선: dd931882ee2b
- 종류: 제품
- 한 줄: 물러나기 목적지는 기지가 있으면 기지 안쪽의 걸을 수 있는 칸(받침 아님)이다. 안에 있으면 기지 점에 가장 가까운 칸, 밖에 있으면 문을 지나 가장 적게 걷는 안쪽 칸이다. 기지가 없으면 (0,−2)(걸을 수 없으면 받침 바로 바깥 고리의 가까운 칸)이다. 타일 계획이 문에 막히면 목적지로 바로 걷고(걷기가 자기 문을 지남), 도착과 '10칸 안 적 없음' 멈춤은 안쪽에 들어간 뒤에만 한다. 후보 설명은 "기지 안으로 돌아가기 — 안쪽 칸 (x,y)까지 문 (dx,dy)을 지나 N칸".
- 관측 지점: 판단 입력의 retreat 후보 문장, Player.log "SKILL 물러나기 DONE|FAILED"
- 정책: pol.base-keep, pol.combat-options
- 코드 앵커: AIEdge.RetreatPoint, AIEdge.StandTile, RetreatSkill.RetreatSkill, RetreatSkill.Start, RetreatSkill.Tick, RetreatSkill.NextLeg
- 근거: work/2026-11/2026-11-05-코어-안전지대/14-단계3-측정.md "체력 30에서 모델이 고름: 문 분기로 1.9초"
- 근거: work/2026-11/2026-11-05-코어-안전지대/34-내문-실게임.md "(-2,0): 문 (-4,-6)을 지나 19칸"
- 메모: 후보 키가 `retreat:0,-1`에서 안쪽 칸 좌표로 바뀌었다. 물러나기 뒤 열린 문으로 적이 따라 들어와 쓰러진 일(14-단계3-측정)은 꼬리 닫기(F.door-pass)로 고쳤다. 확인한 선은 34-내문-실게임 빌드 a07166f 기준이고 판정은 '대체로 통과(아닌 1건)'다: 계획을 다시 고른 뒤 먼 바깥에서 물러날 곳이 "벽 너머라 지금 닿을 수 없음"으로 1번 나왔다(U.retreat-unreachable-once). 물러나기 문장 끝에 공격용 문구가 붙는다(U.mydoor-live-leftovers).

### F.base-testcmds — 기지 안전 측정 탐침과 시험 명령
- 상태: 사용 확인(개발 도구)
- 종류: 개발
- 한 줄: `testcmds.on`일 때만 쓰는 측정·시험 명령. `/ai baseprobe immune|self|door|hits`(immune 칸, AI가 플레이어로 세어지는지, 문 상태, 적이 벽·문을 때리는지)와 `/ai place <id> [x z [force]]`, `/ai edge`(후보·pick·adopt·mydoor·door·breaks·repair·pickup·reach·guard·close·forget). 관찰을 켰을 때만 탐침이 판단 고리에서 돈다.
- 관측 지점: 채팅 `/ai baseprobe …`, `/ai edge …`의 답, Player.log "BASEPROBE …", "EDGE …"
- 정책: pol.behavior-roles
- 코드 앵커: AIBaseProbe.Command, AIBaseProbe.Frame, AIEdge.Command, AIEdge.GuardCommand
- 근거: work/2026-11/2026-11-05-코어-안전지대/5-단계1-측정.md "AI 엔티티 id=Player, PlayerGhost 있음"
- 메모: 단계 4 전까지 둘레 계획은 이 명령으로 고른다(위쪽 LLM 연결 전).

### F.place-goal — goto·place 목표와 장소(장소 식별자·걸어가기·도착·끝)
- 상태: 실게임 확인
- 확인한 선: 5baee9360fa2
- 종류: 제품
- 한 줄: 위쪽 LLM(또는 호스트 `/ai goal goto|place`)이 `goto(장소)`·`place(물건, 장소, count)`를 걸 수 있다. 장소는 `base`·`here`·`core`·`tile:x,y`(둘레 반경 3)·`station:<ID>@x,y`·`chest:`·`pot:`·`ore:<ID>@x,y`·`farm`·`water:x,y`·`rect:x1,y1,x2,y2`·`line:x1,y1,x2,y2`이고, 모두 AI 기억에 있는 지점으로 풀린다. 장소 밖이면 기존 이동(파들어가기·자기 문 지나가기)으로 걸어가 노린 설 칸에 닿으면 도착이다. 장소 안이면 놓기 선택지(F.place-grid)로 놓고, 가방에서 줄고 그 칸에 새로 놓인 물체로 끝난다(옛 같은 물건은 세지 않음). count가 없으면 1이다. 길이 없거나 놓을 칸이 없으면 그 까닭을 막힘 문장으로 돌려준다.
- 관측 지점: Player.log "PLACEGOAL", "SKILL 놓기 DONE", 끝 문장 "X 놓음 (x,y) d칸 · 고른 자리 (x,y)"; 위쪽 보고의 계획 `[place … → craft …]`
- 정책: pol.place-llm-decides-where, pol.place-where-anywhere, pol.place-facts-not-filter
- 코드 앵커: AIPlaceGoal.OnGoalSet, AIPlaceGoal.Resolve, AIPlaceGoal.Arrived, AIPlaceGoal.CheckGoto, AIPlaceGoal.CheckPlace, AIPlaceGoal.GoalDone, AIPlaceGoal.StallNote, AIPlaceGoal.Candidates, AIPlaces.Format, AIPlaces.Parse, AIPlaces.WhereForm, AIPlaces.PlaceCount, AIPlaces.EndOf, AIPlaces.EndText, AIPlaces.NewPlacedIndex, AIGoals.SetGoal/8
- 근거: work/2026-11/2026-11-05-제작대-놓기/15-실게임4.md "새 월드, 모루는 가방에만."
- 근거: work/2026-11/2026-11-05-제작대-놓기/15-실게임4.md "옛 모루(2.2칸)가 아니라 새로 놓은 칸을 적음"
- 근거: work/2026-11/2026-11-05-제작대-놓기/6-실게임.md "16칸 밖에서 아는 길 45칸·벽 1개를 지나 8.1초에 도착(1.2칸)"
- 메모: 2026-11-06 제작대-놓기(ai-character 9df72b7). 확인한 선은 마지막 실게임(15-실게임4) 빌드 9df72b7 기준이다. 모루 사례(가방에만 있는 CopperAnvil → 첫 답 place base → craft CopperSword)는 1·2·4판에서 끝까지 됐다. craft 거절 문장은 모델이 처음부터 place를 계획해 실게임에서 나온 적이 없다(F.place-report). home 도착 판정 변경(노린 설 칸 0.7칸)도 이 선 위에 있다.

### F.place-shape — 여러 칸 놓기(rect·line·count, 한 번에 30칸)
- 상태: 실게임 확인
- 확인한 선: 3b4b53380312
- 종류: 제품
- 한 줄: place의 장소가 `rect:`·`line:`이거나 count가 2 이상이면 여러 칸을 차례로 놓는다(상한 30칸). 받을 때 놓을 수 없는 칸(벽·물체·스폰 받침·이미 그 물건이 있는 칸·물 전용 물건의 땅 칸)은 까닭과 함께 빼고, 가방이 모자라면 "가방에 X n개, 놓을 칸 m개 — n칸만 놓임"을 알린다. 실행 중 칸 때문에 실패한 칸은 건너뛰고, 칸과 상관없는 실패(손 칸 자리 없음 등)는 '멈춤: 까닭'으로 끝낸다. 끝은 이번 목표가 놓은 칸 수로 판정하고(0칸이면 막힘), 건너뛴 칸은 까닭별로 묶어 끝·막힘 문장에 싣는다.
- 관측 지점: Player.log "PLACE", 끝 문장 "m칸 중 k칸에 X 놓음", 막힘 문장 "건너뛴 칸 …"
- 정책: pol.place-any-placeable, pol.place-facts-not-filter
- 코드 앵커: PlaceTilesSkill.PlaceTilesSkill, PlaceTilesSkill.Start, PlaceTilesSkill.Tick, PlaceTilesSkill.Skip, PlaceTilesSkill.Cleanup, AIPlaceGoal.NoteSkip, AIPlaces.ShapeTiles, AIPlaces.ShapeCount, AIPlaces.ShapePlaced, AIPlaces.SkippedText, AIPlaces.ShortFact
- 근거: work/2026-11/2026-11-05-제작대-놓기/6-실게임.md "4칸 놓고 끝남"
- 근거: work/2026-11/2026-11-05-제작대-놓기/13-실게임3.md "물 위 다리를 게임이 실제로 받는 것을 확인"
- 메모: 2026-11-06 제작대-놓기. 확인한 선은 다리 line을 본 3판 빌드 8d1e820 기준이고 지금(9df72b7) 선 서명과 같다(R-b는 이 선 밖의 물·땅 판정만 바꿈, F.placeable). 4판은 한 칸·count 다리를 봤다. 손 칸이 다 찬 '멈춤'은 실게임에서 못 봤다(U.place-live-unseen).

### F.placeable — 놓을 수 있는 물건 판정과 물·땅 칸
- 상태: 실게임 확인
- 확인한 선: 47fb018eb244
- 종류: 제품
- 한 줄: 게임 데이터 `ObjectInfo.objectType == PlaceablePrefab`이면 place로 놓을 수 있다(씨앗만 밭 기능 몫으로 뺌). 칸 바꾸는 것(바닥·다리·벽·울타리·레일)인지 물체인지는 `tileType`으로 가른다. 물 칸은 게임 배치 플래그가 있거나 칸 종류가 다리면 받고, 다리는 땅 칸을 받지 않아 물 칸만 선택지에 나온다. 둘레 5칸 안에 아는 물 칸이 없으면 "물 위에만 놓임 — 지금 둘레 5칸 안에 놓을 수 있는 아는 물 칸이 없음"으로 바로 막힌다. 보고의 `can_do.place`는 가방 속 놓을 수 있는 물건이다.
- 관측 지점: Player.log "PLACE: X can go on water (game placement flag)", "cannot go on ground (settings …, land flag …, tile …)", "PLACE options: X n of m (…)"
- 정책: pol.place-any-placeable
- 코드 앵커: AIPlaceGoal.IsPlaceable, AIPlaceGoal.BagPlaceable, AIPlaceSpot.CanGoOnWater, AIPlaceSpot.CanGoOnLand
- 근거: work/2026-11/2026-11-05-제작대-놓기/15-실게임4.md "물 칸만 나옴"
- 근거: work/2026-11/2026-11-05-제작대-놓기/15-실게임4.md "땅 칸 거절 되풀이 없음"
- 메모: 2026-11-06 제작대-놓기. 다리가 땅을 받지 않는 게임 속성 이름은 추정이라 칸 종류(bridge)로 정했다(14-실게임3-반영). 방향(회전)이 있는 물건은 기본 방향으로만 놓는다(U.place-rotation).

### F.place-report — 보고의 places·can_do와 craft 거절 문장
- 상태: 실게임 확인
- 확인한 선: 527d5ad434e1
- 종류: 제품
- 한 줄: 위쪽 보고에 아는 장소 목록 `places`(고정 base·core·farm, 내 물건, 남의 제작용 제작대, 광석 종류별, 물; 900자 상한, 넘치면 개수)와 `can_do.place`(400자)·`can_do.goto`를 넣고, `can_do.craft`는 제작대별로 묶어 재료 다 있는 것부터 40개·1800자까지 낸다(`craft_more`). 기존 known_stations는 places로 합쳤다. craft 목표가 모르는 제작법으로 막히면 거절 대신 고칠 길을 담은 문장(가방에 제작대 있음 → place 먼저 / 만들 곳 앎 → craft 제작대 → place → craft / 모름)을 돌려준다.
- 관측 지점: 위쪽 보고 JSON의 places·can_do; 거절 사유 문장
- 정책: pol.place-llm-decides-where, pol.place-craft-list
- 코드 앵커: AIPlaceGoal.ReportFields, AIPlaces.Pick, AIPlaces.CraftGroups, AIPlaces.CraftFact, AIPlaces.CapChars, AIPlaces.CapGroups, AICraft.CraftFact
- 근거: work/2026-11/2026-11-05-제작대-놓기/6-실게임.md "석상은 없다"
- 메모: 2026-11-06 제작대-놓기. 확인한 선은 1판 빌드 4e87a87 기준이다. 남의 상자·솥은 장소로 내지 않는다(쓰지 않는 기존 규칙). 실게임에서 본 것은 places·can_do이고, 거절 문장은 시험(PG)만 지났다(U.place-live-unseen).

### F.edge-place-facts — 놓기와 기지 둘레가 겹칠 때의 사실
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: place의 한 칸 선택지·rect/line 묶음 문장에 그 칸이 둘레와 어떻게 겹치는지(둘레 틈을 메움·마지막 틈을 바깥에서 닫음과 문 있음/없음·문 자리를 막음·문 자리에 문 달기·양옆이 벽이어야 문이 놓임)를 사실로 붙인다. 막지 않고 판단기가 고른다. 둘레 칸 종류 선택지는 결과 종류별 3개 상한을 넘어 2개까지 더 낸다. place는 둘레 계획을 만들거나 바꾸지 않고, 둘레 칸·문 자리에 놓은 벽·문만 둘레 기억에 넣는다(안쪽 벽은 넣지 않음, 계획 밖 문은 F.my-door).
- 관측 지점: 판단 입력의 놓기 후보 문장 "그중 n칸이 둘레 틈 … 위(…그 틈을 메움)"; Player.log "EDGE memory: …"
- 정책: pol.place-facts-not-filter, pol.edge-close-outside
- 코드 앵커: AIEdge.MarkPlaceGrid, AIEdge.ShapeEdgeNote, AIPlaces.EdgeFact, AIPlaces.ShapeEdgeText
- 근거: work/2026-11/2026-11-05-제작대-놓기/6-실게임.md "rect 선택지에"
- 메모: 2026-11-06 제작대-놓기. rect 묶음의 틈 메움 사실(1판)과 둘레 메우기의 바깥 닫기 선택지(1·2판)는 봤고, 한 칸 place의 틈 칸 선택지는 2판에서 개수 상한으로 빠졌다가 고친 뒤 실게임에서 못 봤다. 둘레 기억 벽·문 수는 4판 둘레 메우기에서 메운 수와 같았다.

### F.my-door — 내 문(계획 밖에서 놓은 문) 기억·지나가기·닫기
- 상태: 실게임 확인
- 확인한 선: 7f7e09adbc05
- 종류: 제품
- 한 줄: 둘레 계획 밖에서 place로 놓은 문은 '내 문' 목록(월드별 저장)에 들어간다. 걷기의 문 고르기는 둘레 문과 내 문을 함께 보고 돌아감이 가장 짧은 문을 고르되, AI와 목적지가 둘 다 기지 안쪽이면 둘레 문을 쓰지 않는다. 파들어가기 계획은 둘레 문·내 문 칸을 비용 2로 지나갈 수 있는 칸으로 보고(보이는데 문 대신 다른 물체가 있으면 막힘), 걸을 때 열기→지나가기→닫기를 한다. 문 칸 막힘 표시 제외와 놓기 격자의 문 통로도 내 문을 본다. '문 열려 있음 — 닫기' 후보는 내 문에도 낸다. 보수·부서짐 판정은 둘레 것만 본다. 위협·닿음 격자에서 닫힌 내 문은 자기 문(OwnDoor)이고(AIBaseGuard.DoorCell), 시야 밖은 마지막 본 상태다: '서 있음'은 AIBaseGuard.OwnDoorStands 한 곳에서 정한다. 보이는데 문이 없는 보기(영역이 실렸고 칸이 땅, 또는 물·용암)가 1초 넘게 이어지면 잊는다(MyDoorLook). 지나간 내 문도 꼬리 닫기를 받고, 계획이 없으면 가던 쪽에서 닫는다(PassSide). 둘레를 다시 고르면 새 틈 위가 아닌 옛 둘레 문은 서 있으면 내 문으로 옮긴다(RepickOldDoors).
- 관측 지점: Player.log "EDGE memory: WoodDoor at (x,y) is my door (placed off the edge plan; my doors n)", "nav: own door … open -> through -> close", "through own door (x,y)"
- 정책: pol.edge-door-first
- 코드 앵커: AIEdge.ForgetMyDoor, AIEdge.WalkDoorTilesNow, AINavigator.OwnDoorToward, AIPlaces.NearDoor, AITunnelSearch.DoorTileCost, AIEdge.LookMyDoors, AIEdge.RepickOldDoors, AIBaseGuard.OwnDoorStands, AIBaseGuard.DoorCell, AIBaseGuard.PassSide, AIBaseGuard.MyDoorLook.Look, AIBaseGuard.MyDoorLook.LookTile, AIBaseGuard.MyDoorLook.Drop, AIBaseGuard.MyDoorLook.Clear, AIDoorFacts.InWindowLastByTile, AIDoorFacts.FirstAt, AIDoorFacts.OpenTilesExcept
- 메모: 2026-11-06 후보902721e 추가 실게임: 새 자기 문 설치/기억과 열기→통과→닫기→도착, 완전 종료/같은 월드 재입장 후 기존 문으로 같은 흐름을 관측했다. 실제 Player 로그 근거이며 movetrace CSV는 생성되지 않았다. 근거 work/리팩토링-20261106/15-unity-and-live.md §3.1–3.2. 문 소실·청크 unload 등 전 장면 검증은 아니다.
- 근거: work/2026-11/2026-11-05-제작대-놓기/9-실게임2.md "문 옆 벽·둘레 벽을 판 일 없음"
- 근거: work/2026-11/2026-11-05-코어-안전지대/34-내문-실게임.md "로그 전체의 `forgotten` 3줄은 모두 실제로 문이 없어진 경우다"
- 근거: work/2026-11/2026-11-05-코어-안전지대/34-내문-실게임.md "걷는 적 2마리가 문 밖에 있는 동안 자기 방어와 판단 호출은 0이었다"
- 메모: 2026-11-06 제작대-놓기. 실게임에서 본 것은 기억(재접속)과 지나가기다. 닫기 후보는 못 봤다. 확인한 선은 2판 빌드 8136d37 기준이다. 그 뒤 P1(문 고르기)·Q1(안쪽끼리는 둘레 문 안 씀)·Q5가 이 선을 바꿨고 실게임에서 다시 보지 못했다(U.place-live-unseen). 화면 밖 내 문의 위협 판정·계획 밖 닫힌 내 문의 ShutDoor 분류·Pick 뒤 옛 둘레 문은 코어-안전지대 mydoor-fix(a07166f)에서 고쳤고 34-내문-실게임에서 봤다. 확인한 선은 그 빌드 a07166f 기준이다. 물 칸 잊기(N1)와 시야 밖 내 문 너머 적의 '못 옴'은 직접 보지 못했다(U.mydoor-live-leftovers).

### F.tunnel-detour — 둘레 먼저 막고 찾기·우회 사실·둘레 파는 길 후보·실행 중 둘레 멈춤
- 상태: 실게임 확인
- 확인한 선: 0db82868e654
- 종류: 제품
- 한 줄: 파들어가기 계획은 먼저 둘레를 여는 벽(둘레 벽·문 옆 벽, 목적지 칸 제외)을 막고 찾고, 길이 없을 때만 열어 다시 찾아 "둘레를 파지 않는 길은 아는 칸에 없음"(탐색 상한이면 "찾기 범위 안에서 못 찾음")을 붙인다. goto/place·home·적 쪽 파들어가기는 우회 검사를 켜서, 첫 길이 15칸 이상이거나 벽이 있고 둘레 허용 길이 예상 시간(칸 + 타격×1.2) 10칸 걷기 이상 빠르면 첫 길 후보에 "문으로 돌아감 N칸·약 X초(둘레 (x,y)를 파면 M칸·약 Y초, 둘레가 열림 …)"를 붙이고 둘레 길을 별도 후보 `digedge:x,y`로 낸다(둘레 몫 안, 자를 때 먼저 빠짐). 모든 dig·mine 후보는 계획 길이 둘레를 열면 둘레 사실을 단다. 실행 중 다시 찾은 길이 고를 때 밝히지 않은 둘레 벽을 열면 파지 않고 멈춰 막힘 문장으로 돌려주고 그 목적지 캐시를 지운다.
- 관측 지점: Player.log "TUNNEL … first way N tiles est E, edge way M tiles est F -> offered", 준비 로그 tunnel·2pass 수; 후보 문장 "문으로 돌아감 …", 후보 키 digedge_x_y
- 정책: pol.edge-door-first, pol.place-facts-not-filter
- 코드 앵커: AITunnel.Plan, AITunnelSearch.Find/13, AITunnelSearch.FindWithDetour, AITunnelSearch.Hits, AITunnelSearch.Heap.Push, AIPlaceGoal.DetourNote, AIPlaceGoal.DetourOption, AIPlaceGoal.DoorsOn, AIApproach.FirstCached, AIApproach.FirstImmediate, AIApproachSearch.FirstOfThree, AIWallRisk.RouteText, AIMoveEstimate.Seconds
- 근거: work/2026-11/2026-11-05-제작대-놓기/9-실게임2.md "판단기는 문 길(0.95, digedge 0.04)을 골라 문 (-10,0)으로 들어감"
- 메모: 2026-11-06 제작대-놓기. 실게임에서 본 것은 우회 사실 줄과 digedge 후보까지이고, digedge 실행과 실행 중 둘레 멈춤은 못 봤다(U.place-live-unseen). 확인한 선은 2판 빌드 8136d37 기준이다. 그 뒤 E1~H3·Q1 등이 이 선을 바꿨다. 비용: 시험 지도(261×261) 두 번 찾기 44~81ms, 실게임 판단 준비 최대 74~80ms(2·4판). 우회 검사 밖 경로(광석·탐험·기억한 제작대·농사·솥·상자·물러나기)는 둘레를 피해 크게 돌아갈 수 있고 사실도 붙지 않는다(U.place-detour-outside). 문턱 15·10칸은 HEAD 처음 값이다.

## 설계만 있는 것(행동 층)

아래는 코드가 없거나 일부만 있다. 설계 문서는 행동설계 v2다. v2 자체는 설계 제안이다(cand.v2-design).

### F.goal-plan — LLM 목표와 준비 단계
- 상태: 설계만
- 종류: 제품
- 한 줄: LLM이 목적·완료 조건·준비 단계·우선순위를 정하고, 목표에 수정 번호를 붙인다.
- 정책: pol.behavior-roles, pol.llm-can-retreat, cand.v2-design
- 근거: work/2026-11/2026-11-03-jev-행동설계-v2.md "## 3. 목표와 준비 단계: 지금 못 한다고 계획에서도 사라지지 않게"
- 메모: 지금은 호스트가 채팅으로 목표를 준다(F.goal-cmd). 수정 번호는 거기서 이미 쓴다. 2026-11-04 첫 조각(구독 Claude가 다음 목표 하나를 정함)은 F.llm-goal. 준비 단계·완료 조건·재점검은 아직 없다. 2026-11-04 제작 목표의 준비 경로(모으기→만들기→놓기→계속)는 게임 쪽 판단부에 생김(F.craft).

### F.executor — 단일 실행부와 결과 판정
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 선택된 행동의 몸 입력을 수행하고 관측한 결과를 전달한다. 정책 시간·진척 조건은 AISkill.ObserveOnce와 AIBrain.ExecutionObservation을 통해 현장·상위 입력에 보존한다. 중단·전환은 새 AI 선택과 사용자의 직접 명령으로 실행한다. 생활 조회 응답은 몸 실행 전에 별도 소비한다. 실제 선택은 현재 대상 재검증과 생산 skill 결과로 연결한다.
- 메모: 이전 구현 설명은 당시 기록이다. 한 실행부만 캐릭터 입력을 쥐고 이동·공격·작업을 이어서 한다. 선택·적용·진행·종료를 나눠 기록하고 실제 결과로 판정한다.
- 정책: pol.realtime-by-program, pol.body-fight, pol.body-progress, cand.two-layer, cand.v2-design
- 근거: work/2026-11/2026-11-03-jev-행동설계-v2.md "## 6. 명령 수락과 행동 결과를 구분한다"
- 메모: 단일 실행부와 기술별 결과 판정(사실 목록)은 F.move-input과 F.skill-*로, 선택·적용 기록(DECISION RESULT … APPLY|DISCARD)은 F.action-select로 만들었다. 구간 단위 결과 묶음은 아직 없다(F.llm-report).

### F.llm-report — 구간 보고와 LLM 재판단
- 상태: 설계만
- 종류: 제품
- 한 줄: 프로그램이 목표 아래 구간의 진행과 사건을 모아 LLM에 보낸다. LLM이 목표를 바꾸면 수정 번호를 올리고 옛 번호의 늦은 응답은 버린다.
- 정책: pol.llm-can-retreat, pol.fewer-calls, cand.v2-design
- 근거: work/2026-11/2026-11-03-jev-행동설계-v2.md "## 8. 전투 중 LLM 개입과 늦은 응답"

### F.reflex — 응답이 늦을 때 반사 행동
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 선택 담당(OpenAI Decisions)의 응답이 마감(기본 1.5초)을 넘긴 동안에만 반사를 켜고, 응답이 오는 순간 끈다. 지금 반사는 최소안뿐이다: 진행 중 기술이 유효하면 계속하고, 없으면 제자리. 물러나기·피하기·먹기·반격은 없다.
- 관측 지점: Player.log "REFLEX ON|OFF"
- 정책: pol.reflex-while-late, pol.reflex-minimal-now, cand.min-reflex
- 코드 앵커: AIBrain.SetReflex
- 근거: work/AI-전용-캐릭터/history.md "'늦음'의 마감 값과 반사 행동의 내용은 아직 미정(시험으로 정함)."
- 근거: work/2026-11/2026-11-03-행동판단-연결-구현.md "| 반사(늦을 때만) | 첫 빌드에서 자연 발생 1회: `REFLEX ON (no response after 1.5s)` → `REFLEX OFF (response arrived after 2.04s)`. 최종 빌드에서는 1.5초 넘는 응답이 없어 다시 보지 못함. |"
- 메모: 켜고 끄기는 첫 빌드에서 봤다. 최종 빌드 코드로는 못 봤으므로 확인한 선을 적지 않는다. 2026-11-04 전투 시험에서 판단 71번 응답 중앙값 0.26초·p95 0.47초, 반사 0번이라 지금대로 두기로 했다(작업자 판단).
- 근거: work/2026-11/2026-11-04-전투-선택지.md "판단: 늦은 응답 반사는 지금대로 "하던 유효한 행동을 계속하거나 멈춤"으로 둔다."

### F.memory — 기억과 지식 전달
- 상태: 설계만
- 종류: 제품
- 한 줄: 게임 규칙·관찰 상태·경험을 나눠 저장하고, 요청마다 필요한 사실을 골라 넣는다. 캐릭터가 모르는 정보는 주지 않는다.
- 정책: pol.llm-can-retreat, cand.v2-design
- 근거: work/2026-11/2026-11-03-jev-행동설계-v2.md "## 9. 기억과 지식: 필요한 정보는 매번 전달하되 선별을 매번 LLM에 맡기지 않는다"
- 메모: 2026-11-04 경험 기억 1단계(사건·목표 결과·까닭별 막힘 판정)는 F.exp-memory로 만들었다. 게임 지식(아이템·레시피 그래프, 캐릭터별 열림)은 게임-지식그래프 작업 브랜치(ai-knowledge)에 있고 아직 ai-character에 없다. 2026-11-04 늦게: 게임 지식(AIKnowledge.cs, Tools/knowledge)은 ai-character에 들어갔고, 위쪽 LLM이 목차를 보고 기억·게임 지식을 필요할 때 찾는 검색은 F.memory-recall로 만들었다.

### F.chatroom-link — 단톡방 멤버와 게임 몸 잇기
- 상태: 미설계
- 종류: 제품
- 한 줄: 단톡방 멤버(LLM)의 목표·말과 모드 안 몸을 잇는다. 연결 방식은 정하지 않았다.
- 정책: pol.scope-chatroom, cand.chatroom-attach, cand.file-queue
- 근거: work/branch-20261021T092745-5c346ae4/history.md "첫 연결은 JSONL 파일 대기열 권장"

## 선 밖 코드 분류

어떤 기능 선에도 들지 않는 함수를 여기서 분류한다. 이것도 판단이다. 분류: 진입점(게임이 부름) / 기반(여러 일을 받침) / 진단 / 시험용 / 죽은 코드.
표에 없고 선에도 없는 함수는 도구가 미결(선 밖, 분류 안 됨)로 낸다.

| 함수 | 분류 | 이유 |
|---|---|---|
| `ExpBook.Add` | 기반 | 경험 기억의 목표 시도 목록(F.exp-memory). ExpRecorder가 인스턴스로 불러 선이 따라가지 못한다 |
| `ExpBook.Has` | 기반 | 같은 목표 시도가 이미 있는지(중복 집계 막기, F.exp-memory) |
| `ExpBook.Sort` | 기반 | 목표 시도 목록 정렬(F.exp-memory) |
| `AIExperience.static` | 기반 | 캐릭터별 경험 기억 상태 필드(F.exp-memory). 읽기 도구가 정적 필드 선언을 함수로 읽은 것 |
| `AIPlayerSpikeMod.EarlyInit` | 진입점 | IMod. 모드 로더가 부른다. 로그만 남긴다 |
| `AIPlayerSpikeMod.Init` | 진입점 | IMod. 비어 있다 |
| `AIPlayerSpikeMod.ModObjectLoaded` | 진입점 | IMod. 비어 있다 |
| `AIPlayerSpikeMod.CanBeUnloaded` | 진입점 | IMod |
| `AIPlayerSpikeMod.Shutdown` | 진입점 | IMod. 모드가 내려갈 때 정리한다(F.leave의 DisposeThinWorld를 부름) |
| `AIPlayerSpikeMod.Update` | 진입점 | IMod. 매 프레임 Tick을 부른다 |
| `AIPlayerSpikeMod.Tick` | 기반 | 단계 기계. 호스팅 월드에 들어가면 패치를 걸고 월드 설정을 읽어, 켜짐이면 10초 뒤 STEP을 차례로 부르고 꺼짐이면 명령을 기다린다 |
| `AIPlayerSpikeMod.InHostedGame` | 기반 | Tick의 조건(호스팅 중인가) |
| `AIPlayerSpikeMod.ResetState` | 기반 | 상태 초기화 |
| `AIPlayerSpikeMod.SetStage` | 기반 | 단계 바꾸기와 로그 |
| `AIPlayerSpikeMod.Log` | 기반 | 로그 |
| `AIPlayerSpikeMod.LogError` | 기반 | 로그 |
| `AICharacterStore.LogInventories` | 진단 | 스폰 5초 뒤 서버 가방을 로그로 남긴다 |
| `AICharacterStore.BufferSummary` | 진단 | LogInventories가 쓰는 가방 요약 글 |
| `AICharacterStore.TestOnlyGiveFromFile` | 시험용 | `.testgive` 파일이 있으면 아이템 하나를 넣는다. 저장 확인용 |
| `ThinClientDriverConstructor.CreateServerDriver` | 기반 | 인터페이스 때문에 있는 함수. 부르면 예외를 던진다 |
| `HeadlessSdk.FindType` | 기반 | 빌드 도구 보조 |
| `HeadlessSdk.Fail` | 기반 | 빌드 도구 보조 |
| `AISkill.Finish/2` | 기반 | 기술이 상태와 이유를 정하고 끝낼 때 쓰는 공통 함수(리팩토링 S2: 결과 코드 없이 끝냄) |
| `AISkill.Finish/3` | 기반 | 위와 같음. 결과 코드(`SkillEnd`)를 함께 정한다(리팩토링 S2) |
| `AISkill.Cleanup` | 기반 | 기술 정리 기본(비어 있음). 기술마다 덮어쓴다 |
| `LookSkill.LookSkill` | 기반 | 생성자(이름만) |
| `InvSkill.InvSkill` | 기반 | 생성자(이름만) |
| `GotoSkill.GotoSkill` | 기반 | 생성자(목표 저장) |
| `MineSkill.MineSkill` | 기반 | 생성자(대상 저장) |
| `AttackSkill.AttackSkill/0` | 기반 | 생성자(이름만, 가장 가까운 적) |
| `AttackSkill.AttackSkill/1` | 기반 | 생성자(판단 층이 정한 적 엔티티) |
| `WaitSkill.WaitSkill` | 기반 | 생성자(기다릴 시간) |
| `TunnelSkill.TunnelSkill` | 기반 | 생성자(대상·벽인지·길만 열면 되는지) |
| `WalkTestSkill.WalkTestSkill` | 기반 | 생성자(이름만) |
| `AIBody.TestOnlyTiles` | 시험용 | `/ai tiles`: 벽 타일 엔티티 수·지역 로드 반경·단단한 벽 종류(먼 곳 캐기 진단). 같은 `HandleCommand`에 `/ai plan`(굴 계획만), `/ai hostto`(호스트를 게임 지도 순간이동으로 옮김)도 시험용으로 있다 |
| `WalkTestSkill.Pattern` | 시험용 | 예전 STEP3 걷기 무늬(`/ai walktest`) |
| `AIPlayerSpikeMod.MakeInput` | 기반 | 실행부가 만든 입력에 틱·보간 지연을 채워 명령으로 바꾼다 |
| `AIAgent.AIAgent` | 기반 | 멤버 표 한 줄 생성자(F.multi-ai) |
| `AIAgent.Scope.Dispose` | 기반 | `using (AIAgent.Enter(a))`가 끝날 때 앞 캐릭터 범위로 되돌린다(F.multi-ai) |
| `AIAgent.ToString` | 진단 | 로그에서 캐릭터 이름으로 보이게 |
| `AIBody.StateOf` | 기반 | 캐릭터별 상태 꺼내기. static 속성 `S`(식 본문)에서 불려 도구의 호출 따라가기에 안 잡힌다(F.multi-ai) |
| `AIBrain.StateOf` | 기반 | 위와 같음 |
| `AICharacterStore.StateOf` | 기반 | 위와 같음 |
| `AIDrops.StateOf` | 기반 | 위와 같음 |
| `AIMemory.StateOf` | 기반 | 위와 같음 |
| `AIPlanner.StateOf` | 기반 | 위와 같음 |
| `AITunnel.StateOf` | 기반 | 위와 같음 |
| `AIVitals.StateOf` | 기반 | 위와 같음 |
| `BreakSkill.StateOf` | 기반 | 위와 같음 |
| `AIGoals.StateOf` | 기반 | 위와 같음(리팩토링 S3, 목표 상태 칸) |
| `GoalEnd.GoalEnd` | 기반 | 목표 끝 값(까닭·결과·막힘 분류)의 생성자(리팩토링 S2·S3, `Core/AIOutcomes.cs`) |
| `PlanTrigger.PlanTrigger` | 기반 | 위쪽 LLM 보고 계기 값의 생성자(리팩토링 S4, `Core/AIPlanInbox.cs`) |
| `AIBrain.IsMoveKind` | 죽은 코드 | 전투 단계 2에서 만든 감싸기(`AIRunGate.IsMoveKind`를 부름). 부르는 곳이 없다. 통계는 `AIRunGate.SimilarMove`·`ShortMove`가 직접 `AIRunGate.IsMoveKind`를 쓴다 |
| `AIChargeMath.ArriveIn` | 시험용 | 돌진 도착 시각 계산. 제품 코드에서 부르는 곳이 없고 core-tests(`CombatS2Tests.cs`)만 쓴다(제품 그래프 B 재추출의 미도달 목록에도 있음) |
| `AIChargeMath.MoveSeconds` | 시험용 | 직선 이동 시간 계산. 위와 같음 |
| `AIRunGate.HeldSurvives` | 시험용 | 버린 답(StaleContinue)이면 보류 답이 남는다는 소유자 규칙의 판정. 제품 고리는 같은 규칙을 갈래로 직접 쓰고 이 함수는 core-tests만 부른다(F.charge-urgent) |
| `V2.V2` | 기반 | 2차원 값 형식의 생성자(전투 단계 2, `Core/AIChargeMath.cs`). 돌진 예측·짧은 이동 계산이 쓴다 |
| `AIChargeMath.StopWhat.StopWhat` | 기반 | 돌진이 멈춘 대상(벽·걸을 수 없는 칸·물체·막힘) 값의 생성자(전투 단계 2, F.charge-watch) |
| `AIBrain.CandBuild.CandBuild` | 기반 | 후보 생성기 한 번의 생성자(리팩토링 S6b). `new`라 선이 따라가지 않는다 |
| `AICandidateAssembler.static` | 기반 | 조립기의 정적 표 선언. 읽기 도구가 정적 필드 선언을 함수로 읽은 것 |
| `AIBrain.AsmRec.Id` | 시험용 | 조립 기록(ASM-IN/ASM-OUT, 스위치 `decisionlog.on`)에서 후보 번호를 붙인다(리팩토링 S6a). 인스턴스로 불려 선이 따라가지 못한다. 기록 함수 AsmIn·AsmOut·AsmNote는 F.candidates 선 위에 있다 |
| `AIPlayerSpikeMod.ServerPlayerOf` | 죽은 코드 | 멤버넷 작업 중 만든 보조 함수, 지금 부르는 곳 없음 |
| `AIBaseGuard.BreakerNote/1` | 시험용 | 벽 부수는 종류 문장의 편의 오버로드. 제품은 `Breaker`·`KillFacts`를 쓰고 core-tests(`FixRound3Tests.cs`)만 부른다(F.base-reach) |
| `AIBaseGuard.BreakerNote/2` | 시험용 | 위와 같음 |
| `AIBaseGuard.EdgeWallNote` | 시험용 | 리팩토링 뒤 제품은 WallFact의 의미 필드를 사용하며 이 문자열 호환 함수는 core-tests GuardTests가 호출한다 |
| `AIBaseGuard.CombatNow` | 시험용 | 도달 판정으로 전투 상태인지 묻는 순수 함수. 제품은 같은 규칙을 `AIEdge.AnyThreat`로 쓰고 core-tests(`GuardTests.cs`)만 부른다(F.base-reach) |
| `AIDoorPass.StepNote` | 기반 | 문 단계 중 "지금 하는 일"·계속 후보 문장. 식 본문 속성 `AIBody.DoorStepNote`에서 불려 선이 따라가지 못한다(F.door-pass) |
| `DoorPassState.Note` | 기반 | 문 단계 상태의 기록 문장(인스턴스, F.door-pass) |
| `PressQueue.Bit` | 기반 | 누름 대기열이 이번 틱에 E 비트를 낼지(인스턴스로 불려 선이 따라가지 못함, F.door-pass) |
| `PressQueue.DroppedWhy` | 기반 | 버린 누름의 까닭(인스턴스, `AIBody.UseDroppedWhy`가 부름, F.door-pass) |
| `SettleState.Note` | 기반 | 멈춰 서기 상태 기록 문장(인스턴스, F.door-pass·F.place-grid) |
| `PlaceRef.ToString` | 기반 | 장소 식별자를 글로 되돌림(AIPlaces.Format을 부르는 식 본문, F.place-goal의 로그·보고 문장) |
| `AITunnelSearch.FindAvoidingEdge` | 시험용 | 두 번 찾기의 둘레 막고 찾기만 따로 부르는 입구. 제품 경로는 FindWithDetour를 쓰고 이것은 core-tests PG 17·20만 부른다 |
| `AIEdge.IsOwnDoorTile` | 죽은 코드 | 둘레 문·내 문 판정. 제품 코드에서 부르는 곳이 없다(걷기는 IsWalkDoorTile, 2026-11-06 제품 그래프 미도달 목록) |
| `SettleState.ResetPulses` | 기반 | 멈춰 서기 보정 펄스 초기화(인스턴스, F.door-pass·F.place-grid) |
| `AIBaseEdgeCore.DoorSideNeed.Text` | 기반 | 문 양옆 벽 대기 문장(값 형식의 인스턴스, F.edge-fill) |
| `AIBaseEdgeCore.Flow.Edge` | 기반 | 둘레 후보의 최소 절단(max flow) 간선 추가(인스턴스, F.edge-plan) |
| `AIBaseEdgeCore.Flow.Reset` | 기반 | 위와 같음, 흐름 초기화 |
| `EdgeGrid.Idx` | 기반 | 둘레 격자 칸 번호(값 형식의 인스턴스, F.edge-plan) |
| `EdgeGrid.In` | 기반 | 둘레 격자 안인지(위와 같음) |
| `EdgeGrid.Open` | 기반 | 둘레 격자의 열린 칸인지(위와 같음) |
| `GuardGrid.At` | 기반 | 지키기 걷기 격자 칸 읽기(인스턴스, F.base-reach) |
| `GuardGrid.Idx` | 기반 | 위와 같음 |
| `GuardGrid.In` | 기반 | 위와 같음 |
| `BaseMemory.BaseMemory` | 기반 | 둘레 기억 값의 생성자(`new`라 선이 따라가지 않음, F.base-reach·F.base-repair) |
| `BaseMemory.InBase` | 기반 | 기억한 기지 안쪽 칸인지(인스턴스, F.base-reach) |
| `BaseMemory.OpenBeyond` | 기반 | 시야 밖 둘레를 마지막 본 상태로 넘을 수 있는지(인스턴스, F.base-reach) |
| `BaseMemory.Remembered` | 기반 | 기억한 칸인지(인스턴스, F.base-reach) |
| `BaseShape.Inside` | 기반 | 기지 모양 값의 안쪽 칸 판정(인스턴스, F.base-reach·F.base-repair) |
| `BaseShape.IsEdge` | 기반 | 위와 같음, 둘레 칸 판정 |
| `BigObjectMemory.Clear` | 기반 | 시야 밖 큰 물체 기억(캐릭터 상태의 인스턴스, F.base-reach) |
| `BigObjectMemory.Drop` | 기반 | 위와 같음 |
| `BigObjectMemory.Forget` | 기반 | 위와 같음 |
| `BigObjectMemory.Has` | 기반 | 위와 같음 |
| `BigObjectMemory.Seen` | 기반 | 위와 같음 |
| `EdgeLastSeen.Clear` | 기반 | 둘레 칸 마지막 본 상태(memory.json `edgeSeen`, 인스턴스, F.base-repair) |
| `EdgeLastSeen.Forget` | 기반 | 위와 같음 |
| `EdgeLastSeen.Last` | 기반 | 위와 같음 |
| `EdgeLastSeen.ToArray` | 기반 | 위와 같음(저장) |
| `TileBreak.V` | 기반 | 타일·물체 피해 키 값(못 읽으면 null) 읽기(값 형식의 인스턴스, F.base-reach) |
| `AIBaseEdgeCore.static/3` | 기반 | 둘레 계산의 정적 표 선언. 읽기 도구가 정적 필드 선언을 함수로 읽은 것 |
| `AIBaseEdgeCore.static/4` | 기반 | 위와 같음 |
| `AIBaseEdgeCore.static/5` | 기반 | 위와 같음 |
| `AIBaseEdgeCore.static/6` | 기반 | 위와 같음 |
| `AIBaseGuard.static/6` | 기반 | 지키기 계산의 정적 표·문장 선언. 읽기 도구가 정적 필드 선언을 함수로 읽은 것 |
| `AIBaseGuard.static/10` | 기반 | 위와 같음 |
| `AIBaseGuard.static/11` | 기반 | 위와 같음 |
| `AIDoorPass.static/6` | 기반 | 문 단계의 정적 값 선언. 읽기 도구가 정적 필드 선언을 함수로 읽은 것 |
| `AIEdge.static/3` | 기반 | 둘레 게임 쪽 캐릭터별 상태 선언. 읽기 도구가 정적 필드 선언을 함수로 읽은 것 |
| `AIEdge.static/4` | 기반 | 위와 같음 |


## 2026-11-06 책임 분리 후보(902721e)

기능 ID와 제품 정책은 유지한다. 목표 메타데이터/기능 진입, 후보의 의미 필드, 문·물체 사실 조회, 접근 탐색과 위협 관측/기록 소유를 나누었다. AIBodySkills의 몸·관측·길찾기·기술은 각 형식 파일, AIExplore의 기억·떨굼·물체·굴길은 각각의 파일로 이동했다. 명령은 AIBody.Commands 및 AIPlayerSpikeMod.Chat, 저장은 AICharacterStore/AIWorldSettings, 위협은 AIEdge.Reach가 진입점이다. 기존 확인한 선과 실게임 증거는 이 후보의 실행 확인으로 승격하지 않는다. 정확한 이동 목록은 work/리팩토링-20261106/09-move-manifest.md와 14-graph-stage-result.md에 있다.





## 2026-11-06 판단 선택지 개선 적용판 b25099f

목표 재료와 필요한 음식의 직접 줍기 후보를 먼저 보존한다. 같은 필요에서는 자동 유입보다 접근이 필요한 물건을 먼저 남기며 최종 선택은 모델이 한다. 프리팹의 IsExplosiveCD로 확인한 폭발성 벽의 위험과 자동 후퇴 없는 실행 한계를 후보·계속하기에 알린다. 본/대안 경로는 digDetour로 구분한다. 걷기 표시 ETA는 5.7칸/초로 통일하고 기존 경로 선택 점수는 유지한다. 독립 검수 및 실게임 기록은 work/2026-11/2026-11-06-선택지개선/ 아래를 따른다. 기존 확인한 선·76563bf 화면 관측·902721e 과거 실행 관측을 이 후보의 실행 확인으로 승격하지 않는다.


### F.ai-info-ui — AI 스킬과 실제 착용 장비 조회
- 확인한 선: 2c91bcf86e15
- 상태: 실게임 확인
- 종류: 제품
- 한 줄: 작은 선택부에서 멤버를 고르면 실제 서버 스킬 레벨과 착용 슬롯을 읽는다. 호스트 읽기 전용이며 모델 호출이나 자동착용을 추가하지 않는다. 실제 빈 슬롯과 장착 fixture 양성 표시, 참가 해제/복귀 갱신을 확인했다.
- 관측 지점: AI 선택부·스킬/착용 장비 탭; AI-UI equipment source 로그
- 정책: pol.ui-embedded-readonly
- 코드 앵커: AIInfoPanel.OnGUI, AIInfoPanel.Frame, AIInfoSnapshot.Read
- 근거: work/ai-panel-integration-20261106/08-acceptance.md "실제 방어구를 세 부위에 장착한 시험"
- 메모: fixture는 표시 검증이며 제품 자동착용 성공이 아니다. 원래 프로세스의 상태를 소급 확정하지 않는다.

### F.chat-compact-screen — 기본 채팅 출력 본문을 작게 읽기
- 확인한 선: 24857121287a
- 상태: 실게임 확인
- 종류: 제품
- 한 줄: 기본 ChatWindow/PugText와 입력·멘션·Tab·알림·fade를 유지하고 출력 glyph만 화면 해상도로 작게 표시한다. 최종 실게임은 본문43~44→24 캡처px와 줄간격26~29px를 확인했다. 입력행·멘션 힌트 크기는 원래대로 보존한다.
- 관측 지점: 실제 AI 발언 화면38, CHAT-SCREEN metrics, fade 캡처
- 정책: pol.ui-embedded-readonly
- 코드 앵커: AICompactFont.SetPugFontPrefix, AIChatScreenLayer.AfterRender, AIChatScreenLayer.OnGUI, AIChatScreenLayer.BeforeRelease, AICompactFont.RefreshScreenMetrics
- 근거: work/ai-panel-integration-20261106/08-acceptance.md "입력행과 멘션 힌트의 기존 크기는 유지하며 추가 축소 대상은 출력 본문이다."
- 메모: 실제 OS IME 조합·다중버튼 동시 유지·다른 해상도 실게임은 미시험. 렌더 실패/미지원 마스크·재질은 원래 게임 표시로 돌아간다.

### F.common-execution — AI의 명시 중단·단계 완료·재계획
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: stop_action은 몸만 중단하고 목표와 부탁을 유지한다. finish_goal은 현재 단계만 완료한다. request_plan은 유효한 몸 동작을 유지하며 상위에 현재 사실을 전달한다.
- 정책: pol.ai-action-control, pol.original-request-completion
- 코드 앵커: AIBrain.Apply, AIBrain.CancelPendingChoices, AIBrain.ObserveExecution, AIGoals.CheckDone, AIGoals.FinishChosen, AIPlanner.RequestReplan, AIHostOrders.GoalEnded
- 근거: work/2026-11/2026-11-07-공통실행자료/contract.md "stop_action은 현재 몸 동작만 AI가 중단한다."
- 근거: work/2026-11/2026-11-07-공통실행자료/implementation.md "AISkill.ObserveOnce는 실행 회차에 조건을 보존하고 재판단을 요청한다."


### F.life-tree — 읽기 전용 생활 선택 탐색과 직접 실행
- 상태: 구현됨·새 실게임 미확인
- 종류: 제품
- 한 줄: 제작·요리·농사·보관/꺼내기를 필요한 깊이만 조회하고 이미 정한 대상은 같은 입구에서 직접 실행한다. 탐색은 몸·대기 선택·목표를 바꾸지 않고 수량은 상위 작업량을 공유한다. 실제 실행량과 가방 확보량을 구분한다.
- 정책: pol.behavior-roles, pol.vision-human-view, pol.ai-action-control, pol.original-request-completion
- 코드 앵커: AILife.Compose, AILife.Navigate, AILife.Resolve, AILife.Ended, AILife.Report, AIBrain.AssembleLifePage, AIBrain.HandleResponse, AIFarm.ExactCandidate, AICook.ExactCandidate, AICraft.ExactCandidate, AIInventory.CandidatesFor, LifeCursor.Check, LifeCursor.Apply, LifeProgress.Record, AILife.LeaveNavigation, AILife.NavStale, AILife.BlocksInventoryView
- 근거: work/2026-11/2026-11-07-생활트리구현/08-integration.md "첫 단위는 수량·칸 조합을 나열하지 않는 읽기전용 트리와 동일 입구의 직접 실행을 제공한다."
- 메모: c4a0f5e에 본 코드 통합. 생산코드 어댑터·회귀·Unity와 독립검수 근거는05/07에 한정한다. 실제 모델/API·전체 Tick·실게임 소비자는 미확인이며 수리·자동 가공·강화·거래·낚시는 후속이다.

### F.inventory-exact — 정확한 장비·상자·수량 조작
- 상태: 구현됨·새 실게임 미확인
- 종류: 제품
- 한 줄: 정확한 물품·슬롯·variation·상태를 지정해 장착·교체·보관·꺼내기·내려놓기·전달을 정상 요청으로 실행한다. 양쪽 실제 변화와 수량 receipt로 결과를 확인하고 원래 부탁을 유지한다.
- 정책: pol.behavior-roles, pol.vision-human-view, pol.original-request-completion
- 코드 앵커: AIInventory.CandidatesFor, AIInventory.LifeEntries, AIInventory.Stale, InventorySkill.Start, InventorySkill.Tick
- 근거: work/2026-11/2026-11-07-생활트리구현/08-integration.md "이 판은 공통 실행 및 정확한 가방장비 실행을 포함하며 생활 트리의 제작·요리·농사·보관/꺼내기를 연결한다."
- 메모: 독립 가방장비 후보·공통 실행·상자 용량 수정이 포함된1fc89b9를 기반으로c4a0f5e에서 생활 연결을 검증했다. 다른 사람 자원 공유나 타인 가방 직접 쓰기를 추가한 것은 아니다.

### F.hand-choice — 명시 선택한 손 도구·근접무기 유지
- 상태: 구현됨·새 실게임 미확인
- 종류: 제품
- 한 줄: 명시 선택을 캐릭터별로 유지하고 실제 손 물품의 신원·파손·장착 상태를 확인한다. 실행 중 다른 물품으로 몰래 교체하지 않는다.
- 정책: pol.behavior-roles, pol.vision-human-view
- 코드 앵커: AIHandLease.Check, AIInventory.TryChosenHand, AIHeldChoice.Check
- 근거: work/2026-11/2026-11-07-가방장비구현/implementation.md "명시한 손 장비"
- 메모: 가방장비 문서의 격리 후보 지위는 당시 기록으로 보존하며 본 반영은 생활트리08의c4a0f5e 통합 기록을 따른다. 기존 실제 장비 표시 관측을 새 행동 성공으로 승격하지 않는다.

## 2026-11-07 가방·장비 격리 후보

### F.bag-equip — 장비 장착·교체·해제
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 실제 활성 장비 슬롯과 출발/도착 물품을 고르고 정상 UI Swap 뒤 양 슬롯 교환을 관측한다. 반지·파우치 목적지와 용량/태그 조건을 구별한다.
- 관측 지점: Player.log의 가방 조작 기술 결과, 실제 양 슬롯·현재 손 물품·누적 이동량·바닥 entity·수령 대상 관측. 이번에는 코드와 오프라인 시험만 확인했다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다.
- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles
- 코드 앵커: AIInventory.GearSlots, AIInventory.Stale, AIInventory.CapacityAfterReplacement, InventorySkill.Start, InventorySkill.Tick, AIInventory.Reset, AIInventory.GoalSet, AIInventory.GoalDone, AIInventory.StallNote, AIInventory.PromptLine, AIInventory.ReportJson, InventorySkill.Cleanup, InventorySkill.Progress, InventorySkill.InventorySkill, InventoryStamp.InventoryStamp, InventoryStamp.Exact, InventoryStamp.SameObject, InventorySwapProgress.InventorySwapProgress, InventorySwapProgress.MayRequest, InventorySwapProgress.Observe, InventorySwapProgress.RequestedOnce, AIInventory.MenuMoves, AIInventoryMenu.FinalMove
- 근거: work/2026-11/2026-11-07-가방장비구현/agreement.md "검수된 정적 규칙과 실게임 성공을 구별한다."
- 메모: 최종 고정 커밋과 실제 검증 범위는 work/2026-11/2026-11-07-가방장비구현/implementation-status.md를 따른다. 과거 다른 기능의 실게임 확인·서명을 이번 능력 확인으로 승격하지 않는다.
- 게임 지점: Pug.Other/Inventory/Create.cs:223 Swap
- 게임 지점: Pug.Other/Inventory/InventoryUtility.cs:668 Swap
- 메모: tree-spec의 현재 메뉴 최종 leaf에서 정상 조작 primitive를 재사용한다. 초기 inventory-spec은 역사적 설계이며 현재 트리·수량·수령 경계는 tree-spec과 이번 체크포인트 근거를 함께 읽는다.

- 메모: 새 정비 통합에서 CapacityAfterSwap/CapacityAfterRemoval은 객체 초기화를 포함한 arrow 위임 함수다. 경량 근거 파서는 이 두 선언을 잡지 못하므로 실제 공통 구현 CapacityAfterReplacement를 앵커로 잇는다. 두 위임 함수의 실제 신원은 제품 Roslyn 코드층에 있으며 생산 코드 부재를 뜻하지 않는다.

### F.bag-hold — 명시 도구·근접무기 손선택 유지
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: Decisions가 고른 구체 핫바칸의 물품을 실제 장비 관측으로 확인한다. 후속 채굴·근접공격은 선택 물품 신원과 파손을 확인하고 자동 대체하지 않는다.
- 관측 지점: Player.log의 가방 조작 기술 결과, 실제 양 슬롯·현재 손 물품·누적 이동량·바닥 entity·수령 대상 관측. 이번에는 코드와 오프라인 시험만 확인했다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다.
- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles
- 코드 앵커: AIInventory.TryChosenHand, AIHandLease.Check, AIHeldChoice.Check, AIBody.HandSlot, AIBody.WeaponSlotForAttack, AIHandLease.AIHandLease, AIInventory.MenuMoves, AIInventoryMenu.FinalMove
- 근거: work/2026-11/2026-11-07-가방장비구현/agreement.md "검수된 정적 규칙과 실게임 성공을 구별한다."
- 메모: 최종 고정 커밋과 실제 검증 범위는 work/2026-11/2026-11-07-가방장비구현/implementation-status.md를 따른다. 과거 다른 기능의 실게임 확인·서명을 이번 능력 확인으로 승격하지 않는다.
- 게임 지점: Pug.Other/Inventory/Create.cs:223 Swap
- 메모: tree-spec의 현재 메뉴 최종 leaf에서 정상 조작 primitive를 재사용한다. 초기 inventory-spec은 역사적 설계이며 현재 트리·수량·수령 경계는 tree-spec과 이번 체크포인트 근거를 함께 읽는다.

### F.bag-store — 품목·자기 상자·수량 지정 보관
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 선택한 일반 가방 품목을 자기 상자의 유효 슬롯으로 보관한다. 요청 잔량과 keep을 반영하며 실제 source 감소=destination 증가만 누적한다.
- 관측 지점: Player.log의 가방 조작 기술 결과, 실제 양 슬롯·현재 손 물품·누적 이동량·바닥 entity·수령 대상 관측. 이번에는 코드와 오프라인 시험만 확인했다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다.
- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles
- 코드 앵커: AIInventory.StoreRemaining, AIInventory.Stale, InventorySkill.Tick, AIInventoryRules.MoveCount, AIInventoryRules.ObservedTransfer, AIInventory.MenuMoves, AIInventoryMenu.FinalMove, AIBase.MayStoreKnownChest, AIInventory.HasStoreRoom
- 근거: work/2026-11/2026-11-07-가방장비구현/agreement.md "검수된 정적 규칙과 실게임 성공을 구별한다."
- 메모: 최종 고정 커밋과 실제 검증 범위는 work/2026-11/2026-11-07-가방장비구현/implementation-status.md를 따른다. 과거 다른 기능의 실게임 확인·서명을 이번 능력 확인으로 승격하지 않는다.
- 게임 지점: Pug.Other/Inventory/Create.cs:125 MoveAmount
- 게임 지점: Pug.Other/Inventory/InventoryUtility.cs:831 MoveAmount
- 메모: tree-spec의 현재 메뉴 최종 leaf에서 정상 조작 primitive를 재사용한다. 초기 inventory-spec은 역사적 설계이며 현재 트리·수량·수령 경계는 tree-spec과 이번 체크포인트 근거를 함께 읽는다.

### F.bag-take — 자기 상자에서 지정 수량 꺼내기
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 자기 상자를 열어 관측한 품목만 실제 가방 목적 슬롯으로 꺼낸다. 부분 이동은 관측 수량만 기록하고 다음 선택을 받는다.
- 관측 지점: Player.log의 가방 조작 기술 결과, 실제 양 슬롯·현재 손 물품·누적 이동량·바닥 entity·수령 대상 관측. 이번에는 코드와 오프라인 시험만 확인했다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다.
- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles
- 코드 앵커: AIInventory.Destination, AIInventory.Stale, InventorySkill.Tick, AIInventoryRules.ObservedTransfer, AIInventory.MenuMoves, AIInventoryMenu.FinalMove
- 근거: work/2026-11/2026-11-07-가방장비구현/agreement.md "검수된 정적 규칙과 실게임 성공을 구별한다."
- 메모: 최종 고정 커밋과 실제 검증 범위는 work/2026-11/2026-11-07-가방장비구현/implementation-status.md를 따른다. 과거 다른 기능의 실게임 확인·서명을 이번 능력 확인으로 승격하지 않는다.
- 게임 지점: Pug.Other/Inventory/Create.cs:125 MoveAmount
- 게임 지점: Pug.Other/Inventory/InventoryUtility.cs:2539 GetAmountToMoveFromInventory
- 메모: tree-spec의 현재 메뉴 최종 leaf에서 정상 조작 primitive를 재사용한다. 초기 inventory-spec은 역사적 설계이며 현재 트리·수량·수령 경계는 tree-spec과 이번 체크포인트 근거를 함께 읽는다.

### F.bag-drop — 명시 수량 내려놓기
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 명시 목표에서 정상 DropItem을 한 번 요청하고 자기 슬롯 감소와 이번에 생긴 바닥 물품을 확인한다. trash 삭제를 제공하지 않는다.
- 관측 지점: Player.log의 가방 조작 기술 결과, 실제 양 슬롯·현재 손 물품·누적 이동량·바닥 entity·수령 대상 관측. 이번에는 코드와 오프라인 시험만 확인했다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다.
- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles
- 코드 앵커: AIInventory.Stale, InventorySkill.Tick, AIInventory.MenuMoves, AIInventoryMenu.FinalMove
- 근거: work/2026-11/2026-11-07-가방장비구현/agreement.md "검수된 정적 규칙과 실게임 성공을 구별한다."
- 메모: 최종 고정 커밋과 실제 검증 범위는 work/2026-11/2026-11-07-가방장비구현/implementation-status.md를 따른다. 과거 다른 기능의 실게임 확인·서명을 이번 능력 확인으로 승격하지 않는다.
- 게임 지점: Pug.Other/Inventory/Create.cs:100 DropItem
- 게임 지점: Pug.Other/Inventory/InventoryUtility.cs:695 DropItem
- 메모: tree-spec의 현재 메뉴 최종 leaf에서 정상 조작 primitive를 재사용한다. 초기 inventory-spec은 역사적 설계이며 현재 트리·수량·수령 경계는 tree-spec과 이번 체크포인트 근거를 함께 읽는다.

### F.bag-give — 지정 수령자 전달 결과 관측
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 보이는 수령자에 접근해 정상 바닥 드롭을 사용한다. 추적 바닥 물품의 지정 수령 요청과 실제 감소량=수령자 일치 품목 증가량이 함께 관측된 수량만 기록한다.
- 관측 지점: Player.log의 가방 조작 기술 결과, 실제 양 슬롯·현재 손 물품·누적 이동량·바닥 entity·수령 대상 관측. 이번에는 코드와 오프라인 시험만 확인했다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다. 실게임 미확인이다.
- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles
- 코드 앵커: AIInventory.Recipient, AIInventory.MatchingCount, InventorySkill.Tick, AIInventory.MenuMoves, AIInventoryMenu.FinalMove
- 근거: work/2026-11/2026-11-07-가방장비구현/agreement.md "검수된 정적 규칙과 실게임 성공을 구별한다."
- 메모: 최종 고정 커밋과 실제 검증 범위는 work/2026-11/2026-11-07-가방장비구현/implementation-status.md를 따른다. 과거 다른 기능의 실게임 확인·서명을 이번 능력 확인으로 승격하지 않는다.
- 게임 지점: Pug.Other/Inventory/Create.cs:100 DropItem
- 게임 지점: Pug.Other/PickUpItemSystem.cs:554 CheckIfCanPickup

<!-- 77484ec 가방 원본 재생성: 생성자·인스턴스 규칙 호출은 기존 추출기의 직접 정적 호출 선에서 빠질 수 있으므로 관련 능력의 명시 앵커로 등록했다. 동작 실행을 뜻하지 않는다. -->
- 메모: tree-spec의 현재 메뉴 최종 leaf에서 정상 조작 primitive를 재사용한다. 초기 inventory-spec은 역사적 설계이며 현재 트리·수량·수령 경계는 tree-spec과 이번 체크포인트 근거를 함께 읽는다.

## 2026-11-07 분류 기반 가방 선택 트리 준비

### F.bag-navigation — 분류·보유 물품·조작의 메뉴 탐색
- 상태: 구현됨·미확인
- 종류: 제품
- 한 줄: 실제 보유 분류에서 물품·조작·필요한 인자를 고르고 최종 leaf에서만 정상 조작을 요청한다. 메뉴 뒤로·전체 선택 취소와 몸 동작/목표/원래 부탁 제어를 구별한다.
- 관측 지점: 현재 메뉴 revision/노드·정확한 물품 snapshot·선택 경로·현재 자식 payload와 leaf 결과. 생산자 소스 링크 오프라인 검사만 연결했으며 연결 trace·독립검수·Unity·실게임·최종 수용은 별도다.
- 정책: pol.bag-agency, pol.bag-navigation, pol.choice-no-cut, pol.bag-boundary
- 근거: work/2026-11/2026-11-07-가방장비구현/tree-agreement.md "현재 메뉴의 자식 선택지만 현장 판단 모델에 전달한다."
- 코드 앵커: AIInventoryMenu.Candidates, AIInventoryMenu.Category, AIInventoryMenu.Items, AIInventoryMenu.ApplyNavigation, AIInventoryMenu.Stale, AIInventoryMenu.Reset, AIInventoryMenu.NoteLeafSelected, AIInventoryMenu.FinalMove, AIInventoryMenu.AddOperations, AIInventoryMenu.AddQuantities, AIInventoryMenu.AddDigits, AIInventoryMenuFrame.Copy, AIInventoryMenuChoice.AIInventoryMenuChoice, AIBrain.InventoryChoiceSurface, AIBrain.InventoryMenuControl, AIBrain.Apply, AIBrain.CancelPendingChoices, AILife.LeaveNavigation, AILife.Compose, AILife.NavStale, AILife.BlocksInventoryView
- 메모: 현재 고정 체크포인트의 정적 구현·생산자 오프라인 검사다. flat 774/1fc 부품 검사와 774 Unity를 새 트리 통과로 승계하지 않는다. 메뉴 탐색과 몸·목표·원래 부탁 제어를 구별한다.

<!-- 통합판: AIInventory.Candidates/ChestCandidates는 생활의 명시 storage binding·준비 경로에서 사용한다. 공개 root의 무조건 flat 목록과 구별하며741의 dead 분류를 승계하지 않는다. -->


### F.maintenance — 정확 장비의 일반 수리·단일 레벨업·자기 투입물 회수
- 상태: 구현됨·새 실게임 미확인
- 종류: 제품
- 한 줄: 선택 장비와 시설·실제 비용을 조회하고 정상 단일 수리 또는 대장간 입력→한 레벨 변경→가방 회수를 수행한다. 자기 pending의 게시·투입·처리 근거를 보존하고 정확 상태/슬롯과 수령 receipt로 결과를 구분한다. 원래 부탁의 진척과 완료 주체를 유지한다.
- 정책: pol.behavior-roles, pol.vision-human-view, pol.ai-action-control, pol.original-request-completion
- 코드 앵커: AIMaintenance.LifeEntries, AIMaintenance.ExactCandidate, AIMaintenance.ReadCosts, AIMaintenance.Make, AIMaintenance.SlotBarrier, AIMaintenance.MoveReceiptReady, AIMaintenance.CleanupIntent, MaintenanceSkill.Start, MaintenanceSkill.Tick, MaintenanceSkill.Cleanup, AIMaintenanceRules.Check, AIMaintenanceRules.RepairConfirmed, AIMaintenanceRules.UpgradeConfirmed, AIMaintenanceRules.RecoveryReady
- 근거: work/장비정비-트리/00-agreement.md "이번 결과는 정확한 장비의 일반 수리와 레벨 업그레이드를 기존 생활 트리와 직접 지정 실행에 연결하는 것이다."
- 근거: work/장비정비-트리/04-implementation.md "일반 수리·단일 레벨 업그레이드·자기 pending 회수를 구현했다."
- 메모: 통합5a05b80의생산113/0·직접영향회귀·fast경고0/오류0·같은SHA Unity0과새독립교차30/0을확인한뒤공식에반영했다. 원판b1의88/0·독립pending22/0과구분한다. 실제모델/API·전체Tick·실게임소비자는미확인이며치트환경구축후묶음검증한다. 내구강화true·일괄/최대반복·가공·거래·낚시는이번지원이아니다.

- 근거: work/장비정비-트리/10-main-integration.md "이번 결과는 일반 단일 수리, 단일 레벨 업그레이드, 중단 뒤 자신의 정확 투입물 회수이다."


### F.action-constraints-open — 큰 작업·전체 후보와 정보·공간 제약 해제
- 상태: 구현됨·정적/대표오프라인수용·새실게임미확인
- 종류: 제품
- 한 줄: 공개된 지식과 실제 게임조건을 바탕으로 큰 목표량·긴 계획·여러 부탁과 후보/정보를 보존한다. 임의 수량/페이지/거리 제한을 전체 작업에 씌우지 않는다.
- 정책: pol.action-constraints-open, pol.ai-action-control, pol.original-request-completion
- 코드 앵커: AIKnowledge.KnownItems, AIPlanner.CheckGoal, AIPlanner.SetGoalFrom, AIHostOrders.Add, AILife.Compose, AICraft.ReadRecipe, AIFarm.ExactCandidate
- 근거: work/2026-11/2026-11-07-제약해제/00-agreement.md "사용자는 감사에서 현재 제약으로 보고한 항목을 전반적으로 해제하라고 지시했다."
- 메모: 코드 5cc5a43ff8df91634c04b44bac9f15f849bdb611의 정적연결과 실제 오프라인 보고만 연결하며 미확인 게임/모델 호출을 성공으로 표시하지 않는다.

### F.weapon-use-expanded — 손선택과 무기 입력·범용 정밀 제어
- 상태: 구현됨·정적/대표오프라인수용·새실게임미확인
- 종류: 제품
- 한 줄: 선택한 물품의 실제 유형/지원정보에 따라 정상 손선택·기본/보조입력과 관측적의 짧은 이동/접근/타격을 실행한다.
- 정책: pol.action-constraints-open, pol.ai-action-control
- 코드 앵커: AICombatOptions.Candidates, AIWeaponInput.Build, AIWeaponUseControl.Step, AttackSkill.Tick, AIInventory.TryChosenHand
- 근거: work/2026-11/2026-11-07-제약해제/actions.md "독립 검수 P1 소환 의미 수정 후속 commit:"
- 메모: 개별소환/최대충전해제의 게임입력 의미를 구분한다. 투사체·소환수·마나소비·명중의 실제게임 성공은 미확인이다.

