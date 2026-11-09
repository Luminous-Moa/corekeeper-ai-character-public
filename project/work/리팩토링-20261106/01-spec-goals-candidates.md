# 단계 0~2 구현 명세 — 목표 계약과 후보 의미

2026-11-06. HEAD 승인 대상. 기준 `C:/Dev/CoreKeeperModSDK`의 `ai-character/a07166f38086e0241c37c601c16452664236813e`. 사용자 승인 개발 방향은 [plan.md](../2026-11/2026-11-06-리팩토링-현황과-계획/plan.md)이며 [source-audit.md](../2026-11/2026-11-06-리팩토링-현황과-계획/source-audit.md)를 대조했다. 아래에서 `S/`는 `Assets/AIPlayerSpike/Scripts/`, `B/`는 `Tools/ai-bridge/`다.

## 1. 결과와 보존 조건

이번 묶음은 목표 종류의 공통 계약을 한곳에서 찾게 하고, 목표의 후보/완료/막힘 연결을 기능 책임과 구분하며, 후보·부서짐·둘레 위험의 의미를 표시 문장과 분리한다. 게임 행동을 바꾸지 않는다.

보존: GoalKind의 숫자/이름, host/llm source, goal revision, 호출·종료 순서, 문자열/JSON/로그/저장 형식, 후보 key/value/순서/상한/EdgeShare·detour 규칙, 경로 계산 횟수·시점, 게임 API 조회 시점, 기존 예외 경계. C# 8 이하, `.cs` 소스 배포, 새 파일 `.meta` 포함. 게임/Unity/라이브 모델 실행과 설치는 코더 작업에 포함하지 않고 HEAD가 별도 검증 단위로 지시한다.

명세가 가리키는 원본 메서드 본문은 현재 동작의 규칙이다. '그대로 이동'은 공백·이름·인자 전달 외 처리 순서/분기/문장을 바꾸지 않는다는 뜻이다. 이상해 보여도 이번에 고치지 않는다.

## 2. 단계 0 — 작업 환경과 기준

- 구현 트리 `C:/Dev/CoreKeeperModSDK-refactor-20261106`, 브랜치 `refactor-maint-20261106`, 시작점 a07166f. HEAD가 SDK 7개·게임 plugins·KG를 복사해 준비했고 Library는 복사하지 않았다. 코더는 경로·브랜치·HEAD를 먼저 확인하여 이어 쓴다. 기존 경로를 지우거나 다른 브랜치를 덮지 않는다.
- 본 저장소의 미커밋 7개 SDK 설정, Assets/Plugins/CoreKeeper, 필요한 Library 및 지식 데이터는 기존 작업 절차를 따라 복사하되 추적 변경에 섞지 않는다. 게임 운영 AIPlayers·설치본·브리지 logs/data를 수정하지 않는다. compile-check와 core-tests가 필요한 참조만 우선 준비하고 불필요한 Library 전체 복사는 피한다.
- 기존 비Unity gates 전부 통과(core-tests 973)가 기준이다. 새 worktree에서 환경 검증으로 한 번 실행한다. 생성 파일이 다른 변경에 섞이지 않게 전후 status를 기록한다. TEMP/TMP는 새 작업 전용 하위 폴더를 사용한다.
- 커밋 경계: `R1 목표 계약/연결` → `R2a 후보 인자 이름/역할` → `R2b 부서짐·둘레 위험 코드값`. 각 경계의 수정 목록과 검증 근거를 같은 결과 보고서에 기록한다. 단계 3~4 파일 분리는 이번에 섞지 않는다.

## 3. R1 — 목표 공통 계약

### 3.1 순수 메타데이터 `S/Core/AIGoalKinds.cs` (새 파일)

`GoalKind` enum을 AICandidateAssembler.cs에서 이 파일로 그대로 옮긴다. 공통 메타데이터 `AIGoalKindInfo`와 조회 `AIGoalKinds`를 둔다. 정적 읽기 전용 자료이며 Unity/게임 형식, 콜백, 캐릭터 상태를 담지 않는다.

각 행의 필드: Kind, WireName(없으면 null), HostWords, ItemMode(None/Required/Optional), PlannerMaxCount(해당 없으면 0), Completion(None/Inventory/Feature/FollowTimeout), Feature(None/Base/Farm/Cook/Place), NeedsProgress, ExperienceItem(None/Always/WhenPresent). Count 하한이나 정규화는 경계별로 다르므로 이 행으로 일괄 검증하지 않는다.

