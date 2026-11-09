# 현재 소스 독립 조사 — a07166f

2026-11-06. 목적은 흩어진 같은 규칙과 한곳에 섞인 서로 다른 책임을 정리하여 이후 유지보수를 쉽게 만드는 것이다. 단계 4 준비만으로 범위를 제한하지 않는다. 구현·설치·게임/Unity 실행·브랜치 변경 없이 현재 소스와 인계, 개발 그래프를 읽었다. 아래 개선은 제안이며 구현 승인이 아니다. 과거 개발용 모델·추론 지정은 적용하지 않았다.

경로 약어: `S/` = `C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/`, `B/` = 같은 저장소의 `Tools/ai-bridge/`. 줄은 현재 작업트리 a07166f 기준. 관찰과 그에 대한 제안을 구분한다.

## 1. 실제 기준과 규모

- 브랜치 `ai-character`, HEAD `a07166f`. 조사한 모드 Scripts와 bridge/core-tests 추적 파일의 HEAD 대비 diff는 없다.
- 저장소 전체는 깨끗하지 않다. 추적 변경 7개: GameVersionRegistry, ConditionsTable와 meta, Packages manifest/lock, ProjectSettings, ShaderGraphSettings. 미추적 `.codex/`, `.head/`, `.vsconfig`, `Assets/Plugins/CoreKeeper/`, bridge `data/`, stdout/stderr·logs가 있다. 이 자료를 정리하거나 덮지 않았다. 조사 결과를 저장소 전체 clean 판정으로 확대하면 안 된다.
- 모드 `.cs` **53개, 28,565줄**. 1c2a61b→HEAD의 `.cs` diff는 **50개 파일 +12,362 −566**. 기존 인계의 약 26,500줄과 43파일 +6,728은 현재 총량이 아니다. 집계 범위와 기준 커밋을 함께 기록해야 한다.

| 파일 | 현재 줄 | 실제로 함께 들어 있는 책임 |
|---|---:|---|
| AIBodySkills.cs | 2,972 | 입력·실행기, 명령, 시야·타일·관측, 길찾기, 기본 기술 |
| AIPlayerSpikeMod.cs | 2,169 | 모드 수명, 캐릭터별 조율, 채팅, 입력 전송, 패치, 월드/캐릭터 저장 |
| AIBrain.Loop.cs | 1,435 | 틱·호출·응답 적용·막힘 종료·경로 캐시·후보 조율·요청문 |
| Core/AIBaseGuard.cs | 1,366 | 닿음 지도, 위협, 피해, 둘레 기억·보수, 문 꼬리·회수 규칙 |
| AICraft.cs | 1,309 | 제작과 별개인 놓기 격자/실행, 뿌리·물체 파괴 |
| AIExplore.cs | 1,271 | 기억 저장, 드롭, 물체 점유, 파들어가기 계획·실행 |
| AIBrain.Candidates.cs | 1,192 | 종류별 후보, 전투 짧은 동작, 조립기 어댑터 |
| AIBase.cs / AIEdge.Guard.cs | 1,089 / 1,038 | 생활 기지 / 게임 자료를 읽어 순수 guard 계산에 연결 |

개발 그래프 README·제품그래프-사용법을 확인했다. 제품 그래프 기준도 a07166f이며 정적 경로는 실제 실행 trace가 아니다. 특히 미커밋 상태를 자동으로 전부 탐지하거나 현재 설치본·모든 실게임 동작을 보증하지 않는다. 그래프는 위치를 찾는 색인으로 쓰고 아래 판단은 코드를 다시 읽었다.

## 2. 실제 끝까지 흐름과 바꾸기 좋은 경계

