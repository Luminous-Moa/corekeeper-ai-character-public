# 가방·장비 실행 부품 독립 검수 — 선택 트리 전환 전

2026-11-07. 최신 검수 고정판은 `1fc89b93edbee31a9001a174581e32568119efc1`이다. 공통 실행 기준 `685dde4` 위의 전체 가방·장비 변경과 `b4cbef7 → 77484ec` 관측 대기 변경을 확인했다. 이전 상세 검수 대화가 복원된 것이 아니라, 기존 기록을 읽어 시작한 새 독립 검수다.

**현재 판정: 1fc89b9의 실행 부품 P2 해소 확인. 기존 평면 선택지의 최종 수용·패키징은 보류.** 이전 단계1 네 지적과 feature 판 두 실패는 해소됐고, 77484ec에서 새로 발견한 최대 스택 상자 준비 P2도 1fc89b9에서 해소됐다. 사용자가 물건 우선 선택 트리(인벤토리→물건→조작→슬롯/수량/대상→실행), 뒤로/전체 선택 취소 방향을 새로 지정했으므로 이 보고서를 새 사용자 흐름의 완료·최종 수용 근거로 쓰면 안 된다. 새 트리 고정판의 독립 검수는 별도 대기한다. 아래 결과는 오프라인 코드·실제 소스 링크 검수이며 실게임 QA 완료 판정이 아니다.

## 독립성·대상과 실제 실행

- `C:/Dev/AGENTS.md`, HEAD `start-job/SKILL.md`, 합의·계획·조사·상세 명세·이전 두 검수 보고서와 지식그래프 README의 QA 지침을 읽었다. 기존 모델·추론 강도 지정은 현재 조건으로 재적용하지 않았다.
- 생산 작업트리 상태를 먼저 읽고, 존재하지 않던 `C:/Dev/CoreKeeperModSDK-bag-review-final`에 detached `b4cbef7` worktree를 만들었다. 고정 SHA를 받은 뒤 `77484ec`, 후속 P2 수정 고정판 `1fc89b9`로 순서대로 이동했다. 생산 제품 소스는 수정하지 않았다. 검수 worktree의 변경은 `review-repro/`뿐이다.
- 고정 입력 `inputs-pinned.json`의 9개 해시는 모두 일치했다. 게임 원본 `PickUpItemSystem.cs`, `Inventory/Create.cs`, `InventoryUtility.cs`의 해당 소비 경로를 직접 대조했다. 전체 게임 그래프의 수용이나 runtimeTested 범위를 새로 확대하지 않았다.

| 검증 | 독립 실행 결과 | 근거 |
|---|---|---|
| 최종판 fast compile-check | 경고0/오류0 | `C:/Dev/CoreKeeperModSDK-bag-review-final/review-repro/final-compile.log` |
| Core 회귀 | 1201 통과 | `review-repro/b4-core.log`; 최종 변경에서 Core와 Core 시험 입력의 diff가 없음을 확인 |
| bridge protocol | 118 통과 | `review-repro/b4-protocol.log`; 최종 변경에서 bridge/protocol diff 없음 |
| 실제 공통 목표·제어 입구 | 36/36 통과 | `review-repro/final-execution.log` |
| 실제 Inventory/Hand 소스 링크 및 Base 메서드 추출 | **1fc89b9: 67건 모두 통과** (77484ec: 64건 중1건 실패 보존) | `review-repro/77484ec-adapter.log`, `review-repro/final-adapter.log` |
| diff whitespace 검사 | 지적 없음 | `git diff --check 685dde4 1fc89b9` 종료0 |

실행 명령은 `dotnet run --project review-repro/Review.csproj -c Release`, `dotnet build Tools/compile-check -c Release`, `dotnet run --project Tools/core-tests/CoreTests.csproj -c Release`, `node Tools/ai-bridge/test/protocol.test.mjs`다. 공통 입구는 UTF-8 기본인 `pwsh -NoProfile -File Tools/execution-tests/run.ps1`로 실행했다. Windows PowerShell 5의 기본 디코딩으로 생성 코드의 한글이 깨진 첫 실행은 실패했고, 같은 제품 소스에서 pwsh 재실행이 통과했다. 제품 컴파일 오류로 취급하지 않았다.

