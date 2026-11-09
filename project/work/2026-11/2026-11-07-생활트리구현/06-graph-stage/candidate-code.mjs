import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const root=path.dirname(fileURLToPath(import.meta.url));
const repo='C:/Dev/CoreKeeperModSDK-life-tree';
const commit=process.argv[2]||'24c61c246053237784c057cf97282b9ca0c5d564';
if(!/^[0-9a-f]{40}$/.test(commit))throw Error('정확한40자 후보 SHA 필요');
const base='1fc89b93edbee31a9001a174581e32568119efc1';
execFileSync('git',['-C',repo,'merge-base','--is-ancestor',base,commit]);
const git=(...args)=>execFileSync('git',['-C',repo,...args]);
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const json=x=>JSON.stringify(x,null,2)+'\n';
const currentFile=path.join(root,'candidate-code-current.json');
const previous=fs.existsSync(currentFile)?JSON.parse(fs.readFileSync(currentFile,'utf8')):null;
const previousRoot=previous?path.join(root,previous.relativeRoot):null;
const previousRaw=previousRoot&&previous.sourceCommit!==commit?JSON.parse(fs.readFileSync(path.join(previousRoot,'csharp.raw.json'),'utf8')):null;
const previousFiles=previousRaw?JSON.parse(fs.readFileSync(path.join(previousRoot,'input-manifest.json'),'utf8')).files:[];
const codeRoot=path.join(root,'candidate-code',commit.slice(0,12));
const snapshot=path.join(codeRoot,'snapshot');
const paths=[
 'AIBrain.Loop.cs','AIBrain.Candidates.cs','AIBrain.cs','AILife.cs','Core/AILifeTree.cs',
 'AIFarm.cs','AICook.cs','AICraft.cs','AIInventory.cs','AIActions.cs','AIDecisions.cs',
 'AIGoals.cs','AIPlanner.cs','Core/AIPlanProtocol.cs','Core/AIPlanRules.cs','AIBody.cs',
 'AIGoalFeatures.cs','AIGoalText.cs','AIAgents.cs','AIBrain.Prep.cs'
].map(p=>'Assets/AIPlayerSpike/Scripts/'+p);
const files=[];
for(const p of paths){
 const bytes=git('cat-file','blob',commit+':'+p),out=path.join(snapshot,p);fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,bytes);
 const oid=git('rev-parse',commit+':'+p).toString().trim();
 files.push({path:p,blob:oid,sha256:sha(bytes),bytes:bytes.length});
}
const extractor=path.resolve(root,'../../2026-11-05-지식그래프-개편/B/extractor/bin/Debug/net10.0/Extract.dll');
if(!fs.existsSync(extractor))throw Error('기존 Roslyn extractor 없음: '+extractor);
const rawPath=path.join(codeRoot,'csharp.raw.json');
let deltaFiles=files.map(f=>f.path);
if(previousRaw){
 deltaFiles=files.filter(f=>previousFiles.find(p=>p.path===f.path)?.sha256!==f.sha256).map(f=>f.path);
 const deltaSnapshot=path.join(codeRoot,'delta-snapshot');
 for(const p of deltaFiles){const out=path.join(deltaSnapshot,p);fs.mkdirSync(path.dirname(out),{recursive:true});fs.copyFileSync(path.join(snapshot,p),out);}
 const deltaPath=path.join(codeRoot,'csharp.delta.raw.json');
 execFileSync('dotnet',[extractor,deltaSnapshot,deltaPath],{stdio:'pipe',maxBuffer:32*1024*1024});
 const delta=JSON.parse(fs.readFileSync(deltaPath,'utf8')),changed=new Set(deltaFiles);
 const mergedNodes=[...previousRaw.nodes.filter(n=>!changed.has(n.file)),...delta.nodes];
 const mergedIds=new Set(mergedNodes.map(n=>n.id));
 const changedIds=new Set(previousRaw.nodes.filter(n=>changed.has(n.file)).map(n=>n.id));
 const merged={...delta,nodes:mergedNodes,
  edges:[...previousRaw.edges.filter(e=>!changed.has(e.file)&&!changedIds.has(e.from)&&mergedIds.has(e.from)&&mergedIds.has(e.to)),...delta.edges],
  facts:[...previousRaw.facts.filter(f=>!changed.has(f.file)),...delta.facts],
  diagnostics:[...previousRaw.diagnostics.filter(d=>!changed.has(d.file)),...delta.diagnostics],
  incremental:{fromCommit:previous.sourceCommit,changedFiles:deltaFiles,unchangedReused:files.length-deltaFiles.length,boundary:'바뀐 소스만 Roslyn 재추출. 미변경 심볼/정적 관계는 같은 blob에서 재사용. delta 범위 밖 호출의 새 해석을 추가하지 않음.'}};
 fs.writeFileSync(rawPath,json(merged));
}else execFileSync('dotnet',[extractor,snapshot,rawPath],{stdio:'pipe',maxBuffer:32*1024*1024});
const raw=JSON.parse(fs.readFileSync(rawPath,'utf8'));
const wants=[
 ['AILife.cs','AILife.Compose('],['AILife.cs','AILife.Navigate('],['AILife.cs','AILife.ExecuteStale('],
 ['AILife.cs','AILife.Starting('],['AILife.cs','AILife.Ended('],['AILife.cs','AILife.Report('],
 ['AILife.cs','AILife.ForGoal('],['AILife.cs','AILife.Remaining('],
 ['Core/AILifeTree.cs','LifeCursor.Check('],['Core/AILifeTree.cs','LifeCursor.Apply('],
 ['Core/AILifeTree.cs','LifeProgress.Record('],['Core/AILifeTree.cs','AILifeGoalForm.Check('],
 ['AIBrain.Loop.cs','AIBrain.HandleResponse('],['AIBrain.Loop.cs','AIBrain.Apply('],
 ['AIActions.cs','AIActions.Make('],['AIActions.cs','AIActions.Stale('],
 ['AIFarm.cs','AIFarm.LifeEntries('],['AIFarm.cs','AIFarm.ExactCandidate('],
 ['AIFarm.cs','FarmSkill.Start('],['AIFarm.cs','FarmSkill.Tick('],
 ['AICook.cs','AICook.LifeEntries('],['AICook.cs','AICook.ExactCandidate('],
 ['AICook.cs','CookSkill.Start('],['AICook.cs','CookSkill.Tick('],
 ['AICraft.cs','AICraft.LifeEntries('],['AICraft.cs','AICraft.ExactCandidate('],
 ['AICraft.cs','CraftSkill.Start('],['AICraft.cs','CraftSkill.Tick('],
 ['AIInventory.cs','AIInventory.LifeEntries('],['AIInventory.cs','AIInventory.CandidatesFor('],
 ['AIInventory.cs','InventorySkill.Start('],['AIInventory.cs','InventorySkill.Tick('],
 ['Core/AIPlanProtocol.cs','AIPlanProtocol.Read('],['AIBrain.Prep.cs','AIBrain.WantedItems(']
];
const anchors=wants.map(([file,sig])=>{
 const matches=raw.nodes.filter(n=>n.file==='Assets/AIPlayerSpike/Scripts/'+file&&n.signature?.startsWith(sig));
 if(matches.length!==1)throw Error('후보 심볼이 없거나 모호함: '+file+' '+sig+' '+matches.length);
 return {...matches[0],sourceCommit:commit,runtimeTrace:false,evidenceScope:'isolated implementation candidate; validation in progress'};
});
const ids=new Set(anchors.map(n=>n.id));
const edges=raw.edges.filter(e=>ids.has(e.from)&&ids.has(e.to));
const graph={schemaVersion:1,sourceCommit:commit,baseCommit:base,status:'격리 구현 후보·검증 진행중',officialIntegrated:false,installed:false,runtimeTested:false,
 parser:raw.parser,parserVersion:raw.version,
 scope:'생활 대표 생산 입구의 부분 코드 색인; 관련 소스의 변경분만 기존 Roslyn 추출기로 분석하고 동일 blob의 미변경 심볼은 재사용. 전체 제품 코드층/호출경로 전수 추출 아님.',
 nodes:anchors,edges,
 boundaries:{semanticMetadataLoaded:false,unresolvedOutsideScope:true,syntaxDiagnostics:raw.diagnostics.filter(d=>d.kind==='syntax').length,semanticDiagnostics:raw.diagnostics.filter(d=>d.kind==='semantic').length,staticReferencesNotRuntimeTrace:true,
 note:'부분 source 범위 밖 호출/Unity·게임 metadata 미적재 진단을 제품 컴파일 실패나 런타임 실패로 해석하지 않음.'}};
