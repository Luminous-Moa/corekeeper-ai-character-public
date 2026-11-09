# 가방·장비 상세 연결 명세

상위 근거는 agreement.md와 승인된 계획안이다. 상세 실행 담당이 기존 구조를 확인해 구현 입구·상태·오류·검증을 아래처럼 구체화했다. 제품의 목적·소유 정책·판단 주체는 바꾸지 않는다.

## 입력과 출력

상위 계획의 기존 do/item/count/where에 keep(보관시 남길개수), slot(정확한 출발칸)을 선택 필드로 추가한다. 누락은 -1로 보존한다. do는 equip/unequip/hold/take/drop/give를 등록한다. 기존 store는 무품목·무수량·무목적지의 일반 가방정리를 유지하고, 품목/개수/keep/slot/목적지 중 명시조건이 있으면 새 경로로 처리한다. where는 장비 목적칸, 자기상자 식별자, 전달 수령자다. 새 do가 허용됐다는 사실만으로 실행지원이 있다고 보고하지 않고 ReportJson.operations와 현재 CheckGoal을 일치시킨다.

상위 응답은 AIPlanProtocol → PlanStep → AIPlanner → AIGoals.SetGoal 순서로 필드를 손실없이 전달한다. 미래 계획단계는 형태를, 실제 착수시점에는 현재 월드에서 가능한 대상을 검증한다. 없는 품목·수령자 없는 give·유효범위 밖의 수량/slot은 이유와 함께 거절한다. 장비 동작은 한번에 1개, 일반 이동은 요청량과 여유공간에 제한된다. 비중첩 장비의 amount는 개수가 아닌 상태로 유지한다.

## 상태와 선택

새 AIInventory 모듈은 캐릭터별 상태, 개별 인벤토리 스냅샷, 후보→정상 게임 요청→관측결과를 소유한다. AICandidate.inventory는 대상 엔티티·정확한 출발/목적 슬롯·판단 당시 스냅샷·개수·연결 목표를 담는다. 후보 kind=inventory는 AIAction을 통해 InventorySkill을 시작하고 Stale은 재검증을 맡는다. 원래 부탁과 제어동작은 기존 공통 실행 소유다.

장비·들기 후보는 목표가 있는 동안 현재 가진 물건에서 실제 대상별로 비교한다. 내려놓기·전달은 해당 목표가 있을 때 생성해 코드가 임의로 물건을 버리지 않는다. 착용칸은 현재 활성 프리셋의 실제 인덱스다. ring/pouch는 목적칸을 명시하고 구분한다. 장착·해제·핫바 교체는 정상 Swap 요청과 양쪽 실제 변화, hold는 실제 EquippedObjectCD를 확인한다. 배낭/파우치 변경의 용량·태그 영향으로 자동 드롭될 물건이 있으면 막고 사실을 보고한다.

명시 손 선택은 도구/근접무기별로 보존한다. AIBody.ChooseSlot/CanHold/HandSlot/WeaponSlotForAttack/FindWeapon은 그 선택이 있으면 우선 사용한다. 손 물품의 소실·파손·작업 부적합은 이유로 보고하며 다른 물건으로 몰래 대체하지 않는다. 명시 선택이 없으면 기존 자동 준비 경로를 유지한다.

## 완료와 실패

InventorySkill은 한번 요청한 후 실제 슬롯 변화를 기다린다. 큐 접수는 성공이 아니다. 후보 이후 슬롯 내용·프리셋·목적지 조건이 달라지면 그 물건을 조작하지 않고 사유를 보고한다. 관측 대기 한도 뒤에는 실제 확인 여부를 명시하며 같은 요청을 무조건 반복하지 않는다.

개별 성공 receipt는 연결된 같은 목표 객체에만 반영한다. AIGoals.CheckDone/CheckAfterBuild는 새 목표의 관측 성공·진행불가를 note에 올리고 자동 원래부탁 완료·목표 취소를 수행하지 않는다. 단계 완료는 공통실행 finish_goal에 의존한다. 따라서11eb461 기반 독립판의 장비조작 확인과 공통실행 고정판을 합친 전체 계획 실행 확인은 구별한다.

Drop/Give는 transfer-rules.md와 독립 교차조사를 사용한다. 자기 현재 위치에서 정상 DropItem으로 내려놓고 dropper의 재줍기를 차단한다. 다른 플레이어 가방 직접 주입은 하지 않는다. Give는 특정 수령자를 보며 접근하고 바닥 물품/targetEntity와 수령자의 실제 물품증가를 대조한다. 타인 선점·일부 수령·귀속 미확인은 그대로 보고한다.

## 변경 범위와 검증

격리 worktree의 AIInventory 새 모듈, AIActions/AIBody/AIGoalFeatures/AIGoals/AIGoalText/AIDecisions/AIPlanner/AIPlanReport/AIBrain 입력, Core의 목표등록/상위응답/계획필드·형태검사와 실제 상태계산 helper, bridge protocol/brief, 공유계약·영향받는 테스트를 변경한다. 본 저장소 및 기존 미커밋 변경은 보존한다.

검증은 fast compile-check, 실제 프로토콜과 공유계약 회귀, 전체 Core 회귀, 새 실제 helper를 사용한 상태/수량/거짓성공 경계시험으로 한다. Unity 컴파일은 HEAD와 실행직전 조정한다. 정식 설치·게임실행·자동조작은 보류하며 runtime-plan.md에 대표 흐름을 준비한다. 최종 독립 검수는 HEAD가 소유한다.
