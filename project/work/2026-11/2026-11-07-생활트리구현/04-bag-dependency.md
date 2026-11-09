# 생활 트리의 가방장비 의존 후보 수용 조사

## 판단과 적용 범위

**생활 트리의 격리 통합 기반으로 실행 부품을 재사용할 수 있다. 다만 `77484ec` 그대로보다, 그 판의 상자 준비 P2를 수정하고 독립 확인한 `1fc89b9`을 정확히 고정하는 편이 적합하다.** 이는 HEAD의 통합 판단을 위한 권고이며, 이 조사에서는 후보를 병합하거나 수정하지 않았다.

`1fc89b9`의 실행 부품 지적 해소와 새 물건 우선 선택 트리의 수용은 다르다. 기존 가방 후보를 새 트리 완료품으로 취급하지 않는다. 생활 후보에서 재사용할 것은 정상 물품 조작·대상 식별·실제 결과 관측·공통 실행 연결이다. 새 트리와 생활 연결은 해당 최종 후보에서 검증해야 한다. 가방장비 작업의 공식 반영·실게임 재개·사용자 병합승인을 묵시적으로 받은 것으로 주장하지 않는다.

이번 조사는 기존 결과와 필요한 차이/로그 표본만 확인했다. 새 전체 코드 독립검수나 회귀·Unity 재실행은 하지 않았으며 제품·정식 그래프·다른 작업 후보를 변경하지 않았다.

## 확인한 사실: 정확한 코드와 미커밋 상태

| 항목 | 직접 확인 결과 |
|---|---|
| 공식 저장소 | `C:/Dev/CoreKeeperModSDK`, HEAD `685dde4d27dd91a782a14d8ae33fb576160fa578` |
| 요청에서 지정한 후보 | `77484ec7f563f4169600a5b01aa66487cfbbb493` — pending 요청을 취소/재전송하지 않고 관측하도록 변경 |
| 조사 시점 후보 작업트리 | `C:/Dev/CoreKeeperModSDK-bag-common`, branch `bag-equipment-common-20261107`, HEAD `1fc89b93edbee31a9001a174581e32568119efc1` |
| 기반 포함 관계 | `git merge-base HEAD 685dde4`가 공식 SHA와 같다. 후보는 공통 실행 통합을 포함한다. 공식 HEAD에는 아직 가방 후보의 6개 후속 커밋이 포함되지 않는다 |
| `77484ec → 1fc89b9` | `AIBase.cs`, `AIInventory.cs` 두 제품 파일 변경. 보이는·열어 본 상자의 실제 수용 공간을 준비 판단에도 사용 |
| 후보의 제품 소스/Tools | `git status --porcelain -- Assets/AIPlayerSpike/Scripts Tools` 결과 없음. 이 경로의 미커밋 코드가 후보 기능에 몰래 포함된 상태는 발견하지 못함 |
| 후보의 환경/도구 미커밋 | `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset` 수정. `.vsconfig`, `Assets/HeadlessTools/Editor/BagCandidateExport.cs`, `Assets/Plugins/CoreKeeper/` 미추적 |

공식 작업트리에도 기존 Unity 설정·리소스·SDK/로컬 데이터 수정과 미추적 파일이 있다. 이 조사에서는 정리하거나 복사하지 않았다. 통합은 깨끗한 Git 고정판을 기준으로 하고, 위 환경/로컬 파일을 기능 델타에 통째 포함하지 않아야 한다. 단 이 상태 조사는 파일 내용 전체의 무해성을 보증하는 검수가 아니다.

## 확인한 사실: 검증된 범위와 판별 지위

읽은 주 자료는 가방 결과폴더의 [implementation](../2026-11-07-가방장비구현/implementation.md), [validation](../2026-11-07-가방장비구현/validation.md), [feature 검수](../2026-11-07-가방장비구현/review-feature.md), [최종 독립 검수](../2026-11-07-가방장비구현/review-final.md)다. 앞의 두 요약은 `77484ec` 기준이고, 최종 독립 검수는 `1fc89b9`까지 이어져 있다.

| 근거 | 실제 확인 범위 | 확대하면 안 되는 주장 |
|---|---|---|
| `77484ec` 생산 검증 | fast compile 0경고/0오류, Core1201, protocol118, 공통 코드 입구36/36, 실제 AIInventory+최소 World adapter40/0 | 실제 게임 소비자·전체 모델 선택 성공 |
| 과거 feature `bed7ef9` | 독립52건 중2실패: Give 거짓 성공, slot-only Store 상자 준비 누락. 이전 네 지적 수정 확인 | 이 과거 실패를 최신판의 현재 blocker로 그대로 유지하거나, 과거 생산 adapter30 통과를 독립 수용으로 읽기 |
| 최종 독립 `1fc89b9` | compile0/0, 공통 입구36/36, 실제 소스 링크/실제 Base helper 추출 adapter67/0. Core1201/protocol118은 앞선 고정판 실행 후 해당 입력 diff 없음 확인으로 적용 | 모든 항목을 최신판에서 다시 전수 실행했다는 주장 |
| Unity 최종 로그 | 입력 sourceCommit=`77484ec`, `final-unity-exit.json` exitCode0, 로그11817행 `[HeadlessSdk] CompileCheck: ok`, AIPlayerSpike.dll 컴파일/후처리 산출물 | 후속 두 소스가 바뀐 `1fc89b9` 또는 새 생활 후보의 Unity 통과 |

