import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';

const folder=path.dirname(fileURLToPath(import.meta.url));
const work=path.resolve(folder,'../..');
const product=path.join(work,'지식그래프');
const cli=path.join(product,'tools/product.mjs');
const graph=JSON.parse(fs.readFileSync(path.join(work,'2026-11/2026-11-05-지식그래프-실화면/D/graph.json'),'utf8'));
const canonical=JSON.parse(fs.readFileSync(path.join(work,'게임행동-그래프/published/graph.json'),'utf8'));
const rows=[];
function execute(args,name){
 const result=spawnSync(process.execPath,[cli,...args],{cwd:product,encoding:'utf8',maxBuffer:50*1024*1024});
 fs.writeFileSync(path.join(folder,name),result.stdout||'');
 if(result.error||result.status!==0)throw Error('CLI failed '+JSON.stringify(args)+' '+result.stderr);
 return JSON.parse(result.stdout);
}
const check=execute(['check'],'check.json');
if(!check.ok)throw Error('product check failed');
const linked=graph.nodes.filter(n=>n.externalBehavior);
for(const node of linked){
 const original=canonical.nodes[node.externalBehavior.id];
 if(!original||original.kind!==node.externalBehavior.kind||JSON.stringify(original.origin??null)!==JSON.stringify(node.externalBehavior.origin))throw Error('foreign reference mismatch '+node.id);
}
for(const topic of graph.behaviorLink.topics){
 const behavior=execute(['behavior',topic.id],topic.id+'.json');
 const target=behavior.topics[0].targets.find(id=>graph.nodes.find(n=>n.id===id)?.externalBehavior.origin);
 if(!target)throw Error('no source example '+topic.id);
 const shown=execute(['show',target],topic.id+'-show.json');
 const source=execute(['source',target],topic.id+'-source.json');
 const original=canonical.nodes[target.slice(5)];
 if(source.origin?.normalizedValueSha256!==original.origin.normalizedValueSha256)throw Error('source hash differs '+target);
 rows.push({topic:topic.id,counts:topic.counts,target,sourceOrigin:source.origin,showSucceeded:Boolean(shown),sourceSucceeded:true});
}
const recorded=execute(['behavior','R.expedition-game-evidence'],'recorded-evidence.json');
if(recorded.recordedEvidence.length!==3)throw Error('recorded exact evidence incomplete');
const cooking=execute(['behavior','P.life-tree-first-scope'],'planning-life.json');
if(!cooking.topics.some(t=>t.id==='R.game-topic.cook'))throw Error('planning traversal failed');
const pair=execute(['source','game:cooking:1'],'cooking-pair-source.json');
const finalGraph=JSON.parse(fs.readFileSync(path.join(work,'2026-11/2026-11-05-지식그래프-실화면/D/graph.json'),'utf8'));
const finalCheck=execute(['check'],'check.json');
if(!finalCheck.ok)throw Error('final product check failed');
const summary={status:'pass',productNodes:finalGraph.nodes.length,productEdges:finalGraph.edges.length,foreignReferences:linked.length,canonicalNodes:Object.keys(canonical.nodes).length,canonicalEdges:canonical.edges.length,topics:rows,recordedEvidence:recorded.recordedEvidence.length,planningLifeTopics:cooking.topics.map(t=>t.id),cookingPair:pair.origin,productCheck:{ok:finalCheck.ok,structuralErrors:finalCheck.structuralErrors,stale:finalCheck.stale,baselineDrift:finalCheck.baselineDrift}};
fs.writeFileSync(path.join(folder,'evidence.json'),JSON.stringify(summary,null,2)+'\n');
console.log(JSON.stringify({status:summary.status,topics:rows.length,foreignReferences:linked.length}));
