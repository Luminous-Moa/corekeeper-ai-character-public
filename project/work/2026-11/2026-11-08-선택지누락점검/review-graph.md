# 빈 선택지 그래프 갱신 독립 검수

2026-11-08. 대상: [graph-update.md](graph-update.md)의 갱신(기준 코드 `C:\Dev\CoreKeeperModSDK` ai-character `49bd748`). 파일은 고치지 않았다. 읽고 조회하고 검사만 돌렸다. 검사를 돌릴 때 생성물이 덮어써지는 것(legacy `build.mjs --strict`, B `verify.mjs`)은 미리 사본을 떠 두었고, 돌린 뒤 바이트가 같은지 확인했다. 검사 전에 그래프 파일 102개의 해시를 떠 두었고, 검사 뒤 모두 같았다.

## 결론

- 차단: 0건.
- 중요: 3건. 기획연결 그래프가 낡아 자체 check가 실패한다(I1). 사용자가 정하지 않은 구현·HEAD 판단이 사용자 결정 노드 안에 들어 있다(I2). 옛 정책 pol.bag-boundary에 대체 관계가 구조로 남지 않았다(I3).
- 경미: 6건.
- agreement.md의 결정 항목, 정적 대응 31개 전부, 행동 판단 연결 11관계, 게시본 해시, 도달성, 기존 세 가지 검사(product check, legacy --strict, 재생성 동일성)는 모두 주장대로 확인했다.

## 발견

### 중요

**I1. 기획연결 `graph.json`·`관계목록.md`가 최종 기능.md보다 낡았다. `node graph.mjs check`가 `ok:false`를 낸다**
- 파일: `work/2026-11/2026-11-07-기획연결/graph.json`, `관계목록.md`. 노드 `R.capabilities`, 관계 E059(P.execution→R.capabilities), E061(R.memory→R.capabilities).
- 근거: `node graph.mjs check`의 결과는 `{"ok":false,"deterministic":false,"mapMatches":true}`, exit 1이다. 현재 원문으로 컴파일한 것과 저장본을 비교하면, 차이는 원문 `oldCapabilities`(`지식그래프/기획/기능.md`) 하나다. 저장된 해시는 `0cd57b18…`이고 현재 해시는 `150aca56…`이다. 그 결과 위 노드 하나와 관계 둘의 근거 줄 번호가 다르다.
- 원인: 시각을 보면 `final-logs/planning.log`(기획 build)는 19:26:45이고, `기능.md`를 마지막으로 고친 시각은 19:28:04다. 이후 `run-final.mjs`는 `legacy-build`부터 다시 돌았고 `planning` 단계는 다시 돌지 않았다. legacy-build.log가 19:28:06이다.
- 영향: 제품 그래프는 `D/planning.mjs`가 `compile()`로 매번 새로 컴파일하므로 영향이 없다. 기획연결 `graph.json`은 product의 meta.sources에 등록돼 있지 않아 product check에도 걸리지 않는다. 하지만 기획연결 폴더 자체의 check가 실패하고, 그 폴더의 `graph.mjs show/path` 조회는 "원문/선언이 변경됨" 경고를 낸다. graph-update.md 검증표의 "생성 단계 모두 exit 0"은 돌린 당시에는 사실이었지만 최종 상태에는 맞지 않는다.
- 고칠 것: 기획연결에서 `node graph.mjs build` → `node graph.mjs check`를 돌린다. 제품 재생성은 필요 없다.

