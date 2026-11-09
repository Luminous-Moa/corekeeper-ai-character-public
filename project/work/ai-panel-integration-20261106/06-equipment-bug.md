# Gemini 장비 미표시 — 독립 조사

2026-11-06. 사용자 직접 플레이 중 읽기 전용 조사. 게임 입력·종료·Unity·설치·제품 코드 변경 없이 기존 스크린샷, 현재 Player.log, Gemini 저장 파일과 설치 소스, 게임 디컴파일 소스를 확인했다.

## 현재 결론과 판정

**최신 재검증: 134b1c3 재입장 후 t=97.4의 동일 Gemini에서 서버·클라이언트·렌더러 원천이 모두 방어구 미착용으로 일치한다. 이 시점 패널의 ‘없음’은 올바르며 장비 표시 결함은 확인되지 않았다.** 아래 초기 조사 기록은 당시 한계와 함께 보존한다. 최초 2762495 관측 순간에는 raw 기록이 없어 재시작 전까지 소급 확정하지 않는다. 비어 있지 않은 실제 착용 슬롯의 표시는 별도 미검증이다.

이번 조사에서 확인한 구체적 사실은 다음과 같다.

1. Gemini는 실제로 나무 투구·상의·하의를 제작했고 서버 인벤토리에 모두 있다. 단순 AI 발언만 있는 상태가 아니다.
2. 이번 실행의 제작 경로는 결과를 일반 가방으로 이동한 뒤 완료한다. 이후 주기 저장에서도 세 방어구는 일반 핫바 4·5·7번 칸에 남고, 10~129번 칸은 전부 ObjectID.None이다.
3. 원게임의 착용 효과 경로는 현재 EquipmentCD의 슬롯을 읽는다. 게임은 이 컴포넌트를 활성 EquipmentPresetsBuffer 항목에서 복사하며, 패널은 그 활성 프리셋 슬롯을 읽는다. 따라서 **‘프리셋 대신 EquipmentCD로 바꾸면 원인 해결’이라고 현재 증거만으로 단정할 수 없다.**
4. 현행 로그/저장에는 같은 순간의 active preset 번호, 각 preset/current EquipmentCD 슬롯 인덱스, 슬롯별 원시 objectID와 UI 출력의 대응이 없다. 사용자 관측을 실제 ECS와 같은 시점에 대조하는 마지막 증거가 부족하다. 이 제한을 사용자 착각이나 AI 발언의 문제로 돌려서 종료하지 않는다.

**제작→일반 가방 보관까지 수행하는 코드/로그는 확인했지만, 이것만으로 사용자가 본 실제 착용 외형을 설명할 수 없다.** 추가 추적에서 외형은 호스트 ClientWorld의 치장 우선 경로, 패널은 ServerWorld의 활성 장비 프리셋 경로임을 확인했다. 현재는 이 두 경로의 동일 시점 raw 값이 없어 특정 원인을 우선 확정하지 않는다. 아래 추가 조사에 데이터 원천과 진단 분기점을 정리했다.

## 실행 대상·실제 데이터

- 현재 실행/설치 후보는 `2762495`. 글자 크기 보완 `25db8ed`는 아직 미설치이며 장비 스냅샷 코드는 두 후보에서 동일하다.
- 현재 월드: `a66565fae4bf489b0ec9c119ec31add3`, 시험명 `aiui1106`.
- Gemini GUID: `c1a0de0000000000000000000000a003`.
- Player.log의 참가 인벤토리 기록: `Entity(82315:6)` (`t=163.4`). 이 엔티티 번호는 참가 시점 기록이며 조사 시점의 live entity 재확인으로 승격하지 않는다.
- 현재 로그: `C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/Player.log`.
- 저장 파일: `C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers/a66565fae4bf489b0ec9c119ec31add3/Gemini.json`.
- 설치 `.../StreamingAssets/Mods/AIPlayerSpike/Scripts/Scripts/AIInfoSnapshot.cs`와 작업 트리 파일의 SHA256은 모두 `BCE7FEA7A58920458AF710D47FB297A7C907F54D5780403CCD1A8485D6A2A7F7`. 설치에 다른 스냅샷 코드가 남았다는 근거는 없다.

