import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url));
const result=path.dirname(stage);
const work=path.resolve(stage,'../../..');
const plan=path.join(work,'2026-11/2026-11-07-기획연결');
const candidateRepo='C:/Dev/CoreKeeperModSDK-observation-redecision';
const repo='C:/Dev/CoreKeeperModSDK';
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
const write=(name,value)=>fs.writeFileSync(path.join(stage,name),JSON.stringify(value,null,2)+'\n');
const git=(root,...args)=>execFileSync('git',['-C',root,...args],{encoding:'utf8'}).trim();
const candidate=git(candidateRepo,'rev-parse','HEAD');
const current=await import(pathToFileURL(path.join(plan,'source.mjs')).href);
const documents={};
const pins=[];
fs.mkdirSync(path.join(stage,'source-pins'),{recursive:true});
for(const[id,name,layer]of [
  ['observationAgreement','agreement.md','재기획'],
  ['observationImplementation','implementation.md','참고'],
  ['observationReceipt','give-receipt-implementation.md','참고'],
  ['observationReview','review-code.md','참고'],
  ['observationReviewedHashes','review-code-sha256.json','참고']
]){
  const original=path.join(result,name),pinned=path.join(stage,'source-pins',name),bytes=fs.readFileSync(original);
  fs.writeFileSync(pinned,bytes);
  documents[id]={layer,path:path.relative(plan,pinned).replaceAll('\\','/'),originalPath:path.relative(plan,original).replaceAll('\\','/')};
  pins.push({id,original,pinned,sha256:hash(bytes)});
}
const ev=(doc,anchor)=>({doc,anchor});
const nodes=[
  {id:'P.observation-completion-check',title:'완료 선택과 실제 미완료 사실을 대조하고 재판단',layer:'재기획',status:'합의·현재 후속 요청',summary:'모델이 완료를 선택했어도 실제 부족·미확인이면 현재 사실을 알리고 판단모델을 다시 호출한다. 관측은 목표·단계·대상·실행 회차에 귀속하며 원래 부탁과 미완료 의무를 보존한다. 계속·중단·전환의 AI 선택권을 유지하고 실제 완료 사실과 미완료를 알고 중단하는 의사를 구별한다.',evidence:[ev('observationAgreement','사용자는 모델이 완료를 선택했더라도'),ev('observationAgreement','## 구현 기준')]},
  {id:'R.observation-implementation',title:'관측 소속·완료 적용 직전 사실 검사·지속 대기 재검토 후보',layer:'참고',status:'참고·코드/오프라인 수용 후보·실기 대기',sourceCommit:candidate,summary:'현재 몸 Tick에서 다시 확인된 대기 사실을 다음 Brain에서 읽고 같은 pending은5초 간격으로 재검토한다. 진행 중 요청·준비·미소비 사건은 중복 알림을 막고 새 사실은 즉시 전달한다. 목표 세대·이름·run을 입력과 상위 보고에 유지한다. finish_goal은 현재 수량·도착·수령을 다시 읽어 부족·미확인이면 단계 advance를 보류한다. 측정 완료 조건 없는 탐색·따르기의 종료와 stop_action/request_plan은 유지한다.',evidence:[ev('observationImplementation','## 실행 관측'),ev('observationImplementation','## 단계 완료'),ev('observationReview','## 수용 근거')]},
  {id:'R.observation-delivery',title:'sender·floor·recipient 귀속과 실제 소비를 대조하는 Give 관측',layer:'참고',status:'참고·생산 observer 구현·실기 순서 미확인',sourceCommit:candidate,summary:'pickup 이전 sender 표식·정확 물품·자기 슬롯 감소·새 유일 floor를 귀속하고 inventory 소비 이전 동일 floor의 지정 recipient receipt를 관측한다. 이후 실제 floor 소비와 수령자 증가를 맞춘다. 수령 수량은 floor 포착과 독립적으로 매 Tick 측정하고 누락은 미관측으로 표시한다. 송신 drop을 자동 반복하지 않으며 이전 목표의 완료가 새 목표 수령량을 올리지 않게 한다.',evidence:[ev('observationReceipt','## 실제 처리 경계'),ev('observationReceipt','## 변경한 관측'),ev('observationReview','## 게임 처리 순서 독립 대조')]},
  {id:'R.observation-review',title:'관측·재판단 코드와 생산 경로 오프라인 독립 수용',layer:'참고',status:'참고·독립 코드/오프라인 수용·Unity/실기 별도',sourceCommit:candidate,summary:'실제 실행·관측·완료·계획 메서드156개, inventory/observer171개, travel51개를 독립 통과했다. Brain→Body의50ms·2초 순서, 이전 acknowledgment와 새 관측, 타인 동일 물품·지정 외 pickup·복수 floor·즉시 착용·cleanup 반례를 확인했다. HTTP와 ECS consumer/정렬은 대역이며 실제 지정 수령·후속 부위 실행·조용한 통로 재판단·새 목표 관측 분리는 실기로 확인해야 한다.',evidence:[ev('observationReview','## 판정'),ev('observationReview','## 직접 확인한 결과'),ev('observationReview','## 남은 검증')]}
];
const edge=(from,to,type,doc,anchor)=>({from,to,type,class:'해석',evidence:[ev(doc,anchor)]});
const edges=[
  edge('P.valid-action','P.observation-completion-check','계속·중단·전환 권한을 유지하는 후속 완료 확인','observationAgreement','- 계속·중단·전환은 AI가 선택한다.'),
  edge('P.original-request','P.observation-completion-check','미완료 의무와 원래 부탁의 보존','observationAgreement','- 모델의 완료 선택과 실제 관측이 맞지 않으면'),
  edge('P.observation-completion-check','R.finish-goal','참고: 기존 명시 단계 완료 계약의 후속 보완','observationAgreement','현재 요청에 따라 완료 선언과 실제 사실이 어긋날 때의 재판단 흐름을 보완한다.'),
  edge('P.observation-completion-check','R.observation-implementation','참고: 실제 관측과 재판단 구현','observationImplementation','## 단계 완료'),
  edge('P.observation-completion-check','R.observation-delivery','참고: 정확한 수령 관측 구현','observationReceipt','## 변경한 관측'),
  edge('P.observation-completion-check','R.observation-review','참고: 독립 검수와 남은 실기','observationReview','## 판정'),
  edge('R.observation-implementation','R.observation-delivery','참고: 완료 사실의 전달 관측 소비','observationReceipt','## 변경한 관측')
];
const ids=new Set(current.nodes.map(node=>node.id));
for(const node of nodes){if(ids.has(node.id))throw Error('Existing delta requires reconciliation '+node.id);ids.add(node.id);}
for(const item of edges)if(!ids.has(item.from)||!ids.has(item.to))throw Error('Missing endpoint');
const addition='\n// 사용자 후속 완료 확인 요청과 관측·재판단 구현 근거.\nObject.assign(documents,'+JSON.stringify(documents,null,2)+');\nnodes.push(...'+JSON.stringify(nodes,null,2)+');\nedges.push(...'+JSON.stringify(edges,null,2)+');\n';
write('delta.json',{candidate,documents,nodes,edges,canonicalApplied:false});
write('source-pins.json',pins);
fs.writeFileSync(path.join(stage,'canonical-addition.mjs.txt'),addition);
const isolated=path.join(stage,'planning');
fs.mkdirSync(isolated,{recursive:true});
for(const name of ['compile.mjs','graph.mjs'])fs.copyFileSync(path.join(plan,name),path.join(isolated,name));
const absolute=Object.fromEntries(Object.entries({...current.documents,...documents}).map(([id,doc])=>[id,path.resolve(plan,doc.path).replaceAll('\\','/')]));
fs.writeFileSync(path.join(isolated,'source.mjs'),fs.readFileSync(path.join(plan,'source.mjs'),'utf8')+addition+'\nconst isolatedPaths='+JSON.stringify(absolute)+';\nfor(const[id,doc]of Object.entries(documents))doc.path=isolatedPaths[id];\n');
const prior=JSON.parse(fs.readFileSync(path.join(work,'2026-11/2026-11-08-path-optimization/graph-stage/final-verification.json'),'utf8'));
const canonicalFiles=prior.canonicalSources.map(item=>({path:item.path,sha256:hash(fs.readFileSync(item.path))}));
write('preparation.json',{candidate,candidateConfirmedByHead:false,officialHead:git(repo,'rev-parse','HEAD'),canonicalFiles,canonicalApplied:false,liveOutcomeReceived:false,productDirty:git(repo,'status','--porcelain'),candidateDirty:git(candidateRepo,'status','--porcelain')});
console.log(JSON.stringify({candidate,nodes:nodes.length,edges:edges.length,pins:pins.length,canonicalFiles:canonicalFiles.length,canonicalApplied:false}));
