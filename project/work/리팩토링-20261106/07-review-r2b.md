# R2b 독립 코드 검수

2026-11-06. 대상 `bcdec88..f512924`, 저장소 `C:/Dev/CoreKeeperModSDK-refactor-20261106`. 고정 커밋의 `git show/diff/grep`만으로 변경된 10파일과 연결 경로를 읽었다. 제품 소스·브랜치·작업트리는 변경하지 않았다.

## 판정

**R2b 독립 코드 검수 통과. Must-fix 발견 없음.** `01-spec-goals-candidates.md` R2b의 부서짐 의미 플래그와 순수 벽 사실 분리에 부합한다. 정식 생산자의 부서짐 의미·기존 표시 문장·후보 우선순위 조건·세계 조회 및 경로 계산 시점을 바꾸는 결함은 발견하지 못했다.

공통 AGENTS, HEAD start-job, 05-dev-planning의 실제 코드 대조·책임·검증 경계 기준과 승인 합의/계획을 R1·R2a와 동일하게 적용했다. 작업자 `02-result-goals-candidates.md`는 보조 근거이며 직접 diff 판단을 대신하지 않았다.

이하 소스 위치는 `f512924` 기준, `S/`는 `Assets/AIPlayerSpike/Scripts/`다.

## 부서짐 의미의 수명과 연결

| 경계 | 직접 확인한 동작 |
|---|---|
| 상태 소유 | `S/AIBrain.cs:49`의 `_needBreach`는 기존 캐릭터별 State 안 bool이다. `StateOf(AIAgent)`/AISlot.Brain을 통해 읽는 기존 S를 사용한다. 전역 공유 플래그·저장 파일·추가 이벤트 큐가 아니다. 기본값은 false다. |
| 새 이유 | `S/AIBrain.cs:192`의 `_needReason` setter가 기존 emergency 초기화와 함께 breach도 false로 만든다. 전체 참조 검색에서 필드를 직접 쓰는 우회 생산자는 없었다. AIGoals의 새 목표·자유 시간도 기존 forwarder를 통해 이 setter를 쓴다. 기술 끝·새 적·체력 감소·긴급 판단·답 폐기·재시도의 이유 대입 역시 여기를 거친다. |
| 정식 생산 | `S/AIBrain.Loop.cs:204/206`의 NewBreach 조회 위치·try/catch·기존 이유와 합치는 AddReason 표현식은 그대로다. 이유 대입이 setter를 지난 **뒤** `_needBreach=true`, 그 뒤 `_needCall=true`를 수행하므로 즉시 지워지는 오류가 없다. |
| 일반 Send | `S/AIBrain.Loop.cs:231`이 현재 캐릭터의 `_needBreach`를 마지막 인자로 전달한다. `:634`에서 optional bool을 받으며 `:650`의 `_buildBreach = breach || AIEdge.PlanActive && AIEdge.BreachPending`에만 사용한다. candidate assembly의 breachCall 연결과 보수 우선 처리 자체는 바뀌지 않았다. |
| Prefetch | `S/AIBrain.Loop.cs:155`의 기존 prefetch 호출은 새 마지막 인자를 생략해 false다. 이미 소비된 이전 일반 요청의 `_needBreach`가 필드에 남아 있더라도 prefetch에 잘못 전달하지 않는다. 현재 plan의 실제 pending은 기존과 같이 별도 조건으로 반영된다. |
| 덮어쓰기 뒤 복원 | 기술 종료/긴급 이유/새 목표가 reason을 교체하면 flag는 false가 된다. 그러나 현재 plan의 BreachPending이 남으면 후보를 만들기 전에 `_buildBreach`를 true로 복원한다. 이후 `:702`의 WithPendingBreach가 기존 위치에서 문장을 붙인다. 후보 생성 전에 pending을 소비하지 않는다. |
| 소비·조기 반환 | `WithPendingBreach`는 후보 생성과 prefetch skip·목표 종료·only-wait 조기 반환 뒤에 그대로 있다. 실제 입력을 만드는 요청에서 한 번 소비하는 시점이 변하지 않았다. Send 성공 때 `_needCall=false`만 되는 기존 방식도 유지된다. 이후 새 이유가 들어오면 flag가 초기화된다. |
| planVersion | `S/AIEdge.Guard.cs:529`의 BreachPending은 pending 비어 있지 않음과 `w.plan == S.planVersion`을 모두 확인한다. `:377` WithPendingBreach→TakePending도 현재 planVersion을 전달한다. `S/Core/AIBaseGuard.cs:1123` BreachTrigger의 계획 교체 시 count/pending 초기화와 `:1136` TakePending의 다른 계획 pending 폐기 코드는 변경되지 않았다. |

