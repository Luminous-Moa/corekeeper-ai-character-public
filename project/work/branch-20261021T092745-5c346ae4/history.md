<!-- head-context branch:1 -->
# branch-20261021T092745-5c346ae4

이름표: 사용자 규칙: 서브에이전트는 Opus 5.5 high 이상만, 하위 모델 금지
시작: 2026-10-21T09:27:45.715Z
갈래: 코어키퍼-AI-캐릭터/메인@없음
갈래-ID: 
상태: 끝남

## 지금까지 요약

초기 타당성 조사와 실험1 모드(AIPlayerSpike) 단계의 옛 형식 작업 브랜치. 이후 작업(몸 방식 확정, 몸-기본-기술 등)으로 이어져 이미 넘어섰다. 이름표의 서브에이전트 모델 지정은 2026-10-31 사용자 결정으로 철회됐다(context.md). 2026-11-04 브랜치 정리로 닫았다(사용자 "일단 브랜치 정리해줘").


## 2026-10-21T09:34:54.192Z

요약: 타당성 조사 완료: 서버 모드로 기존 NPC/생물을 몸으로 소환·조종 가능, 가짜 플레이어는 불가

사용자 목표: AI 단톡방 멤버를 Core Keeper 안에서 캐릭터로 움직이게 함(채팅 연결만이 아님). 사용자가 게임 설치를 해줄 수 있다고 함(현재 미설치).

조사 결과(문서·공개 모드 소스, 게임 미실행):
- 가짜 플레이어 소환 불가: ChatCommands 모드가 ObjectID.Player 소환을 거부. 플레이어 개체에는 네트워크 연결(PlayerGhost.connection)이 필요.
- 서버 모드가 임의 개체 소환(EntityUtility.CreateEntity), LocalTransform으로 위치 변경(공식 system-example), 타일 제거/설치(ChatCommands Remove/PlaceTileCommand), 아이템 드롭(API.Server.DropObject) 가능.
- 주변 인식: TileAccessor/PugDatabase로 타일 읽기(CoreKeeperAccess TileReader.cs).
- 채팅: CoreLib ServerCommSystem.SendChatMessage(서버·클라 모두 CoreLib 필요).
- 외부 통신: Skip Safety Checks 모드가 File IO 사용한 사례 있음(CoreKeeperAccess). HTTP/소켓 사용 사례는 못 찾음 → 첫 연결은 JSONL 파일 대기열 권장.
- 공격·아이템 사용은 디컴파일 필요(미확인). 컴패니언/봇 모드 선례 없음.
- ModExistsOn Client/Server/Both 설정 존재. 서버 전용 모드로 클라이언트가 모드 없이 들어올 수 있는지는 미확인.
- SDK 임포터는 CoreKeeper.exe 있는 폴더 요구 → 게임 클라이언트 설치 필요.
- 대안: AI마다 실제 게임 클라이언트+Steam 계정 하나씩, 모드로 입력 주입(ClientInputData) → 진짜 플레이어 몸이지만 계정·실행 비용 큼.
- 단톡방 재사용: 멤버는 JSON(action/move/build/mine/craft/hunt/world_look)으로 행동, actors.mjs/queue.mjs가 단계 실행, 주변은 한국어 글 지도로 전달 → 2D 좌표·타일 글지도로 바꿔 브리지(ckBridge)로 모드에 전달하는 구조 추천.

출처: modding.corekeepergame.com, github.com/Pugstorm/CoreKeeperModSDK, github.com/limoka/CoreKeeperMods, github.com/CoreKeeperMods/CoreLib, github.com/kaeltosh/CoreKeeperAccess

다음: 사용자와 문제·목표·조건 맞추기: 몸 방식(NPC 조종 vs AI별 실제 게임 클라이언트), 첫 범위, 게임 설치 시점



## 2026-10-21T09:37:36.361Z

요약: 몸 방식 합의: 모드가 조종하는 NPC, 플레이어처럼 보이고 싸우고 가방·장비·제작으로 게임 진행

사용자 결정(1장 문제·목표·조건):
- 몸: 모드로 조종하는 NPC. 게임이 진짜 플레이어로 인식할 필요는 없음 — "플레이어처럼 게임 진행만 되면 돼".
- 꼭 필요한 것: 보이는 것(사람 모습·이름표·말풍선), 싸우는 것(적이 노리고 AI도 싸움), 가방·장비·제작(흉내 방식 허용).
- 제가 덧붙인 것(사용자 이의 없음, 2장에서 확인): 단톡방은 그대로 돌고 Core Keeper는 또 하나의 월드로 붙음 / 방장이 이 PC에서 월드를 열고 AI가 들어옴 / 한 멤버로 먼저 확인 후 넷으로.
- 미확인·게임 필요: 접속 없는 플레이어 개체 소환 가능 여부(되면 최선), NPC의 원래 AI 끄기·이동 충돌, 공격·피해 방법, 제작 처리.

