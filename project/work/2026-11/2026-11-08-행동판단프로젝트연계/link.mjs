import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {topics} from './topics.mjs';

const folder=path.dirname(fileURLToPath(import.meta.url));
const work=path.resolve(folder,'../..');
const game=path.join(work,'게임행동-그래프');
export const productRoot=path.join(work,'2026-11/2026-11-05-지식그래프-개편');
const relative=file=>path.relative(productRoot,file).replaceAll('\\','/');
const read=file=>JSON.parse(fs.readFileSync(file,'utf8'));
export const foreignId=id=>'game:'+id;
export function runGame(args){
 const command=args.find(arg=>!arg.startsWith('--'));
 if(!['overview','find','target','show','source','gaps','cooking','path','check'].includes(command)||args.some(arg=>arg.startsWith('--')&&!['--variation','--limit','--include-history'].includes(arg)))throw Error('game은 현재 정본의 overview/find/target/show/source/gaps/cooking/path/check 읽기 명령을 받습니다.');
 const python=path.join(process.env.USERPROFILE,'.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe');
 const result=spawnSync(python,['-X','utf8',path.join(game,'graph.py'),...args],{cwd:game,stdio:'inherit'});
 if(result.error)throw result.error;
 return result.status??1;
}
export function addBehavior(graph){
 const canonical=read(path.join(game,'published/graph.json'));
 const existing=new Map(graph.nodes.map(n=>[n.id,n]));
 const ruleSource=relative(path.join(folder,'topics.mjs'));
 const ruleLocations=[{path:ruleSource,start:1,end:topics.length+3}];
 const edge=(from,to,kind)=>graph.edges.push({from,to,type:'reference',kind,class:'개발 참고 색인',runtimeTrace:false,evidence:ruleLocations});
 const root='R.game-behavior';
 graph.nodes.push({id:root,type:'기획 참고',title:'현재 게임 행동 판단 정본',status:'개발용 정적 근거',summary:'전체 게임 정본은 game 명령으로 조회. 주제 연결은 개발 검토 참고이며 모드 지원·실제 실행 증거는 각각 기존 구현과 관측에서 확인한다.',sourceLocations:[{path:relative(path.join(game,'README.md')),start:1,end:20}]});
 edge('P.game-first',root,'현재 정적 게임 근거 조회');
 edge('R.extraction',root,'추출 근거의 현재 파생 조회');
 const labelTopics=new Map();
 const coverage=[];
 for(const topic of topics){
  const id='R.game-topic.'+topic.id;
  for(const label of topic.labels){if(labelTopics.has(label))throw Error('duplicate topic label '+label);labelTopics.set(label,id);}
  const planning=topic.planning.map(id=>{if(!existing.has(id))throw Error('missing planning '+id);return id;});
  const code=topic.signatures.flatMap(signature=>{
   const found=graph.nodes.filter(n=>n.type==='코드'&&n.signature?.startsWith(signature));
   if(found.length!==1)throw Error('missing/ambiguous behavior code anchor '+signature);
   return found.map(n=>n.id);
  });
  graph.nodes.push({id,type:'기획 참고',title:topic.title+' 게임 규칙',status:'주제 관련 개발 참고·지원 여부 별도 확인',summary:'분류 근거는 원본 행위 label/action이다. 코드 연결은 검토 위치를 안내하며 이 범위 전체의 구현이나 성공을 뜻하지 않는다.',sourceLocations:ruleLocations});
  edge(root,id,'게임 규칙 주제');
  for(const from of [...planning,...code])edge(from,id,'관련 게임 규칙 검토');
  coverage.push({id,title:topic.title,planning,code,labels:topic.labels,counts:{},unmatchedLabels:[]});
 }
 const rootKinds=new Set(['source','conditional_relation','condition_consumer','configuration_consumer','configuration_rule','action_completion_rule','gap_assessment','boundary']);
 const primaryKinds=new Set(['verb','claim','action_completion','cooking_pair',...rootKinds]);
 const membership=new Map(canonical.edges.filter(e=>e[1]==='uses_label').map(e=>[e[0],e[2].slice(5)]));
 const unclassified=[];
 for(const n of Object.values(canonical.nodes)){
  if(!primaryKinds.has(n.kind))continue;
  const label=n.kind==='cooking_pair'?'cooking':n.kind==='verb'?n.name:n.action||membership.get(n.id);
  const topic=labelTopics.get(label);
  if(label&&!topic)unclassified.push({id:n.id,kind:n.kind,label});
  const id=foreignId(n.id);
  graph.nodes.push({id,type:'데이터',title:n.name||label||n.id,status:'게임 원본의 정적 근거 참조',summary:n.kind+' / '+n.id,externalBehavior:{id:n.id,kind:n.kind,origin:n.origin??null,sourceKey:n.sourceKey??null,variation:n.variation??null},sourceLocations:ruleLocations,nextQuery:{show:['game','show',n.id],source:n.origin?['game','source',n.id]:null},runtimeTested:false});
  edge(topic||root,id,topic?'해당 주제의 원본 근거':'원본의 대상·보완규칙·미결');
  if(topic){const counts=coverage.find(t=>t.id===topic).counts;counts[n.kind]=(counts[n.kind]||0)+1;}
 }
 const labelsPresent=new Set(Object.values(canonical.nodes).flatMap(n=>[n.name,n.action]).filter(Boolean));
 for(const [from,target] of [
  ['R.expedition-game-evidence','completion:B:melee-pick-2-0:cost-followup-v1'],
  ['R.expedition-game-evidence','door'],
  ['R.expedition-game-evidence','completion:B:place-tile:CAB-2ebcba97367158c9b08f790c25d642c9:-1592303294285069928']
 ]){
  if(!existing.has(from)||!canonical.nodes[target])throw Error('missing recorded exact behavior '+target);
  graph.edges.push({from,to:foreignId(target),type:'reference',kind:'기존 구현 기록에서 실제 조회한 게임 근거',class:'과거 조회 대응',runtimeTrace:false,evidence:existing.get(from).sourceLocations});
  const topic=target.includes('melee-pick')?'R.game-topic.mining':'R.game-topic.move';
  if(!graph.edges.some(e=>e.from===topic&&e.to===foreignId(target))){
   graph.edges.push({from:topic,to:foreignId(target),type:'reference',kind:'해당 주제의 원본 근거',class:'기존 구현 조회 대응',runtimeTrace:false,evidence:existing.get(from).sourceLocations});
   const row=coverage.find(t=>t.id===topic);row.counts[canonical.nodes[target].kind]=(row.counts[canonical.nodes[target].kind]||0)+1;
  }
 }
 for(const row of coverage)row.unmatchedLabels=row.labels.filter(label=>!labelsPresent.has(label));
 if(unclassified.length)throw Error('unclassified action labels: '+JSON.stringify(unclassified));
 graph.behaviorLink={root,topics:coverage,scope:'주제별 개발 참고 및 정확 원본 ID. 모드 지원·실게임 성공은 기존 코드/관측 증거로 판단.',foreignNodes:graph.nodes.filter(n=>n.externalBehavior).length,canonicalNodes:Object.keys(canonical.nodes).length,canonicalEdges:canonical.edges.length,query:'product.mjs game <기존 graph.py 명령>',unclassified};
 const publication=read(path.join(game,'publication.json'));
 return [relative(fileURLToPath(import.meta.url)),ruleSource,...['graph.json','coverage.json','manifest.json'].map(f=>relative(path.join(game,'published',f))),...['publication.json','graph.py','legacy_graph.py','README.md',publication.generator].map(f=>relative(path.resolve(game,f)))];
}
export function behaviorView(graph,id,relatedIds=[]){
 const node=graph.nodes.find(n=>n.id===id);
 if(!node)throw Error('unknown product ID '+id);
 const ids=new Set([id,...relatedIds]);
 const direct=graph.edges.filter(e=>ids.has(e.from)&&e.kind==='관련 게임 규칙 검토').map(e=>e.to);
 const selected=id===graph.behaviorLink.root?graph.behaviorLink.topics:graph.behaviorLink.topics.filter(t=>direct.includes(t.id)||t.id===id);
 return {id,status:graph.behaviorLink.scope,topics:selected.map(t=>({...t,targets:graph.edges.filter(e=>e.from===t.id&&e.kind==='해당 주제의 원본 근거').map(e=>e.to)})),recordedEvidence:graph.edges.filter(e=>ids.has(e.from)&&e.kind==='기존 구현 기록에서 실제 조회한 게임 근거'),wholeGraph:['game','overview'],notice:'각 game: ID의 show에서 원문 ID와 관련 조건·결과·실패를 찾는다. source는 origin이 있는 노드에서 실행한다.'};
}
