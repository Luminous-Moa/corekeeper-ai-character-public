# 이동·수면 보강 게시 실물 독립 검수 — PASS

정확 계약 `74614162f5aafdbc594318fa5b08f93b02dc23f011cbc2a22ed6edd485d1b743`의 4범위·5레코드를 실제 설치 후 검수하여 `independent_pass`로 수용한다. 생산과 원문·생성규칙 검수에 참여하지 않은 게시 실물 검수다. 현재 G 기본 정본과 프로젝트 정상 조회의 연결을 모두 포함하며 열린 finding은 없다.

| 실제 게시 대상 | SHA256 |
|---|---|
| graph.json | `a16c73944d7bc0587640e6760d0d35388cc4ea1adb67b2e9963ecd8990de5713` |
| coverage.json | `e63c72fb61af78815273a95c35b737906a7038307089f5ddc1729df4782fa6a5` |
| manifest.json | `5e28c7fff9cb972574a20781ff47a0fa594d3c1c04d696c96265e4c2da9d75ae` |
| 현재 publication.json | `11c85252460fbb1dfea253f944bea3916f9520e7a431a5c31a1c5f7556e2820e` |

게시 3파일은 candidate-final과 같고 현재 설정은 검수된 설정과 같다. 기본 graph.py→publication.json→현재 overlay 진입 및 실제 조회를 확인했다. 현재 수용 묶음은260, action_completion은2542, 게시 노드는251,477개·간선은350,373개다. 새5레코드·4acceptance와 coverage/manifest의 보강 상태는 모두 independent_pass다.

기존256판의251,417개 노드는 속성·origin까지 정확하고350,275개 간선은 전부 남아 있다. 교체 전 보존3파일도 baseline-256과 정확히 같다. 기존raw의251,486개 노드와350,369개 간선이 보존됐고, old/new raw에는 decisionView가 없다. old raw의 projection은 계약 baseline SHA와 같으며 new raw projection의 바이트는 실제 게시 graph와 같다. 기존 원문 전수조사와 역사 전체 재생성은 반복하지 않았다.

신규 primary·field·acceptance의 origin60개를 실제 파일의 JSON pointer와 정규화 payload 해시로 전부 확인했다. 5레코드 전체 record hash와 sourceKey/objectId/variation·role도 정확하다.8373/8374/6551/9609의4직접 binding만 있으며, Mount는 source-free action_completion,322는 source-free condition_consumer다. 두 기록은 직접 source 입력 간선이 없다.3750의 확정 공급자나9609→322, 실제DB/world 성공을 추가하지 않았다.

manifest 물리 입력2,758개를 실제 해시와 크기로 전부 검사했다. 현재 G/publication.json도 실제 바이트로 대조했으며 게시 전 계획 전환 예외는 사용하지 않았다. historical aliases도 실제 보존 입력과 일치한다. source-report/source-findings를 읽고 receipt가 정확 계약·findings 실물 해시를 가리키며 findings 자체 independent_pass/정확계약/reportSHA를 검사했다. source-report SHA는6109ad51…, findings는7521207f…, receipt는235d3b68…이다.

finalize_reviewed.py와 install_reviewed.py의 실제 실행 기록을 읽었다. 동일 계약·receipt 아래 첫 accepted 생성과 설치 직전 별도 accepted 생성이 같은 게시3SHA를 낸 뒤 설치됐다. check-published.json의 같은 두 생성·실제 물리 핀 PASS와 기본canonical6회 실출력·실행 코드를 확인했다. 추가 두 번의 전체 재생성은 하지 않았다.

별도로 G 기본6회와 프로젝트 정상6회를 실제 실행했다. 출력과 hash는 post-publication-consumer-check.json 및 post-publication-cli-*.json에 있다.

