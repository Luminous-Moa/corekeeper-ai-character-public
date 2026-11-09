import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {scenes,choices,modelBrief} from './core.mjs';
const ROOT=path.dirname(fileURLToPath(import.meta.url)),PILOT=path.dirname(ROOT);
const results=[];
const check=(ok,name)=>{results.push({name,ok});if(!ok)throw Error(name);};
const batch=fs.readFileSync(path.join(ROOT,'batch.mjs'),'utf8');
const instructions=batch.match(/instructions:'([^']+)'/)[1];
let upper=0,decisions=0;
for(const f of ['smoke/s1-A','smoke/s1-B','remaining/s2-A','remaining/s2-B','remaining/s3-A','remaining/s3-B']){
 const old=JSON.parse(fs.readFileSync(path.join(PILOT,'runs',f+'.json'),'utf8')),s=scenes.find(x=>x.id===old.scene);
 for(const e of old.events){
  if(e.type==='upper'){check(modelBrief(old.route,e.reason)===e.prompt.brief,`${f} upper brief ${++upper} byte-identical`);check(JSON.stringify(s.obs)===JSON.stringify(e.prompt.turn.observation),`${f} upper observation unchanged`);}
  if(e.type==='decisions'){const input=JSON.parse(e.prompt.input);check(instructions===e.prompt.questions[0].instructions,`${f} decisions instructions ${++decisions} byte-identical`);check(JSON.stringify(choices(s,old.route,{plan:input.plan,retrieved:input.retrieved}))===JSON.stringify(e.prompt.questions[0].choices),`${f} choices unchanged`);check(JSON.stringify(s.obs)===JSON.stringify(input.observation),`${f} decisions observation unchanged`);}
 }
}
const queue=JSON.parse(fs.readFileSync(path.join(ROOT,'queue.json'),'utf8'));
const ids=queue.pairs.flatMap(p=>p.routes.map(r=>p.pairId+'-'+r));
check(ids.length===600&&new Set(ids).size===600,'600 unique trial IDs');
for(const s of scenes){for(const route of ['A','B']){check(queue.pairs.filter(p=>p.scene===s.id).flatMap(p=>p.routes).filter(r=>r===route).length===100,`${s.id}-${route} exactly100`);check(queue.pairs.filter(p=>p.scene===s.id&&p.routes[0]===route).length===50,`${s.id} ${route}-first 50pairs`);}}
// Exercise the actual terminal predicate with a fake filesystem reader; no simulated model answers.
const terminalSource=batch.match(/function terminal\(id\)\{[^\n]+/)[0];
const statuses={done:{status:'completed'},error:{status:'failed'},blocked:{status:'blocked'},running:{status:'running'}};
const terminal=new Function('statusOf','return ('+terminalSource+');')(id=>statuses[id]);
check(terminal('done')&&terminal('error'),'completed AND failed terminal trials skipped on resume');
check(!terminal('blocked')&&!terminal('running')&&!terminal('new'),'blocked/interrupted/new trials remain resumable');
const out={at:new Date().toISOString(),modelCalls:0,checks:results.length,upperPromptsCompared:upper,decisionsPromptsCompared:decisions,results};
fs.writeFileSync(path.join(ROOT,'validation.json'),JSON.stringify(out,null,2));
console.log(JSON.stringify({ok:true,modelCalls:0,checks:results.length,upperPromptsCompared:upper,decisionsPromptsCompared:decisions}));
