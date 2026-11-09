#!/usr/bin/env node
// Reuse the baseline integration in memory. Never rewrite its artifacts.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {build as baselineBuild} from '../../2026-11-05-지식그래프-개편/D/build.mjs';
import {abs,live,read,hash,bytes,write,cmp,validate,indexes} from './live-lib.mjs';
import {screenLayer,location} from './screens.mjs';
import {addPlanning} from './planning.mjs';

export function integrate(){
 const baseline=baselineBuild(),g=structuredClone(baseline.g),a=screenLayer(),c=read(live('C/overlay-source.json'));
 const mapping=read(live('B/existing-node-mapping.json'));
 if(c.schemaVersion!==1||c.baseCommit!==g.meta.sourceCommit||mapping.baseCommit!==g.meta.sourceCommit)throw Error('overlay/baseline schema or commit mismatch');
 g.nodes.push(...a.nodes);
 for(const original of c.nodes){const n=structuredClone(original);n.layer='live-C';n.doc=live(n.doc);n.source={...n.source,document:live(n.source.document)};n.sourceLocations=[location(original.source.document,'# '+original.source.section)];g.nodes.push(n);}
 g.edges.push(...a.edges,...c.edges.map(e=>{const n={...e,from:e.source,to:e.target,evidence:live(e.evidence)};delete n.source;delete n.target;if(n.evidenceSources)n.evidenceSources=n.evidenceSources.map(live);return n;}));
 const planningSources=addPlanning(g);
 const ix=indexes(g);
 // A separately scoped final-code run supplements historical screens; it does not rewrite their evidence or infer function traces.
 let refactorObservations=null;
 if(c.refactorObservationSource){
  refactorObservations=read(live(c.refactorObservationSource));
  if(refactorObservations.schemaVersion!==1||(refactorObservations.sourceCommit!==g.meta.sourceCommit&&refactorObservations.sourceCommit!==c.historicalRefactorObservationCommit))throw Error('refactor observation code baseline mismatch');
  for(const input of refactorObservations.inputs)if(hash(live(input.stagedPath))!==input.sha256)throw Error('refactor evidence snapshot hash mismatch: '+input.stagedPath);
  const dev=read('../../지식그래프/graph/graph.json'),devIds=new Set(dev.nodes.map(n=>n.id));
  for(const observation of refactorObservations.observations){
   for(const id of observation.developmentCapabilities)if(!devIds.has(id))throw Error('missing development capability '+id);
   for(const id of observation.relatedProductFeatures){const n=ix.nodes.get(id);if(n?.type!=='기능')throw Error('missing supplemental feature '+id);(n.supplementalObservationIds??=[]).push(observation.id);}
  }
  g.supplementalRuntimeObservations={...refactorObservations,sourceDocument:live(c.refactorObservationSource),currentSourceCommit:g.meta.sourceCommit,sameRevision:refactorObservations.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:refactorObservations.sourceCommit===g.meta.sourceCommit};
 }
 for(const annotation of c.existingNodeAnnotations){const n=ix.nodes.get(annotation.id);if(!n)throw Error('missing existing annotation ID '+annotation.id);n.liveScreenSupplement={...annotation,supplementDocument:live(annotation.supplementDocument)};}
 for(const annotation of c.screenAnnotations){const n=ix.nodes.get(annotation.id);if(n?.type!=='화면')throw Error('missing screen '+annotation.id);n.iaGroup=annotation.iaGroup;}
 for(const target of c.reconciliation)if(!ix.nodes.has(target.canonicalId))throw Error('missing reconciliation target '+target.canonicalId);
 for(const n of mapping.nodes)if(!ix.nodes.has(n.id))throw Error('version correspondence ID missing: '+n.id);
 let uiObservations=null;
 if(c.uiObservationSource){
  uiObservations=read(live(c.uiObservationSource));
  if(uiObservations.schemaVersion!==1||(uiObservations.sourceCommit!==g.meta.sourceCommit&&uiObservations.sourceCommit!==c.historicalUiObservationCommit))throw Error('UI observation code baseline mismatch');
  for(const input of uiObservations.inputs)if(hash(live(input.stagedPath))!==input.sha256)throw Error('UI evidence snapshot hash mismatch: '+input.stagedPath);
  const dev=read('../../지식그래프/graph/graph.json'),devIds=new Set(dev.nodes.map(n=>n.id));
  for(const observation of uiObservations.observations){
   for(const id of observation.developmentCapabilities)if(!devIds.has(id))throw Error('missing UI development capability '+id);
   for(const id of observation.relatedProductFeatures){const n=ix.nodes.get(id);if(n?.type!=='기능')throw Error('missing UI feature '+id);(n.uiObservationIds??=[]).push(observation.id);}
  }
  g.uiRuntimeObservations={...uiObservations,sourceDocument:live(c.uiObservationSource),currentSourceCommit:g.meta.sourceCommit,sameRevision:uiObservations.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:uiObservations.sourceCommit===g.meta.sourceCommit};
 }
 const observed=read(live('A1/observations.json'));
 const submission=observed.find(o=>o.name==='04-ai-help');
 if(!submission?.action.startsWith('Submit /ai;'))throw Error('A1 submitted command evidence missing');
 if(!observed.some(o=>o.name==='07-target-cancelled'&&o.action.includes('no command execution')))throw Error('cancelled draft evidence missing');
 const matches=[{observation:'A1/04-ai-help',command:'/ai',featureId:'slash:ai',inputId:'CKAI-02#3',outputId:'CKAI-02#2-7',basis:'submitted command and state-list source correspondence',evidence:[live('A1/observations.json'),live('A1/04-ai-help.png'),live('B/existing-node-mapping.json')],runtimeTrace:false},{observation:'C3/04-ai-response',command:'/ai',featureId:'slash:ai',inputId:'CKAI-02#3',outputId:'CKAI-02#2-7',basis:'independent user flow repeat; state-list visual confirmation',evidence:[live('C3/flow-check.md'),live('C3/04-ai-response.png')],runtimeTrace:false}];
 for(const m of matches)for(const id of [m.featureId,m.inputId,m.outputId])if(!ix.nodes.has(id))throw Error('request match target missing '+id);
 const unmatched=a.nodes.filter(n=>n.type==='화면요소'&&!['CKAI-02#3','CKAI-02#2-7'].includes(n.id)).map(n=>({id:n.id,reason:n.id==='CKAI-02#2-6'?'게임 채팅 에코; 모드 Reply 출력으로 매칭하지 않음':n.id.startsWith('CKAI-02#2')?'전투/기존 로그: 관측만 있음; 변경 실행판의 특정 실행 함수로 매칭하지 않음':'게임 본체 표시·메뉴·플레이어 관리·HUD/이름표; 모드 추출 범위에 renderer/조작 입구 없음'}));
 for(const row of unmatched)ix.nodes.get(row.id).mappingBoundary=row.reason;
 const errors=validate(g);if(errors.length)throw Error(errors.join('\n'));
 const allowed=new Set(['제품코어','유저저니','IA','유저 활동','화면','화면요소','정책','기능','코드','데이터','외부 연결','DB','계측 이벤트','기획 항목','기획 참고']);
 for(const n of g.nodes)if(!allowed.has(n.type))throw Error('nonstandard node type '+n.type);
 for(const e of g.edges){const natural=ix.nodes.get(e.from).type+'→'+ix.nodes.get(e.to).type;if(!['포함','reference','read','write',natural].includes(e.type))throw Error('nonstandard edge type '+e.type);}
 const sources=new Set(g.meta.sources.map(s=>s.path));
 const register=p=>{if(!fs.existsSync(abs(p)))throw Error('missing source '+p);sources.add(p);};
 const walk=p=>{for(const ent of fs.readdirSync(abs(p),{withFileTypes:true})){const child=p+'/'+ent.name;if(ent.isDirectory())walk(child);else if(/\.(md|json|png|html|mjs|py|ps1)$/.test(child))register(child);}};
 for(const folder of ['A1','A2','A3','A4','B','C','C3'])walk(live(folder));
 for(const p of ['agreement.md','D/integrate.mjs','D/screens.mjs','D/live-lib.mjs','D/query.mjs','D/verify.mjs','D/questions.mjs','D/check-entrypoints.mjs','D/preexisting-hashes.json'])register(live(p));
 for(const p of ['README.md','제품그래프-사용법.md','tools/product.mjs','tools/product-build.mjs'])register('../../지식그래프/'+p);
 for(const p of c.evidenceReferences)register(live(p));
 for(const p of [live('D/planning.mjs'),...planningSources])register(p);
 g.meta={...g.meta,stage:'D-live-follow-up',baselineIntegration:'2026-11-05-지식그래프-개편/D',installedSourceCommit:c.installedSourceCommit,liveInputFiles:[live('A2/manifest.json'),live('C/overlay-source.json'),live('B/existing-node-mapping.json')],sources:[...sources].sort(cmp).map(p=>({path:p,sha256:hash(p)}))};
 g.liveScreen={reconciliation:c.reconciliation,boundaries:c.boundaries,unresolved:c.unresolved,c3:{...c.c3,input:live(c.c3.input),report:live(c.c3.report),reflection:live(c.c3.reflection)},scope:'5 observed screens / 57 elements / A2 14 image samples; not exhaustive game states'};
 g.limitations=g.limitations.filter(x=>x!=='실게임 화면 및 컴퓨터 유즈 검증은 사용자 요청으로 유예');
 g.limitations.push('이번 후속에서 5 화면·57 요소·14 A2 증거와 독립 C3 /ai 조회를 관측. 전체 게임 상태·목표/가방/기억 명령·저장/재입장·외부 모델 성공은 미관측',...c.boundaries);
 g.nodes.sort((a,b)=>cmp(a.id,b.id));g.edges=[...new Map(g.edges.map(e=>[bytes(e),e])).values()].sort((a,b)=>cmp(bytes(a),bytes(b)));
 const used=new Set(g.edges.filter(e=>e.type==='유저 활동→화면요소').map(e=>e.to));
 const screens=g.nodes.filter(n=>n.type==='화면');
 const validation={baseline:baseline.validation,D1:{matches,unmatched,unexecuted:[{command:'/ai Claude',evidence:live('A1/observations.json'),reason:'draft cancelled; not a request'},{commands:['/ai inv','/ai mem','/ai goal'],reason:'code-only branches, not executed'}],structuralErrors:errors,reconciliation:c.reconciliation},D2:{observedFeatures:['slash:ai'],observedInput:'CKAI-02#3',staticEntry:'code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.Chat.cs#AIPlayerSpikeMod.TryHandleChatCommand(string)',frontBoundary:'게임 ChatSendMessage를 모드 hook으로 가로챔. 게임 프론트 코드와 HTTP/IPC/실행 trace 없음.',unobservedFeatures:g.nodes.filter(n=>n.type==='기능'&&n.id!=='slash:ai'&&!baseline.validation.D2.observedFeatures.includes(n.id)).map(n=>n.id),verdict:'A1와 C3 입력/출력 및 B 동일 소스 구간 대조. 런타임 함수 coverage 또는 죽은 코드 판정 아님.'},D4:{elementsWithFeatureButNoActivity:g.nodes.filter(n=>n.type==='화면요소'&&!used.has(n.id)&&g.edges.some(e=>e.from===n.id&&e.type==='화면요소→기능')).map(n=>n.id),screensWithoutActivity:screens.filter(n=>!g.edges.some(e=>e.from===n.id&&e.type==='화면→화면요소'&&used.has(e.to))).map(n=>n.id),corePolicyReview:'기존 정책·의도 유지; A2 표시규칙 후보 6개 지위 유지. C/통합기획.md',screenPolicy:'A2 manifest 표시규칙을 동일 문자열로 적재; 문서 화면 정책 절을 source로 연결',navigation:'실제 Return/Escape 및 A3 플레이어 관리 진입 관측. 게임 navigate 코드 외부 경계; 동선 부재로 판정하지 않음',journey:'C3 흐름 성공/부분 이해를 C4 반영. 새 활동 다음 활동 ID 없음; 기존 저니 필수 단계로 추가하지 않음'},D5:{correctionRule:'원본/파서 수정 후 재생성. JSON 직접 수정 금지',remaining:c.unresolved,notObserved:['전체 게임 상태','목표/가방/기억 명령','캐릭터 저장·재입장','실제 함수 trace','전체 전투판 코드 추출']}};
 if(refactorObservations){
  g.limitations.push('위 liveScreen/D1-D5 미관측 문구는 역사적76563bf 화면 관측 범위다. 902721e 추가 관측은 supplementalRuntimeObservations의 능력별 상태와 한계를 따르며 과거 기록을 승격하지 않는다.');
  validation.supplementalRuntime={sourceCommit:refactorObservations.sourceCommit,currentSourceCommit:g.meta.sourceCommit,sameRevision:refactorObservations.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:refactorObservations.sourceCommit===g.meta.sourceCommit,status:refactorObservations.status,document:live(c.refactorObservationSource),observations:refactorObservations.observations.map(o=>({id:o.id,status:o.status,capability:o.capability,developmentCapabilities:o.developmentCapabilities,boundary:o.boundary})),functionTraceObserved:false};
 }
 if(uiObservations){
  g.limitations.push('11eb461 UI 실게임 관측은 uiRuntimeObservations의 범위에 한한다. 역사적 D1-D5 미관측 문구와 별도로 보존하며 함수 실행 trace를 주장하지 않는다.');
  validation.uiRuntime={sourceCommit:uiObservations.sourceCommit,currentSourceCommit:g.meta.sourceCommit,sameRevision:uiObservations.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:uiObservations.sourceCommit===g.meta.sourceCommit,status:uiObservations.status,document:live(c.uiObservationSource),observations:uiObservations.observations,limitations:uiObservations.limitations,functionTraceObserved:false};
 }
 if(g.bagImplementationEvidence)validation.bagImplementation=g.bagImplementationEvidence;
 return {g,validation};
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 try{const result=integrate();if(process.argv.includes('--check')){for(const [file,obj] of [['graph.json',result.g],['validation.json',result.validation]])if(fs.readFileSync(abs(live('D/'+file)),'utf8')!==bytes(obj))throw Error('regeneration differs: '+file);console.log('regeneration byte-identical');}else{write(live('D/graph.json'),result.g);write(live('D/validation.json'),result.validation);console.log('built '+result.g.nodes.length+' nodes / '+result.g.edges.length+' edges');}}catch(e){console.error(e.stack);process.exitCode=1;}
}
