"""Retain unchanged graph CLI evidence; rerun only the corrected target selector."""
from pathlib import Path
from collections import defaultdict
import json,subprocess,sys
import overlay as gen
P=gen.P;old=P/'candidate-review';new=P/'candidate-review-v2';out=P/'verification-candidate-v2';out.mkdir(exist_ok=True)
assert gen.sha(old/'graph.json')==gen.sha(new/'graph.json')
assert gen.sha(old/'coverage.json')==gen.sha(new/'coverage.json')
records=gen.read(P/'production/supplement.json')['records'];retained=[];targets=defaultdict(list)
for i,record in enumerate(records):
    ident=record['id']
    for kind in ('show','source','path'):
        file=P/'verification-candidate'/(str(i)+'-'+kind+'.json');value=gen.read(file)
        if kind=='show':
            assert value['node']['id']==ident
            fields={f['field']:f['value'] for f in value['fields']}
            for key in ('conditions','costs','flow','results','failures','boundary'):
                if key in record['value']:assert fields[key]==record['value'][key]
        elif kind=='source':assert value['value']==record['value']
        else:assert value['found'] and len(value['edges'])==2 and value['edges'][-1][2]==ident+'/result/results'
        retained.append({'kind':kind,'record':ident,'path':str(file),'sha256':gen.sha(file),'basis':'Actual prior CLI output revalidated against identical graph/coverage bytes and unchanged source payload; selector change affects target summary only.'})
    for role in record.get('sourceBindings',[]):targets[str(role['objectId'])].append(record)
queries=[]
for oid,expected in targets.items():
    args=['target',oid,'--limit','100'];proc=subprocess.run([sys.executable,'-B','-X','utf8',str(P/'overlay.py'),'--graph-dir',str(new),*args],capture_output=True,text=True,encoding='utf-8')
    file=out/('target-'+oid+'.json');file.write_text(proc.stdout,encoding='utf-8');assert proc.returncode==0,proc.stderr
    result=json.loads(proc.stdout);entries={e['id']:e for s in result['sources'] for e in s['essentialDecisionEvidence']}
    for record in expected:
        entry=entries[record['id']]
        for key in ('summary','conditions','costs','flow','results','failures','boundary'):
            if key in record['value']:assert entry['decision'][key]==record['value'][key],(record['id'],key)
    queries.append({'args':args,'exitCode':0,'path':str(file),'sha256':gen.sha(file)})
summary={'status':'pass','graphAndCoverageUnchangedAfterSelectorFix':True,'supplementSha256':gen.sha(P/'production/supplement.json'),'retainedActualNonTargetCliOutputs':retained,'actualSelectorCliReruns':queries,'validatedQueryCount':len(retained)+len(queries),'newCandidateHashes':{n:gen.sha(new/n) for n in gen.NAMES},'runtimeTested':False,'wholeScopeComplete':False}
(out/'summary.json').write_bytes(gen.api.encode(summary));print(json.dumps({k:v for k,v in summary.items() if k not in {'retainedActualNonTargetCliOutputs','actualSelectorCliReruns'}},ensure_ascii=False))
