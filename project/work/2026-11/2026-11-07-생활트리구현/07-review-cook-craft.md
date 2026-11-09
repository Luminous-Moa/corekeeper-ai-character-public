# 요리·제작 독립 정적 검수

검수 기준은 합의/방향/명세 및 두 승인 문서(`00`~`03`), 기반 `1fc89b93edbee31a9001a174581e32568119efc1`, 고정 후보 `24c61c246053237784c057cf97282b9ca0c5d564`이다. `C:/Dev/AGENTS.md`와 HEAD Core `start-job/SKILL.md`를 먼저 확인했다. 움직이는 작업트리의 미커밋 수정은 판정 근거로 사용하지 않았고 제품 코드는 수정하지 않았다.

이 보고서는 첫 단위의 `AICook/CookSkill`, `AICraft/CraftSkill`, 연결된 `ExactCandidate`와 제작 `WantedItems` 경계만 검수한다. 전체 생활 기능·최종 통합·Unity 통과 판정은 하지 않는다.

## 중요 오류

### R3 · P2 — 옛 제작 재료 검사가 다중 출력 레시피의 직접 후보를 막음

- 위치: 고정 후보 `Assets/AIPlayerSpike/Scripts/AIBrain.Prep.cs:76`, `:143`. 계산 근거는 `AICraft.cs:226`의 `Materials`, 실제 실행 계산은 `AICraft.cs:144`~`:157`이다.
- 조건: 현재 craft 목표의 남은 품목 수가 3개, 선택 레시피의 한 회 기본 출력이 2개, 한 회 비용이 Wood 1개, 가방 Wood가 2개인 상태. 현재 관측되는 수동 제작대가 해당 레시피를 제공하고 `where`까지 지정한다.
- 현상: `WantedItems`는 `tg.n=3`을 `Materials`에 넘겨 Wood 3개 필요로 계산한다. `HasMaterials(m)`이 false이므로 `ExactCandidate` 호출 자체에 도달하지 않고 Wood 1개 부족을 보고하거나 채집/꺼내기 준비 대상으로 삼는다. 같은 선택을 트리 말단의 `ExactCandidate`로 생성하면 2회 제작, 기본 출력 4개, Wood 비용 2개로 정상 후보가 나온다.
- 영향: 이미 정확히 지정한 실행이 가능한데도 직접 접근이 사라지고 불필요한 자원 준비가 노출된다. 명세 6의 직접/트리 동일 생산 입구와 7.3의 실제 recipe 출력·비용 계약을 만족하지 않는다. 할인 비용이 기존 기본 비용보다 작아지는 경우에도 같은 선행 gate의 영향이 가능하지만 이 추가 조건은 실행하지 않았다.
- 수정 방향: 관측 가능한 제작대의 직접 후보 허용 여부를 옛 `Materials` 결과로 먼저 차단하지 않고 실제 `ExactCandidate`/`ReadRecipe` 결과에 맡긴다. 준비 설명의 비용도 실제 선택 레시피와 일치시킨다. 새 소비 정책은 필요하지 않다.

### R3의 직접 실행 증거

고정 커밋의 생산 `AICraft.cs`, `AIInventory.cs`, Core 계약 세 파일과 고정 craft-test 경계를 임시 경로 `C:/Users/<USER>/AppData/Local/Temp/life-cook-craft-review-24c61c2`로 복사해 최소 표본을 실행했다. 기존 Setup의 Wood를 2개로 바꾸고 Torch 기본 출력 2/요청 3 조건에서 생산 `ExactCandidate`, `Materials`, `HasMaterials`를 직접 호출했다. 어댑터의 DB 기본 비용과 가방 조회만 실제 표본 상태를 반환하도록 연결했으며 생산 정책은 복사 재구현하지 않았다.

재현 자료를 [review-cook-craft-boundary/README.md](review-cook-craft-boundary/README.md)에 보존했다. [Program.cs](review-cook-craft-boundary/Program.cs), [실제 실행 로그](review-cook-craft-boundary/run.log), [종료 코드](review-cook-craft-boundary/exit.txt), [고정 소스 Git blob ID](review-cook-craft-boundary/fixed-git-blobs.txt)를 함께 남겼다. 증거 경로로 복사한 뒤 같은 최소 표본을 다시 실행해 동일 결과를 확인했다.

