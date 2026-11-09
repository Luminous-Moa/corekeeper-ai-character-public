// Project consumer check for the ProcessResources chain: preservation, exact foreign references,
// normal project queries (behavior -> topic -> show/source game:<ID>), check and rebuild identity.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {isDeepStrictEqual} from 'node:util';
const C=path.dirname(fileURLToPath(import.meta.url)),P=path.dirname(C),work=path.resolve(P,'../..');
const product=path.join(work,'지식그래프'),cli=path.join(product,'tools/product.mjs');
const env={...process.env,PYTHONDONTWRITEBYTECODE:'1'};
const live=path.join(work,'2026-11/2026-11-05-지식그래프-실화면/D');
const game=path.join(work,'게임행동-그래프');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^﻿/,''));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const same=(a,b)=>isDeepStrictEqual(a,b);
const assert=(ok,text)=>{if(!ok)throw Error(text);};
const todoFile=path.resolve(work,'../todo.md'),todoSha=sha(todoFile),todoLines=fs.readFileSync(todoFile,'utf8').split(/\r?\n/);
const pinRefresh={nodes:[],edges:[]};
function preserve(old,current,kind){
 if(same(old,current))return;
 assert(current,'old '+kind+' removed '+old.id);
 assert(old.evidence?.length===1&&current.evidence?.length===1,'unexpected change '+old.id);
 const expected=structuredClone(old),e=current.evidence[0];
 assert(e.path==='../../../todo.md'&&e.doc==='todo'&&e.sha256===todoSha,'unexpected change beyond todo pin '+old.id);
 expected.evidence[0].sha256=todoSha;
 assert(same(expected,current),'change beyond todo whole-file SHA '+old.id);
 assert(todoLines[e.line-1]?.includes(e.anchor),'current todo anchor line differs '+old.id);
 pinRefresh[kind].push({id:old.id,line:e.line,anchor:e.anchor,beforeSha256:old.evidence[0].sha256,currentSha256:todoSha});
}
const before=read(path.join(C,'before/graph.json')),g=read(path.join(live,'graph.json')),canonical=read(path.join(game,'published/graph.json'));
const records=read(path.join(P,'production/supplement.json')).records;
const nodes=new Map(g.nodes.map(n=>[n.id,n])),beforeNodes=new Map(before.nodes.map(n=>[n.id,n])),foreign=g.nodes.filter(n=>n.externalBehavior);
for(const old of before.nodes)preserve(old,nodes.get(old.id),'nodes');
const edgeSet=new Set(g.edges.map(e=>JSON.stringify(e)));
for(const old of before.edges)if(!edgeSet.has(JSON.stringify(old)))preserve(old,g.edges.find(e=>e.id&&e.id===old.id),'edges');
for(const node of foreign){const n=canonical.nodes[node.externalBehavior.id];assert(n&&same(node.externalBehavior.origin,n.origin??null)&&node.externalBehavior.kind===n.kind,'foreign mismatch '+node.id);}
const addedNodes=g.nodes.filter(n=>!beforeNodes.has(n.id)).map(n=>n.id);
const beforeEdgeSet=new Set(before.edges.map(e=>JSON.stringify(e)));
const addedEdges=g.edges.filter(e=>!beforeEdgeSet.has(JSON.stringify(e)));
assert(sha(path.join(live,'validation.json'))===sha(path.join(C,'before/validation.json')),'runtime/product validation changed');
const queries=[];
function run(args,name){
 const r=spawnSync(process.execPath,[cli,...args],{cwd:product,encoding:'utf8',maxBuffer:80*1024*1024,env});
 fs.writeFileSync(path.join(C,name+'.json'),r.stdout||'');
 if(r.stderr)fs.writeFileSync(path.join(C,name+'.stderr.txt'),r.stderr);
 assert(!r.error&&r.status===0,'project CLI failed '+name+' '+r.stderr);
 const value=JSON.parse(r.stdout);queries.push({args,name,exitCode:r.status,output:path.join(C,name+'.json')});return value;
}
const check=run(['check'],'check');assert(check.ok&&check.structuralErrors.length===0&&check.stale.length===0,'stale/project structure');
const planning=run(['behavior','P.life-tree-first-scope'],'planning-life-tree');
const craft=planning.topics.find(t=>t.id==='R.game-topic.craft');assert(craft,'normal planning craft entry absent');
const rows=[];
for(const [i,r] of records.entries()){
 const id='game:'+r.id;
 assert(!beforeNodes.has(id)&&nodes.has(id),'new exact foreign reference missing '+id);
 assert(craft.targets.includes(id),'craft topic does not reach '+id);
 assert(g.edges.some(e=>e.from==='R.game-topic.craft'&&e.to===id&&e.kind==='해당 주제의 원본 근거'),'topic edge missing '+id);
 const shown=run(['show',id],i+'-show'),source=run(['source',id],i+'-source');
 assert(shown.node.id===r.id&&shown.node.status==='independent_pass','show differs '+id);
 assert(same(source.value,r.value)&&same(source.origin,canonical.nodes[r.id].origin),'exact source payload differs '+id);
 const fields=new Map(shown.fields.map(f=>[f.field,f.value]));
 for(const key of ['conditions','flow','results','failures','boundary'])assert(same(fields.get(key),r.value[key]),'judgment payload hidden '+id+'/'+key);
 rows.push({id,topic:'R.game-topic.craft',action:r.action,sources:r.sourceBindings.map(b=>b.objectId),sourceOrigin:source.origin});
}
// Existing open statement now leads to the new chain; the old row itself is unchanged.
const old='base:D::CAB-2ebcba97367158c9b08f790c25d642c9:7493579351412767700#inventory_transfer';
const oldShow=run(['show','game:'+old],'furnace-inventory-transfer-show');
const rel=(oldShow.relations||[]).filter(x=>x.relation==='has_process_resources_supplement').map(x=>x.target).sort();
assert(same(rel,['completion:process-resources:craft-timer-v1','completion:process-resources:output-v1']),'old open statement does not lead to new chain '+JSON.stringify(rel));
assert(same(beforeNodes.get('game:'+old)?.externalBehavior?.origin,canonical.nodes[old].origin),'old row origin changed');
const target=run(['game','target','4004'],'game-target-4004');
const ev=target.sources[0].essentialDecisionEvidence.map(e=>e.id).sort();
assert(same(ev,records.map(r=>r.id).sort()),'target 4004 decision evidence '+JSON.stringify(ev));
const buildCheck=spawnSync(process.execPath,[path.join(product,'tools/product-build.mjs'),'--check'],{cwd:product,encoding:'utf8',maxBuffer:80*1024*1024,env});
fs.writeFileSync(path.join(C,'build-check.txt'),(buildCheck.stdout||'')+(buildCheck.stderr||''));
assert(!buildCheck.error&&buildCheck.status===0&&buildCheck.stdout.includes('byte-identical'),'product rebuild differs');
const summary={status:'pass',productNodes:g.nodes.length,productEdges:g.edges.length,foreignReferences:foreign.length,priorProjectNodes:before.nodes.length,priorProjectEdges:before.edges.length,addedNodes,addedEdges:addedEdges.length,pinRefresh,newRecords:rows,oldOpenStatement:{id:old,relations:rel,unchanged:true},target4004DecisionEvidence:ev,projectCheck:{ok:check.ok,stale:check.stale,structuralErrors:check.structuralErrors},rebuildByteIdentical:true,actualProjectCliQueries:queries,gameHashes:Object.fromEntries(['graph.json','coverage.json','manifest.json'].map(n=>[n,sha(path.join(game,'published',n))])),projectGraphSha256:sha(path.join(live,'graph.json')),todoSha256:todoSha,runtimeTested:false};
fs.writeFileSync(path.join(C,'verification.json'),JSON.stringify(summary,null,2)+'\n');
console.log(JSON.stringify({status:'pass',newRecords:rows.length,actualQueries:queries.length,projectNodes:g.nodes.length,projectEdges:g.edges.length,addedNodes:addedNodes.length,addedEdges:addedEdges.length,foreignReferences:foreign.length,pinRefresh:pinRefresh.nodes.length+'/'+pinRefresh.edges.length}));