최종 독립 보고서는 이전 손 장비 교체·명시 Store 회귀·핫바 목적지 누락·완료 뒤 역교체, Give 거짓 성공·slot-only 준비 누락이 해소됐다고 확인한다. 정상 Swap/MoveAmount/DropItem, 정확 슬롯·variation·aux·상태, 양쪽 이동량 관측, 같은 목표 객체 receipt, `finish_goal`과 원래 부탁 완료 분리, 지연 요청의 비취소/비재전송이 재사용 근거다.

최소 World/DB/큐·길찾기는 대역이며 결과 슬롯 변화는 fixture에서 수동 공급한다. 따라서67/0은 Unity 게임 소비자·UI·드롭 타이밍·실제 수령 성공의 증거가 아니다. 영구 물품 UUID를 새로 제공한 것도 아니다.

### `77484ec`에 남아 있던 P2와 해소 판

최종 독립 로그 `C:/Dev/CoreKeeperModSDK-bag-review-final/review-repro/77484ec-adapter.log`에는 **64건 중1실패**가 보존돼 있다. 자기 상자8칸이 모두 Wood9999, free0, 예비 Chest를 가진 상태에서 정확 이동 후보는0인데 과거 helper가 중첩 가능으로 판단해 새 상자 준비를 억제하는 사례다.

`1fc89b9`은 `AIInventory.HasStoreRoom`이 기존 Destination의 실제 슬롯 제한을 사용하고, `AIBase.MayStoreKnownChest`가 이를 보이는·열어 본 상자의 준비 판단에 반영한다. 직접 Git diff에서 visible 후보 계산·화면 밖 경로·새 상자 배치 차단의 세 호출부 교체를 확인했다. 독립 `final-adapter.log`의67/0 및 생산 `capacity-adapter.log`의43/0을 읽었다. full9999 불가,9998 잔량1 가능, one-item 제한, 화면 밖 마지막 관측의 재확인 가능성이 검수됐으며 전체 Base.Candidates/실게임 배치 실행을 증명한 것은 아니다.

## 확인한 사실: graph-stage와 수용 상태

[stage-state.json](../2026-11-07-가방장비구현/graph-stage/stage-state.json)은 다음 상태다.

- `codeExtractionCommit=1fc89b93edbee31a9001a174581e32568119efc1`
- `currentProductGraphCommit=77484ec7f563f4169600a5b01aa66487cfbbb493`
- `finalAcceptance=false`, `canonicalWritten=false`
- 새 승인 선택 트리의 최종 SHA·근거 대기

[final-pin.json](../2026-11-07-가방장비구현/graph-stage/final-pin.json)은1fc89b9/공식685dde4/101코드 파일을 기록한다. 현재 command-results는 그 판의 추출·code build·verify·determinism 네 단계 exit0이며, 이전 query-results/evidence-plan은77484ec의 제품 연결과 당시 Unity·독립 검수 대기 상태를 보존한다. **코드 추출판과 현재 제품 그래프판이 다른 중간 상태**이므로 완결된 최신 그래프 패키지가 아니다.

가방 graph-stage 안의 `Review/independent-review.md` 두 개를 열어 확인한 결과2026-11-05 개편·실화면 검수 사본이다. 이 조사에서 가방 델타/새 트리를 최종 수용한 별도 graph-stage 독립 보고서는 찾지 못했다. 기존 사본의 PASS나 `validation-review.json` 이름, 명령 exit0을 새 가방 그래프의 독립 수용으로 승격하지 않는다. 실행 부품의 독립 수용 근거는 `review-final.md`이고, 그 보고서 역시 기존 평면 선택지의 최종 수용/패키징 및 새 트리 검수는 보류라고 명시한다.

[패치 적용 경계](../2026-11-07-가방장비구현/graph-stage/패치-적용경계.md)는 공식 쓰기 금지, 생성 JSON 직접 패치 금지, history/context/work 통째 역복사 금지, 병합 후 새 공식 SHA의 원본 스냅샷/재생성 필요를 명시한다. 그래프 staging 미완결은 격리 코드 재사용의 기술적 선행 blocker는 아니지만 공식 그래프 반영 완료를 주장할 수 없게 한다.

## 해석: 생활 후보에서의 수용 조건과 남은 blocker

