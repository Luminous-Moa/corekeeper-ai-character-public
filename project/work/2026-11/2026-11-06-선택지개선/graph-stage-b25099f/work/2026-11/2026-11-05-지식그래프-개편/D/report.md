# D 통합 및 조회 결과

D는 A·B·C를 작성한 컨텍스트와 별개로 통합했다. 제품 코드, 기존 그래프·기획·기존 도구, 게임 데이터는 변경하지 않았다. 생성 graph JSON을 직접 편집하지 않는다.

최종 입력 C freeze 이후 **3,516노드/13,198간선, 원문254파일**로 재생성했다. `build --check`, `verify`, 기존 wrapper의 `check/user/policy` 모두 종료코드0이었다. 문서 노드149개의 source 시작점이 실제 해당 헤딩인지 확인했고 stale 목록은 비었다. wrapper 증거는 wrapper-check.json, wrapper-user.json, wrapper-policy.json이다. ACT-A-01의11개 요소 중 health는 기능에 연결되고 관측된404의3개 요소는 선언 endpoint가 없어 gap을 남긴다.

## 실행

이 작업 폴더에서 실행한다. Node 기본 모듈만 사용한다.

```powershell
node D/build.mjs
node D/build.mjs --check
node D/verify.mjs
node D/questions.mjs
node D/query.mjs overview
node D/query.mjs user ACT-A-01
node D/query.mjs feature 'ai-bridge:GET /v1/health'
node D/query.mjs source pol.multi-ai
node D/query.mjs policy pol.multi-ai
node D/query.mjs check
node D/query.mjs legacy overview
node D/query.mjs legacy why F.memory-recall
```

기존 폴더의 `tools/product.mjs`와 `tools/product-build.mjs`도 같은 진입점이다. `build`는 이미 생성된 A/B/C를 통합하며 제품 코드를 재추출하지 않는다. 전체 재추출은 A 문서→A/build-graph-A.mjs, B 추출 도구→code.graph.json/meanings, C 문서→C/build-graph-C.mjs를 먼저 실행한 뒤 D를 만든다. B7는 C가 대조하는 추정 근거로 보존한다.

## D-1: 통합과 보존

- 코드 정본은 B/code.graph.json이며 B/meanings.json의 한 줄 뜻만 별도로 덧붙인다. A의 실제 출력과 C의 정본 문서는 문서 ID로 적재한다.
- A의 CORE-A-01/JOURNEY-A-01/IA-A-01은 C의 reconciliation 선언으로 정본 상위문서와 구별한다. A 원형과 D의 archivedInferences에 보존하며 제품코어·저니·IA 정본을 중복 생성하지 않는다.
- GET /v1/health, POST /v1/plan의 두 오류 응답은 메서드·경로가 같은 기계 추출 입구에 연결한다. `기능→화면요소`는 결과 생산 관계다. 읽기 출력에 가짜 조작 트리거를 추가하지 않는다. `user`는 이 간선을 역방향으로 조회했음을 표시한다.
- GET /의 404는 선언된 API 입구가 없어 미매칭 목록에 남긴다. 새 기능을 만들지 않는다. 시작 출력은 HTTP 요청 매핑이 아니다.
- 기존 graph 1,551노드/10,416간선, 정책60·후보19·미결137·게임312를 원형 보존한다. 문서 미결60과 자동 생성 미결을 구별한다. F.*36개는 과거 기술 능력이며 API 기능으로 자동 승격하지 않는다. C 대응표의 활동·코드앵커·표준입구와 legacy 조회를 사용한다.
- 원문 경로·SHA-256, 고정 snapshot manifest, 보존 manifest를 검사한다. 고정 원본 변경은 실패다. 현재 제품 작업트리 HEAD가 기준 e82eb87 이후로 이동하면 별도 drift 경고이며 고정 원문 조회를 무효화하지 않는다.

## D-2: 실제 요청 대조

A의 raw metadata와 method/path/status를 다시 대조한다. D도 읽기 전용 GET health(200, ok=false)와 GET /(404)를 독립 실행했다. 원문은 runtime-observation.json에 있다. 외부 모델 호출과 게임 조작은 수행하지 않았다.

이 관측은 격리 bridge의 HTTP 처리와 정적 route handler 대조다. 게임 프론트 조작 또는 로드된 함수의 runtime trace를 수집한 것은 아니다. 관측하지 않은 20개 기능을 죽은 코드로 판정하지 않는다. A의 실패 응답은 정상 계획 성공이나 게임 내 AI 동작의 증거가 아니다. 런타임 복제 provenance도 실행 후 파일 비교이며 동시점 로드 증명이 아니다.

## D-3: 실제 물음과 원문 대조

재현 파일은 questions.mjs이며 결과 questions.json에 사용자 활동, 코드 도달 결과, 최대32단계 visited BFS 최단 경로, 실제 원문을 남긴다. 표시용 feature 트리는 깊이12/코드800개로 제한하며 잘림을 표시한다. 다른 기능/화면 입구·navigate·re-render 경계를 넘겨 펼치지 않는다. 가능한 콜백 연결은 실행 확인과 구별한다.

