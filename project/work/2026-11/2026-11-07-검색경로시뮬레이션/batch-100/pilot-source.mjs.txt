import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import { performance } from 'node:perf_hooks';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { Adapters } from 'file:///C:/Dev/ai-chatroom/lib/agents/adapters.mjs';
import { CLI } from 'file:///C:/Dev/ai-chatroom/lib/agents/cli/index.mjs';
import { run } from 'file:///C:/Dev/ai-chatroom/lib/agents/run.mjs';
import { createPreflight } from 'file:///C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/preflight.mjs';
import * as M from 'file:///C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs';
import { firstJson } from 'file:///C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/recall-loop.mjs';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const arg = (name, fallback) => { const i = process.argv.indexOf('--' + name); return i < 0 ? fallback : process.argv[i + 1]; };
const mode = arg('mode', 'fixture');
const label = arg('label', 'r0');
const OUT = path.join(ROOT, 'runs', label);
const write = (f, obj) => { fs.mkdirSync(path.dirname(f), {recursive:true}); fs.writeFileSync(f, typeof obj === 'string' ? obj : JSON.stringify(obj,null,2)); };
const read = (f, fallback = {}) => { try { return JSON.parse(fs.readFileSync(f,'utf8').replace(/^\uFEFF/,'')); } catch { return fallback; } };
const ms = t => +(performance.now()-t).toFixed(3);
const DATA = path.join(ROOT, 'fixtures');
const game = M.loadGame(M.DEFAULT_KG);
if (!game) throw Error('Existing game name index unavailable');
const cond = (level) => ({hp:100, hand:'WoodMiningPick', tool:'WoodMiningPick', tlv:level, wpn:'WoodSword', wlv:1, alv:0, area:'Slime', cell:'0,0', core:0, mates:0, hunger:80, seen:100, st:1, ore:1});

// All world facts below are synthetic test premises. Tool level thresholds are NOT claims about Core Keeper recipes.
const SCENES = [
  {id:'s1', name:'위치와 경험 조회', level:2, requirement:2, ore:[12,4], past:'success', expected:'move_resource', age:60},
  {id:'s2', name:'준비 계획 변경', level:1, requirement:2, ore:[12,4], past:'tool_weak', expected:'move_bench', age:60},
  {id:'s3', name:'오래된 위치와 새 고갈 관측', level:2, requirement:2, ore:[12,4], past:'depleted', expected:'explore', age:1200},
];

