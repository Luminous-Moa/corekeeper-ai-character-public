// 그래프 독립 검수(review-graph.md) I2·I3·L1~L6 반영. I1은 이후 전 단계 재생성으로 해결한다.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../../..');
const planning=path.join(work,'2026-11/2026-11-07-기획연결');
const legacy=path.join(work,'지식그래프');
const live=path.join(work,'2026-11/2026-11-05-지식그래프-실화면');
const B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B');
const read=f=>fs.readFileSync(f,'utf8');
const swap=(text,a,b,label)=>{if(!text.includes(a))throw Error('Missing text: '+label);return text.replace(a,b);};

// 1. 기획 delta: 사용자 결정과 구현·HEAD 판단 분리(I2·L3·L4), 열린 질문 상태(L2).
const delta=JSON.parse(read(path.join(stage,'final-delta.json')));
const node=id=>{const n=delta.nodes.find(x=>x.id===id);if(!n)throw Error('node '+id);return n;};
const ev=(doc,anchor)=>({doc,anchor});
node('P.chests-shared').summary='보관·꺼내기·빠른 쌓기·제작 재료 계산이 호스트·다른 AI 상자를 포함한 모든 상자를 공용으로 쓴다. 제작은 게임처럼 제작대 근처 상자 재료로 바로 한다. 기존 가방 합의의 호스트·다른 AI 상자 제외를 대체한다.';
node('P.chest-open-mode').summary+=' 연 상자가 쓸모없으면(가득 참·원하는 물건 없음) 닫은 뒤 다음 판단에서 기억 내용을 반영한 구역 대표 상자로 이어진다.';
node('P.chest-open-mode').evidence.push(ev('gapAgreement','연 상자가 쓸모없으면'));
node('P.maintenance-abandon').summary='대장간이 없거나 입력 칸이 비어 맡긴 장비를 돌려받을 수 없으면 판단 모델에 그 사실과 포기 선택지를 준다. 포기하면 pending을 지우고 상위 LLM에 어떤 장비를 어느 대장간에서 잃었는지와 가져간 것으로 보이는 사람(사라진 순간 대장간 근처 플레이어 추정, 추정임 명시)을 알린다. 대장간 파괴·식별 불가면 모른다는 사실을 그대로 전한다. 따질지 넘어갈지는 LLM이 정하며 따지라는 지시는 넣지 않는다.';
node('P.maintenance-abandon').evidence.push(ev('gapAgreement','대장간 파괴·식별 불가면 모른다는 사실을 그대로 전한다.'));
const q1=node('Q.choice-total-limit');q1.status='미정';q1.summary+=' 사용자 판단 대기다.';
const q2=node('Q.salvage');q2.status='미정';q2.summary+=' 세션 할 일로 남겼다.';
const impl=node('R.gap-implementation');
impl.summary+=' HEAD 판단·구현(사용자 결정 아님): 회수 불가 포기를 대장간 없음·칸 빔을 직접 본 경우로 한정했고, 기지에 상자를 더 만들지 판단할 때 세는 상자는 직접 놓은 상자로 남겼다.';
impl.evidence.push(ev('gapIntegration','## 상자 판정 확인'),ev('gapFixes','### 경미'));
delta.nodes.push({id:'R.chest-areas-implementation',title:'상자가 많을 때 구역 단계로 나누는 구현 해법(40개 기준)',layer:'참고',status:'참고·구현 해법·사용자 결정 아님',sourceCommit:delta.sourceCommit,summary:'상자 공용으로 기억 상자가 늘어 판단 요청 후보가 255를 넘는 문제를 검수에서 반복 발견해, 상자를 후보로 만드는 생산자 18곳을 공통 규칙으로 묶었다. 40개 이하면 그대로 두고, 넘으면 구역→작은 구역→상자 단계로 나눠 한 단계가 40개 이하가 되게 하며 모든 상자에 닿는다. 후보 상한·페이지는 만들지 않았다. 40이라는 수와 구역 분할은 HEAD·구현 판단이다.',evidence:[ev('gapFixes','### 공통 규칙'),ev('gapFixes','### 전수 목록')]});
delta.edges.push({from:'P.chests-shared',to:'R.chest-areas-implementation',type:'참고: 상자가 많을 때의 구현 해법(사용자 결정 아님)',class:'해석',evidence:[ev('gapFixes','### 공통 규칙')]});
const addition='\n// 빈 선택지 3범위·상자 공용·상자 열림 모드·전투 진입 기준(공식 49bd748). 검수 반영(review-graph.md) 포함.\nObject.assign(documents,'+JSON.stringify(delta.documents,null,2)+');\nnodes.push(...'+JSON.stringify(delta.nodes,null,2)+');\nedges.push(...'+JSON.stringify(delta.edges,null,2)+');\n';
fs.copyFileSync(path.join(stage,'before-final-graph/planning/source.mjs'),path.join(planning,'source.mjs'));
fs.appendFileSync(path.join(planning,'source.mjs'),addition);
fs.writeFileSync(path.join(stage,'final-addition.mjs.txt'),addition);
fs.writeFileSync(path.join(stage,'final-delta.json'),JSON.stringify(delta,null,2)+'\n');

