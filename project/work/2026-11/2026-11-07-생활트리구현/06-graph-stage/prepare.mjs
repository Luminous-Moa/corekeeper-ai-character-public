import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url));
const original=path.resolve(stage,'../../2026-11-07-기획연결');
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const documents={
 lifeTreeAgreement:{layer:'재기획',path:'../2026-11-07-생활트리구현/00-agreement.md'},
 lifeTreeDirection:{layer:'재기획',path:'../2026-11-07-생활트리구현/02-direction-decision.md'},
 lifeTreeSpecApproval:{layer:'재기획',path:'../2026-11-07-생활트리구현/03-spec-decision.md'},
 lifeTreeSpec:{layer:'참고',path:'../2026-11-07-생활트리구현/03-spec.md'}
};
const ev=(doc,anchor)=>({doc,anchor});
const n=(id,title,status,summary,doc,anchor)=>({id,title,layer:id.startsWith('R.')?'참고':'재기획',status,summary,evidence:[ev(doc,anchor)]});
const nodes=[
 n('P.life-tree','생활 선택지를 필요한 단계로 좁혀 탐색','합의','기지 생활에서 작업·대상/재료·시설/구역을 필요한 수준만 펼쳐 본다. 수량·칸의 모든 조합을 후보로 열거하지 않는다. 작업 순서는 AI가 정한다.','lifeTreeAgreement','- 트리는 AI가 필요한 선택지를 좁혀 조회하는 구조다.'),
 n('P.life-tree-readonly','탐색은 현재 몸 행동을 바꾸지 않는 조회','합의','뒤로·루트·다른 갈래로 탐색하고 닫을 수 있다. 탐색만으로 유효한 몸 행동·대기 선택·목표·원래 부탁을 취소하거나 완료하지 않는다.','lifeTreeAgreement','- 탐색 도중 돌아가기와 다른 작업 선택이 가능하다.'),
 n('P.life-tree-direct','아는 대상은 트리를 거치지 않고 직접 실행','합의','대상·재료·시설·수량이 정해졌으면 루트부터 다시 내려가지 않는다. 트리 말단과 직접 실행이 같은 실행·현재 상태 재검증 입구를 쓴다.','lifeTreeAgreement','- AI가 이미 대상과 수량을 알면 바로 실행한다.'),
 n('P.life-tree-count','수량은 상위 작업량으로 전달','HEAD 승인·구현 기준','상위 count/범위를 현장에 전달하고 수량별 후보를 나열하지 않는다. 현장 자유 정수 생성 지원을 가정하지 않으며 수량 변경은 request_plan으로 연결한다. 원래 부탁 수량·단계 요청량·이번 실제 실행량을 구별한다.','lifeTreeDirection','수량 조합을 열거하지 않고 상위 계획의 작업량을 쓰며'),
 n('P.life-tree-first-scope','첫 단위는 제작·요리·농사·보관/꺼내기','HEAD 승인·구현 범위','공통 탐색과 기존 네 갈래의 실제 선택·실행·결과를 연결한다. 승인된 범위이며 아직 구현 완료나 정상 AI 실게임 관측을 뜻하지 않는다. 가방장비 실행을 별도로 복제하지 않는다.','lifeTreeDirection','첫 구현 단위는 공통 트리와 제작·요리·농사·보관/꺼내기의 실제 선택 확대이다.'),
 n('P.life-tree-result','실행량·확보량과 원래 부탁의 완료를 구분','HEAD 승인·구현 기준','현재 목표 밖 행동을 원래 목표 진척에 합산하지 않는다. 실제 실행/확보량만 보고하며 시설·커서·바닥 잔류를 가방 확보로 세지 않는다. 단계 완료와 원래 부탁 완료의 주체를 유지한다.','lifeTreeSpecApproval','실제 실행/확보한 만큼만 결과를 보고한다.'),
 n('Q.life-tree-followup','수리·자동 가공·강화·거래·낚시는 후속 실행 단위','후속 범위·이번 단위 미구현','첫 네 갈래 이후의 새 몸 실행 단위다. 탐색 갈래나 실행 가능한 말단으로 이름만 올려 지원 능력으로 표시하지 않는다. 전체 시설 생활 완료로 보고하지 않는다.','lifeTreeDirection','수리·자동 가공·강화·거래·낚시의 새 몸 실행은 개발 방향의 후속 단위로 유지한다.'),
 n('R.life-tree-spec','생활 트리 첫 단위의 승인 명세','참고·승인된 구현 명세','수정번호에 묶인 캐릭터별 임시 커서·단계 페이지·실제 대상 재검증·부분 결과와 실제 생산코드 어댑터 검증을 정한 명세다. 명세의 존재는 구현·검증의 성공 증거가 아니다.','lifeTreeSpec','## 1. 입력과 구현 경계')
];
const e=(from,to,type,doc,anchor,classification='해석')=>({from,to,type,class:classification,evidence:[ev(doc,anchor)]});
const edges=[
 e('P.life','P.life-tree','생활 행동의 선택 탐색으로 구체화','lifeTreeAgreement','## 지킬 결과','합의'),
 e('P.noncombat-choice','P.life-tree','비전투 선택지 탐색으로 연결','lifeTreeAgreement','- 트리는 AI가 필요한 선택지를 좁혀 조회하는 구조다.'),
 e('P.life-tree','P.life-tree-readonly','몸 실행과 조회를 구별','lifeTreeAgreement','- 탐색 도중 돌아가기와 다른 작업 선택이 가능하다.','합의'),
 e('P.life-tree','P.life-tree-direct','이미 지정한 선택의 직접 실행','lifeTreeAgreement','- AI가 이미 대상과 수량을 알면 바로 실행한다.','합의'),
 e('P.life-tree','P.life-tree-count','숫자 조합을 만들지 않는 구현 기준','lifeTreeDirection','수량 조합을 열거하지 않고 상위 계획의 작업량을 쓰며','HEAD 승인'),
 e('P.life-tree','P.life-tree-first-scope','첫 구현 단위로 구체화','lifeTreeDirection','첫 구현 단위는 공통 트리와 제작·요리·농사·보관/꺼내기의 실제 선택 확대이다.','HEAD 승인'),
 e('P.life-tree-first-scope','P.life-tree-result','실제 결과와 진척의 구현 기준','lifeTreeSpecApproval','실제 실행/확보한 만큼만 결과를 보고한다.','HEAD 승인'),
 e('P.original-request','P.life-tree-result','부탁 보존을 생활 결과에도 적용','lifeTreeAgreement','- 시설 투입·생산·가방 수령과 실제 장비 변화, 작업 단계 완료와 원래 부탁 완료를 구별한다.'),
 e('P.valid-action','P.life-tree-readonly','조회 중 유효한 행동 보존','lifeTreeAgreement','- 탐색 도중 돌아가기와 다른 작업 선택이 가능하다.'),
 e('P.life-tree-first-scope','Q.life-tree-followup','전체 시설 생활의 남은 단위','lifeTreeDirection','수리·자동 가공·강화·거래·낚시의 새 몸 실행은 개발 방향의 후속 단위로 유지한다.','HEAD 승인'),
 e('P.life-tree-first-scope','R.life-tree-spec','참고: 승인된 구체 구현 명세','lifeTreeSpec','## 1. 입력과 구현 경계'),
 e('P.knowledge','P.life-tree','탐색에도 캐릭터 지식 범위 유지','lifeTreeAgreement','- 기존 자기 밭/솥/상자 등 권한과 캐릭터에게 열린 지식을 유지한다.')
];

