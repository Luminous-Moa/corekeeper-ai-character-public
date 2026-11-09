# 생활 트리 첫 단위 독립 검수

## 최종 판정

**최종 후보 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`은 이번 첫 단위의 합의·주요 실행에 대한 독립 검수를 통과한다. 미해소 blocker/중요 오류는 확인하지 않았다.** 중간 고정 구현 `24c61c246053237784c057cf97282b9ca0c5d564`에서 발견한 R1~R3은 수정 후 직접 영향 재검수와 독립 실행으로 해소를 확인했다. 수정 전 근거는 아래에 보존한다.

이 판정은 공통 탐색과 제작·요리·농사·보관/꺼내기의 **격리 구현·오프라인 생산 파이프라인·SDK/Unity 컴파일** 범위다. 전체 생활 기능, 실제 모델/API와 실제 게임 소비자의 성공, 정식 설치·배포·실게임 완료 판정이 아니다. 설치·실게임 보류는 유지한다. 공식 코드/그래프 반영과 작업 결과 저장은 부른 HEAD가 판단한다.

검수 기반은 `1fc89b93edbee31a9001a174581e32568119efc1`이며 고정 구현의 조상임을 Git으로 확인했다. 진행 중 작업트리 변경을 고정 구현으로 혼동하지 않고 `git show 24c61c2:<path>`로 읽었다. 제품 코드, 설치, 게임, 다른 작업 후보는 수정하지 않았다.

## 기준과 분담

`C:/Dev/AGENTS.md`, `head-context:start-job`, `00-agreement.md`, `01-direction.md`, `02-direction-decision.md`, `03-spec.md`, `03-spec-decision.md`, `04-bag-dependency.md`를 기준으로 대조한다. 첫 단위는 공통 탐색과 제작·요리·농사·보관/꺼내기이며 후속 생활 기능이나 새 정책을 확장해 검수하지 않는다.

주 검수자는 공통 커서·응답·상위 selector·진척·후보 조립을 확인하고 하위 독립 검수의 근거를 직접 대조해 통합한다. 농사/보관은 `07-review-farm-inventory.md`, 요리/제작은 `07-review-cook-craft.md`의 분담 근거를 받는다. 각 하위 검수의 파일 소유는 해당 보고서로 제한했다.

## 수정 전 발견 사항

### R1 — 미지정 품목/재료를 현장에서 좁힐 때 상위 수량이 선택별로 재시작됨

중요 오류. `AILife.cs:82`의 `ProgressKey`는 상위가 지정한 Item/Second만 덮고 미지정 값은 현장 선택 그대로 남긴다. `Core/AILifeTree.cs:80`의 Identity에는 Item/Second가 들어가며 `LifeProgress.Record/Remaining`은 그 Identity로 집계한다(`:104`, `:109`).

예를 들어 상위가 `farm/plant/count6`만 주고 종자를 현장 선택에 남기면 종자 A의 여섯 칸을 확인한 뒤 종자 B로 바꿀 때 새 여섯 칸을 다시 받을 수 있다. 요리의 미지정 상대 재료 변경도 같은 경계다. 현장 선택이 상위 미지정 값을 좁히면 원래 count의 잔량을 쓴다는 명세 3·6·8과 충돌한다. HEAD는 구현자도 원인을 확인했으며 canonical goal progress key 수정과 생산 회귀를 추가 중이라고 통보했다.

수정 후 확인할 사항은 미지정 종자·첫/둘째 재료 변경의 잔량 공유, 명시된 다른 품목/작업의 별도 행동은 진척 0 유지, 동일 종료 통지의 중복 집계 방지다.

### R2 — 상자의 다른 품목 슬롯 이동이 원래 품목 목표의 진척으로 합산될 수 있음

중요 오류. 보관/꺼내기 커서는 item을 설정하지 않고 정확 slot을 고른다. `AILife.cs:43`은 빈 selected item을 일치로 인정하고, `AIInventory.cs:234`의 `LifeQuery`는 그 빈 Item을 None으로 바꾼다. 따라서 `:279`의 source filter는 다른 품목도 허용한다. `ExactCandidate:294`는 실제 슬롯 품목을 목표와 대조하기 전에 `selection.MatchesGoal`만 보고 원래 목표를 `progressGoal`로 넘기고 `:296`에서 정확 품목 binding도 원래 selection의 복사로 덮는다. `Completed:628`은 목표 reference가 같으면 해당 실제 이동량을 원래 목표에 합산한다.

재현 조건은 현재 `take Wood/count3/chest:1,0`에서 트리로 같은 상자의 Ore 슬롯을 선택하는 것이다. 원래 부탁과 다른 품목을 원래 Wood 단계로 합산할 수 있어 명세 6·7.4·8의 핵심 경계에 어긋난다. HEAD에 즉시 전달했고 구현자가 수정 중이다.

독립 경계 표본은 결과폴더 `review-farm-inventory-boundary/ReviewBoundary.csproj`에서 고정 생산 AIInventory/InventorySkill을 직접 빌드했다. 현재 Wood 목표 count1에서 CopperSword 슬롯에 잘못된 현재 목표 binding을 전달하면 실제 LifeEntries→ExactCandidate→InventorySkill의 정상 MoveAmount/양쪽 슬롯 receipt를 통해 `Wood_goal_moved=1`, `Wood_goal_done=True`를 재현했다(exit0). 빈 Item을 goal 일치로 보는 AILife.Matches는 정적으로 대조했고 그 잘못된 binding은 표본의 입력으로 제공했다. 전체 공통 navigation 경로를 그 표본에서 실행했다고 확대하지 않는다.

수정 후에는 실제 source 품목을 해석한 뒤 목표 일치·잔량·progressGoal를 판정하는지, 다른 품목은 한 개의 별도 행동으로 남고 원래 moved/done은 불변인지 확인한다.

### R3 — 직접 제작 후보가 실제 출력 단위와 다른 기존 재료 gate에 막힘

중요 오류. `AIBrain.Prep.cs:76`, `:143`은 `AICraft.Materials`의 결과로 ExactCandidate 호출 여부를 먼저 정한다. 기존 Materials는 `AICraft.cs:235`에서 `required.amount * 요청 품목 수`를 계산한다. 반면 실제 `ReadRecipe:144`는 한 회 출력을 읽고 `:145`에서 필요한 회수를 올림 계산한다.

한 회 출력 2, 품목 3개 요청, 한 회 Wood 1, main Wood 2인 경우 트리의 실제 ExactCandidate는 두 회/예상 네 개/비용 두 개로 가능하지만 Prep은 Wood 3이 필요하다고 보고 직접 제작 후보를 만들지 않고 불필요한 재료 준비를 제시한다. 명세 6·7.3의 직접 지정/말단 동일 생산자와 실제 레시피 출력·회수 구별을 충족하지 않는다. 독립 모듈 검수자의 근거를 주 검수자가 직접 소스 대조했고 HEAD에 전달했다.

고정 생산 함수를 직접 실행한 독립 최소 표본은 `exact=True, calls=2, expected=4, actualCost=2, legacyNeed=3, bag=2, legacyGate=False`를 반환했다(exit0). 이는 생산 ExactCandidate/Materials/HasMaterials의 실행 결과와 전체 Prep의 정적 호출 대조이며, 전체 WantedItems/BuildCandidates를 실제 실행한 표본은 아니다. 상세 근거는 `07-review-cook-craft.md`에 있다.

주 검수자도 두 고정 경계 프로젝트를 각각 `dotnet run -c Release --no-restore`로 직접 다시 실행해 R2의 잘못된 Wood 목표 충족과 R3의 실제 비용/legacy gate 차이 출력을 확인했다. 두 명령 모두 exit0이며, 이는 수정 전 결함 재현 성공을 뜻한다.

수정 후에는 실제 레시피 생산자가 legacy gate 전에 직접 실행을 판단하는지, 동일 입력의 직접/트리 후보가 같은 시설·회수·비용을 가지는지 확인한다.

## 정적 경계 대조

- `AILifeTree`는 Unity와 분리돼 있으며 owner/goal/cursor revision과 제시된 nav 값을 확인한다. back은 마지막 선택 하나, root/close는 경로와 페이지를 초기화한다.
- `AIBrain.Loop.cs:821`의 nav 응답 분기는 prefetch queue·ApplyOrHold보다 앞에서 반환한다. 성공/실패 모두 몸 시작·중단, held/queued 교체, 목표 변경, 실행 성공 집계로 흐르지 않는 구조다.
- `AILife.Compose`는 생활 페이지와 back/root/close/prev/next를 기존 14개 cut 뒤에 조립하고, 공통 제어는 `AddCommonControls`에서 마지막에 추가한다. 전투에서 생활 실행·nav를 제거하고 커서를 보존한다.
- 상위 mode/second는 Protocol→PlanStep→AIPlanner→AIGoals로 전달되며 농사/요리 count의 형식 검사는 누락과 잘못된 지정값을 구분한다. 범위는 rect 양끝 정규화 후 관측 타일 필터로 전달된다.
- 결과는 ILifeResult의 작업량/실제 수령량을 별도로 읽고 run으로 중복 집계를 막는다. 목표 reference/revision이 달라진 이전 결과를 새 목표 진척으로 더하지 않는 구조다. 위 R1/R2 때문에 원래 목표와의 일치 판정까지 올바르다고 수용한 것은 아니다.

## 중간 검증의 지위

검수 시작 시 통보받은 fast compile/Core1295/bridge154/common36/cook42/craft11/inventory79/farm16은 구현자의 중간 검증이다. 그 숫자를 최종판의 독립 전회귀 실행으로 옮기지 않았다. `Tools/life-tests`의 README와 추출 경계를 읽어 생산 HandleResponse·Apply·AILife·FarmSkill을 쓰는 구성과 ECS/입력/몸 실행 어댑터 범위를 확인했고, 최종 고정 커밋과 실제 로그는 아래와 같이 추가 대조했다.

전체 CandBuild/전체 Tick 스케줄러, 실제 모델/API 및 Unity 게임 소비자의 성공은 어댑터 시험에서 증명되지 않는다. 설치·실게임은 기존 보류이며 미확인이다.

## 수정 후 검수 — c4a0f5e

후속 고정 후보는 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`이다. `24c61c2..c4a0f5e`의 제품·검증 diff와 작업트리 status를 직접 확인했다. 제품/Tools 미커밋은 없고 Unity 환경 사본만 남는다. 이 절은 수정 전 R1~R3의 근거를 덮지 않는다.