| 단계 | 현재 연결과 근거 | 유지보수상 개선 지점 |
|---|---|---|
| 사용자 입력·캐릭터 귀속 | `AIPlayerSpikeMod.cs:492` 멘션 파싱, `:502` AIAgent.Enter, `:505` OnHostSaid. 슬래시 지시→`AIBody.HandleCommand`(:593), goal은 `AIBodySkills.cs:562`→`AIGoals.HandleGoalCommand`(:108). | 채팅 라우팅/명령 해석과 몸 실행 책임을 따로 볼 수 있게 한다. AIAgent.Enter의 범위를 유지한다. |
| 상위 목표·계획 | `AIPlanner.OnHostSaid:264`, Report:411→`AIPlanReport.ReportJson:22`→브리지. Tick:450→Answer:496→Apply:544→CheckReason:745/StepForm:768/CheckGoal:792→SetGoalFrom:721→`AIGoals.SetGoal:201`. 순수 inbox/orders/plan rules와 JSON protocol이 이미 나뉘어 있다. | 목표 어휘/메타데이터와 종류별 처리 연결을 모으되 외부 답의 형태 검사와 현재 세계에서 실행 가능 검사 경계를 구별한다. 다음 계획 단계 시작 시 재검사는 필요하다. |
| 관측·후보 | `AIBrain.Send:634`에서 기억 강제 관측(:645), AIObservation.Take(:646), BuildCandidates(:655). 조율 함수(:1192)는 Prep→Continue→Enemies→CombatOptions→Brute→Food→Walls→…→Explore→Assemble 순서다. | 현재 조율 함수는 이미 읽히므로 다시 큰 프레임워크로 감쌀 필요 없다. 종류별 생성기와 공통 사실 조회, 조립기 전달 데이터의 경계를 정리한다. |
| 선택·적용 | `CandBuild.Assemble:1075`→순수 `AICandidateAssembler.Select/Mark/Finish`→`BuildJson:1379`/DecisionsApi. `HandleResponse:805`, `StaleReason:941`, `Apply:949`; `AIActions.Make:73`/Stale:76이 기술 생성·현실 재검사를 맡는다. | 기존 AIActions 표는 재사용할 패턴이다. 후보 설명을 읽어 정책을 정하는 부분만 명시적 사실 필드로 바꿀 가치가 크다. |
| 몸·문·입력 | `AIPlayerSpikeMod.cs:1221` Brain.Tick 뒤 `:1223` Body.Tick. `AIBody.Tick:260`은 TrackDoor(:266), 꼬리(:275) 또는 Current.Tick(:290)를 한 경로로 수행한다. 기술 종료 시 누름 폐기(:307), 문 닫기 후 종료 보고. | 파일을 옮겨도 실행 순서/단일 입력 소유자는 유지한다. 문 단계를 별도 실행기에서 병렬 갱신하면 안 된다. |
| 관측·기억·다음 판단 | Body.Tick의 Observe/Vitals/Planner(:335–337), Planner.Tick의 AIExperience.Tick(:452). `AIBody.EndSkill:470`→`AIBrain.OnSkillEnded:364`. `AIMemory.Observe:242`는 상자·밭·솥 관측(:289–292)과 저장(:293); 목표 끝은 `AIGoals.EndGoal:320`→경험/상위 보고. | 기억 읽기도 Ensure를 거쳐 월드 변경 저장에 닿는다(`AIExplore.cs:159,164`). 순수 조회처럼 캐싱·호출 순서를 이동하지 말고 관측/저장 책임과 호출 시점을 드러내야 한다. |

## 3. 반복의 실재와 서로 다른 뜻

### 목표 종류: 가장 넓은 변경 전파

관찰: GoalKind(`Core/AICandidateAssembler.cs:12`)는 12개(None/Free 포함)이고 상위 모델용 `B/protocol.mjs:12`는 10개다. 목표 텍스트(AIDecisions), 명령 파싱/시작/완료(AIGoals), 후보 선택(`AIBrain.Candidates.cs:74–80`), prefetch/막힘(`AIBrain.Loop.cs:659–731`), 답 매핑·형태·현재 상태 검사(`AIPlanner.cs:721,768,792`), 보고(AIPlanReport), 경험 키(AIExperience), bridge protocol/brief가 여전히 종류를 안다. 옛 메모의 넓은 변경 전파는 유효하지만 “새 종류마다 16곳을 반드시 고침”은 변경 종류에 따라 달라지는 점검 목록으로 읽어야 한다.

