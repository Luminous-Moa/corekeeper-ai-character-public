# 농사·보관/꺼내기 독립 검수 — 고정 24c61c2

## 판정과 범위

고정 후보 `24c61c246053237784c057cf97282b9ca0c5d564`, 기반 `1fc89b93edbee31a9001a174581e32568119efc1`에 대한 하위 모듈 검수다. **중요 오류 1건(R2)을 확인했으므로 이 범위는 수정 후 영향 경계 재검수가 필요하다.** 전체 생활 트리 통과·최종 통합·Unity 성공을 판정하지 않는다.

`C:/Dev/AGENTS.md`, HEAD Core `start-job/SKILL.md`, `00-agreement.md`, `01-direction.md`, `02-direction-decision.md`, `03-spec.md`, `03-spec-decision.md`를 읽었다. 생산 변경은 `git show`/고정 커밋 간 diff로 읽었으며, 진행 중인 worktree 변경을 후보 구현으로 간주하지 않았다. 제품 코드·기존 제품 테스트·작업 기록 도구는 변경하지 않았다. 부모의 추가 지시에 따라 결과폴더에 독립 경계표본만 작성했다.

검수 대상은 `AIFarm`/`FarmSkill`, `AIInventory`/`InventorySkill`, 연결에 필요한 `AILife`의 binding/진척, `AIActions`의 stale/Make, 농사 selector 전달과 관련 `AIGoals`/`AIPlanner` diff다. 요리·제작 실행 내부와 전체 brain 조립은 다른 검수 범위다.

## R2 — 다른 물품을 꺼내도 원래 물품 목표의 진척으로 합산됨

중요도: 높음. 명세 6절의 별도 행동 1개·목표 진척 미합산 및 7.4절의 정확 물품 질의/목표 분리를 위반한다.

### 고정 소스 원인

- `Assets/AIPlayerSpike/Scripts/AILife.cs:43`: `Selected`는 선택값이 빈 문자열이면 목표의 지정 품목과 일치한다고 취급한다.
- `AILife.cs:48`, `AILife.cs:64`, `AILife.cs:79`: 상자 커서의 `Item`은 비어 있지만 `MatchesGoal=true`와 원래 목표 참조·잔량을 부여한다. 상자 탐색에는 item 선택 단계가 없고 mode→where→slot만 있다.
- `Assets/AIPlayerSpike/Scripts/AIInventory.cs:234`: `LifeQuery`가 `selection.Item`만 파싱한다. 비어 있으면 원래 목표의 `item`이 전달되지 않아 `query.item=None`이다.
- `AIInventory.cs:279`: 이 질의는 상자 안의 다른 품목 슬롯도 노출한다. 슬롯을 골라도 실제 물품을 읽어 목표 일치 여부를 다시 판정하지 않는다.
- `AIInventory.cs:294`, `AIInventory.cs:417`: 선택 슬롯의 실제 물품과 원래 목표 품목이 달라도 `progressGoal`을 이동 payload에 붙인다. `ExactCandidate`가 원래 빈 Item binding을 그대로 복사한다.
- `AIInventory.cs:628`: `Completed`는 목표 참조가 같다는 이유로 그 물품의 이동량을 원래 목표의 `S.moved`에 더한다.

### 재현 조건과 실제 증거

원래 목표: `take Wood count=1 where=chest:1,0`. 자기 상자를 연 상태에서 슬롯0에 Wood5, 슬롯1에 CopperSword1(상태100)이 있다. 생활 트리에서 inventory→take→해당 상자→슬롯1을 고른다. Item을 고르는 단계가 없으므로 빈 Item 상태가 유지된다.

독립 표본: `review-farm-inventory-boundary/ReviewBoundary.csproj`.

```powershell
dotnet run --project 'C:\Dev\.head\sessions\코어키퍼-AI-캐릭터\work\2026-11\2026-11-07-생활트리구현\review-farm-inventory-boundary\ReviewBoundary.csproj' -c Release
```

종료코드 0. 핵심 출력:

