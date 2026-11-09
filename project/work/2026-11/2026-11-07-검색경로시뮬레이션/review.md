# 독립 검수

2026-11-07. 검수 범위: 두 정본 합의, agreement.md, run.mjs, 합성 fixture, runs/smoke 및 runs/remaining의 여섯 실제 요청·응답, 최종 report.md와 audit.mjs/audit.json. 모델을 추가 호출하지 않았고 제품·게임·브리지 설정을 수정하지 않았다. 최종 판정은 **제한된 탐색 시험 결과로 수용**이다. 보고서의 실제 응답·시간·호출 수를 확인했으며, 아래 지적의 설명 누락은 해소되었다. 탐색 책임 경계 자체는 미정으로 보존한다.

## 핵심 판정

여섯 실행은 실제 모델 응답을 보존한다. 장면별 A/B 초기 관찰과 공통으로 조회한 결과는 JSON 대조에서 동일했다. 기대 행동·숨은 requirement·past·expected 값이 모델 요청에 직접 전달되는 경로는 발견하지 않았다. 모델에는 같은 공통 검색 어댑터를 제공하고 검색을 호출할 주체만 다르게 했다. 검색 결과에 사건 원문의 사실을 제공하는 것은 이 시험에서 의도한 근거 공급이며, 기대 행동을 모델에 노출한 것과 구분한다.

다만 s3-B의 자동 invalid는 원합의 위반으로 단정할 수 없다. 하네스는 explore 행동에 상위 plan.task=explore를 강제하지만, 모델 지시는 gather를 구리 확보로 설명하고 구체 장소·작업 내 대상 선택을 Decisions에 위임했다. 탐색을 반드시 상위 계획 변경으로 간주한다는 조건은 Decisions 지시에 명시되어 있지 않다. 따라서 이 실행은 고갈 광맥 회피와 새 탐색 선택은 적절했으며, 자동 판정의 계획 형식 조건은 미통과했고, 원합의상 책임 경계는 미정이라고 분리해야 한다. 자동 false를 삭제하거나 일반 성공으로 뒤집어서는 안 된다.

## 실제 응답과 시간 확인

| 장면 | A | B | 실제 응답 확인 |
|---|---:|---:|---|
| 정상 위치 | 18.848초, 상위 2/Decisions 1 | 9.301초, 상위 1/Decisions 2 | 둘 다 ore1 이동 선택. B의 resource_sites에도 near가 반환한 동일 등급 성공 경험 e:3이 있어 별도 history 없이 사용할 근거가 있었다. |
| 도구 부족 | 17.016초, 상위 2/Decisions 1 | 20.060초, 상위 2/Decisions 4 | A와 B 최종 계획은 prepare_tool/bench1. B는 두 조회 후 escalate하고 상위 계획 변경을 받아 제작대 이동을 선택했다. |
| 오래된 광맥 | 15.679초, 상위 2/Decisions 1 | 9.884초, 상위 1/Decisions 3 | 둘 다 두 조회 후 frontier1 탐색 선택. A는 상위 explore 계획, B는 기존 gather 계획. B 수치는 엄격한 하네스의 첫 유효 행동 시간이 아니라 탐색 후보 선택까지의 시간이다. |

여섯 원본의 totalMs와 upperMs+decisionsMs+searchMs 차이는 0.224~1.066ms로, 실행 제어 오버헤드 수준이다. totalMs는 최초 상위 호출 직전부터 모의 판정 직후까지이며 사전 fixture 구축·preflight는 제외한다. 게임의 이동 시작·도착·제작·채굴 시간은 포함하지 않는다. 정상 장면은 A→B, 나머지는 B→A 한 번씩 실행했으므로 일반적 속도 우열이나 성공률 추정 자료가 아니다.