제안: 우선 고정 어휘·표시·수량 제약·종류별 핸들러 연결을 명시한다. 모든 함수와 상태를 거대한 목표 표 하나에 넣지 않는다. 파싱/형태/실행 가능/후보 생성/완료·막힘의 책임은 분리하고 그 안에서 같은 규칙만 공유한다. C#↔JS는 이미 plan fixture 시험이 있으므로 공통 예제 계약을 확장하면 된다.

중요한 차이: `StepForm`은 아직 시작하지 않은 계획 전체의 형태 검사, `CheckGoal`은 지금 시작할 목표의 품목·재고·현재 위치 검사다(:791 주석). 후자를 “중복 검증”으로 없애면 안 된다. `EndGoal:316–331`의 ClearStopReport/ClearReportStop은 명시적으로 보존된 순서 차이다. 종료 세 줄을 합친다고 순서를 통일하면 기능 변경이다.

### 칸과 문: 조회 공유는 가능하지만 격자 의미는 다르다

관찰: `AIEdge.BuildGrid`(:149 부근), `AIEdge.GuardGridAround:63`, `AIPlaceSpot.Grid`(AICraft:539 부근)는 여전히 각 목적의 격자를 만든다. 둘레는 cut/막을 자리, guard는 적의 보행·비행·사격 도달, place는 물체 footprint/놓기·설 자리다. guard에서 물·울타리는 low(:90), place는 횃불 등 점유도 고려(`OccupiedInView:546`), 둘레는 자기 둘레 문과 둘레 밖 내 문의 역할이 다르다(`AIEdge.cs:182–183`). BFS와 Key/Idx가 닮았다는 이유로 같은 격자로 합칠 근거는 부족하다.

최신 통합: guard 문 분류는 `AIBaseGuard.DoorCell`을 호출하며(:87) 소유 판정에 `IsWalkDoorTile`을 쓴다. `OwnDoorTilesNow:48`은 순수 OwnDoorStands를 쓰고, 놓기 격자는 `WalkDoorTilesNow`를 사용한다(AICraft:550). 따라서 옛 메모의 “my door가 닫힌 남의 문/시야 밖 열린 칸” 결함은 현재 잔여로 세면 안 된다. 문 소유는 둘레 문의 역할 정보와 내 문 기억을 구별하며, `RememberedDoor`는 별도 활성 저장 집합이 아니라 현재 대입이 없는 위임 훅이다. 옛 메모의 “집합 셋”을 현재 저장 상태 셋으로 그대로 세지 않는다.

남은 반복: DoorsNear(:28), BuildGrid 내부 문 조회, DoorAt(AIEdge:497), 시험용 AIBaseProbe 조회가 따로 있다. 필요한 창/시야/소유 해석을 보존한 채 엔터티→DoorView 사실 추출을 공유하는 것은 유효하다. “한 프레임 캐시”는 지금 바로 넣을 구조 변경이 아니다. 세계·캐릭터·시야·entity 수명·같은 틱 내 문 상태 변경에 대한 무효화 계약과 성능 증거가 먼저 필요하다.

### 서는 칸·기지: 겉모양이 같은 식에도 의미 차이가 있다

- `StandOk(AIEdge.Guard:138)`는 walkable+닫힌 문 제외+물체 제외. 광석/수확/뿌리 인접 설 자리 등에 쓰인다. `AIBase.StandNear:395–415`는 **아는 칸 반경 6→실패 시 모르는 walkable 칸 반경 3**이라는 fallback을 갖고 문/물체 검사를 하지 않는다. `AICraft.StandTilesFor:216–236`은 물체 footprint 바깥의 알려진 walkable 칸을 거리순으로 낸다. `AIPlaceGoal.SideStands:252`도 물만 따로 제외한다. 모두 StandOk로 바꾸면 동작 변경이다.
- `AIBase.BaseRadius:38`은 게임 주변 상자 반경 10이고 AtBase(:63), 상자 저장 후보(:448,472), 보고(:606)에 쓰인다. `AIEdge.IsInside:65`는 계획한 둘레 내부다. `AIPlaceGoal.Arrived:267`, InArea(:280), CountsIn(:288)는 둘레 내부를 인정하되 기존 거리 판정도 남긴다. **현재 InArea는 반경 OR 둘레**이며 옛 점검 제안의 “둘레가 있으면 둘레, 없으면 반경”과 다르다.

