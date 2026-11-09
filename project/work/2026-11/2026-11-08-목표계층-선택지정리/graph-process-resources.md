# 행동 판단 그래프: ProcessResources(용광로 등) 가공 사슬 후보

작성 2026-11-09. **후보만 만들었다. 정본에는 게시하지 않았다.** 다른 대화가 정본에 먼저 게시하기로 해서 홀드가 걸렸기 때문이다. 후보 위치는 `work/2026-11/2026-11-08-용광로가공판단보강/`이다. 이 폴더를 아래에서 P로 줄여 쓴다.

## 1. 최종 고정본

| 대상 | SHA-256 |
|---|---|
| `P/production/supplement.json` (기록 본문) | `129db1076fff0c7708076bf029bfee973811f0e9771df85ec2011642ca3818cd` |
| `P/contract.json` (생성 계약) | `2564f9bad5a4224654675f08ce2e7fc143e8866cf3ae4bf2076077564295f0d8` |
| `P/candidate-review-v5/graph.json` | `b93ccc36c028144b0c5498ac85eefab3105a020d9cb676f97947f02dcf1909e6` |
| `P/candidate-review-v5/coverage.json` | `6a9fd584be263c9fd324928d0cb19b0b62e27292ff56c3014f00499318afd0b9` |
| `P/candidate-review-v5/manifest.json` | `340b766886328cafd318769e79fc557e98ef02bb121b200574925d0a7f2d9fe3` |

- 기준판은 현재 정본 260판이다(published graph `a16c7394…`, 복사본 `P/baseline-260/`).
- 후보는 기존 251,477노드·350,373간선을 그대로 보존한다. 여기에 45노드·161간선을 더해 251,522노드·350,534간선이 된다.
- 후보 상태는 `pending_independent_review`이다. 독립 수용 영수증(`review-acceptance.json`)은 아직 만들지 않았다. 게시 단계(rebase 뒤)에서 만든다.

## 2. 처음 전제와 달랐던 점

정본에는 4004 Furnace의 base D 두 행만 있는 것이 아니었다. 11-07 상호작용미결보강에서 이미 다음 기록이 들어와 있었다.
- `supp:D::…#process_recipe_selection`: 활성과 재료 이동을 요약한다.
- `supp:D::…#automatic_recipe_result`: 타이머와 출력을 요약한다.
- `completion:full-d:production:…#automatic_recipe_result`: 레시피별 craftingTime이 들어 있다.

`R.game-topic.craft`도 이 행들에 이미 닿는다. 그래서 이번 작업은 같은 내용을 다시 넣는 것이 아니다. 원본별 분기, 실패 분기, 정정 사항을 판단 단위 4기록으로 보탠다. 기존 행은 고치지 않고 관계로만 이었다.

## 3. 추가한 규칙 4개

모두 `kind=action_completion`, `recordFormat=process_resources_decision_connection`, `runtimeTested=false`, `sourceComplete=false`이다.

| 기록 | action | 묶은 원본(has_completion) | 요지 |
|---|---|---|---|
| `completion:process-resources:recipe-activation-v1` | process_recipe_activation | 3905·4004·4009·4015·4032 | 레시피 클릭이 서버 요청으로 받아들여지는 조건이다. 재료 1종, 가방과 반경 10 안 보관함 최대 20개의 합계, 읽기 전용 월드의 비관리자 제외가 조건이다. 이 단계에서 제품은 생기지 않는다. |
| `completion:process-resources:material-move-v1` | process_material_move | 4004·4009·4015·4032 | 입력·출력 칸의 다른 물건을 플레이어 위치에 떨어뜨린다(가방으로 돌려주지 않음). 그다음 FindAndMoveTo로 재료를 입력 칸 0으로 옮긴다. mod1은 10회분을 옮기지만 서버는 1회분만 확인하므로 부분 이동이 될 수 있다. 칸 상한은 9999이다. variation 0만 옮긴다. |
| `completion:process-resources:craft-timer-v1` | process_craft_timer | 7원본 전부 | 시설 인벤토리에서 CanCraft가 참인 첫 레시피를 고른다(누른 레시피가 아님). 1초 타이머로 시간을 줄인다. 전력이 필요한 시설은 4009·1200이다. 손으로 넣기와 자동 이동기 두 입력 경로를 모두 적었다. |
| `completion:process-resources:output-v1` | process_craft_output | 7원본 전부 | Craft의 소비는 비태그 레시피면 ConsumeObject, 태그 레시피면 ConsumeObjectWithTag(1개)로 갈린다. 출력 칸이 막히면 가공이 멈춘다. 제품은 출력 칸에 남는다. 꺼내는 경로는 플레이어나 이동기이다. |

