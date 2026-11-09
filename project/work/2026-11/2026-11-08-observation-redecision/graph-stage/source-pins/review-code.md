# 코드·오프라인 독립 검수

2026-11-08. 격리 작업 폴더 `C:/Dev/CoreKeeperModSDK-observation-redecision`, 기반43c3817에서 만든 후보09e418a8af85bd1578b2cfea09cdbe3c0180db77을 검수했다. 이후 상위 완료 경계 검수까지 반영한 최종 후보는20b1aced7594a47eacb4be060405bcb53d38f48f다. 검수한12개 제품 파일의 최종 SHA256은 [review-code-sha256.json](review-code-sha256.json)에 보존했다. 최종 제품 소스와 Tools의 HEAD 대비 차이0을 확인했다.

## 판정

코드·오프라인·Unity 컴파일 범위 수용. 현재 확인한 차단 결함은 없다. 최종 수용에는 실제 게임의 지정 수령·다음 부위 진행·조용한 통로 대기에서 재판단 후 이동 증거가 필요하다.

## 직접 확인한 결과

| 검사 | 결과 |
|---|---|
| `Tools/execution-tests/run.ps1` | 156/156, exit0 |
| `dotnet run --project Tools/life-inventory-tests/LifeInventoryTests.csproj -c Release` | 171개, 실패0, exit0 |
| `dotnet run --project Tools/travel-tests/TravelTests.csproj -c Release` | 51/51, exit0 |
| `git diff --check` | whitespace 오류0 |
| Unity 최종 로그 | `[HeadlessSdk] CompileCheck: ok`,5915행 직접 확인 |

execution 검사는 실제 AIGoals·AISkill과 생산 Apply·관측·재판단·예산·Planner 메서드 추출을 사용한다. HTTP transport acknowledgment와 세계 상태는 대역이다. inventory는 실제 InventorySkill과 두 observer를 실행하고 ECS consumer·시스템 그룹 정렬은 대역이다. Travel은 실제 TravelSkill.Tick을 실행한다. 전체 게임 Tick과 실제 ECS 정렬 결과는 실기에서 검증한다.

검수 도중 owner가1초 freshness 조건을 실제 body tick 관측 여부로 교체하면서 기존 시험2개가 실패했다. 시험을 실제 BeginObservationTick 수명에 맞춰 수정한 뒤 독립 재실행156/156을 확인했다. 새 시험은 실제 Brain→Body 순서를50ms와2초 frame 간격으로 검증한다.

Unity1차에서 새 SystemBase 선언2개의 partial 누락이 확인돼 수정됐다. 두 선언의 partial 추가를 직접 확인했으며, 최초 검수 이후 SHA가 바뀐 제품 파일은 AIInventory.cs 하나였다. 최종 해시를 갱신했고 최종 Unity 성공 로그를 확인했다.

## 수용 근거

