import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../../..'),B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B'),live=path.join(work,'2026-11/2026-11-05-지식그래프-실화면');
const target=process.argv[2],old=JSON.parse(fs.readFileSync(path.join(B,'code.graph.json'),'utf8')).meta.sourceCommit,repo='C:/Dev/CoreKeeperModSDK';
if(!/^[0-9a-f]{40}$/.test(target??''))throw Error('final SHA required');
const paths=['Assets/AIPlayerSpike','Assets/HeadlessTools','Tools/ai-bridge','Tools/knowledge'];
const changed=execFileSync('git',['-C',repo,'diff','--name-only',old,target,'--',...paths],{encoding:'utf8'}).trim();if(changed)throw Error('source inputs changed, re-extract needed '+changed);
const hashes={};for(const name of ['csharp.raw.json','javascript.raw.json'])hashes[name]=crypto.createHash('sha256').update(fs.readFileSync(path.join(B,name))).digest('hex');
const allChanged=execFileSync('git',['-C',repo,'diff','--name-only',old,target],{encoding:'utf8'}).trim().split(/\r?\n/).filter(Boolean);
for(const name of ['pin-snapshot.mjs','build.mjs','verify.mjs']){const f=path.join(B,'extractor',name),t=fs.readFileSync(f,'utf8');if(!t.includes(old))throw Error('old commit absent '+name);fs.writeFileSync(f,t.replaceAll(old,target));}
const overlayFile=path.join(live,'C/overlay-source.json'),overlay=JSON.parse(fs.readFileSync(overlayFile,'utf8'));overlay.baseCommit=target;fs.writeFileSync(overlayFile,JSON.stringify(overlay,null,2)+'\n');
for(const f of [path.join(work,'2026-11/2026-11-07-기획연결/source.mjs'),path.join(work,'지식그래프/기획/기능.md'),path.join(live,'D/bag-source.mjs'),path.join(stage,'successor-projection.json'),path.join(stage,'new-code-mappings.json')])fs.writeFileSync(f,fs.readFileSync(f,'utf8').replace(/^\uFEFF/,'').replaceAll(old,target));
fs.writeFileSync(path.join(stage,'test-head-source-proof.json'),JSON.stringify({ok:true,rawExtractionCommit:old,finalPinCommit:target,checkedInputScopes:paths,changedInputPaths:[],allChangedPaths:allChanged,rawSha256:hashes,roslynReexecuted:false,boundary:'test-only commit outside all extraction inputs; exact source equality checked before retaining raw extraction'},null,2)+'\n');
console.log('test-only head pinned with exact input equality '+old+' -> '+target);