### 원본별 분기

판정 근거는 `P/production/static-check.json`의 `recipe-material-vs-input-slot` 표이다.

| 원본 | 레시피 클릭 | 재료 자동 이동 | 소비 |
|---|---|---|---|
| 4004 Furnace, 4009 TableSaw, 4015 SmelterKiln, 4032 FuryForge | 성립 | 성립(재료가 입력 칸 acceptsObjectIds에 있음) | ConsumeObject |
| 3905 SantasLittleHelper | 석탄으로 관문 통과 | 석탄(태그 38)이 입력 칸 태그 17/18에 걸려 이동 0. 입력·출력 칸의 다른 물건만 떨어진다. | 태그 17/18 음식 1개. UI에 보이는 석탄 비용과 다르다. |
| 4044 GlassSmelter | 불성립(1025 Glass는 재료 0종인 태그 29 레시피) | 없음 | 태그 29 물건 1개 |
| 1200 EggIncubator | 불성립(hideRecipes=1) | 없음 | ConsumeObject |

플레이어 행동으로 넣을 때는 클릭이나 손 이동을 쓴다. 7원본 모두 AutomatedStorageAuthoring이 있어 자동 이동기 투입·회수 경로도 있다. 이 경로는 범위 밖이고 실게임에서 관측하지 않았다.

### 기존 행과의 관계

기존 노드와 근거는 바이트 그대로 보존했고, verify.py로 확인했다. 새 기록에서 다음 간선을 걸었다. 기존 노드 쪽에는 역방향 `has_process_resources_supplement`가 생긴다.
- `refines`: supp process_recipe_selection(5개), supp automatic_recipe_result(7개), rule 필드
- `reuses_consumer`: full-d production 완결(7개)
- `clarifies_open_statement`: base inventory_transfer(4004·4009·4015·4032·4044)와 supp inventory_transfer_extended(3905·1200)
  - 이 행들에 있던 '활성·타이머·출력은 넣기에서 추론하지 않음'이라는 미결 문구는 그대로 둔다. 그 미결은 timer와 output 기록으로 이어진다.
- `excludes_source_claim`: 4044의 supp process_recipe_selection(클릭 경로 없음), 3905의 같은 행(이동 실패)
- `continues_after`: 활성 → 이동 → 타이머 → 출력

### 정정 사항

- craft-diagnosis.md의 '다른 광석은 가방으로 돌려준다'는 정적 코드와 다르다. `MoveAllToOrDrop(…,0,-1,…)`의 두 반복문(3218, 3229)은 0<-1이라 돌지 않는다. 그래서 3240의 DropItem으로 떨어뜨린다. 실게임 확인 대상이다.
- 기존 rule의 'back to player or drop'도 같은 이유로 떨어뜨림 쪽이다.

## 4. 정적 근거와 실게임의 지위

- 근거 코드는 고정 디컴파일 `work/게임원본-상호작용/code`이다. 그 code-inputs의 Pug.Other.dll과 Pug.Automation.dll은 설치본 11-03과 SHA가 같다.
- authoring 근거는 `raw/components.jsonl`과 `raw/objects.json`이다.
- 모든 기록의 boundary 첫 항목에 '실게임에서 이 사슬의 성공은 아직 관측하지 않았다'를 적었다.

## 5. 검수 경과와 원문 확인 행