**I2. 사용자가 정하지 않은 구현 방식·HEAD 판단이 '사용자 결정·확정'과 '합의' 노드 안에 표시 없이 들어 있다**
- `지식그래프/기획/정책.md` pol.chests-shared(등급 사용자 결정, 확정도 확정): 규칙에 "상자가 많으면 구역 단계로 나눠 모든 상자에 닿게 한다"가 들어 있다. 이것은 4차 공통 규칙의 구현 해법이다(fixes.md:46-47, `AIChestAreas.Split/Step`, 40개 기준). agreement.md에는 없다.
- 기획 `P.chests-shared`(재기획, 상태 '합의·기존 자기 상자 한정을 대체')의 summary에는 "기지에 상자를 더 만들지 판단할 때 세는 상자는 직접 놓은 상자로 유지한다"와 "구역 단계" 문장이 있다. 앞 문장은 legacy 메모에서는 "(HEAD 판단, 상자 사용과 별개)"로 정직하게 표시돼 있지만, 기획 노드에는 그 표시가 없다. 근거 앵커는 agreement.md뿐이다.
- 제품 `pol.chests-shared`(D/planning.mjs가 만든 노드, class '사용자 결정 요약', status '사용자 결정')가 위 summary를 그대로 복사한다.
- 지식그래프 README의 규칙은 "사용자가 정하지 않은 것(추정·제안)은 정책이 아니라 후보로 둔다"이다. 이 상태로 두면 나중 작업이 구역 40개 단위나 직접 놓은 상자 세기를 사용자 결정으로 오인할 수 있다.
- 고칠 것: 두 문장을 R.gap-implementation summary나 정책 메모로 옮기고 'HEAD 판단·구현'이라고 표시한다. 또는 후보로 둔다. P.chests-shared summary와 pol.chests-shared 규칙에는 agreement.md 22-24줄의 내용만 남긴다.

**I3. 옛 정책 pol.bag-boundary의 상자 부분이 대체됐다는 사실이 메모 글로만 있다. 정책 층에는 대체 관계가 없다**
- 기획 층은 맞게 했다. P.chests-shared → P.bag-item-first·R.bag-store·R.bag-take에 '대체' 관계가 있고, 옛 노드는 그대로 남아 있다.
- legacy(`기획/정책.md:994-1001`): 규칙은 여전히 "자기 상자 접근 범위를 유지하고…"이고 확정도는 '확정'이다. 1001줄 메모만 추가됐다. `query.mjs show pol.bag-boundary`를 돌리면 이 정책이 그대로 `-적용->` F.bag-store("품목·**자기 상자**·수량 지정 보관"), F.bag-take("**자기 상자**에서 지정 수량 꺼내기")로 이어진다. 두 F 노드는 제목·내용을 고치지 않았고 pol.chests-shared와도 연결되지 않았다. 그래서 `why F.bag-take`는 옛 자기 상자 정책을 현재 확정 정책으로 보여 준다.
- 제품(`D/graph.json`): pol.bag-boundary는 status '합의'이고 summary는 옛 규칙 그대로다. pol.chests-shared와 오가는 관계가 없다. pol.chests-shared는 들어오는 관계 1개만 있고 나가는 관계가 없다. `product.mjs policy pol.bag-boundary`가 이 정책을 현재 합의처럼 보여 준다. 다만 sourceLocations가 994-1003줄이어서 `source`로 원문을 열면 메모가 보인다.
- 기존 관례(pol.body-npc의 '확정도: 대체됨', 제목의 '(대체됨)', 규칙 안의 "…를 대체한다")와도 다르다. 이번에는 부분 대체(trash 경계는 유지)라 그대로 따르기는 어렵지만, 구조로 표시하는 방법이 필요하다.
- 고칠 것(예시): legacy에서는 pol.bag-boundary 규칙에 "(상자 부분은 pol.chests-shared로 대체됨)"을 넣거나 상자 부분을 떼어 낸다. F.bag-store·F.bag-take에는 정책 pol.chests-shared와 메모를 더한다. 제품에서는 `D/planning.mjs`에 pol.chests-shared → pol.bag-boundary '대체: 상자 접근 부분' 관계를 더한다.

### 경미

