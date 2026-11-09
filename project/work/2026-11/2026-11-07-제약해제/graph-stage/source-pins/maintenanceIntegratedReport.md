# 장비 정비와 가방·생활 트리 통합 결과

통합 후보는 `5a05b80c4fe1ea764caa62ee83d06ac7fb3177ef`, worktree는 `C:/Dev/CoreKeeperModSDK-maintenance-integrated`, 브랜치는 `maintenance-integrated-20261107`이다. 새 교차 검증을 포함한 생산113/0 및 같은 SHA의 Unity 최종 컴파일이 통과했다. 공식 반영·설치·실게임은 수행하지 않았다.

## 통합 입력과 변경

생성 직전 공식 HEAD와 ai-character가 모두 `39b22df96dffe869ab0ccd474b18d31462b71d70`임을 확인했다. 공식의 기존 환경/SDK/브리지 작업 상태도 읽었으며 수정하지 않았다. 정비 입력은 독립 검수된 `b1f5f4290c97d5b68dc64e5cce6b09bc1aa29635`로 고정했다. 통합 커밋의 두 부모는 위 39b와 b1f5다.

[통합 계획](06-integration-plan.md)대로 별도 worktree를 39b에서 만들고 b1f5를 병합했다. 실제 충돌은 preview와 같은 두 곳이었다.

- AIBrain.Loop의 `AIInventoryMenu.PromptLine()`과 `AIMaintenance.Report(server, player)`를 모두 한 번씩 유지했다.
- AIDecisions의 `inventoryMenu`와 `maintenance` payload 필드를 모두 유지했다.

제품의 수동 통합은 이 두 인접 추가를 보존하는 해결뿐이다. 별도의 정책·상태 소유자·트리 엔진을 추가하지 않았다. 7430176/b1e05a8/39b의 rendered view guard, 전투 suspend, 커서 lifecycle reset과 정비의 pending/정확 신원·슬롯 장벽·수리/비용·단일 레벨 변화·가방 회수·원래 목표 참조/revision의 의미를 함께 유지했다.

## 통합된 생산 경계 검증

기존 maintenance harness에 전체 생산 `AIInventoryMenu`를 링크하고, 현재 Loop의 `InventoryChoiceSurface`와 `InventoryMenuControl` 본문을 추출했다. 새 IntegrationCases의 25개 검사는 생산 메뉴·생활 Compose/Stale·실행 Apply·정비/InventorySkill을 같이 실행한다. 새 boundary는 관측 적/기억시설 목록만 공급하며 메뉴 정책·정비 장벽·완료 판단을 복사하지 않는다.

- 표시 중인 생활 화면의 구 가방 토큰 거절, 전투로 숨은 생활 화면에서 가방 진입/공격 유지, 소유권 전환 뒤 구 생활 토큰 무효화와 원래 몸/목표/게시 요청 보존.
- 가방 active 중 정비 direct/기억시설 이동 미공개, 취소 뒤 exact direct 복귀, 기억시설 이동의 life binding과 전투 제외.
- 실제 가방 조작 목록과 MenuMoves의 수리 source 및 posted take의 실제 main destination 장벽, 무관 슬롯 허용과 정상 처리 뒤 해제.
- 중단된 강화 pending의 메뉴·전투 전환 뒤 회수, 원래 upgrade의 작업1/확보1 단일 기록, 목표 변경 뒤 새 quota 오염 방지, goal/LeaveNavigation의 suspended 초기화와 reset 뒤 기존 forge 물품 미채택.

소비자 ECS/UI, native 게임 비용 helper, 전체 Tick/경로계산/모델 API는 기존 README에 명시한 어댑터 경계다. 이번 통합 검사로 설치·실게임 성공을 주장하지 않는다. 첫 단위 baseline/그래프 검수나 원판 독립 검수는 반복하지 않았다.

## 실행 시점과 고정 SHA 근거

초기 개발 검사는 merge가 진행 중인 HEAD39b + staged 정비 병합 + 새 harness 변경 상태에서 실행했다. 이 실행을 최종5a 커밋 실행으로 소급 표기하지 않는다.