명령: `dotnet run --project review-cook-craft-boundary/Review.csproj -c Release --nologo`

결과(exit 0):

```text
R3 exact=True, calls=2, expected=4, actualCost=2, legacyNeed=3, bag=2, legacyGate=False
```

`WantedItems` 전체나 실제 `BuildCandidates`를 이 표본에서 실행한 것은 아니다. 위 실제 생산 함수 결과와 고정 `WantedItems`의 선행 조건·호출 순서를 대조해 직접 후보 누락을 확인했다. 기존 craft 표본은 Wood 20개를 사용하고 `AIBrain.Prep.cs`를 링크하지 않아 이 조건을 다루지 않는다.

## 확인한 구현 경계

- 요리 `LifeEntries`와 직접 후보는 같은 `ExactCandidate`를 사용한다. 첫 재료 축에서는 전체 조합 예측을 만들지 않고 상대 재료 페이지에서 선택 첫 재료와 예측한다. `BestPair`는 최종 허용 필터가 아니며 둘째 자기 솥도 선택 가능하다.
- 요리 payload에는 솥 entity/version, 순서 있는 재료 ID/variation/슬롯, 투입량, 입력·출력 stamp가 남는다. `AICook.Stale`와 `CookSkill.Start`, 첫 요청 직전 Tick에서 자기 솥·현재 관측·위치·슬롯 상태를 검증한다. 화면 밖 솥은 기억 설명과 이동 후보를 만들고 현재 버퍼 실행으로 취급하지 않는다.
- put은 첫 입력의 가방 감소와 솥 입력 관측량을 확인한 다음 그 확인량으로만 둘째 입력을 요청한다. 둘째 입력은 중복 발송하지 않으며 입력 잔량과 해당 출력 증가를 함께 반영한다. put/fill의 확인량과 가방 수령량은 분리된다. take는 지정 Amount를 정상 이동 요청에 전달하고 정확한 출력 ID/variation의 main inventory 증가만 수령량으로 기록한다. 커서만 늘면 수령 완료로 끝내지 않는다.
- 제작은 실제 관측/기억한 제작대와 손 레시피를 좁히고 자동 가공 시설을 수동 제작 말단에서 제외한다. 실행 payload에는 station entity/version과 recipe entry·비용·재료 stamp·출력 단위·요청 회수가 남는다. `ReadRecipe` 및 Start/Tick 재검증은 현재 실제 recipe와 재료, 커서 공간을 확인한다.
- 제작 결과는 main inventory와 커서가 분리된다. 비용 감소가 확인되지 않으면 우연한 출력 증가를 작업 성공으로 확정하지 않으며, 정상 Stow 요청 뒤 커서가 남으면 확보 완료로 종료하지 않는다. 해당 비용/출력 수식은 로컬 decompile `Pug.Other/Inventory/InventoryUtility.cs:1673`, `:1774`, `:1809` 및 `InventoryUpdateSystem.cs:411` 이후의 정상 Craft 소비 경계와 대조했다.
- 생활 binding/다른 목표 진척의 공통 문제는 상위 검수의 별도 finding 범위다. 이 보고서에서 같은 문제를 중복 번호로 등록하지 않았다.

## 판정에서 제외한 경계와 미확인

- `second=#v...` 변형 지정이 공통 Matches에서 무시될 수 있다는 초기 의심은 실제 상위 입력에서 `AIPlanner.StepForm`→`AIGoals.TryItem`의 enum 검사로 거절되므로 finding에서 제외했다.
- 동일 station Entity를 유지한 채 Transform을 2→3칸으로 바꾸면 `current.Where`가 달라져도 제작 Stale가 null인 최소 표본은 확인했다. 그러나 실제 게임에서 같은 entity를 유지하는 해당 변경이 발생한다는 근거를 확보하지 못했으므로 중요 오류로 확대하지 않았다. 솥은 별도 PotKey 비교를 한다.
- 기존 요리/제작 회귀 전체를 반복 실행하지 않았다. 고정 테스트 코드를 읽어 검증 의도·경계를 확인했으며 그 소스 열람을 이번 독립 실행 성공으로 표기하지 않았다.
- 어댑터 수량/상태는 수동 공급한 값이며 실제 게임 소비자, 모델/API 선택, 네트워크 처리, 자동 요리 생산, 실제 커서 정리·가방 수령의 실게임 증거가 아니다. 최종 Unity와 통합은 진행 중인 별도 범위이며 설치/게임 실행은 하지 않았다.

