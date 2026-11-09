import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const C=path.dirname(fileURLToPath(import.meta.url)),root=path.dirname(C);
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,'');
const blocks=[...read('C/기획/그래프-선언.md').matchAll(/```json\s*([\s\S]*?)```/g)];
if(blocks.length!==1)throw Error('Expected exactly one declaration block');
const g=JSON.parse(blocks[0][1]),A=JSON.parse(read('A/graph-A.json')),B=JSON.parse(read('B/code.graph.json'));
const local=new Set(),external=new Set([...A.nodes,...B.nodes].map(n=>n.id)),errors=[];
for(const n of g.nodes){if(local.has(n.id)||external.has(n.id))errors.push('Duplicate '+n.id);local.add(n.id);if(!fs.existsSync(path.join(root,n.doc)))errors.push('Missing doc '+n.doc);}
const allowed=new Set(['유저 활동→유저 활동','유저 활동→화면요소','정책→기능']);
for(const e of g.edges){if(!allowed.has(e.type))errors.push('Invalid edge '+e.type);for(const id of [e.from,e.to])if(!local.has(id)&&!external.has(id))errors.push('Missing target '+id);}
for(const r of g.meta.reconciliation)if(!external.has(r.sourceId)||(!local.has(r.canonicalId)&&!external.has(r.canonicalId)))errors.push('Invalid reconciliation '+r.sourceId);
const maps=JSON.parse(read('C/legacy-capability-map.json')).mappings;
if(maps.length!==36||new Set(maps.map(m=>m.legacyId)).size!==36)errors.push('Legacy 36 mapping failed');
for(const m of maps)for(const id of m.standardFunctionIds)if(!B.nodes.some(n=>n.id===id&&n.type==='기능'))errors.push('Invalid function '+id);
const serialized=JSON.stringify(g,null,2)+'\n';
fs.writeFileSync(path.join(C,'graph-C.json'),serialized);
fs.writeFileSync(path.join(C,'validation-C.json'),JSON.stringify({ok:errors.length===0,errors,nodes:g.nodes.length,edges:g.edges.length,legacyCapabilities:maps.length,source:'C/기획/그래프-선언.md',sha256:crypto.createHash('sha256').update(serialized).digest('hex'),note:'문서/참조 검사. 게임 화면 검증이 아니며 사용자 유예 부분은 미완료로 유지.'},null,2)+'\n');
console.log(JSON.stringify({ok:errors.length===0,errors,nodes:g.nodes.length,edges:g.edges.length}));
if(errors.length)process.exitCode=1;