계획 교체가 **이미 대기 중인 현재 reason 자체**를 추가로 지우지는 않는다. 이는 기존 문자열 Contains 경로도 같은 이유를 유지하던 동작이며, 이번에 planVersion 검사를 더 넓혀 정리할 대상이 아니다. pending의 수명과 현재 reason의 수명을 혼동하지 않았다.

`기지 둘레 부서짐` 문자열의 모드 소스 전체 참조를 확인했다. 정식 문구 생성은 `AIBaseGuard.BreachTrigger` 한 곳이며, 새 플래그를 켜는 Tick 연결이 존재한다. 별도 생산자가 그 문구를 직접 조작해 보수 우선권을 받던 경로는 발견하지 못했다. 자유 문자열에 우연히 그 문구가 들어갈 때의 분류 제거는 승인 명세가 명시한 범위다.

## EdgeWallFact 생산과 소비

`S/Core/AIBaseGuard.cs:24`의 EdgeWallFact는 readonly struct이고 Kind/문 좌표만 가진다. OpensEdge는 BoundaryWall 또는 HungDoorSide인지로 계산하며 Text를 읽지 않는다. Text는 기존 EdgeWallText/DoorSideWallText/DoorFallText를 그대로 사용한다. None과 default는 false/빈 문자열이고 알 수 없는 enum도 새 예외를 만들지 않는다.

`S/Core/AIBaseGuard.cs:766` WallFact의 분기 순서를 기준 코드와 대조했다.

1. shape null → None.
2. hungDoor 존재 및 doorSide에 해당 → HungDoorSide와 기존 문 좌표.
3. sides 포함 → DoorSideWall.
4. edgeSet 포함 및 inside 아님 → BoundaryWall.
5. 그 밖 → None.

걸린 문의 옆 벽이 일반 문 자리 옆 벽보다 먼저 판정되고, 일반 문 자리 옆 벽은 둘레 개방으로 세지 않는 차이가 그대로다. 원래 상수와 DoorFallText 본문은 diff에서 변경되지 않았다.

