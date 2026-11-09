# 가방·장비 단계1 독립 검수 — 수정 필요

2026-11-07. 검수 대상은 `d8f2084b3ee3d2703376693ff1d122cbaafcb43a`, 비교 기준은 `11eb461ae122f404acec94563c086b09e002987c`다. 생산자가 진행 중인 worktree는 수정하지 않았다. 경로 존재 여부를 확인한 뒤 별도 detached worktree `C:/Dev/CoreKeeperModSDK-bag-review-s1`를 만들었다. 제품 소스 변경은 없고 독립 재현용 `review-repro/`만 추가했다.

**판정: 수정 필요.** P1 두 건과 P2 두 건을 확인했다. 기존 Core 1177건·protocol 118건·빠른 컴파일 통과는 독립 재실행으로 확인했지만, 실제 실행 중 개별 손 장비 보존과 기존 품목 지정 보관 호환성을 보장하지 않는다. 이 판정은 오프라인 코드/어댑터 검수 결과다. 설치·Unity·게임·외부 모델 호출은 실행하지 않았다.

## 적용한 기준과 근거

- `C:/Dev/AGENTS.md`, HEAD `start-job/SKILL.md`를 읽고 독립 읽기/검수로 범위를 제한했다. 작업 기록 도구·공식 그래프는 변경하지 않았다.
- 이 폴더의 `agreement.md`, 상위 폴더 `2026-11-07-가방장비-계획안.md`·`조사.md`, `2026-11-07-공통실행자료/contract.md`를 대조했다. 목적·계획은 상위 LLM, 구체 선택은 Decisions, 코드는 정상 입력·실제 관측을 담당한다.
- `work/지식그래프/README.md`가 가리키는 `C:/Dev/_ref/head-agent-core/graph/build/07-qa.md`를 읽었다. 실제 서비스에서 대표 기능을 끝까지 관측하는 QA와 오프라인 검증을 구분한다. 현재 게임 보류 때문에 대표 실게임 경로는 미검증으로 남긴다.
- 생산자가 고정한 `inputs-pinned.json`의 9개 파일은 현재 SHA256과 모두 일치했다. `full/C/gear-rule-v2.json`과 실제 `Pug.Other/EquipGearSlot.cs`, `Inventory/InventoryUpdateSystem.cs`, `Inventory/InventoryUtility.cs`, `ContainedObjectsBuffer.cs`를 읽었다. 원본 규칙의 생산 당시 pending 표기는 이후 independent_pass 수용과 구분하며, `runtimeTested=false`를 실게임 성공으로 읽지 않았다.
- 반지의 자동 대상 선택과 파우치의 첫 빈칸/첫 슬롯 예외는 `EquipGearSlot.cs:49` 이후에 있다. 이번 코드는 자동 우클릭 대상 결정 대신 **AI가 구체적으로 고른 정상 UI Swap**을 사용한다. 따라서 반지 두 칸·파우치 네 칸을 개별 후보로 제시하는 것 자체를 위반으로 판정하지 않았다. 해제는 별도 장착 효과를 추정하지 않고 실제 일반 Swap 소비자와 빈 가방·요구조건·용량 검사를 대조했다.

## R1 — P1: 실행을 시작한 뒤에는 선택한 개별 무기·도구가 바뀌어도 다른 물건으로 입력을 계속 보낼 수 있음

위치:

- `Assets/AIPlayerSpike/Scripts/AIBody.cs:478,557,601,700,746`: 명시 선택 검사는 준비 함수 진입에 추가됐다.
- `AIInventory.cs:329`: `TryChosenHand`는 slot/objectID/variation/aux 및 파손을 확인하지만, 이 확인이 모든 실행 프레임을 감싸지는 않는다.
- `AIAttackSkill.cs:48,114`: Start에서 `_slot`을 얻고, Tick의 공격은 그 슬롯에 `primary=true`를 보낸다. 도중 물품/variation/aux/실제 손 상태를 다시 확인하지 않는다.
- `AIMineSkill.cs:71,128-142,163`: Start에서 슬롯을 고정하고, 이후 접근·장착·반복 채굴에서 저장한 슬롯을 사용한다. 131행의 실제 손 ID는 Facts 문자열에만 들어가며 선택 일치 검사가 아니다.
- `AIShortSkills.cs:144,161-162`: 짧은 공격은 `ObjectID`만 저장·비교한다. 같은 ID의 다른 variation/aux 물품을 동일 개별 무기로 취급한다. `AIBody`의 공통 Tick/입력 송신에도 이를 보완하는 개별물품 검사가 없다.

