# 행동 정체 독립 근본원인 분석

2026-11-08. 공식 소스 `C:/Dev/CoreKeeperModSDK`의 `43c3817fc1a36142c8591fb087ed867665633fe1`와 이전 `7af8f91`, 기존 합의, 장비 진단, 최적화 실기 기록, 최신 사용자 실행 사본을 읽었다. 제품 코드·설치·게임·서비스는 변경하지 않았다. 요청된 분석 문서만 작성했다.

## 판단

현재 사용자 실행의 장기 정체에는 실행 결과를 다음 판단으로 이어 주는 공통 연결 문제가 있다. 스킬이 실제 몸 입력은 Idle로 내보내면서 Running 상태를 유지하고, 미해결 조건은 ObserveOnce로 한 번만 알린다. 하위 모델이 continue를 고르면 그 알림을 소비한 뒤 비전투 판단을 다시 요청할 계기가 없어질 수 있다. 실제 전달 수령 관측 실패와 통로 갈림길 대기에서 이 조합이 각각 확인됐다.

사용자 정책은 AI가 계속·중단·전환·단계 완료를 판단하고 코드가 실제 상태를 관측해 전달하는 것이다. 현재 문제를 해결하려면 그 판단에 필요한 사실의 정확성, 미해결 상태의 재판단, 단계 간 관측 귀속을 복구해야 한다. 해당 사례들만으로 정책 전체를 폐기할 근거는 없다.

핵심 경로는 이전 7af8f91에도 있다. 43c3817의 준비 분산·트리 연결 최적화가 새로 도입한 장기 정체라고 판정할 수 없다. 최적화 자체에는 이동 장애물 변화로 준비를 반복 취소하는 별도 지연이 남는다.

## 1. 실제 실행에서 두 종류의 장기 정체가 재현됐다

최신 사본은 [20261108-110318-Player.log](evidence/20261108-110318-Player.log)다. 동일 aiui1106 실행이 t737.5에 종료됐으며 이전 ai2 최적화 시험과 분리한다.

| 시각 | 관측 |
|---|---|
| t80.4 | 상위가 보유 나무 장비를 투구→갑옷→바지 순서로 전달하는 계획 설정 |
| t82.6 | 하위가 투구 전달 실행 전에 finish_goal 선택, give:WoodHelm completed |
| t144.1 | 갑옷 give 가방 조작 START |
| t150.2 | 출발 감소 1/1, 바닥 Entity.Null, 수령자 증가 0으로 표시된 미확인 관측과 하위 #37 SEND |
| t150.5 | #37 continue 선택 |
| t282.9~283.2 | 상자 보관 새 목표로 #38 SEND, 기존 갑옷 give 139초째 continue 선택 |
| t568.8~569.4 | 구리 검 새 요청의 채집 목표로 #39 SEND, stop_action을 골라 give 종료 |
| t569.4 | give CANCELLED after 425.3s, 시작·종료 좌표 모두 (3.37,5.22) |
| t611.7 | WoodMiningPick 제작 단계 완료 |
| t611.8~612.1 | 구리 5개 채집 목표인데 직전 1/1 확보 관측을 trigger로 #56 SEND, finish_goal 선택, completed got 0/5 |
| t625.0~625.3 | 통로 갈림길 공통 구간 끝에서 #62 SEND, continue 선택 |
| t637.0~637.5 | 상위가 구리 0개를 확인해 채집부터 재계획, #63이 현재 통로 탐색 continue 선택 |
| t649.8,709.8,737.5 | 동일 좌표 (-2.060793,8), #63 이후 신규 SEND 없음 |
| t737.5 | 세계 종료로 통로 탐색 취소, facts에 갈림길 방향 응답 대기 유지 |

최신 실행은 SEND63회·정상 파싱63회·HTTP 오류0회·준비 취소0회다. 모든 SEND의 준비 누적은1.748초, 개별 최대 준비126.4ms·최대경과144.6ms였다. #37 응답은0.27초, #38은0.35초이며 #37→#38 SEND 간격132.7초, #38→#39는285.9초다. 마지막 통로 대기는 #63 정상 응답 후 종료까지100초다. 실기 담당의 metrics.json 수치와 로그를 교차 확인했다.

## 2. 자기복구가 사라지는 공통 경로

