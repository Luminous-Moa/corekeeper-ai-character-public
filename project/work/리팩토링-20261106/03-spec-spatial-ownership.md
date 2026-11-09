# 단계 3~4 구현 명세 — 공간 공통화와 책임 소유

2026-11-06. 작성 기준 `C:/Dev/CoreKeeperModSDK`, ai-character `a07166f`. 코드·게임·시험 실행 없이 소스를 읽어 작성했다. 구현할 worktree는 `C:/Dev/CoreKeeperModSDK-refactor-20261106`, 브랜치는 `refactor-maint-20261106`이다. 단계 0~2 구현·검수 뒤 실제 HEAD와 이 명세의 앵커를 대조하고 진행한다.

입력: [승인 범위](00-agreement.md), [승인된 개발 방향](../2026-11/2026-11-06-리팩토링-현황과-계획/plan.md), [최신 조사](../2026-11/2026-11-06-리팩토링-현황과-계획/source-audit.md), 앞선 `01-spec-goals-candidates.md`와 그 구현 결과. 이 문서는 부모 HEAD의 검토 뒤 코더 입력이 된다.

경로 약어 `S/`는 worktree의 `Assets/AIPlayerSpike/Scripts/`, `T/`는 `Tools/core-tests/`다. 아래 줄 번호는 위치 안내이며 함수·형식의 실제 경계를 따른다. 새 파일은 C# 8 이하, 같은 소스 어셈블리에 두고 `.meta`도 포함한다. Core는 Unity·게임 참조를 갖지 않는다.

## 1. 결과와 구현 단위

목표는 기능별 변경 위치와 공유 규칙의 소유자를 명확하게 하는 것이다. 기존 동작을 보존하며 다음 네 단위를 각각 커밋·검수한다.

1. **M: 기존 형식의 파일 분리** — 몸/관측/이동/기술, 제작/놓기, 탐험 파일에 들어 있는 기억·물체·경로, 모드/저장/패치의 실제 형식 경계를 드러낸다.
2. **Q: 공간 사실 조회** — 문 조회와 놓인 물체 필터의 소유자를 정하되 각 소비자의 시야·순서·물체 해석을 보존한다.
3. **R: 이동 후보 공통 절차** — 첫 세 설 칸 중 처음 성공한 경로 선택, 보행용 dig 후보의 공통 필드를 공유한다. 경로 정책과 문장은 기능에 남긴다.
4. **O: 판단 준비와 위협 기록** — 준비 계기를 호출 인자로 한정하고 위협 평가·기록·이전 판정 조회를 구별한다. 새로운 snapshot이나 초기화 시점은 만들지 않는다.

M을 먼저 두는 이유는 뒤의 실제 공통화 변경과 기계적 이동을 diff에서 구별하기 위해서다. M의 게임 동작 근거가 없는 책임은 기준 실행을 먼저 확보한다. 확보가 불가능하면 해당 책임만 보류하고 HEAD에 알린다. 제품 소스 전체의 대체·새 프레임워크·새 저장소는 만들지 않는다.

## 2. 공통 불변식

- 반경 10 생활 기지, 둘레 내부, goto 도착, place 집계 영역은 서로 다른 용도다. `AIPlaceGoal.InArea`의 현재 **반경 OR 둘레** 식을 보존한다.
- `AIBase.StandNear`의 알려진 칸 반경 6→실패 시 모르는 걸을 수 있는 칸 반경 3 fallback, `AICraft.StandTilesFor`의 footprint 바깥 후보, 밭의 점유물 제외·정렬을 그대로 둔다. `StandOk`를 이들에 새로 적용하지 않는다.
- `PlanC`의 캐시 key·보존 시간·거리·detour 표시, `AITunnel.Calls/TwoPass`, 탐색/후보 예산, 후보 순서·중복 제외·`curKey` 비교 시점은 유지한다. 밭은 `AITunnel.Plan` 직접 호출이다.
- 문 틱·누름·입력 실행은 AIBody 단일 소유다. `StandTile/PlanFrom/DoorCell/OwnDoorStands/MyDoorLook/PassSide/AISettle/AIPressQueue`를 다시 구현하지 않는다.
- 메모리의 `Ensure`는 월드 변경 시 저장/로드에 닿는다. 조회를 미리 계산하거나 캐시로 바꾸거나 순서를 옮기지 않는다. 게임 관측/기억 저장·부활/재접속·캐릭터 scope도 그대로다.
- 기존 예외 경계와 로그·문장·ID·목표 revision·EndOrder를 보존한다. 새 fallback/retry/로그를 추가하지 않는다.