// 2. 정책(I2·I3·L3·L4).
const A='work/2026-11/2026-11-08-선택지누락점검/agreement.md',G='work/2026-11/2026-11-08-선택지누락점검';
const policyFile=path.join(legacy,'기획/정책.md');let pol=read(policyFile);
pol=swap(pol,'### pol.bag-boundary — 자기 상자·명시 이동 경계\n- 규칙: 가방·장비 작업에서 / AI는 / 자기 상자 접근 범위를 유지하고 삭제(trash)·자원 공유 정책 변경을 하지 않는다.','### pol.bag-boundary — 명시 이동 경계(상자 부분은 대체됨)\n- 규칙: 가방·장비 작업에서 / AI는 / 삭제(trash)를 하지 않는다. 상자 접근 부분(자기 상자 접근 범위 유지, 상자 자원 공유 없음)은 pol.bag-boundary-chests로 떼어 내 대체됨으로 두었고 현재 기준은 pol.chests-shared다.','bag-boundary rule');
pol=swap(pol,'- 메모: 2026-11-08 사용자 결정으로 상자 부분(자기 상자 접근 범위 유지, 상자 자원 공유 없음)은 pol.chests-shared가 대체했다. trash 삭제를 제공하지 않는 경계는 유지한다.',`- 메모: 2026-11-08 사용자 결정으로 상자 부분은 pol.chests-shared가 대체했다. 옛 상자 규칙 원문은 pol.bag-boundary-chests(대체됨)에 남겼다. trash 삭제를 제공하지 않는 경계는 유지한다.

### pol.bag-boundary-chests — 자기 상자 접근 범위 유지 (대체됨)
- 규칙: 가방·장비 작업에서 / AI는 / 자기 상자 접근 범위를 유지하고 상자 자원 공유 정책을 바꾸지 않는다.
- 등급: 사용자 결정
- 확정도: 대체됨
- 종류: 역사
- 날짜: 2026-11-07
- 근거: work/2026-11/2026-11-07-가방장비구현/agreement.md "경계: 자기 상자 접근 범위 유지."
- 메모: 2026-11-07 pol.bag-boundary의 상자 부분이다. 2026-11-08 사용자가 pol.chests-shared(모든 상자 공용, 호스트·다른 AI 상자 포함)로 대체했다. 기록으로만 남긴다. 기능이 지지 않는다.`,'bag-boundary memo');
pol=swap(pol,'- 규칙: 상자를 쓸 때 / AI는 / 호스트·다른 AI 상자를 포함한 모든 상자를 보관·꺼내기·빠른 쌓기·제작 재료에 쓴다. 상자가 많으면 구역 단계로 나눠 모든 상자에 닿게 한다.','- 규칙: 상자를 쓸 때 / AI는 / 호스트·다른 AI 상자를 포함한 모든 상자를 보관·꺼내기·빠른 쌓기·제작 재료에 쓴다. 제작은 게임처럼 제작대 근처 상자 재료로 바로 한다.','chests rule');
pol=swap(pol,'- 메모: pol.bag-boundary의 자기 상자 접근 범위 유지를 대체한다. 기지에 상자를 더 만들지 판단할 때 세는 상자는 직접 놓은 상자로 남겼다(HEAD 판단, 상자 사용과 별개).',`- 근거: ${A} "제작: 게임처럼 제작대 근처 상자 재료로 바로 제작"\n- 메모: pol.bag-boundary-chests(pol.bag-boundary의 상자 부분)를 대체한다. 다음은 사용자 결정이 아니다. 상자가 40개를 넘으면 구역 단계로 나누는 것은 구현 해법이다(${G}/fixes.md 4차 공통 규칙, F.chest-areas). 기지에 상자를 더 만들지 판단할 때 직접 놓은 상자만 세는 것은 HEAD 판단이다(${G}/integration.md 상자 판정 확인).`,'chests memo');
pol=swap(pol,'그 상자의 꺼내기·넣기·빠른 쌓기·정렬과 닫기만 선택지로 준다. 상자가 없어지거나 보이지 않거나 손닿는 거리를 벗어나면 모드를 풀고, 전투 상태에 들어가면 상자를 닫는다.','그 상자의 꺼내기·넣기·빠른 쌓기·정렬과 닫기만 선택지로 준다. 연 상자가 쓸모없으면 닫은 뒤 다음 판단에서 기억 내용을 반영한 구역 대표 상자로 이어진다. 상자가 없어지거나 보이지 않거나 손닿는 거리를 벗어나면 모드를 풀고, 전투 상태에 들어가면 상자를 닫는다.','chest-open rule');
pol=swap(pol,'- 규칙: 맡긴 장비를 돌려받을 수 없음을 직접 보면 / 판단 모델은 / 포기를 고를 수 있고, 포기하면 상위 LLM에 상황만 알린다. 따질지는 상위 LLM이 정한다.','- 규칙: 대장간이 없거나 입력 칸이 비어 맡긴 장비를 돌려받을 수 없으면 / 판단 모델은 / 포기를 고를 수 있고, 포기하면 상위 LLM에 상황만 알린다. 대장간 파괴·식별 불가면 모른다는 사실을 그대로 전한다. 따질지는 상위 LLM이 정한다.','abandon rule');
pol=swap(pol,'- 메모: 길을 찾을 수 없는 화면 밖 대장간은 포기 범위가 아니다(U.maintenance-unreachable).',`- 근거: ${A} "대장간 파괴·식별 불가면 모른다는 사실을 그대로 전한다."\n- 메모: 두 경우를 직접 본 경우로 한정한 것은 HEAD 해석이다(${G}/fixes.md 경미 L8). 그래서 길을 찾을 수 없는 화면 밖 대장간은 포기 범위가 아니다(U.maintenance-unreachable).`,'abandon memo');
fs.writeFileSync(policyFile,pol);

