import fs from 'node:fs';
import path from 'node:path';
const S='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터';
const P=S+'/work/2026-11/2026-11-08-이동수면판단보강';
const g=JSON.parse(fs.readFileSync(P+'/baseline-256/graph.json','utf8'));
const ids=['completion:essential:grapple-input-aim-v1','animal-saddle','base:B::CAB-2ebcba97367158c9b08f790c25d642c9:9022154055506081267|saddle','completion:B:new-move-player','completion:B:new-control-vehicle:v2','rule:supp:A:boat_exit','completion:B:moving-exit-feedback','completion:B:place-object:CAB-2ebcba97367158c9b08f790c25d642c9:-7937923918425634137','common:new-conditions-buffer-consumer','bed-sleep','completion:B:eatable:CAB-2ebcba97367158c9b08f790c25d642c9:3667643087846801758'];
const out={};
for(const id of ids){out[id]=Object.fromEntries(Object.entries(g.nodes).filter(([k])=>k===id||k.startsWith(id+'/')));}
fs.writeFileSync(P+'/production/existing-values.json',JSON.stringify(out,null,2)+'\n');
const objects=JSON.parse(fs.readFileSync(S+'/work/게임원본-상호작용/raw/objects.json','utf8'));
const bindings=[];const keys=new Set();
for(const [i,o] of objects.entries()) if([8373,8374,6551,9609].includes(o.objectId)&&o.variation===0){
 const classes=o.objectId===9609?['GivesConditionsWhenConsumedAuthoring']:o.objectId===6551?['MinecartAuthoring']:['OffHandAuthoring'];
 const components=[];
 for(const [li,l] of o.links.entries()) if(l.kind==='authoringRef')for(const [mi,m] of l.matches.entries()) for(const [ci,c] of m.components.entries())if(classes.includes(c.script?.class)){
  components.push({pointer:`/${i}/links/${li}/matches/${mi}/components/${ci}`,linkKind:l.kind,linkAddress:l.address,prefabKey:m.prefabKey,componentKey:c.key,class:c.script.class});keys.add(c.key);
 }
 bindings.push({objectId:o.objectId,variation:o.variation,sourceKey:o.sourceKey,objectPointer:`/${i}`,sourceRawByteSha256:o.sourceRawByteSha256,prefabInfo:o.objectInfo.prefabInfo,components});
}
const components=fs.readFileSync(S+'/work/게임원본-상호작용/raw/components.jsonl','utf8').split(/\r?\n/);
const selected=[];
for(let i=0;i<components.length;i++)if([...keys].some(k=>components[i].includes('"key":"'+k+'"'))){const c=JSON.parse(components[i]);if(keys.has(c.key))selected.push({line:i+1,value:c});}
fs.writeFileSync(P+'/production/source-bindings.json',JSON.stringify({bindings,components:selected},null,2)+'\n');
console.log(JSON.stringify({existingRoots:ids.map(id=>({id,exists:!!g.nodes[id]})),bindings:bindings.map(x=>({objectId:x.objectId,components:x.components})),components:selected.map(x=>({line:x.line,key:x.value.key,data:x.value.data}))},null,2));
