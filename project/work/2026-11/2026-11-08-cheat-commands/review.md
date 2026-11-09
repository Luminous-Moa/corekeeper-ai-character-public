# 시험용 지형·물체 명령 독립 검수

- 대상: worktree `C:/Dev/CoreKeeperModSDK-cheat-commands`, 브랜치 `ai-cheat-commands-20261108`, 커밋 `c0bd393`(부모 `20b1ace`)
- 기준: [합의](agreement.md). 구현자 설명은 [구현](implementation.md)이며, 아래에서 그 주장을 하나씩 확인했다.
- 원본 비교: `gh api`로 limoka/CoreKeeperMods `3454ffd`의 `ChatCommands/Scripts/Commands/{PlaceTileCommand,PlaceTileAreaCommand,RemoveTileCommand,RemoveTileAreaCommand,SpawnCommandHandler}.cs`와 `LICENSE`, CoreKeeperMods/CoreLib의 `Package/CoreLib/Command/Scripts/Util/CommandUtil.cs`를 받았다. 게임 API 동작은 `C:\Dev\_ref\corekeeper-decomp`에서 확인했다.
- 코드·게임·설정은 바꾸지 않았다. 컴파일 확인만 다시 했다(Unity는 실행하지 않음).

## 판정: 수정 필요

중간 결함이 둘 있다. 범위 상한이 정수 오버플로로 뚫려 호스트가 멈출 수 있고(F1), `spawn`의 아이템 경로가 설명과 달리 아이템을 땅에 두지 않고 AI 가방으로 끌어간다(F2). 두 결함 모두 한두 줄로 고칠 수 있다. 나머지는 낮음 또는 참고 사항이다. 합의 조건(스위치 게이트, 기존 명령 불변, CoreLib 미복사, MIT 표시와 전문)은 모두 충족한다.

## 결함

### F1 (중간) 범위 칸 수 계산이 오버플로해 4096칸 상한이 뚫림 → 호스트 정지·메모리 고갈

- 위치: `Assets/AIPlayerSpike/Scripts/AITestWorldEdit.cs:127-128`, `:210-211`(칸 수 계산과 상한 검사), `:130-132`, `:213-215`(반복문), `:81`(`origin + d`)
- 근거: `int n = (hi.x - lo.x + 1) * (hi.y - lo.y + 1)`이 `int`로 계산된다. 예를 들어 `/ai placetilearea dirt wall 0 0 50000 50000`이면 50001² = 2,500,100,001이 넘쳐서 −1,794,867,295가 된다. 음수라서 `n > MaxAreaTiles` 검사를 통과하고, 반복문은 약 25억 번 `PlaceOne`을 불러 TileUpdateBuffer에 항목을 계속 넣는다. `0 0 65535 65535`이면 n이 0이 되어 마찬가지로 통과한다. 자리 수 오타만으로 생길 수 있는 입력이다. `hi.x == int.MaxValue`이면 `x++`가 넘쳐 반복이 끝나지 않는다(n=1이라 검사도 통과). 구현이 상한을 둔 이유(":48 a typo could freeze the host")가 바로 이 경우에 지켜지지 않는다. 명령은 메인 스레드의 채팅 패치 안에서 동기로 실행되므로(`AIPlayerSpikePatches.cs:71-76` → `AIPlayerSpikeMod.Chat.cs:88`) try/catch로도 막지 못한다.
- 수정 제안: 칸 수를 `long`으로 계산하거나 가로·세로를 각각 먼저 검사한다(예: `long n = (long)(hi.x - lo.x) + 1` …, 또는 `hi.x - lo.x >= MaxAreaTiles`면 거부). 좌표 절대값에도 상한을 두면(예: ±1,000,000) `:81`의 덧셈 오버플로와 `int.MaxValue` 반복 문제가 함께 사라진다.

### F2 (중간) `spawn`의 아이템 경로가 아이템을 AI에게 강제로 줍게 함(원본은 호스트, 설명은 "땅에 떨굼")

