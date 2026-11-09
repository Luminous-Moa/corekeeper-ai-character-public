import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../..'),repo='C:/Dev/CoreKeeperModSDK';
const target='7af8f91e5d26e4fd9ce5996809ae41e213340118',old='5cc5a43ff8df91634c04b44bac9f15f849bdb611';
const graph=path.join(work,'지식그래프'),plan=path.join(work,'2026-11/2026-11-07-기획연결'),B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B'),C=path.join(work,'2026-11/2026-11-05-지식그래프-개편/C'),live=path.join(work,'2026-11/2026-11-05-지식그래프-실화면');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex'),json=o=>JSON.stringify(o,null,2)+'\n';
const git=(...args)=>execFileSync('git',['-C',repo,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==target)throw Error('official HEAD changed');
const prep=JSON.parse(fs.readFileSync(path.join(stage,'final-preparation.json'),'utf8'));
for(const x of prep.canonicalFiles)if(sha(fs.readFileSync(x.path))!==x.sha256)throw Error('canonical changed; rebase first '+x.path);
if(git('diff','--name-only','--diff-filter=D',old,target,'--','Assets/AIPlayerSpike','Assets/HeadlessTools','Tools/ai-bridge','Tools/knowledge'))throw Error('deleted source requires cleanup');
const backup=path.join(stage,'before-final-graph');if(fs.existsSync(backup))throw Error('already applied; inspect checkpoint');
fs.mkdirSync(backup,{recursive:true});
for(const [name,p]of [['지식그래프',graph],['기획연결',plan],['B',B],['C',C],['live-B',path.join(live,'B')],['live-C',path.join(live,'C')],['live-D',path.join(live,'D')]])fs.cpSync(p,path.join(backup,name),{recursive:true,filter:p=>!/[\\/](node_modules|bin|obj)([\\/]|$)/.test(p)});
for(const name of ['pin-snapshot.mjs','build.mjs','verify.mjs']){const f=path.join(B,'extractor',name),s=fs.readFileSync(f,'utf8');if(!s.includes(old))throw Error('old commit mismatch '+name);fs.writeFileSync(f,s.replaceAll(old,target));}
fs.appendFileSync(path.join(plan,'source.mjs'),fs.readFileSync(path.join(stage,'final-canonical-addition.mjs.txt'),'utf8'));
const cfgFile=path.join(graph,'tools/config.json'),cfg=JSON.parse(fs.readFileSync(cfgFile,'utf8'));
const files=git('ls-tree','-r','--name-only',target,'Assets/AIPlayerSpike/Scripts').split(/\r?\n/).filter(p=>p.endsWith('.cs'));
for(const p of files)if(!cfg.codeFiles.some(f=>f.path===p))cfg.codeFiles.push({container:'모드(게임 안에서 실행)',path:p});fs.writeFileSync(cfgFile,json(cfg));
const policy=`

### pol.expedition-flow — 채집·이동·귀환의 모델 판단과 실제 실행 연결
- 규칙: 구체 행동 직접 선택과 얕은 대상 탐색을 병행한다. 곁일 여부·범위는 판단모델에 맡기고, 선택 당시 가시 묶음을 고정해 연속 실행한다. 실제 확보량과 원래 부탁·귀환 후속 작업을 보존하며 일반 목표량을 강제 상한으로 만들지 않는다.
- 종류: 원칙
- 등급: 결정
- 확정도: 확정
- 근거: work/2026-11/2026-11-07-채집이동귀환-확정합의.md "곁일 여부와 범위는 판단모델에게 맡긴다."
- 메모: 실제 코드와 오프라인 수용은7af8f91의 별도 참고다. 조명 시야는 pol.expedition-visibility-scope의 현재 범위를 따른다.

### pol.expedition-visibility-scope — 이번 구현은 화면범위 관측, 조명 시야는 후속
- 규칙: 이번 채집·이동·귀환은 기존 화면 범위 관측으로 수용하고 조명·어둠의 실제 사람 시야 동일성은 별도 후속으로 분리한다. 다음 갈림길 방향 선행 읽기를 일반 관측·자원·위험 공개로 확대하지 않는다.
- 종류: 범위
- 등급: 결정
- 확정도: 확정
- 근거: work/2026-11/2026-11-08-채집이동귀환-조명후속분리.md "사용자 결정: “조명 시야는 후속으로 분리”."
- 메모: R1 해결·CPU 근사·AI별 오프스크린 렌더 방식을 확정한 것이 아니다. 설치·실게임·외부 모델 시험은 기존 후속 방침을 따른다.
`;
fs.appendFileSync(path.join(graph,'기획/정책.md'),policy);
const definitions=[
 ['F.expedition-choice','직접·분류 선택과 곁일·원작업 맥락','눈앞의 행동과 분류 탐색을 병행하고 메뉴/곁일 전환 중 원래 부탁과 실제 진행을 보존한다.','AIExpedition.Compose, AIExpedition.Navigate, AIExpedition.Resolve, AIExpedition.Starting, AIExpedition.Report, AIBrain.Apply'],
 ['F.expedition-gather','고정 채집 묶음·관측 확보와 명시 조건','선택한 가시 대상만 접근·채집·회수하며 실제 가방 증가를 관측하고 원부탁의 명시 max_count/max_order 조건을 실제 입력 경계에서 지킨다.','AIGathering.AddCandidates, AIGathering.LimitReason, AIGathering.WallLimit, AIGathering.HarvestLimit, AIGatherSkill.Tick, AIExpedition.BindPlanLimits, AIExpedition.ActiveLimits'],
 ['F.expedition-travel','개인 통행기억과 통로·목적지 이동','정확 관측 통행기억, 외길 조향·순환관측, 다음 갈림길 방향 선행 질문과 물체 접근 tolerance를 연결한다.','AITravel.ObserveView, AITravel.KnownPath, AITravel.Compose, AITravel.TryApplyTurn, TravelSkill.Tick, AITravelRules.PassageExits, AITravelRules.KnownPath'],
 ['F.expedition-report-return','자동 재계획 보고와 귀환·후속 작업','미검토 이탈기회와 전제상실을 중복 억제하여 상위에 보고하고, 부족량·장소와 귀환 목적을 실제 다음 계획 소비에 연결한다.','AIExpedition.Choosing, AIExpedition.AfterBuild, AIExpedition.Sent, AIExpedition.Accepted, AIPlanner.RequestReplan, AIPlanner.OnGoalEnded, AIPlanner.StartNextStep, AIPlanner.SetGoalFrom']
];
for(const [id,title,summary,anchors]of definitions)fs.appendFileSync(path.join(graph,'기획/기능.md'),`\n\n### ${id} — ${title}\n- 상태: 공식7af8f91 구현·오프라인 독립수용·조명후속·미실게임\n- 종류: 제품\n- 한 줄: ${summary}\n- 정책: pol.expedition-flow, pol.expedition-visibility-scope, pol.ai-action-control, pol.original-request-completion\n- 코드 앵커: ${anchors}\n- 근거: work/채집이동귀환-구현자료/review.md "시야 R1을 제외한 이번 구현 범위는 수용 가능하다."\n- 메모: 사용자 조명후속 분리 결정 후 해당 범위로 수용했다. 물리/ECS·transport fixture의 오프라인 검증을 실게임 성공으로 승격하지 않는다.\n`);
const mapping=[
 ['P.expedition-choice','AIExpedition.Compose('],['P.expedition-choice','AIExpedition.Navigate('],['P.expedition-choice','AIBrain.Apply('],
 ['P.expedition-sidework','AIExpedition.Starting('],['P.expedition-sidework','AIExpedition.Resolve('],['P.expedition-sidework','AIExpedition.Report('],
 ['P.expedition-bundle','AIGathering.AddCandidates('],['P.expedition-bundle','AIGatherSkill.Tick('],['P.expedition-bundle','AIGatherSkill.ObserveDiscoveries('],
 ['P.expedition-return','AIExpedition.BindPlanLimits('],['P.expedition-return','AIExpedition.ActiveLimits('],['P.expedition-return','AIExpedition.Observe('],['P.expedition-return','AIGathering.LimitReason('],['P.expedition-return','AIGathering.WallLimit('],['P.expedition-return','AIPlanner.OnGoalEnded('],['P.expedition-return','AIPlanner.StartNextStep('],['P.expedition-return','AIPlanner.SetGoalFrom('],
 ['P.expedition-navigation','AITravel.ObserveView('],['P.expedition-navigation','AITravel.KnownPath('],['P.expedition-navigation','AITravelRules.PassageExits('],['P.expedition-navigation','TravelSkill.Tick('],
 ['P.expedition-lookahead','TravelSkill.Forecast('],['P.expedition-lookahead','TravelSkill.ValidateTurn('],['P.expedition-lookahead','AITravel.TryApplyTurn('],
 ['P.expedition-blocked','AIExpedition.AfterBuild('],['P.expedition-report','AIExpedition.Choosing('],['P.expedition-report','AIExpedition.AfterBuild('],['P.expedition-report','AIPlanner.RequestReplan('],['P.expedition-report','AIPlanReport.ReportJson('],
 ['P.expedition-light-followup','AIVision.CanSee(']
];
fs.writeFileSync(path.join(stage,'new-code-mappings.json'),json({sourceCommit:target,runtimeTrace:false,mapping}));
const planningFile=path.join(live,'D/planning.mjs');let p=fs.readFileSync(planningFile,'utf8');
const extension=`  // 7af8f91 채집·이동·귀환: 정적 구현 대응이며 실제 게임 trace가 아니다.\n  for(const [from,signature] of ${JSON.stringify(mapping)}){const targets=graph.nodes.filter(n=>n.type==='코드'&&n.signature?.startsWith(signature));if(targets.length!==1)throw Error('Expedition anchor absent/ambiguous '+signature);graph.edges.push({from,to:targets[0].id,type:'reference',kind:'채집·이동·귀환 최종 구현 위치',class:'코드 대응',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});}\n  for(const [id,from,title] of [['pol.expedition-flow','P.expedition','채집·이동·귀환의 모델 판단과 실제 실행'],['pol.expedition-visibility-scope','P.expedition-light-followup','이번 화면범위 시야 수용과 조명후속']]){const n=byId.get(from);graph.nodes.push({id,type:'정책',title,summary:n.summary,status:n.status,class:'사용자 결정 요약',sourceLocations:n.sourceLocations,evidence:n.evidence});graph.edges.push({from,to:id,type:'reference',kind:'현재 합의의 정책 요약',class:'합의',evidence:n.evidence});}\n`;
if(!p.includes('  graph.planning ='))throw Error('planning hook absent');fs.writeFileSync(planningFile,p.replace('  graph.planning =',extension+'  graph.planning ='));
const overlayFile=path.join(live,'C/overlay-source.json'),overlay=JSON.parse(fs.readFileSync(overlayFile,'utf8').replace(/^\uFEFF/,''));overlay.baseCommit=target;fs.writeFileSync(overlayFile,json(overlay));
const menu='Assets/AIPlayerSpike/Scripts/AIInventoryMenu.cs',prior='39b22df96dffe869ab0ccd474b18d31462b71d70';
if(git('rev-parse',prior+':'+menu)!==git('rev-parse',target+':'+menu))throw Error('menu changed; needs new projection');
const P=path.join(work,'2026-11/2026-11-05-지식그래프-개편');
const projection={priorEvidenceCommit:prior,currentSourceCommit:target,sameRevision:false,evidenceAppliesToCurrentRevision:false,runtimeTrace:false,scope:'과거39 실행증거는39에 한정. 이번7af8f91 원정의 오프라인/독립검수는 새 근거를 따른다.',unchangedSource:{file:menu,priorBlob:git('rev-parse',prior+':'+menu),currentBlob:git('rev-parse',target+':'+menu),sha256:sha(execFileSync('git',['-C',repo,'cat-file','blob',target+':'+menu]))},integrationSources:['expeditionValidation.md','expeditionFinalReview.md','expeditionLightDecision.md','expeditionIntegration.json'].map(n=>{const f=path.join(stage,'final-source-pins',n);return {path:path.relative(P,f).replaceAll('\\','/'),sha256:sha(fs.readFileSync(f))};})};
fs.writeFileSync(path.join(stage,'successor-projection.json'),json(projection));
const bagFile=path.join(live,'D/bag-source.mjs');let bag=fs.readFileSync(bagFile,'utf8');
const replacements=[
 [" const integrationPath=currentUnrestriction?",` const currentExpedition=g.meta.sourceCommit==='${target}';\n const integrationPath=currentExpedition?'../../채집이동귀환-구현자료/graph/final-source-pins/expeditionIntegration.json':currentUnrestriction?`],
 [" const projectionPath=currentUnrestriction?"," const projectionPath=currentExpedition?'../../채집이동귀환-구현자료/graph/successor-projection.json':currentUnrestriction?"],
 ["||currentUnrestriction);","||currentUnrestriction||currentExpedition);"],
 ["if(!currentUnrestriction&&(","if(!currentUnrestriction&&!currentExpedition&&("],
 ["scopeBoundary:currentUnrestriction?","scopeBoundary:currentExpedition?'39 실행증거의 sameRevision=false를 유지한다. 현재7af8f91 정적대응 및 원정 오프라인 근거는 새 기록, 조명은 사용자 후속분리를 따른다.':currentUnrestriction?"]
];
for(const [a,b]of replacements){if(!bag.includes(a))throw Error('bag source changed '+a);bag=bag.replace(a,b);}fs.writeFileSync(bagFile,bag);
const intro=`> 현재 채집·이동·귀환 코드층 기준은 \`${target}\`다. 공식 ai-character와 동일하다. [이번 반영](../채집이동귀환-구현자료/graph/final.md) 및 사용자 조명후속 분리 범위를 따른다. 실제 조명 동일시야·설치·실게임·외부 모델 성공은 포함하지 않는다.\n\n`;
for(const name of ['README.md','제품그래프-사용법.md']){const f=path.join(graph,name);fs.writeFileSync(f,intro+fs.readFileSync(f,'utf8'));}
const br=path.join(B,'README.md');fs.writeFileSync(br,`> 현재 고정 코드 기준: \`${target}\`. 채집·이동·귀환 구현의 정적 코드층이며 조명 시야는 사용자 결정으로 후속이다. 실게임 trace가 아니다.\n\n`+fs.readFileSync(br,'utf8'));
fs.writeFileSync(path.join(stage,'applied-canonical.json'),json({sourceCommit:target,previous:old,backup,canonicalSourceBeforeSha256:prep.canonicalSourceSha256,canonicalSourceAfterSha256:sha(fs.readFileSync(path.join(plan,'source.mjs'))),codeFiles:files.length,newMappings:mapping.length,codeExtractionCountPlanned:1,officialAtApply:git('rev-parse','HEAD'),lighting:'deferred-by-user-not-fixed',installed:false,liveGame:false}));
console.log('Applied source delta and exact final pin '+target+'; code generation next.');
