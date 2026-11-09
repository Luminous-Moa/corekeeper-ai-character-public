"""Independent structural and CLI checks. Writes only into this review directory."""
from pathlib import Path
from collections import defaultdict, deque
import hashlib, importlib.util, json, subprocess, sys
P=Path(__file__).resolve().parents[1]
G=P.parents[1]/'게임행동-그래프'
O=P/'review'
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def enc(v):return (json.dumps(v,ensure_ascii=False,sort_keys=True,indent=2,allow_nan=False)+'\n').encode()
def ptr(v,p):
 for t in p.split('/')[1:]:
  t=t.replace('~1','/').replace('~0','~');v=v[int(t)] if isinstance(v,list) else v[t]
 return v
def main(candidate):
 c=read(P/'contract.json');g=read(candidate/'graph.json');b=read(P/'baseline-246/graph.json')
 R=P.parent/'2026-11-08-행동그래프추가기록';raw=read(R/'history-current/graph.json');rawmanifest=read(R/'history-current/manifest.json')
 assert 'decisionView' not in raw
 assert raw['coverage']['acceptedBatches']==246
 spec=importlib.util.spec_from_file_location('reviewed_generator',P/'overlay.py');gen=importlib.util.module_from_spec(spec);spec.loader.exec_module(gen)
 assert enc(gen.view.project(raw))==enc(b)
 candidate_hashes={n:sha(candidate/n) for n in ['graph.json','coverage.json','manifest.json']}
 contract_pins={str(Path(f['path']).resolve()):f['sha256'] for f in c['pins']}
 for n in ['graph.json','coverage.json','manifest.json']:assert contract_pins[str((R/'history-current'/n).resolve())]==sha(R/'history-current'/n)
 rawfiles={str((G/f['path']).resolve()):f for f in rawmanifest['files']}
 for n in ['contract.json','review-acceptance.json','selection.json']:
  p=R/n;assert str(p.resolve()) in rawfiles;assert rawfiles[str(p.resolve())]['sha256']==sha(p)
 oldreceipt=read(R/'review-acceptance.json');assert oldreceipt['status']=='independent_pass';assert oldreceipt['reviewedContractSha256']==sha(R/'contract.json');assert sha(oldreceipt['review']['path'])==oldreceipt['review']['sha256']
 assert read(P/'baseline-246/manifest.json')['historicalPublicationAliases']==rawmanifest['historicalPublicationAliases']
 ns=g['nodes'];es=set(map(tuple,g['edges']));out=defaultdict(list)
 for s,r,t in es:
  assert s in ns and t in ns,(s,r,t)
  out[s].append((r,t))
 assert all(ns[k]==v for k,v in b['nodes'].items())
 assert set(map(tuple,b['edges']))<=es
 assert g['coverage']['wholeScopeComplete'] is False and g['coverage']['runtimeTested'] is False
 manifest=read(candidate/'manifest.json');physical=[];planned_switches=[]
 for f in manifest['files']:
  p=(G/f['path']).resolve();logical=p
  if p==(G/'publication.json').resolve() and sha(p)!=f['sha256'] and candidate.resolve()!=(G/'published').resolve():
   p=P/'publication.config.json';planned_switches.append({'logicalPath':str(logical),'reviewedTemplate':str(p),'sha256':sha(p)})
  assert sha(p)==f['sha256'],str(p);assert p.stat().st_size==f['bytes'];physical.append(str(logical))
 for f in c['pins']:assert sha(f['path'])==f['sha256']
 origins=[];cache={};added=set(ns)-set(b['nodes'])
 for k in added:
  n=ns[k];assert 'origin' in n,k;r=n['origin'];p=(G/r['path']).resolve()
  if p not in cache:cache[p]=read(p)
  assert hashlib.sha256(enc(ptr(cache[p],r['pointer']))).hexdigest()==r['normalizedValueSha256'],k
  assert str(p) in physical,k
  origins.append(k)
 routes=[];queries=[]
 def cli(args,name):
  proc=subprocess.run([sys.executable,'-B','-X','utf8',str(P/'overlay.py'),'--graph-dir',str(candidate),*args],capture_output=True,text=True,encoding='utf-8')
  assert proc.returncode==0,(name,proc.stderr)
  data=json.loads(proc.stdout);(O/(name+'.json')).write_text(proc.stdout,encoding='utf-8');queries.append({'args':args,'output':name+'.json'});return data
 for i,item in enumerate(c['records']):
  rec=ptr(read(item['path']),item['pointer']);k=rec['id'];n=ns[k]
  assert hashlib.sha256(enc(rec)).hexdigest()==item['recordSha256']
  for field in ['sourceEvidence','catalogTargetEvidence']:
   for evidence in rec['value'].get(field,[]):
    baseline_node=b['nodes'][evidence['nodeId']];assert baseline_node.get('origin')==evidence.get('origin')
    ref=evidence.get('origin')
    if ref:
     p=(G/ref['path']).resolve()
     if p not in cache:cache[p]=read(p)
     assert hashlib.sha256(enc(ptr(cache[p],ref['pointer']))).hexdigest()==ref['normalizedValueSha256']
  assert n['runtimeTested'] is False and n['sourceComplete'] is False
  assert n['status']==g['coverage']['essentialCompletionStatus']
  for f in ['conditions','flow','results','failures','boundary']:
   kind={'conditions':'condition','flow':'flow','results':'result','failures':'failure','boundary':'unknown'}[f]
   assert ns[k+'/'+kind+'/'+f]['value']==rec['value'][f]
  starts=['source:'+r['sourceKey'] for r in rec.get('sourceBindings',[])]
  starts+=list(dict.fromkeys(l['to'] for l in rec.get('links',[]) if l.get('from',k)==k and l['to'] in b['nodes']))
  assert starts,k
  for start in starts:
   assert any(t==k for r,t in out[start]),(start,k)
   assert (k,'has_result',k+'/result/results') in es
   routes.append([start,k,k+'/result/results'])
  shown=cli(['show',k],str(i)+'-show');assert shown['node']==n
  assert {v['field']:v['value'] for v in shown['fields']}==rec['value']
  source=cli(['source',k],str(i)+'-source');assert source['value']==rec['value']
  for oid in sorted({r['objectId'] for r in rec.get('sourceBindings',[])}):
   target=cli(['target',str(oid),'--limit','100'],str(i)+'-target-'+str(oid))
   matching=[e for s in target['sources'] for e in s['essentialDecisionEvidence'] if e['id']==k]
   assert matching,(oid,k)
   assert all(e['decision']=={f:rec['value'][f] for f in ['summary','conditions','costs','flow','results','failures','boundary'] if f in rec['value']} for e in matching)
  route=cli(['path',starts[0],k+'/result/results'],str(i)+'-path');assert route['found'] and len(route['edges'])==2 and route['edges'][0][2]==k and route['edges'][1][2]==k+'/result/results'
 result={'status':'pass','reviewedContractSha256':sha(P/'contract.json'),'reviewedCandidateHashes':candidate_hashes,'raw246HasDecisionView':False,'raw246PriorContractSha256':sha(R/'contract.json'),'raw246PriorReceiptSha256':sha(R/'review-acceptance.json'),'priorNodeCount':len(b['nodes']),'priorEdgeCount':len(b['edges']),'newOriginCount':len(origins),'physicalPinCount':len(physical),'plannedPublicationSwitches':planned_switches,'everyEntrypointRoutes':routes,'queries':queries,'wholeScopeComplete':False,'runtimeTested':False}
 (O/'independent-checks.json').write_bytes(enc(result));print(json.dumps({k:v for k,v in result.items() if k not in ['everyEntrypointRoutes','queries']},ensure_ascii=False))
if __name__=='__main__':main(Path(sys.argv[1]).resolve())
