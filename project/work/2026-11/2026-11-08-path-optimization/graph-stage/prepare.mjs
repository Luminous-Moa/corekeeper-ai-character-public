import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath, pathToFileURL} from 'node:url';

const stage = path.dirname(fileURLToPath(import.meta.url));
const result = path.dirname(stage);
const work = path.resolve(result, '../..');
const plan = path.join(work, '2026-11/2026-11-07-기획연결');
const repo = 'C:/Dev/CoreKeeperModSDK';
const target = '43c3817fc1a36142c8591fb087ed867665633fe1';
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const writeJson = (name, value) => fs.writeFileSync(path.join(stage, name), JSON.stringify(value, null, 2) + '\n');
const current = await import(pathToFileURL(path.join(plan, 'source.mjs')).href);
const documents = {};
const pins = [];
fs.mkdirSync(path.join(stage, 'source-pins'), {recursive: true});
for (const [id, name, layer] of [
  ['pathAgreement', 'agreement.md', '재기획'],
  ['pathImplementation', 'implementation.md', '참고'],
  ['pathReview', 'review-final.md', '참고'],
  ['pathFirstLive', 'live-validation.md', '참고'],
  ['pathTreeAudit', 'existing-tree-audit.md', '참고'],
  ['pathSharedReview', 'review-shared-search.md', '참고'],
  ['pathSharedIntegration', 'tunnel-shared.md', '참고']
]) {
  const source = path.join(result, name);
  const pinned = path.join(stage, 'source-pins', name);
  const bytes = fs.readFileSync(source);
  fs.writeFileSync(pinned, bytes);
  documents[id] = {layer, path: path.relative(plan, pinned).replaceAll('\\', '/'), originalPath: path.relative(plan, source).replaceAll('\\', '/')};
  pins.push({id, source, pinned, sha256: hash(bytes)});
}
const evidence = (doc, anchor) => ({doc, anchor});
const nodes = [
  {id:'P.path-optimization', title:'선택지와 위험 정보를 보존하며 길 계산 준비를 최적화', layer:'재기획', status:'합의', summary:'중복 경로 계산·캐시 전체 삭제·수천 목적지 동기 처리를 개선한다. 모든 후보와 선택 전 위험 정보, 도구·문·목적지 예외, 지식 공개와 정상 게임 조건을 보존한다. 임의 후보 상한·목표별 숨김·대표지점 압축을 복원하지 않는다.', evidence:[evidence('pathAgreement','목표는 Gemini 응답 이후'),evidence('pathAgreement','모든 후보와 행동 선택권')]},
  {id:'R.path-first-attempt', title:'2a0112d 프레임 분산 후보의 1차 실기 미통과', layer:'참고', status:'참고·과거 후보·실기 미통과·복원 완료', summary:'오프라인과 Unity 검수 뒤 ai2를 시험했다. 7526 frontier 준비가147.9초·54.6초CPU 후 위협 변화로 취소됐고 별도258개 전투 요청은255 API 상한으로HTTP400이었다. UI118~120fps는 유지됐으나 성공 응답·몸실행은 미확인이다. 설치·월드·자료·레지스트리·시험스위치는 시험 전과 차이0으로 복원했다.', evidence:[evidence('pathFirstLive','## 1차 실기 결과'),evidence('pathFirstLive','## 1차 실기 최종 요청·정상 종료·복원'),evidence('pathReview','## 판정')]},
  {id:'R.path-tree-audit', title:'기존 생활·가방·채집이동 트리의 전송 연결 감사', layer:'참고', status:'참고·기존 트리 존재 확인·생산 연결 결함', summary:'세 트리가 이미 구현되어 있었다. 닫힌 원정 트리의 전체 직접 후보 뒤에 open을 붙여 첫 요청이255를 넘으면 탐색 진입도 실패했다. dig kind 뒤의 전 leaf도 단일 요청을 초과했다. 기존 직접 실행·얕은 탐색·모든 후보 접근·현재 행동 보존 계약에 맞춰 진입과 leaf 준비를 연결한다. 화면 안 lExplore는BFS 거리를 재사용하고7526 fr만 목적지별터널 반복이었다.', evidence:[evidence('pathTreeAudit','## 결론'),evidence('pathTreeAudit','## 합의와 범위'),evidence('pathTreeAudit','## 후속 검수에 넘기는 판단')]},
  {id:'R.path-implementation', title:'43c3817 공유 검색과 기존 원정 트리 연결 수정 후보', layer:'참고', status:'참고·2차 후보·독립 코드 수용·실기 대기', summary:'같은 배치의 열린·닫힌 검색을 공유하고 경계 목적지는 마지막 한 칸으로 복원한다. 기존 원정 트리에서 싼key/좌표 목록을 탐색한 뒤 현재 leaf의 상세 경로와 실행 전 위험을 준비한다. 가까운 직접 행동·entity/life/명시 목적지·현재 실행과 원래 목표를 보존한다. 소수계획은 단일검색을 쓰고8은 공유검색 전환 조건이다. 직접 대응만255를 초과하면 진단 후 전송을 중단한다. 동률 최단경로 순서와 공유 관측 진단 범위는 달라질 수 있다.', evidence:[evidence('pathSharedReview','## 선택 트리 연결 이후 checkpoint'),evidence('pathSharedReview','## 구현 확인'),evidence('pathSharedIntegration','## 범위')]},
  {id:'R.path-offline', title:'공유 검색과 기존 트리 전수 leaf 오프라인 검증', layer:'참고', status:'참고·오프라인·Unity 컴파일 확인', summary:'공유검색8640 목적지 조합·Core1571·scheduler25·execution118·restriction47을 확인했다. 실제258요청fixture의208개mine/dig와합성452/7526leaf를Compose/Nav/JSON에서전수방문했다. 각노드는제어·직접후보포함255이하,선택leaf만준비했다. 생산AITunnel통합대역72084 검사는 별도 구현 보고를 따른다. CandBuild/Unity 세계 대역과 합성 측정 경계가 있다.', evidence:[evidence('pathSharedReview','## 직접 실행'),evidence('pathSharedReview','## 선택 트리 연결 이후 checkpoint'),evidence('pathSharedIntegration','## 검증')]},
  {id:'R.path-review', title:'43c3817 코드·오프라인·Unity 후속 독립 수용', layer:'참고', status:'참고·독립 검수·2차 실기 결과 대기', summary:'공유 검색의 동기 예열과상한오판,직접후보key충돌을수정·재검수했다. 코드·오프라인과Unity범위는후속실기에넘길수있다. 실제ai2첫SEND·트리탐색·실행선택·행동과7526목록응답성은실기대기다. 원자epoch미보장·직접대응만255초과시전송중단·동률경로와진단범위변화를공개한다.', evidence:[evidence('pathSharedReview','## 2차 통합 코드 검수 판정'),evidence('pathSharedReview','## 후속 발견과 처리')]}
];
const edge = (from, to, type, doc, anchor) => ({from, to, type, class:'해석', evidence:[evidence(doc,anchor)]});
const edges = [
  edge('P.execution','P.path-optimization','선택된 행동을 준비하는 계산 개선','pathAgreement','목표는 Gemini 응답 이후'),
  edge('P.unrestricted-choice','P.path-optimization','기존 선택권 보존 조건','pathAgreement','모든 후보와 행동 선택권'),
  edge('P.path-optimization','R.path-first-attempt','참고: 1차 실기 실패와 복원','pathFirstLive','## 1차 실기 결과'),
  edge('P.path-optimization','R.path-tree-audit','참고: 기존 트리와 전송 연결','pathTreeAudit','## 결론'),
  edge('P.expedition-choice','R.path-tree-audit','참고: 기존 직접 실행과 얕은 탐색 합의 대조','pathTreeAudit','## 합의와 범위'),
  edge('P.path-optimization','R.path-implementation','참고: 2차 구현 후보의 실제 변경','pathSharedReview','## 선택 트리 연결 이후 checkpoint'),
  edge('P.path-optimization','R.path-review','참고: 후속 독립 수용 범위','pathSharedReview','## 2차 통합 코드 검수 판정'),
  edge('R.path-implementation','R.path-offline','참고: 생산 경로와 회귀 검증','pathSharedReview','## 직접 실행')
];
const ids = new Set(current.nodes.map(n => n.id));
for (const n of nodes) {
  if (ids.has(n.id)) throw Error('Existing delta requires reconciliation ' + n.id);
  ids.add(n.id);
}
for (const e of edges) if (!ids.has(e.from) || !ids.has(e.to)) throw Error('Missing endpoint');
const addition = '\n// 길 계산 최적화의 합의와 검증 범위.\nObject.assign(documents,' + JSON.stringify(documents,null,2) + ');\nnodes.push(...' + JSON.stringify(nodes,null,2) + ');\nedges.push(...' + JSON.stringify(edges,null,2) + ');\n';
fs.writeFileSync(path.join(stage,'canonical-addition.mjs.txt'),addition);
writeJson('delta.json',{sourceCommit:target,documents,nodes,edges});
writeJson('source-pins.json',pins);
const planning = path.join(stage,'planning');
fs.mkdirSync(planning,{recursive:true});
for (const name of ['compile.mjs','graph.mjs']) fs.copyFileSync(path.join(plan,name),path.join(planning,name));
const paths = Object.fromEntries(Object.entries({...current.documents,...documents}).map(([id,d]) => [id,path.resolve(plan,d.path).replaceAll('\\','/')]));
fs.writeFileSync(path.join(planning,'source.mjs'),fs.readFileSync(path.join(plan,'source.mjs'),'utf8') + addition + '\nconst isolatedPaths=' + JSON.stringify(paths) + ';\nfor(const[id,d]of Object.entries(documents))d.path=isolatedPaths[id];\n');
const canonicalPaths = [
  '지식그래프/README.md','지식그래프/제품그래프-사용법.md','지식그래프/기획/정책.md','지식그래프/기획/기능.md','지식그래프/tools/config.json','지식그래프/tools/product-build.mjs',
  '2026-11/2026-11-07-기획연결/source.mjs','2026-11/2026-11-07-기획연결/compile.mjs',
  '2026-11/2026-11-05-지식그래프-개편/B/README.md',
  ...['pin-snapshot.mjs','build.mjs','verify.mjs'].map(n=>'2026-11/2026-11-05-지식그래프-개편/B/extractor/'+n),
  '2026-11/2026-11-05-지식그래프-실화면/C/overlay-source.json',
  '2026-11/2026-11-05-지식그래프-실화면/D/planning.mjs','2026-11/2026-11-05-지식그래프-실화면/D/bag-source.mjs'
];
writeJson('preparation.json',{
  candidate:target,
  officialHead:execFileSync('git',['-C',repo,'rev-parse','HEAD'],{encoding:'utf8'}).trim(),
  canonicalFiles:canonicalPaths.map(relative=>({relative,path:path.join(work,relative),sha256:hash(fs.readFileSync(path.join(work,relative)))})),
  productDirty:execFileSync('git',['-C',repo,'status','--porcelain'],{encoding:'utf8'}),
  canonicalApplied:false, liveOutcomeReceived:false,
  parallelOwner:'HEAD reported active external conversation 수집범위를확인하세요; reconcile latest canonical before writing'
});
console.log('Prepared isolated planning delta, pinned sources and canonical fingerprints.');
