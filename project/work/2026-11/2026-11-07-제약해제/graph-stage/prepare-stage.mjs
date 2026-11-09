import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url));
const work='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work';
const canonical=path.join(work,'2026-11/2026-11-07-기획연결');
const repo='C:/Dev/CoreKeeperModSDK-unrestricted';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const json=x=>JSON.stringify(x,null,2)+'\n';
const sourceFile=path.join(canonical,'source.mjs');
const canon=await import(pathToFileURL(sourceFile).href+'?pin='+Date.now());
const docs={
 unrestrictionAgreement:{layer:'재기획',path:'../2026-11-07-제약해제/00-agreement.md'},
 unrestrictionCriteria:{layer:'참고',path:'../2026-11-07-제약해제/01-acceptance.md'},
 unrestrictionAudit:{layer:'참고',path:'../2026-11-07-제약감사/보고.md'},
 unrestrictionKnowledge:{layer:'참고',path:'../2026-11-07-제약감사/05-지식해금과채집허용.md'},
 unrestrictionInfo:{layer:'참고',path:'../2026-11-07-제약해제/info.md'}
};
const ev=(doc,anchor)=>({doc,anchor});
const n=(id,title,status,summary,doc,anchor)=>({id,title,layer:id.startsWith('R.')?'참고':'재기획',status,summary,evidence:[ev(doc,anchor)]});
const nodes=[
 n('P.unrestricted-actions','임의 행동·정보 제약 전반 해제','사용자 지시·구현 진행','감사의 수량·계획·지시·후보·정보·공간 및 지원행동 제한을 전반 해제한다. 사용자 목표이며 구현 완료나 새 실게임 성공을 뜻하지 않는다.','unrestrictionAgreement','사용자는 감사에서 현재 제약으로 보고한 항목을 전반적으로 해제하라고 지시했다.'),
 n('P.unrestricted-work','큰 작업량·긴 계획·열린 부탁을 보존','사용자 지시·구현 진행','채집30·제작10·배치30, 계획 단계·열린 부탁 개수의 임의 상한을 없애고 총량/원래 부탁을 실행 묶음과 구분한다.','unrestrictionAgreement','범위:'),
 n('P.unrestricted-choice','생활 페이지·후보 압축과 목표/허기 기반 숨김 해제','사용자 지시·구현 진행','생활 분류를 유지하며5개 페이지 제한을 없앤다. 옛 방위별 탐험 대표 압축과 목표 밖 채집 후보 숨김을 제거하고 합의된 이동과 연결한다.','unrestrictionAgreement','생활 분류 기능 자체 대신'),
 n('P.unrestricted-space','기지·농장·장거리의 고정 공간상한 해제','사용자 지시·새 공간정책 미정','기지/농장 고정범위와 장거리 이동 거절을 해제한다. 새 거점 구역이나 농장 배치 정책을 이 지시로 임의 확정하지 않는다.','unrestrictionAgreement','새로 거점·농장 정책을 임의로 정하지 않는다.'),
 n('P.unrestricted-information','보고·대화·기억검색의 임의 잘림 해제','사용자 지시·구현 진행','상위 계획과 현장 선택에 필요한 정보가 top-N/글자수만으로 누락되지 않게 한다. 캐릭터가 모르는 지식을 공개하는 변경은 아니다.','unrestrictionAgreement','후보와 정보의 잘림'),
 n('P.unrestricted-support','지원 행동 유형도 정상 실행과 실제 결과까지 확대','사용자 추가 지시·구현 진행','도구/무기 및 특정 적 전용 제어 제한을 미구현이란 이유로 제외하지 않는다. 이름만 노출하지 않고 선택→정상 입력→실제 결과 관측을 연결한다.','unrestrictionAgreement','사용자 추가 확인으로 사용자 부탁 수·계획 길이·장거리 이동·지원 행동 범위를 모두 포함한다.'),
 n('P.unrestricted-guards','지식공개·소유권·비용·실제 게임조건 유지','합의·유지 조건','본 것·보유품·본 제작대의 레시피/재료로 공개하는 지식정책을 유지한다. 게임 슬롯/충돌/재료·정상요청·사용자예산/구독·중복실행방지는 별도 의미다.','unrestrictionAgreement','실제 게임 물품/슬롯/충돌/재료 조건,'),
 n('R.unrestriction-audit','해제 판단의 현행5a 감사와 지식해금 구분','참고·정적 조사','제약 위치와 성격을 찾은 감사다. 지식 공개와 별도 gather 고정목록임을 확인했고, 과거8/14 후보 cap은 이미 제거돼 있었음을 구분한다.','unrestrictionAudit','# AI의 계획·선택·정보 제약 감사 결과'),
 n('R.unrestriction-candidate','격리 제약해제 후보와 완료 판단 기준','참고·구현중·미수용·미실게임','격리 ai-remove-restrictions-20261107 후보를 구현/검수 중이다. 후보 SHA와 실제 테스트의 별도 증거가 있어야 구현을 수용할 수 있다. 본 반영·설치·게임 성공은 미확정이다.','unrestrictionCriteria','# 완료 판단 기준'),
 n('R.unrestriction-info','정보·기억·채팅 해제의 오프라인 후보검증','참고·정보영역 자체검증·통합수용대기','목차·검색·경험·대화·전투정보 잘림을 제거한 후보 자체검증이다. memgraph84/recall41/retention7과experience ALL PASS를 보고했다. 전체 통합·독립검수·게임 성공으로 확대하지 않는다.','unrestrictionInfo','## 검증')
];
const e=(from,to,type,doc,anchor,classification='해석')=>({from,to,type,class:classification,evidence:[ev(doc,anchor)]});
const edges=[
 e('P.execution','P.unrestricted-actions','사용자 지시에 따른 실행범위 갱신','unrestrictionAgreement','사용자는 감사에서'),
 e('P.noncombat-choice','P.unrestricted-choice','현장 선택을 임의 개수·목표필터로 제한하지 않음','unrestrictionAgreement','범위:'),
 ...['work','choice','space','information','support','guards'].map(x=>e('P.unrestricted-actions','P.unrestricted-'+x,'해제 범위와 유지 조건','unrestrictionAgreement',x==='guards'?'실제 게임 물품/슬롯/충돌/재료 조건,':'범위:')),
 e('P.original-request','P.unrestricted-work','긴 계획·여러 부탁도 원래 목적 보존','unrestrictionAgreement','사용자 추가 확인으로'),
 e('P.knowledge','P.unrestricted-guards','캐릭터 공개 지식은 유지','unrestrictionAgreement','사람 시야와 본 것·보유품·본 제작대 레시피/재료로 지식 공개를 여는 정책은 유지한다.'),
 e('P.unrestricted-actions','R.unrestriction-audit','참고: 해제 전 활성 제약 조사','unrestrictionAudit','# AI의 계획·선택·정보 제약 감사 결과'),
 e('P.unrestricted-actions','R.unrestriction-candidate','참고: 후보의 완료 판단 경계','unrestrictionCriteria','# 완료 판단 기준'),
 e('P.unrestricted-information','R.unrestriction-info','참고: 정보영역 후보의 자체검증','unrestrictionInfo','## 검증')
];
const historical=[
 {id:'R.life-tree-spec',title:'과거 생활 트리 첫 단위의 승인 명세',status:'참고·과거 승인명세·페이지정책 대체',summary:'당시 임시 커서·단계 페이지·대상 재검증·부분 결과의 승인 명세다. 페이지 제한은 P.unrestricted-choice의 새 지시로 대체하며, 조회/실행·진척 분리와 실제 재검증 의미는 유지한다.'},
 {id:'R.bag-cap-audit',title:'과거39 후보 상한과 소비자 제한 구별',status:'참고·과거39 적용판정·해제범위 확대',summary:'당시 현장 후보 cap을 없애고 보고/기억/로그를 별도 유지한 판정이다. 현재 정보잘림 해제는 P.unrestricted-information을 따르며, 호출비용·지식공개·실제게임조건과의 구분은 유지한다.'},
 {id:'R.bag-life-consumers',title:'과거39 생활·가방 소비자 계약 통합',status:'참고·과거39 통합계약',summary:'당시 가방 무페이지·생활 페이지 보존의 통합 판단이다. 현재 생활 페이지의 유지 정책은 P.unrestricted-choice의 새 해제 지시로 대체된다. pending·진척·몸행동 보존 의미는 이어 쓴다.'},
 {id:'R.maintenance-consumers',title:'과거5a 정비와 생활·가방 소비자 경계',status:'참고·과거5a 통합기준',summary:'5a 통합 당시 생활 페이지·직접실행과 가방 메뉴의 소비자 경계를 기록한다. 현재 페이지 제한의 제거는 P.unrestricted-choice를 따른다. pending·정확 슬롯·실제 결과의 의미는 별도 유지 조건이다.'}
];
for(const h of historical)if(!canon.nodes.some(x=>x.id===h.id))throw Error('historical ID absent '+h.id);
for(const h of historical)edges.push(e('P.unrestricted-choice',h.id,'참고: 과거 페이지 정책의 대체 범위','unrestrictionAgreement','생활 분류 기능 자체 대신'));
const ids=new Set(canon.nodes.map(x=>x.id));
for(const x of nodes){if(ids.has(x.id))throw Error('existing delta '+x.id);ids.add(x.id);}
for(const x of edges)if(!ids.has(x.from)||!ids.has(x.to))throw Error('endpoint absent');
let delta='\n// 사용자 지시: 임의 제약 해제. 구현/검증/본반영 지위는 별도 참고에서 갱신한다.\n';
delta+='Object.assign(documents,'+JSON.stringify(docs,null,2)+');\n';
delta+='nodes.push(...'+JSON.stringify(nodes,null,2)+');\n';
delta+='edges.push(...'+JSON.stringify(edges,null,2)+');\n';
delta+='for(const update of '+JSON.stringify(historical,null,2)+'){ const item=nodes.find(n=>n.id===update.id); if(!item)throw Error("Historical node absent "+update.id); Object.assign(item,update); }\n';
fs.mkdirSync(stage,{recursive:true});fs.writeFileSync(path.join(stage,'canonical-addition.mjs.txt'),delta);
fs.writeFileSync(path.join(stage,'delta-source.json'),json({documents:docs,nodes,edges,historicalUpdates:historical}));
const staged=path.join(stage,'planning');fs.mkdirSync(staged,{recursive:true});
for(const name of ['compile.mjs','graph.mjs'])fs.copyFileSync(path.join(canonical,name),path.join(staged,name));
const pins=path.join(stage,'source-pins');fs.mkdirSync(pins,{recursive:true});
const paths={},pinManifest=[];
for(const [id,doc] of Object.entries({...canon.documents,...docs})){
 const original=path.resolve(canonical,doc.path),bytes=fs.readFileSync(original),target=path.join(pins,id+path.extname(original));
 fs.writeFileSync(target,bytes);paths[id]=target.replaceAll('\\','/');
 pinManifest.push({id,original,pinned:target,sha256:sha(bytes)});
}
fs.writeFileSync(path.join(stage,'source-pins.json'),json(pinManifest));
const normalize='\n// Stage only: immutable evidence copies, not part of canonical addition.\nconst stagedEvidencePaths='+JSON.stringify(paths)+';\nfor(const [id,d] of Object.entries(documents))d.path=stagedEvidencePaths[id];\n';
fs.writeFileSync(path.join(staged,'source.mjs'),fs.readFileSync(sourceFile,'utf8')+delta+normalize);
const head=execFileSync('git',['-C',repo,'rev-parse','HEAD'],{encoding:'utf8'}).trim();
const status=execFileSync('git',['-C',repo,'status','--porcelain'],{encoding:'utf8'}).trim();
fs.writeFileSync(path.join(stage,'stage-manifest.json'),json({schemaVersion:1,canonicalSource:sourceFile,canonicalSourceSha256:sha(fs.readFileSync(sourceFile)),baseCommit:'5a05b80c4fe1ea764caa62ee83d06ac7fb3177ef',candidateHead:head,candidateDirty:!!status,candidateStatus:status,codeExtractionRun:false,implementationAccepted:false,canonicalMutated:false,installed:false,liveGame:false,newNodes:nodes.length,newEdges:edges.length,historicalUpdates:historical.map(x=>x.id)}));
console.log('prepared '+nodes.length+' nodes / '+edges.length+' edges; canonical unchanged');
