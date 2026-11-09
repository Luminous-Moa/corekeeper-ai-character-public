# 장비 정비와 가방·생활 트리 통합 준비

2026-11-07 읽기 전용 preview. 입력은 00-agreement.md, 01-direction-decision.md, 02-spec.md, 02-spec-decision.md와 아래 고정 Git 객체다. 제품·공식·다른 작업 worktree를 변경하지 않았으며, 정비 worktree의 미커밋 수정은 읽거나 반영하지 않았다. 이 문서만 산출물로 작성했다.

## 고정판과 실제 preview

- 공통 기반: `c4a0f5e38016c79567f880bbaea5e56e0eeae347`.
- 공식: `39b22df96dffe869ab0ccd474b18d31462b71d70`.
- 정비 중간 고정판: `df9a1eab589017ea84cbb3d2e8d4e35c397e9dac`. 최종 M1/M2 수정과 검증 결과를 대신하지 않는다.
- `git merge-base 39b22df df9a1ea`의 실제 결과는 위 c4 기반이다. `git merge-tree c4a0f5e 39b22df df9a1ea`의 세 객체 preview를 사용했다. 실제 merge·checkout·테스트·Unity 실행은 하지 않았다. 이 형식의 merge-tree는 충돌이 있어도 exit 0이므로 충돌 마커를 별도로 확인했다.
- 양쪽 변경 파일은 11개, 텍스트 충돌 파일은 아래 2개다. 확인 종료 시 `ai-character`도 여전히 위 39b였다. 이후 공식이 움직이면 이 고정판과 차이를 먼저 보고하고 자동 추종하지 않는다.

## 텍스트 충돌의 직접 해결

| 파일 | 충돌 | 통합 시 보존 |
|---|---|---|
| `Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs` | 입력 보고의 같은 위치에 가방 PromptLine과 정비 Report 추가 | `AIInventoryMenu.PromptLine()`과 `AIMaintenance.Report(server, player)`를 모두 한 번씩 남긴다. |
| `Assets/AIPlayerSpike/Scripts/AIDecisions.cs` | 후보 payload의 같은 위치에 필드 추가 | `AIInventoryMenuChoice inventoryMenu`와 `MaintenanceRequest maintenance`를 모두 남긴다. 서로의 용도로 재사용하지 않는다. |

`AIActions`, `AIGoalFeatures`, `AIGoals`, `AIInventory`, `AILife`, execution 테스트 project/stub와 두 life binding source도 양쪽 변경이 있지만 이 고정판에서는 충돌 마커 없이 합성됐다. 텍스트 합성만으로 아래 상태 경계 검증을 통과했다고 보지 않는다.

## 함께 보존할 의미 경계

1. **실제로 표시되는 생활 화면의 소유권.** 공식의 `7430176`/`b1e05a8`/`39b22df`를 보존한다. `BlocksInventoryView`는 `Cursor.Open && !ViewSuspended`이고, 전투로 생활 화면이 숨으면 가방 탐색이 가능하다. 가방 active일 때 `AILife.Compose`의 조기 반환과 최종 `InventoryChoiceSurface`의 필터를 그대로 사용한다. 생활 nav를 적용하면 가방 토큰을 초기화하고, 가방 nav를 적용하면 `LeaveNavigation`으로 생활 커서/이전 nav 권한만 폐기한다. 목표 변경과 LeaveNavigation의 `ViewSuspended=false`도 남긴다. 정비를 위한 새 화면 소유자나 공통 엔진은 추가하지 않는다.
2. **정비의 트리·직접 실행·기억시설 이동.** `maintenance` 갈래와 `maintain`의 기존 ExactCandidate/Make/Stale 연결을 추가하되, 정비 후보의 `life` binding을 유지한다. 화면 밖 기억시설로 가는 fallback도 같은 binding을 가지므로 기존 생활 전투 필터를 따른다. 가방 active 상태에서는 기존 최종 필터가 정비 leaf/이동을 공개하지 않는다. 정비 direct를 살리기 위해 이 필터를 우회하거나 두 메뉴를 동시에 열지 않는다. 생활 조회는 기존 몸·held/queued·목표를 유지하고 실행 말단만 행동을 시작한다.
3. **pending과 탐색 상태의 수명 구별.** 중간판 AIMaintenance의 State는 가방 State 및 LifeTree slot과 별도로 AIAgent/world/player Entity 신원에 귀속된다. Sync는 목표 변경이나 전투로 pending을 지우지 않으며, Brain.Reset에 정비 Reset을 더한다. 가방 GoalSet/Reset, 생활 Sync/LeaveNavigation에 정비 Reset을 연결하지 않는다. stop/목표 변경/메뉴 전환 뒤 게시 요청·실제 투입 근거를 보존한다. 부작용 전 intent만 정리하는 최종 수정은 구현자의 finalSHA에서 받아 확인한다. 다른 캐릭터·월드의 pending을 채택하지 않는다.
4. **가방 조작과 정비 슬롯 장벽.** 공식 MenuMoves/최종 leaf도 `AIInventory.Stale`를 사용하므로 정비의 `SlotBarrier`를 그 공통 입구에 남긴다. 미처리 자기 요청의 정확 source/forge 슬롯 이동만 막고 탐색·전투·무관 슬롯을 통째로 막지 않는다. `maintenance_put/take`는 정확 forge/pending 검증 후 기존 정상 TransferTick을 쓰며 일반 store/take의 자기 상자 권한을 넓히지 않는다. 정비 이동으로 상자 opened 상태나 일반 가방 목표 moved 진척을 올리지 않는다. 가방/파우치 용량·태그 보호의 기존 swap과 새 removal을 함께 보존한다.
5. **목표 형식과 실제 완료.** GoalKind/GoalFeature의 Maintenance는 기존 enum 끝에 추가되어 이전 값이 유지된다. 기존 PlanStep 필드와 maintain 전용 형식 검사를 사용하고 일반 요청의 해석을 바꾸지 않는다. 일반 수리는 실제 내구도 변화/비용, upgrade는 정상 forge 투입→한 레벨 변화→main 회수를 구별한다. 게시 사실만 성공으로 세지 않는다. 같은 원래 goal 참조/revision의 강화된 pending 회수만 upgrade를 한 번 마무리하며 미강화 원본/다른 목표 회수는 그 목표의 진척에 합산하지 않는다. 공통 ProgressKey의 미지정 selector wildcard와 ILifeResult의 run당 기록을 유지한다.

