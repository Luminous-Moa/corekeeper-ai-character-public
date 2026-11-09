import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url));
const canonical='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터';
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const manifest=[];
function copy(rel){const src=path.join(canonical,rel),dest=path.join(stage,rel);if(!fs.existsSync(src))return;if(fs.statSync(src).isDirectory()){for(const ent of fs.readdirSync(src,{withFileTypes:true})){if(['bin','obj','.git'].includes(ent.name))continue;if(ent.name==='node_modules'){copy(path.posix.join(rel,ent.name,'acorn'));continue;}copy(path.posix.join(rel,ent.name));}}else{fs.mkdirSync(path.dirname(dest),{recursive:true});fs.copyFileSync(src,dest);manifest.push({path:rel,sha256:hash(src)});}}
for(const rel of ['work/지식그래프','work/2026-11/2026-11-05-지식그래프-개편','work/2026-11/2026-11-05-지식그래프-실화면','work/2026-11/2026-11-07-기획연결'])copy(rel);
const P='work/2026-11/2026-11-05-지식그래프-개편';
const product=JSON.parse(fs.readFileSync(path.join(canonical,'work/2026-11/2026-11-05-지식그래프-실화면/D/graph.json'),'utf8'));
for(const source of product.meta.sources){const full=path.resolve(canonical,P,source.path);if(full.startsWith(path.resolve(canonical)+path.sep)&&fs.existsSync(full))copy(path.relative(canonical,full).replaceAll('\\','/'));}
for(const rel of ['agreement.md','integration-spec.md','inventory-spec.md','stage1-result.md','transfer-investigation.md','transfer-rules.md'])copy('work/2026-11/2026-11-07-가방장비구현/'+rel);
for(const rel of ['2026-11-07-가방장비-계획안.md','2026-11-07-가방장비-조사.md'])copy('work/2026-11/'+rel);
const unique=[...new Map(manifest.map(row=>[row.path,row])).values()].sort((a,b)=>a.path.localeCompare(b.path));
fs.writeFileSync(path.join(stage,'canonical-before.json'),JSON.stringify({canonical,files:unique},null,2)+'\n');
console.log('Copied '+unique.length+' canonical files into isolated stage.');