function fixture(s) {
  const world = crypto.createHash('md5').update('retrieval-sim-'+s.id).digest('hex');
  const dir = path.join(DATA,world,'gpt');
  const base = {sess:'fixture', world, char:'gpt', src:'game'};
  const event = (id,kind,play,d={},more={}) => ({...base,id,kind,play,t:1794000000+play,run:null,ents:[],d,...more});
  const end = s.past === 'success' ? {outcome:'done',cls:'',cause:'이 광맥에서 현재와 같은 등급 2 도구로 구리 광석 4개를 확보함',got:4} : s.past === 'tool_weak' ? {outcome:'blocked',cls:'tool_weak',cause:'광맥(12,4)의 채굴에는 도구 등급 2가 필요함을 실제 관측. 등급 1로는 캘 수 없음.',got:0} : {outcome:'blocked',cls:'missing',cause:'타일(12,4)을 새로 확인: 구리 광맥은 완전히 고갈되어 없음. 다른 알려진 광맥도 없음.',got:0};
  const ev = [event(1,'session_start',0),event(2,'goal_start',200,{key:'gather:CopperOre',count:4,source:'host',c:cond(s.level)}, {run:'fixture.2',ents:['item:CopperOre','cell:0,0','area:Slime']}),event(3,'goal_end',s.past==='depleted'?1190:1100,{run:'fixture.2',key:'gather:CopperOre',item:'CopperOre',count:4,...end,secs:30,downs:0,down_by:[],area:'Slime',source:'host',c0:cond(s.level),c1:cond(s.level),play0:200,play1:s.past==='depleted'?1190:1100},{run:'fixture.2',ents:['item:CopperOre','cell:0,0','area:Slime']})];
  write(path.join(dir,'events.jsonl'),ev.map(x=>JSON.stringify(x)).join('\n')+'\n');
  write(path.join(dir,'exp_state.json'),{v:1,next:4,play:1200});
  write(path.join(dir,'known.json'),{version:1,seenId:[1500,4004],heldId:[1500],seenAt:[],heldAt:[],tiles:[],tilesAt:[],dropSrc:[],dropItem:[]});
  write(path.join(dir,'memory.json'),{baseSet:1,baseXY:[0,0],oreXY:s.ore,oreId:[1500],stationXY:[2,0],stationId:[4004]});
  const obs = {request:'구리 광석 4개를 확보해 줘.', now:{tile:[0,0],health:100,hunger:80,tool_level:s.level,bag:{CopperOre:0,upgrade_material:1},danger:false}, visible:{resource_sites:[],bench:{id:'bench1',tile:[2,0],ability:'시험 전제: 재료 1개로 도구 등급 2를 준비할 수 있음'},frontier:{id:'frontier1',tile:[4,0],note:'아직 확인하지 않은 구역. 광석 유무 모름.'}}, memory_catalog:{resource_sites:'기억한 자원 위치·관측 시각',resource_history:'자원 확보 시도의 성공/실패·준비 조건·새 관측'}, working_rules:['이 장면은 합성 시험이며 게임 수치나 제작법을 외부 지식으로 추정하지 않는다.','사용자 목표를 유지하며 필요한 준비·이동·탐색 순서를 정한다.','기억한 위치는 현재 상태 보장이 아니다. 현재 관측과 최신 경험을 함께 판단한다.']};
  const ctx = M.context({root:DATA,world,member:'gpt',game,tile:[0,0]});
  return {...s,world,dir,obs,ctx};
}
const scenes = SCENES.map(fixture);
write(path.join(DATA,'scene-manifest.json'),scenes.map(({ctx,...s})=>s));
write(path.join(ROOT,'fixture-notes.md'),'# Fixture와 조회 경계\n\n세 장면 모두 새 부탁은 구리 광석 4개 확보이다. 위치·조건·이력은 합성 장면이며 현재 게임 월드의 사실이 아니다. 도구 등급 2 필요 및 등급 2 준비에 재료 1개가 든다는 설정은 시험 전제다. 장면 3은 memory.json의 옛 좌표와 이후 고갈 관측이 상충하는 경우다.\n\n기존 memgraph.mjs의 context/runQueries를 그대로 사용한다. resource_history는 goal 조회와 같은 events.jsonl 원본 사건의 관측 cause·당시 조건을 함께 반환한다. 기존 goal 요약은 상세 cause를 생략하므로 이 보충 없이 정확한 필요 도구 등급과 고갈 관측을 평가할 수 없다. resource_sites는 near 조회에 같은 memory.json의 정확한 oreXY/oreId 좌표와 fixture 관측시각을 보충한다. 기존 near는 광석 개수만 반환하므로 이 두 보충은 시험용 API 제안이며 현행 제품 API의 성능이라고 하지 않는다. 두 경로 모두 같은 조회 도구를 사용한다.\n\n모델에 주는 초기 목차는 동일한 두 조회 항목의 설명이다. 원래 memoryIndex가 아니라 새 API 비교를 위한 최소 목차다. 세 장면 기대 답과 이름은 판정 파일에만 있고 모델 입력에는 없다. 원래 제품 brief 대신 역할·출력 형식만 적은 시험 프롬프트를 사용하므로 실제 제품의 전체 입력량과 동일한 지연 실험은 아니다.\n');

const queryDefs = [
  {id:'resource_sites',description:'구리 광석의 기억한 위치·관측 시각을 조회한다. 현재도 남아 있다는 보장은 없다.'},
  {id:'resource_history',description:'구리 광석 확보의 과거 결과·준비 조건·최근 확인 내용을 조회한다.'},
];
function search(s, q, state) {
  const t = performance.now();
  let queries;
  if(q==='resource_sites') queries=[{q:'near',place:'here',radius:32}];
  else if(q==='resource_history') queries=[{q:'goal',key:'gather:CopperOre'}];
  else return {id:q,error:'unknown query',ms:ms(t)};
  const raw=M.runQueries(s.ctx,queries);
  const evidence={id:`${s.id}:${q}`,text:raw.text,engine_queries:queries};
  if(q==='resource_sites') evidence.locations=[{id:'ore1',item:'CopperOre',tile:s.ore,observed_seconds_ago:s.age,certainty:'last_observed_not_current'}];
  if(q==='resource_history') evidence.observations=fs.readFileSync(path.join(s.dir,'events.jsonl'),'utf8').trim().split('\n').map(JSON.parse).filter(e=>e.kind==='goal_end'&&e.d.item==='CopperOre').map(e=>({id:'e:'+e.id,play:e.play,source:e.src,observed_condition:e.d.c1,cause:e.d.cause,outcome:e.d.outcome}));
  const result={id:q,evidence,ms:ms(t)};
  state.retrieved[q]=evidence;
  state.events.push({type:'search',...result});
  return result;
}

