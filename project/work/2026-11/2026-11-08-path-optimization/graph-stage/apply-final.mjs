import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const stage = path.dirname(fileURLToPath(import.meta.url));
const work = path.resolve(stage,'../../..');
const repo = 'C:/Dev/CoreKeeperModSDK';
const target = '43c3817fc1a36142c8591fb087ed867665633fe1';
const previous = '7af8f91e5d26e4fd9ce5996809ae41e213340118';
const legacy = path.join(work,'지식그래프');
const planning = path.join(work,'2026-11/2026-11-07-기획연결');
const B = path.join(work,'2026-11/2026-11-05-지식그래프-개편/B');
const C = path.join(work,'2026-11/2026-11-05-지식그래프-개편/C');
const live = path.join(work,'2026-11/2026-11-05-지식그래프-실화면');
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const json = value => JSON.stringify(value,null,2)+'\n';
const git = (...args) => execFileSync('git',['-C',repo,...args],{encoding:'utf8'}).trim();
const read = file => fs.readFileSync(file,'utf8');
if (git('rev-parse','HEAD') !== target) throw Error('Official HEAD changed');
const prepared = JSON.parse(read(path.join(stage,'preparation.json')));
const changed = prepared.canonicalFiles.filter(file => hash(fs.readFileSync(file.path)) !== file.sha256);
if (changed.length) throw Error('Canonical changed, reconcile latest first '+changed.map(x=>x.path).join(', '));
if (git('diff','--name-only','--diff-filter=D',previous,target,'--','Assets/AIPlayerSpike','Assets/HeadlessTools','Tools/ai-bridge','Tools/knowledge')) throw Error('Deleted source needs snapshot reconciliation');
const final = JSON.parse(read(path.join(stage,'final-delta.json')));
const backup = path.join(stage,'before-final-graph');
if (fs.existsSync(backup)) throw Error('Backup exists, inspect applied checkpoint');
for (const [name,source] of [['legacy',legacy],['planning',planning],['B',B],['C',C],['live-B',path.join(live,'B')],['live-C',path.join(live,'C')],['live-D',path.join(live,'D')]]) {
  fs.cpSync(source,path.join(backup,name),{recursive:true,filter:file=>!/[\\/](node_modules|bin|obj)([\\/]|$)/.test(file)});
}
fs.appendFileSync(path.join(planning,'source.mjs'),read(path.join(stage,'final-addition.mjs.txt')));
for (const name of ['pin-snapshot.mjs','build.mjs','verify.mjs']) {
  const file = path.join(B,'extractor',name);
  const source = read(file);
  if (!source.includes(previous)) throw Error('Unexpected B baseline '+name);
  fs.writeFileSync(file,source.replaceAll(previous,target));
}
const stagedB = path.join(stage,'B-shared');
for (const name of ['snapshot','runtime-package']) fs.cpSync(path.join(stagedB,name),path.join(B,name),{recursive:true});
for (const name of ['csharp.raw.json','javascript.raw.json','code.graph.json','B.graph.json','validation.json','boundaries.json','call-forms.json','snapshot.manifest.json','snapshot-verification.json','external-runtime.manifest.json','question-coverage.json']) {
  fs.copyFileSync(path.join(stagedB,name),path.join(B,name));
}
const configFile = path.join(legacy,'tools/config.json');
const config = JSON.parse(read(configFile));
const sourceFiles = git('ls-tree','-r','--name-only',target,'Assets/AIPlayerSpike/Scripts').split(/\r?\n/).filter(file=>file.endsWith('.cs'));
for (const file of sourceFiles) if (!config.codeFiles.some(entry=>entry.path===file)) config.codeFiles.push({container:'모드(게임 안에서 실행)',path:file});
fs.writeFileSync(configFile,json(config));
const renamed = ['DigOre','Breakables','RememberedOres','Items','Follow','Explore','Assemble'];
const featureFile = path.join(legacy,'기획/기능.md');
let features = read(featureFile);
for (const name of renamed) features = features.replaceAll('AIBrain.CandBuild.'+name, 'AIBrain.CandBuild.'+name+'Steps');
features += `\n\n### F.path-preparation — 선택권 보존 경로 준비와 기존 원정 탐색 연결\n- 상태: 공식43c3817 구현·코드/오프라인/Unity 수용·ai2 대표 실기 확인\n- 종류: 제품\n- 한 줄: 기존 원정 트리의 현재 선택면에 필요한 경로와 위험을 준비하고, 많은 목적지의 검색·관측 사실·완료 결과를 공유하며 프레임 사이에 이어 계산한다. 직접 행동과 전체 대상 접근을 보존한다.\n- 정책: pol.choice-no-cut, pol.action-constraints-open, pol.expedition-flow\n- 코드 앵커: AIBrain.PreparationSteps, AIBrain.PreparePlan, AIBrain.BuildCandidateSteps, AIBrain.BuildJsonSteps, AIBrain.CandBuild.ExploreSteps, AIBrain.CandBuild.RememberedOresSteps, AITunnel.PlanBatch.StepSearches, AITunnel.PlanBatch.BeginSinglePlan, AITunnelSearch.SharedSearchJob.Step, AIExpedition.Compose, AIExpedition.Navigate\n- 근거: work/2026-11/2026-11-08-path-optimization/graph-stage/head-acceptance.md "HEAD는 2차 후보"\n- 메모: 3ms 공유 목표 예산이며 단일 작업과 모든 게임 프레임의 상한을 보장하지 않는다. 동률 최단경로 순서와 공유 진단 범위는 달라질 수 있다. 직접 대응만255개를 넘으면 진단 후 전송을 중단한다. 1차 실패와2차 대표 실기 범위·이동 재준비 지연은 별도 원문을 따른다. 최종 설치 상태도 별도 기록한다.\n`;
fs.writeFileSync(featureFile,features);
const mapping = JSON.parse(read(path.join(stage,'shared-verification.json'))).anchors.map(anchor=>['R.path-implementation',anchor.signature]);
const planningFile = path.join(live,'D/planning.mjs');
let planningSource = read(planningFile);
const addition = `  // 43c3817 길 계산과 기존 원정 트리 연결의 정적 대응.\n  for (const [from, signature] of ${JSON.stringify(mapping)}) {\n    const targets = graph.nodes.filter(node => node.type === '코드' && node.signature === signature);\n    if (targets.length !== 1) throw Error('Path implementation anchor absent or ambiguous ' + signature);\n    graph.edges.push({from, to:targets[0].id, type:'reference', kind:'길 계산 최적화 구현 위치', class:'코드 대응', evidence:byId.get(from).sourceLocations, runtimeTrace:false, sourceCommit:graph.meta.sourceCommit});\n  }\n`;
if (!planningSource.includes('  graph.planning =')) throw Error('Planning hook missing');
fs.writeFileSync(planningFile,planningSource.replace('  graph.planning =',addition+'  graph.planning ='));
fs.writeFileSync(path.join(stage,'new-code-mappings.json'),json({sourceCommit:target,mapping,runtimeTrace:false}));
const overlayFile = path.join(live,'C/overlay-source.json');
const overlay = JSON.parse(read(overlayFile).replace(/^\uFEFF/,''));
overlay.baseCommit = target;
fs.writeFileSync(overlayFile,json(overlay));
const prior = '39b22df96dffe869ab0ccd474b18d31462b71d70';
const menu = 'Assets/AIPlayerSpike/Scripts/AIInventoryMenu.cs';
const priorBlob = git('rev-parse',prior+':'+menu);
const currentBlob = git('rev-parse',target+':'+menu);
if (priorBlob !== currentBlob) throw Error('Bag menu changed, projection needs review');
const projection = {priorEvidenceCommit:prior,currentSourceCommit:target,sameRevision:false,evidenceAppliesToCurrentRevision:false,runtimeTrace:false,
  scope:'과거39 가방 실행 증거는 당시 판에 귀속된다. 이번 길 계산 구현과 ai2 대표 실기는 새 원문 범위를 따른다.',
  unchangedSource:{file:menu,priorBlob,currentBlob,sha256:hash(execFileSync('git',['-C',repo,'cat-file','blob',target+':'+menu]))},
  integrationSources:Object.values(final.documents).filter(doc=>['pathAcceptance','pathLiveFinal','pathFinalImplementation'].some(id=>final.documents[id]===doc)).map(doc=>{
    const file=path.resolve(planning,doc.path);
    return {path:path.relative(path.dirname(B),file).replaceAll('\\','/'),sha256:hash(fs.readFileSync(file))};
  })};
