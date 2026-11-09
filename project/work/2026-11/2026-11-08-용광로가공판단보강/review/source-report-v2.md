# ProcessResources 가공 사슬 보완 v3: 독립 재검수 보고

- 검수 일자: 2026-11-09
- 검수 대상 계약: `contract.json` SHA-256 `06d06bbeab6d4037b2d8b4498256e6017429ea076456980b49926ed65ead8115`
- 검수한 기록 본문: `production/supplement.json` SHA-256 `7fa78b66e51947192353fc2d2b17640d6ca926119e22cea769e506a8c033385b`
- 검수한 후보: `candidate-review-v3/`
  - graph.json `ce730d9a7f5244a7735353e4929c01467e4f5ac5508576775626c823ef7b1aaa`
  - coverage.json `6a9fd584be263c9fd324928d0cb19b0b62e27292ff56c3014f00499318afd0b9`
  - manifest.json `4db2724befc382b779c968d595cfe52da78b471f18c10b6a26c29837edd770dd`
- 근거 루트: `...\게임원본-상호작용\code\`(아래 경로는 이 루트 기준), `...\raw\components.jsonl`, `...\raw\objects.json`
- 이전 검수:
  - `review/source-report.md`(v1, 계약 `dd458c46…`, F1–F3)
  - 이 파일의 이전 내용(v2, 계약 `6eaa774a…`, supplement `1ea5bbe1…`, F4). 요청에 따라 v3 판정으로 덮어썼다.
- 판정: **changes_required**(열린 지적 1건 F4. F1–F3 해소, 외부 지적 (a)(b)(c) 반영 확인)

## 1. 무결성

- 4건의 codeReferences(objects.json pointer 포함)를 다시 해시했다. 모두 일치하고 refs 누락도 없다.
- contract.json pins 31개도 전부 일치한다. baselineGraphSha256은 `a16c7394…`(baseline-260)이다.
- verification-candidate/summary.json은 status pass이고 hashes는 candidate-review-v3의 실제 해시와 같다.
- 내가 직접 baseline-260과 candidate-review-v3을 비교했다.
  - 기존 노드 251477개가 모두 같고 기존 간선도 모두 남았다. 노드 45개, 간선 160개가 늘었다.
  - `base:…:7493579351412767700#inventory_transfer`에서 나가는 간선은 `has_process_resources_supplement`만 늘었다.
  - 4044·3905 기존 행으로 가는 `excludes_source_claim`이 있다.
- manifest의 `publication.scope`는 ProcessResources 사슬 설명이다(F3 해소 유지).

## 2. v2와 v3의 차이

verification-candidate-v2의 `*-source.json`(v2 기록 값)과 v3 supplement.json을 항목별로 비교했다. 달라진 곳은 아래뿐이고 sourceBindings·links·codeReferences는 같다.

- record0 recipe-activation: conditions[5](서버 판정), failures[0], boundary(하한 검사 없음 항목 추가)
- record2 craft-timer: decisionImpact, flow[1], failures[1]
- record3 output: flow[3]

## 3. 외부 지적 (a)(b)(c) 반영 확인

- **(a) GetRecipeInfo 상한만 검사**: Pug.Other/Inventory/InventoryUtility.cs:2214-2222를 원문으로 읽었다.
  - `TryGetBuffer`가 실패하면 default(None)를 돌려준다.
  - `bufferData.Length > index ? bufferData[index] : default`이라 상한만 본다.
  - 그 뒤 `GetRecipeInfo(db, objectID)`는 DB에 없으면 None이다(2224-2235).
  - 음수 index이면 `bufferData[index]` 접근으로 가고 하한 방어가 없다.
  - 새 조건 "buffer 없음·index>=길이·DB 없음만 None, 하한 검사 없음"과 실패 목록, boundary("음수 index 결과 미확인, 무해한 거절로 보지 않음")가 원문과 맞다.
- **(b) 진행 초기화 조건**: PugAutomationStartCraftSystem.cs:203-221을 확인했다.
  - CanCraft가 참인 첫 레시피 i가 없으면 0/-1이 된다.
  - i가 currentlyCraftingIndex와 다를 때만 시간을 다시 정한다. 같으면 손대지 않는다.
  - "재료를 일부 빼도 같은 첫 레시피가 계속 선택되면 남은 시간 보존"과 decisionImpact의 "(같은 레시피가 유지되면 보존)"이 맞다.
- **(c) 시간 결정 순서**: StartCraft 215-219와 PugAutomationCraftingSystem.cs:210-214 모두 `instant ? 0 : DB craftingTime`을 먼저 정하고, `craftingTimeOverride > 0`이면 덮어쓴다. record2 flow[1]("InstantCrafting이어도 양수 override가 이긴다")과 record3 flow[3]이 맞다. v1·v2의 표현 메모 N2는 이로써 해소되었다.

## 4. F1–F3(v1 지적) 해소 유지