```text
goal=Wood; empty cursor item lists: 출발 슬롯 0: Wood(변형 0, 상태 5, 개수 5) ... | 출발 슬롯 1: CopperSword(변형 0, 상태 100, 개수 1) ...
selected=CopperSword; bound_original_goal=True; MatchesGoal=True; count=1
actual_received=1; received_item=CopperSword; Wood_goal_moved=1; Wood_goal_done=True
REPRODUCED: wrong-item receipt advances and satisfies the original Wood goal.
```

표본은 **고정 생산 AIInventory/InventorySkill 소스**를 직접 컴파일하여 `LifeEntries→ExactCandidate→InventorySkill.Start/Tick→정상 MoveAmount 요청→양쪽 슬롯 변화 확인→Completed`를 실행한다. ECS·버퍼·게임 소비자는 기존 어댑터이며 실제 양쪽 변화는 표본이 공급한다. `AILife`의 실제 응답/커서 처리는 이 표본에서 실행하지 않았다. 빈 Item·`MatchesGoal=true` 입력은 위 고정 `AILife` 정적 연결에 따라 수동으로 제공했다. 따라서 모델 선택·실제 게임 수령의 재현이라고 보고하지 않는다.

이는 조회 노출만의 문제가 아니다. 잘못된 품목을 실제로 수령한 뒤 원래 Wood 목표의 확인량과 목표 충족 사실이 오염된다. 원래 목표 count가 더 크면 다른 품목에도 그 잔량을 적용할 수 있다. 명세상 별도 행동이라면 1개 요청 및 원래 목표 미합산이어야 한다. 수정은 실제 선택 슬롯의 물품과 binding/요청량/목표 참조가 일치하도록 해야 하며, 새 권한 정책을 만들 필요는 없다.

## 정적으로 확인한 주요 계약

- 농사 직접 지정과 트리 말단은 `AIFarm.ExactCandidate`를 사용한다(`AIFarm.cs:684`, `AIFarm.cs:770`). `AIActions`의 farm stale/Make가 선택 payload를 `FarmSkill`로 전달한다.
- 농사 payload는 실제 선택 슬롯·종자/도구 ID/variation/상태, 다중 종자 source 목록, 타일 목록과 전 상태를 보존한다(`AIFarm.cs:730`). stale/Start에서 선택 슬롯과 타일·소유·작물 상태를 확인한다(`AIFarm.cs:741`, `AIFarm.cs:1045`). 선택된 도구/종자를 매번 같은 ID의 다른 슬롯으로 바꾸는 기존 기본 경로와 명시 선택 경로가 분리돼 있다(`AIFarm.cs:1062`).
- 명시 hoe count/where는 NewPlotTiles 수집 시 기존 전체 6칸 제한을 쓰지 않는다(`AIFarm.cs:627`). `offered=min(6, requested, available)`은 실행 묶음 크기이며 남은 요청량을 payload/설명에 유지한다(`AIFarm.cs:728`). 다른 캐릭터의 기존 밭은 땅/자기 plot 필터상 심기·수확 대상에 포함되지 않는다.
- 실제 칸 작업량과 main inventory 확보량은 별도 필드다(`AIFarm.cs:1001`, `AIFarm.cs:1032`). 이미 만족된 칸을 작업 전 건너뛰는 분기는 `_done`을 올리지 않는다(`AIFarm.cs:1147`). 수확의 바닥 드롭을 단순 `_done`으로 가방 확보에 더하는 연결은 보이지 않는다.
- 상자는 자기 기억/현재 관측·열기 후 slot 수집을 거친다(`AIInventory.cs:251`, `AIInventory.cs:264`). source 목록은 전체 유효 슬롯을 수집하고(`AIInventory.cs:277`) 정확 슬롯을 고른 뒤에만 목적지 계산을 한다(`AIInventory.cs:294`). 기존 12개 실행 후보 cap은 source 목록 생성에 적용되지 않으므로 13번째 이후 슬롯 접근을 막지 않는다.
- 보관/꺼내기 실행은 기존 정상 `MoveAmount`와 양쪽 슬롯의 실제 수량 변화를 재사용한다(`AIInventory.cs:767`, `AIInventory.cs:774`). 출발/목적 슬롯 교체는 실행 stale에서 exact 검사한다(`AIInventory.cs:493`).
- 명시적으로 `MatchesGoal=false`인 별도 상자 행동은 `move.goal=null`로 전달되고 Completed의 목표 reference 조건을 통과하지 않는다. 단, **R2는 실제로 다른 물품인데 MatchesGoal=true가 되는 경계**이므로 그 보호가 적용되지 않는다.
- 화면 밖 농사 tile는 가서 재관측하는 경로로 분리하고(`AIFarm.cs:696`), 현재 농사 Snap의 작물/칸 상태는 관측 범위에서 모은다(`AIFarm.cs:472`). 상자 내용은 열기 전 inspect/이동만 제공한다(`AIInventory.cs:264`). 이 검수에서 새 전지적 읽기나 소유 권한 확대는 확정하지 않았다.