fs.writeFileSync(path.join(stage,'successor-projection.json'),json(projection));
const bagFile = path.join(live,'D/bag-source.mjs');
let bag = read(bagFile);
const changes = [
  [" const integrationPath=currentExpedition?",` const currentPathOptimization=g.meta.sourceCommit==='${target}';\n const integrationPath=currentPathOptimization?'../2026-11-08-path-optimization/graph-stage/source-pins/head-acceptance.md':currentExpedition?`],
  [" const projectionPath=currentExpedition?"," const projectionPath=currentPathOptimization?'../2026-11-08-path-optimization/graph-stage/successor-projection.json':currentExpedition?"],
  ["||currentExpedition);","||currentExpedition||currentPathOptimization);"],
  ["!currentUnrestriction&&!currentExpedition&&(","!currentUnrestriction&&!currentExpedition&&!currentPathOptimization&&("],
  ["scopeBoundary:currentExpedition?","scopeBoundary:currentPathOptimization?'39의 가방 증거는 당시 판에 귀속된다. 43c3817의 대표 실기와 한계는 길 계산 최적화 새 근거를 따른다.':currentExpedition?"]
];
for (const [before,after] of changes) {
  if (!bag.includes(before)) throw Error('Bag projection hook changed '+before);
  bag=bag.replace(before,after);
}
for (const name of renamed) bag=bag.replaceAll('AIBrain.CandBuild.'+name,"AIBrain.CandBuild."+name+'Steps');
fs.writeFileSync(bagFile,bag);
const intro = `> 현재 코드층 기준은 \`${target}\`이며 공식 ai-character와 같다. 공유 검색과 기존 원정 트리 연결을 반영했다. ai2 대표 실기와 잔여 준비 지연, 설치 상태는 [길 계산 최적화 반영](../2026-11/2026-11-08-path-optimization/graph-update.md)을 따른다.\n\n`;
for (const name of ['README.md','제품그래프-사용법.md']) {
  const file=path.join(legacy,name);
  fs.writeFileSync(file,intro+read(file));
}
const bReadme=path.join(B,'README.md');
fs.writeFileSync(bReadme,`> 현재 고정 코드 기준은 \`${target}\`다. 공유 검색과 기존 원정 트리 연결의 정적 코드층이다. [이번 반영](../../2026-11-08-path-optimization/graph-update.md)의 대표 실기 범위와 설치 상태를 따른다.\n\n`+read(bReadme));
fs.writeFileSync(path.join(stage,'applied-canonical.json'),json({sourceCommit:target,previous,backup,canonicalInputsMatched:true,officialHead:git('rev-parse','HEAD'),canonicalApplied:true,codeExtraction:'isolated exact SHA Roslyn/Acorn result copied after verified byte determinism',newMappings:mapping.length,sourceFiles:sourceFiles.length,finalInstall:'separate live-owner report confirms 43c3817 and 116 source hashes; no game rerun after final reinstall'}));
console.log('Applied final source delta and exact verified code snapshot.');