조사 중 읽은 저장은 130슬롯이다. 비어 있지 않은 칸은 아래 10개뿐이며 10~129는 전부 None이었다. amount는 내구도가 들어가는 장비도 있으므로 수량으로 해석하지 않았다.

| 슬롯 | objectID | 물건 | raw amount |
|---:|---:|---|---:|
| 0 | 10 | WoodMiningPick | 114 |
| 1 | 8302 | SmallLantern | 1 |
| 2 | 9602 | FoodRation | 3 |
| 3 | 8909 | WoodSword | 88 |
| 4 | 8601 | WoodBreastArmor | 75 |
| 5 | 8501 | WoodHelm | 45 |
| 6 | 8016 | RootSeed | 1 |
| 7 | 8701 | WoodPantsArmor | 70 |
| 8 | 300 | WallDirtBlock | 21 |
| 9 | 1500 | CopperOre | 5 |

저장 mtime 관측: 20:35:59. 현재 로그의 `t=518.4`, `578.4`, `638.4`, `698.4` 서버 주기 저장도 방어구 위치 4·5·7을 반복해서 기록한다. `AICharacterStore.SaveFromServer`는 서버 ContainedObjectsBuffer 전체를 순서대로 저장하므로 실제 보유/슬롯 위치의 증거다. 그러나 active preset/EquipmentCD 자체는 저장하지 않으므로 이 파일만으로 착용 프리셋 전체를 확정하지 않는다.

## 원게임·모드 경로 대조

### 실제 제작 후 처리

- `AICraft.cs:153-178`: 제작 결과인 커서 슬롯을 `Create.MoveOrDropItem(..., player, 0, outIdx, ...)`로 일반 가방에 넣는다.
- `AICraft.cs:494-497`: StowCursorItem 이후 커서가 비면 제작을 Done으로 끝낸다. 여기에는 방어구 장착 슬롯으로 옮기는 동작이 없다.
- 이번 Player.log `t=444.3`(행715): WoodHelm을 `moving it into the bag` 처리.
- `t=451.7`(행776): WoodPantsArmor를 같은 방식으로 처리.
- `t=471.3`(행841): WoodBreastArmor를 같은 방식으로 처리.
- 모드 Scripts 전체에서 EquipmentCD/helmSlotIndex/breastSlotIndex/pantsSlotIndex 접근은 현재 AIInfoSnapshot 외에는 발견하지 못했다. secondary 입력 호출 경로도 음식/배치/농사 등이며, 방어구를 선택해 원게임 EquipGearSlot을 실행하는 전용 동작은 찾지 못했다.

### 원게임의 착용과 표시

- `Pug.Other/EquipGearSlot.cs:12-36` 및 마지막 Create.Swap: 방어구를 손에 선택하고 secondary 사용하면 EquipmentCD의 해당 착용 슬롯과 인벤토리 슬롯을 교환한다. 제작만으로 이 경로가 실행되지는 않는다.
- `Pug.Other/PlayerEquipment/SelectedEquipmentChangeSystem.cs:579-586`: ClientInput.equipmentPresetIndex로 ActiveEquipmentPresetCD를 갱신한다.
- 같은 파일 `:871-874`: `equipmentCD = equipmentPresetsBuffer[activeEquipmentPresetCD.Value].equipment`.
- `Pug.Other/SummarizeConditionsSystem.cs:211-245`: 방어구/목걸이/반지 등의 효과를 EquipmentCD가 가리키는 ContainedObjectsBuffer 칸에서 수집한다.
- `Pug.Other/EquipmentHandler.cs:42-55,71-83`: 게임 UI도 활성 프리셋의 슬롯을 InventoryHandler 시작 위치로 삼는다.
- `Pug.Other/InventoryHandler.cs:107-135`: 시작 슬롯+오프셋의 실제 ContainedObjectsBuffer 물건을 읽는다.
- `EquipmentHandler.GetActiveVisibleArmorObjectIDs`는 VanitySlotsHandler도 참조한다. 화면 외형과 능력치 장비를 대조하려면 vanity도 확인해야 한다. 사용자 외형 관측을 저장 파일의 일반 가방 정보로 덮어쓰지 않는다.

