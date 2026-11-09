# 개발 지식그래프 개편 독립 검수

검수일: 2026-11-05. 검수 담당은 A/B/C/D 작성에 참여하지 않은 별도 컨텍스트다. start-job 및 제작 정의/절차, agreement.md와 사용자 실게임 화면 유예 조건을 읽고 검수했다. 제품 소스·운영 과정·게임 저장을 수정하지 않았다. 새 HTTP 요청은 허용된 127.0.0.1:18331의 GET /v1/health 하나뿐이다.

최종 판정: **이번 합의 범위에서 통과. 중요 미해결 지적 없음.** 고정 B·원문 보존·실질 탐색과 최종 D/C 재생성·신규 CLI를 독립 확인했다. 실게임 화면 유예와 정적 추출/실행 관측 한계는 아래와 같이 남긴다.

## 발견 사항과 조치 확인

1. **중요, 수정 확인:** 정책 source가 ID의 첫 문자열 출현을 선택해 이전 정책의 상호참조를 원문으로 제시했다. 부모가 발견했고 독립 실행에서도 `policy pol.multi-ai`가 141~143행의 앞 항목 메모만 출력하는 것을 재현했다. D의 헤딩 정확 매칭 수정 후 같은 CLI가 144~153행의 해당 제목·규칙·등급·확정도·근거를 출력했다. 표준 정책 43개 전수의 시작 헤딩 및 규칙 본문 포함도 독립 검사했다. 실패 0. `policy-source-check.json`, `cli-4.txt`, `traversal-results.json` 참조.
2. **경미, 수정 확인:** D/questions.mjs의 호스트/자율 목표 질문이 UA-03(생활·제작)으로 분류되어 있었다. 부모에게 전달한 뒤 UA-02와 additionalActivities UA-04로 바뀐 최종 소스와 D 보고서를 읽어 교정을 확인했다. 코드 경로 자체는 아래 독립 대조에서 맞았다.
3. **경계, 막음 아님:** product check는 고정 스냅샷·통합 입력의 해시와 현재 Git HEAD를 다룬다. 현재 작업트리의 미커밋 변경이나 모든 원래 세션 기록의 변화를 실시간으로 검출하는 것은 아니다. 부모는 사용법에 이 범위를 명시하고 기존 원본 검사 `tools/query.mjs check`를 안내했다. 독립 계산에서 보존 사본은 모두 유지되었고 원래 README만 새 진입점 안내 추가로 달라졌다.

## 원본과 보존 검수

- B snapshot.manifest의 **115개** 등록 파일을 실제 `git cat-file blob e82eb87f25641cc7c31e6216f64e20a34e58d0c0:<path>`로 읽어 SHA-256을 독립 계산했다. 전부 일치. 이는 manifest의 추출 대상 파일 수이며 별도 전체 archive 검증 파일 수와 혼동하지 않는다. `git-blob-verification.json`.
- B snapshot과 preserved manifest에 기록된 사본 해시 전부 일치. B determinism의 7개 산출물 현재 해시도 기록된 두 회차 값과 일치. B 노드 중복 0, 깨진 간선 0. `fixed-audit.json`.
- 기존 능력 F.* 36개가 고유 ID로 대응표에 존재한다. 기존 그래프 전체·게임 참조·자동 미결은 별도 원형으로 보존되며 F.*를 API 입구로 자동 승격하지 않는다.
- 기존 정책 **60**, 후보 **19**, 문서 미결 **60** 항목의 제목과 본문을 모두 새 보존 문서에서 확인했다. 추가된 legacy-source 주석 외 기존 본문 누락 0. `preservation-samples.json`.
- evidence-index의 원문 인용 **352개**가 보존 원문 파일에 실제 존재한다. 이는 현재 동작의 증거가 아니라 당시 기록 존재의 증거다.
- 후보의 등급, 정책의 확정·잠정·검토·대체 표시, 미결의 종류와 닫는 법이 유지된다. 개발 모델·추론 강도 과거 지정은 새 작업 조건으로 승계하지 않는다는 사용법 경계를 확인했다. 제품 자체 Decisions/구독 CLI 정책과 구별된다.

## 독립 비대표 표본

