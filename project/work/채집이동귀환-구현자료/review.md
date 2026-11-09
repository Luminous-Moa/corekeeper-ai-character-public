# 채집·이동·귀환 독립 검수

검수자: `/root/expedition_review`. 제품 소스 수정 없음. 최초 동결판 `93a8bbc21dc544dfb86a9431d167798768d8a97e` (`5cc5a43` 이후). 최초 발견과 당시 판정은 아래에 이력으로 보존한다. 현재 판정은 다음 2026-11-08 범위 조정 항목을 따르며, 코드 검증 근거는 3차 검수 기록을 유지한다. 설치·실게임·외부 모델 호출은 수행하지 않았다.

## 현재 판정 · 2026-11-08 사용자 범위 조정

**사용자의 “조명 시야는 후속으로 분리” 결정에 따라 이번 채집·이동·귀환 구현 범위를 수용한다. 조명 시야 R1은 해결되지 않은 별도 후속 항목이다.** [최신 범위 결정](../2026-11/2026-11-08-채집이동귀환-조명후속분리.md)과 [확정 합의 상단](../2026-11/2026-11-07-채집이동귀환-확정합의.md)을 확인했다.

이번 수용 대상은 `7af8f91e5d26e4fd9ce5996809ae41e213340118`의 구현과 기존 화면 범위 관측이다. 제품 변경 없이 HEAD가 본소스 ai-character에 fast-forward 통합한 상태이며, R2~R7 수정·오프라인 생산 경로·최종 Unity 컴파일의 기존 독립 검수 근거를 유지한다. 동일 코드 검수와 테스트는 반복하지 않았다.

빛·어둠까지 사람과 동일한 시야를 구현했다고 주장하지 않으며 조명 구조도 이번에 확정하지 않는다. 설치·실게임·외부 모델 시험은 기존 보류 방침을 유지한다. 개발 지식그래프 정본 반영의 delta 독립 검수는 별도 후속이며, 이 상태 갱신은 그래프 검수 완료를 의미하지 않는다. 아래 3차 이하 판정은 당시 범위에 따른 이력이다.

## 3차 최종 코드 검수 · 7af8f91e5d26e4fd9ce5996809ae41e213340118

**시야 R1을 제외한 이번 구현 범위는 수용 가능하다. R2~R7은 수정 확인했고 추가 중대 블로커는 발견하지 못했다. R1은 여전히 미해결이므로 전체 합의 완료·실게임 성공·배포 승인으로 해석하지 않는다.** 검수 기준 제품/Tools 소스가 이 commit과 동일함을 `git diff --exit-code 7af8f91 -- Assets/AIPlayerSpike/Scripts Tools`로 확인했다. 최종 tracked clean, 미추적 Assets/Plugins/CoreKeeper는 Unity compile용 읽기복사이다.

### R4 수정 확인: 실제 파괴 입력과 원부탁 수명

- `Assets/AIPlayerSpike/Scripts/AIGathering.cs:121`의 LimitReason은 progress/itemId만으로 파괴 산출을 추정하지 않는다. mine/edgemat, root, break, 실제 dig 경로 벽, farm harvest 및 고정 묶음의 다음 대상을 검사한다. 관측으로 산출 상한을 확정하지 못하거나 남은량보다 불가분 stack이 크면 이유를 남겨 보류한다. 일반 count에는 이 상한이 생기지 않는다.
- 선택 검사만으로 끝내지 않는다. `AIMineSkill.cs:72,113`, `AIBreakSkills.cs:31,53,138,166`, `AITunnel.cs:476`, `AIFarm.cs:1045,1129`의 실제 Start/Tick/다음 자식 실행 경계가 신규 조건을 다시 확인한다. Tools/gather-tests/native/run.ps1 -Baseline에서 e1cedcf의 실제 MineSkill/TunnelSkill primary 우회를 독립 재현하고 수정판 14개 검사를 다시 통과시켰다. Farm 실제 생산 경계는 life105에 포함된다.
- `AIExpedition.cs:107` BindPlanLimits와 실제 AIPlanner 계획 수락부가 미래 gather의 max_count/max_order도 Home 등 선행 단계 전에 등록한다. `AIExpedition.cs:127` ActiveLimits는 원부탁 소유와 실행중 BodyOwners를 사용하고, :87~88은 품목별 실제 증가를 누적한다. 귀환·보관·목표 source 교체·미룸/재개에도 누적 조건을 보존한다. 다른 별개 원부탁에 조건/획득량을 자동 전파하지 않는다. 원부탁 완료 시 조건을 해제한다.
- 다중 원부탁에서 모호한 소유와 기존 사용자 상한의 무단 변경은 실제 계획 수락 경계에서 거절하고 기존 몸/goal을 유지하며 재계획 사유를 보낸다. 공동 계획이 명시적으로 함께 수행하는 원부탁들은 각 관련 품목 조건과 실제 획득을 함께 반영할 수 있다는 HEAD 해석을 적용했다. 별개 계획의 자동전파와 구분했으며 새 자원배분 정책을 요구하지 않았다.
- passive native 자동줍기와 불가분 native 결과의 절대량 보장을 주장하지 않는다. 검증된 것은 명시 상한의 소유·누적·보고 및 다음 유발 입력의 보류 경계이다.