if(mode==='fixture') {
  for(const s of scenes) {const state={retrieved:{},events:[]};for(const q of queryDefs)search(s,q.id,state);write(path.join(DATA,s.id+'-query-check.json'),state);}
  console.log(JSON.stringify({fixture:true,scenes:scenes.length,output:DATA}));
  process.exit(0);
}

// Read the same credential source as the mod, without logging or writing the value.
let decisionKey=process.env.OPENAI_API_KEY;
if(!decisionKey) decisionKey=execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-Command','[Environment]::GetEnvironmentVariable("OPENAI_API_KEY", "User")'],{encoding:'utf8',windowsHide:true}).trim();
for(const k of ['ANTHROPIC_API_KEY','OPENAI_API_KEY','GEMINI_API_KEY','GOOGLE_API_KEY','XAI_API_KEY'])delete process.env[k];
if(!decisionKey)throw Error('Decisions credential unavailable (value not logged)');
const productionConfig=read('C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/config.json');
const upper=productionConfig.agents?.gpt ?? {model:'gpt-6.1-sol',effort:'low'};
if(productionConfig.seats?.gpt)throw Error('Unexpected GPT seat override: inspect before running');
const decisionSettings=read(path.join(os.homedir(),'AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers/decisions-settings.json'));
if(!decisionSettings.model)throw Error('Cannot read existing Decisions settings');
const usage=read(path.join(os.homedir(),'AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers/decisions-usage.json'));
if(usage.month===new Date().toISOString().slice(0,7) && usage.usd>=decisionSettings.monthlyCapUsd)throw Error('Existing Decisions monthly cap reached');
const adapters=new Adapters(OUT,{agents:{gpt:upper},turnTimeoutSec:90,bins:productionConfig.bins||{},webSearch:false});
const preflight=createPreflight({MEMBERS:{gpt:{cli:'codex',name:'ChatGPT'}},adapters,CLI,run,HERE:OUT,QUOTA_SKIP_PCT:85,busy:new Set(),FAKE_CHAT:null});
const prepT=performance.now();
const health=await preflight.check('gpt');
const preflightMs=ms(prepT);
write(path.join(OUT,'preflight.json'),{at:new Date().toISOString(),model:upper,decisionModel:decisionSettings.model,found:health.found,login:health.login,subscriptionOk:health.subscriptionOk,skip:health.skip,maxPct:health.maxPct,skipWhy:health.skipWhy,preflightMs,decisionCredentialPresent:true});
if(!health.ok)throw Error(`Upper CLI unavailable: ${health.skipWhy||'login/subscription failure'}`);
console.log(JSON.stringify({preflight:true,model:upper,decisionModel:decisionSettings.model,maxPct:health.maxPct,ms:preflightMs}));

