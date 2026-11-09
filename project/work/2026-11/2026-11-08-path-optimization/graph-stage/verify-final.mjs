import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync,execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url));
const work=path.resolve(stage,'../../..');
const legacy=path.join(work,'지식그래프');
const B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B');
const product=path.join(work,'2026-11/2026-11-05-지식그래프-실화면/D/graph.json');
const target='43c3817fc1a36142c8591fb087ed867665633fe1';
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
const read=file=>JSON.parse(fs.readFileSync(file,'utf8'));
const queries=path.join(stage,'final-queries');
fs.mkdirSync(queries,{recursive:true});
const inputs=[['intent'],['intent','P.path-optimization'],['source','P.path-optimization'],
  ['show','R.path-first-attempt'],['show','R.path-tree-audit'],['show','R.path-implementation'],
  ['show','R.path-offline'],['show','R.path-review'],['show','R.path-live'],['source','R.path-live'],
  ['show','R.path-integration'],['show','pol.choice-no-cut'],['show','P.expedition-choice'],['check']];
const results=[];
for(const args of inputs){
  const result=spawnSync(process.execPath,['tools/product.mjs',...args],{cwd:legacy,encoding:'utf8',maxBuffer:64*1024*1024});
  fs.writeFileSync(path.join(queries,args.join('_')+'.txt'),(result.stdout??'')+(result.stderr??''));
  results.push({args,exit:result.status});
  if(result.status!==0)throw Error('Representative query failed '+args.join(' '));
}
const g=read(product);
const check=read(path.join(queries,'check.txt'));
const delta=read(path.join(stage,'final-delta.json'));
const expected=read(path.join(stage,'new-code-mappings.json'));
const mappings=expected.mapping.map(([from,signature])=>{
  const nodes=g.nodes.filter(node=>node.signature===signature);
  if(nodes.length!==1)throw Error('Ambiguous final anchor '+signature);
  const node=nodes[0];
  const edges=g.edges.filter(edge=>edge.from===from&&edge.to===node.id&&edge.kind==='길 계산 최적화 구현 위치');
  const blob=execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','cat-file','blob',target+':'+node.file]);
  return {from,signature,id:node.id,edges:edges.length,runtimeTrace:edges[0]?.runtimeTrace,
    blobMatch:blob.equals(fs.readFileSync(path.join(B,'snapshot',node.file)))};
});
const pins=read(path.join(stage,'final-source-pins.json'));
const pinnedMismatch=pins.filter(pin=>hash(fs.readFileSync(pin.pinned))!==pin.sha256).map(pin=>pin.id);
const reachable=new Set();
const queue=['P.release-goal'];
while(queue.length){
  const id=queue.pop();
  if(reachable.has(id))continue;
  reachable.add(id);
  for(const edge of g.edges)if(edge.from===id)queue.push(edge.to);
}
const missingReach=delta.nodes.filter(node=>!reachable.has(node.id)).map(node=>node.id);
const oldEvidence=g.bagImplementationEvidence;
const finalInstall=read(path.join(stage,'source-pins/final-installed-compare.json'));
const canonicalSources=read(path.join(stage,'preparation.json')).canonicalFiles.map(file=>({path:file.path,sha256:hash(fs.readFileSync(file.path))}));
const output={sourceCommit:g.meta.sourceCommit,officialHead:execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','rev-parse','HEAD'],{encoding:'utf8'}).trim(),
  nodes:g.nodes.length,edges:g.edges.length,check:{ok:check.ok,structuralErrors:check.structuralErrors,stale:check.stale,baselineDrift:check.baselineDrift},
  newNodes:delta.nodes.length,newEdges:delta.edges.length,queries:results,mappings,pinnedMismatch,missingReach,
  oldBagEvidence:{sameRevision:oldEvidence.sameRevision,evidenceAppliesToCurrentRevision:oldEvidence.evidenceAppliesToCurrentRevision},
  finalInstalledFiles:finalInstall.length,finalInstalledHashesMatch:finalInstall.every(file=>file.sourceHash===file.installedHash),
  canonicalSources,taskCodeChanges:'none by graph task'};
fs.writeFileSync(path.join(stage,'final-verification.json'),JSON.stringify(output,null,2)+'\n');
if(g.meta.sourceCommit!==target||output.officialHead!==target||!check.ok||pinnedMismatch.length||missingReach.length||mappings.some(m=>m.edges!==1||m.runtimeTrace!==false||!m.blobMatch)||oldEvidence.sameRevision!==false||oldEvidence.evidenceAppliesToCurrentRevision!==false||!output.finalInstalledHashesMatch)throw Error('Final verification failed');
console.log(JSON.stringify({sourceCommit:target,nodes:output.nodes,edges:output.edges,queries:results.length,mappings:mappings.length,pinnedMismatch,missingReach,check:output.check,finalInstalledFiles:output.finalInstalledFiles}));
