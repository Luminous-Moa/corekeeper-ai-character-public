"""선택된 네 범위의 원문 귀속만 독립 재확인한다. 검수 폴더만 쓴다."""
from pathlib import Path
import hashlib,json
P=Path(__file__).resolve().parents[1];S=P.parents[2];W=S/'work';C=W/'게임원본-상호작용/code';R=W/'게임원본-상호작용/raw'
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for x in iter(lambda:f.read(4*1024*1024),b''):h.update(x)
 return h.hexdigest()
def ptr(v,p):
 for t in p.split('/')[1:]:
  t=t.replace('~1','/').replace('~0','~');v=v[int(t)] if isinstance(v,list) else v[t]
 return v
b=read(P/'production/source-bindings.json');objects=read(R/'objects.json');checked=[]
for item in b['bindings']:
 v=ptr(objects,item['objectPointer']);assert v['sourceKey']==item['sourceKey'];assert v['sourceRawByteSha256']==item['sourceRawByteSha256'];assert (v['objectId'],v['variation'])==(item['objectId'],item['variation']);assert v['objectInfo']['prefabInfo']==item['prefabInfo']
 for ref in item['components']:
  component=ptr(objects,ref['pointer']);assert component['key']==ref['componentKey'];assert component['script']['class']==ref['class'];assert ref['linkKind']=='authoringRef'
 checked.append({'objectId':item['objectId'],'variation':item['variation'],'sourceKey':item['sourceKey'],'pointer':item['objectPointer'],'components':item['components']})
del objects
selected={i['line']:i['value'] for i in b['components']};lines=[]
with (R/'components.jsonl').open(encoding='utf-8-sig') as f:
 for n,line in enumerate(f,1):
  if n in selected:assert json.loads(line)==selected[n];lines.append(n)
  if n>=max(selected):break
assert set(lines)==set(selected)
enum=(C/'Pug.Base/ConditionID.cs').read_text(encoding='utf-8-sig').split('{',1)[1].rsplit('}',1)[0]
names=[x.strip() for x in enum.split(',') if x.strip()]
assert names.index('Sleeping')==210 and names.index('SleepingFromStandingStill')==322
code=['Pug.Other/PlayerEquipment/PlayerGrapplingRoutineSystem.cs','Pug.Other/PlayerState/MountRiding.cs','Pug.Other/PlayerState/MinecartRiding.cs','Pug.ECS.Components/PlayerState/MinecartRidingStateCD.cs','Pug.ECS.Conversion/MinecartConverter.cs','Pug.ECS.Conversion/OffHandConverter.cs','Pug.Other/ForceSleepByConditionSystem.cs','Pug.Other/PlayerState/Sleep.cs','Pug.Other/PlayerVelocityCalculationSystem.cs','Pug.Base/ConditionID.cs']
out={'status':'pass','selectedExactBindings':checked,'selectedRawComponentLines':lines,'conditionEnums':{'Sleeping':210,'SleepingFromStandingStill':322},'fixedOriginalPins':[{'path':str(p),'sha256':sha(p)} for p in [R/'objects.json',R/'components.jsonl',*[C/x for x in code]]],'scope':'선택된 귀속과 코드만 확인. source-free Mount/322를 object9609 또는3750의 새 공급자로 귀속하지 않음.','wholeScopeComplete':False,'runtimeTested':False}
(P/'review/source-original-checks.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps({'status':'pass','exactBindings':len(checked),'componentLines':lines,'codeFiles':len(code)},ensure_ascii=False))