`Review.csproj`는 검수판 `AIInventory.cs`, `AIHandLease.cs`, `Core/AIInventoryRules.cs`, `Core/AIHeldChoice.cs`를 직접 링크한다. `ExtractBase.ps1`은 고정판의 `Storable`, `ChestTarget`, `MayStackStored` 본문을 추출한다. 마지막 메서드의 접근자만 시험 호출을 위해 public으로 바꾸고 `ChestMem` 타입 별칭을 붙였다. World/DB/큐/길찾기·주변 호출은 대역이다. 양 슬롯 교환과 바닥 소멸·수령 증가 등 결과 상태는 fixture가 수동 공급한다. **이 입력 공급은 Unity 게임 소비자가 성공했다는 증거가 아니다.** adapter의 `math` 이름 경고는 제품 compile-check 경고0과 구별한다.

## 이전 발견의 해소

| 항목 | 최종판에서 확인한 증거 |
|---|---|
| 실행 중 손 장비 변경 | Attack/Mine/HitOnce의 Tick이 `AIHandLease.Check`를 호출하고 실제 손 준비 전에 primary를 허용하지 않는다. 동일 ID 다른 variation·aux, 명시 물품 파손 차단과 정상 내구도 감소 허용을 링크 재현했다. 복합 동작의 짧은 공격은 같은 HitOnce 경로다. |
| 기존 named Store 회귀 | named Store가 수용되고 기존 Base 준비 경로를 유지한다. 장비 지정 Store의 `Storable`/`ChestTarget` 실제 추출 메서드로 확인했다. |
| 점유 hotbar 목적지 | 다른 빈칸이 있어도 명시 hotbar:5의 Swap 후보가 존재한다. 실제 교환 양쪽과 손 상태 뒤 명시 손 선택을 보존한다. |
| 완료 뒤 역교체 후보 | 같은 ID 다른 variation의 장비 교환 완료 뒤 같은 목표 후보는0이다. 돌아온 이전 장비를 다시 진행 대상으로 바인딩하지 않는다. |
| Give 거짓 성공 F1 | floor=3 그대로, receiverGain=3, HasBeenPickedUp이면 **Running/GoalDoneFalse**다. 정상 성공 fixture는 지정 pickup 요청 관측 후 동일 floor의 실제 소비도 공급하도록 고쳤다. 바닥 미소비를 성공으로 기대하던 이전 fixture는 새 계약에 맞지 않아 그대로 PASS 근거로 쓰지 않았다. |
| slot-only Store 무상자 준비 F2 | 실제 `AIGoals.SetGoal` 링크 입구의36번째 검사가 slot12의 흉갑 ID를 목표에 바인딩함을 확인한다. adapter Goal helper도 그 확인된 바인딩을 반영한 뒤 실제 `Storable`/`ChestTarget` 추출로 상자 준비를 확인했다. adapter가 실제 SetGoal 전체를 실행했다는 뜻은 아니다. |
| 가득 찬 상자 속 같은 비중첩 장비 | 실제 추출 `MayStackStored`가 false여서 이전 비중첩 ID의 중첩 공간 오판은 해소됐다. 아래 최대 중첩 수량 경계도 후속 고정판에서 해소됐다. |

## P2 — 최대 수량 상자 준비 누락: 77484ec 발견, 1fc89b9 해소

제품 위치: `AIBase.cs:313`의 `MayStackStored`, `:493`, `:504`, `:520` 전후, `AIGoalFeatures.cs:17` 전후.

