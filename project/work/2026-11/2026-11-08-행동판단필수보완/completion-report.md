# 판단에 필요한 열 범위의 정적 연결 보완 완료

**기존 여섯 후보와 추가 네 범위, 총10범위를12레코드로 보완하여 기본 정본에 반영했다.** 아이템/행동에서 발동 조건·비용·입력·대상 선택·실패/종료·후속 결과로 이어지는 연결을 추가했다. 원문·생성규칙 독립 검수와 실제 게시물 별도 독립 검수 모두 PASS이며 미해결 검수 finding은 없다.

목적은 AI의 상황 판단과 행동 수행에 필요한 정확한 근거를 보완하는 것이다. 새 선택 전략이나 실제 성공을 만들어 기록하지 않았다. 이번 end-to-end 확인은 **고정 원문·원본 → 검수된12레코드 → 정확 계약 → 생성된 정본 → 기본 개발용 CLI 소비**이며, 게임에서 AI가 행동을 성공했다는 검증은 아니다.

## 어떤 판단·수행 공백을 막는가

| 범위 | 이번에 연결한 내용 | 막는 오판 또는 누락 |
|---|---|---|
| Grapple8373/8374 | offhand 발동, aim 선택, 직접 요청, cooldown, NothingHit/Walk | 장착·입력 통과를 이동 성공으로 간주하거나 실패 후 비용을 놓치는 판단 |
| Lightning9135 | 최초 표적·chain buffer·실제 wall flag, 조건부 공격과 기존 beam/비용 소비 | 표적 선택만으로 피해가 들어갔다고 판단하거나 장애물/공격 gate를 생략하는 판단 |
| Effect58 | 두 Execute의 query/order, 버튼 입력 억제·방향 복사 조건 | 사용 가능한 장비라도 현재 입력이 억제되는 이유를 놓치는 판단 |
| 6839 Condition39→Effect2 | 초기값·부호·레벨 변환과 해당 개체 자신의 nonplayer 이동속도 소비 | 다른 피해자에게 주는 감속으로 오인하거나 기존 건강/전리품 근거로 속도를 추측하는 판단 |
| 3101 Condition102→Effect59 | 고유 range9222 발사체와 상속·확률·수신 stun 조건 | 다른 펫의 생산자 근거를 빌리거나 모든 명중이 기절을 일으킨다는 판단 |
| 1355/5875/5942/5953 Condition206 | EffectNone과 별개의 direct-ID aura 등록·반경·진영·수신·timer 제거 | None을 무효로 처리하거나 범위/면역/등록·갱신 방향/타이머 guard를 생략하는 판단 |
| 번식 후속 | 실제 partner/양쪽 Meals 재검증, 자식 생성 요청·Meals reset·변형 선택 | Breed 상태 진입만으로 출생했다고 보거나 소모한 식사량을 재사용하는 판단 |
| Dash8340/8341 | offhand gate·방향·timer·현재 값에 따른 force·충돌 보정·종료 Walk | 장착을 발동으로 보거나 고정 거리·무적 이동·무제한 대시를 가정하는 판단 |
| Detonator8330/8331 | 소유 tracked minion flag, death-effect 조건과 현재 offhand 배율, 폭발 피해 설정 | 다른 소유자 소환수를 대상으로 잡거나 일반 해제를 폭발 성공으로 보는 판단 |
| 사망·부활 lifecycle | 모드별 무덤/인벤토리 이전, armor 비용, 부활 gate/timer·HP/허기/상태 요청 | 모드별 손실·복귀를 혼동하거나 정확히15% 비용·즉시 부활을 가정하는 판단 |

사망·부활은 하나의 lifecycle 범위를 무덤/이전·방어구 비용·부활3레코드로 나눴다. 다른9범위는 각1레코드다. 12레코드는 schema상 action_completion8·condition_consumer1·conditional_relation3이며 고유 게임행동12개를 뜻하지 않는다. [여섯 범위 생산 근거](six/report.md), [네 범위 생산 근거](four/report.md)를 따른다.

## 기본 조회와 정본

- `target`의 `essentialDecisionEvidence.decision`에서 원문에 있는 조건·비용·flow·결과·실패·경계를 직접 읽는다. `nextQuery`는 정확한 `show`/`source`를 안내한다.
- source가 없는 Effect58 지원과 플레이어 사망3레코드는 기존 소비자의 `has_essential_supplement`로 연결했다. 임의 source나 실제 runtime DB primary를 만들지 않았다.
- 새 자료는 조건부 정적 근거다. `conditional=true`, `sourceComplete=false`, `runtimeTested=false`, `wholeScopeComplete=false`를 유지한다. 번식 source authoring은 독립 확인한 Cow1300:0 대표만 직접 귀속하며 다른 종별 authoring을 자동 승인하지 않는다.
- 기본 정본은256수용묶음·2,539case·251,417노드·350,275간선이다. 직전246판의251,264노드·350,068간선 전부를 정확히 보존해153노드·207간선이 추가됐다. 수용묶음·case·역할행은 서로 다른 기록 단위다.
- 기존3,235원본·5,880주장, 과거 이력·계약·원문 추적성, 환경음13역할 분류/상세82노드 제외 정책은 유지했다. 미적용 전수분리 제안도 적용하지 않았다.

