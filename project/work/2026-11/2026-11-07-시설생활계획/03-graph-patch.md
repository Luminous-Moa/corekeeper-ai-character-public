# 시설 생활 계획의 그래프 대응 제안

이 문서는 [계획안](02-plan.md)의 변경 제안 대응표다. 실제 프로젝트/게임 그래프를 수정하지 않았다. 사용자가 승인한 구현 범위와 관측 결과가 생긴 뒤 그 지위에 맞춰 반영한다. 새 ID·프로토콜은 아직 확정하지 않는다.

| 계획안 자리 | 현재 노드·연결과 관측 | 제안하는 변경/연결 | 지위와 간접 영향 |
|---|---|---|---|
| 왜 하는가 / 선택할 수 있게 만들 것 | P.experience→P.life→P.hierarchy. P.execution은 LLM의 대상·수량·순서 선택과 코드의 실행을 구별 | 시설별 계획·선택·실행·수령을 해당 경험/합의부터 추적할 수 있게 연결 | 합의 유지, 시설 구체화는 제안. Q.autonomy/Q.goal-rules의 미정을 확정하지 않음 |
| 결과 단계 구별 / 첫 요리 흐름 | P.original-request→P.valid-action. UA-03은 제작 부탁 활동이지만 product user 조회에 출력요소/기능 연결 없음. 실제 CraftSkill·CookSkill 존재 | 기존 활동에 현행 실행·상태 보고와 결과 수령 근거를 이어 줌. 시설 투입/생산/수령/원래 부탁 완료의 의미를 구별 | 빈 그래프를 미구현으로 복제하지 않음. 새로운 실기 증거 전에는 정상 AI 흐름 관측 상태를 올리지 않음 |
| 1 요리 | 기존 F.craft/F.goal-cmd에 대응하는 UA-03 및 P.life. 게임 G 4013:0 cooking과 요리 입력 순서쌍, 현행 AICook Predict/BestPair/Candidates/CookSkill | 재료 짝/순서/수량/자기 솥 선택, 투입·실제 수령과 후보 identity를 프로젝트 기획/기능/관측으로 연결 | BestPair는 추천 계산 재사용 후보. 게임 자체 조건을 제품 정책으로 바꾸지 않음 |
| 2 제작·제련 | G WoodenWorkBench recipe_ui_craft와 Furnace process_recipe_selection/automatic_recipe_result는 서로 다른 단계. AICraft/CraftSkill은 일반 제작 구조 | 일반 제작과 자동 가공의 실행·생산·수령 대응을 각각 기록. 현재 코드가 확인하는 조건과 새로 보완할 조건을 분리 | 레시피/결과 variation·출력 개수·재료 보정은 source와 runtime 조건 보존. 구리 주괴 확보를 갑옷 부탁 완료로 표시하지 않음 |
| 3 수리 / 5 강화 | G 4025 open_repair, 4046 use_upgrade_forge. 후속 completion:C:repair-item-conditional-v1 / completion:C:upgrade-equipment-conditional-v1와 source별 conditional application이 staging에 있음 | 장비 상태 관측→대상 선택→비용 확인→해당 요청→내구도/레벨·재료 실제 변화로 연결. 수리/내구강화/레벨업을 구분 | source의 원본 생산 당시 review_pending과 receipt independent_pass를 보존. 가방장비 담당의 정확한 슬롯/물건 선택과 경계 연결 |
| 4 농사 | P.life, 실제 AIFarm 후보·FarmSkill. G 작물 대상 역조회는 일부 빈칸, 농사팔 행위는 별도 주체 | 직접 경작의 종자/칸/수량 선택과 결과를 기존 밭 관측·기억에 연결. 로봇팔 자동화의 역할을 손 경작과 혼동하지 않음 | G 역조회 전면 수정은 이 계획의 선행 조건 아님. 자료 수집/정본 통합 담당과 중복 변경하지 않음 |
| 6 낚시·거래 | G 도구/상인의 시작 entry, staging completion:B:fishing-50-0 / completion:terra:merchant-3900-sell-5013-0에 결과 소비자 | 별도 실제 목표→입력/상태→전리품·거래 수령의 실행과 결과 관측을 연결 | 판매 표본을 구매 성공으로 확장하지 않음. 무작위 전리품을 고정 품목 보장으로 표시하지 않음 |
| 기존 작업과 연결 | 공통 실행 685dde4의 기존 AICandidate/AISkill와 stop_action/finish_goal/request_plan. 가방장비 작업 진행 중 | 시설의 대상/입력/수량과 실행 결과가 같은 공통 계약을 사용하도록 명세. 후보 key와 실행 회차·상태 관측 대응 | 제어 동작 중복 구현을 만들지 않음. 기존 Unity/게임 버퍼와 실행 사실의 원본 주체를 유지 |
| 지킬 조건과 검증 | P.knowledge, 자기 상자·솥·밭 규칙, 기존 실게임 보류 | 계획 입력은 공개 범위와 현재 관측/마지막 관측을 구분. 승인된 범위 구현 뒤 대표 실기·정적 근거·미관측 상태를 각각 기록 | 공용 물건 접근·새 예산·자율 소비 정책의 신규 결정 없음. 게임 지식 전체 공개나 새 UI 자동 추가 없음 |

## 실제 갱신 시 원칙

프로젝트 기획 원문에 승인된 범위만 기록하고 프로젝트 그래프의 생성 원본에서 연결한다. 구현 후보는 후보, 통합된 코드는 구현, 실기 결과는 관측으로 지위를 구별한다. generated graph.json에 수동 패치하지 않는다. 게임 행동 G는 게임 규칙의 파생 색인으로 유지하며 제품 실행 능력 추가를 게임 원본의 새 사실로 넣지 않는다.

이번 계획에서 저장 상태의 새 영구 스키마, 외부 API, 사용자 화면, 계측 서비스는 제안하지 않았다. 시설 입력·생산·수령과 장비 변화의 전후 사실은 기존 실행 보고·기억 경로에 필요한 연결을 보완하는 방향이며, 새 데이터가 필요한지는 명세에서 기존 상태와 비교한 뒤 정한다. 비용·재료 수치와 특정 장비 상태는 source별 근거와 runtime 관측에 기대며 전역 상수로 일반화하지 않는다.
