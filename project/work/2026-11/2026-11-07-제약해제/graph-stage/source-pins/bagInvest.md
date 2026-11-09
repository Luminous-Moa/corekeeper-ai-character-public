# 가방·장비 계획용 사실 조사

2026-11-07. 독립 읽기 조사. 제품 기준 `C:/Dev/CoreKeeperModSDK` HEAD `11eb461`. `C:/Dev/AGENTS.md` 확인, 저장소 안 추가 AGENTS.md 없음. 제품 수정, 게임 실행, 설치, 모델 호출, 다른 대화 메시지 발송 없음. 아래 상세 조사와 그래프 CLI 조회만 수행했다. 계획 결정은 HEAD가 한다.

## 계획을 바꿀 사실

1. **먼저 가진 장비를 골라 입고 바꾸는 흐름을 완성할 수 있다.** 기존 모드에는 equip/unequip 후보·실행 등록이 없지만 게임의 검수된 장착 규칙은 입력→두 슬롯 Swap→장비 조건 재요약을 잇는다. 기존 장비는 출발 슬롯으로 돌아가므로 교체는 새 장비를 입는 것으로 끝나지 않고 이전 장비의 위치까지 관측해야 한다.
2. **손에 들기만 추가하면 선택한 무기가 유지되지 않는다.** 공격 시작은 `WeaponSlotForAttack`을 다시 호출하고, 이 함수는 핫바의 첫 정상 근접무기를 선택한다. 채굴도 `HandSlot`에서 첫 정상 곡괭이를 고른다. 따라서 선택한 정확한 물품을 공격/짧은 공격/채굴 실행까지 전달하거나, 현재 선택 장비를 존중하는 규칙을 함께 연결해야 한다. 기존 '무기 있는데 맨손으로 싸우지 않기' 원칙을 최적 무기 자동 교체 정책으로 확대하지 않는다.
3. **가방 조작의 실행은 일부 이미 있다.** UIActionBuffer로 정상 게임 인벤토리 요청을 보내는 `AICraft.QueueInventoryChange`, 상자 접근·자기 상자 식별·기억·상자 보관·품목/수량 꺼내기를 재사용할 수 있다. 상위 일반 꺼내기 지시, 수량/잔량 보관, 장비 보관 연결을 보완해야 한다.
4. **관측을 ID 합계에서 실제 슬롯·개별 물품으로 보강해야 한다.** 같은 ID의 장비 여러 개는 내구도/variation 등이 다를 수 있다. `InventoryCounts`는 ID별 개수로 합치므로 장비 선택 및 교체·전달 증명에 충분하지 않다. 비중첩 물품의 `amount`는 개수로 읽지 않는다(기존 InventoryCounts도 비중첩 물품을 1개로 센다).
5. **직접 전달은 상자 보관과 별도 신규 경로다.** 현재 give 제품 행동은 없다. 공용/호스트 상자 접근은 기존 소유 제한이 있으므로 재사용한다고 공유 허용을 확대할 수 없다. 직접 전달의 수령 보장/근접·대상/중간 바닥 드롭 의미와 실제 게임 입력 근거를 추가 확인해야 한다. 이번 대표 그래프 조회로 플레이어 간 직접 전달을 증명하지 않았다.

## 최신 그래프를 실제 조회한 범위

원래 정본 `work/게임행동-그래프`는 고정 수집 입력이다. 최근 결과는 후속 통합의 **stage-04/semantic-04, stage04-evidence-v2/replay-1**에 있는 증분 그래프에서 조회했다. 입력 인덱스의 수용 관계와 `target/show/source` 결과를 함께 확인했다. 그래프 전체는 `wholeScopeComplete=false`; 대표 조회의 성공은 전체 최종 통합 검수 완료나 실게임 성공이 아니다. `integration/README.md` 본문은 아직 23묶음 단계를 설명하므로 최신 실제 인덱스·조회와 구분한다.

### 장착 대표

