# 정비와 현재 가방·생활 탐색 통합 독립 검수

## 최종 판정과 범위

고정 통합판 `5a05b80c4fe1ea764caa62ee83d06ac7fb3177ef`의 병합 해결 및 직접 교차 경계를 검수했다. 부모는 공식 `39b22df96dffe869ab0ccd474b18d31462b71d70`과 수용 정비 `b1f5f4290c97d5b68dc64e5cce6b09bc1aa29635`다. 두 부모와 합의·`06-integration-plan.md`를 기준으로 하며, 완료된 b1 전체·첫 생활 단위·이전 그래프의 재검수를 수행하지 않는다.

**고정 통합판 `5a05b80c4fe1ea764caa62ee83d06ac7fb3177ef`는 지정된 병합·직접 교차 경계의 독립 검수를 통과한다. 새 blocker/중요 오류는 확인하지 않았다.** 병합 보존, 독립 통합25개/별도 oracle5개 실행, 최종 `07` 결과와 같은 통합 SHA의 Unity/고정 입력 근거를 대조했다. 이는 이미 수용된 두 부모의 전체 검수나 이전 그래프 검수를 반복한 판정이 아니다. 제품·공식·다른 담당 후보·게임·설치는 변경하지 않았다.

통과 범위는 부모 양쪽의 메뉴 렌더 소유권·전투 suspend/lifecycle와 정비 pending/slotBarrier·직접 실행/회수의 결합이다. 실제 모델/API·native helper·실제 게임 소비자·설치/실게임·배포 완료를 뜻하지 않는다. 공식 반영은 HEAD의 후속 판단이다.

## 병합 해결과 양쪽 소유권 보존

`git show --remerge-diff 5a05b80`을 직접 읽었다. 제품 충돌 해결은 다음 두 곳이며 새 별도 제품 정책은 없다.

| 위치 | 실제 해결 |
|---|---|
| AIBrain.Loop.cs:1391,1392 | AIInventoryMenu.PromptLine과 AIMaintenance.Report를 각각 한 번 유지한다. |
| AIDecisions.cs:121,122 | AIInventoryMenuChoice inventoryMenu와 MaintenanceRequest maintenance를 별도 payload 필드로 함께 유지한다. |

정비 핵심 AIMaintenance.cs의 blob은 b1과 통합 모두 `4057157ec934bf6261bc6c8e91800751c1ddd696`이다. 가방 핵심 AIInventoryMenu.cs의 blob은 39b와 통합 모두 `07378dabc7940e4ba4451e1782ffe19717b17e7a`이다. 각 부모의 핵심을 새 통합 구현으로 다시 작성하지 않았다. 자동 합성된 AILife/AIActions/AIGoals/AIGoalFeatures/AIInventory의 직접 겹침도 부모 차이로 대조했다.

- `AILife.cs:149`의 active 가방 조기 반환, `:150`의 ViewSuspended 설정, `:152`의 전투 생활 실행/nav 제외와 `BlocksInventoryView:42`의 Open&&!Suspended가 보존된다. maintenance 실행·기억시설 이동은 life binding을 유지하므로 이 경계에 함께 걸린다.
- 실제 최종 선택면 `AIBrain.Loop.cs:1231`은 active 가방에서 기존 공통 제어/필요 전투만 보존하고 정비 direct·기억시설 이동을 숨긴다. 가방 취소 뒤에는 정비 direct를 다시 제시한다. 이를 우회하는 정비 전용 렌더 소유자는 추가되지 않았다.
- 생활 nav 응답 `Loop:838`과 직접 Apply `:925`는 가방의 이전 토큰을 초기화한다. 가방 nav `:935`는 LeaveNavigation과 기존 CancelPendingChoices를 사용해 이전 탐색/대기 선택의 권한을 폐기하며 현재 몸·원래 목표와 정비 pending은 보존한다. 가방 nav가 held/queued를 폐기하는 것은 공식 부모의 기존 의미다. 생활 nav의 읽기 전용 보존 계약과 혼동하지 않는다.
- `AIInventory.cs:608`의 SlotBarrier는 실제 MenuMoves의 공통 Stale 입구에 남는다. 수리 source와 게시 take의 실제 main 목적 슬롯은 처리 증거 전 차단되고 무관 슬롯/목적지는 허용된다. 정비 이동이 일반 가방 goal moved나 상자 Opened를 갱신하지 않는 b1 경계도 보존된다.
- 목표 변경/LeaveNavigation의 suspended 초기화와 가방 Ensure의 goal/world/player 갱신은 정비 State.Reset에 연결되지 않는다. 전투/메뉴 전환/목표 교체가 정상 게시·투입된 pending을 지우지 않으며, 캐릭터/월드 reset만 임시 근거를 지운다.