### 현재 패널

- `AIInfoSnapshot.cs:27-49`: 활성 프리셋의 9개 착용 슬롯과 EquippedObjectCD의 손 슬롯을 읽고, 각 슬롯의 물건을 이름으로 바꾼다.
- `AIPlayerSpikeMod.cs:190-191`와 `AIAgents.cs:65-70`: 선택한 AIAgent의 상태에서 서버 엔티티를 가져온다. Gemini 인덱스는 2다. 모든 AI가 Claude 컨텍스트를 읽는 식의 명백한 선택 매핑 오류는 확인되지 않았다.
- `AIInfoPanel.Frame`은 선택 멤버만 0.5초마다 새 스냅샷으로 교체한다. 예외는 새 빈 상태로 바꾸며 로그를 남긴다. 조사한 로그에서 `AI-UI snapshot failed`는 발견하지 못했다.
- 현재 코드가 유효하지 않은 슬롯 인덱스와 정상 빈 슬롯을 모두 ‘없음’으로 표시하는 점은 진단을 막는다. raw 슬롯의 유효성을 모르는 상태를 착용 없음으로 확정하는 표시를 구별해야 한다.

### 착용 증거로 사용할 수 없는 기존 자료

- `AIExperience.cs:136-149`의 armorLv는 **전체 인벤토리**에서 가장 높은 방어구 레벨을 구한다. `AIExpCore.cs:163`의 ‘방어구 레벨 2’는 현재 착용을 뜻하는 데이터가 아니다. 이 문구를 장비 UI 정확성 검증으로 사용하지 않는다.
- 기존 `08-gemini-equipment.png`는 제작 전 20:27:32 화면이다. 이후 착용의 정확성을 검증하는 스크린샷이 아니다.
- 테스터는 20:33 stop 직후와 20:34 재관찰에서 손은 나무 곡괭이, 머리/상의/하의는 없음으로 관측했다고 전달했다. 이것은 테스터 관측 진술이며 동일 순간 raw ECS의 직접 덤프는 아니다.
- `19-user-observed-equipment-mismatch.png`도 열어 확인했다. 사용자가 패널을 닫은 상태이고 Gemini가 어두운 곳에 있어 이 이미지 하나로 착용/미착용 또는 패널 불일치를 확정하지 않는다.

## 구현자에게 전달한 수정·확인 제안

1. **다음 허용된 빌드에서 읽기 전용 진단을 추가한다.** 선택된 member/GUID, world, server entity(index/version), active preset, 모든 preset의 armor 슬롯, 현재 EquipmentCD의 슬롯, 각 슬롯→objectID/variation/amount, EquippedObjectCD의 containedObject와 slot, vanity 슬롯/물건, 최종 UI 문자열을 같은 스냅샷으로 기록한다. 최초 선택 또는 값 변경 때만 기록해 매프레임 로그를 피한다. 이 진단을 위해 추가 모델 호출이나 캐릭터 변경은 필요 없다.
2. **빈 장비와 읽기 실패를 구분한다.** 유효한 실제 슬롯에서 objectID=None일 때만 ‘없음’을 표시한다. 누락 컴포넌트·invalid preset·out-of-range는 ‘확인 불가’와 구체 로그로 남겨 빈 슬롯처럼 숨기지 않는다. 손은 EquippedObjectCD.containedObject와 인벤토리 슬롯을 함께 대조한다.
3. **실제 raw 차이로 수정 방향을 결정한다.** EquipmentCD와 active preset이 다르면 현재 적용값/갱신 시점을 해결한다. 둘이 같고 올바른 착용 슬롯에 아이템이 있는데 UI만 None이면 스냅샷/선택/캐시 경로를 수정한다. 방어구가 일반 가방에만 있으면 보유와 착용 상태를 명확히 보여주고, 실제 착용 동작 추가는 별도 작업 범위로 정한다. 가방에서 발견한 방어구를 곧바로 ‘착용 장비’ 행에 대신 넣지 않는다.
4. **실제 검증에는 비어 있지 않은 착용 슬롯이 필요하다.** 같은 Gemini에서 장착→해제→다시 장착의 raw 슬롯과 UI를 비교하고, 슬롯이 비어 있는 상태만으로 장비 표시를 통과 처리하지 않는다. 헤드/상의/하의 및 손, 선택 AI 전환, 프리셋 변경, 0.5초 갱신을 함께 확인한다. 사용자가 직접 플레이하는 현재 회차에는 이 조작을 수행하지 않았다.