1. AISkill.ObserveOnce는 같은 key를 한 번만 ExecutionObservations에 추가하고 AIBrain.ObserveExecution을 부른다. 후자는 실행 수정번호를 증가시키고 _needCall을 켠다. [AISkill.cs 64행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AISkill.cs:64), [AIBrain.Loop.cs 22행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:22)
2. 요청을 만들 때 AskedExecutionRevision을 현재 값으로 맞춘다. 준비 중 다른 이유가 생기지 않았다면 송신 뒤 _needCall도 꺼진다. 미해결 관측이 요청에 한 번 포함된 상태가 된다. [AIBrain.Loop.cs 747행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:747), [AIBrain.Loop.cs 767행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:767)
3. continue는 현재 실행을 유지한다. InventorySkill과 TravelSkill은 ETA를 따로 제공하지 않아 기본 -1이며 완료 예고에 의한 prefetch도 생기지 않는다. [AISkill.cs 57행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AISkill.cs:57), [AIBrain.Loop.cs 509행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:509)
4. Running 중 연속 재판단은 전투 조건에만 있다. 비전투에서 _needCall이 꺼지면 바로 반환한다. minGapSeconds는 이미 생긴 호출의 최소 간격만 제한한다. 시간이 지나면 새 호출을 만드는 타이머가 아니다. [AIBrain.Loop.cs 238행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:238), [AIBrain.Loop.cs 252행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:252)
5. 상위에 관측 이벤트를 추가하는 AddEvent는 이벤트 목록에 보관한다. 별도 즉시 재계획 요청을 만들지 않는다. 상위 keep 응답도 하위 재판단을 깨우지 않고 반환한다. [AIPlanner.cs 228행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanner.cs:228), [AIPlanner.cs 627행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanner.cs:627)

따라서 새로운 목표·위협·수량 변화·다른 스킬 관측이 오기 전까지 미해결 작업은 Running으로 남는다. 화면에서 움직이지 않아도 스케줄러는 현재 행동이 있다는 상태로 판단한다. 장비의 #37→#38→#39, 통로의 #62→#63→종료가 이 경로와 일치한다.

이 문제의 수정 목표는 미해결 판단 필요 상태가 실제로 해소됐는지 추적하는 것이다. 계속하기 선택권을 유지하면서 현재 선택이 추가 이동이나 물품 변화를 만들 수 있는지, 다음 판단은 무엇이 언제 깨울지를 실행 계약에 연결해야 한다. 호출 예산과 기존 중단 권한은 그대로 고려해야 한다.

## 3. 갑옷 전달 관측의 구체적 결함

AIInventory.DropTick은 실제 DropItem 요청을 한 번 넣은 뒤 자기 가방 감소를 확인한다. 그러나 수령자 보유량 증가는 `_floor != Entity.Null && removed == count` 내부에서만 계산한다. _floor가 없으면 매 tick gained=0 초기값을 표시한다. 이 값은 수령자 인벤토리를 새로 측정한 0이라는 뜻이 아니다. [AIInventory.cs 990행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIInventory.cs:990)

실제 로그에서 출발 감소1/1이 확인됐고 사용자는 갑옷을 받았다고 말했다. 이후 슬롯12가 비었고 갑옷 보유량이2→1이었다. 임시 바닥 entity를 놓친 원인은 상세 생성·픽업 trace가 없어 확정되지 않았다. 빠른 자동 픽업은 가능한 설명이다.

ObservedDelivery는 바닥 entity 귀속, 지정 pickup receipt, 바닥 감소, 수령자 증가가 모두 맞아야 성공을 반환한다. _floor를 잡지 못하면 이 증거 체계가 회복되지 않는다. 6초 후 미확인 사실을 한 번 보내고 계속 Idle로 기다리는 경로가 남는다. [AIInventory.cs 1023행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIInventory.cs:1023), [AIInventory.cs 1051행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIInventory.cs:1051), [AIInventoryRules.cs 54행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/Core/AIInventoryRules.cs:54)

따라서 물품 전달 귀속을 요청 처리·수령 측 관측과 지속적으로 연결해야 한다. 바닥 entity가 없어도 수령자 증가를 실제 측정하고, 미측정·0증가·수령 확인을 구별해야 한다. 출발 감소만으로 수령 성공을 확정하면 오배송과 다른 물품 변화를 구분할 수 없다.

## 4. 통로 정체도 같은 공통 문제를 드러낸다

TravelSkill은 갈림길까지 걸은 뒤 선택한 방향이 없으면 `ObserveOnce("waiting:" + actual.Token, ...)`를 호출하고 Idle을 반환한다. 상태는 Running이다. Forecast도 같은 junction token을 이미 알렸다면 추가 알림을 내지 않는다. [AITravel.cs 204행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AITravel.cs:204), [AITravel.cs 277행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AITravel.cs:277)

