// 빈 선택지(49bd748) 그래프 반영의 최종 검증. 관측·재판단 verify-final.mjs와 같은 항목을 본다.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync,spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../../..');
const legacy=path.join(work,'지식그래프'),B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B');
const target='49bd748a8fcd390fc658166ca662ddb13b5c5c0d';
const read=file=>JSON.parse(fs.readFileSync(file,'utf8'));
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const generated=['code.graph.json','B.graph.json','javascript.raw.json','validation.json','boundaries.json','call-forms.json','snapshot.manifest.json'];
const hashes=Object.fromEntries(generated.map(name=>[name,hash(path.join(B,name))]));
execFileSync(process.execPath,['extractor/build.mjs'],{cwd:B,stdio:'pipe'});
const generatedChanges=generated.filter(name=>hashes[name]!==hash(path.join(B,name)));
const output=path.join(stage,'final-queries');fs.mkdirSync(output,{recursive:true});
const commands=[['intent'],['intent','P.choice-gaps'],['source','P.choice-gaps'],['show','P.chests-shared'],['show','P.chest-open-mode'],['show','P.combat-entry'],['source','P.combat-entry'],['show','P.maintenance-abandon'],['show','R.gap-implementation'],['show','R.gap-integration'],['show','Q.choice-total-limit'],['show','pol.chests-shared'],['show','pol.combat-entry'],['policy','pol.bag-boundary'],['show','R.chest-areas-implementation'],['behavior','P.gap-life'],['behavior','P.gap-bag'],['behavior','P.maintenance-abandon'],['show','R.game-topic.food'],['check']];
for(const args of commands){
  const result=spawnSync(process.execPath,['tools/product.mjs',...args],{cwd:legacy,encoding:'utf8',maxBuffer:64000000});
  fs.writeFileSync(path.join(output,args.join('_')+'.txt'),(result.stdout??'')+(result.stderr??''));
  if(result.status!==0)throw Error('Query failed '+args.join(' '));
}
const product=read(path.join(work,'2026-11/2026-11-05-지식그래프-실화면/D/graph.json'));
const check=read(path.join(output,'check.txt'));
const mapping=read(path.join(stage,'new-code-mappings.json')).mapping;
const mappings=mapping.map(([from,signature])=>{
  const nodes=product.nodes.filter(node=>node.signature===signature);
  if(nodes.length!==1)throw Error('Anchor missing or ambiguous '+signature);
  const node=nodes[0],edges=product.edges.filter(edge=>edge.from===from&&edge.to===node.id&&edge.kind===(from==='P.combat-entry'?'임시 대리 신호(전투 진입 미구현)':'빈 선택지 구현 위치'));
  const blob=execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','cat-file','blob',target+':'+node.file]);
  return {from,signature,edgeCount:edges.length,runtimeTrace:edges[0]?.runtimeTrace,blobMatch:blob.equals(fs.readFileSync(path.join(B,'snapshot',node.file)))};
});
const pins=read(path.join(stage,'final-source-pins.json'));
const pinnedMismatch=pins.filter(pin=>hash(pin.pinned)!==pin.sha256).map(pin=>pin.id);
const reached=new Set(),queue=['P.release-goal'];
while(queue.length){const id=queue.pop();if(reached.has(id))continue;reached.add(id);for(const edge of product.edges)if(edge.from===id)queue.push(edge.to);}
const delta=read(path.join(stage,'final-delta.json'));
const missingReach=delta.nodes.filter(node=>!reached.has(node.id)).map(node=>node.id);
const policies=['pol.chests-shared','pol.chest-open-mode','pol.combat-entry','pol.maintenance-abandon','pol.choice-gaps'].filter(id=>!product.nodes.some(n=>n.id===id));
const topicEdges=product.edges.filter(e=>e.to?.startsWith('R.game-topic.')&&delta.nodes.some(n=>n.id===e.from)).map(e=>e.from+'→'+e.to);
const bag=product.bagImplementationEvidence;
const result={sourceCommit:product.meta.sourceCommit,officialHead:execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','rev-parse','HEAD'],{encoding:'utf8'}).trim(),nodes:product.nodes.length,edges:product.edges.length,check:{ok:check.ok,structuralErrors:check.structuralErrors,stale:check.stale,baselineDrift:check.baselineDrift},queries:commands.length,mappings,pinnedMismatch,missingReach,missingPolicies:policies,behaviorTopicEdges:topicEdges,generatedChanges,generatedHashes:hashes,oldBag:{sameRevision:bag.sameRevision,evidenceAppliesToCurrentRevision:bag.evidenceAppliesToCurrentRevision,scopeBoundary:bag.scopeBoundary},installation:'not installed; live game not run'};
fs.writeFileSync(path.join(stage,'final-verification.json'),JSON.stringify(result,null,2)+'\n');
if(result.sourceCommit!==target||result.officialHead!==target||!check.ok||check.stale.length||check.structuralErrors.length||generatedChanges.length||pinnedMismatch.length||missingReach.length||policies.length||mappings.some(item=>item.edgeCount!==1||item.runtimeTrace!==false||!item.blobMatch)||bag.sameRevision!==false||!topicEdges.length)throw Error('Final verification failed');
console.log(JSON.stringify({sourceCommit:target,nodes:result.nodes,edges:result.edges,queries:commands.length,mappings:mappings.length,topicEdges:topicEdges.length,check:{ok:check.ok,stale:check.stale.length,structuralErrors:check.structuralErrors.length},generatedChanges,pinnedMismatch,missingReach}));