다음: 2장 사용 흐름 승인받기 + 게임 설치 요청 → 설치 후 실험(접속 없는 플레이어 소환, NPC 조종·공격)



## 2026-10-21T09:50:59.122Z

요약: 게임 디컴파일 결과: 게임 내장 thin client 경로로 진짜 플레이어 몸 가능성 높음, 폴백은 미니언 몸

사용자가 Core Keeper 설치(기본 Steam 경로, Mono 빌드). 서브에이전트가 ilspycmd 11.1.0.9782 nuget 패키지를 C:\Dev\_ref\tools\ilspycmd-pkg에 받아(사용자 사전 승인 없음, 보고 필요) 10개 어셈블리를 C:\Dev\_ref\corekeeper-decomp\에 디컴파일. 게임 폴더 무변경.

핵심(V=코드 확인):
- 플레이어 생성: StartGameRPCSystem이 서버에서 EntityUtility.CreateEntity(ObjectID.Player)+PlayerGhost{connection}+GhostOwner+CommandTarget. 게임 자체는 Player 소환을 막지 않음.
- Thin client(V): Unity Netcode for Entities 사용, 게임에 ThinClientConnectSystem 존재(127.0.0.1:7777 접속, PlayerConnectRequestRPC isOwner=false, StartGameRPC isThinClient=true, 호스트 캐릭터 데이터 재사용). 서버는 진짜 연결 기반 플레이어 생성. 단 ECSManager.CreateThinClientWorld()는 private이고 호출처 없음; ClientServerBootstrap.CreateThinClientWorld()는 public. thin 월드는 SendClientInputSystem 없음 → 모드가 입력 작성 필요.
- 연결 없는 Player(connection=Entity.Null)는 플레이어 대상 RPC가 전체 방송되는 문제(RpcCommandRequest.cs:39).
- 폴백 몸: MeleeMinion/Caveling, 상태머신 AI(StateInfoCD, ChaseStateCD.isDisabled 등), MoveToPositionFromCommandStateCD+PathFindCD로 이동, 주인 없으면 MinionHandlerSystem이 파괴.
- 전투: ChaseStateRequest가 FactionCD/HealthCD/BehaviourTags로 타깃 선택(플레이어 불필요), AttackSystem.Helper.Attack로 피해.
- 가방: 플레이어·상자 모두 InventoryBuffer, 레시피 PugDatabase.requiredObjectsToCraft.
- 외형: PlayerCustomizationCD는 PlayerController만 그림 → 플레이어 모습은 Player 프리팹만. 이름표 PlayerController.UpdateName. 말풍선 없음, 채팅 NetworkChatServerSystem.
라이브 테스트로만 확인할 것: 런타임 2번째 클라 월드 생성 가능(드라이버 공유, Steam 전송), 접속 검사 통과, playerGuid 충돌·저장, 모드 시스템이 thin 월드에 붙는지, 서버측 입력 주입, 어그로·사망.

다음: 참고 모드 조사 결과 받기 → 사용자에게 보고(thin client 1순위, ilspycmd 다운로드 고지) → 라이브 실험 준비(Unity 설치·로그인)



## 2026-10-21T09:51:20.723Z

요약: 참고 모드 조사 완료: AI 동료 선례 없음, 부품별 참고 코드 확보(Archipelago WebSocket·AggressivePets·ServerNeverSleeps 등)

