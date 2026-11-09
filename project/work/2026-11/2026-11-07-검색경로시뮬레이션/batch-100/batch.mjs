import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import {performance} from 'node:perf_hooks';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {Adapters} from 'file:///C:/Dev/ai-chatroom/lib/agents/adapters.mjs';
import {CLI} from 'file:///C:/Dev/ai-chatroom/lib/agents/cli/index.mjs';
import {run,killAll,running} from 'file:///C:/Dev/ai-chatroom/lib/agents/run.mjs';
import {createPreflight} from 'file:///C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/preflight.mjs';
import {firstJson} from 'file:///C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/recall-loop.mjs';
import {scenes,queryDefs,search,choices,accept,modelBrief,fixtureIndexMs} from './core.mjs';

const ROOT=path.dirname(fileURLToPath(import.meta.url));
const arg=(k,d)=>{const i=process.argv.indexOf('--'+k);return i<0?d:process.argv[i+1];};
const MODE=arg('mode','dry-run');
const COUNT=100, SEED=20261107, CONCURRENCY=4;
const now=()=>new Date().toISOString();
const read=(f,d=null)=>{try{return JSON.parse(fs.readFileSync(f,'utf8').replace(/^\uFEFF/,''));}catch{return d;}};
function atomic(file,data){fs.mkdirSync(path.dirname(file),{recursive:true});const tmp=file+'.tmp-'+process.pid;fs.writeFileSync(tmp,JSON.stringify(data,null,2));fs.renameSync(tmp,file);}
function append(file,data){fs.mkdirSync(path.dirname(file),{recursive:true});fs.appendFileSync(file,JSON.stringify(data)+'\n');}
const hash=x=>crypto.createHash('sha256').update(x).digest('hex');
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
const elapsed=t=>+(performance.now()-t).toFixed(3);
let seed=SEED;
function random(){seed=(seed+0x6D2B79F5)|0;let t=seed;t=Math.imul(t^(t>>>15),t|1);t^=t+Math.imul(t^(t>>>7),t|61);return ((t^(t>>>14))>>>0)/4294967296;}
const generated=[];
for(const s of scenes)for(let rep=1;rep<=COUNT;rep++)generated.push({scene:s.id,rep,pairId:`${s.id}-${String(rep).padStart(3,'0')}`,routes:rep%2?['A','B']:['B','A']});
for(let i=generated.length-1;i>0;i--){const j=Math.floor(random()*(i+1));[generated[i],generated[j]]=[generated[j],generated[i]];}
const queueFile=path.join(ROOT,'queue.json');
const existing=read(queueFile);
const queue={seed:SEED,countPerSceneRoute:COUNT,pairs:generated};
if(existing&&JSON.stringify(existing)!==JSON.stringify(queue))throw Error('Existing queue differs; refusing replacement');
if(!existing)atomic(queueFile,queue);
const planCounts={};
for(const p of generated)for(const route of p.routes)planCounts[p.scene+'-'+route]=(planCounts[p.scene+'-'+route]||0)+1;
const sourceFiles=['core.mjs','batch.mjs','pilot-source.mjs.txt'];
const fingerprint=Object.fromEntries(sourceFiles.map(f=>[f,hash(fs.readFileSync(path.join(ROOT,f)))]));
const contract=read(path.join(ROOT,'contract.json'));
if(fingerprint['core.mjs']!==contract.coreSha256)throw Error('Frozen core differs from pilot extraction');
if(MODE==='dry-run'){
  const dry={at:now(),modelsCalled:false,fixtureIndexMs,seed:SEED,maxConcurrentTrials:CONCURRENCY,pairs:generated.length,trials:generated.length*2,counts:planCounts,firstTen:generated.slice(0,10),fingerprint,ordering:'Seeded shuffled pairs; odd replicate A→B, even B→A. Four pair workers; within each pair routes sequential.',resume:'Terminal completed/failed trials are never rerun. Interrupted/blocked attempts are preserved and resumed with a new attempt number.'};
  atomic(path.join(ROOT,'dry-run.json'),dry);console.log(JSON.stringify(dry,null,2));process.exit(0);
}
if(MODE!=='run')throw Error('Use --mode dry-run or run');