## 교차 표본과 실제 생산 연결

별도 [IntegrationReview.csproj](review-integration-boundary/IntegrationReview.csproj)가 통합판의 maintenance 프로젝트를 참조하고 [좁은 실행기](review-integration-boundary/IntegrationReviewRun.cs)에서 IntegrationCases 네 묶음만 호출한다. b1의88개 전체를 다시 실행하지 않았다. 원래 통합 교차25개와 아래 oracle5개를 합쳐 **30개/실패0, exit0**을 직접 확인했다([실행 로그](logs/review-integration-only.log)).

생산 전체 AIInventoryMenu/AIInventory/AILife/AIMaintenance를 링크한다. 실제 Loop의 InventoryChoiceSurface/InventoryMenuControl/HandleResponse/Apply가 생성 harness 안에 원문 그대로 포함되는지도 직접 비교해 모두 true임을 확인했다([고정 검수 pin](review-integration-boundary/review-pin.json)). 뷰 정책·슬롯 장벽·완료 판단의 대역 구현으로 시험하지 않는다. ECS·적/기억시설 관측·UI 큐 소비와 상태 변화는 fixture다.

직접 교차25개는 다음을 확인한다.

- 정비 생활 화면 표시 중 구 가방 토큰 거절, 생활 nav의 몸/held/queued/목표/게시 요청 보존.
- 전투 일시중지 뒤 기존 공격+가방 진입, 가방 nav의 생활 구토큰 폐기와 몸/정비 pending 보존, 취소 뒤 한 렌더 소유자 복구.
- 닫힌 뷰의 maintain 직접 실행, active 가방에서 direct/기억시설 이동 숨김, 취소 뒤 복구 및 전투의 기억시설 실행 제외.
- 실제 메뉴 작업 목록과 MenuMoves가 미처리 수리 source/게시 take 목적지를 차단하고 소비 확인 뒤 해제, 무관 슬롯/목적지 허용.
- 전투·메뉴 전환 뒤 이미 강화된 정확 pending 회수로 같은 원래 upgrade만 한 번 완료, 새 goal은 이전 결과를 합산하지 않음.
- goal/LeaveNavigation의 suspended 초기화와 캐릭터/world reset의 임시 pending 폐기.

## 초기 교차 실패의 oracle 수정 대조

HEAD가 설명한 초기5건 실패 원인은 투구/나무를 hold 가능한 대상으로 둔 기대와, 다음 검증 입구 전 Active getter가 즉시 메뉴 reset을 수행해야 한다는 기대다. 이 설명을 단순 수용하지 않고 생산 경로와 독립5개 표본으로 대조했다.

1. pending이 없는 상태에서도 투구 slot12의 hold 후보는0이다. 같은 투구의 equip 후보는 실제로 존재한다.
2. 나무 hold 후보는0이고, 별도 MiningPick slot10은 유효한 hold 목적지를 갖는다.
3. 이 결과는 `AIInventory.Stale:666`의 hold 타입 제한(MiningPick/MeleeWeapon)과 실제 AddOperations→MenuMoves→Stale의 흐름에 일치한다. 따라서 수리 source 장벽을 equip으로, 무관 슬롯 허용을 지원되는 pick hold로 시험하는 수정은 production을 통과시키기 위해 장벽을 완화한 것이 아니다.
4. 목표를 바꾼 직후 수동 Active getter는 이전 frame을 유지한다. 실제 InventoryChoiceSurface를 호출하면 Candidates→Ensure에서 goal/world/player를 검증해 이전 메뉴가 닫힌다. 이를 직접 각각 검사했다. 프로덕션 메뉴의 lazy 갱신을 eager getter로 잘못 기대했던 것을 실제 공개 관측 입구 기준으로 고친 것이다.

