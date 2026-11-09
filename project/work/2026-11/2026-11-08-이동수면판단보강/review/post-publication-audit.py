"""Independent post-install review. Reads artifacts; emits only post-publication evidence."""
from pathlib import Path
import hashlib,json,sys,importlib.util
P=Path(__file__).resolve().parents[1]
G=P.parents[1]/'게임행동-그래프'
EXPECTED_CONTRACT='74614162f5aafdbc594318fa5b08f93b02dc23f011cbc2a22ed6edd485d1b743'
def read(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def canonical(v): return json.dumps(v,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode('utf-8')
def pointer(v,p):
    for x in p.split('/')[1:]:
        x=x.replace('~1','/').replace('~0','~')
        v=v[int(x)] if isinstance(v,list) else v[x]
    return v
def audit():
    contract=read(P/'contract.json')
    assert sha(P/'contract.json')==EXPECTED_CONTRACT,'Exact contract changed'
    names=['graph.json','coverage.json','manifest.json']
    hashes={n:sha(G/'published'/n) for n in names}
    assert hashes=={n:sha(P/'candidate-final'/n) for n in names},'Candidate/publication mismatch'
    assert sha(G/'publication.json')==sha(P/'publication.config.json'),'Config mismatch'
    config=read(G/'publication.json');assert config['defaultView']=='published'
    assert (G/config['generator']).resolve()==P/'overlay.py'
    assert Path(config['historyGraphDirectory']).resolve()==P/'history-current'
    assert config['runtimeTested'] is False and config['wholeScopeComplete'] is False
    pub=read(G/'published/graph.json');base=read(P/'baseline-256/graph.json')
    assert all(pub['nodes'].get(k)==v for k,v in base['nodes'].items()),'Baseline node changed'
    assert set(map(tuple,base['edges']))<=set(map(tuple,pub['edges'])),'Baseline edge removed'
    raw=read(P/'history-current/graph.json')
    old=read(P.parent/'2026-11-08-행동판단필수보완/history-current/graph.json')
    assert 'decisionView' not in raw and 'decisionView' not in old
    assert all(raw['nodes'].get(k)==v for k,v in old['nodes'].items()),'Raw history node changed'
    assert set(map(tuple,old['edges']))<=set(map(tuple,raw['edges'])),'Raw history edge removed'
    spec=importlib.util.spec_from_file_location('essential_review_overlay',P/'overlay.py')
    overlay=importlib.util.module_from_spec(spec);spec.loader.exec_module(overlay)
    assert hashlib.sha256(overlay.api.encode(overlay.view.project(old))).hexdigest()==contract['baselineGraphSha256']
    assert overlay.api.encode(overlay.view.project(raw))==(G/'published/graph.json').read_bytes(),'Raw projection differs'
    receipt=read(P/'review-acceptance.json')
    assert receipt['status']=='independent_pass' and receipt['reviewedContractSha256']==EXPECTED_CONTRACT
    review=receipt['review'];assert sha(review['path'])==review['sha256']
    findings=read(review['path'])
    assert findings['status']=='independent_pass' and findings['reviewedContractSha256']==EXPECTED_CONTRACT
    assert sha(findings['report']['path'])==findings['report']['sha256']
    manifest=read(G/'published/manifest.json')
    mismatches=[]
    for f in manifest['files']:
        actual=(G/f['path']).resolve()
        if not actual.is_file() or sha(actual)!=f['sha256'] or actual.stat().st_size!=f['bytes']:
            mismatches.append({'pin':f,'resolved':str(actual)})
    assert not mismatches,('Physical pin mismatch',mismatches)
    added=set(pub['nodes'])-set(base['nodes']);origins=0
    for k in added:
        ref=pub['nodes'][k].get('origin')
        if ref:
            assert overlay.api.digest(pointer(read(G/ref['path']),ref['pointer']))==ref['normalizedValueSha256'],('Origin mismatch',k)
            origins+=1
    edges=set(map(tuple,pub['edges']))
    checked=[]
    for r in contract['records']:
        record=pointer(read(r['path']),r['pointer']);node=pub['nodes'][r['id']]
        assert overlay.api.digest(record)==r['recordSha256']
        assert node['kind']==record['kind'] and node['action']==record['action']
        assert node['status']=='independent_pass' and node['runtimeTested'] is False and node['sourceComplete'] is False
        assert pub['nodes']['acceptance:movement-sleep:'+r['group']]['status']=='independent_pass'
        assert node['roleBindings']==[{**v,'conditional':True} for v in record.get('sourceBindings',[])]
        for b in node['roleBindings']:
            source=pub['nodes']['source:'+b['sourceKey']]
            assert (source['objectId'],source['variation'])==(b['objectId'],b['variation'])
        if r['id'] in ['completion:movement-sleep:mount-safe-dismount-v1','completion:movement-sleep:condition-sleep322-v1']:
            assert node['roleBindings']==[]
            assert not [(s,rel,t) for s,rel,t in edges if t==r['id'] and s.startswith('source:')],'Invented source binding'
        checked.append({'id':r['id'],'kind':node['kind'],'roleBindings':len(node['roleBindings'])})
    for v in [pub['coverage'],read(G/'published/coverage.json'),manifest['movementSleepCompletion'],manifest['publication']]:
        assert v['runtimeTested'] is False and v['wholeScopeComplete'] is False
    assert pub['coverage']['movementSleepCompletionStatus']=='independent_pass'
    assert manifest['movementSleepCompletion']['status']=='independent_pass'
    generation=read(P/'final-generation.json');installation=read(P/'installation.json')
    assert generation['status']=='generated' and generation['contractSha256']==EXPECTED_CONTRACT
    assert generation['receiptSha256']==sha(P/'review-acceptance.json')
    assert generation['publication']==hashes and installation['hashes']==hashes
    assert installation['status']=='installed' and installation['configSha256']==sha(G/'publication.json')
    assert generation['history']=={n:sha(P/'history-current'/n) for n in names}
    assert {n:sha(P/'published-before-switch'/n) for n in names}=={n:sha(P/'baseline-256'/n) for n in names}
    result={'status':'pass','contractSha256':EXPECTED_CONTRACT,'publishedSha256':hashes,
            'configSha256':sha(G/'publication.json'),'physicalManifestPins':len(manifest['files']),
            'baselineNodesExact':len(base['nodes']),'baselineEdgesPreserved':len(base['edges']),
            'rawHistoryNodesExact':len(old['nodes']),'rawHistoryEdgesPreserved':len(old['edges']),
            'newOriginPayloadsExact':origins,'newRecords':checked,'reviewReceipt':review,
            'generationEvidence':[str(P/'final-generation.json'),str(P/'installation.json')],'runtimeTested':False,'wholeScopeComplete':False}
    output=P/'review/post-publication-artifact-check.json'
    output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
if __name__=='__main__': audit()



