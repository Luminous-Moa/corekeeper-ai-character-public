# 월드 프리팹 수집 보고

갱신된 입력 1,209개 key를 각각 보존했고 기존 상호작용을 그대로 복사했다. 제작 관계는 출력 variation이 미상인 ObjectID 관계이며, 개별 행에서 관찰한 req는 recipeObservation으로 분리됐다. 원래 canCraft 배열 항목도 rawEntry로 보존한다. PlaceablePrefab 타입에 대해서만 배치 슬롯의 조건부 코드 후보를 추가했다. canPlaceObject, CanPlaceItem, 인벤토리 소모 가능 검사 등 조건을 포함한다. 코드와 설치 빌드의 대응 및 게임 실행은 확인하지 않았고, 코드상 상태 진입은 실제 배치 완료를 보장하지 않는다.

추가 조사에서는 Chest.Use의 인벤토리 활성화·UI, CraftingBuilding.Use의 제작 핸들러 활성화·UI, Sitting 상태의 좌석 처리와 복귀, tile 구성요소의 AddTile 경로, Gate의 variation별 시각 처리를 확인했다. 코드 원문·줄·해시는 rules에 남겼다. 입력에서 프리팹과 해당 구성요소 또는 이벤트 호출 연결을 확인하지 못해 어느 특정 ID에도 이 후보를 붙이지 않았다. Gate 시각 처리만으로 플레이어가 문을 여는 행동을 주장하지 않는다. 후보는 전체 함수 목록이 아니다.

가장 중요한 미확인은 ID·variation에서 실제 프리팹/엔티티 구성요소와 상호작용 이벤트로 이어지는 매핑, 잠금·전기·권한·위치·아이템 등 객체별 조건, 타일/가구/침대/보관함/기계의 세부 동작, 디컴파일 버전과 설치 빌드의 일치 여부다. 이를 풀려면 해당 빌드의 데이터베이스 프리팹 참조, Unity 직렬화 에셋과 MonoBehaviour/ECS 컴포넌트 및 이벤트 바인딩, 관련 조건 데이터를 추출·연결해야 한다. 설치 디렉터리에 `CoreKeeper_Data/resources.assets`, `sharedassets*.assets`, `StreamingAssets/aa/catalog.bin`이 존재하나 이번에는 바이너리에서 매핑을 추출하거나 유효성을 확인하지 않았다. SDK Assets에서 살펴본 asset 파일 목록과 디컴파일 클래스 이름만으로는 연결을 증명할 수 없다. 모든 항목에 개별 행동 미확인을 남겼으며 미확인은 불가능을 뜻하지 않는다.

대표 대조:

- `100:0`: dump `/objects/70`의 id 100, var 0 및 req `[[1610,5]]`가 입력 raw와 일치한다. `/objects/1679/canCraft`에 `[100,1,0]`이 있으며 기존 관계에 원문 항목을 보존한다. 출력 variation과 상자 열기는 확정되지 않는다.
- `1200:0`: dump `/objects/351`의 id 1200, var 0 및 canCraft 첫 항목 `[1221,1,0]`이 입력과 일치하며 기존 craft_at 관계를 보존한다. 출력 variation 및 실제 제작 성공은 확정하지 않는다.
- `103:1`: dump `/objects/74`의 id 103, var 1과 입력 raw가 일치하고 기존 관계는 비어 있다. variation key를 유지하고 타입별 코드 후보만 추가했다.

검사: 입력·출력 key 순서와 집합 및 중복을 검사하고 각 기존 interaction 배열의 동일성을 확인했다. 1,209건 모두 미확인 사항이 남는다. 결과를 두 번 재생성한 SHA-256은 모두 `4e8d434c8210f6729cb41b44c410ce74cdbdfef44ea71752ae40fa53f59701a1`이다. 근거 줄과 전체 파일 해시는 스크립트가 원본에서 계산한다.

재현: 이 디렉터리에서 `node collect.mjs`. 입력 스냅샷과 `C:/Dev/_ref/corekeeper-decomp/`가 필요하다. 전체 key 처리와 전체 상호작용 조사는 다르며 후자는 완료되지 않았다.
