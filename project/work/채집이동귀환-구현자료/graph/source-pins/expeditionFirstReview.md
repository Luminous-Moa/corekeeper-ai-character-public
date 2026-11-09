# 채집·이동·귀환 독립 검수

검수자: `/root/expedition_review`. 제품 소스 수정 없음. 최초 동결판 `93a8bbc21dc544dfb86a9431d167798768d8a97e` (`5cc5a43` 이후). **현재 판정은 수정 대기 중인 1차 검수이며 최종 승인 아님.** 설치·실게임·외부 모델 호출은 수행하지 않았다.

## 확인한 계약과 근거

- HEAD start-job, C:/Dev/AGENTS.md, 확정합의, 역할판단, 공통실행 contract, direction을 읽었다. 모델/추론강도 과거 지정은 적용하지 않았다.
- 핵심: 사람과 같은 일반 시야, 다음 갈림길 방향만 선행 질문, 외길 코너 연속 실행, 미선택 갈래 진입 금지, 선택 당시 고정 채집 묶음, 실제 확보량 누적, 목표량만으로 자동 완료/귀환 금지, 명시적 엄격 상한 구별, 원작업·귀환 후속 연결.
- 게임행동-그래프 README 및 현재 published CLI `show completion:B:melee-pick-2-0:cost-followup-v1`를 조회했다. 정상 입력/타격/피해/파괴/실제 드롭은 별도 조건부 결과이며 정적 근거를 실게임 성공으로 보지 않는다.

## 발견 사항

### R1 · 높음 · 일반 시야가 사람 시야가 아님

`AIVision.cs:17-21,66-70`은 호스트 카메라 크기의 사각형에 들어오면 보인다고 판정하고 빛·어둠을 명시적으로 무시한다. 새 `AITravel.cs:42-58`은 이 판정으로 해당 타일의 실제 통행 상태를 읽고 영구 지도에 기록한다. `AIGathering.cs:50,63,138,154`도 같은 판정을 후보와 재검증에 사용한다. 따라서 어두워 사람이 식별하지 못하는 영역의 지형/자원을 일반 관측·경로·채집 대상으로 소비할 수 있다.

이 판정 자체는 기존 코드이지만 이번 합의가 유지하도록 요구한 사람 시야 기준을 충족하지 않으며 신규 기억 지도의 근거로 확대된다. `movement-evidence.md`의 “이번 범위에서 변경하지 않았다”는 합의상 예외가 아니다. 빛/어둠에 따른 실제 관측 경계의 확인과 보완이 필요하다. 정확히 어떤 게임 광원 상태가 어떻게 가려지는지는 실게임 미실행으로 미검증이다.

### R2 · 높음 · 폭 있는 외길에서 매칸 갈림길 정지

`AITravelRules.cs:22-33`은 4방향 인접 칸 중 직전 칸 하나만 제외한다. `TravelSkill.Tick` (`AITravel.cs:238-267`)은 출구가 2개 이상이면 모델 분기 선택을 요구한다. 폭2의 직선 통로도 앞 칸과 옆 칸이 각각 출구가 되어 한 칸마다 정지한다. 옆 줄로 이동하면 이전 진행 방향의 뒤쪽 칸도 선택에 다시 들어올 수 있다. 외길에서 코너·장애물 조향은 코드가 잇는 합의와 다르다.

독립 재현: `review-proof/ReviewProof.csproj`는 93a8bbc의 실제 AITravel/AITravelRules/AIGotoSkill/AISkill 복사본과 기존 travel-tests 물리 경계를 사용한다. 폭2×길이8 직선에서 (0,0)→(1,0) 이동 후 `input=0,0`, `travel_turn=동/북 2개`. 게임 입력 성공 검증이 아니라 생산 통로 상태기계 재현이다.

### R3 · 중간 · 고리 외길을 재판단 없이 반복 순환