- **F1**: recipe-activation은 [3905, 4004, 4009, 4015, 4032], material-move는 [4004, 4009, 4015, 4032]이다. 4044 기존 행에 excludes_source_claim이 있다. 1025 Glass가 0종 재료·태그 29라 두 관문이 거짓이라는 진술을 원문으로 확인했다(objects.json /358, ProcessResourcesCraftingUI.cs:100, CraftingHandler.cs:14-36, InventoryUtility.cs:3080).
- **F2**: 3905는 move 묶음에서 빠졌고 excludes_source_claim이 있다. 다음 근거로 확인했다.
  - LumpOfCoal(5192, 태그 38)과 입력 칸 태그 [17,18](components.jsonl:436)
  - 태그 불일치 시 FailedRequirement(InventoryUtility.cs:2368-2455)
  - 떨어뜨림(3086-3095, 3206-3244)
- **F3**: 위 1절과 같다.
- 4004·4009·4015·4032의 재료가 모두 입력 칸 acceptsObjectIds에 들어 있다는 진술도 맞다(raw 67869, 32429, 28963, 33476). 3905·4044 태그 레시피의 CanCraft 판정(1653-1672)도 맞다.

## 5. 열린 지적

### F4. "손으로 넣기만"은 자동 이동기 투입 경로를 배제해 정적으로 틀린다(v3에서 미반영)

- 해당 문장:
  - record0 recipe-activation conditions[2]: "4044는 손으로 넣기만 가능하다."
  - record0 decisionImpact: "…손으로 넣어야 하는 시설(3905·4044·1200)을 구별한다."
  - record2 craft-timer conditions[2]: "4004·4009·4015·4032는 레시피 클릭(앞 단계) 또는 손으로 넣기, 3905·4044·1200은 손으로 넣기(인벤토리 이동)만으로 입력 칸이 채워진다."
- 근거:
  - 7개 원본 모두 AutomatedStorageAuthoring이 있다. raw components.jsonl 줄은 3905:55461, 4004:48899, 4009:45220, 4015:20781, 4032:16140, 4044:29253, 1200:658이다.
  - 이 컴포넌트가 있으면 AutomationType.Storage가 켜진다(Pug.Automation.Conversion/PugAutomationConverter.cs:162-165). StorageCD는 Pug.Automation/Pug.Automation/PugAutomationInstantiateSystem.cs:1215에서 생긴다.
  - 이동기가 이동을 끝내면 `PlacedAtPosition.Add(mover.stop, …)`가 실행된다(Pug.Automation/Pug.Automation/PugAutomationSystem.cs:190-208).
  - PlaceInStorageJob(PugAutomationSystem.cs:2398-2434)은 그 위치의 보관 엔티티에 대해 처리한다. slotRequirements를 만족하면 `Create.MoveOrDropAmount(item, 0, storage.inventoryEntity, …)`로 넣고, 만족하지 않으면 떨어뜨린다.
  - 따라서 이동기(로봇 팔 등)도 이 시설들의 입력 칸을 채울 수 있다. 3905에는 조리 음식, 4044에는 모래, 1200에는 알, 나머지에는 해당 광석이다.
- 문제: "만"·"넣어야 하는" 단정이 자동화로 공급하는 판단을 배제한다.
- 요청: "플레이어 행동으로는 손으로 넣기(인벤토리 이동)만. 이동기(AutomatedStorage) 투입은 정적으로 가능하며 이 기록 범위 밖·실게임 미관측"처럼 한정하거나 경로를 더한다. 세 문장의 문구만 고치면 되고 묶음·간선은 바꿀 필요가 없다. 4004 등에 대한 "레시피 클릭 또는 손으로 넣기"도 같은 방식으로 이동기 경로를 덧붙이면 일관된다.

## 6. 표현 메모

- N2는 (c)로 해소되었다.
- N6("플레이어 없음")은 이번 (a) boundary와 같은 결로, 서버 쪽 비정상 요청의 결과는 확인하지 않은 것으로 읽힌다. 의미를 바꾸지 않는다.
- 나머지 N1, N3, N4, N5, N7, N8과 v2의 N9(역방향 `has_process_resources_supplement` 간선 이름이 배제 관계와 구별되지 않음)는 그대로이고, 모두 의미를 바꾸지 않는다.

## 7. 재사용 조건

- 이 판정은 supplement.json SHA-256 `7fa78b66…`에 묶인다. rebase로 계약 SHA만 바뀌고 supplement.json 바이트가 같으면, 이 판정(changes_required, F4 하나 열림)과 원문 확인 내용을 그대로 재사용할 수 있다.
- F4를 고친 다음 판에서는 바뀐 문장과 그 refs만 다시 보면 된다. 그 밖의 항목은 위 2절의 항목별 비교 방식으로 바뀐 데가 없는지만 확인한다.
- 정본 위로 rebase하면 기존 노드·간선 보존 검사(verify.py)는 새 baseline으로 다시 돌려야 한다.

## 8. 결론

v3은 외부 지적 (a)(b)(c)를 원문대로 반영했다. F1–F3 해소도 유지된다. 남은 것은 v2에서 지적한 F4("손으로 넣기만"이 이동기 투입 경로를 배제)로, 세 문장의 문구를 고치면 된다. runtimeTested=false, wholeScopeComplete=false.