- 위치: `AITestWorldEdit.cs:279` `EntityUtility.DropNewEntity(server, item, at, bank, player)`. 여기서 `player`는 명령을 받은 AI의 서버 엔티티다.
- 근거: 디컴파일한 `Pug.Other/EntityUtility.cs:574-595`를 보면, 다섯째 인자 `pullTowardsPlayerEntity`가 있을 때 `PickUpItemCD { state = ForcePickUp, targetEntity = 그 엔티티 }`를 넣는다. `PickUpItemSystem.cs:529`와 `:605`에서 ForcePickUp은 거리 검사를 건너뛰고, 시야가 막히지 않았고 가방에 자리가 있으면 그 대상에게 끌려간다. 그래서 프리팹이 없는 물체를 생성하면 (x,z)에 남지 않고 AI 가방으로 들어간다. 원본은 `player`를 명령을 보낸 호스트로 넘겼다(`SpawnCommandHandler.cs:18`, `:108`). 그래서 원본에서는 사실상 "호스트에게 지급"이었고, 이번 이식은 대상이 AI로 바뀌었다. implementation.md의 "땅에 아이템으로 떨군다"와도 다르다. 줍기·채집 시험장을 만들 때 AI 가방이 예고 없이 바뀌어 시험 조건이 오염된다.
- 수정 제안: 땅에 두는 것이 의도라면 `Entity.Null`을 넘긴다(기본값이므로 인자를 빼도 된다). 원본처럼 지급하는 쪽이 의도라면 그렇다고 문서와 응답 문구에 밝힌다. AI 지급은 이미 `/ai give`가 있으므로 앞쪽(`Entity.Null`)을 권한다.

### F3 (낮음) 알 수 없는 타일셋 번호에서 NullReference가 나고 채팅에 응답이 없음

- 위치: `AITestWorldEdit.cs:140`(Tileset·TileType에 `IsDefined` 검사 없음), `:155-157`
- 근거: `Enum.TryParse`는 숫자 문자열을 정의되지 않은 값이어도 받는다(`/ai placetile 999 wall 0 0`). `TilesetDataBlock.Get`은 모르는 번호에 `null`을 돌려주므로(`Pug.Other/PugTilemap/TilesetDataBlock.cs:198-205`) `data.layerDefinition`에서 NRE가 난다. 예외는 `AIPlayerSpikeMod.Chat.cs:88-89`에서 잡혀 로그에만 남고, 사용자는 아무 응답도 받지 못한다. 원본도 같은 경로가 있었지만 그쪽은 CoreLib이 예외를 처리했다.
- 수정 제안: `data == null`이나 `layerDefinition.Get() == null`이면 "타일셋 없음"을 돌려준다. `:140`에도 `Enum.IsDefined`를 건다(`:223`처럼).

### F4 (낮음) ObjectID 경로에도 타일셋 정의 검사를 걸어 원본과 다르고, 이 차이가 문서에 없음

- 위치: `AITestWorldEdit.cs:112-114`, `:123-125`
- 근거: 원본 `PlaceTileCommand.PlaceObjectID`(`:150-158`)는 아이템의 TileCD를 검사 없이 바로 놓는다. `TryPlaceTile`의 QuadGenerator 검사는 `<Tileset> <TileType>` 경로에서만 했다. 이번 구현은 두 경로 모두 `CheckTile`을 거친다. 타일 아이템이라면 보통 정의가 있으므로 실제 차이는 작을 것으로 보이지만, 어떤 타일 아이템이 거부되면 원본보다 좁아진다. implementation.md의 "원본과 달라진 점 셋"에는 이 차이가 빠져 있다.
- 수정 제안: 원본대로 ObjectID 경로에서는 검사를 빼거나, 차이를 문서에 적는다.

### F5 (낮음, 문서화됨) 여러 AI에게 보내면 대상마다 실행됨

- 위치: `AIPlayerSpikeMod.Chat.cs:83-90`(대상마다 `Dispatch`), `AITestWorldEdit.cs:51`
- 근거: `/ai all spawn X 10 10`은 들어와 있는 멤버 수만큼 물체를 만든다. `~` 좌표도 멤버마다 기준이 달라진다. implementation.md가 이를 적어 두었고, 기존 `/ai arena spawn`도 같은 방식이라 기존 체계와는 맞는다. 다만 절대 좌표 명령에서는 중복이 생길 뿐 이점이 없다. `verbose`처럼 `order > 0`이면 건너뛰게 하려 해도 지금은 `HandleCommand`에 `order`가 넘어오지 않는다.
- 수정 제안: 이번에는 고치지 않아도 된다. 고치려면 `spawn`과 `placetile*`에서 `all`을 거부하거나 한 번만 실행하게 한다.

### 참고 (결함 아님)