- 대상 `8549:0`, 이름 HiveBoneHelm, source `CAB-2ebcba97367158c9b08f790c25d642c9:-1017010194669319066`.
- 적용 ID `completion:C:equip-gear-swap-v1/application/CAB-2ebcba97367158c9b08f790c25d642c9:-1017010194669319066`.
- `target`은 장착/수리/업그레이드 조건부 적용 세 개를 `independent_pass`로 반환한다. `show`는 장착 슬롯 `helmSlotIndex`, 원본별 predicate, `applies_rule → completion:C:equip-gear-swap-v1`, `accepted_by → acceptance:C-gear-v2`, 장비 역링크 증거로 이어진다.
- `source .../evidence/destination`은 `full/C/gear-sources.json` `/sources/0/destination`의 실제 값 `helmSlotIndex`와 정규화 해시 `e7e3981f0c054f16dfe6def95c7833dede2c1ae7c43e2c4e41e5e97aae8b2f93`를 반환했다.
- 공통 규칙 `show completion:C:equip-gear-swap-v1`도 조회했다. `status=independent_pass`, 생산 당시 `producerStatus=static_completion_pending_independent_review`, `runtimeTested=false`가 함께 유지된다. 생산 당시 pending을 현재 미검수로 읽지 않는다.
- 조건: 유효 상태·입력·쿨다운, 고장 아님, 커서 물품 없음, prefab/ObjectData/DB 선택·지원 슬롯·유효 인벤토리. Ring/Pouch의 자동 대상 선택은 별도 분기다. 요청 소비와 두 슬롯 교환, 잠금 재계산, 장비 조건/세트 보너스 재요약까지 규칙에 있다. 특정 장비의 효과를 모두 같은 값으로 단정하지 않는다.
- **해제는 독립 검수된 별도 행동으로 이번 조회에서 확인하지 않았다.** 일반 인벤토리 이동/Swap 소비자는 있지만, 장착 규칙만으로 임의 장비 해제·특정 반지 슬롯 교체·가방 축소를 모두 보장하지 않는다. 첫 구현에서 일반 장비 슬롯→일반 가방 슬롯의 정상 입력 경로와 요구조건을 따로 확인한다.

### 상자 대표

- 대상 `100:0`, InventoryChest, source `CAB-2ebcba97367158c9b08f790c25d642c9:-7625700015621503986`.
- 적용 ID `completion:C:container-inventory-transfer-v2/application/CAB-2ebcba97367158c9b08f790c25d642c9:-7625700015621503986`.
- `target/show`에서 `independent_pass`, `accepted_by → acceptance:C-container-v2`, 공통 규칙 `completion:C:container-inventory-transfer-v2`, 원본별 슬롯/권한 조건으로 이어짐을 확인했다.
- `source .../condition/sourceSpecificTransferConditions`는 `/sources/88/sourceSpecificTransferConditions`의 세 값 `objectsGetLockedInPlace=false`, `cantAddObjectsToInventory=false`, `canOnlyContainOneItemPerSlot=false`와 정규화 해시 `6611684662c05ab9d8ccc7484d172c08b888296c92c4bc8bf1106f6ba112f75a`를 반환했다. 원본 설정은 6×3, slotRequirements=[]다. 실제 runtime 내용·용량은 별도 관측한다.
- 규칙 `show` 결과: 활성 객체·플레이어/handler/UI·양 inventory/버퍼·slot 요구조건·용량·권한을 충족해야 한다. 일반 입력에서 guest 비관리자 차단, stackable 실제 이동량은 요청량/원본량/용량에 제한, 비중첩 물품은 whole source amount 의미가 있다. `MoveAmount` 요청 처리가 내부 `TryMove` 성공을 보증하지 않으므로 **양쪽 실제 물품/수량 변화로 완료를 판정**해야 한다.
- 수용 범위는 205 source의 이동205+열기180 주장이다. 합산한 서로 다른 물품 수가 아니다. 규칙은 정적 검수이며 `runtimeTested=false`다.

### 재현 명령

