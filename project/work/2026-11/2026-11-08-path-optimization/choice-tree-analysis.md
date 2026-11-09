# 기존 선택 트리와 258개 요청 대조

2026-11-08 첫 단계 읽기 조사. 기준 후보는 `2a0112d04559ff84fcaf87e1e2e2b2292ee22d2c`이며 첫 조사에서는 소스 변경을 수행하지 않았다. 이후 HEAD의 기존 트리 연결 수정 승인에 따라 아래 후속 구현 절의 담당 파일을 수정했다. 현재 worktree에는 다른 담당자의 경로 최적화 미커밋 수정도 있으므로 이 보고서는 전체 후보의 완성 판정에 쓰지 않는다.

## 실제 실패 요청

`live-evidence/ai2-final.decisions.log` 7행은 ASM-IN #12, 8행은 ASM-OUT #12, 10행은 DECISION #1 INPUT의 전체 후보 원문, 11행은 SEND, 12행은 HTTP400이다. t422.51의 close-fight 요청이 258개를 전송했고, t423.23에 `questions[0].choices` 최대255 오류가 발생했다. request id는 `req_58555125c51b484e94f86e6a7214d33f`다.

| 최종 kind | 수 | 생산 출처 |
|---|---:|---|
|dig|166|현시야 lDig42, 기억광석 lRemember124|
|mine|42|lOther의 현시야 개별 벽|
|step|26|적별 짧은 방향 행동|
|attack|4|적4개체|
|approach|4|적4개체 접근|
|away|1|전투 대응|
|explore|4|AITravel의 다음 통로 방향|
|gather_group|5|기존 mine와 함께 추가된 같은 종류 연결묶음|
|wait, finish_goal, request_plan|각1|공통 선택|
|life_nav, expedition_nav, inventory_menu|각1|모두 닫힘 화면의 진입 선택|
|합계|258|208개 개별 채굴 후보가 대부분|

실제 nav는 c245 `life_nav:2:open:`, c257 `expedition_nav:0:open:`, c258 `inventory-menu:4:root:enter:인벤토리에서 물품 분류와 가능한 조작 고르기`다. 이 요청에서는 세 탐색 메뉴가 모두 닫혀 있었다.

앞선7526 frontier 탐색은 취소된 별도 준비다. 이번 최종 요청의 ASM-IN에는 lExplore553개가 들어왔고, ASM-OUT은 생활 진입·공통 제어·wait를 포함해800개다. 이후 AITravel이553개를 제거하고4개를 추가해251개, 채집묶음5개를 추가해256개, 원정 진입과 가방 진입을 더해258개가 된다. 553개가 최종 전송에서 교체되는 사실은 소스와 로그 수치가 일치한다. lExplore553의 거리는 공유 viewport BFS 결과를 읽으며 후보별 PlanC를 수행하지 않는다. 기억한 frontier7526의 lDigExplore PreparePlan 작업과 구별해야 한다.

## 적용 중인 트리와 평면 합성 위치

소스 경로의 공통 기준은 `C:/Dev/CoreKeeperModSDK-path-opt/Assets/AIPlayerSpike/Scripts`다.

