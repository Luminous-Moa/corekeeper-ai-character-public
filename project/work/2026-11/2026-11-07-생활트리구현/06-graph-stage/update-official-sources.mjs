import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url));
const work=path.resolve(stage,'../../..'),month=path.resolve(stage,'../..');
const graph=path.join(work,'지식그래프'),plan=path.join(month,'2026-11-07-기획연결');
const B=path.join(month,'2026-11-05-지식그래프-개편/B'),C=path.join(month,'2026-11-05-지식그래프-개편/C'),live=path.join(month,'2026-11-05-지식그래프-실화면');
const target='c4a0f5e38016c79567f880bbaea5e56e0eeae347',old='685dde4d27dd91a782a14d8ae33fb576160fa578';
if(execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','rev-parse','HEAD'],{encoding:'utf8'}).trim()!==target)throw Error('공식 HEAD가 승인판과 다름');
const backup=path.join(stage,'official-before');
if(fs.existsSync(backup))throw Error('기존 backup이 있음: 반복실행하지 말고 현재 상태 확인');
fs.mkdirSync(backup,{recursive:true});
for(const [name,source]of [['지식그래프',graph],['기획연결',plan],['B',B],['C',C],['live-B',path.join(live,'B')],['live-C',path.join(live,'C')],['live-D',path.join(live,'D')]]){
 fs.cpSync(source,path.join(backup,name),{recursive:true,filter:p=>!/[\\/](node_modules|bin|obj)([\\/]|$)/.test(p)});
}
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
fs.writeFileSync(path.join(backup,'manifest.json'),JSON.stringify({beforeCode:old,acceptedCode:target,purpose:'HEAD 승인 후 공식 생성원본·생성물 갱신 전 사본; snapshot과 역사 관측 보존',planningSourceSha256:sha(fs.readFileSync(path.join(plan,'source.mjs')))},null,2)+'\n');
for(const p of ['extractor/pin-snapshot.mjs','extractor/verify.mjs','extractor/build.mjs']){
 const f=path.join(B,p),before=fs.readFileSync(f,'utf8');if(!before.includes(old))throw Error('예상 코드 기준 없음 '+p);fs.writeFileSync(f,before.replaceAll(old,target));
}
// 삭제된 파일이 없음을 별도로 확인했다. snapshot의 옛 파일을 남기는 pin 문제가 생기면 중단한다.
const deleted=execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','diff','--name-only','--diff-filter=D',old,target,'--','Assets/AIPlayerSpike','Assets/HeadlessTools','Tools/ai-bridge','Tools/knowledge'],{encoding:'utf8'}).trim();
if(deleted)throw Error('삭제 파일이 있어 빈 snapshot 준비 필요: '+deleted);
const configPath=path.join(graph,'tools/config.json'),config=JSON.parse(fs.readFileSync(configPath,'utf8'));
const additions=['AIHandLease.cs','AIInventory.cs','AILife.cs','Core/AIHeldChoice.cs','Core/AIInventoryGoalForm.cs','Core/AIInventoryRules.cs','Core/AILifeTree.cs'].map(p=>'Assets/AIPlayerSpike/Scripts/'+p);
for(const p of additions)if(!config.codeFiles.some(f=>f.path===p))config.codeFiles.push({container:'모드(게임 안에서 실행)',path:p});
config.codeFiles.sort((a,b)=>a.path.localeCompare(b.path,'en'));fs.writeFileSync(configPath,JSON.stringify(config,null,2)+'\n');

const featurePath=path.join(graph,'기획/기능.md');let features=fs.readFileSync(featurePath,'utf8');
if(features.includes('### F.life-tree'))throw Error('생활 기능이 이미 등록됨');
features+='\n\n### F.life-tree — 읽기 전용 생활 선택 탐색과 직접 실행\n- 상태: 구현됨·새 실게임 미확인\n- 종류: 제품\n- 한 줄: 제작·요리·농사·보관/꺼내기를 필요한 깊이만 조회하고 이미 정한 대상은 같은 입구에서 직접 실행한다. 탐색은 몸·대기 선택·목표를 바꾸지 않고 수량은 상위 작업량을 공유한다. 실제 실행량과 가방 확보량을 구분한다.\n- 정책: pol.behavior-roles, pol.vision-human-view, pol.ai-action-control, pol.original-request-completion\n- 코드 앵커: AILife.Compose, AILife.Navigate, AILife.Resolve, AILife.Ended, AILife.Report, AIBrain.AssembleLifePage, AIBrain.HandleResponse, AIFarm.ExactCandidate, AICook.ExactCandidate, AICraft.ExactCandidate, AIInventory.CandidatesFor, LifeCursor.Check, LifeCursor.Apply, LifeProgress.Record\n- 근거: work/2026-11/2026-11-07-생활트리구현/08-integration.md "첫 단위는 수량·칸 조합을 나열하지 않는 읽기전용 트리와 동일 입구의 직접 실행을 제공한다."\n- 메모: c4a0f5e에 본 코드 통합. 생산코드 어댑터·회귀·Unity와 독립검수 근거는05/07에 한정한다. 실제 모델/API·전체 Tick·실게임 소비자는 미확인이며 수리·자동 가공·강화·거래·낚시는 후속이다.\n';
features+='\n### F.inventory-exact — 정확한 장비·상자·수량 조작\n- 상태: 구현됨·새 실게임 미확인\n- 종류: 제품\n- 한 줄: 정확한 물품·슬롯·variation·상태를 지정해 장착·교체·보관·꺼내기·내려놓기·전달을 정상 요청으로 실행한다. 양쪽 실제 변화와 수량 receipt로 결과를 확인하고 원래 부탁을 유지한다.\n- 정책: pol.behavior-roles, pol.vision-human-view, pol.original-request-completion\n- 코드 앵커: AIInventory.CandidatesFor, AIInventory.LifeEntries, AIInventory.Make, AIInventory.Stale, InventorySkill.Start, InventorySkill.Tick\n- 근거: work/2026-11/2026-11-07-생활트리구현/08-integration.md "이 판은 공통 실행 및 정확한 가방장비 실행을 포함하며 생활 트리의 제작·요리·농사·보관/꺼내기를 연결한다."\n- 메모: 독립 가방장비 후보·공통 실행·상자 용량 수정이 포함된1fc89b9를 기반으로c4a0f5e에서 생활 연결을 검증했다. 다른 사람 자원 공유나 타인 가방 직접 쓰기를 추가한 것은 아니다.\n';
features+='\n### F.hand-choice — 명시 선택한 손 도구·근접무기 유지\n- 상태: 구현됨·새 실게임 미확인\n- 종류: 제품\n- 한 줄: 명시 선택을 캐릭터별로 유지하고 실제 손 물품의 신원·파손·장착 상태를 확인한다. 실행 중 다른 물품으로 몰래 교체하지 않는다.\n- 정책: pol.behavior-roles, pol.vision-human-view\n- 코드 앵커: AIHandLease.Check, AIInventory.TryChosenHand, AIHeldChoice.Check\n- 근거: work/2026-11/2026-11-07-가방장비구현/implementation.md "명시한 손 장비"\n- 메모: 가방장비 문서의 격리 후보 지위는 당시 기록으로 보존하며 본 반영은 생활트리08의c4a0f5e 통합 기록을 따른다. 기존 실제 장비 표시 관측을 새 행동 성공으로 승격하지 않는다.\n';
for(const [id,extra]of [['F.candidates',' 생활 탐색이 열리면 현재 깊이의 목록을 페이지로 조립하고 back/root/close와 공통 제어를 보존한다.'],['F.executor',' 생활 조회 응답은 몸 실행 전에 별도 소비한다. 실제 선택은 현재 대상 재검증과 생산 skill 결과로 연결한다.']]){
 const at=features.indexOf('### '+id+' '),end=features.indexOf('\n### ',at+1);let section=features.slice(at,end<0?undefined:end);section=section.replace(/^- 한 줄: (.*)$/m,(all)=>all+extra);features=features.slice(0,at)+section+(end<0?'':features.slice(end));
}
fs.writeFileSync(featurePath,features);

const sourcePath=path.join(plan,'source.mjs'),source=fs.readFileSync(sourcePath,'utf8'),base=await import(pathToFileURL(sourcePath));
if(base.nodes.some(n=>n.id==='P.life-tree'))throw Error('트리 기획이 이미 있음');
const staged=await import(pathToFileURL(path.join(stage,'source.mjs')));
const oldNodeIds=new Set(base.nodes.map(n=>n.id));
const ns=staged.nodes.filter(n=>!oldNodeIds.has(n.id)&&!n.id.startsWith('R.life-tree-'));
const ds=Object.fromEntries(Object.entries(staged.documents).filter(([k])=>!base.documents[k]&&!k.startsWith('lifeTreeCandidate')));
ds.lifeTreeImplementation={layer:'참고',path:'../2026-11-07-생활트리구현/05-implementation.md'};
ds.lifeTreeIntegration={layer:'참고',path:'../2026-11-07-생활트리구현/08-integration.md'};
ds.lifeTreeReview={layer:'참고',path:'../2026-11-07-생활트리구현/07-review.md'};
const ev=(doc,anchor)=>({doc,anchor});
const n=(id,title,status,summary,doc,anchor)=>({id,title,layer:'참고',status,summary,evidence:[ev(doc,anchor)]});
ns.push(staged.nodes.find(n=>n.id==='R.life-tree-spec'));
ns.push(n('R.life-tree-implementation','생활 트리c4a0f5e 구현과 본 코드 통합','참고·구현 및 본 코드 통합','첫 네 갈래의 읽기전용 탐색·직접 실행·상위 수량 공유·실제 결과를c4a0f5e에 통합했다. 설치·게임·브리지는 변경하지 않았으며 실제 모델/게임 소비자는 미확인이다.','lifeTreeIntegration','공식 C:/Dev/CoreKeeperModSDK의 ai-character를'));
ns.push(n('R.life-tree-offline','생활 트리 생산코드·회귀·Unity 자체검증','참고·오프라인 및 컴파일 검증','Core1295/bridge154/공통36/생활96/요리51/제작19/상자91과fast 경고0·오류0, 최종SHA Unity exit0·compiler error0을 보고했다. 어댑터 경계와 전체CandBuild/Tick·모델·실게임 미확인을 유지한다.','lifeTreeImplementation','## 실제 검증'));
ns.push(n('R.life-tree-review','생활 트리 최종 독립 검수','참고·독립 검수 수용','최종c4a0f5e에서R1~R3 수정과 실제 생활96, 독립 보관18·제작2·요리4 표본을 확인한 뒤 HEAD가 본 반영했다. 전체 회귀·Unity의 독립 재실행이나 실게임을 뜻하지 않는다.','lifeTreeReview','## 수정 후 검수 — c4a0f5e'));
const allIds=new Set([...base.nodes,...ns].map(n=>n.id));
const es=staged.edges.slice(base.edges.length).filter(e=>allIds.has(e.from)&&allIds.has(e.to));
const e=(from,to,type,doc,anchor)=>({from,to,type,class:'해석',evidence:[ev(doc,anchor)]});
es.push(e('P.life-tree-first-scope','R.life-tree-implementation','참고: 수용된 본 코드 구현','lifeTreeIntegration','공식 C:/Dev/CoreKeeperModSDK의 ai-character를'));
es.push(e('R.life-tree-implementation','R.life-tree-offline','참고: 고정판의 실제 코드 검증','lifeTreeImplementation','## 실제 검증'));
es.push(e('R.life-tree-implementation','R.life-tree-review','참고: 최종 독립 검수','lifeTreeReview','## 최종 검증 근거와 한계'));
fs.writeFileSync(sourcePath,source+'\n// 생활 트리: 승인 방향과 실제c4a0f5e 반영·검증을 구분한다.\nObject.assign(documents,'+JSON.stringify(ds,null,2)+');\nnodes.push(...'+JSON.stringify(ns,null,2)+');\nedges.push(...'+JSON.stringify(es,null,2)+');\n');
// 새 코드 대응은 전체 B 재생성 뒤 사용한다. 기존 제작 의도/공통 실행 매핑은 그대로 둔다.
const planningPath=path.join(live,'D/planning.mjs');let planning=fs.readFileSync(planningPath,'utf8');
const mappings=[['P.life-tree','AILife.Compose('],['P.life-tree-readonly','AIBrain.HandleResponse('],['P.life-tree-readonly','AILife.Navigate('],['P.life-tree-direct','AILife.Resolve('],['P.life-tree-direct','AIFarm.ExactCandidate('],['P.life-tree-direct','AICook.ExactCandidate('],['P.life-tree-direct','AICraft.ExactCandidate('],['P.life-tree-direct','AIInventory.CandidatesFor('],['P.life-tree-count','AIPlanProtocol.Read('],['P.life-tree-count','AILife.Remaining('],['P.life-tree-result','AILife.Ended('],['P.life-tree-result','LifeProgress.Record(']];
const addition='  // 生活 트리: 수용된 최신 정적 코드 대응. 함수 실행 trace를 뜻하지 않는다.\n  for (const [from, signature] of '+JSON.stringify(mappings)+') {\n    const targets=graph.nodes.filter(n=>n.type===\'코드\'&&n.signature?.startsWith(signature));\n    if(targets.length!==1)throw Error(\'Life code anchor absent or ambiguous: \'+signature);\n    graph.edges.push({from,to:targets[0].id,type:\'reference\',kind:\'c4a0f5e 생활 구현 입구\',class:\'코드 대응\',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});\n  }\n';
if(!planning.includes('  graph.planning ='))throw Error('planning 앵커 변경');planning=planning.replace('  graph.planning =',addition+'  graph.planning =');fs.writeFileSync(planningPath,planning);
const overlayPath=path.join(live,'C/overlay-source.json'),overlay=JSON.parse(fs.readFileSync(overlayPath,'utf8').replace(/^\uFEFF/,''));overlay.baseCommit=target;fs.writeFileSync(overlayPath,JSON.stringify(overlay,null,2)+'\n');
for(const name of ['README.md','제품그래프-사용법.md']){
 const f=path.join(graph,name);let content=fs.readFileSync(f,'utf8');content=content.replace(/^> (공식 코드 기준|현재 코드 기준)([^\n]*685dde4[^\n]*)/m,'> 과거 코드 기준$2');
 fs.writeFileSync(f,'> 현재 공식 코드 기준은 `'+target+'`이다. 생활 트리 첫 네 갈래와 정확한 가방장비 실행을 본 코드에 반영했다. [반영 기록](../2026-11/2026-11-07-생활트리구현/08-integration.md)과 [검수](../2026-11/2026-11-07-생활트리구현/07-review.md)를 따른다. 설치·실게임은 보류이며 과거 화면 관측을 새 코드판 성공으로 승격하지 않는다.\n\n'+content);
}
console.log(JSON.stringify({sourcesUpdated:true,officialCode:target,backup,addedPlanningNodes:ns.length,addedPlanningEdges:es.length,newCodeFiles:additions.length,legacyFeatures:3,gameGraphChanged:false},null,2));
