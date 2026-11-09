# 최종 독립 검수 — 진행 중

## 수정 필요 (부모에게 전달)

1. objects-interactions.md는 각 variation 행에 ObjectID 공유 제작·재료 관계를 표시하면서 variation 미확정을 생략한다. 112:0~4처럼 서로 다른 변형이 모두 제작 가능하다는 오해를 막도록 각 공유 관계와 머리말에 variation 미확정을 표시해야 한다. collection.json의 subject 및 applicability는 이를 보존한다.
2. reproduce.mjs는 각 worker의 코드 해시를 현재 디컴파일 파일에서 새로 계산한 뒤 integrate.mjs가 같은 현재 파일을 대조한다. 따라서 이전 검수 때와 코드가 달라져도 새 해시로 통과할 수 있고 README의 원본 코드 변경 감지 주장은 보장되지 않는다. 재현 전에 고정 코드 해시 manifest를 비교하고, 검수 후 명시적으로 갱신할 필요가 있다.
3. items 코드 출처의 excerpt에는 실제 발췌가 아닌 생략부호를 포함한 요약이 있다. 지침의 원문 발췌 계약에 맞춰 실제 문구로 교체하거나 요약임을 식별해야 한다. integrate는 현재 excerpt와 해당 줄의 일치를 확인하지 않는다.

## 현재 확인

- world의 PlaceablePrefab, creatures의 Critter/TrainingDummy 배치와 Pet 슬롯 경로, items의 Eatable/CastingItem 조건부 슬롯 경로는 지정 소스의 분기와 대체로 부합한다. 프리팹, 버전 및 실행은 미확정이라고 표기한다. 표본 대조: PlayerController.cs:3881-3944, PlaceObjectSlot.cs:37-98, InventoryUtility.cs:3551-3557, EatableSlot.cs:13-54, CastingItemSlot.cs:14-87.
- 각 그룹은 타입만으로 특정 상자/동물 등의 별도 기능을 확정하지 않고 미매핑 후보로 둔다. 공통 data의 variation 경계는 JSON에 남는다.
- coverage.json은 3,031개 기록 전부에 unresolved를 나타내며, README는 전체 상호작용 완성을 주장하지 않는다. 수치는 완전한 행동 탐색의 증거가 아니다.

## 미완료

- equipment 보강, 전체 재현 후 결과·해시 및 최종 표본 검수 전이다. 본 문서는 최종 통과 판정이 아니다.
## 수정 후 최종 한정 판정

앞선 세 지적은 수정 확인: Markdown 각 공유 관계에 해당 변형 적용 미확정을 표시하고, code-sources.json 고정 해시를 재현 시작 전에 검사하며 통합 시 미등록 코드도 거부한다. items 발췌는 실제 소스 발췌로 수정되었다. 장비의 HoeSlot.cs:27-109, BucketSlot.cs:18-119, WaterCanSlot.cs:20-145, AttackWithEquipmentSystem.cs:144-186을 표본 대조했고 조건과 기록된 처리 의미에 막는 차이는 발견하지 못했다. 다른 세 그룹의 대표 소스 표본은 위에 기재했다.

최종 collection을 직접 읽어 3,031개 key 중복 없음, 규칙 37개, data 1,086건, code_candidate 2,155건을 확인했다. 112:0·112:1의 craft variation 미확정 표기와 8901:0의 자료 및 코드 후보 수준을 표본 확인했다. 전체 재현 성공은 통합 담당자의 실행 보고를 확인한 것이며 이 검수자는 수집 파일을 변경하지 않기 위해 명령을 다시 실행하지 않았다. 모집단 처리와 제시된 재현 실행에 대해 한정 통과한다.

범위 한계: 전체 상호작용의 완성, 개별 프리팹 연결, 디컴파일과 설치 빌드의 대응, 실제 게임 실행 및 모든 행의 의미를 검증한 것은 아니다. 모든 행에 unresolved가 남는다. 위 판정은 표본 의미 대조와 구조·수치 확인에 한정된다.

최종 SHA-256:
- collection.json: 43865464125894b5a53e3722d26df3df51684b02caee3cd1f3a069485ec1c6e4
- coverage.json: 82d906c4790a70412f90cf0d8131b0c9d2d5fcc28d2e4229c84c7503114d09be
- objects-interactions.md: 2a8b472c3228e63a630b27e8e64549e6db32ffe84de6b4bb840752fb5bafd2eb
- code-sources.json: d82f27d57651d6977b0b5479691233f9a41bed5a09dd9c887ce58650b35cec91
