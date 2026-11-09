"""Independent consumer audit; no generator imports and no source mutation."""
from pathlib import Path
from collections import Counter, defaultdict
import json, hashlib, subprocess, sys

HERE=Path(__file__).resolve().parent
WORK=HERE.parents[2]
ROOT=WORK/'게임행동-그래프'
SPECIAL={'Infinity':'positive_infinity','-Infinity':'negative_infinity','NaN':'not_a_number'}
def read(p, strict=False):
    def constant(s):
        if strict: raise ValueError('non-standard JSON constant: '+s)
        return {'floatSpecial':SPECIAL[s]}
    return json.loads(p.read_text(encoding='utf-8-sig'),parse_constant=constant)
def digest(p):
    with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def ptr(doc,p):
    for key in p.split('/')[1:]:
        key=key.replace('~1','/').replace('~0','~')
        doc=doc[int(key)] if isinstance(doc,list) else doc[key]
    return doc
def normalize_digest(v):
    return hashlib.sha256((json.dumps(v,ensure_ascii=False,sort_keys=True,indent=2,allow_nan=False)+'\n').encode()).hexdigest()
def run(name,*args):
    r=subprocess.run([sys.executable,'-X','utf8',str(ROOT/'graph.py'),*args],capture_output=True,text=True,encoding='utf-8')
    assert r.returncode==0,(args,r.stderr)
    value=json.loads(r.stdout,parse_constant=lambda s: (_ for _ in ()).throw(ValueError(s)))
    (HERE/(name+'.json')).write_text(r.stdout,encoding='utf-8')
    commands.append({'name':name,'args':args,'exitCode':r.returncode})
    return value

g=read(ROOT/'graph.json',True); nodes=g['nodes'];edges=g['edges'];out=defaultdict(list)
assert len({tuple(e) for e in edges})==len(edges)
for a,r,b in edges:
    assert a in nodes and b in nodes,(a,r,b)
    out[a].append((r,b))
original=read(WORK/'게임원본-상호작용/final/collection.json')
raw=read(WORK/'게임원본-상호작용/raw/objects.json')
base=read(WORK/'2026-11/2026-11-07-행동전체추출/package/collection.json')
supp=read(WORK/'2026-11/2026-11-07-상호작용미결보강/package/collection.json')
gap=read(WORK/'2026-11/2026-11-07-상호작용미결보강/package/gap-index.json')
pairdoc=read(WORK/'2026-11/2026-11-07-행동전체추출/D-outcomes/cooking-pairs.json')
sources={n['sourceKey']:n for n in nodes.values() if n['kind']=='source'}
assert len(sources)==3235
assert set(sources)=={r.get('sourceKey',r.get('key')) for r in raw}=={r['sourceKey'] for r in original['records']}
for row in original['records']:
    n=sources[row['sourceKey']]
    assert (n['objectId'],n['variation'],n['names'])==(row['objectId'],row['variation'],row['names'])
expected_claims=[]
for i,r in enumerate(original['records']):
    for j,c in enumerate(r['interactions']):expected_claims.append(('initial',r['sourceKey'],f'/records/{i}/interactions/{j}'))
for ds,doc in [('base',base),('supp',supp)]:
    for i,gr in enumerate(doc['groups']):
        for j,r in enumerate(gr['payload']['extraction.json']['records']):expected_claims.append((ds,r['sourceKey'],f'/groups/{i}/payload/extraction.json/records/{j}'))
actual_claims=[(n['dataset'],n['sourceKey'],n['origin']['pointer']) for n in nodes.values() if n['kind']=='claim']
assert Counter(actual_claims)==Counter(expected_claims)
field_names={'condition':{'conditions','condition'},'result':{'result','results'},'failure':{'failure','failureBranches','failurePaths'},'flow':{'inputProcessingConsumer','consumer','flow','inputs','input'},'unknown':{'unknown','unresolved','remaining'},'evidence':{'assetEvidence','codeEvidence','codeFiles','code','sourceEvidence','sourceLinks','detailedCodeEvidence'}}
expected_fields=Counter();expected_rules=Counter()
def collect_fields(v,root,path):
    for kind,keys in field_names.items():
        for k in keys.intersection(v):expected_fields[(root,path+'/'+k,kind,normalize_digest(v[k]))]+=1
for i,r in enumerate(original['records']):
    for j,v in enumerate(r['interactions']):collect_fields(v,'initial',f'/records/{i}/interactions/{j}')
for ds,doc in [('base',base),('supp',supp)]:
    for i,gr in enumerate(doc['groups']):
        ext=gr['payload']['extraction.json'];prefix=f'/groups/{i}/payload/extraction.json'
        for j,v in enumerate(ext['records']):collect_fields(v,ds,prefix+f'/records/{j}')
        for name,v in ext.get('rules',{}).items():
            path=prefix+'/rules/'+name.replace('~','~0').replace('/','~1')
            collect_fields(v,ds,path);expected_rules[(ds,path,normalize_digest(v))]+=1