최소 재현 조건:

1. 핫바 0의 CopperSword variation 0을 hold하여 명시 선택을 남긴다.
2. AttackSkill/MineSkill이 해당 슬롯으로 시작한 뒤, 첫 실제 공격 전 또는 접근 중 그 슬롯을 다른 물품/동일 ID의 다른 variation 물품으로 바꾼다. 파손 상태로 바뀐 경우도 별도로 본다.
3. 긴 공격/채굴 Tick은 기존 `_slot`을 계속 입력한다. 짧은 공격은 같은 ID 다른 variation이면 `WeaponCheck(true, ...)`가 되어 새 물품으로 공격 입력을 낸다.

독립 실행 증거: 아래 harness는 실제 `AIInventory.cs`와 `AIRunGate.cs`를 링크한다. 동일 슬롯을 variation 1로 교체했을 때 `TryChosenHand`는 `slot=-1`과 변경 이유를 반환하지만, 실제 짧은 공격 호출부와 같은 ID 비교를 `HitOnceLogic`에 전달하면 `done=False; next primary=True`였다. **짧은 공격 전체나 Unity를 실행한 결과가 아니라 실제 Core 소비 논리와 호출 인수의 불일치를 재현한 결과**다. 긴 공격/채굴의 반복 입력은 위 실제 Tick 소스 경로를 대조한 정적 발견이다.

수정 요구: 각 실행이 승인받은 손 물품의 식별을 보존하고, 실제 입력 직전에 slot/objectID/variation/aux 및 현재 손 상태를 대조해야 한다. 정상적인 내구도 감소는 허용하되 물품 소실·교체·파손·작업 부적합이면 다른 물품으로 primary를 보내지 않고 사실과 이유를 남겨 재판단하게 한다. 긴 공격, 짧은 공격, 채굴 및 복합 행동 내부 단계에 같은 기준을 적용한다. 새 목표를 만들거나 원래 부탁을 지우는 방식으로 처리하면 안 된다.

## R2 — P1: 기존 품목 지정 Store 입구가 무조건 거부되는 회귀

위치:

- `AIGoals.cs:188-200`: 기존 `/ai goal store Wood` 또는 `/ai goal 보관 Wood`도 새 `AIInventory.CheckGoal`로 보낸다.
- `AIPlanner.cs:797-805`: 기존 상위 응답 `{do:"store", item:"Wood"}`도 새 검사로 보낸다.
- `AIInventory.cs:197`: Equip/Unequip/Hold 이외의 kind를 즉시 `아직 연결되지 않은 가방 동작`으로 반환한다.

최소 재현: 기존에는 보관 가능한 Wood가 가방에 있는 상황에서 위 명령 또는 상위 goal을 사용한다. 새 검사는 실제 자기 상자·보관 가능 품목을 검사하기 전에 Store라는 이유로 거부한다. 독립 harness의 `CheckGoal(... GoalKind.Store, ObjectID.Wood, 0, null, -1, -1)`도 동일 오류를 반환했다. Base의 기존 `Storable`·StoreSkill은 남아 있으나 해당 입구로 도달하지 못한다. item 없는 generic Store는 이 발견과 구별한다.

수정 요구: 새 장비/수량 Store가 아직 없는 단계라도 기존 품목 지정 Store는 기존 검사·실행 경로로 유지해야 한다. 후속 완성판에서는 기존 item-only Store와 generic Store, 새 count/keep/where/slot Store를 각각 검증한다. 이것은 신규 Store/Take/Drop/Give 미완료를 지적한 것이 아니라 **기존 지원 지시의 회귀**다.

## R3 — P2: 특정 핫바 칸을 지명하면 유효한 교체가 있어도 후보가 사라짐

위치: `AIInventory.cs:154-168`. 가방에서 hold할 때 빈 핫바가 하나라도 있으면 빈칸만 targets에 넣는다. 그 다음 where 필터가 적용되므로, 지명한 칸이 점유 중이면 다른 빈칸 때문에 그 칸의 정상 Swap 후보가 없어질 수 있다. 출발 물품이 이미 핫바에 있으면 자기 칸만 targets에 넣어, 다른 핫바 칸을 명시한 경우도 만들지 못한다.

