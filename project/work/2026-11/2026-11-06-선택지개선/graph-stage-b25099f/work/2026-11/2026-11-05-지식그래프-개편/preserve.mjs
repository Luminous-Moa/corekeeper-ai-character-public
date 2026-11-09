import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const session=path.resolve(here,'../../..');
const legacy=path.join(session,'work/지식그래프');
const out=path.join(here,'preserved');
if(fs.existsSync(path.join(out,'manifest.json'))) throw new Error('Preservation already exists; do not overwrite');
fs.mkdirSync(out,{recursive:true});
const files=[];
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
function copy(src,rel,role){
 const bytes=fs.readFileSync(src); const target=path.join(out,rel);
 fs.mkdirSync(path.dirname(target),{recursive:true});fs.writeFileSync(target,bytes,{flag:'wx'});
 files.push({source:src.replaceAll('\\','/'),snapshot:rel,sha256:sha(bytes),bytes:bytes.length,role});
}
function walk(dir,base='') {for(const ent of fs.readdirSync(dir,{withFileTypes:true})){const rel=path.join(base,ent.name); if(ent.isDirectory())walk(path.join(dir,ent.name),rel);else copy(path.join(dir,ent.name),`legacy/${rel.replaceAll('\\','/')}`,'legacy');}}
walk(legacy);
const g=JSON.parse(fs.readFileSync(path.join(legacy,'graph/graph.json'),'utf8'));
const refs=[...new Set(g.nodes.flatMap(n=>(n.sources||[]).map(s=>s.path)))].sort();
const missing=[];
for(const rel of refs){const src=path.resolve(session,rel);if(!fs.existsSync(src)){missing.push(rel);continue;}copy(src,`evidence/${sha(Buffer.from(rel)).slice(0,16)}${path.extname(rel)}`,'authority-evidence');}
fs.writeFileSync(path.join(out,'manifest.json'),JSON.stringify({capturedAt:new Date().toISOString(),session,legacy,files,missing},null,2)+'\n');
console.log(JSON.stringify({files:files.length,evidence:refs.length,missing},null,2));