## 3. M — 기존 책임 단위의 기계적 이동

### 3.1 파일 배치

아래 형식의 선언·속성·메서드 본문·필드 선언 순서는 동일하게 옮긴다. using은 원본 집합을 복사해 시작할 수 있다. 불필요 using 정리는 이번 판정에 중요하지 않다. namespace, 공개 이름, 접근 수준, attribute, 상속, 기본값은 바꾸지 않는다.

| 원본 | 남기는 것 / 옮길 형식 | 목적 파일 |
|---|---|---|
| AIBodySkills.cs | AIBody 본체를 남긴다. 파일명은 AIBody.cs로 변경하고 기존 `.meta` GUID 유지 | AIBody.cs |
| 같은 파일 | BodyInput, SkillStatus, AISkill | AISkill.cs |
| 같은 파일 | AIVision | AIVision.cs |
| 같은 파일 | AITileReader, AIWallData | AITileReader.cs |
| 같은 파일 | SeenEnemy, AIObservation | AIObservation.cs |
| 같은 파일 | NavStatus, AINavigator | AINavigator.cs |
| 같은 파일 | LookSkill, InvSkill | AIInspectSkills.cs |
| 같은 파일 | GotoSkill | AIGotoSkill.cs |
| 같은 파일 | MineSkill | AIMineSkill.cs |
| 같은 파일 | AttackSkill | AIAttackSkill.cs |
| 같은 파일 | WalkTestSkill, AIMoveTrace | AIMoveTrace.cs |
| AICraft.cs | AICraft, CraftSkill를 남김 | AICraft.cs |
| 같은 파일 | AIPlaceSpot, PlaceSkill | AIPlace.cs |
| 같은 파일 | RootSkill, BreakSkill | AIBreakSkills.cs |
| AIExplore.cs | AIMemory를 옮기고 기존 파일의 `.meta` GUID 유지 | AIMemory.cs |
| 같은 파일 | AIDrops | AIDrops.cs |
| 같은 파일 | AIObjects | AIObjects.cs |
| 같은 파일 | TunnelPlan, AITunnel, TunnelSkill | AITunnel.cs |
| AIPlayerSpikeMod.cs | AIPlayerSpikeMod 본체를 남김 | AIPlayerSpikeMod.cs |
| 같은 파일 | ThinClientDriverConstructor, AIPlayerSpikePatches | AIPlayerSpikePatches.cs |
| 같은 파일 | AIWorldSettings | AIWorldSettings.cs |
| 같은 파일 | AICharacterStore | AICharacterStore.cs |
| AIEdge.cs | EdgeFillSkill, DoorPassSkill, DoorCloseSkill | AIEdgeSkills.cs |
| AIEdge.Guard.cs | PickupSkill | AIPickupSkill.cs |

AIExplore.cs에는 위 형식들 외 독립 탐험 조율기가 없다. 이름만 남긴 빈 파일을 만들지 않는다. 참조하는 주석·현재 개발 문서는 새 책임 파일을 가리키게 한다. 역사 문서의 원문 경로는 고치지 않는다.

### 3.2 몸의 명령 경계

`AIBody`를 `static partial`로 하고 `HandleCommand`, `TestOnlyWhere`, `TestOnlyTiles`, `IsSkillCommand`, `IsTestCommand`, `TestSwitchPath`, `TestCommandsOn`만 `AIBody.Commands.cs`로 옮긴다. 각 본문은 그대로 둔다. `TestCommands` 배열을 포함한 정적 필드/상태 선언은 AIBody.cs에 남겨 초기화 순서와 상태 소유를 유지한다. 새 command 객체나 상태 복사본을 만들지 않는다. 배열에 'command parsing metadata; consumed by AIBody.Commands' 주석만 가능하다.

`Tick`, `StartSkill`, `Stop`, `EndSkill`, `UseBitFor`, `NudgeFor`, State는 AIBody.cs에 남는다. `HandleCommand`의 `goal` 분기는 단계 1의 바뀐 진입점을 그대로 가져간다. 명령 결과를 다른 틱으로 미루거나 별도 큐로 보내지 않는다.

