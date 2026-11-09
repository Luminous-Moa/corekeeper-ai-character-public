"""Update the current developer entry documentation after the accepted install."""
from pathlib import Path
import json
P=Path(__file__).resolve().parent;G=P.parents[1]/'게임행동-그래프'
cv=json.loads((G/'published/coverage.json').read_text(encoding='utf-8'))
assert cv['acceptedBatches']==261 and cv['processResourcesAddedRecords']==4 and cv['processResourcesCompletionStatus']=='independent_pass'
nodes=sum(cv['nodeKinds'].values());edges=sum(cv['relations'].values());cases=cv['completionCases']
file=G/'README.md';original=file.read_bytes();text=original.decode('utf-8')
assert '**`published/`의260수용묶음·2,542case**' in text
(P/'README-before.md').write_bytes(original)
lines=text.splitlines()
assert lines[6].startswith('기본 `graph.py` 명령은') and lines[8].startswith('기존 환경음13개')
lines[6]=('기본 `graph.py` 명령은 **`published/`의261수용묶음·{:,}case**를 조회한다. 수용260판의 모든251,477노드·350,373관계를 보존하고, '
 'ProcessResources 시설(용광로 등 7원본)의 레시피 활성→재료 이동→자동화 타이머→출력 사슬을4레코드로 보완했다. 현재{:,}노드·{:,}관계이며 '
 '이동·수면5레코드, 기존10범위 보완·5,880주장·3,235원본도 유지한다. 수용묶음·case·역할행은 서로 다른 기록 단위이며 고유 게임행동 수로 합산하지 않는다. '
 '`wholeScopeComplete=false`, `runtimeTested=false`다. [용광로 가공 보완](../2026-11/2026-11-08-용광로가공판단보강/completion-report.md), '
 '[이동·수면 보완](../2026-11/2026-11-08-이동수면판단보강/completion-report.md), [이전10범위 보완](../2026-11/2026-11-08-행동판단필수보완/completion-report.md)을 따른다.').format(cases,nodes,edges)
lines[8]=('기존 환경음13개 분류/상세82노드 제외 필터는 유지한다. 전수 관련성 조사의 미적용 분리 제안은 적용하지 않았다. 같은source의다른관계는유지한다. '
 '`--include-history`를붙이면 현재 보완분까지 포함한 비필터 원형을 명시적으로 조회한다. 직전260판 실물은 용광로 보완 폴더의 `baseline-260/`와 `published-before-switch/`, '
 '그 수용raw 이력은 이동·수면 보완 폴더의 `history-current/`에 보존한다. 과거 고정 원형과 정확 코드·영수증을 덮어쓰지 않는다.')
result=[];seen={'source':0,'build':0,'check':0,'project':0}
for line in lines:
    if line.startswith('- 이번 source 연결은'):
        line+=' 용광로 가공 사슬도 같은 판단 payload를 반환하고, 기존 process_recipe_selection·automatic_recipe_result·inventory_transfer 행에서는 `has_process_resources_supplement` 관계로 도달한다.';seen['source']+=1
    if line.startswith('`build`는'):
        assert '256판' in line and '새5레코드' in line
        line=line.replace('256판','260판').replace('256기준','260기준').replace('새5레코드','새4레코드');seen['build']+=1
    if line.startswith('`check`는'):
        assert '수용256 raw' in line and '2026-11-08-이동수면판단보강/check-published.json' in line
        line=line.replace('수용256 raw','수용260 raw').replace('2026-11-08-이동수면판단보강/check-published.json','2026-11-08-용광로가공판단보강/check-published.json');seen['check']+=1
    if line.startswith('프로젝트의 정상 진입점'):
        line=line.replace('`behavior P.life`에서 섭취·휴식 주제로 이어간다.','`behavior P.life`에서 섭취·휴식, `behavior P.life-tree-first-scope`에서 제작·가공(용광로 가공 사슬 포함) 주제로 이어간다.');seen['project']+=1
    result.append(line)
assert all(v==1 for v in seen.values()),seen
file.write_text('\n'.join(result)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({'updated':str(file),'cases':cases,'nodes':nodes,'edges':edges}))
