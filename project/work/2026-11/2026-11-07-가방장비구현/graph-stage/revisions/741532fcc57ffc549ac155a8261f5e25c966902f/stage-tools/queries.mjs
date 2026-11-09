import fs from 'node:fs';import path from 'node:path';import {spawnSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),G=path.join(stage,'work/지식그래프'),L=path.join(stage,'work/2026-11/2026-11-05-지식그래프-실화면');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const graph=read(path.join(L,'D/graph.json')),dev=read(path.join(G,'graph/graph.json')),pin=read(path.join(stage,'final-pin.json'));
const rows=[],assertions=[];
function assert(name,ok){assertions.push({name,ok});if(!ok)throw Error(name);}
function run(id,tool,args){const r=spawnSync(process.execPath,['tools/'+tool+'.mjs',...args],{cwd:G,encoding:'utf8',maxBuffer:64*1024*1024});const file='evidence/query-'+id+'.txt';fs.writeFileSync(path.join(stage,file),(r.stdout||'')+(r.stderr||''));rows.push({id,tool,args,exitCode:r.status,evidence:file});if(r.error||r.status!==0)throw r.error||Error(id+' failed');}
assert('product final SHA matches owner-confirmed pin',graph.meta.sourceCommit===pin.sourceCommit);
assert('old UI observation remains historical',graph.uiRuntimeObservations.sourceCommit==='11eb461ae122f404acec94563c086b09e002987c'&&!graph.uiRuntimeObservations.evidenceAppliesToCurrentRevision);
assert('old refactor observation remains historical',graph.supplementalRuntimeObservations.sourceCommit==='902721ec505df42d74d0b2d245ed34a049faadad'&&!graph.supplementalRuntimeObservations.evidenceAppliesToCurrentRevision);
assert('release goal remains planning entry',graph.planning.entry==='P.release-goal');
assert('shared control planning remains',graph.nodes.some(n=>n.id==='R.common-implementation')&&graph.nodes.some(n=>n.id==='R.stop-action')&&graph.nodes.some(n=>n.id==='R.finish-goal')&&graph.nodes.some(n=>n.id==='R.request-plan'));
assert('shared control code anchors remain',graph.edges.filter(e=>e.kind==='685dde4 구현 입구').length===8);
assert('bag live-game unresolved remains',dev.nodes.some(n=>n.id==='U.bag-live'));
assert('tree checkpoint is not accepted',graph.bagImplementationEvidence.checkpoint===true&&graph.bagImplementationEvidence.accepted===false);
assert('tree Unity is not inherited',graph.bagImplementationEvidence.validation.unity.status.includes('승계금지'));
assert('tree implementation and game consumer execution differ',graph.bagImplementationEvidence.liveGameObserved===false&&graph.bagImplementationEvidence.gameConsumerExecuted===false);
assert('navigation capability remains unobserved',dev.nodes.some(n=>n.id==='F.bag-navigation'&&n.status==='구현됨·미확인'));
function reachable(from,to){const seen=new Set([from]),q=[from];for(const id of q){if(id===to)return true;for(const e of graph.edges)if(e.from===id&&e.type==='reference'&&!seen.has(e.to)){seen.add(e.to);q.push(e.to);}}return false;}
for(const id of ['P.bag-item-first','P.bag-navigation','P.bag-no-cut','R.bag-navigation','R.bag-cap-audit','R.bag-menu-examples','R.bag-connected-trace','R.bag-review','R.bag-source-export']){
 assert('release intent → '+id,reachable('P.release-goal',id));
 run(id.replaceAll('.','-')+'-intent','product',['intent',id]);run(id.replaceAll('.','-')+'-source','product',['source',id]);run(id.replaceAll('.','-')+'-show','product',['show',id]);
}
assert('navigation → actual state/dispatch/leaf code',graph.edges.filter(e=>e.from==='R.bag-navigation'&&e.kind==='트리 상태·전달·leaf 구현 위치(정적)').length===11);
assert('cap audit → actual assembler and world generators',graph.edges.filter(e=>e.from==='R.bag-cap-audit'&&e.kind==='후보 개수 상한 제거 구현 위치(정적)').length>=11);
for(const id of ['pol.bag-navigation','pol.choice-no-cut'])run(id.replaceAll('.','-'),'product',['source',id]);
run('navigation-why','query',['why','F.bag-navigation']);run('navigation-code','query',['code','F.bag-navigation']);
const samples=read(path.join(stage,'work/2026-11/2026-11-07-가방장비구현/evidence/tree-menu-adapter/payloads.json'));
assert('isolated current items example exceeds fourteen',samples.some(x=>x.sample==='more than fourteen exact items'&&x.choices.length>14));
assert('isolated destination example preserves thirty-six slots',samples.some(x=>x.sample==='all thirty-six destination slots'&&x.choices.length===38));
assert('isolated examples include distinct back and selection cancel',samples.some(x=>x.choices.some(c=>c.action==='back'))&&samples.some(x=>x.choices.some(c=>c.action==='cancel')));
assert('independent menu count is distinct from producer count',graph.bagImplementationEvidence.validation.independentReview.menu.passed===394&&graph.bagImplementationEvidence.validation.isolatedMenu.passed===348);
assert('own 741 Unity completion is bounded',graph.bagImplementationEvidence.validation.unity.sourceCommit===pin.sourceCommit&&graph.bagImplementationEvidence.validation.unity.exitCode===0);
assert('source package preserves raw and EOL boundaries',graph.bagImplementationEvidence.validation.sdkSourcePackage.sourceFiles===101&&graph.bagImplementationEvidence.validation.sdkSourcePackage.producerRawMatch===101&&graph.bagImplementationEvidence.validation.sdkSourcePackage.reviewRawMatch===92&&graph.bagImplementationEvidence.validation.sdkSourcePackage.reviewEolOnly===9&&graph.bagImplementationEvidence.validation.sdkSourcePackage.binaryRelease===false);
assert('connected source pin corresponds to current snapshot',graph.bagImplementationEvidence.connectedSourceCorrespondence.sourceCommit===pin.sourceCommit&&graph.bagImplementationEvidence.connectedSourceCorrespondence.allMatch===true);
const connected=read(path.join(stage,'work/2026-11/2026-11-07-가방장비구현/evidence/tree-connected/connected-trace.json'));
assert('connected local request count is distinct from runtime/model calls',connected.filter(x=>x.requestJson).length===19&&connected.filter(x=>x.navigation).length===17&&graph.bagImplementationEvidence.validation.connectedTrace.externalModels===0);
const canonicalPlan=fs.readFileSync(path.join(stage,'recorded-official-685/work/2026-11/2026-11-07-기획연결/source.mjs'),'utf8');
assert('recorded 685 official planning source retained verbatim before bag additions',fs.readFileSync(path.join(stage,'work/2026-11/2026-11-07-기획연결/source.mjs'),'utf8').startsWith(canonicalPlan));
const drift=read(path.join(stage,'official-drift.json'));
assert('new official life-tree drift is detected and direct apply blocked',drift.currentOfficialHead==='c4a0f5e38016c79567f880bbaea5e56e0eeae347'&&drift.canApplyDirectlyToCurrentOfficial===false&&drift.originalPlanningPrefixMatchesRecordedHash===true);
for(const mode of ['equip','hold','store','take','drop','give']){
 const id='R.bag-'+mode;
 assert('goal → '+mode,reachable('P.original-request',id));
 assert(mode+' → code',graph.edges.some(e=>e.from===id&&e.kind==='구현 앵커(정적, 실행 미확인)'&&e.to.startsWith('code:')));
 assert(mode+' → game rules',graph.edges.some(e=>e.from===id&&e.to==='R.bag-rules'));
 assert(mode+' → validation',graph.edges.some(e=>e.from===id&&e.to==='R.bag-validation'));
 assert(mode+' → development capability',dev.nodes.some(n=>n.id==='F.bag-'+mode&&n.status==='구현됨·미확인'));
 assert(mode+' → game consumer site',dev.edges.some(e=>e.from==='F.bag-'+mode&&e.to.startsWith('game:site:')));
 run(mode+'-intent','product',['intent',id]);run(mode+'-show','product',['show',id]);run(mode+'-source','product',['source',id]);
 run(mode+'-why','query',['why','F.bag-'+mode]);run(mode+'-code','query',['code','F.bag-'+mode]);
}
for(const id of ['P.bag-equipment','R.bag-rules','R.bag-validation','pol.bag-agency','pol.bag-boundary'])run(id.replaceAll('.','-'),'product',['source',id]);
run('overview','product',['overview']);run('legacy-entry','product',['legacy','code','F.bag-equip']);
run('release-goal-intent','product',['intent']);run('release-goal-source','product',['source','P.release-goal']);run('common-source','product',['source','R.common-implementation']);
fs.writeFileSync(path.join(stage,'query-results.json'),JSON.stringify({sourceCommit:pin.sourceCommit,assertions,commands:rows},null,2)+'\n');
console.log(assertions.length+' representative path assertions; '+rows.length+' queries passed.');
