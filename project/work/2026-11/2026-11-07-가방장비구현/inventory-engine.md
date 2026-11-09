# 가방·장비 실행 모듈 결과

격리 worktree `C:/Dev/CoreKeeperModSDK-bag-equipment`의 새 `Assets/AIPlayerSpike/Scripts/AIInventory.cs`와 `Core/AIInventoryRules.cs`, `Tools/core-tests/InventoryFlowTests.cs`를 작성했다. 기존 파일의 상위 계획·프로토콜·몸 입력 연결과 커밋은 상위 실행 소유자가 맡는다. 단계1은 d8f2084, 후속은 상위 소유자가 bed7ef9334ff290d95d9a5aae4c14901b827805f로 커밋했다. 현재 제품 파일은 검수를 위해 동결했다.

## 실제 구현

- 장착·교체·해제는 실제 활성 장비 슬롯과 정상 Swap 요청을 사용한다. 반지·파우치 목적지는 구분한 후보이며, 양 슬롯 교환이 관측돼도 활성 프리셋이 바뀌었으면 현재 착용 성공으로 처리하지 않는다. 배낭·파우치의 용량/태그 변화로 게임이 물품을 자동 드롭하는 조건을 검사한다.
- 명시 핫바 목적지는 점유 상태와 무관하게 실제 교체 후보로 제시한다. 완료는 Swap 결과와 EquippedObjectCD의 해당 slot/objectID/variation/aux 관측을 함께 확인한다. 곡괭이·근접무기의 개별 명시 선택을 캐릭터별로 보존하며 Reset으로 월드/캐릭터 수명 경계를 정리한다.
- 같은 목표의 성공 receipt 이후 이전 장비가 들어간 출발 슬롯을 다시 진행 후보로 제시하지 않는다. 일반 목표에서는 장착·곡괭이 선택·근접무기 선택·해제 범주를 먼저 확보하고, 같은 물품의 목적 슬롯 조합이 후보를 독점하지 않게 구성한다. 점수로 최적 장비를 자동 선택하지 않는다.
- 자기 상자는 기억된 소유 관계를 유지한다. 접근·열어 살펴보기 후에만 현재 슬롯을 후보로 공개하고, 다음 판단에서 품목·정확 출발 슬롯·요청 수량·남길 수량을 지정한 보관/꺼내기를 실행한다. 명시 slot-only 장비 보관도 허용한다. 일반 무품목 자동 보관의 whitelist는 기존 모듈이 유지한다.
- 물품 이동은 overflow drop이나 destroy를 사용하지 않는 정상 MoveAmount 요청이다. 요청 한 번 이후 출발 감소와 목적 증가가 같은 실제 수량인지 확인한다. 비중첩 물품의 amount는 개수가 아닌 상태로 유지한다. 일부 이동은 실제 수량만 누적하며 요청 전체를 성공으로 만들지 않는다.
- Drop/Give는 명시 목표에서만 만든다. Give는 보이는 지정 수령자에 접근해 자기 현재 위치에서 정상 DropItem을 사용한다. 다른 플레이어 가방에 직접 넣거나 강제 pickup 상태를 쓰지 않는다. 출발 감소·신규 바닥 entity·지정 target의 HasBeenPickedUp 관측 receipt·실제 수령자 가방 증가를 함께 확인한다. IsBeingPickedUp만 본 뒤 바닥 물품이 사라진 경우는 전달 완료로 승격하지 않는다. 귀속 모호·다른 수령자·미관측은 재전송 없이 사실로 보고한다.
- 목표 바인딩은 같은 AIGoal 인스턴스에 한정한다. 개별 요청 성공은 원래 부탁이나 계획 단계를 자동 종료하지 않는다.

## 실제 검증

2026-11-07, 후속 동결판에서 다음을 실행했다.

- `dotnet build Tools/compile-check -c Release --nologo`: 성공, 경고 0·오류 0. 설치된 게임 어셈블리에 대한 C# 정적 컴파일이며 Unity/실게임 검증은 아니다.
- `dotnet run --project Tools/core-tests -c Release -- INV-FLOW`: 제품에서 사용하는 SwapProgress/수량·수령 관측 helper 22건 통과. 큐 접수와 성공 구별, 중복 전송 차단, 두 슬롯 보존, 비중첩 상태, reserve/부분 수량, 우연한 수령자 가방 증가·모호한 바닥 귀속·pickup 미완료의 거짓 성공 방지를 포함한다.
- `dotnet run --project adapter-repro/Review.csproj -c Release`: 실제 `AIInventory.cs`와 규칙을 Link해 30건 통과. **World·버퍼·DB·큐는 대역이고 실제 슬롯 변화는 시험이 수동으로 주입했다. 게임 consumer·실게임 성공 증거가 아니다.** 대역의 소문자 math 형식명 경고 CS8981 1개는 제품 컴파일에 없다.

adapter 시험은 점유 핫바 지정, 큐 접수 이후 미관측 대기·중복요청 없음, 실제 두 슬롯/손 상태, 동일 ID 다른 variation 교체 후 역교체 방지, 활성 프리셋 변경, 무상자 Store CheckGoal 허용과 미실행 경계, inspect 전 내용 비공개, 30개 중 10개 남기고 20개 실제 보관, 지정 5개 실제 꺼내기, slot-only 장비 보관, 후보 범주 다양성, 수명 Reset, 정확 pickup receipt 없는 Give 미확인 및 receipt+수령 변화 완료를 확인했다. 기존 Base.Candidates의 상자 제작/배치 자체는 이 adapter에 포함하지 않았다.

## 근거·한계

- [모듈 명세](inventory-spec.md), [상위 연결 명세](integration-spec.md), [정상 Drop/Give 경로](transfer-rules.md), [이번 조사](../2026-11-07-가방장비-조사.md)의 고정 게임 규칙을 사용했다.
- [입력/제품 파일 해시](inventory-input-hashes.json)에 후속 동결판과 장착·컨테이너 수용 규칙 및 증분 인덱스를 기록했다. 이 인덱스 전체가 실게임으로 검증됐다는 뜻은 아니다.
- 슬롯/objectID/variation/aux/state 조합은 현재 관측 가능한 물품 구별 근거이며, 게임이 제공하지 않는 영구적인 물품 UUID라고 주장하지 않는다. 빠른 pickup이나 바닥 병합으로 출처와 pickup receipt를 놓치면 수령을 추정하지 않는다.
- 설치·실게임·Unity 실행은 하지 않았다. 최종 독립 검수와 Unity 및 공통 실행 통합은 HEAD/상위 실행 소유자가 진행한다.