상위 A의 최종 target_id는 각각 ore1, bench1, frontier1이며 evidence_ids의 sN:resource_sites, sN:resource_history, e:3은 실제 조회 결과에 존재한다. s2-B도 bench1과 해당 근거를 올바르게 지정했다. s1-B/s3-B의 null target과 빈 evidence_ids는 검색 전 상위 작업을 위임한 형식이며, 실제 Decisions 선택은 반환된 대상/보이는 경계에 연결된다. 잘못된 대상이나 준비 선택이 자동 valid로 통과한 실제 사례는 발견하지 않았다. Decisions API 응답에는 선택과 확률만 있어 내부 추론이나 실제로 어떤 문장을 근거로 썼는지는 검증할 수 없다.

## 발견 및 해소 확인 항목

1. **탐색의 책임 경계 — 보고 표현 해소, 제품 경계 미정 유지.** report.md는 s3-B 원본 false를 보존하고 목표 적합/상위 enum 불일치/역할 경계 미정을 구분한다. 9.884초를 확정 계약에 따른 실행 수락 시간으로 사용하지 않아, 실패 은폐나 원합의 위반 단정 문제는 없다.
2. **공통 시험 어댑터 설명 누락 — 해소.** resource_sites는 near에 합성 좌표/관측 나이를 보충한다. resource_history는 goal 조회뿐 아니라 events.jsonl의 원본 cause·관측 조건·outcome을 보충한다. 특히 정확한 도구 등급과 고갈 사실은 이 cause에 있다. 수정된 fixture-notes와 report.md가 양쪽 모두 시험 어댑터를 썼으며 현행 완성 API가 아니라는 점을 명시한다.
3. **자동 판정의 대상·근거 검사 한계 — 실제 결과 수동 확인 완료.** accept()는 plan.task와 조회 존재를 검사하지만 target_id와 evidence_ids의 유효성까지 검사하지 않는다. 위 실제 응답 감사에서는 잘못된 대상 통과를 찾지 못했다. 이후 확장 시험에서는 자동 valid와 근거/대상 적합성 감사를 분리해야 한다.
4. **B의 상위 검색 가능성 — 실제 결과 확인 완료.** 코드상 재상신 후 상위 검색이 가능하다. 이번 실행에서는 s2-B가 재상신했으나 상위가 추가 검색하지 않았고, 실제 B의 모든 검색은 Decisions가 요청했다. 상위 계획 변경과 상위 검색을 혼동하지 않는다.
5. **시험 규모와 성공 의미 — 보고서 확인 완료.** 광맥·제작대·미탐색 경계가 각각 하나인 제한된 합성 장면이며 첫 행동 선택까지만 검사한다. 여러 대상 비교, 게임 길 찾기, 목적 완수, 연속 실행, 기억 신뢰도 갱신은 시험하지 않았다. 보고서는 단일 표본·합성 기록·실게임 미실행·제품 전체 프롬프트와 다른 조건을 명시하고 일반적 우열을 주장하지 않는다.

최종 audit.json의 상위 10회/Decisions 12회/검색 11회, 토큰 합계와 report.md의 표를 원본 기록과 대조했다. audit.mjs는 저장된 호출만 읽으며 네트워크나 모델 호출 경로가 없다. 감사는 ID의 당시 공개 여부와 최종 대상 일치 여부를 확인하는 보완이며 의미적 정확성 전체를 자동 보증하는 도구로 해석하지 않는다. 첫 묶음 설정 BOM 처리 문제도 보고서에 공개되어 있고, 실제 요청에 기록된 Decisions 모델은 현재 운영 설정의 gpt-6-luna와 일치한다.

## 근거 파일

- 하네스: [run.mjs](run.mjs), [agreement.md](agreement.md), [fixture-notes.md](fixture-notes.md)
- 최종 보고 및 사후 감사: [report.md](report.md), [audit.mjs](audit.mjs), [audit.json](audit.json)
- 원본 정상 장면: [s1-A](runs/smoke/s1-A.json), [s1-B](runs/smoke/s1-B.json)
- 원본 도구 부족: [s2-A](runs/remaining/s2-A.json), [s2-B](runs/remaining/s2-B.json)
- 원본 낡은 광맥: [s3-A](runs/remaining/s3-A.json), [s3-B](runs/remaining/s3-B.json)
