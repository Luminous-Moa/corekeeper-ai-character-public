"""Exercise one actual canonical entry for each reviewed group after install."""
from pathlib import Path
import json,subprocess,sys
import overlay as gen
P=gen.P;G=gen.G;output=P/'verification-published';output.mkdir(exist_ok=True)
contract=gen.read(P/'contract.json');groups={}
for item in contract['records']:
    if item['group'] not in groups:groups[item['group']]=gen.api.pointer(gen.read(item['path']),item['pointer'])
queries=[]
def query(name,args):
    proc=subprocess.run([sys.executable,'-B','-X','utf8',str(G/'graph.py'),*args],capture_output=True,text=True,encoding='utf-8')
    (output/(name+'.json')).write_text(proc.stdout,encoding='utf-8')
    assert proc.returncode==0,(name,proc.stderr)
    value=json.loads(proc.stdout);queries.append({'name':name,'args':args,'exitCode':0,'output':str(output/(name+'.json'))});return value
for group,record in groups.items():
    ident=record['id'];bindings=record.get('sourceBindings',[])
    if bindings:
        result=query(group+'-target',['target',str(bindings[0]['objectId']),'--limit','100'])
        entries=[e for source in result['sources'] for e in source['essentialDecisionEvidence'] if e['id']==ident]
        assert entries and all(e['reviewStatus']=='independent_pass' and e['runtimeTested'] is False and e['sourceComplete'] is False for e in entries)
        for entry in entries:
            for key in ('conditions','costs','flow','results','failures','boundary'):
                if key in record['value']:assert entry['decision'][key]==record['value'][key]
    else:
        start=record['links'][0]['to'];end=ident+'/result/results'
        result=query(group+'-path',['path',start,end]);assert result['found'] and len(result['edges'])==2 and result['edges'][0][2]==ident
first=next(iter(groups.values()));ident=first['id']
result=query('representative-show',['show',ident]);assert result['node']['status']=='independent_pass'
result=query('representative-source',['source',ident]);assert result['value']==first['value']
assert {n:gen.sha(G/'published'/n) for n in gen.NAMES}=={n:gen.sha(P/'candidate-final'/n) for n in gen.NAMES}
summary={'status':'pass','groups':len(groups),'actualCanonicalCliQueries':queries,'hashes':{n:gen.sha(G/'published'/n) for n in gen.NAMES},'runtimeTested':False,'wholeScopeComplete':False}
(output/'summary.json').write_bytes(gen.api.encode(summary));print(json.dumps({k:v for k,v in summary.items() if k!='actualCanonicalCliQueries'},ensure_ascii=False))