## 증거 고정과 확인하지 않은 것

경계표본의 최종 버전은 진행 중인 worktree의 기존 DLL을 참조하지 않는다. `git show 24c61c246053237784c057cf97282b9ca0c5d564:<path>`로 생산 AIInventory/Core 규칙과 기존 어댑터 Stubs/fixture를 결과폴더에 추출해 빌드했다. fixture는 테스트 준비 함수만 추출했으며 생산 정책 로직은 복사해 재구현하지 않았다. SDK는 `dotnet 10.0.403`이다.

고정 Git blob:

| 생산 소스 | 24c61c2 blob |
|---|---|
| AIInventory.cs | `281b719ee468fb7f08ff1719f580264fe15dd6b9` |
| AILife.cs | `e95cc83a3a3662c70d26f869c8b436268d3af851` |
| AIFarm.cs | `cf5c6c92984b6ecde0c4ff551fb5201d22508998` |

추출 `FrozenInventory.cs` SHA256: `A2E0631EA75E916F15B8B78A1F57D8A5B2268807AF5AD7628ACD9926FBB2996D` (LF로 추출, BOM 없음).

독립 실행 assembly: `review-farm-inventory-boundary/bin/Release/net10.0/ReviewBoundary.dll`, 최종 재실행 SHA256 `0537151A686951C7C2EF1271BC9A4A486FF2C786A9B5DA4B02D1805B169A65A0`. 전체 출력은 `review-farm-inventory-boundary/boundary.log`에 남겼다.

기존 `life-tests.log`의 count12→6+3/잔량3, 수확과 가방 확보 분리, 다른 종자의 목표 미합산 표본 및 `life-inventory-tests.log`의 79/0·후반 슬롯·별도 store 목표 불변 결과는 참고했다. 이는 기존 수행 로그 확인이며 본 검수자가 그 전회귀를 다시 수행한 결과가 아니다. 특히 기존 별도 행동 표본은 처음부터 `MatchesGoal=false`인 입력이어서 R2를 검증하지 않는다.

실제 모델/API, 실제 게임 소비자의 정상 입력 처리와 획득, Unity/SDK 컴파일, 최종 통합 커밋, 설치 상태는 이 하위 검수의 확인 증거가 아니다. 진행 중인 수정은 고정 후보의 오류 해소로 자동 인정하지 않으며, 수정 커밋에서 R2 영향 경계의 재검수가 필요하다.

---

## 수정 후 재검수 — 최종 고정 c4a0f5e