| 경로 | 현재 연결과 결과 |
|---|---|
|Core/AICandidateAssembler.cs Select 91–155, Finish 202–225|여러 pool을 List로 합치고 모든 실행 가능 후보를 보존한다. close-fight에서도 goalSide 후 누락 pool을 다시 합친다. 이 클래스 자체는 탐색 트리 상태를 관리하지 않는다.|
|AIBrain.Candidates.cs 1155 → AILife.Compose 148|생활 트리 호출이 연결돼 있다. 닫힘이면 actual 전체에 life_open을 더한다. 열림이면 new List(actual)에 현재 깊이 선택지를 모두 더한다.|
|AILife.cs Entries 125–140 → AIMaintenance.LifeEntries 242|생활 갈래에서 정비를 호출한다. mode→정확장비슬롯→시설→ExactCandidate로 내려간다. 목표가 완전 지정되면 AIGoalFeatures39의 동일 ExactCandidate 직접 실행도 존재한다. 이번258에는 정비 실행이나 펼쳐진 정비 자식이 없다.|
|AIBrain.Loop.cs 1248 → AITravel.Compose 87|기존 explore 후보를 전부 제거한 뒤 다음 통로 방향을 만든다. viewport BFS 기반 개별 걷기 목록을 먼저 생성한 뒤 교체하는 중복 작업이다.|
|AIBrain.Loop.cs 1249 → AIGathering.AddCandidates|기존 개별 후보를 유지하면서 연결묶음을 추가한다. 묶음 생성은 개별 후보를 트리 아래로 이동시키지 않는다.|
|AIBrain.Loop.cs 1250 → AIExpedition.Compose 158|기존 원정 트리 호출이 연결돼 있다. 닫힘이면 list 전체와 open을 반환한다. 열림에서만 Gather/Travel 후보를 제거하고 branch→item→해당 종류 전체 리프로 좁힌다. 마지막 종류 아래의 대량 리프는 여전히 전부 반환한다.|
|AIBrain.Loop.cs InventoryChoiceSurface1261–1267|가방 메뉴도 연결돼 있다. 일반 inventory/store/take 리프를 제거하고 메뉴를 더한다. Active이면 공통 제어·현재행동·위험대응을 남기는 필터도 적용한다. 이번에는 enter만 추가됐다.|
|AIBrain.Loop.cs BuildJsonSteps1457–1473|최종 cands의 모든 원소를 단일 questions[0].choices에 직렬화한다.255개 경계를 만족시키는 탐색 화면 연결이나 전송 전 변환이 없다.|

AIExpedition의 닫힌 직접 화면 때문에 최초 질문부터258개를 보내야 ‘목록 펼치기’를 고를 수 있었다. 열린 트리도 종류 아래에166개 dig나 더 큰 같은 종류 집합이 쌓이면 다시 물리 한도를 넘을 수 있다. 실제258의 생활 메뉴는 닫혀 있으므로 생활 열린 화면의 확대가 이번 초과의 직접 발생 조건은 아니다.

## 정본과 변경 이력

생활트리 `00-agreement.md`는 AI가 필요한 선택지를 좁혀 조회하고, 대상·수량을 이미 알면 바로 실행하는 구조를 승인한다. `03-spec.md` §5는 열린 화면에 continue/eat/doorclose·공통제어와 현재 페이지, 직접 지정 후보를 보존하도록 구체화했다.

제약해제 `00-agreement.md`는 ‘생활 분류 기능 자체 대신 현재 설명한5항목 페이지 제한을 제거’한다고 명시한다. `coverage.md` C01은 현재 깊이 전체 제공, C02/C03은 전투·None에서도 생활 접근, C07–C10은 임의 대표지점 압축 제거, C14는 비진척 후보 자동 배제 제거를 기록한다. `validation.md`는 오프라인 회귀 통과를 기록하지만255개 API 요청의 성공 증거는 제시하지 않는다.

`git diff c3a02ef^ c3a02ef -- .../AILife.cs`에서 열린 화면의 `actual.FindAll(continue/eat/doorclose)`가 `new List<AICandidate>(actual)`로 변경됐다. 같은 커밋에서5개 페이지와 prev/next를 제거하고 combat/None 배제를 제거했다. 현재 깊이 전체 제공은 기록된 제약해제 해석에 부합한다. 일반 세계 후보까지 열린 생활 화면에 전부 누적하는 변경은 기존 생활 탐색의 좁히기 기능을 약화했다. 해당 변경을 별도로 승인한 근거는 이번에 읽은 합의·coverage에서 찾지 못했다.

채집이동귀환 확정합의 §1은 눈앞의 채집물 묶음, 현재 이동 계속, 이미 정해진 목적지를 바로 선택하도록 보존하며, 다른 대상·장소는 얕은 분류 트리로 좁히도록 한다. 기존 AIExpedition은 이 합의 뒤 `93a8bbc`에서 도입됐다. 닫힘 전체 직접면과 열린 종류별 목록은 그 커밋부터 존재하는 구조다. 이번 path-opt 커밋이 새로 그 구조를 만든 것으로 귀속할 근거는 없다. 직접선택 보존을 모든 미선택 개별리프의 동시 flat 노출 의무로 확대할 근거도 확인되지 않았다.

