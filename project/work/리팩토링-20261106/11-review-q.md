# Q 독립 코드 검수

2026-11-06. 고정 diff `fbb98b7..3c31536`, `C:/Dev/CoreKeeperModSDK-refactor-20261106`. 승인 `03-spec-spatial-ownership.md` §Q 및 공통 HEAD 검수 기준을 적용했다. R/O 동시 작업과 분리하여 제품 소스는 고정 git object에서만 읽었다.

## 판정

**Q 정적 독립 검수 통과. Must-fix 발견 없음.** 같은 사실 판독의 소유자를 AIDoorFacts/AIWorldFacts로 정했고, 기존 소비자별 query 선택 의미와 필터·조회 순서를 유지한다. 서로 다른 첫/마지막/any 의미를 하나의 결과로 강제 통일하지 않았다. 새 캐시·상태·catch/fallback은 없다.

변경된 10파일 전체 diff와 실제 호출부를 대조했다. 제품 소스/브랜치/작업트리를 수정하거나 게임·Unity를 실행하지 않았다.

## 문 사실 조회

이하 위치는 `3c31536` 기준, `S/`는 `Assets/AIPlayerSpike/Scripts/`이다.

| 경계 | 기준 코드와의 대조 |
|---|---|
| 문 여부 | `S/AIDoorFacts.cs:20`은 DoorCD → GateCD의 OR 단락 평가다. 기존 `AIBaseProbe.IsDoorLike`는 이에 위임하고 시험 query/거리/출력은 그대로다. |
| 마지막 문 | `AIDoorFacts:23` InWindowLastByTile은 기존 DoorsNear 본문이다. DoorCD query 다음 GateCD query, 각각 ObjectDataCD+LocalTransform과 Temp 배열 열거를 같은 순서로 사용한다. LocalTransform→TileOf→x/y 범위→ObjectData 순서와 `res[t]=...`의 마지막 할당 승리가 유지된다. 경계는 기존 Chebyshev 포함 범위다. 정렬이나 중복 제외를 추가하지 않았다. |
| 첫 문 | `AIDoorFacts:40` FirstAt은 전체 ObjectDataCD+LocalTransform query에서 문 여부→위치→해당 tile 일치→ObjectData→ActivatedByElectricityStateCD 순서로 읽고 첫 일치에서 반환한다. InWindow 사전으로 대체하지 않았다. 없는 경우 nullable fact null이다. |
| 기존 DoorView API | `S/AIEdge.cs:485/487` DoorView와 DoorAt의 public 형식/인자는 유지된다. fact를 기존 필드에 매핑하고 같은 `math.distance(me.xz,(float2)tile)`을 계산한다. 거리 계산이 helper 반환 뒤로 옮겨졌지만 게임 조회나 상태 변경이 없는 같은 입력의 순수 계산이며 query의 선택·필터·component 읽기에는 영향을 주지 않는다. |
| 하나라도 열린 문 | `AIDoorFacts:56` OpenTilesExcept는 기존 BuildGrid의 전체 ObjectDataCD+LocalTransform query다. 문 여부→위치→x/y 범위→excluded→ObjectData variation→IsOpen→HashSet.Add 순서다. 같은 칸에 닫힌 문이 뒤에 와도 앞의 열린 문을 지우지 않는다. |
| excluded의 현재 scope | `S/AIEdge.cs:160`은 `t => S.doors.ContainsKey(t)`를 전달한다. S나 doors를 미리 캡처해 다른 시점으로 바꾸지 않고, 기존 조건문이 평가하던 각 범위 내 칸에서 같은 S를 읽는다. 범위 밖에서는 단락 평가로 excluded를 호출하지 않는다. |

기존 DoorsNear의 8개 호출을 모두 InWindowLastByTile로 옮겼다. Guard의 BreachesNow(Window+2), DirectStepOk(radius0), Place의 WalkDoorTilesNow/LookMyDoors, Reach의 OwnDoorTilesNow/GuardGridAround/ShutDoorsNear/StandTile(radius1) 각각 기존 인자·호출 위치·선행 guard를 유지한다. 이전 private DoorsNear 구현/호출은 남지 않았다. 이름이 다른 ShutDoorsNear는 기존 의미의 consumer로 유지된다.

특히 LookMyDoors의 `seen.Count>0 && areaLoaded` 조건과 query를 건너뛰는 null 경로, 시야 밖 기억 판정, 소유한 문과 타인 문 구분은 caller에 그대로 있다. helper가 소유/시야 정책을 새로 결정하지 않는다.