const upperBase=`게임 동료의 상위 계획자다. 제공된 관찰·기억 결과와 사용자 부탁에 근거해 목적과 작업을 정한다. 이 장면에서 외부 게임 지식은 쓰지 않는다. 모르는 사실은 추측하지 않는다. 필요한 준비 순서를 계획한다. 검색하지 않아도 충분하면 계획할 수 있다. JSON 하나만 답한다. 가능한 답: {"decision":"recall","queries":["resource_sites" 또는 "resource_history"],"why":"이유"} 또는 {"decision":"plan","task":"gather"|"prepare_tool"|"explore"|"wait","target_id":"알려진 대상 ID 또는 null","why":"이유","evidence_ids":["조회 결과 ID"]}. gather는 구리 확보, prepare_tool은 채굴을 위한 도구 준비, explore는 새 광맥 탐색이다. 수량은 4개다. 목표나 준비 순서의 중요한 변경은 네 책임이다. 좌표를 만들지 않는다.`;
async function upperCall(s,route,state,reason='initial') {
  if(state.upperCalls>=3)throw Error('upper call budget hit');
  const brief=upperBase+(route==='B'&&reason==='initial'?'\n이번 경로에서는 현장 자료 조회와 대상 선택을 Decisions가 할 수 있다. 현재 정보로 작업을 정해 넘기고, 구체 장소와 작업 내 대상 선택은 Decisions에 맡긴다. 필요한 자료가 없어서 계획 자체를 정할 수 없다면 recall로 요청할 수 있다.':'\n이번 경로에서는 네가 작업 계획에 필요한 기억 조회를 담당한다.');
  const turn=JSON.stringify({observation:s.obs,query_tools:queryDefs,retrieved:state.retrieved,current_plan:state.plan||null,reason});
  const t=performance.now();const r=await adapters.chat('gpt',brief,turn);const elapsed=ms(t);
  state.upperCalls++;
  const answer=r.ok?firstJson(r.text):null;
  state.events.push({type:'upper',reason,ms:elapsed,cli_ms:r.ms,model:r.model,effort:r.effort,prompt:{brief,turn:JSON.parse(turn)},answer,raw:r.text,ok:r.ok,detail:r.detail,usage:r.usage});
  if(!r.ok||!answer)throw Error('upper response failed: '+r.detail);
  if(answer.decision==='recall') {
    for(const q of (answer.queries||[]).slice(0,2))search(s,q,state);
    return upperCall(s,route,state,'requested search results');
  }
  if(answer.decision!=='plan'||!['gather','prepare_tool','explore','wait'].includes(answer.task))throw Error('invalid plan');
  state.plan=answer;
}
function choices(s,route,state) {
  const arr=[];
  if(route==='B') for(const q of queryDefs)if(!state.retrieved[q.id])arr.push({value:'query:'+q.id,description:q.description});
  for(const loc of state.retrieved.resource_sites?.locations||[])arr.push({value:'move_resource',description:`記憶한 구리 광맥 ${loc.id} 타일 ${loc.tile}를 향해 이동을 시작. 오래된 위치일 수 있음. 현재 준비 조건을 고려한다.`});
  arr.push({value:'move_bench',description:'보이는 제작대 bench1 (2,0)로 이동해 도구 등급 2 준비 작업을 시작한다. 재료 1개가 있으며 현재 작업 계획과 맞아야 한다.'});
  arr.push({value:'explore',description:'보이는 미탐색 경계 frontier1 (4,0)로 이동해 새 광맥을 찾기 시작한다. 알려진 광맥으로 이동하는 것은 아니며 발견을 보장하지 않는다.'});
  arr.push({value:'escalate',description:'알게 된 사실 때문에 준비 순서·목적 등 작업 계획의 중요한 변경이 필요하여 위쪽 LLM에 판단을 요청한다.'});
  arr.push({value:'wait',description:'지금 유효한 행동을 정할 수 없어 기다린다.'});
  return arr;
}
async function choose(s,route,state) {
  if(state.decisionCalls>=5)throw Error('decision call budget hit');
  const opts=choices(s,route,state);
  const payload={model:decisionSettings.model||'gpt-6-luna',input:JSON.stringify({observation:s.obs,plan:state.plan,retrieved:state.retrieved}),questions:[{type:'choice',name:'action',instructions:'현재 작업과 관찰·조회 근거를 보고 다음 행동 또는 정보 조회 하나를 고른다. 필요한 정보가 부족하면 조회할 수 있다. 도구 준비 등 작업 계획의 중요한 변경은 상위 판단에 요청한다. 기억과 최신 확인이 충돌하면 최신 확인을 고려한다. 불필요한 대기는 피하되 없는 사실을 추측하지 않는다.',choices:opts}]};
  const t=performance.now();
  const response=await fetch('https://api.openai.com/v1/decisions',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+decisionKey},body:JSON.stringify(payload),signal:AbortSignal.timeout(15000)});
  const result=await response.json();const elapsed=ms(t);state.decisionCalls++;
  state.events.push({type:'decisions',ms:elapsed,prompt:payload,status:response.status,response:result});
  if(!response.ok)throw Error('Decisions HTTP '+response.status+': '+String(result.error?.message||'unknown').slice(0,200));
  const a=result.answers?.[0];
  if(a?.type!=='choice'||!opts.some(o=>o.value===a.choice))throw Error('invalid Decisions answer');
  return a.choice;
}
function accept(s,state,action) {
  const history=!!state.retrieved.resource_history;
  const preparationEvidence=history||(s.past==='success'&&!!state.retrieved.resource_sites);
  let valid=false, reason='';
  if(action==='move_resource') {valid=!!state.retrieved.resource_sites&&preparationEvidence&&s.past!=='depleted'&&s.level>=s.requirement&&state.plan.task==='gather';reason=!preparationEvidence?'준비 조건·새 확인이 담긴 경험을 확인하지 않음':s.past==='depleted'?'새 관측으로 고갈된 대상':s.level<s.requirement?'필요 도구 미준비':state.plan.task!=='gather'?'작업 계획과 실행 불일치':'기억한 대상·경험·현재 도구 조건에 부합';}
  else if(action==='move_bench') {valid=history&&s.level<s.requirement&&state.plan.task==='prepare_tool';reason=valid?'경험의 도구 부족에 따라 상위 계획을 준비 작업으로 정함':'불필요한 준비 또는 상위 계획 변경 없이 준비로 전환';}
  else if(action==='explore') {valid=history&&s.past==='depleted'&&state.plan.task==='explore';reason=valid?'고갈 관측을 반영해 상위 계획을 새 광맥 탐색으로 정함':'근거 있는 기존 대안 검토 없이 탐색하거나 상위 계획과 불일치';}
  else reason='대기 선택: 첫 유효 행동에 도달하지 않음';
  return {action,valid,reason,expected_action:s.expected,endpoint:action==='explore'?'exploratory_first_movement':action==='move_bench'?'preparation_first_movement':action==='move_resource'?'resource_first_movement':'no_movement',simulation_only:true};
}
const wanted=(arg('scenes','s1,s2,s3')).split(',');
const routeOrder=arg('routes','A,B').split(',');
const results=[];
trials: for(const s of scenes.filter(s=>wanted.includes(s.id)))for(const route of routeOrder) {
  const state={scene:s.id,route,upperCalls:0,decisionCalls:0,retrieved:{},events:[],startedAt:new Date().toISOString()};
  const t=performance.now();
  try {
    await upperCall(s,route,state);
    for(let n=0;n<5;n++) {
      const action=await choose(s,route,state);
      if(action.startsWith('query:')) {search(s,action.slice(6),state);continue;}
      if(action==='escalate') {await upperCall(s,route,state,'Decisions requests major plan reconsideration using retrieved facts');continue;}
      state.execution=accept(s,state,action);break;
    }
    if(!state.execution)state.error='No first action within budget';
  }catch(e){state.error=String(e.message||e);}
  state.totalMs=ms(t);
  state.searchMs=+state.events.filter(e=>e.type==='search').reduce((a,e)=>a+e.ms,0).toFixed(3);
  state.upperMs=+state.events.filter(e=>e.type==='upper').reduce((a,e)=>a+e.ms,0).toFixed(3);
  state.decisionsMs=+state.events.filter(e=>e.type==='decisions').reduce((a,e)=>a+e.ms,0).toFixed(3);
  write(path.join(OUT,`${s.id}-${route}.json`),state);
  const summary={scene:s.id,route,totalMs:state.totalMs,upperMs:state.upperMs,decisionsMs:state.decisionsMs,searchMs:state.searchMs,upperCalls:state.upperCalls,decisionCalls:state.decisionCalls,queries:Object.keys(state.retrieved),execution:state.execution,error:state.error};
  results.push(summary);write(path.join(OUT,'summary.json'),results);console.log(JSON.stringify(summary));
  if(state.error) {console.log('Stopping new model calls after error; inspect saved evidence.');process.exitCode=1;break trials;}
}
write(path.join(OUT,'source-hashes.json'),['C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs','C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/recall-loop.mjs','C:/Dev/ai-chatroom/lib/agents/adapters.mjs',fileURLToPath(import.meta.url)].map(f=>({file:f,sha256:crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex')})));
