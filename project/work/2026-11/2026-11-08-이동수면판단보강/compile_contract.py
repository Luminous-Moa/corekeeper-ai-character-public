"""Freeze the four scoped continuations and physical code/evidence references."""
from pathlib import Path
import hashlib,json
P=Path(__file__).resolve().parent;G=P.parents[1]/'게임행동-그래프'
def read(path):return json.loads(Path(path).read_text(encoding='utf-8-sig'))
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def encode(value):return (json.dumps(value,ensure_ascii=False,sort_keys=True,indent=2,allow_nan=False)+'\n').encode('utf-8')
pins={};records=[];groups=[]
def pin(path,expected=None):
    path=Path(path).resolve()
    if path.is_relative_to(G/'published'):raise ValueError('Mutable published input must use baseline-256 copy')
    value={'path':str(path),'sha256':sha(path)}
    if expected and value['sha256']!=expected:raise ValueError('Changed pin '+str(path))
    pins[str(path)]=value
def refs(value):
    if isinstance(value,dict):
        if isinstance(value.get('path'),str) and isinstance(value.get('sha256'),str) and len(value['sha256'])==64:pin(value['path'],value['sha256'])
        for child in value.values():refs(child)
    elif isinstance(value,list):
        for child in value:refs(child)
for folder in ('production',):
    path=P/folder/'supplement.json';doc=read(path);pin(path);refs(doc)
    for i,record in enumerate(doc['records']):
        group=record.get('group',record['id'].split('movement-sleep:',1)[1])
        if group not in groups:groups.append(group)
        records.append({'id':record['id'],'group':group,'path':str(path),'pointer':'/records/'+str(i),'recordSha256':hashlib.sha256(encode(record)).hexdigest()})
    for name in ('pins.json','report.md','static-check.json'):
        path=P/folder/name
        if path.exists():
            pin(path)
            if name=='pins.json':refs(read(path))
for path in [P/'overlay.py',P/'compile_contract.py',P/'publication.config.json',P/'publication-before.json',P/'verify.py',P/'install_reviewed.py',P/'00-agreement.md',*[P/'baseline-256'/n for n in ('graph.json','coverage.json','manifest.json')],*[P.parent/'2026-11-08-행동판단필수보완/history-current'/n for n in ('graph.json','coverage.json','manifest.json')]]:pin(path)
out={'schemaVersion':1,'groups':groups,'records':records,'pins':sorted(pins.values(),key=lambda f:f['path']),'baselineGraphSha256':sha(P/'baseline-256/graph.json'),'wholeScopeComplete':False,'runtimeTested':False}
(P/'contract.json').write_bytes(encode(out));print(json.dumps({'status':'compiled','groups':len(groups),'records':len(records),'pins':len(pins),'contractSha256':sha(P/'contract.json')}))