| 소비 경계 | 동등성 근거 |
|---|---|
| 게임 측 단일 칸 조회 | `S/AIEdge.Guard.cs:301` WallFact는 기존 EdgeWallNote와 같은 PlanActive 조건에서 같은 ShapeCached()를 한 번 읽는다. EdgeWallNote는 `.Text`, OpensEdge는 `.OpensEdge`를 읽는다. ShapeCached의 프레임별 ShapeNow 갱신 코드는 그대로다. PlanActive가 false면 ShapeCached를 호출하지 않는다. |
| 경로의 둘레 벽 수집 | `S/Core/AIBaseGuard.cs:779` EdgeWallsOn은 기존 walls 순서대로 순회하고 ok 포함 칸을 fact delegate 호출 **전에** 건너뛴다. OpensEdge인 칸만 `(tile, fact.Text)`로 추가한다. 기존 note 결과는 이 생산자가 만드는 네 분류 중 하나였으므로 분류와 출력 배열 순서가 같다. |
| 목표 경로 설명 | `S/AIPlaces.cs:95` DigEdge는 같은 PlanActive/plan null guard와 같은 plan.walls 변환에서 delegate만 typed fact로 바꾼다. PlanC/AITunnel 호출·캐시·우회 선택은 이 diff에서 변경되지 않았다. DigEdgeText의 max·서식도 그대로다. |
| 실행 중 재계산 검사 | `S/AIExplore.cs:1118` TunnelSkill.NextStep은 기존 TargetDone 및 필요 시 AITunnel.Plan 뒤, EdgeOk/PlanActive 조건 안에서 같은 walls와 edgeOk를 전달한다. 새로운 미허용 둘레 벽이 있으면 기존 stop 문장·NoteStop·ForgetPlan·Finish 순서로 중단한다. 캐시 폐기 시점을 앞당기거나 추가하지 않았다. |
| 채굴 후보의 사실 | `S/AIBrain.Candidates.cs:1132/1176` 두 소비자는 기존 위치에서 WallFact를 읽고 MineEdgeFact로 전달한다. `S/Core/AIBaseGuard.cs:823`은 OpensEdge면 기존 Text+'.', 아니면 빈 문자열이다. 후보 포함/실패로 제외된 후보의 두 경로와 각 try/catch 경계가 동일하다. |
| 기존 표시 소비자 | `S/AIBrain.Candidates.cs:346` 등 EdgeWallNote 표시 호출은 wrapper로 유지된다. 기존 두 번 조회하는 표현식까지 그대로이며, 이 작업에서 임의 캐시나 호출 통합을 넣지 않았다. |
| 중복 문장 방지 | WithEdgeFact 본문은 그대로다. 그 안의 문자열 비교는 정책 분류가 아니라 같은 설명을 두 번 붙이지 않기 위한 것이므로 명세대로 유지된다. |

production의 OpensEdgeNote 문자열 판정은 제거됐다. 모든 EdgeWallsOn/MineEdgeFact 생산 호출을 고정 커밋에서 검색해 typed 입력으로 옮겨졌음을 확인했다. 새 fact를 만드는 데 게임 API·위험 판정용 표시 파싱·숨은 상태 갱신을 추가하지 않았다.

## 시험과 한계

- 직접 수행한 `git diff --check bcdec88..f512924`는 통과했다.
- GuardTests의 새 사례는 null/안쪽/무관한 칸과 문 자리 옆/경계 벽의 Kind·OpensEdge·Text를 확인한다. GuardLiveTests는 걸린 문의 좌표와 문구를 확인한다. PlaceGoalTests는 기존 route 순서·허용된 벽 제외·재계산 중단·재구성 뒤 허용 및 MineEdgeFact 기대 동작을 typed 입력으로 유지한다. 기존 golden 문구를 새 출력에 맞춰 바꾼 흔적은 없다.
- 기존 GuardLive L7의 이유 덮어쓰기 이후 pending 복원·한 번 소비, FixMergeReview S5의 다른 계획 pending 폐기, GuardTests의 plan별 재계수·AddReason 중복 방지 시험은 유지된다.
- **비차단 검증 한계:** 위 순수 시험은 새 AIBrain state setter→Tick→Send의 게임 측 wiring을 직접 실행하지 않는다. 이번 검수는 명세가 허용한 독립 diff 대조로 새 이유→breach 추가→다른 이유 대입→현재 pending 복원, prefetch false, planVersion 조건을 확인했다. 시험 편의를 위한 새 production 추상화는 요구하지 않는다.
- 작업자 보고 core 1048 / protocol 106 / 전체 gate 실패 0은 보조 근거로만 기록한다. 이 검수에서 core/compile/replay·Unity·게임·모델 호출을 재실행하지 않았다. 정적 동등성 판단을 실게임 실측으로 표시하지 않는다.

## HEAD 반환

R2b 소스 수정 요청 없음. R1/R2a/R2b의 정적 독립 검수는 모두 must-fix 0이다. 최종 대표 실행에서 부서짐 뒤 후보의 보수 우선, 이유가 교체된 요청의 부서짐 사실, 미허용 둘레 벽으로 재계산된 이동의 중단·후속 후보 설명을 확인할 범위는 유지한다.