- AI가 월드에 들어와 걷는 상태가 아니면 절대 좌표 명령도 쓸 수 없다(`AIPlayerSpikeMod.Chat.cs:194-203`의 `IsSkillCommand` 경로). `/ai` 체계에 넣은 이상 자연스러운 결과이며 합의와도 어긋나지 않는다.
- `RemoveTileArea`의 인자 수 검사는 원본(`parameters.Length < 4`)보다 이번 구현(`a.Count < 5`)이 맞다.
- 범위 명령은 칸마다 `CreateEntityQuery`를 만들고 `List`를 할당한다(`:172`, `:186-191`, `:235`). 4096칸이면 몇 ms 수준으로 보이며 정지 위험은 없다. 쿼리를 반복문 밖으로 빼면 더 깔끔하다.
- tileset 2는 `Tileset.Obsidian`이다(`Pug.Base/PugTilemap/Tileset.cs`). 크리에이티브가 아닌 월드에서는 흑요석 벽을 놓을 수 없다. 원본과 같은 동작이며, implementation.md의 "이름 확인 안 함" 항목에 대한 답이다.

## 확인 항목별 결과

### 1. 원본 동작 보존과 서버 월드 API

- 서버 월드: `server`는 `Manager.ecs.ServerWorld`다(`AIPlayerSpikeMod.cs:287-296`). 원본의 `API.Server.World`와 같다. 새 코드는 클라이언트 월드를 쓰지 않는다.
- 타일 설치(`PlaceOne` :163-184): 원본 `PlaceTile`(`PlaceTileCommand.cs:74-136`)과 같다. 크리에이티브 검사와 코어 네 칸·tileset 2 거부, TileUpdateBuffer 싱글턴에 Add를 넣고, 벽이면 roofHole을, 바닥이면 pit·water를 Remove한다.
- 타일 제거(`RemoveOne` :233-252): 원본 `RemoveTile`(`RemoveTileCommand.cs:73-130`)과 같은 순서다. 대상 Remove, 같은 칸에서 그 타일을 필요로 하는 타일(`GetNeededTile`) Remove, ground·water면 pit Add. 원본의 TileAccessor 대신 `AITileReader.Layers`(SubMapRegistry와 SubMapLayerBuffer를 읽고, 읽지 못한 영역은 `TileAccessor.DefaultTile`)를 썼는데 같은 자료다. DynamicBuffer를 쥔 채 호출하는 것은 구조 변경이 아닌 읽기(`Exists`/`HasBuffer`/`GetBuffer(ro)`)뿐이므로 버퍼 무효화 문제는 없다.
- 엔티티 생성(`Spawn` :258-284): 원본 `SpawnID`와 같다. `graphicalRef.hasAddress`로 나누어 `EntityUtility.CreateEntity(World, pos, id, 1, bank, variation)`를 높이 0에서 부른다. 이 메서드는 `EntityManager.Instantiate`와 `LocalTransform`·`RandomCD`를 설정한다(`EntityUtility.cs:109-128`). 데이터베이스는 CoreLib `GetDatabase`가 아니라 모드에 이미 있는 관용구(`AIBase.cs:237`, `AICombat.cs:1004-1005` 등)로 읽는다. 원본보다 나아진 점은 `info == null` 방어와 생성 실패(`Entity.Null`) 보고다. 아이템 경로의 끌어당김 대상은 F2를 보라.

### 2. 좌표 규칙과 파싱 오류 경로

- 절대 좌표가 기본이고, `~N`은 `AIBody.TileOf(AIBody.Pos(server, player))`, 곧 명령을 받은 AI의 타일(`math.round`)에서 계산한다. `~`만 쓰면 AI의 타일이다. 합의와 문서대로다.
- 잘못된 숫자, 인자 부족, 모르는 이름은 사용법이나 사유 문구로 응답한다. 예외: F1의 오버플로와 F3의 미정의 타일셋.
- `spawn`의 선택 variation은 마지막 단어가 정수이고 그 앞 두 단어가 좌표로 읽힐 때만 variation으로 본다(:264). `Slime Blob 10 20` 같은 여러 단어 이름과 겹치지 않는다. 마지막이 `~3`이면 variation으로 보지 않는다(원본과 같다).

### 3. testcmds.on 게이트와 기존 명령

