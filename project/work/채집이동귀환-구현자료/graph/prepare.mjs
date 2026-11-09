import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url));
const work=path.resolve(stage,'../..');
const canonical=path.join(work,'2026-11/2026-11-07-기획연결');
const source=path.join(canonical,'source.mjs');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const json=x=>JSON.stringify(x,null,2)+'\n';
const current=await import(pathToFileURL(source).href+'?'+Date.now());
const documents={
 expeditionAgreement:{layer:'재기획',path:'../2026-11-07-채집이동귀환-확정합의.md'},
 expeditionRoles:{layer:'재기획',path:'../2026-11-07-채집이동귀환-역할판단.md'},
 expeditionBehaviorGraph:{layer:'참고',path:'../../게임행동-그래프/README.md'},
 expeditionDirection:{layer:'참고',path:'../../채집이동귀환-구현자료/direction.md'},
 expeditionDirectionAcceptance:{layer:'참고',path:'../../채집이동귀환-구현자료/graph/direction-acceptance.md'},
 expeditionVisionInvestigation:{layer:'참고',path:'../../채집이동귀환-구현자료/vision-authority-investigation.md'},
 expeditionFirstReview:{layer:'참고',path:'../../채집이동귀환-구현자료/review.md'}
};
const ev=(doc,anchor)=>({doc,anchor});
const node=(id,title,summary,doc,anchor)=>({id,title,layer:id.startsWith('R.')?'참고':'재기획',status:id.startsWith('R.')?'참고·개발용 정적 근거·미실게임':'사용자 합의·구현 수용 별도',summary,evidence:[ev(doc,anchor)]});
const nodes=[
 node('P.expedition','채집·이동·귀환과 후속 작업을 하나의 흐름으로 연결','이번 외출의 목적·현장 판단·실제 확보량·귀환 목적·후속 작업을 연결한다. 원정은 새 목표 계층이 아니며 원래 부탁과 중간 작업의 완료를 구별한다.','expeditionAgreement','## 3. 목표량·귀환·후속 작업 연결'),
 node('P.expedition-choice','구체 행동 직접 선택과 얕은 대상 탐색 병행','눈앞의 묶음과 이미 정해진 이동은 바로 선택하고 다른 대상·장소는 분류를 좁힌다. 탐색 중 유효 행동을 유지하고 메뉴 취소를 몸 동작·부탁 취소와 구별한다.','expeditionAgreement','## 1. 선택 트리와 직접 실행 병행'),
 node('P.expedition-sidework','곁일 여부와 범위는 판단모델이 결정','고정 비율·개수·시간으로 곁일 정책을 대신하지 않는다. 원래 작업과 진척, 지연·가방·위험·재방문 비용을 판단에 제공하고 곁일 뒤 재개 맥락을 보존한다. 중요한 계획 변경은 기존 상위 재계획에 연결한다.','expeditionAgreement','## 2. 곁일은 판단모델에 위임'),
 node('P.expedition-bundle','선택 당시 보이는 자연스러운 묶음을 고정해 연속 실행','벽 광맥·식물·바닥 아이템의 같은 종류 대상 묶음과 개별 선택을 지원한다. 선택 범위의 접근·채집·회수는 연속 실행하되 새 발견은 다음 선택으로 제공하고 대상 수와 실제 확보량을 구별한다.','expeditionRoles','## 채집물 전반의 기본 선택 단위 — 사용자 합의'),
 node('P.expedition-return','실제 누적량과 귀환 목적·원작업 재개를 연결','임의 목표량 상한은 별도 제약해제 합의에 따르며 목표량과 실행 묶음을 분리한다. 단순 목표 달성과 명시 상한을 구별하고 귀환 뒤 보관·강화·원작업을 이어 간다. 새 발견만으로 귀환을 자동 연장하지 않는다.','expeditionAgreement','## 3. 목표량·귀환·후속 작업 연결'),
 node('P.expedition-navigation','통로 탐색과 알려진 목적지 이동을 구별','외길 탐색은 코너 수와 무관하게 이어지고, 알려진 목적지의 유효 경로는 갈림길마다 다시 묻지 않는다. 기본은 현재 시야와 직접 획득한 기억이며 현재 관측·마지막 관측·미지를 구별한다.','expeditionRoles','## 이동 방식 합의와 현장 호출 기준'),
 node('P.expedition-lookahead','응답 지연에 맞춰 다음 갈림길 방향만 선행 질문','캐릭터 기준 일정 범위에서 다음 갈림길 방향을 준비하고 실제 전환 시 재검증한다. 늦으면 미선택 갈래로 들어가지 않는다. 구체 거리는 실제 속도·응답시간에 맞춰 정하며 숨은 자원·위험을 공개하는 예외가 아니다.','expeditionAgreement','## 4. 선행 질문과 채집 묶음'),
 node('P.expedition-blocked','막힘의 사실과 실제 가능한 수단을 모델에게 제공','선택한 이동을 수행하는 조향은 코드가 맡는다. 걷기가 막히면 원래 목적·이유·알거나 모르는 사실과 실행 가능한 수단을 제시한다. 새 수단은 Jev가 선택하며 미지 통로를 확정 귀환 우회로로 단정하지 않는다.','expeditionRoles','## 이동 중 막힘 처리 — 사용자 합의'),
 node('P.expedition-report','이탈 전 미검토 기회와 작업 전제 상실을 자동 보고','현장을 떠날 때 미검토 관련 기회가 있거나 작업 전제가 깨지고 맡겨진 범위에 대안이 없으면 상위에 자동 보고한다. 같은 사실의 중복 요청을 억제하고 후속 사실을 모으며 대기 중 판단·유효 행동을 유지하고 응답을 재검증한다.','expeditionRoles','## 채집 자동 호출과 응답 중 현장 판단 — 사용자 합의'),
 node('R.expedition-behavior-graph','현재 게시 행동 판단 그래프를 개발 근거로 활용','검수된 126묶음·2460사례의 대상→행위/처리→조건·결과·실패→원문을 조회한다. 전체 게임 행동 전수완료나 실제 AI 런타임 연결·실게임 성공을 뜻하지 않는다. 구체 구현이 사용한 조회와 코드 연결은 별도 인계 근거로 확정한다.','expeditionBehaviorGraph','## 현재 게시본'),
 {...node('R.expedition-direction','기존 실행·계획을 재사용하는 채집·이동·원정 책임','AIGathering의 고정 묶음 실행, AITravel의 탐색/목적지 이동, AIExpedition의 관측/맥락/선택 탐색을 기존 후보·AIActions·AISkill·AIPlanner에 연결하는 방향을 HEAD가 수용했다. 실제 생산 경로 대표 오프라인 실행을 계획하며 코드 freeze·구현 수용·실게임 완료는 별도다.','expeditionDirection','## 연결 구조'),status:'참고·HEAD 수용 방향·구현 진행',evidence:[ev('expeditionDirection','## 연결 구조'),ev('expeditionDirection','## 검증'),ev('expeditionDirectionAcceptance','## HEAD 수용 메시지')]},
 {...node('R.expedition-observed-map','캐릭터·월드별 정확 관측 통행기억의 필요성 수용','기존 미관측 BFS 의존과 coarse 방문표를 보완하기 위해 정확한 관측 통행기억 및 가시 재관측 갱신을 구현하는 방향을 HEAD가 수용했다. 다음 갈림길 선행 방향은 일반 기억이나 자원·위험 공개로 확대하지 않는다. 현재 시야의 빛·어둠 경계 문제는 별도 미해결이다.','expeditionDirectionAcceptance','## 정확 관측 통행기억에 대한 추가 수용'),status:'참고·HEAD 수용 방향·시야 경계 미해결'},
 {...node('R.expedition-vision-gap','사람 동일 시야의 조명·어둠 구현은 제품 결정 대기','게임 지도 공개는 호스트 카메라의 GPU irradiance를 사용한다. AI ThinClient에는 독립 조명 렌더가 없어 호스트 버퍼 밖 관측, AI별 오프스크린 렌더, CPU 근사의 제품·성능 차이를 결정해야 한다. 거리/사각형 CanSee를 사람 동일 시야 완료로 수용하지 않는다.','expeditionVisionInvestigation','## HEAD에 반환하는 판단'),status:'참고·R1 미해결·사용자 제품선택 대기'},
 {...node('R.expedition-first-review','첫 93a8bbc 후보의 독립 검수와 보완 요구','1차 검수에서 시야 R1, 넓은 통로 R2, 순환 R3, 명시 상한 R4, 고갈 보고 R5, 귀환 맥락 R6, 물체 접근 R7을 발견했다. 후속 e1cedcf 후보의 보완은 재검수 대기이며 이 1차 기록을 수정판 수용으로 승계하지 않는다.','expeditionFirstReview','## 발견 사항'),status:'참고·1차 검수·최종 수용 아님'}
];
const edge=(from,to,type,doc,anchor)=>({from,to,type,class:'해석',evidence:[ev(doc,anchor)]});
const edges=[
 edge('P.execution','P.expedition','선택된 행동 실행을 채집·이동·귀환 흐름으로 구체화','expeditionAgreement','## 3. 목표량·귀환·후속 작업 연결'),
 edge('P.original-request','P.expedition','중간 작업과 원래 부탁 보존','expeditionAgreement','## 3. 목표량·귀환·후속 작업 연결'),
 ...['choice','sidework','bundle','return','navigation','lookahead','blocked','report'].map(s=>edge('P.expedition','P.expedition-'+s,'채집·이동·귀환 합의의 구성',s==='bundle'||s==='navigation'||s==='blocked'||s==='report'?'expeditionRoles':'expeditionAgreement',s==='bundle'?'## 채집물 전반의 기본 선택 단위 — 사용자 합의':s==='navigation'?'## 이동 방식 합의와 현장 호출 기준':s==='blocked'?'## 이동 중 막힘 처리 — 사용자 합의':s==='report'?'## 채집 자동 호출과 응답 중 현장 판단 — 사용자 합의':s==='choice'?'## 1. 선택 트리와 직접 실행 병행':s==='sidework'?'## 2. 곁일은 판단모델에 위임':s==='return'?'## 3. 목표량·귀환·후속 작업 연결':'## 4. 선행 질문과 채집 묶음')),
 edge('P.unrestricted-work','P.expedition-return','상한 해제와 실제 실행 묶음·귀환 의미 연결','expeditionAgreement','## 3. 목표량·귀환·후속 작업 연결'),
 edge('P.valid-action','P.expedition-report','계획 응답 대기 중에도 유효 실행과 판단 유지','expeditionRoles','## 채집 자동 호출과 응답 중 현장 판단 — 사용자 합의'),
 edge('P.field-choice','P.expedition-sidework','현장 재량을 모델 판단에 위임','expeditionAgreement','## 2. 곁일은 판단모델에 위임'),
 edge('P.expedition','R.expedition-behavior-graph','참고: 실제 행동 조건·결과·실패의 개발 근거','expeditionBehaviorGraph','## 현재 게시본'),
 edge('P.expedition','R.expedition-direction','참고: 합의를 구현하는 책임과 연결 구조','expeditionDirection','## 연결 구조'),
 edge('P.expedition-bundle','R.expedition-direction','참고: 고정 묶음 생산 실행 경로','expeditionDirection','## 검증'),
 edge('P.expedition-navigation','R.expedition-direction','참고: 이동 의도와 기존 실행 재사용','expeditionDirection','## 연결 구조'),
 edge('P.expedition-navigation','R.expedition-observed-map','참고: 개인 관측 통행기억의 개발 방향','expeditionDirectionAcceptance','## 정확 관측 통행기억에 대한 추가 수용'),
 edge('P.expedition-navigation','R.expedition-vision-gap','참고: 동일 시야 구현의 조명 미해결','expeditionVisionInvestigation','## HEAD에 반환하는 판단'),
 edge('P.expedition-bundle','R.expedition-vision-gap','참고: 채집 일반 관측에도 같은 시야 경계 적용 필요','expeditionVisionInvestigation','## HEAD에 반환하는 판단'),
 edge('P.expedition','R.expedition-first-review','참고: 최초 후보의 독립 보완 요구','expeditionFirstReview','## 발견 사항')
];
const ids=new Set(current.nodes.map(n=>n.id));
for(const n of nodes){if(ids.has(n.id))throw Error('already present '+n.id);ids.add(n.id);}
for(const e of edges)if(!ids.has(e.from)||!ids.has(e.to))throw Error('missing edge endpoint');
const delta='\n// 확정 채집·이동·귀환 합의. 구현·검증 증거는 별도로 연결한다.\nObject.assign(documents,'+JSON.stringify(documents,null,2)+');\nnodes.push(...'+JSON.stringify(nodes,null,2)+');\nedges.push(...'+JSON.stringify(edges,null,2)+');\n';
fs.mkdirSync(stage,{recursive:true});
fs.writeFileSync(path.join(stage,'canonical-addition.mjs.txt'),delta);
fs.writeFileSync(path.join(stage,'delta-source.json'),json({documents,nodes,edges}));
const planning=path.join(stage,'planning'),pins=path.join(stage,'source-pins');
fs.mkdirSync(planning,{recursive:true});fs.mkdirSync(pins,{recursive:true});
for(const f of ['compile.mjs','graph.mjs'])fs.copyFileSync(path.join(canonical,f),path.join(planning,f));
const pinnedPaths={},pinManifest=[];
for(const [id,doc] of Object.entries({...current.documents,...documents})){
 const original=path.resolve(canonical,doc.path),bytes=fs.readFileSync(original),target=path.join(pins,id+path.extname(original));
 fs.writeFileSync(target,bytes);pinnedPaths[id]=target.replaceAll('\\','/');
 pinManifest.push({id,original,pinned:target,sha256:hash(bytes)});
}
fs.writeFileSync(path.join(stage,'source-pins.json'),json(pinManifest));
fs.writeFileSync(path.join(planning,'source.mjs'),fs.readFileSync(source,'utf8')+delta+'\nconst stagedPaths='+JSON.stringify(pinnedPaths)+';\nfor(const [id,doc] of Object.entries(documents))doc.path=stagedPaths[id];\n');
const canonicalFiles=[source,...['기획/정책.md','기획/기능.md','tools/config.json','tools/product-build.mjs'].map(x=>path.join(work,'지식그래프',x)),...['extractor/pin-snapshot.mjs','extractor/build.mjs','extractor/verify.mjs'].map(x=>path.join(work,'2026-11/2026-11-05-지식그래프-개편/B',x)),path.join(work,'2026-11/2026-11-05-지식그래프-실화면/D/planning.mjs')];
fs.writeFileSync(path.join(stage,'stage-manifest.json'),json({schemaVersion:1,preparedAt:new Date().toISOString(),officialHead:execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','rev-parse','HEAD'],{encoding:'utf8'}).trim(),reportedCandidate:'e1cedcf9033f5ec72f43555a27b3f59a9b565db1',candidateSourceExtracted:false,candidateReviewStatus:'independent re-review pending; R1 vision product decision pending',canonicalFiles:canonicalFiles.map(p=>({path:p,sha256:hash(fs.readFileSync(p))})),canonicalMutated:false,codeExtractionRun:false,implementationAccepted:false,liveGame:false,externalModelCalled:false,newNodes:nodes.length,newEdges:edges.length}));
console.log('Prepared '+nodes.length+' nodes / '+edges.length+' edges; canonical untouched; no code extraction.');
