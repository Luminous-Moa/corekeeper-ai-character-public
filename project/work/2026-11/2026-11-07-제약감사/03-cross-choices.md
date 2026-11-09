# 교차 검수: 상위 계획·정보 감사

01 조사자가 02-plans-info.md의 우선 항목을 별도 코드 읽기로 대조했다. 기준 ai-character 5a05b80. 실행 검증이 아닌 정적 경로 확인이다.

- **P01/P02/P03 수용:** AIGoalKinds의 gather30/craft10이 StepForm과 CheckGoal 양쪽에서 실제 거절로 연결된다. gather 품목4종+_gatherFood도 CheckGoal 거절 조건이다. 게임 물리규칙이 아니며 원정량과 분리할 우선 후보라는 판단에 동의한다.
- **P04 수용:** protocol.mjs PLAN_MAX10은11단계 이상 전체 shape reject, AIPlanner.cs:676~677은 upperPlanMaxSteps를 넘는 뒤쪽을 RemoveRange한다. 모드에서는 AddEvent로 잘림을 알리므로 완전히 조용한 잘림이라고 표현하면 안 된다. 단, 잘린 계획 본문은 보존되지 않는다.
- **P05 수용:** Core/AIPlaces.cs:36,79에서 rect/line ShapeCount>30이면 Parse 단계 거절, AIPlanner.cs:811에서 place Count 상한을 적용한다. 한 요청량과 프로젝트 전체 작업량 분리가 적절하다.
- **I01 중요한 정정:** 상위 bag 요약 첫10개는 사실이지만, AIPlanReport.cs:94가 별도로 AIInventory.ReportJson을 넣고 AIInventory.cs:795~800의 bag_slots는 MainStart..MainEnd 모든 비어 있지 않은 슬롯을 전달한다. **상위 LLM이 10품목 밖 물품을 못 본다고 단정하면 틀리다.** 첫10 요약과 전체 별도 필드의 표현 불일치/중복 문제로 기술할 것. 현장12개도 가방 트리에서 전체를 탐색할 수 있어 단순 보유물품 접근 불가가 아니다.
- **I02 수용:** AIPlanReport.cs:80~81의 CraftGroups40 및 CapGroups1800자 소비를 확인했다. 현장 제작 트리에서 품목을 별도 선택할 수 있으므로 현재 게임에 그 제작행동 자체가 불가능하다는 뜻은 아니다. 상위의 한 보고에서 비교할 수 있는 대안 축약이다.
- **I04/I08 수용:** AIPlaces.Pick는12줄/900자 및 그룹별 quota를 실제 적용하고 more를 만든다. AIHostOrders.Add는 MaxOpen3 초과 때 오래된 열린 지시를 ‘밀려남’으로 바꾼 뒤 Prune한다. 후자는 단순 보고 잘림보다 강한 상태 변경이다.
- **숫자!=결함 구별:** 생활 count9999·장비1·서비스 예산·검색 deadline·실행 묶음5/6은 목적별 상한이다. 상위 gather30과 같은 이유로 일괄 삭제하면 안 된다. 02가 구분한 판단은 타당하다.

01 자기 수정도 기록: 생활 트리의 combat 필터를 가방 장비 전체 금지로 읽을 위험을 발견해 C02를 좁혔다. 실제 InventoryChoiceSurface는 위험 상황에서도 별도 가방 메뉴를 추가한다. 8/14 상한 제거 여부는 source Finish/LimitFor와 menu 전체 순회를 확인했으며 제거됨으로 수용한다.

최종 보고에서 먼저 다룰 항목은 수량30/10, 품목 허용4종, plan 길이와 열린지시3, 장소·제작 대안 보고 축약, 현장의 목표/허기 기반 후보 숨김이다. 페이지5는 생활 트리에서 전체 탐색 가능하므로 ‘5개 초과 불가’라고 쓰지 않는다.
