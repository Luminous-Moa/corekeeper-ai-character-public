# 빈 선택지·상자 공용·상자 열림 모드의 공식 지식그래프 반영

2026-11-08. 공식 ai-character `49bd748a8fcd390fc658166ca662ddb13b5c5c0d`(빈 선택지 3범위 + 상자 공용 + 상자 열림 모드 + 공식 61aa67b 병합)에 맞춰 코드층·개발 지식그래프·제품 그래프·행동 판단 연결을 갱신했다. 직전 기준은 `20b1aced`였다. 제품 코드·설치·게임은 건드리지 않았다. 절차는 [관측·재판단 반영](../2026-11-08-observation-redecision/graph-update.md)과 같다(스테이징 B 추출 → 정본 적용 → 생성 단계 전체 → 최종 검증).

## 반영한 결정과 구현

- 기획(재기획 원본 `2026-11-07-기획연결/source.mjs`)에 14노드·26관계를 더했다.
  - 합의: P.choice-gaps(빈 선택지 범위), P.gap-bag, P.gap-life(가축 제외 명시), P.gap-maintenance, P.maintenance-abandon(판단 모델 포기 → 상위 LLM 상황 알림, 따지라는 지시 없음), P.chests-shared, P.chest-open-mode, P.combat-entry(상태: 전투 구현 시작 때 사용자에게 제시·확인 전 확정 설계 아님).
  - 대체 관계: P.chests-shared → P.bag-item-first·R.bag-store·R.bag-take에 '대체: 자기 상자 한정을 모든 상자로'. 옛 노드는 지우지 않았다.
  - 미정: Q.choice-total-limit(G3, 참고층), Q.maintenance-unreachable(L8, 참고층), Q.salvage(분해).
  - 참고: R.gap-implementation, R.gap-review, R.gap-integration(설치·실게임 미실시).
- 기존 근거 그래프 원본:
  - 정책 5개를 더했다: pol.choice-gaps, pol.chests-shared, pol.chest-open-mode, pol.combat-entry(확정도 잠정), pol.maintenance-abandon. pol.bag-boundary에는 상자 부분을 pol.chests-shared가 대체했다는 메모를 달았다(trash 경계는 유지).
  - 기능 10개를 더했다: F.chest-areas, F.chest-open-mode, F.bag-bulk, F.bag-slots, F.bag-use, F.bag-rearrange, F.bed-sleep, F.craft-chests, F.farm-cook-gaps, F.maintenance-gaps. 모두 '구현됨·새 실게임 미확인'이다.
  - 미결 4개를 더했다: U.gap-live, U.choice-total-limit, U.maintenance-unreachable, U.salvage.
  - `tools/config.json`에 새 코드 파일 5개(AIBed, AIChestAreas, AIChestMode, AITestControl, AITestWorldEdit)를 넣었다.
- 제품 그래프 코드층: 49bd748에서 B를 다시 추출했다(snapshot 367파일 Git blob 고정, Roslyn/Acorn, 결정성 2회 동일). `D/planning.mjs`에 새 기획→코드 정적 대응 31개(`빈 선택지 구현 위치`, runtimeTrace=false)와 새 정책 요약 5개를 더했다.
- 과거 가방 39 실행 증거: 가방 메뉴 파일이 39b22df와 달라져 같은 blob 투영을 할 수 없다. `D/bag-source.mjs`에 49bd748 분기를 넣어 `changedSource`(priorBlob≠currentBlob, 현재 snapshot 해시 일치)를 확인하고 sameRevision=false·현재판 미적용을 유지했다. [successor-projection](graph-stage/successor-projection.json).
- 행동 판단 연결: `2026-11-08-행동판단프로젝트연계/topics.mjs`의 기존 주제 8개(craft, cook, farm, inventory, maintenance, food, interact, combat)에 새 기획과 코드 검토 위치를 더했다. 새 기획 노드에서 게임 규칙 주제로 가는 관계는 11개다(예: P.gap-life → 섭취·휴식(claim_and_sleep)·농사·요리, P.maintenance-abandon → 수리·강화, P.combat-entry → 전투). 게시본 `graph.json`은 바꾸지 않았다.