| Kind | WireName / HostWords | ItemMode / max | Completion / Feature | NeedsProgress | ExperienceItem |
|---|---|---|---|---|---|
| None | null / 없음 | None / 0 | None / None | false | None |
| Gather | gather / 없음 | Required / 30 | Inventory / None | true | Always |
| Explore | explore / 탐험, explore | None / 0 | None / None | false | None |
| Follow | follow / 따라와, follow | None / 0 | FollowTimeout / None | false | None |
| Craft | craft / 만들기, craft | Required / 10 | Inventory / None | true | Always |
| Store | store / 보관, store | Optional / 0 | Feature / Base | true | WhenPresent |
| Home | home / 기지로, home | None / 0 | Feature / Base | true | WhenPresent |
| Farm | farm / 농사, 밭, farm | None / 0 | Feature / Farm | true | WhenPresent |
| Cook | cook / 요리, cook | None / 0 | Feature / Cook | true | WhenPresent |
| Free | null / 없음 | None / 0 | None / None | false | None |
| Goto | goto / 가기, goto | None / 0 | Feature / Place | true | WhenPresent |
| Place | place / 놓기, place | Required / AIPlaces.MaxShapeTiles | Feature / Place | true | WhenPresent |

표의 HostWords '없음'은 **빈 배열**이라는 뜻이다. None의 취소 단어(`없음/none/off/해제`), auto·verbose는 목표 종류가 아닌 기존 명령이므로 `AIGoals.IsNoGoalWord`와 명령 처리에 그대로 둔다. Gather에 `gather` 명령을 새로 허용하지 않는다.

API: `All`(enum 기존 순서), `Of(GoalKind)`(등록되지 않은 값은 null), `TryWire(string,out GoalKind)`(현재와 같이 대소문자 구분), `IsHostWord(GoalKind,string)`(caller가 이미 lower-case한 문자열과 기존 단어 비교). 정의 누락을 런타임 새 예외로 바꾸지 않고 시험이 잡는다.

`ExperienceKey(GoalKind kind, string item, bool hasItem)`도 이 순수 파일에서 만든다. **None/Free는 item/hasItem 값과 무관하게 빈 문자열**이다. Gather/Craft는 hasItem=false여도 콜론과 item 문자열을 붙인다(실제 ObjectID.None 입력은 `gather:None`/`craft:None`). Explore/Follow는 item/hasItem을 무시하고 wire key만 반환한다. 나머지 등록 종류는 hasItem일 때만 콜론과 item을 붙인다. 등록 밖 enum은 기존 fallback인 `kind.ToString().ToLowerInvariant()`에 hasItem일 때만 품목을 붙인다(예: `(GoalKind)999`, `"Wood"`, true → `"999:Wood"`; false → `"999"`). AIExperience.KeyOf는 null goal이면 먼저 빈 문자열을 반환하고, 나머지는 `g.item.ToString()`과 `g.item != ObjectID.None`을 전달한다. 이 순수 경계에서 실제 key 호환 시험을 한다.

HEAD 승인 후 확인: `AIPlaces.MaxShapeTiles`는 **순수 `S/Core/AIPlaces.cs:36`의 const 30**이다. 이 파일의 의존은 System뿐이며 이미 CoreTests.csproj의 Core/**/*.cs에 포함된다. 같은 이름의 루트 `S/AIPlaces.cs` 파일에는 게임 연결 형식 AIPlaceGoal 등이 있고 별개의 AIPlaces 정의는 없다. 따라서 AIGoalKinds가 기존 Core 상수를 직접 참조하면 순수 경계를 유지한다. 상수 복제·이동이나 Unity 의존을 추가하지 않는다.

### 3.2 소비자 연결

