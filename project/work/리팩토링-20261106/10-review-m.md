# M 독립 코드 검수

2026-11-06. 고정 diff `f512924..fbb98b7`, 저장소 `C:/Dev/CoreKeeperModSDK-refactor-20261106`. Q 동시 구현과 분리하여 모든 제품 소스 입력은 git object에서 읽었다. 승인 `03-spec-spatial-ownership.md` §M과 `08-live-evidence-plan.md`, 공통 AGENTS/HEAD 검수 기준을 적용했다.

## 판정

**M 정적 독립 검수 통과. Must-fix 발견 없음.** 선언 이름·접근·상속·attribute·멤버 소속 및 본문을 유지한 책임별 파일 분리다. AIBody와 AIPlayerSpikeMod의 partial 추가는 승인 범위이며 새로운 실행/상태 소유자가 생기지 않았다. 기존 혼합 파일에서 각 책임의 위치가 드러나는 효과가 있다. 상태·공간 규칙의 실제 공통화는 이후 Q/R/O의 몫이므로 M만으로 그 단계까지 완료했다고 판단하지 않는다.

제품 소스·작업트리·브랜치 수정, 게임/Unity 실행은 하지 않았다. 결과 폴더에 독립 비교 도구와 이 보고서만 작성했다.

## 독립 확인 근거

작업자의 `09-move-manifest.md`, `m-field-order.txt`의 38형식 결과와 `m-token-check.txt`는 먼저 읽되 단독 근거로 사용하지 않았다.

별도 `review-m-check/ReviewM.csproj`를 만들어 Roslyn으로 **두 고정 커밋의 전체 Scripts .cs**를 읽기 전용 비교했다. 제품을 빌드하거나 현재 작업트리 소스를 읽지 않는다. 결과:

> Compared 281 fully qualified top-level/nested type identities; failures 0

확인 항목은 최상위/중첩 타입의 namespace·소유 타입을 포함한 이름 집합, 선언 종류/접근 modifier(partial만 제외), 타입 attribute와 base 목록, 각 소유 타입 안의 직접 멤버 선언/본문/attribute 전체 토큰, 비const 필드와 initializer property의 선언 순서, enum 전체 선언이다. 단순 전체 토큰 멀티셋과 달리 **다른 타입으로 잘못 옮긴 같은 본문**도 차이로 검출한다. 정적 생성자와 중첩 State 본문도 소유 타입의 멤버 비교에 포함된다. C#8 구문 오류도 없었다.

추가로 고정 소스 .cs/.meta 목록과 GUID를 직접 비교했다. `fbb98b7`의 79개 .cs 모두 meta가 있으며 신규 meta 23개는 Assets 범위에서 GUID가 각각 한 번만 나타났다. AIBodySkills.cs.meta→AIBody.cs.meta와 AIExplore.cs.meta→AIMemory.cs.meta의 내용은 완전히 같다. `git diff --check f512924..fbb98b7`도 통과했다.

## 타입·멤버 이동과 상태 소유

이하 `S/`는 `Assets/AIPlayerSpike/Scripts/`, 위치는 `fbb98b7` 기준이다.