R1: `ProgressKey`가 Mode/Item/Second/Where를 모두 원래 목표의 지정값 또는 빈 wildcard로 정규화한다. 같은 상위 목표에서 현장 종자·재료·출력 종류를 좁혀도 count를 공유하고, 명시 selector 충돌의 MatchesGoal=false는 Record 보호를 유지한다. 주 검수자가 최종판 `Tools/life-tests/run.ps1`을 직접 실행해 **96/0, exit0**을 확인했다([독립 최종 생활 로그](review-final-life.log)). 실제 Protocol→SetGoalFrom/Goal→트리→HandleResponse/Apply→FarmSkill의 A4+B2 결과와 양쪽 종자의 추가 leaf 차단, 명시 SeedA와 다른 SeedB1 진척 미합산을 확인한다. cook의 미지정 second/출력별 공유는 실제 AILife에 구조화 결과를 전달하는 경계 표본이며 CookSkill 자체를 이 harness에서 실행하지 않는다.

CostAllows: 기존 Tick의 `usage.usd >= monthlyCapUsd` 분기를 같은 위치의 `if (!CostAllows(now)) return`으로 추출한 정적 동등성을 확인했다. 원래 log/reply/retryAt+30은 유지하고 return은 false로, 통과는 true로 반환한다. 새 비용 정책은 없다. 위 독립 96건에는 상한 직전 허용, 상한에서 새 호출 보류와 retry30, 현재 probe·held/queued·목표·실행 카운터 불변 검사가 들어 있다. 전체 Tick 스케줄러 실행 증거로 확대하지 않는다.