- **L1. P.combat-entry → `AIChestMode.FightStarted`의 관계 이름이 '빈 선택지 구현 위치'다**(`D/planning.mjs`). FightStarted는 전투 진입 기준을 구현한 것이 아니라 피격 신호를 임시로 대신 쓰는 자리다(agreement 58줄). 이 노드는 상태가 '확정 설계 아님'인데, 관계 이름만 보면 구현됐다고 읽힐 수 있다. '임시 대리 신호(전투 진입 미구현)' 같은 이름이 낫다.
- **L2. 새 미정 Q.choice-total-limit('미정·사용자 판단 대기')와 Q.salvage('미정·할 일')가 기획 `graph.mjs overview`의 열린 질문 목록에 나오지 않는다.** 목록 필터가 `status==='미정'`과 정확히 같은 값만 고르기 때문이다. 실제 출력에는 Q.maintenance-unreachable만 나온다. legacy 미결(U.*)과 제품 도달성에는 문제가 없다.
- **L3. pol.maintenance-abandon 규칙 "돌려받을 수 없음을 **직접 보면**"과 P.maintenance-abandon summary의 "직접 보면"은 HEAD 해석이다**(fixes.md:72 "합의 M2의 두 경우는 직접 본 것이 전제다"). 타당한 해석이지만, 사용자 결정·확정 규칙 안에 표시 없이 들어 있다. I2와 같은 종류인데 영향이 작다.
- **L4. 빠진 합의 문장 두 개.** P.chest-open-mode와 pol.chest-open-mode에 "연 상자가 쓸모없으면 닫은 뒤 다음 판단에서 구역 대표 상자로"(agreement 56줄)가 없다. P.maintenance-abandon과 pol.maintenance-abandon에 "대장간 파괴·식별 불가면 모른다는 사실을 그대로 전한다"(39줄)가 없다. 틀린 내용은 아니고 덜 들어간 것이다.
- **L5. `기능.md` F.free-time 메모와 B `verify.mjs`의 `freeTimeRemoved`는 "GoalKind.Free"가 제거됐다고 쓴다.** 실제로는 `Core/AIGoalKinds.cs:6` enum에 `Free`가 남아 있다. 저장된 숫자를 지키기 위해서이며, 같은 파일 5줄 주석과 0c8a3e8 커밋 본문에 그렇게 적혀 있다. 제거된 것은 자유 시간 동작(FreeTime·FreeTimeReason·FreeStoreCandidates)이다.
- **L6. B `verify.mjs` 목표 구분 질문이 약해졌다.** 이전에는 경로 검사 `freeTimePath`(Update→FreeTime)가 있었는데, 지금은 `idleHoldCode` id 존재 확인으로만 바뀌었다. 자율 진입점에서 IdleHold까지의 경로(`AIBrain.Loop.cs:298`에서 호출)는 계산하지 않는다. `freeSourceLine`도 빠졌다. 옛 값은 before-final-graph/B 사본에 남아 있다.

## 확인 항목별 결과

### 1. agreement.md 결정 반영 (기획 source.mjs, 기획/정책·미결·기능.md)

