import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
const stage=path.resolve(process.argv[2]);
if(!stage.includes('2026-11-06-선택지개선')||!path.basename(stage).startsWith('graph-stage-'))throw Error('Unexpected stage');
const G=stage+'/work/지식그래프',L=stage+'/work/2026-11/2026-11-05-지식그래프-실화면';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const write=(p,v)=>fs.writeFileSync(p,JSON.stringify(v,null,2)+'\n');
const overlay=read(L+'/C/overlay-source.json');
const historical=read(L+'/'+overlay.refactorObservationSource);
const canonicalObservation='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-05-지식그래프-실화면/'+overlay.refactorObservationSource;
if(historical.sourceCommit!=='902721ec505df42d74d0b2d245ed34a049faadad'
 || !fs.readFileSync(canonicalObservation).equals(fs.readFileSync(L+'/'+overlay.refactorObservationSource)))
 throw Error('The accepted historical observation must be the unchanged canonical 902721e record.');
// Explicit historical source, not an arbitrary mismatch exemption. The observed source and evidence hashes stay intact.
overlay.historicalRefactorObservationCommit=historical.sourceCommit;
write(L+'/C/overlay-source.json',overlay);
const ip=L+'/D/integrate.mjs';let text=fs.readFileSync(ip,'utf8');
text=text.replace("refactorObservations.sourceCommit!==g.meta.sourceCommit)throw Error('refactor observation code baseline mismatch')",
 "(refactorObservations.sourceCommit!==g.meta.sourceCommit&&refactorObservations.sourceCommit!==c.historicalRefactorObservationCommit))throw Error('refactor observation code baseline mismatch')");
text=text.replace("g.supplementalRuntimeObservations={...refactorObservations,sourceDocument:live(c.refactorObservationSource)};",
 "g.supplementalRuntimeObservations={...refactorObservations,sourceDocument:live(c.refactorObservationSource),currentSourceCommit:g.meta.sourceCommit,sameRevision:refactorObservations.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:refactorObservations.sourceCommit===g.meta.sourceCommit};");
text=text.replace("validation.supplementalRuntime={sourceCommit:refactorObservations.sourceCommit,status:",
 "validation.supplementalRuntime={sourceCommit:refactorObservations.sourceCommit,currentSourceCommit:g.meta.sourceCommit,sameRevision:refactorObservations.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:refactorObservations.sourceCommit===g.meta.sourceCommit,status:");
fs.writeFileSync(ip,text);
const steps=[];
function run(label,args){const r=spawnSync('node',args,{cwd:stage,encoding:'utf8',maxBuffer:32*1024*1024,windowsHide:true});steps.push({label,args,exitCode:r.status,stdout:r.stdout??'',stderr:r.stderr??''});write(stage+'/finish-commands.json',steps);console.log(label+': '+r.status);if(r.status!==0)throw Error(label);}
run('historical overlay validation',[L+'/C/validate-C.mjs']);
run('product build',[G+'/tools/product-build.mjs']);
run('product questions',[L+'/D/questions.mjs']);
run('product check',[G+'/tools/product.mjs','check']);
run('product deterministic',[G+'/tools/product-build.mjs','--check']);
const manifest=read(stage+'/canonical-before.json');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const changed=manifest.filter(x=>hash(fs.readFileSync(x.path))!==x.sha256);
write(stage+'/audit.json',{sha:overlay.baseCommit,historicalObservedCommit:historical.sourceCommit,stage,canonicalFiles:manifest.length,canonicalChanged:changed,steps:steps.map(({label,exitCode})=>({label,exitCode}))});
if(changed.length)throw Error('Canonical changed concurrently; inspect audit before future application.');
