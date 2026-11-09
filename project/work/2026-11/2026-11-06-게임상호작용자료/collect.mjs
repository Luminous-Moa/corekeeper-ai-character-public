import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const source='C:/Dev/CoreKeeperModSDK/Tools/knowledge/data/26332184';
const inputs=path.join(root,'inputs');
fs.mkdirSync(inputs,{recursive:true});
const sources={dump:path.join(source,'dump.json'),ids:path.join(source,'conf/ID/ObjectID.json'),manifest:'C:/Program Files (x86)/Steam/steamapps/appmanifest_1621690.acf'};
const filenames={dump:'dump.json',ids:'ObjectID.json',manifest:'appmanifest_1621690.acf'};
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
for(const [key,file] of Object.entries(sources)) {
  const dest=path.join(inputs,filenames[key]);
  if(!fs.existsSync(dest)) fs.copyFileSync(file,dest);
}
const read=f=>fs.readFileSync(path.join(inputs,f),'utf8').replace(/^\uFEFF/,'');
const dump=JSON.parse(read('dump.json').replace(/(?<=[\[,:]\s*)-?(?:Infinity|NaN)(?=\s*[\],}])/g,'null'));
const manifest=read(filenames.manifest);
const build=manifest.match(/"buildid"\s+"([^"]+)"/)[1];
if(build!=='26332184') throw Error('Source build mismatch');
const ids=new Map();
for(const m of read('ObjectID.json').matchAll(/^\s*"([^"]+)"\s*:\s*(-?\d+)/gm)) {
  const id=Number(m[2]); if(id<=0) continue;
  if(!ids.has(id)) ids.set(id,[]); ids.get(id).push(m[1]);
}
const records=new Map();
for(let i=0;i<dump.objects.length;i++) {
  const o=dump.objects[i],key=`${o.id}:${o.var??0}`;
  if(records.has(key)) throw Error(`duplicate ${key}`);
  records.set(key,{key,id:o.id,variation:o.var??0,names:ids.get(o.id)||[],ko:o.ko||null,en:o.en||null,type:o.type||null,raw:o,dumpIndex:i,interactions:[],unresolved:[]});
}
for(const [id,names] of ids) if(![...records.values()].some(o=>o.id===id)) records.set(`${id}:0`,{key:`${id}:0`,id,variation:0,names,ko:null,en:null,type:null,raw:null,dumpIndex:null,interactions:[],unresolved:['ObjectID에만 등록됨; 덤프에 없어 타입·변형·상호작용 미확인']});
const all=[...records.values()].sort((a,b)=>a.id-b.id||a.variation-b.variation);
const stations=new Map(),uses=new Map();
for(const o of all) {
  for(const entry of o.raw?.canCraft||[]) {const [id,n]=entry;if(!stations.has(id))stations.set(id,[]);stations.get(id).push({key:o.key,amount:n,rawEntry:entry,dumpIndex:o.dumpIndex});}
  for(const [id,n] of o.raw?.req||[]) {if(!uses.has(id))uses.set(id,[]);uses.get(id).push({key:o.key,amount:n,dumpIndex:o.dumpIndex});}
}
const equipment=new Set(['Helm','BreastArmor','PantsArmor','Necklace','Ring','Offhand','Bag','Lantern','Pouch','MeleeWeapon','RangeWeapon','SummoningWeapon','ThrowingWeapon','Shovel','Hoe','MiningPick','FishingRod','BugNet','Sledge','RoofingTool','DrillTool','BeamWeapon','WaterCan','Bucket','Seeder','PaintTool','Instrument']);
for(const o of all) {
  const s=stations.get(o.id)||[];
  // Craft output lists carry ObjectID, not output variation. Keep applicability explicit.
  if(s.length) o.interactions.push({verb:'craft',label:'제작하기',objectRole:'output',subject:{objectId:o.id,variation:null},variationApplicability:'unknown',evidenceLevel:'data',applicability:'ObjectID 단위 제작 목록의 공유 관계. 이 행의 variation을 제작할 수 있다는 주장이 아님',conditions:{stations:s},recipeObservation:o.raw?.req?{observedOn:o.key,ingredients:o.raw.req,recipeToOutputVariation:'unresolved',source:{file:'inputs/dump.json',pointer:`/objects/${o.dumpIndex}/req`}}:null,result:{objectId:o.id,outputVariation:null},sources:s.map(x=>({file:'inputs/dump.json',pointer:`/objects/${x.dumpIndex}/canCraft`}))});
  if(uses.has(o.id)) o.interactions.push({verb:'use_as_ingredient',label:'제작 재료로 사용하기',objectRole:'ingredient',subject:{objectId:o.id,variation:null},variationApplicability:'unknown',evidenceLevel:'data',applicability:'ObjectID 단위 재료 목록의 공유 관계. 이 행의 variation을 재료로 쓸 수 있다는 주장이 아님',conditions:{recipes:uses.get(o.id)},sources:uses.get(o.id).map(x=>({file:'inputs/dump.json',pointer:`/objects/${x.dumpIndex}/req`}))});
  if(o.raw?.canCraft?.length) o.interactions.push({verb:'craft_at',label:o.id===6000?'손으로 제작하기':'제작 장소로 사용하기',objectRole:'station',subject:{objectId:o.id,variation:o.variation},evidenceLevel:'data',applicability:'이 덤프 행의 제작 목록 관측. 실제 설치·해금·거리 조건은 미확인',conditions:{outputs:o.raw.canCraft},sources:[{file:'inputs/dump.json',pointer:`/objects/${o.dumpIndex}/canCraft`}]});
  o.group=equipment.has(o.type)?'equipment':o.type==='PlaceablePrefab'?'world':['Creature','Critter','Pet','TrainingDummy','PlayerType'].includes(o.type)?'creatures':'items';
  o.unresolved.push('기록된 상호작용은 확인된 부분집합; 미기록을 불가능으로 해석하지 않음');
  if(o.raw?.noPrefab)o.unresolved.push('덤프에 noPrefab 표시; 실제 사용 가능성 미확인');
}
const write=(name,value)=>fs.writeFileSync(path.join(root,name),JSON.stringify(value,null,2)+'\n');
write('sources.json',{build,dumpTime:dump.time,dumpErrors:dump.errors,scope:'고정 ObjectID 목록과 기존 runtime dump의 합집합. 타일 좌표·액체·추상 대상·미등록 생성물 전부를 포괄한다고 주장하지 않음.',sources:Object.entries(sources).map(([key,file])=>({original:file,snapshot:`inputs/${filenames[key]}`,sha256:sha(fs.readFileSync(path.join(inputs,filenames[key])))}))});
write('objects.json',all);
for(const group of ['equipment','world','creatures','items'])write(`input-${group}.json`,all.filter(x=>x.group===group));
const pilot=all.find(x=>x.id===8901&&x.variation===0);
write('pilot-iron-sword.json',pilot);
console.log(JSON.stringify({build,dumpRows:dump.objects.length,totalRecords:all.length,idsOnly:all.filter(x=>!x.raw).length,groups:Object.fromEntries(['equipment','world','creatures','items'].map(g=>[g,all.filter(x=>x.group===g).length])),pilot:{key:pilot.key,name:pilot.ko,interactions:pilot.interactions.map(x=>x.label),ingredients:pilot.raw.req,stations:(stations.get(8901)||[]).map(s=>({key:s.key,name:records.get(s.key)?.ko}))}},null,2));
