from pathlib import Path
import json,sys,subprocess,concurrent.futures,hashlib
P=Path(__file__).resolve().parents[1];G=P.parents[1]/'게임행동-그래프';OUT=P/'review'
grapple='completion:essential:grapple-input-aim-v1'
effect='completion:essential:effect58-input-direction-v1'
death='completion:essential:player-death-armor-cost-v1'
source='source:CAB-2ebcba97367158c9b08f790c25d642c9:1753535845213101411'
tasks=[
('target-grapple',['target','8373','--limit','100']),
('show-grapple',['show',grapple]),
('source-grapple',['source',grapple]),
('path-grapple',['path',source,grapple+'/result/results']),
('target-3101',['target','3101','--limit','100']),
('show-effect-entry',['show','beam']),
('path-effect',['path','beam',effect+'/result/results']),
('show-death-entry',['show','object:6002']),
('source-death-armor',['source',death])]
def execute(task):
    name,args=task
    proc=subprocess.run([sys.executable,'-B','-X','utf8',str(G/'graph.py'),*args],capture_output=True,text=True,encoding='utf-8')
    assert proc.returncode==0,(name,proc.stderr)
    dest=OUT/('post-publication-cli-'+name+'.json');dest.write_text(proc.stdout,encoding='utf-8')
    return name,json.loads(proc.stdout),{'name':name,'args':args,'exitCode':proc.returncode,'path':str(dest),'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()}
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:actual=list(pool.map(execute,tasks))
values={n:v for n,v,e in actual}
target=values['target-grapple']
evidence=[e for s in target['sources'] for e in s['essentialDecisionEvidence'] if e['id']==grapple]
assert len(evidence)==1
assert {'conditions','costs','flow','results','failures','boundary'}<=set(evidence[0]['decision'])
assert evidence[0]['reviewStatus']=='independent_pass' and evidence[0]['runtimeTested'] is False and evidence[0]['sourceComplete'] is False
record=json.loads((P/'six/supplement.json').read_text(encoding='utf-8'))['records'][0]
assert values['source-grapple']['value']==record['value']
fields={v['field']:v for v in values['show-grapple']['fields']}
assert fields['costs']['kind']=='condition' and fields['failures']['kind']=='failure'
for name,expected in [('path-grapple',grapple),('path-effect',effect)]:
    v=values[name];assert v['found'] and len(v['edges'])==2 and v['edges'][0][2]==expected
target3101=[e for s in values['target-3101']['sources'] for e in s['essentialDecisionEvidence'] if e['id']=='completion:essential:condition102-source3101-v1']
assert len(target3101)==1 and target3101[0]['reviewStatus']=='independent_pass'
for name,wanted in [('show-effect-entry',effect),('show-death-entry','completion:essential:player-death-grave-inventory-v1')]:
    assert wanted in json.dumps(values[name],ensure_ascii=False)
armor=json.loads((P/'four/supplement.json').read_text(encoding='utf-8'))['records'][4]
assert values['source-death-armor']['value']==armor['value'] and armor.get('sourceBindings',[])==[]
result={'status':'pass','canonicalEntry':str(G/'graph.py'),'actualQueries':[e for n,v,e in actual],
'assertions':['source action target exposes exact conditions/costs/flow/results/failures/boundary',
'source returns exact approved action payload','show retains cost condition and failure typing',
'2-edge source-to-grapple-result is direct action evidence','3101 source-specific conditional effect attribution',
'beam entry exposes source-free Effect58 consumer and 2-edge result path',
'object6002 exposes source-free death lifecycle supplement','source death armor returns exact conditional cost formula payload'],
'runtimeTested':False,'wholeScopeComplete':False}
(OUT/'post-publication-cli-check.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False))
print('MANUAL_REVIEW_GRAPPLE='+json.dumps(evidence[0]['decision'],ensure_ascii=False))
print('MANUAL_REVIEW_3101='+json.dumps(target3101[0]['decision'],ensure_ascii=False))
print('MANUAL_REVIEW_ARMOR='+json.dumps(armor['value'],ensure_ascii=False))

