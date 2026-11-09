# 가방·장비 feature 고정판 독립 재검수 — 수정 필요

2026-11-07. 대상은 `bed7ef9334ff290d95d9a5aae4c14901b827805f`, 기준은 `11eb461`, 이전 검수판은 `d8f2084`다. 경로 존재 여부를 먼저 확인한 뒤 `C:/Dev/CoreKeeperModSDK-bag-review-feature`에 detached worktree를 만들었다. 생산자의 코드·Unity 작업은 건드리지 않았으며 검수판 제품 소스도 수정하지 않았다. 독립 재현 파일만 `review-repro/`에 추가했다.

**판정: 수정 필요.** 이전 네 지적의 수정은 확인했다. 새 전달 완료 판정의 거짓 성공(P1)과 slot-only 장비 보관의 무상자 준비 누락(P2)을 실제 제품 소스 링크/메서드 추출 재현으로 확인했다. 별도로 가득 찬 상자의 비중첩 장비 공간 오판을 코드 대조에서 확인했다. owner에게 즉시 전달했으며, 수정 중이라는 답은 받았지만 이 고정판의 발견을 해소하는 증거로 사용하지 않았다. 공통 실행 통합판의 최종 SHA는 아직 받지 않았다.

## 독립 실행 결과

| 검증 | 실제 결과 | 독립 로그 |
|---|---|---|
| `dotnet build Tools/compile-check -c Release` | 경고0/오류0, 종료0 | [compile.log](C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/compile.log) |
| `dotnet run --project Tools/core-tests/CoreTests.csproj -c Release` | 1195건 통과, 종료0 | [core-tests.log](C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/core-tests.log) |
| `node Tools/ai-bridge/test/protocol.test.mjs` | 118건 통과, 종료0 | [protocol.log](C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/protocol.log) |
| 생산자의 실제 AIInventory 소스 링크 adapter 사례를 독립판으로 복사·고정 | 30건 통과, 종료0 | [producer-cases.log](C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/producer-cases.log) |
| 독립 경계 추가 후 전체 adapter | **52건 중 2건 실패, 종료2** | [extended-results.log](C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/extended-results.log) |
| `git diff --check 11eb461 bed7ef9` | 종료0, 지적 없음 | 독립 실행 출력 |

로그 보존을 위해 마지막에 같은 고정판의 컴파일·Core·protocol을 한 번 더 실행했다. 생산자 로그 수치만 옮긴 것이 아니다. adapter의 `math` 대역 이름에서 CS8981 경고1이 발생하며 제품 compile-check 경고0과 구별한다.

재현 명령:

```powershell
dotnet run --project C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/Review.csproj -c Release
```

[Review.csproj](C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/Review.csproj)는 이 고정판의 `AIInventory.cs`, `AIHandLease.cs`, `Core/AIInventoryRules.cs`, `Core/AIHeldChoice.cs`를 직접 링크한다. [ExtraCases.cs](C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/ExtraCases.cs)는 독립 추가 조건이다. 준비 경계는 [ExtractBase.ps1](C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/ExtractBase.ps1)로 고정판 `AIBase.cs`의 `Storable`·`ChestTarget` 메서드를 그대로 추출한 [BaseSourceSlices.cs](C:/Dev/CoreKeeperModSDK-bag-review-feature/review-repro/BaseSourceSlices.cs)를 사용했다. 주변 호출은 대역이다. 메서드 추출을 다시 할 때는 `./review-repro/ExtractBase.ps1`을 먼저 실행한다.

**이 실행은 Unity/게임 소비자를 실행한 것이 아니다.** World/DB/길찾기/큐는 최소 대역이고, 실제로 일어날 버퍼 변화는 시험 입력이 수동 공급한다. 소비자의 의미는 별도로 게임 원본 코드를 읽어 대조했다. 설치·Unity·게임·기존 프로세스 종료·외부 모델 호출·공식 그래프 수정은 수행하지 않았다.

## 이전 네 지적의 확인

