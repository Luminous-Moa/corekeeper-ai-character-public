# 시설 생활 행동: 그래프 조회와 현행 대조

## 조사 기준과 한계

본 저장소 `C:/Dev/CoreKeeperModSDK`의 조사 시점 HEAD는 `685dde4d27dd91a782a14d8ae33fb576160fa578`이다. 공통 실행이 이미 본 `ai-character`에 통합되어 있다. 앞선 세션 요약의 “공통 실행 진행 중”과 구분한다. 제품 코드·설치·게임 상태는 이번 조사에서 변경하지 않았다. 저장소에는 Unity 설정·로컬 로그 등 기존 미커밋 파일이 있으며 이를 수정하거나 정리하지 않았다. 저장소 아래 AGENTS.md는 `rg --files -g AGENTS.md`에서 발견되지 않았고 상위 `C:/Dev/AGENTS.md`를 적용했다.

[프로젝트 그래프](../../지식그래프/README.md)는 제작 의도부터 합의 원문을 찾는 데 사용했다. 일반 안내에 과거 코드 기준이 함께 남아 있으므로 실행 구현의 최신성은 실제 HEAD와 코드를 별도로 확인했다. [게임 행동 그래프 G](../../게임행동-그래프/README.md)는 기존 전체추출·후속보강의 고정판이며, 현재 계속 수집되는 완결 기록은 [별도 staging](../2026-11-07-행동결과완결/integration/README.md)에 있다. 정본을 재생성하거나 진행 중 통합을 변경하지 않았다.

G의 overview는 `completeGameActionEnumeration=false`, `runtimeTested=false`이다. 전체 source 처리나 원본 주장 수를 실행 가능한 봇 행동 수로 읽지 않는다. `target`이 비어도 불가능으로 해석하지 않았다. 예컨대 작물8005 역관계는 정본의 공백이며 후속 농사팔 근거와 별개이다.

## 프로젝트 그래프에서 실제 확인한 의도와 합의

`work/지식그래프`에서 `node tools/product.mjs intent`, `intent P.life`, `intent P.original-request`, `find 제작|수리|농사|요리|낚시|거래|생활`, `source P.life|P.original-request|P.execution|P.knowledge|R.missing`, `user UA-03`을 실행했다.

| 조회 | 실제 내용 | 이번 계획에 주는 조건 |
|---|---|---|
| P.experience / P.life | 자연어로 소통하며 함께 생활하는 동료. 부탁이 없어도 생활·탐험을 이어 가며 요청이 있으면 돕는다 | 생활을 별도 자동 모드로 추가하는 방향보다 기존 동료의 계획·행동을 넓힌다 |
| P.execution | LLM이 목적지·대상·수량·준비·순서·대안을 정하고 코드는 지정 행동의 입력과 결과 확인을 담당한다 | 시설·재료·수량 선택을 관측 근거와 함께 AI가 할 수 있어야 한다 |
| P.original-request / P.valid-action | 중간 수량 충족과 원래 부탁 완료 구분. 현장 전환은 Decisions, 중요한 계획 변경·부탁 종료는 위쪽 LLM | 투입 성공·시설 도착·한 접시 완성을 원래 부탁 종료와 혼동하지 않는다 |
| P.knowledge | 캐릭터에게 열린 제작법·행동 조건 | 개발용 전체 추출을 캐릭터의 전지적 지식으로 제공하지 않는다 |
| R.missing | 자동/특수 상황에서만 가능한 실행과 일반 지시가 없는 경우를 구분 | 기존 실행을 재사용하면서 모델이 부를 수 있는 선택을 연결한다 |
| UA-03 | “필요한 물건 제작을 맡기기”, legacy F.craft/F.goal-cmd. user 조회는 연결 출력요소/기능 없음과 정상 AI 흐름 미관측을 표시 | 프로젝트 그래프의 이 연결 빈칸은 실제 CraftSkill 부재가 아니다. 현행 코드와 실기 증거를 연결하는 계획이 필요하다 |

합의 원문은 [목표 계층](../2026-11-07-목표계층과-제약자율조정.md:7), [계획 주도권](../2026-11-07-동료형AI-계획주도권.md:7), [비전투 연속 판단](../2026-11-07-비전투-연속판단.md:11), [행동 취소 판단 주체](../2026-11-07-행동취소-판단주체.md:3)에서 확인했다. P.constraint의 게임 내 활동 제약 조정 합의는 개발 승인 규칙으로 사용하지 않는다. 세부 자율성·목표 규칙은 미정 항목을 그대로 유지한다.

