"""Independent installed consumer evidence; no shared-file writes."""
from pathlib import Path
import os,sys,json,hashlib,subprocess,concurrent.futures,shutil
import copy
P=Path(__file__).resolve().parents[1];R=P/'review';W=P.parents[1];G=W/'게임행동-그래프'
PRODUCT=W/'지식그래프';ENTRY=PRODUCT/'tools/product.mjs'
LIVE=W/'2026-11/2026-11-05-지식그래프-실화면/D'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def normalized(v):return json.dumps(v,sort_keys=True,ensure_ascii=False,separators=(',',':'))
records=read(P/'production/supplement.json')['records'];byid={r['id']:r for r in records}
before=read(P/'consumer/before/graph.json');current=read(LIVE/'graph.json');canonical=read(G/'published/graph.json')
nodes={n['id']:n for n in current['nodes']}
node_deltas=[{'id':n['id'],'before':n,'after':nodes.get(n['id'])} for n in before['nodes'] if nodes.get(n['id'])!=n]
edges={normalized(e) for e in current['edges']}
edge_deltas=[e for e in before['edges'] if normalized(e) not in edges]
old_todo_sha='7df61223bbc96541f94cc56cb5ddba903aa88385efbf6beedfaee9d355c8dfa3'
new_todo_sha='69d832210ac51a53d224a40a4fef30bc3fdc44474c21d2becb82d95661963c2c'
allowed_nodes={'R.chapters','R.memo','R.mention','R.playtest'}
allowed_edges={'E053','E055','E056'}
assert {d['id'] for d in node_deltas}==allowed_nodes
assert {d['id'] for d in edge_deltas}==allowed_edges
todo=(W/'2026-11/2026-11-05-지식그래프-개편/../../../todo.md').resolve()
assert sha(todo)==new_todo_sha
lines=todo.read_text(encoding='utf-8-sig').splitlines()
checked_lines={}
def refreshed(old):
    value=copy.deepcopy(old)
    assert len(value['evidence'])==1
    e=value['evidence'][0]
    assert e['path']=='../../../todo.md' and e['doc']=='todo' and e['sha256']==old_todo_sha
    assert e['line'] in [2,9,10,22] and e['anchor'] in lines[e['line']-1]
    checked_lines[str(e['line'])]=lines[e['line']-1]
    e['sha256']=new_todo_sha
    return value
for d in node_deltas:assert refreshed(d['before'])==d['after'],('Non-pin node change',d['id'])
current_edges_by_id={e.get('id'):e for e in current['edges'] if e.get('id')}
for d in edge_deltas:assert refreshed(d)==current_edges_by_id[d['id']],('Non-pin edge change',d['id'])
source_refresh={'status':'pass','basis':'HEAD가 정확4node/3edge의 whole-file todo evidence.sha 갱신만 수용하도록 범위를 명시함',
 'path':str(todo),'oldSha256':old_todo_sha,'currentSha256':new_todo_sha,'nodeIds':sorted(allowed_nodes),'edgeIds':sorted(allowed_edges),
 'actualAnchorLines':checked_lines,'allOtherNodeAndEdgeFieldsExact':True}
assert sha(LIVE/'validation.json')==sha(P/'consumer/before/validation.json'),'Runtime validation changed'
foreign=[n for n in current['nodes'] if n.get('externalBehavior')]
for n in foreign:
    e=n['externalBehavior'];real=canonical['nodes'][e['id']]
    assert e['kind']==real['kind'] and e['origin']==real.get('origin')
    assert e.get('sourceKey')==real.get('sourceKey') and e.get('variation')==real.get('variation')
for r in records:
    assert 'game:'+r['id'] in nodes and 'game:'+r['id'] not in {n['id'] for n in before['nodes']}
project={'priorNodesPreserved':len(before['nodes']),'priorEdgesPreserved':len(before['edges']),
'foreignReferencesChecked':len(foreign),'runtimeValidationExact':True,
'projectGraphSha256':sha(LIVE/'graph.json'),'runtimeValidationSha256':sha(LIVE/'validation.json')}
project['priorNodesExact']=len(before['nodes'])-len(node_deltas)
project['priorEdgesExact']=len(before['edges'])-len(edge_deltas)
origins={r['id']:canonical['nodes'][r['id']]['origin'] for r in records}
del before,current,canonical,nodes,edges,foreign
prefix='completion:movement-sleep:'
grapple=prefix+'grapple-pull-end-v1';mount=prefix+'mount-safe-dismount-v1'
minecart=prefix+'minecart-rail-brake-dismount-v1';s210=prefix+'condition-sleep210-v1';s322=prefix+'condition-sleep322-v1'
tasks=[
('game-target-minecart','game',['target','6551','--limit','100']),
('game-target-grapple','game',['target','8373','--limit','100']),
('game-show-grapple','game',['show',grapple]),
('game-source-210','game',['source',s210]),
('game-path-mount','game',['path',byid[mount]['links'][0]['to'],mount+'/result/results']),
('game-path-322','game',['path',byid[s322]['links'][0]['to'],s322+'/result/results']),
('project-navigation','project',['behavior','P.expedition-navigation']),
('project-expedition','project',['behavior','P.expedition']),
('project-life','project',['behavior','P.life']),
('project-show-minecart','project',['show','game:'+minecart]),
('project-source-mount','project',['source','game:'+mount]),
('project-source-322','project',['source','game:'+s322])]
node=shutil.which('node')
if not node:
    node=str(Path(os.environ['USERPROFILE'])/'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node.exe')