- 새 다섯 단어가 `AIBody.TestCommands`(`AIBody.cs:447`)에 들어 있다. `Dispatch` 첫머리(`AIPlayerSpikeMod.Chat.cs:138-143`)가 명령마다 `File.Exists(testcmds.on)`을 보고 거부한다. `arg`는 소문자로 바뀐 뒤 검사되고, `/ai all …`이면 대상마다 검사한다. 게이트를 우회하는 경로는 없다.
- 기존 분기: `HandleCommand`에는 case 추가만 있고(`AIBody.Commands.cs:355-362`), 이웃 case와 이어서 실행되는 부분도 없다. `IsSkillCommand`와 `TestCommands`에도 덧붙이기만 했다. 기존 단어 중 겹치는 것이 없다. `arena`의 하위 `spawn`(`AICombat.cs:945`)과 행동 키 `placetiles`(`AIActions.cs:46`)는 서로 다른 이름 공간이다. 바뀌는 행동은 새 다섯 단어에 대한 응답뿐이다(스위치가 꺼져 있으면 예전의 도움말 대신 시험용 거부 안내가 나온다).

### 4. CoreLib 복사 여부와 MIT 표시

- CoreLib `CommandUtil`과 비교했다. `Coord`/`Pos`는 "`~`면 기준값에 더한다"는 같은 착상이지만 구현이 다르다. CoreLib은 `int.Parse`와 try/catch에 `[1..]`을 쓰고 `~` 단독을 허용하지 않으며 끝 인덱스 기준이다. 이번 코드는 `TryParse`, `~` 단독 허용, 시작 인덱스 기준이다. `ObjectOf`는 `Enum.TryParse` 한 줄뿐이고 CoreLib의 friendlyNameDict 검색이 없다. `GetPlayerEntity`/`CommandOutput`/`AppendAtStart`에 해당하는 코드도 없다. 데이터베이스 조회는 모드의 기존 관용구다. 문장 수준의 복사는 없다고 판단한다.
- ChatCommands와는 구조가 매우 비슷하다. MIT 코드를 이식한 것이므로 문제가 없다.
- MIT 표시: `AITestWorldEdit.cs:16-36` 주석의 전문을 원본 `LICENSE`와 비교했다. 주석 기호를 걷어 내면 글자 하나까지 같다. ModBuilder가 `.cs` 원본을 배포판 `Scripts/`에 복사하므로(`Packages/dev.pugstorm.mod/SDK/Editor/ModBuilder.cs:323-400`) 표시도 배포판에 들어간다. 합의 조건을 충족한다.

### 5. 실게임 결함 위험

- 예외: 명령 전체가 `AIPlayerSpikeMod.Chat.cs:88-89`의 try/catch 안에 있어 게임이 죽지는 않는다(F3은 응답이 없는 문제).
- 무한 루프·정지: F1.
- 엔티티 매니저: 모든 호출이 `server.EntityManager`다. 쿼리는 `using`으로 해제되고 `NativeList`도 Dispose된다.
- 멀티 AI: F5. AI별 상태(`AIAgent.Cur`)를 쓰지 않으므로 범위가 꼬이지는 않는다.

## 컴파일 재확인

- `dotnet build Tools/compile-check -c Release --no-incremental`: 경고 0, 오류 0. 산출물 `Tools/compile-check/bin/Release/netstandard2.1/CompileCheck.dll`(2026-11-08 15:14:25, 새로 만들어짐)에 `AITestWorldEdit`가 들어 있다. csproj는 `Assets/AIPlayerSpike/Scripts/**/*.cs`를 LangVersion 8로 포함한다. bin/obj는 gitignore 대상이다.
- Unity CompileCheck는 다시 돌리지 않았다. 구현자의 [unity-compile.log](unity-compile.log)에서 `[HeadlessSdk] CompileCheck: ok`만 확인했다.

## 실게임에서만 판정할 수 있는 항목

- TileUpdateBuffer로 넣은 Add/Remove가 서버에 반영되고 호스트 화면과 AI thin client 양쪽에 동기화되는지. 읽지 않은 영역의 좌표에서는 어떻게 되는지.
- `spawn`으로 만든 제작대·상자·화로가 정상 설치물(상호작용, 저장 뒤 유지, 타일 점유)로 동작하는지, AI가 그것을 인식하는지. 이 경로는 원본도 `CreateEntity`를 높이 0에서 불렀을 뿐 일반 설치 경로를 거치지 않는다.
- F2를 고치기 전이라면, 프리팹 없는 아이템 생성이 실제로 AI 가방으로 끌려가는지(시야가 막혔거나 가방이 가득 찼을 때는 땅에 남을 수 있다).
- 놓거나 지운 지형을 AI의 길찾기·채집이 바로 반영하는지(AI 지형 기억은 그 칸을 다시 볼 때까지 옛 상태일 수 있음).
- F4: 쓰려는 타일 아이템(벽·바닥·광석 벽·레일 등)이 `CheckTile`에 거부되지 않는지.
- 4096칸 범위를 한 번에 넣을 때의 순간 부하.