조사 중간 사실과 위 진단 제안은 HEAD 및 구현자 `integrate_ai_ui`에게 전달했다. 테스터 `ui_live_verify`에게 현재 자료 위치/관측을 받아 대조했다. 현재 상태는 **원인 후보 구체화·raw ECS 증거 부족으로 최종 원인/완료 판정 보류**다.

최종 중단 조건: HEAD가 physicalEscape로 Computer Use가 중단됐음을 전달했다. 이후 추가 CU·스크린샷·다른 경로 UI 호출은 수행하지 않는다. 사용자의 명시적 재개 전에는 동일 시점 raw 런타임 검증도 보류한다. 게임/bridge/시험 설치는 그대로 유지하며 원인 미단정 상태로 이 조사를 마무리한다. 이후 raw 장비 진단을 포함한 새 코드 검수는 별도 followup에서 수행한다.

## 추가 조사 — 사용자 외형 확인과 두 데이터 원천

사용자는 “채팅 내용과 게임 속 캐릭터 모습을 보면 장비 착용한 게 맞는데 AI 장비창에 안 뜬다”고 추가로 확인했다. 이 진술은 실제 외형을 포함하므로, 보유 아이템이나 AI 발언만 확인하고 종료할 수 없다. 아래는 코드만 추적한 결과이며 실제 런타임 차이가 확정됐다는 뜻은 아니다.

### 캐릭터 외형 → 클라이언트 원천

디컴파일 기준 경로는 `C:/Dev/_ref/corekeeper-decomp/Pug.Other/`다.

1. `EntityMonoBehaviour.cs:348,350`: 렌더링 객체의 world는 `Manager.ecs.ClientWorld`이며 해당 객체의 entity를 사용한다. 서버 엔티티 번호나 AI thin-client 월드로 읽는 경로가 아니다.
2. `PlayerController.cs:3740-3755`: 원격 플레이어도 자신의 client entity/world로 EquipmentHandler와 VanitySlotsHandler를 생성하고 ActiveEquipmentPresetCD를 적용한다. non-local 조기 반환은 그 뒤다.
3. `PlayerController.cs:1593-1600,3846-3854`: 원격 플레이어도 활성 프리셋 변경을 읽고 EquipmentHandler의 슬롯을 갱신한다. 따라서 “원격 캐릭터의 프리셋 갱신이 전혀 없다”는 가설은 코드와 맞지 않는다.
4. `PlayerController.cs:1548-1563`: ManagedLateUpdate에서 로컬/원격 모두 UpdateAnyNewCustomization 뒤 UpdateGearCustomization을 실행한다.
5. `PlayerController.cs:4408-4468`: 머리·상의·하의 각각 **client VanitySlotsHandler의 치장 물건을 우선** 읽고, objectID=None이면 client EquipmentHandler의 실제 장비 물건으로 대체한다. 치장 슬롯의 amount<0이면서 None이면 숨김 플래그이며, 해당 부위는 장비가 있어도 보이지 않는다. 유효한 물건에서 EquipmentSkinCD가 있고 파손되지 않았으면 그 skin 주소를 선택한다. 그 외에는 Empty다.
6. 선택된 세 주소는 `activeCustomization.helm/breastArmor/pantsArmor`로 반영된다. `PlayerController.cs:4630-4669`의 RefreshCustomization은 이 주소에 해당하는 helmSkin/breastArmorSkin/pantsArmorSkin을 로드하거나 비운 뒤 렌더러를 끈다.
7. `PlayerController.cs:4486-4502`: 초기 및 trigger 변경 때는 client PlayerCustomizationCD도 activeCustomization을 공급한다. 같은 LateUpdate 안에서 장비 경로가 이어지므로, 네트워크 customization 주소만 기록하면 최종 보이는 외형을 완전히 설명하지 못한다. 최종 activeCustomization 주소를 함께 남겨야 한다.