| 소비자 | 바꿀 것과 남길 것 |
|---|---|
| AIGoals.HandleGoalCommand:143~190 | 명령 단어 비교만 IsHostWord로 교체. 분기 순서, `parts.Length` 조건, 기본값·사용법 문장, Gather fallback 유지. 인자가 없는 craft/goto/place를 새로 받아들이지 않음 |
| AIPlanner.SetGoalFrom:721 | TryWire로 매핑. 기존 최종 fallback Follow를 유지. 앞 검증이 알 수 없는 do를 거절하는 계약은 그대로. 품목 읽기 순서, place count<1→1 유지 |
| AIPlanner.StepForm:768, CheckGoal:792 | 각 메서드는 별개로 유지. 어휘는 WireName, gather/craft 상한은 PlannerMaxCount를 사용. 현행 본문의 검사 순서·문장·게임 조회 그대로. StepForm place는 **상한만** 검사하고 CheckPlace는 실제 시작을 검사함 |
| AIGoal.Text:89~95 | `S/AIGoalText.cs`의 `Format(AIGoal)`로 원본 삼항식 본문 이동. AIGoal은 위임. 상태·게임 조회 없음. 의미 없는 delegate 표로 바꾸지 않음 |
| AIExperience.KeyOf:62 | WireName/ExperienceItem을 사용하되 null goal→빈 문자열, None/Free→빈 문자열, Gather/Craft는 None 품목도 `:None`, Explore/Follow는 품목 무시, 나머지는 item 있으면 붙임을 유지. 등록 밖 enum은 기존 ToString().ToLowerInvariant fallback 유지 |
| AIPlanReport:86,101~104 | 목표 key 문자열은 WireName 조회로 대체 가능. JSON 필드의 기존 순서, can_do의 게임별 값 계산과 호출 순서, craft_more/place_more 등 보조 필드는 그대로. expKeys의 순서와 생략(store/home), 품목 목록은 그대로 |
| AIBrain.Send:659 | 긴 목표 나열은 NeedsProgress로 교체. 후보 수<2/Explore 별도 조건과 prefetch 순서 유지 |
| AIGoals.SetGoal:205,211~212 | Inventory와 Place의 inventory 시작값 기록, goto/place의 장소 초기화, Store의 선택 품목 처리 구분은 그대로. 공통 ItemMode를 이유로 게임 호출을 더하지 않음 |

`B/protocol.mjs`/brief는 새 목표/범위를 허용하거나 문구를 바꾸지 않는다. JS는 형식 검사만 하므로 gather count=0/31 같은 정수는 bridge에서 통과해 mod에서 거절되는 현재 경계를 유지한다. C# range를 bridge에 복사하지 않는다.

### 3.3 게임 기능 연결 `S/AIGoalFeatures.cs` (새 파일)

이 파일은 기능별 구현을 **호출하는 경계**만 가진다. 실제 알고리즘과 상태는 AIBase/AIFarm/AICook/AIPlaceGoal에 남긴다. 정적 delegate 레지스트리·플러그인 등록·상태 저장은 만들지 않는다.

- `AddCandidates(server,player,o,curKey,List<AICandidate> dest)`: 현재 CandBuild.Prep의 74~80을 옮긴다. None 제외 base 후보를 먼저(Free는 FreeStoreCandidates), 이후 Farm/Cook/Place의 해당 후보를 추가한다. 원래 네 try/catch와 각 LogError 문구를 유지하고 한 큰 try로 합치지 않는다. `WantedItems`, free ResetMissing, edge/guard/door 후보는 원래 순서에 그대로 둔다.
- `TryDone(server,player,GoalKind,out string note)`: Feature에 따라 AIBase/AIFarm/AICook/AIPlaceGoal.GoalDone 호출. 미대상은 false/빈 note. 게임 조회는 caller가 현재 0.5초/기술 끝 조건을 통과했을 때만 수행한다.
- `StallNote(server,player,GoalKind)`: 기존 Send 673~677의 종류별 prefix+해당 기능 StallNote를 그대로 반환. 다른 종류에는 호출하지 않는다.

`AIGoals.CheckDone`은 FollowTimeout → Feature → Inventory 순서로 정리한다. 기존 goto/place와 생활 기능의 완료 출력/EndOrder가 같으므로 Feature 한 블록으로 합친다. `_lastGoalCheck`를 feature 판정 전에 갱신하는 시점, inventory 감소 시 startCount 재설정, follow의 180초·LLM만·ClearStopReport, 다른 완료의 ClearReportStop을 유지한다. 쓸모 있는 게임 조회를 speculative하게 미리 실행하지 않는다.

### 3.4 후보 이후의 목표 막힘 책임

`AIGoals.CheckAfterBuild(server,player,now,GoalBuildFacts facts)`로 AIBrain.Send 668~715의 세 블록(Feature stalled, inventory stalled, explore exhausted)을 본문 그대로 이동한다. 반환 true면 caller는 즉시 return. 새 `GoalBuildFacts`는 저장하지 않는 값 묶음(struct)이며 다음 필드만 갖는다: HasProgress, InCombat, FrontierPending, ExploreOptions, ExploreNoPath, ExploreFrontierNote, SkippedNote, PrepNote, GatherFacts, BlockNote, FailMemorySeconds. 값은 기존 BuildCandidates 직후의 칸에서 채운다. Goal·CurrentSkill·Inventory·SeenCount는 원래 분기 안에서 읽는다.