// 3. 기능(I3·L5).
const featureFile=path.join(legacy,'기획/기능.md');let fe=read(featureFile);
const section=(id)=>{const s=fe.indexOf('### '+id+' — ');if(s<0)throw Error('feature '+id);const e=fe.indexOf('\n### ',s+4);return [s,e<0?fe.length:e];};
const editFeature=(id,fn)=>{const[s,e]=section(id);fe=fe.slice(0,s)+fn(fe.slice(s,e))+fe.slice(e);};
const chestMemo='\n- 메모: 2026-11-08 사용자 결정으로 상자는 호스트·다른 AI 상자를 포함해 모두 공용이다(pol.chests-shared). 49bd748에서 이 능력은 기억한 모든 상자를 대상으로 하며 상자가 많으면 구역 단계로 고른다(F.chest-areas). 위 2026-11-07 문구의 자기 상자 한정은 당시 판의 범위다.';
editFeature('F.bag-store',t=>{t=swap(t,'### F.bag-store — 품목·자기 상자·수량 지정 보관','### F.bag-store — 품목·상자(모두 공용)·수량 지정 보관','store title');t=swap(t,'선택한 일반 가방 품목을 자기 상자의 유효 슬롯으로 보관한다.','선택한 일반 가방 품목을 상자(호스트·다른 AI 상자 포함, 모두 공용)의 유효 슬롯으로 보관한다.','store line');t=swap(t,'- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles','- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles, pol.chests-shared','store pol');return t.trimEnd()+chestMemo+'\n\n';});
editFeature('F.bag-take',t=>{t=swap(t,'### F.bag-take — 자기 상자에서 지정 수량 꺼내기','### F.bag-take — 상자(모두 공용)에서 지정 수량 꺼내기','take title');t=swap(t,'자기 상자를 열어 관측한 품목만 실제 가방 목적 슬롯으로 꺼낸다.','상자(호스트·다른 AI 상자 포함, 모두 공용)를 열어 관측한 품목만 실제 가방 목적 슬롯으로 꺼낸다.','take line');t=swap(t,'- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles','- 정책: pol.bag-agency, pol.bag-boundary, pol.behavior-roles, pol.chests-shared','take pol');return t.trimEnd()+chestMemo+'\n\n';});
fe=swap(fe,'- 메모: 2026-11-08 공식 0c8a3e8에서 자유 시간(GoalKind.Free, AIGoals.FreeTime, AIPlanner.FreeTimeReason, AIBase.FreeStoreCandidates)이 제거됐다.','- 메모: 2026-11-08 공식 0c8a3e8에서 자유 시간 동작(AIGoals.FreeTime, AIPlanner.FreeTimeReason, AIBase.FreeStoreCandidates)이 제거됐다. GoalKind.Free 열거값은 저장된 숫자를 지키려고 남아 있다(Core/AIGoalKinds.cs).','free-time memo');
fs.writeFileSync(featureFile,fe);

