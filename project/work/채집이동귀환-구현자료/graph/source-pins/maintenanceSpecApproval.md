# 장비 정비 명세 승인

[명세](02-spec.md)를 승인한다. 기존 생활 트리를 재사용해 maintain의 일반 수리·단일 레벨 업그레이드·자기 투입물 회수를 연결하는 범위는 [승인 방향](01-direction-decision.md)과 일치한다. 새 필수 사용자 결정은 없다.

게임의 실제 비용 helper와 정상 단일 Create 요청을 사용하고, 정확한 장비/시설·물품 이동·요청 처리 여부·상태 변화·가방 회수를 구별한다. 임시 pending은 중단 뒤 자신의 투입물 회수에 필요한 근거로 보존하며 타인의 물품이나 새로운 영구 저장 체계를 추가하지 않는다. 수리/강화 반복과 원래 부탁의 종료를 자동화하지 않는다.

명세의 실제 생산 파이프라인·직접 영향 회귀·Unity 컴파일을 수행하고 새 변경을 독립 검수한다. 이전 단계 그래프 검수·기존 baseline 검증은 반복하지 않는다. 설치·실게임 보류는 유지한다. 다른 담당과의 최종 통합에 필요한 현재 코드 확인은 새 변경의 통합 경계에서 수행한다.

## 정상 업그레이드 API 인수 정정

실제 Inventory/Create.cs의 공개 Upgrade는 inventoryEntity, playerEntity, craftingEntity, index, objectID, chestsStartIndex, craftingMaterialsAreNotRequired의 7인수다. 명세의 예시에서 누락된 craftingEntity를 선택한 forge로 정정해 Create.Upgrade(forge, player, forge, inputAbsoluteSlot, itemId, 1, false)를 사용한다. 대상 물품을 대장간 입력에 넣은 뒤 같은 시설에서 정상 업그레이드한다는 기존 의도를 보존하는 API 어댑터 정정이며 새 정책 결정은 아니다. HEAD가 이 정정을 승인했다.

## 부작용 전 intent 정리 보충

HEAD는 Posted=false && PutConfirmed=false인 로컬 intent가 시작 실패/중단으로 끝났을 때 Cleanup에서 그 intent만 제거하는 분기를 승인했다. 정상 로컬 큐 등록은 아직 UI 게시 전이어도 Posted로 취급한다. 등록/게시했거나 투입이 확인된 pending은 기존대로 보존한다. 부작용 없는 intent가 새 정비와 회수를 함께 막는 상태를 방지하는 보충이며, 재발송·자동 성공·타인 물품 채택을 추가하지 않는다. 명세 §6과 §10의 직접 영향 회귀에 반영했다.