| 지적 | 반영 | 원문 확인 행 |
|---|---|---|
| F1 4044 클릭 경로 없음 | 활성·이동 묶음에서 제외, 원본별 조건 추가 | ProcessResourcesCraftingUI.cs:100, CraftingHandler.cs:14-36, InventoryUtility.cs:3080, objects.json /358 |
| F2 3905 재료 거절 | 이동 묶음에서 제외, 결과와 실패에 명시 | InventoryUtility.cs:2368-2455, 3086-3089, components.jsonl:436·28914, objects.json /3095 /3116 /1245 |
| F3 manifest scope 문구 | prepare_framework 치환 순서 수정 | overlay.py:96 |
| 외부 F3 음수 index | 'buffer 없음·index>=길이·DB 없음만 None'으로 좁히고, boundary에 하한 검사 없음 명시 | InventoryUtility.cs:2218(상한만 검사) |
| 외부 F5 진행 초기화 | 첫 CanCraft 레시피가 없어지거나 바뀔 때만 초기화. 같으면 보존 | PugAutomationStartCraftSystem.cs:203-221 |
| 외부 F4 시간 우선순위 | 'instant ? 0 : DB'를 먼저 정한 뒤 양수 override가 덮어씀 | StartCraft 215-219, PugAutomationCraftingSystem.cs:210-214 |
| 음수 index 가드(Finish) | 원문에 있음. 'index<0이면 만들지 않음' 유지 | PugAutomationCraftingSystem.cs:198-201 |
| F4 손으로만 → 이동기 경로 | 플레이어 행동으로 범위를 한정하고 이동기 경로를 조건과 경계에 추가 | PugAutomationSystem.cs:192-208, 2398-2434, InventoryUtility.cs:1562-1574, components.jsonl:55461·48899·45220·20781·16140·29253·658 |
| F6 태그 소비 분기·3905 비용 차이 | output flow를 분기로 나누고 3905 문단 추가 | InventoryUtility.cs:1772-1807, 1611-1672 |

- 이 세션 검수 보고: `P/review/source-report.md`(v1), `P/review/source-report-v2.md`(v3 기준)
- 외부 검수 보고: `work/2026-11/2026-11-08-행동사슬전수보완/review/furnace/`
- 외부 검수 쪽은 v5에서 RF1·RF2가 내용상 해소됐다고 통보했다.

## 6. 자체 검증(v5)

- `python -X utf8 -B production/produce.py`
  - 앵커 61건이 모두 pass했다.
  - 7원본 모집단(craftingType==1이 정확히 7)과 레시피·입력 칸 표 assert가 pass했다.
- `python -X utf8 -B compile_contract.py` → pins 32, 계약 `2564f9ba…`
- `python -X utf8 -B overlay.py --output-dir candidate-review-v5 build` → 위 해시
- 같은 명령으로 별도 폴더에 다시 생성했다. 3파일이 바이트 동일하다(재현성).
- `python -X utf8 -B verify.py candidate-review-v5` → pass
  - 기존 251,477노드와 350,373간선을 보존했다.
  - 새 origin 45건의 해시가 일치했다.
  - 4기록마다 show, source, path(원본에서 2간선) 조회가 통과했다.
  - target 7원본 조회가 통과했다.
  - 출력은 `P/verification-candidate/`에 있다.

### 대표 조회(후보)

| 조회 | 결과 |
|---|---|
| `target 4004` | essentialDecisionEvidence에 4기록 |
| `target 3905` | 활성·타이머·출력 |
| `target 4044` / `target 1200` | 타이머·출력 |
| `show base:D::…7493579351412767700#inventory_transfer` | relations에 `has_process_resources_supplement` → craft-timer-v1, output-v1. 기존 필드는 그대로 |
| `source completion:process-resources:craft-timer-v1` | `production/supplement.json` `/records/2/value`의 원문(해시 일치) |

조회 결과 파일은 `P/verification-candidate/representative-*.json`에 있다. 프로젝트 정상 진입점(`product.mjs behavior …`, `show/source game:<ID>`) 조회는 홀드 때문에 실행하지 않았다. 7절 7~8단계에서 한다.

