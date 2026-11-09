import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';

// This is artifact tooling, not product introspection. It reads only the named A documents.
const root=path.dirname(fileURLToPath(import.meta.url));
const documents=['A2/screens.md','A4/inferences.md'];
const allowedTypes=new Set(['제품코어','유저저니','IA','유저 활동','화면','화면요소','정책']);
const nodes=[],edges=[],requestMappings=[],sources=[];
for(const document of documents){
  const text=fs.readFileSync(path.join(root,document),'utf8');
  const blocks=[...text.matchAll(/```json\s*\r?\n([\s\S]*?)\r?\n```/g)];
  if(blocks.length!==1) throw new Error(`${document}: expected exactly one JSON declaration`);
  const declaration=JSON.parse(blocks[0][1]);
  const prose=text.replace(blocks[0][0],'');
  sources.push({document,sha256:createHash('sha256').update(text).digest('hex')});
  for(const node of declaration.nodes??[]){
    if(!node.id || !allowedTypes.has(node.type))throw new Error(`Invalid node ${node.id}`);
    if(!prose.includes(node.id))throw new Error(`ID missing in visible document ${node.id}`);
    nodes.push({...node,title:node.title??node.summary,class:document.startsWith('A4/')?'A-inferred':'A-observed-output',doc:document,source:{...node.source,document}});
  }
  edges.push(...(declaration.edges??[]).map(edge=>({...edge,from:edge.source,to:edge.target,class:document.startsWith('A4/')?'A-inferred':'A-observed-output',evidence:edge.evidence??document})));
  requestMappings.push(...(declaration.requestMappings??[]));
}
const ids=new Set();
for(const node of nodes){if(ids.has(node.id))throw new Error(`Duplicate ID ${node.id}`);ids.add(node.id);}
const nodeById=new Map(nodes.map(n=>[n.id,n]));
for(const ia of nodes.filter(n=>n.type==='IA')){
  for(const [screenId,group] of Object.entries(ia.screenGroups??{})){
    const screen=nodeById.get(screenId);
    if(!screen || screen.type!=='화면')throw new Error(`IA points to missing screen ${screenId}`);
    if(screen.iaGroup && screen.iaGroup!==group)throw new Error(`Multiple IA groups ${screenId}`);
    screen.iaGroup=group;screen.iaSource=ia.source;screen.iaInference=true;
  }
}
for(const edge of edges){
  if(!ids.has(edge.source)||!ids.has(edge.target))throw new Error(`Dangling edge ${JSON.stringify(edge)}`);
  const expected=nodeById.get(edge.source).type+'→'+nodeById.get(edge.target).type;
  if(edge.type!==expected && edge.type!=='포함')throw new Error(`Unexpected edge type ${edge.type}; expected ${expected}`);
}
for(const mapping of requestMappings){
  if(!ids.has(mapping.outputId))throw new Error(`Missing output ${mapping.outputId}`);
  if(mapping.rawMeta){
    const meta=JSON.parse(fs.readFileSync(path.join(root,mapping.rawMeta),'utf8').replace(/^\uFEFF/,''));
    if(mapping.method!==meta.method || mapping.status!==meta.httpStatus)throw new Error(`HTTP mismatch ${mapping.observationId}`);
    if(new URL(meta.url).pathname!==mapping.path)throw new Error(`Path mismatch ${mapping.observationId}`);
  }
  if(mapping.rawBody && !fs.existsSync(path.join(root,mapping.rawBody)))throw new Error(`Missing body ${mapping.rawBody}`);
}
const graph={
  schema:'product-graph/A/v1',stage:'A',status:'partial-actual-http-observation',referenceCommit:'e82eb87',
  boundaries:{productSourceRead:false,codeGraphRead:false,realProductHttp:true,mock:false,replay:false,realGameObserved:false,realGameUiDeferredByUser:true,externalModelCallsRequested:false,isolatedPort:18331},
  sources,nodes,edges,requestMappings,
  evidenceDocuments:['scope.md','A1-exploration.md','A3/A3-first-user-report.md'],
  environmentObservations:[
    {id:'OBS-01',kind:'client-connection-failure',source:'A1-exploration.md',raw:'raw/health-02.stderr.txt',productResponse:false,httpStatus:null},
    {id:'OBS-02',kind:'incomplete-package-startup-failure',source:'A1-exploration.md',raw:'raw/bridge-start.stderr.txt',productHttpResponse:false}
  ],
  deferredOrUnobserved:['Real game UI and chat commands (user deferred)','Successful AI planning and actual companion behavior','Authenticated/subscription/quota states','Busy, cancellation and timeout states','Populated world, memory and game knowledge graph','Operational installed-game version'],
  matchingNote:'HTTP method/path retained for D; no function IDs or code mappings inferred in A.'
};
graph.evidenceManifest=['raw','A3'].flatMap(dir=>fs.readdirSync(path.join(root,dir)).sort().filter(name=>fs.statSync(path.join(root,dir,name)).isFile()).map(name=>{
  const file=dir+'/'+name,bytes=fs.readFileSync(path.join(root,file));
  return {file,bytes:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex')};
}));
fs.writeFileSync(path.join(root,'graph-A.json'),JSON.stringify(graph,null,2)+'\n');
const counts={};for(const node of nodes)counts[node.type]=(counts[node.type]??0)+1;
const verification={passed:true,counts,nodes:nodes.length,edges:edges.length,requestMappings:requestMappings.length,duplicateIds:0,danglingEdges:0,httpEvidenceMismatches:0,checks:['Document JSON parsed','Allowed A node types','Unique IDs','Edge endpoints','Edge names from node types','Observed request method/path/status against raw metadata','Referenced response bodies exist'],limits:['Structural verification is not complete product coverage','Runtime provenance supplied by HEAD','Game UI deferred by user']};
fs.writeFileSync(path.join(root,'graph-A-validation.json'),JSON.stringify(verification,null,2)+'\n');
console.log(JSON.stringify(verification));