읽기 CLI만 실행했다. 아래 `$argsGraph`는 PowerShell splatting용 문자열 배열이며 셸 예약 변수 `$args`를 재사용하지 않는다.

```powershell
$graphPython = 'C:/Users/<USER>/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$integration = 'C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-07-행동결과완결/integration'
$argsGraph = @('-B','-X','utf8',"$integration/semantic.py",'--standard-index',"$integration/stage-04.json",'--semantic-index',"$integration/semantic-04.json",'--output-dir',"$integration/stage04-evidence-v2/replay-1")
& $graphPython @argsGraph target 8549 --variation 0
& $graphPython @argsGraph show 'completion:C:equip-gear-swap-v1'
& $graphPython @argsGraph show 'completion:C:equip-gear-swap-v1/application/CAB-2ebcba97367158c9b08f790c25d642c9:-1017010194669319066'
& $graphPython @argsGraph source 'completion:C:equip-gear-swap-v1/application/CAB-2ebcba97367158c9b08f790c25d642c9:-1017010194669319066/evidence/destination'
& $graphPython @argsGraph target 100 --variation 0
& $graphPython @argsGraph show 'completion:C:container-inventory-transfer-v2'
& $graphPython @argsGraph show 'completion:C:container-inventory-transfer-v2/application/CAB-2ebcba97367158c9b08f790c25d642c9:-7625700015621503986'
& $graphPython @argsGraph source 'completion:C:container-inventory-transfer-v2/application/CAB-2ebcba97367158c9b08f790c25d642c9:-7625700015621503986/condition/sourceSpecificTransferConditions'
```

`build/check/verify`를 새로 실행하지 않았다. 위 기존 staging은 다른 대화가 갱신 중인 자료이므로 구현 착수 때 수용한 입력·graph/index 해시를 고정해야 한다.

## 현재 코드 대조

경로는 `C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/` 기준이다.

| 연결 | 재사용 가능한 부분 / 현재 공백 | 근거 |
|---|---|---|
| 명시적 장착·해제 | 전체 목표/행동 등록에 equip/unequip 없음 | `Core/AIGoalKinds.cs:5,34`, `AIActions.cs:19` |
| 선택 무기/도구 유지 | 핫바 첫 정상 장비 자동 선택. 모델이 고른 개별 무기 인수가 AttackSkill/MineSkill 생성에 없음. 짧은 공격도 같은 자동 함수 호출 | `AIBody.cs:476,596,694`, `AIAttackSkill.cs:48`, `AIShortSkills.cs:140`, `AIMineSkill.cs:71`, `AIActions.cs:23` |
| 손 칸 실행 | `BodyInput.slot`→ClientInput.equippedSlotIndex 경로 있음. 핫바 교체 helper도 존재 | `AIBody.cs:300,596,694`, `AICombat.cs:463`, `AICraft.cs:323` |
| 착용 상태 읽기 | 활성 프리셋과 실제 슬롯을 읽는 UI snapshot 있음. UI 문자열을 행동 상태로 직접 재사용하는 대신 같은 관측 근거를 공유할 수 있음 | `AIInfoSnapshot.cs:55` 이후 |
| 인벤토리 요청 | 서버 tick+1 UIInputAction.InventoryChange 큐 연결 있음 | `AICraft.cs:139` |
| 보관 품목 | whitelist가 도구·무기·방어구를 제외, 품목 명시 예외도 음식만. 목표 검사/실행 모두 같은 필터 | `AIBase.cs:268,288`, `AIPlanner.cs:811`, `AIBase.cs:964` |
| 보관 수량 | StoreSkill(chest,item), 스택 전량 MoveOrDropItem. 요청 횟수 `_sent`를 성공과 가까이 취급하므로 실제 양쪽 변화 확인 보강 필요 | `AIActions.cs:40`, `AIBase.cs:904,978,988` |
| 꺼내기 | TakeSkill(chest,item,count), 기존 MoveOrDropAmount 사용 및 가방 증가 확인 있음. ID별 수량으로 비교하므로 동일 ID의 특정 장비 지명/variation·내구도 선택에는 부족 | `AIActions.cs:42`, `AIBase.cs:1001,1063,1074`, `AIBody.cs:778` |
| 소유 | NoteOpened가 자기 상자가 아니면 거부. 기존 호스트 상자 사용 제한은 유지하는 설계 필요 | `AIBase.cs:142,148,957,1049` |
| 일반 보급 지시 | take는 제작재료/배고픔 음식/농사 씨앗 특수 후보 연결. 상위 take 목표 없음. store count/목적지 일반 의미도 없음 | `Core/AIGoalKinds.cs:34`, `AIBase.cs:518,534,555,585`, `AIFarm.cs:626` |