// 4. B verify(L5·L6): B와 스테이징 사본 모두.
for(const file of [path.join(B,'extractor/verify.mjs'),path.join(stage,'B-final/extractor/verify.mjs')]){
  let v=read(file);
  v=swap(v,"freeTimeRemoved:'공식 0c8a3e8에서 자유 시간(GoalKind.Free, FreeTime)이 제거됨. 목표가 없으면 IdleHold가 판단 호출을 쉬게 한다.',idleHoldCode:find('IdleHold','AIRunGate').id,","freeTimeRemoved:'공식 0c8a3e8에서 자유 시간 동작(FreeTime·FreeTimeReason·FreeStoreCandidates)이 제거됨. GoalKind.Free 열거값은 저장 숫자 보존용으로 남음. 목표가 없으면 IdleHold가 판단 호출을 쉬게 한다.',idleHoldCode:find('IdleHold','AIRunGate').id,idleHoldPath:pathTo(find('Update','AIPlayerSpikeMod.cs').id,find('IdleHold','AIRunGate').id),",'verify freeTime');
  v=swap(v,"llmHostProtectionFunction:find('OnHostGoal','AIPlanner.cs').id,linesComputedFromSnapshot:true}","llmHostProtectionFunction:find('OnHostGoal','AIPlanner.cs').id,idleHoldCallFile:'Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs',idleHoldCallLines:linesOf('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs',/AIRunGate\\.IdleHold\\(/),linesComputedFromSnapshot:true}",'verify lines');
  v=swap(v,"q.runQueryPath===null))throw Error('Question path missing');","q.runQueryPath===null||q.idleHoldPath===null||(q.evidence&&(q.evidence.hostCallLines?.length===0||q.evidence.idleHoldCallLines?.length===0))))throw Error('Question path missing');",'verify check');
  fs.writeFileSync(file,v);
}

// 5. 제품 planning.mjs(L1·I3).
const planningFile=path.join(live,'D/planning.mjs');let pm=read(planningFile);
pm=swap(pm,"kind:'빈 선택지 구현 위치',class:'코드 대응'","kind:from==='P.combat-entry'?'임시 대리 신호(전투 진입 미구현)':'빈 선택지 구현 위치',class:'코드 대응'",'combat kind');
pm=swap(pm,"    graph.edges.push({from,to:id,type:'reference',kind:'현재 결정의 정책 요약',class:'합의',evidence:plan.evidence});\n  }\n","    graph.edges.push({from,to:id,type:'reference',kind:'현재 결정의 정책 요약',class:'합의',evidence:plan.evidence});\n  }\n  if(!byId.get('pol.bag-boundary')&&!graph.nodes.some(n=>n.id==='pol.bag-boundary'))throw Error('Missing pol.bag-boundary for replacement edge');\n  graph.edges.push({from:'pol.chests-shared',to:'pol.bag-boundary',type:'reference',kind:'대체: 상자 접근 부분(trash 경계는 유지)',class:'합의',evidence:byId.get('P.chests-shared').evidence});\n",'replacement edge');
fs.writeFileSync(planningFile,pm);
console.log('review fixes applied');
