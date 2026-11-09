# 생활 빈 선택지 구현 결과

2026-11-08. 합의: [agreement.md](agreement.md)의 생활 절 전부와 상자 공용 중 "근처 상자 재료로 바로 제작". 점검 근거: [life.md](life.md).

- worktree `C:\Dev\CoreKeeperModSDK-gap-life`, 브랜치 `gap-life-20261108`, 기준 `20b1ace`.
- 공식 체크아웃과 다른 worktree는 건드리지 않았다. 실게임·설치·공식 병합·독립 검수는 하지 않았다.

## 커밋

| 커밋 | 내용 |
|---|---|
| `a875d93` | 요리: 비대칭 솥 채우기, 같은 재료 보충, 입력 칸 빼기(M3). 상위 brief의 fill 설명을 갱신 |
| `af26b70` | 농사: 빈 판 땅 미리 물주기(M5), 괭이 없이 쳐서 수확(M6), 씨앗 뿌리개 심기(M7) |
| `b669fbf` | 제작: 근처 상자 재료(공용, 호스트·다른 AI 포함)로 제작. 기억 시설 레시피 누출을 고치고, 가축은 제작대에서 제외 |
| `b62df0b` | 휴식: 생활 루트에 `rest` 갈래 추가, 침대에서 자기(M2) |

## 항목별 구현

### M3 솥 입력 정리 — 완료 (`AICook.cs`)

- **채우기(`fill`)**
  - 한쪽 입력만 찬 솥이면 빈 입력 칸에 넣는다. 입력0이든 입력1이든 같다. 예전에는 입력1만 받았다.
  - 두 입력이 다 차 있으면, 같은 재료가 든 칸에 보충한다. 수량은 다른 칸 개수, 가방 재고, 칸 공간으로 제한한다.
  - 위치: `ExactCandidate`의 fill 분기(`bool topUp`, :681 부근). `Selection.TargetSlot`을 새로 둔다.
- **새 모드 `clear`**
  - item 단계에서 `slot0`/`slot1`을 고르면, 그 입력 칸의 재료를 정상 UI 이동으로 가방에 옮긴다. 가방 증가량과 입력 칸 감소량이 모두 확인되어야 완료로 친다.
  - 위치: 항목 목록(:555), 후보(:649), 실행(`CookSkill`의 `_sentClear`, :945).
- **채우기 확인 방식**: 실제 추가량을 `(목표 칸 현재 수 − 선택 시점 수) + 자동 생산량`으로 계산한다(:984). 생산이 동시에 진행되어도 맞게 센다.
- **상위 계획 형식**: `clear`는 생활 트리 안에서만 고를 수 있다. 상위 LLM의 목표 형식 `AILifeGoalForm`은 바꾸지 않았다. `Tools/ai-bridge/brief.md`의 fill 설명만 새 의미로 고쳤다.

### M5 빈 판 땅 물주기 — 완료 (`AIFarm.cs`)

- water의 item 단계에 `ground`("작물 없는 판 땅만")를 추가했다. `all`도 빈 판 땅을 포함한다.
- 대상 판정은 `Eligible`의 water 분기다(:622). 작물이 있는 칸은 `ground`에서 빠진다. 이미 젖은 빈 땅도 빠진다.

### M6 괭이 없는 수확 — 완료 (`AIFarm.cs`)

- **후보**: 쓸 수 있는 괭이가 없으면 harvest 말단으로 `AttackHarvestCandidate`(:806)를 준다. `Selection.Attack`을 켠 후보다.
- **실행(`FarmSkill`)**
  - `AIBody.WeaponSlotForAttack`으로 고른 슬롯을 든다.
  - BreakSkill과 같은 방식으로 primary를 조준해 누른다(`Aimed`, `Attacking`).
  - 작물이 사라지거나 다 자란 상태가 아니게 된 칸만 작업으로 센다. 그래서 농사 목표 진척에 그대로 들어간다.
  - 근거: 게임의 공격 경로도 `PlayerController.OnHarvest`를 부른다(`PlayerController.cs:5922`).
- **준비 경로**: `ToolTargets`가 수확만을 이유로 괭이 제작을 요구하던 것을 없앴다.

### M7 범위 농사 도구(씨앗 뿌리개) — 완료 (`AIFarm.cs`)

