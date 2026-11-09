# 이동·조건수면 네 범위 보완 완료

**4범위를5레코드로 보완하고 정본과 프로젝트 정상 조회에 반영했다.** 원문·생성규칙 및 게시물·프로젝트 소비의 별도 독립 검수 모두 PASS이며 열린 지적은 없다.

| 범위 | 보완한 판단 연결 |
|---|---|
| Grapple | 발동 후 curve 기반 당김, 타이머·거리 조건의 종료와 Walk 요청 |
| 탑승동물 | 안전 하차 후보의 지면·충돌 검사, 실패 시 유지, Exit 재검사 |
| 광차 | 정확6551 귀속, 선로·역입력 제동·force와 실제 하차 gate. 하차 검사는 해당tick 감속보다 먼저 |
| 조건수면210/322 | 각각의 진입·회복·해제와 Sleep+210 이동 억제를2레코드로 구분 |

현재 정본은 **260수용묶음·2,542case·251,477노드·350,373간선**이다. 기존256판251,417노드·350,275간선을 정확히 보존해60노드·98간선을 추가했다. 기존 원본·주장·이전10범위 보완과 환경음 제외 정책도 유지했다. 기록 단위는 고유 행동 수와 구별한다.

직접 source binding은8373/8374/6551/9609의4개뿐이다. Mount와322는 source-free typed 소비이며 기존 소비자에서 조회한다.9609가322의 공급자라고 주장하지 않았다.

프로젝트 정상 behavior→주제→정확 game:ID→show/source에서도 새5기록이 보인다. 기존3개 주제의5label과 참조·입력핀만 갱신했다. 프로젝트는27,020노드·49,596간선·19,127정확참조다. 기존 데이터 의미는 보존하고, 정확4노드·3간선의 todo.md 전체파일SHA만 현재 값으로 최신화했다. runtime validation은 불변이다.

해당 TODO 완료 후 마지막 product-build와 check/--check를 마감했다. todo SHA는 790d836d5d7493658f1a6417a2fda7a5ae3c76b1046b1ae78e98cf8bf043f5da, 최종 project graph SHA는 ffefb5437aa013ca28edc1e36f3306fa061d8416691a1edb85eb9cf6ee1204c다. check는 ok=true/stale=[]/structuralErrors=[], --check는 byte-identical이다. 이후 다른 TODO 변경은 새 stale로 구별한다.

| 완료 근거 | 결과 |
|---|---|
| [원문·생성 독립 검수](review/source-report.md) |5payload·원본귀속·source-free 경계·selector/재사용 관계 PASS |
| [게시 검증](check-published.json) |같은 계약/receipt의 별도 두 accepted 생성·게시3SHA 일치, 실제2,758물리핀·기존256실물 보존 PASS |
| [후보 영향 검증](verification-candidate-v2/summary.json) · [기본 조회](verification-published/summary.json) |동일 graph/coverage의 기존15출력은 재사용하고 target4만 새 실행. 기본canonical6회 PASS |
| [프로젝트 소비](consumer/verification.json) |17실제 정상조회 보존, 최종 TODO 후 정확참조 재대조·check1회 및 --check PASS |
| [게시·프로젝트 별도 독립 검수](review/post-publication-report.md) |신규60origin·모든정확참조·757project입력핀·G6+project6실제조회와 최종metadata 갱신 PASS |
| [기계 요약](verification-summary.json) |정확 계약·게시 해시·실행/재사용 구분·근거 링크 |

runtimeTested=false, wholeScopeComplete=false, 새 sourceComplete=false를 유지한다. 실제 도착·하차·수면/회복 성공, 전체 collision/physics·광차 회전 계획·종별 authoring·322공급자 전수와 기존 원본 경계는 별개다. 제품 runtime·모드·세이브·원본·context.md를 수정하거나 실제 게임/모델을 호출하지 않았다. 전체 행동 공백0을 주장하지 않는다.
