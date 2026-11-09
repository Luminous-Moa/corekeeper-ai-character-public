import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const text=fs.readFileSync(path.join(root,'A2/screens.md'),'utf8');
const declaration=JSON.parse([...text.matchAll(/```json\s*\r?\n([\s\S]*?)\r?\n```/g)][0][1]);
const candidates=declaration.nodes.filter(n=>n.type==='화면요소' && n.location?.jsonPointer && (n.visibleValue===null || typeof n.visibleValue!=='object'));
let state=20261105;
const random=()=>{state=(Math.imul(state,1664525)+1013904223)>>>0;return state/4294967296;};
const shuffled=[...candidates];
for(let i=shuffled.length-1;i>0;i--){const j=Math.floor(random()*(i+1));[shuffled[i],shuffled[j]]=[shuffled[j],shuffled[i]];}
const selected=shuffled.slice(0,7);
const results=selected.map(node=>{
  const raw=JSON.parse(fs.readFileSync(path.join(root,node.location.rawFile),'utf8').replace(/^\uFEFF/,''));
  const actual=node.location.jsonPointer.split('/').slice(1).map(s=>s.replaceAll('~1','/').replaceAll('~0','~')).reduce((value,key)=>value[key],raw);
  return {id:node.id,document:'A2/screens.md',rawFile:node.location.rawFile,jsonPointer:node.location.jsonPointer,declared:node.visibleValue,actual,pass:JSON.stringify(actual)===JSON.stringify(node.visibleValue)};
});
const out={seed:20261105,candidateCount:candidates.length,sampleCount:results.length,selection:'Seeded Fisher-Yates shuffle of scalar JSON-pointer screen elements; first seven',results,passed:results.every(r=>r.pass),limit:'These samples verify output facts, not full product coverage or source code.'};
fs.writeFileSync(path.join(root,'sample-check.json'),JSON.stringify(out,null,2)+'\n');
console.log(JSON.stringify(out));
if(!out.passed)process.exitCode=1;