제안: 이름과 입력으로 ‘현재 실제로 선 칸’, ‘대상에 접근할 설 후보’, ‘기지 중심 근처’, ‘둘레 내부’, ‘놓기 목표 집계 영역’을 구별한다. 공통 물리 사실 조회만 먼저 묶고 정책 통일은 별도 결정으로 둔다.

### 이동 후보 조립: 반복은 실재, 공통화의 단위는 좁게

확인한 대표 호출: `AIBrain.Prep:88–89` 기억한 제작대, `AIBase:450–465` 귀환, :497–499 기억 상자, :544–546 상자 재료, `AICook:464–466` 솥, `AIFarm:690` 밭, `AIPlaceGoal.Go:527–556` 장소. “여섯/일곱”이라는 개수보다 설 후보 일부 탐색→경로→dig 후보라는 절차의 반복이 문제다.

그러나 장소 Go는 detour=true(:538), digEdgeOk(:548), RouteFact(:544), DoorsOn·DetourOption(:549–552)이 있다. 기존 밭은 AITunnel.Plan 직접 호출(:690)이고 다른 대다수는 PlanC 캐시를 쓴다. 공통 헬퍼에는 순서·시도 수·진행 표식·도착 의미·우회 허용·설명·실패 이유를 명시하되 기존 값 그대로 넣어야 한다. 기존 모든 경로를 최신 AIPlaceGoal.Go로 치환하거나 밭에 캐시를 넣는 것은 동작/비용 변경이다.

### 전투 판단·놓은 물체·문장 계약

- `Threat(AIEdge.Guard:221)`는 현재 도달·거리·피해를 읽고 cantCome 집합을 갱신한다(:227–228). `CannotCome:239`는 그 집합을 읽는다. close fight(`AIBrain.Loop:1129`), 호출 적 목록(:768), 후보 내 거리만 보는 전투(:701), Brute 적 선택(:751), 조립기의 near6(:1091)는 동일 목록이 아니다. 이들은 보이는 적/실제 위협/돌진 분석 대상/문장에 적는 적 수라는 의미 차이도 있다. 한 목록으로 통일하지 말고 한 판단에서 관측한 적 사실과 Threat 계산 결과의 소유·수명을 먼저 드러낸다. 변경된 후보 수·정책은 별도로 검증한다.
- `PlaceSkill` 완료는 상자(:1032)·솥(:1041)·제작대(:1058)·일반 놓기(:1063)에 맞는 기록을 남기고 공통 Placed(:1122)에서 목표 진척을 기록한다. `AIEdge.NotePlaced:428`은 둘레 밖 문도 NoteMyDoor(:442)로 기억한다. “문은 기억할 곳 없음”은 해결됐지만 놓기 결과의 여러 소비자는 남았다. 상자 내용물/솥 조리/제작대 기억/둘레 소유는 원래 다른 상태다. 통합 저장 등록부는 형식 이전을 수반하므로 이번 구조 정리의 선행조건이 아니다. 공통 성공 사건을 각 소유자가 받는 경계 정도가 우선이다.
- 실제 정책을 한국어 문구로 읽는다: `AIBrain.Loop:650` trigger.Contains, `AICandidateAssembler.IsRepair:247`·EdgeKindWord:259, `AIBaseGuard.OpensEdgeNote:763`. 설명을 고쳤다가 우선순위나 둘레 위험 판정이 달라질 수 있으므로 명시적 종류/계기/위험 사실 필드로 바꾸는 우선순위가 높다. 출력 문구는 그대로 유지한다.
- `AIPlanner.Post(owner,trigger):114`의 호출은 현재 소스에서 찾지 못했다. `RememberedDoor`도 선언·읽기는 있으나 대입은 없다. 지금 당장 일반 이벤트 버스를 만들거나 새로운 등록 경로를 붙이지 말고 사용되지 않는 호환 훅인지 검증·정리할 후보로 둔다.

