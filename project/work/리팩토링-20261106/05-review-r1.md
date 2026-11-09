# R1 독립 코드 검수

2026-11-06. 대상 저장소 `C:/Dev/CoreKeeperModSDK-refactor-20261106`, 고정 diff `a07166f..33f477b`. R2 동시 작업과 분리하기 위해 제품 코드와 시험 소스는 `git show 33f477b:path`, `git diff a07166f..33f477b`, `git grep 33f477b`로 읽었다. 제품 소스·브랜치·작업트리를 수정하지 않았다.

## 판정

**R1 독립 코드 검수 통과. Must-fix 발견 없음.** 승인 명세의 목표 공통 계약·기능 호출 경계·후보 이후 막힘 책임 분리에 부합한다. 현재 등록된 목표의 관측 가능한 동작이 달라지는 근거를 발견하지 못했다. 이는 고정 diff의 정적 검수 결과이며 게임 실행 통과를 의미하지 않는다.

기준: `C:/Dev/AGENTS.md`, HEAD Core `head-context/start-job/SKILL.md`, `C:/Dev/_ref/head-agent-core/graph/build/05-dev-planning.md`의 검수 기준, `00-agreement.md`, `01-spec-goals-candidates.md` R1, 승인된 `../2026-11/2026-11-06-리팩토링-현황과-계획/plan.md`. 과거 모델·추론 강도 지정은 적용하지 않았다. 제품 계약은 승인 명세의 보존 목록과 기준 커밋의 실제 분기·호출·출력 및 `Tools/ai-bridge/brief.md`를 대조했다.

## 직접 대조한 경계

아래 행의 소스 위치는 모두 `33f477b` 기준이며 경로의 `S/`는 `Assets/AIPlayerSpike/Scripts/`이다.

| 책임 | 근거와 판정 |
|---|---|
| 목표 선언 | `S/Core/AIGoalKinds.cs:5` enum 이름·배열 순서는 기준과 같다. `:34`의 12행은 None/Free 포함 승인 표와 일치하며 외부 wire는 10개이다. `All`과 `HostWords`는 읽기 전용 래퍼이고 상태·게임 형식·콜백·등록 프레임워크가 없다. Place 상한은 기존 순수 Core 상수 `AIPlaces.MaxShapeTiles`를 참조한다. |
| 명령 입력 | `S/AIGoals.cs:143` 이후 단어 비교만 교체되었다. craft/goto/place의 인자 개수 선행 조건, 분기 순서, 기본값, 사용법 문장과 Gather의 품목 fallback을 보존한다. None 취소 단어·auto·verbose는 별도 경계에 남았다. Gather에 새 명령 별칭을 만들지 않았다. |
| 상위 목표 적용 | `S/AIPlanner.cs:721`의 `TryWire` 실패 시 Follow fallback은 이전 삼항식의 최종 fallback과 같다. 품목 읽기 조건과 Place count<1의 1 정규화가 유지된다. 실제 적용 앞의 `CheckReason`은 첫 단계에 CheckGoal, 후속 단계에 StepForm을 호출한다. |
| 형태 검사와 세계 검사 | `S/AIPlanner.cs:766` StepForm과 `:789` CheckGoal은 별도 함수로 남았다. Gather/Craft 상한 30/10, 품목 검사 우선순위, Craft의 음식·레시피 검사 순서가 동일하다. 후속 Place 단계는 상한만 검사하며 하한은 추가되지 않았다. 시작 시 Place/Goto는 기존 `AIPlaceGoal.CheckPlace/CheckGoto`를 호출한다. ItemMode를 근거로 새 전역 검증을 추가하지 않았다. |
| 목표 시작 | `S/AIGoals.cs:201` SetGoal 본문은 diff에서 변경되지 않았다. Gather/Craft/Place의 inventory 시작값, Goto/Place 장소 초기화, Store 선택 품목, goal revision·source·기존 요청 취소 순서를 보존한다. |
| 후보 생성 | `S/AIBrain.Candidates.cs:72`가 실제로 `AIGoalFeatures.AddCandidates`를 호출한다. `S/AIGoalFeatures.cs:8`은 None 제외 Base 후보를 먼저, Free에서 FreeStoreCandidates, 그 뒤 Farm/Cook/Place를 기존 순서로 추가한다. 네 개의 개별 try/catch와 LogError 문장이 동일하다. WantedItems·Free ResetMissing 앞뒤 순서, 이후 edge/guard/door 후보 위치는 변하지 않았다. 후보 생성과 조립 알고리즘은 옮기지 않았다. |
| 완료 관측 | `S/AIGoals.cs:248`의 FollowTimeout → Feature → Inventory 순서. Follow는 LLM source·180초 조건을 보존한다. Feature/Inventory의 0.5초 또는 기술 종료 조건을 통과한 뒤 `_lastGoalCheck`를 먼저 갱신하고 기존 세계 조회를 한다. `S/AIGoalFeatures.cs:22`는 기존 모듈의 GoalDone으로만 연결한다. inventory 감소 때 startCount를 낮추는 동작도 동일하다. |
| 후보 이후 막힘 | `S/AIBrain.Loop.cs:659`의 prefetch 선행 탈출과 `:667` DropQueued 다음, 후보 수<2 처리 전에 `CheckAfterBuild`가 호출된다. `S/AIGoals.cs:302`는 Feature stalled → Inventory stalled → Explore exhausted 순서를 유지한다. Combat·진행 후보·frontier pending·현재 기술 조건, no_place/cannot_craft/null 분류, Trim 길이 220/260/100 및 출력 문장이 같다. InventoryCounts, Current, SeenCount는 기존 해당 분기 안에서 읽는다. |
| 임시 사실 수명 | `GoalBuildFacts`는 호출 시 만든 값 struct이며 저장되는 캐시가 아니다. 현재 판단의 필드 값과 순수 `cands.Exists(c => c.progress)`만 전달한다. 다른 캐릭터의 상태를 새로 공유하거나 비동기 단계로 옮기지 않았다. AIBrain.Goal은 기존 AIGoals.Goal forwarder이고 두 파일의 Trim 구현도 동일하다. |
| 종료 순서 | `S/AIGoals.cs:353` EndGoal 본문은 변경되지 않았다. Follow 완료·Feature/Inventory 막힘은 ClearStopReport, Feature/Inventory 완료·탐험 소진은 ClearReportStop을 그대로 쓴다. only-wait와 요청 스케줄 상태는 AIBrain에 남았다. |
| 표시·경험 key | `S/AIDecisions.cs:89`의 Text는 `S/AIGoalText.cs:4`로 원래 삼항식 본문을 이동했다. `S/AIExperience.cs:62`의 null goal은 빈 문자열. `S/Core/AIGoalKinds.cs:69`는 None/Free 빈 값, Gather/Craft의 `:None`, Explore/Follow 품목 무시, 나머지 선택 품목을 보존한다. 등록 밖 enum은 기존 ToString().ToLowerInvariant와 hasItem 조건을 사용한다. |
| 보고 순서·외부 계약 | `S/AIPlanReport.cs:86`의 can_do JSON 필드 및 게임별 값 계산 순서는 그대로이고 이름만 WireName 조회다. `:101` expKeys의 gather→craft→explore/follow/farm/cook/goto→place 순서, store/home 생략, 품목 목록과 split은 동일하다. production protocol과 brief는 변경되지 않았고 brief의 열 가지 목표·Gather 1~30/Craft 1~10/Place 30칸 설명과 일치한다. |