- finish_goal은 적용 직전 현재 수량·기능 결과를 다시 읽는다. 구리0/5 및 수령 미확인에서는 목표·원래 부탁·몸 실행과 남은 계획을 보존하고 현 사실로 재판단한다. 확인된 결과는 명시 완료 선택으로 다음 단계로 이어진다.
- 완료 성공→다시 미확인으로 변한 사실도 새 수정번호로 전달한다. 이전 요청의 acknowledgment가 새 관측을 소비하지 않는 검사가 있다.
- 동일 pending은5초 검토 간격을 사용하고 기존 요청·준비·미소비 사건이 있으면 중복 주기 알림을 만들지 않는다. 새 사실은 즉시 전달되며 기존 호출·월 비용 가드를 따른다.
- 몸의 실제 Tick에서 관측한 조건만 다음 Brain에서 다시 검토한다. 같은 조건이 사라지거나 실행이 끝나면 과거 대기 사실로 주기 호출을 만들지 않는다.
- 새 목표는 이전 전역 완료 관측을 비운다. 유지되는 기존 몸 실행의 관측은 원래 목표·실행 회차 라벨을 가진다.
- Give는 수령자를 매 tick 측정하고 미측정을 명시한다. 현재 목표의 확인된 수령0/N도 첫 행동 전부터 입력에 표시한다.
- Give의 새 floor 귀속은 요청 전 존재하던 전체 pickup entity를 제외하고, sender의 pickup-block 상태·정확한 물품·자기 슬롯 실제 감소·유일 entity를 요구한다. 이후 동일 floor의 지정 수령 pickup receipt·실제 floor 소비·수령자 증가를 대조한다.
- 이미 송신한 drop을 pending 재판단 때문에 자동 재송신하는 경로는 없다. 타인의 동일 물품이 먼저 보이는 반례, 지정 외 플레이어 pickup, 모호한 복수 floor, 즉시 착용·내구도 변화, 관측 누락과 cleanup을 검사했다.
- 기존 목표가 바뀐 뒤 끝나는 Give가 새 목표의 수령 수량을 증가시키지 않도록 Completed의 원래 목표 참조 검사를 유지한다.
- 통로 방향 대기에서 continue는 서 있는 대기를 유지한다는 효과와 구체 방향 선택의 필요를 표시하며, 지속 대기를 다시 판단받는다.

## 발견사항 재검수

R1의 부정확한 대기 경과 시간은 현재 실행 경과 시간으로 표시하도록 수정됐다.

R2의 타인 동일 물품 오귀속은 sender pickup-block 상태를 PickupSystem 이전에 포착하도록 보완됐다. 독립 inventory 실행에서 타인 먼저·부수적 수령 증가 반례의 성공 오판이 차단됐다.

추가로 확인한 실제 호출 순서는 AIPlayerSpikeMod에서 Brain.Tick 다음 Body.Tick이다.1초 freshness 추정은 BeginObservationTick 기반으로 교체됐고50ms·2초 frame 검사가 모두 통과했다.

## 게임 처리 순서 독립 대조

게임 추출 소스에서 다음 순서를 직접 읽었다.

- PickUpItemSystem은 PredictedSimulationSystemGroup에 속하고 EndPredictedSimulationSystemGroup 이전이다.
- InventorySystemGroup은 EndPredictedSimulationSystemGroup 내부이며 InventoryUpdateSystem이 그 요청을 소비한다.
- DropItem은 새 entity에 BlockPickupUntilReEnterStart와 sender target을 기록한다.
- PickUpItemSystem은 지정 target으로 MoveOrDropAllItems를 큐에 넣고 HasBeenPickedUp을 기록한다.

따라서 새 source observer의 prePickup과 receipt observer의 preInventory 배치는 소스의 처리 경계와 맞는다. 런타임 동적 생성·그룹 등록·정렬의 실제 성공은 게임 검증이 필요하다.

## 남은 검증

실기 담당은 실제 하위 input·후보 key·선택 응답·목표rev/run을 보존해야 한다. 한 부위 수령, 후속 부위 실제 실행, 중복 지급 없음, 조용한 방향 대기에서 재판단과 이동, 새 목표의 부족량·완료 보류를 확인한다. 최종 설치 해시는 별도 재대조한다.

검수자는 제품 소스를 직접 수정하지 않았다.

## 최종 후속 후보 연결

상위 계획 거절과 동일 미완료 원부탁 done의 부분 적용 보호는 [후속 독립 검수](review-upper-completion.md)에서 수용했다. 최종20b1aced의 변경 파일은 기존 검수본 대비 AIGoals.cs·AIPlanner.cs 두 제품 파일이며 최종 SHA를 갱신했다. `unity-compile-order-guard.log`1794행의 CompileCheck: ok를 직접 확인했다.09e418a8의 실제 세 부위 수령과 다음 단계 성공은 [실기 독립 대조](review-live.md)에 기록했다. 최종 설치는 사용자 게임 종료 승인 대기 때문에 아직 보류 상태다.

