import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const session='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터',work=session+'/work',stage=work+'/리팩토링-20261106/graph-stage';
const roots=['지식그래프','2026-11/2026-11-05-지식그래프-개편','2026-11/2026-11-05-지식그래프-실화면'];
const omit=new Set(['node_modules','bin','obj','snapshot','runtime-package','.git']);
const manifest=[];
function copy(from,to){fs.mkdirSync(to,{recursive:true});for(const e of fs.readdirSync(from,{withFileTypes:true})){if(omit.has(e.name))continue;const a=path.join(from,e.name),b=path.join(to,e.name);if(e.isDirectory())copy(a,b);else if(e.isFile()){const bytes=fs.readFileSync(a);fs.writeFileSync(b,bytes);manifest.push({path:a,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),bytes:bytes.length});}}}
if(fs.existsSync(stage))throw Error('stage already exists; preserve existing work');
for(const r of roots)copy(work+'/'+r,stage+'/work/'+r);
copy(work+'/'+roots[1]+'/B/extractor/node_modules/acorn',stage+'/work/'+roots[1]+'/B/extractor/node_modules/acorn');
fs.writeFileSync(stage+'/canonical-before.json',JSON.stringify(manifest,null,2)+'\n');
const G=stage+'/work/'+roots[0],P=stage+'/work/'+roots[1],L=stage+'/work/'+roots[2],repo='C:/Dev/CoreKeeperModSDK-refactor-20261106',sha='902721ec505df42d74d0b2d245ed34a049faadad';
const config=JSON.parse(fs.readFileSync(G+'/tools/config.json','utf8'));config.sessionRoot=session;config.codeRoot=repo;
function cs(dir){return fs.readdirSync(repo+'/'+dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?cs(dir+'/'+e.name):e.name.endsWith('.cs')?[dir+'/'+e.name]:[]);}
config.codeFiles=[...cs('Assets/AIPlayerSpike/Scripts'),'Assets/HeadlessTools/Editor/HeadlessSdk.cs'].sort();fs.writeFileSync(G+'/tools/config.json',JSON.stringify(config,null,2)+'\n');
for(const f of ['pin-snapshot.mjs','verify.mjs','build.mjs']){const p=P+'/B/extractor/'+f;let s=fs.readFileSync(p,'utf8').replaceAll('a07166f38086e0241c37c601c16452664236813e',sha);if(f==='verify.mjs')s=s.replace("find('StatusText','AIPlayerSpikeMod.cs')","find('StatusText','AIPlayerSpikeMod.Chat.cs')");fs.writeFileSync(p,s);}
const overlay=JSON.parse(fs.readFileSync(L+'/C/overlay-source.json','utf8'));overlay.baseCommit=sha;fs.writeFileSync(L+'/C/overlay-source.json',JSON.stringify(overlay,null,2)+'\n');
for(const f of [L+'/D/integrate.mjs'])fs.writeFileSync(f,fs.readFileSync(f,'utf8').replaceAll('code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs#AIPlayerSpikeMod.TryHandleChatCommand(string)','code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.Chat.cs#AIPlayerSpikeMod.TryHandleChatCommand(string)'));
console.log(JSON.stringify({stage,copied:manifest.length,bytes:manifest.reduce((a,x)=>a+x.bytes,0),codeFiles:config.codeFiles.length,sha}));