## 게임 행동 그래프 실제 조회

고정 Python은 `C:/Users/<USER>/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`이며 `-X utf8`로 G의 `graph.py`를 실행했다. `find Workbench|Cooking|Repair|Upgrade|Fishing|Merchant|Furnace|Salvage|RootPlant|FarmArm` 및 `target 4025|4046|4004|8005|100`으로 정확한 source와 claim을 찾았다. 선택한 claim은 `show`, 이어서 `source`를 실제 실행했다. 원문 결과는 [queries](queries/)에 보존했다. 초기 탐색의 틀린 claim 조합은 target 결과에서 정확한 ID를 찾아 재조회했으며, 아래 표는 성공한 결과만 사용한다.

| 대표 대상과 claim | 확인한 조건·결과 | 구현 계획의 의미 |
|---|---|---|
| WoodenWorkBench 4003:0, `base:D::CAB-2ebcba97367158c9b08f790c25d642c9:2727462929140199568#recipe_ui_craft` | 선택 레시피·prerequisite·권한·재료·출력 공간과 동적 비용/출력 효과. UI→CraftingHandler/InventoryHandler→서버 소비자. 재료 소모와 출력은 조건부 | 단순 ID 레시피 목록과 기본 재료만으로 모든 제작을 가능하다고 판단하지 않는다 |
| CookingPot 4013:0, `base:D::CAB-2ebcba97367158c9b08f790c25d642c9:-2106314794829400105#cooking` | 허용 tag24 두 입력, 수량, primary prefab·결과 variation·출력 용량·timer. 자동 소비자가 재확인 후 소모·생산 | 두 재료 투입과 실제 완성/수령을 단계로 분리. 순서·variation을 보존 |
| Furnace 4004:0, `supp:D::CAB-2ebcba97367158c9b08f790c25d642c9:7493579351412767700#process_recipe_selection` | 본문보다 `rules`에서 공통규칙 전개. 레시피 활성화는 재료를 slot0으로 옮기는 준비. 생산은 별도 자동 timer 소비자 | 일반 즉시 Craft를 용광로에 그대로 적용해 완료라고 판단하지 않는다 |
| SalvageAndRepairStation 4025:0, initial `#0` | 수리·분해 화면 열림. 실제 수리와 별도 | 수리대 열기 성공은 내구도 회복 성공이 아니다 |
| UpgradeForge 4046:0, `base:C::...:-4311004146624401223#use_upgrade_forge` | 유효 UI·선택 가능한 장비; 실제 upgrade 요청·소비자 별도 | 수리, 내구 강화, 레벨 업그레이드를 별도 행동으로 구별 |
| WoodFishingRod 50:0, initial `...:-4481218627703392936#0` | 파손·마우스 물건·cooldown 및 secondary input 후 fishing state 시작 | 시작만으로 물고기 확보 능력이라고 보지 않는다 |
| CavelingMerchant 3900:0, initial `...:-8881191158473362106#0` | onUseActions event와 상인 화면 열림 | 판매/구매 후 품목과 동전의 실제 증감은 별도 |
| FishingNet 4054:0, `supp:D::...:2382209455901869173#automatic_fishing_result` | Critter 미끼·timer·지역·난수. 성공 시 미끼 슬롯 전체를 첫 전리품 1개로 대체 | 낚싯대 조작과 어망 자동 가공은 서로 다른 실행 방식. 미끼 한 개만 감소한다고 일반화하지 않는다 |

### 검수 수용된 완결 후속 근거

staging `additional.py --index stage-02.json --output-dir additional-evidence-v2/replay-1 target ...` 및 `show/source`를 실제 실행했다. [수리 receipt](../2026-11-07-행동결과완결/integration/receipts/C-repair-v2.json), [업그레이드 receipt](../2026-11-07-행동결과완결/integration/receipts/C-upgrade-v2.json), [낚시 receipt](../2026-11-07-행동결과완결/integration/receipts/B-fishing-v2.json)는 독립 PASS와 수용 판단·artifact SHA를 연결한다. source 문서의 생산 당시 `review_pending` 문자열과 현재 receipt의 검수 상태를 구분한다. 개별 묶음의 수용을 staging 전체 최종 검수 완료로 확대하지 않는다.