1. **캐릭터 상태 조회**: UA-01의 의도와 slash:ai를 대조했다. 멤버 없는 `/ai`/status는 StatusAll, 멤버 지정 status는 해당 AIAgent 범위의 StatusText로 간다. AIWorldSettings.Members가 월드별 `AIPlayers/<ServerGuid>/settings.json`을 읽으며, 멤버·stage·Walking 중 Goal.Text를 모아 Reply 큐로 보낸다. bridge health의 CLI 사전 점검과 다른 상태다.
2. **호스트 목표와 자율 목표**: UA-02(호스트 목표), UA-04(자율 활동)를 대조했다. 호스트 명령은 AIGoals.HandleGoalCommand→SetGoal(source=host)다. 알아서/auto는 AIPlanner.Enable을 켜고 보고를 보내며, Update 입구의 Tick/Apply/CheckReason에서 답의 수정번호·호스트 목표를 검사해 같은 SetGoal로 수렴한다. 늦은 자율 답이 호스트 목표를 덮지 못하는 이유는 원문의 revision/source 검사다. HTTP 요청 발신 입구와 다음 프레임 응답 처리는 동일한 동기 호출 체인으로 꾸미지 않는다.
3. **기억 검색 결과**: UA-06 의도와 POST /v1/plan→plan→memgraph.runQueries를 대조했다. runQueries는 기본3개 질의, 질의별 몫, 전체 검색 상한을 적용해 text/log를 만든다. runQuery는 유한 Q 레지스트리의7개 후보 중 입력에 따라 호출한다. B가 후보를 possible=true callback으로 추출하므로 전부 실제 실행됐다는 의미가 아니다.

세 게임 활동의 실제 출력 요소는 사용자 유예/미관측이므로 `user` 결과에 gap이 남는다. 이를 가짜 화면 ID로 메우지 않았다. 완전히 관측된 부분 체인은 ACT-A-01→BRG-02 요소→(결과 생산 관계 역조회)health→코드→원문이다. 캐릭터/자율/기억의 코드 답은 원문으로 확인했으나 게임에서의 성공 동작을 새로 검증한 것은 아니다.

## D-4: 정합성 판정

| 검사 | 판정과 근거 |
|---|---|
| 제품코어와 정책 충돌 | CORE-01과 적재 정책 원문을 읽었다. thin client 몸·월드별 자기 캐릭터·멤버별 참여라는 정체성과 직접 충돌을 확인하지 못했다. Jev→OpenAI, 반사범위 유보, 평화/전투 빈도는 과거/후속 범위 메모를 C에 반영하도록 회송했다. 의미 일치를 단순 문자열 검사로 합격시키지 않는다. |
| 트리거 요소에 유저 활동 없음 | 자동 검사 결과는 validation.json에 기록. 현재 읽기 출력은 결과 생산 관계이므로 트리거 간선을 만들지 않는다. |
| 다음 활동으로 navigate 없음 | C의 게임 활동 간 다음 활동은 기획 의도다. 게임 화면·navigate 관측이 유예되어 실동선 합격으로 판정하지 않는다. |
| 목적 없는 화면 후보 | BRG-01 시작, BRG-04 unknown-member, BRG-05 malformed 출력은 활동 연결이 없다. A의 실제 개발 진단 출력이며 정상 사용자 활동으로 확인되지 않았다. C4에 매핑 부재를 보존하며 목적 없다고 확정하지 않는다. |
| 화면 표시 정책과 정책 노드 차이 | A의 출력 문서는 표시정책을 새로 선언하지 않았다. C의 처리정책과 혼동하지 않는다. 실게임 화면 표시정책 대조는 유예다. |
| 다음 활동과 저니 순서 차이 | C 저니는 참여→플레이→대응 분기→마침→재참여이며 UA-08 개발자 활동은 게임 저니 밖이다. 다음활동은 가능한 분기/순환이며 단일 선형 순서를 강요하지 않는다. 관측 기반 게임 순서 검증은 유예다. |

## D-5: 교정과 남은 범위

source의 처음 구현이 본문 속 ID 언급을 절 제목으로 잘못 잡는 문제를 HEAD 검수에서 찾았다. 실제 Markdown 헤딩만 정확히 매칭하고 0개/복수는 실패하도록 고쳤다. pol.multi-ai가 이전 pol.single-ai-first 메모가 아닌 자기 규칙·대체 근거를 읽는 것을 확인하고, 전체 문서 노드 헤딩 검사를 추가했다. 정책 원문 시간범위 지적은 C 담당자에게 원문 수정·재적재로 돌렸다.

사용자가 유예한 게임 화면 원본·위치 매칭·처음 사용자 게임 흐름, 정상 외부 CLI 계획/기억 응답은 미확인으로 남긴다. B의 semantic diagnostics·미도달·미해결 호출·possible 연결은 B 보고와 조회에 드러낸다. 모의 실행을 실제 관측으로 승격하지 않았다. 개발 작업 모델 지시 철회는 과거 기록 보존과 별개이며 이 도구가 개발 모델/추론 강도를 지정하지 않는다. 제품 AI 선택 정책은 보존한다.

## 검증 증거

- verification.json: 두 번 build 바이트 동일 및 저장본 동일, 중복ID/끊긴참조 부정검사, missing/hash 변경 감지, 문서 헤딩·pol.multi-ai 회귀검사, 보존본 전체 해시 동일, F.*36대응 확인.
- seed **2026110505**, 정렬된 비대표 코드 후보에서 LCG6개를 골라 source로 실제 읽었다. LethalHit는 lethal dictionary 조회 함수, _lastGoalCheck는 float 필드, lastTile은 int2 필드, ExpJson.GetI는 GetD 결과 정수 변환, _settings는 DecisionsSettings 필드, AIBody.Current.get은 현재 멤버 상태의 getter로 원문 선언·줄·종류가 맞았다. 기능 실행 성공으로 해석하지 않는다.
- runtime-observation.json: D의 실제 읽기 전용 HTTP 응답.
- questions.json: 세 물음의 사용자 단계 한계와 원문 답·경로.
- validation.json: D1 매칭/미매칭, D2 관측/미관측, D4 자동 목록, D5 유예 범위.

최종 독립검수와 기존 진입점 연결 확인은 HEAD가 별도 컨텍스트에서 진행한다.