새 seed **72945137**. B 작성자의 8개 표본 및 health/plan/slash 대표 입구를 제외한 기능을 정렬하고 LCG/Fisher–Yates로 섞어 처음 4개를 골랐다. 고른 원문을 실제로 읽었다.

| 표본 | 판정과 원문 대조 이유 |
|---|---|
| HeadlessSdk.CompileCheck | 맞음. HeadlessSdk.cs 146~155에서 scriptCompilationFailed를 읽고 종료 코드를 선택하며 catch에서 Fail을 호출. 그래프의 로컬 Fail 연결이 일치하고 외부 Unity 동작을 실행 확인으로 주장하지 않는다. |
| AIPlayerSpikePatches.UnloadWorldsPrefix | 맞음. AIPlayerSpikeMod.cs 1403~1406에서 DisposeAll을 호출. 719~720의 UnloadWorldsInternal prefix 등록도 직접 확인했다. |
| AIPlayerSpikePatches.ServerNetworkUpdatePrefix | 맞음. 같은 파일 1398~1401에서 Pump 호출. 717~718 등록과 일치한다. |
| AIPlayerSpikeMod.CanBeUnloaded | 맞음. 같은 파일 207~210이 true를 반환하며 다른 로컬 호출이 없다. 뜻 파일 설명과 일치한다. |

동일 seed로 정책·후보·미결에서 각 2개를 새로 골라 본문 및 인용 주변 문맥까지 읽었다.

- `pol.realtime-by-program`: 원문 98d60caf29d2f0d7.md 125/127의 사용자 원칙과 일치한다. 인접 HEAD의 두 층 제안을 사용자 결정으로 승격하지 않는다.
- `pol.recall-index-then-query`: b6e49b7c142e1575.md 60의 사용자 구상/HEAD 확인과 일치한다. 효과 향상은 추정이라는 문맥을 현재 실행 성과로 바꾸지 않는다.
- `cand.chatroom-attach`: 98d60caf29d2f0d7.md 46의 ‘제가 덧붙인 것, 2장에서 확인’ 문맥에 맞게 설계 제안과 확인 기록 없음이 유지된다.
- `cand.v2-design`: e4046c26897d1747.md 13에서 이미 확정받은 계약으로 기록하지 않는다는 원문을 확인했다. 후보 유지가 맞다.
- `U.planner-policy-scattered`: 0fe0e0781470b7f1.md 125의 다섯 군데 계기 규칙 문제와 관련 기능·닫는 법이 일치한다.
- `U.openai-credit`: 7dd4032e727303cc.md 171 및 50에서 실제 잔액 미확인과 미정 항목을 확인했다. 현재 잔액·한도 완료로 승격하지 않는다.

## 실제 질의와 원문 답

대표 활동은 신규 진입점으로 `user ACT-A-01` → BRG-02 출력 요소 → 역방향 `기능→화면요소` 생산자 → `ai-bridge:GET /v1/health` → preflight.health/memberHealth 및 send를 따라갔다. `feature`와 `source`를 별도로 실행하여 실제 코드 줄을 읽고, `impact preflight.health`로 역방향 후보 영향도 확인했다. GET /v1/health의 현재 응답은 HTTP 200, ok:false, 멤버 4명, busy=[]였고 격리 환경의 비어 있는 사전 점검으로 해석했다. `http-health.json`, `cli-1.txt`, `cli-2.txt`, `cli-12.txt`.

주의: API 기능 트리는 정의서에 따라 호출 측 C# HealthProbe도 별도 루트로 포함한다. 그 아래 발견되는 ToggleLog의 write는 호출 측 로깅 후보이며 이 독립 HTTP GET에서 게임 코드가 실행됐다는 뜻이 아니다. 서버 health는 메모리의 사전 점검 결과를 읽는다.

