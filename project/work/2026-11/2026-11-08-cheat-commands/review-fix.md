# 시험용 지형·물체 명령 수정 재검수

- 대상: worktree `C:/Dev/CoreKeeperModSDK-cheat-commands`, 커밋 `8a9fab4`(diff `c0bd393..8a9fab4`, `Assets/AIPlayerSpike/Scripts/AITestWorldEdit.cs` 한 파일, +39 −20)
- 기준: [이전 검수](review.md)의 F1~F4, [구현](implementation.md)
- 코드·게임·설정은 바꾸지 않았다. 컴파일 확인만 했다(Unity는 실행하지 않음). 아래 행 번호는 `8a9fab4`의 `AITestWorldEdit.cs` 기준이다.

## 판정: 수용

F1~F4가 모두 해소됐다. 수정 때문에 생긴 새 결함은 없다. 아래 "남은 사항"은 모두 낮음 또는 참고 수준이고, 이번 커밋을 막을 정도는 아니다.

## F1~F4 확인

### F1 해소: 오버플로와 무한 루프

- `:52` `MaxCoord = 1000000`. `Coord`(`:77-92`)는 `~N` 덧셈을 `long`으로 계산하고(`:84`), 절대 좌표와 상대 좌표 모두 ±1,000,000을 넘으면 거부한다(`:89`). 범위를 벗어나면 `Pos`가 false를 돌려주므로 사용법 문구로 응답한다.
- `AreaTiles`(`:95`)는 `long`으로 곱한다. 좌표가 ±1e6 안이므로 한 변은 최대 2,000,001이고, 넓이는 약 4e12로 `long` 범위 안이다. 상한 검사(`:137-138`, `:226-227`)는 `long`끼리 비교하고, 통과한 뒤에만 `(int)`로 바꾼다(최대 4096이라 안전).
- 반복 변수는 `hi.x ≤ 1e6`이므로 `x++`, `z++`가 넘치지 않는다. 이전 반례인 `0 0 50000 50000`(2,500,100,001칸), `0 0 65535 65535`(4,294,967,296칸)은 이제 상한에 걸려 "범위가 너무 큼"으로 응답한다. `int.MaxValue` 좌표는 `Coord`에서 거부된다.
- 단일 명령(`PlaceTile`, `RemoveTile`, `Spawn`)도 같은 `Pos`를 거치므로 좌표 상한이 똑같이 적용된다. 실제 월드 크기에 비해 ±1e6은 넉넉해서 정상 사용을 막지 않는다.

### F2 해소: spawn 아이템이 땅에 남음

- `:298` `EntityUtility.DropNewEntity(server, item, at, bank, Entity.Null)`. 디컴파일한 `Pug.Other/EntityUtility.cs`의 `DropNewEntity`는 `pullTowardsPlayerEntity != default(Entity)`일 때만 `PickUpItemCD{ForcePickUp}`를 넣는다. 그래서 `Entity.Null`(= `default(Entity)`)이면 끌어당김이 생기지 않는다.
- `Spawn`의 `player` 매개변수를 없앴다(`:67`, `:275`). 그래서 AI 엔티티가 실수로 다시 들어갈 경로도 없다. `Command`(`:54-72`)에서는 `player`를 `~` 기준 타일 계산(`:59`)에만 쓴다.
- 일반 자동 줍기는 그대로 동작한다. `PickUpItemSystem.cs:150`의 `pickupDistance = 1.4 * (1 + 보너스)` 안에 있는 플레이어나 AI는 떨군 아이템을 주울 수 있다. live-validation의 "AI 3칸 옆" 경우는 보너스가 없다면 이 거리 밖이므로 땅에 남아야 한다. 이것은 결함이 아니라 게임의 원래 동작이다. 실게임에서 다시 확인할 항목이다.
- 문서(`implementation.md:28`, `:44`)와 파일 머리 주석(`:9-10`)에 "끌어당김 없이 땅에 떨굼"이 반영됐다.

### F3 해소: 미정의 타일셋에서 NRE·무응답