클론 위치 C:\Dev\_ref\ck-mods-ref\<owner>_<repo>. Core Keeper AI 동료/봇 플레이어 모드 선례 없음.
참고(V=코드 확인):
1. URAQTDev/Core-Keeper-Archipelago (MIT): 모드 안 외부 WebSocket(netstandard2.1 DLL+외부 DLL, manifest skipSafetyChecks/accessesExtraAssemblies), ConcurrentQueue로 메인스레드 전달, PlayerGhost로 플레이어 찾고 InventoryChangeBuffer로 아이템 지급, API.Server.InstantiateObject 스폰.
2. Sisyphean-a/hhjy AggressivePets (무라이선스): StateRequestGroup 앞 시스템으로 ChaseStateCD·NearbyEntitiesTrackerCD·CombatantsTrackerBuffer 조정해 바닐라 AI 조종.
3. Adrriiannn/ck-mods ExpandNullforge: 상태 Authoring 필드 정리, 걷는 커스텀 NPC(DimensionTalkingCreatureView), 플로팅 텍스트.
4. Valgard/core_keeper docs/ck 핸드북: 샌드박스 목록, 넷코드=수정된 NetCode for Entities 1.2.4, RPC 추가 시 프로토콜 해시 변경, 사람 없으면 서버 timescale 0. LabeledSign(이름표 복제).
5. KrisCris ServerNeverSleeps: 앵커 엔티티로 플레이어 없는 영역 유지.
6. CombatMeter/limoka DummyMod (MIT): HealthChangeBuffer 읽기.
7. ReishyouSose (GPL-3): 서버 투사체 생성, TileAccessor 대량 설치.
8. core-keeper-rpc: Player.log 채널. LivePlayers(MIT).
9. flown-dev-1/Player-Skins: PlayerController Harmony 패치.
타 게임: mindcraft(MIT, Node+mineflayer), Voyager, Sapienheim(Valheim 서버모드+외부 에이전트, 비공개).

다음: 사용자 보고 후 라이브 실험 준비: Unity Hub+6000.0.59f2(Linux Build Support Mono)+로그인, SDK 클론, 실험1 thin client 플레이어 띄우기



## 2026-10-21T10:38:02.822Z

요약: Unity 6000.0.59f2+Linux Mono 설치·로그인 완료, SDK 클론, 실험1 모드(AIPlayerSpike) 제작 위임

- 사용자가 Unity 계정 생성·로그인. Unity Hub는 MSIX로 설치됨(WindowsApps\UnityTechnologies.UnityHub_3.22.3...) — 처음에 Program Files만 확인해 미설치로 오판했었음.
- 에디터: C:\Program Files\Unity\Hub\Editor\6000.0.59f2 (Linux/Windows 지원). 헤드리스 설치도 끝나서 6000.0.59f2-x86_64 중복 설치 있음(나중에 Hub에서 삭제 가능).
- SDK: C:\Dev\CoreKeeperModSDK (Pugstorm/CoreKeeperModSDK 0e4e137, 2026-10-14).
- Unity changeset 6000.0.59f2 = ef281c76c3c1.
- 실험1 위임: AIPlayerSpike 모드 — 월드 호스팅 후 thin client 월드로 두 번째 플레이어 생성, 이름 "Claude", 입력 주입으로 걷기, [AIPlayerSpike] 로그. 불가능하면 미니언 몸 폴백. 빌드·설치까지, 게임 실행은 사용자.

다음: AIPlayerSpike 준비 결과 받기 → 사용자 라이브 테스트 → Player.log로 판정



## 2026-10-21T11:02:10.033Z

요약: 실험1 모드 AIPlayerSpike 빌드·설치 완료(헤드리스 SDK, thin client 경로), 사용자 라이브 테스트 대기

- 헤드리스 SDK 도우미: C:\Dev\CoreKeeperModSDK\Assets\HeadlessTools\Editor\HeadlessSdk.cs (ImportGameFiles 209 어셈블리 → Assets/Plugins/CoreKeeper, CreateMod, BuildAndInstall). Unity.exe -batchmode -quit -projectPath ... -executeMethod HeadlessSdk.<step>. 로그 Logs\headless-*.log. Personal 라이선스로 배치모드 동작.
- 모드 소스: C:\Dev\CoreKeeperModSDK\Assets\AIPlayerSpike\Scripts\AIPlayerSpikeMod.cs. 설치본: <게임>\CoreKeeper_Data\StreamingAssets\Mods\AIPlayerSpike\ (게임 폴더 유일한 변경). 게임이 시작 시 소스 컴파일. 컴파일 확인용 C:\Dev\_scratch\AIPlayerSpikeCheck\.
- 경로: thin client. 게임 기본 thin 설정은 호스트 클라 드라이버를 리셋·같은 7777 공유 → 모드가 127.0.0.1:7778 별도 링크 만들고 Harmony로 NetworkingManager.ServerNetworkUpdate에 패킷 중계. 미니언 폴백은 미구현.
- 동작: 호스팅 월드 진입 10초 후 thin 월드 생성→접속·handshake→두 번째 플레이어(STEP1), 이름 Claude(STEP2, 캐릭터데이터 개명+서버 직접 개명), 5초 후 이동 입력 route A(thin 클라 입력)/3초 무이동이면 route B(서버 입력 버퍼)(STEP3). 30초 정체 시 진단 로그. 퇴장 시 정리.
- 주의: Claude는 호스트 캐릭터 복사본으로 접속 → 아이템 복제·월드에 저장 → 버릴 월드에서 테스트. 모드는 모든 호스팅 월드에서 동작.
- 로그: %USERPROFILE%\AppData\LocalLow\Pugstorm\Core Keeper\Player.log (추정), 키워드 AIPlayerSpike, STEP 1 OK 등.

