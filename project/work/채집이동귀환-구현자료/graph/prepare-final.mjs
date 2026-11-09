import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {execFileSync} from 'node:child_process';import {fileURLToPath,pathToFileURL} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),result=path.dirname(stage),work=path.resolve(stage,'../..'),plan=path.join(work,'2026-11/2026-11-07-기획연결');
const target='7af8f91e5d26e4fd9ce5996809ae41e213340118',repo='C:/Dev/CoreKeeperModSDK',source=path.join(plan,'source.mjs');
const json=o=>JSON.stringify(o,null,2)+'\n',sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const git=(...args)=>execFileSync('git',['-C',repo,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==target)throw Error('official source is not final');
const delta=JSON.parse(fs.readFileSync(path.join(stage,'delta-source.json'),'utf8'));
const current=await import(pathToFileURL(source).href+'?'+Date.now());
const doc=(p,layer='참고')=>({layer,path:p});
Object.assign(delta.documents,{
 expeditionLightDecision:doc('../2026-11-08-채집이동귀환-조명후속분리.md','재기획'),
 expeditionHandoff:doc('../../채집이동귀환-구현자료/graph-handoff.md'),
 expeditionValidation:doc('../../채집이동귀환-구현자료/validation.md'),
 expeditionCoverage:doc('../../채집이동귀환-구현자료/coverage.md'),
 expeditionFinalReview:doc('../../채집이동귀환-구현자료/review.md'),
 expeditionIntegration:doc('../../채집이동귀환-구현자료/integration.json')
});
const ev=(doc,anchor)=>({doc,anchor}),node=(id,title,status,summary,evidence)=>({id,title,status,summary,layer:id.startsWith('P.')?'재기획':'참고',evidence});
const find=id=>{const n=delta.nodes.find(x=>x.id===id);if(!n)throw Error(id);return n;};
Object.assign(find('P.expedition-navigation'),{summary:'통로 탐색과 알려진 목적지 이동을 구별하고 개인 관측 통행기억·재관측을 사용한다. 외길·유효 목적지 경로는 매칸/매갈림길 재선택을 요구하지 않는다. 이번 수용은 기존 화면범위 시야이며 조명·어둠의 사람동일시야는 사용자 결정으로 후속 분리한다.',evidence:[...find('P.expedition-navigation').evidence,ev('expeditionLightDecision','사용자 결정:')]});
Object.assign(find('R.expedition-observed-map'),{status:'참고·7af8f91 구현·조명 후속',summary:'캐릭터·월드별 정확 관측 통행기억과 재관측 갱신을 구현했다. 4x4 coarse 기억을 정확 통로로 승격하지 않는다. 방향 선행조회는 일반 지도·자원·위험에 기록하지 않는다. 일반 관측은 기존 화면범위이며 조명 경계는 사용자 결정으로 후속 분리한다.'});
Object.assign(find('R.expedition-vision-gap'),{title:'조명·어둠의 사람동일시야는 사용자 결정으로 후속 분리',status:'참고·R1 미해결·후속 분리 확정',summary:'실제 조명은 호스트 GPU 렌더에 의존하고 AI별 광도 확보는 추가 아키텍처/실게임 검증이 필요하다. 사용자는 이번 채집·이동·귀환을 기존 화면범위로 수용하고 조명 시야를 후속으로 분리했다. R1 해결이나 CPU근사/오프스크린 구조 확정을 뜻하지 않는다.'});
Object.assign(find('R.expedition-first-review'),{title:'최초 후보의 보완 요구 이력',status:'참고·과거 1차/2차 발견 이력',summary:'93a8bbc의 R1~R7 및 e1cedcf의 R4/R5 잔여경로 발견을 보존한다. 현재 R2~R7은 7af8f91 최종검수 수용을 따르고 R1은 사용자 결정으로 후속 분리한다. 이력의 선택대기 문구를 현재 미결 승인상태로 재적용하지 않는다.'});
delta.nodes.push(
 node('P.expedition-light-followup','이번 행동 구현은 화면범위 시야로 수용하고 조명은 후속 분리','사용자 결정·현재 범위','이번7af8f91 채집·이동·귀환은 기존 화면범위 관측으로 수용한다. 사람과 같은 조명·어둠 시야 R1은 해결이 아닌 별도 후속이며 CPU근사/오프스크린 방식은 정하지 않았다. 설치·실게임·외부 모델 보류를 유지한다.',[ev('expeditionLightDecision','사용자 결정:'),ev('expeditionLightDecision','- 이번 수용 범위는')]),
 node('R.expedition-implementation','7af8f91 채집·이동·원정 생산 구현','참고·공식 통합·오프라인 수용','직접/트리 선택·고정 묶음과 실제 확보·개인 통행기억·방향선행 선택·곁일/원작업·자동상위보고·귀환후속을 기존 실행과 계획에 연결했다. max_count/max_order는 사용자 명시 조건의 소유·수명이며 일반 목표량 상한이 아니다.',[ev('expeditionHandoff','## 최종 7af8f91 앵커 보정·추가'),ev('expeditionCoverage','|합의|구현 앵커|검증 근거|남은 경계|')]),
 node('R.expedition-offline','실제 생산 경로의 원정·후속 보고·조건 수명 검증','참고·오프라인 실행·미실게임','execution92는 실제 Apply→body/관측→RequestReplan/Report/pending·HTTP payload와 finish_goal→OnGoalEnded→다음tick→StartNextStep→SetGoalFrom을 실행했다. gather61/native14/travel43/navigator9/producer11와 관련 회귀·전체 .NET/Unity compile을 연결한다. 물리/ECS와 외부 transport는 fixture 경계다.',[ev('expeditionValidation','## 대표 생산 경로'),ev('expeditionValidation','## Unity와 소스 동일성')]),
 node('R.expedition-final-review','7af8f91의 R2~R7 수정과 생산 경로 독립 수용','참고·독립 수용·조명 후속·미실게임','최종 독립검수에서 R2~R7 수정과 추가 중대블로커 부재를 확인했다. 시야 R1은 검수 당시 미해결이었고 이후 사용자가 후속 분리했다. 이전 Unity 로그 대신 최종v2 CompileCheck ok/exit0만 확인했다.',[ev('expeditionFinalReview','## 3차 최종 코드 검수 · 7af8f91e5d26e4fd9ce5996809ae41e213340118'),ev('expeditionFinalReview','### 직접 검증 결과와 한계')]),
 node('R.expedition-integration','공식 ai-character 7af8f91 통합과 기존 변경 보존','참고·공식 소스 통합','HEAD가 기존5cc에서7af8f91로 fast-forward 통합했다. 기존 사용자 dirty 파일 해시를 보존했고 설치·실게임을 실행하지 않았다. 그래프 코드층은 이 정확 커밋에서 재생성한다.',[ev('expeditionIntegration','"afterHead"'),ev('expeditionIntegration','"originalDirtyPreserved"')]),
 node('R.expedition-game-evidence','실제 조회한 채굴·문·다리의 정적 게임 근거','참고·게시본 조회 원문·미실게임','곡괭이 정상입력·피해·드롭 조건, 문 variation/toggle, 다리 pit/water 배치조건·소비·결과의 정확 source를 개발 근거로 사용했다. 최신 행동그래프 전체를 복제하거나 게임 성공으로 승격하지 않으며 이 작업은 해당 그래프를 수정하지 않는다.',[ev('expeditionHandoff','## 사용한 게임행동 그래프 조회와 원문근거')])
);
const e=(from,to,type,doc,anchor)=>({from,to,type,class:'해석',evidence:[ev(doc,anchor)]});
delta.edges.push(
 e('P.expedition','P.expedition-light-followup','이번 수용의 사용자 조정 범위','expeditionLightDecision','사용자 결정:'),
 e('P.expedition-navigation','P.expedition-light-followup','관측 시야의 현재 수용 범위','expeditionLightDecision','- 이번 수용 범위는'),
 e('P.expedition-light-followup','R.expedition-vision-gap','참고: 후속 조명 과제의 원본 조사','expeditionVisionInvestigation','## HEAD에 반환하는 판단'),
 e('P.expedition-light-followup','R.expedition-first-review','참고: 과거 시야 선택대기의 후속 결정','expeditionLightDecision','- [독립 검수]'),
 e('P.expedition','R.expedition-implementation','참고: 확정 범위의 실제 코드','expeditionHandoff','## 최종 7af8f91 앵커 보정·추가'),
 e('P.expedition','R.expedition-final-review','참고: 최종 구현의 독립 수용','expeditionFinalReview','## 3차 최종 코드 검수'),
 e('P.expedition','R.expedition-integration','참고: 공식 소스 통합','expeditionIntegration','"afterHead"'),
 e('R.expedition-implementation','R.expedition-offline','참고: 실제 생산 경로 검증','expeditionValidation','## 대표 생산 경로'),
 e('R.expedition-implementation','R.expedition-final-review','참고: 독립 검수 범위','expeditionFinalReview','## 3차 최종 코드 검수'),
 e('R.expedition-implementation','R.expedition-game-evidence','참고: 사용한 게임 규칙 원문','expeditionHandoff','## 사용한 게임행동 그래프 조회와 원문근거')
);
const allIds=new Set(current.nodes.map(n=>n.id));for(const n of delta.nodes){if(allIds.has(n.id))throw Error('duplicate '+n.id);allIds.add(n.id);}for(const x of delta.edges)if(!allIds.has(x.from)||!allIds.has(x.to))throw Error('missing endpoint');
const canonicalDocs={},pinManifest=[];fs.mkdirSync(path.join(stage,'final-source-pins'),{recursive:true});
for(const [id,d] of Object.entries(delta.documents)){
 const original=path.resolve(plan,d.path),bytes=id==='expeditionBehaviorGraph'?fs.readFileSync(path.join(stage,'source-pins/expeditionBehaviorGraph.md')):fs.readFileSync(original);
 const pinned=path.join(stage,'final-source-pins',id+path.extname(original));fs.writeFileSync(pinned,bytes);
 canonicalDocs[id]={...d,path:path.relative(plan,pinned).replaceAll('\\','/'),originalPath:d.path};
 pinManifest.push({id,original,pinned,sha256:sha(bytes),snapshotNote:id==='expeditionBehaviorGraph'?'126판 준비 당시 실제 조회 안내를 보존. 최신 게시본 전체 완료 주장이 아니다.':null});
}
delta.documents=canonicalDocs;
fs.writeFileSync(path.join(stage,'final-source-pins.json'),json(pinManifest));
fs.writeFileSync(path.join(stage,'final-delta.json'),json(delta));
const addition='\n// 채집·이동·귀환 최종 합의/사용자 조명후속 결정과7af8f91의 정적·오프라인 근거.\nObject.assign(documents,'+JSON.stringify(delta.documents,null,2)+');\nnodes.push(...'+JSON.stringify(delta.nodes,null,2)+');\nedges.push(...'+JSON.stringify(delta.edges,null,2)+');\n';
fs.writeFileSync(path.join(stage,'final-canonical-addition.mjs.txt'),addition);
const staged=path.join(stage,'final-planning');fs.mkdirSync(staged,{recursive:true});
for(const f of ['compile.mjs','graph.mjs'])fs.copyFileSync(path.join(plan,f),path.join(staged,f));
const paths=Object.fromEntries(Object.entries({...current.documents,...canonicalDocs}).map(([id,d])=>[id,path.resolve(plan,d.path).replaceAll('\\','/')]));
fs.writeFileSync(path.join(staged,'source.mjs'),fs.readFileSync(source,'utf8')+addition+'\nconst finalStagePaths='+JSON.stringify(paths)+';\nfor(const[id,d]of Object.entries(documents))d.path=finalStagePaths[id];\n');
const old=JSON.parse(fs.readFileSync(path.join(stage,'stage-manifest.json'),'utf8'));
const updates=old.canonicalFiles.map(x=>({path:x.path,beforeSha256:x.sha256,sha256:sha(fs.readFileSync(x.path))}));
fs.writeFileSync(path.join(stage,'final-preparation.json'),json({sourceCommit:target,officialHead:target,canonicalSourceSha256:sha(fs.readFileSync(source)),canonicalFiles:updates,parallelChanges:updates.filter(x=>x.beforeSha256!==x.sha256),newNodes:delta.nodes.length,newEdges:delta.edges.length,implementationAcceptedWithinAdjustedScope:true,lighting:'deferred-by-user-not-fixed',liveGame:false,codeExtractionRun:false}));
console.log('final delta prepared '+delta.nodes.length+' nodes/'+delta.edges.length+' edges; latest canonical preserved');