goal reset의 suspended 검사는 core cursor를 Compose 없이 다시 열어 `BlocksInventoryView`를 확인하므로 단순 redraw가 stale suspension을 숨기지 않는다. LeaveNavigation도 같은 방식으로 별도 검사한다. 제품 소스를 수정하거나 검증 기준에서 pending·slotBarrier·진척 조건을 제거한 변화는 없다.

초기5건의 당시 실패 원문을 이 검수자가 실행한 결과로 표기하지 않는다. 최종 `07-integration-result.md`는 개발 중108통과/5실패가 콘솔 관측이고 원문 미보존이라고 명시한다. `integration-maintenance-initial.log`는 교차 추가 전88/0 로그로 직접 확인했으며, 이를 초기5실패 증거로 오인하지 않았다. 현재 수정의 정당성 판단은 위 실제 생산 대조와 독립 oracle 표본에 근거한다. 당시 콘솔 원문 전체를 사후 복원했다고 주장하지 않는다.

## 최종 근거와 한계

최종 [07-integration-result.md](07-integration-result.md), [고정 실행 summary](logs/integration-fixed-summary.json), [최종 상태](logs/integration-final-state.json)와 실제 로그를 직접 열어 대조했다. 초기 개발 실행과 최종 커밋 뒤의 고정 실행은 별도 기록이다.

| 검증 | 확인 결과 | 독립 검수의 수행 구분 |
|---|---|---|
| 직접 통합25 + oracle5 | 30/0, exit0 | 별도 runner에서 주 검수자가 직접 실행 |
| 고정 통합 생산 maintenance | 113/0(88+25), exit0 | 최종 구현자 로그/생산 연결 열람, b1 전체 독립 재실행 아님 |
| 공유 public surface/생활/가방 직접 영향 | execution55 / life96 / inventory91 통과 | 고정 로그와 summary 대조, 독립 전회귀 반복 아님 |
| fast compile | 경고0/오류0, exit0 | 고정 최종 로그 열람 |
| Unity6000.0.59f2 | 5a 입력·종료, exit0 / CS0 / CompileCheck:ok 1건 | 실제 최종 로그·실행 메타데이터 대조, 별도 Unity 재실행 아님 |

시작/종료 manifest의 tracked 생산·Tools381개와 외부 정상 Create/근거3개를 직접 비교해 총384개가 모두 같은 것을 확인했다. 양쪽 sourceCommit은 모두5a이고 summary의 `inputsUnchanged=true`와 일치한다. 최종 상태 기록 및 주 검수자의 현재 status에서 통합 제품/Tools clean을 확인했다. 정비 원판 b1과 공식39b가 보존됐다는 기록도 읽었으며 이 후보가 그 두 고정 부모의 merge인 사실을 Git 객체에서 직접 확인했다.

[Unity 시작 기록](logs/integration-unity-start.json), [종료 summary](logs/integration-unity-summary.json)은 sourceCommit/endCommit 모두5a를 가리킨다. PID59296, exit0, CS0, 완료 표식1을 확인했고 실제 `logs/integration-unity.log:11817`의 `[HeadlessSdk] CompileCheck: ok`와 compiler error 패턴 없음도 직접 확인했다. 정비 원판 b1의 Unity 성공을 통합판 성공으로 옮기지 않았다.

실제 모델/API, 전체 CandBuild/전체 Tick 스케줄러, native 비용 helper·실제 게임 소비자·외부 동시 조작·설치/실게임 성공은 이 통합 표본에서 증명되지 않는다. 기존 승인·수용 결과의 지위를 바꾸거나 이전 그래프를 재검수하지 않았다.