가방 `tree-agreement.md`는 현재 메뉴의 자식 선택지만 전달하도록 명시하고, 일반 가방8/전체14 상한을 없애며 분류 기반 트리를 확정한다. 동시에 ‘임의 숫자로 자르거나 페이지로 분할하지 않는다’는 명시 조건이 있다. 물리255 처리를 이유로 가방 전체에 공통 페이지를 자동 도입하면 이 정본과 대조가 필요하다. 정비 `work/장비정비-트리/00-agreement.md`는 기존 생활 트리와 직접 지정 실행에 연결하는 범위다.

## 기존 구조를 재사용하는 수정 접점

아래는 읽기 설계안이며 구현은 수행하지 않았다.

1. `BuildCandidateSteps` 초반1224–1233에서 관측·목표 revision과 기존 커서의 현재 단계 질의를 한 번 고정한다. 기존 `AILife.Cursor`와 `AIInventoryMenu.Active`를 재사용하고, AIExpedition에는 private State.Cursor의 Branch/Item을 읽기 전용으로 전달하는 최소 입구를 둔다. 새 트리 상태 저장소를 만들 필요가 없다.
2. 현재 `!AIInventoryMenu.Active`가 세계 후보 생산 전체를 둘러싸고 있다. 이 지점을 기존 원정·생활의 현재 노드 질의와 연결하면 선택하지 않은 세계 리프의 경로 준비를 뒤로 미룰 수 있다. 루트/분류 단계에서는 관측된 품목·종류·장소 신원과 개수만 수집하고, 실행 가능성이 필요한 선택된 대상에서 기존 PreparePlan/PlanC를 사용한다. 아직 계산하지 않은 대상을 실행 가능으로 표시하지 않아야 한다.
3. `Walls`는 현재 관측 벽 분류와 접근 후보 생산을 함께 수행하며, `DigOreSteps379`, `RememberedOresSteps501`, `ExploreSteps703`가 각 타깃에 PreparePlan을 수행한다. 분류 수집과 선택 리프 구체화를 이 기존 생산자 안에서 분리하면 API에서 버릴 리프를 먼저 모두 계산하는 문제가 줄어든다. 생활·정비는 이미 현재 선택을 받아 Entries/ExactCandidate로 내려가는 입구가 있다.
4. 닫힘 화면에서 직접 보존할 현재행동·위험대응·명시 목적지·눈앞 채집묶음은 유지하고, 아직 고르지 않은 다른 개별 채굴 대상은 기존 expedition open/branch/item 경로에서 접근하게 연결한다. 최초 요청부터 메뉴 진입이 가능한 크기가 되어야 한다. 실제208개 중 기억광석124개가 이 탐색 접근과 직접 맞닿는다.
5. AITravel이 최종적으로 대체하는 legacy ExploreSteps의553개 BFS 기반 후보 설명을 왜 먼저 만드는지 호출 의존을 확인한다. 개별 explore 리프 생산만 건너뛰어도 되는지, 현재 관측 기록과 frontier 발견·blocked 사실을 다른 소비자가 필요로 하는지 구별해야 한다. 이 작업을553회 PlanC 절감으로 계산하면 안 된다. 대량 기억 frontier의 경로 준비는 lDigExplore 생산 단계에서 별도로 좁혀야 한다.
6. 마지막 직렬화 직전에는 메뉴 자식과 공통 제어·위험대응을 합친 총수가255 이내인지 확인해야 한다. 종류 하나 자체가255를 넘으면 기존 cursor 안의 장소·연결구역 같은 의미 있는 추가 깊이 또는 손실 없는 운송 분할이 필요하다. 임의 상위N 제거는 선택권을 잃는다. 가방의 페이지 금지와 생활5개 페이지 제거 정본 때문에 운송 분할의 제품 동작은 HEAD가 최신 사용자 정정과 함께 확정할 경계다.

직접행동을 모두 없애고 매번 루트부터 강제하는 변경은 채집이동귀환 합의의 직접실행 보존을 바꾼다. 기존 원정의 다른 대상 조회에 개별리프를 제대로 연결하는 수정과 구별해 판단해야 한다. 이번258은 개별 채굴208개와 전투 관련35개가 합성된 결과이므로 우선 기존 원정 탐색 연결을 수정할 근거가 있다.