R2: 실제 열린 자기 상자의 source 슬롯을 먼저 읽고 그 ID/variation/slot을 Resolve에 전달해 일치 여부·요청량·progressGoal를 다시 판정한다. 미지정/base 목적지는 기존 BaseRadius 계약과 같은 거리식을 사용한다. 다른 품목을 숨기지 않고 별도1개로 노출하며 원래 keep/count/진척을 다른 품목에 적용하지 않는다. 하위 독립 재검수는 최종 고정 생산 InventorySkill과 AILife의 Sync/Matches/Bind/Resolve/Remaining 원문 prefix를 직접 컴파일해 **18개 검사 통과, exit0**을 확인했다. 중간 표본의 수동 MatchesGoal 주입 한계는 이 최종 표본에서 해소됐다. take/store 양쪽에서 Ore1→Wood 진척0/미완료→Wood3→진척3을 확인하고 take 확보3/store 확보0도 구분했다. 주 검수자가 수정 diff·최종 표본/보고서와 구현자 91/0 로그를 대조했다([독립 보관 최종 로그](review-farm-inventory-boundary-final/boundary.log)).

R3: 전체 생산 WantedItems가 실제 ExactCandidate를 먼저 호출하고 실행 후보가 있으면 legacy 준비 추정을 건너뛴다. 재료 부족으로 실행 불가인 실제 runtime recipe의 비용/출력 메타데이터는 준비 부족량 설명에 재사용하되, 실행 가능은 ReadRecipe bool 성공을 계속 요구한다. 전체 AIBrain.Prep.cs를 직접 컴파일한 독립 최소 표본 **2/0, exit0**에서 Wood2/요청3/출력2의 직접·트리 key/station/2회/예상4/비용2가 같고 불필요 준비가 없는 것, Wood1일 때 실행 불가·부족1인 것을 확인했다. 이는 수정 전의 생산 함수+Prep 정적 대조보다 강한 **전체 WantedItems 실행** 근거다([독립 제작 최종 로그](review-cook-craft-boundary-c4a0f5e/craft/run.log)).