삽입 위치는 `if (!prefetch && _queued != null) DropQueued(...)` **다음**, `if(cands.Count<2)` **이전**이다. prefetch로 호출되더라도 기존 선행 skip 뒤 남는 경우는 원래와 똑같이 처리한다. HasProgress는 기존 Exists의 순수 결과를 재사용하며 상태를 읽는 메서드를 미리 실행하지 않는다. GoalBuildFacts는 새 캐시가 아니다.

기존 no_place/cannot_craft/null classification, trim 길이·문구, 각 EndOrder를 그대로 사용한다. `only-wait` 블록(717 이후)은 요청 스케줄 상태를 소유하므로 AIBrain에 남긴다. EndGoal의 두 순서는 통일하지 않는다.

## 4. R2a — 후보 데이터와 역할

### 4.1 인자 의미 이름 바꾸기

AICandidate의 분기별 의미 혼용을 다음과 같이 풀고 **생성부터 AIActions.Make/Stale까지** 모든 참조를 같은 커밋에서 옮긴다. 새 상태/저장/JSON은 없다. 기존 `kind/key/tile/entity/desc`는 유지한다. 기본값(0,false,null/빈 문자열,ObjectID.None)은 유지한다.

| 이전 | 새 필드 / 사용 행동 |
|---|---|
| eatId | itemId (먹기·제작·놓기·보관·꺼내기·농사·요리·회수의 대상 품목) |
| eatVariation | itemVariation: eat만 / itemCount: craft,take,cook / edgeGap: edgefill,edgerepair / waitSeconds: cookwait |
| item2 | secondItemId: cook |
| digWhat | targetLabel: dig,craft,follow,retreat,step,combo / actionMode: farm,cook / exploreDirection: explore의 Tag |
| placeStandOnly | placeStandOnly: place만 / pickupStandOnly: edgepickup / retreatInside: retreat |
| placeStand | placeStand: place만 / pickupStand: edgepickup |

새 이름이 다른 뜻으로 쓰이지 않게 AICandidate에 행동별 주석을 둔다. payload 클래스 계층이나 공장 전체 재설계는 하지 않는다. 기존 `dest`의 edgepickup NaN 처리 등 표에 없는 인자는 이번에 바꾸지 않는다. `AIActions`의 생성자 인자 순서/기본 label/Key override/재검사 위치는 그대로다. 기존 test command도 같은 필드로 옮긴다.

바뀌는 곳은 AIDecisions, AIActions와 생산자 AIBrain.Candidates/Prep/Loop, AIBase, AICook, AICraft, AIEdge/Guard, AIFarm, AIPlaces 및 이 필드를 직접 다루는 기존 시험이다. `rg`로 각 옛 필드의 사용을 모두 확인한다. 같은 이름의 비후보 필드는 바꾸지 않는다.

### 4.2 후보의 둘레 역할

Core에 `EdgeCandidateRole { None, DoorPlace, DoorRepair, TorchPlace }`를 두고 AICandidate와 AsmCand에 `edgeRole`을 추가한다. 기존 kind만으로 알 수 있는 edgefill/edgerepair/edgemat/edgepickup까지 중복 태그로 늘리지 않는다.

- AIEdge.Candidates의 문 두 방향(:590,:599)은 DoorPlace, 횃불(:621)은 TorchPlace.
- AIEdge.GuardCandidates의 재설치 문 두 방향(:775,:783)은 DoorRepair.
- 일반 AIPlaceSpot/AIPlaceGoal 놓기 후보는 None. 종류/순서/key/desc는 그대로.
- CandBuild.Assemble의 AICandidate→AsmCand 어댑터가 값을 복사한다.
- IsRepair는 `kind==edgerepair || kind==place && edgeRole==DoorRepair`.
- EdgeKindWord는 기존 kind switch를 유지하되 place에서 role에 따라 기존 문구를 선택한다. None은 '놓기'.
- AIEdge 시험 명령 door/torch의 후보 찾기도 이 role로 바꾼다. outside의 `:out` 조건과 Candidates→GuardCandidates fallback 순서는 유지한다.

기존 ASM 로그 형식은 이번에 바꾸지 않는다. 과거 fixture에는 role이 없으므로 **시험 코드의 legacy adapter에서만** 옛 kind/desc→role을 복원한다. production 조립기에는 문장 fallback을 두지 않는다. 새 core 시험은 role을 명시한 같은 후보에서 desc만 다른 문자열로 바꾸어도 IsRepair/잘림 결과가 동일함을 확인한다. 과거 replay adapter를 새 구현의 정답 생성기로 쓰지 않는다.