다음 갈래 진입에는 travel_turn 선택으로 ReserveTurn이 실행돼야 한다. 일반 continue는 그 방향을 지정하지 않는다. #62·#63 continue 뒤 같은 갈림길에서 기다리는 것은 이 실행 의미와 일치한다. [AITravel.cs 236행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AITravel.cs:236)

현재 continue 설명은 공통으로 “이어 하기”와 전환 비용을 강조한다. TravelSkill.Progress는 “다음 행동 판단 중”이라고 표시한다. 이미 방향 응답이 끝났고 아무 방향도 선택하지 않은 상태에서 이 설명이 계속 남는다. 대기 상태에서 continue가 실제로 만드는 효과와 아직 필요한 선택을 정확히 표시해야 한다. 이 설명이 모델 선택을 유발한 직접 원인인지는 실제 전체 입력 미보존 때문에 확정할 수 없다. [AIBrain.Candidates.cs 193행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs:193), [AITravel.cs 302행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AITravel.cs:302)

## 5. 완료 권한과 완료 사실의 연결도 손실된다

finish_goal은 사용자가 합의한 하위 모델의 현재 단계 완료 권한이다. 원래 부탁의 완료는 상위 order=done이다. 이 권한 자체를 버그로 분류하지 않는다. [공통 실행 합의](../2026-11-07-공통실행-합의.md), [공통 실행 계약](../2026-11-07-공통실행자료/contract.md)

그와 별개로 FinishChosen은 완료 관측이 없을 때 detail에 “행동 판단기가 현재 단계를 완료로 선택”을 넣는다. 그러나 다음 단계가 있으면 AIPlanner.OnGoalEnded는 “계획 n단계 끝” 이벤트만 추가하고 즉시 반환한다. 이 분기에서 why의 관측 근거가 보존되지 않는다. 경험 기록에도 completed가 남는다. 상위가 추후 실제 완료와 모델의 완료 선언을 구별하는 데 필요한 정보가 약해진다. [AIGoals.cs 368행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIGoals.cs:368), [AIPlanner.cs 311행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanner.cs:311)

투구는 실제 give 실행 없이 완료 선택 후 갑옷 단계로 넘어갔다. 구리 채집은 최신 실행에서 0/5인데도 finish_goal이 선택됐다. 두 사례 모두 실제 물품 확보를 검증한 완료와 모델이 단계 종료를 택했다는 기록을 함께 보존해야 한다.

## 6. 이전 단계 관측이 새 단계 입력에 섞인다

SetGoal은 Completion·Blocked를 지우지만 AIBrain의 ExecutionReason·ExecutionRevision을 새 목표에 귀속시키거나 초기화하지 않는다. BuildInput은 “최근 실행 관측”에 이 문자열을 그대로 넣는다. [AIGoals.cs 261행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIGoals.cs:261), [AIBrain.Loop.cs 1427행](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:1427)

최신 #56은 새 목표 CopperOre5에 대해 trigger가 직전 제작의 “요청 수량 확보1/1”이었다. #56 뒤 CopperOre가0/5로 완료 기록됐다. BuildInput 코드에는 새 목표의0/5도 들어가므로 입력 일부가 모두 잘못됐다고 단정할 수 없다. 다만 서로 다른 단계의 완료 정보가 명확한 귀속 없이 한 요청에 공존하는 경로와 SEND 로그가 확인된다.

t637 상위는 실제 구리0을 보고 채집부터 다시 계획했다. 상위 재계획 기능은 동작했다. 이후 기존 통로 스킬을 continue하면서 다시 현장 판단이 잠들었다. 계획 생성 능력만 개선해도 이 연결 문제는 남는다.

## 7. 최적화와 기존 결함 구분

직접 `git diff 7af8f91 43c3817`로 확인한 AIInventory.cs·AITravel.cs·AISkill.cs·AIPlanner.cs·AIGoals.cs 변경은0이다. 회귀 비교 담당도 AIExperience.cs·AIInventoryMenu.cs까지 동일함을 독립 확인했다. Loop의 AskedExecutionRevision 소비, 비전투 _needCall gate, combatContinuous 전용 연속호출도 기존 경로다. 최적화에서 준비 중 새 이벤트 보존 등 주변 변경은 있으나 위 두 정체의 필수 조건을 새로 만들지 않았다.

회귀 담당이 확인한 도입 이력은 다음과 같다. 2026-11-07의685dde4에서 ObserveOnce·ExecutionRevision 공통 관측 연결이 들어왔다. 같은 날77484ec7은 give6초 미확인 시 Failed로 끝내던 처리를 ObserveOnce 후 Running 유지로 바꿨다. 해당 diff는 이 조사에서도 직접 대조했다. 51e07a0의 통로 구현은 처음부터 갈림길 대기를 한 번 알리고 Idle·Running을 유지했다. 자동취소를 제거한 사용자 의도와, 이후 미해결 상태가 다시 판단받지 못하는 구현 공백을 구분해야 한다. [회귀 독립 분석](regression-analysis.md)

