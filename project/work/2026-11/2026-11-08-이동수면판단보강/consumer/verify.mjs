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
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const same=(a,b)=>isDeepStrictEqual(a,b);
const assert=(ok,text)=>{if(!ok)throw Error(text);};
const finalizing=process.argv.includes('--finalize'),queriesOnly=process.argv.includes('--queries-only');
const todoFile=path.resolve(work,'../todo.md'),todoSha=sha(todoFile),todoLines=fs.readFileSync(todoFile,'utf8').split(/\r?\n/);
const pinRefresh={nodes:[],edges:[]};
function preserve(old,current,allowed,kind){
 if(same(old,current))return;
 assert(allowed.has(old.id)&&current,'unexpected old '+kind+' change '+old.id);
 assert(old.evidence?.length===1&&current.evidence?.length===1,'unexpected evidence shape '+old.id);
 const expected=structuredClone(old),e=current.evidence[0];
 assert(e.path==='../../../todo.md'&&e.doc==='todo'&&e.sha256===todoSha,'unexpected source pin refresh '+old.id);
 expected.evidence[0].sha256=todoSha;
 assert(same(expected,current),'change beyond todo whole-file SHA '+old.id);
 assert(todoLines[e.line-1]?.includes(e.anchor),'current anchor line differs '+old.id);
 pinRefresh[kind].push({id:old.id,path:e.path,anchor:e.anchor,line:e.line,currentLine:todoLines[e.line-1],beforeSha256:old.evidence[0].sha256,currentSha256:todoSha,basis:'Current session todo changed; identical semantic payload and anchor/line preserved, only the accurate whole-file source pin is refreshed.'});
}
const before=read(path.join(C,'before/graph.json')),g=read(path.join(live,'graph.json')),canonical=read(path.join(game,'published/graph.json'));
const records=read(path.join(P,'production/supplement.json')).records;
const targetTopics={grapple_pull_movement_end:'move',mount_safe_dismount:'vehicle',minecart_rail_brake_dismount:'vehicle',condition_sleep210:'food',condition_sleep322:'food'};
const nodes=new Map(g.nodes.map(n=>[n.id,n])),foreign=g.nodes.filter(n=>n.externalBehavior),beforeNodes=new Map(before.nodes.map(n=>[n.id,n]));
for(const old of before.nodes)preserve(old,nodes.get(old.id),new Set(['R.chapters','R.memo','R.mention','R.playtest']),'nodes');
const edgeSet=new Set(g.edges.map(e=>JSON.stringify(e)));
for(const old of before.edges)if(!edgeSet.has(JSON.stringify(old)))preserve(old,g.edges.find(e=>e.id===old.id),new Set(['E053','E055','E056']),'edges');
for(const node of foreign){const n=canonical.nodes[node.externalBehavior.id];assert(n&&same(node.externalBehavior.origin,n.origin??null)&&node.externalBehavior.kind===n.kind,'foreign mismatch '+node.id);}
assert(sha(path.join(live,'validation.json'))===sha(path.join(C,'before/validation.json')),'runtime/product validation changed');
const queries=[];
function run(args,name){
 const r=spawnSync(process.execPath,[cli,...args],{cwd:product,encoding:'utf8',maxBuffer:50*1024*1024,env});
 fs.writeFileSync(path.join(C,name+'.json'),r.stdout||'');
 if(r.stderr)fs.writeFileSync(path.join(C,name+'.stderr.txt'),r.stderr);
 assert(!r.error&&r.status===0,'project CLI failed '+name+' '+r.stderr);
 const value=JSON.parse(r.stdout);queries.push({args,name,exitCode:r.status,output:path.join(C,name+'.json')});return value;
}
const check=run(['check'],finalizing?'final-check':'check');assert(check.ok&&check.structuralErrors.length===0&&check.stale.length===0,'stale/project structure');
let rows=[],retainedQueries=[],preTodoEvidence=null;
if(finalizing){
 const prior=path.join(C,'verification-pre-todo.json'),v=read(prior);
 assert(v.status==='pass'&&v.awaitingFinalTodoAndBuild,'pre-todo query evidence absent');
 assert(same(v.gameHashes,Object.fromEntries(['graph.json','coverage.json','manifest.json'].map(n=>[n,sha(path.join(game,'published',n))]))),'game data changed since actual queries');
 for(const row of v.newRecords){const n=nodes.get(row.id);assert(n&&same(n.externalBehavior.origin,row.sourceOrigin),'new source reference changed '+row.id);assert(g.edges.some(e=>e.from===row.topic&&e.to===row.id&&e.kind==='해당 주제의 원본 근거'),'new classification changed '+row.id);}
 rows=v.newRecords;retainedQueries=v.actualProjectCliQueries;preTodoEvidence={path:prior,sha256:sha(prior),basis:'Exact game hashes/new foreign origins/topic links unchanged after todo-only provenance refresh; retain actual normal queries.'};
}else{
const topics=new Map();
for(const topic of new Set(Object.values(targetTopics)))topics.set(topic,run(['behavior','R.game-topic.'+topic],'topic-'+topic));
const planning=run(['behavior','P.expedition-navigation'],'planning-navigation');
assert(planning.topics.some(t=>t.id==='R.game-topic.move'),'normal planning move entry absent');
const expedition=run(['behavior','P.expedition'],'planning-expedition');assert(expedition.topics.some(t=>t.id==='R.game-topic.vehicle'),'normal expedition vehicle entry absent');
const life=run(['behavior','P.life'],'planning-life');assert(life.topics.some(t=>t.id==='R.game-topic.food'),'normal life food entry absent');
for(const [i,r] of records.entries()){
 const id='game:'+r.id,topic=targetTopics[r.action];assert(topic,'unclassified new action '+r.action);
 assert(!beforeNodes.has(id)&&nodes.has(id),'new exact foreign reference missing '+id);
 const view=topics.get(topic);assert(view.topics[0].targets.includes(id),'new topic entry missing '+id);
 const shown=run(['show',id],i+'-show'),source=run(['source',id],i+'-source');
 assert(shown.node.id===r.id&&shown.node.status==='independent_pass','show differs '+id);
 assert(same(source.value,r.value)&&same(source.origin,canonical.nodes[r.id].origin),'exact source payload differs '+id);
 const fields=new Map(shown.fields.map(f=>[f.field,f.value]));
 for(const key of ['conditions','costs','flow','results','failures','boundary'])if(key in r.value)assert(same(fields.get(key),r.value[key]),'judgment payload hidden '+id+'/'+key);
 rows.push({group:r.group,id,topic:'R.game-topic.'+topic,sourceOrigin:source.origin,sourceFree:r.sourceBindings.length===0});
}
}
let byteIdentical=null;
if(!queriesOnly){
const buildCheck=spawnSync(process.execPath,[path.join(product,'tools/product-build.mjs'),'--check'],{cwd:product,encoding:'utf8',maxBuffer:50*1024*1024,env});
fs.writeFileSync(path.join(C,'build-check.txt'),(buildCheck.stdout||'')+(buildCheck.stderr||''));
assert(!buildCheck.error&&buildCheck.status===0&&buildCheck.stdout.includes('byte-identical'),'product rebuild differs');
byteIdentical=true;
}
const summary={status:'pass',completionStage:queriesOnly?'normal_queries_verified_before_final_todo_update':'final_after_todo_refresh',awaitingFinalTodoAndBuild:queriesOnly,productNodes:g.nodes.length,productEdges:g.edges.length,foreignReferences:foreign.length,priorProjectSemanticNodesPreserved:before.nodes.length,priorProjectSemanticEdgesPreserved:before.edges.length,priorProjectByteExactNodes:before.nodes.length-pinRefresh.nodes.length,priorProjectByteExactEdges:before.edges.length-pinRefresh.edges.length,sourcePinRefresh:pinRefresh,currentTodoSha256:todoSha,priorRuntimeValidationUnchanged:true,newRecords:rows,actualProjectCliQueries:queries,retainedPreTodoActualQueries:retainedQueries,preTodoEvidence,productCheck:{ok:check.ok,structuralErrors:check.structuralErrors,stale:check.stale,baselineDrift:check.baselineDrift},productBuildCheckByteIdentical:byteIdentical,gameHashes:Object.fromEntries(['graph.json','coverage.json','manifest.json'].map(n=>[n,sha(path.join(game,'published',n))])),productHashes:{graph:sha(path.join(live,'graph.json')),validation:sha(path.join(live,'validation.json')),topics:sha(path.join(work,'2026-11/2026-11-08-행동판단프로젝트연계/topics.mjs'))},runtimeTested:false,wholeScopeComplete:false};
fs.writeFileSync(path.join(C,queriesOnly?'verification-pre-todo.json':'verification.json'),JSON.stringify(summary,null,2)+'\n');console.log(JSON.stringify({status:'pass',newRecords:rows.length,actualQueries:queries.length,retainedQueries:retainedQueries.length,awaitingFinalTodoAndBuild:queriesOnly,projectNodes:g.nodes.length,projectEdges:g.edges.length,foreignReferences:foreign.length}));
