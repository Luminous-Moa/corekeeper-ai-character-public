import assert from 'node:assert/strict';
import fs from 'node:fs';
import {integrate} from './integrate.mjs';
import {abs,live,read,bytes,write,validate,stale,readSources,indexes,featureTree,hash} from './live-lib.mjs';
const one=integrate(),two=integrate(),g=one.g,ix=indexes(g);
assert.equal(bytes(one),bytes(two));
assert.equal(fs.readFileSync(abs(live('D/graph.json')),'utf8'),bytes(g));
assert.equal(fs.readFileSync(abs(live('D/validation.json')),'utf8'),bytes(one.validation));
assert.deepEqual(validate(g),[]);assert.deepEqual(stale(g),[]);
for(const previous of read(live('D/preexisting-hashes.json')))assert.equal(hash(previous.path),previous.sha256,'prior artifact preserved: '+previous.path);
const liveElements=g.nodes.filter(n=>n.layer==='live-A2'&&n.type==='화면요소');
assert.equal(liveElements.length,57);
for(const n of liveElements){const source=readSources(n,g)[0];assert(source.text.includes(n.id),'exact element source '+n.id);}
const old=read('D/graph.json');
for(const n of old.nodes){const current=ix.nodes.get(n.id);assert(current,'old ID retained '+n.id);for(const [k,v] of Object.entries(n))assert.deepEqual(current[k],v,'old node field retained '+n.id+'.'+k);}
const serializedEdges=new Set(g.edges.map(bytes));
for(const edge of old.edges)assert(serializedEdges.has(bytes(edge)),'old edge retained');
for(const s of read('preserved/manifest.json').files)assert.equal(hash('preserved/'+s.snapshot),s.sha256);
const activity='CKAI-ACT-01',input='CKAI-02#3',output='CKAI-02#2-7',feature='slash:ai';
const has=(a,b,t)=>g.edges.some(e=>e.from===a&&e.to===b&&e.type===t);
assert(has(activity,input,'유저 활동→화면요소'));assert(has(input,feature,'화면요소→기능'));assert(has(feature,output,'기능→화면요소'));
assert(!has(output,feature,'화면요소→기능'),'result is not a trigger');
const tree=featureTree(g,feature);const status='code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs#AIPlayerSpikeMod.StatusAll()';
const members='code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs#AIWorldSettings.Members(string)';
assert(tree.reached.includes(status));assert(tree.reached.includes(members));
assert(has(members,'data:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs:p','read'));
assert(readSources(ix.nodes.get(status),g)[0].text.includes('AIBrain.Goal.Text'));
assert.equal(g.nodes.filter(n=>n.type==='제품코어').length,old.nodes.filter(n=>n.type==='제품코어').length,'no second product core');
assert.equal(g.nodes.filter(n=>n.type==='기능').length,old.nodes.filter(n=>n.type==='기능').length,'no invented command features');
for(const n of g.nodes.filter(n=>n.layer==='live-A2'&&n.type==='정책'))assert(n.authority.includes('후보')&&n.status.includes('후보'));
let state=2026110506;const random=()=>{state=(Math.imul(state,1664525)+1013904223)>>>0;return state/4294967296;};
const candidates=liveElements.filter(n=>!['CKAI-02#3','CKAI-02#2-7','CKAI-01#1-1'].includes(n.id)).sort((a,b)=>a.id<b.id?-1:1),samples=[];
for(let i=0;i<5;i++){const n=candidates.splice(Math.floor(random()*candidates.length),1)[0];samples.push({id:n.id,source:readSources(n,g),positions:n.positions,verdict:'원문 ID·문구·관측 조건 대조; 시각 검수 판정은 report.md 참조'});}
const report={passed:true,seed:2026110506,samples,checks:['two independent builds byte-identical','on-disk graph and validation match regeneration','all registered source hashes match','all prior node fields and edges preserved','prior generated artifacts and preserved citations unchanged','57 element document sections have exact ID','activity to input to slash:ai to status/members/file-data path exists','output producer reverse direction preserved','no additional product core or command feature','display candidates remain explicitly non-authoritative'],limits:['정적 도달은 실행 trace 아님','현재 게임 설정·저장 파일은 읽지 않음','실제 게임 렌더러·전투 전체 실행판은 기준 그래프 밖']};
write(live('D/verification.json'),report);
console.log(JSON.stringify({passed:true,seed:report.seed,sampleIds:samples.map(s=>s.id),checks:report.checks},null,2));