### 패널 → 서버 원천

현재 실행 `2762495`의 패널은 선택된 AIAgent의 ServerPlayerOf(member)를 통해 ServerWorld 엔티티를 읽는다. `AIInfoSnapshot.cs:27-49`는 서버 ActiveEquipmentPresetCD → EquipmentPresetsBuffer[active].equipment → ContainedObjectsBuffer[slot] → 이름/‘없음’ 순서다. 치장 슬롯이나 PlayerController의 최종 외형 주소는 표시 원천이 아니다.

따라서 두 경로는 의도된 의미도 다르고(외형/치장 대 능력치 장비), 읽는 월드도 다르다. 실제 장착이 서버에 있는데 패널만 누락되는 경우, client 치장만 보이는 경우, client 장비 복제/프리셋/캐시가 서버와 다른 경우를 현재 저장 자료만으로 구별할 수 없다. **‘사용자가 착용을 잘못 봤다’ 또는 ‘치장 때문에 생긴 문제다’라고 판정하지 않는다.**

### AI 생성 시 호스트 장비 복사 가설

- `AICharacterStore.cs:97-101`의 CreateServerCopy는 AI CharacterData를 JSON 왕복으로 깊게 복사한다.
- `AICharacterStore.cs:118-127`의 신규 AI 생성은 호스트와 다른 외형을 고른 뒤 helm/breastArmor/pantsArmor 주소를 명시적으로 Empty로 설정한다.
- `AIPlayerSpikePatches.cs:107-143`는 서버 spawn 요청의 playerGuid에 해당하는 AI를 찾아 자신의 CharacterData로 교체한다.
- 실제 첫 시험 로그의 `t=158.3`은 들어온 호스트 characterGuid를 Gemini 자신의 `c1a0de0000000000000000000000a003`로 교체했다고 기록한다. `t=158.4` spawn에는 server Entity(82315:6), playerIndex=2, playerGuid=`8bc92592511c027f446f91659fd21e7d`가 기록되고 expected GUID match=true가 이어진다.

이 코드는 초기 호스트 방어구 주소를 그대로 상속했다는 가설의 우선순위를 낮춘다. 이후 client 동기화/외형 갱신 문제까지 배제하는 증거는 아니다. character 저장 GUID와 PlayerGhost.playerGuid는 서로 다른 식별자이므로 진단에서 혼용하면 안 된다.

### 동일 시점 진단에 필요한 대응

- 선택 AI → server entity → **PlayerGhost.playerGuid**를 먼저 기록하고, 호스트 ClientWorld에서 같은 PlayerGhost.playerGuid의 entity를 찾아 대응시킨다. 서버 entity index/version을 다른 월드의 entity로 그대로 쓰지 않는다.
- 서버·클라이언트 각각 active preset, 현재 EquipmentCD 및 모든 프리셋의 head/top/legs 슬롯, 그 슬롯의 objectID/variation/amount를 기록한다.
- client VanitySlotsCD의 세 슬롯과 실제 물건/숨김값, EquipmentHandler가 실제 사용하는 시작 슬롯을 기록한다. 원시 프리셋과 handler 캐시가 다른지도 구별할 수 있다.
- client PlayerCustomizationCD의 trigger/방어구 주소와 PlayerController.activeCustomization의 최종 세 주소를 같이 기록한다. 가능하면 세 armor renderer의 활성 상태도 읽어 최종 외형 적용까지 연결한다.
- 같은 진단 행에 패널의 최종 문자열과 선택 member를 남긴다. 유효성 실패는 ‘없음’과 분리한다. 이 비교 전에는 UI가 참조할 데이터를 임의로 가방 아이템이나 외형 아이템으로 대체하지 않는다.

### 재개 상태와 새 화면 증거의 한계

