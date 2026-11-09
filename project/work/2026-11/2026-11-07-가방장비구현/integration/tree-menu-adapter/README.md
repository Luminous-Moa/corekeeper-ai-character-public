# 통합판의 기존 메뉴 회귀

현재 작업 폴더의 실제 `AIInventoryMenu.cs`, `AIInventory.cs`, Core 규칙을 링크해 이전 741 fixture의 Program/assertions를 변경하지 않고 실행했다. 결과는 [상위 menu.log](../menu.log)의 **348 checks / 0 failures**, exit 0이다.

통합으로 추가된 life 경계의 컴파일을 위해 이 사본의 `Stubs.cs`에 `Entity.Version`, `AIGoal.itemName`, `AICandidate.life`, `LifeSelection`·`ILifeResult` 타입 계약을 추가했다. 실제 `AILife`는 이 fixture에서 대체하되 `ForGoal`/`Resolve`/`RequestSatisfied`와 life 전용 `ParseItem`은 호출하면 예외를 내도록 했다. 따라서 이 통과는 생활 트리·공유 수량 정책을 가짜 구현으로 재현한 근거가 아니다. 그 연결은 상위 소유자의 실제 AILife 포함 connected integration 검사에서 확인한다.

World/DB/경로/큐는 최소 대체이며 실제 결과 버퍼는 시험에서 수동 공급한다. 실제 게임 소비자·모델·게임 실행은 수행하지 않았다. fixture의 소문자 `math` 이름 경고 CS8981 하나는 제품 컴파일 경고와 구별한다. 이 보완은 제품 소스·Git stage/commit을 변경하지 않았다.