## 검증해야 하는 결과

- 실제258개 원문 fixture로 최초 질문부터 API 경계를 만족하고 모든 원래 개별대상·묶음에 기존 탐색으로 도달할 수 있어야 한다.
- 메뉴가 닫힘/열림/한 종류의 대량 자식/생활·정비·가방 소유권 전환일 때 각각 현재 노드와 공통제어를 함께 검증해야 한다.
- 현재행동과 prefetch/held/queued, owner·goal·cursor revision, 선택 후 대상변화 재검증을 기존 생산 경로로 유지해야 한다.
- 기본 화면과 분류 조회에서 선택 전 리프의 PreparePlan 횟수, leaf 조회의 실제 경로·위험사실, 기존 AITravel 대체 후보의 준비 횟수를 함께 측정해야 한다.
- 기존 실기는 HTTP400까지 확인됐다. 성공 응답·몸 실행이나 새 수정의 실게임 성공은 이 보고서로 입증되지 않는다.

## 근거 파일

- [실기 최종 후보·요청 원문](live-evidence/ai2-final.decisions.log)
- [실기 검증 기록](live-validation.md)
- [생활 트리 합의](../2026-11-07-생활트리구현/00-agreement.md)와 [구현 명세](../2026-11-07-생활트리구현/03-spec.md)
- [제약해제 합의](../2026-11-07-제약해제/00-agreement.md), [coverage](../2026-11-07-제약해제/coverage.md), [validation](../2026-11-07-제약해제/validation.md)
- [가방 트리 합의](../2026-11-07-가방장비구현/tree-agreement.md)
- [채집이동귀환 확정합의 보존본](../../채집이동귀환-구현자료/graph/final-source-pins/expeditionAgreement.md)
- [정비 트리 합의](../../장비정비-트리/00-agreement.md)

## 후속 승인 구현과 검증

HEAD가 기존 원정 트리 연결 수정 범위를 승인한 뒤 `AIExpedition.cs`, `Core/AIExpeditionRules.cs`, `Tools/execution-tests/ExpeditionFlowTests.cs`, 해당 csproj와 `fixtures/expedition-258.json`을 수정했다. Candidate/Loop 생산자 연결은 부모 구현 담당 소유다. 커밋과 설치는 이 담당 범위에서 수행하지 않았다.

- 기존 ExpeditionCursor에 범위 경계와 부모 복귀 상태를 추가했다. 대량 자식은 좌표·안정 key 순서의 두 범위로 나뉘며, 모든 대상은 후속 범위에서 접근 가능하다. 항목 수로 원본을 제거하지 않는다.
- `BeginBuild()`의 `ExpeditionQuery`는 owner/world/goal/cursor를 고정한다. 생산자는 `Add(key, branch, item, x, y, description, source)`로 가벼운 관측 신원을 등록하고 `Seal(maxLeaves)` 뒤 `ShouldPrepare(key)`가 참인 리프만 실제 경로를 계산한다. Source는 생산자 소유 객체를 그대로 전달한다.
- Compose는 명시 query에 등록된 개별 세계 리프를 기존 원정 탐색에 연결한다. gather_group/current/entity 대상/life binding과 Home·Goto의 명시 진척 경로를 직접 보존한다. 최종 후보와 세계 메타가 같은 key를 공유해도 이 보존 조건을 독립 검사한다.
- 조회 대상이 남은 상태는 실행 대안이 없다는 자동 재계획 판단에서 구별한다. 아직 경로를 계산하지 않은 관측 자원도 기존 Opportunities/Descriptions와 이탈 보고·응답 수신 ledger에 남기며, 설명에 경로·실행 조건 미확인을 표시한다.
- 자식 목록 재조립 시 이미 남겨진 제어와 뒤에 붙는 가방 진입의 공간을 고려한다. 보존 direct 자체가254개 이상인 별도 경계는 HEAD가 요청 전 진단·차단과 한계 공개를 지시했다. 이 담당의 원정 범위 세분화만으로 모든 direct를 한 질문에 담는 문제를 해결했다고 보고하지 않는다. 최종 전송 사전검사는 부모 Loop 담당과 연결한다.

