# 독립 원문·생성규칙 검수 — PASS

정확한 계약 `a35eb25f02f4f64223542126eb7be64d09d4c8ad8cb16841fce50fd8bf10753f`와 `candidate-review-v3`의 10개 한정 범위·12개 새 레코드를 수용한다. 검수자는 생산 파일을 수정하지 않았고 검수 출력은 `review/` 안에만 기록했다. 원문 C#·raw/components.jsonl·기존 노드의 실제 의미를 직접 읽었으며 생산자 self-check를 독립 판정으로 사용하지 않았다.

검수 후보의 SHA256:

- `coverage.json`: `75ce5b614e9ef208a417f3a576e68c93df3f066cd33e35ac6f46c85f3c210620`
- `graph.json`: `5137cd2fd345dba0df09bd3866ae652f9ea94f4aeff9a82281efc47fdd358f16`
- `manifest.json`: `0a801604ffbdb7329efb124b436c6d242445f38446d6f45a19bda1edaff7cccc`

| 새 레코드 | 원문 및 의미 대조 |
|---|---|
| `grapple-input-aim-v1` | 두 hook raw entityMono/기계7/거리7·10/쿨다운0.8·0.6, 입력 상태 gate, mouse/pad 방향 차이, hit와 무관한 timer/index write 및 NothingHit의 Walk 조건을 직접 대조했다. 이동 성공을 추가하지 않았다. |
| `lightning-chain-target-v1` | 전체 Execute, ray/sphere 첫 표적 helper, chain helper를 대조했다. 초기 endpoint와 firstHitEntity 구분, 양 collision world, 후보/홉 사이 유지되는 hitWall, 최대3 chain, cooldown elapsed tick1+nonpartial 공격 조건을 보존했다. |
| `effect58-input-direction-v1` | 두 job의 실제 query/order와 Execute를 대조했다. buttonSetMask만0, 상태·reorientation·effect58 gate 뒤 facingDirection copy라는 범위가 정확하다. ConditionID58과 혼동하거나 movement zero를 추가하지 않았다. |
| `condition39-source6839-v1` | 6839 자신의 초기39, active AreaLevel7, 음수 base-50·multiplier5·seeded scaling, summary 부호 보존/하한-900, nonplayer speed20*(1+effect2/1000) 소비를 대조했다. 피해자 slow로 전용하지 않았다. |
| `condition102-source3101-v1` | 3101 raw range9222:0과 firing owner 전달을 직접 대조했다. level 재계산, inherited102→59, ranged chance/receiver58·60/지속시간 factor, damage gate 뒤 NewConditionsBuffer append를 확인했다. pet1270의 producer identity를 전용하지 않았다. |
| `condition206-four-source-aura-v1` | 4개 exact raw source와 level, 반경 override11·7, marker 활성, 등록 emitter→receiver와 갱신 receiver→emitter의 실제 faction 호출, max206·면역243·조건207, radio flag에 종속된 timer 생성/삭제, due tick 뒤 effect96 검사 순서를 대조했다. |
| `breed-downstream-cow1300-v1` | Cow1300 raw80056의 baby1304/Meals30/variation weights와 converter, Breed 진입 후 LeaveState 및 partner·양 Meals 재검증, CreateEntity 요청 후 양 Meals reset을 대조했다. null prefab 뒤에도 reset 예약이 남는 경계를 확인했다. |
| `dash8340-8341-v1` | 두 raw authoring과 cooldown, input/ItemIsBroken gate, 보존되는 initial direction, condition205 timer, 현재 offhand·speed·corner smoothing·push-out force, 만료 Walk를 대조했다. 고정 거리/면역을 추가하지 않았다. |
| `minion-detonator8330-8331-v1` | 소유 tracked minion에만 세 flag 설정, 공통 dismissal, skip/death-effect 조건, 소비 시점 현재 offhand mechanicValue 또는1, int damage/tileDamage 설정을 대조했다. 활성 시 배율 고정이나 폭발 성공을 추가하지 않았다. |
| `player-death-grave-inventory-v1` | health/state gate, 모든 mode의 grave 요청, casual 무이전/일반 slot10/hardcore slot0, MoveInventory 유효성·IgnoreTransfer·occupied destination·source clear 조건을 대조했다. object6002는 대상 문맥이며 catalog source를 player producer로 승격하지 않았다. |
| `player-death-armor-cost-v1` | nonhardcore server의 percentage+=0.15, armor 세 slot, damage만 지우는 보존/무적 분기, 최소flat1+round(maxDurability*누적percentage), 같은ID SetAmount 및 별도 weapon replacement 경계를 대조했다. |
| `player-death-respawn-gates-v1` | permakill gate, timer5/6와1, spawnedPlayer 1회 guard, 기존 bed/default 위치, HP/hunger70/respawn tick/면역요청, hit gate 및 Exit를 대조했다. 영구 면역/세이브 삭제/실제 복귀 성공을 추가하지 않았다. |