## 책임 분리 평가

선언의 소유자는 `AIGoalKinds`, 표시의 소유자는 `AIGoalText`, 목표 수명과 종료 판단의 소유자는 `AIGoals`로 드러난다. `AIGoalFeatures`는 43줄의 호출 경계이며 실제 Base/Farm/Cook/Place 알고리즘·지속 상태를 가져오지 않았다. 새로운 중앙 실행 허브나 불필요한 확장 장치가 생기지 않았다. 요청 준비와 only-wait는 AIBrain에 남아 주요 실행 순서도 읽을 수 있다.

`ItemMode`가 모든 게임 입력에 강제 적용되지 않는 점, Place 시작값을 Inventory completion으로 묶지 않는 점은 누락이 아니라 승인된 경계 차이를 보존한 것이다. 기능별 분기를 모두 없애려고 명령·세계 검사·후보 생성까지 하나의 테이블에 넣을 이유는 없다.

## 시험 근거와 한계

- 직접 실행한 읽기 전용 검사는 `git diff --check a07166f..33f477b`이며 오류가 없었다. R2가 같은 트리를 변경 중이므로 시험·컴파일을 재실행하지 않았다.
- 작업자의 core 1043, protocol 106, 전체 gate 실패 0 보고는 보조 근거로만 받았다. 이 보고서에서 독립 재실행 성공으로 표기하지 않는다.
- 새 `GoalContractTests.cs`와 공유 fixture, protocol 시험을 직접 읽었다. enum 등록 누락·중복, 외부 vocabulary, 별칭, bridge 정수 범위 비제한, C# parser의 값 보존, 상한 메타데이터, 경험 key 특수값을 확인하는 데 유효하다. 기존 기대 fixture를 새 출력으로 갈아 끼운 변경은 없다.
- **비차단 검증 한계:** C# fixture 루프는 bridge가 수용한 사례만 parser로 읽으며 실제 `AIPlanner.CheckGoal/StepForm`을 실행하지 않는다. Place 후속 단계 하한 비적용 등은 소스 문자열 검사로 보호된다. 따라서 이 시험 수를 실제 게임 세계 조건·완료/막힘 연결의 실행 증거로 해석하면 안 된다. 이번 정적 검수에서는 해당 consumer와 분기를 직접 대조했고 동작 차이를 찾지 못했다.
- null goal의 `AIExperience.KeyOf` 가드는 실제 소스에서 보존을 확인했다. 순수 경험 fixture는 이 Unity 측 wrapper를 실행하지 않는다. unknown enum은 fixture와 실제 fallback 소스 양쪽으로 확인했다.
- 최종 승인 명세의 실게임 대표 경로—호스트 제작/놓기 → 상위 수용 → 후보 → 이동/문 → 실행 → 완료 또는 알려진 막힘 → 경험/다음 판단—및 목표 교체 후 오래된 응답 폐기는 별도 실행 확인이 남아 있다. Unity 컴파일·실게임·라이브 모델을 이 검수에서 실행하지 않았다.

## HEAD 반환

R1 소스 수정 요청은 없다. 이후 R2와 단계 3~4의 고정 커밋 검수를 이어갈 수 있다. R1 정적 검수 통과를 기록하되, 최종 실게임 미확인 범위는 유지한다.