### R5 수정 확인: 후보 생산자와 자동보고/후속 소비

- `AIBrain.Candidates.cs:646,697`의 실제 lExplore/lDigExplore 생산자는 GoalKind.Explore일 때만 progress를 표시한다. Gather/Home/Goto에서 후보 자체는 남는다. Tools/travel-tests/candidate-progress는 실제 initializer/HasProgress를 추출하여 생산 Goals.CheckAfterBuild/Expedition.AfterBuild로 검사한다. 일반 탐험만 남은 고갈은 보고하고, 목표 품목의 실행 가능한 기억 대안은 고갈 보고를 억제하며 다른 품목 기억은 오인하지 않는 11개 검사를 직접 통과했다.
- 생산 Apply의 continue/동일 key/유효 travel_turn도 `AIExpedition.Choosing`(:227)을 통과하여 새 기회 이탈 보고를 놓치지 않는다. 첫 보고 pending 중 새 사실을 실제 AddEvent로 넣고 pending dispatch 후 실제 Report의 events와 expedition_observation 직렬화/오프라인 HTTP payload에 남는 경로를 확인했다.
- Tools/execution-tests 92개는 실제 OnGoalEnded → StartNextStep → SetGoalFrom 소비를 사용한다. finish_goal 이후 Home→Store→Craft, Gather50/획득20→Home→Store→명시 Gather30 및 기존 위치 복귀를 검증한다. 수동 PlanQueue.Advance가 생산 소비를 대신하지 않는다. 요청 중 몸 유지, 후속 보고와 원부탁 상한 수명도 함께 검사한다.

### 직접 검증 결과와 한계

최종 코드에서 직접 실행: execution92, gathering61, native14(수정 전 실제 우회 재현 포함), candidate-progress11, core1418, life105, maintenance115, restriction47, bridge protocol188 및 ceiling/owner25. 전체 .NET compile은 경고0/오류0이다. Travel43/생산 AINavigator9는 e1cedcf 검수에서 직접 통과했고 해당 이동/길찾기 소스는 최종판에서 변경되지 않았다. 경로 접근 tolerance, 중간벽·대각 모서리 부정 사례의 근거는 2차 기록과 movement-navigator-tests.log에 있다.

최종 Unity는 구현자가 같은 7af8f91 제품 소스로 실행했다. 검수자가 `unity-final-v2.log:1814`의 `[HeadlessSdk] CompileCheck: ok`, compiler failure 부재, 정상 shutdown 및 `unity-final-v2-exit.txt`의 0을 직접 확인했다. 이 로그에는 메뉴 등록 한도와 종료시 Curl callback 경고가 있으나 C# compilation failure는 없다. 이전 unity-final 로그는 최종 성공 근거로 쓰지 않았다.

독립 실행은 생산 파일 링크/메서드 추출과 물리·HTTP fixture를 사용한 오프라인 검증이다. 모든 ECS/물리 scheduler, 실제 회수·상자 입력, 게임 실행, 외부 모델 응답, 설치를 검증한 것은 아니다. renderer와 사람 시야 동등성 R1은 사용자 선택 대기이다. R1의 노출이 현재 이동지도/채집 후보에도 영향을 미친다는 점을 그대로 유지한다.

