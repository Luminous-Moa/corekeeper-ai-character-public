"""Verify existing origin contract digests using exactly legacy_graph encode semantics."""
from pathlib import Path
import hashlib, json
ROOT = Path(__file__).resolve().parent
SPECIAL = {'Infinity':'positive_infinity','-Infinity':'negative_infinity','NaN':'not_a_number'}
def read(p):
    return json.loads(Path(p).read_text(encoding='utf-8-sig'), parse_constant=lambda x: {'floatSpecial':SPECIAL[x]})
def digest(v):
    return hashlib.sha256((json.dumps(v, ensure_ascii=False, sort_keys=True, indent=2, allow_nan=False)+'\n').encode('utf-8')).hexdigest()
def pointer(doc, ptr):
    for part in ptr.split('/')[1:] if ptr else []:
        part=part.replace('~1','/').replace('~0','~')
        doc=doc[int(part)] if isinstance(doc,list) else doc[part]
    return doc
checks=read(ROOT/'static-check.json')
cache={}
results=[]
for node in checks['existingTargets']:
    origin=node.get('origin')
    if not origin:
        continue
    path=origin['resolvedPath']
    if path not in cache: cache[path]=read(path)
    value=pointer(cache[path],origin['pointer'])
    actual=digest(value)
    result={'nodeId':node['id'],'path':path,'pointer':origin['pointer'],'expected':origin['normalizedValueSha256'],'actual':actual,'matches':actual==origin['normalizedValueSha256']}
    results.append(result)
    assert result['matches'], result
report={'runtimeTested':False,'wholeScopeComplete':False,'encodeMethod':'json.dumps(ensure_ascii=False,sort_keys=True,indent=2,allow_nan=False)+newline; parse constants per legacy_graph.py','count':len(results),'allMatch':all(x['matches'] for x in results),'results':results}
(ROOT/'normalized-origin-check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'canonicalOriginsChecked':len(results),'allMatch':report['allMatch']}))