추가 직접 영향: 요리의 같은 재료 1+1 분리 스택을 두 정확 source로 고정해 투입1/음식 확보0을 확인하고, 2+2+2+2 스택에서 전체가능4/이번 고정묶음2/부족0을 구별한 독립 **4/0, exit0** 표본을 대조했다([독립 요리 최종 로그](review-cook-craft-boundary-c4a0f5e/cook/run.log)). 농사 소수 diff는 명시 mode의 도구 준비·실제 dug ground·상위 where의 6칸 전체제한 방지·확인된 종자 기억/유지관리 준비 복원에 한정된다. 명시 mode는 앞의 ExactCandidate 반환을 유지해 다른 종자/야생 수집으로 대체하지 않는다. 화면 밖 기억과 현재 관측·소유 권한 경계를 확장하는 새 중요 오류는 확인하지 않았다.

## 최종 검증 근거와 한계

[05-implementation.md](05-implementation.md), [final-validation.json](logs/final-validation.json), 실제 각 로그를 직접 열어 최종 후보와 결과를 대조했다.

| 근거 | 확인 결과 | 검수의 수행 구분 |
|---|---|---|
| fast compile | 경고0 / 오류0 | 최종 구현자 로그 열람 |
| Core / bridge / 공통 실행 | 1295 / 154 / 36 통과 | 로그와 변경 입력 경계 대조, 전체 독립 재실행 아님 |
| 생활 생산 파이프라인 | 96/0, exit0 | 주 검수자가 최종 고정판에서 직접 실행 |
| 요리 / 제작 / 보관 생산 어댑터 | 51/0 / 19/0 / 91/0 | 최종 로그·테스트의 생산 연결 대조 |
| 보관 / 제작 / 요리 수정 후 독립 최소 표본 | 18 / 2 / 4 통과, 각 exit0 | 하위 독립 실행과 주 검수자의 근거 확인 |
| Unity6000.0.59f2 | 최종 SHA 입력 exit0 / compiler error0 / CompileCheck:ok 1건 | 로그/실행 메타데이터 직접 대조, 독립 Unity 재실행 아님 |

Unity의 [시작 기록](logs/unity-start.json), [종료 기록](logs/unity-exit.json), [요약](logs/unity-summary.json)은 모두 sourceCommit `c4a0f5e38016c79567f880bbaea5e56e0eeae347`를 가리킨다. 최종 `unity.log:1814`의 `[HeadlessSdk] CompileCheck: ok`와 compiler error 패턴 없음, 배치 PID41772의 종료코드0을 함께 확인했다. 초기 Unity 성공과 최종 수정판의 성공을 구별했으며 BuildAndInstall/게임 실행 근거로 확대하지 않는다. 최종 제품/Tools status는 깨끗하고 환경 사본만 남은 것을 직접 확인했다.

생산 응답/Apply/skill/receipt를 실행한 어댑터 증거를 전체 CandBuild·전체 Tick·모델/API·실제 게임 소비자 검증으로 확대하지 않는다. 요리·제작·상자 최종 버퍼 변화와 경계 데이터는 fixture가 공급한다. 남은 실게임 대표 흐름은 보류 해제 후 실제 모델 탐색→정상 게임 입력/소비→가방 결과→다음 판단이다. 수리·자동 가공/제련·강화·거래·낚시의 새 실행은 후속 단위로 남으며 이번 통과에 포함하지 않는다.