`AIPlayerSpikeMod`도 `partial`로 하고 채팅/명령 조율 method만 `AIPlayerSpikeMod.Chat.cs`로 옮긴다: `TryHandleChatCommand`, `TryHandleMention`, `IsAll`, `Without`, `DefaultAgent`, `Dispatch`, `IsAiCommand`, `StageText`, `StatusText`, `StatusAll`, `Reply`, `FlushReplies`, `MentionNames` property. 상태 필드 `_pendingReplies`, `_renderChatText`, `_mentionNames`, `_repeatGate`, `ChatPatched`와 나머지 필드는 원래 본체에 남겨 정적 초기화 순서를 유지한다. `TurnOn/TurnOff/SetRoute/RequestServerDisconnect`는 실제 수명/입력 변경이므로 본체에 남는다. 이 이동은 같은 class의 private 경계 안에서 이루어지고 `AIAgent.Enter` scope와 호출 순서를 그대로 유지한다. 이 부분을 새 채팅 큐·서비스 도입이나 상태 소유자 변경으로 보고하지 않는다.

### 3.3 둘레 파일의 읽기/행동 경계

`AIEdge.Guard.cs`의 `ReachRange`부터 `CannotCome`까지(현 기준 23~239, OwnDoorTilesNow/DoorsNear/StandOk/StandTile 포함)를 `AIEdge.Reach.cs`의 같은 partial로 이동한다. 단계 Q/O에서 바꾸기 전 M에서는 본문을 그대로 유지한다. `S`의 State 정의는 AIEdge.cs에 남긴다. 나머지 guard는 둘레 기억·보수·문 꼬리·후퇴를 맡는다. `MemoryNow`는 reach가 사용하는 기존 method이므로 같은 새 Reach 파일로 이동한다. guard 내 ShapeNow/ShapeCached 등은 그대로 두고 partial의 기존 private 접근을 쓴다.

AIBaseGuard(Core)의 BFS·피해·문 꼬리·보수 계산을 여기서 다시 재배치하지 않는다. 이미 순수 시험 경계가 있고 이번 작업은 게임 연결부의 책임을 우선한다. 후보 생성의 Brute/전투 일반화도 하지 않는다. 필요하면 이후 전투 기능이 소유할 작업이다.

### 3.4 M 검증

- `movecheck`를 M 전후 전체 Scripts 범위에 실행한다. 허용 실변경은 using/주석, AIBody와 AIPlayerSpikeMod의 `partial` 선언, 파일 경로 수정뿐이다. method/class body의 토큰 차이는 허용하지 않는다.
- 기존 source-wiring 시험은 이동한 symbol의 새 위치를 읽도록 수정한다. 확인된 예: `CombatS2Tests`의 AIExplore 경로는 해당 AITunnel 소스, AIBodySkills 참조는 새 소유 파일로 변경. 여러 파일에 걸친 같은 기존 검사는 필요한 파일만 합쳐 읽는다. 단언 내용과 기대값은 보존한다. 단언을 삭제하거나 전 Scripts 문자열 검색으로 느슨하게 바꾸지 않는다.
- 타입 중복/누락 없음, 모든 source 포함, 새 `.meta` GUID 중복 없음, C#8 compile-check를 확인한다. 기존 비Unity gates 전체를 한 번 통과시킨다.
- 최종 설치에서 mod manifest의 source 목록과 실제 복사 경로를 대조하고 게임 자체의 컴파일 성공을 확인한다.

## 4. Q — 문·물체 사실의 소유

### 4.1 문 조회: 결과 의미를 이름으로 구별

새 `S/AIDoorFacts.cs`(게임 연결 코드)에 다음을 둔다. 새 캐시·상태·시간 기준은 없다. 각 호출 때 기존과 같은 쿼리를 실행한다.

