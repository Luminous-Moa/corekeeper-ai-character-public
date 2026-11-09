# 공식 웹 근거 대조 — 2026-11-07 확인

HEAD start-job을 읽었으며 부모의 범위 합의 아래 웹·코드 읽기만 수행했다. API 실행·설치·배포는 하지 않았다. GitHub는 조회 시점 `main`이므로 고정 릴리스 보장은 없다. 코어키퍼 구현은 조사 범위 밖이다.

## 기사: 주장과 증거의 범위

[Anthropic 기사](https://www.anthropic.com/research/components-of-agent-memory)는 2026-11-06 발행이다. 메모리를 원문 기록 → 기억 노트 → 위키 같은 표현 → 세션에 주입되는 작은 상위층으로 설명한다. Dreaming은 사후에 원문을 다시 검토하여 오류를 고치고 표현·검색 경로를 개선하는 작업이다. 가중치 학습이나 대화 압축과는 구별된다.

기사의 Stainless 실험은 약 35만 파일, 평가 질문 25개다. 위키+검색은 최종 61%, 검색만은 53%, 위키만은 48%였다. 이는 원문 검색을 유지하는 보조 표현의 근거이며 검색 대체의 근거는 아니다. Pokémon 비교는 일부 단일 실행·사례 분석이다. 다른 법률 과제에서는 이득이 없거나 위키가 원문보다 커졌다. 공유 파일 동시 수정으로 사실이 유실됐고 문서별 단일 작성자로 개선한 사례도 있다. 따라서 모든 도메인·모델에서 개선되는 일반 법칙으로 해석하면 안 된다. 주입층은 작게 유지하라는 설명도 동일 기사에 있다.

## Cookbook: 실제 동작

[Cookbook](https://platform.claude.com/cookbook/managed-agents-cma-dreaming-with-a-dynamic-workflow)은 2026-11-02 발행이다. lead가 동적 workflow를 작성하고, transcript별 analyzer가 약 20개씩 묶여 실행된 뒤 organizer가 한 번 돈다. 결과는 연결된 Markdown과 `_index.md`다. 이후 업무 세션은 저장소를 읽기 전용으로 연결한다. 예제 입력은 합성 자료 11개다. 별도 50개 transcript 비용 비교에서 고정 사실 목록의 보존 비율을 언급하지만, 실제 후속 업무 정확도 개선을 검증한 평가와 같지 않다.

같은 이름으로 혼동하면 안 된다. [Dreams API](https://platform.claude.com/docs/en/managed-agents/dreams)는 research preview이며 기존 저장소와 1–100개 세션을 받아 **새 저장소**를 만든다. Cookbook의 야간 작업은 **기존 저장소를 직접 갱신**한다. 기본 검토·승인 단계가 없으며 부분 실패 전 변경도 이미 반영될 수 있다.

## 프롬프트·코드에서 확인한 조건

- [analyzer](https://raw.githubusercontent.com/anthropics/claude-cookbooks/main/managed_agents/example_data/memory_distill/prompts/analyzer_system.md): transcript와 관련 기억을 전부 읽고, 반증은 수정하며 불확실성은 낮춰 표현한다. 해당 transcript에 근거가 없다는 이유만으로 기존 기억을 지우지 않는다. 확인 시점·방법·유효 범위를 기록한다. 그러나 강제된 claim별 증거 스키마나 기계적 진실 검증은 없다.
- [organizer](https://raw.githubusercontent.com/anthropics/claude-cookbooks/main/managed_agents/example_data/memory_distill/prompts/organizer_system.md): 명백한 후속 정정이면 양쪽 날짜·출처를 남긴다. 그렇지 않으면 양쪽을 보존하고 충돌로 표시한다. 한 세션의 발언이나 검색 실패를 기존 사람의 결정에 대한 정정으로 취급하지 않는다.
- [analyzer 동시성](https://raw.githubusercontent.com/anthropics/claude-cookbooks/main/managed_agents/example_data/memory_distill/prompts/analyzer_system.md): 수정 직전 재읽기, 부분 수정, 수정 후 확인·병합을 지시한다. 이는 프롬프트 협조이며 잠금이나 원자적 병합을 보장하지 않는다. organizer는 analyzer가 모두 끝난 뒤 병합한다.
- [nightly 코드](https://raw.githubusercontent.com/anthropics/claude-cookbooks/main/managed_agents/dreaming_nightly.py): 커서는 세션 생성시각 기준이다. 성공·입력없음이면 전진하고 실패면 구간을 유지한다. [문서](https://platform.claude.com/cookbook/managed-agents-cma-dreaming-with-a-dynamic-workflow)는 당시 실행 중인 세션과 export 이후 추가 턴이 이후 야간 실행에서 누락됨을 명시한다. 코드의 상태 파일은 계정 홈 아래 하나이며 작업별 분리·중첩 실행 잠금은 없다.
- [helper](https://raw.githubusercontent.com/anthropics/claude-cookbooks/main/managed_agents/memory_distill_utils.py): 입력 경로가 붙은 analyzer thread의 정상 종료, workflow 완료, organizer 완료, 보고된 실패를 검사한다. 이는 처리 커버리지의 대리 지표이지 모든 사실의 정확성·완전성 검사는 아니다. workflow 요청 대비 차이는 측정이며 차단 조건이 아니다. 오류는 기록·정리 뒤 발생시키며 자동 원상복구는 확인되지 않았다.

## 적용 해석 — 조사자의 제안

부모가 확인한 합의는 자동 공간·사건 기록과 파생 검색 그래프 유지이며, 자유로운 교훈·관계 편집은 승인 범위 밖이다. 따라서 첫 실험은 원문 대조와 정정 후보 생성으로 제한하는 편이 맞다. 신뢰도 감점에 따른 복구 가능한 비활성화도 실제 승인 조건에 맞춰 다뤄야 한다. 교훈 추가는 별도 선택이다. 장차 자동화를 넓힌다면 claim별 출처·시점·상태, 누락 없는 처리 식별자, 단일 병합 작성자, 검증 후 반영을 별도 설계한다. 이는 Cookbook 통째 채택이 아닌 기법 차용 제안이다.
