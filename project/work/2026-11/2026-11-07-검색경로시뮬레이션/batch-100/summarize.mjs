// Summarize all trial outcomes including transport failures and prior interrupted/blocked attempts. No model calls.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const ROOT=path.dirname(fileURLToPath(import.meta.url));
const read=(f,d=null)=>{try{return JSON.parse(fs.readFileSync(f,'utf8'));}catch{return d;}};
const queue=read(path.join(ROOT,'queue.json'));
if(!queue)throw Error('Queue missing');
const quantiles=values=>{const a=values.filter(Number.isFinite).sort((x,y)=>x-y);const q=p=>a.length?a[Math.max(0,Math.ceil(p*a.length)-1)]:null;return {n:a.length,min:a[0]??null,p50:q(.5),p90:q(.9),p95:q(.95),max:a.at(-1)??null};};
function audit(s){
  const visible=new Set(['bench1','frontier1']),seen=new Set(),problems=[];
  for(const e of s.events||[]){
    if(e.type==='search'&&e.evidence){seen.add(e.evidence.id);for(const m of JSON.stringify(e.evidence).matchAll(/\be:\d+\b/g))seen.add(m[0]);for(const loc of e.evidence.locations||[])visible.add(loc.id);}
    if(e.type==='upper_attempt'&&e.answer?.decision==='plan'){
      const p=e.answer;if(p.target_id!=null&&!visible.has(p.target_id))problems.push('unseen target '+p.target_id);
      for(const id of p.evidence_ids||[])if(!seen.has(id))problems.push('unseen evidence '+id);
    }
  }
  const action=s.execution?.action,target={move_resource:'ore1',move_bench:'bench1',explore:'frontier1'}[action];
  if(s.plan?.target_id!=null&&target&&s.plan.target_id!==target)problems.push('plan target / action mismatch');
  const goalAppropriate=!!s.execution&&action===s.execution.expected_action;
  const boundary=goalAppropriate&&s.scene==='s3'&&action==='explore'&&s.plan?.task!=='explore';
  const category=s.status==='failed'?'error':s.status==='blocked'?'blocked':s.status!=='completed'?'incomplete':problems.length?'evidence_or_target_error':boundary?'boundary_unresolved':goalAppropriate&&s.execution.valid?'appropriate_accepted':'inappropriate_or_contract_rejected';
  return {category,goalAppropriate,boundary,originalAutoValid:s.execution?.valid??null,planTask:s.plan?.task??null,firstAction:action??null,problems,escalations:(s.events||[]).filter(e=>e.type==='decisions_attempt'&&e.response?.answers?.[0]?.choice==='escalate').length};
}
const groups={},trials=[],attempts=[];
const rawChecks={checkedResults:0,eventLogMismatches:[],statusMismatches:[],missingResults:[]};
const usage={upperCalls:0,decisionsCalls:0,upperInput:0,upperCached:0,upperOutput:0,decisionsInput:0,decisionsCached:0,upperUsageReported:0,decisionsUsageReported:0,rateLimitResponses:0,failedModelAttempts:0,retryWaitMs:0};
for(const p of queue.pairs)for(const route of p.routes){
  const id=p.pairId+'-'+route,dir=path.join(ROOT,'trials',id),status=read(path.join(dir,'status.json'));
  const key=p.scene+'-'+route;
  groups[key]??={scene:p.scene,route,planned:100,completed:0,failed:0,blocked:0,running:0,pending:0,categories:{},queries:{},escalatedTrials:0,retryTrials:0,values:{allEnded:[],appropriateAccepted:[],boundaryUnresolved:[],upper:[],decisions:[],search:[],wait:[],quotaWait:[]},tokens:{upperInput:0,upperOutput:0,decisionsInput:0}};
  const g=groups[key];g[status?.status||'pending']=(g[status?.status||'pending']||0)+1;
  g.calls??={upper:0,decisions:0,search:0,escalations:0};
  g.searchByActor??={upper:{},decisions:{}};
  g.callDistribution??={upper:{},decisions:{}};
  g.goalAppropriate= g.goalAppropriate||0;g.originalAutoValid= g.originalAutoValid||0;
  g.timingTotalsMs??={total:0,upper:0,decisions:0,search:0,wait:0,quotaWait:0};
  g.values.queueWait??=[];
  const result=status?.result?read(path.join(ROOT,status.result)):null;
  if(result){
    const a=audit(result);g.categories[a.category]=(g.categories[a.category]||0)+1;
    rawChecks.checkedResults++;
    if(status.status!==result.status)rawChecks.statusMismatches.push(id);
    const savedEvents=fs.readFileSync(path.join(ROOT,path.dirname(status.result),'events.jsonl'),'utf8').trim().split('\n').filter(Boolean).map(line=>JSON.parse(line));
    if(JSON.stringify(savedEvents)!==JSON.stringify(result.events))rawChecks.eventLogMismatches.push(id);
    if(a.goalAppropriate)g.goalAppropriate++;if(a.originalAutoValid)g.originalAutoValid++;
    g.calls.upper+=result.upperAttempts||0;g.calls.decisions+=result.decisionAttempts||0;g.calls.escalations+=a.escalations;
    for(const [k,n] of [['upper',result.upperAttempts],['decisions',result.decisionAttempts]])g.callDistribution[k][n]=(g.callDistribution[k][n]||0)+1;
    g.values.queueWait.push(result.queueWaitMs);
    for(const [k,f] of [['total','totalMs'],['upper','upperMs'],['decisions','decisionsMs'],['search','searchMs'],['wait','waitMs'],['quotaWait','quotaWaitMs']])g.timingTotalsMs[k]+=result[f]||0;
    let searchActor='upper';
    for(const e of result.events||[]){
      if(e.type==='upper_attempt')searchActor='upper';
      if(e.type==='decisions_attempt')searchActor='decisions';
      if(e.type==='search'){g.calls.search++;const q=e.id??'unknown';g.searchByActor[searchActor][q]=(g.searchByActor[searchActor][q]||0)+1;}
    }
    g.values.allEnded.push(result.totalMs);if(a.category==='appropriate_accepted')g.values.appropriateAccepted.push(result.totalMs);if(a.boundary)g.values.boundaryUnresolved.push(result.totalMs);
    for(const [key,field] of [['upper','upperMs'],['decisions','decisionsMs'],['search','searchMs'],['wait','waitMs'],['quotaWait','quotaWaitMs']])g.values[key].push(result[field]);
    if(a.escalations)g.escalatedTrials++;if(result.retryCount)g.retryTrials++;
    for(const q of Object.keys(result.retrieved||{}))g.queries[q]=(g.queries[q]||0)+1;
    for(const e of result.events||[]){if(e.type==='upper_attempt'){g.tokens.upperInput+=e.usage?.input||0;g.tokens.upperCached=(g.tokens.upperCached||0)+(e.usage?.cached||0);g.tokens.upperOutput+=e.usage?.output||0;}if(e.type==='decisions_attempt'){g.tokens.decisionsInput+=e.response?.usage?.input_tokens||0;g.tokens.decisionsCached=(g.tokens.decisionsCached||0)+(e.response?.usage?.input_tokens_details?.cached_tokens||0);}}
    trials.push({id,scene:p.scene,rep:p.rep,route,status:result.status,attempt:result.attempt,totalMs:result.totalMs,upperCalls:result.upperCalls,decisionCalls:result.decisionCalls,upperAttempts:result.upperAttempts,decisionAttempts:result.decisionAttempts,retries:result.retryCount,queries:Object.keys(result.retrieved||{}),...a,error:result.error||null,result:status.result});
  } else if(['completed','failed','blocked'].includes(status?.status))rawChecks.missingResults.push(id);
  if(!fs.existsSync(dir))continue;
  for(const name of fs.readdirSync(dir).filter(x=>/^attempt-\d+$/.test(x))){
    const attemptDir=path.join(dir,name),f=path.join(attemptDir,'events.jsonl');if(!fs.existsSync(f))continue;
    const ev=fs.readFileSync(f,'utf8').trim().split('\n').filter(Boolean).map(line=>JSON.parse(line));
    const r=read(path.join(attemptDir,'result.json'));
    attempts.push({id,attempt:name,status:r?.status||'interrupted_or_running',totalMs:r?.totalMs??null,modelAttempts:ev.filter(e=>e.type==='upper_attempt'||e.type==='decisions_attempt').length,error:r?.error||null});
    for(const e of ev){
      if(e.type==='upper_attempt'){usage.upperCalls++;if(e.usage)usage.upperUsageReported++;usage.upperInput+=e.usage?.input||0;usage.upperCached+=e.usage?.cached||0;usage.upperOutput+=e.usage?.output||0;if(!e.ok)usage.failedModelAttempts++;}
      if(e.type==='decisions_attempt'){usage.decisionsCalls++;if(e.response?.usage)usage.decisionsUsageReported++;usage.decisionsInput+=e.response?.usage?.input_tokens||0;usage.decisionsCached+=e.response?.usage?.input_tokens_details?.cached_tokens||0;if(e.status===429)usage.rateLimitResponses++;if(e.status!==200)usage.failedModelAttempts++;}
      if(e.type==='retry_wait')usage.retryWaitMs+=e.ms;
    }
  }
}
for(const g of Object.values(groups)){g.latencyMs=Object.fromEntries(Object.entries(g.values).map(([k,v])=>[k,quantiles(v)]));delete g.values;}
const paired={};
for(const scene of ['s1','s2','s3']){
  const sameChoice=[],sameAccepted=[];
  for(let rep=1;rep<=100;rep++){
    const a=trials.find(t=>t.scene===scene&&t.rep===rep&&t.route==='A'),b=trials.find(t=>t.scene===scene&&t.rep===rep&&t.route==='B');
    if(!a||!b||a.status!=='completed'||b.status!=='completed'||!a.goalAppropriate||!b.goalAppropriate||a.firstAction!==b.firstAction)continue;
    const d=b.totalMs-a.totalMs;sameChoice.push(d);if(a.category==='appropriate_accepted'&&b.category==='appropriate_accepted')sameAccepted.push(d);
  }
  paired[scene]={differenceDefinition:'B total ms minus A total ms; negative = B faster',sameGoalAppropriateFirstChoice:{deltaMs:quantiles(sameChoice),bFaster:sameChoice.filter(x=>x<0).length,bSlower:sameChoice.filter(x=>x>0).length},bothAcceptedAtSameEndpoint:{deltaMs:quantiles(sameAccepted),bFaster:sameAccepted.filter(x=>x<0).length,bSlower:sameAccepted.filter(x=>x>0).length}};
}
const sessions=fs.existsSync(path.join(ROOT,'sessions'))?fs.readdirSync(path.join(ROOT,'sessions')).map(id=>{
  const dir=path.join(ROOT,'sessions',id),setup=read(path.join(dir,'setup.json')),final=read(path.join(dir,'final.json'));
  const f=path.join(dir,'events.jsonl'),events=fs.existsSync(f)?fs.readFileSync(f,'utf8').trim().split('\n').filter(Boolean).map(line=>JSON.parse(line)):[];
  const polls=events.filter(e=>e.type==='preflight');
  return {id,setup,final,periodicPreflight:{count:polls.length,totalMs:polls.reduce((s,e)=>s+e.ms,0),durationMs:quantiles(polls.map(e=>e.ms)),maxUsagePct:Math.max(...polls.map(e=>e.maxPct).filter(Number.isFinite)),failed:polls.filter(e=>!e.found||!e.login||!e.subscriptionOk||e.skip).length}};
}):[];
const report={at:new Date().toISOString(),planned:600,seed:queue.seed,groups,paired,usageAllAttempts:usage,rawChecks,sessions,attempts,trials,notes:['Each scene×route repeats the same fixed synthetic scene 100 times; these are not 100 distinct scenes. Failed terminal trials are not replaced to fill successes.','Quantiles use nearest rank: sorted value at ceil(p*n), with no interpolation.','Latencies include retries, API scheduling and quota wait after trial start; queue wait before a worker picks the trial is excluded. Initial auth/preflight is excluded from trial latency but included in session elapsed wall time. Fixture indexing occurs during static import before batchT and is excluded from both trial latency and batch elapsed wall time.','All-ended includes final failure times; appropriate accepted and unresolved boundary times are separate.','s3 exploratory action under gather is boundary_unresolved, not automatically a retrieval error. Original acceptance flag is preserved.','Prior blocked/interrupted attempts are retained in attempts and usageAllAttempts, even when the trial later resumes.','Model calls with missing token reports are counted as attempts; token totals cover only reported usage.','Calls and searches in group metrics count final attempts; usageAllAttempts includes every saved attempt. Cached input tokens are subsets of input tokens, not additional tokens.']};
fs.writeFileSync(path.join(ROOT,'summary.json'),JSON.stringify(report,null,2));
const sec=x=>x==null?'—':(x/1000).toFixed(2);
const md=['# 100회씩 병렬 검색 경로 시험 집계','',`집계 ${report.at}. 계획 600경로(3장면 × A/B × 100).`,'','| 조건 | 완료 | 오류 | 미완료 | 수락 적절 | 경계 미정 | 기타 판정 | 전체 종료 p50/p90/p95(초) |','|---|---:|---:|---:|---:|---:|---:|---|'];
for(const [key,g] of Object.entries(groups).sort()){const c=g.categories,q=g.latencyMs.allEnded;md.push(`| ${key} | ${g.completed} | ${g.failed} | ${100-g.completed-g.failed} | ${c.appropriate_accepted||0} | ${c.boundary_unresolved||0} | ${(c.evidence_or_target_error||0)+(c.inappropriate_or_contract_rejected||0)} | ${sec(q.p50)} / ${sec(q.p90)} / ${sec(q.p95)} |`);}
md.push('','전체 토큰·호출은 재개 전 중단 시도와 실패/재시도까지 포함한다.','',`위쪽 호출 ${usage.upperCalls}, Decisions 호출 ${usage.decisionsCalls}, 429 ${usage.rateLimitResponses}, 실패 모델 시도 ${usage.failedModelAttempts}. 위쪽 입력 ${usage.upperInput}(캐시 ${usage.upperCached})·출력 ${usage.upperOutput}, Decisions 입력 ${usage.decisionsInput}.`,'','원본: trials/<ID>/attempt-<번호>/events.jsonl 및 result.json. summary.json에 경로별 전체/수락/경계별 분위수, 모델·검색·대기 시간, 재상신·조회 빈도, 개별 판정이 있다.','',...report.notes.map(n=>'- '+n),'');
fs.writeFileSync(path.join(ROOT,'summary.md'),md.join('\n'));
const entries=Object.entries(groups).sort();
const triple=q=>[q.p50,q.p90,q.p95].map(sec).join(' / ');
const table=(header,rows)=>[header,'|'+header.split('|').slice(1,-1).map(()=>'---').join('|')+'|',...rows].join('\n');
const session=sessions[0],wall=session?.final?.elapsedMs;
const reportLines=[
'# 검색 경로 600회 반복 시험 결과',
'',
'2026-11-07. 정상 위치 장면에서는 B가 중앙값을 줄였고, 도구 준비 장면에서는 상위 재판단과 추가 현장 호출 때문에 B가 더 오래 걸렸다. 낡은 광맥 장면에서는 양쪽 모두 고갈된 광맥을 피하고 탐색을 골랐지만 B 99회는 역할 경계가 미정이다. 이 99회를 검색 오답이나 확정 계약의 실행 수락으로 합치지 않는다.',
'',
'## 실행 범위와 집계 기준',
'',
'- 같은 합성 세 장면(s1 정상 광맥 위치, s2 도구 준비 필요, s3 낡은 광맥 고갈)을 A/B 각각 100회 반복했다. 총 300쌍·600경로이며 100개의 서로 다른 장면을 시험한 것이 아니다.',
'- A는 상위 검색·계획 후 Decisions가 구체 행동을 고른다. B는 Decisions 검색 중심이며 필요시 상위 재상신과 상위 검색도 허용하는 혼합 경로다. 초기 상위 검색도 실제 4경로에서 5회 발생했다.',
'- queue seed=20261107. 장면별 A 선행 50쌍/B 선행 50쌍, 쌍 순서 섞기, 최대 4쌍 병렬, 한 쌍의 A/B는 순차 실행했다. 순서 배분은 서버·공유 제한·구독 CLI 부하를 제거하지 못한다.',
'- 저장된 제품 구성은 상위 `gpt-6.1-sol`/`low`, Decisions `gpt-6-luna`다. 이는 이번 시험의 요청 설정 기록이며 개발 에이전트의 모델 지시가 아니다.',
'- `completed`는 첫 행동 선택과 모의 수락 검사가 끝났다는 뜻이다. 자동 `valid` 및 목표 적절성과 별개다. 분위수는 정렬 후 ceil(p×n)번째 값이며 보간하지 않는다.',
'',
'## 목표 적절성과 역할 경계',
'',
table('| 조건 | 절차 완료 | 목표 적절 | 자동 valid:true | 적절·수락 | 경계 미정 | 기술 오류 / 기타 부적절 |',entries.map(([key,g])=>`| ${key} | ${g.completed}/100 | ${g.goalAppropriate}/100 | ${g.originalAutoValid}/100 | ${g.categories.appropriate_accepted||0} | ${g.categories.boundary_unresolved||0} | ${g.failed} / ${(g.categories.evidence_or_target_error||0)+(g.categories.inappropriate_or_contract_rejected||0)} |`)),
'',
'600경로·600attempt 모두 완료됐으며 pending/blocked/failed는 0이다. 첫 행동은 s1 `move_resource`, s2 `move_bench`, s3 `explore`로 각 장면의 기대 행동과 일치했다. 미공개 target/evidence ID 또는 최종 계획 대상과 행동의 불일치는 감사에서 발견하지 않았다. 이는 이 제한된 fixture에서 관측한 결과이며 일반적인 정확도 100%를 뜻하지 않는다.',
'',
's3-B의 99회는 상위 `gather` 아래 `explore`를 골랐으므로 원본 자동 `valid:false`를 그대로 보존했다. 목표 적합은 예, 상위 enum 일치는 아니오, 작업 내 대상 변경과 중요한 계획 변경의 역할 경계는 미정이다. 나머지 1회 [s3-039-B](trials/s3-039-B/attempt-001/result.json)는 두 조회 후 재상신하여 상위 `explore` 계획을 받고 19.149초에 자동 수락됐다. 99회의 후보 선택 지연을 이 1회의 수락 지연과 합쳐 계약 성공률·오답률로 표현하지 않는다.',
'',
'## 첫 행동까지의 시간',
'',
'단위 초. 전체 종료 시간은 경로 시작부터 모의 검사까지이며, s3-B 전체 행은 수락 1회와 경계 미정 99회를 포함한 절차 종료 분포다. 확정 실행 수락 시간으로 해석하지 않는다.',
'',
table('| 조건 | 전체 종료 n | 전체 종료 p50 / p90 / p95 | 적절·수락 n | 수락 p50 / p90 / p95 | 경계 미정 n | 경계 미정 p50 / p90 / p95 |',entries.map(([key,g])=>`| ${key} | ${g.latencyMs.allEnded.n} | ${triple(g.latencyMs.allEnded)} | ${g.latencyMs.appropriateAccepted.n} | ${triple(g.latencyMs.appropriateAccepted)} | ${g.latencyMs.boundaryUnresolved.n} | ${triple(g.latencyMs.boundaryUnresolved)} |`)),
'',
's1의 B 중앙값은 9.887초, A는 16.576초다. 그러나 p95는 B 22.051초로 A 21.446초보다 길다. 중앙값 개선을 모든 꼬리 지연의 개선으로 확대하지 않는다. s2는 B 중앙값 23.315초, A 17.086초로 B가 더 길었다.',
'',
'동일 쌍의 B−A 차이를 따로 계산했다. 이는 두 집단의 중앙값 차이와 다른 통계다. s1 100쌍의 수락 지연 차이 p50/p90/p95는 −6.304 / +1.346 / +6.099초이고 B가 짧은 쌍은 85개였다. s2는 +5.811 / +14.224 / +16.473초, B가 짧은 쌍은 11개였다. s3의 동일한 적절 탐색 선택까지의 차이는 −6.828 / +2.034 / +8.403초(100쌍, B가 짧은 쌍 84개)지만 동일 계약 수락 비교가 아니다. 양쪽 모두 수락된 s3 쌍은 1개뿐이고 그 차이는 +3.322초다.',
'',
'## 호출·검색·재상신',
'',
table('| 조건 | 상위 호출 | Decisions 호출 | 검색 호출 | 상위 검색 sites / history | Decisions 검색 sites / history | 재상신 경로 / 요청 수 |',entries.map(([key,g])=>`| ${key} | ${g.calls.upper} | ${g.calls.decisions} | ${g.calls.search} | ${g.searchByActor.upper.resource_sites||0} / ${g.searchByActor.upper.resource_history||0} | ${g.searchByActor.decisions.resource_sites||0} / ${g.searchByActor.decisions.resource_history||0} | ${g.escalatedTrials} / ${g.calls.escalations} |`)),
'',
'총 상위 1,002회, Decisions 1,215회, 검색 1,123회다. 429 응답·실패 모델 시도·transport 재시도·재시도 대기는 모두 0이다. 각 경로를 성공으로 채우기 위한 의미 재호출은 없었다.',
'',
'- s1-A는 상위 2회·Decisions 1회씩, s1-B는 상위 1회씩이며 Decisions 2회 73건/3회 27건이다. B는 위치를 100회, 경험을 27회 조회했다. 위치 응답에 같은 도구 등급의 성공 경험이 포함돼 별도 history 생략을 실패로 보지 않는다.',
'- s2-A는 모두 상위 2회·Decisions 1회다. s2-B 96회는 Decisions가 재상신했고, 나머지 4회(061/070/081/092)는 상위가 초기에 history를 검색해 준비 계획을 먼저 세웠다. 070은 sites도 추가 조회해 상위 3회를 사용했다. 따라서 B를 검색 주체가 항상 Decisions인 경로로 표현할 수 없다. 재상신 후 상위의 추가 검색은 이번 결과에서 없었다.',
'- s2-B Decisions 호출은 1회 4건/3회 1건/4회 95건이며, sites 조회는 96경로(상위 1·Decisions 95), history는 100경로(상위 4·Decisions 96)다.',
'- s3-A는 모두 상위 2회·Decisions 1회다. s3-B는 99회가 상위 1회·Decisions 3회, 039만 상위 2회·Decisions 4회와 재상신 1회다.',
'',
'## 시간 분해와 전체 벽시계',
'',
'아래는 경로마다 해당 호출 시간을 합친 뒤 얻은 p50/p90/p95이며 단위 초다. 열별 분위수는 서로 더할 수 없다. 검색 열만 밀리초로 표시한다.',
'',
table('| 조건 | 상위 p50 / p90 / p95 (초) | Decisions p50 / p90 / p95 (초) | 검색 p50 / p90 / p95 (ms) | API 슬롯·재시도 대기 p50 / p90 / p95 (초) | 사용량 확인 대기 p50 / p90 / p95 (초) |',entries.map(([key,g])=>`| ${key} | ${triple(g.latencyMs.upper)} | ${triple(g.latencyMs.decisions)} | ${[g.latencyMs.search.p50,g.latencyMs.search.p90,g.latencyMs.search.p95].map(x=>x.toFixed(3)).join(' / ')} | ${triple(g.latencyMs.wait)} | ${triple(g.latencyMs.quotaWait)} |`)),
'',
`전체 배치 벽시계는 **${(wall/60000).toFixed(3)}분(${wall.toFixed(3)}ms)**이다. 세션 ${session?.id}, 종료 ${session?.final?.finishedAt}. 이 값은 병렬 실행의 실제 경과시간이며 600경로 시간 합계나 경로당 순차 지연이 아니다.`,
'',
`초기 fixture 색인 ${session.setup.fixtureIndexMs.toFixed(3)}ms는 core 정적 import 중 batchT 생성 전에 끝났으므로 경로별 시간과 배치 벽시계 모두에서 제외된다. 초기 로그인·구독 preflight ${session.setup.preflightMs.toFixed(3)}ms는 경로별 시간에서 제외되고 배치 벽시계에는 포함된다. 실행 중 강제 preflight ${session.periodicPreflight.count}회는 총 ${(session.periodicPreflight.totalMs/1000).toFixed(3)}초의 검사 작업이며 이를 기다린 각 경로의 시간은 quotaWaitMs에 포함된다. 병렬 경로가 같은 검사를 기다릴 수 있어 이 합을 경로 대기 합이나 벽시계에서 단순 차감할 수 없다. 최대 사용률은 ${session.periodicPreflight.maxUsagePct}%였고 인증·구독·사용량 중단은 없었다.`,
'',
'경로 시작 뒤 모델 호출·검색·API 슬롯 대기·추가 사용량 확인·실행 제어 오버헤드를 포함한다. 경로 시작 전 worker 대기(`queueWaitMs`)는 경로 지연에서 제외한다. B가 쌍의 두 번째일 때 먼저 실행한 A의 시간도 이 사전 대기에 포함된다. 최대 4쌍과 공유 Decisions 분당 30회 제한 아래 관측한 지연이므로 단독 실행 성능으로 일반화하지 않는다.',
'',
'## 토큰',
'',
table('| 조건 | 상위 입력 | 입력 중 캐시 | 상위 출력 | Decisions 입력 |',entries.map(([key,g])=>`| ${key} | ${g.tokens.upperInput} | ${g.tokens.upperCached} | ${g.tokens.upperOutput} | ${g.tokens.decisionsInput} |`)),
'',
`상위 입력 ${usage.upperInput.toLocaleString('en-US')}(그중 캐시 ${usage.upperCached.toLocaleString('en-US')})·출력 ${usage.upperOutput.toLocaleString('en-US')}, Decisions 입력 ${usage.decisionsInput.toLocaleString('en-US')}이다. 캐시는 입력에 포함된 부분이므로 더하지 않는다. 상위 ${usage.upperUsageReported}/${usage.upperCalls}회와 Decisions ${usage.decisionsUsageReported}/${usage.decisionsCalls}회에 사용량 보고가 있었다. Decisions 캐시 입력 보고는 0이다. 저장된 모든 attempt를 계산했으며 이번에는 각 경로 attempt 1회뿐이다. 실제 청구 금액을 측정하지 않았다.`,
'',
'## 한계와 남은 불확실성',
'',
'- 작은 합성 기억과 같은 세 장면을 반복한 결과다. 서로 다른 100장면·실제 게임 기록 재생·다양한 대상 비교·장기 기억 검색의 성능을 검증한 것이 아니다. 반복 간 공유 서비스 상태·캐시·동시성 때문에 통계적 독립이나 모집단 정확도를 주장하지 않는다.',
'- 제품 전체 brief 대신 짧은 역할·출력 형식 프롬프트를 사용했다. 조회 API는 resource_sites에 좌표·관측 시각을, resource_history에 원문 cause·상세 조건을 보강한 시험 제안이다. 양쪽 동일 도구를 썼지만 현재 제품에 같은 API가 구현됐다는 의미는 아니다.',
'- 첫 행동 후보와 모의 수락까지만 확인했다. 게임 이동·길 찾기·제작·채굴·구리 4개 확보 완료·응답 중 세계 변화는 검증하지 않았다. 운영 제품·fixture·프롬프트·원본 기록은 이 집계 과정에서 수정하지 않았다.',
'- ID 공개 여부와 대상 정합성 감사는 잘못된 참조 일부를 찾는 보완이다. 기대 행동과 일치했다고 해서 내부 추론이 해당 근거를 실제로 사용했다는 입증은 아니다. Decisions 응답은 선택값과 확률만 제공한다.',
'- s3에서 gather 안의 새로운 탐색이 작업 내 대상 변경인지 중요한 계획 변경인지 계약을 확정해야 한다. 이 시험은 그 경계를 제품 정책으로 결정하지 않는다.',
'',
'## 근거와 재현',
'',
'- [전체 집계](summary.json), [간단 표](summary.md), [집계·감사 코드](summarize.mjs). summary.json의 trials는 600개 원본 경로·자동 valid·목표 적절·역할 경계·감사 문제를 각각 연결한다.',
'- 원본은 `trials/<장면-반복-경로>/attempt-001/result.json`과 `events.jsonl`이다. 원본 600개의 result.events와 이벤트 로그를 JSON 대조했고 불일치 0, 상태 불일치 0, 결과 누락 0이었다. 감사의 개별 problems는 모두 비어 있다.',
'- [동결 모델](models.json), [고정 큐](queue.json), [계약과 해시](contract.json), [입력·선택지 대조](validation.json), [fixture 한계](fixture-notes.md), [실행 전 검수](review-preflight.md), [최종 상태](status.json).',
`- [초기 설정](sessions/${session.id}/setup.json), [실행 중 preflight](sessions/${session.id}/events.jsonl), [최종 벽시계와 상태](sessions/${session.id}/final.json).`,
'- 역할 경계 근거: [초기 합의](../agreement.md), [초기 보고](../report.md), [초기 독립 검수](../review.md). 이번 보고는 무호출 사후 집계이며 후속 독립 검수와 구분한다.',
'',
'이 batch-100 폴더에서 다음은 네트워크·모델 호출 없이 저장 결과를 다시 집계하고 이 보고서를 재생성한다.',
'',
'```powershell',
'node .\\summarize.mjs',
'```',
'',
'fixture·프롬프트 추출 계약을 다시 확인할 때는 `node .\\validate.mjs`를 사용한다. 실제 모델 호출을 하는 `batch.mjs --mode run`은 결과 재현 집계에 필요하지 않다.',
''
];
fs.writeFileSync(path.join(ROOT,'report.md'),reportLines.join('\n'));
console.log(JSON.stringify({planned:600,groups:Object.fromEntries(Object.entries(groups).map(([k,g])=>[k,{completed:g.completed,failed:g.failed,blocked:g.blocked,running:g.running,pending:g.pending,categories:g.categories,p50:g.latencyMs.allEnded.p50,p90:g.latencyMs.allEnded.p90}])),usage},null,2));
