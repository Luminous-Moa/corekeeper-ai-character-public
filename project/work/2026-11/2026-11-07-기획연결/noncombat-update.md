# 비전투 연속 판단 합의의 그래프 반영

정본 [비전투 행동의 연속 판단 방향](../2026-11-07-비전투-연속판단.md)을 독립 재기획 그래프에 반영했다. 구현 완료를 주장하지 않으며, 전투 방향은 확정 관계에 넣지 않았다.

## 바뀐 관계

- `P.agency`: 위쪽 LLM의 목적·작업 계획과 중요한 변경 역할로 표현을 명확히 하고 새 정본의 근거를 추가했다.
- `P.execution`: 선택된 행동의 몸 입력·실행 역할을 명확히 했다. 넓은 구역 자동처리로 Decisions 판단 기회를 줄이는 방향으로 해석하지 않는다.
- `P.noncombat-choice`: Decisions가 갱신된 상황에서 구체 행동과 다음 대상을 계속 판단한다.
- `P.noncombat-continuity`: 판단 주기와 몸의 멈춤을 분리하여 유효한 현재 행동은 응답 대기 중에도 이어 간다.
- `P.noncombat-next`: 현재 작업 완료 전 다음 선택을 Decisions가 준비한다.
- `Q.noncombat-rules`: 호출 간격·사건 조건·비용/상한, 다음 선택 준비 시점·세계 상태 처리, 전환/중단, 선택지·입출력 세부는 미정이다.

새 4노드와 8관계를 추가해 총 **47노드(재기획 36·참고 11), 63관계, 15원문(재기획 합의 7·참고 8)**이다. README의 전체 그림과 질문표, 근거 범위를 갱신하고 `graph.json`·`관계목록.md`를 재생성했다. `graph.mjs check`의 근거 문서 수는 등록 합의 수를 계산하도록 바꾸었다.

## 합의의 경계

정본에서 사용자 합의보다 강한 고정 수치나 자동실행 범위를 발견하지 않았다. 전환 시 실행 가능성을 확인하는 세부 방식도 미정으로 남아 있다. 약 2분의 상위 LLM 주기는 현행 구조 설명이며 새 고정 정책 노드로 승계하지 않았다. 기존 LLM 계획 주도권과 비전투 Decisions 선택 역할을 상호 배제하지 않고 상위 계획과 작업 중 선택으로 연결했다.

## 검증

`node graph.mjs build` 후 `node verify-noncombat.mjs`를 실행했다. [추가 검증 결과](noncombat-verification.json)는 `ok: true`다. 아래 대표 경로·원문 조회 7건 모두 종료 코드 0이며, 핵심 경험에서 모든 47노드로 연결된다.

1. `check`: 원문 앵커·참조·참고 경계·JSON/관계목록 재생성 일치.
2. `path P.experience P.noncombat-choice`: 핵심 경험에서 비전투 구체 선택 주체까지.
3. `path P.agency P.noncombat-next`: 상위 계획에서 다음 대상의 Decisions 선택까지.
4. `path P.noncombat-choice P.execution`: 판단과 코드 실행의 역할 연결.
5. `path P.noncombat-continuity Q.noncombat-rules`: 실행 연속성과 아직 정하지 않은 전환 규칙 연결.
6. `source P.noncombat-choice`: 연속 판단 합의 원문 확인.
7. `source Q.noncombat-rules`: 남은 설계 사항 원문 확인.

기존 `result.md`, 독립 검수 보고와 `verification.json`은 당시 기록으로 보존했다. 기존 공식 지식그래프·제품 코드·context.md·세션 기록은 수정하지 않았으며 게임·모델 호출도 하지 않았다.