`MayStackStored`는 기억에 같은 ID가 있고 그 물품이 stackable이면 true다. 실제 상자에 남은 중첩 공간이 있는지는 검사하지 않는다. 현재 보이는·이미 열어 본 상자의 모든 슬롯이 Wood9999이고 free=0이어도 true가 된다. Base.Candidates는 해당 상자를 기존 store 후보로 세어 n을 증가시키므로 n==0인 새 상자 배치 분기에 들어가지 않는다. 통합 연결은 이 기존 store 후보를 제거한다. 정확 이동 후보는 실제 공간이 없어0이다.

독립 재현은 `review-repro/FinalCases.cs`에 있다. 자기 상자8칸을 모두 Wood9999로, 기억을 opened/free0로, 자기 가방을 Wood10+예비 Chest1로 두고 Store Wood5를 선택했다. 실제 Inventory 후보 생성과 실제 Base helper를 실행한 결과:

```text
FULL STACK PREPARATION BOUNDARY: chest slots=8, every Wood=9999, free=0, exact moves=0, MayStackStored=True, spare Chest in bag=Chest
FAIL independent full-stack preparation: completely full maximum stacks must not suppress the spare chest placement route
```

전체 Base.Candidates를 대역에서 실행한 것은 아니다. helper와 정확 Inventory 후보는 실제 소스 실행 근거이고, 그 결과가 n++ 및 place 억제로 이어지는 것은 위 실제 호출부 대조 근거다. owner와 HEAD에 즉시 전달했다. 화면 밖의 과거 기억을 실제 현재 공간으로 단정하라는 요구가 아니다. 현재 보이는 상자의 실제 유효 슬롯·중첩 잔량을 준비 경로에도 일치시키도록 요구했다.

수정판 `1fc89b9`은 `AIInventory.HasStoreRoom`이 실제 이동 후보와 같은 Destination 검사를 사용하고, `AIBase.MayStoreKnownChest`가 보이는·열어본 상자에 이를 적용한다. visible 상자를 n++로 세는 조건, 화면 밖 상자 경로 조건, n==0 이후 새 상자 배치 차단 조건의 세 호출부가 같은 helper를 사용함을 실제 diff에서 확인했다. 새 helper 본문도 독립 추출해 실행했다. full9999는 false/정확 이동 후보0, 한 스택9998은 true/정확 이동 수량1, occupied one-item 제한은 false/후보0, 화면 밖 과거 기억은 가서 확인할 가능성을 유지했다. 기존 재현을 포함한 최신 adapter는67/0이다. 로그의 FULL STACK 줄은 새 helper를 호출하지만 기존 출력 필드명 MayStackStored를 유지하므로 메서드명으로 오해하지 않는다. 전체 Base.Candidates 실행·실게임 상자 배치를 새로 증명한 것은 아니다.

## 통합 관측·요청·후보 범위

