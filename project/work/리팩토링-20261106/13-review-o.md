# O 독립 코드 검수

2026-11-06. 고정 diff `a760da1..902721e`, `C:/Dev/CoreKeeperModSDK-refactor-20261106`. 승인 `03-spec-spatial-ownership.md` §O 및 공통 HEAD 검수 기준을 적용했다. 제품 소스는 고정 git object에서 읽었고 제품/브랜치/설치본을 수정하지 않았다.

## 판정

**O 정적 독립 검수 통과. Must-fix 발견 없음.** 한 번의 후보 준비 인자와 캐릭터에 남는 기록의 수명이 분명해졌다. 위협 평가·평가 결과 기록·마지막 기록 조회를 구분하며 기존 호출 시점과 순회 정책을 보존한다. 새로운 공유 snapshot/refresh/중앙 관리자를 만들지 않았다.

변경된 10파일 전체 diff를 검토하고, 전후 사용처를 git grep으로 전수 대조했다. 아래의 시험 전용 진단 카운터 차이는 제품 동작 결함과 구분하여 남긴다.

## 후보 빌드 인자

이하 위치는 `902721e`, `S/`는 `Assets/AIPlayerSpike/Scripts/`이다.

- `S/AIBrain.Loop.cs:649` Send는 Observation과 PrepPart 이후, _building/경로 카운터 설정 이전에 `breach || AIEdge.PlanActive && AIEdge.BreachPending`을 계산한다. 이전 `_buildBreach` 대입식과 위치가 같다. breach=true이면 PlanActive/BreachPending을 읽지 않고, PlanActive=false이면 BreachPending을 읽지 않는 단락 평가가 유지된다. BreachPending은 기존 pending 존재와 planVersion 일치를 검사하는 getter이며 소비/초기화 시점을 옮기지 않았다.
- `Loop:654/1149`, `Candidates:21/35`의 연결은 Send → BuildCandidates → CandBuild readonly 필드다. 외부 진입은 Send 한 곳, 내부 재귀는 Assemble 한 곳으로 확인했다. 기존 후보 준비 단계 호출 순서와 객체/목록 전달은 그대로다.
- 기존 prefetch 읽기 7곳(Continue, attack 대상 제거, 현재 이동 목적지, 끝날 벽 제외, 탐험 중 여부, 문 열어두기 설명, AsmIn 로그)을 모두 명시 인자로 바꿨다. 조건식의 나머지 피연산자와 short-circuit 순서는 같다. 조립기의 breachCall은 같은 이름의 CandBuild 필드 값을 받는다.
- `Candidates:1154` 도구 개선 재귀는 두 필드를 그대로 전달한다. `_rebuildingForTool=true`와 try/finally에서 false 복구, recursed 로그, 기존 조건은 유지된다. 재귀에서 prefetch를 default false로 잃거나 breach를 다시 조회하지 않는다.
- `Loop:1195` AsmIn은 마지막 bool 인자로 받은 prefetch를 기존 JSON의 같은 위치/문장에 쓴다. recorder의 후보 identity, 근접 적 수와 CloseFight 호출 위치는 바뀌지 않았다.
- Brain.State의 두 필드와 forwarding property가 모두 제거됐고 전후 사용처 검색에서 누락된 읽기/쓰기가 없다. `_needBreach`, `_reqPrefetch` 등 실제 요청 지속 상태는 남아 있다. Send finally는 `_building=null`을 유지한다. _ctx, ThreadStatic _building, BC, 경로 카운터와 prep 문자열의 수명/소유를 바꾸지 않았다.

## 위협 평가·기록과 캐릭터 scope

- `S/AIEdge.Reach.cs:195` AssessThreatWithinRange는 dist>range에서 null을 반환한다. 그 밖에는 기존 EnemyTile → ReachOf → HurtRecently(Time.realtimeSinceStartup) → AIBaseGuard.IsThreat 순서를 따른다. range 경계를 <= 등으로 재작성하지 않았으므로 동등 경계와 NaN 비교 경로도 원본과 같다.
- `Reach:204` ObserveThreat는 null이면 false를 반환하고 S/history에 접근하지 않는다. 이전 범위 밖 값이 남는 동작이다. 평가가 있으면 그 뒤에 Record 한 번, 같은 bool 반환이다. 평가 도중 예외가 나면 기록하지 않고 같은 예외가 전파되며 새 catch/fallback이 없다.
- ReachNow의 0.4초 cache, 동일 타일 비교, 필요시 BreachesNow, 기억/큰 물체 조회와 reach 기록은 원본 그대로다. 따라서 Assess는 위협 history를 쓰지 않지만 전체가 순수 함수는 아니다. 기존 reach/breach/cache 부수효과를 제거하지 않았다.
- `S/Core/AIThreatHistory.cs:7`은 이전 HashSet 연산 순서 그대로 `Count > 64`이면 먼저 Clear, true면 Remove, false면 Add다. 65번째 false 기록까지 유지하고 그 다음 Record에서 clear한다. 이미 있던 key의 재기록이나 true 기록도 먼저 크기 검사를 한다. 비교자/Entity key 의미는 이전 기본 HashSet과 같다.
- `S/AIEdge.cs:47/62` history는 캐릭터별 State의 readonly 필드다. S는 기존 `AIAgent.Cur.Get<State>(AISlot.BaseEdge)`이며 AIAgents.Get의 기존 slot 수명과 lazy 생성 정책을 유지한다. 전역 history나 snapshot을 추가하지 않았다. ClearMemory/Import/Export는 변경되지 않았고 history를 새로 지우거나 저장하지 않는다.
- `Reach:219/222` 마지막 기록 조회와 이전 public Threat/AnyThreat/CannotCome의 forwarding 경로가 있다. public 인자와 반환 계약을 유지하며 내부 소비자는 목적이 드러나는 새 이름을 사용한다.