조건39·102·206의 테이블 매핑은 UnityPy로 설치 원본 resources.assets:27162를 재독해 확인했다. 원본 resources SHA256은 `8a1eac2f9c6eda4be2753a852bbd7139d367dae7f5d70c2a028ca15d7ef9c907`, 해당 table raw SHA256은 `ee1c78681e166cbed06c78a9ed58abe2df5725ad53d43bdb4571e8dba10c1eb7`이다. 39→Effect2/negative, 102→Effect59/inherited, 206→Effect0의 실제 entry가 기존 crosswalk와 일치했다. 관련 없는 10개 DLL load 오류도 검수 증거에 보존했다.

생성규칙은 overlay/compile_contract/verify/install_reviewed/publication config의 최종 핀 바이트로 검토했다. 두 supplier 스크립트는 출력 폴더만 review/로 바꿔 직접 재실행하여 최종 supplement bytes와 정확히 일치시켰다. four SHA256은 `defd64fa1d9cac5567968af6f9bd934726c03eb8581a54959f1883be7e0bbaa6`, six는 `9b711be20d51f665c50c0da2d7cdf990bf108f8d46405c95a1194e30c6622805`이다. 206 정밀화 이후 다른 five records의 정규화 해시도 독립 비교로 불변 확인했다.

accepted246 raw history에 decisionView가 없고 acceptedBatches=246임을 확인했다. 그 manifest에 기존 contract/selection/independent receipt가 연결되고 물리 해시가 일치하며, 기존 receipt도 정확한 계약과 report hash를 가리킨다. 해당 raw history의 projection은 baseline-246과 정확히 같았다. 후보는 기존 251,264개 노드의 속성·origin과 350,068개 간선을 전부 보존한다. 신규 origin 153개, 계약의 모든 pin, manifest 2,716개 pin의 실제 바이트·크기를 직접 확인했다. 과거 Steam metadata와 publication aliases는 기존 물리 보존 사본을 그대로 가리킨다.

게시 전 `G/publication.json` 한 경로는 예정된 config switch이므로 후보가 핀한 `P/publication.config.json`의 정확한 바이트·크기로 대조했다. 이 예외는 `independent-checks.json.plannedPublicationSwitches`에 명시했다. 게시 실물 검수에서는 실제 `G/publication.json`을 예외 없이 확인해야 한다.

독립 CLI 50회를 실행해 각 새 레코드의 show/source와 정확 payload, 해당 source의 기본 target 요약(조건·존재하는 costs·flow·results·failures·boundary), 기존 entry→새 레코드→result 경로를 확인했다. 모든 명시 entry는 graph에서 직접 2-edge로 결과에 도달한다. source를 임의 부여하지 않은 Effect58 및 사망3개는 기존 consumer/object를 show했을 때 supplement가 실제 노출됨을 4회 추가 CLI로 확인했다. 비용 필드와 conditional/sourceComplete/runtime 경계가 기본 요약에 보존된다.

receipt 검증은 격리 fixture로 실행했다. 유효 receipt는 gate를 통과했고 FAIL/pending findings, 다른 findings 계약, pending receipt, 다른 receipt 계약, 변경된 report 및 변경된 findings의 7개 부정 사례는 모두 차단됐다. 초기 검수에서 제기한 findings 자체 verdict/contract 검증, 기본 판단 payload와 costs 누락, 206 faction 방향·timer guard는 최종 동결본에서 해소됐다. 현재 수용을 막는 미해결 finding은 없다.

이 수용 대상은 pending 상태의 정확한 계약·후보·정적 근거다. pending 후보의 essential status는 pending_independent_review이며 acceptedBatches는246이다. 수용 receipt에 따른 최종 생성에서는 신규 acceptance/레코드 status와 coverage/manifest 수용 상태·acceptedBatches=256 및 review/report/receipt manifest pins가 바뀐다. 최종 accepted 게시 바이트의 canonical CLI, 두 exact 재생성 및 물리 manifest 일치, 별도 게시 실물 독립 검수가 후속 확인이다. source payload와 기존 노드/간선이 변하면 이 수용으로 대체할 수 없다.

`wholeScopeComplete=false`, `runtimeTested=false`, 새 record의 `sourceComplete=false`를 유지한다. 게임·모드·원본·기존 계약·세션 기록을 수정하거나 실제 행동 성공을 검증하지 않았다. 증거는 `independent-checks.json`, `entrypoint-show-check.json`, `original-table-check.json`, `supplement-replay.json`, `receipt-probe.json`, `206-revision-delta.json`과 각 실제 CLI 출력에 있다.