const candidateFile=path.join(stage,'candidate-code-current.json');
let candidate=null;
if(fs.existsSync(candidateFile)){
 candidate=JSON.parse(fs.readFileSync(candidateFile,'utf8'));
 documents.lifeTreeCandidate={layer:'참고',path:'../2026-11-07-생활트리구현/06-graph-stage/candidate-implementation.md'};
 nodes.push(n('R.life-tree-candidate','생활 트리의 격리 구현 후보 '+candidate.sourceCommit.slice(0,7),'참고·격리 구현 후보·검증 진행중','고정 Git blob과 기존 Roslyn으로 대표 생산 입구만 추출한 후보 색인이다. 공식 반영·설치·실게임·최종 수용은 미확인이다. 테스트 진행 보고를 최종 커밋 검증으로 승격하지 않는다.','lifeTreeCandidate','## 고정 후보와 지위'));
 nodes.push(n('R.life-tree-candidate-code','생활 트리 대표 코드 앵커의 부분 추출','참고·정적 코드 대응','관련'+candidate.checks.files+'소스에서 실제 심볼'+candidate.checks.anchors+'개를 확인했다. 부분 정적 참조는 전체 호출 경로나 실행 trace가 아니며 정식 제품 코드층을 교체하지 않는다.','lifeTreeCandidate','## 대표 코드와 경계'));
 edges.push(e('P.life-tree-first-scope','R.life-tree-candidate','참고: 진행 중인 격리 구현','lifeTreeCandidate','## 고정 후보와 지위'));
 edges.push(e('R.life-tree-candidate','R.life-tree-candidate-code','참고: 고정판의 실제 생산 심볼','lifeTreeCandidate','## 대표 코드와 경계'));
 if(fs.existsSync(path.join(stage,'candidate-validation.md'))){
  documents.lifeTreeCandidateValidation={layer:'참고',path:'../2026-11-07-생활트리구현/06-graph-stage/candidate-validation.md'};
  nodes.push(n('R.life-tree-self-validation','생활 트리 고정판의 자체검증과 Unity 확인','참고·격리 자체검증·독립수용 대기','고정 후보의 실제 생산코드 어댑터·회귀·fast compile·Unity 자체검증 보고를 연결한다. 어댑터 경계와 전체스케줄러/실제게임 미확인을 유지하고 최종 독립 수용으로 승격하지 않는다.','lifeTreeCandidateValidation','## 생산자 자체검증과 수용 경계'));
  edges.push(e('R.life-tree-candidate','R.life-tree-self-validation','참고: 고정판의 생산자 자체검증','lifeTreeCandidateValidation','## 생산자 자체검증과 수용 경계'));
 }
}