1. **캐릭터 상태 조회 처리/데이터:** slash:ai → TryHandleChatCommand → Dispatch → StatusText. 전체 상태는 StatusAll. StatusText 654~658은 월드별 Members(WorldId), 현재 멤버와 stage, Walking일 때 Goal.Text를 사용한다. Members 1501~1521은 PathFor(worldId)의 파일이 있으면 UTF-8 JSON을 읽고 members 또는 이전 aiEnabled 형식을 처리한다. bridge health와 다른 상태다. 해당 `source` 두 개를 실제 실행했다.
2. **호스트와 자율 목표 결정/코드:** 호스트는 Dispatch → AIBody.HandleCommand → AIBrain 전달 함수 → AIGoals.HandleGoalCommand → SetGoal(...,"host"). 자율은 Update → Tick/Step3_Drive → AIBody.Tick → AIPlanner.Tick/Apply → AIBrain.SetGoal → 같은 AIGoals.SetGoal에 llm 출처로 수렴한다. AIGoals 175~197의 revision 갱신·OnHostGoal, AIPlanner 353의 llm 출처와 366~376의 CheckReason을 읽어 늦은 답/호스트 목표 거절을 확인했다. 사용자 의도인 위쪽 목표 LLM과 현장 행동 선택 정책은 별도 보존된다. 실게임 동작은 검증하지 않았다.
3. **기억 검색 결과 생성:** POST /v1/plan → plan → memgraph.runQueries → forEach callback → runQuery. runQuery 576~584는 Q[q.q]에 따라 함수 하나를 고르고 질의별 길이 제한을 적용한다. runQueries 587~609가 최대 3개 질의·헤더·각 질의 몫·전체 길이 제한을 적용해 `{text,log}`를 조합한다. Q의 7개 후보는 possible 경로로 표시되어 특정 실행의 7개 호출로 오해하지 않는다. 실제 기억이 채워진 외부 모델 성공 응답은 관측하지 않았다.

위 7개 세부 경로는 작성자의 question-coverage를 그대로 신뢰하지 않고 독립 BFS로 간선 존재를 확인했다. `traversal-results.json`. 깊이 제한 없는 모든 경로 나열 대신 방문 집합을 두고 한 경로만 찾았다.

## 오류 검출과 한계

독립 메모리 입력에 중복 노드·깨진 참조·잘못된 해시·없는 파일을 넣어 각각 검출됨을 확인했다. 제품 파일 자체를 변조하지 않았다. 실제 D 수정 중에도 CLI가 변경된 lib.mjs를 stale로 경고하고 check/재생성 비교를 실패시키는 모습을 확인했다.

실게임 화면·컴퓨터 유즈·게임 처음 사용자 동선은 사용자 유예이며 이번 막음이 아니다. Unity/게임 외부 메타데이터 부재, 동적 호출, 가능한 정적 경로, 성공한 외부 CLI/기억검색 응답 미관측은 명시된 경계로 유지한다. B-7의 잘못된 검색 인자로 타 프로젝트 일부가 보인 사고는 inference.md에 공개되어 있다. 제공 기록상 A 및 기존 코어키퍼 기획을 추정 근거로 사용한 증거는 발견하지 못했다. 완전 무오염을 추가 증명했다고 주장하지 않는다.

## 최종 재검사

최종 입력 고정 알림 뒤 2026-11-05 01:22:33 UTC에 재검사를 마쳤다.

- 신규 product 진입점의 overview/user/feature/source/policy/check/legacy show/impact/find 및 product-build --check **16개 명령 모두 exit 0, stderr 없음**.
- `D/build.mjs --check`: 바이트 동일. `D/verify.mjs`: 두 번 build·저장본 비교, 문서 헤딩, 보존 해시, 오류 주입 검사 전부 통과.
- 최종 CLI check: ok=true, stale=[], structuralErrors=[].
- 정책 43개 원문 및 7개 세부 코드 경로를 최종 그래프로 다시 대조했다. 실패 0.
- 최종 C README가 현재 허용 범위 완료, 재생성 방법, 실제 게임 화면·정상 plan/recall 미관측을 명시하는 것 확인. D 목표 질문의 UA-02/UA-04 수정 확인.
- 최종 graph: 3,516노드 / 13,198간선 / 입력 254개. SHA-256 `3aa0e230f2752207b42f859cf4c83022bf9fa4b3c2819ce4b5771dcd4e693196`.

기계 판정은 `final-results.json`, 실행별 결과는 `cli-results.json` 및 `cli-*.txt`, 독립 경로·오류 주입은 `traversal-results.json`에 있다. 이 판정은 전체 게임 실행 완료라는 의미가 아니다.
