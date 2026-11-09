from pathlib import Path
import hashlib,json,sys
P=Path(__file__).resolve().parents[1];W=P.parents[1];R=W/'게임원본-상호작용';A=W/'2026-11/2026-11-07-행동결과완결/full/A'
sys.path.insert(0,str(R/'deps'));import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
root=Path('C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
manifest=json.loads((A/'minion-1356-root-typetree-v2/root-typetree-search.json').read_text(encoding='utf-8-sig'));pins={f['path']:f for f in manifest['manifest']}
files=[root/'resources.assets',root/'globalgamemanagers.assets'];dlls=sorted((root/'Managed').glob('*.dll'))
for p in files+dlls:assert sha(p)==pins[str(p.relative_to(root))]['sha256'],str(p)
env=UnityPy.load(*map(str,files));obj=next(o for o in env.objects if o.assets_file.name=='resources.assets' and o.path_id==27162);assert hashlib.sha256(obj.get_raw_data()).hexdigest()=='ee1c78681e166cbed06c78a9ed58abe2df5725ad53d43bdb4571e8dba10c1eb7'
generator=TypeTreeGenerator(obj.assets_file.unity_version);errors=[]
for p in dlls:
 try:generator.load_dll(p.read_bytes())
 except Exception as e:errors.append({'path':str(p),'error':str(e)})
env.typetree_generator=generator;tree=obj.read_typetree();entries=[]
cross=json.loads((A/'condition-effect-table-crosswalk-v2/condition-effect-table-crosswalk.json').read_text(encoding='utf-8-sig'))
for ci,category in enumerate(tree['conditionCategories']):
 for i,item in enumerate(category['conditions']):
  if item['Id'] in [39,102,206]:
   group=next(g for g in cross['groups'] if g['conditionID']==item['Id']);assert group['tableEntries'][0]['conditionInfo']==item
   entries.append({'pointer':f'/conditionCategories/{ci}/conditions/{i}','conditionInfo':item})
assert len(entries)==3
result={'status':'pass','originalResourcesSha256':sha(files[0]),'tableRawSha256':hashlib.sha256(obj.get_raw_data()).hexdigest(),'entries':entries,'dllLoadErrors':errors}
(P/'review/original-table-check.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
