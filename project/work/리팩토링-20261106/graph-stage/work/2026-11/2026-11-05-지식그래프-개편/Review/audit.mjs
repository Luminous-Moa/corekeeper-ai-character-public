import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const read=f=>JSON.parse(fs.readFileSync(path.join(root,f),'utf8'));
const hash=f=>crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
const b=read('B/code.graph.json'), manifest=read('B/snapshot.manifest.json'), preserved=read('preserved/manifest.json');
const ids=new Set(b.nodes.map(n=>n.id));
const r={seed:72945137,hashFailures:[],duplicates:b.nodes.filter((n,i)=>b.nodes.findIndex(x=>x.id===n.id)!==i).map(n=>n.id),brokenEdges:b.edges.filter(e=>!ids.has(e.from)||!ids.has(e.to)),sourceDrift:[],determinismHashes:[],quoteFailures:[]};
for(const f of manifest.files) if(hash(path.join(root,'B/snapshot',f.path))!==f.sha256)r.hashFailures.push(f.path);
for(const f of preserved.files){if(hash(path.join(root,'preserved',f.snapshot))!==f.sha256)r.hashFailures.push(f.snapshot);if(fs.existsSync(f.source)&&hash(f.source)!==f.sha256)r.sourceDrift.push(f.source);}
for(const [f,h] of Object.entries(read('B/determinism.json').second))r.determinismHashes.push({file:f,match:hash(path.join(root,'B',f))===h});
for(const q of read('C/evidence-index.json').checks) if(!fs.readFileSync(path.join(root,q.snapshot),'utf8').includes(q.quote))r.quoteFailures.push(q);
const reviewed=new Set(read('B/validation-review.json').samples.map(x=>x.id));
let seed=r.seed;
const rand=()=>((seed=(Math.imul(seed,1664525)+1013904223)>>>0)/4294967296);
const pool=b.nodes.filter(n=>n.type==='기능'&&!reviewed.has(n.id)&&!n.id.startsWith('ai-bridge:')&&n.id!=='slash:ai').sort((a,b)=>a.id.localeCompare(b.id));
for(let i=pool.length-1;i>0;i--){let j=Math.floor(rand()*(i+1));[pool[i],pool[j]]=[pool[j],pool[i]];}
r.codeSamples=pool.slice(0,4).map(n=>({node:n,edges:b.edges.filter(e=>e.from===n.id)}));
const m=read('C/legacy-capability-map.json').mappings;r.legacyCapabilities={count:m.length,unique:new Set(m.map(x=>x.legacyId)).size};
fs.writeFileSync(path.join(root,'Review/fixed-audit.json'),JSON.stringify(r,null,2)+'\n');
console.log(JSON.stringify(r,null,2));