| 결정 | 반영 | 판정 |
|---|---|---|
| 상자 전부 공용이 옛 제한을 대체 | P.chests-shared, pol.chests-shared. 기획 층에서는 '대체' 관계 3개가 옛 노드를 남긴 채 연결됐다 | 맞음. 단 I2(덧붙은 비사용자 내용), I3(정책 층 대체 관계 없음) |
| 상자 열림 모드 | P.chest-open-mode, pol.chest-open-mode(꺼내기·넣기·빠른 쌓기·정렬·닫기, 원래 선택면 복귀, 사라짐·시야 밖·거리 벗어남 해제, 전투 진입 시 닫기) | 맞음(L4 누락 1문장) |
| 전투 진입 기준과 '사용자 확인 전 확정 아님' | P.combat-entry 상태 '합의(사용자 구상)·전투 구현 시작 때 사용자에게 제시·확인 전 확정 설계 아님'. pol.combat-entry 확정도 '잠정'. 제품 pol.combat-entry status '사용자 구상·전투 구현 시작 때 재확인'. 도주 가능·공격 불가 예외, 임시 피격 신호도 들어 있다 | 맞음(L1) |
| 정비 포기 → 상위 LLM 상황 알림, 따지라는 지시 없음 | P.maintenance-abandon, pol.maintenance-abandon. "따질지 넘어갈지는 LLM이 정하며 따지라는 지시는 넣지 않는다"와 추정 명시가 들어 있다. 사용자 정정 뒤의 내용이다 | 맞음(L3, L4) |
| 가축 제외 | P.gap-life summary, pol.choice-gaps 메모 | 맞음 |
| 분해·L8·G3 미결 | Q.salvage/U.salvage, Q.maintenance-unreachable/U.maintenance-unreachable, Q.choice-total-limit/U.choice-total-limit. 모두 '미정'이고 확정으로 쓰지 않았다 | 맞음(L2) |

가방(B1-B9, B10 제외), 생활(M2·M3·M5·M6·M7·기억 시설 누출, M1 제외), 정비(M1·M3·M4) 범위도 P.gap-*와 F.*에 사실대로 들어갔다. source-pins 9개는 원본과 바이트가 같다.

### 2. 기획→코드 정적 대응 31개 (전수)

`D/graph.json`에서 kind '빈 선택지 구현 위치'인 관계를 모두 뽑았더니 31개였다. 각각에 대해 다음을 확인했다.
- `git show 49bd748:<file>`의 기록된 줄 범위에 그 메서드 선언이 있다. 31/31.
- B snapshot 파일이 Git blob과 같다. 31/31.
- `runtimeTrace=false`이고 `sourceCommit=49bd748…`이다. 31/31.
- 내부 클래스 소속을 확인했다. InventorySkill.UseTick은 AIInventory.cs:1167(class InventorySkill), AIInventoryBulk.Moves/ActivePreset은 AIInventory.cs:1488/1510(class AIInventoryBulk), SleepSkill.Tick은 AIBed.cs:172, MaintenanceAbandonSkill.Tick은 AIMaintenance.cs:901이다.

대상은 다음과 같다.
- P.chests-shared 5개: AIChestAreas.Step/Within, AIInventory.KnownChestWhere, AICraft.CraftChests/MaterialCount
- P.chest-open-mode 4개
- P.combat-entry 1개
- P.maintenance-abandon 6개
- P.gap-bag 7개
- P.gap-life 5개
- P.gap-maintenance 3개

모두 그 기획 내용에 맞는 함수다.

### 3. 행동 판단 연결 11관계와 게시본

- `topics.mjs` diff에서 새 기획→주제 관계는 다음 11개다.
  - P.chests-shared → craft·inventory
  - P.gap-life → cook·farm·food(claim_and_sleep)
  - P.gap-bag → inventory·interact(play_instrument)
  - P.chest-open-mode → inventory
  - P.gap-maintenance·P.maintenance-abandon → maintenance
  - P.combat-entry → combat
- 제품 그래프에도 같은 11개가 있다. 주제 선택은 모두 맞다.
- 새 코드 검토 위치 11개는 모두 code→주제 관계로 연결됐다.
- `behavior P.gap-life/P.combat-entry/P.chest-open-mode/P.maintenance-abandon`이 각각 맞는 주제를 돌려준다.
- 게시본 해시: `게임행동-그래프/published/graph.json`은 `a16c7394…5713`이다. 갱신 전 D(before-final-graph)와 현재 D의 meta.sources 등록값이 둘 다 이 값이다. coverage·manifest·publication.json도 같다. 파일 수정 시각은 15:17로, 이번 작업(19:2x) 전이다. 바뀌지 않았다.

### 4. 도달성·대표 조회·기존 검사 (직접 실행)

