# 일반 수리·단일 레벨 업그레이드 독립 검수

## 최종 판정

**최종 후보 `b1f5f4290c97d5b68dc64e5cce6b09bc1aa29635`는 이번 일반 수리·단일 레벨 업그레이드 격리 구현의 독립 검수를 통과한다. 미해소 blocker/중요 오류는 확인하지 않았다.** 기반은 완료된 첫 단위 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`이며 조상 관계를 직접 확인했다. 중간 고정판 `df9a1eab589017ea84cbb3d2e8d4e35c397e9dac`에서 발견한 M1/M2는 후속 diff·생산 직접 영향 회귀·독립 최소 표본과 최종 Unity 근거를 대조해 해소를 확인했다. 수정 전 근거는 아래에 보존한다.

이 판정은 새 정비 diff의 합의·주요 실행, 오프라인 생산 파이프라인, 실제 SDK/Unity 컴파일 범위다. 실제 모델/API·native 비용 helper·실제 게임 소비자의 성공, 설치·실게임·배포 완료를 뜻하지 않는다. 동시 변경된 공식 main과의 통합은 별도 검수 경계이며 이 원판 통과를 그대로 통합판의 검수로 옮기지 않는다.

완료된 첫 단위 코드·그래프와 기존 baseline을 재검수하지 않았다. 새 정비 diff와 직접 영향에 한정하며 제품 코드, 공식/다른 담당 후보, 설치·게임을 변경하지 않았다. 진행 중인 worktree 변경은 고정 중간판과 구분하고 `git show df9a1ea:<path>`로 읽었다.

## 기준과 분담

`C:/Dev/AGENTS.md`, 이미 읽은 HEAD Core `head-context:start-job`, 이 폴더의 `00-agreement.md`, `01-direction-decision.md`, `02-spec.md`, `02-spec-decision.md`를 대조했다. 일반 수리·단일 단계 업그레이드·자신의 정상 투입 pending 회수만 범위다. 내구 강화·최대 반복·새 소비 정책·과거 무관 복구로 확대하지 않는다.

주 검수는 상위 selector/목표 binding·정상 UI 큐·InventorySkill 이동·용량 보호·생산 연결을 맡고 결과를 통합한다. 공개 비용/정상 API·일반 수리는 `05-review-cost-repair.md`, 업그레이드/pending/중단/회수는 `05-review-pending.md`의 독립 하위 근거를 직접 대조한다. 보고서와 필요한 결과폴더 내 경계표본만 작성한다.

## 수정 전 발견 사항

### M1 — 게시·투입 전 무효 intent가 새 정비와 회수를 막음

중요 오류. 고정판 `AIMaintenance.cs:525`의 Begin은 새 Pending을 State에 붙인다. 이후 `MaintenanceSkill.Tick:647`에서 Begin한 뒤 InventorySkill.Start가 실패하거나 정상 큐 등록 전 중단되더라도 `Cleanup:727`은 nav/이동 정리만 한다. `Posted=false && PutConfirmed=false`인 부작용 없는 intent가 계속 남는다.

`ExactCandidate:295`는 Pending이 있으면 새 정비를 차단하고 `Stale:362`도 같은 조건으로 거절한다. recover는 PutConfirmed를 요구하므로 이 상태를 회수로 해소할 수도 없다. 명세 §6의 승인된 intent 정리 보충과 §10의 직접 회귀 조건에 어긋난다.

이 사실을 HEAD에 즉시 전달했다. HEAD는 구현자도 원인을 확인했고 후속판에 제한된 CleanupIntent를 연결 중이라고 통보했다. 수정 후에는 Posted=false/PutConfirmed=false의 해당 intent만 제거하며, 정상 큐 등록·게시 또는 투입 확인 뒤에는 pending을 보존하는지, 정리가 재발송·자동 성공·진척 증가를 만들지 않는지 확인한다. 진행 중인 수정은 최종 SHA 전 자동 해소로 인정하지 않는다.

### M2 — 강화 관측 뒤 원본으로 교체된 물품 회수가 upgrade 성공으로 집계됨

중요 오류(P1). `AIMaintenance.cs:461` 이후 ObservePending은 예상 nextLevel을 관측하면 `p.Current=item`, `p.Upgraded=true`로 기록한다. 이후 같은 ID/aux의 원본 variation 물품으로 입력이 교체되면 `:464`는 Current를 그 원본으로 다시 채택하지만 Upgraded=true를 유지한다. 이미 관측한 정확 upgraded snapshot이 뒤로 바뀌어도 회수 가능한 자신의 강화 물품으로 남는 것이다.

Recovery `:341`은 이 bool과 원래 goal reference/revision을 사용해 upgrade binding을 붙이고, Received `:719` 이후는 실제 수령된 variation 대신 ExpectedLevel을 observedLevel/expectedLevel 양쪽 인수에 넣는다. 따라서 미강화 원본을 정상 회수했는데도 work1/received1로 원래 upgrade 목표가 충족된다. 이는 원자적 잠금이나 영구 UUID를 요구하는 지적이 아니라, 이미 확정한 물품 snapshot의 변경을 재채택하고 과거 강화 사실을 현재 회수물에 붙이는 오류다.

독립 하위 표본은 고정 생산 소스에서 put 소비→upgrade 게시 뒤 중단→upgrade 소비→후보 재관측으로 expected snapshot 확인→forge 입력을 BeforeItem으로 교체→recover/정상 MoveAmount 소비를 실행했다. 미강화 원본 회수 뒤 원래 upgrade `RequestSatisfied=true`가 되는 것을 재현했다([수정 전 pending 표본](review-pending-boundary/independent-pending-df9a1ea.log)). 큐·상태 변화는 어댑터 경계이며 실제 게임 외부 동시 조작을 실행한 것은 아니다. 주 검수자가 해당 원문 경로를 직접 대조하고 HEAD에 즉시 전달했다.

주 검수자도 격리 snapshot의 같은 생산 경계 프로젝트를 `dotnet run -c Release --no-restore`로 직접 실행해 18개 중15개 통과/3개 실패, dotnet exit3을 확인했다([주 검수 직접 실행](review-pending-boundary/primary-review-df9a1ea.log)). 실패1은 M1이고 실패2는 같은 M2의 차단/진척 검사를 각각 나타낸다. snapshot AIMaintenance의 Git blob은 `90d63b108534f8a437a1fdf3826671cfbf3077b2`로 중간 고정판과 일치함을 직접 비교했다. M1 표본은 생산 Begin handle을 스킬 private pending에 연결해 실제 Cleanup을 호출하는 좁은 경계이며 전체 실패 경로 실행이라고 확대하지 않는다.

수정 후에는 강화 사실을 확인한 뒤 원본/다른 variation/상태로 바뀐 입력을 승인된 snapshot으로 다시 채택하지 않는지, 정확 업그레이드 물품의 실제 main 회수만 upgrade work1로 집계하는지, 정상 지연 회수와 미강화 원본의 명시 recover가 서로 구별되는지 확인한다.

M2의 직접 수정 영향으로 정상 이동의 실제 필드 전이도 대조했다. 로컬 SDK InventoryUtility.TryMove는 목적지 기존 struct에 source의 ID/variation/amount/aux를 대입하지만 source variationUpdateCount는 복사하지 않는다. SetVariation은 variation만 바꾸며 updateCount를 증가시키지 않는다. ContainedObjectsBuffer.EqualsExact는 updateCount도 비교하므로 source 전체 snapshot과 목적지 전체 snapshot을 곧바로 같다고 요구하면 정상 회수를 과엄격하게 막을 수 있다. 주 검수자가 기존 소비자 원문을 읽고, 비용 담당도 실제 준비 DLL의 같은 메서드를 새로 decompile해 동일 사실을 확인했다. HEAD는 해당 필드 전이에 맞춰 receipt와 fixture를 수정 중이라고 통보했다. 새 정책/별도 비용체계 지적으로 확대하지 않고 최종 M2 수정에서 정상 회수와 교체 차단을 함께 확인한다.

## 새 변경의 정적 경계 대조

- maintain은 기존 enum 끝에 추가되고 mode repair/upgrade/recover 및 count1 형식을 사용한다. Protocol에서 명시 count 오류와 누락을 구분하며 PlanStep→SetGoalFrom→AIGoals에 기존 item/slot/where/mode를 전달한다. 기존 필드를 새 의미로 전역 변경하는 범위는 추가되지 않았다.
- 실제 source 슬롯의 품목을 읽어 AILife.Resolve로 목표 일치·잔량을 다시 판정한다. 다른 품목/슬롯/시설은 별도 행동이며 기존 goal 진척을 새로 배정하지 않는 구조다. recover가 이미 확인된 upgrade를 마무리할 때만 원래 목표 참조/revision의 binding을 재사용한다.
- `AICraft.QueueObservedUIAction`은 UIActionBuffer/UIActionsCD/NetworkTime을 요구하고 정비 경로에 CraftBuffer fallback을 사용하지 않는다. 현재 server tick 다음과 마지막 처리 tick 뒤에서, 미처리 항목과 충돌하지 않는 tick에 정상 요청을 추가한다.
- 로컬 SDK `TriggerUIActionsSystem.cs:275` 이후의 GetOldestNonAppliedActionFromTick은 현재 tick 이하의 미처리 항목 중 가장 오래된 tick 하나를 선택하고 lastActionTick을 올린다. 이어 Craft/InventoryChange를 정상 소비 버퍼에 넣는다. `InventoryUpdateSystem.cs:411` 이후는 정상 CraftAction을 처리한 뒤 CraftBuffer를 Clear한다. 이 원문 대조는 게시 수락·소비 처리·실제 결과를 분리하는 큐 장벽의 근거이며 실제 소비자를 실행한 것은 아니다.
- 정비용 maintenance_put/take는 기존 InventorySkill.TransferTick/정상 MoveAmount와 양쪽 변화 관측을 사용한다. AIInventory.Stale에서 자기 Pending·대장간·원래 source와 현재 활성 슬롯·정확 before snapshot·꺼내기/목적 제한을 확인하며, 기존 상자 소유 경로를 우회하는 일반 허용으로 만들지 않는다. 정비 이동은 기존 상자 open 상태와 목표 이동 진척을 갱신하지 않는다.
- 기존 CapacityAfterSwap은 replacement map을 받는 동일 용량/태그 검사로 추출됐다. 정비 source→빈칸 제거도 같은 검사에 전달하며 자동 drop·파괴·임의 장비 교체 fallback은 없다.
- Reset은 정비 State를 캐릭터별로 지우며 목표 변경 자체가 게시/투입 Pending을 초기화하지 않는다. 새 영구 기록·예약 엔진은 추가되지 않았다.

## 중간 증거의 지위와 한계

통보받은 대표 생산17/0·fast compile0/0은 구현자의 중간 결과다. 본 검수자의 전회귀 재실행으로 바꾸지 않는다. 생산 본문 직접 링크/추출과 ECS·입력·비용/소비 어댑터의 경계를 확인한 뒤 최종 후보의 직접 영향만 확인한다.

게임 helper의 공개 인수/정상 Create·소비자 원문 대조와 fixture 비용행/수동 상태 공급은 서로 다른 증거다. 실제 helper의 모든 게임 조건·실제 모델/API·실제 게임 소비자·설치 성공을 뜻하지 않는다. 최종 SHA와 Unity 근거는 아래와 같이 별도 확인했다. 설치·실게임은 기존 보류다.

## 수정 후 재검수 — b1f5f42

최종 생산 후보로 전달받은 `b1f5f4290c97d5b68dc64e5cce6b09bc1aa29635`와 `df9a1ea..b1f5f42`의 직접 영향 diff를 대조했다. 제품/Tools 미커밋 없음도 직접 확인했다. 이 절은 중간판 M1/M2의 원인·재현 근거를 덮지 않는다.

M1: Cleanup이 `CleanupIntent`를 호출한다. State의 world/player와 현재 Pending handle이 같고 `!Posted && !PutConfirmed`인 그 intent만 제거한다. 정상 로컬 등록·게시 또는 실제 투입 뒤에는 Pending과 게시 근거를 보존하며, 정리 자체의 물품 이동·재발송·성공 집계가 없다.

M2: 예상 강화 snapshot을 한 번 확인한 뒤 Current를 원본 variation이나 다른 stamp로 다시 채택하지 않는다. LiveTarget/ObserveExactInput도 frozen upgraded snapshot 불일치를 차단한다. 회수는 NormalMoveReceipt가 실제 TryMove의 ID/variation/amount/aux 전이와 목적지 beforeTo의 updateCount 보존을 계산해 대조하고, 실제 ReceivedItem.variation을 UpgradeConfirmed의 observedLevel로 전달한다. 강화된 입력 snapshot 동결과 정상 가방 이동의 stamp 전이를 별도로 검증한다.

기타 직접 영향은 request의 World/Owner/player 신원과 selection 복사, mode/recover where 검사, 정비 InventorySkill의 매 tick 시설 시야/거리 검사, 게시된 take의 실제 main 목적 슬롯 장벽이다. 범위 밖 슬롯/다른 작업을 전역 예약하는 변경은 없다. 수리의 소비 proof 뒤 대상은 자기 장비 슬롯에서 관측하고, 화면 밖 시설 buffer를 읽어 결과를 확정하지 않는다.

주 검수자가 고정 최종판에서 `Tools/maintenance-tests/run.ps1`을 직접 실행해 **88/0, exit0**을 확인했다([직접 실행 로그](logs/review-final-maintenance.log)). M1의 Begin 뒤 게시 실패/stop→새 Exact 가능, posted intent 보존, M2의 원본/다른 stamp 대체 차단, 정상 put→Upgrade→main 회수, owner/world/entity/시야·공간·비용·양쪽 상태·처리 장벽·원래/새 goal 분리 표본을 소스와 대조했다. 비용 helper는 실제 Native 실행이 아닌 공개 호출 인수와 고정 반환 행의 어댑터이고, 정상 Create는 게임 원문 생성 본문을 추출한다. 전체 CandBuild/전체 Tick/실제 게임 소비자 실행 증거로 확대하지 않는다.

하위 독립 최종 pending 표본은 고정 b1 snapshot의 생산 소스/본문을 직접 사용해 **22개 검사/실패0, dotnet exit0**을 확인했다([독립 최종 로그](review-pending-boundary-final/independent-pending-b1f5f42.log)). M1 정리와 등록/투입 후 보존, 소비 proof 전 회수 차단, 같은 원래 목표의 강화 회수1과 목표 변경 후 진척0, 원본/다른 stamp로 교체된 frozen 입력의 차단과 목표 미충족을 확인했다. 정상 TryMove 목적지 stamp11을 유지한 put 및 stamp29를 유지한 take가 정확 ID/variation/amount/aux의 회수로 통과한다. SetVariation은 실제 원문처럼 variation만 바꾸는 fixture다.

주 검수자도 이 최종 좁은 프로젝트를 직접 다시 실행해 **22/0, exit0**을 확인했다([주 검수 최종 재실행](review-pending-boundary-final/primary-review-b1f5f42.log)). 고정 snapshot AIMaintenance의 Git blob `4057157ec934bf6261bc6c8e91800751c1ddd696`이 b1 원문과 같은 것을 직접 비교했다. 중간 18검사/3실패와 최종 22검사/0실패는 상태·원본을 구분해 남겼으며 실패 증거를 지우지 않았다. 이 독립 표본의 게임 ECS·시야·lookup·정상 UI 소비/버퍼 변화는 어댑터가 공급한다.

공개 비용 helper·정상 payload는 최종 후속에서 바뀌지 않아 SDK를 다시 추출하거나 전체 비용계를 재검증하지 않았다. 비용 담당의 [수리/비용 검수](05-review-cost-repair.md)는 실제 준비 SDK 공개 호출 묶음·8인수 repair/7인수 upgrade·main-first/nearby 후순위 소비 및 정상 Move stamp를 직접 대조했고 최종 변경에서 새 중요 오류를 찾지 못했다. [pending 담당 검수](05-review-pending.md)의 최종 소스·독립 실행 근거도 주 검수자가 확인했다.

## 최종 근거 대조

[04-implementation.md](04-implementation.md), [최종 validation 메타데이터](logs/final-validation.json), 관련 실제 로그와 생산 테스트 연결을 직접 열어 대조했다.

| 근거 | 확인 결과 | 검수의 실제 수행 구분 |
|---|---|---|
| 새 정비 생산 파이프라인 | 88/0, exit0 | 주 검수자가 고정 최종판에서 직접 실행 |
| 독립 pending·중단·신원·정상 stamp 표본 | 22/0, exit0 | 하위 독립 실행 후 주 검수자도 직접 실행 |
| SDK compile-check | 경고0/오류0, exit0 | 최종 구현자 로그 열람 |
| 관련 제작 / 가방 이동 | 19/0 / 91/0 | 최종 로그·직접 영향 소스 대조, 독립 전회귀 반복 아님 |
| 새 Core/Protocol 및 공유 실행 영향 | Core1393 / bridge188 / common36 / life96 통과 | 구현자 직접 영향 로그/최종 메타데이터 대조, 기존 첫 단계 재검수 아님 |
| Unity6000.0.59f2 | b1 최종 입력 exit0 / compiler error0 / CompileCheck:ok 1건 | 최종 실행 기록·실제 로그 대조, 독립 Unity 재실행 아님 |

정비 최초 88/0 실행 당시 Git HEAD가 df9였다는 `maintenance-tests-summary.json`의 원래 값은 보존돼 있다. HEAD는 실행에 사용한 네 생산 소스(AIMaintenance/AIInventory/AICraft/AILife)의 기록 SHA256과 b1 현재 파일을 별도 비교해 final-validation에 남겼다. 주 검수자도 네 파일의 SHA256을 직접 다시 계산해 모두 같은 것을 확인했고, b1 고정/제품·Tools clean 상태에서 위 88/0을 직접 실행했다. 이전 HEAD 명칭만으로 중간 소스를 최종 성공으로 승격한 근거가 아니다.

[Unity 최종 시작 기록](logs/unity-final-start.json)과 [최종 종료 기록](logs/unity-final-exit.json)은 모두 sourceCommit b1을 가리킨다. 배치 PID3408, `-batchmode -quit -projectPath C:/Dev/CoreKeeperModSDK-maintenance-tree -executeMethod HeadlessSdk.CompileCheck`, exit0을 확인했고, `logs/unity-final.log:1816`의 `[HeadlessSdk] CompileCheck: ok` 및 compiler error 패턴 없음도 직접 확인했다. 중간 Unity 성공과 최종 수정판 성공을 구분한다. `04`의 최종 결과 표 갱신도 확인했다.

완료된 첫 단위 코드·그래프의 재검수, 기존 전체 baseline의 반복, 설치·게임 실행·그래프 저장은 수행하지 않았다. 수리/업그레이드의 외부 동시 조작까지 원자적으로 잠그는 구조나 재입장 뒤 pending 복원은 이번 지원 범위가 아니다. 실제 모델의 선택→정상 게임 소비→정확 장비·재료/가방 결과→다음 판단은 실게임 보류 해제 뒤 별도로 확인해야 한다. 공식 통합·새 변경의 그래프 반영·작업 결과 저장은 부른 HEAD가 담당한다.
