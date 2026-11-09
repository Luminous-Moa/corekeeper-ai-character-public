import fs from 'node:fs';
import {abs,hash,read} from './live-lib.mjs';
// Authoring overlay. Anchors are resolved against the final machine-extracted code graph.
export function addBagImplementation(g){
 const evidencePath='../2026-11-07-가방장비구현/implementation-evidence.json',evidence=read(evidencePath);
 const sameRevision=evidence.sourceCommit===g.meta.sourceCommit;
 const currentUnrestriction=g.meta.sourceCommit==='5cc5a43ff8df91634c04b44bac9f15f849bdb611';
 const currentExpedition=g.meta.sourceCommit==='7af8f91e5d26e4fd9ce5996809ae41e213340118';
 const currentPathOptimization=g.meta.sourceCommit==='43c3817fc1a36142c8591fb087ed867665633fe1';
 const currentObservation=g.meta.sourceCommit==='20b1aced7594a47eacb4be060405bcb53d38f48f';
 const currentChoiceGaps=g.meta.sourceCommit==='49bd748a8fcd390fc658166ca662ddb13b5c5c0d';
 const integrationPath=currentChoiceGaps?'../2026-11-08-선택지누락점검/graph-stage/source-pins/integration.md':currentObservation?'../2026-11-08-observation-redecision/graph-stage/source-pins/head-acceptance.md':currentPathOptimization?'../2026-11-08-path-optimization/graph-stage/source-pins/head-acceptance.md':currentExpedition?'../../채집이동귀환-구현자료/graph/final-source-pins/expeditionIntegration.json':currentUnrestriction?'../2026-11-07-제약해제/graph.md':'../../장비정비-트리/10-main-integration.md';
 const projectionPath=currentChoiceGaps?'../2026-11-08-선택지누락점검/graph-stage/successor-projection.json':currentObservation?'../2026-11-08-observation-redecision/graph-stage/successor-projection.json':currentPathOptimization?'../2026-11-08-path-optimization/graph-stage/successor-projection.json':currentExpedition?'../../채집이동귀환-구현자료/graph/successor-projection.json':currentUnrestriction?'../2026-11-07-제약해제/graph-stage/successor-projection.json':'../../장비정비-트리/09-graph-stage/successor-projection.json';
 const acceptedLaterSource=evidence.sourceCommit==='39b22df96dffe869ab0ccd474b18d31462b71d70'&&(g.meta.sourceCommit==='5a05b80c4fe1ea764caa62ee83d06ac7fb3177ef'||currentUnrestriction||currentExpedition||currentPathOptimization||currentObservation||currentChoiceGaps);
 if((!sameRevision&&!acceptedLaterSource)||evidence.liveGameObserved!==false)throw Error('Bag evidence SHA/scope mismatch');
 let projection=null;
 if(acceptedLaterSource){
  projection=read(projectionPath);
  if(projection.priorEvidenceCommit!==evidence.sourceCommit||projection.currentSourceCommit!==g.meta.sourceCommit||projection.sameRevision!==false||projection.evidenceAppliesToCurrentRevision!==false||projection.runtimeTrace!==false)throw Error('Bag successor projection scope mismatch');
  if(currentChoiceGaps){if(projection.changedSource.priorBlob===projection.changedSource.currentBlob||hash('B/snapshot/'+projection.changedSource.file)!==projection.changedSource.sha256)throw Error('Bag menu changed-source correspondence mismatch');}
  else if(projection.unchangedSource.priorBlob!==projection.unchangedSource.currentBlob||hash('B/snapshot/'+projection.unchangedSource.file)!==projection.unchangedSource.sha256)throw Error('Bag menu fixed blob correspondence mismatch');
  for(const input of projection.integrationSources)if(hash(input.path)!==input.sha256)throw Error('Bag successor integration/review source mismatch: '+input.path);
  const integration=fs.readFileSync(abs(integrationPath),'utf8');if(!currentUnrestriction&&!currentExpedition&&!currentPathOptimization&&!currentObservation&&!currentChoiceGaps&&(!integration.includes(evidence.sourceCommit)||!integration.includes(g.meta.sourceCommit)))throw Error('Bag successor source integration evidence mismatch');
 }
 for(const input of evidence.inputs)if(hash(input.stagedPath)!==input.sha256)throw Error('Bag evidence snapshot hash mismatch: '+input.stagedPath);
 g.bagImplementationEvidence={...evidence,sourceDocument:evidencePath,evidenceKind:'compile/offline/source-review; not live game',currentSourceCommit:g.meta.sourceCommit,sameRevision,evidenceAppliesToCurrentRevision:sameRevision,...(acceptedLaterSource?{currentIntegrationDocument:integrationPath,successorStaticProjection:projection,scopeBoundary:currentChoiceGaps?'39의 가방 증거를 당시 판에 보존한다. 49bd748은 가방 메뉴가 바뀌어 같은 blob 투영을 하지 않으며 새 가방 선택지는 빈 선택지 구현·검수 근거를 따른다.':currentObservation?'39의 가방 증거를 당시 판에 보존한다. 관측 수정의09e418a 실기와20b1aced 추가 경계 검증·설치 대기는 각각 새 근거를 따른다.':currentPathOptimization?'39의 가방 증거는 당시 판에 귀속된다. 43c3817의 대표 실기와 한계는 길 계산 최적화 새 근거를 따른다.':currentExpedition?'39 실행증거의 sameRevision=false를 유지한다. 현재7af8f91 정적대응 및 원정 오프라인 근거는 새 기록, 조명은 사용자 후속분리를 따른다.':currentUnrestriction?'39의실행증거는39근거로보존. 현재제약해제는새actions/craft/info/통합검수보고를따르며현재정적연결만투영한다.':'39의검수·연결snapshot은39근거로보존. 현재5a의새정비통합근거는10본통합과07/08을따르며옛검증을승격하지않음.'}:{})};
 const policyPath='../../지식그래프/기획/정책.md',policyText=fs.readFileSync(abs(policyPath),'utf8');
 for(const id of ['pol.bag-agency','pol.bag-boundary','pol.bag-navigation','pol.choice-no-cut']){
  const at=policyText.indexOf('### '+id+' — '),body=policyText.slice(at).split('\n### ')[0],start=policyText.slice(0,at).split('\n').length;
  if(at<0)throw Error('Missing policy source '+id);
  const rule=body.match(/^- 규칙: (.*)$/m)[1],loc={path:policyPath,start,end:start+body.trimEnd().split('\n').length-1};
  g.nodes.push({id,type:'정책',title:body.split('\n')[0].split(' — ')[1],rule,summary:rule,status:'합의',class:'결정',sourceLocations:[loc]});
  g.edges.push({from:'P.bag-agency',to:id,type:'reference',kind:'승인 정책의 원문 요약',class:'해석',evidence:[loc]});
  for(const mode of ['equip','hold','store','take','drop','give'])g.edges.push({from:id,to:'R.bag-'+mode,type:'reference',kind:'정책의 능력 적용 위치',class:'해석',evidence:[loc],coverage:'관련 구현 위치 색인. 전체 규칙 충족이나 실게임 성공을 보증하지 않음'});
 }
 const capabilities=[["equip","장비 장착·교체·해제","실제 활성 장비 슬롯과 출발/도착 물품을 고르고 정상 UI Swap 뒤 양 슬롯 교환을 관측한다. 반지·파우치 목적지와 용량/태그 조건을 구별한다.","AIInventory.Candidates, AIInventory.GearSlots, AIInventory.Stale, AIInventory.CapacityAfterSwap, InventorySkill.Start, InventorySkill.Tick","AIInventory.Candidates","장착/교체/해제:"],["hold","명시 도구·근접무기 손선택 유지","Decisions가 고른 구체 핫바칸의 물품을 실제 장비 관측으로 확인한다. 후속 채굴·근접공격은 선택 물품 신원과 파손을 확인하고 자동 대체하지 않는다.","AIInventory.TryChosenHand, AIHandLease.Check, AIHeldChoice.Check, AIBody.PickTool, AIBody.PickWeapon","AIInventory.TryChosenHand","손 선택:"],["store","품목·자기 상자·수량 지정 보관","선택한 일반 가방 품목을 자기 상자의 유효 슬롯으로 보관한다. 요청 잔량과 keep을 반영하며 실제 source 감소=destination 증가만 누적한다.","AIInventory.ChestCandidates, AIInventory.StoreRemaining, AIInventory.Stale, InventorySkill.Tick, AIInventoryRules.MoveCount, AIInventoryRules.ObservedTransfer","AIInventory.ChestCandidates","## 후속 상자 조작"],["take","자기 상자에서 지정 수량 꺼내기","자기 상자를 열어 관측한 품목만 실제 가방 목적 슬롯으로 꺼낸다. 부분 이동은 관측 수량만 기록하고 다음 선택을 받는다.","AIInventory.ChestCandidates, AIInventory.Destination, AIInventory.Stale, InventorySkill.Tick, AIInventoryRules.ObservedTransfer","AIInventory.ChestCandidates","## 후속 상자 조작"],["drop","명시 수량 내려놓기","명시 목표에서 정상 DropItem을 한 번 요청하고 자기 슬롯 감소와 이번에 생긴 바닥 물품을 확인한다. trash 삭제를 제공하지 않는다.","AIInventory.DropCandidates, AIInventory.Stale, InventorySkill.Tick","AIInventory.DropCandidates","## 후속 내려놓기·전달"],["give","지정 수령자 전달 결과 관측","보이는 수령자에 접근해 정상 바닥 드롭을 사용한다. 추적 바닥 물품의 지정 수령 요청과 실제 감소량=수령자 일치 품목 증가량이 함께 관측된 수량만 기록한다.","AIInventory.DropCandidates, AIInventory.Recipient, AIInventory.MatchingCount, InventorySkill.Tick","InventorySkill.Tick","## 후속 내려놓기·전달"]];
 const root='../2026-11-07-가방장비구현/';
 const add=(from,to,kind,evidence,cls='해석')=>g.edges.push({from,to,type:'reference',kind,class:cls,evidence,runtimeTrace:false,sourceCommit:g.meta.sourceCommit});
 const planEvidence=g.nodes.find(n=>n.id==='P.bag-equipment').evidence;
 for(const [mode,title,summary,anchors,symbol] of capabilities){
  const id='R.bag-'+mode,loc={path:root+'tree-spec.md',start:25};
  const currentSymbol={equip:'AIInventory.MenuMoves',hold:'AIInventory.TryChosenHand',store:'InventorySkill.TransferTick',take:'InventorySkill.TransferTick',drop:'InventorySkill.DropTick',give:'InventorySkill.DropTick'}[mode];
  const code=g.nodes.filter(n=>n.type==='코드'&&n.kind==='MethodDeclaration'&&n.signature?.startsWith(currentSymbol+'('));
  if(code.length!==1)throw Error('bag anchor missing/ambiguous: '+symbol);
  const capability=g.nodes.find(n=>n.id===id);if(!capability)throw Error('bag planning source missing: '+id);capability.developmentCapability='F.bag-'+mode;
  add(id,code[0].id,'구현 앵커(정적, 실행 미확인)',[loc]);
 }
 for(const symbol of ['AIPlanner.SetGoalFrom','AIBrain.Apply','AIInventory.CheckGoal','AIInventory.ReportJson','AIGoals.FinishChosen','AIPlanner.RequestReplan','AISkill.ObserveOnce']){
  const code=g.nodes.filter(n=>n.kind==='MethodDeclaration'&&n.signature?.startsWith(symbol+'('));
  if(code.length!==1)throw Error('Role/common control anchor missing or ambiguous: '+symbol);
  add('P.bag-agency',code[0].id,'역할·공통 제어 경계의 구현 위치(정적)',planEvidence);
 }

 // Actual tree navigation identities; source correspondence only, not runtime trace.
 const menuNode=g.nodes.find(n=>n.id==='R.bag-navigation');menuNode.developmentCapability='F.bag-navigation';
 for(const symbol of ['AIInventoryMenu.Candidates','AIInventoryMenu.Category','AIInventoryMenu.Items','AIInventoryMenu.ApplyNavigation','AIInventoryMenu.Stale','AIInventoryMenu.NoteLeafSelected','AIInventoryMenu.FinalMove','AIBrain.InventoryChoiceSurface','AIBrain.InventoryMenuControl','AIBrain.Apply','AIBrain.BuildJson']){
  const targets=g.nodes.filter(n=>n.type==='코드'&&n.kind==='MethodDeclaration'&&n.signature?.startsWith(symbol+'('));
  if(targets.length!==1)throw Error('Tree code anchor missing/ambiguous: '+symbol);
  add('R.bag-navigation',targets[0].id,'트리 상태·전달·leaf 구현 위치(정적)',menuNode.evidence);
 }
 for(const symbol of ['AICandidateAssembler.Finish','AICandidateAssembler.LimitFor','AIPickupOptions.Select','AIBrain.CandBuild.AssembleSteps','AIBrain.CandBuild.Enemies','AIBrain.CandBuild.Walls','AIBrain.CandBuild.DigOreSteps','AIBrain.CandBuild.ItemsSteps','AIBrain.CandBuild.ExploreSteps']){
  const targets=g.nodes.filter(n=>n.type==='코드'&&n.kind==='MethodDeclaration'&&(n.signature?.startsWith(symbol+'(')||n.signature?.startsWith(symbol+'<')));
  if(targets.length===0)throw Error('No-cut code anchor missing: '+symbol);
  for(const target of targets)add('R.bag-cap-audit',target.id,'후보 개수 상한 제거 구현 위치(정적)',g.nodes.find(n=>n.id==='R.bag-cap-audit').evidence);
 }
 for(const mode of ['equip','hold','store','take','drop','give'])add('R.bag-'+mode,menuNode.id,'현재 메뉴 leaf의 선택·실행 경계',menuNode.evidence);

 // Consumer-specific policy boundaries.
 for(const [from,to] of [['P.bag-navigation','pol.bag-navigation'],['P.bag-no-cut','pol.choice-no-cut'],['pol.bag-navigation','R.bag-navigation'],['pol.choice-no-cut','R.bag-cap-audit'],['pol.choice-no-cut','R.bag-life-consumers']])add(from,to,'소비자별 승인 정책 적용 경계',g.nodes.find(n=>n.id==='R.bag-life-consumers').evidence);
 // Integrated consumer anchors. Static source correspondence, no runtime trace inference.
 for(const symbol of ['AILife.BlocksInventoryView','AILife.Compose','AILife.NavStale','AILife.LeaveNavigation','AILife.Resolve','AILife.Remaining','AIInventory.CandidatesFor','AIInventory.ExactCandidate','AIGoalFeatures.AddCandidates','AIBrain.Apply','AIBrain.InventoryChoiceSurface']){
  const targets=g.nodes.filter(n=>n.type==='코드'&&n.kind==='MethodDeclaration'&&n.signature?.startsWith(symbol+'('));if(targets.length!==1)throw Error('Integrated consumer anchor missing/ambiguous: '+symbol);
  add('R.bag-life-consumers',targets[0].id,'동일 통합판의 소비자·권한·수량 구현 위치(정적)',g.nodes.find(n=>n.id==='R.bag-life-consumers').evidence);
 }
 const correspondencePath='../2026-11-07-가방장비구현/connected-source-check.json',correspondence=read(correspondencePath);
 if(correspondence.sourceCommit!==evidence.sourceCommit||correspondence.allMatch!==true)throw Error('Integrated connected correspondence mismatch');
 g.bagImplementationEvidence.connectedSourceCorrespondence={...correspondence,currentSourceCommit:g.meta.sourceCommit,sameRevision:correspondence.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:correspondence.sourceCommit===g.meta.sourceCommit};
 return [correspondencePath,evidencePath,...evidence.inputs.map(input=>input.stagedPath),...(acceptedLaterSource?[projectionPath,...projection.integrationSources.map(input=>input.path)]:[])];
}

