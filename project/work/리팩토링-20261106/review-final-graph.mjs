import fs from 'node:fs';import path from 'node:path';import cp from 'node:child_process';import crypto from 'node:crypto';
const root='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work',G=root+'/지식그래프',B=root+'/2026-11/2026-11-05-지식그래프-개편/B',L=root+'/2026-11/2026-11-05-지식그래프-실화면',sha='902721ec505df42d74d0b2d245ed34a049faadad';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));const cfg=read(G+'/tools/config.json'),dev=read(G+'/graph/graph.json'),b=read(B+'/B.graph.json'),d=read(L+'/D/graph.json');
const bad=p=>/graph-stage|CoreKeeperModSDK-refactor-20261106/.test(p);
console.log('config',JSON.stringify({codeRoot:cfg.codeRoot,sessionRoot:cfg.sessionRoot,stagedDocumentRoot:cfg.stagedDocumentRoot??null,count:cfg.codeFiles.length}));
console.log('baseline',JSON.stringify({B:b.meta.sourceCommit,D:d.meta.sourceCommit,oldScreen:d.meta.installedSourceCommit,originalInference:read(B+'/inference.graph.json').meta.commit,supplement:d.supplementalRuntimeObservations.sourceCommit}));
console.log('activeSourceStagePaths',JSON.stringify([['G',dev],['B',b],['D',d]].map(([name,g])=>({name,bad:(g.meta.sources??[]).map(x=>x.path).filter(bad)}))));
const ents=cp.execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','ls-tree','-r',sha],{maxBuffer:20*1024*1024}).toString().trim().split('\n').map(s=>{const[a,p]=s.split('\t');return {p,hash:a.split(' ')[2]};});
const walk=p=>fs.readdirSync(p,{withFileTypes:true}).flatMap(x=>x.isDirectory()?walk(path.join(p,x.name)):[path.join(p,x.name)]);
for(const [name,prefixes] of [['snapshot',['Assets/AIPlayerSpike/','Assets/HeadlessTools/','Tools/ai-bridge/']],['runtime-package',['Tools/ai-bridge/','Tools/knowledge/']]]){
const dir=path.join(B,name),actual=walk(dir).map(x=>path.relative(dir,x).replaceAll('\\','/')),expected=ents.filter(e=>prefixes.some(p=>e.p.startsWith(p)));
const wrong=expected.filter(e=>{if(!actual.includes(e.p))return true;const v=fs.readFileSync(path.join(dir,e.p));return crypto.createHash('sha1').update(Buffer.from(`blob ${v.length}\0`)).update(v).digest('hex')!==e.hash;}).map(e=>e.p);
console.log(name,JSON.stringify({count:actual.length,expected:expected.length,wrong,extra:actual.filter(x=>!expected.some(e=>e.p===x))}));
}
