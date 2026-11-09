# 생물 그룹 수집 보고

고정 입력 198개(Pet 17, Creature 129, Critter 50, TrainingDummy 1, PlayerType 1)의 key와 기존 interactions를 보존했다. Pet 슬롯 적합성 후보 17개와 Critter/TrainingDummy의 소지품 배치 경로 후보 51개를 추가했다. 모두 code_candidate이며 디컴파일과 설치 빌드의 대응 및 실제 실행은 미확인이다. 배치는 보조 상호작용 입력, 빈 마우스, 배치 가능 판정 및 소모 가능 판정 등 조건을 유지한다. 펫 슬롯 조건은 슬롯 적합성이지 입수 또는 소환 성공의 증거가 아니다.

포획 코드는 충돌 필터와 Critter 컴포넌트를 요구한다. ObjectType만으로 컴포넌트 연결이 증명되지 않아 각 Critter에 포획 행동을 부여하지 않았다. Creature의 배치는 Creative 모드 또는 CattleCD/MountCD 분기가 있으나 개별 연결은 확인하지 않았다. 추가 조사에서 유효 피해 판정(EntityUtility.cs:2256-2263), 먹기 상태의 held/contained 먹이 소비(EatStateSystem.cs:182-195, 226-235), NPC 상점 UI 열기(NPC.cs:16-34), CattleCage와 CattleCD를 이용한 가축 우리 넣기 및 LeashedCD를 이용한 목줄 경로(Casting.cs:407-470)를 확인했다. 이들은 컴포넌트, 거리, 상태, 도구 및 프리팹 연결을 확인해야 하므로 개별 ID에 행동으로 부여하지 않고 출처와 함께 규칙 후보로 남겼다. 쓰다듬기는 효과음 이벤트만 확인했으며 대상과 입력은 미확인이다. 전리품과 그 밖의 개별 특수 행동도 미확인이다. 198개 모두 unresolved를 유지하며, 조사 완료가 전체 행동의 완성을 의미하지 않는다.

## 원문 대조

- `1222:0`: `inputs/dump.json` `/objects/361`은 `type: Pet`. `InventoryUtility.cs:3551-3557`의 PetSlot/Pet 일치와 `PlayerController.cs:3898-3899`의 장비 슬롯 경로를 대조하여 슬롯 후보로 기록했다.
- `1300:0`: `/objects/410`은 `type: Creature`, `canCraft: [[7900,1,0]]`. 갱신된 입력의 기존 `craft_at` 관계, station 역할, 정확한 variation과 포인터를 그대로 보존했다. 출력·재료의 ID 단위 관계는 해당 variation이 미확정임을 유지하며 recipeObservation을 실행 조건으로 승격하지 않는다. 이 값만으로 동물의 실제 상호작용이나 제작 성공을 단정하지 않는다.
- `3500:0`: `/objects/707`은 `type: Critter`. `PlayerController.cs:3916-3919` 및 `PlaceObjectSlot.cs:35-98`의 조건부 배치 경로와 대조했다. `PlayerAttackSystem.cs:483-494`의 별도 컴포넌트 요건이 충족되는지는 확인하지 못했다.

## 재현과 확인

작업 폴더에서 `node workers/creatures/collect.mjs`를 실행한다. Node 내장 모듈만 사용하며 스크립트는 입력·출력 key의 순서와 유일성, 기존 interaction 보존, 코드 인용 원문을 검사한다. 재실행 전후 results.json SHA-256은 모두 `6504b52a1fc6d8eb3739f17e49f1246c6849c1e6735b2af0b872df1450189ac0`이다. 입력과 출력 각 198개, 고유 key 198개, unresolved 198개다. 코드 파일의 전체 해시와 줄 범위는 결과의 각 근거에 포함된다.