## finalSHA 수령 뒤 격리 통합과 검증

최종 정비 SHA 및 그 SHA의 검증 결과를 받은 뒤, 위 공식 고정판에서 별도 통합 worktree를 만들고 최종 정비 변경을 병합한다. 중간판을 최종판처럼 공식에 반영하지 않는다. finalSHA가 중간판에서 더 바뀐 경계와 새 충돌만 다시 판단하고, 위 두 텍스트 충돌은 양쪽 추가를 함께 남기는 방식으로 해결한다.

- **생산 대표 파이프라인:** plan→트리/직접 ExactCandidate→AIActions→MaintenanceSkill→정상 repair false payload와 실제 내구도/재료 변화, upgrade의 정확 forge 입력→단일 Upgrade→next variation/비용→가방 회수→ILifeResult를 실행한다. 소비자 ECS/UI 경계 어댑터의 한계를 기록하고 정책/상태기계를 테스트에 복사해 통과시키지 않는다.
- **새 직접 교차 회귀:** 정비 생활 화면 표시 중 가방 구토큰 거절; 전투 일시중지 뒤 가방 진입과 몸/목표/pending 보존; active 가방에서 정비 direct/기억시설 이동 미공개; 가방 취소 뒤 정비 복귀; 생활 nav/가방 nav 전환 뒤 이전 답 무효화; 목표 변경/LeaveNavigation 뒤 suspended 상태 초기화. repair/put/upgrade 게시 중 가방 최종 조작의 정확 슬롯 장벽과 무관 슬롯 허용도 확인한다.
- **pending 수명·완료:** 투입 전 실패/중단에서 미게시 intent만 제거, 게시·투입 이후 stop/목표 변경/전투에는 보존, tick 적용·소비 버퍼 부재 전 회수 차단, 처리 확인 뒤 정상 회수, 같은 goal의 강화된 pending만 한 번 완료, 미강화/다른 goal 회수 진척 제외, 캐릭터/월드 reset 이후 이전 pending 및 후보 재사용 금지를 확인한다. 정확 variation/aux/slot·활성 프리셋·비용 변화·가득 찬 가방과 기존 용량/상자 권한도 직접 영향 표본으로 포함한다.
- **관련 현재판 회귀:** compile-check, core-tests, bridge protocol, execution-tests, life-tests, life-inventory-tests, life-craft-tests 및 최종 maintenance-tests를 통합 소스로 실행한다. 기존 adapter/binding source의 양쪽 변경도 보존한다. 가방 stub만 둔 정비 테스트나 정비 stub만 둔 가방 테스트는 교차 경계 검증으로 세지 않는다. 기존 baseline 모드 및 완료된 첫 단계 그래프 검수를 반복하지 않는다.
- **Unity:** 준비된 Unity 6000.0.59f2의 HeadlessSdk.CompileCheck를 다른 배치와 겹치지 않게 통합 worktree에서 실행하고, 실제 통합 SHA·exit·완료 표식·CS 오류를 기록한다. 환경용 Packages/ProjectSettings/SDK assembly를 제품 변경에 섞지 않는다. 설치·실게임·기존 프로세스 종료는 보류한다.

이번 preview는 두 고정판의 직접 통합 준비까지다. 새 사용자 정책, 그래프 확장, 과거 전수 복구 및 이미 완료된 첫 단위의 재검사는 범위에 포함하지 않는다.
