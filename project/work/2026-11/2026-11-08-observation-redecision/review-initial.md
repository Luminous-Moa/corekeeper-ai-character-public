# 설계·1차 코드 검수

2026-11-08. 진행 중 checkpoint이며 최종 수용 전이다. 검수 기준은 [review-criteria.md](review-criteria.md)다.

## 확인한 구현 방향

- AIGoals.FinishChosen은 완료 적용 직전에 실제 계량·기능 상태를 다시 관측한다. 미확인 시 목표·계획·몸 실행을 유지하고 재판단을 요청한다. Explore·Follow의 기존 비계량 종료 의미는 유지한다.
- 다음 계획 단계로 진행할 때 완료 근거를 상위 이벤트에 보존한다.
- AISkill은 현재 반복해서 관측되는 대기 조건을 추적하고, Brain이 기존 요청·준비·미소비 관측이 없을 때 재판단을 갱신한다. 새로운 관측은 즉시 수정번호를 증가시킨다.
- 목표 변경 시 전역 최근 관측을 갱신하고 현재 몸 실행의 원래 목표·실행 회차를 표시한다.
- TravelSkill의 방향 미선택 상태와 continue가 실제로 유지하는 정지 상태를 설명한다.

## 발견 및 대응

### R1. 대기 경과 시간의 의미

AISkill 최초 대기 시각이 실행 전체에서 한 번만 설정돼 다른 대기가 발생해도 첫 대기부터 시간을 표시했다. 구현 담당에게 전달했고, 현재 실행 시작부터 경과 시간으로 명시하도록 변경했다고 회신받았다. 최종 diff에서 재확인한다.

### R2. Give 바닥 물품의 송신자 귀속

초기 CaptureDelivery는 반경·아이템·상태·수량·기존 entity 제외만으로 새 floor를 귀속했다. 타인의 동일 물품이 먼저 유일하게 보이는 경우 이번 송신으로 잘못 귀속할 수 있다. 자기 출발 감소와 다른 물품의 지정 수령이 함께 관측되면 오성공 가능성이 남는다.

구현 담당에게 반례를 전달했다. 담당자는 PickUpItemCD의 BlockPickupUntilReEnterStart 및 targetEntity=sender를 PickUpItemSystem 이전에 관측하고, 자기 출발 감소와 함께 귀속하는 보완을 진행한다. 별도의 InventoryUpdateSystem 이전 관측은 이미 귀속된 동일 entity의 HasBeenPickedUp receipt를 보존한다. 최종 코드·실제 시스템 순서·반례시험에서 재검수한다.

### R3. 실제 순서 검증

새 관측 시스템은 게임의 DropItem 생성→PickUpItemSystem→InventorySystemGroup 소비 사이를 포착해야 한다. 생성한 observer가 선언된 update attribute대로 실제 서버 그룹에 등록되는지, 등록 시점 이후 다음 tick에서 제대로 호출되는지, 여러 AI와 cleanup 때 상태가 섞이지 않는지 확인해야 한다. 담당자가 게임 코드 순서 근거를 구현 보고서에 기록할 예정이다.

## 아직 필요한 증거

- 생산 AISkill·Brain 스케줄의 fake clock 검사와 준비·요청 중 새 관측 보존
- 실제 AIGoals·Apply·Planner 경로의 미완료 finish 재판단
- sender가 다른 동일 물품의 오귀속 차단, 빠른 수령, 미측정 상태, 중복 송신0
- 최종 Unity 컴파일과 실게임 실제 수령·다음 부위 진행
- 최종 하위 실제 입력과 후보·응답 원문

검수자는 제품 파일을 직접 수정하지 않았다.