## 7. 새 정본 위에 얹는 방법(rebase)

다른 대화의 게시가 끝나면 새 `게임행동-그래프/publication.json`의 generator 폴더(이하 R2)와 새 published 해시를 받는다.

1. 기준판을 교체한다.
   - 새 `published/` 3파일을 `P/baseline-<N>/`에 복사한다.
   - 새 `publication.json`을 `P/publication-before.json`에 덮어쓴다.
   - 지금의 baseline-260 사본은 지워도 된다.
2. 생성기를 다시 만든다. `prepare_framework.py`를 다음과 같이 고친다.
   - 원본 폴더 R을 이동수면판단보강에서 R2로 바꾼다.
   - 260을 N으로 바꾼다.
   - 이전 생성기의 recordFormat 집합에 R2의 새 형식이 있으면 `process_resources_decision_connection`과 함께 넣는다.
   - R2의 overlay.py가 이동수면판과 구조가 다르면 치환 대상을 맞춘다.
   - 고친 뒤 실행한다.
   - compile_contract의 baseline 경로(260)와 history-current 경로도 R2 기준으로 바꾼다.
3. 기록을 다시 만든다.
   - `production/produce.py`가 baseline에서 읽는 경로(`baseline-260`)를 바꾸고 실행한다.
   - supplement.json SHA가 `129db107…`로 같으면 기록 본문 검수 판정을 재사용할 수 있다.
   - 다르면 바뀐 sourceEvidence origin만 다시 검수한다. 새 정본이 참조 노드의 origin을 바꾼 경우가 여기에 해당한다.
4. 계약을 만들고 후보를 생성한다.
   - `compile_contract.py` → `overlay.py --output-dir candidate-final-review build` → `verify.py candidate-final-review`
   - verify.py의 baseline 경로도 새 기준판으로 바꾼다.
5. 독립 수용을 받는다.
   - 새 계약 SHA로 `review/source-findings*.json`(status independent_pass, report SHA)을 받는다.
   - `review-acceptance.json`(review path·sha, reviewedContractSha256)을 쓴다.
6. 게시한다.
   - `finalize_reviewed.py` → `install_reviewed.py candidate-final`
   - 이 단계에서 published와 publication.json이 원자적으로 교체된다. 직전판은 `P/published-before-switch/`에 보존된다.
   - `graph.py check`를 실행한다. 출력이 `P/check-published.json`을 덮으므로 따로 보관한다. 그다음 `check_published_inputs.py`를 실행한다.
   - `update_readme.py`의 숫자와 문구 assert를 새 판에 맞춘 뒤 실행한다.
7. 프로젝트를 연결한다.
   - `P/consumer/topics.candidate.mjs`의 두 변경을 `2026-11-08-행동판단프로젝트연계/topics.mjs`에 반영한다. 변경은 2행 주석, 그리고 craft 줄의 4 label(process_recipe_activation, process_material_move, process_craft_timer, process_craft_output)이다.
   - 그 시점의 프로젝트 graph.json과 validation.json, topics.mjs를 `P/consumer/before/`에 다시 복사한다.
   - `node tools/product-build.mjs`를 실행한다.
8. 프로젝트를 확인한다. `node P/consumer/verify.mjs`가 다음을 한 번에 확인한다.
   - check ok
   - `behavior P.life-tree-first-scope` → craft 주제에서 4개 `game:` ID 도달
   - 각 `show/source game:<ID>`의 원문 일치
   - 4004 inventory_transfer에서 새 기록으로 가는 관계
   - `game target 4004`
   - `product-build --check` byte-identical

## 8. 공유 파일에 쓴 것과 되돌림

| 파일 | 한 일 | 현재 |
|---|---|---|
| `2026-11-08-행동판단프로젝트연계/topics.mjs` | 홀드 전에 craft label 4개와 주석을 넣었다 | 원래 바이트로 복원(`0707c9d6…`). 수정본은 `P/consumer/topics.candidate.mjs`에 있다 |
| `2026-11-08-이동수면판단보강/check-published.json` | 변경 전 기준 확인으로 `graph.py check`를 실행했을 때 덮어써졌다 | 원래 바이트로 복원(`2d42894f…`). 덮인 내용은 scratchpad에 있었다 |

