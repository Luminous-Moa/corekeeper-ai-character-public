# 합의별 구현·검증·미해결 대조

2026-11-08 최신 상태: [사용자 범위 결정](../2026-11/2026-11-08-채집이동귀환-조명후속분리.md)에 따라 조명 시야 R1은 별도 후속이다. HEAD가 본 소스 C:/Dev/CoreKeeperModSDK의 ai-character를 7af8f91로 통합했고 기존 dirty 파일 해시 보존을 [integration.json](integration.json)에 기록했다. 개발 그래프 정본 반영은 별도 담당 진행 중이며 완료 대기다. 아래 검증 기록은 그대로 재사용하며 제품코드 수정이나 재검증은 하지 않았다.


검수 후보 `7af8f91e5d26e4fd9ce5996809ae41e213340118`, 작업본 `C:/Dev/worktrees/corekeeper-expedition`, 브랜치 `feature/expedition-20261107`. 출발점 `5cc5a43`. 아래 구현은 오프라인 생산 코드 경계에서 검증했으며 실게임·외부 판단모델 성공을 뜻하지 않는다. 조명 시야 R1은 2026-11-08 사용자 결정으로 별도 후속에 분리됐다. 이번 범위는 기존 화면 범위 관측을 사용하는 행동 구현이다.

|합의|구현 앵커|검증 근거|남은 경계|
|---|---|---|---|
|직접 행동+얕은 분류 트리, 메뉴와 몸 분리|AIExpedition.Compose/Navigate, AIBrain.Apply의 expedition_nav|execution92: 실제 Apply로 메뉴를 열고 현재 body/goal 유지, 토큰 revision·캐릭터·월드 stale|실제 모델의 분류 선택 품질|
|원래 부탁·현재 작업·급함·비용 제공|AIExpedition.Report, AIPlanner.Accepted(report,why), 기존 BuildInput 가방·도구·위험 관측|원래 HostOrder, 상위 판단 원문, 지연·원작업·위치·후속계획을 같은 입력에 전달|상위 자연어의 실제 판단 품질|
|곁일 여부/범위는 모델, 원작업 재개|AIExpedition.Starting/Resolve/WorkRecord|execution92: 부분120→곁일Wood2→원래 고정후보 재개→누적220|고정 곁일 quota 없음. 월드/캐릭터 runtime 상태 소유|
|가시 자연 묶음·단일 병행, snapshot 고정|AIGathering.AddCandidates, AIGatherSelection, AIGatheringRules.Components|gather61: 301대상 제한 없음, 단일보존, entity/version/타일 서명, 신규대상 제외|빛·어둠 포함 실제 가시성은 R1 별도 후속|
|연속 접근·채집·회수와 실제확보 구별|AIGatherSkill.Tick/Received/CompletedTargets, AIGatherRuntime.Recovery|gather61: 실제 생산 sequence, 잔여drop 있을 때 회수, 실패/무획득/중단/재개|native 물리/자동줍기는 fixture. 관측 증가를 특정 파괴의 인과 보상으로 단정하지 않음|
|새 발견은 다음 선택, 실행 중 현장판단|AIGatherSkill.ObserveDiscoveries, AIObservation.rootTiles, Loop running watch|gather61: ore/root/item/object 새사실 1회 통지, body 유지·snapshot 불변|실제 시야/실제 모델 응답은 후속|
|임의30 제한 없음, 목표량/현장묶음 분리|기존 5cc5a43 + count 진척 관측|core1418, restriction47, execution92의200/220 목표후 body 계속|목표량 자체로 자동 종료/귀환 안함|
|사용자 명시 엄격상한만 별도 존중|PlanStep.MaxCount, Goal.maxCount, LimitReason, EnforceExplicitLimit|bridge25·core22·life105의 protocol→SetGoalFrom→Goal, gather61의9/10·불가분stack·unknown yield·dig, execution92의in-flight보류|산출량 미확정이면 다음행동 보류. 통제 불가능한 주변 자동줍기까지 절대최대 보장하지 않음|
|통로 탐색/알려진 목적지 이동 분리|TravelSkill, GotoSkill, AITravel.KnownPath, AINavigator|travel43 + 실제 navigator9, 알려진599칸 길·미지거부·목적지 접근|구형 coarse 기억을 정확통로로 복원 안함. 재관측 필요|
|사람시야·개인기억·재관측·격리|AITravel.ObserveView/Ensure/travel-memory.json|travel43: 현재/마지막관측/미지, 개방·막힘 재관측, 채굴결과, 캐릭터/월드 저장격리|**R1 후속 분리**: 이번 범위는 기존 화면 관측. 실제 조명 확보 구조는 후속에서 결정|
|외길지속·넓은통로·고리|PassageExits, TravelSkill.BeginStep/Tick|travel43: 폭2 직선/열린방, 폭3 코너, 넓은십자, 반복유향간선 관측 후 모델선택|일반동굴 체감 실게임 보류|
|다음갈림길 방향만 선행질의, 지연/재검증|TravelSkill.Forecast/ValidateTurn/ReserveTurn, AITravel.TryApplyTurn|travel43: speed×latency, 조기예약/늦은응답정지/출구변경폐기/다른run거부, 선행조회 지도비기록|선행은 자원·위험·일반지도 사실로 승격하지 않음|
|막힘 사실+실제 가능한 수단|TravelSkill 관측, 기존 후보생산자/AIActions|막힘/순환/길없음 관측, 다른방향·기존지원행동을 모델이 선택|다리/채굴의 성공을 그래프만으로 보장하거나 자동선택하지 않음|
|이탈기회/전제상실 2개 자동상위보고|AIExpedition.Starting/AfterBuild, Ledger.Sent/Accepted/ShouldReport|execution92: 실제 RequestReplan→Report→offline HTTP, 중복억제·응답중 몸유지, 빈통로를 채집대안으로 오인하지 않음|실서버/실모델 호출 보류|
|조기귀환 부족량·장소·재개할일 보존|AIExpedition.WorkRecord, Gather.where, SetGoal 직전 Observe|execution92: Gather50/획득20→Home→Store→새Gather30 tile:20,30, 이전20·부족30·장소 보고유지|새 동일품목 목표를 코드가 임의로 같은작업으로 합치지 않음. 상위가 잔여량·장소를 계획|
|귀환목적→도착후작업, 단계/원부탁 구별|AIPlanner.OnGoalEnded/StartNextStep/SetGoalFrom, PlanText/Report|execution92: 실제 finish_goal→생산 callback→다음tick Home→Store→Craft goal소비, HostOrder는 열림|물리 귀환/상자보관은 실게임 미검증. 실제 Navigator 접근9로 목적지 경계 별도검증|

