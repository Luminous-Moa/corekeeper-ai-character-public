# 게시 실물 독립 검수 — PASS

실제 설치 완료 통지를 받은 뒤 기본 `게임행동-그래프/published`, 현재 `publication.json`, 정본 진입점 `graph.py`와 그 기본 조회를 검사했다. 정확한 계약과 10개 한정 범위·12개 레코드의 게시 실물을 수용한다. 생산·원문·생성규칙 검수와 분리된 최종 게시 검수이며 미해결 finding은 없다.

| 대상 | 실제 SHA256 |
|---|---|
| 계약 | `a35eb25f02f4f64223542126eb7be64d09d4c8ad8cb16841fce50fd8bf10753f` |
| 게시 graph.json | `94ab86d02ae1c6b2d276e72fa1f3bb6eafac26a1a44618d9400a20b45e8e8120` |
| 게시 coverage.json | `2cde1441e312e8078d608df4e7795e062d95866bc5a9e3a0d755d1c90bf77c73` |
| 게시 manifest.json | `98377f972972c5f3dbcd555ae0c9e42ed5421eb559992d6fd4d45db3d5bf3870` |
| 현재 publication.json | `bbd5ba883a1886228ebcc070e7f1b974892dfe59d9894b4ff546cd0e3f0ae8bd` |

게시 3파일은 `candidate-final`과 바이트 해시가 같고 현재 설정은 검수된 설정과 같다. `graph.py`가 현재 설정의 generator를 읽어 필수 보완 overlay로 연결됨을 코드를 읽고 실제 기본 조회로 확인했다. 신규 12개 레코드와 10개 acceptance 노드, coverage/manifest의 필수 보완 상태는 모두 `independent_pass`다. 게시 수용 묶음은256, action_completion은2539이며 게시 노드는251,417개, 간선은350,275개다.

기존 246 수용 묶음의 251,264개 게시 노드 속성·origin은 전부 정확히 보존되고 350,068개 간선도 모두 남았다. 교체 전 3파일 보존 사본은 baseline-246과 해시가 같다. 기존 raw history에는 decisionView가 없으며 251,333개 raw 노드와 350,162개 간선이 새 raw history에 그대로 남았다. 기존 raw projection의 정확 해시는 baseline 계약과 같고 새 raw projection의 바이트는 현재 게시 graph와 같다. 과거 조사 전수 재생성은 수행하지 않았다.

신규 origin153개는 포인터를 실제 파일에서 다시 열어 실제 payload의 정규화 해시와 정확히 일치시켰다. 12개 레코드의 계약상 전체 record 해시도 같다. sourceKey별 실제 objectId·variation과 roleBindings를 대조했다. Effect58은 condition_consumer, 조건39·102·206은 conditional_relation, 사망3개는 action_completion으로 남았다. Effect58·사망3개는 sourceBindings가 비어 있고 직접 source 입력 간선도 없다. object6002는 무덤 생성 대상 문맥으로 노출되며 runtime player의 출처로 승격되지 않았다.

실제 manifest의 물리 파일2,719개를 전부 해시와 크기로 다시 대조했다. 현재 `G/publication.json`도 실제 바이트로 검사했으며 게시 전 예정 전환 예외를 적용하지 않았다. 기존 historical alias는 보존된 실제 입력으로 검사됐다.

원문·생성규칙 검수의 `report.md`와 `findings.json`을 읽고 영수증이 exact contract 및 findings의 실제 해시를 가리키는지 확인했다. findings 자체 `independent_pass`, 정확한 계약, 보고서 실제 해시가 일치한다. 사전 검수에서 해결된 receipt 검증·기본 비용 노출·206 방향/타이머 경계가 동결 입력에 반영돼 있다.

`final-generation.json`과 `installation.json` 및 각각의 실행 코드를 근거로 같은 계약·영수증 아래 두 번의 accepted 재생성과 정확 게시 3SHA 일치를 확인했다. `check-published.json`과 `verification-published/summary.json`의 실제 검증 결과 및 실행 코드를 읽었다. 후자는 10개 범위 대표와 show/source를 합한 기본 canonical12회 통과를 기록한다. 전체 재생성을 중복 실행하지 않았다.

별도로 기본 `graph.py`를 실제9회 실행했다. 출력은 `post-publication-cli-*.json`, 검사 요약은 `post-publication-cli-check.json`에 있다.

- grapple8373의 target/show/source/path: 조건·costs·flow·결과·실패·경계가 정확한 수용 payload로 노출됐다. hit 실패에도 먼저 시작될 수 있는 cooldown0.8/0.6초, NothingHit/Walk, 요청과 실제 이동 성공의 차이가 남았다. 경로는 source→해당 completion→해당 result의2간선이며 무관한 loot 경로가 아니다.
- 3101 target: 자체9222 발사체 caller와 inherited102→59, 현재 range cooldown 공식, receiver stun 조건과 요청 경계가 보였다. pet1270 공통 소비자를 재사용하면서 producer identity를 빌리지 않았다.
- beam show 및 Effect58 result path: 기존 행동에서 source-free consumer로 진입해2간선으로 결과에 도달했다. 공급자나 실제 성공을 추가하지 않았다.
- object6002 show 및 death armor source: source-free lifecycle 보완이 기존 진입점에 노출됐고 armor 비용은 누적percentage에 대한 반올림+최소flat loss로 유지됐다. 정확15% 또는 재고 반영 성공으로 단정하지 않는다.

검수 증거는 `post-publication-artifact-check.json`, `post-publication-cli-check.json`과 그9개 실제 출력이다. 검수자가 작성한 파일은 이 폴더의 `post-publication*`뿐이다. 게임·모드·세이브·원본·생산 파일·세션 기록은 변경하지 않았다.

`runtimeTested=false`, `wholeScopeComplete=false`, 신규 기록 `sourceComplete=false`를 유지한다. 이번 PASS는 지정10범위의 정적 게시 연결과 기본 조회에 대한 수용이며 게임 내 실제 행동 성공, AI 정책/모델 호출, 전 게임 완전성의 검증을 뜻하지 않는다.

