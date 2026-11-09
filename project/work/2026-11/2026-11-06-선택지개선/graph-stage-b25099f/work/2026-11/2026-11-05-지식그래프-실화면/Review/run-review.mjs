import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
const here=path.dirname(fileURLToPath(import.meta.url));
const base=path.dirname(here), prior=path.resolve(base,'../2026-11-05-지식그래프-개편');
const entry=path.resolve(base,'../../지식그래프/tools/product.mjs');
const graph=JSON.parse(fs.readFileSync(path.join(base,'D/graph.json'),'utf8'));
const hash=p=>createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const logs=[];
function run(args){const r=spawnSync(process.execPath,[entry,...args],{encoding:'utf8',maxBuffer:30*1024*1024});logs.push({command:[process.execPath,entry,...args],exitCode:r.status,stdout:r.stdout,stderr:r.stderr,error:r.error?.message});return r;}
for(const args of [['overview'],['check'],['user','CKAI-ACT-01'],['show','CKAI-02#3'],['show','CKAI-02#2-7'],['feature','slash:ai'],['source','CKAI-ACT-01'],['source','CKAI-02#2-7'],['source','code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs#AIPlayerSpikeMod.TryHandleChatCommand(string)'],['source','code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs#AIPlayerSpikeMod.StatusAll()'],['source','code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs#AIWorldSettings.Members(string)'],['source','code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs#AIPlayerSpikeMod.FlushReplies()'],['show','data:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs:p'],['legacy','show','pol.ai-own-character']])run(args);
let state=20261105;
for(const signature of ['AIWorldSettings.PathFor(string)','AIPlayerSpikeMod.Reply(string)','AIPlayerSpikeMod.Update()'])run(['source','code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs#'+signature]);
const rng=()=>{state=(Math.imul(state,1664525)+1013904223)>>>0;return state/4294967296;};
const primary=new Set(graph.edges.filter(e=>e.from==='CKAI-ACT-01').map(e=>e.to));
const pool=graph.nodes.filter(n=>n.type==='화면요소'&&/^CK/.test(n.id)&&!primary.has(n.id)).sort((a,b)=>a.id.localeCompare(b.id,'en'));
const chosen=[];
for(let i=0;i<3&&pool.length;i++){const index=Math.floor(rng()*pool.length);chosen.push(pool.splice(index,1)[0].id);}
for(const id of chosen){run(['show',id]);run(['source',id]);}
const preservation=JSON.parse(fs.readFileSync(path.join(base,'D/preexisting-hashes.json'),'utf8')).map(x=>({...x,actual:hash(path.join(prior,x.path))}));
const versions=JSON.parse(fs.readFileSync(path.join(base,'B/version-evidence.json'),'utf8'));
const installedSample=versions.installedScripts.filter(x=>/\/(AIPlayerSpikeMod|AIBodySkills)\.cs$/.test(x.source)).map(x=>{const normalized=fs.readFileSync(x.installed,'utf8').replaceAll('\r\n','\n');const actualNormalized=createHash('sha256').update(normalized).digest('hex');const git=spawnSync('git',['-C','C:/Dev/CoreKeeperModSDK-combat-s2','show',versions.runCandidateCommit+':'+x.source],{encoding:'utf8',maxBuffer:5*1024*1024});return {source:x.source,installed:x.installed,actualNormalized,gitNormalized:createHash('sha256').update(git.stdout.replaceAll('\r\n','\n')).digest('hex'),recorded:x.normalizedSha256,gitExitCode:git.status};});
const a2=JSON.parse(fs.readFileSync(path.join(base,'A2/manifest.json'),'utf8').replace(/^\uFEFF/,''));
const containment=[];
for(const evidence of a2.evidence){const positions=new Map(evidence.elements.map(e=>[e.id,e]));for(const child of evidence.elements){if(!child.parent||!positions.has(child.parent))continue;const p=positions.get(child.parent).bbox,c=child.bbox;containment.push({evidence:evidence.id,id:child.id,ok:c[0]>=p[0]&&c[1]>=p[1]&&c[0]+c[2]<=p[0]+p[2]&&c[1]+c[3]<=p[1]+p[3]});}}
const output={seed:20261105,algorithm:'LCG(1664525*x+1013904223) mod 2^32; sorted non-activity CK screen element pool; sample without replacement',chosen,graphSha256:hash(path.join(base,'D/graph.json')),preservation,installedSample,containment,logs};
fs.writeFileSync(path.join(here,'command-log.json'),JSON.stringify(output,null,2)+'\n');
console.log(JSON.stringify({seed:output.seed,chosen,commands:logs.length,failures:logs.filter(x=>x.exitCode!==0).map(x=>({command:x.command,error:x.stderr})),preservation:preservation.map(x=>({path:x.path,ok:x.sha256===x.actual})),graphSha256:output.graphSha256},null,2));