실행 재현: 가방 12번에 CopperSword, 핫바 5번에 Wood, 핫바 0번 빈칸. goal은 `hold CopperSword slot=12 where=hotbar:5`. `CheckGoal`은 accepted, `Candidates.Count`는 0이었다. 목적지 5와 출발지 12의 정상 교체가 가능하지만 모듈이 제시하지 않는다. 가방이 가득 찬 문제가 아니다.

수정 요구: 명시 목적지가 있으면 그 정확한 슬롯의 유효 Swap/hold를 우선 구성·검증한다. 목적지를 지정하지 않은 경우의 빈칸 우선 제시는 별개다. 출발·목적지 모두 핫바인 이동도 같은 명시 목적지 계약을 지켜야 한다.

## R4 — P2: 교체 완료 후 돌아온 이전 장비를 원래 선택의 진행 후보로 다시 제시

위치: `AIInventory.cs:131-147,320-327`. 완료 receipt는 `S.done=true`로 저장하지만 후보 생성은 이를 사용하지 않고 현재 출발 슬롯과 ObjectID만으로 목표를 다시 매칭한다.

실행 재현:

1. 가방 12번에 CopperHelm variation 1/상태100, head 40번에 CopperHelm variation 0/상태50을 둔다.
2. goal `equip CopperHelm slot=12 where=head`에서 생성한 후보를 시작하고 요청을 보낸다.
3. 독립 버퍼에서 정상 교환의 결과를 반영한다. 실제 `InventorySkill.Tick`은 `Done`, `GoalDone=True`가 된다.
4. 같은 목표의 후보를 다시 생성하면 **가방으로 돌아온 variation 0을 head의 variation 1과 바꾸는 progress 후보**가 1개 나온다.

이것은 자동으로 다시 Swap한다는 뜻이 아니다. Decisions가 다음에 받을 진행 후보가 원래 선택을 되돌리는 물품으로 바뀐다는 뜻이다. 사용자가 고른 개별 장비의 완료 receipt와 다음 판단의 후보 의미가 어긋난다. 공통 finish_goal이 아직 없는 사실 자체와 구분한다. 공통 통합 후에도 완료·다음 선택 사이에서 이 역교체가 목표 진척으로 제시되면 대표 흐름을 깨뜨린다.

수정 요구: 동일 목표에서 관측된 한 번의 개별 교체를 완료한 뒤에는 그 출발 칸에 돌아온 이전 물품을 같은 요청의 미완료 대상으로 다시 바인딩하지 않는다. 완료 사실과 finish_goal/다음 판단을 연결하고, 다른 장비를 다시 고르려면 새 구체 선택으로 명확히 구분한다. 동일 ObjectID의 서로 다른 variation 및 서로 다른 내구도 상태로 회귀를 확인한다.

## 독립 검증과 산출물

검수판에서 아래를 실제 실행했다. 로그를 보존하려고 마지막에 동일 판을 한 번 더 실행하여 독립 경로에 저장했다.

| 실행 | 실제 결과 | 독립 증거 |
|---|---|---|
| `dotnet build Tools/compile-check -c Release` | 성공, 경고0/오류0 | `C:/Dev/CoreKeeperModSDK-bag-review-s1/review-repro/compile.log` |
| `dotnet run --project Tools/core-tests/CoreTests.csproj -c Release` | `ALL CHECKS OK (1177)` | `review-repro/core-tests.log` |
| `node Tools/ai-bridge/test/protocol.test.mjs` | `ALL 118 CHECKS OK` | `review-repro/protocol.log` |
| `git diff --check 11eb461 d8f2084` | 종료0, 지적 없음 | 독립 실행 출력 |
| 실제 소스 링크 어댑터 재현 | R2/R3/R4 및 R1의 짧은 공격 인수 불일치 확인 | `review-repro/results.log` |

[재현 프로젝트](C:/Dev/CoreKeeperModSDK-bag-review-s1/review-repro/Review.csproj), [재현 입력](C:/Dev/CoreKeeperModSDK-bag-review-s1/review-repro/Program.cs), [대역과 한계](C:/Dev/CoreKeeperModSDK-bag-review-s1/review-repro/Stubs.cs), [재현 출력](C:/Dev/CoreKeeperModSDK-bag-review-s1/review-repro/results.log).

