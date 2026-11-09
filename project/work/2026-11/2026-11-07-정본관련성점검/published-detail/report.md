# 현재 게시본 관련성 독립 상세 검토

결론: 기본 정본에 행동 판단과 무관한 렌더 구현 세부가 일부 남아 있다. 세 대표 혼합 행에서 확정했다. 이 결과는 전체 내용의 불필요 여부를 전수 의미 판정했다는 뜻이 아니다. 원본/그래프/정책/생성기 수정, 재수집과 전체 재생성은 하지 않았다.

## 기준과 고정 입력

기본 graph.py가 읽는 published/graph.json을 검토했다. 루트 graph.json은 과거 고정 입력으로 취급했다. 게시 graph SHA-256은 dd94872b41160ce8e05a04a48a056ff25e2bbd5c5f91ebdf8259eb213379f0ca이며 시작·종료 hash가 같다. 게시 coverage/manifest와 graph.py도 시작·종료 불변이다. 입력 상세는 [input-hashes.json](input-hashes.json), 결과 전체는 [evidence.json](evidence.json).

판단 기준은 scope-realignment/00-purpose-and-scope.md와 relevance-policy.json, 게시 README다. 관측 신호·행동 조건/비용/시간/결과/실패/대안/실행 지원은 유지하며, 그 의미를 바꾸지 않는 렌더 세부만 분리 후보로 판정했다. 코드 근거·출처 hash·그래프 메타데이터 자체를 불필요로 판정하지 않았다.

## 확정 잔존: 최소 분리 단위

### 1. CrudeDrill / 6656 variation1

노드: completion:full-d:powered-presentation:supp:A::CAB-2ebcba97367158c9b08f790c25d642c9:-1017436649789838430#animate_powered_drill

원문: ../2026-11/2026-11-07-행동결과완결/full/D/powered-presentation-helper/output2/cases.json #/cases/0

- 필드 노드 completion:full-d:powered-presentation:supp:A::CAB-2ebcba97367158c9b08f790c25d642c9:-1017436649789838430#animate_powered_drill/condition/conditions 의 /value/0; 원문 pointer /cases/0/conditions
- 필드 노드 completion:full-d:powered-presentation:supp:A::CAB-2ebcba97367158c9b08f790c25d642c9:-1017436649789838430#animate_powered_drill/flow/transitions 의 /value/0/text; 원문 pointer /cases/0/transitions
- 필드 노드 completion:full-d:powered-presentation:supp:A::CAB-2ebcba97367158c9b08f790c25d642c9:-1017436649789838430#animate_powered_drill/result/outcomes 의 /value/0/text; 원문 pointer /cases/0/outcomes

확정 불필요: idle/active animation integers -1949102368 / 1260321794; rendering-only X scale -1 and loop fade/stop handling. These exact implementation constants do not affect the available action, power gate, mining damage, loot, or the meaning of an observable powered/working signal.

유지할 맥락: Keep powered/idle signal and its electricity condition; retain direction, whether powered collision creates particles, and separation from damage/mining/loot. Precise particle query offset/timer/mask is not counted as confirmed unnecessary in this audit because observation conditions can require detail.

최소 분리 제안: Separate only animation integer IDs, XScale implementation and loop lifecycle clauses from the mixed field. Keep signal conditions and existing mining/damage/loot relations. Apply consistently to rule:supp:A:drill_visual_load and source-specific duplicate fields rather than removing the source or whole role.

기본 CLI 증거: [drill-target.json](drill-target.json) → [drill-show.json](drill-show.json) → [drill-source.json](drill-source.json). 정확한 명령 인자는 [cli-records.json](cli-records.json)에 있다.

### 2. AquariumBeach / 202 variation0

노드: completion:full-d:minisim:CAB-2ebcba97367158c9b08f790c25d642c9:-1485742008041264056

원문: ../2026-11/2026-11-07-행동결과완결/full/D/minisim-v2/cases.json #/cases/7

- 필드 노드 completion:full-d:minisim:CAB-2ebcba97367158c9b08f790c25d642c9:-1485742008041264056/flow/transitions 의 /value/2/text; 원문 pointer /cases/7/transitions
- 필드 노드 completion:full-d:minisim:CAB-2ebcba97367158c9b08f790c25d642c9:-1485742008041264056/result/outcomes 의 /value/0/text; 원문 pointer /cases/7/outcomes

확정 불필요: tableItemsLists[0].tableItems[index].transform.localPosition = (x, y-0.2, -0.59+y*0.02) and localScale.x = -1/1 from flipX. The depth adjustment and sprite flip scaling are local rendering writes; no game movement, item change, risk, cost, execution gate or interaction outcome is established by these clauses.

유지할 맥락: Keep source identity, contained-item/preview relationship and any meaningful display capacity or runtime observation limits. Inventory open/transfer and placement roles remain separate. The whole minisim family is not declared unnecessary.

최소 분리 제안: Separate the local transform formula and flip scale clauses from transitions[2].text and outcomes[0].text. Keep a concise observation relation and necessary validity conditions; archive detailed rendering evidence through an explicit history/developer view.

기본 CLI 증거: [aquarium-target.json](aquarium-target.json) → [aquarium-show.json](aquarium-show.json) → [aquarium-source.json](aquarium-source.json). 정확한 명령 인자는 [cli-records.json](cli-records.json)에 있다.

### 3. AncientWire / 6502 variation0

노드: completion:full-d:ancient-electricity:CAB-2ebcba97367158c9b08f790c25d642c9:-7658880460584646847

