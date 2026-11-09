import path from 'node:path';
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
    ['R.common-implementation', 'AIBrain.ObserveExecution('],
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
  graph.planning = {entry, core: 'CORE-01', nodes: plan.nodes.length,
    relations: plan.edges.length, sourceDocuments: Object.keys(plan.sources).length};
  return ['source.mjs', 'compile.mjs', ...Object.values(plan.sources).map(s => s.path)].map(productPath);
}