## 5. R2b — 부서짐 계기와 둘레 위험의 코드값

### 5.1 부서짐 이유의 의미 전달

현재 `_buildBreach`는 trigger.Contains('기지 둘레 부서짐')를 읽는다. `AIBrain.State`에 `_needBreach` bool을 두어 `_needReason`와 같은 수명으로 관리한다.

- `_needReason` setter는 현재 `_needEmergency=false`에 더해 `_needBreach=false`를 한다.
- Tick의 NewBreach 반환이 non-null인 곳(:206)은 기존 문자열 합치기/대입 **뒤에** `_needBreach=true`로 설정한다. 다른 reason 생산자는 false 그대로다.
- `Send(...,bool prefetch=false,bool emergency=false,bool breach=false)`의 마지막 인자로 명시 전달한다. 일반 Tick 호출은 `S._needBreach`, prefetch는 false.
- `_buildBreach = breach || AIEdge.PlanActive && AIEdge.BreachPending`로 바꾼다. 판단 전에 Pending을 소비하지 않는다. 기존 WithPendingBreach/TakePending의 문장 부착·한 번 소비·planVersion 처리는 그대로.
- 새 목표/기술 종료/긴급 이유가 reason을 덮으면 flag도 지워지고, 아직 실제 pending인 부서짐은 기존 BreachPending 경로로 다시 포함된다. 전 캐릭터 공용 bool로 두지 않는다.

이는 정식 생산자의 계기 의미를 보존하는 변경이다. 자유 문자열에 우연히 그 한국어 문구가 들어 있다고 보수 우선권을 주는 동작은 생산 계약이 아니다. 알려진 생산자 외 코드가 그 phrase를 이용한 것이 발견되면 수정하지 말고 HEAD에 올린다.

### 5.2 둘레 벽 사실

`AIBaseGuard`의 현재 EdgeWallNote 계산을 값과 표시로 나눈다. 새 순수 값 `EdgeWallFact`는 `Kind { None, BoundaryWall, DoorSideWall, HungDoorSide }`, 문 좌표(마지막 경우), `OpensEdge`(BoundaryWall/HungDoorSide), `Text`를 갖는다. `Text`는 현재 EdgeWallText/DoorSideWallText/DoorFallText와 정확히 같다. None은 빈 문자열/false.

`WallFact(BaseShape,t)`의 분기는 기존 EdgeWallNote 754~759를 그대로 사용: null→None, hungDoor && doorSide→HungDoorSide, sides→DoorSideWall, edgeSet&&!inside→BoundaryWall, 나머지 None. `EdgeWallNote`는 `WallFact(...).Text` 위임으로 남겨 표시 소비자를 보존한다.

- AIEdge.Guard에 `WallFact(int2)`를 두고 기존 PlanActive/ShapeCached 시점으로 읽는다. `OpensEdge`는 이 값의 bool을 읽는다.
- `EdgeWallsOn`은 `Func<tile,EdgeWallFact>`를 받아 OpensEdge일 때 `(tile,fact.Text)`를 기존 순서로 결과에 넣는다. ok 제외와 max/formatter의 동작은 같다.
- AIPlaces.RouteEdge, TunnelSkill의 재계산 검사에서 note delegate를 fact delegate로 바꾼다. 캐시·경로 호출은 이동하지 않는다.
- MineEdgeFact는 EdgeWallFact를 받아 OpensEdge일 때 Text+'.', 아니면 빈 문자열. Candidates의 두 호출을 바꾼다.
- production의 OpensEdgeNote(string)는 제거한다. 과거 core 시험은 typed 입력으로 바꾸고 기대하는 위험 여부/문장은 그대로 둔다. WithEdgeFact의 중복 표시 제거용 문장 비교는 **정책 판단이 아닌 출력 중복 방지**이므로 유지한다.

## 6. 검증·완료 기준

### 오프라인