- 정상 요청은 `InventorySkill → AICraft.QueueInventoryChange → UIActionBuffer(ServerTick+1, InventoryChange)`다. Swap, Store/Take의 `MoveAmount(...destroyExisting:false)`, 자기 위치 `DropItem(self,slot,count,selfPos,self)`를 사용한다. 상대 가방 직접 주입이나 성공값 직접 쓰기 경로를 추가하지 않았다.
- Store count5/keep10에서 실제3 이동은 동작 결과3, 목표 미완료, 다음 잔량2다. Take도 양쪽 실제 일치 변화가 필요하다. 비중첩 검1개의 state300/aux9와 목적 state299는 구별되고 정확 상태 도착 전에는 완료하지 않는다. 출발 snapshot 변경은 요청 전에 차단한다.
- Give는 추적된 새 floor, 지정 수령 요청, 동일 floor 감소/소멸, 일치 물품 수령 증가를 함께 요구한다. HasBeenPickedUp가 소비 전 큐 상태라는 게임 원본 의미와 일치한다. 타인 선점은 성공이 아니며, 귀속 미확인도 자동 재전송하지 않는다. floor 병합/생성/소멸을 관측하지 못하면 성공 대신 미확인으로 남을 수 있다.
- 3초 Swap/손·이동,6초 수령,30초 접근은 관측 사실을 알리고 선택을 유지한다. 독립 지연 회귀에서 요청을 한 번만 보낸 채 Running을 유지하고, 늦은 실제 교환/손/수량 관측 뒤 Done이 되는 것을 확인했다. 타인 선점·stale 슬롯·활성 프리셋 불일치 같은 실제 변경 실패는 별개다.
- 실제 공통 입구는 수량 충족/장비 receipt를 관측으로 남기고 목표·실행·원래 부탁을 자동 종료하지 않는다. `stop_action`은 몸만, `finish_goal`은 현재 단계만, 원래 부탁의 done은 상위 답변이 소유한다.36개 입구 재현에서 이 구분과 늦은 stop의 run 불일치 차단을 확인했다.
- 일반 가방 후보는 equip·곡괭이 hold·근접 hold·unequip 범주와 서로 다른 출발 물품을 우선해8개를 남긴다. 명시 Inventory 목표는 요청 조건으로 필터한다. 전체 비전투14 cut은 별도이며 continue 복원·wait·제어 후보 후첨 때문에 최종 총수14를 뜻하지 않는다. 모델은 `{value,description}` 목록에서 개별 물품/슬롯 조작을 고른다. 장비 후보 설명에는 ID·변형·상태·개수·양 슬롯·교체 대상이 있고, 방어력·효과·세트 스탯의 전후 비교표는 현재 없다. 전체 그래프를 실시간 프롬프트에 붙이는 경로는 확인되지 않았다.
- 사용자 중간 질문을 위한 흉갑·곡괭이·검·자기 상자 샘플은 `review-repro/Samples.cs`, 로그의 SAMPLE 줄에 있다. 이는 실제 소스가 가정한 최소 World 상태에서 출력한 후보 원문이며 실게임/전체 모델 입력 로그가 아니다.

## Unity와 미검증 경계

검수자는 Unity를 실행하지 않았다. owner의 `final-unity-exit.json` exitCode0/종료 시각과 `final-unity.log`의 AIPlayerSpike 컴파일·후처리 산출물을 읽었다. `final-unity-input.json`의 sourceCommit은77484ec이며100개 C# 해시를 독립 검수 worktree와 대조해 불일치0이었다. 환경/APIUpdater/asmref 경고는 별도로 존재한다. 최신1fc89b9은 그 뒤 두 제품 소스가 바뀐 판이므로 이 로그로 최신판 Unity 컴파일을 선언하지 않는다. 이는77484ec의 Unity 컴파일 증거에 한정하며 후속 소스 변경으로 자동 승계하지 않는다.

설치·게임·자동조작·기존 프로세스 종료·외부 모델 호출·공식 그래프 쓰기는 하지 않았다. 흉갑 선택→실제 Swap/착용/이전 장비 위치→다음 판단, 지정 검/도구 행동, 자기 상자 수량 보급→교체→행동→잔량 보관→원래 부탁 지속, 실제 바닥 전달 수령의 대표 사용자 흐름은 미관측이다. `runtime-plan.md`가 준비한 실게임 확인을 완료하지 않은 상태이며 정적 검수/컴파일 통과를 실게임 QA PASS 또는 배포 완료로 기록하면 안 된다.


## 새 선택 트리 검수 대기

HEAD가 전달한 새 방향은 물건 우선 메뉴와 뒤로/메뉴 전체 취소이며, 메뉴 취소를 원래 부탁 취소 또는 현재 몸 동작 중단과 구별한다. 최종 실행 leaf를 고르기 전에는 인벤토리 요청이 나가면 안 된다. 현재1fc89b9에는 이 새 흐름을 검증한 증거가 없다. 타 행동 전체의 트리 변경이나 추가 예산을 승인한 것으로 해석하지 않는다. 새 합의와 새 고정판을 받은 뒤 해당 흐름을 검수한다.