`AITravelRules.NextJunction`의 visited는 선행 조회 루프만 끊는다. `TravelSkill.Tick`의 실제 실행에는 방문 간선/순환 관측이 없고 단일 출구를 계속 따라간다. 재현에서 폭1 고리8칸을 3바퀴 이동해도 24/24 tick이 이동, `Running`, 방향 후보0, 실행 관측0이었다. 이미 돈 외길이라는 사실을 알리고 다음 행동을 판단할 계기가 필요하다. 바깥 스케줄러의 정기 질문이 개입할 수 있으므로 “전체 시스템이 반드시 무한 실행한다”고 단정하지 않는다.

### R4 · 높음 · 명시적 엄격 상한 표현/실행 경계 누락

93a8bbc의 `AIGoal` (`AIDecisions.cs:76-96`)와 `PlanStep`은 목표 count만 갖고 엄격 상한을 구별하는 값이 없다. `AIGatherSkill.Tick` (`AIGathering.cs:239-289`)은 남은 고정 대상을 차례로 실행하며 확보량/명시 상한을 확인하지 않는다. 일반 목표량을 강제상한으로 만들라는 뜻은 아니다. “구리 10개를 절대 넘기지 말라”, 이미 9개 확보, 5대상 묶음을 선택한 경우 묶음 안에서 추가 실행을 막을 별도 의미 전달/검증 경계가 없다. 구현 담당이 누락을 확인·수용했다. 명시 max_count만 별도 실행 경계로 전달하고 불확정 native 자동줍기량은 보장하지 않는 방향으로 보완 중이다.

### R5 · 높음 · 빈 통로가 광맥 고갈 자동 보고를 억제

93a8bbc `AITravel.Compose`의 모든 explore가 `progress=true`이다. `AIGoals.CheckAfterBuild`는 HasProgress=true이면 고갈 Blocked를 만들지 않는다. `AIExpedition.AfterBuild` 또한 임의 explore/travel을 무조건 대안으로 간주한다. 광맥 고갈 후 빈 통로가 보이는 보통 상황에서는 “맡겨진 범위에 실행 대안 없음” 보고가 발생하지 않는다. 다른 새 탐험을 가능 후보로 유지하는 것과 이를 채집의 유효한 대안으로 단정하는 것은 별개다.

`review-flow/ReviewFlow.csproj`는 93a8bbc의 실행 하네스 어셈블리를 고정 복사해 실제 AIExpedition/AIGoals를 호출했다. 광맥고갈/구리후보없음 facts+progress=true explore에 `replans=0`, `goal observation=' / '` 재현. 구현 담당이 누락을 확인하고 실제 후보→GoalBuildFacts 연결을 보완 중이다.

### R6 · 높음 · 조기 귀환 목표로 바꾸면 이전 채집의 부족량·장소가 빠짐

93a8bbc `AIGoals.SetGoal`은 Gather의 where를 저장하지 않는다. `AIExpedition.Sync`는 목표 객체가 바뀌면 Suspended를 지우고, Report는 현재 goal.item의 누적량만 출력한다. `review-flow`에서 Gather50/where=tile:20,30→관측확보20→Home 목표 교체 시 원래 where는 빈 문자열, 귀환 보고는 `None 0`, `재개 대상 현재 작업 맥락 유지`였다. 원래 목표50/확보20/부족30/원래 장소를 구조적으로 남기지 않는다. 예전 보고나 모델 자체 기억이 있을 수 있으나 그것을 이번 합의의 맥락 보존 구현으로 대신할 수 없다. 구현 담당에 보완 요청했다.

### R7 · 높음 · 물체 목표의 접근 경로가 막힘으로 퇴행

`AITravel.ObserveView` (`AITravel.cs:54`)는 물체가 차지한 칸을 false로 저장한다. `AINavigator.cs:324,332`는 “목표는 물체일 수 있고 tolerance에서 멈춘다”는 목표칸 예외를 남겼지만 `AITravelRules.KnownPath`가 observed map의 false를 먼저 거절한다. `AIBase.cs:940,1048`의 Store/Take 및 `AIBreakSkills.cs:182`의 Break는 물체 칸 자체를 target으로 주고 가까운 tolerance에서 정지하도록 만들어져 있다. 따라서 떨어진 곳에서 상자·부술 물체에 접근하려 하면 인접 길이 열려도 정확 목표 경로가 null이 된다.