- **선택 흐름**: plant에서 종자를 고른 뒤, 가방에 씨앗 뿌리개가 있으면 `second` 단계가 생긴다. 고를 수 있는 값은 `hand`와 `seeder:<도구>`다. 목표 형식의 second는 비어 있으므로, plant 목표의 진척에 그대로 합산된다.
- **게임 규칙**: `SeederSlot.PlaceItem`은 가방 앞쪽의 첫 심을 수 있는 종자를 쓴다.
  - 그래서 그 첫 종자가 고른 종자와 같을 때만 후보를 낸다(`SeederCandidate`, :782, `FirstSeederSeed` :776).
  - 선택 문구에 실제로 쓰일 종자를 밝힌다.
- **실행**
  - 도구 슬롯을 든다. 같은 실행 안에서 앞서 누른 범위 때문에 심긴 대상 칸도 이번 작업으로 센다(:1256).
  - 종자 슬롯은 선택 시점 상태를 따로 보관하고, 시작할 때 바뀌었는지 확인한다.

### 화면 밖 기억 시설 레시피 누출 — 완료 (`AICraft.cs`)

- `PrefabManual`(:67)로 기억한 시설의 prefab `CraftingCD`를 확인한다. 이 확인은 품목 목록, 장소 목록, "가서 확인" 이동 후보 세 곳에 모두 적용된다. 그래서 기억한 화로나 솥의 자동 가공 출력은 더 나오지 않는다.
- `ManualType`(:63)은 가축(`CraftingType.Cattle`)도 제작대로 보지 않는다. 화면 안의 소가 우유 제작대로 잡힐 가능성(life.md 3절)을 막는다.

### M2 침대에서 자기 — 완료 (새 파일 `AIBed.cs` + `.meta`)

- **선택지 구성**
  - 생활 루트에 `rest` 갈래를 추가했다(`AILife.cs`, `Core/AILifeTree.cs`의 갈래 허용 목록).
  - 실행 kind는 `sleep`이다(`AIActions.cs`).
  - 보이는 침대 가운데 빈 침대와 내 침대를 선택지로 낸다. 남이 차지한 침대는 고를 수 없고, "게임 규칙상 잘 수 없음"이라는 사실로만 보인다.
- **실행(`SleepSkill`)**
  1. 침대 근처로 이동한다.
  2. 잠자리로 직접 올라선다. 잠자리 위치는 침대 위치에 침대 방향별 `OccupiableCD` 오프셋을 더한 곳이다. 게임의 `Sleep.GetOffsetAndFacingDirectionFromOccupiable`와 같은 계산이다.
  3. 가만히 서 있는다.
- **결과 판정**
  - `PlayerStateCD`의 Sleep 상태가 관측되면 잠든 것으로 확인한다.
  - 끝나는 조건: 체력 가득, 60초 경과, 게임이 깨움.
  - 잠자리에 5초 서 있었는데 잠들지 않으면 실패로 끝내고 이유를 보고한다(근처 적 등).

### 근처 상자 재료로 제작 — 완료 (`AICraft.cs`, `AIBrain.Prep.cs`)

- 게임 서버의 `Craft`는 재료를 가방에서 먼저 쓰고, 이어서 제작대 10칸 안의 `InventoryAutoTransferEnabledCD` 엔티티를 가까운 순으로 최대 20개까지 쓴다. 소유자는 따지지 않는다.
- 같은 집합을 계산해 다음 세 곳에 반영했다.
  - 가능 판정과 부족량(`ReadRecipe`)
  - 실행 전 상자 슬롯 변경 감지(`Stale`, `AICraftSelection.ChestMaterials`)
  - 소비 확인(`CraftSkill`의 before/after)
- 선택 문구에 상자가 대는 양을 적는다(예: "근처 상자 1개의 재료도 게임이 씀: Wood 1").
- 준비 경로의 부족 사실(`AIBrain.Prep.cs:117`)도 같은 집계를 쓴다.

## 제작용 상자 판정 함수 (통합 시 가방 쪽 판정과 맞출 곳)

- `AICraft.CraftChests(World, Entity player, Entity station)` — `AICraft.cs:88`.
  - 기준점: 제작대 위치. 손 제작이면 플레이어 위치.
  - 대상: `InventoryAutoTransferEnabledCD`, `ContainedObjectsBuffer`, `InventoryBuffer`가 있는 엔티티. 플레이어와 PlayerGhost는 뺀다.
  - 10칸 이내를 가까운 순으로 최대 20개(`CraftChestRange`, `CraftChestMax`).
  - 소유 판정은 없다. 모든 상자가 공용이다.
