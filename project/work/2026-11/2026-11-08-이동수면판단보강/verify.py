"""Verify appended evidence, preservation, origins and real developer CLI flows."""
from pathlib import Path
from collections import defaultdict
import concurrent.futures,hashlib,json,subprocess,sys
import overlay as gen
P=gen.P;G=gen.G
def run(directory,canonical=False):
    graph=gen.read(directory/'graph.json');base=gen.read(P/'baseline-256/graph.json');nodes=graph['nodes'];edges={tuple(e) for e in graph['edges']}
    assert all(nodes[k]==v for k,v in base['nodes'].items()),'Prior nodes modified'
    assert set(map(tuple,base['edges']))<=edges,'Prior edges removed'
    records=gen.read(P/'contract.json')['records'];cache={};origin_count=0
    for ident,node in nodes.items():
        if ident in base['nodes']:continue
        ref=node.get('origin')
        if not ref:continue
        path=(G/ref['path']).resolve()
        if path not in cache:cache[path]=gen.read(path)
        assert gen.api.digest(gen.api.pointer(cache[path],ref['pointer']))==ref['normalizedValueSha256'],ident
        origin_count+=1
    tasks=[];target_expect=defaultdict(set)
    for index,item in enumerate(records):
        doc=gen.read(item['path']);record=gen.api.pointer(doc,item['pointer']);ident=record['id']
        assert ident in nodes
        fields={key for key in record['value']}
        assert {'conditions','flow','results','failures','boundary'}<=fields,(ident,fields)
        tasks.extend([(str(index)+'-show',['show',ident],('show',ident)),(str(index)+'-source',['source',ident],('source',ident))])
        result=ident+'/result/results';starts=['source:'+r['sourceKey'] for r in record.get('sourceBindings',[])]
        starts+=list(dict.fromkeys(l['to'] for l in record.get('links',[]) if l.get('from',ident)==ident and l['to'] in base['nodes']))
        assert starts,(ident,'No existing entry point')
        start=starts[0];tasks.append((str(index)+'-path',['path',start,result],('path',start,ident,result)))
        for role in record.get('sourceBindings',[]):target_expect[str(role['objectId'])].add(ident)
    for oid,expected in target_expect.items():tasks.append(('target-'+oid,['target',oid,'--limit','100'],('target',expected)))
    output=P/('verification-published' if canonical else 'verification-candidate');output.mkdir(exist_ok=True)
    entry=G/'graph.py' if canonical else P/'overlay.py'
    def execute(task):
        name,args,expect=task;cmd=[sys.executable,'-B','-X','utf8',str(entry),*args]
        if not canonical:cmd=[*cmd[:5],'--graph-dir',str(directory),*cmd[5:]]
        proc=subprocess.run(cmd,capture_output=True,text=True,encoding='utf-8')
        (output/(name+'.json')).write_text(proc.stdout,encoding='utf-8')
        assert proc.returncode==0,(name,proc.stderr)
        value=json.loads(proc.stdout)
        if expect[0]=='show':
            assert value['node']['id']==expect[1]
            actual={f['field']:f['kind'] for f in value['fields']};assert actual['conditions']=='condition' and actual['results']=='result' and actual['failures']=='failure' and actual['boundary']=='unknown'
            if expect[1]+'/condition/costs' in nodes:assert actual['costs']=='condition'
        elif expect[0]=='source':assert value['value']==gen.api.pointer(gen.read(next(r['path'] for r in records if r['id']==expect[1])),next(r['pointer'] for r in records if r['id']==expect[1]))['value']
        elif expect[0]=='path':
            assert value['found'];route=value['edges'];assert route[-1][2]==expect[3] and any(e[2]==expect[2] for e in route)
            # New source connections or reverse supplemental links make this
            # the intended decision flow rather than an unrelated loot route.
            assert len(route)<=2,(name,route)
        else:
            actual={e['id']:e for s in value['sources'] for e in s['essentialDecisionEvidence']};assert expect[1]<=actual.keys(),(name,expect[1]-actual.keys())
            for ident in expect[1]:
                assert {'conditions','flow','results','failures','boundary'}<=actual[ident]['decision'].keys()
                if ident+'/condition/costs' in nodes:assert 'costs' in actual[ident]['decision']
                for key,content in actual[ident]['decision'].items():
                    matches=[nodes[t]['value'] for r,t in [(r,t) for s,r,t in edges if s==ident] if r.startswith('has_') and t.startswith(ident+'/') and nodes[t].get('field')==key]
                    assert matches==[content],(ident,key)
        return {'name':name,'exitCode':proc.returncode,'args':args,'output':str(output/(name+'.json'))}
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:queries=list(pool.map(execute,tasks))
    result={'status':'pass','canonicalEntry':canonical,'priorNodesPreserved':len(base['nodes']),'priorEdgesPreserved':len(base['edges']),'newOriginPointersChecked':origin_count,'records':len(records),'actualCliQueries':queries,'wholeScopeComplete':False,'runtimeTested':False,'hashes':{n:gen.sha(directory/n) for n in gen.NAMES}}
    (output/'summary.json').write_bytes(gen.api.encode(result));print(json.dumps({k:v for k,v in result.items() if k!='actualCliQueries'},ensure_ascii=False));return result
if __name__=='__main__':run(Path(sys.argv[1]).resolve(),len(sys.argv)>2 and sys.argv[2]=='--canonical')