const lockFile=path.join(ROOT,'running.lock');
if(fs.existsSync(lockFile)){
  const old=read(lockFile,{});let alive=false;
  if(Number.isInteger(old.pid)){try{process.kill(old.pid,0);alive=true;}catch{}}
  if(alive)throw Error('Another batch runner is alive: '+old.pid);
  fs.renameSync(lockFile,path.join(ROOT,'stale-lock-'+Date.now()+'.json'));
}
fs.writeFileSync(lockFile,JSON.stringify({pid:process.pid,at:now()}),{flag:'wx'});
let stop=null,signalName=null,active=0,maxActive=0,upperAttemptsTotal=0,decisionsTokens=0,decisionsAttempts=0,rateLimitResponses=0,lastProgressAt=null,lastQuota=null;
const sessionId=now().replace(/[:.]/g,'-');
const sessionDir=path.join(ROOT,'sessions',sessionId);
fs.mkdirSync(sessionDir,{recursive:true});
atomic(path.join(sessionDir,'sources.json'),fingerprint);
const batchT=performance.now();
const controllers=new Set();
function stopAll(reason){if(!stop){stop={at:now(),reason};append(path.join(sessionDir,'events.jsonl'),{type:'stop',...stop});}}
for(const name of ['SIGINT','SIGTERM'])process.on(name,()=>{signalName=name;stopAll(name);for(const c of controllers)c.abort();killAll();});
function statusOf(id){return read(path.join(ROOT,'trials',id,'status.json'));}
function terminal(id){return ['completed','failed'].includes(statusOf(id)?.status);}
function progress(){let completed=0,failed=0,blocked=0,pending=0,runningCount=0;const groups={};for(const p of generated)for(const route of p.routes){const status=statusOf(p.pairId+'-'+route)?.status;const key=p.scene+'-'+route;groups[key]??={completed:0,failed:0,blocked:0,running:0,pending:0};if(status==='completed'){completed++;groups[key].completed++;}else if(status==='failed'){failed++;groups[key].failed++;}else if(status==='blocked'){blocked++;groups[key].blocked++;}else if(status==='running'){runningCount++;groups[key].running++;}else {pending++;groups[key].pending++;}}const data={at:now(),sessionId,planned:600,completed,failed,error:failed,blocked,pending,running:runningCount,remaining:600-completed-failed,active,maxActive,rate_limit:rateLimitResponses,lastProgressAt,preflight:lastQuota?{...lastQuota,cached:Date.now()-Date.parse(lastQuota.at)>0,ageMs:Date.now()-Date.parse(lastQuota.at)}:null,upperAttemptsTotal,decisionsAttempts,decisionsTokens,elapsedMs:elapsed(batchT),stop,groups};atomic(path.join(ROOT,'progress.json'),data);atomic(path.join(ROOT,'status.json'),data);return data;}
let progressTimer;