| 이전 지적 | 이 판에서 확인한 수정과 범위 |
|---|---|
| 실행 중 개별 손 장비 미검사 | AttackSkill/MineSkill/HitOnceSkill이 매 Tick `AIHandLease.Check`를 호출하고 실제 손이 준비되기 전 primary를 보내지 않는다. 동일 ID의 다른 variation·aux 교체, 명시 장비 파손은 차단되고 동일 장비의 정상 내구도 감소는 허용되는 것을 실제 lease 링크 재현으로 확인했다. 실제 손 슬롯이 다르면 ready=false다. 복합 행동의 짧은 공격도 이 HitOnceSkill을 사용한다. |
| 기존 품목 지정 Store 거부 | Store를 CheckGoal이 수용하고 기존 Base 준비 후보를 유지하되 실제 `store` 동작만 정확 수량 후보로 대체한다. 실제 `Storable`·`ChestTarget` 메서드 재현에서 item=검인 Store는 상자가 없을 때 ChestTarget=Chest였다. 아래 slot-only 경계는 별개다. |
| 지정한 점유 핫바 목적지 누락 | 명시 hotbar:5를 먼저 targets에 넣는다. 가방12의 검, 점유5, 빈0에서 후보가 5를 대상으로 생성됐다. |
| 교체 완료 뒤 역교체 progress 후보 | 같은 목표의 완료 receipt가 있으면 후보를 반환하지 않는다. 같은 ID 다른 variation의 장착 완료 후 후보0을 확인했다. |

일반 목표에서 많은 반지 사이에도 장착·곡괭이 hold·근접무기 hold 범주가 남는 것을 생산자 사례의 독립 재실행으로 확인했다. 모든 가능한 장비가 한 번에 8개 후보 안에 들어간다는 보장은 아니며, 이를 자동 최적장비 정책으로 해석하지 않았다.

## F1 — P1: 소비되지 않은 바닥 물품과 우연한 수령자 증가를 전달 성공으로 처리

제품 위치: `AIInventory.cs:735-749`, `Core/AIInventoryRules.cs:53` 이후.

현재 코드는 추적 floor의 `targetEntity`가 수령자이고 `state==HasBeenPickedUp`이면 `_pickupReceipt=true`를 남긴다. 이후 수령자의 같은 ID/variation/aux 물품 수가 증가하면 Done/GoalDone으로 처리한다. **추적한 바닥 entity의 물품이 실제로 줄거나 사라졌는지는 검사하지 않는다.** `_sawRecipient`와 `_pickupReceipt`를 분리했지만 실제 소비 증명이 추가된 것은 아니다.

실제 게임 소비자 근거: `work/게임원본-상호작용/code/Pug.Other/PickUpItemSystem.cs:1298-1307`는 `Create.MoveOrDropAllItems(entity, target, ...)`를 `InventoryChangeBuffer`에 추가한 직후 `pickUpItem.state=HasBeenPickedUp`를 설정한다. 이 상태는 이동 요청 제출 상태다. `InventoryUtility.cs:590-643,831-844`의 TryMove/MoveOrDrop 후 실제 이동량과 구별해야 한다. 수령자 여유 공간·조건이 바뀌거나 같은 시점의 다른 수령이 있으면 해당 요청의 실제 결과와 수령자 총량 변화가 일치한다고 단정할 수 없다.

독립 최소 재현:

1. 내 가방 Wood10에서 Friend에게3을 주는 후보를 시작한다. 큐는 DropItem 한 번이다.
2. 출발을 Wood7로, 새 바닥 entity203을 Wood3·수령대상Friend·IsBeingPickedUp으로 관측시킨다.
3. 다음 관측에서 entity203의 **Wood3을 그대로 둔 채** 상태만 HasBeenPickedUp으로 바꾸고, Friend 가방에 다른 경로로 들어온 Wood3을 공급한다.
4. 실제 제품 코드는 `skill=Done`, `GoalDone=True`다.

고정 출력:

```text
DELIVERY CONSUMER BOUNDARY: floor=3, receiverGain=3, state=HasBeenPickedUp, skill=Done, goalDone=True
FAIL independent delivery: queued pickup with original floor still full plus coincidental receiver gain must not prove receipt
```