1. `IsDoorLike(EntityManager em, Entity e)` = `HasComponent<DoorCD> || HasComponent<GateCD>`, 왼쪽 단락 평가 보존.
2. `InWindowLastByTile(World server, int2 center, int radius)` = 기존 DoorsNear 본문. 반환은 기존 `Dictionary<int2,(Entity e,ObjectID id,int variation)>`. **DoorCD 쿼리 다음 GateCD 쿼리**, 각 쿼리 안 현재 열거 순서, 같은 칸은 마지막 할당이 이긴다. Chebyshev 경계 포함 그대로다.
3. `FirstAt(World server, int2 tile)` = 기존 DoorAt의 전체 ObjectDataCD+LocalTransform 쿼리와 첫 일치 선택. 반환 `DoorFact?`(struct: Entity, ObjectID, int2 Tile, int Variation, DoorKind Kind). 위치를 읽고 칸이 맞은 뒤 ObjectData와 전기 활성 component를 읽는 순서까지 유지한다. 없으면 null. `AIEdge.DoorAt`은 기존 반환 타입 DoorView와 인자를 유지하며 Fact를 View로 바꾸고 기존 `math.distance(me.xz,(float2)tile)`을 계산한다.
4. `OpenTilesExcept(World server, int2 center, int radius, Func<int2,bool> excluded)` = BuildGrid의 openDoors 생성 블록. 전체 ObjectDataCD+LocalTransform 쿼리, Door/Gate 필터, 범위→excluded→variation 순서 그대로. 한 칸에 열린 문이 하나라도 있으면 HashSet에 남긴다. BuildGrid에서 excluded는 기존 `S.doors.ContainsKey`다.

기존 private DoorsNear는 삭제하고 모든 기존 호출을 `AIDoorFacts.InWindowLastByTile`로 변경한다. AIEdge.Place의 LookMyDoors/WalkDoorTilesNow도 같은 함수로 바뀐다. DoorView는 공개 API 보존을 위해 AIEdge 안에 그대로 남긴다. AIBaseProbe의 문 여부 함수는 위 IsDoorLike로 위임하되 시험용 query/거리/출력은 유지한다.

**통일하지 않을 차이:** InWindow의 마지막 문, FirstAt의 첫 문, OpenTilesExcept의 하나라도 열림은 의도적으로 다른 query contract다. FirstAt을 InWindow 사전에서 읽는 식으로 바꾸지 않는다. 읽는 범위·시야·기억·문 소유는 AIEdge 소비자가 결정한다. 원본은 query 실패를 이 함수들 안에서 catch하지 않았으므로 새 catch/fallback을 넣지 않는다.

### 4.2 공통 물체·알려진 칸 사실

새 `S/AIWorldFacts.cs`는 다음의 **기존과 동일한** 작은 판독만 소유한다.

- `IsKnown(float3 me,int2 tile)` = `AIVision.CanSee(me,(float2)tile) || AIMemory.SeenTile(tile)`. 짧은 회로 평가와 호출 횟수 보존. 정확히 같은 식인 BuildGrid와 AIPlaceSpot.Grid 두 곳만 대체한다. GuardGridAround는 `inView = AIVision.CanSee(...)`를 한 번 계산해 `g.view`와 DoorCell에도 사용하므로 기존 `inView || AIMemory.SeenTile(tile)` 재사용식을 유지한다. 시야 조회를 두 번 만들지 않으며 cached overload도 추가하지 않는다. 다른 곳까지 기계적으로 확장하지 않는다.
- `IsExcludedPlacedEntity(EntityManager em,Entity e)` = EnemyCD→PlayerGhost→TileCD→PickUpItemCD→CritterCD의 기존 OR 순서.
- `IsPlacedObjectOrDoor(EntityManager em,Entity e,ObjectID id)` = 기존 PlacedAt/PlacedOn의 `PugDatabase.GetObjectInfo(id,0)` 후 `info!=null && (PlaceablePrefab || DoorCD || GateCD) && id!=TheCore` 순서. 함수 이름은 생물/타일 제외까지 포함한다고 주장하지 않는다. 호출자는 앞 함수로 별도 필터한다.

적용: AIEdge.ObjectsInWindow는 제외 predicate만 공유하고 variation fallback·collider·TheCore 허용·footprint 계산·종류 분류는 그대로 둔다. PlacedAt/PlacedOn은 두 predicate를 공유하되 조회(HealthCD 포함), 위치 필터와 선택 순서를 보존한다. PlacedAt은 첫 일치, PlacedOn은 칸별 첫 일치다. AICraft/AIObjects의 다른 제외 정책에 확장하지 않는다.

물/용암·스폰 받침·footprint·문 차단·설 자리 정책은 새 AIWorldFacts로 억지로 옮기지 않는다. 격자 소비자는 기존 Core 규칙을 그대로 호출한다. `KindOf`의 이름 기반 분류도 결과가 바뀌지 않도록 유지한다.

### 4.3 Q 검증