`review-proof`의 실제 KnownPath에 0..4 열린 길/5 물체 막힘을 넣으면, target을 허용하는 predicate=true여도 5까지 경로=null, 인접4까지는 4칸 경로가 나온다. 이는 순수 규칙 재현+생산 호출 연결 검수이며 전체 물리 navigator를 실제 게임에서 실행한 증거는 아니다. 실제 navigator를 stub한 기존 travel/gather 검사가 이 회귀를 가린다. 관측된 접근점/tolerance에 맞는 목적 경로를 검증해야 하며 대상물 칸을 무조건 통과 가능하게 하면 안 된다.

## 직접 실행한 검사

| 검사 | 결과 | 의미/경계 |
|---|---|---|
| travel-tests | 28/28 | 실제 AITravel/TravelSkill/GotoSkill, 물리 navigator/시야/ECS fixture |
| gather-tests | 32/32 | 실제 묶음 후보·snapshot·runtime·sequence, 물리 자식 기술 fixture |
| execution-tests | 71/71 | 생산 Apply/Stale/Body 종료의 발췌와 원정 상태, planner는 stub |
| core-tests | 1396/1396 | 일반 규칙 회귀 |
| restriction-tests | 47/47 | 후보/수량/지식공개 관련 기존 회귀 |
| life-tests | 98/98 | 생활 선택 연결 회귀 |
| maintenance-tests | 115/115 | 정비 선택 연결 회귀 |
| compile-check | 경고0 오류0 | 설치된 게임 참조를 통한 전체 생산 C# .NET 컴파일 |
| 독립 review-proof | R2/R3 재현 | 위 93a8bbc 고정 복사본 실행 |

`dotnet run`에 `--nologo`를 주면 core-tests가 이를 그룹명으로 받아 0개 검사하므로, 해당 오실행을 결과에 쓰지 않고 옵션 없이 재실행하여 1396을 확인했다. execution-tests의 .generated는 run.ps1이 생산 소스를 발췌한다. 최초 직접 실행은 구현자가 생성한 현재 발췌본을 사용했으며 최종판에서는 run.ps1을 다시 실행해 최신 원문과 맞출 예정이다.

## 확인된 연결과 남은 한계

- 묶음 선택은 readonly snapshot이며 새 대상 자동 편입이 없다. 기존 개별 후보가 남아 있다. entity/version·타일 서명 재검증, 가방 관측량과 완료 대상 수의 분리, 회수 단계와 중단 후 기존 대상 재선택은 생산 채집 하네스에서 확인됐다.
- 선행 조회는 별도 Look predicate이고 일반 map/자원 기억 쓰기가 없다. 현재 선행 방향 선택은 run/token/출구 재검증을 한다. 늦은 응답은 분기 공통 구간 끝에서 대기한다. R1의 일반 시야 누출은 이 경계와 별개다.
- 일반 목표량 도달은 observation으로 남고 몸을 자동 중단하지 않는다. 원정 메뉴 open/back/close는 Apply에서 몸 시작 경로를 타지 않는다. 기존 제한 해제 회귀는 유지됐다.
- `AIExpedition`은 후보 선택/확보량/곁일/원작업/상위 보고 상태를 연결한다. `AIPlanner` 기존 후속 계획 큐를 재사용한다. execution-tests의 귀환 후속 검사는 실제 `PlanQueue.Advance`를 직접 호출하므로 **전체 scheduler→상위 응답→귀환→보관의 자동 연결을 실게임 성공으로 검증한 것은 아니다**.
- Unity 최종 컴파일과 R1~R4 보완판 delta 검수는 대기 중이다. 전반적인 제품 승인/실게임 성공으로 보고할 수 없다.
