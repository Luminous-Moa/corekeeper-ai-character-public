# 장비 자료 수집 보고

갱신된 입력의 621개 key를 각각 출력하고 기존 데이터 관계를 그대로 보존했다. 제작 관계는 `subject.variation: null`, `variationApplicability: unknown`과 별도 `recipeObservation`, `canCraft`의 `rawEntry`를 유지한다. 재료 관찰을 특정 출력 변형의 제작법으로 확정하지 않는다.

디컴파일에서 ObjectType→슬롯 선택, EquipmentUpdateSystem 또는 AttackWithEquipmentSystem의 해당 슬롯 호출, 슬롯 처리 조건을 대조했다. code_candidate 규칙은 15개다. 장비 교체 450, 삽 7, 낚싯대 7, 근접 무기·곡괭이·큰망치·드릴 54, 원거리·투척 51, 광선 5, 소환 7, 괭이 5, 물뿌리개 3, 양동이 8, 페인트 14, 악기 6, 지붕 도구 1, 씨뿌리개 2건에 적용했다. 양동이는 담기와 붓기를 별도 규칙으로 기록한다. 효과나 소환 성공 대신 확인된 상태 시작·요청·트리거를 기록하며 대상, 물, 재료, 마나, 서버 및 상태 조건을 각 규칙에 보존한다.

타입별 미확인 경계:

- Helm, BreastArmor, PantsArmor, Necklace, Ring, Offhand, Bag, Lantern, Pouch: 공통 교체 외 개별 효과, 전용 조작과 실제 프리팹 연결 미확인. 링과 파우치의 선택 로직은 원문에 있으며 결과는 교체 요청까지만 주장한다.
- MeleeWeapon, MiningPick, Sledge, DrillTool: 공통 타격 대기열 처리만 확인했다. 특정 광물의 채굴 가능성과 도구별 보조 행동은 프리팹·대상 조건이 필요하다.
- RangeWeapon, ThrowingWeapon, BeamWeapon, SummoningWeapon: 투사체 트리거 또는 조건부 소환 호출까지 확인했다. 투사체 연결, 명중, 소모, 소환 성공과 개별 능력은 미확인이다.
- Shovel, Hoe: 유효 배치 시 상태 처리까지만 공통화했다. 실제 대상·타일별 변화, 수확 조건과 변형별 예외는 미확인이다.
- FishingRod: 낚시 상태 시작만 확인했다. 물·입질·미니게임 및 어획 성공은 미확인이다.
- WaterCan, Bucket: 코드에 기재한 물·양·타일·용기 분기만 후보로 수집했다. 물 원천의 실제 배치 가능성과 액체별 결과는 미확인이다.
- PaintTool, Seeder, RoofingTool, Instrument: 구성요소·대상·씨앗·자원 및 상태 조건이 충족되는 경로만 기록했다. 각 프리팹과 대상별 호환성은 미확인이다.
- BugNet 1건: PlayerController는 BugNet 슬롯을 선택하지만 EquipmentUpdateSystem에 전용 BugNet 갱신 호출을 찾지 못했다. 그물 포획의 구체적 호출 경로와 대상 조건을 연결하지 못해 행동을 추가하지 않았다.

모든 항목에 개별 특수 행동, 프리팹 연결, 디컴파일과 설치 빌드 대응, 실제 실행 결과 미확인을 남겼다. 621건 순회는 게임 전체 상호작용 완성이 아니다. 게임은 실행하지 않았다. 검색 과정에서 PowerShell에 bash식 중괄호와 와일드카드 경로를 사용해 구문 또는 경로 오류가 발생했고, 명시적 파일 경로로 다시 검색했다.

대표 원문 대조:

- 구리 삽 `1:0`: 덤프 `/objects/1/req`는 `[[1610,4],[1001,2]]`, `/objects/757/canCraft`는 `[1,1,0]`을 포함한다. `PlayerController.cs:3912–3913`, `EquipmentUpdateSystem.cs:156–157`, `ShovelSlot.cs:24–59`는 삽 슬롯과 조건부 파기 상태를 보여준다.
- 철검 `8901:0`: 덤프 `/objects/2425/req`는 `[[1002,8],[1003,1]]`; `/objects/765`, `/772`, `/775`, `/785`, `/798`의 `canCraft`는 해당 ID를 포함하며 장소 ID는 각각 4014, 4021, 4024, 4034, 4047이다. 출력 variation과 제작 성공은 미확인이다. 근접 공격 후보는 공격 시스템의 미파손·루틴·차지 분기와 MeleeWeaponSlot의 타격 큐를 근거로 한다.
- 급조 고글 `6820:0`: 덤프 `/objects/1824` 유형은 Helm이며 `PlayerController.cs:3889–3899`, `EquipGearSlot.cs:11–32,86–104`는 유형별 슬롯과 조건부 교체 요청을 보여준다. 개별 프리팹 연결은 미확인이다.

재현: 작업 폴더에서 `node workers/equipment/collect.mjs`. 중복 및 누락 없는 key, 기존 관계 보존, raw와 기존 포인터, 제작 관찰·원본 배열과 재료 수량을 검사했다. 재실행 전후 `results.json` SHA-256은 모두 `2EC43B4304BF478A04764D45D7F82D0EFF61DAF7394877DE62445881C5E7B4F6`였다. 각 코드 출처의 발췌문과 전체 파일 해시도 생성 시 확인한다.
