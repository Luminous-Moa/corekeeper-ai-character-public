# 시범 수집 독립 검수 (1단계)

## 수정 필요

- `collect.mjs`의 craft는 `canCraft`를 ID로 역참조해 모든 variation에 붙이고 각 레코드의 `raw.req`를 해당 제작 관계의 조건으로 함께 둔다. 출력 variation 연결은 원본에 없으므로 조합된 조건이 그 variation의 실제 제작법임은 입증되지 않는다. 예를 들어 `inputs/dump.json#/objects/88/req` (112:0)는 [[1610,1],[301,1]]이고 `#/objects/89/req` (112:1)는 [[1610,1]]이다. 각 req는 별도 관측으로 보존하고 연결을 미확정으로 표시해야 한다. craft의 sources에도 해당 req 포인터가 빠져 있다.
- 기존 data interaction에는 `objectRole`이 없다. 제작 결과물, 재료, 제작 장소 역할이 명시되지 않아 수집지침의 대상·도구·장소 구분을 기계적으로 검증할 수 없다. 결과물의 역할은 추가 근거 없이 target 등으로 임의 지정하지 말고 의미를 명시해야 한다.
- `canCraft` 배열의 세 번째 값은 입력에서 버려진다. 현재 SDK writer `C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIKnowledge.cs:285-291`에 따르면 이 값은 craftingTimeOverride다. 예컨대 `inputs/dump.json#/objects/765/canCraft`의 철검 항목은 [8901,1,0]이다. 원본 보존 및 재현 가능성을 위해 값을 버린 사실을 명시하거나 보존해야 한다. 이 writer가 해당 dump를 생성한 정확한 버전인지는 미검증이다.

## 확인한 사실

- 고정 원본의 dump 3,020행은 (id,var) 중복이 없고, 양수 ObjectID 중 dump에 없는 ID는 11개다. 합집합 정의에 따라 3,031건이다. 이는 게임의 모든 상태·상호작용의 완전성을 의미하지 않는다.
- `inputs/dump.json#/objects/2425`의 8901:0은 철검이며 req는 [[1002,8],[1003,1]]이다. `#/objects/765/canCraft`, `#/objects/772/canCraft`, `#/objects/775/canCraft`, `#/objects/785/canCraft`, `#/objects/798/canCraft`에 각각 8901이 있다. 실제 게임 내 제작 성공은 확인하지 않았다.
- inputs 세 파일의 SHA-256은 sources.json 기재와 일치한다. 설치 manifest의 buildid와 dump 생성 버전이 실제 일치한다는 별도 증명은 확인하지 않았다.

## 미검증

- 전체 작업자 결과, 코드 후보의 특정 프리팹 연결 및 디컴파일 코드와 설치 빌드의 일치 여부는 아직 검수하지 않았다.
- 이 문서는 입력과 시범 결과의 정적 대조이며 게임 실행 검증이 아니다.

## 수정 후 재검수

공통 collect.mjs와 재생성된 objects.json, pilot 및 지침을 대조했다. 이전 세 지적은 해결되었다: craft의 subject와 출력 variation은 null이고 applicability는 unknown이며, req는 원본 variation과 pointer를 가진 별도 recipeObservation으로 분리되었다. objectRole이 명시되고 canCraft rawEntry의 세 번째 값을 포함한 전체 배열이 보존된다. 112:0과 112:1의 서로 다른 req도 각각의 관측으로 유지된다.

3,031행의 기존 interaction 1,086건에 역할이 있는지, 모든 craft의 variation 범위·req 관측 값·station rawEntry가 고정 dump와 일치하는지 프로그램으로 확인했으며 오류는 없었다. 이는 공통 결과에 대한 검사이며, 작업자 보강분과 최종 통합 결과의 검수 또는 게임 실행 확인은 아니다.