## 다른 작업 때문에 함께 맞춘 것

코드층 재추출로 공식 61aa67b에 들어온 다른 대화의 변경이 드러나 생성이 멈춘 곳만 최소로 고쳤다. 기획 노드는 만들지 않았다.

- B `extractor/verify.mjs`: 목표 구분 질문이 FreeTime을 찾다가 실패했다(0c8a3e8 자유 시간 제거). FreeTime 경로를 IdleHold로 바꾸고, 낡아 있던 증거 줄 번호를 snapshot에서 계산하게 했다.
- 기능.md F.free-time: 상태를 '제거됨(공식 0c8a3e8 자유 시간 제거)'으로 바꾸고 없어진 앵커 3개를 뺐다. 당시 실게임 확인은 당시 판 기록으로 남겼다.
- 기능.md F.expedition-travel: 없어진 `AITravelRules.PassageExits` 앵커를 빼고 메모를 달았다(91445e2).
- `D/planning.mjs`: 원정 정적 대응에서 없어진 `AITravelRules.PassageExits`·`TravelSkill.Forecast` 두 줄을 빼고, `AIExpedition.Compose`의 bool 매개변수 추가에 맞춰 길 계산 대응 서명을 고쳤다.

## 바뀐 그래프 파일

- `work/지식그래프/`: `graph/graph.json`, `graph/요약.md`, `README.md`, `제품그래프-사용법.md`, `tools/config.json`, `기획/기능.md`, `기획/미결.md`, `기획/정책.md`
- `work/2026-11/2026-11-07-기획연결/`: `source.mjs`, `graph.json`, `관계목록.md`
- `work/2026-11/2026-11-05-지식그래프-개편/B/`: `snapshot/`, `runtime-package/`, `extractor/pin-snapshot.mjs`·`build.mjs`·`verify.mjs`, `code.graph.json`, `B.graph.json`, `csharp.raw.json`, `javascript.raw.json`, `validation.json`, `boundaries.json`, `call-forms.json`, `snapshot.manifest.json`, `snapshot-verification.json`, `question-coverage.json`, `determinism.json`, `README.md`
- `work/2026-11/2026-11-05-지식그래프-개편/C/`: `graph-C.json`, `legacy-capability-map.json`, `validation-C.json`, `기획/그래프-선언.md`
- `work/2026-11/2026-11-05-지식그래프-실화면/`: `B/existing-node-mapping.json`, `C/overlay-source.json`(baseCommit), `C/validation.json`, `D/planning.mjs`, `D/bag-source.mjs`, `D/graph.json`, `D/validation.json`
- `work/2026-11/2026-11-08-행동판단프로젝트연계/topics.mjs`

적용 전 사본은 [before-final-graph](graph-stage/before-final-graph/)(topics.mjs는 `behavior-topics.mjs`), 적용 스크립트는 [apply.mjs](graph-stage/apply.mjs)와 [fix-planning.mjs](graph-stage/fix-planning.mjs)(기획 compile 규칙에 맞춘 층·관계명 보정), 근거 고정 사본은 [source-pins](graph-stage/source-pins/)와 [final-source-pins.json](graph-stage/final-source-pins.json)이다.

## 검증

| 항목 | 결과 |
|---|---|
| 공식 Git HEAD·B·제품 기준 | 모두 49bd748 |
| B 코드층 | 8,067노드·32,190관계, verify 통과, 결정성 7파일 동일 |
| 생성 단계 | code-verify·planning·legacy-build --strict·legacy-check·C-integrate·C-build·screen-remap·screen-validate·product-build·product-check·product-determinism 모두 exit 0 |
| legacy check | 원본 해시 일치·인용 전부 존재·그래프 안 경고 없음 |
| 제품 | 27,619노드·52,316관계(직전 27,015·49,591) |
| 제품 check | ok:true, 구조오류 0, stale 0, baseline drift 없음 |
| 제품 재생성 | 바이트 동일 |
| B 재빌드 | 생성물 7개 변화 0 |
| 실제 조회 | 18개 exit 0(intent·source·show·behavior·check) |
| 정적 대응 | 31개 각 1관계·runtimeTrace=false·snapshot/Git blob 일치 |
| 새 기획 노드 | 14개 모두 P.release-goal에서 도달, 고정 사본 해시 불일치 0 |
| 새 정책 요약 | 5개 모두 제품 그래프에 존재 |