- `AICraft.MaterialCount(World, player, station, ObjectID)` — `AICraft.cs:122`. 가방 수량과 위 상자들의 수량을 더한다.
- **통합 주의**
  - 이 집합은 게임 서버 `Craft`의 상자 집합을 그대로 따른다. 그래서 자동 이체 표지가 있으면 솥 같은 시설 인벤토리도 포함될 수 있다(게임 규칙과 같음).
  - 가방 포크가 만드는 "공용 보관 대상 상자" 판정은 목적이 다를 수 있다(시설 제외 등). 그대로 합칠지는 통합 때 정한다.
  - `AIBase.cs` 상단 주석의 "own chests only", "mod는 가방만 계산"은 이번 결정으로 낡았다. 다만 가방 포크가 AIBase를 고칠 가능성이 있어 이번에는 손대지 않았다.

## 시험 결과 (모두 `C:\Dev\CoreKeeperModSDK-gap-life`에서 실행)

| 시험 | 기준선(20b1ace) | 변경 후 |
|---|---|---|
| compile-check(게임 어셈블리) | 오류 0 | 오류 0 |
| life-cook-tests | 51 통과 | 70 통과(M3 19개 추가) |
| life-tests(농사·생활 파이프라인) | 105 통과 | 129 통과(M5·M6·M7 16개, 침대 8개 추가) |
| life-craft-tests | 29 통과 | 38 통과(상자 공용·기억 시설 9개 추가) |
| maintenance-tests | 115 통과 | 115 통과(루트 갈래 수 기대값 5→6 갱신) |
| life-inventory-tests | 171 | 171 |
| execution-tests | 181/181 | 181/181 |
| core-tests | 1571 | 1571 |
| restriction-tests | 47(`prepare.mjs` 선행 필요) | 47 |
| gates.sh(유니티 제외) | — | failed gates 0. memgraph는 KG 데이터가 없어 건너뜀 |
| Unity 배치 컴파일 | — | **미실행**: CoreKeeper.exe가 실행 중이었음. 규칙상 돌리지 않음 |

시험 어댑터 변경 내역:
- `Tools/life-tests`: `FarmStubs`의 ObjectType에 Seeder 추가, CanHold·WeaponSlotForAttack 경계, normalize, HealthCD.maxHealth. 새 `BedCases.cs`.
- `Tools/life-craft-tests`: CraftingType에 Cattle 추가, 기억 시설 목록을 설정 가능하게 함.
- `Tools/maintenance-tests`: AIBed 스텁, Cattle 추가.

## 미구현·추정·통합 주의점

- **추정 — 실게임 확인 필요**
  - 씨앗 뿌리개의 실제 범위 크기와 조준점 기준 배치. 시험 fixture는 2x2를 가정했다.
  - 씨앗 뿌리개가 쓰는 첫 종자를 장비 칸 말고 main 가방 순서로 읽은 것.
  - 손 칸으로 바꿔 들 때 종자 순서가 바뀔 가능성.
  - 침대 충돌 때문에 잠자리에 실제로 올라설 수 있는지. ClaimBedSystem이 서버의 AI 플레이어에게도 돌아가는지.
  - 쳐서 수확할 때 무기 종류에 따른 타격 수.
  - 상자 거리를 상자 중심으로 쟀다. 게임은 콜라이더 기준이라 10칸 경계에서 조금 다를 수 있다.
- **넣지 않은 것**
  - `clear` 모드의 상위 계획 형식, 솥 비대칭 상태의 목표 경로 자동 제안(`AICook.Candidates`). 트리에서는 고를 수 있다.
  - 침대의 상위 목표 형식, 화면 밖 기억 침대.
- **겹침 주의**
  - `AILife.cs`의 루트 갈래와 `AIActions.cs`의 kind 표는 다른 포크도 고칠 수 있는 공유 지점이다.
  - 이동전달-개선4건(move-deliver)의 4/4가 가방 메뉴를 바꾸지만, 이 브랜치는 가방 메뉴 파일을 건드리지 않았다.
- **시험 실행 메모**: 추출 스크립트(`ExtractLifeBinding.ps1`, `ExtractBase.ps1`)를 돌리면 생성 파일의 줄바꿈만 바뀐다. 커밋 전에 되돌렸다.