def dataset_of(n):
    path=n['origin']['path']
    return 'initial' if '게임원본-상호작용' in path else ('base' if '행동전체추출' in path else 'supp')
assert Counter((dataset_of(n),n['origin']['pointer'],n['kind'],normalize_digest(n['value'])) for n in nodes.values() if n['kind'] in field_names)==expected_fields
assert Counter((n['dataset'],n['origin']['pointer'],n['origin']['normalizedValueSha256']) for n in nodes.values() if n['kind']=='rule')==expected_rules
for n in nodes.values():
    if n['kind']=='claim':assert ('has_claim',n['id']) in out['source:'+n['sourceKey']]
expected_history=Counter((r['sourceKey'],j,v) for r in original['records'] for j,v in enumerate(r['unresolved']))
actual_history=Counter()
for sk,n in sources.items():
    for relation,ident in out[n['id']]:
        if relation=='has_historical_gap':
            h=nodes[ident];actual_history[(sk,int(h['origin']['pointer'].split('/')[-1]),h['text'])]+=1
            assert h['status']=='historical_source_statement_not_auto_closed'
assert actual_history==expected_history
actual_pairs=sorted((int(n['origin']['pointer'].split('/')[-1]),n['value']) for n in nodes.values() if n['kind']=='cooking_pair')
assert [v for i,v in actual_pairs]==pairdoc['pairs'] and len(actual_pairs)==6561
for n in nodes.values():
    if n['kind']=='cooking_pair':
        for slot,sk in enumerate(n['value']['inputSourceKeys']):assert (f'cooking_input_{slot}',n['id']) in out['source:'+sk]
        assert any(r=='produces_id_conditionally' and nodes[t]['objectId']==n['value']['outputObjectId'] for r,t in out[n['id']])
edge_set={tuple(e) for e in edges}
for index in supp['recordIndex']:
    for prior in index.get('priorRecordIds',[]):assert ('supp:'+index['id'],'supplements','base:'+prior) in edge_set
for link in supp['stageLinks']:
    ds='base' if link['targetOrigin']=='baseline' else 'supp'
    assert ('supp:'+link['from'],link['relation'],ds+':'+link['to']) in edge_set
assert Counter(normalize_digest(n['value']) for n in nodes.values() if n['kind']=='coverage_disposition')==Counter(map(normalize_digest,gap['previousUnknownLedger']))
assert Counter(normalize_digest(n['value']) for n in nodes.values() if n['kind']=='gap_assessment')==Counter(map(normalize_digest,gap['originalGapStatus']))
cache={};origin_count=0
for n in nodes.values():
    if 'origin' not in n:continue
    ref=n['origin'];p=(ROOT/ref['path']).resolve()
    if p not in cache:cache[p]=read(p)
    v=ptr(cache[p],ref['pointer'])
    assert normalize_digest(v)==ref['normalizedValueSha256'],n['id']
    if 'value' in n:assert n['value']==v,n['id']
    origin_count+=1
unsafe=[];specials=[];pathid_count=0
def walk(v,path=''):
    global pathid_count
    if isinstance(v,dict):
        if 'floatSpecial' in v:specials.append((path,v['floatSpecial']))
        for k,x in v.items():
            if k in ('m_PathID','pathId','sourcePathId','m_high','m_low'):
                pathid_count+=1
                if not isinstance(x,str):unsafe.append((path+'/'+k,x))
            walk(x,path+'/'+k)
    elif isinstance(v,list):
        for i,x in enumerate(v):walk(x,path+'/'+str(i))
    elif isinstance(v,int) and not isinstance(v,bool) and abs(v)>2**53-1:unsafe.append((path,v))
walk(g)
assert not unsafe,unsafe[:5]
manifest=read(ROOT/'manifest.json',True)
input_before=read(HERE/'input-before.json')
for entry in manifest['files']:
    p=(ROOT/entry['path']).resolve()
    if '--preliminary' in sys.argv and p==ROOT/'graph.py':continue
    assert digest(p)==entry['sha256'],str(p)
    if str(p) in input_before:assert entry['sha256']==input_before[str(p)],str(p)