| 이동 책임 | 직접 대조한 결과 |
|---|---|
| 몸 실행/명령 | AIBody는 `S/AIBody.cs`와 `S/AIBody.Commands.cs`의 같은 public static partial 타입이다. HandleCommand와 여섯 보조 진입점만 Commands로 옮겼고 State·TestCommands 배열은 본체에 남았다. Tick(:198), StartSkill(:328), Stop(:367), EndSkill(:408), UseBitFor(:175), NudgeFor(:165)는 본체의 단일 실행 소유를 유지한다. method 본문·AIAgent scope·호출 순서를 바꾸거나 명령 처리를 다음 틱으로 미루지 않았다. |
| 몸 파일의 독립 형식 | BodyInput/SkillStatus/AISkill, AIVision, AITileReader/AIWallData, SeenEnemy/AIObservation, NavStatus/AINavigator, Look/Inv/Goto/Mine/Attack/WalkTestSkill/AIMoveTrace는 원래의 최상위 타입으로 각각 옮겼다. 접근·상속·필드·기본값·기술 생성자와 실행 메서드는 같다. 타입을 새 클래스 안에 중첩하거나 namespace/assembly를 바꾸지 않았다. |
| 제작/놓기/부수기 | AICraft/CraftSkill은 원래 파일, AIPlaceSpot/PlaceSkill은 AIPlace.cs, RootSkill/BreakSkill은 AIBreakSkills.cs로 분리된다. 기존 AISlot.BreakSkill=9와 타입 소유는 유지되고 AIAgents diff는 주석 경로만 바뀌었다. 제작·놓기·부수기 정책 본문은 그대로다. |
| 기억/물체/경로 | AIMemory, AIDrops, AIObjects, TunnelPlan/AITunnel/TunnelSkill은 각각 목적 파일로 이동했다. 기억 조회의 Ensure·월드 경계·Save/Load/migration, 캐릭터 State 및 파일 경로 표현식은 소유 타입별 토큰이 동일하다. '조회'를 순수하게 바꾸거나 저장 시점을 이동하지 않았다. R2b의 typed WallFact 소비도 최신 기준 f512924 본문을 유지한다. |
| 모드/채팅 | AIPlayerSpikeMod는 기존 IMod 타입 그대로 partial로 선언된다. Chat 파일의 Dispatch/TryHandleMention/Reply/FlushReplies 등은 같은 private 경계를 공유한다. `_pendingReplies/:115`, `_renderChatText/:116`, `ChatPatched/:117`, `_mentionNames/:412`, `_repeatGate/:494`는 본체에 남았다. TurnOn/TurnOff/SetRoute/RequestServerDisconnect 역시 본체다. 새로운 채팅 큐나 캐릭터 상태 복사본은 없다. |
| 저장/patch 타입 | AIWorldSettings와 AICharacterStore, ThinClientDriverConstructor와 AIPlayerSpikePatches는 원래의 최상위 선언으로 이동했다. serializer 입력 형식·field 순서·scope·경로·기존 예외 경계는 같은 타입의 같은 본문이다. |
| 둘레 읽기/기술 | AIEdge.Reach.cs는 같은 AIEdge partial에 ReachRange부터 CannotCome까지를 둔다. DoorsNear·MemoryNow·StandTile·Threat 등 실제 대상 멤버가 포함되고 ShapeNow/ShapeCached와 guard 동작은 Guard에 남았다. AIEdge State는 본체에 남는다. EdgeFill/DoorPass/DoorCloseSkill과 PickupSkill은 독립 형식으로 이동했을 뿐 실행 소유/본문은 동일하다. |

초기화 순서가 의미를 가질 수 있는 선언을 별도로 봤다. AIBody의 TestCommands와 AIPlayerSpikeMod의 채팅 관련 static 필드는 파일 분리된 메서드와 함께 옮기지 않았다. AIEdge.Reach의 ReachRange는 const이며, `_tileBreak`는 기존 expression-bodied property이다. 이 둘을 옮기는 데 새 런타임 초기화 순서는 생기지 않는다. 중첩 State에 들어 있는 instance initializer도 독립 비교에서 순서 차이 0이었다.

using은 각 원본 집합에서 복사됐고, 새 파일들은 같은 Scripts/asmdef 범위에 있다. diff에는 조건부 컴파일 directive 추가/삭제가 없다. 파일명에 따른 별도 등록이나 새 어셈블리를 요구하는 변경은 발견하지 못했다.

## Harmony·소스 포함·meta

`S/AIPlayerSpikeMod.cs:498` InstallPatches 본문은 유지된다. ServerNetworkUpdate/UnloadWorlds/SpawnLambda/CharacterData/ChatSend의 hook은 여전히 `typeof(AIPlayerSpikePatches)`와 `nameof(...)`로 명시적으로 등록한다. 패치 클래스를 파일로 옮겼어도 타입/메서드 정체성과 호출 순서가 같다. 새로운 assembly scan이나 파일명 의존 등록으로 바꾸지 않았다. ThinClientDriverConstructor도 같은 인터페이스·타입을 사용한다.

