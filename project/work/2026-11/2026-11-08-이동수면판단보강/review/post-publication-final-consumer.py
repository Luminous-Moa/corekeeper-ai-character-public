"""Final read-only project revalidation after authorized TODO completion."""
from pathlib import Path
import json,hashlib,copy,sys
P=Path(__file__).resolve().parents[1];R=P/'review';W=P.parents[1];G=W/'게임행동-그래프'
ROOT=W/'2026-11/2026-11-05-지식그래프-개편';LIVE=W/'2026-11/2026-11-05-지식그래프-실화면/D'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def norm(v):return json.dumps(v,ensure_ascii=False,sort_keys=True)
expected_todo=sys.argv[1];expected_project=sys.argv[2]
todo=(ROOT/'../../../todo.md').resolve()
assert sha(todo)==expected_todo and sha(LIVE/'graph.json')==expected_project
before=read(P/'consumer/before/graph.json');current=read(LIVE/'graph.json')
canonical=read(G/'published/graph.json');audit=read(R/'post-publication-artifact-check.json')
assert audit['publishedSha256']=={n:sha(G/'published'/n) for n in audit['publishedSha256']}
initial=read(R/'post-publication-consumer-check.json');assert initial['status']=='pass'
for q in initial['actualIndependentQueries']:assert q['exitCode']==0 and sha(Path(q['path']))==q['sha256']
nodes={n['id']:n for n in current['nodes']};edgebyid={e['id']:e for e in current['edges'] if 'id' in e}
nodechanges=[n for n in before['nodes'] if nodes.get(n['id'])!=n]
edges={norm(e) for e in current['edges']};edgechanges=[e for e in before['edges'] if norm(e) not in edges]
assert {n['id'] for n in nodechanges}=={'R.chapters','R.memo','R.mention','R.playtest'}
assert {e['id'] for e in edgechanges}=={'E053','E055','E056'}
lines=todo.read_text(encoding='utf-8-sig').splitlines();actualanchors={}
def refresh(old):
    v=copy.deepcopy(old);assert len(v['evidence'])==1;e=v['evidence'][0]
    assert e['path']=='../../../todo.md' and e['doc']=='todo' and e['sha256']=='7df61223bbc96541f94cc56cb5ddba903aa88385efbf6beedfaee9d355c8dfa3'
    assert e['line'] in [2,9,10,22] and e['anchor'] in lines[e['line']-1]
    actualanchors[str(e['line'])]=lines[e['line']-1];e['sha256']=expected_todo
    return v
for n in nodechanges:assert refresh(n)==nodes[n['id']]
for e in edgechanges:assert refresh(e)==edgebyid[e['id']]
assert sha(LIVE/'validation.json')==sha(P/'consumer/before/validation.json')
for k in before:
    if k not in {'nodes','edges','meta','behaviorLink'}:assert current[k]==before[k],('Unrelated graph section',k)
assert {k:v for k,v in before['meta'].items() if k!='sources'}=={k:v for k,v in current['meta'].items() if k!='sources'}
oldmeta={s['path']:s['sha256'] for s in before['meta']['sources']}
newmeta={s['path']:s['sha256'] for s in current['meta']['sources']}
changedmeta=[{'path':k,'before':oldmeta.get(k),'after':newmeta.get(k)} for k in sorted(set(oldmeta)|set(newmeta)) if oldmeta.get(k)!=newmeta.get(k)]
allowed_meta={
todo,G/'README.md',G/'publication.json',G/'published/graph.json',G/'published/coverage.json',G/'published/manifest.json',
W/'2026-11/2026-11-08-행동판단프로젝트연계/topics.mjs',
W/'2026-11/2026-11-08-행동판단필수보완/overlay.py',P/'overlay.py'}
for d in changedmeta:assert (ROOT/d['path']).resolve() in allowed_meta,('Unexpected source change',d)
for s in current['meta']['sources']:assert sha((ROOT/s['path']).resolve())==s['sha256'],('Final project source pin mismatch',s['path'])
topics=W/'2026-11/2026-11-08-행동판단프로젝트연계/topics.mjs'
stripped=topics.read_text(encoding='utf-8-sig')
for addition in [' grapple_pull_movement_end',' mount_safe_dismount minecart_rail_brake_dismount',' condition_sleep210 condition_sleep322']:
    assert stripped.count(addition)==1
    stripped=stripped.replace(addition,'')
assert stripped==(P/'consumer/before/topics.mjs').read_text(encoding='utf-8-sig'),'Topics changed beyond five labels'
foreign=[n for n in current['nodes'] if n.get('externalBehavior')]
for n in foreign:
    e=n['externalBehavior'];actual=canonical['nodes'][e['id']]
    for key in ['kind','origin','sourceKey','variation']:assert e.get(key)==actual.get(key),('Foreign mismatch',n['id'],key)
records=read(P/'production/supplement.json')['records']
assert set(nodes)-{n['id'] for n in before['nodes']}=={'game:'+r['id'] for r in records}
expected_old_edges={norm(refresh(e) if e.get('id') in {'E053','E055','E056'} else e) for e in before['edges']}
added_edges=[e for e in current['edges'] if norm(e) not in expected_old_edges]
classification={'grapple_pull_movement_end':'move','mount_safe_dismount':'vehicle','minecart_rail_brake_dismount':'vehicle','condition_sleep210':'food','condition_sleep322':'food'}
assert len(added_edges)==5
assert {(e['from'],e['to'],e['kind']) for e in added_edges}=={('R.game-topic.'+classification[r['action']],'game:'+r['id'],'해당 주제의 원본 근거') for r in records}
assert all(e['runtimeTrace'] is False and e['class']=='개발 참고 색인' for e in added_edges)
for r in records:
    n=nodes['game:'+r['id']]
    assert n['externalBehavior']['origin']==canonical['nodes'][r['id']]['origin']
    fields={canonical['nodes'][t]['field']:canonical['nodes'][t]['value'] for s,relation,t in canonical['edges'] if s==r['id'] and relation.startswith('has_') and t.startswith(r['id']+'/') and canonical['nodes'][t].get('field')}
    assert fields==r['value']
out={'status':'pass','finalTodoSha256':expected_todo,'finalProjectGraphSha256':expected_project,
'finalValidationSha256':sha(LIVE/'validation.json'),'publishedSha256':audit['publishedSha256'],
'oldProjectNodes':len(before['nodes']),'oldProjectEdges':len(before['edges']),
'oldProjectNodesExact':len(before['nodes'])-4,'oldProjectEdgesExact':len(before['edges'])-3,
'sourcePinRefresh':{'nodeIds':sorted(n['id'] for n in nodechanges),'edgeIds':sorted(e['id'] for e in edgechanges),'actualAnchorLines':actualanchors,
'onlyEvidenceShaChanged':True,'semanticFieldsPreserved':True},
'currentProjectNodes':len(current['nodes']),'currentProjectEdges':len(current['edges']),
'allForeignReferencesExact':len(foreign),'allFinalProjectSourcePinsExact':len(newmeta),'changedMetaSources':changedmeta,
'onlyFiveTopicLabelsAdded':True,'runtimeValidationExact':True,'unrelatedGraphSectionsExact':True,
'onlyFiveNewForeignNodesAndFiveTopicReferenceEdges':True,
'actualIndependentQueries':initial['actualIndependentQueries'],'queriesReusedAfterOnlySourcePinRefresh':True,
'runtimeTested':False,'wholeScopeComplete':False}
(R/'post-publication-final-consumer-check.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in out.items() if k not in ['actualIndependentQueries']},ensure_ascii=False))