query 본문 이동과 call-site 치환은 독립 diff에서 선택 순서와 필터 순서 대조. 다른 3개 격자의 소유/low/occupied/noStand 의미가 그대로인지 체크한다. compile과 기존 door/guard/place core-tests 전체 및 gates를 실행한다. 새 순수 테스트로 게임 API를 흉내 내지 않는다. 실게임에서는 자기 문/남의 문, 문 통과→닫기, 시야 밖 기억→다시 관측, 물가 다리와 기지 주변 놓기를 확인한다. 겹친 엔터티의 재현이 어려우면 query 순서 보존은 코드 대조 근거로 구별해서 남긴다.

## 5. R — 첫 경로 탐색과 보행 후보 조립

### 5.1 순수 첫 성공 선택

새 `Core/AIApproachSearch.cs`에 좁은 generic 알고리즘을 둔다(게임 타입 없이 기존 절차를 시험하기 위한 generic이며 실행 정책을 등록하는 틀이 아니다).

```csharp
public delegate TPlan TryApproach<TTile,TPlan>(TTile tile, out string why) where TPlan:class;
public static TPlan FirstOfThree<TTile,TPlan>(IList<TTile> stands,
    TryApproach<TTile,TPlan> attempt, out TTile stand, out string why) where TPlan:class
```

처리: `plan=null; stand=default; why="설 수 있는 아는 칸이 없음";` → 인덱스 0부터 `i<stands.Count && i<3 && plan==null` 동안 attempt를 한 번 호출 → `stand=stands[i]` → 루프 끝에 plan 반환. 실패하면 마지막 시도의 why와 stand를 그대로 돌려준다. 빈 목록은 기본 stand와 최초 문장, null plan. 입력 null을 새로 허용하거나 정렬/중복 제거하지 않는다. attempt 예외는 그대로 전파된다.

새 `S/AIApproach.cs`:
- `FirstCached(World,Entity,IList<int2>,out int2,out string)`는 위 helper의 attempt로 `AIBrain.PlanC(server,player,t,out why)`(detour 기본 false)를 쓴다.
- `FirstImmediate(...)`는 `AITunnel.Plan(server,player,t,out why)`를 쓴다. 밭용이다.
- `WalkCandidate(int2 stand,string what,bool progress,string description)`는 기존 `new AICandidate { kind="dig",key=$"dig:{stand.x},{stand.y}",tile=stand,digWall=false,digWhat=what,progress=progress,desc=description }`만 만든다. 단계 2가 후보 필드 이름을 바꿨다면 **같은 의미의 확정 필드**를 사용한다. 그 외 필드 기본값을 바꾸지 않는다.

### 5.2 적용점과 남기는 정책

| 호출부(기준 앵커) | 첫 경로 helper | 반드시 호출부에 남길 것 |
|---|---|---|
| AIBase.Candidates의 기억 상자(497 부근) | FirstCached | stands 계산, plan 없음 로그, curKey 일치 시 n 증가/break, 중복/목표 분기, 상자별 문장 |
| AIBase.TakeOrGo(544 부근) | FirstCached | 현재 보이는 상자 take 분기, curKey와 res 중복 검사, 진행 값, 원래 문장 |
| AICook.Candidates의 기억한 솥(464 부근) | FirstCached | 솥 열거 순서, curKey면 break, 성공 뒤 break, 로그/문장 |
| AIBrain.Prep의 기억 제작대(88 부근) | FirstCached | 재료 검사와 missing 등록 순서, 충분할 때만 후보, prep notes/문장 |
| AIFarm.GoOption(690 부근) | FirstImmediate | 설 칸 생성/정렬/점유 사본, 직접 Plan 호출, curKey 검사와 실패 문장 |

위 다섯 루프만 First helper로 바꾼다. 실제 초기 why 문장이 위 표와 다르면 구현을 멈추고 명세 작성자에게 알린다. 후보는 WalkCandidate로 조립하되 호출부가 원래 문장을 계산해 넘긴다. PlanC 시간 계산과 카운터는 이동시키지 않는다.

추가로 AIBase의 home/store 귀환, AICook의 기지에 솥 놓으러 이동, AIPlaceGoal.Go는 후보의 공통 필드만 WalkCandidate로 만든다. 이 세 경로의 **경로 계산·current key 검사·정책·상태**는 그대로 둔다. 특히 AIPlaceGoal.Go는 각 stand에서 curKey/digedge를 먼저 검사해 즉시 return하며 detour=true로 탐색한다. 성공 뒤 RouteFact를 쓰고 digEdgeOk/DoorsOn/DetourOption을 붙이는 순서를 유지한다. Home도 detour=true와 alt 후보 current key 검사를 유지한다. 이 경로를 FirstCached로 바꾸지 않는다.

