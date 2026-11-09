"""Update the current developer entry documentation after the accepted install."""
from pathlib import Path
import json
P=Path(__file__).resolve().parent;G=P.parents[1]/'게임행동-그래프'
cv=json.loads((G/'published/coverage.json').read_text(encoding='utf-8'))
assert cv['acceptedBatches']==260 and cv['movementSleepAddedRecords']==5
file=G/'README.md';original=file.read_bytes();text=original.decode('utf-8')
assert '**`published/`의256수용묶음·2,539case**' in text
(P/'README-before.md').write_bytes(original)
lines=text.splitlines()
lines[6]='기본 `graph.py` 명령은 **`published/`의260수용묶음·2,542case**를 조회한다. 수용256판의 모든251,417노드·350,275관계를 보존하고, Grapple 발동 후 당김·종료, 탑승동물 안전 하차, 광차 선로·제동·하차, 조건수면210/322의 네 범위를5레코드로 보완했다. 현재251,477노드·350,373관계이며 기존10범위 보완·5,880주장·3,235원본도 유지한다. 수용묶음·case·역할행은 서로 다른 기록 단위이며 고유 게임행동 수로 합산하지 않는다. `wholeScopeComplete=false`, `runtimeTested=false`다. [이동·수면 보완](../2026-11/2026-11-08-이동수면판단보강/completion-report.md), [이전10범위 보완](../2026-11/2026-11-08-행동판단필수보완/completion-report.md)을 따른다.'
lines[8]='기존 환경음13개 분류/상세82노드 제외 필터는 유지한다. 전수 관련성 조사의 미적용 분리 제안은 적용하지 않았다. 같은source의다른관계는유지한다. `--include-history`를붙이면 현재 보완분까지 포함한 비필터 원형을 명시적으로 조회한다. 직전256판 실물은 이동수면 보완 폴더의 `baseline-256/`와 `published-before-switch/`, 그 수용raw 이력은 이전 필수연결 보완 폴더의 `history-current/`에 보존한다. 과거 고정 원형과 정확 코드·영수증을 덮어쓰지 않는다.'
result=[]
for line in lines:
    if line.startswith('- 이번 source 연결은'):
        line+=' 이동·수면 보완도 같은 판단 payload를 반환하며 Mount/322의 source-free 소비는 `has_movement_sleep_supplement` 관계로 도달한다. 자기 record의 has_* 필드와 재사용한 기존 결과 relation은 구별한다.'
    if line.startswith('`build`는'):
        line=line.replace('246판','256판').replace('246기준','256기준').replace('새12레코드','새5레코드')
    if line.startswith('`check`는'):
        line=line.replace('수용246 raw','수용256 raw').replace('2,719물리핀','전체 물리핀').replace('2026-11-08-행동판단필수보완/check-published.json','2026-11-08-이동수면판단보강/check-published.json')
    result.append(line)
    if line.startswith('이 조회는 개발용 상세 보기다.'):
        result.extend(['','프로젝트의 정상 진입점 `node ../지식그래프/tools/product.mjs behavior P.expedition-navigation`에서 이동, `behavior P.expedition`에서 차량, `behavior P.life`에서 섭취·휴식 주제로 이어간다. 주제의 정확한 `game:<ID>`는 같은 진입점의 `show game:<ID>`와 `source game:<ID>`로 조회한다. `game target 8373`처럼 현재 전체 게임 CLI로 직접 들어갈 수도 있다. 이 연결은 개발 참고 색인이며 모드 구현·AI의 실제 실행 성공과 구별한다.'])
file.write_text('\n'.join(result)+'\n',encoding='utf-8',newline='\n')
print('updated accepted260 README and normal project consumer instructions')