- [integration-maintenance-initial.log](logs/integration-maintenance-initial.log)는 **교차 표본 추가 전** 생산 메뉴 링크 조합의 88/0이다.
- 교차 표본 첫 실행의 108통과/5실패는 **개발 중 콘솔 관측, 원문 미보존**이다. 최초 표본이 Helm/Wood에 hold를 기대했으나 생산 AIInventory.Stale의 hold는 MiningPick/MeleeWeapon만 허용한다. 수리 Helm은 equip으로, 무관 물품은 실제 MiningPick hold로 수정했다. 수리 뒤에는 과거 슬롯 snapshot을 재사용하지 않고 실제 메뉴를 재조회했다. 새 maintenance 목표 직후 메뉴 Active는 Ensure가 다음 관측에서 닫는 lazy 경계이므로 실제 public Surface를 거친 뒤 초기화를 판정하도록 고쳤다. 제품은 수정하지 않았으며 옛 잘못된 표본을 재실행하지 않았다.
- [integration-maintenance.log](logs/integration-maintenance.log)는 위 기대 수정 뒤 precommit 113/0이다. 초기 execution 실행은 기본 PowerShell의 UTF-8 해석 문제로 생성 문자열 오류가 났으며, PowerShell7 실행으로 해결했다. 해당 suite의 현재 로그를 최초 실패 원문이라고 부르지 않는다.

최종5a를 커밋한 뒤 로그를 별도로 남겨 동일 후보를 한 번 고정 검증했다. [실행 스크립트](logs/run-integration-fixed.ps1)는 시작/종료 HEAD=5a, 제품·Tools status=[]를 확인하고, tracked 생산/Tools 381개의 Git blob과 실제 SHA256 및 외부 정상 Create·수리/upgrade 근거 3개의 SHA256을 기록했다. [시작 manifest](logs/integration-fixed-inputs-start.json)와 [종료 manifest](logs/integration-fixed-inputs-end.json)의 384 입력은 모두 동일하다. [고정 실행 summary](logs/integration-fixed-summary.json)는 `inputsUnchanged=true`, 모든 suite의 sourceCommit=5a와 exit0을 담는다.

| 최종5a 검사 | 결과 | 로그 |
|---|---|---|
| 생산 정비 + 양쪽 메뉴 직접 교차 | 113/0: 기존 대표88 + 새 교차25 | [maintenance](logs/integration-fixed-maintenance.log) |
| 기존 공통 실행/가방 public surface | 55/55 | [execution](logs/integration-fixed-execution.log) |
| 기존 생활 실행/조회/공유 잔량 | 96/0 | [life](logs/integration-fixed-life.log) |
| 기존 가방 이동·생활 binding | 91/0 | [inventory](logs/integration-fixed-inventory.log) |
| 통합 전체 생산 fast compile | 경고0/오류0 | [fast](logs/integration-fixed-fast.log) |

## Unity와 환경

정비 단독 b1f5 배치 PID3408이 자연 종료했다는 HEAD 결과를 받고, 현재 Unity.exe가 없음을 다시 확인한 뒤 통합 배치 PID59296을 시작했다. 실행은 Unity6000.0.59f2의 `-batchmode -quit -projectPath C:/Dev/CoreKeeperModSDK-maintenance-integrated -executeMethod HeadlessSdk.CompileCheck`이며 다른 배치와 겹치지 않았다.

준비된 maintenance-tree의 SDK 및 환경 설정을 **통합 worktree로만 복사**했다. [환경 기록](logs/integration-environment.json)의 4개 manifest/ProjectSettings SHA256은 원본과 같고 SDK는 440파일/38,433,592바이트다. 환경 파일과 SDK는 제품 커밋에서 제외했다.

[Unity 시작 기록](logs/integration-unity-start.json)의 sourceCommit은 5a다. [Unity 로그](logs/integration-unity.log)와 [종료 summary](logs/integration-unity-summary.json)에서 자연 종료 exit0, `CompileCheck: ok` 1건, CS 오류0을 확인했다. 시작은 2026-11-07 06:20:11 UTC, 종료는 06:24:53 UTC이며 sourceCommit/endCommit 모두 5a다.

[최종 상태](logs/integration-final-state.json)에서도 통합 HEAD=5a와 제품·Tools clean을 확인했다. 남은 통합 worktree 변경은 Unity/SDK 컴파일 환경뿐이며 커밋에 섞지 않았다. 공식은 여전히 39b, 정비 원판은 b1f5이고 원판의 제품·Tools도 clean이다. 원판·공식·다른 worktree를 수정하지 않았다. 독립 통합 검수는 HEAD가 별도로 배정하며, 공식 반영 판단은 HEAD에 돌린다.