## 2차 검수 · e1cedcf9033f5ec72f43555a27b3f59a9b565db1

**당시 판정(후속 수정 전): 시야 이외도 수용 불가, R4/R5 잔여 경로가 있었다.** R1은 사용자 아키텍처 결정 대기이며 해소로 취급하지 않았다. 아래 최초 발견 기록은 보존한다.

- R2/R3: 폭2 직선·폭3 코너·열린방 조향·십자 분기, 실행 유향간선 순환 관측이 추가됐다. 실제 TravelSkill 하네스 43/43 직접 통과. 넓은 통로의 측면 칸을 매칸 독립 분기로 처리하던 경로와 반복 고리 문제는 수정 확인.
- R6: WorkRecord가 원래 목표/source/확보시작/누적/부족량/명시 where 또는 선택 위치를 보존한다. Gather.where가 이제 보존된다. 생산 `OnGoalEnded`→`StartNextStep`→`SetGoalFrom`를 추출한 오프라인 하네스로 실제 finish_goal 이후 Home→Store→Craft 및 조기귀환 후 Gather30/원위치 소비가 검증된다. 과거 수동 Advance 검사는 제거됐다. 원작업 기록 보존 문제 수정 확인.
- R7: 열린 접근칸이 tolerance 이내이고 목표까지 사이가 관측상 막히지 않은 경로 종점만 허용한다. 실제 AINavigator/AITravel 링크 하네스 9/9 직접 통과: Store/Take/Break 거리의 접근, 대상칸 비통행 유지, 중간벽·대각 모서리·벽너머 숫자상 근접의 부정 사례. 물리/문/게임 실환경은 여전히 경계 fixture이다.
- R4 일부 수정: optional max_count가 bridge/protocol/PlanStep/planner/goal/report로 전달되고 일반 count는 상한이 아니다. 직접 선택·과거 응답·매대상 실행·새 상한이 도착한 실행중 몸을 확인한다. 불확정 native 산출/남은량보다 큰 불가분 stack은 보류한다. passive 자동줍기의 절대량 보장으로 주장하지 않는다.

### 2차 잔여 R4 · 높음 · 비진척 dig 및 edgemat 입력이 상한 검사를 우회

`AIGathering.LimitReason`은 `candidate.progress || candidate.itemId == goal.item`일 때만 상한을 적용한다. 실제 적·다른 자원·곁일 물체를 향한 dig는 progress=false/itemId=None일 수 있고, TunnelSkill 경로 중 제한 품목 광석을 캘 수 있다(`AIBrain.Candidates.cs:245,381,449,541`). 목표 진척 플래그는 실제 산출 품목의 증거가 아니다.

`review-limit-e1cedcf`에서 실제 AIGathering이 링크된 고정 GatherTests.dll을 호출했다. Copper max10/관측10에서 동일 dig에 progress=true면 보류, progress=false면 **ALLOWED**다. 기존 테스트는 progress=true만 확인한다. 또 `AIActions`의 edgemat→MineSkill은 LimitReason 및 AIExpedition.Receives 분류에서 빠져 있다. 실제 채굴 대상/경로의 제한 품목·불확정 산출 확인으로 닫아야 하며 passive 자동줍기 한계와 다른 새 채굴 입력 우회다. 구현자에게 전달했다.

### 2차 잔여 R5 · 높음 · 기억한 frontier 탐험은 여전히 채집 대안

`AIBrain.Candidates.cs:696` lDigExplore의 “기억한 탐색 끝” 후보는 kind=dig/progress=true이고 wantExplore=true로 Gather에서도 생성된다. AITravel.Compose는 kind=explore만 치환하므로 이 후보는 최종 목록에 남아 HasProgress/AfterBuild 대안으로 계산된다. 보이는 통로 explore의 수정만으로 모든 일반 탐험 경로가 분리된 것은 아니다. 새 테스트는 progress=false explore를 직접 넣어 이 생산자 경로를 거치지 않는다. 구현자에게 전달했다.

직접 재실행: execution run.ps1 최신 소스추출 78/78, gather52, travel43, 실제 navigator9, core1409, restriction47, life100, maintenance115, bridge strict14 통과. 전체 .NET compile 경고0/오류0. Unity 결과는 구현자 진행 중이며 후속 결과를 확인해야 한다.

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