1. 각 커밋 경계에서 `bash Tools/core-tests/gates.sh`(Unity 제외). 기존 fixture 기대값을 새 출력에 맞춰 고쳐 통과시키지 않는다. 옮긴 경로를 고정 참조하는 검사만 경로와 인자 표현을 맞춘다.
2. `GoalContractTests`를 추가: enum 전 항목이 한 번 등록되는지, 외부 목표 열 개와 내부 None/Free 구분, gather/craft/place 상한 및 place 하한 비적용, host aliases의 기존 집합, 경험 키의 null/None item/특이 Home item/unknown enum 호환을 확인한다. 값표를 그대로 복제한 구현 거울 시험보다 실제 소비 경계의 차이를 검증한다.
3. 같은 contract fixture를 C# 시험과 B/protocol.test.mjs에서 읽는다. 열 개 목표의 올바른/잘못된 **shape**와 count 경계, 누락된 item/where, optional store item/place count를 포함한다. bridge가 정수 count 범위는 거절하지 않는 차이를 기대값으로 명시한다. fixed JSON의 목록은 contract coverage 자료이고 production 원본으로 쓰지 않는다. brief의 목표/범위 설명도 이 목록과 검토한다(프롬프트 변경 없음).
4. 후보 role 시험은 기존 edge share/cut/repair·detour 사례를 유지하고, 같은 role에서 desc만 바꾼 사례가 같은 순서·몫을 얻는지 확인한다. legacy replay 1회는 기존 expected 로그를 그대로 쓴다.
5. EdgeWallFact: null/일반 벽/안쪽/문 옆 벽/걸린 문 옆 벽과 이미 허용된 route wall 제외를 검사한다. Text는 기존 golden 문구와 같고 OpensEdge는 문구와 무관해야 한다. 기존 PlaceGoalTests·GuardTests·GuardLiveTests의 의미 기대값 유지.
6. 부서짐: 새 reason→breach 추가→다른 reason 대입→pending으로 회복, planVersion 변경, prefetch 경로를 확인한다. `NewBreach`가 상태를 바꾸는 시점은 변경하지 않는다. 작은 순수 helper를 시험 편의를 위해 생산 경로에 추가하지 않는다. 현재 state wiring은 독립 diff 검수로도 대조한다.
7. 후보 인자 이름 변경은 compile-check와 생산자→AIActions 소비자 매핑 검수로 확인한다. 기존 constructor 값/순서가 같은지 전체 변경 diff를 대조한다. 테스트만을 위한 새 실행 추상화는 추가하지 않는다.

### 독립 검수·실제 흐름 (HEAD가 별도 지시)

검수자는 기준 a07166f와 결과 diff를 비교하여 목표 완료/막힘·형태 vs 세계 검사·예외 경계·후보 인자 mapping·태그 생산 누락·비동기 변경 없음·비용 변화 없음을 확인한다. 문구의 일부가 실제로 달라졌다면 예상하지 않은 동작 차이로 취급한다.

대표 실행은 호스트 자연어 제작/놓기 요청 → 상위 목표 수용 → 현장 후보 → 문을 지나 목적지 이동 → 놓기/제작 완료 → 목표 끝/경험/상위 다음 판단이다. 정상 외에 알려진 막힘 하나와 호스트 목표 교체의 오래된 답 폐기를 확인한다. role 변경은 둘레 보수·문 재설치·횃불 후보가 잘림 전후 올바르게 분류되는 장면/기록으로 확인한다. 입력 바인딩 이동은 없으므로 별도 전투 성능 전수 측정은 요구하지 않는다. 변경 후 게임의 컴파일 성공은 별도로 확인한다.

현재 상세 코더는 게임을 시작하지 않는다. HEAD는 묶음이 준비되면 사용 일정·백업·설치본·새 시험 월드·끝 복원을 확인해 실행 담당에 맡긴다. 관련 실제 장면을 못 만들면 코드/시험 통과를 실게임 통과로 쓰지 않고 남은 장면을 보고한다.

## 7. 범위 밖과 반환 조건

기지 반경/둘레 통합, 설 칸 엄격화, 경로 캐시 추가, 우회 범위 확장, 저장 등록부 통합, 모델/프롬프트/후보 정책 변경, 이벤트 큐 도입, 전투 위협 snapshot은 이 명세 밖이다. 파일 대분리는 단계 3~4에서 한다.

알려진 차이의 통일이나 새 호출/상태가 없으면 구현할 수 없는 경우, 기준 소스가 바뀐 경우, 기존 시험이 동작 차이를 보여 주는 경우에는 증거와 함께 HEAD에 돌린다. 코더가 임의로 기존 의미를 고치지 않는다.

결과 파일 `02-result-goals-candidates.md`에 커밋, 실제 수정 책임, 오프라인 결과, 독립 검수에 필요한 차이와 실게임 미확인 범위를 쓴다. HEAD가 확인·세션 저장·다음 묶음 지시를 맡는다.