[최종 검사](graph-stage/final-verification.json), [생성 로그](graph-stage/final-logs/), [조회 출력](graph-stage/final-queries/).

```powershell
node tools/product.mjs intent P.choice-gaps
node tools/product.mjs source P.combat-entry
node tools/product.mjs show P.chests-shared
node tools/product.mjs behavior P.gap-life
node tools/product.mjs check
```

## 그래프에 아직 없는 다른 작업 (각 담당 몫, 이번에 기획 노드를 만들지 않음)

공식 20b1aced→49bd748 사이 다른 대화가 넣은 변경은 코드층에만 들어 있고 기획·정책·기능 노드가 없다.

- 이동·전달 개선 4건(이동전달-개선4건): 1/4 같은 탐험 안 선택을 현장 이탈에서 제외·자원 보고를 계획 재료∪값진 종류로 한정, 2/4 출구 방문 결과 실행 간 보존, 3/4 장애물 비교에 의한 준비 취소 제거, 4/4 완료 거절 안내와 가방 메뉴 목표 품목 표시.
- 판단 모델 호출 간격 변경(분당 상한·거절 뒤 5초 제거, 트리·전투 제한 해제)과 이동 중 호출 계기 개편(항목 6, 3aa7bc8).
- 자유 시간 제거와 상위 LLM의 값진 종류 변경(0c8a3e8, acf2dd1). F.free-time은 상태만 '제거됨'으로 맞췄다.
- 지역·출구 단위 이동(91445e2)과 그 재검수 수정(c82be20~61aa67b). F.expedition-travel은 앵커만 맞췄다.
- 판단 입력 토큰 절감 1~4(2cc87c6, 37e9d04, 6e10d15, f334a85).
- 시험 명령·/ai hold·/ai tp(ae09acf)와 이동 브랜치의 hold 정지(092c692). AITestControl·AITestWorldEdit는 기능 앵커가 없어 '선 밖 코드' 미결로 잡힌다.

독립 검수는 호출한 HEAD가 맡는다. 설치·실게임은 이번 범위가 아니다.

## 검수 반영 ([review-graph.md](review-graph.md))

위 절의 수치는 검수 전 값이다. 최종 값은 이 절을 따른다. 적용 스크립트는 [fix-review.mjs](graph-stage/fix-review.mjs)다.

- **I1 기획연결 graph.json 낡음:** 아래 변경을 모두 넣은 뒤 `run-final.mjs`를 처음 단계(code-verify)부터 끝까지 다시 돌렸다. 기획연결 `node graph.mjs check`는 ok:true, deterministic:true, mapMatches:true다.
- **I2 사용자 결정과 구현·HEAD 판단 분리:**
  - P.chests-shared summary와 pol.chests-shared 규칙에는 agreement.md 상자 공용 절의 내용만 남겼다(모든 상자 공용, 호스트·다른 AI 포함, 제작은 근처 상자 재료).
  - 구역 단계 분할(40개 기준)은 새 참고 노드 R.chest-areas-implementation('참고·구현 해법·사용자 결정 아님', 근거 fixes.md 4차 공통 규칙·전수 목록)으로 옮겼다. P.chests-shared에서 '참고: 상자가 많을 때의 구현 해법(사용자 결정 아님)' 관계로 잇는다.
  - 기지 상자 제작 판단은 직접 놓은 상자만 센다는 것은 R.gap-implementation summary와 pol.chests-shared 메모에 'HEAD 판단'으로 표시했다.
  - 제품 pol.chests-shared는 P summary를 복사하므로 함께 바뀌었다.
