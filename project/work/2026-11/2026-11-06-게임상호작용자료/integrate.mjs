import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,''));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const fail=m=>{throw Error(m)};
const sources=read('sources.json'), objects=read('objects.json');
const sourceMap=new Map(sources.sources.map(s=>[s.snapshot,s]));
for(const s of sources.sources) if(hash(fs.readFileSync(path.join(root,s.snapshot)))!==s.sha256) fail(`snapshot hash mismatch: ${s.snapshot}`);
const dump=JSON.parse(fs.readFileSync(path.join(root,'inputs/dump.json'),'utf8').replace(/^\uFEFF/,'').replace(/(?<=[\[,:]\s*)-?(?:Infinity|NaN)(?=\s*[\],}])/g,'null'));
const pinned=read('code-sources.json');
const pinMap=new Map(pinned.sources.map(s=>[s.file,s.sha256]));
if(pinMap.size!==pinned.sources.length) fail('duplicate pinned code source');
const cache=new Map();
function source(s,where){
 if(!s || typeof s.file!=='string') fail(`${where}: missing source file`);
 if(s.pointer!==undefined){
  if(!sourceMap.has(s.file)) fail(`${where}: unregistered pointer source ${s.file}`);
  if(typeof s.pointer!=='string'||!s.pointer.startsWith('/')) fail(`${where}: invalid pointer`);
  let v=s.file==='inputs/dump.json'?dump:read(s.file);
  for(const p of s.pointer.slice(1).split('/').map(x=>x.replace(/~1/g,'/').replace(/~0/g,'~'))) {if(v===null||typeof v!=='object'||!Object.hasOwn(v,p)) fail(`${where}: missing pointer ${s.pointer}`);v=v[p];}
 } else {
  if(!path.isAbsolute(s.file)||!Number.isInteger(s.line)||!Number.isInteger(s.endLine)||s.line<1||s.endLine<s.line||!/^\w{64}$/.test(s.sha256||'')||typeof s.excerpt!=='string'||!s.excerpt) fail(`${where}: invalid code source`);
  if(!cache.has(s.file)){const b=fs.readFileSync(s.file);cache.set(s.file,{hash:hash(b),lines:b.toString('utf8').split(/\r?\n/)});}
  const c=cache.get(s.file); if(!pinMap.has(s.file)||pinMap.get(s.file)!==s.sha256) fail(`${where}: unpinned or changed code source ${s.file}`); if(c.hash!==s.sha256||s.endLine>c.lines.length) fail(`${where}: code hash/line mismatch ${s.file}`);
 }
}
function walk(x,where){if(!x||typeof x!=='object')return;if(Array.isArray(x)){x.forEach((v,i)=>walk(v,`${where}/${i}`));return;}if(typeof x.file==='string'&&(x.pointer!==undefined||x.sha256!==undefined||x.line!==undefined))source(x,where);for(const [k,v] of Object.entries(x))walk(v,`${where}/${k}`);}
const all=new Map();for(const o of objects){if(o.key!==`${o.id}:${o.variation}`||all.has(o.key))fail(`invalid/duplicate object ${o.key}`);all.set(o.key,o);}
if(all.size!==3031)fail(`expected 3031 objects, got ${all.size}`);
const records=[],rules=[],coverage={interpretation:'전체 key 처리 여부이며 모든 상호작용을 발견했다는 뜻이 아니다. 미확인은 불가능이 아니다.',totalInput:all.size,groups:{},evidenceLevels:{},unresolvedRecords:0};
for(const group of ['equipment','world','creatures','items']){
 const input=read(`input-${group}.json`), result=read(`workers/${group}/results.json`); if(result.group!==group||!Array.isArray(result.records)||!Array.isArray(result.rules))fail(`${group}: invalid result`);
 const wanted=new Map();for(const o of input){if(wanted.has(o.key)||!all.has(o.key)||JSON.stringify(all.get(o.key))!==JSON.stringify(o)||o.group!==group)fail(`${group}: invalid input ${o.key}`);wanted.set(o.key,o);}
 const seen=new Set();for(const rec of result.records){const o=wanted.get(rec.key);if(!o||seen.has(rec.key))fail(`${group}: extra/duplicate ${rec.key}`);seen.add(rec.key);if(!Array.isArray(rec.interactions)||!Array.isArray(rec.unresolved))fail(`${group}/${rec.key}: invalid record`);
 const remaining=rec.interactions.map(x=>JSON.stringify(x));for(const original of o.interactions){const idx=remaining.indexOf(JSON.stringify(original));if(idx<0)fail(`${group}/${rec.key}: original interaction not preserved: ${original.verb}`);remaining.splice(idx,1);}
 walk(rec,`${group}/${rec.key}`);for(const i of rec.interactions){coverage.evidenceLevels[i.evidenceLevel]=(coverage.evidenceLevels[i.evidenceLevel]||0)+1;if(i.ruleId&&!result.rules.some(r=>(r.id||r.ruleId)===i.ruleId))fail(`${group}/${rec.key}: missing rule ${i.ruleId}`);}
 if(rec.unresolved.length)coverage.unresolvedRecords++; records.push({...o,interactions:rec.interactions,unresolved:[...new Set([...o.unresolved,...rec.unresolved])],reviewMethod:rec.reviewMethod});}
 if(seen.size!==wanted.size)fail(`${group}: missing ${[...wanted.keys()].filter(k=>!seen.has(k)).join(', ')}`);
 if(result.coverage?.inputCount!==wanted.size||result.coverage?.outputCount!==seen.size||result.coverage?.unresolvedCount!==result.records.filter(r=>r.unresolved.length).length)fail(`${group}: coverage mismatch`);
 walk(result.rules,`${group}/rules`);rules.push(...result.rules.map(r=>({group,...r})));coverage.groups[group]={input:wanted.size,output:seen.size,unresolved:result.coverage.unresolvedCount,rules:result.rules.length};
}
if(records.length!==all.size||new Set(records.map(o=>o.key)).size!==all.size)fail('group union mismatch');
records.sort((a,b)=>a.id-b.id||a.variation-b.variation);
const write=(p,v)=>fs.writeFileSync(path.join(root,p),JSON.stringify(v,null,2)+'\n');
write('collection.json',{build:sources.build,scope:sources.scope,sources:sources.sources,records,rules});write('coverage.json',coverage);
const esc=x=>String(x??'').replace(/\|/g,'\\|').replace(/\r?\n/g,' ');
const lines=['# 오브젝트별 상호작용','',`빌드 ${sources.build}. 전체 key 처리는 모든 상호작용의 발견을 뜻하지 않는다. 미확인은 불가능을 뜻하지 않는다. code_candidate는 프리팹 연결·빌드 일치·실행 미확인. ObjectID 공유 관계의 변형 적용과 recipeObservation의 출력 변형 연결은 미확인이다. 근거 참조는 collection.json의 해당 key와 규칙 또는 표시된 파일·pointer를 확인한다.`, '', '| key | 이름 | 행위 (역할; 근거 수준; 근거 참조) | 미확인 |','|---|---|---|---|'];
for(const o of records)lines.push(`| ${esc(o.key)} | ${esc(o.ko||o.en||o.names.join(', ')||'이름 미확인')} | ${esc(o.interactions.map(i=>`${i.label||i.verb} (${i.objectRole||'역할 미확인'}; ${i.evidenceLevel||'미확인'}${i.variationApplicability==='unknown'||i.subject?.variation===null?'; ObjectID 공유 관계·이 변형 적용 미확인':''}${i.recipeObservation?'; 관측 재료와 출력 변형 연결 미확인':''}; ${i.ruleId?`규칙 ${i.ruleId}`:(i.sources||[]).map(s=>s.pointer?`${s.file}#${s.pointer}`:`${s.file}:${s.line}`).join(', ')||'근거 미확인'})`).join('; ')||'확인된 행위 없음')} | ${esc(o.unresolved.join('; ')||'없음')} |`);
fs.writeFileSync(path.join(root,'objects-interactions.md'),lines.join('\n')+'\n');console.log(JSON.stringify(coverage,null,2));
