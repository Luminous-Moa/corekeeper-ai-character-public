import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url)),result=path.dirname(stage);
const plan=path.resolve(stage,'../../2026-11-07-기획연결');
const target='20b1aced7594a47eacb4be060405bcb53d38f48f';
const tested='09e418a8af85bd1578b2cfea09cdbe3c0180db77';
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
const delta=JSON.parse(fs.readFileSync(path.join(stage,'delta.json'),'utf8'));
const pins=JSON.parse(fs.readFileSync(path.join(stage,'source-pins.json'),'utf8'));
const docs=[['observationUpperReview','review-upper-completion.md'],['observationLiveReview','review-live.md'],['observationLive','live-validation.md'],['observationDirtyBefore','main-dirty-before.json'],['observationDirtyAfter','main-dirty-after.json']];
for(const[id,name]of docs.concat([['observationAcceptance','head-acceptance.md']])){
  const original=path.join(id==='observationAcceptance'?stage:result,name);
  const pinned=path.join(stage,'source-pins',name),bytes=fs.readFileSync(original);
  fs.writeFileSync(pinned,bytes);
  delta.documents[id]={layer:'참고',path:path.relative(plan,pinned).replaceAll('\\','/'),originalPath:path.relative(plan,original).replaceAll('\\','/')};
  pins.push({id,original,pinned,sha256:hash(bytes)});
}
const ev=(doc,anchor)=>({doc,anchor});
const node=id=>delta.nodes.find(item=>item.id===id);
Object.assign(node('R.observation-implementation'),{title:'20b1aced의 관측 소속·완료 사실 검사·지속 대기 재검토',status:'참고·공식 구현·코드/오프라인/Unity 수용',sourceCommit:target});
Object.assign(node('R.observation-delivery'),{status:'참고·공식 코드·09e418a의 세 부위 실수령',sourceCommit:target});
node('R.observation-delivery').summary+=' 09e418a에서 세 부위 각각1개 실수령과 후속 단계 진행을 확인했다. 해당 receipt 소스는20b1aced에서 동일하며 추가 상위 완료 경계의 실기 성공으로 확대하지 않는다.';
node('R.observation-delivery').evidence.push(ev('observationLiveReview','## 장비 세 부위 전달 확인'));
Object.assign(node('R.observation-review'),{status:'참고·독립 코드/오프라인/Unity 수용',sourceCommit:target});
node('R.observation-review').summary='기존 관측·실행156개, inventory171개, travel51개를 독립 통과했고 최종 상위 완료 경계는 생산 검사181개와 Unity를 수용했다. HTTP transport와 ECS 정렬·consumer 대역 경계가 있다. 실기09e418a의 장비 수령과 통로 선택은 별도 증거다. 조용한 통로에서 continue 후5초 재호출 조합과 floorNull 빠른 수령은 실기에서 관측하지 않았다.';
node('R.observation-review').evidence.push(ev('observationUpperReview','## 판정'));
delta.nodes.push(
  {id:'R.observation-upper-completion',title:'거절된 새 계획과 동일 미완료 원부탁 done의 부분 적용 보호',layer:'참고',status:'참고·20b1aced 추가 경계·오프라인/Unity 수용',sourceCommit:target,summary:'새 set 계획 전체가 거절되고 현재 유효 목표가 활성 계획 source와 같으며 같은 미완료 원부탁을 done으로 갱신할 때 적용 전 보류한다. 후속 단계 또는 재관측한 마지막 단계의 미완료를 근거로 원부탁·목표·몸·계획을 유지하고 상위에 다시 보고한다. source/goal revision/원부탁 ID/실제 사실 키 집합으로 같은 모순의 반복 보고를 억제한다. 무관한 done과 defer/drop/answered, 유효 새 계획과 비계량 마지막 목표의 기존 권한은 유지한다.',evidence:[ev('observationUpperReview','## 적용 범위 확인'),ev('observationUpperReview','## 원부탁 보존과 재보고'),ev('observationUpperReview','## 직접 재실행한 주요 검사')]},
  {id:'R.observation-live',title:'09e418a 세 부위 수령·다음 단계·원래 부탁 완료와 통로 선택',layer:'참고',status:'참고·09e418a 실기·20b1aced 전체 실기와 구별',sourceCommit:tested,functionTraceObserved:false,summary:'투구0.9초·갑옷0.7초·바지1초의 drop 각1회와 sender 감소·정확 floor·지정 pickup/소비·호스트 증가를 확인했다. 단계마다 다음 부위로 진행했고 마지막 finish 뒤 plan_done과 원부탁 done에 도달했다. 바지 미확인 finish는 약5.3초마다 재판단하며 의무를 보존한 뒤 정상 전달했다. 조용한 통로에서 방향 필요→모델 방향 선택→이동을 확인했다. continue 유지 뒤 재호출과 floorNull 빠른 pickup 조합은 실기 미관측이다. 사용자의 이후 왕복·지연은 별도 조사 범위다.',evidence:[ev('observationLiveReview','## 장비 세 부위 전달 확인'),ev('observationLiveReview','## 미확인 완료 뒤 재판단'),ev('observationLiveReview','## 통로 방향 선택 실기'),ev('observationLiveReview','## 남은 수용 범위')]},
  {id:'R.observation-integration',title:'공식20b1aced 통합·설치09e418a·사용자 종료 승인 대기',layer:'참고',status:'참고·공식 코드 통합·최종 설치/복원 대기',sourceCommit:target,summary:'HEAD가20b1aced를 공식 ai-character에 fast-forward 통합하고 기존 dirty 내용을 보존했다. 현재 사용자는09e418a 시험 설치 상태에서 게임을 직접 조작 중이며 정상 종료·시험 자료 복원·최종20b1aced 설치 승인을 기다린다.20b1aced 추가 경계는 코드181검사·독립·Unity 수용 범위다.',evidence:[ev('observationAcceptance','최종 코드20b1aced'),ev('observationAcceptance','코드 수용과 정본 반영을 승인했다.'),ev('observationDirtyAfter','"Unchanged": true'),ev('observationLive','## 사용자 개입 의심과 종료 대기')]}
);
const edge=(from,to,type,doc,anchor)=>({from,to,type,class:'해석',evidence:[ev(doc,anchor)]});
delta.edges.push(
  edge('P.observation-completion-check','R.observation-upper-completion','참고: 같은 미완료 원부탁의 상위 완료 모순 보호','observationUpperReview','## 적용 범위 확인'),
  edge('P.observation-completion-check','R.observation-live','참고: 정확한 후보판의 실기 관측','observationLiveReview','## 장비 세 부위 전달 확인'),
  edge('P.observation-completion-check','R.observation-integration','참고: 공식 통합과 설치 대기','observationAcceptance','최종 코드20b1aced'),
  edge('R.observation-review','R.observation-upper-completion','참고: 최종 추가 경계 독립 검수','observationUpperReview','## 판정')
);
const amendment={title:'finish_goal — 현재 사실을 확인한 명시 단계 완료',status:'참고·기존 계약과 후속 완료 확인',summary:'685dde4에서 현재 단계의 명시 완료와 다음 단계·상위 보고를 연결했다.2026-11-08 후속 요청에 따라 측정 완료 조건이 있는 단계는 적용 직전 사실을 다시 읽고 부족·미확인이면 단계·원래 부탁을 유지하며 재판단한다. 확인된 완료와 비계량 목표의 판단 종료, AI의 중단·전환 권한은 보존한다.'};
const addition='\n// 관측·재판단20b1aced의 공식 코드와 후보별 실기 범위.\nObject.assign(documents,'+JSON.stringify(delta.documents,null,2)+');\nnodes.push(...'+JSON.stringify(delta.nodes,null,2)+');\nedges.push(...'+JSON.stringify(delta.edges,null,2)+');\nObject.assign(nodes.find(node=>node.id===\'R.finish-goal\'),'+JSON.stringify(amendment)+');\nnodes.find(node=>node.id===\'R.finish-goal\').evidence.push('+JSON.stringify(ev('observationAgreement','현재 요청에 따라 완료 선언과 실제 사실이 어긋날 때의 재판단 흐름을 보완한다.'))+');\n';
const files=['Assets/AIPlayerSpike/Scripts/AIInventory.cs','Assets/AIPlayerSpike/Scripts/AISkill.cs','Assets/AIPlayerSpike/Scripts/AITravel.cs'];
const proof=files.map(file=>{
  const blob=commit=>execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','rev-parse',commit+':'+file],{encoding:'utf8'}).trim();
  return {file,testedBlob:blob(tested),currentBlob:blob(target),sameSource:blob(tested)===blob(target)};
});
if(proof.some(item=>!item.sameSource))throw Error('Tested receipt/travel source changed');
fs.writeFileSync(path.join(stage,'tested-source-correspondence.json'),JSON.stringify({testedCommit:tested,currentCommit:target,sameRevision:false,evidenceAppliesToCurrentRevision:false,runtimeTrace:false,files:proof},null,2)+'\n');
fs.writeFileSync(path.join(stage,'final-delta.json'),JSON.stringify({...delta,sourceCommit:target,legacyAmendment:amendment},null,2)+'\n');
fs.writeFileSync(path.join(stage,'final-addition.mjs.txt'),addition);
fs.writeFileSync(path.join(stage,'final-source-pins.json'),JSON.stringify(pins,null,2)+'\n');
console.log('Final delta '+delta.nodes.length+' nodes / '+delta.edges.length+' edges, tested receipt/travel blobs match');
