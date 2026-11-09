import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const currentPath=path.join(root,'candidate-code-current.json');
const current=JSON.parse(fs.readFileSync(currentPath,'utf8'));
const dir=path.join(root,current.relativeRoot),raw=JSON.parse(fs.readFileSync(path.join(dir,'csharp.raw.json'),'utf8'));
const graphPath=path.join(dir,'anchors.graph.json'),graph=JSON.parse(fs.readFileSync(graphPath,'utf8'));
for(const prefix of ['AILife.Resolve(','AIBrain.AssembleLifePage(','AIBrain.AddCommonControls(','AIBrain.OnSkillEnded(']){
 const matches=raw.nodes.filter(n=>n.signature?.startsWith(prefix));
 if(matches.length!==1)throw Error('실제 앵커가 없거나 모호함: '+prefix);
 if(!graph.nodes.some(n=>n.id===matches[0].id))graph.nodes.push({...matches[0],sourceCommit:current.sourceCommit,runtimeTrace:false,evidenceScope:'isolated final production candidate; self checks passed, independent acceptance pending'});
}
const ids=new Set(graph.nodes.map(n=>n.id));graph.edges=raw.edges.filter(e=>ids.has(e.from)&&ids.has(e.to));
fs.writeFileSync(graphPath,JSON.stringify(graph,null,2)+'\n');
const manifestPath=path.join(dir,'input-manifest.json'),manifest=JSON.parse(fs.readFileSync(manifestPath,'utf8'));
manifest.checks.anchors=graph.nodes.length;manifest.checks.staticEdges=graph.edges.length;
current.checks.anchors=graph.nodes.length;current.checks.staticEdges=graph.edges.length;
fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2)+'\n');fs.writeFileSync(currentPath,JSON.stringify(current,null,2)+'\n');
console.log(JSON.stringify({sourceCommit:current.sourceCommit,anchors:graph.nodes.length,staticEdges:graph.edges.length,newSourceExtraction:false},null,2));