다음 파일은 쓰지 않았다. 현재 해시로 확인했다.
- `게임행동-그래프/published/*`(graph `a16c7394…`), `publication.json`(`11c85252…`), `README.md`
- 프로젝트 `2026-11-05-지식그래프-실화면/D/graph.json`(`c680e41f…`)
- 게임·모드 코드, 세이브

## 9. 새로 만든 파일(모두 P 아래)

- 문서·설정
  - `00-agreement.md`, `publication-before.json`(260판 config 사본), `publication.config.json`
- 생성 스크립트
  - `prepare_framework.py`
  - prepare_framework가 만든 것: `overlay.py`, `compile_contract.py`, `verify.py`, `install_reviewed.py`, `finalize_reviewed.py`, `check_published_inputs.py`, `query_published.py`
  - `update_readme.py`(미실행)
- 계약·기록
  - `contract.json`
  - `production/produce.py`, `production/supplement.json`, `production/static-check.json`
- 기준판·후보
  - `baseline-260/`(3파일)
  - `candidate-review/`, `-v2/`, `-v3/`, `-v4/`(과거 검수판), `candidate-review-v5/`(최종)
- 조회 출력
  - `verification-candidate-v1~v4/`, `verification-candidate/`(v5)
- 검수
  - `review/source-report.md`, `source-findings.json`, `source-report-v2.md`, `source-findings-v2.json`
- 프로젝트 연결 준비
  - `consumer/verify.mjs`, `consumer/topics.candidate.mjs`
  - `consumer/before/`: 프로젝트 graph·validation·check·topics 사본, 이전 supplement v2~v4, produce.v1.py

폴더 전체는 약 2.2 GB이다. 대부분 후보 graph 사본이다. v1~v4 후보 폴더는 rebase 뒤에 지워도 된다.

## 10. push 대상

push하지 않았다. 게시 뒤 올린다면 대상은 GitHub `Luminous-Moa/corekeeper-ai-character`(로컬 `C:\Dev\CoreKeeperModSDK`의 remote `mine`)의 main이다. 세션 폴더를 `project/` 아래에 미러하는 방식이다(d6d1f3b 선례).
- 올릴 것
  - `project/work/2026-11/2026-11-08-용광로가공판단보강/`의 스크립트·기록·검수·consumer
  - 변경된 `project/work/2026-11/2026-11-08-행동판단프로젝트연계/topics.mjs`
  - 재생성된 `project/work/2026-11/2026-11-05-지식그래프-실화면/D/graph.json`
  - 이 문서
- 올리지 않을 것: 380 MB 크기의 graph 사본들(baseline·candidate·published). 과거 push에도 `게임행동-그래프`는 포함되지 않았다.

## 11. 확인하지 못한 것

- 실게임에서 아무것도 확인하지 않았다. 확인할 대상은 다음과 같다.
  - 클릭 뒤 재료 이동
  - 다른 물건이 가방으로 가지 않고 떨어지는지
  - 타이머 시간
  - 출력 생성
  - 3905에서 석탄 클릭의 효과와 음식 소비
  - 4009·1200의 전력 조건
- 자동 이동기 투입·회수의 실제 조건(배치·방향·전력)과 결과는 판정하지 않았다.
- 음수 recipeIndex 요청의 실제 결과(예외 여부)는 확인하지 않았다.
- 화면 visibleSlotIndex와 서버 buffer index가 같다는 전제는 확인하지 않았다.
- ECB 지연, tick 환산, 자동화를 다시 구성할 때 남은 시간이 보존되는지는 확인하지 않았다.
- 프로젝트 정상 진입점 조회는 홀드 때문에 실행하지 않았다.
- 새 정본 위의 재현과 독립 수용 영수증은 아직 없다.
