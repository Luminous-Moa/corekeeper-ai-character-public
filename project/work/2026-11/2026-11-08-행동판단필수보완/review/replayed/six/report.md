# Six bounded static supplements

현재 정본은 immutable `baseline-246/graph.json`과 대조했다. 원문·게임·제품·게시물 변경 없이 supplement6개를 작성했다. `runtimeTested=false`, `wholeScopeComplete=false`이다. 독립 원문·생성규칙 검수 및 게시/기본CLI 검증은 통합 담당의 후속 단계다.

| 범위 | 추가 | 재사용 | 판단 공백 방지 |
|---|---|---|---|
|8373/8374|offhand입력·상태·aim·직접grapple요청·실패/Walk·선행cooldown|기존equip/swap|From either hook item, distinguish a usable input/aim request, cooldown rejection and NothingHit from a successful traversal; gear equip alone cannot provide that distinction.|
|9135|첫target/aim/chain buffer와 실제wall flag·조건부공격|기존beam·cost/health 소비|From9135, expose aim-only and chain-position results separately from cooldown-gated attacks and reused beam costs, preventing a selected chain from being treated as damage already dealt.|
|Effect58|정확한두Execute query/order·buttonmaskclear·directioncopy조건|기존조건buffer와pet1270공통소비·beam입구|Existing beam/condition consumers can expose why held-button actions or direction changes are suppressed despite an action being otherwise available; no invented Effect58 supplier is needed.|
|6839 Condition39|정확초기값·signed level변환·source自身speed쓰기|공통initial/summary소비|From6839, expose its own signed slow and matching movement write so behavior is not inferred from unrelated health evidence or incorrectly assigned to its victims.|
|3101 Condition102|고유range9222caller·inherited102→59·수신stun요청|공통inheritance/stun·NewConditionsBuffer|From3101, expose its own ranged projectile and receiver stun predicates instead of borrowing pet1270’s producer identity or assuming any hit must stun.|
|4source Condition206|HasAura활성·등록·반경/진영·max206→207·제거|공통initial/summary·기존victim면역/tick|From each of1355/5875/5942/5953, expose the direct-ID emitter, radius/receiver/immunity and removal gates so EffectNone or victim-only damage evidence cannot hide the actual aura path.|

원문 대조의 중요한 경계:

- Grapple 전체 이동/충돌 후속은 이 범위 밖이다. 입력 통과와 field write는 이동 성공이 아니다. failed hit에도 cooldown/timer/index가 이미 기록될 수 있다.
- Lightning의 hitWall은 후보별로 초기화되지 않는다. 최초ray/sphere와 chain helper의 수신자 조건도 다르다. 표적buffer와 실제공격·자원차감은 구별했다.
- 6839는 적 자신의 초기39이며 피해자에게electric slow를주는 근거가 아니다. isNegative는level변환기초값에관여하며summary에서부호반전하지않는다.
- 3101 authored106와runtime summary106은동일명제가아니다. activeAreaLevel의재계산·테이블상속·발사체생성·수신공격조건을보존했다.
- 206은EffectNone에도직접ID소비를통해작동한다. max선택이며합산이아니다. effect96검사는due tick피해queue후일수있다.
- Effect58은공급자전수조사를하지않았고sourceBindings=[]다. 정확기존consumer/action링크를제공했으며통합생성기의역연결로기존행동조회에서도달하도록한다.

각규칙의 refs는 같은레코드codeReferences의ID이며경로·전체파일SHA256·정확line범위를가리킨다. 자산설정은raw/components.jsonl의단일행,조건mapping은기존고정tablecrosswalk의정확범위와pointer를함께제시했다. sourceEvidence는현재source node의origin을보존한다. pins는모든최종근거및읽은입력파일을핀한다.

비교검증: 13개 assertion, 오류0개. 이는생산자정적self-check이며게임런타임·독립수용판정이아니다.