const sourcePath=path.join(original,'source.mjs');
const before=fs.readFileSync(sourcePath,'utf8');
const baseline=await import(pathToFileURL(sourcePath));
for(const n of nodes)if(baseline.nodes.some(old=>old.id===n.id))throw Error('기존 ID 충돌: '+n.id);
for(const d of Object.keys(documents))if(d in baseline.documents)throw Error('문서 ID 충돌: '+d);
const addition='\n// 생활 트리: 승인된 방향·범위만 추가. 구현/실게임 성공은 별도 근거가 생긴 뒤 연결한다.\n'+
 'Object.assign(documents, '+JSON.stringify(documents,null,2)+');\n'+
 'nodes.push(...'+JSON.stringify(nodes,null,2)+');\n'+
 'edges.push(...'+JSON.stringify(edges,null,2)+');\n';
fs.writeFileSync(path.join(stage,'source.mjs'),before+addition);
fs.writeFileSync(path.join(stage,'source-addition.txt'),addition);
const compiler=fs.readFileSync(path.join(original,'compile.mjs'),'utf8');
const rootLine="const root=path.dirname(fileURLToPath(import.meta.url));";
if(!compiler.includes(rootLine))throw Error('compile.mjs의 원문 기준 경로 선언이 달라짐');
fs.writeFileSync(path.join(stage,'compile-stage.mjs'),compiler.replace(rootLine,'// Stage 사본만 import하고 근거 문서는 정식 원문 위치에서 읽는다.\nconst root='+JSON.stringify(original.replaceAll('\\','/'))+';'));
fs.writeFileSync(path.join(stage,'stage-inputs.json'),JSON.stringify({
 status:candidate?'approved-design-with-isolated-candidate-pending':'approved-design-only',officialMutated:false,sourceCommit:'685dde4d27dd91a782a14d8ae33fb576160fa578',candidateCommit:candidate?.sourceCommit??null,finalAcceptance:'pending',
 canonicalSource:sourcePath,canonicalSourceSha256:sha(before),
 canonicalCompiler:path.join(original,'compile.mjs'),canonicalCompilerSha256:sha(compiler),
 stagedSourceSha256:sha(before+addition),
 entryPreserved:baseline.entry,baselineNodes:baseline.nodes.length,baselineEdges:baseline.edges.length,
 addedNodes:nodes.map(n=>n.id),addedEdges:edges.length,
 implementationEvidencePending:'../05-implementation.md'
},null,2)+'\n');
console.log('기존 '+baseline.nodes.length+'노드/'+baseline.edges.length+'관계 보존; 승인 설계 '+nodes.length+'노드/'+edges.length+'관계 stage. 공식 파일 변경 없음.');