- **I3 정책 층 대체 관계:**
  - legacy: pol.bag-boundary에서 상자 부분을 떼어 새 항목 pol.bag-boundary-chests(확정도 대체됨, 종류 역사, 옛 근거 그대로)로 남겼다. pol.bag-boundary는 trash 경계만 갖고 대체 사실을 규칙에 적었다. 옛 정책은 지우지 않았다.
  - F.bag-store·F.bag-take: 제목과 한 줄을 '상자(모두 공용)'로 고치고 정책에 pol.chests-shared를 더했다. 2026-11-07 자기 상자 한정은 당시 판 범위라는 메모를 달았다.
  - 제품: `D/planning.mjs`에 pol.chests-shared → pol.bag-boundary '대체: 상자 접근 부분(trash 경계는 유지)' 관계를 더했다.
- **L1:** P.combat-entry → AIChestMode.FightStarted 관계 이름을 '임시 대리 신호(전투 진입 미구현)'로 바꿨다.
- **L2:** Q.choice-total-limit과 Q.salvage의 status를 '미정'으로 맞췄다(부가 설명은 summary로 옮김). 기획 overview 열린 질문에 Q.choice-total-limit·Q.maintenance-unreachable·Q.salvage가 모두 나온다.
- **L3:** 포기를 '직접 본 경우'로 한정한 것은 P.maintenance-abandon summary와 pol.maintenance-abandon 규칙에서 빼고, pol 메모와 R.gap-implementation에 'HEAD 해석'으로 표시했다(근거 fixes.md 경미 L8).
- **L4:** P/pol.chest-open-mode에 '연 상자가 쓸모없으면 닫은 뒤 구역 대표 상자로', P/pol.maintenance-abandon에 '대장간 파괴·식별 불가면 모른다는 사실을 그대로 전한다'를 넣었다.
- **L5:** F.free-time 메모와 B verify의 freeTimeRemoved를 '자유 시간 동작(FreeTime·FreeTimeReason·FreeStoreCandidates) 제거, GoalKind.Free 열거값은 저장 숫자 보존용으로 남음'으로 고쳤다.
- **L6:** B verify 목표 구분 질문에 경로 검사 idleHoldPath를 넣었다(AIPlayerSpikeMod.Update → Tick → Step3_Drive → AIBrain.Tick → AIRunGate.IdleHold, 5단계). 호출 줄 idleHoldCallLines(AIBrain.Loop.cs:298)도 snapshot에서 계산한다. 경로가 없거나 증거 줄이 비면 실패한다.

### 최종 수치와 검사

| 항목 | 결과 |
|---|---|
| 기획 그래프 | 161노드·255관계(새 15노드·27관계), `graph.mjs check` ok |
| legacy 정책·기능·미결 | +6(pol.bag-boundary-chests 포함)·+10·+4. build --strict·query check 경고 0 |
| 생성 단계 11개 | code-verify부터 product-determinism까지 모두 exit 0 |
| 제품 | 27,620노드·52,318관계, check ok·구조오류 0·stale 0, 재생성 바이트 동일 |
| B 재빌드 | 생성물 변화 0 |
| 실제 조회 | 20개 exit 0(policy pol.bag-boundary, show R.chest-areas-implementation 추가) |
| 정적 대응 | 31개 각 1관계(P.combat-entry 1개는 '임시 대리 신호' 이름), runtimeTrace=false, blob 일치 |
| 새 기획 노드 | 15개 모두 P.release-goal에서 도달 |

### 재확인 반영

- N1: 기획연결 `node verify.mjs`를 다시 돌려 `verification.json`을 현재 상태로 갱신했다.
- N2: F.bag-store·F.bag-take 메모에 2026-11-07 당시 제목('품목·자기 상자·수량 지정 보관', '자기 상자에서 지정 수량 꺼내기')을 인용해 메모가 가리키는 대상을 밝혔다.
- N3은 기존 방식과 같아 그대로 둔다.
- 생성 단계 11개 모두 exit 0, 기획연결 check ok, 제품 check ok·구조오류 0·stale 0, 최종 검증 통과(27,620노드·52,318관계).