`WalkCandidate` 뒤 호출부가 필요한 `digEdgeOk`를 기존 값으로 채우는 것은 허용한다. 설명을 새 공통 template로 바꾸거나 평균 시간 계산을 다시 정하지 않는다. 실패 로그와 반환값은 기존 그대로다.

### 5.3 R 검증

Core test는 빈 목록, 첫 성공, 두 번 실패 후 세 번째 성공, 첫 세 개 실패 후 네 번째는 호출 안 함, 마지막 why/stand, 예외 전파를 **호출 기록과 반환 결과**로 검사한다. 이 시험은 후보 정책을 복제하지 않고 이동 과정에서 쉽게 바뀌는 시도 수/순서 계약을 지킨다.

기존 source/fixture 테스트와 gates 전체를 통과한다. 실제 실행 전후 각 기능의 후보 key·순서·문구 및 PERF의 plan/2pass/cached 호출 수를 대조한다. 실게임 대표 놓기 경로 외에 기억한 제작대(또는 상자/솥) 재접근 하나와 밭의 직접 경로 하나를 확인한다. 모든 이동에 detour를 켠 결과를 '최적화'로 수용하지 않는다.

## 6. O — 판단 준비 인자와 위협 기록

### 6.1 빌드별 인자를 캐릭터 지속 상태에서 제거

현 `_buildPrefetch`/`_buildBreach`는 Send가 설정하고 CandBuild만 읽는다. BuildCandidates의 유일한 외부 호출은 Send이며 재귀는 도구 준비를 위한 Assemble 한 곳이다. 다음처럼 명시 전달한다.

- `BuildCandidates(World,Entity,AIObservation,bool prefetch,bool breachCall)`와 `CandBuild` 생성자에 두 인자를 추가한다.
- CandBuild에 `readonly bool prefetch, breachCall`을 저장한다. 기존 `_buildPrefetch` 읽기 7곳은 `prefetch`, 조립기 `breachCall`은 같은 이름의 인자를 읽는다.
- Send는 같은 시점의 prefetch와 단계 2의 확정된 부서짐 계기 bool을 넘긴다. `AIEdge.BreachPending`을 읽는 시점/단락 평가를 앞당기지 않는다.
- 도구 개선 재귀는 현재 CandBuild의 두 인자를 그대로 다음 BuildCandidates로 넘긴다. `_rebuildingForTool`의 try/finally는 그대로다.
- AsmIn은 마지막 bool 인자로 prefetch를 받아 기존 로그를 쓴다. 준비 플래그 때문에 새 로그 형식을 만들지 않는다.
- Brain.State의 두 필드와 forwarding property를 삭제한다. Send의 finally는 `_building=null`만 유지한다. 사용처 전수가 위 범위를 넘으면 구현을 멈추고 HEAD에 보고한다.
- `_ctx`, `[ThreadStatic] _building`, BC, 경로 카운터·준비 결과 문자열은 이번에 per-character snapshot으로 바꾸지 않는다. 이는 후보 생성 뒤 SEND/BuildJson이 읽는 기존 생명주기와 연결되어 있으므로 별도 설계 범위다.

이 변경은 async request payload를 새 공유 상태로 참조하게 하지 않는다. 후보 목록·goal_rev·host order·stale 검사는 현행 그대로다.

### 6.2 위협 평가와 마지막 판정 기록 분리

HEAD가 2026-11-06 이 방향을 승인했다: 새 판단 snapshot은 만들지 않고 평가/기록/조회 책임을 명시하며 이전 시점과 범위 밖·size clear 의미를 유지한다.

새 `Core/AIThreatHistory.cs`:

```csharp
public sealed class AIThreatHistory<T>
{
    private readonly HashSet<T> cannotCome = new HashSet<T>();
    public void Record(T enemy, bool isThreat) {
        if (cannotCome.Count > 64) cannotCome.Clear();
        if (isThreat) cannotCome.Remove(enemy); else cannotCome.Add(enemy);
    }
    public bool LastAssessedCannotCome(T enemy) => cannotCome.Contains(enemy);
}
```

