import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync,spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const task=path.dirname(fileURLToPath(import.meta.url));
const session='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터', work=session+'/work';
const repo='C:/Dev/CoreKeeperModSDK-choice-fixes';
const sha=execFileSync('git',['-C',repo,'rev-parse','HEAD'],{encoding:'utf8'}).trim();
const baseline='902721ec505df42d74d0b2d245ed34a049faadad';
const stage=task+'/graph-stage-'+sha.slice(0,7);
const roots=['지식그래프','2026-11/2026-11-05-지식그래프-개편','2026-11/2026-11-05-지식그래프-실화면'];
const omit=new Set(['node_modules','bin','obj','snapshot','runtime-package','.git']);
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const manifest=[];
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const write=(p,v)=>fs.writeFileSync(p,JSON.stringify(v,null,2)+'\n');
function copy(from,to){fs.mkdirSync(to,{recursive:true});for(const e of fs.readdirSync(from,{withFileTypes:true})){
 if(omit.has(e.name))continue;const a=path.join(from,e.name),b=path.join(to,e.name);
 if(e.isDirectory())copy(a,b);else if(e.isFile()){const bytes=fs.readFileSync(a);fs.writeFileSync(b,bytes);manifest.push({path:a,sha256:hash(bytes)});}
}}
if(fs.existsSync(stage))throw Error('Stage exists; preserve it and use a new revision or resume with recorded commands.');
for(const r of roots)copy(work+'/'+r,stage+'/work/'+r);
copy(work+'/'+roots[1]+'/B/extractor/node_modules/acorn',stage+'/work/'+roots[1]+'/B/extractor/node_modules/acorn');
write(stage+'/canonical-before.json',manifest);
const G=stage+'/work/'+roots[0],P=stage+'/work/'+roots[1],L=stage+'/work/'+roots[2];
const cfg=read(G+'/tools/config.json');cfg.codeRoot=repo;cfg.sessionRoot=session;cfg.stagedDocumentRoot=G;
for(const n of ['AIMoveEstimate','AIPickupOptions','AIWallRisk'])cfg.codeFiles.push({container:'모드(게임 안에서 실행)',path:'Assets/AIPlayerSpike/Scripts/Core/'+n+'.cs'});
cfg.codeFiles.sort((a,b)=>a.path<b.path?-1:a.path>b.path?1:0);write(G+'/tools/config.json',cfg);
for(const f of ['lib.mjs','query.mjs'])fs.copyFileSync(work+'/리팩토링-20261106/graph-stage/work/지식그래프/tools/'+f,G+'/tools/'+f);
const anchors={
 'F.candidates':['AIPickupOptions.Select','AIWallData.RiskText','AIWallRisk.Text','AIWallRisk.RouteText','AIMoveEstimate.Seconds'],
 'F.skill-mine':['AIWallData.RiskText','AIWallRisk.Text'],
 'F.skill-dig':['TunnelPlan.RiskText','AIWallData.RiskText','AIWallRisk.Text'],
 'F.tunnel-detour':['AIWallRisk.RouteText','AIMoveEstimate.Seconds']
};
let current='';const fp=G+'/기획/기능.md';
let ft=fs.readFileSync(fp,'utf8').split('\n').map(l=>{
 const m=l.match(/^### (F\.[\w-]+)/);if(m)current=m[1];
 if(l.startsWith('- 코드 앵커:')&&anchors[current])return l.replace(/\r$/,'')+', '+anchors[current].join(', ');
 return l;
}).join('\n');
ft+='\n\n## 2026-11-06 판단 선택지 개선 후보 '+sha.slice(0,7)+'\n\n목표 재료와 필요한 음식의 직접 줍기 후보를 먼저 보존한다. 같은 필요에서는 자동 유입보다 접근이 필요한 물건을 먼저 남기며 최종 선택은 모델이 한다. 프리팹의 IsExplosiveCD로 확인한 폭발성 벽의 위험과 자동 후퇴 없는 실행 한계를 후보·계속하기에 알린다. 본/대안 경로는 digDetour로 구분한다. 걷기 표시 ETA는 5.7칸/초로 통일하고 기존 경로 선택 점수는 유지한다. 독립 검수 및 실게임 기록은 work/2026-11/2026-11-06-선택지개선/ 아래를 따른다. 기존 확인한 선·76563bf 화면 관측·902721e 과거 실행 관측을 이 후보의 실행 확인으로 승격하지 않는다.\n';
fs.writeFileSync(fp,ft);
for(const f of ['pin-snapshot.mjs','build.mjs','verify.mjs']){
 const p=P+'/B/extractor/'+f;fs.writeFileSync(p,fs.readFileSync(p,'utf8').replaceAll(baseline,sha).replaceAll("repo='C:/Dev/CoreKeeperModSDK'",`repo='${repo}'`));
}
const overlay=read(L+'/C/overlay-source.json');overlay.baseCommit=sha;write(L+'/C/overlay-source.json',overlay);
const inf=read(P+'/B/inference.graph.json');inf.meta.currentCodeAssociations={commit:sha,basis:'Same qualified code identities; original historical inference and runtime limits preserved. New choice helpers are statically extracted, not retroactively observed.'};write(P+'/B/inference.graph.json',inf);
const steps=[];
function run(label,exe,args,cwd=stage){
 const r=spawnSync(exe,args,{cwd,encoding:'utf8',maxBuffer:32*1024*1024,windowsHide:true});
 const result={label,exe,args,cwd,exitCode:r.status,stdout:r.stdout??'',stderr:r.stderr??''};steps.push(result);write(stage+'/commands.json',steps);
 console.log(label+': '+r.status);if(r.status!==0)throw Error(label+' failed; see commands.json');
}
run('dev build','node',[G+'/tools/build.mjs','--strict']);
run('dev check','node',[G+'/tools/query.mjs','check']);
run('pin','node',[P+'/B/extractor/pin-snapshot.mjs']);
run('Roslyn','dotnet',['run','--project',P+'/B/extractor/Extract.csproj','--',P+'/B/snapshot',P+'/B/csharp.raw.json']);
run('code build','node',[P+'/B/extractor/build.mjs']);
run('code verify','node',[P+'/B/extractor/verify.mjs']);
run('remap historical screens','node',[L+'/B/remap-base.mjs','--base',sha]);
run('P C integrate','node',[P+'/C/integrate-c.mjs']);
run('P C graph','node',[P+'/C/build-graph-C.mjs']);
run('L C validate','node',[L+'/C/validate-C.mjs']);
run('historical evidence and product integration','node',[task+'/finish-graphs.mjs',stage]);
const changed=manifest.filter(x=>hash(fs.readFileSync(x.path))!==x.sha256);
write(stage+'/audit.json',{sha,baseline,stage,canonicalFiles:manifest.length,canonicalChanged:changed,steps:steps.map(({label,exitCode})=>({label,exitCode}))});
if(changed.length)throw Error('Canonical changed concurrently; review audit before any future application.');
console.log(JSON.stringify({stage,sha,canonicalChanged:changed.length}));