**‘새 가방 선택 트리’의 뜻 보충:** 이 표현은 `review-final.md`가 기록한 후속 요구사항(인벤토리→물건→조작→슬롯/수량/대상, 뒤로/전체 취소)을 가리킨다. `1fc89b9`에 이미 계층 탐색이 있다는 뜻이 아니다. 해당 고정판의 `AIInventory.Candidates`는 완성된 `AIInventoryMove`를 `kind="inventory"` 후보로 직접 만들고 일반 후보는8개로 추린다(`AIInventory.cs:142~204`). `AICandidate.inventory`는 이 실행 payload이며 nav/back/root/page 상태나 탐색 경로가 아니다. 모델 입력은 여전히 `{value,description}` 목록(`AIBrain.Loop.cs:1349`), 답변은 `DAnswer.choice`이고, 선택은 `AIActions.cs:41`의 `new InventorySkill(c.inventory)`로 이어진다. `AIInventory.cs`의 `_nav/Navigate`는 상자/수령자에게 걷는 길찾기이며 `AIActions`의 root는 뿌리 캐기다. 따라서 이 SHA와 생활 트리의 **구현된 탐색 엔진끼리 중복은 없다**. 향후 가방 담당도 별도 트리를 만들 수 있는 계획상 경계만 있으므로, 공통 탐색을 한쪽이 소유하고 기존 InventoryMove/Stale/Skill을 실행 leaf로 재사용하면 된다.

1. **기반 고정.** 생활의 [방향 판단](02-direction-decision.md)은77484ec를 명시한다. 이를 유지하면 위 P2가 남는다.1fc89b9 전체를 새 기반으로 고정하거나 해당 수정만 포함한 통합 SHA를 명확히 남기는 선택을 HEAD가 해야 한다. 이 보고서는 자체로 합의를 변경하지 않는다.
2. **새 트리와 기존 부품의 경계.** 새 가방 물건 우선 메뉴는 별도 작업의 최종 검수 대기다. 생활이 이전 flat 후보·정상 실행 부품을 재사용하는 것은 가능하지만, 새 가방 메뉴 완성에 기대거나 타담당의 트리를 복제한 결과를 그 작업의 완료로 수용하지 않는다. 공유 진입점/계약은 이번 통합 명세에서 소유자를 정해 연결한다.
3. **최종 통합 검증.**1fc89b9의 오프라인 지적 해소가 새 생활 연결을 대신 검증하지 않는다. 기존 증거는 재사용하고 생활 변경과 실제 연결 경계에 필요한 대표 오프라인 흐름·회귀·Unity·독립 검수를 최종 통합판에서 수행하면 된다. 새 기반만을 이유로 과거 전체 시험을 반복할 필요는 없다.1fc89b9 단독 Unity 증거가 없다는 점은 기록하되, 승인된 최종 생활 후보 Unity로 해당 차이까지 검증할 수 있다.
4. **공식 수용/실게임.** 실게임의 자연어→상위 계획→모델 선택→실제 입력·소비→착용/수량 변화→다음 판단은 미관측이다. 이는 현재 격리 오프라인 구현을 시작하지 못할 이유는 아니지만 실게임 QA·배포 완료 판정의 blocker다.

## 승인과 보류의 근거 구별

- [가방 구현 합의](../2026-11-07-가방장비구현/agreement.md)는 사용자의 구현 진행 승인을 기록하며 격리 구현·오프라인/독립 검수·Unity 컴파일을 허용한다. 그 문서가 설치·실게임·자동조작·기존 프로세스 종료까지 허용하지는 않는다.
- [runtime-plan](../2026-11-07-가방장비구현/runtime-plan.md)은 기존 실게임 보류와 flat판 설치/패키징/추가 Unity 보류를 명시한다. 실게임은 보류 해제와 설치 후보 확인 뒤에 진행하며 새 트리 고정판으로 계획을 갱신하도록 돼 있다.
- 읽은 가방 합의/결과 묶음에서는 후보를 공식에 병합하라는 별도 사용자 승인 근거를 확인하지 못했다. 이를 특정한 새 사용자 승인 규칙이 이미 합의됐다고 만들지는 않는다. 이번 요청은 생활 **격리 기반 수용 조사**이며 공식 병합은 그 승인 범위에 포함되지 않는다. 추후 공식 반영은 원래 HEAD가 실제 사용자 승인·프로젝트 절차와 최종 후보를 확인해 판단할 일이다.
- [생활 구현 합의](00-agreement.md)는 격리 구현과 오프라인/Unity 검증, 기존 설치·실게임 보류 유지를 정한다. 따라서 가방 flat판 보류를 가방 기능 재사용 자체의 전면 금지나 승인된 생활 후보 검증의 금지로 확대해석하지 않는다.

기존 요약의 `validation.md` Unity “실행 중”은 뒤에 생긴 exit0/CompileCheck ok로 업데이트된 사실과 구별했다. 반대로77484ec의 성공을1fc89b9이나 새 선택 트리로 옮기지 않았다. 이후 후보가 더 진행되면 이 보고서의 조사 시점 SHA와 새 SHA를 대조해야 한다.
