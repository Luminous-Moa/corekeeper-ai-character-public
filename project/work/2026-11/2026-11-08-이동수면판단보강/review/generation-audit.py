"""정확 pending artifact와 원문·물리 pin·대표 CLI를 독립 검사한다."""
from pathlib import Path
from collections import defaultdict
import gc,hashlib,importlib.util,json,subprocess,sys
P=Path(__file__).resolve().parents[1];G=P.parents[1]/'게임행동-그래프';R=P.parent/'2026-11-08-행동판단필수보완';O=P/'review'
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'),parse_constant=lambda x:{'floatSpecial':{'Infinity':'positive_infinity','-Infinity':'negative_infinity','NaN':'not_a_number'}[x]})
def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for x in iter(lambda:f.read(4*1024*1024),b''):h.update(x)
 return h.hexdigest()
def enc(v):return (json.dumps(v,ensure_ascii=False,sort_keys=True,indent=2,allow_nan=False)+'\n').encode()
def ptr(v,p):
 for t in p.split('/')[1:]:
  t=t.replace('~1','/').replace('~0','~');v=v[int(t)] if isinstance(v,list) else v[t]
 return v
def main(candidate):
 contractSha=sha(P/'contract.json');c=read(P/'contract.json');assert len(c['records'])==5 and len(c['groups'])==4
 pins={str(Path(f['path']).resolve()):f['sha256'] for f in c['pins']}
 for f in c['pins']:assert sha(f['path'])==f['sha256'],f['path']
 for folder in [R/'history-current',P/'baseline-256']:
  for name in ['graph.json','coverage.json','manifest.json']:assert pins[str((folder/name).resolve())]==sha(folder/name)
 spec=importlib.util.spec_from_file_location('reviewed_movement_generator',P/'overlay.py');gen=importlib.util.module_from_spec(spec);spec.loader.exec_module(gen)
 raw=read(R/'history-current/graph.json');assert 'decisionView' not in raw and raw['coverage']['acceptedBatches']==256
 projectionSha=hashlib.sha256(enc(gen.view.project(raw))).hexdigest();assert projectionSha==sha(P/'baseline-256/graph.json')==c['baselineGraphSha256'];del raw;gc.collect()
 b=read(P/'baseline-256/graph.json');g=read(candidate/'graph.json');ns=g['nodes'];es=set(map(tuple,g['edges']));out=defaultdict(list)
 for s,r,t in es:assert s in ns and t in ns;out[s].append((r,t))
 assert all(ns[k]==v for k,v in b['nodes'].items()),'old node changed'
 assert set(map(tuple,b['edges']))<=es,'old edge lost'
 assert not g['coverage']['wholeScopeComplete'] and not g['coverage']['runtimeTested']
 m=read(candidate/'manifest.json');physical=[];planned=[]
 for f in m['files']:
  logical=(G/f['path']).resolve();p=logical
  if logical==(G/'publication.json').resolve() and sha(p)!=f['sha256']:
   assert sha(p)==sha(P/'publication-before.json');p=P/'publication.config.json';planned.append({'logicalPath':str(logical),'reviewedTemplate':str(p),'sha256':sha(p)})
  assert sha(p)==f['sha256'] and p.stat().st_size==f['bytes'],str(p);physical.append(str(logical))
 assert len(planned)==1
 added=set(ns)-set(b['nodes']);origins=[];cache={};records=[];routes=[]
 for k in added:
  n=ns[k];ref=n['origin'];p=(G/ref['path']).resolve()
  if p not in cache:cache[p]=read(p)
  assert hashlib.sha256(enc(ptr(cache[p],ref['pointer']))).hexdigest()==ref['normalizedValueSha256'],k
  assert str(p) in physical,k
  origins.append(k)
 for item in c['records']:
  r=ptr(read(item['path']),item['pointer']);ident=r['id'];records.append(r)
  assert hashlib.sha256(enc(r)).hexdigest()==item['recordSha256']
  assert ns[ident]['recordFormat']=='movement_sleep_decision_connection' and ns[ident]['status']=='pending_independent_review'
  assert ns[ident]['sourceComplete'] is False and ns[ident]['runtimeTested'] is False
  if r['action'] in ['mount_safe_dismount','condition_sleep322']:assert r['sourceBindings']==[]
  if r['action']=='condition_sleep210':assert [(x['objectId'],x['variation']) for x in r['sourceBindings']]==[(9609,0)]
  if r['action']=='minecart_rail_brake_dismount':assert [(x['objectId'],x['variation']) for x in r['sourceBindings']]==[(6551,0)]
  for evidence in r['value'].get('sourceEvidence',[]):
   assert b['nodes'][evidence['nodeId']]['origin']==evidence['origin'];ref=evidence['origin'];p=(G/ref['path']).resolve()
   if p not in cache:cache[p]=read(p)
   assert hashlib.sha256(enc(ptr(cache[p],ref['pointer']))).hexdigest()==ref['normalizedValueSha256']
   assert str(p) in physical,str(p)
  for field,v in r['value'].items():
   targets=[t for rel,t in out[ident] if rel.startswith('has_') and t.startswith(ident+'/') and ns[t].get('field')==field];assert len(targets)==1 and ns[targets[0]]['value']==v,(ident,field,targets)
  starts=['source:'+x['sourceKey'] for x in r.get('sourceBindings',[])]+[l['to'] for l in r.get('links',[]) if l.get('from',ident)==ident and l['to'] in b['nodes']]
  assert starts,ident
  for start in starts:assert any(t==ident for rel,t in out[start]);routes.append([start,ident,ident+'/result/results'])
 result={'status':'pass','reviewedContractSha256':sha(P/'contract.json'),'candidateDirectory':str(candidate),'candidateHashes':{n:sha(candidate/n) for n in ['graph.json','coverage.json','manifest.json']},'raw256ProjectionSha256':projectionSha,'priorNodeCount':len(b['nodes']),'priorEdgeCount':len(b['edges']),'newOrigins':len(origins),'physicalManifestFiles':len(physical),'plannedPublicationSwitches':planned,'recordIds':[r['id'] for r in records],'entryRoutes':routes,'wholeScopeComplete':False,'runtimeTested':False}
 oldid='completion:essential:grapple-input-aim-v1';oldfield=oldid+'/result/results';oldorigin=ns[oldfield]['origin'];olddecision={ns[t]['field']:ns[t]['value'] for rel,t in out[oldid] if rel.startswith('has_') and t.startswith(oldid+'/') and ns[t].get('field') in ['summary','conditions','costs','flow','results','failures','boundary']}
 # Free large graph data before launching representative real CLI processes.
 del b,g,ns,es,out,gen;gc.collect()
 def cli(name,args):
  name=candidate.name+'-'+name
  p=subprocess.run([sys.executable,'-B','-X','utf8',str(P/'overlay.py'),'--graph-dir',str(candidate),*args],capture_output=True,text=True,encoding='utf-8');assert p.returncode==0,(name,p.stderr)
  v=json.loads(p.stdout);(O/(name+'.json')).write_text(p.stdout,encoding='utf-8');return v
 grapple=next(r for r in records if r['action']=='grapple_pull_movement_end');t=cli('source-cli-grapple-target',['target','8373','--limit','100']);entries=[e for s in t['sources'] for e in s['essentialDecisionEvidence']]
 ids={e['id'] for e in entries};assert grapple['id'] in ids and 'completion:essential:grapple-input-aim-v1' in ids
 for e in entries:
  if e['id']==grapple['id']:assert e['decision']=={k:grapple['value'][k] for k in ['summary','conditions','costs','flow','results','failures','boundary'] if k in grapple['value']}
  if e['id']==oldid:assert e['decision']==olddecision and e['reviewStatus']=='independent_pass'
 v=cli('source-cli-grapple-show',['show',grapple['id']]);assert any(r['relation']=='continues_after' and r['target']==oldfield for r in v['relations'])
 assert {f['field']:f['value'] for f in v['fields']}==grapple['value']
 v=cli('source-cli-old-grapple-results-show',['show',oldfield]);assert v['node']['value']==olddecision['results'] and v['node']['origin']==oldorigin
 v=cli('source-cli-old-grapple-results-source',['source',oldfield]);assert v['value']==olddecision['results'] and v['origin']==oldorigin
 minecart=next(r for r in records if r['action']=='minecart_rail_brake_dismount');v=cli('source-cli-minecart-source',['source',minecart['id']]);assert v['value']==minecart['value']
 mount=next(r for r in records if r['action']=='mount_safe_dismount');v=cli('source-cli-mount-show',['show',mount['id']]);assert {f['field']:f['value'] for f in v['fields']}==mount['value']
 sleep=next(r for r in records if r['action']=='condition_sleep322');start=sleep['links'][0]['to'];v=cli('source-cli-sleep322-path',['path',start,sleep['id']+'/result/results']);assert v['found'] and len(v['edges'])==2 and v['edges'][0][2]==sleep['id']
 result['representativeActualCliQueries']=[['target','8373','--limit','100'],['show',grapple['id']],['show',oldfield],['source',oldfield],['source',minecart['id']],['show',mount['id']],['path',start,sleep['id']+'/result/results']]
 assert sha(P/'contract.json')==contractSha
 for name in ['overlay.py','compile_contract.py','verify.py','install_reviewed.py','publication.config.json']:assert sha(P/name)==pins[str((P/name).resolve())],name
 (O/'source-generation-checks.json').write_bytes(enc(result));print(json.dumps({k:v for k,v in result.items() if k not in ['entryRoutes','representativeActualCliQueries']},ensure_ascii=False))
if __name__=='__main__':main(Path(sys.argv[1]).resolve())
