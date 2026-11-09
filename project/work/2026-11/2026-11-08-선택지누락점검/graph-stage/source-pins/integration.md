# 빈 선택지 3범위 통합 (2026-11-08)

- worktree `C:\Dev\CoreKeeperModSDK-gap-integrated`, 브랜치 `gap-integrated-20261108`, 기준 20b1ace.
- 합친 순서: gap-maint-20261108(bff88f9, fast-forward) → gap-life-20261108(머지 `a854958`) → gap-bag-20261108(머지 `068c7b5`).
- 이동전달-개선4건(move-deliver)은 공식 미반영이라 넣지 않았다. 그쪽 4/4가 `AIInventoryMenu.cs`·`AIInventory.cs`를 고쳐 나중에 충돌이 예상된다([impl-bag.md](impl-bag.md) 통합 주의점).
- 근거: [합의](agreement.md), [impl-bag](impl-bag.md), [impl-life](impl-life.md), [impl-maint](impl-maint.md).

## 충돌 해결

| 위치 | 두 쪽 | 해결 |
|---|---|---|
| `Tools/maintenance-tests/WorldStubs.cs` (생활 머지) | 정비: `PlayerGhost.onlineName`, `PlayerCustomization(CD)` 추가 ↔ 생활: `CraftingType.Cattle` 추가 | 둘 다 보존 |
| `Assets/AIPlayerSpike/Scripts/AILife.cs:130-132` 루트 갈래 (가방 머지) | 생활: `rest` 갈래 추가 ↔ 가방: inventory 문구 "상자(모두 공용)" | 가방 문구 + rest 갈래 |

`AIMaintenance.cs`, `AIActions.cs`, 시험 스텁·`Program.cs`는 자동 병합됐다. `GapCases.cs`는 정비(maintenance-tests)와 가방(life-inventory-tests)이 서로 다른 폴더라 겹치지 않았다.

## 통합 때 고친 것 (머지 커밋 `068c7b5`에 포함)

- `AIMaintenance.cs` `MainHolds`: 회수 수령 확인 검사를 main만이 아니라 `AIInventory.CarriedSlots`(main + 착용 파우치)로 넓혔다. 가방 M4로 파우치 장비도 정비 출발 칸이 될 수 있다. 회수 take의 목적지는 여전히 `Destination(mainOnly: true)`라 main에 떨어지지만, 손실 판정(`ObserveLoss`)이 들고 있는 장비를 '잃음'으로 잘못 보지 않게 했다.
- `AIMaintenance.cs:347` 비용 부족 안내 문구: "자기 상자 꺼내기" → "상자 꺼내기".
- `AIBase.cs` 머리 주석: '자기 상자만'(2026-11-04) → '모든 상자 공용(2026-11-08)', `AIBase.Usable`·`BaseChests()` 예외 명시. 제작은 가방만 센다는 문구 → `AICraft.CraftChests`/`MaterialCount`가 게임과 같은 상자를 센다는 문구.

## 상자 판정 확인

- 접근(보관·꺼내기·빠른 쌓기·메뉴): `AIBase.Usable(World, Entity)` (`AIBase.cs:167`) — 존재하고 보이는 상자면 주인 무관.
- 제작 재료: `AICraft.CraftChests` (`AICraft.cs:88`) — 게임 서버 규칙(`InventoryAutoTransferEnabledCD`, 제작대 10칸, 가까운 순 20개), 주인 무관, 플레이어 제외.
- 두 기준 모두 '모든 상자 공용' 합의와 맞다. 제작 쪽은 게임 규칙상 자동 이체 표지가 있는 시설 인벤토리도 포함될 수 있으며 게임과 같으므로 그대로 둔다.
- `BaseChests()`는 HEAD 결정으로 내가 놓은 상자만 센다(기지 상자 제작 판단용).

## 시험 결과 (통합 HEAD `068c7b5`)

| 시험 | 결과 |
|---|---|
| compile-check | 오류 0 |
| core-tests | ALL CHECKS OK (1571) |
| exp-sim / recall-loop(41) / bridge-wiring(23) / protocol(188) / fake-bridge / logshape / movecheck | OK |
| memgraph | SKIPPED (worktree에 KG 데이터 없음, gates.sh 기본 동작) |
| execution-tests | 181/181 |
| gather-tests | PASS 61 |
| info-tests | PASS |
| life-cook-tests | 70 passed, 0 failed |
| life-craft-tests | 38, FAILED 0 |
| life-inventory-tests | 217, FAILED 0 |
| life-tests | 129 passed, 0 failed |
| maintenance-tests | 144 passed, 0 failed |
| path-preparation-tests | ALL SCHEDULER CHECKS OK (25) |
| travel-tests | PASS |
| restriction-tests | ALL PASS 47 (prepare.mjs 선행) |
| action-input-tests | PASS 43 (`run.mjs`로 실행; `dotnet run` 직접 실행은 생성 파일이 없어 빌드 실패 — 시험 방식 문제, 코드 문제 아님) |

gates.sh: failed gates 0.

## Unity 컴파일

통과: `HeadlessSdk.CompileCheck` exit 0, error CS 0, "CompileCheck: ok" 1회 (로그 `C:\Dev\CoreKeeperModSDK-gap-integrated\Logs\compile-gapint.log`). 처음에는 CoreKeeper.exe가 돌고 있어 기다렸고, 꺼진 것을 확인한 뒤 실행했다. 컴파일 환경 파일(ConditionsTable.asset(.meta), Packages manifest/lock, ProjectSettings 2개, .vsconfig, Assets/Plugins/CoreKeeper)은 `CoreKeeperModSDK-gap-maint`에서 복사했고 커밋하지 않았다.

## 남은 위험

- 실게임·실제 모델 미검증(기존 방침: 치트 환경 이후 묶어 검증). 각 impl 문서의 '추정' 항목(파우치 실제 수납, 착용 칸 직접 내려놓기, 펫·꾸미기 칸 교환, 근처 상자 빠른 쌓기 범위 근사, 악기, 씨앗 뿌리개 범위, 침대 점유·수면 처리, 상자 거리 기준)이 그대로 남는다.
- 정비 비용 재료 계산(`AIMaintenance.Main`)은 여전히 main 가방만 센다. 게임 대장간이 파우치·상자 재료를 쓰는지 확인하지 않았다(기존 동작 유지).
- `AIBase.cs`·`AICook.cs` 안쪽의 개별 함수 주석에 'own chest' 표현이 남아 있다(동작 문구 아님).
- move-deliver(이동전달-개선4건) 통합 시 가방 메뉴 충돌.
