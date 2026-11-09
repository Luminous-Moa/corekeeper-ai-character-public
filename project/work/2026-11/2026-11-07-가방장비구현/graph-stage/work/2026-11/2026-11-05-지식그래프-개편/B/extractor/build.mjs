import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {extractJs} from './js-ast.mjs';
const B=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),S=path.join(B,'snapshot');
const cmp=(a,b)=>a<b?-1:a>b?1:0;
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const write=(p,o)=>fs.writeFileSync(path.join(B,p),JSON.stringify(o,null,2)+'\n');
const cs=JSON.parse(fs.readFileSync(path.join(B,'csharp.raw.json'),'utf8')),js=extractJs(S);write('javascript.raw.json',js);
const nodes=new Map([...cs.nodes,...js.nodes].map(n=>[n.id,n])),edges=[...cs.edges,...js.edges],facts=[...cs.facts,...js.facts],boundaries=[],features=[];
function node(n){nodes.set(n.id,n);return n.id;}
function edge(from,type,to,extra={}){edges.push({from,type,to,...extra});}
function feature(id,kind,target,evidence,extra={}){node({id,type:'기능',kind,name:id,...evidence,...extra});features.push(id);edge(id,'기능→코드',target,evidence);}
node({id:'code:context:corekeeper-ai',type:'코드',kind:'C4 Context',name:'Core Keeper AI product'});
for(const [id,name] of [['mod','Game mod'],['build','Unity editor build tools'],['bridge','Node ai-bridge']]){node({id:'code:container:'+id,type:'코드',kind:'C4 Container',name});edge('code:context:corekeeper-ai','포함','code:container:'+id);}
const fileNodes=[...nodes.values()].filter(n=>n.kind==='file');
for(const f of fileNodes){const c=f.file.startsWith('Tools/')?'bridge':f.file.includes('HeadlessTools')?'build':'mod';const comp='code:component:'+path.posix.dirname(f.file);if(!nodes.has(comp)){node({id:comp,type:'코드',kind:'C4 Component',name:path.posix.dirname(f.file),judgment:'LLM: same source directory groups modules'});edge('code:container:'+c,'포함',comp);}edge(comp,'포함',f.id);}
const methods=cs.nodes.filter(n=>n.kind==='MethodDeclaration');
for(const f of cs.facts.filter(f=>f.form==='call'&&!f.target&&f.candidates?.length===1))edge(f.from,'reference',f.candidates[0],{kind:'call',file:f.file,line:f.line,resolution:'single-Roslyn-candidate-with-semantic-error',possible:true});
const getMethod=(type,name)=>methods.filter(n=>n.signature.startsWith(type+'.'+name+'('));
for(const cl of cs.nodes.filter(n=>n.bases))for(const m of methods.filter(m=>m.file===cl.file&&m.lineStart>cl.lineStart&&m.lineEnd<cl.lineEnd&&m.modifiers?.includes('override'))){for(const baseName of cl.bases.slice(1).split(',').map(s=>s.trim()))for(const bm of getMethod(baseName,m.name))if(!edges.some(e=>e.from===bm.id&&e.to===m.id&&e.kind==='framework-invoked'))edge(bm.id,'reference',m.id,{kind:'framework-invoked',file:m.file,line:m.lineStart,resolution:'syntax-base-override-potential-missing-metadata',possible:true});}
function hook(n,origin){feature('hook:'+n.signature.split('(')[0],'hook',n.id,{file:n.file,line:n.lineStart},{registration:origin});}
// Known interface entry contract is a recorded stack judgment; membership comes from AST declarations.
for(const cl of cs.nodes.filter(n=>n.bases)){
 const base=cl.bases;
 for(const m of methods.filter(m=>m.file===cl.file&&m.lineStart>cl.lineStart&&m.lineEnd<cl.lineEnd&&m.signature.startsWith(cl.name+'.'))){
  if(base.includes('MonoBehaviour')&&m.name==='OnGUI')hook(m,'Unity MonoBehaviour.OnGUI message; statically identified, not a runtime trace');
  if(base.includes('IMod')&&['EarlyInit','Init','ModObjectLoaded','CanBeUnloaded','Shutdown','Update'].includes(m.name))hook(m,'IMod interface');
  if(base.includes('INetworkStreamDriverConstructor')&&['CreateClientDriver','CreateServerDriver'].includes(m.name))hook(m,'INetworkStreamDriverConstructor interface');
 }
}
for(const f of cs.facts.filter(f=>f.form==='call'&&f.expr==='AccessTools.Method')){
 const ty=f.args[0]?.expr.match(/^typeof\(([^)]+)\)$/)?.[1],name=f.args[1]?.constant??f.args[1]?.expr.match(/^nameof\([^.)]+\.([^.)]+)\)$/)?.[1];
 if(ty&&name){const mm=getMethod(ty,name);if(mm.length===1){edge(f.from,'reference',mm[0].id,{kind:'framework-invoked',file:f.file,line:f.line,resolution:'AccessTools.Method typeof+constant/nameOf'});if(ty==='AIPlayerSpikePatches')hook(mm[0],'Harmony patch registered via AccessTools.Method');}}
}
const slash=getMethod('AIPlayerSpikeMod','TryHandleChatCommand')[0];if(slash)feature('slash:ai','slash command',slash.id,{file:slash.file,line:slash.lineStart},{selector:'IsAiCommand /ai; subcommands handled within one request dispatcher'});
for(const m of methods.filter(m=>m.signature.startsWith('HeadlessSdk.')&&['ImportGameFiles','CreateMod','BuildAndInstall','CompileCheck'].includes(m.name)))feature('cli:HeadlessSdk.'+m.name,'CLI',m.id,{file:m.file,line:m.lineStart},{registration:'Unity -executeMethod convention; LLM stack judgment, not invoked during extraction'});
for(const r of js.routes)feature(r.id,'API',r.handler,{file:r.file,line:r.line,endLine:r.endLine},{method:r.method,path:r.path});
feature('ai-bridge:startup:bridge.mjs.module','startup','code:Tools/ai-bridge/bridge.mjs',{file:'Tools/ai-bridge/bridge.mjs',line:1},{entry:'ES module top level; synthetic module name, no distinct startup function'});
for(const i of js.imports){if(nodes.has('code:'+i.target))edge(i.from,'reference','code:'+i.target,{kind:'type reference',file:i.file,line:i.line,resolution:'static-import'});else boundaries.push({...i,form:'static-import',decision:'boundary',reason:i.target.startsWith('node:')?'Node runtime':'dependency outside product extraction scope'});}
function data(f,key,operation,extra={}){const id='data:'+key;node({id,type:'데이터',name:key,...extra});edge(f.from,operation,id,{file:f.file,line:f.line,expression:f.expr});}
function external(f,key,extra={}){const id='external:'+key;node({id,type:'외부 연결',name:key,...extra});edge(f.from,'코드→외부 연결',id,{file:f.file,line:f.line,expression:f.expr});return id;}
for(const f of facts){
 if(f.form==='env')data(f,'env:'+f.key,f.operation,{dynamic:f.key==='k'});
 if(f.form==='call'||f.form==='new'){
  const a=f.args||[],v=a[0]?.constant??a[0]?.expr;
  if(/^(?:System\.IO\.)?(?:File|Directory)\.(?:Read\w*|Exists|GetFiles|Enumerate\w*)$|^fs\.(?:read\w*|existsSync|readdirSync|statSync)$/.test(f.expr))data(f,f.file+':'+v,'read',{locator:v,locatorKind:a[0]?.constant!=null?'constant':'expression'});
  if(/^(?:System\.IO\.)?(?:File|Directory)\.(?:Write\w*|Append\w*|Delete|Move|Copy|Create\w*|Replace)$|^fs\.(?:write\w*|append\w*|mkdir\w*|unlink\w*|rename\w*|rm\w*)$/.test(f.expr))data(f,f.file+':'+v,'write',{locator:v,locatorKind:a[0]?.constant!=null?'constant':'expression'});
  if(/^(?:System\.IO\.)?StreamWriter$/.test(f.expr))data(f,f.file+':'+v,'write',{locator:v,locatorKind:'expression',operation:'stream-open-writer'});
  if(/^(?:System\.IO\.)?FileStream$/.test(f.expr)){const access=a.find(x=>x.expr?.startsWith('FileAccess.'))?.expr;if(access?.includes('Read'))data(f,f.file+':'+v,'read',{locator:v,access});if(access?.includes('Write'))data(f,f.file+':'+v,'write',{locator:v,access});if(!access)boundaries.push({...f,decision:'boundary',reason:'FileStream access not statically identified'});}
  if(f.expr.startsWith('EditorPrefs.Set'))data(f,'EditorPrefs:'+v,'write',{locator:v});
  if(f.expr.startsWith('EditorPrefs.Get'))data(f,'EditorPrefs:'+v,'read',{locator:v});
  if(f.expr==='Environment.GetEnvironmentVariable')data(f,'env:'+v,'read');
  if(f.expr==='UnityWebRequest'||f.expr==='UnityWebRequest.Get'){
   const url=a[0]?.constant??a[0]?.expr,verb=f.expr.endsWith('.Get')?'GET':a[1]?.constant??a[1]?.expr;
   if(typeof url==='string'&&url.startsWith('http://127.0.0.1:8331/')){
    const route=js.routes.find(r=>r.method===verb&&r.path===url.slice('http://127.0.0.1:8331'.length));
    if(route){edge(f.from,'reference',route.handler,{kind:'http',file:f.file,line:f.line,address:url,method:verb,feature:route.id});edge(route.id,'기능→코드',f.from,{file:f.file,line:f.line,role:'API caller'});}else boundaries.push({...f,decision:'boundary',reason:'local HTTP route not matched',url,verb});
   }else external(f,String(url),{method:verb,address:url});
  }
  if(/^(?:adapters|ad)\.chat$/.test(f.expr)||f.expr==='run'||f.expr.startsWith('CLI[')||f.expr==='killTree')external(f,'ai-chatroom:'+f.expr,{boundary:true,contract:'runtime modules outside pinned SDK commit'});
  if(/(?:GetMethod|GetField|\.Invoke$|FindType|AccessTools|GetNestedTypes|GetMethods)/.test(f.expr))boundaries.push({...f,decision:'boundary',reason:'reflection target outside source or unresolved; explicit local AccessTools targets represented separately'});
  if(!f.target)boundaries.push({...f,decision:'boundary',reason:'not statically resolved by available semantic/lexical bindings; may be BCL/framework, dynamic or missing reference'});
 }
 if(f.form==='dynamic-import'){external(f,'import:'+f.expr,{dynamic:true});boundaries.push({...f,decision:'boundary',reason:'dynamic path import; external chatroom or test-only fake module'});}
}
// Property and field references in C# are code references, not inferred storage/DB entities.
const sortedNodes=[...nodes.values()].sort((a,b)=>cmp(a.id,b.id));
const sortedEdges=[...new Map(edges.map(e=>[JSON.stringify(e),e])).values()].sort((a,b)=>cmp(JSON.stringify(a),JSON.stringify(b)));
const missing=sortedEdges.filter(e=>!nodes.has(e.from)||!nodes.has(e.to));if(missing.length)throw new Error('dangling edges '+JSON.stringify(missing.slice(0,3)));
const graph={meta:{schema:'02a-graph-definitions',stage:'B',sourceCommit:'39b22df96dffe869ab0ccd474b18d31462b71d70',sourceRoot:'snapshot',parsers:{csharp:cs.version,javascript:'acorn '+js.version},c4Judgment:'LLM: context=product; containers=mod/editor tool/Node process; components=source directories',limitations:['No Unity/game metadata references: semantic diagnostics and unresolved calls retained','JS lexical and direct factory wiring only; dynamic dispatch remains boundary','Runtime trace is not observed in B; requires separate authorized execution evidence','Source code only; A artifacts and existing planning documents excluded']},nodes:sortedNodes,edges:sortedEdges};write('code.graph.json',graph);
const adjacency=new Map();for(const e of sortedEdges)if(e.type==='reference'){if(!adjacency.has(e.from))adjacency.set(e.from,[]);adjacency.get(e.from).push(e.to);}
const reached=new Set(),queue=sortedEdges.filter(e=>e.type==='기능→코드').map(e=>e.to);while(queue.length){const n=queue.shift();if(reached.has(n))continue;reached.add(n);queue.push(...(adjacency.get(n)||[]));}
const executable=sortedNodes.filter(n=>/MethodDeclaration|ConstructorDeclaration|AccessorDeclaration|LocalFunction|AnonymousFunction|LambdaExpression|FunctionDeclaration|FunctionExpression/.test(n.kind));
const unreached=executable.filter(n=>!reached.has(n.id));
let seed=20261105;const next=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed;};
const shuffled=[...features].sort(cmp);for(let i=shuffled.length-1;i>0;i--){const j=next()%(i+1);[shuffled[i],shuffled[j]]=[shuffled[j],shuffled[i]];}
write('validation.json',{seed:20261105,samples:shuffled.slice(0,8).map(id=>({id,handlers:sortedEdges.filter(e=>e.from===id&&e.type==='기능→코드'),sourceCheck:'pending manual review'})),unreached,apiWithoutCaller:js.routes.filter(r=>!sortedEdges.some(e=>e.from===r.id&&e.role==='API caller')),runtimeCheck:{status:'not-performed-by-B',reason:'No product execution authorized in this code-only lane. A runtime results excluded as inputs; D must compare independent runtime evidence.'},counts:{nodes:sortedNodes.length,edges:sortedEdges.length,csharpFiles:cs.nodes.filter(n=>n.kind==='file').length,jsFiles:js.nodes.filter(n=>n.kind==='file').length,features:features.length,executable:executable.length,unreached:unreached.length,syntaxDiagnostics:cs.diagnostics.filter(d=>d.kind==='syntax').length,semanticDiagnostics:cs.diagnostics.filter(d=>d.kind==='semantic').length}});
write('boundaries.json',boundaries.sort((a,b)=>cmp(JSON.stringify(a),JSON.stringify(b))));
const forms={};for(const f of facts){const k=f.file.endsWith('.cs')?'C#':'JavaScript';forms[k]??={};forms[k][f.form]=(forms[k][f.form]||0)+1;}write('call-forms.json',{forms,imports:js.imports,notImported:js.nodes.filter(n=>n.kind==='file'&&!js.imports.some(i=>i.target===n.file)).map(n=>n.file),db:{status:'none-detected',basis:'No DB schema or SQL/ORM call in scoped source. PugDatabase is game SDK boundary, JSON/JSONL are data.'},analytics:{status:'none-detected',basis:'Local JSONL logs and in-game memory events are data; no analytics transport found.'}});
function inventory(dir,base=dir){return fs.readdirSync(dir,{withFileTypes:true}).sort((a,b)=>cmp(a.name,b.name)).flatMap(e=>e.isDirectory()?inventory(path.join(dir,e.name),base):[{path:path.relative(base,path.join(dir,e.name)).replaceAll('\\','/'),sha256:hash(fs.readFileSync(path.join(dir,e.name))),bytes:fs.statSync(path.join(dir,e.name)).size}]);}
write('snapshot.manifest.json',{commit:graph.meta.sourceCommit,creation:'git archive initially; then pin-snapshot.mjs materializes exact git cat-file blob bytes, avoiding Windows archive EOL filters; no checkout/worktree mutation',files:inventory(S),runtimePackage:inventory(path.join(B,'runtime-package')),excluded:'runtime-package Tools/knowledge is deployment dependency, not source of product graph nodes'});
const meaningsPath=path.join(B,'meanings.json');if(fs.existsSync(meaningsPath)){const meanings=JSON.parse(fs.readFileSync(meaningsPath,'utf8'));for(const n of graph.nodes)if(meanings.features[n.id])n.summary=meanings.features[n.id];graph.meta.meanings='meanings.json';}
const inference=path.join(B,'inference.graph.json');if(fs.existsSync(inference)){const g=JSON.parse(fs.readFileSync(inference,'utf8'));graph.nodes.push(...g.nodes);graph.edges.push(...g.edges);graph.meta.inference='inference.graph.json (code-only hypothesis, not observed product truth)';}
const finalIds=new Set(graph.nodes.map(n=>n.id));if(finalIds.size!==graph.nodes.length)throw Error('Duplicate merged node IDs');if(graph.edges.some(e=>!finalIds.has(e.from)||!finalIds.has(e.to)))throw Error('Dangling merged edge');
graph.nodes.sort((a,b)=>cmp(a.id,b.id));graph.edges.sort((a,b)=>cmp(JSON.stringify(a),JSON.stringify(b)));write('B.graph.json',graph);
console.log(JSON.stringify({nodes:graph.nodes.length,edges:graph.edges.length,features:features.length,unreached:unreached.length}));