- **수리**: 362 exact source, base/supp 주장724의 조건부 수리. 표본 8555:0의 `completion:C:repair-item-conditional-v1/application/CAB-2ebcba97367158c9b08f790c25d642c9:-6961237460797322265`에서 source별 primary 선택과 수리 조건 확인. [공통 rule 원문](../2026-11-07-행동결과완결/full/C/repair-rule-v2.json)은 손상·slot ID·LevelEntitiesBuffer/DurabilityCD·권한·재료를 검사하고 실제 amount를 내구도로 변경하는 소비자를 추적한다. 재료 소비 부족 오류 뒤에도 SetAmount로 가는 게임 코드 분기가 있으므로 원자적 성공을 가정하지 않고 실제 재료/내구도 전후를 관측한다.
- **레벨 업그레이드**: 523 source의 조건부 주장. 같은 8555:0 표본은 기본 유효 레벨6→7, 비용표 항목7(1002×10, 2021×132)을 보여 준다. 이는 해당 source 표본이며 모든 장비의 고정 비용이 아니다. variation>0이면 variation, 아니면 prefab 레벨에서 유효 레벨을 정하고 최대20에서 거절하는 [공통 rule](../2026-11-07-행동결과완결/full/C/upgrade-rule-v2.json)을 확인했다.
- **낚싯대**: `completion:B:fishing-50-0`에서 입력→물/지형검사→입질·미니게임→미끼 실제 소모→지연 전리품 생성→가방 또는 넘친 물건 드롭까지 추적된다. 미끼 소모 실패면 전리품 생성이 진행되지 않는 분기와 특정 보스 미끼 예외도 보존한다. 실제 물고기는 지역·스킬·난수에 의존한다.
- **판매**: `completion:terra:merchant-3900-sell-5013-0`에서 3900 상인→5013 Geode 판매 슬롯 이동→SellAll→TrySell→TryConsume→2021 AncientCoin 생성/드롭까지 추적된다. 이 표본 원본 가격33은 동적 보정 없는 해당 경로의 값이다. 판매 가능·금지 대상과 slot/권한·실제 소비를 구분한다. 이 표본을 구매 완결의 증거로 확대하지 않는다.
- **농사**: 정본의 직접 행위와 자동 농사팔의 행위주체를 구분한다. RobotFarmArm 6658의 variation0~3에는 수확·이송·씨앗 심기 근거가 있다. AI가 손으로 경작할 수 있다는 것과 농사팔 자동화 설비를 운영할 수 있다는 것은 별도이다. 후속 성장38/뿌리3 등의 통합은 수집·검수 상태를 확인하고 구현 때 고정할 자료다.

## 실제 모드 코드와의 대조

코드 루트는 `C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts`이다. 목표 등록→후보 생산→몸 실행과 결과 확인의 연결을 확인했다. 단순 검색 부재만으로 전체 미구현을 단정하지 않았다.

| 생활 묶음 | 지금 재사용할 것 | 확인된 부족과 경계 |
|---|---|---|
| 일반 제작 | `AICraft.cs` StationsInView/HandRecipes/Materials/QueueCraft, CraftSkill. `AIBrain.Prep.cs` WantedItems, 기억한 제작대 이동·시설 놓기·재료 준비 | 가까운 첫 제작대/기본 재료와 count 계산이 중심. recipe prerequisite·출력 배수·효과, 정확한 대상/시설 선택을 새 규칙과 대조해야 함. CraftSkill 492~498은 2초 뒤 커서 잔류를 표시하면서 Done 가능하므로 “제작됨”과 “가방 수령 완료”를 나눠야 함 |
| 자동 가공/제련 | 기존 시설 관측·이동·인벤토리 요청 기반 | 독립 process 목표/단계와 자동 생산 후 수령의 일반 연결을 확인하지 못함. generic Craft 경로가 자동 가공을 완결한다고 보장할 수 없음 |
| 요리 | `AICook.cs` Predict/BestPair, 자기 솥 기억·상태 관측, 투입/보충/수령 CookSkill, 기다리기와 화면 밖 솥 복귀 | BestPair(215~232)는 생식 대비 배고픔 이득이 있는 가장 나은 1짝만 후보에 올림. Candidates(402~455)는 시야의 첫 자기 솥과 그 짝을 고름. 선택 뒤 임의 교체로 확인한 것은 아니며, 선택 전에 대안이 좁아짐. 목적 버프·지정 재료·지정 수량·솥 비교가 부족. 현재 cook goal은 item/count 없음 |
| 농사 | `AIFarm.cs` 자기 밭·씨앗·괭이·물 관측, 일구기/심기/물주기/수확, 자기 상자 씨앗 꺼내기 | 596~601은 `s.seeds[0]`을 선택해 최대6칸 묶음으로 심음. 지금 farm goal은 item/count 없음. 모델이 씨앗·칸·범위/수량을 지시하는 연결과 비식용 종자 범위 대조 필요. “자기 밭 관리” 기존 권한은 유지 |
| 수리/내구 강화/레벨 업그레이드 | QueueCraft/QueueInventoryChange와 실제 장비 슬롯 관측·선택 계약을 재사용 후보로 둠 | Core/AIGoalKinds.cs에는 수리·강화 목표가 없고 AIBody 실행 분기에도 해당 일반 실행이 없음. 기지 벽 edgerepair와 장비 수리는 다른 능력. 장비 identity·내구도·레벨은 가방장비 담당과 먼저 맞춰야 함 |
| 낚시/거래 | 이동·손에 쥐기·입력·인벤토리 변화 관측의 기반 | GoalKind/일반 후보·몸 실행의 닫힌 흐름은 확인되지 않음. 낚싯대 상태/미니게임, 상인 상품/재고·가격·슬롯 소비자의 런타임 연결 조사부터 필요 |

