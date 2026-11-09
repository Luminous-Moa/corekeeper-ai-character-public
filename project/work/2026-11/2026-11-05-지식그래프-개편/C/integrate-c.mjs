import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const C=path.dirname(fileURLToPath(import.meta.url)),root=path.dirname(C);
const read=p=>fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,'');
const write=(p,t)=>fs.writeFileSync(path.join(C,p),t,'utf8');
const json=(p,o)=>write(p,JSON.stringify(o,null,2)+'\n');
const A=JSON.parse(read('A/graph-A.json')),B=JSON.parse(read('B/code.graph.json'));
const legacy=JSON.parse(read('preserved/legacy/graph/graph.json'));
const hash=p=>crypto.createHash('sha256').update(read(p)).digest('hex');
const allIds=new Set([...A.nodes,...B.nodes].map(n=>n.id));
const docs=JSON.parse(read('C/c1-documents.json')).documents;
const nodes=[],edges=[];
const c1warn=/^> C-1 기존 기획 전환 초안\..*\n\n/m;
const integratedWarn='> 통합 기획: 기존 의도와 기준 코드 e82eb87, 격리 HTTP 관측을 구별한다. 게임 화면 확인은 사용자 지시로 후속 유예했다. 새 사용자 결정은 없다.\n\n';
for(const d of docs){
 if(d.type==='화면')continue;
 let text=read('C/'+d.doc).replace(c1warn,integratedWarn);
 text=text.replaceAll('화면요소 ID: **미확정**(A 수신 전).','화면요소 ID: **미확정**(게임 화면/정상 AI 출력은 이번에 관측하지 않음).');
 text=text.replaceAll('실제 이동 확인 전.','실제 게임 이동 검증은 사용자 유예.');
 const title=text.match(/^# (.+)$/m)?.[1]??d.id;
 nodes.push({id:d.id,type:d.type,title,class:d.type==='제품코어'?'사용자 결정 요약':'통합 해석',doc:'C/'+d.doc,source:{document:'C/'+d.doc,section:title},legacyIds:d.legacyIds??[],status:d.type==='유저 활동'?'게임/정상 AI 흐름 미관측; 문서화 완료':'기존 의도+A/B 부분 근거 통합',observationStatus:'게임 화면 deferred-by-user; HTTP만 별도 관측'});
 if(d.type==='유저 활동'){
  const next=text.match(/## 다음 활동\s+([\s\S]*?)(?=\n##|$)/)?.[1]??'';
  for(const id of new Set(next.match(/UA-\d+/g)??[]))edges.push({from:d.id,type:'유저 활동→유저 활동',to:id,class:'기획 흐름 해석',evidence:'C/'+d.doc+'#다음 활동',verification:'게임 navigate 미확인; 사용자 유예'});
 }
 write(d.doc,text);
}
// Preserved original paragraphs are authoritative for the policy strings; candidates remain outside standard nodes.
const ptext=read('C/기획/정책-기준.md');
const pols=legacy.nodes.filter(n=>n.type==='정책');
const exclude=new Set(['pol.single-ai-first','pol.body-npc','pol.no-self-workaround','pol.exp-no-repeat-misfit']);
const disposition=[];
for(const p of pols){
 const start=ptext.indexOf('### '+p.id+' — '), next=ptext.indexOf('\n### ',start+4),body=ptext.slice(start,next<0?undefined:next);
 const field=k=>body.match(new RegExp('^- '+k+': (.*)$','m'))?.[1]??null;
 const kind=field('종류'),rule=field('규칙'),eligible=['처리','원칙'].includes(kind)&&!exclude.has(p.id);
 disposition.push({legacyId:p.id,disposition:eligible?'표준 정책; 원문 규칙/확정도 유지':'정본 문서 보존; 역사·개발범위·전제·평가기준',reason:exclude.has(p.id)?'대체/역사 또는 개발 판단 기준':kind,source:'C/기획/정책-기준.md#'+p.id});
 const applicationNotes={
  'pol.combat-options':'원문의 Jev 담당명은 pol.selector-openai 후속 결정에 의해 OpenAI Decisions로 읽는다. 선택지 계속 갱신 의도와 원문 잠정 등급은 보존.',
  'pol.behavior-roles':'역할 구분은 유지. 선택 담당 Jev 구상은 pol.selector-openai의 OpenAI Decisions 결정으로 후속 대체.',
  'pol.reflex-while-late':'원문의 Jev는 후속 선택 담당 OpenAI Decisions. 응답 지연 동안만이라는 조건 유지. 내용은 pol.reflex-minimal-now 최소안 잠정 범위 참조.',
  'pol.reflex-minimal-now':'하던 유효 행동 계속/정지만인 현재 잠정 최소안. 전술 반사 허용 결정이나 실제 최종판 관측으로 확대하지 않음.',
  'pol.idle-stand':'평화 시 기본 정지. 위협 시에는 pol.self-defence의 후속 예외 적용.',
  'pol.cost-ceiling':'원문의 월 10만원 검토는 역사적 검토 상태 보존. 실행 한도 사용자 결정은 pol.cost-cap-200; cand.cost-cap-70은 HEAD 기본값 기록.',
  'pol.start-kit-role-only':'후속 pol.weapon-default가 무기 예외를 추가. 직업 기본만 문장을 무기 예외 없이 적용하지 않음.'
 };
 if(eligible)nodes.push({id:p.id,type:'정책',title:p.title,class:p.class??'결정',rule,summary:rule,kind,grade:field('등급'),certainty:field('확정도'),notes:[...body.matchAll(/^- 메모: (.*)$/gm)].map(m=>m[1]),applicationNote:applicationNotes[p.id]??null,doc:'C/기획/정책-기준.md',source:{document:'C/기획/정책-기준.md',section:p.id},legacyIds:[p.id],sources:p.sources,implementationStatus:'개별 연결 근거 외 구현 충족 미판정',appliesToCommit:['pol.combat-kill-brute','pol.combat-no-auto-dodge','pol.combat-composite'].includes(p.id)?'기준판 이후 의도; e82eb87 구현 주장 아님':'원문 적용 범위 유지'});
}
const pLinks=[
 ['pol.llm-subscription','ai-bridge:POST /v1/plan','B/snapshot/Tools/ai-bridge/plan.mjs:31','B/snapshot/Tools/ai-bridge/bridge.mjs:43','preflight.check 이후 subscriptionOk 거절 및 API 키 환경변수 제거/구독 adapter 호출(plan.mjs:75). 외부 CLI 내부는 B 경계.'],
 ['pol.recall-budget','ai-bridge:POST /v1/plan','B/snapshot/Tools/ai-bridge/plan.mjs:63','B/snapshot/Tools/ai-bridge/recall-loop.mjs:122','기본/설정 budget을 고리에 전달하고 검색 때 감소. 기본 2이며 설정이 바뀌면 정책 충족 재검토 필요.'],
 ['pol.recall-index-then-query','ai-bridge:POST /v1/plan','B/snapshot/Tools/ai-bridge/plan.mjs:47','B/snapshot/Tools/ai-bridge/plan.mjs:65','memoryIndex 활성+유효 context에서 목차를 reportTurn에 넣음. 모든 실패 요청에 목차가 있는 것은 아님.'],
 ['pol.recall-game-by-query','ai-bridge:POST /v1/plan','B/snapshot/Tools/ai-bridge/memgraph.mjs:576','B/snapshot/Tools/ai-bridge/plan.mjs:77','runQuery의 Q dispatch와 캐릭터 context를 사용하는 검색 경로. 실제 게임 지식 범위 검증은 별도.']
];
for(const [from,to,...evidence]of pLinks)if(nodes.some(n=>n.id===from)&&allIds.has(to))edges.push({from,type:'정책→기능',to,class:'코드 근거 해석',evidence,coverage:'해당 정책을 담당하는 처리 위치; 전체 규칙 충족을 보증하지 않음'});
json('policy-disposition.json',{policies:disposition,candidates:'C/기획/후보-보존.md; 표준 정책 자동 승격 없음',unknowns:'C/기획/미결-보존.md 및 legacy graph 별도 보존'});

// Match the legacy code anchors to B identities without creating any nonstandard graph edge.
const maps=JSON.parse(read('C/legacy-capability-map.json'));
for(const m of maps.mappings){
 const names=(m.oldCodeAnchors??'').split(',').map(s=>s.trim().replace(/\/\d+$/,'')).filter(Boolean);
 m.codeAnchorMappings=names.map(anchor=>({legacyAnchor:anchor,codeIds:B.nodes.filter(n=>n.type==='코드'&&n.signature&&(n.signature.split('(')[0]===anchor)).map(n=>n.id)}));
 const extra=m.legacyId==='F.memory-recall'?['Tools/ai-bridge/plan.mjs','Tools/ai-bridge/recall-loop.mjs','Tools/ai-bridge/memgraph.mjs']:[];
 m.additionalCodeEvidence=extra.map(file=>({file,codeIds:B.nodes.filter(n=>n.file===file&&['plan','runQueries','runQuery','recallLoop','memoryIndex'].includes(n.name)).map(n=>n.id)}));
 m.standardFunctionIds=[];
 // Entry associations are explicit review aids, not evidence of every internal traversal.
 const explicit={
  'F.ai-toggle':['slash:ai'],'F.goal-cmd':['slash:ai'],'F.multi-ai':['slash:ai','hook:AIPlayerSpikeMod.Update'],
  'F.dev-build':['cli:HeadlessSdk.BuildAndInstall','cli:HeadlessSdk.CompileCheck','cli:HeadlessSdk.ImportGameFiles','cli:HeadlessSdk.CreateMod'],
  'F.memory-recall':['ai-bridge:POST /v1/plan'],'F.llm-goal':['slash:ai','ai-bridge:POST /v1/plan'],
  'F.test-switch':['slash:ai'],'F.ai-char-load':['hook:AIPlayerSpikePatches.CharacterDataPostfix'],
  'F.leave':['slash:ai','hook:AIPlayerSpikePatches.UnloadWorldsPrefix','hook:AIPlayerSpikeMod.Shutdown']
 };
 m.standardFunctionIds=(explicit[m.legacyId]??[]).filter(id=>allIds.has(id));
 m.mappingStatus=m.standardFunctionIds.length?'근거 있는 입구 연관; 능력과 입구는 동일 단위 아님':'코드 앵커 대응/별도 설계 보존; 독립 요청 입구 자동 생성 안 함';
 m.mappingClass='C 통합 해석';
}
maps.meta={stage:'C-2',notStandardFunctions:true,notGraphEdges:true,sourceCommit:B.meta.sourceCommit,sourceGraphHash:hash('B/code.graph.json'),note:'codeAnchorMappings는 이름/서명 대응이며 call 추출 또는 현재 실게임 검증이 아니다.'};
json('legacy-capability-map.json',maps);
const reconciliation=[
 {sourceId:'CORE-A-01',canonicalId:'CORE-01',action:'supersede-partial-inference',reason:'기존 정체성 기준에 브리지 출력에서 확인한 부분 단서를 포함. A 추정 원본은 보존'},
 {sourceId:'JOURNEY-A-01',canonicalId:'JOURNEY-01',action:'supersede-partial-inference',reason:'연결 준비 부분 저니를 기존 참여/플레이/떠남 의도 저니의 준비 분기로 포함'},
 {sourceId:'IA-A-01',canonicalId:'IA-01',action:'supersede-partial-inference',reason:'A 실제 출력 5종의 IA 묶음을 그대로 채택하고 미관측 게임 화면은 별도 유예 목록'},
 {sourceId:'ACT-A-01',canonicalId:'ACT-A-01',action:'adopt-reference',reason:'유저 활동 그대로 채택. C-3 반복 관측과 C-4 한계는 C/기획/연결준비-통합.md에 추가; 노드/간선 중복 생성 없음'}
];
const meta={stage:'C-5',scope:'문서 통합 적재; 실게임 화면/C-3 사용자 유예',sourceCommit:B.meta.sourceCommit,inputs:{A:{path:'A/graph-A.json',sha256:hash('A/graph-A.json')},B:{path:'B/code.graph.json',sha256:hash('B/code.graph.json')},B7:{path:'B/inference.graph.json',sha256:hash('B/inference.graph.json')}},reconciliation,externalNodeReferences:[...new Set(edges.map(e=>e.to).filter(id=>!nodes.some(n=>n.id===id)))],deferred:['게임 화면 원문/그림/요소 및 게임 C-3: 사용자 지시','정상 plan/외부 CLI 호출·자발적 recall 미관측'],c3:'C/C3/report.md',c4:'C/C4-반영.md',B7Status:'수신·차이검토 완료; C/C2-통합판정.md 및 B7-correspondence.json. 코드 추정은 별도 보존하며 실측 승격 없음'};
const declaration={meta,nodes,edges};
write('기획/그래프-선언.md','# C 문서 적재 선언\n\n제품코어·저니·IA·활동의 원본은 각 doc 문서다. 정책 규칙은 보존된 기획 문서에서 가져온다. 아래 선언은 문서 ID와 원문 위치를 명시하며 새 기능/코드를 정의하지 않는다. 실제 출력은 A 문서를 참조하고 D가 method/path 근거로 기능→화면요소를 잇는다. C-1 예약 OUT/GAME 문서는 적재하지 않는다.\n\n```json\n'+JSON.stringify(declaration,null,2)+'\n```\n');
console.log(JSON.stringify({nodes:nodes.length,edges:edges.length,capabilities:maps.mappings.length,policies:nodes.filter(n=>n.type==='정책').length}));