다른 API·타이머·world reset·Count 노출은 만들지 않는다. AIEdge.State의 기존 `HashSet<Entity> cantCome`을 `readonly AIThreatHistory<Entity> threatHistory` 하나로 대체한다. 기존에는 ClearMemory/Import에서 이 set을 비우지 않으므로 새로 비우지 않는다. 캐릭터 State의 수명 그대로다.

`AIEdge.Reach.cs`에서:
- `AssessThreatWithinRange(...) -> bool?`: 기존 `dist>range`면 null. 그렇지 않으면 기존 순서대로 EnemyTile→ReachOf→HurtRecently→AIBaseGuard.IsThreat를 호출하고 bool 반환. 이 함수는 **위협 기록에 쓰지 않는다**. ReachNow가 기존 reach/breach cache를 다루므로 전체를 순수 함수라고 부르지 않는다.
- `ObserveThreat(...) -> bool`: Assess가 null이면 false를 반환하고 history는 건드리지 않는다. 값이 있으면 `S.threatHistory.Record(e,value)` 한 번 후 value 반환.
- `ObserveAnyThreat(...)`: 기존 AnyThreat와 같은 전체 foreach. 처음 true에서 멈추지 않는다. 각 항목 ObserveThreat를 호출하고 any 누적.
- `LastAssessedCannotCome(Entity)`는 해당 history 조회.
- 기존 공개 `Threat`, `AnyThreat`, `CannotCome`는 소스 호환을 위해 각각 새 이름으로 얇게 위임한다. 내부 소비자는 새 이름을 사용한다.

호출 위치는 그대로 치환한다: Tick의 AnyThreat, range 적 Exists(단락 평가 유지), self-defence의 Find(단락 평가 유지), CandBuild.CombatOptions, GuardCommand reach. CloseFight와 SEND 적 목록은 LastAssessedCannotCome를 읽는다. 새 사전 계산·refresh·목록 정렬은 하지 않는다. 거리만 보는 Brute/near6/통계는 변경하지 않는다.

이 결과는 숨은 쓰기를 이름과 소유자로 드러내지만 **모든 소비자가 같은 시점의 새 위협 목록을 읽도록 보장하는 기능 변경은 아니다**. 그 변경은 기존의 범위 밖 이전 값/짧은 회로 상태를 바꾸므로 후속 제품 판단이다. 현재 사용자가 승인한 동작 불변 범위를 넘어가지 않는다.

### 6.3 O 검증

- ThreatHistory는 기록 안 된 적 false, false 기록 후 true, true 기록 후 제거, 인스턴스별 격리, 65개 유지 후 다음 Record 시 clear(정확히 >64), 동일 key 반복을 검사한다. Entity 흉내 대신 정수 key로 순수 계약을 검사한다.
- source 대조는 dist>range에서 Record 미호출, AnyThreat 전체 열거, Exists/Find 단락 유지, ClearMemory/Import에서 새 reset 미추가를 확인한다.
- 후보 replay에서 일반/미리 묻기/도구 재귀의 플래그와 후보 순서·문장이 같아야 한다. 기존 CombatS2·ContinueWarning·RunGate·assembler 시험 기대값은 그대로다.
- 두 캐릭터 교대 호출, 목표 교체/늦은 응답 폐기, 전투 진입/이탈, 문 뒤 적과 닿는 적이 공존하는 실게임 장면에서 동일한 정책을 확인한다. 상태 판단이 바뀌면 '더 정확함'으로 수용하지 말고 회귀로 보고한다.

## 7. 단계 1~2와의 교차 지점

- AIGoals/AIPlanner/goal contract는 이 명세에서 수정하지 않는다. 이동 이후 호출 위치만 새 파일로 따라간다.
- 후보 필드 이름·edge 역할·부서짐 계기 bool은 단계 2 결과가 정본이다. R의 WalkCandidate와 O의 breachCall은 그 정본을 사용한다. 명세 작성자와 확인한 R2b 계약은 `_needReason` 설정 때 `_needBreach=false`, Tick NewBreach가 값을 반환한 뒤 `_needBreach=true`, Send의 마지막 `breach=false` 인자에 정상 호출만 `_needBreach` 전달이다. O에서 로컬로 계산하는 식은 `breach || (AIEdge.PlanActive && AIEdge.BreachPending)`이며 기존 pending 소비 시점은 옮기지 않는다. `_needBreach`는 호출까지 보존하는 상태라 O에서 없애는 대상이 아니다. 없애는 것은 빌드 동안만 쓰는 `_buildBreach`다.
- `AIBrain.Loop.Send`, `AIBrain.Candidates.CandBuild`, `AIEdge.Guard.OpensEdge`는 충돌 가능 위치다. stage 2 코드를 옛 a07166f 본문으로 덮지 않는다. 변경 전 read/diff로 확정하고 결과 보고에 대응을 적는다.
- M은 단계 1~2의 완성된 타입 본문을 옮긴다. 입력은 a07166f의 파일 사본이 아니라 **구현 직전 브랜치 HEAD**다.