본 범위는 중요 오류 R3 수정 뒤 직접 영향을 재검수해야 한다. 전체 통과를 선언하지 않는다.
## 수정 후 재검수 — c4a0f5e

대상은 고정 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`이며, 이전 후보 `24c61c2` 이후 요리 분리 스택·재고 설명과 제작 R3 수정의 직접 영향만 다시 보았다. 이전 finding과 최초 재현은 당시 후보의 근거로 위에 보존한다.

**R3는 이 고정판에서 해소됐다.** `AIBrain.Prep.cs:76` 이후 실제 `ExactCandidate` 생성이 옛 `Materials` 추정보다 먼저 실행되고, 후보가 생성되면 legacy 재료 경로를 건너뛴다. 실제 recipe 비용·출력 메타데이터는 재료 부족으로 실행할 수 없는 경우에도 `ReadRecipe`의 별도 반환값으로 관측되며, Prep이 이 정보를 사용해 부족량을 계산한다. 실행 후보는 여전히 ReadRecipe의 bool 성공을 요구하므로 비용 메타데이터만 확보됐다고 실행 가능으로 노출하지 않는다.

검증 코드의 생산 연결도 확인했다. 고정 `Tools/life-craft-tests/LifeCraftTests.csproj`는 **전체 생산 `AIBrain.Prep.cs`를 직접 링크**한다. `TestPrep`은 `WantedItems`를 부르는 얇은 wrapper다. `ExtractLifeBinding.ps1`은 실제 `AILife.cs`의 Entries 이전 prefix를 추출하며, 고정 `LifeBindingSource.cs`가 실제 생산 prefix와 줄바꿈 정규화 후 정확히 같은 것을 독립 비교했다. Prep/후보/비용 정책의 테스트용 재구현이 아니다.

독립 실행은 수정 직접 영향의 최소 표본만 수행했다. [재현 자료](review-cook-craft-boundary-c4a0f5e/README.md), [제작 실제 로그](review-cook-craft-boundary-c4a0f5e/craft/run.log), [요리 실제 로그](review-cook-craft-boundary-c4a0f5e/cook/run.log), [고정 blob ID](review-cook-craft-boundary-c4a0f5e/fixed-git-blobs.txt)를 남겼다.

- 실제 전체 `WantedItems`: Wood2/목표3/출력2에서 직접·트리 key/station/요청2회/game amount2/예상4/비용2가 같고 잘못된 추가 재료 준비가 없음. Wood1에서는 실행 후보가 없고 실제 부족1을 보고함. **2개 검사 통과, 실패0, exit0.**
- 실제 `AICook/CookSkill`: 같은 재료 1+1 분리 스택에서 서로 다른 정확한 source를 선택하고, 정상 두 이동 요청/소비 후 투입1·가방 음식 확보0을 확인함. 2+2+2+2 분리 스택은 전체 재고 가능4와 이번 고정 source 묶음2, 부족0을 구별함. **4개 검사 통과, 실패0, exit0.**

요리 변경은 전체 재고 가능량과 실제 선택한 source의 실행량을 분리하고, 같은 재료가 두 스택에 나뉘어 있을 때 필요한 두 source를 고정한다. ID/variation·솥·슬롯 stamp 재검증과 순차 투입/가방 수령 분리 경계는 유지된다. `CanCook`에 추가한 부분 입력 솥 확인도 `PotsInView`의 현재 관측 범위를 사용하며 화면 밖 현재 buffer 읽기로 확대하지 않는다.

구현자 로그의 craft19/0, cook51/0은 소스와 함께 열람했으나 이번 독립 전체 회귀로 재표기하지 않는다. 이번 수정 영향 범위에서 새 blocker/중요 오류는 확인하지 않았다. 실제 게임/모델/API·최종 통합·최종 Unity 결과는 이 재검수로 확인한 범위가 아니며 전체 통과는 선언하지 않는다.