생산 실행 어댑터는118/118 통과했다. 실기258개 원문 kind/key를 fixture에 보존하고, 그중208개 세계 리프와 별도452·7526개 목록을 전수 탐색했다. 닫힌 화면·분류 단계 준비0, 현재 리프만 준비, 모든 리프 도달, 요청별255 이내, 직접 묶음·현재동작·목적지·위험대응 보존, back/root/close·stale, 관측 자원 이탈 보고를 확인했다. 좌표/key가 동일한 긴급 dig·생활 실행·명시 귀환·일반 세계 리프의 교차 충돌도 시험했다. [실행 원문](choice-tree-execution.log)

이 시험은 생산 Compose·Apply navigation·BuildJson을 실행하며 메타 수집과 경로 준비 경계에는 어댑터를 둔다. 실제 CandBuild→PlanC 준비량, 부모의 최종 전송 가드, 게임 실행은 통합 담당의 후속 검증 범위다. 최초 분석 중 lExplore553을 후보별 경로 준비로 묶은 중간 설명은 수정했으며,553은 공유 BFS 기반 후보이고7526 기억 frontier의 PreparePlan과 구별한다.

## 최종258개 후보 신원

아래는 decisions.log10행에서 `c번호 [kind key]` 부분을 원순서로 추출한 목록이다. 설명 전문은 위 원문에 보존돼 있다.

