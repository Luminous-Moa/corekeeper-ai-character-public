import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {spawnSync} from 'node:child_process';import {fileURLToPath,pathToFileURL} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),G=path.join(stage,'work/지식그래프'),P='work/2026-11/2026-11-07-기획연결',L='work/2026-11/2026-11-05-지식그래프-실화면',T='work/2026-11/2026-11-07-가방장비구현',json=p=>JSON.parse(fs.readFileSync(path.join(stage,p),'utf8').replace(/^\uFEFF/,'')),hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const graph=json(L+'/D/graph.json'),dev=json('work/지식그래프/graph/graph.json'),pin=json('final-pin.json'),baseline=json('rebase-baseline.json'),manifest=json('canonical-before.json'),evidence=graph.bagImplementationEvidence,plan=json('evidence-plan.json');
const assertions=[],commands=[];
function assert(name,ok){assertions.push({name,ok});if(!ok)throw Error(name);}
function query(id,tool,args){const r=spawnSync(process.execPath,['tools/'+tool+'.mjs',...args],{cwd:G,encoding:'utf8',maxBuffer:64*1024*1024}),file='evidence/integrated-query-'+id+'.txt';fs.writeFileSync(path.join(stage,file),(r.stdout||'')+(r.stderr||''));commands.push({id,tool,args,exitCode:r.status,evidence:file});if(r.status!==0)throw Error(id+' query failed');}
function reachable(from,to){const seen=new Set([from]),q=[from];for(const id of q){if(id===to)return true;for(const e of graph.edges)if(e.from===id&&e.type==='reference'&&!seen.has(e.to)){seen.add(e.to);q.push(e.to);}}return false;}
assert('combined code source and evidence source match',graph.meta.sourceCommit===pin.sourceCommit&&evidence.sourceCommit===pin.sourceCommit&&pin.baseCommit===baseline.sourceCommit);
assert('latest official c4 source was the rebase input',baseline.sourceCommit==='c4a0f5e38016c79567f880bbaea5e56e0eeae347');
const originalText=fs.readFileSync(path.join(stage,'recorded-official-c4',P,'source.mjs'),'utf8'),currentText=fs.readFileSync(path.join(stage,P,'source.mjs'),'utf8');
assert('official c4 planning source retained byte for byte before bag additions',currentText.startsWith(originalText)&&crypto.createHash('sha256').update(originalText).digest('hex')===baseline.planningSha256);
for(const rel of [P+'/source.mjs',L+'/D/planning.mjs','work/지식그래프/기획/기능.md','work/지식그래프/기획/정책.md','work/지식그래프/README.md'])assert('official authoring still unchanged: '+rel,manifest.files.some(row=>row.path===rel&&hash(path.join(manifest.canonical,rel))===row.sha256));
const original=await import(pathToFileURL(path.join(stage,'recorded-official-c4',P,'source.mjs')).href);
assert('all original planning node meanings retained',original.nodes.every(n=>{const got=graph.nodes.find(x=>x.id===n.id);return got&&['title','summary','status','layer'].every(k=>got[k]===n[k]);}));
assert('all original planning relations retained',original.edges.every(e=>graph.edges.some(x=>x.from===e.from&&x.to===e.to&&x.kind===e.type)));
assert('release entry remains project final goal',graph.planning.entry==='P.release-goal');
assert('common control eight source anchors retained',graph.edges.filter(e=>e.kind==='685dde4 구현 입구').length===8);
assert('official life source anchors retained on actual combined code',graph.edges.filter(e=>e.kind==='c4a0f5e 생활 구현 입구').length===12);
for(const ancestor of [baseline.sourceCommit,'741532fcc57ffc549ac155a8261f5e25c966902f'])assert('combined Git ancestry includes '+ancestor,spawnSync('git',['-C',pin.repo,'merge-base','--is-ancestor',ancestor,pin.sourceCommit]).status===0);
assert('new evidence is not official or live acceptance',evidence.accepted===false&&evidence.gameConsumerExecuted===false&&evidence.liveGameObserved===false);
assert('old UI/refactor runtime observations remain historical',!graph.uiRuntimeObservations.evidenceAppliesToCurrentRevision&&!graph.supplementalRuntimeObservations.evidenceAppliesToCurrentRevision);
assert('bag live unresolved retained with menu capability',dev.nodes.some(n=>n.id==='U.bag-live')&&dev.nodes.some(n=>n.id==='F.bag-navigation'&&n.status==='구현됨·미확인'));
for(const id of ['P.release-goal','P.life-tree','P.life-tree-readonly','P.life-tree-direct','P.life-tree-count','P.bag-item-first','P.bag-navigation','P.bag-no-cut','R.bag-life-consumers','R.bag-connected-trace','R.bag-validation']){
 assert('release intent reaches '+id,reachable('P.release-goal',id));query(id.replaceAll('.','-')+'-intent','product',['intent',id]);query(id.replaceAll('.','-')+'-source','product',['source',id]);
}
assert('both life and bag policy paths reach consumer reconciliation',reachable('P.life-tree','R.bag-life-consumers')&&reachable('P.bag-navigation','R.bag-life-consumers')&&reachable('P.bag-no-cut','R.bag-life-consumers'));
assert('consumer reconciliation reaches actual same-commit code',graph.edges.filter(e=>e.from==='R.bag-life-consumers'&&e.kind==='동일 통합판의 소비자·권한·수량 구현 위치(정적)').length>=10);
for(const mode of ['equip','hold','store','take','drop','give']){
 const id='R.bag-'+mode;assert('bag capability path '+mode,reachable('P.bag-navigation',id)&&reachable(id,'R.bag-rules')&&reachable(id,'R.bag-validation'));
 assert('bag code identity '+mode,graph.edges.some(e=>e.from===id&&e.kind==='구현 앵커(정적, 실행 미확인)'&&e.to.startsWith('code:')));
 query(mode+'-source','product',['source',id]);query(mode+'-why','query',['why','F.bag-'+mode]);query(mode+'-code','query',['code','F.bag-'+mode]);
}
for(const symbol of ['AILife.LeaveNavigation','AILife.BlocksInventoryView','AIInventory.CandidatesFor','AIBrain.InventoryChoiceSurface']){const n=graph.nodes.find(n=>n.signature?.startsWith(symbol+'('));assert('actual integrated source '+symbol,!!n);query(symbol.replaceAll('.','-'),'product',['source',n.id]);}
for(const id of ['pol.bag-navigation','pol.choice-no-cut'])query(id.replaceAll('.','-'),'product',['source',id]);
query('life-code','query',['code','F.life-tree']);query('navigation-code','query',['code','F.bag-navigation']);query('navigation-why','query',['why','F.bag-navigation']);query('overview','product',['overview']);
const trace=json(T+'/evidence/integration/tree-connected/connected-trace.json');assert('actual local requests/nav agree with scoped connected evidence',trace.filter(x=>x.requestJson).length===plan.validation.connectedTrace.constructedRequests&&trace.filter(x=>x.navigation).length===plan.validation.connectedTrace.navigationChoices);
assert('connected source correspondence covers both consumers',evidence.connectedSourceCorrespondence.sourceCommit===pin.sourceCommit&&evidence.connectedSourceCorrespondence.allMatch&&evidence.connectedSourceCorrespondence.files.length===14);
assert('inventory remains unpaged with no fourteen-item cut',json(T+'/evidence/tree-menu-adapter/payloads.json').some(x=>x.sample==='more than fourteen exact items'&&x.choices.length>14));
if(evidence.validation.unity.exitCode===0)assert('new Unity result belongs to actual combined source',evidence.validation.unity.sourceCommit===pin.sourceCommit);
if(evidence.validation.independentReview.passed){assert('producer192 and independent199 are separate',evidence.validation.connectedTrace.passed===192&&evidence.validation.independentReview.connected===199);assert('current SDK has103 source files and no runtime install claim',evidence.validation.sdkSourcePackage.sourceFiles===103&&evidence.validation.sdkSourcePackage.binaryRelease===false&&evidence.validation.sdkSourcePackage.installed===false);for(const id of ['R.bag-review','R.bag-source-export','R.bag-head-source-acceptance']){assert('bounded acceptance path '+id,reachable('R.bag-validation',id));query(id.replaceAll('.','-'),'product',['source',id]);}}
fs.writeFileSync(path.join(stage,'query-results.json'),JSON.stringify({sourceCommit:pin.sourceCommit,baseline:baseline.sourceCommit,assertions,commands},null,2)+'\n');console.log(assertions.length+' integrated provenance/path assertions; '+commands.length+' queries passed.');