재현 명령:

```powershell
dotnet run --project C:/Dev/CoreKeeperModSDK-bag-review-s1/review-repro/Review.csproj -c Release
```

이 프로젝트는 제품 `AIInventory.cs`·`Core/AIInventoryRules.cs`·`Core/AIRunGate.cs`를 csproj Link로 직접 포함한다. 대역 World·버퍼·DB·큐를 사용하며, Swap 결과 버퍼 교환은 재현 코드가 직접 반영한다. 게임 상태를 바꾼 것이 아니다. 큐 접수 또는 실제 Unity 소비를 검증하는 시험이 아니다. harness의 `math` 대역 이름 때문에 CS8981 경고1이 있으며, 제품 compile-check의 경고0과 구별한다. owner가 후속판 검증에 재사용하려면 별도 복사 후 Link를 후속 고정판에 맞춰야 한다. 이 검수판을 최신 생산판으로 오해하면 안 된다.

같은 재현으로 확인한 정상 경계:

- 요청 전 출발 물품의 내구도 상태가 바뀌면 stale 이유로 차단한다.
- 커서에 물품이 있으면 차단한다.
- 고장 난 장착 장비는 후보에서 제외한다.
- 양 슬롯이 교환됐더라도 활성 프리셋의 head 슬롯이 달라지면 현재 착용 성공으로 처리하지 않는다.
- 실제 Core 회귀는 양 슬롯 변화 전에는 교환을 관측하지 않고, 한쪽 변화만으로 성공하지 않으며, 같은 요청을 두 번 허용하지 않는 규칙을 확인한다.

## 범위 판단과 검증 한계

- 실제 요청은 `InventorySkill.Tick → AICraft.QueueInventoryChange → UIActionBuffer(tick+1, InventoryChange, Create.Swap)`로 이어진다. 직접 아이템/내구도/능력치 성공값을 쓰는 제품 경로는 추가되지 않았다. 실제 게임 Swap 소비자는 양 슬롯을 교환하고 이후 inventory space를 갱신한다.
- 활성 프리셋/실제 장비 슬롯, 양방향 정확한 물품 상태, 가방 축소·파우치 태그 변경의 자동 드롭 예방 검사가 있다. 다만 전체 prefab·DB·슬롯 요구조건·용량 조합을 Unity에서 실행한 것은 아니다. 슬롯 태그와 ID 조건이 동시에 있는 특수 설정에서 모듈의 조건 우선순위가 게임과 같은지도 후속 확대 시 확인해야 한다(게임은 tags가 설정돼 있으면 해당 결과를 먼저 반환한다).
- 일반 목표의 장비 후보가 앞쪽 반지/파우치 범주에 몰릴 수 있는 생성 제한은 존재한다. owner가 후속판에서 다양성 개선 중임을 받았다. 이 문제를 신규 독립 안전성 발견이나 자동 최적장비 정책의 근거로 확대하지 않았다.
- stale 응답은 기존 `AIBrain.StaleReason`의 goal revision/응답 나이 검사와 inventory의 양 슬롯 `EqualsExact` 재검사에 연결된다. 새 InventorySkill 실패는 원래 AIGoal을 취소하지 않는다. 기존 호출 실패·중단·finish_goal의 전체 정책은 공통 실행 담당의 후속 통합 경계다.
- Take/Drop/Give 및 새 수량/장비 Store 실행 전체가 미완료인 것은 단계1 경계로 남겼다. 상위 brief가 후속 어휘를 소개하는 사실을 완료 증거로 취급하지 않았다. 기존 자기 상자 정책을 바꾸는 diff는 없고 외부 인벤토리 조작은 현재 Stale에서 거부한다.
- 실제 흉갑 선택→Swap→활성 착용/이전 장비 위치→다음 판단, 실제 지정 무기 공격/짧은 공격·도구 채굴 및 파손/교체의 사용자 흐름은 **게임 실행 보류로 미관측**이다. Unity 컴파일도 이번 검수에서 실행하지 않았다. 이 판의 오프라인 통과를 전체 계획 완료 또는 실게임 QA PASS로 기록하면 안 된다.

후속 고정판에서는 R1~R4를 먼저 재검수하고, 확장된 Store/Take/Drop/Give와 공통 완료 연결을 해당 판의 실제 diff에 맞춰 확인해야 한다.