재검수 대상은 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`이다. 앞부분은 중간 후보 24c61c2에 대한 역사적 검수 결과이며, **R2는 이 최종 고정판의 코드 및 독립 경계 실행에서 해소를 확인했다. 이 하위 범위에서 새 중요 오류는 확인하지 않았다.** 최종 Unity가 예정된 상태이므로 전체 생활 트리·Unity·통합 완료 판정으로 확대하지 않는다.

### R2 코드 확인

- `AIInventory.cs:280`: 열린 자기 상자의 source 목록을 읽을 때 실제 물품 ID/variation과 슬롯을 먼저 넣고 `AILife.Resolve`를 호출한다. 다른 품목은 별도 행동·1개·목표 미합산으로 설명한다. source 전체 축과 12개 밖 슬롯 접근은 유지한다.
- `AIInventory.cs:298`: 정확 실행은 자기 상자 현재 관측·열림·거리 조건을 충족한 뒤에만 source 물품을 읽는다. 화면 밖/열기 전 내용을 읽어 실행 품목을 해석하지 않는다.
- `AIInventory.cs:304`: 실제 source 슬롯의 유효 범위와 물품 존재를 검사하고, `AIInventory.cs:305`에서 실제 ID/variation을 담아 Resolve한다. **이후에** 요청 충족 여부, LifeQuery의 수량, progressGoal을 결정한다(`AIInventory.cs:306`). 따라서 원래 커서 Item이 비어 있어도 말단 실행의 품목 일치 여부는 실제 물품을 기준으로 다시 계산된다.
- `AILife.cs:68`의 생산 Bind가 Matches를 다시 적용해 다른 품목의 목표 참조를 null, RequestedCount를 1로 정한다. `AILife.cs:76`의 Resolve는 이 Bind를 그대로 호출한다. 바뀐 binding을 candidate.life에도 보존한다(`AIInventory.cs:309`).
- `AILife.cs:60`은 inventory의 미지정/base 목표를 기지 반경 안 자기 상자 선택에만 목표 일치로 처리한다. 먼 자기 상자는 별도 행동으로 남으며 새 소유 권한을 추가하지 않는다.

### 독립 실행: take와 store 2개 영향 경계

새 표본은 `review-farm-inventory-boundary-final/ReviewBoundary.csproj`, 출력은 같은 폴더의 `boundary.log`다. 중간 후보 재현 파일은 보존했다.

```powershell
dotnet run --project 'C:\Dev\.head\sessions\코어키퍼-AI-캐릭터\work\2026-11\2026-11-07-생활트리구현\review-farm-inventory-boundary-final\ReviewBoundary.csproj' -c Release
```

최종 고정판의 AIInventory/InventorySkill, Core 규칙, 기존 어댑터를 `git show c4a0f5e:<path>`로 추출해 직접 컴파일했다. AILife는 동일 최종 고정 소스에서 `Entries` 직전까지의 prefix를 그대로 추출해 **Sync/Matches/Bind/Resolve/ForGoal/Remaining의 실제 생산 코드**를 실행한다. 저장소의 `Tools/life-inventory-tests/LifeBindingSource.cs`도 이 생산 prefix와 마지막 개행 외 동일함을 대조했다. 중간 재현의 수동 `MatchesGoal=true`/목표/요청량 주입 한계가 이 최종 표본에는 없다. 초기 빈 Item 선택도 실제 `AILife.Resolve`로 바인딩한다.

독립 실행은 18개 검사 통과, 종료코드 0이다. 원래 Wood count3 목표에 대해 다음을 take/store 양쪽에서 확인했다.

1. 실제 CopperOre variation2 슬롯은 목록에 별도 행동 1개로 표시된다.
2. 말단은 실제 CopperOre ID/variation, 수량1, MatchesGoal=false, life.Goal=null, inventory.goal=null이다.
3. 정상 MoveAmount 요청 수량은1이다. 양쪽 슬롯 변화로 Ore1 이동을 확인해도 원래 Wood 진척은0이며 미완료다.
4. 이어 실제 Wood 슬롯을 선택하면 원래 목표의 잔량3과 목표 참조가 복원된다. 실제 양쪽 Wood3 이동을 관측했을 때만 Wood 진척이3이 된다.
5. take의 ConfirmedReceived는3이고 store의 ConfirmedReceived는0이다. 두 작업의 ConfirmedWork는 각각3으로 보고된다.

최종 출력:

```text
FINAL mode=take; OreRequested=1; originalWoodMoved=3; confirmedWoodWork=3; confirmedWoodReceived=3
FINAL mode=store; OreRequested=1; originalWoodMoved=3; confirmedWoodWork=3; confirmedWoodReceived=0
FINAL BOUNDARY PASS: take and store actual identity -> production Resolve -> separate Ore1/Wood0 -> matching Wood3/Wood3.
```

ECS·큐·실제 이전/이후 버퍼 변화는 어댑터다. 전체 모델/트리 응답 처리나 실제 게임 소비자를 실행한 증거는 아니다. 기존 생산 상자 시험의 91/0 로그와 LifeCases의 후반 슬롯 Ore1→Wood0→Wood3 및 별도 store 표본도 코드와 대조했다. 기존 생활 시험 96/0 로그에는 6칸 묶음/전체 요청량·종자 교체·부분 수량·수확/가방 분리·다른 종자의 목표 미합산 회귀가 남아 있다. 이 기존 전체 시험은 본 재검수에서 반복하지 않았다.

### 농사 소수 변경 검수

`24c61c2→c4a0f5e`의 AIFarm 전체 diff를 검토했다.

- SeedsInChests가 식용 여부 대신 확인된 종자 규칙을 적용한다. 기존 자기 상자의 마지막 열기 기억을 읽으며 현재 화면 밖 상자 버퍼를 새로 읽지 않는다.
- ToolTargets는 명시 hoe/harvest와 water/fill에 필요한 도구를 준비 대상으로 삼는다. 실제 명시 실행의 선택 도구 고정·stale·Start 경로는 변경하지 않았다.
- 명시 상위 where가 있는 hoe도 전체 6칸 제한과 분리한다. 실제 몸 실행의 최대6칸 묶음과 요청 잔량은 유지한다.
- plant/water/harvest 후보는 현재 dug ground 여부를 추가로 검사해, 자기 밭 기억만 남고 땅이 바뀐 칸을 실행 후보로 삼지 않게 한다.
- 종자 꺼내기·야생 종자 준비 후보 복원은 mode 미지정 유지관리 분기에만 있다. 명시 mode는 앞에서 ExactCandidate 결과를 반환하므로 명시 작업을 다른 씨앗/야생 수집으로 대체하지 않는다. 기존 자기 상자·ForeignCrop 경계를 재사용한다.

이 변경들은 선택 전달/재검증·6칸 묶음·화면 밖 지식·소유 권한의 기존 계약을 깨는 새 중요 오류로 확인되지 않았다. 전체 농사 어댑터를 추가 재실행하지 않았으며, 농사 판단은 고정 diff 정적검수와 기존 96/0 증거 대조 범위다.

### 최종 증거 고정

| 생산 소스 | c4a0f5e Git blob |
|---|---|
| AIInventory.cs | `63988f437ec38f81b75aadb3da59482e5e095ab7` |
| AILife.cs | `af62adee2cff42e7a7babe5c18f15739e4457c82` |
| AIFarm.cs | `f8f58b3de65d34fbb9852a7fe297f3b979496990` |

- 최종 추출 FrozenInventory.cs SHA256: `33E9AEB160B3A657C89D9B6AB476A414D8533310B4AC9418B56E986F47813CAB`.
- 최종 추출 FrozenLifeBinding.cs SHA256: `4580096A58676EFEED1848B9821C4CF6F6A11E1CC876EEB3492639A9AE65B98D`.
- 독립 `review-farm-inventory-boundary-final/bin/Release/net10.0/ReviewBoundary.dll` SHA256: `AD118DDD1B2B552474834D1DBD80D8A88E4B2CDE3103828B4DC0C322C3E02ACD`.
- 실행 SDK는 dotnet10.0.403이다. 생산 세 파일과 inventory test의 worktree 변경 없음도 확인했다.

제품 코드 수정·기존 전회귀 반복·설치·게임 실행은 하지 않았다. 최종 Unity 및 다른 하위 검수의 완료 여부는 HEAD가 별도로 종합한다.
