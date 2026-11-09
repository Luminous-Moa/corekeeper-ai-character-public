# 생활·가방 통합 작업트리의 생활 회귀

`C:/Dev/CoreKeeperModSDK-bag-common`, `bag-life-integration-20261107`의 생활/가방 병합 작업트리에서 수행했다. 고정 최종 커밋 수용이나 실게임 판정이 아니며 제품 코드는 이 작업에서 수정하지 않았다.

| 실제 실행 | 결과 | 로그 |
|---|---|---|
| `Tools/life-tests/run.ps1` | 96 통과 / 0 실패, exit0 | [life-main.log](life-main.log) |
| `dotnet run --project Tools/life-cook-tests -c Release` | 51 통과 / 0 실패, exit0 | [life-cook.log](life-cook.log) |
| `ExtractLifeBinding.ps1` 후 life-craft-tests | 19 통과 / 0 실패, exit0 | [life-craft.log](life-craft.log) |
| `ExtractBase.ps1`, `ExtractLifeBinding.ps1` 후 life-inventory-tests | 91 통과 / 0 실패, exit0 | [life-inventory.log](life-inventory.log) |

추출기는 현재 생산 파일을 실제로 다시 읽었다. craft/inventory 실행은 각 추출기 exit0을 확인한 뒤 검사했다. 이전 생성본에 기대어 통과한 것으로 취급하지 않는다. 기존 assertions는 변경하거나 약화하지 않았다.

## 좁게 보완한 대체 경계

새 인벤토리 메뉴 metadata와 공통 Loop 호출이 기존 생활 전용 harness에 없어서 초기 컴파일이 실패했다. 초기 로그도 `life-main-initial.log`, `life-craft-initial.log`, `life-inventory-initial.log`로 보존했다. 다음 파일만 추가해 관련 없는 메뉴 경계를 공급했다.

- `Tools/life-tests/InventoryMenuBoundary.cs`: 실제 데이터 클래스의 `AIInventoryMenuChoice`와 Loop의 메뉴 Stale/Reset/Apply/leaf 호출 대체 경계. 이 harness는 실제 AILife·Loop·farm 소스를 검증하며 인벤토리 메뉴 정책은 실행하지 않는다.
- `Tools/life-craft-tests/InventoryMenuBoundary.cs`, `Tools/life-inventory-tests/InventoryMenuBoundary.cs`: 실제 AIInventory lifecycle의 메뉴 Reset 대체 경계.

현재 소스에서 그대로 재추출되어 변경된 파일은 `Tools/life-craft-tests/LifeBindingSource.cs`, `Tools/life-inventory-tests/LifeBindingSource.cs`, `Tools/life-inventory-tests/BaseSourceSlices.cs`다. 이 생성 파일을 손으로 바꾸어 정책을 대체하지 않았다. 대상 harness 디렉터리의 `git diff --check`도 exit0이다. stage/commit은 수행하지 않았다.

## 증거 범위

기존 생활의 actual Protocol→SetGoalFrom→AIGoals→life 탐색→실제 skill 입력·구조화 진척 연결과 held/queued/몸 보존, 요리/제작/인벤토리 primitive 회귀가 새 가방 metadata 연결 뒤에도 통과한 결과다. 물리·ECS·DB·요청 큐·네트워크 등은 기존 fixture 경계이며 Unity 게임 소비자와 외부 모델을 실행하지 않았다.

새 가방/생활 메뉴의 양방향 숨김과 인벤토리 메뉴 전체 정책을 이 생활 전용 대체 경계가 입증한다고 주장하지 않는다. 전체 통합 고정판의 실제 메뉴/Loop 연결 및 최종 독립 검수에서 별도로 확인해야 한다. Unity·설치·게임·공식 그래프는 실행하거나 수정하지 않았다.