HEAD는 사용자 명시 재개를 전달했고 공유 UI 소유자는 테스터다. 독립 조사자는 계속 code/log read-only만 수행했다. 테스터는 `evidence/live/22-gemini-equipment-and-avatar.png`에서 Gemini 외형과 장비 패널(손 나무 곡괭이, 머리/상의/하의 없음)을 한 화면으로 확보했다고 전달했다. 이 보고에는 client/server raw ECS가 아직 없으므로 외형이 어느 슬롯에서 비롯됐는지 확정하는 자료로 승격하지 않는다. 독립 조사자는 이 추가 조사에서 CU·스크린샷·게임 입력·설치를 수행하지 않았다.

구현자와 HEAD에 위 두 원천 및 구체 조회식을 전달했다. 서버 진단 후보와 추가 client GUID/치장/최종 skin 진단의 런타임 근거가 도착하면 원인을 재평가한다. 현 판정은 **사용자 관측을 미해결 결함으로 유지, 두 데이터 경로까지 추적 완료, root cause 미확정**이다.

## 최신 raw 대조 — 134b1c3 재입장 t=97.4

테스터가 사용자 재개 승인 후 동일 월드/동일 캐릭터로 재입장했다. 재입장 뒤 추가 AI off/on 없이 확보한 현재 Player.log 행294의 `AI-UI equipment source`를 독립 조사자가 직접 읽었다.

- 서버 Entity(79010:1), 활성 프리셋0: 현재 EquipmentCD와 selected preset 모두 head51/chest53/legs54=`None x0`.
- 서버 치장: head66/chest67/legs68=`None x0`.
- PlayerGhost.playerGuid=`8bc92592511c027f446f91659fd21e7d`와 일치하는 ClientWorld0 Entity(24593:2): current/preset0의 방어구 및 치장 슬롯도 모두 None.
- 실제 PlayerController의 EquipmentHandler 및 VanitySlotsHandler가 반환한 head/chest/legs 물건도 모두 None.
- 최종 activeCustomization의 세 armor address 모두 `{m_low:0,m_high:0}`.
- 손은 서버 슬롯과 heldContained 모두 WoodMiningPick x114로 맞고 패널의 나무 곡괭이와 일치한다.

`ScriptableData/DataBlockAddress.cs:23-26`의 Empty는 Guid.Empty다. 따라서 기록된 0/0 주소는 **기본 방어구 스킨이 아니라 방어구 스킨 없음**을 뜻한다. 기본 머리·옷·바지는 별도 customization 필드다. `Pug.Base/PlayerCustomization.cs:9-35`는 hair/shirtSkin/pantsSkin과 helm/breastArmor/pantsArmor를 분리하고, 생성자 :43-72는 기본 옷·바지를 선택하면서 세 armor 주소만 Empty로 둔다.

`PlayerController.RefreshCustomization:4520-4629`는 기본 머리·셔츠·바지를 각각 독립적으로 읽어 그린다. 세 armor가 Empty이면 :4630-4669의 armorSkin Clear 및 renderer disable 경로를 따른다. 그러므로 **이번 raw로 확인한 모습은 기본 캐릭터 외형 경로이며, 방어구 아이템을 실제 착용한 상태는 아니다.** 외형을 만드는 실제 handler와 최종 주소까지 대조한 판단이다.

확인한 제작 로그·가방 위치4/5/7과 이 결과를 함께 보면, 이번 재현에서는 나무 방어구를 제작·보유했으나 착용하지 않았고 패널이 그 상태를 정확히 표시했다. 장비 UI 결함이라는 확정 표현은 이 증거에 따라 철회한다. 치장/클라이언트 복제 불일치도 이번 동일 시점에는 없었다.

한계: 최초 사용자 관측과 이번 raw 사이에 게임 정상 종료·재입장이 있었다. 따라서 최초 순간의 외형까지 완전히 같은 상태였다고 소급 단정하지 않는다. 테스터의 `31-diagnostic-equipment-avatar.png`와 과거22 화면이 유사하다는 전달은 보조 관측이며, 독립 검수자는 새 CU나 화면 캡처를 수행하지 않았다. 현재 장비 데이터 정확성 조사와 비어 있지 않은 실제 착용 슬롯의 양성 기능 검증은 구분한다. 보유 장비 표시 추가나 AI 자동 착용 행동은 이번 판단으로 범위를 확대하지 않는다.