## 4. 실제 혼합 책임 분리와 우선순위

권장 순서는 (1) 경계별 동작 기준선, (2) 아래 구조 정리 두 축, (3) 필요한 동작 개선을 별도 검토하는 것이다. 단계 4가 없더라도 유지보수에 도움이 되는 범위를 포함한다.

1. **분산 규칙의 책임 정리**: 목표 종류 메타데이터/핸들러 연결과 완료·막힘 책임, 문장 계약 제거. 변경 누락이 컴파일로 드러나지 않는 비용이 커서 우선한다.
2. **서로 다른 책임의 파일 분리**: 이미 존재하는 형식을 기준으로 `AIBodySkills`에서 관측(AIVision:1333/AIObservation:1581), 타일(AITileReader:1392/AIWallData:1463), 길찾기(AINavigator:1694), 기술을 분리할 수 있다. `AICraft`의 AIPlaceSpot(:516)/PlaceSkill(:953)은 제작과 별도로 자주 바뀐다. `AIExplore`의 AIMemory(:25), AIObjects(:531), AITunnel(:735)/TunnelSkill(:1040)도 책임이 다르다. `AIPlayerSpikeMod`의 패치(:1480), AIWorldSettings(:1575), AICharacterStore(:1646)는 명확한 기존 형식 경계다. 각각 이름·함수·접근 수준·호출 순서를 유지한 이동부터 별도 단위로 검수한다. 큰 partial 파일만 여러 개 만드는 것은 책임 정리 효과가 작다.
3. **게임 사실 조회와 이동 후보 조립**: 중복된 물체/문 사실 읽기, 경로 후보 조립을 의미별로 공유한다. 정적 Core 계산에 Unity 의존을 넣지 않는다. 관측 시점/캐시/후보 순서가 바뀌지 않는지 검증한다.
4. **후속 설계가 필요한 범위**: 한 틱 공간 snapshot, 전투 사실 snapshot, 저장 등록부 통합, 기지 의미 통일, 설 칸 규칙 강화, 농사 캐시/탐색 비용 변경. 유지보수 효과는 있으나 순수 파일 이동과 섞으면 회귀 원인을 찾기 어려워 별도 변경으로 다룬다.

파일 분리의 완료 기준은 줄 수가 아니라 “이 책임을 이해·수정할 파일을 바로 찾고, 별개의 정책을 건드리지 않는가”이다. 목표 등록 표를 새 거대 결합점으로 만들거나 모든 격자를 하나로 만드는 일반화는 권하지 않는다.

## 5. 인계 대비 해결/잔여와 회귀 기준

**이미 해결되어 보존할 것:** 4238cce의 AISettle와 한 서버 틱 nudge, 9df72b7의 goto/place·내 문 월드별 저장·문 비용 2/둘레 벽 두 번째 탐색, a07166f의 DoorCell/OwnDoorStands/MyDoorLook/PassSide. 근거: `AIBodySkills:139,227,2024,2221`, `AIExplore:104,964–967`, `AIEdge.Place:18,25,45,88–98`, `AIEdge.Guard:87,381–388,437`, `Core/AIBaseGuard:408,423,431,1202`. 예전 todo의 미완료 표시나 옛 문 개수만으로 잔여 결함으로 재분류하면 안 된다. 이전 “옛 둘레 문을 내 문으로 옮길 길 없음”도 해소되어 있다. `RepickOldDoors(AIEdge.cs:353)`가 여전히 서 있는 옛 둘레 문을 myDoors로 옮기고(:370) 없어진 문은 잊는다(:376).

**남은 구조 문제:** 목표 분기 전파, 게임 자료 빌더의 중복, 다른 설 자리·기지 의미를 드러내지 않는 이름, 이동 후보 조립 반복, Threat 읽기의 상태 갱신, 문구를 정책으로 읽기, 큰 파일의 혼합 책임. 뿌리/단계4 판단·긴 입력 지연 시 속도 문제는 별도 기능/실측 항목이며 이 조사에서 해결했다고 주장하지 않는다.