## 물체·알려진 칸 판독

- `S/AIWorldFacts.cs:6` IsKnown은 기존 `CanSee || SeenTile` 그대로다. 시야에서 true이면 기억 조회를 생략하므로 AIMemory.Ensure에 닿는 시점도 유지된다. 적용점은 BuildGrid와 AIPlaceSpot.Grid의 정확히 같은 식 두 곳뿐이다.
- `S/AIEdge.Reach.cs:53` GuardGridAround는 기존 inView를 한 번 계산해 g.view·known 검사·DoorCell에 재사용한다. IsKnown으로 바꾸어 시야 판정을 두 번 만드는 실수가 없다. 기존 low/occupied/unknown/noStand와 문 통과 정책은 변경하지 않았다.
- `AIWorldFacts:8` IsExcludedPlacedEntity는 EnemyCD → PlayerGhost → TileCD → PickUpItemCD → CritterCD 순서의 OR다. 기존 query별 entity 열거 위치에서 호출하므로 앞쪽 true 뒤의 component 조회는 여전히 생략된다.
- `AIWorldFacts:12` IsPlacedObjectOrDoor는 먼저 `PugDatabase.GetObjectInfo(id,0)`을 읽고, info 존재→PlaceablePrefab 또는 DoorCD 또는 GateCD→TheCore 제외 순서로 평가한다. 이전 두 continue 조건의 부정 형태와 같은 short-circuit이다. info null일 때 component를 읽지 않으며 PlaceablePrefab일 때 Door/Gate 조회를 생략한다. TheCore 검사를 앞당기지 않았다.
- `S/AIEdge.Guard.cs:381` PlacedAt은 기존 ObjectDataCD+LocalTransform+HealthCD query, 제외 predicate→위치 필터→ObjectData→placed/door predicate→첫 일치 반환 순서를 유지한다. `id=ObjectID.None`, Entity.Null fallback도 같다.
- `S/AIEdge.Guard.cs:401` PlacedOn은 위치→요청 tiles와 res 중복 여부→제외 predicate→ObjectData→placed/door predicate 순서를 유지한다. 칸별 첫 일치만 남기며 query 하나로 처리한다.
- `S/AIEdge.cs:100` ObjectsInWindow는 제외 predicate만 공유한다. variation 우선/0 fallback, TheCore 허용, collider 허용, footprint와 corner 계산, KindOf의 이름 기반 분류는 그대로다. 더 엄격한 PlacedAt 정책을 이 경로에 잘못 적용하지 않았다. AIObjects/AICraft 등의 다른 제외 정책에도 확장하지 않았다.

## 예외·비용·책임

query/Temp 배열의 using 수명과 열거 순서는 helper 내부에 유지되고 새 catch가 없다. 기존 caller의 blocked query 실패 처리 등은 원래 위치 그대로다. helper가 예외를 빈 목록으로 바꾸거나 범위를 재시도하지 않는다.

추가된 정적 클래스는 함수와 작은 DoorFact 값 전달만 가진다. query 결과 저장소·공유 dictionary·frame 캐시·새 refresh가 없다. 기존 query마다 동일하게 새 결과를 만들고, 게임 조회와 캐시 비용을 앞당기거나 합치지 않는다. 문·물체 사실과 목적별 정책의 책임이 분명해졌고, 범용 격자 허브는 추가되지 않았다.

신규 .cs 두 개의 meta가 있고 GUID 각각이 Assets 범위에서 한 번만 나타남을 직접 확인했다. 기존 Scripts/**/*.cs compile 포함·같은 asmdef 범위에 위치한다.

## 검증과 미확인 범위

`git diff --check fbb98b7..3c31536` 통과. 작업자 compile0/core1048/protocol106/전체 gate 실패0은 보조 근거이며 이 검수에서 재실행하지 않았다. 시험 단언/fixture 변경은 이 diff에 없다.

query 내부 component 순서와 첫/마지막/any 의미는 직접 소스 대조 근거다. 겹친 엔터티를 실게임에서 재현해 확인했다고 주장하지 않는다. 자기/타인 문·문 통과/닫기·시야 밖 기억/재관측·물가 다리·기지 놓기는 08 계획의 최종 실행 범위로 남는다.

## HEAD 반환

Q 소스 수정 요청 없음. R 고정 diff 검수를 이어갈 수 있다.
