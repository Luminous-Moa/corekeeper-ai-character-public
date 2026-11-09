import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {compile} from './compile-stage.mjs';
const root=path.dirname(fileURLToPath(import.meta.url));
const current=JSON.parse(fs.readFileSync(path.join(root,'candidate-code-current.json'),'utf8'));
const code=JSON.parse(fs.readFileSync(path.join(root,current.relativeRoot,'anchors.graph.json'),'utf8'));
const plan=compile();
const node=(prefix)=>{const ns=code.nodes.filter(n=>n.signature?.startsWith(prefix));if(ns.length!==1)throw Error('모호하거나 없는 후보 앵커 '+prefix);return ns[0];};
const mappings=[
 ['P.life-tree','AILife.Compose('],['P.life-tree','LifeCursor.Apply('],
 ['P.life-tree-readonly','AIBrain.HandleResponse('],['P.life-tree-readonly','AILife.Navigate('],
 ['P.life-tree-readonly','AIBrain.AssembleLifePage('],['P.life-tree-direct','AILife.Resolve('],
 ['P.life-tree-direct','AIFarm.ExactCandidate('],['P.life-tree-direct','AICook.ExactCandidate('],
 ['P.life-tree-direct','AICraft.ExactCandidate('],['P.life-tree-direct','AIInventory.CandidatesFor('],
 ['P.life-tree-count','AIPlanProtocol.Read('],['P.life-tree-count','AILife.Remaining('],
 ['P.life-tree-result','AILife.Ended('],['P.life-tree-result','LifeProgress.Record(']
].map(([from,prefix])=>{const n=node(prefix);return {from,to:n.id,type:'reference',kind:'격리 후보 코드 대응',class:'정적 대응',sourceCommit:current.sourceCommit,runtimeTrace:false,evidence:[{path:current.relativeRoot+'/snapshot/'+n.file,start:n.lineStart,end:n.lineEnd}]};});
const graph={meta:{scope:'기획과 생활 격리 후보 코드의 부분 색인; 정식 전체 제품 그래프 아님',sourceCommit:current.sourceCommit,officialCodeBaseline:'685dde4d27dd91a782a14d8ae33fb576160fa578',finalAcceptance:'pending',installed:false,runtimeTested:false},entry:plan.entry,
 nodes:[...plan.nodes.map(n=>({...n,type:n.layer==='재기획'?'기획 항목':'기획 참고'})),...code.nodes],edges:[...plan.edges,...code.edges,...mappings]};
const ids=new Set(graph.nodes.map(n=>n.id));if(ids.size!==graph.nodes.length)throw Error('중복 ID');
for(const e of graph.edges)if(!ids.has(e.from)||!ids.has(e.to))throw Error('끝점 없음');
fs.writeFileSync(path.join(root,'candidate-project-preview.json'),JSON.stringify(graph,null,2)+'\n');
console.log(JSON.stringify({ok:true,nodes:graph.nodes.length,edges:graph.edges.length,codeMappings:mappings.length,sourceCommit:current.sourceCommit,finalAcceptance:'pending',officialMutated:false},null,2));