- G target8373은 기존 입력/aim와 새 pull/end의 자기 decision을 각각 정확히 보여준다. show는 재사용 관계를 보존하고 자기 has_* 필드를 구분한다. timer 만료/거리<0.5의 Inactive/Walk 요청과 curve 차이의 속도 가산이 도착 성공으로 바뀌지 않는다.
- G target6551 및 프로젝트 game:ID show는 선로·역입력 감속 비용, 실제 하차 속도 gate와 실패를 보인다. 하차가 해당tick 감속보다 먼저라는 순서와250000 등호 허용이 남았다.
- G source210은9609의210만 연결하고 Sleep 진입/회복 조건·이동0·조건 소실 pop을 정확 payload로 반환한다.
- Mount와322는 각각 기존 안장/공통조건 소비자→새 record→새 result의2간선으로 도달한다. 프로젝트 game:ID source도 같은 value와 origin을 반환한다. Mount의 최초 실패·Exit 재검사,322의 진입 당시 flag·조건 소실/이동>0.1 해제를 실제 성공이나210식 무조건 이동 봉쇄로 확대하지 않는다.
- 정상 product.mjs behavior의 P.expedition-navigation→move에서Grapple, P.expedition→vehicle에서Mount/Minecart, P.life→food에서210/322의5정확 ID가 보인다.

프로젝트 마지막 TODO 갱신과 마지막 build 이후 별도 최종 읽기 검사를 다시 했다. 최종 프로젝트 graph SHA는 `fffefb5437aa013ca28edc1e36f3306fa061d8416691a1edb85eb9cf6ee1204c`, validation은 `cc993e7f2105fc3a111f91098d284fb7e45da94fb7b51affa3951877b283b7ca`, topics는 `1993a174ca3be0ce263628c449fc1bc2cf68312c7f710b3cb352c69935d945ff`다. 최종 graph는27,020노드·49,596간선이며 모든 foreign reference19,127개의 실제G id/kind/origin/sourceKey/variation을 다시 대조했다. 최종 project meta.sources757개도 실제 파일 hash와 모두 같았다. topics는 기존3행의5label 추가만 있고 link/query/integrate/runtime 등 그 밖의 source pin은 불변이다. 추가된 project 노드와 간선은 정확 game:5ID와 주제 참조5개이며 runtimeTrace=false다. graph의 다른 섹션·meta의sources외 필드와 runtime validation도 불변이다.

프로젝트 기존 데이터의 정확 보존과 출처 핀 최신화는 구분한다. 최초 엄격 비교에서 R.chapters/R.memo/R.mention/R.playtest와 E053/E055/E056의 todo.md 전체파일SHA 변경을 발견하여 즉시 PASS를 보류하고 HEAD에 알렸다. HEAD는 이 정확4노드·3간선에 한해 evidence.sha만 실제 현재hash로 갱신하고 나머지를 엄격 보존하도록 범위를 명시했다. 검수자는 이를 독립 확인했다. 기존27,015노드·49,591간선의 의미·모든 그 밖의 필드는 보존됐으며27,011노드·49,588간선은 전체값이 정확하다. 나머지4/3은 evidence.sha만 최종 TODO SHA `790d836d5d7493658f1a6417a2fda7a5ae3c76b1046b1ae78e98cf8bf043f5da`로 갱신됐다. 실제2/9/10/22행의 기존anchor를 확인했으며 title/summary/status/endpoint/type/kind 등은 그대로다. 최초delta와 보류 증거도 post-publication-project-*-delta.json 및 post-publication-consumer-initial-drift.json에 보존했다. 이 예외는 G256 보존이나 다른 project payload 변경에 적용하지 않았다.

마지막 consumer/verification.json의 PASS와 실제 final-check.json의 ok=true/stale=[]/structuralErrors=[], build-check.txt의 regeneration byte-identical을 확인했다. TODO 전 실제17조회는 verification-pre-todo.json과 출력으로 보존돼 있고, 최종화는 동일G·새참조·분류를 재대조한 뒤 final check/--check만 실행했다. 독립12조회도 최종 G3SHA·payload·분류·foreignrefs 보존과 각 실제 출력hash를 다시 확인하여 재사용했다. TODO 출처 핀 갱신만으로 동일 조회를 반복하지 않았다.

독립 최종 증거는 post-publication-artifact-check.json, post-publication-consumer-check.json, post-publication-final-consumer-check.json 및12개 실제 출력이다. 검수자 쓰기는 새P/review/post-publication*에만 한정했다. 공유 graph·topic·제품·게임·원문·session 기록은 변경하지 않았다.

runtimeTested=false, wholeScopeComplete=false, 새sourceComplete=false를 유지한다. 이번 PASS는 지정4범위의 정적 연결과 G/프로젝트 정상 조회에 한정된다. 게임 내 실제 도착·하차·회복·수면 해제 성공, 전체 collision helper·종별·322공급자 전수, AI/모델 실행 및 전게임 공백0을 검증한 것이 아니다.

