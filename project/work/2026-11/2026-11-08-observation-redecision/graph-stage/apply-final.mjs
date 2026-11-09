import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../../..');
const target='20b1aced7594a47eacb4be060405bcb53d38f48f',previous='43c3817fc1a36142c8591fb087ed867665633fe1';
const repo='C:/Dev/CoreKeeperModSDK',legacy=path.join(work,'지식그래프');
const planning=path.join(work,'2026-11/2026-11-07-기획연결');
const B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B'),C=path.join(work,'2026-11/2026-11-05-지식그래프-개편/C');
const live=path.join(work,'2026-11/2026-11-05-지식그래프-실화면');
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
const json=value=>JSON.stringify(value,null,2)+'\n';
const read=file=>fs.readFileSync(file,'utf8');
const git=(...args)=>execFileSync('git',['-C',repo,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==target)throw Error('Official HEAD changed');
const preparation=JSON.parse(read(path.join(stage,'preparation.json')));
for(const item of preparation.canonicalFiles)if(hash(fs.readFileSync(item.path))!==item.sha256)throw Error('Canonical changed, reconcile '+item.path);
if(git('diff','--name-only','--diff-filter=D',previous,target,'--','Assets/AIPlayerSpike','Assets/HeadlessTools','Tools/ai-bridge','Tools/knowledge'))throw Error('Deleted source needs snapshot cleanup');
const stagedB=path.join(stage,'B-final');
const code=JSON.parse(read(path.join(stagedB,'code.graph.json')));
if(code.meta.sourceCommit!==target)throw Error('Staged code version mismatch');
const signatureGroups={
  'R.observation-implementation':['AISkill.BindObservationGoal(','AISkill.BeginObservationTick(','AISkill.RefreshPendingObservation(','AIBrain.OnGoalObservationChanged(','AIBrain.ObserveIncompleteCompletion(','AIGoals.FinishChosen(','AIGoals.RefreshCompletion(','AIPlanReport.ReportJson('],
  'R.observation-delivery':['InventoryDeliverySourceObservationSystem.OnUpdate(','InventoryDeliveryObservationSystem.OnUpdate(','InventorySkill.CaptureDelivery(','InventorySkill.DropTick(','AIInventory.Completed('],
  'R.observation-upper-completion':['AIPlanner.OnOrder(','AIPlanner.ReportCompletionConflict(','AIGoals.CompletionPending(','AIPlanner.RequestReplan(']
};
const mappings=[];
for(const[from,signatures]of Object.entries(signatureGroups))for(const signature of signatures){
  const nodes=code.nodes.filter(node=>node.signature?.startsWith(signature));
  if(nodes.length!==1)throw Error('Missing or ambiguous anchor '+signature);
  mappings.push([from,nodes[0].signature]);
}
const backup=path.join(stage,'before-final-graph');
if(fs.existsSync(backup))throw Error('Already applied, inspect checkpoint');
for(const[name,source]of [['legacy',legacy],['planning',planning],['B',B],['C',C],['live-B',path.join(live,'B')],['live-C',path.join(live,'C')],['live-D',path.join(live,'D')]])fs.cpSync(source,path.join(backup,name),{recursive:true,filter:file=>!/[\\/](node_modules|bin|obj)([\\/]|$)/.test(file)});
fs.appendFileSync(path.join(planning,'source.mjs'),read(path.join(stage,'final-addition.mjs.txt')));
for(const name of ['pin-snapshot.mjs','build.mjs','verify.mjs']){
  const file=path.join(B,'extractor',name),source=read(file);
  if(!source.includes(previous))throw Error('Unexpected B baseline');
  fs.writeFileSync(file,source.replaceAll(previous,target));
}
for(const name of ['snapshot','runtime-package'])fs.cpSync(path.join(stagedB,name),path.join(B,name),{recursive:true});
for(const name of ['csharp.raw.json','javascript.raw.json','code.graph.json','B.graph.json','validation.json','boundaries.json','call-forms.json','snapshot.manifest.json','snapshot-verification.json','external-runtime.manifest.json','question-coverage.json'])fs.copyFileSync(path.join(stagedB,name),path.join(B,name));
const policyFile=path.join(legacy,'기획/정책.md');
fs.appendFileSync(policyFile,`\n\n### pol.completion-observation-check — 완료 선택과 실제 관측의 대조·재판단\n- 규칙: 모델의 완료 선택이 현재 실제 부족·미확인 사실과 어긋나면 현재 사실을 알리고 판단모델을 다시 호출한다. 원래 부탁과 미완료 의무를 유지하고 관측을 목표·단계·대상·실행 회차에 연결한다. 계속·중단·전환은 AI가 선택한다.\n- 등급: 사용자 결정\n- 확정도: 확정\n- 종류: 처리\n- 날짜: 2026-11-08\n- 근거: work/2026-11/2026-11-08-observation-redecision/agreement.md "사용자는 모델이 완료를 선택했더라도 실제 완료되지 않은 사실을 스크립트가 알리고 판단모델을 다시 호출하는 흐름으로 수정하도록 요청했다."\n- 메모: 기존 AI 제어·원래 부탁 보존 계약의 후속 보완이다.5초는 동일 pending의 구현 재검토 주기이며 기존 호출·비용 가드를 따른다.\n`);
const featureFile=path.join(legacy,'기획/기능.md');
let features=read(featureFile);
const descriptions={
  'F.action-select':'현재 몸 Tick의 관측을 목표·실행 회차에 귀속해 판단에 제공한다. 지속 대기 사실은5초 간격으로 다시 검토하고 진행 중 요청·준비와 미소비 사건은 중복 알림을 억제한다. 새 사실은 즉시 전달하며 AI의 계속·중단·전환과 기존 호출·비용 가드를 유지한다.',
  'F.plan-queue':'상위의 계획과 원래 부탁을 유지한다. 측정 조건 있는 finish_goal은 현재 완료 사실을 검사하고 미확인이면 단계 전환을 보류한다. 새 계획 전체 거절과 같은 미완료 원부탁 done이 충돌하면 적용 전 보류하고 상위에 사실을 재보고한다. 정상 완료·유효 새 계획과 무관한 done/defer/drop/answered 권한은 유지한다.',
  'F.executor':'선택한 몸 입력을 수행하고 관측에 원래 목표·세대·실행 회차를 붙인다. 매 몸 Tick에서 확인된 대기만 다음 Brain에서 재검토한다. 새 목표는 이전 전역 완료 관측을 초기화하며 계속되는 이전 실행의 사실은 원래 소속을 표시한다. 중단·전환은 AI 선택과 사용자 지시를 따른다.',
  'F.common-execution':'stop_action은 몸만 중단하고 목표와 부탁을 유지한다. finish_goal은 현재 측정 완료를 재검사해 부족·미확인이면 사실을 알리고 단계·의무를 보존해 재판단한다. 비계량 목표의 판단 종료를 유지한다. request_plan은 유효한 몸 동작을 유지하며 상위에 현재 사실을 전달한다.',
  'F.bag-give':'요청 전 존재한 pickup을 제외하고 sender 표식·물품 신원·출발 슬롯 감소·새 유일 floor를 귀속한다. 같은 floor의 지정 recipient pickup과 실제 소비·수령자 증가를 대조해 완료를 확인한다. 수령자 수량은 매 Tick 읽고 누락은 미관측으로 표시한다. 송신 drop을 자동 반복하지 않는다.'
};
const anchors={
  'F.action-select':'AISkill.RefreshPendingObservation, AIBrain.OnGoalObservationChanged, AIBrain.ObserveIncompleteCompletion',
  'F.plan-queue':'AIGoals.CompletionPending, AIPlanner.OnOrder, AIPlanner.ReportCompletionConflict',
  'F.executor':'AISkill.BindObservationGoal, AISkill.BeginObservationTick, AISkill.RefreshPendingObservation',
  'F.common-execution':'AIGoals.RefreshCompletion, AIGoals.CompletionPending, AIBrain.ObserveIncompleteCompletion',
  'F.bag-give':'InventoryDeliverySourceObservationSystem.OnUpdate, InventoryDeliveryObservationSystem.OnUpdate, InventorySkill.CaptureDelivery'
};
for(const[id,description]of Object.entries(descriptions)){
  const start=features.indexOf('### '+id+' — ');
  if(start<0)throw Error('Missing feature '+id);
  const next=features.indexOf('\n### ',start+4),end=next<0?features.length:next;
  let section=features.slice(start,end);
  section=section.replace(/^- 한 줄: .*$/m,'- 한 줄: '+description);
  section=section.replace(/^- 코드 앵커: (.*)$/m,(_,value)=>'- 코드 앵커: '+value+', '+anchors[id]);
  section=section.replace(/^- 정책: (.*)$/m,(_,value)=>'- 정책: '+value+', pol.completion-observation-check');
  section+='\n- 근거: work/2026-11/2026-11-08-observation-redecision/implementation.md "## 단계 완료"\n- 메모: 현재 코드20b1aced.09e418a 실기의 세 부위 수령·다음 단계·원부탁 완료와 통로 방향 선택은 해당 판의 관측이며 추가 상위 완료 보호는181개 오프라인·Unity 근거다. 최종 설치와 시험 자료 복원은 사용자 종료 승인 대기다.\n';
  features=features.slice(0,start)+section+features.slice(end);
}
features=features.replaceAll('AIBrain.ObserveExecution,','AIBrain.ObserveExecution/1, AIBrain.ObserveExecution/2,');
fs.writeFileSync(featureFile,features);
const planningFile=path.join(live,'D/planning.mjs');
const extension=`  // 관측·재판단20b1aced 정적 구현 대응과 현재 정책.\n  for (const [from, signature] of ${JSON.stringify(mappings)}) {\n    const targets=graph.nodes.filter(node=>node.type==='코드'&&node.signature===signature);\n    if(targets.length!==1)throw Error('Observation anchor absent or ambiguous '+signature);\n    graph.edges.push({from,to:targets[0].id,type:'reference',kind:'관측·재판단 구현 위치',class:'코드 대응',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});\n  }\n  const observationPolicy=byId.get('P.observation-completion-check');\n  graph.nodes.push({id:'pol.completion-observation-check',type:'정책',title:'완료 선택과 실제 관측의 대조·재판단',summary:observationPolicy.summary,status:'사용자 후속 결정',class:'사용자 결정 요약',sourceLocations:observationPolicy.sourceLocations,evidence:observationPolicy.evidence});\n  graph.edges.push({from:'P.observation-completion-check',to:'pol.completion-observation-check',type:'reference',kind:'현재 후속 계약의 정책 요약',class:'합의',evidence:observationPolicy.evidence});\n  const observed=byId.get('R.observation-live');\n  observed.currentSourceCommit=graph.meta.sourceCommit;\n  observed.sameRevision=observed.sourceCommit===graph.meta.sourceCommit;\n  observed.evidenceAppliesToCurrentRevision=observed.sameRevision;\n`;
const planningSource=read(planningFile).replace("'AIBrain.ObserveExecution('","'AIBrain.ObserveExecution(string)'");
if(!planningSource.includes('  graph.planning ='))throw Error('Missing planning hook');
fs.writeFileSync(planningFile,planningSource.replace('  graph.planning =',extension+'  graph.planning ='));
fs.writeFileSync(path.join(stage,'new-code-mappings.json'),json({sourceCommit:target,mapping:mappings,runtimeTrace:false}));
const overlayFile=path.join(live,'C/overlay-source.json'),overlay=JSON.parse(read(overlayFile).replace(/^\uFEFF/,''));
overlay.baseCommit=target;fs.writeFileSync(overlayFile,json(overlay));
const prior='39b22df96dffe869ab0ccd474b18d31462b71d70',menu='Assets/AIPlayerSpike/Scripts/AIInventoryMenu.cs';
const priorBlob=git('rev-parse',prior+':'+menu),currentBlob=git('rev-parse',target+':'+menu);
if(priorBlob!==currentBlob)throw Error('Bag menu changed');
const projection={priorEvidenceCommit:prior,currentSourceCommit:target,sameRevision:false,evidenceAppliesToCurrentRevision:false,runtimeTrace:false,scope:'과거39 가방 실행 증거는 당시 판에 귀속된다.09e418a 실수령과20b1aced 추가 상위 완료 경계의 오프라인 검증은 새 원문을 따른다.',unchangedSource:{file:menu,priorBlob,currentBlob,sha256:hash(execFileSync('git',['-C',repo,'cat-file','blob',target+':'+menu]))},integrationSources:['head-acceptance.md','implementation.md','review-upper-completion.md','review-live.md'].map(name=>{const file=path.join(stage,'source-pins',name);return {path:path.relative(path.dirname(B),file).replaceAll('\\','/'),sha256:hash(fs.readFileSync(file))};})};
fs.writeFileSync(path.join(stage,'successor-projection.json'),json(projection));
const bagFile=path.join(live,'D/bag-source.mjs');let bag=read(bagFile);
const changes=[
  [' const integrationPath=currentPathOptimization?',` const currentObservation=g.meta.sourceCommit==='${target}';\n const integrationPath=currentObservation?'../2026-11-08-observation-redecision/graph-stage/source-pins/head-acceptance.md':currentPathOptimization?`],
  [' const projectionPath=currentPathOptimization?'," const projectionPath=currentObservation?'../2026-11-08-observation-redecision/graph-stage/successor-projection.json':currentPathOptimization?"],
  ['||currentPathOptimization);','||currentPathOptimization||currentObservation);'],
  ['!currentPathOptimization&&(','!currentPathOptimization&&!currentObservation&&('],
  ['scopeBoundary:currentPathOptimization?',"scopeBoundary:currentObservation?'39의 가방 증거를 당시 판에 보존한다. 관측 수정의09e418a 실기와20b1aced 추가 경계 검증·설치 대기는 각각 새 근거를 따른다.':currentPathOptimization?"]
];
for(const[before,after]of changes){if(!bag.includes(before))throw Error('Bag hook changed '+before);bag=bag.replace(before,after);}
fs.writeFileSync(bagFile,bag);
const intro=`> 현재 코드층 기준은 \`${target}\`이며 공식 ai-character와 같다. 완료 사실 검사·관측 재판단·정확 수령 관측을 반영했다. 실기는09e418a, 추가 상위 완료 보호는20b1aced 오프라인·Unity 범위다. 종료·복원·최종 설치는 사용자 승인 대기다. [관측·재판단 반영](../2026-11/2026-11-08-observation-redecision/graph-update.md)을 따른다.\n\n`;
for(const name of ['README.md','제품그래프-사용법.md']){const file=path.join(legacy,name);fs.writeFileSync(file,intro+read(file));}
const bReadme=path.join(B,'README.md');
fs.writeFileSync(bReadme,`> 현재 고정 코드 기준은 \`${target}\`다.09e418a 실기와20b1aced 추가 경계의 오프라인·Unity 검증을 구별한다. [이번 반영](../../2026-11-08-observation-redecision/graph-update.md)에 설치·복원 승인 대기를 기록했다.\n\n`+read(bReadme));
fs.writeFileSync(path.join(stage,'applied-canonical.json'),json({sourceCommit:target,previous,backup,canonicalInputsMatched:true,canonicalApplied:true,officialHead:git('rev-parse','HEAD'),newNodes:7,newMappings:mappings.length,testedRevision:'09e418a8af85bd1578b2cfea09cdbe3c0180db77',installedRevision:'09e418a8af85bd1578b2cfea09cdbe3c0180db77',finalInstall:'awaiting user exit and restoration approval'}));
console.log('Applied observation source delta, verified fixed code and scoped runtime evidence.');