## 8. 실제 실행 검증과 인계

전체 대표 경로: 호스트의 제작/놓기 지시 → 대상 캐릭터 scope와 상위 목표 수용 → 후보 준비/선택 → 자기 문 통과 → 재료·목적지 접근 → 놓기/제작 → 결과·기억 → 다음 상위 판단.

| 변경 책임 | 관측할 기존 동작 | 필요한 확인 |
|---|---|---|
| 명령/모드 파일 분리 | 올바른 캐릭터가 명령을 받고 기존 순서로 처리 | 단일 지정과 all stop, 자연어 대상 귀속 로그 |
| input/patch/Body | 입력이 한 소유자에서 나가고 취소 후 미전송 입력 폐기 | 문 통과·닫기, 실행 중 stop/교체, nudge/use 로그 |
| Memory/CharacterStore/WorldSettings | 같은 파일에 저장하고 재입장 복원 | 시험 캐릭터·시험 월드에서 놓은 문/기억/가방/참여 상태 재입장 대조 |
| 공간 query | 첫/마지막/any 의미와 시야 밖 기억 유지 | 자체/타인 문, 현재·시야 밖·다시 보이는 문, 다리/놓기 회귀 |
| 이동 helper/Tunnel | 탐색 순서·캐시 정책과 후보·취소 동일 | 기억 시설 재접근, 밭 직접 탐색, 장소 detour와 둘레 재계산 중단 |
| 판단 인자·ThreatHistory | 미리 묻기/재귀/캐릭터 격리·범위 밖 기록 유지 | 기존 replay+문 뒤 적/보이는 적, 두 캐릭터 교대, 늦은 답 폐기 |

실게임 일정·설치·백업·시험 월드 삭제는 부모가 관리하는 environment/live 명세를 따른다. 원본 사용자 월드에서 시험하지 않는다. 설치 전에 게임/Unity가 닫혔는지 확인하며 둘을 겹쳐 실행하지 않는다. 최종 설치본 파일 목록·hash와 커밋을 연결하고, 게임 컴파일 성공 및 실제 흐름 결과를 별도로 남긴다.

각 단위의 최소 정적 관문: compile-check + 기존 gates. Unity compile과 실제 실행은 부모가 묶음 상태와 일정에 맞춰 순차 승인한 환경에서 한다. 실패 원인 없이 동일 검사를 계속 반복하지 않는다. 기존 로그 shape/replay를 우선 사용하고 새 테스트 도구를 여러 벌 만들지 않는다.

함수·파일 이동은 개발 지식그래프 `tools/config.json`과 source 앵커에 반영해야 한다. 제품 그래프는 최종 커밋 snapshot 재추출 절차가 별개다. 그래프 결과물을 이 코더가 임의 수정하지 않고 부모의 그래프 담당으로 이동 manifest(원본 파일/형식→새 파일/형식)와 최종 커밋을 넘긴다.

## 9. 중단하여 명세 작성자/HEAD로 돌릴 조건

- 단계 2와 실제 후보/계기 구조가 달라 단순 대응으로 옮길 수 없음.
- 같은 것처럼 보인 조회·루프가 이 문서와 달리 시점·순서·예외 차이를 가짐.
- method body를 그대로 옮겼는데 공개 타입/Unity 로딩/컴파일이 달라짐.
- 기존 runtime 동작/로그·후보·저장 결과가 달라짐. 더 좋은 결과처럼 보여도 임의 수용하지 않는다.
- 상태/캐시/저장 형식이나 정책을 바꿔야만 공통화할 수 있음.

보고에는 바뀐 책임과 실제 검증, 보존된 의미 차이, 이동 manifest, 아직 실게임으로 보지 못한 장면을 구별한다. HEAD가 결과를 확인하고 저장한다.
