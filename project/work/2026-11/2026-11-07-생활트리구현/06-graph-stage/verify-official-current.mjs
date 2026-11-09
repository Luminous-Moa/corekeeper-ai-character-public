import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),out=path.resolve(stage,'..'),month=path.resolve(stage,'../..'),work=path.resolve(stage,'../../..');
const graph=path.join(work,'지식그래프'),B=path.join(month,'2026-11-05-지식그래프-개편/B'),D=path.join(month,'2026-11-05-지식그래프-실화면/D');
const product=JSON.parse(fs.readFileSync(path.join(D,'graph.json'),'utf8')),code=JSON.parse(fs.readFileSync(path.join(B,'code.graph.json'),'utf8'));
const target='c4a0f5e38016c79567f880bbaea5e56e0eeae347';
const run=(cwd,args)=>execFileSync(process.execPath,args,{cwd,encoding:'utf8',maxBuffer:12*1024*1024});
const checkText=run(graph,['tools/product.mjs','check']);fs.writeFileSync(path.join(out,'graph-product-check.json'),checkText);
const check=JSON.parse(checkText);
const byteCheck=run(graph,['tools/product-build.mjs','--check']);fs.writeFileSync(path.join(out,'graph-product-determinism.log'),byteCheck);
const oldPlan=await import(pathToFileURL(path.join(stage,'official-before/기획연결/source.mjs'))),newPlan=await import(pathToFileURL(path.join(month,'2026-11-07-기획연결/source.mjs')));
const previousPreserved=JSON.stringify(oldPlan.nodes)===JSON.stringify(newPlan.nodes.slice(0,oldPlan.nodes.length))&&JSON.stringify(oldPlan.edges)===JSON.stringify(newPlan.edges.slice(0,oldPlan.edges.length))&&oldPlan.entry===newPlan.entry;
const queries=[['intent'],['intent','P.life-tree-first-scope'],['source','P.life-tree-count'],['show','P.life-tree-readonly'],['show','Q.life-tree-followup'],['source','R.life-tree-implementation'],['source','R.life-tree-review']];
const anchors=[];
for(const signature of ['AILife.Compose(','AILife.Navigate(','AILife.Resolve(','AIFarm.ExactCandidate(','AICook.ExactCandidate(','AICraft.ExactCandidate(','AIInventory.CandidatesFor(','AILife.Ended(']){
 const matches=product.nodes.filter(n=>n.type==='코드'&&n.signature?.startsWith(signature));if(matches.length!==1)throw Error('코드 심볼 모호/누락 '+signature);
 anchors.push({id:matches[0].id,file:matches[0].file,start:matches[0].lineStart,end:matches[0].lineEnd});queries.push(['source',matches[0].id]);
}
let transcript='';
for(const args of queries){transcript+='> node tools/product.mjs '+args.map(a=>JSON.stringify(a)).join(' ')+'\n'+run(graph,['tools/product.mjs',...args])+'\n';}
fs.writeFileSync(path.join(out,'graph-representative-queries.txt'),transcript);
const legacy=run(graph,['tools/query.mjs','check']);fs.writeFileSync(path.join(out,'graph-legacy-check.log'),legacy);
const planCheck=JSON.parse(run(path.join(month,'2026-11-07-기획연결'),['graph.mjs','check']));
const snapshot=JSON.parse(fs.readFileSync(path.join(B,'snapshot-verification.json'),'utf8'));
const determinism=JSON.parse(fs.readFileSync(path.join(B,'determinism.json'),'utf8'));
const actualLifeMappings=product.edges.filter(e=>e.kind==='c4a0f5e 생활 구현 입구');
const result={ok:check.ok&&check.stale.length===0&&check.structuralErrors.length===0&&!check.baselineDrift.warning&&previousPreserved&&code.meta.sourceCommit===target&&product.meta.sourceCommit===target&&snapshot.allMatch&&determinism.equal&&planCheck.ok,
 sourceCommit:target,code:{nodes:code.nodes.length,edges:code.edges.length},product:{nodes:product.nodes.length,edges:product.edges.length},planning:{nodes:planCheck.nodes,edges:planCheck.edges,sources:planCheck.sources,previousPreserved},
 exactSnapshotFiles:snapshot.fileCount,codeDeterminism:determinism.equal,productDeterminism:byteCheck.trim(),structuralErrors:check.structuralErrors.length,stale:check.stale.length,baselineWarning:check.baselineDrift.warning,
 lifeCodeMappings:actualLifeMappings.length,codeAnchors:anchors,
 historicalObservations:{uiSource:product.uiRuntimeObservations?.sourceCommit,uiApplies:product.uiRuntimeObservations?.evidenceAppliesToCurrentRevision,uiTrace:product.uiRuntimeObservations?.functionTraceObserved,refactorSource:product.supplementalRuntimeObservations?.sourceCommit,refactorApplies:product.supplementalRuntimeObservations?.evidenceAppliesToCurrentRevision},
 boundary:'현재 코드·합의 원문·컴파일/어댑터/독립검수와 본 통합을 연결. 설치/실게임/실제모델 성공·전체함수 실행 trace를 새로 관측하지 않음.'};
fs.writeFileSync(path.join(out,'graph-integration-checks.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({...result,codeAnchors:anchors.length},null,2));if(!result.ok)process.exitCode=1;
