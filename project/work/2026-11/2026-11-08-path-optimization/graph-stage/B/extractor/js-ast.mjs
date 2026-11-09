import fs from 'node:fs';
import path from 'node:path';
import {parse,version} from 'acorn';

export function extractJs(root) {
 const files=fs.readdirSync(path.join(root,'Tools/ai-bridge')).filter(f=>f.endsWith('.mjs')&&f!=='replay.mjs').sort().map(f=>'Tools/ai-bridge/'+f);
 const nodes=[],edges=[],facts=[],routes=[],imports=[],asts=new Map(), byId=new Map();
 const functions=new Set(['FunctionDeclaration','FunctionExpression','ArrowFunctionExpression']);
 function children(n){return Object.entries(n).filter(([k])=>!['loc','start','end'].includes(k)).flatMap(([,v])=>Array.isArray(v)?v.filter(x=>x?.type):v?.type?[v]:[]);}
 for(const file of files){
  const text=fs.readFileSync(path.join(root,file),'utf8'),ast=parse(text,{ecmaVersion:'latest',sourceType:'module',locations:true});
  const parents=new Map(),owner=new Map(),scope=new Map(),bindings=new Map(),ids=new Map(),all=[];
  const fid='code:'+file; nodes.push({id:fid,type:'코드',kind:'file',name:path.basename(file),file,lineStart:1,lineEnd:ast.loc.end.line});
  function walk(n,p,own,sc){
   parents.set(n,p);all.push(n);
   let nsc=sc;
   if(n.type==='Program'||functions.has(n.type)||n.type==='BlockStatement'||n.type==='CatchClause'){nsc=n;bindings.set(n,new Map());scope.set(n,sc);}
   let id;
   if(functions.has(n.type)||n.type==='ClassDeclaration'){
    let name=n.id?.name || (p?.type==='VariableDeclarator'?p.id.name:null) || (p?.type==='Property'||p?.type==='MethodDefinition'?p.key.name||p.key.value:null) || 'anonymous';
    id=fid+'#'+name+'@'+n.start;ids.set(n,id);byId.set(id,n);
    nodes.push({id,type:'코드',kind:n.type,name,file,lineStart:n.loc.start.line,lineEnd:n.loc.end.line,start:n.start,end:n.end});
    edges.push({from:own,to:id,type:'포함'}); if(functions.has(n.type)&&own!==fid) edges.push({from:own,to:id,type:'reference',kind:'defines',file,line:n.loc.start.line});
    if(p?.type==='CallExpression'||p?.type==='NewExpression')edges.push({from:own,to:id,type:'reference',kind:'callback',file,line:n.loc.start.line,resolution:'inline-argument'});
   }
   owner.set(n,id||own); if(!scope.has(n))scope.set(n,nsc);
   for(const ch of children(n))walk(ch,n,id||own,nsc);
  }
  walk(ast,null,fid,null);
  function enclosing(n){if(bindings.has(n))return n;return scope.get(n);}
  function bind(n,name,v){if(name)bindings.get(enclosing(n))?.set(name,v);}
  for(const n of all){
   const p=parents.get(n);
   if(n.type==='FunctionDeclaration'||n.type==='ClassDeclaration')bind(p,n.id?.name,{id:ids.get(n),node:n});
   if(n.type==='VariableDeclarator'&&n.id.type==='Identifier')bind(n,n.id.name,{id:ids.get(n.init),node:n.init});
   if(n.type==='ImportDeclaration'){
    const target=n.source.value.startsWith('.')?path.posix.normalize(path.posix.join(path.posix.dirname(file),n.source.value)):n.source.value;
    imports.push({from:fid,file,line:n.loc.start.line,target});
    for(const s of n.specifiers)bind(n,s.local.name,{import:target,name:s.imported?.name||'default'});
   }
  }
  function lookup(n,name){let sc=enclosing(n);const seen=new Set();while(sc&&!seen.has(sc)){seen.add(sc);if(bindings.get(sc)?.has(name))return bindings.get(sc).get(name);sc=scope.get(sc);}return null;}
  const src=n=>text.slice(n.start,n.end);
  asts.set(file,{ast,all,parents,owner,scope,bindings,ids,lookup,src,byId});
 }
 const exports=new Map();
 for(const [file,x]of asts){const m=new Map();exports.set(file,m); for(const n of x.all)if(n.type==='ExportNamedDeclaration'&&n.declaration){const d=n.declaration;if(d.id)m.set(d.id.name,x.ids.get(d));if(d.declarations)for(const v of d.declarations)m.set(v.id.name,x.ids.get(v.init));}}
 function resolve(x,n){if(!n)return null;if(x.ids.has(n))return x.ids.get(n);if(n.type==='Identifier'){const b=x.lookup(n,n.name);return b?.id || (b?.import?exports.get(b.import)?.get(b.name):null);}return null;}
 function registryTargets(x,n,seen=new Set()){
  if(!n||seen.has(n))return [];seen.add(n);
  const one=resolve(x,n);if(one)return [one];
  if(n.type==='Identifier')return registryTargets(x,x.lookup(n,n.name)?.node,seen);
  if(n.type==='MemberExpression'&&n.object.type==='Identifier'){
   const obj=x.lookup(n,n.object.name)?.node;if(obj?.type==='ObjectExpression')return obj.properties.filter(p=>n.computed||p.key.name===n.property.name).flatMap(p=>registryTargets(x,p.value,seen));
  }
  return [];
 }
 for(const [file,x]of asts){
  for(const n of x.all){const from=x.owner.get(n),line=n.loc.start.line;
   if(n.type==='CallExpression'||n.type==='NewExpression'){
    const target=resolve(x,n.callee);if(target)edges.push({from,to:target,type:'reference',kind:'call',file,line,resolution:'lexical-binding'});
    else for(const candidate of registryTargets(x,n.callee))edges.push({from,to:candidate,type:'reference',kind:'callback',file,line,resolution:'finite-object-registry-potential',possible:true});
    for(const a of n.arguments){const values=a.type==='ObjectExpression'?a.properties.map(p=>p.value):[a];for(const v of values){const cb=resolve(x,v);if(cb&&v?.type==='Identifier')edges.push({from,to:cb,type:'reference',kind:'callback',file,line,resolution:'function-value-argument'});}}
    facts.push({form:n.type==='CallExpression'?'call':'new',from,file,line,expr:x.src(n.callee),args:n.arguments.map(a=>({expr:x.src(a),constant:a.type==='Literal'?a.value:null})),target});
   }
   if(n.type==='ImportExpression')facts.push({form:'dynamic-import',from,file,line,expr:x.src(n.source)});
   if(n.type==='Literal'&&typeof n.value==='string')facts.push({form:'string',from,file,line,value:n.value});
   if(n.type==='MemberExpression'&&x.src(n.object)==='process.env')facts.push({form:'env',from,file,line,key:n.computed?(n.property.type==='Literal'?n.property.value:x.src(n.property)):n.property.name,operation:x.parents.get(n)?.type==='UnaryExpression'&&x.parents.get(n).operator==='delete'?'write':'read'});
   if(n.type==='IfStatement'){
    const terms=[];function termsOf(a){if(a.type==='LogicalExpression'){termsOf(a.left);termsOf(a.right);}else if(a.type==='BinaryExpression'&&a.operator==='==='&&a.right.type==='Literal')terms.push([x.src(a.left),a.right.value]);}termsOf(n.test);
    const method=terms.find(t=>t[0]==='req.method')?.[1],url=terms.find(t=>t[0]==='req.url')?.[1];
    if(method&&url){const handler='code:'+file+'#route:'+method+' '+url;nodes.push({id:handler,type:'코드',kind:'HTTP route handler',name:method+' '+url,file,lineStart:line,lineEnd:n.consequent.loc.end.line,start:n.start,end:n.consequent.end});routes.push({id:'ai-bridge:'+method+' '+url,method,path:url,handler,dispatcher:from,file,line,endLine:n.consequent.loc.end.line});}
   }
  }
 }
 // Return-object factory wiring is lexical, not arbitrary points-to inference. Resolve only direct keys.
 for(const [file,x]of asts){
  for(const n of x.all.filter(n=>n.type==='VariableDeclarator'&&n.init?.type==='CallExpression')){
   const factory=resolve(x,n.init.callee);if(!factory)continue;
   const fnode=byId.get(factory);const fx=[...asts.values()].find(a=>a.all.includes(fnode));if(!fx)continue;
   const returned=fx.all.filter(q=>q.type==='ReturnStatement'&&fx.owner.get(q)===factory);
   if(n.id.type==='Identifier')for(const r of returned){const target=resolve(fx,r.argument);if(target){for(const c of x.all.filter(q=>q.type==='CallExpression'&&q.callee.type==='Identifier'&&q.callee.name===n.id.name&&x.lookup(q,n.id.name)?.node===n.init))edges.push({from:x.owner.get(c),to:target,type:'reference',kind:'call',file,line:c.loc.start.line,resolution:'direct-returned-function'});}}
   const props=returned.flatMap(r=>r.argument?.type==='ObjectExpression'?r.argument.properties:[]);
   for(const p of props){const target=resolve(fx,p.value);if(!target)continue;const key=p.key.name||p.key.value;
    for(const c of x.all.filter(q=>q.type==='CallExpression'&&q.callee.type==='MemberExpression'&&q.callee.object.name===n.id.name&&q.callee.property.name===key))edges.push({from:x.owner.get(c),to:target,type:'reference',kind:'call',file,line:c.loc.start.line,resolution:'factory-return-property'});
    // const { health } = preflight; references to this exact destructuring.
    for(const d of x.all.filter(q=>q.type==='VariableDeclarator'&&q.id.type==='ObjectPattern'&&q.init?.name===n.id.name))for(const p2 of d.id.properties.filter(p=>p.key.name===key))for(const c of x.all.filter(q=>q.type==='CallExpression'&&q.callee.type==='Identifier'&&q.callee.name===p2.value.name))edges.push({from:x.owner.get(c),to:target,type:'reference',kind:'call',file,line:c.loc.start.line,resolution:'factory-destructuring'});
   }
  }
 }
 for(const r of routes){for(const e of edges)if(e.from===r.dispatcher&&e.file===r.file&&e.line>=r.line&&e.line<=r.endLine)e.from=r.handler;for(const f of facts)if(f.from===r.dispatcher&&f.file===r.file&&f.line>=r.line&&f.line<=r.endLine)f.from=r.handler;edges.push({from:r.dispatcher,to:r.handler,type:'reference',kind:'framework-invoked',file:r.file,line:r.line,resolution:'HTTP-method-path-dispatch'});}
 return {parser:'acorn',version,nodes,edges,facts,routes,imports};
}
