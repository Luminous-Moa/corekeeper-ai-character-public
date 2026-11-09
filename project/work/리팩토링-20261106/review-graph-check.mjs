import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import cp from 'node:child_process';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const stage=path.join(root,'graph-stage');
const read=(p)=>JSON.parse(fs.readFileSync(path.join(stage,p),'utf8').replace(/^\uFEFF/,''));
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const git=(...args)=>cp.execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK-refactor-20261106',...args],{maxBuffer:20*1024*1024});
const commit='902721ec505df42d74d0b2d245ed34a049faadad';
const G='work/지식그래프',B='work/2026-11/2026-11-05-지식그래프-개편/B',L='work/2026-11/2026-11-05-지식그래프-실화면';
const before=read('canonical-before.json');
console.log('canonical',JSON.stringify({checked:before.length,changed:before.filter(x=>!fs.existsSync(x.path)||sha(fs.readFileSync(x.path))!==x.sha256).map(x=>x.path)}));
const entries=git('ls-tree','-r',commit).toString().trim().split('\n').map(s=>{let [a,p]=s.split('\t');return {p,hash:a.split(' ')[2]};});
const cfg=read(G+'/tools/config.json');
const paths=cfg.codeFiles.map(x=>typeof x==='string'?x:x.path);
const expected=entries.filter(x=>(x.p.startsWith('Assets/AIPlayerSpike/Scripts/')||x.p==='Assets/HeadlessTools/Editor/HeadlessSdk.cs')&&x.p.endsWith('.cs')).map(x=>x.p);
console.log('configFiles',JSON.stringify({count:paths.length,expected:expected.length,missing:expected.filter(x=>!paths.includes(x)),extra:paths.filter(x=>!expected.includes(x))}));
function walk(d){return fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]);}
for(const [name,prefixes] of [['snapshot',['Assets/AIPlayerSpike/','Assets/HeadlessTools/','Tools/ai-bridge/']],['runtime-package',['Tools/ai-bridge/','Tools/knowledge/']]]){
 const base=path.join(stage,B,name),actual=walk(base).map(x=>path.relative(base,x).replaceAll('\\','/'));
 const exp=entries.filter(x=>prefixes.some(p=>x.p.startsWith(p)));
 const bad=exp.filter(e=>{if(!actual.includes(e.p))return true;const b=fs.readFileSync(path.join(base,e.p));return crypto.createHash('sha1').update(Buffer.from(`blob ${b.length}\0`)).update(b).digest('hex')!==e.hash;}).map(x=>x.p);
 console.log(name,JSON.stringify({actual:actual.length,expected:exp.length,mismatched:bad,extra:actual.filter(x=>!exp.some(e=>e.p===x))}));
}
const dev=read(G+'/graph/graph.json'),prod=read(B+'/B.graph.json');
const canonical=JSON.parse(fs.readFileSync('C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/지식그래프/graph/graph.json','utf8'));
for(const type of ['기능','정책']){
 const a=canonical.nodes.filter(x=>x.type===type).map(x=>x.id).sort(),b=dev.nodes.filter(x=>x.type===type).map(x=>x.id).sort();
 console.log('ids',type,JSON.stringify({before:a.length,after:b.length,equal:JSON.stringify(a)===JSON.stringify(b)}));
}
for(const name of ['AIGoalKinds.Of','AIGoalFeatures.AddCandidates','AICandidateAssembler.Select','AIBody.HandleCommand','AIMemory.Save','AICharacterStore.SaveFromServer','AIDoorFacts.FirstAt','AIEdge.ObserveThreat']){
 const n=dev.nodes.find(x=>x.id==='code:'+name),p=prod.nodes.filter(x=>x.signature?.startsWith(name+'('));
 const [file,line]=n.loc.split(':');const start=Number(line.split('-')[0]);
 const code=git('show',commit+':'+file).toString('utf8').split(/\r?\n/);
 console.log('sample',JSON.stringify({name,loc:n.loc,features:n.features,signature:n.signature,source:code.slice(start-1,start+2),product:p.map(x=>({id:x.id,file:x.file,lineStart:x.lineStart,lineEnd:x.lineEnd}))}));
}
console.log('auditSHA',sha(fs.readFileSync(path.join(stage,'stage-audit.json'))));