현재 기본3파일 SHA256:

| 파일 | SHA256 |
|---|---|
| graph.json | `94ab86d02ae1c6b2d276e72fa1f3bb6eafac26a1a44618d9400a20b45e8e8120` |
| coverage.json | `2cde1441e312e8078d608df4e7795e062d95866bc5a9e3a0d755d1c90bf77c73` |
| manifest.json | `98377f972972c5f3dbcd555ae0c9e42ed5421eb559992d6fd4d45db3d5bf3870` |

`publication.json` SHA256은 `bbd5ba883a1886228ebcc070e7f1b974892dfe59d9894b4ff546cd0e3f0ae8bd`, 정확 계약 SHA256은 `a35eb25f02f4f64223542126eb7be64d09d4c8ad8cb16841fce50fd8bf10753f`다. [현재 조회 안내](../../게임행동-그래프/README.md)도 최신 판과 의미에 맞췄다.

## 완료 근거

| 확인 | 실제 결과·근거 |
|---|---|
| 원문·생성규칙 독립 검수 | [report](review/report.md), [findings](review/findings.json) PASS. 고정 코드·원본12레코드 대조, 두 producer 별도 재실행 exact byte 일치, 원본 condition table3항목 재추출 교차확인, 자체54 CLI |
| 후보 조회·보존 | [후보 검증](verification-candidate/summary.json) PASS. actual50 CLI, 신규153origin pointer·정규화 값 일치, 기존 모든 노드/간선 보존 |
| 두 accepted 재생성 | [final-generation](final-generation.json)의 gen.values 호출과 [installation](installation.json)의 별도 gen.values 호출이 같은 계약/receipt에서 동일3SHA 생성. 실제 게시본도 같음 |
| 최종 물리 입력 | [check-published](check-published.json) PASS. 현재 publication.json을 포함한2,719물리핀의 hash/bytes 전부 일치, 직전246판 실물 바이트 보존 |
| 실제 기본 소비 | [canonical 조회](verification-published/summary.json) PASS. 열 범위 각 대표와 show/source를 기본 graph.py로 실제12회 조회. 조건·비용·실패·후속 결과와 source-free2edge 경로 확인 |
| 별도 게시물 독립 검수 | [게시 실물 report](review/post-publication-report.md), [판정](review/post-publication-findings.json) PASS. actual2719핀·oldraw/projection 보존·153origin·새상태/receipt·대표 canonical9회·최종해시 불변 확인 |

기존246 raw history3파일을 정확 SHA 고정 입력으로 재사용했다. true raw인지 검사하고 projection이 기준246 graph와 같은지 먼저 확인한다. 기존 publication 계약·영수증·원문을 덮어쓰지 않았다. 같은 accepted 입력을 별도 두 실행으로 재생성한 증거가 있으므로 동일한 두 재생성을 추가 반복하지 않았다. [기계 요약](verification-summary.json)에 전체 수용 근거를 모았다.

## 별도로 남는 경계

이번은 합의된 열 범위의 정적 보완이다. 앞선 조사에서 미확정으로 남긴 광차/탈것 등 A·다른 C 후보, 강제 수면, 최초 코어 후속은 이번 완료 범위로 승격하지 않았다. Grapple의 전체 이동·충돌 후속, Effect58 공급자 전수, 종별 번식 authoring 전체를 새로 전수조사하지 않았다. 제품4600 실체, 일반1356 생산자 귀속,3187 marker·generic UI operand 등 기존 원본 경계도 그대로다.

실제 월드 상태·권한·슬롯·DB 선택·ECB/network/timer 결과, 실제 게임/AI/모드 연결과 성공은 별도다. 게임·제품·모드·세이브·context.md를 바꾸거나 실제 게임/모델을 호출하지 않았다. 전체 행동 공백0 또는 사람처럼 플레이 완성이라고 주장하지 않는다.

후속 할 일에서 이번 완료로 표시할 것은 기존6후보 및 추가4범위의 정적 조사·보완 두 항목이다. 위 미확정 후속과 실제 게임 검증은 구별해 유지한다.
