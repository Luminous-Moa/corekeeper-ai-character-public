# R 독립 코드 검수

2026-11-06. 고정 diff `3c31536..a760da1`, `C:/Dev/CoreKeeperModSDK-refactor-20261106`. 승인 `03-spec-spatial-ownership.md` §R와 공통 HEAD 검수 기준을 적용했다. 제품 소스는 고정 git object로만 읽었으며 변경하지 않았다.

## 판정

**R 정적 독립 검수 통과. Must-fix 발견 없음.** 반복되던 최대 세 후보 탐색 규칙과 이동 후보의 공통 필드를 각각 작은 함수에 모으고, 경로 선택·현재 후보 제외·진행 판정·문 설명은 기존 소비자에 남겼다. 새로운 중앙 상태나 거대 조립 허브를 만들지 않았다.

변경 11파일 전체 diff와 호출부·기존 기본 인자를 대조했다. 시험 실행 결과는 아래에 구분한다.

## 탐색 계약과 실제 연결

이하 위치는 `a760da1`, `S/`는 `Assets/AIPlayerSpike/Scripts/`이다.

- `S/Core/AIApproachSearch.cs:6` FirstOfThree는 기존 초기 plan null, stand default, why `설 수 있는 아는 칸이 없음`을 유지한다. 조건도 `i < stands.Count && i < 3 && plan == null`이다. 각 attempt 다음에 stand를 저장한다. 성공한 첫 계획, 세 번 모두 실패했을 때 마지막 stand와 why, 빈 배열의 기본값이 같다. 정렬·null 보정·catch·추가 시도를 넣지 않았다. attempt의 예외는 그대로 전파된다.
- `S/AIApproach.cs:7` FirstCached는 기존 `AIBrain.PlanC(...,out why)`에, FirstImmediate는 기존 `AITunnel.Plan(...,out why)`에 위임한다. PlanC의 detour=false와 AITunnel.Plan의 damageOverride=-1/allowEdge=false/detourCheck=false 기본 인자를 바꾸지 않았다.
- AIBase의 기억 상자 경로(약 500행), AIBase.TakeOrGo(546행), AIBrain.Prep의 기억 제작대(88행), AICook의 기억 냄비(465행)는 FirstCached다. AIFarm.GoOption(689행)만 FirstImmediate다. 다섯 기존 loop 모두 실제 호출부가 연결되어 있다.
- AIBase 기억 상자의 실패 log/continue, k 비교, n 증가와 break가 그대로다. TakeOrGo의 보이는 대상 직접 take, 실패 return false, 현재 key 및 기존 결과 중복 검사, 인자로 받은 progress도 같다.
- Prep의 재료 검사·NoteMissing·target 순서와 충분한 경우에만 후보 추가하는 조건, tg.forGoal 및 notes가 보존된다. 후보 배열 생성보다 앞에 있던 기본 지역변수 초기화가 helper 안으로 이동한 것은 부수효과 없는 초기화뿐이다.
- Cook의 best/outN/busy 선행 조건, 실패 continue, 현재 key에서 break와 성공 break를 보존했다. Farm의 시야/기억 기반 stands 생성, blocked snapshot, 거리 정렬, 실패 log/return과 key 검사를 유지했다. helper가 후보 배열을 다시 정렬하거나 미리 평가하지 않는다.

## 후보 공통 필드와 목적별 경로

`AIApproach.WalkCandidate`는 kind=dig, key=`dig:x,y`, tile=stand, digWall=false, targetLabel=what, progress와 desc만 설정한다. 이전 각 initializer와 같은 필드이며 다른 정책·경로 계산을 포함하지 않는다.

독립 Roslyn 대조로 다섯 파일의 변경된 WalkCandidate 호출 8곳에 대해 마지막 description 인자의 식 token을 기존 desc initializer 식과 일대일 대조했다. **8/8 일치**했다. 표현식뿐 아니라 각 호출의 targetLabel/progress, key 검사 및 주변 계산 순서는 직접 diff로 검수했다.

- AIBase 귀환은 PlanC detour=true, DigEdge 계산, 현재 key 검사, DetourOption과 altH.key 비교를 유지한다. edgeH.ConvertAll은 여전히 description의 DoorsOn보다 먼저 평가된다. 후보의 digEdgeOk 할당은 결과 추가 전에 끝난다.
- AICook 기지 귀환은 현재 key 검사로 PlanC를 건너뛰는 순서를 유지한다. 기존 일반 후보 설명과 중복 정책에 변경이 없다.
- AIPlaceGoal.Go는 자체 최대 세 번 loop를 그대로 가진다. 각 stand의 key/digedge 현재 key에서 즉시 return하는 동작, PlanC detour=true, null이면 다음 stand, DigEdge/EdgeText, RouteFact 저장, edge 변환, DoorsOn 설명, 후보 추가, alt 추가와 return 순서가 유지된다. 공통 FirstCached로 치환하지 않아 이 목적별 제어 흐름이 손실되지 않았다.
- Home/Place의 ConvertAll 결과를 지역변수로 먼저 얻는 것은 기존 initializer의 평가 순서를 보존한다. 외부에 공개되기 전 후보 객체의 할당 위치가 조금 바뀌지만 component 조회·경로 캐시·결과 리스트 변경 순서는 같다.

## 시험·구성 및 한계

새 순수 시험은 빈 배열, 첫 성공, 세 번째 성공, 네 번째 후보 미시도/마지막 실패 이유, 짧은 배열의 마지막 실패, 동일 예외 객체 전파를 확인한다. 기존 golden 단언을 약화하거나 fixture를 바꾸지 않았으며 Program의 AR 등록이 있다. 제품 호출 연결과 cached/immediate 구분은 시험만 믿지 않고 직접 검수했다.

신규 두 .cs의 meta가 있으며 각 GUID가 고정 Assets 범위에서 한 번만 나옴을 확인했다. 기존 Scripts 하위 compile 포함 범위와 assembly 경계 안에 있다. `git diff --check 3c31536..a760da1` 통과.

독립 description 식 검사는 외부 보고 폴더의 `review-m-check` Roslyn 도구를 `--r-descriptions`로 실행했다. 제품 작업트리나 제품 빌드 출력에는 쓰지 않았다. 작업자가 보고한 core1054/protocol106/전체 gate 실패0은 보조 근거이며 이 검수에서 재실행하지 않았다. Unity/게임 실행 검증은 하지 않았다. 08 계획의 귀환·제작/요리·농사·놓기 실제 실행은 최종 검증 범위로 남는다.

## HEAD 반환

R 소스 수정 요청 없음. O 고정 diff 검수를 이어갈 수 있다.
