# 시험용 지형·물체 명령 구현

[합의](agreement.md)대로 격리 worktree `C:/Dev/CoreKeeperModSDK-cheat-commands`, 브랜치 `ai-cheat-commands-20261108`(20b1ace 기반)에 구현했다.

- 첫 구현: `c0bd393`
- [독립 검수](review.md) F1~F3 수정: `8a9fab4`. HEAD가 공식 ai-character에 fast-forward로 합쳤다.
- 추가 범위 `/ai hold`·`/ai tp`: `3dbae75`(8a9fab4 위). 아래 [추가 범위](#추가-범위-hold와-tp-3dbae75) 절에 정리했다.
- hold·tp 검수 F1·F2와 tp 불가 칸 거부: `ae09acf`(현재 최종). 아래 [후속 수정](#후속-수정-ae09acf) 절에 정리했다. 3dbae75와 ae09acf는 공식에 병합하지 않았다.

## 명령

모두 `AIPlayers/testcmds.on`이 있을 때만 동작한다. 기존 시험 명령과 같은 문지기(`AIPlayerSpikeMod.Dispatch`의 IsTestCommand 검사)를 쓴다. 다른 `/ai` 명령처럼 `/ai gemini placetile …`로 대상 AI를 고를 수 있다.

| 명령 | 문법 |
|---|---|
| 타일 하나 놓기 | `/ai placetile <Tileset> <TileType> <x> <z>` · `/ai placetile <ObjectID> <x> <z>` |
| 사각 범위 놓기 | `/ai placetilearea <Tileset> <TileType> <x1> <z1> <x2> <z2>` · `/ai placetilearea <ObjectID> <x1> <z1> <x2> <z2>` |
| 타일 하나 지우기 | `/ai removetile <TileType> <x> <z>` · `/ai removetile <ObjectID> <x> <z>` |
| 사각 범위 지우기 | `/ai removetilearea <TileType> <x1> <z1> <x2> <z2>` · `/ai removetilearea <ObjectID> <x1> <z1> <x2> <z2>` |
| 물체 생성 | `/ai spawn <ObjectID> <x> <z> [variation]` |

- **좌표:** 월드 타일 절대 좌표가 기본이다(`/ai hostto x z`, `/ai rmstation x z`와 같다). `~N`은 명령을 받은 AI의 타일에서 N칸, `~`만 쓰면 AI의 타일이다. 절대값이나 `~N`을 더한 결과가 ±1,000,000을 넘으면 사용법으로 응답한다.
- **이름:** Tileset·TileType·ObjectID는 게임 enum 이름이고 대소문자는 구별하지 않는다. 정의된 숫자도 받고, 정의되지 않은 숫자는 거부한다. 게임 화면의 표시 이름이나 한글 이름은 받지 않는다.
- **범위 상한:** 범위 명령은 한 번에 최대 4096칸이다. 칸 수는 long으로 센다. 원본에는 상한이 없다.
- **이름 겹침:** `spawn`은 최상위 명령이다. 기존 `/ai arena spawn`은 `arena`의 하위 단어라 겹치지 않는다.
- **동작:**
  - 놓기: TileUpdateBuffer에 Add를 넣는다. 벽은 roofHole, 바닥은 pit·water를 함께 지운다. 크리에이티브가 아니면 tileset 2(`Tileset.Obsidian`)와 코어 주변 네 칸(0,0)(0,1)(-1,1)(1,1)은 거부한다. 원본과 같다.
  - 지우기: 그 타일과, 그 타일을 필요로 하는 같은 칸의 타일을 지운다. 바닥·물을 지우면 pit을 놓는다. 원본과 같다.
  - 생성: 프리팹이 있는 물체(적·제작대·상자·식물 등)는 엔티티로 만든다. 그 밖의 것은 끌어당길 대상 없이(`Entity.Null`) 땅에 아이템으로 떨궈 (x,z)에 남긴다. Player는 거부한다.
- **응답:** 기존 형식대로 `[시험용] …`이고, 로그는 `TEST-ONLY <명령> [인자] from AI tile (x,z): 결과`다.

## 원본과 달라진 점

- 원본은 거부된 타일도 "Tile placed."로 알렸다. 여기서는 거부를 알리고, 범위 명령은 설치 수/전체 수를 알린다.
- 지우기에서 같은 칸의 타일을 읽을 때 원본의 TileAccessor 대신 우리 `AITileReader.Layers`를 쓴다(같은 지도 자료).
- C# 9 패턴 문법은 프로젝트 제한(LangVersion 8)에 맞춰 바꿨다.
- **타일셋 정의 검사:** 원본은 `<Tileset> <TileType>` 형식에서만 그 타일셋에 해당 타일 종류가 정의돼 있는지(QuadGenerator) 검사했고, ObjectID 형식은 타일 아이템의 TileCD를 검사 없이 놓았다. 여기서는 ObjectID 형식에도 같은 검사(`CheckTile`)를 건다. 정의가 없는 타일 아이템은 원본보다 좁게 거부될 수 있다. 다만 실게임 확인([live-validation.md](live-validation.md))에서 거부된 타일 아이템은 없었다.
- **좌표·범위 제한:** 원본에 없는 좌표 ±1,000,000 상한과 4096칸 범위 상한을 두었다.
- **잘못된 타일셋:** 원본은 정의되지 않은 타일셋 번호에서 예외가 났다. 여기서는 enum 정의 여부를 먼저 검사하고, 타일셋 자료나 층 정의가 없으면 "없음"으로 응답한다.
- **아이템 생성:** 원본은 프리팹 없는 물체를 명령 보낸 사람에게 강제로 줍게 했다(`DropNewEntity`의 끌어당김 대상). 여기서는 대상 없이 땅에 둔다. AI 가방을 채울 때는 기존 `/ai give`를 쓴다.

## 독립 검수 반영(`8a9fab4`)

- F1: 좌표는 long으로 계산해 ±1,000,000을 넘으면 거부한다. 범위 칸 수도 long으로 센다. 상한 우회 넘침과 `int.MaxValue` 반복 넘침이 없어졌다.
- F2: `spawn` 아이템 경로의 `DropNewEntity`에 `Entity.Null`을 넘긴다. 실게임에서 AI 3칸 옆에 생성한 나무가 AI 가방으로 들어간 현상(live-validation.md)의 원인이다.
- F3: Tileset·TileType에 `Enum.IsDefined`를 검사하고, `TilesetDataBlock.Get`이나 `layerDefinition.Get()`이 null이면 응답한다. NRE가 나지 않는다.
- F4: ObjectID 경로의 `CheckTile`은 유지하고 위 "원본과 달라진 점"에 적었다.
- F5(여러 AI에게 보내면 대상마다 실행됨)는 검수 권고대로 이번에 고치지 않았다.

## 바꾼 파일

- `Assets/AIPlayerSpike/Scripts/AITestWorldEdit.cs` (새 파일)과 `.meta`: 명령 처리 전부. 인자 해석(좌표·이름)은 우리 코드로 새로 썼고 CoreLib 코드는 쓰지 않았다.
- `Assets/AIPlayerSpike/Scripts/AIBody.Commands.cs`: `HandleCommand` switch에 다섯 명령을 추가하고 `IsSkillCommand` 목록에 추가했다.
- `Assets/AIPlayerSpike/Scripts/AIBody.cs`: `TestCommands` 목록에 추가했다. 시험 스위치 검사와 `/ai` 도움말 표시에 쓰인다.

## 라이선스 표시

ChatCommands MIT 원저작권 표시(Copyright (c) 2021 limoka)와 라이선스 전문은 `AITestWorldEdit.cs` 머리 주석에 있다. ModBuilder가 `.cs` 원본을 배포판에 복사하므로 표시도 함께 들어간다(검수 확인). 별도 LICENSE 파일은 만들지 않았다.

## 검증

| 검사 | 첫 구현 `c0bd393` | 검수 반영 `8a9fab4` |
|---|---|---|
| 모드 compile-check (`--no-incremental`) | 경고 0·오류 0, [compile-check.log](compile-check.log) | 경고 0·오류 0, [compile-check-fix.log](compile-check-fix.log) |
| Unity `HeadlessSdk.CompileCheck` | ok·종료코드 0, [unity-compile.log](unity-compile.log) | ok·종료코드 0, [unity-compile-fix.log](unity-compile-fix.log) |

- 두 번 모두 산출 어셈블리에 AITestWorldEdit가 포함된 것을 확인했다.
- 두 번 모두 Unity 실행 전에 Unity 프로세스와 이 worktree의 잠금 파일이 없음을 확인했다.
- worktree 환경은 이전 작업과 같은 방식으로 공식 작업트리에서 `Assets/Plugins/CoreKeeper`, SDK 로컬 설정 7개, `Library`를 복사했다(공식 쪽은 읽기만 함). 이 파일들과 빌드가 바꾼 `AIPlayerSpike.asset`은 커밋하지 않았다.
- 구현 담당은 게임 실행·설치를 하지 않았다. `8a9fab4`의 실게임 확인은 아직 하지 않았다.

## 실게임에서만 확인할 수 있는 점

- `8a9fab4`에서 프리팹 없는 아이템 생성물이 (x,z) 땅에 남는지(F2 수정 확인)
- 놓거나 지운 지형을 길찾기·채집이 정상으로 다루는지. 다만 AI 지형 기억(travel-memory 등)은 AI가 그 칸을 다시 볼 때까지 옛 상태일 수 있다.
- `spawn`으로 만든 제작대·상자·화로가 정상 설치물(상호작용, 저장 뒤 유지, 타일 점유)로 동작하는지, AI가 그것을 인식하는지
- `/ai all spawn …`처럼 여러 AI에게 보내면 대상마다 한 번씩 실행돼 물체가 여러 개 생기고, `~` 좌표도 AI마다 달라진다. 시험 때는 멤버 하나를 지정해야 한다.
- 범위 명령 4096칸이 한 프레임에 들어갈 때의 순간 부하

## 추가 범위: hold와 tp (`3dbae75`)

[합의 추가 범위](agreement.md)대로 같은 worktree·브랜치에 구현했다. 두 명령 모두 `testcmds.on` 아래에서만 동작한다. 다른 시험 명령처럼 AI가 월드에 들어와 걷는 상태일 때만 받는다.

| 명령 | 문법 |
|---|---|
| AI 정지 | `/ai <멤버> hold` (켜고 끄기를 바꿈) · `/ai <멤버> hold on` · `/ai <멤버> hold off` (`켬`·`끔`도 받음) |
| AI 순간이동 | `/ai <멤버> tp <x> <z>` · `/ai <멤버> tp host` |

### hold 동작

- **켤 때**
  - 실행 중 행동을 강제로 멈춘다(`AIBody.Stop(hard)`, 문 닫기 꼬리도 자른다).
  - 하위 판단의 대기 선택과 진행 중 요청을 버린다(`AIBrain.CancelPendingChoices`: 준비 취소·HTTP Abort·반사 끔).
  - 상위 계획의 진행 중 요청을 Abort하고, 그 trigger는 풀린 뒤 보낼 대기로 남긴다(`AIPlanner.DropRequestForHold`).
  - 목표는 지우지 않는다(`/ai stop`과 다른 점).
- **켜져 있는 동안**
  - 하위 판단 tick 전체를 건너뛴다(`AIBrain.Tick` 첫머리 `AITestControl.BlocksBrain`).
  - 몸 tick은 정지 입력만 낸다(`AIBody.HeldTick`): 기술·문 닫기·커서 정리·상위 계획 tick을 하지 않는다. 시험 명령으로 시작된 기술도 다음 프레임에 멈춘다.
  - 기억 관측(`AIMemory.Observe`)과 체력 기록(`AIVitals.Tick`)은 계속 읽는다. 풀린 뒤 실제 상태에서 시작하게 하려는 것이다.
- **풀 때**
  - 대기 선택·탐색 끝 찾기를 버리고 "정지(hold) 풀림"으로 지금 사실에서 다시 판단한다(`AIBrain.ForgetPlace`).
  - 보류된 상위 trigger는 다음 상위 tick에서 보낸다(재시도 시각 규칙 그대로).
- **유지 범위:** `AIAgent.Hold` 필드라서 같은 게임 실행 안의 재참가에는 유지되고, 게임 재시작 때는 꺼진다.

### hold가 막는 경로와 빠뜨림이 없다는 근거

**1. 모델 호출 지점은 두 곳뿐이다.**
- 스크립트 전체에서 `new UnityWebRequest`·`UnityWebRequest.Get/Post`·`System.Net`·`WebClient`·`HttpWebRequest`·`Socket`을 찾았다. 결과는 세 곳이다.
  - `AIDecisionsApi.Post`: 하위 판단 `https://api.openai.com/v1/decisions`
  - `AIPlanner.Report`의 `/v1/plan`: 상위 계획
  - `AIPlanner.HealthProbe`의 `/v1/health`: 시험 명령 `/ai bridge`의 브리지 상태 조회. 모델 호출이 아니어서 막지 않았다.
- `AIDecisionsApi.Post`는 `AIBrain.Loop.cs`의 준비 완료 경로 한 곳에서만 불린다. 그 준비는 `AIBrain.Send`에서만 시작되고, `Send`는 `AIBrain.Tick` 안의 두 곳(곧 끝남 미리 묻기, `_needCall` 판단)에서만 불린다.
- 두 겹으로 막았다. `AIBrain.Tick` 첫머리 게이트로 tick 자체를 막고, `Send` 첫머리에서 `Held`면 바로 돌아간다. 켤 때는 이미 진행 중인 준비·요청을 버린다.
- `AIPlanner.Report`는 private이고, 상위 계획 요청을 만드는 유일한 함수다. 호출하는 곳은 모두 AIPlanner 안이다.
  - `Enable`(auto_on), `OnHostSaid`(host_said: 채팅 멘션), `OnHostGoal`(host_goal: `/ai goal`)
  - `OnGoalEnded`(목표 끝·plan_step_failed), `OnBackUp`(back_up), `OnFightEnded`(fight_end)
  - `RequestReplan`(action_replan: AIBrain·AIExpedition에서 부름)
  - `Tick`(대기 trigger 재시도, periodic, plan_step_failed)
- `Report` 첫머리에서 `Held`면 trigger만 대기로 남기고 돌아간다. 채팅 멘션이나 `/ai goal`처럼 tick 밖에서 오는 경로도 여기서 막힌다.

**2. 자동 행동이 시작되는 곳은 `Step3_Drive` 하나다.**
- 캐릭터마다 프레임마다 `AIPlayerSpikeMod.Step3_Drive`가 `AIBrain.Tick`과 `AIBody.Tick`을 부른다. 이 두 tick을 부르는 다른 곳은 없다.
- **`AIBrain.Tick`이 맡는 자동 판단은 모두 tick 첫머리 게이트 뒤에 있다:**
  - 원정 관측과 명시 한도(`AIExpedition.Observe`·`EnforceExplicitLimit`: 미검토 자원 action_replan의 출처)
  - 목표 없을 때 자기방어(`SelfDefence`: 실기에서 `/ai stop` 뒤 판단 호출이 난 경로)
  - 긴급 재판단, 돌진 예측, 대기 전환 적용, 목표 완료 검사(`AIGoals.CheckDone`), 다음 타격
  - 반사(`SetReflex`, 늦은 응답 동안의 최소 반사), 미리 묻기, 적·체력 감시, 기지 가장자리 계기, 탐색 끝 찾기 작업
- **`AIBody.Tick`이 맡는 자동 행동은 hold면 `HeldTick`으로 갈라진다:**
  - 기술 tick과 문 닫기 꼬리
  - 내 문 추적(`AIEdge.TrackDoor`)과 문 걸음 입력
  - 커서 아이템 정리(`AICraft.StowCursorItem`), 시험 파일 지급
  - `AIPlanner.Tick`(그 안의 `AIExperience.Tick` 포함)
- `AIPlayerSpikeMod`의 Harmony 패치(ServerNetworkUpdate, UnloadWorldsInternal, 캐릭터 생성 람다, GetCharacterDataFromSerialized, SendChatMessage, ChatWindow·PugText·PlayerInput)는 통신·채팅·UI·입력 차단만 하고, 판단이나 행동을 시작하지 않는다.

**3. 실기 경로와 대조했다([live-validation.md](live-validation.md)).** `/ai stop` 뒤 103회 호출은 두 경로에서 나왔다.
- 슬라임이 다가오자 목표 없음 상태의 자기방어 판단이 나왔다. 이 경로는 `AIBrain.Tick → SelfDefence → Send`이고, tick 게이트와 Send 게이트에서 막힌다.
- 그 뒤 상위 계획이 다시 켜져 목표를 세웠다. 이 경로는 싸움 끝·periodic 등의 `Report`이고, `Report` 게이트에서 막히며 상위 tick도 돌지 않는다.
- 상위 답이 없으니 목표도 다시 생기지 않는다.

**막지 않는 것**
- 채팅 응답, 주기 캐릭터 저장, 기억·체력 읽기는 계속된다.
- 체력 기록 중 쓰러짐·복귀 사건은 상위 계획 사건 목록에 쌓이고, `OnBackUp`의 Report는 대기로만 남는다.
- 시험 명령이 시작한 기술은 다음 프레임에 멈춘다. 다만 기술 시작 함수 안의 즉시 효과는 막지 않는다. hold 중에는 기술 시작 시험 명령을 쓰지 않는다.

### tp 동작

- **목표 칸:** 좌표 규칙은 다른 시험 명령과 같다(월드 타일, `~N`은 AI 기준, ±1,000,000). `tp host`는 호스트 타일 바로 옆 1~3칸 고리에서 찾는다. 반경 3 안의 가장 가까운 걷기 가능 칸을 고르고, 고르는 방식은 `arena go`의 Nearest와 같다. 3dbae75는 걷기 가능 칸이 없으면 지정 칸 그대로 보냈다. ae09acf부터는 텔레포트하지 않고 사유와 사용법으로 응답한다(아래 후속 수정).
- **이동 방식:** `arena go`와 같이 게임의 플레이어 상태 기계를 쓴다. `PlayerStateCD.SetNextState(Teleporting)`와 `TeleportingStateCD.targetPosition`이며, 약 6초 걸린다. `arena go`와 달리 적 제거·회복·무기 교체는 하지 않는다. 쓰러져 있으면 거부한다.
- **이전 위치 기준 실행이 이어지지 않게:**
  - 명령 순간에 실행 중 행동을 강제로 멈추고 대기 선택·진행 중 요청을 버린다.
  - 그 뒤 몸이 목표 2칸 안에 있고 Teleporting 상태가 끝날 때까지(최대 20초) 하위 판단 tick을 막는다. 순간이동 중 옛 위치에서 후보를 준비하지 않게 하려는 것이다.
  - 도착(또는 20초)하면 옛 위치에서 준비한 대기 선택·진행 중 요청·탐색 끝 찾기 작업을 다시 버리고 "순간이동 도착"으로 새 위치에서 판단한다.
  - 상위 계획은 순간이동 중에도 막지 않는다(hold가 아니므로). hold 중에 tp하면 풀 때 같은 정리를 한다.

### 바꾼 파일(`3dbae75`)

- `AITestControl.cs` (새 파일)과 `.meta`: 두 명령, 하위 판단 게이트(`BlocksBrain`), 도착 판정
- `AIAgents.cs`: `AIAgent.Hold`, `TpPending/TpX/TpZ/TpSince`
- `AIBrain.Loop.cs`: `Tick` 첫머리 게이트, `Send` 첫머리 게이트
- `AIBrain.cs`: `ForgetPlace`(대기 선택·요청·탐색 끝 찾기 버리고 다시 판단)
- `AIPlanner.cs`: `Report` 첫머리 게이트, `DropRequestForHold`
- `AIBody.cs`: `Tick`의 hold 분기 `HeldTick`. 입력 만들기 끝부분을 `Emit`으로 떼어 냈는데 동작은 바뀌지 않았다.
- `AIBody.Commands.cs`, `AIBody.cs`의 목록: 명령 분기·시험 명령 목록에 `hold`·`tp` 추가. 행동 키 `hold`(제자리 유지 기술)는 채팅 명령과 다른 이름 공간이다.
- `AITestWorldEdit.cs`: 좌표 해석 `Pos`를 internal로 바꿔 tp가 같은 규칙을 쓰게 했다.
- hold·tp가 꺼져 있으면 모든 게이트는 거짓이다. 이때 바뀌는 것은 `AIBody.Tick` 끝부분이 `Emit`으로 옮겨진 것뿐이고, 코드 순서는 같다.

### 검증(`3dbae75`)

- 모드 compile-check(`--no-incremental`): 경고 0, 오류 0. 결과물에 AITestControl이 포함된 것을 확인했다. [compile-check-control.log](compile-check-control.log)
- Unity `HeadlessSdk.CompileCheck`: `CompileCheck: ok`, 종료코드 0. 실행 전에 Unity 프로세스와 잠금 파일이 없음을 확인했다. [unity-compile-control.log](unity-compile-control.log)
- 게임 실행·설치·공식 병합은 하지 않았다.

### 실게임 확인 항목(hold·tp)

- `hold` 켠 상태에서 decisions 사용량과 로그의 `DECISION … SEND`·`PLANNER: report … sent`가 0인지. 시험 조건:
  - 적이 다가와 체력이 깎일 때
  - 채팅 멘션을 받을 때
  - `/ai goal`을 받을 때
  - 쓰러졌다 일어날 때
- `hold` 중 AI가 실제로 서 있는지(입력 정지)와, 진행 중이던 행동이 바로 멈추는지
- `hold off` 뒤 원래 목표로 다시 판단하는지, 보류된 상위 trigger가 한 번 나가는지
- `tp x z`·`tp host`가 약 6초 뒤 목표 근처에 도착하는지, 도착 전에는 하위 판단 SEND가 없는지, 도착 뒤 새 위치 기준 후보로 판단하는지(옛 위치 경로가 이어지지 않는지)
- 걷기 가능 칸이 없는 곳(벽 속, 읽지 않은 영역)으로 tp했을 때의 결과
- hold와 tp를 함께 쓴 경우: hold 중 tp → hold off에서 새 위치로 판단
- F2 수정(`spawn` 아이템이 바닥에 남는지)도 함께 확인

## 후속 수정 (`ae09acf`)

[hold·tp 독립 검수](review-control.md)는 수용됐고, [실게임 확인](live-validation-control.md)도 통과했다. 그 뒤 HEAD 요청으로 아래를 같은 worktree·브랜치에서 처리했다.

### 피해 0의 원인 판정: 게임의 부활 뒤 무적이며, 이번 변경의 결함이 아님

**게임 코드**(`C:/Dev/_ref/corekeeper-decomp`)
- 부활 때 무적 조건이 붙는다. `PlayerController.RespawnPlayer`(PlayerController.cs:6990-7006)가 `ConditionID.ImmuneToDamageAfterRespawn`을 `duration = 0`, 곧 기한 없이 붙인다. 이 함수는 `Death` 상태의 부활(Death.cs:45·131)에서만 불린다.
- 그 무적은 첫 행동에서 끝난다. `ConditionsFromMovementSystem`(ConditionsFromMovementSystem.cs:247-262)이 이 조건을 보고 있다가, 다음 중 하나가 생기면 조건을 2초 기한으로 바꾼다. 그래서 첫 행동 2초 뒤에 무적이 풀린다.
  - 이동 목표 속도가 0.01을 넘음(`targetMovementVelocity` lengthsq > 0.01, :153)
  - `Interact_HeldDown`(공격·채굴 주 버튼), `SecondInteract_HeldDown`, `UseOffHand_HeldDown`
- 무적 동안에는 피격 등록이 막힌다. `AttackPlayerSystem.RegisterFakePlayerHit`·`RegisterPlayerHit`(AttackPlayerSystem.cs:1359·1474)이 이 조건 값이 0보다 크면 피격을 등록하지 않는다.
- 순간이동 자체의 무적은 짧다. Teleporting 상태(Teleporting.cs)는 들어갈 때 `isInvincible`·`isDyingOrDead`를 켜고, 4.1초에 위치를 옮기며 `isDyingOrDead`를 끄고, 6.1초에 `isInvincible`을 끄고 상태 잠금을 푼다. 6.1초 뒤에는 입력과 상관없이 풀리며, "행동 전까지" 계속되는 무적이 아니다.
- 사용자 정보("순간이동 후 행동 전까지 무적")는 이번 실기에서 맞는 현상이다. 다만 행동 전까지 이어진 무적의 출처는 순간이동이 아니라 그 앞의 부활이다. 순간이동은 약 6초 무적만 더한다.
- 벽 속으로 순간이동하면 부활이 생긴다. `ResolveStuckPositionSystem`(:185-224)은 갇힌 몸을 안전한 칸으로 옮기지 못하면 `SetNextState(Death)`로 죽이고, 그 결과 코어에서 부활시킨다.

**AI 입력이 해제 조건을 만족하는지**
- hold 중 `HeldTick`은 정지 입력(이동 0, 버튼 없음)만 보내므로 해제 조건을 만족하지 않는다.
- hold가 아니면 기술의 걸음(movement)과 공격(`Interact_HeldDown`)이 조건을 만족한다. 아래 로그로 확인했다.
- 따라서 hold 자체가 무적을 만들지는 않는다. 부활한 뒤 hold로 행동하지 않는 동안 게임의 무적이 그대로 남는다.

**로그 대조**([control-Player.log](evidence/control-Player.log), [notes](evidence/control-live-notes.txt))

| 시각 | 사건 | 피해 |
|---|---|---|
| t120.6 | hold 켬 | |
| t200.5~210.5 | hold 중, 부활 전. AggressiveSlimeBlob이 AI를 102→75→45→18→0으로 쓰러뜨림(DOWN #1) | 있음 |
| t215.5 | 코어 (0,-0.1)에서 부활. 무적 시작 | |
| t231.6~521.5 | hold 중 tp(11,-5), 적 0~1칸에서 체력 100·5로 대기 | 0 |
| t521.5 | hold off | |
| t522.3 | 첫 행동: 공격 기술 시작·걸음. 이때부터 2초 뒤(약 t524.3) 무적 해제 | |
| t523.9 | CavelingSpearman이 무적 2초 안에 처치됨. 이것이 "hold를 푼 구간 싸움에서도 피해 0"인 이유 | 0 |
| t554.9 | `긴급: 체력 감소(100→82)`, MushroomBrute 돌진(CHARGE target=AI hp 100->82) | 있음 |
| t~700 | 9x9 흙벽 한가운데로 tp(hold 중) | |
| t729.6 | hold off 때 위치가 코어 스폰 (0,-0.1)이었다. 갇힌 몸이 죽고 부활해 무적이 다시 붙은 것으로 추정한다. 근거는 부활 위치와 같은 좌표, 그리고 ResolveStuckPosition 코드이며, 체력이 0이 되지 않아 VITALS DOWN 기록은 없다 | |
| t730.1~743 | AI가 기다리기만 골라 해제 조건 미충족 | |
| t743~1060 | hold 중 tp(30,-20), 적 3종·체력 5에서 대기 | 0 |

- 1차 실기(c0bd393, AI tp 없음, [live-Player.log](evidence/live-Player.log))에서도 AI는 행동 중 피해를 받았다. t1178.6 MushroomEnemy가 27→0으로 쓰러뜨렸고, 그 뒤 부활했다. 부활 뒤 행동하면 피해를 받는다는 위 규칙과 맞는다.
- 판정: 피해 0은 게임의 부활 뒤 무적(해제 조건: 첫 이동이나 공격·상호작용 입력 뒤 2초)이다. tp의 Teleporting 상태는 6.1초에 정상적으로 끝났다(도착 판정에서도 Teleporting 아님을 확인). hold의 정지 입력은 무적을 만들지 않고 해제를 늦출 뿐이다. 이번 변경의 결함이 아니므로 고치지 않았다. 무적을 자동으로 푸는 기능은 사용자 미결정이라 넣지 않았다.
- 단, 벽 속 tp가 죽음과 부활을 일으킨 것은 tp가 걷기 불가 칸에도 보낸 탓이었다. 아래 3번에서 그런 tp를 거부한다.

### 시험 주의사항: 피해·쓰러짐 시험

- AI가 한 번이라도 쓰러져 부활했다면, 피해 시험 전에 무적 해제 조건을 먼저 만족시킨다. 그 뒤 2초가 지나야 피해가 들어간다.
  - 조건: hold를 풀고 AI가 실제로 이동하거나 공격·채굴하게 하기, 또는 시험 명령으로 짧은 이동(`/ai <멤버> goto …`)이나 공격을 시키기
  - hold 중에는 해제되지 않는다.
- 벽 속 순간이동(3dbae75 이전 판), 갇힘 해소 실패, 쓰러짐은 모두 부활이라서 위와 같다.
- `tp`·`arena go` 직후 약 6.1초는 순간이동 자체의 무적이다. 그 뒤 피해 시험을 한다.
- `/ai <멤버> hp N`은 체력 값만 바꾸고 무적 조건은 그대로 둔다.

### 고친 내용

1. **검수 F1(hold 중 호스트 말 보존)**
   - `AIPlanner.DropRequestForHold`가 진행 중이던 보고가 host_said·host_goal이면, 기존 409·AskAgain·KeepForRetry 경로처럼 `S._chatSeen = S._reqChatSeenBefore`로 되돌린다. 풀린 뒤 다시 나가는 보고에서 호스트 말이 새 말로 남는다.
   - 대기 trigger는 `PlanInbox.Priority`로 더 급한 쪽을 남긴다(호스트 > 기타 > periodic). hold 중 `Report` 게이트도 같은 규칙을 쓴다. 그래서 나중에 온 fight_end·periodic이 보류된 host_said·host_goal을 덮지 않는다. 우선순위가 같으면 새 trigger가 남는다.
2. **검수 F2(스위치 없이 hold off)**
   - `Dispatch`의 시험 명령 거부에서 "hold 중인 멤버의 `hold` 명령"은 예외로 둔다. `testcmds.on`이 사라져도 `hold off`를 받는다. hold 중이 아닌 멤버의 hold와 `tp`는 그대로 스위치가 있어야 한다.
   - `/ai <멤버>` 상태에 `정지(hold) 켜짐: /ai <멤버> hold off`가 붙고, `/ai` 전체 상태에는 `/정지(hold)`가 붙는다.
   - 월드를 내릴 때 자동으로 푸는 것은 넣지 않았다(검수 "검토할 만함", 합의는 게임 재시작까지만 유지).
3. **tp 불가 칸 거부**
   - 반경 3 안에 걷기 가능 칸이 없으면 텔레포트하지 않는다. 행동·대기 선택도 건드리지 않고, `[시험용] 순간이동 안 함: … 주변 3칸 안에 걸을 수 있는 칸이 없음(벽 속이거나 아직 읽지 않은 곳)`과 사용법으로 응답한다.
   - `tp host`도 같은 규칙이다.
   - 읽지 않은 먼 영역은 `AITileReader`가 기본 벽으로 읽으므로 거부된다. 그곳을 시험하려면 호스트를 먼저 보내(`/ai <멤버> hostto x z`) 영역을 읽힌 뒤 tp한다.
- 파일 머리 주석(`AITestControl.cs`)에 부활 무적 주의사항을 적었다.

### 검증(`ae09acf`)

- 모드 compile-check(`--no-incremental`): 경고 0, 오류 0. [compile-check-followup.log](compile-check-followup.log)
- Unity `HeadlessSdk.CompileCheck`: `CompileCheck: ok`, 종료코드 0. 실행 전에 Unity 프로세스와 잠금 파일이 없음을 확인했다. [unity-compile-followup.log](unity-compile-followup.log)
- 게임 실행·설치·공식 병합은 하지 않았다.

### 실게임 확인 항목(ae09acf)

- 진행 중인 host_said 보고가 있을 때 hold를 켜고, 그 사이 fight_end 같은 낮은 trigger가 생겨도 풀린 뒤 host_said(또는 host_goal) 보고가 한 번 나가고 호스트 말이 새 말로 들어가는지
- hold 중 `testcmds.on`을 치운 뒤 `hold off`가 받아지는지, `/ai`·`/ai <멤버>` 상태에 hold가 보이는지
- 벽 속·읽지 않은 칸으로 `tp`하면 거부 응답이 나오고 AI가 움직이지 않는지
- 피해 시험: 부활한 AI가 이동·공격한 뒤 2초가 지나면 피해를 받는지(위 주의사항대로)