fs.writeFileSync(path.join(codeRoot,'anchors.graph.json'),json(graph));
const checks={ok:files.every(f=>sha(fs.readFileSync(path.join(snapshot,f.path)))===f.sha256&&sha(git('cat-file','blob',commit+':'+f.path))===f.sha256)&&graph.boundaries.syntaxDiagnostics===0,
 commit,base,files:files.length,anchors:anchors.length,staticEdges:edges.length,syntaxDiagnostics:graph.boundaries.syntaxDiagnostics,
 changedFilesExtracted:deltaFiles.length,unchangedFilesReused:files.length-deltaFiles.length,incrementalFrom:previousRaw?previous.sourceCommit:null,officialMutated:false,fullProductCodeGraphRebuilt:false,isolatedCandidate:true};
fs.writeFileSync(path.join(codeRoot,'input-manifest.json'),json({sourceCommit:commit,baseCommit:base,extractor,extractorSha256:sha(fs.readFileSync(extractor)),files,rawSha256:sha(fs.readFileSync(rawPath)),checks}));
fs.writeFileSync(path.join(root,'candidate-code-current.json'),json({sourceCommit:commit,status:graph.status,relativeRoot:path.relative(root,codeRoot).replaceAll('\\','/'),checks}));
console.log(json(checks));
if(!checks.ok)process.exitCode=1;