생산자 adapter의 마지막 성공 사례도 floor Wood3이 그대로 있는 상태에서 HasBeenPickedUp·receiver Wood3을 공급하고 성공을 기대한다. 따라서 adapter30 통과는 이 소비 경계를 검증하지 못한다. 최소 대역은 거짓 성공을 재현하는 수단이며, HasBeenPickedUp의 실제 의미는 위 원본 소비자에서 확인했다.

수정 요구: 지정 수령대상 관측과 수령자 실제 증가 외에, 이번 요청으로 추적한 **동일 바닥 물품의 실제 감소/소멸 및 수령 처리 결과**를 함께 관측한다. 요청 큐 상태를 성공 receipt로 사용하지 않는다. 소비 전 우연한 증가·타인 선점·일부 수령·미확인은 구분하고, 미확인 물품을 자동으로 재전송하지 않는다.

## F2 — P2: item 없이 출발 slot만 지정한 장비 Store는 상자가 없을 때 준비 경로를 잃음

제품 위치: `AIInventory.cs:350-358`, `AIBase.cs:285-304,345-353,475-521`, `AIGoalFeatures.cs:9-24`, `AIBrain.Prep.cs:40` 전후.

Store는 item 없이 slot만 지정하는 형태를 허용한다. 실제 ChestCandidates도 이 경우 정확한 출발 장비를 저장할 수 있다. 하지만 기존 상자 준비는 `AIBase.Storable(...Goal.item)`를 사용한다. Goal.item이 None이면 자동 정리 whitelist로 돌아가 장비를 제외한다. 검 한 개만 지정한 slot-only Store에서는 CheckGoal은 통과하지만 `ChestTarget`은 상자 제작 목표를 내지 않고, Base.Candidates의 bag.Count도0이 되어 기존 상자 배치를 내지 않는다. 새 ChestCandidates는 알고 있는 자기 상자가 없는 상태에서 후보0이다.

독립 최소 재현: 가방12에 CopperSword variation2/상태300만 놓고 `Store item=None slot=12 count=1`, 자기 상자 없음, 상자 품목·레시피 준비 가능 조건을 대역으로 둔다. 실제 AIInventory CheckGoal은 accepted지만, 실제 AIBase 메서드 추출 재현은 `Storable=0, ChestTarget=None`이었다. 같은 상태에서 item=CopperSword로 지정하면 ChestTarget=Chest로 통과했다. 장비 조작 자체나 실제 제작·길찾기를 대역에서 실행한 것은 아니다.

```text
PASS independent preparation: source-only equipment store is accepted
PREPARATION BOUNDARY: slot-only Storable=0, ChestTarget=None
FAIL independent preparation: accepted source-only equipment store without a chest must retain chest preparation
PASS independent preparation: named equipment store still reaches existing chest preparation
```

수정 요구: 출발 slot의 실제 선택 물품을 동일 목표의 준비 경로에도 전달한다. 다른 물건으로 원래 부탁을 바꾸지 않도록 바인딩 시점·오래된 슬롯 변화를 함께 검증한다. item-only Store의 기존 무상자 준비는 유지해야 한다.

관련 정적 준비 경계: Base.Candidates는 `m.free==0`인 상자라도 `m.items.ContainsKey(보관품목ID)`가 참이면 여유가 있을 것으로 세어 n을 증가시킨다(`AIBase.cs:485-493`). 동일 ID 비중첩 장비가 든 가득 찬 상자는 중첩 공간이 없다. feature 연결은 이 legacy store 후보를 제거하지만 n으로 억제된 새 상자 배치를 복구하지 않는다. ChestTarget이 상자 제작을 준비하더라도, 실제 상자를 가방에 가진 뒤 배치 후보가 막힐 수 있다. 이는 소스 대조 발견이며 추가 실행 재현52에 포함하지 않았다. stackable·실제 빈칸·현재 조건을 반영해야 한다.

## 새 이동·관측의 확인 범위

