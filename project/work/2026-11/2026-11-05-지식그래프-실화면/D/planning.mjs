import path from 'node:path';
import fs from 'node:fs';
import {abs} from './live-lib.mjs';
import {addBagImplementation} from './bag-source.mjs';
import {compile} from '../../2026-11-07-기획연결/compile.mjs';

const folder = '../2026-11-07-기획연결';
const productPath = relative => path.posix.normalize(path.posix.join(folder, relative));

export function addPlanning(graph) {
  const plan = compile();
  const location = evidence => ({
    ...evidence,
    path: productPath(evidence.path),
    start: evidence.line,
    end: evidence.end
  });
  for (const node of plan.nodes) {
    const evidence = node.evidence.map(location);
    graph.nodes.push({
      ...node,
      type: node.layer === '재기획' ? '기획 항목' : '기획 참고',
      evidence,
      sourceLocations: evidence.map(({path, start, end}) => ({path, start, end}))
    });
  }
  for (const edge of plan.edges) {
    graph.edges.push({
      ...edge,
      type: 'reference',
      kind: edge.type,
      evidence: edge.evidence.map(location)
    });
  }
  const entry = plan.entry;
  for (const [to, kind] of [[entry, '프로젝트 최종 목표'], ['P.experience', '제작 의도']]) {
    graph.edges.push({
      from: 'CORE-01', to, type: 'reference', kind,
      class: '색인', evidence: graph.nodes.find(n => n.id === to).evidence
    });
  }
  const byId = new Map(graph.nodes.map(n => [n.id, n]));
  for (const [id, planningId, title, implementationId] of [
    ['pol.ai-action-control', 'P.valid-action', 'AI가 행동의 계속·중단·전환을 선택', 'R.stop-action'],
    ['pol.original-request-completion', 'P.original-request', '중간 진척과 원래 부탁 완료를 구분', 'R.finish-goal']
  ]) {
    const decision = byId.get(planningId);
    graph.nodes.push({id, type: '정책', title, summary: decision.summary,
      class: '사용자 결정 요약', status: '2026-11-07 합의',
      sourceLocations: decision.sourceLocations, evidence: decision.evidence});
    graph.edges.push({from: planningId, to: id, type: 'reference', kind: '정책으로 구체화',
      class: '합의', evidence: decision.sourceLocations});
    graph.edges.push({from: id, to: implementationId, type: 'reference', kind: '구현 계약',
      class: '코드 대응', evidence: byId.get(implementationId).sourceLocations});
  }
  for (const [from, signature] of [
    ['R.common-implementation', 'AIGoals.CheckDone('],
    ['R.common-implementation', 'AIBrain.ObserveExecution(string)'],
    ['R.stop-action', 'AIBrain.Apply('],
    ['R.stop-action', 'AIBrain.CancelPendingChoices('],
    ['R.stop-action', 'AIActions.Stale('],
    ['R.finish-goal', 'AIGoals.FinishChosen('],
    ['R.finish-goal', 'AIHostOrders.GoalEnded('],
    ['R.request-plan', 'AIPlanner.RequestReplan(']
  ]) {
    const targets = graph.nodes.filter(n => n.type === '코드' && n.signature?.startsWith(signature));
    if (targets.length !== 1) throw Error('Common execution code anchor is ambiguous or absent: ' + signature);
    graph.edges.push({from, to: targets[0].id, type: 'reference', kind: '685dde4 구현 입구',
      class: '코드 대응', evidence: byId.get(from).sourceLocations,
      runtimeTrace: false, sourceCommit: graph.meta.sourceCommit});
  }
  for (const [from, to] of [
    ['P.original-request', 'UA-03'], ['P.life', 'UA-04'],
    ['P.knowledge', 'UA-06'], ['P.noncombat-choice', 'pol.behavior-roles']
  ]) {
    graph.edges.push({
      from, to, type: 'reference', kind: '기존 기획 참고', class: '해석',
      evidence: [...byId.get(from).sourceLocations, ...byId.get(to).sourceLocations]
    });
  }
  // 生活 트리: 수용된 최신 정적 코드 대응. 함수 실행 trace를 뜻하지 않는다.
  for (const [from, signature] of [["P.life-tree","AILife.Compose("],["P.life-tree-readonly","AIBrain.HandleResponse("],["P.life-tree-readonly","AILife.Navigate("],["P.life-tree-direct","AILife.Resolve("],["P.life-tree-direct","AIFarm.ExactCandidate("],["P.life-tree-direct","AICook.ExactCandidate("],["P.life-tree-direct","AICraft.ExactCandidate("],["P.life-tree-direct","AIInventory.CandidatesFor("],["P.life-tree-count","AIPlanProtocol.Read("],["P.life-tree-count","AILife.Remaining("],["P.life-tree-result","AILife.Ended("],["P.life-tree-result","LifeProgress.Record("]]) {
    const targets=graph.nodes.filter(n=>n.type==='코드'&&n.signature?.startsWith(signature));
    if(targets.length!==1)throw Error('Life code anchor absent or ambiguous: '+signature);
    graph.edges.push({from,to:targets[0].id,type:'reference',kind:'c4a0f5e 생활 구현 입구',class:'코드 대응',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});
  }
  const bagSources=addBagImplementation(graph);
  // 새 정비 delta의 실제 정적 코드 대응; 이전 생활/가방 검수의 반복이 아니다.
  for(const [from, signature] of [["P.maintenance","AIMaintenance.LifeEntries("],["P.maintenance","AIMaintenance.ExactCandidate("],["P.maintenance-single","AIMaintenance.ReadCosts("],["P.maintenance-single","AIMaintenance.Post("],["P.maintenance-pending","AIMaintenance.Recovery("],["P.maintenance-pending","AIMaintenance.CleanupIntent("],["P.maintenance-pending","AIMaintenance.SlotBarrier("],["P.maintenance-result","AIMaintenance.ConfirmRepair("],["P.maintenance-result","AIMaintenance.ReceivedIdentity("],["P.maintenance-result","AIMaintenanceRules.UpgradeConfirmed("]]){
    const targets=graph.nodes.filter(n=>n.type==='코드'&&n.signature?.startsWith(signature));
    if(targets.length!==1)throw Error('Maintenance anchor absent or ambiguous: '+signature);
    graph.edges.push({from,to:targets[0].id,type:'reference',kind:'5a05b80 정비 구현 입구',class:'코드 대응',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});
  }
  // 임의 제약 해제 최종 후보의 정적 구현 대응. 실행 trace가 아니다.
  for(const [from,signature] of [["P.unrestricted-work","AIPlanner.CheckGoal("],["P.unrestricted-work","AIHostOrders.Add("],["P.unrestricted-work","AICraft.ReadRecipe("],["P.unrestricted-choice","AILife.Compose("],["P.unrestricted-choice","AIBrain.CandBuild.Walls("],["P.unrestricted-space","AIFarm.ExactCandidate("],["P.unrestricted-space","AIPlaces.ShapeTiles("],["P.unrestricted-information","AIPlanner.AddEvent("],["P.unrestricted-guards","AIKnowledge.KnownItems("],["P.unrestricted-support","AICombatOptions.Candidates("],["P.unrestricted-support","AIWeaponInput.Build("],["P.unrestricted-support","AIWeaponUseControl.Step("],["R.unrestriction-actions","AttackSkill.Tick("],["R.unrestriction-craft","AICraft.ExactCandidate("]]){const targets=graph.nodes.filter(n=>n.type==='코드'&&n.signature?.startsWith(signature));if(targets.length!==1)throw Error('Unrestriction anchor absent/ambiguous '+signature);graph.edges.push({from,to:targets[0].id,type:'reference',kind:'제약해제 최종후보 구현 위치',class:'코드 대응',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});}
  // 후속 사용자 해제 지시를 현재 정책으로 드러내되 과거 규칙/수치 원문은 보존한다.
  const openDirective=byId.get('P.unrestricted-actions');
  graph.nodes.push({id:'pol.action-constraints-open',type:'정책',title:'임의 행동·정보 제약 전반 해제',summary:openDirective.summary,status:'사용자 지시·구현 수용은 별도',class:'사용자 결정 요약',sourceLocations:openDirective.sourceLocations,evidence:openDirective.evidence});
  graph.edges.push({from:'P.unrestricted-actions',to:'pol.action-constraints-open',type:'reference',kind:'후속 사용자 지시의 정책 요약',class:'합의',evidence:openDirective.evidence});
  for(const [id,current] of [['pol.nl-plan-steps','P.unrestricted-work'],['pol.place-where-anywhere','P.unrestricted-information'],['pol.place-craft-list','P.unrestricted-information']]){
    let policy=graph.nodes.find(n=>n.id===id);const directive=byId.get(current);
    if(!policy){const policyPath='../../지식그래프/기획/정책.md',raw=fs.readFileSync(abs(policyPath),'utf8'),at=raw.indexOf('### '+id+' — ');if(at<0)throw Error('Historical policy absent '+id);const body=raw.slice(at).split('\n### ')[0],start=raw.slice(0,at).split('\n').length;policy={id,type:'정책',title:body.split('\n')[0].split(' — ')[1],rule:body.match(/^- 규칙: (.*)$/m)[1],summary:body.match(/^- 규칙: (.*)$/m)[1],notes:[...body.matchAll(/^- 메모: (.*)$/gm)].map(m=>m[1]),status:'과거 사용자 합의·구현상한메모는후속대체',class:'결정',sourceLocations:[{path:policyPath,start,end:start+body.trimEnd().split('\n').length-1}]};graph.nodes.push(policy);}
    policy.applicationNote=(policy.applicationNote?policy.applicationNote+' ':'')+'과거 메모의 구현 상한은 후속 사용자 제약해제 지시로 대체된다. 현재 구현 수용·실게임 성공은 별도 근거를 따른다.';
    policy.sourceLocations=[...(policy.sourceLocations??[]),...directive.sourceLocations];
    graph.edges.push({from:current,to:id,type:'reference',kind:'과거 구현 상한 메모의 후속 대체 범위',class:'합의',evidence:directive.evidence});
  }
  // 7af8f91 채집·이동·귀환: 정적 구현 대응이며 실제 게임 trace가 아니다.
  for(const [from,signature] of [["P.expedition-choice","AIExpedition.Compose("],["P.expedition-choice","AIExpedition.Navigate("],["P.expedition-choice","AIBrain.Apply("],["P.expedition-sidework","AIExpedition.Starting("],["P.expedition-sidework","AIExpedition.Resolve("],["P.expedition-sidework","AIExpedition.Report("],["P.expedition-bundle","AIGathering.AddCandidates("],["P.expedition-bundle","AIGatherSkill.Tick("],["P.expedition-bundle","AIGatherSkill.ObserveDiscoveries("],["P.expedition-return","AIExpedition.BindPlanLimits("],["P.expedition-return","AIExpedition.ActiveLimits("],["P.expedition-return","AIExpedition.Observe("],["P.expedition-return","AIGathering.LimitReason("],["P.expedition-return","AIGathering.WallLimit("],["P.expedition-return","AIPlanner.OnGoalEnded("],["P.expedition-return","AIPlanner.StartNextStep("],["P.expedition-return","AIPlanner.SetGoalFrom("],["P.expedition-navigation","AITravel.ObserveView("],["P.expedition-navigation","AITravel.KnownPath("],["P.expedition-navigation","TravelSkill.Tick("],["P.expedition-lookahead","TravelSkill.ValidateTurn("],["P.expedition-lookahead","AITravel.TryApplyTurn("],["P.expedition-blocked","AIExpedition.AfterBuild("],["P.expedition-report","AIExpedition.Choosing("],["P.expedition-report","AIExpedition.AfterBuild("],["P.expedition-report","AIPlanner.RequestReplan("],["P.expedition-report","AIPlanReport.ReportJson("],["P.expedition-light-followup","AIVision.CanSee("]]){const targets=graph.nodes.filter(n=>n.type==='코드'&&n.signature?.startsWith(signature));if(targets.length!==1)throw Error('Expedition anchor absent/ambiguous '+signature);graph.edges.push({from,to:targets[0].id,type:'reference',kind:'채집·이동·귀환 최종 구현 위치',class:'코드 대응',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});}
  for(const [id,from,title] of [['pol.expedition-flow','P.expedition','채집·이동·귀환의 모델 판단과 실제 실행'],['pol.expedition-visibility-scope','P.expedition-light-followup','이번 화면범위 시야 수용과 조명후속']]){const n=byId.get(from);graph.nodes.push({id,type:'정책',title,summary:n.summary,status:n.status,class:'사용자 결정 요약',sourceLocations:n.sourceLocations,evidence:n.evidence});graph.edges.push({from,to:id,type:'reference',kind:'현재 합의의 정책 요약',class:'합의',evidence:n.evidence});}
  // 49bd748: 공식 91445e2(지역·출구 단위 이동)에서 AITravelRules.PassageExits·TravelSkill.Forecast가 없어져 위 원정 정적 대응에서 그 두 줄을 뺐고, AIExpedition.Compose의 bool 매개변수 추가에 맞춰 길 계산 대응 서명을 고쳤다.
  // 43c3817 길 계산과 기존 원정 트리 연결의 정적 대응.
  for (const [from, signature] of [["R.path-implementation","AIBrain.PreparePlan(World, Entity, int2, bool)"],["R.path-implementation","AIBrain.PreparationSteps(AIBrain.Preparation, bool)"],["R.path-implementation","AIBrain.BuildCandidateSteps(World, Entity, AIObservation, bool, bool, System.Action<System.Collections.Generic.List<AICandidate>>)"],["R.path-implementation","AIBrain.BuildJsonSteps(string, System.Collections.Generic.List<AICandidate>, System.Action<string>)"],["R.path-implementation","AIBrain.CandBuild.ExploreSteps()"],["R.path-implementation","AIBrain.CandBuild.RememberedOresSteps()"],["R.path-implementation","AITunnel.PlanBatch.StepSearches(int)"],["R.path-implementation","AITunnel.PlanBatch.BeginSinglePlan(int2, int, bool, bool)"],["R.path-implementation","AIExpedition.Compose(World, Entity, AIObservation, System.Collections.Generic.List<AICandidate>, ExpeditionQuery, bool)"],["R.path-implementation","AIExpedition.Navigate(AICandidate)"],["R.path-implementation","AITunnelSearch.SharedSearchJob.Step(int)"]]) {
    const targets = graph.nodes.filter(node => node.type === '코드' && node.signature === signature);
    if (targets.length !== 1) throw Error('Path implementation anchor absent or ambiguous ' + signature);
    graph.edges.push({from, to:targets[0].id, type:'reference', kind:'길 계산 최적화 구현 위치', class:'코드 대응', evidence:byId.get(from).sourceLocations, runtimeTrace:false, sourceCommit:graph.meta.sourceCommit});
  }
  // 관측·재판단20b1aced 정적 구현 대응과 현재 정책.
  for (const [from, signature] of [["R.observation-implementation","AISkill.BindObservationGoal()"],["R.observation-implementation","AISkill.BeginObservationTick()"],["R.observation-implementation","AISkill.RefreshPendingObservation(float)"],["R.observation-implementation","AIBrain.OnGoalObservationChanged()"],["R.observation-implementation","AIBrain.ObserveIncompleteCompletion(string, float)"],["R.observation-implementation","AIGoals.FinishChosen(World, Entity, float)"],["R.observation-implementation","AIGoals.RefreshCompletion(World, Entity, float)"],["R.observation-implementation","AIPlanReport.ReportJson(World, Entity, string, out int)"],["R.observation-delivery","InventoryDeliverySourceObservationSystem.OnUpdate()"],["R.observation-delivery","InventoryDeliveryObservationSystem.OnUpdate()"],["R.observation-delivery","InventorySkill.CaptureDelivery(World, bool)"],["R.observation-delivery","InventorySkill.DropTick(World, Entity, float)"],["R.observation-delivery","AIInventory.Completed(World, Entity, AIInventoryMove, string, int)"],["R.observation-upper-completion","AIPlanner.OnOrder(World, Entity, float, PlanAnswerJ, bool, bool, string, out string, out string)"],["R.observation-upper-completion","AIPlanner.ReportCompletionConflict(World, Entity, string, string)"],["R.observation-upper-completion","AIGoals.CompletionPending(World, Entity, float, out string)"],["R.observation-upper-completion","AIPlanner.RequestReplan(World, Entity, string)"]]) {
    const targets=graph.nodes.filter(node=>node.type==='코드'&&node.signature===signature);
    if(targets.length!==1)throw Error('Observation anchor absent or ambiguous '+signature);
    graph.edges.push({from,to:targets[0].id,type:'reference',kind:'관측·재판단 구현 위치',class:'코드 대응',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});
  }
  const observationPolicy=byId.get('P.observation-completion-check');
  graph.nodes.push({id:'pol.completion-observation-check',type:'정책',title:'완료 선택과 실제 관측의 대조·재판단',summary:observationPolicy.summary,status:'사용자 후속 결정',class:'사용자 결정 요약',sourceLocations:observationPolicy.sourceLocations,evidence:observationPolicy.evidence});
  graph.edges.push({from:'P.observation-completion-check',to:'pol.completion-observation-check',type:'reference',kind:'현재 후속 계약의 정책 요약',class:'합의',evidence:observationPolicy.evidence});
  const observed=byId.get('R.observation-live');
  observed.currentSourceCommit=graph.meta.sourceCommit;
  observed.sameRevision=observed.sourceCommit===graph.meta.sourceCommit;
  observed.evidenceAppliesToCurrentRevision=observed.sameRevision;
  // 빈 선택지·상자 공용·상자 열림 모드(49bd748) 정적 구현 대응과 현재 정책.
  for (const [from, signature] of [["P.chests-shared","AIChestAreas.Step<T>(System.Collections.Generic.List<T>, System.Func<T, int2>, string, System.Func<T, float>)"],["P.chests-shared","AIChestAreas.Within<T>(System.Collections.Generic.List<T>, System.Func<T, int2>, string)"],["P.chests-shared","AIInventory.KnownChestWhere(string)"],["P.chests-shared","AICraft.CraftChests(World, Entity, Entity)"],["P.chests-shared","AICraft.MaterialCount(World, Entity, Entity, ObjectID)"],["P.chest-open-mode","AIChestMode.Active(World, Entity, int, int, System.Func<int, int, int>, out Entity)"],["P.chest-open-mode","AIChestMode.End(string)"],["P.chest-open-mode","AIInventoryMenu.ChestModeCandidates(World, Entity, AIObservation, string, Entity)"],["P.chest-open-mode","AIInventoryMenu.LeaveChestMode(string)"],["P.combat-entry","AIChestMode.FightStarted(int, int, int, System.Func<int, int, int>)"],["P.maintenance-abandon","AIMaintenance.ObserveLoss(World, Entity, AIMaintenance.Pending)"],["P.maintenance-abandon","AIMaintenance.AbandonCandidate(World, Entity, AIMaintenance.State)"],["P.maintenance-abandon","AIMaintenance.Abandon(World, Entity, MaintenanceRequest)"],["P.maintenance-abandon","MaintenanceAbandonSkill.Tick(World, Entity, float)"],["P.maintenance-abandon","AIMaintenance.LostWho(AIMaintenance.Pending)"],["P.maintenance-abandon","AIMaintenance.PlayersNear(World, Entity, int2)"],["P.gap-bag","AIInventory.Pouches(World, Entity)"],["P.gap-bag","AIInventoryBulk.Moves(World, Entity, string, Entity)"],["P.gap-bag","AIInventoryBulk.ActivePreset(World, Entity)"],["P.gap-bag","AIInventory.UseKind(ObjectInfo)"],["P.gap-bag","InventorySkill.UseTick(World, Entity, float)"],["P.gap-bag","AIInventory.SwapMove(World, Entity, string, int, int, string, AIGoal)"],["P.gap-bag","AIInventory.CrossSwap(World, Entity, Entity, int, int, string, string, string, Entity)"],["P.gap-life","AIBed.ExactCandidate(World, Entity, AIObservation, string, LifeSelection)"],["P.gap-life","SleepSkill.Tick(World, Entity, float)"],["P.gap-life","AIFarm.SeederCandidate(World, Entity, AIObservation, LifeSelection, AIFarm.Snap, System.Collections.Generic.List<int2>)"],["P.gap-life","AIFarm.AttackHarvestCandidate(World, Entity, AIObservation, LifeSelection, AIFarm.Snap, System.Collections.Generic.List<int2>)"],["P.gap-life","AICook.ExactCandidate(World, Entity, AIObservation, string, LifeSelection)"],["P.gap-maintenance","AIMaintenance.Recovery(World, Entity, LifeSelection, AIMaintenance.State)"],["P.gap-maintenance","AIMaintenance.Rebind(World, AIMaintenance.Pending, ObjectID)"],["P.gap-maintenance","AIMaintenance.ObservePending(World, Entity, AIMaintenance.State)"]]) {
    const targets=graph.nodes.filter(node=>node.type==='코드'&&node.signature===signature);
    if(targets.length!==1)throw Error('Choice gap anchor absent or ambiguous '+signature);
    graph.edges.push({from,to:targets[0].id,type:'reference',kind:from==='P.combat-entry'?'임시 대리 신호(전투 진입 미구현)':'빈 선택지 구현 위치',class:'코드 대응',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});
  }
  for (const [id,title,from] of [["pol.chests-shared","모든 상자 공용","P.chests-shared"],["pol.chest-open-mode","상자 열림 모드","P.chest-open-mode"],["pol.combat-entry","전투 상태 진입 기준(전투 구현 시작 때 사용자 재확인)","P.combat-entry"],["pol.maintenance-abandon","회수 불가 정비 장비의 포기와 상위 알림","P.maintenance-abandon"],["pol.choice-gaps","범위 안의 빈 선택지를 판단 모델 후보로 채움","P.choice-gaps"]]) {
    const plan=byId.get(from);
    graph.nodes.push({id,type:'정책',title,summary:plan.summary,status:id==='pol.combat-entry'?'사용자 구상·전투 구현 시작 때 재확인':'사용자 결정',class:'사용자 결정 요약',sourceLocations:plan.sourceLocations,evidence:plan.evidence});
    graph.edges.push({from,to:id,type:'reference',kind:'현재 결정의 정책 요약',class:'합의',evidence:plan.evidence});
  }
  if(!byId.get('pol.bag-boundary')&&!graph.nodes.some(n=>n.id==='pol.bag-boundary'))throw Error('Missing pol.bag-boundary for replacement edge');
  graph.edges.push({from:'pol.chests-shared',to:'pol.bag-boundary',type:'reference',kind:'대체: 상자 접근 부분(trash 경계는 유지)',class:'합의',evidence:byId.get('P.chests-shared').evidence});
  graph.planning = {entry, core: 'CORE-01', nodes: plan.nodes.length,
    relations: plan.edges.length, sourceDocuments: Object.keys(plan.sources).length};
  return ['source.mjs', 'compile.mjs', ...Object.values(plan.sources).map(s => s.path)].map(productPath).concat('../2026-11-05-지식그래프-실화면/D/bag-source.mjs','../../지식그래프/기획/정책.md',...bagSources);
}


