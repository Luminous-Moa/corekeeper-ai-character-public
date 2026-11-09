import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),parent=path.dirname(stage),T='work/2026-11/2026-11-07-가방장비구현',read=p=>fs.readFileSync(path.join(stage,p),'utf8'),write=(p,s)=>fs.writeFileSync(path.join(stage,p),s),hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const pin=JSON.parse(read('final-pin.json')),plan=JSON.parse(read('evidence-plan.json')),manifest=JSON.parse(read('canonical-before.json')),reviewRoot='C:/Dev/CoreKeeperModSDK-bag-review-tree/review-repro';
const parseFile=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
if(pin.sourceCommit!=='741532fcc57ffc549ac155a8261f5e25c966902f')throw Error('741 final source pin required');
for(const file of ['tree-implementation.md','tree-validation.md','tree-runtime-plan.md','review-tree.md']){
 const src=path.join(parent,file);if(!fs.existsSync(src))throw Error('Final source document pending: '+file);fs.copyFileSync(src,path.join(stage,T,file));
 manifest.files=manifest.files.filter(r=>r.path!==T+'/'+file);manifest.files.push({path:T+'/'+file,sha256:hash(src)});
 plan.inputs=plan.inputs.filter(r=>r.file!==file);plan.inputs.push({file,kind:file==='review-tree.md'?'독립 소스·오프라인 검수/Unity·source export 근거 수용;공식 반영·실게임 승인 아님':'현재 고정판 구현/검증/실게임 준비 원문',sourceCommit:pin.sourceCommit});
}
for(const file of ['tree-final-unity-input.json','tree-final-unity-exit.json','tree-final-unity-export.log','tree-artifact-manifest.json','tree-package.json','BagCandidateExport.cs','tree-validation-continuity.json']){
 plan.inputs=plan.inputs.filter(r=>r.file!==file);plan.inputs.push({file,kind:'741 Unity compile+설치없는SDK source export;101 .cs와manifest,바이너리배포/실게임 아님',sourceCommit:pin.sourceCommit});
}
for(const [file,commit] of [['compile-741.log',pin.sourceCommit],['menu-741.log',pin.sourceCommit],['connected.log',pin.sourceCommit],['741532f-input-pin.json',pin.sourceCommit],['export-source-verification.json',pin.sourceCommit],['core.log','b1facaaef2afd66af321db66fc4b5d00725af5ce'],['protocol.log','b1facaaef2afd66af321db66fc4b5d00725af5ce'],['execution.log','b1facaaef2afd66af321db66fc4b5d00725af5ce'],['primitive.log','b1facaaef2afd66af321db66fc4b5d00725af5ce']]){
 const label='independent-review/'+file;plan.inputs=plan.inputs.filter(r=>r.file!==label);plan.inputs.push({file:label,absolutePath:path.join(reviewRoot,file),kind:'독립 검수 원로그/입력 대응;생산자348과394를 합산하지 않음',sourceCommit:commit});
}
const exit=parseFile(path.join(parent,'tree-final-unity-exit.json')),artifact=parseFile(path.join(parent,'tree-artifact-manifest.json')),pkg=parseFile(path.join(parent,'tree-package.json')),verification=parseFile(path.join(reviewRoot,'export-source-verification.json'));
if([exit,artifact,pkg,verification].some(x=>x.sourceCommit!==pin.sourceCommit)||exit.exitCode!==0)throw Error('Unity/export source or completion mismatch');
if(artifact.productCodeChecks.length!==101||artifact.productCodeChecks.some(r=>!r.sourceUnchanged||!r.packageMatches))throw Error('Producer/export raw source mismatch');
if(verification.rows.length!==101||verification.rows.some(r=>!r.producerRawMatchesPin||!r.exportRawMatchesPin||!r.reviewDiffersOnlyLineEnds))throw Error('Independent exported source mismatch');
const raw=verification.rows.filter(r=>r.reviewRawMatchesPin).length,eol=verification.rows.length-raw;
if(hash(pkg.zip.path)!==pkg.zip.sha256.toLowerCase())throw Error('Source package ZIP hash differs');
Object.assign(plan,{checkpoint:true,accepted:false,stagingEvidenceReady:true,summary:'741 고정판의 생산자 컴파일0/0·메뉴 역할348/0, 독립 메뉴394/0·연결102/102와 최신 compile0/0, 자체 Unity exit0/CompileCheck:ok/설치없는SDK source export 완료 근거를 고정했다. 독립 Core1204/protocol118/Loop55/primitive67은 b1에서 실행했고741 diff가 메뉴 위치 역할에 한정되어 관련 입력 불변을 확인한 근거다. 연결검사는 요청구성19·navigation17·정상Swap1이며 World/소비상태는 대역/수동공급이다. SDK 후보는101 .cs+manifest의 소스패키지이며 producer/export raw101일치, review raw'+raw+'+EOL-only'+eol+'를 구별한다. 공식 적용·실게임·실제외부모델·바이너리배포 수용은 아니다.'});
plan.validation.independentReview={passed:true,sourceCommit:pin.sourceCommit,menu:{passed:394,failed:0},connected:{passed:102,failed:0},compile:{warnings:0,errors:0},carriedFromB1:{core:1204,protocol:118,actualLoop:55,primitive:67,scope:'b1 독립 실행;741 변경과 관련 입력 불변을 검수자가 대조'},scope:'소스·오프라인 검수;Unity/source package 근거 대조. 공식반영·실게임 아님'};
plan.validation.unity={status:'741 자체 컴파일/source export 완료;이전774승계금지',sourceCommit:pin.sourceCommit,exitCode:0,compileCheckOk:true,csharpCompilerErrors:0};
plan.validation.sdkSourcePackage={sourceCommit:pin.sourceCommit,sourceFiles:101,producerRawMatch:101,exportRawMatch:101,reviewRawMatch:raw,reviewEolOnly:eol,zipSha256:pkg.zip.sha256.toLowerCase(),binaryRelease:false,installed:false};
write('evidence-plan.json',JSON.stringify(plan,null,2)+'\n');write('canonical-before.json',JSON.stringify(manifest,null,2)+'\n');console.log('Pinned completed review/build evidence; review source '+raw+' raw + '+eol+' EOL-only.');