원문: ../2026-11/2026-11-07-행동결과완결/full/D/ancient-v2/cases.json #/cases/3

- 필드 노드 completion:full-d:ancient-electricity:CAB-2ebcba97367158c9b08f790c25d642c9:-7658880460584646847/flow/transitions 의 /value/2/text; 원문 pointer /cases/3/transitions

확정 불필요: 36x24 rendering texture region, texture Apply dependency completion and Shader.SetGlobalTexture(ElectricityTex, electricityTex) binding. These renderer upload details do not change ancient-electricity propagation, blocking, source energy, state update or action execution.

유지할 맥락: Keep sourceEnergy/electricityAmount, blocking and neighbor propagation, ordinary ElectricityCD distinction, and the observable relationship of positive electricity state to the green signal. Exact RGB byte formula is not independently counted as unnecessary because fidelity required by visual interpretation has not been settled.

최소 분리 제안: Split the texture dimensions/upload/global binding clauses from transitions[2].text. Keep transitions[0:2] component and propagation behavior and the meaningful green-signal gate. Retain evidence pointers and source integrity; no blanket Shader/camera exclusion.

기본 CLI 증거: [ancient-target.json](ancient-target.json) → [ancient-show.json](ancient-show.json) → [ancient-source.json](ancient-source.json). 정확한 명령 인자는 [cli-records.json](cli-records.json)에 있다.

## 원코드와 실제 노출 확인

기본 target → show → source를 세 대표에 각각 실행한 9명령은 모두 exit 0이다. --include-history 또는 별도 graph-dir을 쓰지 않았다. target이 정확 source/variation과 관련 completion을 반환하고, show와 source에서 위 문구가 실제 노출된다. target 6656 variation1은 두 source 후보를 반환하므로 sourceKey가 -1017436649789838430인 CrudeDrill을 정확히 선택했다. 수조는 전체 sourceKey로 조회해 AquariumBeach 202 variation0을 고정했다.

원코드 파일을 직접 읽고 각 수용 근거 SHA와 일치함을 확인했다. Drill.cs 108/117의 애니메이션 정수와 111의 FadeOutAndStop, Aquarium.cs 8의 위치 보정식과 13의 flip scale, ShaderTexturesFinalizeSystem.cs 103~106의 Apply/global binding이 해당 근거다. [original-code-evidence.json](original-code-evidence.json)에 코드 위치·원문·실측/기대 hash를 기록했다. 이 hash 확인은 기존 수용 검수를 반복한 것이 아니라 세 발견의 원문 근거 확인이다.

## 유지 및 모호 후보

- 로봇 팔 FilteringUI의 전력 아이콘·pulse/color는 전력 부족 신호이므로 유지한다.
- waypoint 거리 계산의 카메라 위치는 실제 활성화 거리 조건에 들어가므로 유지한다. camera라는 이름으로 제외할 수 없다.
- 전력 손실 시 deactivation timer와 collider swap은 이동·상호작용에 영향을 주므로 애니메이션과 함께 있다는 이유로 제외하지 않는다.
- serialized onUse는 실제 호출 연결 근거다. source/variation, pointer/hash와 미검증 경계도 유지한다.
- 드릴 파티클의 전력·충돌 조건과 구체 query offset/timer/mask는 정확 관측 조건으로 필요할 수 있다. 이번 확정 불필요 수에 넣지 않았다.
- 고대 전력 RGB 수치 곡선은 시각 관측 정밀도 목적과 관련될 수 있어 확정 제외하지 않았다. 텍스처 업로드/바인딩만 확정했다.
- 악기 성공 소리·상호작용 효과는 행동 자체 결과나 성공 신호일 수 있어 일괄 제외하지 않았다. UI 설정/저장/전송 후보에서도 이름만으로 순수 내부라고 확정하지 않았다.

정확 유지 대표 필드와 모호 후보는 evidence.json에 있다. 이미 분리한 환경음13의 역사 상세82개는 이 감사의 잔존 문제로 세지 않았다.

## broad scan 범위와 한계

게시 137,627노드의 문자열 필드를 재귀 순회해 8범주 텍스트 후보를 추렸다. id/origin key는 후보 검색에서 제외했고, 나머지 메타데이터/근거 내용에는 이름 false positive가 섞일 수 있다. 210,168개 edge는 세 대표 경로 확인에 사용했으며 모든 edge의 의미를 재판정하지 않았다.

| 범주 | 일치 문자열 필드 | 일치 노드 |
|---|---:|---:|
| particle | 1488 | 317 |
| color | 88 | 71 |
| animation | 1374 | 772 |
| sound | 546 | 520 |
| camera | 147 | 91 |
| ui | 7458 | 6283 |
| serialize | 3298 | 1750 |
| helper | 224 | 147 |

범주 간 중복을 제거한 후보 노드는 9314개다. 이 숫자는 불필요한 노드 수가 아니다. [candidate-scan.json](candidate-scan.json)에 후보 목록을 보존했다. 반복 근거와 혼합 행을 보고 대표 3군을 깊게 읽었으며, 전체 2,460case와 모든 원코드를 의미상 재검수하지 않았다. 키워드 누락·동의어·다른 혼합 세부가 남을 수 있다. 확정 결과는 세 대표에서 불필요한 하위 문구가 실제 기본 조회에 남아 있다는 하한이다. 그래프 전체가 현재 AI에게 통째로 제공된다거나 실게임 실행 검증이 끝났다는 결론은 내리지 않는다.