## 독립 검수 발견 대조

|발견|수정/상태|증거|
|---|---|---|
|R1 실제 시야 누락|사용자 결정으로 별도 후속. 해결됐다는 뜻은 아님|2026-11-08-채집이동귀환-조명후속분리.md, vision-authority-investigation.md|
|R2 폭있는 통로 매칸 분기|46c7168 PassageExits|travel43|
|R3 고리 자동무한순환|46c7168 실제 이동 유향간선 재진입 관측/정지|travel43|
|R4 명시상한 전달·실행 누락|d5bb0df + e1cedcf max_count·per-target·직접/진행중/dig 경계|gather61, core22, bridge25, life105, execution92|
|R5 빈통로가 채집대안으로 고갈보고 억제|46c7168 progress 및 e1cedcf AfterBuild|travel43의Gather explore비진척, execution92의빈통로 고갈보고|
|R6 조기귀환 부족량·위치 소실|d5bb0df Gather.where/flush + e1cedcf WorkRecord/실제planner continuation|execution92|
|R7 물체 목적칸 막힘으로 접근회귀|46c7168 관측된 tolerance 접근점/도착면 검사|실제 AINavigator fixture9|

독립 검수의 최종 수용 판단은 HEAD/검수자가 별도 작성한다. 본 문서는 구현자가 작성한 coverage이며 독립수용을 대신하지 않는다.

## 최종 재검수 보완 7af8f91

- R4는 `da466b2`에서 후보 메타데이터 대신 실제 파괴 원본을 재검증했다. Mine/Root/Break Start·Tick, Tunnel 매 Mine 자식 직전, FarmSkill harvest Start·반복 secondary 입력에 같은 명시조건을 적용한다. native14와 life105의 harvest 입력5개가 이를 검증한다. 순수걷기와 일반 hoe/plant/water는 유지한다. 최대 산출표가 미확정인 파괴를 다른 품목만 나온다고 가정하지 않는다.
- R4의 소유·수명은 `ad8fbce`+`7af8f91`이다. `max_order`는 원래 HostOrder ID이고, 단일 원부탁이면 생략을 해석할 수 있다. 여러 원부탁이면 소유를 명시해야 하며 모호함을 무상한 실행으로 바꾸지 않는다. 계획 수락 시 미래 채집단계의 조건도 즉시 등록하고 Home/Store/정비, source 변경, 미룸 후 재개에도 보존한다. 별도 부탁에는 적용하거나 그 획득량을 합산하지 않는다. 같은 원부탁의 상한 수치를 새 상위 계획만으로 변경하는 것도 거절한다. 원부탁 완료/취소 뒤에는 적용하지 않는다. execution92는 실제 Planner set branch→Bind→SetGoalFrom 및 원부탁별 실제 관측 누적 경로로 이 수명을 검증한다.
- R5는 `378561e`에서 lExplore/lDigExplore 생산자의 가용성과 목표진척 의미를 분리했다. 실제 기억 목표광석은 대안으로 유지하며, 일반탐험·다른재료기억은 채집 고갈보고를 숨기지 않는다. 실제 후보 initializer/HasProgress predicate→생산 Goal/Expedition 보고 연결11개 통과.
- 보고 경로는 같은 body key/continue/유효 travel_turn 재선택도 `Choosing`을 거쳐 새로운 미검토 기회를 보고한다. 실제 AddEvent→요청중 pending→응답 후 dispatch→Report events/expedition JSON→offline HTTP payload로 새 사실 보존을 검증했다.
- 시야 R1은 별도 후속으로 분리됐으며 이번 구현은 기존 화면 관측 범위에서 수용됐다. 최종 Unity와 로그 목록은 validation.md를 따른다.