- `TileOf`(`:151-152`)에 `Enum.IsDefined(typeof(Tileset), ts)`와 `Enum.IsDefined(typeof(TileType), tt)`가 추가됐다. 그래서 `/ai placetile 999 wall 0 0`은 `<Tileset> <TileType>` 해석에 실패하고, ObjectID 해석으로 넘어간다. 거기서도 `ObjectOf`가 "9" + "99"... 같은 이름을 맞추지 못하면 "모름" 문구로 응답한다(아래 참고 R3).
- `CheckTile`(`:166-175`)에서 `TilesetDataBlock.Get`이 `null`이면 "타일셋 N 없음"을 돌려준다. 디컴파일 `TilesetDataBlock.cs:198-205`를 보면 사전에 없는 번호는 `null`이다. `layerDefinition.Get()`은 `DataBlockRef.cs:137-141`에서 `TryGet` 결과를 돌려줄 뿐 예외를 던지지 않으므로, null 검사(`:171`)로 충분하다. enum에는 정의됐지만 데이터가 없는 타일셋, 또는 ObjectID 경로에서 TileCD가 가리키는 타일셋이 없는 경우도 이 검사에 걸린다.

### F4 해소: 문서화

- ObjectID 경로의 `CheckTile`은 그대로 두었다(`:123`, `:134`). `implementation.md:36`의 "원본과 달라진 점"에 차이(원본은 검사하지 않음, 원본보다 좁게 거부될 수 있음)와 실게임에서 거부된 사례가 없었다는 점이 적혀 있다. 코드 주석(`:164-165`)에도 같은 내용이 있다.

## 새 결함 여부

- 동작 변화는 좌표 상한, 상한 검사의 자료형, enum 정의 검사, null 검사, pull 대상 제거뿐이다. 기존 정상 입력(정의된 이름이나 숫자, ±1e6 안 좌표, 4096칸 이하 범위)의 결과는 바뀌지 않는다.
- 사용 중인 정의 숫자도 계속 받는다(`IsDefined`는 정의된 숫자를 통과시킨다). 문서 `:22`와도 맞다.
- 게이트(`testcmds.on`)와 다른 파일은 건드리지 않았다.

## 남은 사항(낮음·참고)

- R1 (낮음, 원본과 같음) `:298-299`: `DropNewEntity`의 반환값을 보지 않는다. 그래서 내부 `CreateEntity`가 `Entity.Null`을 돌려줘도 "아이템 떨굼"이라고 응답한다. 프리팹 경로(`:301-302`)는 실패를 보고하므로 비대칭이다. 시험 중에 헷갈릴 수 있으니 다음에 손볼 때 같이 고치면 된다.
- R2 (참고) `:131`, `:222`: 범위 명령 두 개의 사용법 문구에만 "±1000000 안" 표기가 없다. 동작에는 영향이 없다.
- R3 (참고) `:157`: `IsDefined`로 거른 뒤 ObjectID 경로로 넘어가면, 예를 들어 `999 wall`은 `ObjectOf`가 "999wall"로 이어 붙여 해석하다 실패한다. 결과는 "타일셋·타일 종류나 ObjectID를 모름: 999 wall"이다. "타일셋 999 없음"보다 덜 구체적이지만 응답은 나오므로 F3의 무응답 문제는 사라졌다.

## 컴파일 재확인

- `dotnet build Tools/compile-check -c Release --no-incremental`(HEAD `8a9fab4`, `Assets/AIPlayerSpike/Scripts`에 커밋하지 않은 변경 없음): 경고 0, 오류 0. 산출물 `Tools/compile-check/bin/Release/netstandard2.1/CompileCheck.dll`이 2026-11-08 15:55:04에 새로 만들어졌다.
- Unity CompileCheck는 다시 돌리지 않았다.

## 실게임에서 다시 볼 항목

- 프리팹 없는 물체를 AI나 호스트에서 1.4칸보다 멀리 spawn하면 땅에 남는지(이전 실게임의 "AI 3칸 옆 나무" 경우).
- 범위 상한을 넘는 입력과 ±1e6을 넘는 좌표가 채팅에 거부 문구로 돌아오는지.