- Store/Take는 정확한 출발·목적 스냅샷을 재검사하고 `Create.MoveAmount(... destroyExisting:false)`를 UI 큐에 한 번 제출한다. 바닥 drop 경로를 사용하지 않는다. 실제 게임 TryFindSlotToAddTo/TryMove를 대조했고, 유효한 지정 hint 슬롯이면 그 슬롯을 반환하는 경로가 있다. 대역의 수동 두 슬롯 이동으로 실제 소비자가 실행됐다고 주장하지 않았다.
- 독립 추가 재현에서 Store count5+keep10은 후보5를 만들고, 실제 두 슬롯 변화가3이면 동작 결과3·목표 미완료이며 다음 후보는 잔량2였다. 요청 전 출발 스냅샷 변경도 차단됐다. 출발 감소 없는 목적 증가를 성공으로 세지 않는 Core 규칙은 기존 회귀에 있다.
- 비중첩 검은 count1과 state300/aux9를 구별한다. 목적지 state299는 성공으로 처리하지 않고 state300의 실제 도착과 출발 소멸을 공급한 뒤에만 성공했다. variation·aux·내구도·슬롯 단위 관측과 일반 스택 수량을 구분한다.
- Drop/Give는 목표가 명시됐을 때만 생성하고, `DropItem`을 자기 위치에 제출하며 드로퍼의 즉시 재줍기를 막는다. 게임 DropItem의 비중첩 amount 의미와 EntityUtility.DropNewEntity의 contained/aux 전달을 대조했다. 실제 요청량1이 내구도1을 뜻하지 않는다.
- 다른 플레이어가 해당 바닥 entity를 가져간 경우 Failed/GoalDoneFalse를 독립 재현했다. 바닥이 사라졌지만 소비 결과를 관측하지 못한 경우 성공으로 세지 않는 생산자 사례도 통과했다. F1의 거짓 성공과 구별한다.
- 상자 소유권은 기존 Own 기억과 현재 형식·좌표를 확인한다. 열기/살펴보기 뒤 실제 슬롯 후보를 만들고 원거리 상자는 기억한 위치로 가는 별도 후보를 쓴다. 호스트/타 AI 상자를 허용하는 정책 확대는 확인하지 않았다. 실제 UI 열기나 게임에서의 권한 성공은 미관측이다.
- 각 물품 작업 실패가 원래 AIGoal을 새 목표로 바꾸거나 취소하는 신규 코드는 없다. S.moved/receipt는 동일 목표 객체에 바인딩한다. 상위 목표의 keep/slot 전달과 저장은 실제 프로토콜·계획 큐 경로에 연결되어 있다.

## 공통 실행·실게임 경계와 후속 검수

이 feature 판은 공통 실행 `finish_goal` 통합 전이다. 새 Inventory 목표는 성공/막힘을 note로 남기며 자동 목표 완료를 우회한다. 기존 Store 등 공통 제어와 timeout 정책 전체를 이 판에서 완결됐다고 판정하지 않았다. owner는 후속 공통 통합판에서 3/6초 관측 timeout 강제종료를 관측 사실+대기로 바꾸고 F1/F2와 위 비중첩 상자 준비를 수정 중이라고 전달했다. **수정 약속은 이 판의 PASS 근거가 아니며 최종 고정 SHA에서 별도 확인한다.**

이번 검수는 기존 합의, HEAD start-job 및 지식그래프 README가 가리키는07-QA를 적용했다. QA 지침의 실제 대표 사용자 흐름 확인과 오프라인 검증을 구별한다. 게임 보류로 흉갑 선택→실제 장착·이전 장비 관측→다음 선택, 자기 상자 보급→장비 교체→지정 무기/도구 행동→수량 보관→원래 부탁 지속, 바닥 전달의 실제 수령 흐름은 모두 미관측이다. Unity 컴파일도 이번 검수에서 실행하지 않았다.

후속 공통 통합판에서는 F1/F2, full 비중첩 상자 준비, 기존 네 수정의 유지와 함께 공통 stop/finish/request_plan 입구·원래 부탁·미확인 요청의 재전송 여부를 실제 변경분에 맞춰 재검수한다. 이 판의 Core1195/adapter30 통과로 실제 게임 QA 완료를 선언할 수 없다.
