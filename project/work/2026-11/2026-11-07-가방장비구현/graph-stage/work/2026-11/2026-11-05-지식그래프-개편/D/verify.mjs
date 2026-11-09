import assert from 'node:assert/strict';
import fs from 'node:fs';
import {build} from './build.mjs';
import {abs,read,bytes,write,validate,stale,readSources,featureTree,userPaths,hash} from './lib.mjs';
const one=build(),two=build();assert.equal(bytes(one),bytes(two));assert.equal(fs.readFileSync(abs('D/graph.json'),'utf8'),bytes(one.g));assert.equal(fs.readFileSync(abs('D/validation.json'),'utf8'),bytes(one.validation));
const g=one.g;assert.deepEqual(validate(g),[]);assert.deepEqual(stale(g),[]);
let headingChecks=0;for(const n of g.nodes.filter(n=>n.source?.section)){const sources=readSources(n,g),first=sources[0].text.split('\n')[0].replace(/^\d+: /,'');assert(/^#{1,6} /.test(first),'source begins at heading: '+n.id);assert(first.includes(n.source.section),'source heading matches node: '+n.id);headingChecks++;}
assert(readSources(g.nodes.find(n=>n.id==='pol.multi-ai'),g)[0].text.includes('단톡방 멤버마다 AI 캐릭터 하나씩(여럿)'));
assert(validate({...g,nodes:[...g.nodes,g.nodes[0]]}).some(e=>e.includes('duplicate')));
assert(validate({...g,edges:[...g.edges,{from:g.nodes[0].id,to:'__missing__',type:'reference'}]}).some(e=>e.includes('dangling')));
assert(stale({meta:{sources:[{path:g.meta.sources[0].path,sha256:'wrong'}]}}).some(e=>e.reason==='hash changed'));
assert(stale({meta:{sources:[{path:'D/does-not-exist',sha256:'wrong'}]}}).some(e=>e.reason==='missing'));
// Deterministic non-representative sample; primary status/goal/memory flow excluded.
let state=2026110505;const random=()=>{state=(Math.imul(state,1664525)+1013904223)>>>0;return state/4294967296;};
const candidates=g.nodes.filter(n=>n.layer==='B'&&n.type==='코드'&&n.kind!=='file'&&n.sourceLocations?.length&&!/AIPlanner|AIBrain|AIMemory|bridge\.mjs/.test(n.id));
const samples=[];for(let i=0;i<6&&candidates.length;i++){const n=candidates.splice(Math.floor(random()*candidates.length),1)[0],sources=readSources(n,g);assert(sources.length&&sources.every(s=>s.text.length));samples.push({id:n.id,sources,assessment:'원문 파일/줄 범위 실제 읽기; 의미 대조는 D/report.md 기록'});}
const manifest=read('preserved/manifest.json');for(const f of manifest.files)assert.equal(hash('preserved/'+f.snapshot),f.sha256);
const legacy=read('preserved/legacy/graph/graph.json'),mapping=read('C/legacy-capability-map.json');assert.equal(legacy.nodes.filter(n=>n.id.startsWith('F.')).length,mapping.mappings.length);
const report={passed:true,headingChecks,checks:['two builds byte-identical','on-disk graph and validation byte-identical','no duplicate nodes/dangling edges','source hashes valid','duplicate negative test rejected','dangling negative test rejected','stale hash detected','missing path detected','all document source locations begin at exact node heading','multi-ai source contains actual rule rather than prior reference','legacy snapshot all original hashes equal','all legacy F.* have mapping record'],seed:2026110505,samples,legacy:{nodes:legacy.nodes.length,edges:legacy.edges.length,capabilities:mapping.mappings.length},limits:['Structural/source extraction verification is not actual game runtime observation']};
write('D/verification.json',report);console.log(JSON.stringify({passed:report.passed,checks:report.checks,sampleIds:samples.map(s=>s.id)},null,2));