commands=[]
overview=run('cli-overview','overview')
sit=next(n for n in nodes.values() if n['kind']=='claim' and n['dataset']=='base' and n['action']=='sit')
sit_source=run('cli-sit-source-context','source',sit['id'])
context=sit_source['evidenceContext'];assert context
sit_evidence=ROOT/context['groupBasePath']/sit_source['value']['codeEvidence'][0]
assert sit_evidence.is_file()
(HERE/'opened-sit-evidence.md').write_text(sit_evidence.read_text(encoding='utf-8-sig'),encoding='utf-8')
chest=run('cli-find-chest','find','InventoryChest')
ch=next(s for s in chest['sources'] if s['objectId']==100)
openclaim=next(c for c in ch['claims'] if c['action']=='open_inventory')
od=run('cli-chest-detail','show',openclaim['id']);assert any(f['kind']=='condition' for f in od['fields']) and any(f['kind']=='result' for f in od['fields'])
run('cli-chest-source','source',openclaim['id'])
shovel=run('cli-find-shovel','target','1');assert all(s['objectId']==1 for s in shovel['sources']);sh=next(s for s in shovel['sources'] if s['objectId']==1)
repair=next(c for c in sh['claims'] if c['dataset']=='base' and c['action']=='repair')
rep=run('cli-repair-baseline','show',repair['id'])
repair_supp=next(c for c in sh['claims'] if c['dataset']=='supp' and c['action']=='repair')
rs=run('cli-repair-supplement','show',repair_supp['id']);assert rs['rules'] and rs['node']['facts'].get('catalogSelection')
dig=next(c for c in sh['claims'] if c['action']=='dig_shovel')
dg=run('cli-shovel-dig','show',dig['id']);assert 'digStateStarted' in json.dumps(dg)
plate=run('cli-find-plate','find','PressurePlate','--variation','0')['sources'][0]
pc=next(c for c in plate['claims'] if c['dataset']=='supp' and c['action']=='react_to_eligible_collision_candidate')
pa=next(c for c in plate['claims'] if c['action']=='propagate_electrical_power')
run('cli-plate-filter','show',pc['id']);run('cli-plate-power','show',pa['id'])
platepath=run('cli-plate-path','path',pc['id'],pa['id'])
assert platepath['found']
dclaim=next(n for n in nodes.values() if n['kind']=='claim' and n['id'].startswith('supp:D:') and n['action']=='automatic_recipe_result')
dr=run('cli-d-rule-consumer','show',dclaim['id']);assert any(f['kind']=='condition' for r in dr['rules'] for f in r['fields']) and any(f['kind']=='result' for r in dr['rules'] for f in r['fields'])
pair=next(p for p in pairdoc['pairs'] if p['inputObjectIds'][0]!=p['inputObjectIds'][1])
ab=run('cli-cooking-ab','cooking',*[str(x) for x in pair['inputObjectIds']]);ba=run('cli-cooking-ba','cooking',*[str(x) for x in reversed(pair['inputObjectIds'])])
assert ab['pairs'][0]['value']['inputObjectIds']==list(reversed(ba['pairs'][0]['value']['inputObjectIds']))
run('cli-6506','target','6506');cg=run('cli-gap6506','gaps','A-circuit6506');assert any(n.get('value',{}).get('staticStatus')=='unresolved' for n in cg['nodes'])
missing=run('cli-4600','target','4600');assert missing['total']==0 and 'gap:D-4600' in missing['relatedGapNodes'] and missing['noSourceMeaning'] and missing['nextQuery'];pg=run('cli-gap4600','gaps','D-4600');assert any(n.get('value',{}).get('staticStatus')=='unresolved' for n in pg['nodes'])
run('cli-filter-gap','gaps','C-filter');run('cli-map-gap','gaps','D-map1513');run('cli-runtime-gap','gaps','runtime','--limit','5')
no_claim=next(n for n in sources.values() if not any(r=='has_claim' for r,t in out[n['id']]))
nc=run('cli-no-claim','target',no_claim['id']);assert not nc['sources'][0]['claims'] and 'not impossible' in nc['sources'][0]['note']
assert '../게임행동-그래프/README.md' in (WORK/'지식그래프/README.md').read_text(encoding='utf-8-sig')
report={'status':'pass','preliminary':'--preliminary' in sys.argv,'units':{'source':3235,'initial':3499,'baseline':1432,'supplement':949,'cooking':6561,'historical':5090,'disposition':1671},'originsChecked':origin_count,'allFieldValuesChecked':sum(expected_fields.values()),'rulesChecked':sum(expected_rules.values()),'edgesChecked':len(edges),'strictJson':True,'unsafeIntegerOrPathId':unsafe,'pathIdFieldsChecked':pathid_count,'specialFloatTags':Counter(v for p,v in specials),'inputsPinned':len(manifest['files']),'plateDirectedPathFound':platepath['found'],'entryLinkExists':True,'commands':commands,'graphSha256':digest(ROOT/'graph.json'),'scriptSha256':digest(ROOT/'graph.py')}
(HERE/'audit-result.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