try{
  let decisionKey=process.env.OPENAI_API_KEY;
  if(!decisionKey)decisionKey=execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-Command','[Environment]::GetEnvironmentVariable("OPENAI_API_KEY", "User")'],{encoding:'utf8',windowsHide:true}).trim();
  for(const k of ['ANTHROPIC_API_KEY','OPENAI_API_KEY','GEMINI_API_KEY','GOOGLE_API_KEY','XAI_API_KEY'])delete process.env[k];
  if(!decisionKey)throw Error('Decisions credential unavailable');
  const cfg=read('C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/config.json',{});
  const upper=cfg.agents?.gpt??{model:'gpt-6.1-sol',effort:'low'};
  if(cfg.seats?.gpt)throw Error('Unexpected GPT seat override');
  const settingsDir=path.join(os.homedir(),'AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers');
  const ds=read(path.join(settingsDir,'decisions-settings.json'));
  if(!ds?.model)throw Error('Decisions settings not readable');
  const modelFile=path.join(ROOT,'models.json');
  const models={upper,decisions:ds.model};
  const previousModels=read(modelFile);
  if(previousModels&&JSON.stringify(previousModels)!==JSON.stringify(models))throw Error('Model settings changed since batch started; resume stopped to prevent mixed models');
  if(!previousModels)atomic(modelFile,models);
  const adapterConfig={agents:{gpt:upper},turnTimeoutSec:90,bins:cfg.bins||{},webSearch:false};
  const preflightAdapter=new Adapters(path.join(sessionDir,'preflight-runtime'),adapterConfig);
  const preflight=createPreflight({MEMBERS:{gpt:{cli:'codex',name:'ChatGPT'}},adapters:preflightAdapter,CLI,run,HERE:sessionDir,QUOTA_SKIP_PCT:85,busy:new Set(),FAKE_CHAT:null});
  let quotaPromise=null,quotaAt=0,quotaCalls=0;
  async function quotaCheck(force=false){
    if(stop)throw Object.assign(Error(stop.reason),{kind:'blocked'});
    if(quotaPromise)return quotaPromise;
    if(!force&&Date.now()-quotaAt<60000&&upperAttemptsTotal-quotaCalls<20)return;
    quotaPromise=(async()=>{const t=performance.now();await preflight.quotaCheck('gpt',true);const h=await preflight.check('gpt');quotaAt=Date.now();quotaCalls=upperAttemptsTotal;const e={type:'preflight',at:now(),ms:elapsed(t),found:h.found,login:h.login,subscriptionOk:h.subscriptionOk,maxPct:h.maxPct,skip:h.skip,skipWhy:h.skipWhy,forcedRefresh:true};lastQuota=e;append(path.join(sessionDir,'events.jsonl'),e);if(!h.ok){stopAll('Subscription preflight: '+(h.skipWhy||'login/auth failure'));throw Object.assign(Error(stop.reason),{kind:'blocked'});}})();
    try{await quotaPromise;}finally{quotaPromise=null;}
  }
  // Initialize auth before the forced quota poll; same existing preflight API and 85% cutoff.
  const preT=performance.now();
  const initial=await preflight.check('gpt');quotaAt=Date.now();
  lastQuota={at:now(),maxPct:initial.maxPct,skip:initial.skip,skipWhy:initial.skipWhy,forcedRefresh:true};
  atomic(path.join(sessionDir,'setup.json'),{at:now(),upper,decisionsModel:ds.model,fixtureIndexMs,preflightMs:elapsed(preT),found:initial.found,login:initial.login,subscriptionOk:initial.subscriptionOk,maxPct:initial.maxPct,skip:initial.skip,skipWhy:initial.skipWhy,apiCallsPerMinute:ds.maxCallsPerMinute||30,concurrency:4});
  if(!initial.ok)throw Object.assign(Error('Initial preflight: '+(initial.skipWhy||'login/auth failure')),{kind:'blocked'});

  // Shared rolling-window API rate limiter, using the configured per-character ceiling.
  const apiClockFile=path.join(ROOT,'api-clock.json');
  const savedClock=read(apiClockFile,{});
  let apiStarts=(savedClock.starts||[]).filter(t=>Date.now()-t<60000),apiCoolUntil=savedClock.coolUntil||0,apiLastStart=savedClock.lastStart||0,apiTokenEstimatedCost=0;
  const saveApiClock=()=>atomic(apiClockFile,{at:now(),starts:apiStarts,coolUntil:apiCoolUntil,lastStart:apiLastStart});
  // Count prior saved usage across sessions; do not reset cost accounting on resume.
  for(const trial of fs.existsSync(path.join(ROOT,'trials'))?fs.readdirSync(path.join(ROOT,'trials')):[]){for(const attempt of fs.readdirSync(path.join(ROOT,'trials',trial)).filter(x=>x.startsWith('attempt-'))){const f=path.join(ROOT,'trials',trial,attempt,'events.jsonl');if(!fs.existsSync(f))continue;for(const line of fs.readFileSync(f,'utf8').trim().split('\n')){if(!line)continue;const e=JSON.parse(line);if(e.type==='decisions_attempt')apiTokenEstimatedCost+=(e.response?.usage?.input_tokens||0)/1e6*(ds.usdPerMillionInputTokens||0.1);}}}
  async function apiSlot(state){
    const t=performance.now();
    while(true){
      if(stop)throw Object.assign(Error(stop.reason),{kind:'blocked'});
      const usage=read(path.join(settingsDir,'decisions-usage.json'),{});
      const prodCost=usage.month===new Date().toISOString().slice(0,7)?Number(usage.usd||0):0;
      if(prodCost+apiTokenEstimatedCost>=ds.monthlyCapUsd){stopAll('Decisions configured monthly budget reached');throw Object.assign(Error(stop.reason),{kind:'blocked'});}
      const time=Date.now();apiStarts=apiStarts.filter(v=>time-v<60000);
      const gap=Math.max(0,(ds.idleGapSeconds||0.3)*1000-(time-apiLastStart));
      const limit=apiStarts.length>=(ds.maxCallsPerMinute||30)?60000-(time-apiStarts[0])+5:0;
      const wait=Math.max(gap,limit,apiCoolUntil-time,0);
      if(wait>0){await sleep(Math.min(wait,1000));continue;}
      apiStarts.push(time);apiLastStart=time;saveApiClock();const waitMs=elapsed(t);state.waitMs+=waitMs;return waitMs;
    }
  }
  function classify(detail,status){
    if(status===401||status===403||/usage limit|quota exceeded|insufficient_quota|not logged in|authentication|subscription.*limit/i.test(detail))return 'blocked';
    if(status===429||status>=500||/capacity|temporar|timed? ?out|timeout|ECONN|fetch failed|network|rate.?limit/i.test(detail))return 'transient';
    return 'error';
  }
  const retryLimit=2; // initial attempt + at most 2 transport retries; semantic failures never retried.
  async function pauseRetry(state,kind,attempt,reason,retryAfterMs=0){const wait=Math.max(retryAfterMs,1000*2**(attempt-1));state.waitMs+=wait;state.retryCount++;state.emit({type:'retry_wait',kind,attempt,ms:wait,reason});await sleep(wait);}

  async function trial(pair,route,worker){
    const id=pair.pairId+'-'+route;
    if(terminal(id)||stop)return;
    const s=scenes.find(x=>x.id===pair.scene);
    const trialDir=path.join(ROOT,'trials',id);fs.mkdirSync(trialDir,{recursive:true});
    const prior=fs.readdirSync(trialDir).filter(f=>/^attempt-\d+$/.test(f));
    const attempt=1+Math.max(0,...prior.map(f=>+f.slice(8)));
    const attemptDir=path.join(trialDir,'attempt-'+String(attempt).padStart(3,'0'));
    fs.mkdirSync(attemptDir,{recursive:false});
    const adapters=new Adapters(path.join(attemptDir,'runtime'),adapterConfig);
    const start=performance.now();
    const state={id,scene:s.id,rep:pair.rep,route,attempt,worker,sessionId,startedAt:now(),queueWaitMs:elapsed(batchT),status:'running',upperCalls:0,decisionCalls:0,upperAttempts:0,decisionAttempts:0,retryCount:0,waitMs:0,quotaWaitMs:0,retrieved:{},events:[]};
    state.emit=e=>{const item={at:now(),...e};state.events.push(item);append(path.join(attemptDir,'events.jsonl'),item);};
    const doSearch=q=>{const n=state.events.length;const result=search(s,q,state);if(state.events.length===n){state.emit({type:'search',...result});return;}const e=state.events.pop();if(state.events.length!==n)throw Error('search event integrity');state.emit(e);};
    state.emit({type:'trial_start',pairOrder:pair.routes,initialObservation:s.obs});
    atomic(path.join(trialDir,'status.json'),{id,attempt,sessionId,status:'running',startedAt:state.startedAt});
    active++;maxActive=Math.max(maxActive,active);
    async function upperCall(reason='initial'){
      if(state.upperCalls>=3)throw Error('upper call budget hit');
      state.upperCalls++;
      const brief=modelBrief(route,reason);
      const turn=JSON.stringify({observation:s.obs,query_tools:queryDefs,retrieved:state.retrieved,current_plan:state.plan||null,reason});
      let result;
      for(let a=1;a<=retryLimit+1;a++){
        const qt=performance.now();await quotaCheck();state.quotaWaitMs+=elapsed(qt);
        if(stop)throw Object.assign(Error(stop.reason),{kind:'blocked'});
        const t=performance.now();upperAttemptsTotal++;state.upperAttempts++;
        let r;try{r=await adapters.chat('gpt',brief,turn);}catch(e){r={ok:false,text:'',ms:elapsed(t),detail:String(e.message||e)};}
        const item={type:'upper_attempt',logicalCall:state.upperCalls,attempt:a,reason,ms:elapsed(t),cli_ms:r.ms,model:r.model,effort:r.effort,prompt:{brief,turn:JSON.parse(turn)},answer:r.ok?firstJson(r.text):null,raw:r.text,ok:r.ok,detail:r.detail,usage:r.usage};
        state.emit(item);
        if(r.ok){result=item.answer;if(!result)throw Error('upper response invalid JSON');break;}
        const kind=classify(r.detail||'');
        if(kind==='blocked'){stopAll('Upper CLI '+r.detail);throw Object.assign(Error(stop.reason),{kind});}
        if(kind!=='transient'||a>retryLimit)throw Error('Upper CLI failed: '+r.detail);
        await pauseRetry(state,'upper',a,r.detail||'temporary failure');
      }
      if(result?.decision==='recall'){
        for(const q of (result.queries||[]).slice(0,2))doSearch(q);
        return upperCall('requested search results');
      }
      if(result?.decision!=='plan'||!['gather','prepare_tool','explore','wait'].includes(result.task))throw Error('invalid plan');
      state.plan=result;
    }
    async function choose(){
      if(state.decisionCalls>=5)throw Error('decision call budget hit');
      state.decisionCalls++;
      const opts=choices(s,route,state);
      // Exact pilot input/question text; only call transport, limits, logging and retries differ.
      const payload={model:ds.model,input:JSON.stringify({observation:s.obs,plan:state.plan,retrieved:state.retrieved}),questions:[{type:'choice',name:'action',instructions:'현재 작업과 관찰·조회 근거를 보고 다음 행동 또는 정보 조회 하나를 고른다. 필요한 정보가 부족하면 조회할 수 있다. 도구 준비 등 작업 계획의 중요한 변경은 상위 판단에 요청한다. 기억과 최신 확인이 충돌하면 최신 확인을 고려한다. 불필요한 대기는 피하되 없는 사실을 추측하지 않는다.',choices:opts}]};
      for(let a=1;a<=retryLimit+1;a++){
        const waitMs=await apiSlot(state);state.emit({type:'api_slot',logicalCall:state.decisionCalls,attempt:a,ms:waitMs});
        const t=performance.now();const controller=new AbortController();controllers.add(controller);const timer=setTimeout(()=>controller.abort(),15000);
        let response=null,data=null,detail='',retryAfterMs=0;
        state.decisionAttempts++;decisionsAttempts++;
        try{response=await fetch('https://api.openai.com/v1/decisions',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+decisionKey},body:JSON.stringify(payload),signal:controller.signal});data=await response.json();const ra=response.headers.get('retry-after');if(ra){const n=Number(ra);retryAfterMs=Number.isFinite(n)?Math.max(0,n*1000):Math.max(0,Date.parse(ra)-Date.now());}}catch(e){detail=e.name==='AbortError'?'request timeout/abort':String(e.message||e);}finally{clearTimeout(timer);controllers.delete(controller);}
        const tokens=data?.usage?.input_tokens||0;decisionsTokens+=tokens;apiTokenEstimatedCost+=tokens/1e6*(ds.usdPerMillionInputTokens||0.1);
        state.emit({type:'decisions_attempt',logicalCall:state.decisionCalls,attempt:a,ms:elapsed(t),prompt:payload,status:response?.status||0,response:data,error:detail||null});
        if(response?.ok&&data){const answer=data.answers?.[0];if(answer?.type!=='choice'||!opts.some(o=>o.value===answer.choice))throw Error('invalid Decisions answer');return answer.choice;}
        detail=detail||String(data?.error?.message||'Decisions HTTP '+response?.status).slice(0,300);
        const kind=classify(detail,response?.status);
        if(kind==='blocked'){stopAll('Decisions '+detail);throw Object.assign(Error(stop.reason),{kind});}
        if(response?.status===429){rateLimitResponses++;apiCoolUntil=Math.max(apiCoolUntil,Date.now()+Math.max(retryAfterMs,1000*2**(a-1)));saveApiClock();}
        if(kind!=='transient'||a>retryLimit)throw Error('Decisions failed: '+detail);
        await pauseRetry(state,'decisions',a,detail,retryAfterMs);
      }
    }
    try{
      await upperCall();
      for(let n=0;n<5;n++){
        const action=await choose();
        if(action.startsWith('query:')){doSearch(action.slice(6));continue;}
        if(action==='escalate'){await upperCall('Decisions requests major plan reconsideration using retrieved facts');continue;}
        state.execution=accept(s,state,action);break;
      }
      if(!state.execution)throw Error('No first action within budget');
      state.status='completed';
    }catch(e){state.status=e.kind==='blocked'||signalName?'blocked':'failed';state.error=String(e.message||e);}
    finally{
      state.finishedAt=now();state.totalMs=elapsed(start);
      state.upperMs=+state.events.filter(e=>e.type==='upper_attempt').reduce((a,e)=>a+e.ms,0).toFixed(3);
      state.decisionsMs=+state.events.filter(e=>e.type==='decisions_attempt').reduce((a,e)=>a+e.ms,0).toFixed(3);
      state.searchMs=+state.events.filter(e=>e.type==='search').reduce((a,e)=>a+e.ms,0).toFixed(3);
      state.waitMs=+state.waitMs.toFixed(3);state.quotaWaitMs=+state.quotaWaitMs.toFixed(3);
      state.emit({type:'trial_end',status:state.status,totalMs:state.totalMs,error:state.error||null,execution:state.execution||null});
      const saved={...state};delete saved.emit;
      atomic(path.join(attemptDir,'result.json'),saved);
      atomic(path.join(trialDir,'status.json'),{id,attempt,sessionId,status:state.status,startedAt:state.startedAt,finishedAt:state.finishedAt,totalMs:state.totalMs,result:path.relative(ROOT,path.join(attemptDir,'result.json')),error:state.error||null});
      active--;lastProgressAt=now();const p=progress();console.log(JSON.stringify({id,attempt,status:state.status,ms:state.totalMs,action:state.execution?.action,autoValid:state.execution?.valid,finished:p.completed+p.failed,remaining:p.remaining,error:state.error||null}));
    }
  }
  let index=0;
  const maxPairs=Number(arg('max-pairs',generated.length));
  const selected=generated.slice(0,maxPairs);
  async function worker(w){while(!stop){const i=index++;if(i>=selected.length)return;const pair=selected[i];for(const route of pair.routes){if(stop)return;await trial(pair,route,w);}}}
  progressTimer=setInterval(()=>progress(),10000);
  console.log(JSON.stringify({started:true,sessionId,pairs:selected.length,concurrency:CONCURRENCY,upper,decisions:ds.model,fixtureIndexMs}));
  await Promise.all(Array.from({length:CONCURRENCY},(_,i)=>worker(i+1)));
}catch(e){stopAll(String(e.message||e));console.log(JSON.stringify({stopped:true,reason:stop.reason}));process.exitCode=1;}
finally{
  if(progressTimer)clearInterval(progressTimer);
  killAll();
  const final=progress();
  atomic(path.join(sessionDir,'final.json'),{...final,finishedAt:now(),trackedChildren:running.size,signalName});
  if(read(lockFile)?.pid===process.pid)fs.unlinkSync(lockFile);
  console.log(JSON.stringify({finished:true,completed:final.completed,failed:final.failed,pending:final.pending,remaining:final.remaining,stop:final.stop}));
}