구현 승인을 받을 계획에는 적어도 다음을 넣는다.

- **입력부터 다음 소비자까지 대표 경로**: 호스트 지시→보고/답→goal revision→후보 값/순서→Stale→기술 시작→실제 완료·실패→EndGoal/상위 다음 단계. host/llm source와 character/world 귀속을 유지한다.
- **후보 계약**: 비전투 limit 14+wait, 전투 기본 무상한/설정 상한, EdgeShare 4가 progress/continue/combat/door-close를 밀어내지 않음, detour는 마지막·자리 되찾기 없음, 안정 key/value와 설명. `Core/AICandidateAssembler:52,194–241` 및 replay 시험이 기준이다.
- **문과 입력**: StandTile/PlanFrom 단일 판단, 문 단계 입력 독점, open→through→close 후 기술 종료, 취소/교체/초기화 때 미전송 E 폐기, 한 서버 틱 보정. AISettle는 문과 놓기 양쪽 회귀를 함께 본다.
- **관측/기억**: 시야 밖은 마지막 본 상태, reach는 기억 읽기만, 사라짐은 TrackDoor의 MyDoorLook만 갱신, 굶주림/최대 체력 감소를 HurtAmount 피해로 잘못 세지 않음. 내 문 재입장 저장/둘레 편입·재선택도 관련 변경일 때 검증한다.
- **종료/계획**: 두 EndOrder 보존, stale report/goal_rev/host order 변경, plan 단계의 형태 검사와 시작 시 현재 상태 재검사, 목표 완료·막힘 분류/경험 키/로그·브리지 계약 유지.
- **개발 지식 관리**: 새/이동 파일은 `tools/config.json`과 기능 앵커를 갱신한다. 기존 그래프 strict/check와 제품 그래프의 고정 코드 snapshot 재추출 절차는 서로 다르므로 둘 다 해당 범위에서 갱신한다. 소스 문자열을 검사하는 시험도 경로 변경과 함께 정리한다.

## 6. 이번 확인과 한계

기존 `gates.sh`와 시험의 파일/프로세스 경계를 읽었다. Unity 옵션 없이 실행했고 모델 호출은 fake chat으로 치환되며 8399/임시 포트의 격리 시험 서버를 썼다. TEMP/TMP를 이번에 만든 `C:/Users/<USER>/AppData/Local/Temp/ck-refactor-audit-1ed54c3949e54e4fb73bbc04ac40edcd`로 한정했다. 쓰기·삭제 대상은 그 아래 시험 자료와 도구 빌드 출력이다. 게임의 AIPlayers·브리지 runtime data/logs·설치본에 대한 변경 작업은 하지 않았다. 실행 전후 git status 목록은 같았다.

실행: Git Bash의 `bash Tools/core-tests/gates.sh` (`--unity` 없음), 종료 코드 **0**, **failed gates: 0**.

| 게이트 | 결과 |
|---|---|
| compile-check | OK, 오류 0 |
| core-tests | ALL CHECKS OK (973) |
| exp-sim | ALL CHECKS OK |
| recall-loop / bridge-wiring | 41 / 23 checks OK |
| memgraph / protocol | 80 / 68 checks OK |
| fake-bridge / logshape / movecheck | SELFTEST OK |

memgraph는 실제로 실행되어 통과했다(이번 실행에서는 생략되지 않음). 테스트 중 임시 경험/기억 파일은 만들었지만 운영 상태를 읽어 실게임을 구동하거나 라이브 모델을 호출하지 않았다. 이 결과는 조사 시점의 기준선이며 이후 소스 변경마다 관련 회귀 검증이 필요하다.

이 조사는 현재 코드 구조와 기존 오프라인 회귀 기준을 확인한 것이다. 실게임의 입력 지연·프레임 순서·문 통과·성능 개선 효과를 새로 검증한 것은 아니다. 기능 변경을 포함하는 리팩토링을 승인할 근거로 확대하지 않는다.
