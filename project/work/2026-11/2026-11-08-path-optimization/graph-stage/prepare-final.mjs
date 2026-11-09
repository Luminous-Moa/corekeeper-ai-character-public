import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url));
const result=path.dirname(stage);
const plan=path.resolve(stage,'../../2026-11-07-기획연결');
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
const delta=JSON.parse(fs.readFileSync(path.join(stage,'delta.json'),'utf8'));
const pins=JSON.parse(fs.readFileSync(path.join(stage,'source-pins.json'),'utf8'));
const docs=[
  ['pathAcceptance',path.join(stage,'head-acceptance.md'),'head-acceptance.md'],
  ['pathFinalImplementation',path.join(result,'implementation-final.md'),'implementation-final.md'],
  ['pathLiveFinal',path.join(result,'live-validation.md'),'live-final.md'],
  ['pathDirtyBefore',path.join(result,'main-dirty-before.json'),'main-dirty-before.json'],
  ['pathDirtyAfter',path.join(result,'main-dirty-after.json'),'main-dirty-after.json'],
  ['pathInstalled',path.join(result,'final-installed-compare.json'),'final-installed-compare.json'],
  ['pathPreserved',path.join(result,'final-preservation-verify.txt'),'final-preservation-verify.txt']
];
for(const[id,original,name]of docs){
  const pinned=path.join(stage,'source-pins',name);
  const bytes=fs.readFileSync(original);
  if(id==='pathLiveFinal'&&!bytes.toString('utf8').includes('43c3817'))throw Error('Second live final report is pending');
  fs.writeFileSync(pinned,bytes);
  delta.documents[id]={layer:'참고',path:path.relative(plan,pinned).replaceAll('\\','/'),originalPath:path.relative(plan,original).replaceAll('\\','/')};
  pins.push({id,source:original,pinned,sha256:hash(bytes)});
}
const ev=(doc,anchor)=>({doc,anchor});
const implementation=delta.nodes.find(node=>node.id==='R.path-implementation');
implementation.title='43c3817 공유 검색과 기존 원정 트리 연결의 공식 구현';
implementation.status='참고·공식 통합·대표 ai2 실기 수용';
implementation.evidence.push(ev('pathFinalImplementation','## 2차 구현'),ev('pathAcceptance','HEAD는 2차 후보'));
const review=delta.nodes.find(node=>node.id==='R.path-review');
review.status='참고·독립 코드 수용·대표 실기는 별도 근거';
review.summary='공유 검색의 동기 예열과 상한 오판, 직접 후보 key 충돌을 수정·재검수했다. 코드·오프라인·Unity 범위를 수용했으며 대표 실기는 R.path-live의 정확 관측을 따른다. 원자 epoch 미보장, 직접 대응만255 초과 시 전송 중단, 동률 경로와 진단 범위 변화가 남는다.';
delta.nodes.push(
  {id:'R.path-live',title:'43c3817 ai2 대표 요청·응답·행동과 잔여 재준비 지연',layer:'참고',status:'참고·대표 실기 수용·약9~18초 잔여 지연',sourceCommit:delta.sourceCommit,functionTraceObserved:false,summary:'갑옷 상위 응답 t203.9→29개 선택지 SEND t212.8→APPLY t213.1과 실제 이동을 관측했다. 전체 SEND/API 사용31회, 응답 파싱31회, RESULT 로그29개, HTTP 실패0, 후보19~56개, START18/DONE12다.118.4~120fps를 유지했으나 장애물 변경 취소26회와 약9~18초 재준비 지연이 남았다.7526개 대상의 실게임 전수 메뉴 탐색, 갑옷 최종 제작·전달과 채집 확보는 확인하지 않았다. 전체 leaf 방문은 오프라인 근거다.',evidence:[ev('pathLiveFinal','## 2차 대표 실기 · 43c3817'),ev('pathLiveFinal','### 프레임과 남은 재준비 지연'),ev('pathLiveFinal','### 판정 범위')]},
  {id:'R.path-integration',title:'공식 ai-character43c3817 통합과 기존 환경 변경 보존',layer:'참고',status:'참고·공식 소스 통합·최종 설치 확인',summary:'HEAD가 대표 실기를 수용하고 공식 소스를43c3817로 fast-forward 통합했다. 정본 담당이 Git HEAD를 확인했고 기존 dirty 파일은 HEAD의 전후 기록에서 내용 보존을 확인했다. 시험 자료를 복원한 뒤 모드만43c3817로 최종 설치했다. 설치116개 소스 SHA가 일치하며 AI자료·세이브·레지스트리·시험스위치는 시험 전과 같다. 최종 재설치 후 게임은 다시 실행하지 않았다.',evidence:[ev('pathAcceptance','HEAD는 2차 후보'),ev('pathDirtyBefore','Assets/ModSDK/GameVersionRegistry.asset'),ev('pathDirtyAfter','"Unchanged": true'),ev('pathLiveFinal','## 2차 복원과 최종 사용자 설치'),ev('pathInstalled','"installedHash"')]}
);
const edge=(from,to,type,doc,anchor)=>({from,to,type,class:'해석',evidence:[ev(doc,anchor)]});
delta.edges.push(
  edge('P.path-optimization','R.path-live','참고: 대표 실기 관측과 잔여 지연','pathAcceptance','HEAD가 정정 전달한 대표 관측'),
  edge('P.path-optimization','R.path-integration','참고: 공식 통합과 환경 보존','pathAcceptance','HEAD는 2차 후보'),
  edge('R.path-implementation','R.path-live','참고: 같은43c3817 실기 관측','pathLiveFinal','## 2차 대표 실기 · 43c3817')
);
const addition='\n// 길 계산 최적화43c3817의 공식 통합과 확인 범위.\nObject.assign(documents,'+JSON.stringify(delta.documents,null,2)+');\nnodes.push(...'+JSON.stringify(delta.nodes,null,2)+');\nedges.push(...'+JSON.stringify(delta.edges,null,2)+');\n';
fs.writeFileSync(path.join(stage,'final-delta.json'),JSON.stringify(delta,null,2)+'\n');
fs.writeFileSync(path.join(stage,'final-addition.mjs.txt'),addition);
fs.writeFileSync(path.join(stage,'final-source-pins.json'),JSON.stringify(pins,null,2)+'\n');
console.log('Final delta prepared '+delta.nodes.length+' nodes / '+delta.edges.length+' edges');
