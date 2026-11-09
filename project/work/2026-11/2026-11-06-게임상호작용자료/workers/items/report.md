# 아이템 수집 보고

입력 1,003개 key를 모두 개별 기록했다. 입력의 기존 interactions를 순서와 내용 그대로 보존하고 모든 기존 JSON pointer가 dump에서 해석됨을 확인했다. 출력 1,003건, 중복·누락 0건, 미확인 사유가 있는 항목 1,003건이다. 미확인은 불가능을 뜻하지 않는다.

Eatable 184건에 조건부 EatItem 경로, CastingItem 66건에 시전 상태 전환 경로를 code_candidate로 추가했다. 각각 타입별 슬롯 처리에 근거하며 개별 프리팹 연결 및 게임 빌드 대응은 확인하지 못했다. Eatable은 PetCandyCD가 없고 소비 조건이 충족되는 경우로 제한했고, PetCandyCD가 있으면 별도 FeedPet 경로다. CastingItem은 이미 시전 중이면 중단하며 레시피가 있으면 재료 및 해당 시 인근 오브젝트 조건이 있다. 이 결과는 개별 효과, 성공, 일반적인 무조건 사용 가능성을 입증하지 않는다. NonUsable, Valuable, UniqueCraftingComponent, KeyItem 중 CastItem 컴포넌트를 가진 경우에도 슬롯 경로가 있을 수 있으나 개별 연결이 없어 일괄 적용하지 않았다. NonObtainable 및 ID 전용을 포함해 특수 행동은 미확인이다. 게임 실행 검증은 하지 않았다.

원문 대조:
- 7:0 Leash: dump /objects/8은 CastingItem, req [[1640,8]]로 관측되며 출력 variation에 대한 실행 조건은 아니다; /objects/799는 4048:0이고 canCraft에 [7,1,0]. PlayerController.cs 4109–4110과 CastingItemSlot.cs 14–24, 28–87은 조건부 시전 상태 경로를 보여준다.
- 1201:0 Pet Pellet: dump /objects/352는 Eatable이다. PlayerController.cs 4105–4106과 EatableSlot.cs 13–30, 42–54는 PetCandyCD 여부에 따른 분기와 소비 요청을 보여주지만 해당 프리팹이 어느 분기에 연결되는지는 확인하지 못했다.
- 1002:0 Iron Bar: dump /objects/338의 req는 [[1510,1]], /objects/755의 4004:0 canCraft에 [1002,1,0]이 있다. 제작 결과의 variation은 null/unknown이며 req 관측은 실행 조건이 아니다. 게임 내 성공도 별도 확인이 필요하다.

재현: 이 폴더에서 `node collect.mjs`. 두 번 실행한 results.json SHA-256은 모두 `87b39790551d40ad2f17228623864da9ab0f82d294a3f9ced44d22d91e09edf6`. 스크립트는 key 중복과 입출력 순서·집합 일치를 검사하며, 입력의 기존 상호작용 보존과 pointer 해석도 별도로 대조했다. 코드 근거의 SHA-256은 재현 시 실제 소스 파일에서 계산한다.

## 유형별 조사 범위와 미확인

전체 key 순회는 목록의 각 입력을 결과에 남겼다는 뜻이다. 가능한 상호작용을 전수 발견했다는 뜻은 아니다. 공통 코드와 고정 덤프 관계만 조사했으며 개별 프리팹과 실행은 미확인이다.

| ObjectType | 수 | 조사 및 남은 경계 |
|---|---:|---|
| Eatable | 184 | EatableSlot의 조건부 소비 경로 후보. PetCandyCD 유무, 먹이 분기, 개별 효과 미확인. |
| CastingItem | 66 | CastingSlot의 조건부 시전 상태 요청 후보. 개별 시전 효과와 프리팹 미확인. |
| NonUsable | 150 | 덤프 제작 관계 보존. CastItem 컴포넌트가 있으면 시전 슬롯으로 갈 수 있으나 개별 연결 미확인. |
| Valuable | 126 | 덤프 관계 보존. 가치나 sell 필드만으로 판매 가능성을 확정하지 않음. CastItem 연결 미확인. |
| UniqueCraftingComponent | 17 | 덤프 관계 보존. CastItem 컴포넌트에 따른 슬롯 경로와 특수 용도 미확인. |
| KeyItem | 12 | CastItem 컴포넌트에 따른 슬롯 경로만 확인. 열쇠별 대상, 해제와 소모 조건을 연결할 근거 미확인. |
| NonObtainable | 437 | 덤프 관계 보존. 유형명만으로 존재·획득·상호작용 불가능을 단정하지 않음. |
| 덤프 없음 / type null | 11 | 등록 ID 보존. 프리팹·유형·행동 미확인. |

추가 공통 조사: InventoryUtility.cs 837–842는 이동 실패 시 드롭 경로를 보여주지만 각 key의 인벤토리 입장 및 슬롯 조건은 확인되지 않았다. 같은 파일 1062–1078, 1099–1108은 판매 금지 컴포넌트, Legendary와 가격 조건을 보여준다. 2135–2157, 2191–2205는 수리의 내구도·레벨 컴포넌트 및 재료 조건을 보여준다. 각각 개별 연결에 필요한 자료와 해시를 results의 미적용 후보 규칙에 남겼다. 코드와 설치 빌드 대응도 미확인이다.

코드 출처 excerpt 896개는 각 지정 줄 범위의 실제 원문 포함 여부를 검사해 모두 일치했다. 기존 요약은 summary로 분리했다. 출처 파일 목록은 변경하지 않았다.

