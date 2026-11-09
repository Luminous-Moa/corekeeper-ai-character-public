import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';import {parse} from 'acorn';
const B=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),repo='C:/Dev/CoreKeeperModSDK-choice-fixes',commit='b25099f6fd31e4c4c3618357eb9e8101e0e17124';
const hash=(b,alg='sha256')=>crypto.createHash(alg).update(b).digest('hex');
const write=(f,v)=>fs.writeFileSync(path.join(B,f),JSON.stringify(v,null,2)+'\n');
const manifest=JSON.parse(fs.readFileSync(path.join(B,'snapshot.manifest.json'),'utf8'));
const tree=execFileSync('git',['-C',repo,'ls-tree','-r',commit,'Assets/AIPlayerSpike','Assets/HeadlessTools','Tools/ai-bridge','Tools/knowledge'],{encoding:'utf8'}).trim().split(/\r?\n/);const blobs=new Map(tree.map(s=>{const m=s.match(/^\d+ blob ([a-f0-9]+)\t(.+)$/);return m?[m[2],m[1]]:[];}));
const snapshotResults=[];for(const [root,files]of [['snapshot',manifest.files],['runtime-package',manifest.runtimePackage]])for(const f of files){const bytes=fs.readFileSync(path.join(B,root,f.path)),gitBlob=hash(Buffer.concat([Buffer.from('blob '+bytes.length+'\0'),bytes]),'sha1');snapshotResults.push({root,path:f.path,match:gitBlob===blobs.get(f.path)});}
if(snapshotResults.some(x=>!x.match))throw Error('Snapshot differs from git blob: '+JSON.stringify(snapshotResults.filter(x=>!x.match)));
write('snapshot-verification.json',{commit,allMatch:true,fileCount:snapshotResults.length,files:snapshotResults});
// Hash transitive static imports only. Never load/execute the foreign modules or read their config/secrets.
const extroot='C:/Dev/ai-chatroom',seen=new Set(),todo=['lib/agents/adapters.mjs','lib/agents/cli/index.mjs','lib/agents/run.mjs'],external=[];
while(todo.length){const rel=todo.shift();if(seen.has(rel))continue;seen.add(rel);const file=path.join(extroot,rel);if(!fs.existsSync(file)){external.push({path:rel,missing:true});continue;}const bytes=fs.readFileSync(file);external.push({path:rel,sha256:hash(bytes),bytes:bytes.length});const ast=parse(bytes.toString('utf8'),{sourceType:'module',ecmaVersion:'latest'});for(const n of ast.body){const s=n.source?.value;if(typeof s==='string'&&s.startsWith('.'))todo.push(path.posix.normalize(path.posix.join(path.posix.dirname(rel),s)));}}
const status=execFileSync('git',['-C',extroot,'status','--porcelain','--',...external.map(x=>x.path)],{encoding:'utf8'}).trim();
write('external-runtime.manifest.json',{root:extroot,authority:'Outside pinned SDK commit; boundary only. Hash at verification time, not runtime attestations.',gitHead:execFileSync('git',['-C',extroot,'rev-parse','HEAD'],{encoding:'utf8'}).trim(),workingTreeStatus:status,files:external.sort((a,b)=>a.path<b.path?-1:1)});
const g=JSON.parse(fs.readFileSync(path.join(B,'code.graph.json'),'utf8')),ns=new Map(g.nodes.map(n=>[n.id,n]));
const traverse=new Set(['call','callback','framework-invoked']);
function reach(start){const reached=new Set(),q=[start];while(q.length){const n=q.shift();if(reached.has(n))continue;reached.add(n);for(const e of g.edges)if(e.from===n&&e.type==='reference'&&traverse.has(e.kind))q.push(e.to);}return reached;}
function find(name,filePart=''){const n=g.nodes.find(n=>n.name===name&&n.file?.includes(filePart));if(!n)throw Error('Missing '+name);return n;}
function pathTo(start,end){const q=[[start]],seen=new Set();while(q.length){const p=q.shift(),n=p.at(-1);if(n===end)return p;if(seen.has(n))continue;seen.add(n);for(const e of g.edges)if(e.from===n&&e.type==='reference'&&traverse.has(e.kind))q.push([...p,e.to]);}return null;}
const get='code:Tools/ai-bridge/bridge.mjs#route:GET /v1/health',post='code:Tools/ai-bridge/bridge.mjs#route:POST /v1/plan';
const health=reach(get),plan=find('plan','plan.mjs'),rq=find('runQueries','memgraph.mjs'),rquery=find('runQuery','memgraph.mjs'),statusNode=find('StatusText','AIPlayerSpikeMod.Chat.cs'),members=find('Members','AIWorldSettings.cs');
const goal=find('HandleGoalCommand','AIGoals.cs'),setGoal=find('SetGoal','AIGoals.cs');
const coverage={healthDoesNotReachPlan:!health.has(plan.id),healthReachableFunctions:[...health].sort(),questions:[
 {question:'캐릭터 상태 조회 처리와 읽는 데이터',entry:'slash:ai',path:pathTo(find('TryHandleChatCommand').id,statusNode.id),readPath:pathTo(statusNode.id,members.id),reads:g.edges.filter(e=>e.from===members.id&&e.type==='read')},
 {question:'호스트 목표 입력과 자율 목표의 구분',entry:'slash:ai',path:pathTo(goal.id,setGoal.id),autonomyEntry:'hook:AIPlayerSpikeMod.Update',freeTimeCode:find('FreeTime','AIGoals.cs').id,freeTimePath:pathTo(find('Update','AIPlayerSpikeMod.cs').id,find('FreeTime','AIGoals.cs').id),evidence:{file:goal.file,hostCallLines:[147,156,168,192],hostInterruptionLine:224,freeSourceLine:84,llmSourceFile:'Assets/AIPlayerSpike/Scripts/AIPlanner.cs',llmSetGoalLine:726,llmHostProtectionFunction:find('OnHostGoal','AIPlanner.cs').id},note:'source host/free/llm is code data; no new feature ID invented for higher-level goal activity'},
 {question:'기억 검색 결과 생성',entry:'ai-bridge:POST /v1/plan',path:pathTo(post,rq.id),runQueryPath:pathTo(rq.id,rquery.id),registryTargets:g.edges.filter(e=>e.from===rquery.id&&e.resolution==='finite-object-registry-potential')}
 ]};
if(!coverage.healthDoesNotReachPlan)throw Error('Health reaches plan');if(coverage.questions.some(q=>q.path===null||q.readPath===null||q.runQueryPath===null))throw Error('Question path missing');
write('question-coverage.json',coverage);
console.log(JSON.stringify({snapshotFiles:snapshotResults.length,externalFiles:external.length,healthDoesNotReachPlan:coverage.healthDoesNotReachPlan,questions:coverage.questions.length}));