| 검사 | 결과 |
|---|---|
| `node tools/product.mjs check` | exit 0, ok:true, structuralErrors 0, stale 0, baseline=workingTreeHead=49bd748, 경고 없음 |
| `node tools/build.mjs --strict` (legacy) | exit 0, 3,972노드·38,665관계. "원본 해시가 모두 그래프와 같다 / 인용이 모두 지금 원문에 있다 / 그래프 안 경고 없음". graph.json·요약.md는 사본과 바이트 동일 |
| `node tools/query.mjs check` | exit 0, 위와 같은 세 줄 |
| `node tools/product-build.mjs --check` | exit 0, "regeneration byte-identical". D/graph.json·validation.json 바이트 동일 |
| B `extractor/verify.mjs` | exit 0, snapshot 367·질문 3. 생성물 3개 바이트 동일. code.graph.json 8,067노드·32,190관계, sourceCommit 49bd748 |
| 기획연결 `node graph.mjs check` | **exit 1, ok:false** (I1) |
| 공식 Git | ai-character = HEAD = 49bd748 |

- 도달성: 제품 그래프에서 CORE-01부터 나가는 방향으로 BFS를 돌렸다. 새 14노드(4~9단계), 새 pol 5개, 코드 대응 31/31이 모두 닿는다. 기획연결에서도 14개 모두 `path P.release-goal <id>`가 나온다. 예: P.combat-entry는 …P.field-choice→P.combat-entry, P.chests-shared는 …P.life-tree→P.gap-life→P.chests-shared.
- 대표 조회 18개는 모두 exit 0이었다: intent ×3, source ×2, show ×5, behavior ×4, policy, find, impact, legacy show. `source P.combat-entry`는 agreement 42-47줄과 46줄 원문을 해시와 함께 보여 준다.
- 제품 27,619노드·52,316관계이며 중복 id는 0이다.

### 5. 다른 대화 작업 때문에 손댄 곳

| 곳 | 판정 |
|---|---|
| F.free-time 상태 '제거됨', 앵커 3개 삭제 | 정당하다. 49bd748에서 FreeTime·FreeTimeReason·FreeStoreCandidates를 git grep으로 찾으면 0건이다. 0c8a3e8 커밋 본문에 "User decisions: free time is removed"가 있다. 옛 앵커와 당시 실게임 확인은 메모에 남았고, C/legacy-capability-map.json의 oldCodeAnchors·legacyStatus('실게임 확인')도 남아 있다. 정보 손실 없음(L5 표현만) |
| F.expedition-travel 앵커 `AITravelRules.PassageExits` 삭제 | 정당하다(49bd748에서 0건). 메모가 있다 |
| `D/planning.mjs` 원정 대응 2줄 삭제·Compose 서명 수정 | 정당하다. PassageExits·TravelSkill.Forecast는 0건이고, Compose의 실제 서명은 `(…, ExpeditionQuery query = null, bool travelMoving = false)`(AIExpedition.cs:161)다. P.expedition-navigation·lookahead에는 다른 대응이 남아 있다. 지역 이동의 새 대응은 담당 몫으로 graph-update에 명시돼 있다 |
| B `verify.mjs` 질문 변경 | 필요한 변경이다(FreeTime이 없어 실패). 줄 번호를 snapshot에서 계산하게 한 것은 개선이다. hostCallLines는 125…188, hostInterruptionLines는 193(AIGoals.cs의 실제 주석), llmSetGoalLines는 744다. 단 L5, L6 |
| `tools/config.json` | 집합으로 비교하면 5개가 추가되고 삭제는 0이다(정렬만 바뀜) |

## 실행 위치