1차 후보2a0112d는7526개 지점 준비를147.9초 동안 끝내지 못했고 별도258개 목록은API255상한을 넘었다. 이 후보는 실기 미수용이었다. 최종43c3817은 대표 계획→하위 요청→이동을 통과했다. 당시에도 장애물 변화 취소26회와 새 목표 적용 후8.9초 지연이 남았다. [최적화 완료](../2026-11-08-path-optimization/completion.md), [실기 검증](../2026-11-08-path-optimization/live-validation.md)

그 실기는 갑옷 제작·전달 완료, 원정 메뉴 전체 실제 모델 탐색, 오래 기다린 뒤 자기복구를 수용 조건으로 검증하지 않았다. 따라서 최적화의 대표 이동 통과를 사용자 부탁 전체의 수행 품질 입증으로 확대할 수 없다. 현재 장기 정체 구간은 이미 정상 응답을 받은 뒤 추가 SEND가 사라진 구간이므로 준비 취소 누적만으로 설명되지 않는다.

“예전보다 거의 움직이지 않는다”라는 체감의 전체 원인을 정량화하려면 동일 월드·동일 요청·동일 상태의 이전 버전 비교가 필요하다. 이번에는 게임 조작 없이 기존 자료로 원인을 좁혔으며 그 A/B 비교는 수행하지 않았다.

## 8. 실제 수정 우선순위

1. 공통 재판단 연결부터 수정한다. 관측 전달 여부와 미해결 판단 필요 여부를 구별하고, 전달 확인 대기·갈림길 방향 대기에서 continue를 받은 뒤에도 해당 상태가 해소될 때까지 예산 범위 안에서 갱신된 재판단이 가능하게 한다. 작업을 자동 완료하거나 임의 방향으로 이동시키는 방식은 사용자 합의와 맞지 않는다.
2. Give 관측을 복구한다. 요청·출발·수령 측 변화의 귀속을 유지하고, 임시 entity를 놓쳐도 확인 가능한 사실을 계속 수집한다. 미측정을0으로 표시하는 문제와 수령 직후 착용·상태 변화 시 MatchingCount 의미를 함께 검증한다.
3. 현재 단계·이전 단계·실행 회차의 관측 귀속을 명시한다. finish_goal 선택과 실제 완료 관측을 모두 상위·경험 기록까지 전달한다. 이전 제작1/1이 새 채집0/5의 현재 달성 사실처럼 읽히지 않게 한다.
4. 실제 하위 요청 input·전체 후보 key와 설명·선택·실행 회차·관측 수정번호를 연결해 보존한 후 판단 품질을 재검증한다. 메뉴 enter/category/item/operation/recipient/quantity 실행까지의 선택 취소 반복, continue 선택, finish_goal 선택을 이 자료로 평가한다.
5. 준비 취소 반복과 탐색 후보의 트리 이동 후 실제 선택 경로를 별도로 검증한다. 전체 리팩토링 전에 위 공통 정체와 전달 관측을 고친 뒤 동일 요청의 끝까지 실행 결과를 확인하는 순서가 적절하다.

검증에는 실제 전달 뒤 바닥 entity가 보이지 않는 경우, 갈림길에서 continue 후 새로운 외부 사건이 없는 경우, 실행 중 새 사용자 목표, 수량0 상태의 단계 완료 선언, 이전 단계 완료 직후 새 단계 요청을 포함해야 한다. 원래 부탁의 성공 여부는 최종 사용자 수령·제작 결과까지 확인해야 한다.

## 한계

현재 사용자 실행에서는 decisionlog.on이 꺼져 있어 하위 전체 input·후보 설명의 실제 송신본이 남지 않았다. SEND의 trigger·목표·후보수와 RESULT의 선택은 확인되지만, 모델이 그 선택을 한 내부 이유는 알 수 없다. 코드의 입력 생성 경로는 실제 송신문 원문을 대체하지 못한다.

출발 감소와 사용자의 수령 발언은 강한 관측이지만 바닥 entity의 생성·픽업 생애 trace가 없어 누락의 세부 기전은 미확정이다. 최신 사본 이후 실행은 없었으며 실기 담당 확인 시 게임은 종료돼 있었다.

분석 파일 이외의 변경과 신규 모델 호출·게임 시험을 수행하지 않았다.