다음: 사용자 라이브 테스트 → Player.log로 STEP1~3 판정



## 2026-10-21T13:39:24.052Z

요약: 설계 원칙(사용자): AI는 실시간 처리 불가 → 실시간 행동은 프로그램으로 구현하는 게 핵심

사용자(마인크래프트 봇 경험): AI가 실시간 처리를 전혀 못 하니 프로그램 구현이 매우 중요, Core Keeper는 더 그럴 것.
HEAD 제안(설계 기준 후보): 두 층 — AI(단톡방 CLI, 한 번 판단에 수 초~수십 초)는 목표·스킬 선택과 말만, 모드 안 C# 컨트롤러가 매 틱 실행(길찾기, 채굴 반복, 따라가기, 전투 반사: 가까운 적 공격·위험 시 후퇴·체력 낮으면 먹기). 컨트롤러는 사건(맞음, 체력 낮음, 목표 완료, 막힘)을 AI에 보내 다음 판단을 깨움. 빠른 층은 Node가 아니라 모드 안(서버 틱, 통신 지연 없음). 몸 선택과 연결: 진짜 플레이어 몸은 바닐라 AI가 없어 전투 컨트롤러를 직접 만들어야 하고, NPC 몸은 바닐라 추적·공격을 공짜로 얻음.

다음: 사용자 라이브 테스트 → Player.log로 STEP1~3 판정 (설계 시 두 층 원칙 반영)



## 2026-10-21T13:49:31.716Z

요약: 행동 설계 합의(잠정): 기술은 프로그램으로 탄탄히, 선택은 성향값 점수 방식(교체 가능), Jev는 나중 비교 후보. 라이브 테스트는 내일

사용자 질문: 선택지를 세세하게 구현하고 Jev(TypeSafe AI의 즉각 결정 모델, 0.1~0.5초 응답 주장, 정확도 최상위 모델보다 낮음, API 유료) 같은 모델이 고르게 하면 더 낫나?
HEAD 판단:
- 층 구분: 순간 반응(회피·조준·타격, 0.1초 이하)은 프로그램, 전술 선택(0.5~2초)은 Jev 가능 자리, 목표·성격·대화(수십 초)는 단톡방 멤버.
- 기술(길찾기·캐기·따라가기·공격·후퇴·먹기·제작)은 어느 쪽이든 프로그램으로 만들어야 하며 품질 대부분이 여기서 정해짐. 차이는 고르는 부분뿐.
- 시작: 선택지마다 점수(성향값 반영) 매기는 단순 방식. 선택지·상황 설명 형식을 고르는 쪽과 무관하게 만들어 Jev 등으로 교체 가능하게.
- Jev는 API 키·비용 필요 → 단톡방의 "API 키 없음" 원칙과 충돌, 사용 여부는 사용자 결정(미정).
사용자: 오늘은 게임을 못 켜봄, 내일 시험 후 방향 정하기로.
주의: AIPlayerSpike 모드가 게임 폴더(CoreKeeper_Data\StreamingAssets\Mods\AIPlayerSpike)에 설치돼 있어 어느 호스팅 월드에서든 동작함.

다음: 내일: 사용자가 새 월드에서 AIPlayerSpike 시험 → Player.log로 STEP1~3 판정 → 몸 방식·방향 확정 후 구현 설계(3장)



## 2026-10-21T15:00:54.056Z

요약: 사용자 규칙: 서브에이전트는 Opus 5.5 high 이상만, 하위 모델 금지

사용자 지시(이후 모든 작업): 서브에이전트는 Opus 5.5 high 이상 사용, 하위 모델 사용 금지.
적용 방법: Agent 호출 시 subagent_type "opus-high"(Opus 5.5 high 고정) 사용, 또는 model opus + effort high 이상 명시. 위임 브리프에 "하위 에이전트를 띄울 때도 Opus 5.5 high 이상만" 포함.
참고: 어제 위임한 조사·디컴파일·시험 모드 에이전트는 general-purpose(부모 모델 Opus 5.5 상속, effort 미지정)였고, 디컴파일 에이전트가 띄운 보조 에이전트 2개의 모델은 확인 안 됨.

다음: 사용자가 새 월드에서 AIPlayerSpike 시험 → Player.log로 STEP1~3 판정 → 몸 방식·방향 확정 후 구현 설계(3장). 서브에이전트는 opus-high.

