import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync,execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url)), work=path.resolve(here,'../..'), legacy=path.join(work,'지식그래프'), stage=path.join(here,'graph-stage'), data=path.join(here,'graph-review-data');
fs.mkdirSync(data,{recursive:true});
const reports=[];
for(const args of [['check'],['intent','P.unrestricted-actions'],['intent','P.unrestricted-choice'],['intent','P.unrestricted-guards'],['policy','pol.action-constraints-open'],['source','P.unrestricted-support'],['show','R.unrestriction-final-review']]){
 const r=spawnSync(process.execPath,[path.join(legacy,'tools/product.mjs'),...args],{encoding:'utf8',maxBuffer:8*1024*1024});
 fs.writeFileSync(path.join(data,args.join('_').replaceAll(':','-')+'.txt'),r.stdout+'\nSTDERR\n'+r.stderr);reports.push({args,exit:r.status,stderr:r.stderr});
 if(args[0]==='check'){const j=JSON.parse(r.stdout);reports.at(-1).result={ok:j.ok,structuralErrors:j.structuralErrors,stale:j.stale,baselineDrift:j.baselineDrift};}
}
const g=JSON.parse(fs.readFileSync(path.join(work,'2026-11/2026-11-05-지식그래프-실화면/D/graph.json'),'utf8'));
const requested=JSON.parse(fs.readFileSync(path.join(stage,'new-code-mappings.json'),'utf8'));
const anchors=requested.mapping.map(([from,sig])=>{const ns=g.nodes.filter(n=>n.type==='코드'&&n.signature?.startsWith(sig));if(ns.length!==1)return {from,sig,ok:false,matches:ns.length};const n=ns[0],edges=g.edges.filter(e=>e.from===from&&e.to===n.id&&e.kind==='제약해제 최종후보 구현 위치');const bytes=execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','cat-file','blob',g.meta.sourceCommit+':'+n.file]);const snapshot=fs.readFileSync(path.join(work,'2026-11/2026-11-05-지식그래프-개편/B/snapshot',n.file));return {from,sig,code:n.id,lineStart:n.lineStart,lineEnd:n.lineEnd,blobMatch:bytes.equals(snapshot),edgeCount:edges.length,runtimeTrace:edges[0]?.runtimeTrace,ok:bytes.equals(snapshot)&&edges.length===1&&edges[0].runtimeTrace===false};});
const pins=JSON.parse(fs.readFileSync(path.join(stage,'source-pins.json'),'utf8')).map(p=>({id:p.id,ok:crypto.createHash('sha256').update(fs.readFileSync(p.pinned)).digest('hex')===p.sha256}));
const projection=JSON.parse(fs.readFileSync(path.join(stage,'successor-projection.json'),'utf8'));
const summary={source:g.meta.sourceCommit,officialHead:execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','rev-parse','HEAD'],{encoding:'utf8'}).trim(),mappingSource:requested.sourceCommit,checks:reports,anchors,pins:{count:pins.length,fail:pins.filter(p=>!p.ok)},projection};
fs.writeFileSync(path.join(data,'summary.json'),JSON.stringify(summary,null,2));console.log(JSON.stringify(summary,null,2));