게임 정상 소비자는 `work/게임원본-상호작용/code/Pug.Other/Inventory/Create.cs:125` MoveAmount, `:181` MoveOrDropAmount, `:223` Swap에 있다. 장착 입력은 `EquipGearSlot.cs:11` 이후에서 지원 타입→대상 슬롯 선택 후 `:98` 이후 Swap 요청. 일반 인벤토리 UI 큐를 재사용할 수 있다는 것과 입력 상태/slot 제약을 모두 충족한다는 것은 다르므로 구현 시 gate를 대조한다. 현재 hand helper는 버퍼 직접 교환을 쓰지만 새 장착·물품 이전은 가능하면 검수된 정상 요청 소비 경로에 맞추는 것이 관측/잠금 결과와 일치한다.

## 공통 실행 접점

[공통 실행 계약](2026-11-07-공통실행자료/contract.md)의 AICandidate/AISkill 경로를 유지한다. 새로운 가방·장비 후보는 정확한 인벤토리/슬롯/물품·대상·수량을 담고 Start 직전에 다시 대조한다. `stop_action`은 몸 행동 중단만, `finish_goal`은 단계 완료, `request_plan`은 상위 재계획이며 공통 제어 동작을 중복 구현하지 않는다. 장비를 입거나 상자에서 물품을 얻은 사실을 원래 부탁 전체 완료로 바꾸지 않는다. 무기 선택·보관 후 현재 목표를 유지하고 다음 현장 판단으로 연결한다.

## 최소 연속 검증 제안

첫 묶음은 **가방에 가진 방어구 두 개 중 AI가 대상을 선택 → 지정 장비칸으로 교체 → 이전 물품이 출발칸에 돌아온 것과 착용 상태 확인 → 원래 작업 계속**로 잡을 수 있다. 무기/도구 선택을 같이 넣을 때는 고른 물품이 실제 공격/채굴에 사용된 것까지 확인한다. 장비 이름·개수만 변한 화면은 충분하지 않다.

다음 묶음은 **자기 상자에서 지정 장비 하나 꺼내기 → 착용 → 이전 장비만 상자에 보관 → 재료는 정한 수량을 남기고 보관 → 다음 작업 재개**로 연결한다. 각 물품의 양쪽 슬롯 전후/실제 수량·메타데이터 보존, 공간 부족·대상 변경 시 실제 사실 보고와 AI 재판단을 확인한다. 상자 이동 요청 실패로 생긴 바닥 드롭을 '전달/보관 성공'으로 취급하지 않는다.

이 조사는 위 흐름을 실행한 결과가 아니다. 해제·가방 확장/축소·반지/파우치 지정 교체·직접 전달은 각각 정상 입력/슬롯 효과 확인이 추가로 필요하다.

## 재사용한 기존 기록

- [기존 가방·상자·장비 누락 조사](2026-11-06-행동누락조사/01-inventory.md)
- [기존 통합 판단](2026-11-06-행동누락조사/03-findings.md)
- [장비 장착 v2 수용](2026-11-07-행동결과완결/judging/20-장비장착v2수용.md)
- [장비 역링크 수용](2026-11-07-행동결과완결/judging/29-원거리9100과장비역링크수용.md)
- [컨테이너 v2 수용](2026-11-07-행동결과완결/judging/30-음식v4와컨테이너v2수용.md)