현재 `Core/AIGoalKinds.cs:5,40~44`는 Craft/Store/Home/Farm/Cook 등을 등록하며 수리·강화·낚시·거래는 등록하지 않는다. `Tools/ai-bridge/brief.md`도 farm/cook에 item/count를 쓰지 않고 현재 고정 처리 설명을 제공하므로, 후보만 늘려서는 자연어 부탁→위쪽 계획→현장 선택이 끝까지 연결되지 않는다.

기존 자기 상자·솥·밭 권한은 `brief.md`와 실행 코드에 남아 있다. 공용/호스트 물건 접근 확대는 시설 행동 보완으로 묵시 승인된 것이 아니다. 장비 유지·일반 회수의 기존 공백은 [행동 누락 조사](../2026-11-06-행동누락조사/03-findings.md)와도 일치한다.

## 최신 공통 실행과 의존 경계

[공통 실행 계약](../2026-11-07-공통실행자료/contract.md)은 685dde4를 기준으로 다음을 제공한다. 시설 모듈이 복제하지 않고 소비한다.

- `AICandidate` 대상 필드와 `AISkill.Start/Tick/Status/Reason/Facts`, 실제 결과 관측.
- `stop_action`: 관측한 현재 실행 회차만 종료, 목표와 원래 부탁 유지.
- `finish_goal`: 현장 AI가 현재 계획 단계를 완료 처리, 원래 부탁 종료는 위쪽 `order=done`.
- `request_plan`: 사실과 이유를 위쪽에 전달, 기다리는 동안 유효한 몸 동작 유지. 중복 요청 병합.
- 수량 달성·시설 조건 충족·후보 없음과 실행 제한시간/진척 없음을 관측으로 전달. 자동 목표 완료/취소 정책을 별도 추가하지 않음.

가방장비는 정확한 아이템 선택·장착·교체·부분 보관/꺼내기/전달을 담당한다. 시설은 어떤 품목/수량을 어디에 투입할지와 시설 작업 결과를 담당하며, 장비/상자 이동 실행을 중복 구현하지 않는 방향을 계획한다. 후보 identity에 선택한 시설·재료/장비·수량이 빠지면 서로 다른 선택이 같은 key로 합쳐질 수 있다. 예를 들어 현재 cook key는 mode+pot index, farm key는 act+첫 타일 중심이므로 대안 확대 시 이 경계를 명세에서 보완해야 한다.

## 미확인

실제 모델이 새 선택지를 얼마나 잘 고르는지, 현재 게임의 시설 입력/권한/재료 보정과 슬롯 전후, 닫힌 흐름의 실게임 성공은 아직 확인하지 않았다. 구매 경로와 내구 강화(reinforce)는 이번 표본의 수리·판매·업그레이드 근거만으로 완료라고 할 수 없다. 각 구현 단위의 조사/검증 조건으로 남긴다. 이 문서는 계획을 위한 코드·정적 근거 대조이며 실기 성능 보고가 아니다.
