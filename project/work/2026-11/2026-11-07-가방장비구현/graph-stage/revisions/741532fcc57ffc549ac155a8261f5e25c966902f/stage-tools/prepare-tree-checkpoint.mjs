import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),parent=path.dirname(stage),task='work/2026-11/2026-11-07-가방장비구현',sha=JSON.parse(fs.readFileSync(path.join(stage,'final-pin.json'),'utf8')).sourceCommit;
const read=p=>fs.readFileSync(path.join(stage,p),'utf8'),write=(p,s)=>fs.writeFileSync(path.join(stage,p),s),hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const docs=['agreement.md','tree-agreement.md','tree-spec.md','inventory-category-cap-audit.md','cap-removal-spec.md','tree-cap-result.md','tree-design-review.md'];
const manifest=JSON.parse(read('canonical-before.json'));
for(const file of docs){const rel=task+'/'+file,src=path.join(parent,file);fs.copyFileSync(src,path.join(stage,rel));manifest.files=manifest.files.filter(r=>r.path!==rel);manifest.files.push({path:rel,sha256:hash(src)});}
write('canonical-before.json',JSON.stringify(manifest,null,2)+'\n');
const inputs=[...docs.map(file=>({file,kind:file.endsWith('agreement.md')?'사용자 합의 원문':file==='tree-design-review.md'?'설계 독립검토;구현 수용 아님':'명세·정적 조사/구현 범위 기록',sourceCommit:null})),...['compile','core','protocol'].map(name=>({file:'tree-checkpoint-'+name+'.log',kind:'생산자 체크포인트 오프라인 검사',sourceCommit:sha})),{file:'tree-loop-execution.log',kind:'실제 Loop/공통 입구 소스 링크 검사;World/모델 대역',sourceCommit:sha},{file:'tree-menu-adapter/results.log',kind:'메뉴·Inventory 소스 링크 단독 어댑터;게임 소비자 미실행',sourceCommit:sha},{file:'tree-menu-adapter/payloads.json',kind:'현재 노드 메뉴 샘플;단독 어댑터 구성;실제 모델 호출 아님',sourceCommit:sha}];
for(const file of ['Program.cs','Stubs.cs','TreeMenu.csproj'])inputs.push({file:'tree-menu-adapter/'+file,kind:'단독 메뉴 소스 링크 재현 fixture;제품 git 소스와 구별',sourceCommit:sha});
const plan={sourceCommit:sha,checkpoint:true,accepted:false,summary:'분류 기반 트리 체크포인트 '+sha+'의 생산자 컴파일0경고·0오류/Core1204/protocol118/실제Loop소스 링크55/단독 메뉴어댑터326 통과 로그와 현재 노드 메뉴 샘플을 고정했다. 연결 trace와 최종 독립검수는 진행 중이며 트리 Unity·설치·실게임·외부 모델 호출·최종 수용은 없다. flat 774/1fc 검수 및 774 Unity는 역사적 근거로만 보존한다.',validation:{compile:{passed:true,warnings:0,errors:0},core:{passed:1204},protocol:{passed:118},actualLoop:{passed:55,scope:'실제 Loop/공통 제어 입구 소스 링크;World·모델응답 대역'},isolatedMenu:{passed:326,failed:0,scope:'실제 menu/inventory 소스;fake World·큐·수동 결과;별도 단독검사'},connectedTrace:{status:'진행 중'},independentReview:{status:'진행 중;체크포인트 미수용'},unity:{status:'트리판 미실행;774 Unity 승계금지'},liveGame:{observed:false},externalModel:{called:false}},inputs};
if(sha==='741532fcc57ffc549ac155a8261f5e25c966902f'){
 const b1='b1facaaef2afd66af321db66fc4b5d00725af5ce';
 for(const input of inputs)if(input.file.startsWith('tree-checkpoint-')||input.file==='tree-loop-execution.log'||input.file==='tree-menu-adapter/results.log'){input.sourceCommit=b1;input.kind+=';b1 원검사(역사적 로그)';}
 for(const file of ['tree-menu-engine.md','tree-menu-payload-example.md']){
  const rel=task+'/'+file,src=path.join(parent,file);fs.copyFileSync(src,path.join(stage,rel));manifest.files=manifest.files.filter(r=>r.path!==rel);manifest.files.push({path:rel,sha256:hash(src)});inputs.push({file,kind:'소스 모듈 설명/현재 노드 예시;실게임 근거 아님',sourceCommit:null});
 }
 write('canonical-before.json',JSON.stringify(manifest,null,2)+'\n');
 inputs.push({file:'tree-menu-adapter/role-results.log',kind:'741 생산자 단독 메뉴 역할 회귀348/0;원326과 별도',sourceCommit:sha},{file:'tree-menu-role-compile.log',kind:'741 생산자 컴파일0경고·0오류',sourceCommit:sha});
 for(const file of ['run.ps1','Connected.csproj','Program.cs','Stubs.cs','Boundary.cs','Production.cs','source-pin.json','results.log','connected-trace.json'])inputs.push({file:'tree-connected/'+file,kind:'741 실제 메뉴+공개surface/BuildJson+실제Apply/registry+InventorySkill 연결검사;대역World/소비상태',sourceCommit:sha});
 plan.summary='741 트리 고정판의 생산자 컴파일0경고·0오류와 메뉴 역할 회귀348/0, 실제 메뉴→공개surface/BuildJson→실제Apply/동작등록→InventorySkill 연결102/102를 고정했다. 연결 검사는 로컬 요청구성19회·navigation17회이며 원래 goal/order를 유지하고 정상Swap요청 한 번을 관측했다. World·DB·네트워크·게임 소비상태는 대역/수동 공급이므로 외부모델·게임 성공이 아니다. b1의 Core1204/protocol118/Loop55와 단독메뉴326 원로그는 역사적 판으로 구별한다. 최종 독립검수와741 Unity/설치없는export는 진행 중이며 최종수용은 없다.';
 plan.validation.compile={passed:true,warnings:0,errors:0,sourceCommit:sha};
 plan.validation.core={passed:1204,sourceCommit:b1,scope:'역사적b1 원검사;741 변경은 메뉴파일에 한정'};
 plan.validation.protocol={passed:118,sourceCommit:b1};
 plan.validation.actualLoop.sourceCommit=b1;
 plan.validation.isolatedMenu={passed:348,failed:0,sourceCommit:sha,controlledSelections:101,scope:'별도 role-results.log. 원326과 독립394를 합산하지 않음;대역World/수동결과'};
 plan.validation.connectedTrace={passed:102,failed:0,sourceCommit:sha,constructedRequests:19,navigationChoices:17,externalModels:0,normalSwapRequests:1,scope:'실제 메뉴·surface/BuildJson·Apply/registry·InventorySkill 연결. 대역World·소비상태 수동 공급;완전한 runtime tick/네트워크/게임 소비자 아님'};
 plan.validation.unity={status:'741 실행/결과 대기;774 승계금지;b1 export 실패는 역사적 시도'};
}
write('evidence-plan.json',JSON.stringify(plan,null,2)+'\n');
const state=JSON.parse(read('stage-state.json'));Object.assign(state,{status:'트리 체크포인트 그래프 재생성 중;연결trace·독립검수·Unity·수용 대기',codeExtractionCommit:sha,finalAcceptance:false,checkpoint:true});write('stage-state.json',JSON.stringify(state,null,2)+'\n');
console.log('Prepared '+inputs.length+' bounded checkpoint evidence sources.');