검사 출력과 사본: `C:\Users\<USER>\AppData\Local\Temp\claude\c237366b8a8ea80b\44263ae2-99f5-4724-9951-89bf0b261264\scratchpad\` (check.json, legacy-strict.log, pb-check.log, bverify.log, maps.json, q\*.txt, pre-hashes.txt).

## 재확인 (검수 반영 후, 2026-11-08)

대상: [graph-update.md](graph-update.md)의 '검수 반영' 절과 [fix-review.mjs](graph-stage/fix-review.mjs). 이번에도 파일은 고치지 않았다. 검사 전에 그래프 파일 해시를 떠 두었고(`scratchpad\re\pre.txt`), 검사 뒤 모두 그대로였다. 첫 검수 뒤에 바뀐 파일은 반영 범위와 일치하는 12개다. 지식그래프의 graph.json·요약.md·기능.md·정책.md, 기획연결의 source.mjs·graph.json·관계목록.md·verification.json, D의 planning.mjs·graph.json, B의 verify.mjs·question-coverage.json.

### 항목별 판정

| 항목 | 판정 | 근거 |
|---|---|---|
| I1 기획연결 그래프 낡음 | **해결** | `node graph.mjs check`는 ok:true, deterministic:true, mapMatches:true, exit 0이다. 161노드·255관계. 현재 원문으로 컴파일한 것과 저장본의 차이는 0이다 |
| I2 사용자 결정과 구현·HEAD 판단 분리 | **해결** | P.chests-shared summary, pol.chests-shared 규칙, 제품 pol.chests-shared에는 agreement 22-24줄의 내용만 남았다. 구역 분할은 R.chest-areas-implementation('참고·구현 해법·사용자 결정 아님', 근거 fixes.md:209 '### 공통 규칙'·:215 '### 전수 목록')으로 옮겨졌다. 직접 놓은 상자 세기는 R.gap-implementation과 pol 메모에 'HEAD 판단'으로 표시됐다. 새 근거 integration.md:23-28을 보면 28줄에 "`BaseChests()`는 HEAD 결정으로…"가 실제로 있다 |
| I3 정책 층 대체 관계 | **해결** | legacy: pol.bag-boundary-chests(확정도 대체됨, 옛 근거 인용 그대로)가 생겼다. pol.bag-boundary 규칙은 trash 경계만 남기고 대체 사실을 적었다. 원래 근거 인용 전문은 그대로 남아 있다. F.bag-store·F.bag-take의 정책 목록에 pol.chests-shared가 들어가 `why F.bag-take`에 두 정책이 함께 나온다. 제품: pol.chests-shared → pol.bag-boundary '대체: 상자 접근 부분(trash 경계는 유지)' 관계가 있다 |
| L1 FightStarted 관계 이름 | **해결** | 관계 이름이 '임시 대리 신호(전투 진입 미구현)'로 바뀌었다. runtimeTrace=false |
| L2 overview 열린 질문 | **해결** | overview 질문 목록에 Q.choice-total-limit·Q.maintenance-unreachable·Q.salvage가 모두 나온다 |
| L3 '직접 보면' HEAD 해석 | **해결** | 규칙과 summary에서 빠졌다. pol 메모(fixes.md 경미 L8)와 R.gap-implementation에 'HEAD 해석'으로 표시됐다 |
| L4 합의 문장 누락 | **해결** | P/pol.chest-open-mode에 '쓸모없는 상자 → 구역 대표 상자', P/pol.maintenance-abandon에 '파괴·식별 불가면 모른다고 전함'이 들어갔다. 근거 앵커도 더해졌다 |
| L5 GoalKind.Free 표현 | **해결** | F.free-time 메모와 B verify의 freeTimeRemoved가 '동작 제거, 열거값은 저장 숫자 보존용으로 남음'으로 바뀌었다 |
| L6 IdleHold 경로 검사 | **해결** | question-coverage에 idleHoldPath(Update→Tick→Step3_Drive→AIBrain.Tick→AIRunGate.IdleHold)와 idleHoldCallLines [298]이 있다. 경로가 null이거나 줄이 비면 실패하는 검사가 추가됐다. 스테이징 사본 verify.mjs도 같다 |

### 다시 돌린 검사

| 검사 | 결과 |
|---|---|
| 기획연결 `node graph.mjs check` | exit 0, ok:true, deterministic:true |
| legacy `build.mjs --strict` | exit 0, 3,972노드·38,662관계, 정책 104. 해시·인용·경고 모두 통과. 생성물 2개는 사본과 바이트 동일 |
| legacy `query.mjs check` | exit 0, 세 줄 모두 통과 |
| `product.mjs check` | exit 0, ok:true, 구조 오류 0, stale 0, baseline=HEAD=49bd748 |
| `product-build.mjs --check` | exit 0, 재생성 바이트 동일. D/graph.json·validation.json 바이트 동일 |
| B `extractor/verify.mjs` | exit 0, snapshot 367·질문 3. 생성물 3개 바이트 동일 |
| 정적 대응 재검증 | 31/31이 `git show 49bd748` 줄 범위에 선언이 있고, snapshot과 같고, runtimeTrace=false다 |
| 제품 | 27,620노드·52,318관계, 중복 id 0. 새 15노드·pol 5개·코드 31개 모두 CORE-01에서 도달한다 |
| 게시본 `published/graph.json` | `a16c7394…` 그대로 |
| 대표 조회 | intent P.chests-shared, show/source R.chest-areas-implementation, policy pol.bag-boundary·pol.chests-shared, show P.combat-entry, behavior P.gap-bag, source P.maintenance-abandon 모두 exit 0 |

legacy 관계가 3개, 미결이 1개 줄었다(의도만 있음(구현 전) 7→6). 원인을 확인했다. pol.chests-shared가 '구현됨·미확인' 상태인 F.bag-store·F.bag-take를 새로 지게 되면서 자동 미결 'auto:의도만-있음(구현-전):pol.chests-shared'와 그 관계들이 없어졌다. 정상이다.

### 새 발견

차단 0건, 중요 0건이다. 경미 3건은 다음과 같다.

- **N1. 기획연결 `verification.json`이 낡은 실패 상태로 남아 있다.** 파일 수정 시각은 19:42:05로, 첫 검수 뒤이자 fix-review(19:46) 전이다. 내용은 ok:false, counts 160/254이고 `["check"]` exit 1이 기록돼 있다. 현재 check는 통과하므로 기록만 낡은 것이다. product의 meta.sources에는 등록되지 않은 파일이다. 고칠 것: 기획연결에서 `node verify.mjs`를 다시 돌린다.
- **N2. F.bag-store·F.bag-take의 제목과 '한 줄'을 제자리에서 '상자(모두 공용)'로 바꿨다.** 그런데 덧붙인 메모는 "위 2026-11-07 문구의 자기 상자 한정은 당시 판의 범위다"라고 쓴다. 정작 '위 문구'에는 이제 자기 상자가 없어서 메모가 가리키는 대상이 사라졌다. 옛 문구는 before-final-graph 사본과 pol.bag-boundary-chests에만 남아 있다. 메모에 옛 제목("품목·자기 상자·수량 지정 보관" / "자기 상자에서 지정 수량 꺼내기")을 인용해 두면 된다. 상태 '구현됨·미확인'과 근거는 2026-11-07 판 검증 범위 그대로이며, 49bd748에서 모든 상자 대상의 실게임은 U.gap-live에 남아 있다.
- **N3. 제품의 pol.bag-boundary 요약이 'pol.bag-boundary-chests'를 가리키지만, 그 노드는 제품 그래프에 없다.** legacy 조회(`query.mjs show`)에만 있다. 대체된 pol.body-npc도 제품에 없으므로 기존 방식과는 일치한다. 따로 덧붙이면, P.chests-shared → AIChestAreas.Step/Within 코드 대응은 여전히 이름이 '빈 선택지 구현 위치'다. 이 둘은 구역 해법의 코드이므로 R.chest-areas-implementation에서 이어도 된다(선택 사항).
