import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),canonical='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터';
const G='work/지식그래프',P='work/2026-11/2026-11-07-기획연결',T='work/2026-11/2026-11-07-가방장비구현/tree-agreement.md';
const read=p=>fs.readFileSync(path.join(stage,p),'utf8'),write=(p,s)=>fs.writeFileSync(path.join(stage,p),s),hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
fs.copyFileSync(path.join(canonical,T),path.join(stage,T));
const manifest=JSON.parse(read('canonical-before.json'));manifest.files=manifest.files.filter(r=>r.path!==T);manifest.files.push({path:T,sha256:hash(path.join(canonical,T))});write('canonical-before.json',JSON.stringify(manifest,null,2)+'\n');
if(!read(P+'/source.mjs').includes('// Approved category-first bag navigation'))write(P+'/source.mjs',read(P+'/source.mjs')+`
// Approved category-first bag navigation. Implementation revision is pending separately.
documents.bagTree={layer:'재기획',path:'../2026-11-07-가방장비구현/tree-agreement.md'};
nodes.push(
 n('P.bag-item-first','실제 보유 분류에서 물품과 조작으로 좁히기','합의','인벤토리→물품이 있는 실제 분류→정확한 보유 슬롯/변형/상태→조작→필요한 인자로 이어 간다. 목표 맥락을 유지하며 목록을 목표 품목 하나로 숨기지 않는다. 관측한 자기 상자 꺼내기 경로도 같은 방식으로 잇는다.','bagTree','## 만들 결과'),
 n('P.bag-navigation','선택 탐색과 정상 조작 실행을 구별','합의','현재 메뉴 자식만 제시하는 실제 다단계 상태를 유지한다. 뒤로는 부모로, 전체 선택 취소는 일반 판단으로 돌아간다. 메뉴 탐색은 물품 이동/몸 중지/단계 완료/원래 부탁 취소가 아니며 최종 leaf 선택에서만 정상 조작을 한 번 요청한다.','bagTree','## 실행·모델 연결 조건',[ev('bagTree','## 만들 결과')]),
 n('P.bag-no-cut','후보 개수 상한을 없애고 현재 노드 전부 제공','합의','일반 가방8·전체 비전투14와 같은 개수 기반 잘라내기를 제거한다. 분류/물품/조작/대상을 임의 목록 cut이나 페이지로 나누지 않는다. 관측·소유·실행 가능 조건과 로그/기억 보관량 및 기존 호출/비용 제한은 구별한다.','bagTree','## 선택지 상한 제거',[ev('bagTree','## 실행·모델 연결 조건')]),
 n('R.bag-navigation','분류·물품 선택 메뉴와 실행 경계의 새판 검증','참고·새 코드 SHA/검증 대기','flat 774/1fc 부품 검증을 새 트리 통과로 승계하지 않는다. 뒤로/전체 선택 취소/오래된 메뉴 응답/8·14 초과 생성·전달 및 leaf 정상 실행을 새 고정판에서 검증한다.','bagTree','## 검증과 전달')
);
edges.push(
 e('P.bag-equipment','P.bag-item-first','평면 후보 부분을 새 선택 흐름으로 교체','bagTree','2026-11-07 사용자 방향 변경.','합의'),
 e('P.bag-item-first','P.bag-navigation','물품별 단계 탐색과 실행','bagTree','## 만들 결과','합의'),
 e('P.bag-item-first','P.bag-no-cut','분류로 나누고 실제 항목은 모두 제공','bagTree','## 선택지 상한 제거','합의'),
 e('P.noncombat-choice','P.bag-no-cut','현장 후보 상한 제거의 요청 범위','bagTree','## 선택지 상한 제거','합의'),
 e('P.bag-agency','P.bag-navigation','메뉴 선택과 목표·몸 제어의 역할 구분','bagTree','## 실행·모델 연결 조건','합의'),
 e('P.bag-navigation','P.valid-action','실행 가능한 몸 동작과 기존 대응 유지','bagTree','## 실행·모델 연결 조건','합의'),
 e('P.bag-navigation','R.bag-navigation','참고: 새 트리 고정판 검증 경계','bagTree','## 검증과 전달'),
 e('R.bag-navigation','R.bag-validation','참고: 최종 SHA와 실제 검증 범위','bagStatus','# 가방·장비 구현 검증 범위')
);
for(const mode of ['equip','hold','store','take','drop','give'])edges.push(e('R.bag-navigation','R.bag-'+mode,'참고: 최종 leaf에서 재사용하는 조작·관측','bagTree','## 실행·모델 연결 조건'));
`);
const policy=G+'/기획/정책.md';if(!read(policy).includes('### pol.bag-navigation —'))write(policy,read(policy)+`
## 2026-11-07 분류 기반 가방 선택 트리 승인

### pol.bag-navigation — 선택 메뉴와 몸·목표 제어 분리
- 규칙: 가방·장비 선택에서 / Decisions는 / 실제 메뉴 상태의 자식을 선택하고 뒤로·전체 선택 취소를 수행한다. 메뉴 자체는 물품 이동·몸 중지·목표 완료·원래 부탁 취소를 하지 않으며 모든 인자가 정해진 최종 실행 선택에서만 정상 조작을 요청한다.
- 등급: 사용자 결정
- 확정도: 확정
- 종류: 처리
- 날짜: 2026-11-07
- 근거: ${T} "메뉴 조작 자체는 물품 이동·몸 동작 중단·현재 목표 완료·원래 부탁 취소를 수행하지 않는다."

### pol.choice-no-cut — 실제 자식 선택지를 개수로 자르지 않음
- 규칙: 현장 후보 생성·전달에서 / 코드는 / 일반 가방8·전체 비전투14와 같은 후보 개수 잘라내기를 제거하고 현재 노드의 실제 항목을 모두 제공한다. 임의 페이지 분할을 하지 않으며 관측·소유권·실행 가능성 조건, 로그·기억 보관량과 기존 호출·비용 제한은 별개로 유지한다.
- 등급: 사용자 결정
- 확정도: 확정
- 종류: 처리
- 날짜: 2026-11-07
- 근거: ${T} "현재 노드의 실제 항목을 모두 제공하며, 트리의 분류로 한 단계의 선택 범위를 나눈다."
`);
const feature=G+'/기획/기능.md';if(!read(feature).includes('### F.bag-navigation —'))write(feature,read(feature)+`
## 2026-11-07 분류 기반 가방 선택 트리 준비

### F.bag-navigation — 분류·보유 물품·조작의 메뉴 탐색
- 상태: 설계만 있음
- 종류: 제품
- 한 줄: 실제 보유 분류에서 물품·조작·필요한 인자를 고르고 최종 leaf에서만 정상 조작을 요청한다. 메뉴 뒤로·전체 선택 취소와 몸 동작/목표/원래 부탁 제어를 구별한다.
- 관측 지점: 새 코드 SHA·메뉴 전이·실제 모델 전달 문구와 leaf 실행 근거 대기. flat 774/1fc 검증을 새 트리 통과로 승계하지 않는다.
- 정책: pol.bag-agency, pol.bag-navigation, pol.choice-no-cut, pol.bag-boundary
- 근거: ${T} "현재 메뉴의 자식 선택지만 현장 판단 모델에 전달한다."
- 메모: 현재는 기획·승인 연결 준비다. 최종 코드의 실제 앵커를 확인한 뒤 등록하고 재생성한다. 기존 여섯 F.bag 조작 primitive와 새 메뉴 상태를 구별한다.
`);
const unresolved=G+'/기획/미결.md';write(unresolved,read(unresolved).replace('- 관련: F.bag-equip, F.bag-hold, F.bag-store, F.bag-take, F.bag-drop, F.bag-give','- 관련: F.bag-equip, F.bag-hold, F.bag-store, F.bag-take, F.bag-drop, F.bag-give, F.bag-navigation'));
const state=JSON.parse(read('stage-state.json'));Object.assign(state,{status:'분류 기반 무상한 트리 기획 원본 준비;새 코드 SHA·실제 근거 대기',authoringPrepared:true,treeAgreementSha256:hash(path.join(stage,T)),generatedGraphsReflectTree:false,finalAcceptance:false});write('stage-state.json',JSON.stringify(state,null,2)+'\n');
console.log('Prepared approved category-first navigation and no-cut authoring only; generated graphs unchanged.');