## 소비자의 순회·조회 시점

`Reach:212` ObserveAnyThreat는 true 발견 뒤에도 전체 foreach를 끝까지 실행한다. Tick(`Loop:166`)과 CandBuild.CombatOptions(`Candidates:246`)의 각 적 기록 횟수/순서가 유지된다. 반대로 대기 Tick의 EnemiesWithin.Exists(`Loop:196`)와 자기 방어 Find(`Loop:599`)는 기존 첫 true에서 멈춘다. 이 차이를 같은 전체 평가 함수로 통일하지 않았다. Settings/LocalTransform 등 lambda 내부 읽기도 같은 위치다.

GuardCommand reach(`AIEdge.Guard:703`)는 KillFacts 뒤 ObserveThreat 순서를 유지한다. CloseFight(`Loop:1086`)는 거리 단락 뒤 마지막 기록만 읽고, 기존 Exists OR FindAll의 단락 의미를 보존한다. SEND의 _callEnemies(`Loop:725`)도 거리 조건 뒤 조회하며 새 평가를 하지 않는다. Brute, near6, swarm 통계 등 거리만 쓰던 경로는 변경하지 않았다.

## 비차단 관측 차이

`S/AIAgents.cs:73`의 Cur getter는 캐릭터 scope 밖에서 testcmds.on인 경우 TEST-ONLY 진단용 getter 읽기 횟수를 센다. 이번 승인된 forwarding 제거는 그 getter를 덜 호출하고, Threat의 기존 S 조회 2~3회도 Record 이전 1회로 줄었다. 따라서 **scope 밖 시험 진단의 읽기 횟수/스택 위치까지 동일하다고 주장하지 않는다.** 동일한 캐릭터 slot을 선택하며 범위 안 제품 상태/후보/판정은 같다. 이는 getter 구현량을 세는 진단의 구조적 차이이고, 제품 정책이나 외부 동작 변경을 의미하지 않는다. HEAD는 2026-11-06 이를 비제품 계측 차이로 수용했다. 단, scope 밖 호출이 발생했다는 사실 자체는 여전히 경고/결함의 근거이며 이 수용으로 무시하지 않는다. 제품 로그 문구·후보·저장 보존 기준도 그대로다.

## 검증과 한계

ThreatHistoryTests의 8개 단언은 미평가 key, 인스턴스 격리, false 반복, true 제거, 64/65개 유지, 다음 반복 key에서 clear, true 기록에도 clear를 확인한다. Program TH 등록과 CoreTests.csproj의 Core/**/*.cs 및 *.cs 포함으로 연결됨을 확인했다. 기존 시험 기대값/fixture를 변경하지 않았다. 범위 밖 미기록과 Any 전체/Exists·Find 단락은 게임 API 모사 시험으로 대체하지 않고 직접 소스 대조했다.

신규 .cs의 meta가 있으며 해당 GUID는 고정 Assets 범위에서 유일하다. CompileCheck의 Scripts/**/*.cs 포함 범위와 동일 assembly 안에 있다. `git diff --check a760da1..902721e` 통과. 작업자 보고 core1062/protocol106/전체 gate 실패0은 보조 근거다. 새 결함 의문이나 수정이 없어 이미 통과한 전체 시험을 중복 실행하지 않았다.

이 검수에서 Unity/게임을 실행하지 않았다. 실제 두 캐릭터 교대, prefetch/도구 재귀, 늦은 응답 폐기, 문 뒤 적과 닿는 적의 공존, 범위 밖 이전 기록 유지의 실행 증거는 08 최종 live 계획에서 확인해야 한다. fixture/replay 통과만으로 이 장면을 실게임에서 검증했다고 간주하지 않는다.

## HEAD 반환

O 소스 수정 요청 없음. 요청받은 R1/R2a/R2b/M/Q/R/O의 고정 diff 독립 코드 검수 보고가 모두 작성되었다. 최종 설치본 연결·Unity/게임 compile·실제 실행 판단은 HEAD가 별도 증거를 확인한다.