assert Path(node).is_file(),node
env={**os.environ,'PYTHONDONTWRITEBYTECODE':'1'}
reused={}
if '--reuse-outputs' in sys.argv:
    prior=read(R/'post-publication-consumer-check.json')
    assert prior['projectPreservation']['projectGraphSha256']==project['projectGraphSha256']
    assert prior['projectPreservation']['runtimeValidationSha256']==project['runtimeValidationSha256']
    artifact=read(R/'post-publication-artifact-check.json')
    assert artifact['publishedSha256']=={n:sha(G/'published'/n) for n in artifact['publishedSha256']}
    preserved=R/'post-publication-consumer-initial-drift.json'
    if not preserved.exists():preserved.write_bytes((R/'post-publication-consumer-check.json').read_bytes())
    reused={q['name']:q for q in prior['actualIndependentQueries']}
def execute(task):
    name,kind,args=task
    if reused:
        q=reused[name]
        assert q['args']==args and q['exitCode']==0 and sha(Path(q['path']))==q['sha256']
        return name,read(Path(q['path'])),q
    cmd=[sys.executable,'-B','-X','utf8',str(G/'graph.py'),*args] if kind=='game' else [node,str(ENTRY),*args]
    proc=subprocess.run(cmd,capture_output=True,text=True,encoding='utf-8',env=env,cwd=PRODUCT)
    assert proc.returncode==0,(name,proc.stderr)
    output=R/('post-publication-cli-'+name+'.json');output.write_text(proc.stdout,encoding='utf-8')
    return name,json.loads(proc.stdout),{'name':name,'entry':str(G/'graph.py') if kind=='game' else str(ENTRY),'args':args,'exitCode':proc.returncode,'path':str(output),'sha256':sha(output)}
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:out=list(pool.map(execute,tasks))
v={name:value for name,value,e in out}
oldgrapple=read(W/'2026-11/2026-11-08-행동판단필수보완/six/supplement.json')['records'][0]
grappleentries={e['id']:e for s in v['game-target-grapple']['sources'] for e in s['essentialDecisionEvidence']}
for r in [oldgrapple,byid[grapple]]:
    assert r['id'] in grappleentries
    for field in ['conditions','costs','flow','results','failures','boundary']:
        if field in r['value']:assert grappleentries[r['id']]['decision'][field]==r['value'][field]
targets=[e for s in v['game-target-minecart']['sources'] for e in s['essentialDecisionEvidence'] if e['id']==minecart]
assert len(targets)==1 and targets[0]['reviewStatus']=='independent_pass'
for field in ['conditions','costs','flow','results','failures','boundary']:
    if field in byid[minecart]['value']:assert targets[0]['decision'][field]==byid[minecart]['value'][field]
assert v['game-source-210']['value']==byid[s210]['value']
for key,ident in [('game-show-grapple',grapple),('project-show-minecart',minecart)]:
    assert v[key]['node']['id']==ident and v[key]['node']['status']=='independent_pass'
    fields={f['field']:f['value'] for f in v[key]['fields'] if f['relation'].startswith('has_') and f['id'].startswith(ident+'/')}
    for f in ['conditions','flow','results','failures','boundary']:assert fields[f]==byid[ident]['value'][f]
for key,ident in [('game-path-mount',mount),('game-path-322',s322)]:
    assert v[key]['found'] and len(v[key]['edges'])==2 and v[key]['edges'][0][2]==ident
for key,ident in [('project-source-mount',mount),('project-source-322',s322)]:
    assert v[key]['value']==byid[ident]['value'] and byid[ident]['sourceBindings']==[] and v[key]['origin']==origins[ident]
for key,topic,ids in [('project-navigation','R.game-topic.move',[grapple]),('project-expedition','R.game-topic.vehicle',[mount,minecart]),('project-life','R.game-topic.food',[s210,s322])]:
    rows=[r for r in v[key]['topics'] if r['id']==topic]
    assert len(rows)==1 and all('game:'+ident in rows[0]['targets'] for ident in ids),(key,topic)
result={'status':'pass','acceptedSourcePinRefresh':source_refresh,'projectPreservation':project,'actualIndependentQueries':[e for n,value,e in out],
'normalPlanningToTopicToNewRecords':True,'sourceFreeMountAnd322ExactSourcePayload':True,'allForeignIdentityKindOriginChecked':True,
'runtimeTested':False,'wholeScopeComplete':False}
result['actualQueriesReusedFromSameUnchangedArtifacts']=bool(reused)
(R/'post-publication-consumer-check.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
for name in ['game-target-minecart','game-show-grapple','game-source-210','project-source-mount','project-source-322']:
    selected=v[name]
    if name=='game-target-minecart':selected=targets[0]['decision']
    elif 'show' in name:selected={f['field']:f.get('value') for f in selected['fields']}
    else:selected=selected['value']
    print('MANUAL_REVIEW_'+name+'='+json.dumps(selected,ensure_ascii=False))