AIPlayerSpikeMod는 같은 이름의 파일에 그대로 있고 IMod를 구현한다. 분리한 형식을 MonoBehaviour/ScriptableObject로 새로 바꾸거나 Unity 직렬화 객체의 타입 정체성을 변경한 흔적은 없다.

`Tools/compile-check/CompileCheck.csproj`는 기존 `Scripts/**/*.cs` 포함 방식과 LangVersion 8을 유지하므로 새 파일도 정적 compile 대상이다. `Assets/AIPlayerSpike/AIPlayerSpike.asmdef`는 바뀌지 않았다. 모드 asset도 `modPath: Assets\AIPlayerSpike`, `disableScripts:0`, 기존 설정 그대로다. 새 소스를 별도 어셈블리나 소스 제외 폴더로 보내지 않았다.

이는 소스 구성 검수이며 **최종 배포 manifest의 실제 목록·복사 hash 및 게임 자체 컴파일 성공은 확인하지 않았다.** 08 계획대로 설치 시점의 목록/hash와 Unity/game 결과를 별도로 연결해야 한다. 두 기존 GUID 유지와 새 meta의 유일성은 직접 확인했다.

## source-wiring 시험의 의미

시험 변경은 `Tools/core-tests/CombatS2Tests.cs` 두 read 경로뿐이다. 단언·기대값을 삭제하거나 바꾸지 않았다.

- F2의 AIExplore.cs read는 실제 `AIObjects.CritterOnly`와 CritterCD 필터가 옮겨간 AIObjects.cs를 읽는다. 걷기와 돌진 예측의 서로 다른 blocked 집합 및 Predict 호출 개수 단언은 그대로다. 전체 Scripts 검색으로 범위를 느슨하게 만들지 않았다.
- F1은 모드 frame 루프가 남은 AIPlayerSpikeMod.cs와 unload hook이 옮겨간 AIPlayerSpikePatches.cs만 합쳐 읽는다. `!AIChargeWatch.Frame`과 `AIChargeWatch.Reset`의 기존 책임 확인을 유지한다. unrelated Chat/Store까지 합쳐야만 단언이 통과하는 구조가 아니다. watcher와 decision loop의 별도 검사도 그대로다.
- 남은 옛 파일명 참조는 역사적 줄 번호/로그 근거 주석에서 발견되며 실행 read 경로의 유실 근거는 없었다.

## 목적과 검증 한계

혼합 책임이 있던 거대 파일에서 몸 입력, 관측, 탐색, 각 기술, 저장, patch를 별도 파일로 찾을 수 있게 되었다. 이미 독립 타입이던 경계는 그 타입을 그대로 꺼냈고, body/chat/reach는 같은 타입의 조율 역할을 유지하는 partial 이동이다. 공통 규칙을 복사하거나 새 중앙 허브를 만들지 않았다. 한편 partial 자체가 상태 결합을 해소하는 것은 아니므로 M을 '상태 소유 전면 개선'으로 보고하지 않는다. 이 절제가 승인된 M 범위와 맞는다.

작업자 전체 gate 실패0/core1048/protocol106은 보조 근거로만 기록한다. 현재 트리의 제품 compile/gates는 재실행하지 않았다. 독립적으로 실행한 것은 고정 git 소스의 Roslyn 구조 비교, meta 확인, diff --check다.

08 계획의 조건부 M 착수 근거와 최종 미확인 범위를 유지한다. 명령 대상/all stop, 문 입력·미전송 취소, 실제 저장→완전 종료→동일 월드 재입장의 문·기억·가방·참여 상태 대조가 정적 본문 동일성으로 자동 통과하지는 않는다. 최종 설치/게임 실행으로 확인하고 미관측 장면은 그대로 남겨야 한다.

## HEAD 반환

M 소스 수정 요청 없음. Q 고정 diff 검수를 이어갈 수 있다. `review-m-check`는 이 검수의 보조 산출물이며 제품·공식 시험 체계에 추가하지 않았다.