```text
c1 [attack attack:67076]
c2 [attack attack:67303]
c3 [attack attack:67435]
c4 [attack attack:67720]
c5 [approach approach:67720]
c6 [step step:enemy67720:뒤:1]
c7 [step step:enemy67720:왼쪽:1]
c8 [step step:enemy67720:오른쪽:1]
c9 [step step:enemy67720:왼쪽 앞:1]
c10 [step step:enemy67720:오른쪽 앞:1]
c11 [step step:enemy67720:왼쪽 뒤:1]
c12 [step step:enemy67720:오른쪽 뒤:1]
c13 [approach approach:67076]
c14 [step step:enemy67076:앞:1]
c15 [step step:enemy67076:뒤:1]
c16 [step step:enemy67076:왼쪽:1]
c17 [step step:enemy67076:왼쪽 앞:1]
c18 [step step:enemy67076:오른쪽 앞:1]
c19 [step step:enemy67076:왼쪽 뒤:1]
c20 [step step:enemy67076:오른쪽 뒤:1]
c21 [approach approach:67435]
c22 [step step:enemy67435:뒤:1]
c23 [step step:enemy67435:왼쪽:1]
c24 [step step:enemy67435:오른쪽:1]
c25 [step step:enemy67435:왼쪽 앞:1]
c26 [step step:enemy67435:왼쪽 뒤:1]
c27 [step step:enemy67435:오른쪽 뒤:1]
c28 [approach approach:67303]
c29 [step step:enemy67303:앞:1]
c30 [step step:enemy67303:뒤:1]
c31 [step step:enemy67303:왼쪽:1]
c32 [step step:enemy67303:왼쪽 앞:1]
c33 [step step:enemy67303:오른쪽 앞:1]
c34 [step step:enemy67303:왼쪽 뒤:1]
c35 [away away:near]
c36 [dig dig:-4,11]
c37 [dig dig:-5,11]
c38 [dig dig:7,10]
c39 [dig dig:-9,9]
c40 [dig dig:-10,8]
c41 [dig dig:8,10]
c42 [dig dig:-8,10]
c43 [dig dig:7,11]
c44 [dig dig:-7,11]
c45 [dig dig:-10,9]
c46 [dig dig:9,10]
c47 [dig dig:-9,10]
c48 [dig dig:-11,8]
c49 [dig dig:8,11]
c50 [dig dig:-8,11]
c51 [dig dig:12,7]
c52 [dig dig:-10,10]
c53 [dig dig:10,10]
c54 [dig dig:11,9]
c55 [dig dig:-11,9]
c56 [dig dig:9,11]
c57 [dig dig:-9,11]
c58 [dig dig:12,8]
c59 [dig dig:13,7]
c60 [dig dig:11,10]
c61 [dig dig:-11,10]
c62 [dig dig:-10,11]
c63 [dig dig:10,11]
c64 [dig dig:12,9]
c65 [dig dig:13,8]
c66 [dig dig:11,11]
c67 [dig dig:12,10]
c68 [dig dig:14,7]
c69 [dig dig:13,9]
c70 [dig dig:14,8]
c71 [dig dig:12,11]
c72 [dig dig:13,10]
c73 [dig dig:14,9]
c74 [dig dig:13,11]
c75 [dig dig:14,10]
c76 [dig dig:14,-11]
c77 [dig dig:14,11]
c78 [dig dig:22,-42]
c79 [dig dig:23,-42]
c80 [dig dig:28,39]
c81 [dig dig:22,-43]
c82 [dig dig:24,-42]
c83 [dig dig:23,-43]
c84 [dig dig:24,-43]
c85 [dig dig:25,-43]
c86 [dig dig:25,-45]
c87 [dig dig:51,-20]
c88 [dig dig:52,-22]
c89 [dig dig:52,-23]
c90 [dig dig:56,10]
c91 [dig dig:58,4]
c92 [dig dig:-50,-30]
c93 [dig dig:43,42]
c94 [dig dig:60,6]
c95 [dig dig:61,3]
c96 [dig dig:61,4]
c97 [dig dig:61,8]
c98 [dig dig:44,43]
c99 [dig dig:62,2]
c100 [dig dig:62,5]
c101 [dig dig:-13,-67]
c102 [dig dig:-12,-68]
c103 [dig dig:26,64]
c104 [dig dig:6,-71]
c105 [dig dig:7,-71]
c106 [dig dig:12,77]
c107 [dig dig:13,77]
c108 [dig dig:64,58]
c109 [dig dig:67,55]
c110 [dig dig:45,75]
c111 [dig dig:74,49]
c112 [dig dig:74,52]
c113 [dig dig:75,51]
c114 [dig dig:-92,-22]
c115 [dig dig:-92,-23]
c116 [dig dig:-94,-18]
c117 [dig dig:-94,-19]
c118 [dig dig:-93,-25]
c119 [dig dig:-94,-21]
c120 [dig dig:-95,-18]
c121 [dig dig:-94,-23]
c122 [dig dig:-95,-19]
c123 [dig dig:-38,89]
c124 [dig dig:-95,-20]
c125 [dig dig:-95,-22]
c126 [dig dig:-95,-24]
c127 [dig dig:-46,89]
c128 [dig dig:-47,89]
c129 [dig dig:-48,89]
c130 [dig dig:-48,90]
c131 [dig dig:-46,93]
c132 [dig dig:-103,-14]
c133 [dig dig:-103,-15]
c134 [dig dig:-41,97]
c135 [dig dig:-41,98]
c136 [dig dig:-44,98]
c137 [dig dig:-42,99]
c138 [dig dig:-47,97]
c139 [dig dig:-41,100]
c140 [dig dig:-42,100]
c141 [dig dig:-36,104]
c142 [dig dig:66,89]
c143 [dig dig:-110,-17]
c144 [dig dig:-110,-18]
c145 [dig dig:71,86]
c146 [dig dig:-111,-17]
c147 [dig dig:-42,104]
c148 [dig dig:88,70]
c149 [dig dig:-43,104]
c150 [dig dig:88,71]
c151 [dig dig:-113,7]
c152 [dig dig:-43,105]
c153 [dig dig:55,100]
c154 [dig dig:89,72]
c155 [dig dig:-55,106]
c156 [dig dig:-54,107]
c157 [dig dig:-56,106]
c158 [dig dig:58,105]
c159 [dig dig:58,106]
c160 [dig dig:83,88]
c161 [dig dig:-5,121]
c162 [dig dig:84,88]
c163 [dig dig:-54,109]
c164 [dig dig:-4,122]
c165 [dig dig:-56,109]
c166 [dig dig:75,97]
c167 [dig dig:50,114]
c168 [dig dig:82,94]
c169 [dig dig:51,114]
c170 [dig dig:50,115]
c171 [dig dig:81,96]
c172 [dig dig:51,115]
c173 [dig dig:49,116]
c174 [dig dig:60,111]
c175 [dig dig:52,115]
c176 [dig dig:51,116]
c177 [dig dig:50,117]
c178 [dig dig:-4,133]
c179 [dig dig:-5,133]
c180 [dig dig:-1,134]
c181 [dig dig:-3,134]
c182 [dig dig:-4,134]
c183 [dig dig:-90,105]
c184 [dig dig:-85,111]
c185 [dig dig:-91,107]
c186 [dig dig:-91,108]
c187 [dig dig:-90,109]
c188 [dig dig:-23,140]
c189 [dig dig:-91,109]
c190 [dig dig:-110,91]
c191 [dig dig:-94,108]
c192 [dig dig:-91,111]
c193 [dig dig:-94,109]
c194 [dig dig:-26,142]
c195 [dig dig:-27,142]
c196 [dig dig:-28,142]
c197 [dig dig:-44,138]
c198 [dig dig:-29,142]
c199 [dig dig:-27,143]
c200 [dig dig:-28,143]
c201 [dig dig:-29,143]
c202 [mine mine:4,10]
c203 [mine mine:-4,10]
c204 [mine mine:-5,10]
c205 [mine mine:2,11]
c206 [mine mine:-2,11]
c207 [mine mine:7,9]
c208 [mine mine:3,11]
c209 [mine mine:-3,11]
c210 [mine mine:6,10]
c211 [mine mine:4,11]
c212 [mine mine:-9,8]
c213 [mine mine:8,9]
c214 [mine mine:-8,9]
c215 [mine mine:-10,7]
c216 [mine mine:-7,10]
c217 [mine mine:11,6]
c218 [mine mine:6,11]
c219 [mine mine:-6,11]
c220 [mine mine:9,9]
c221 [mine mine:-11,7]
c222 [mine mine:11,7]
c223 [mine mine:12,6]
c224 [mine mine:-12,6]
c225 [mine mine:10,9]
c226 [mine mine:11,8]
c227 [mine mine:-12,7]
c228 [mine mine:-13,6]
c229 [mine mine:13,6]
c230 [mine mine:-12,8]
c231 [mine mine:13,-7]
c232 [mine mine:-12,9]
c233 [mine mine:14,6]
c234 [mine mine:14,-7]
c235 [mine mine:-11,11]
c236 [mine mine:-12,10]
c237 [mine mine:-13,-9]
c238 [mine mine:-14,-8]
c239 [mine mine:14,-8]
c240 [mine mine:14,-9]
c241 [mine mine:-14,-9]
c242 [mine mine:13,-11]
c243 [mine mine:14,-10]
c244 [wait wait]
c245 [life_nav life_nav:2:open:]
c246 [finish_goal control:finish]
c247 [request_plan control:plan]
c248 [explore travel:explore:0,0:1,0]
c249 [explore travel:explore:0,0:-1,0]
c250 [explore travel:explore:0,0:0,1]
c251 [explore travel:explore:0,0:0,-1]
c252 [gather_group gather:mine:2,11|mine:3,11|mine:4,10|mine:4,11]
c253 [gather_group gather:mine:-10,7|mine:-11,11|mine:-11,7|mine:-12,10|mine:-12,6|mine:-12,7|mine:-12,8|mine:-12,9|mine:-13,6|mine:-2,11|mine:-3,11|mine:-4,10|mine:-5,10|mine:-6,11|mine:-7,10|mine:-8,9|mine:-9,8]
c254 [gather_group gather:mine:10,9|mine:11,6|mine:11,7|mine:11,8|mine:12,6|mine:13,6|mine:14,6|mine:6,10|mine:6,11|mine:7,9|mine:8,9|mine:9,9]
c255 [gather_group gather:mine:13,-11|mine:13,-7|mine:14,-10|mine:14,-7|mine:14,-8|mine:14,-9]
c256 [gather_group gather:mine:-13,-9|mine:-14,-8|mine:-14,-9]
c257 [expedition_nav expedition_nav:0:open:]
c258 [inventory_menu inventory-menu:4:root:enter:인벤토리에서 물품 분류와 가능한 조작 고르기]
```

