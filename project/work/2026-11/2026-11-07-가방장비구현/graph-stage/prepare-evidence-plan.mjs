import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),sha=JSON.parse(fs.readFileSync(path.join(stage,'final-pin.json'),'utf8')).sourceCommit;
const inputs=['compile','core','protocol','execution','adapter'].map(name=>({file:'final-'+name+'.log',kind:'최종 통합판 오프라인 검사 로그',sourceCommit:sha}));
for(const file of ['agreement.md','inventory-spec.md','integration-spec.md','transfer-investigation.md','transfer-rules.md'])inputs.push({file,kind:'plan',sourceCommit:null});
const plan={sourceCommit:sha,summary:'최종 통합판 컴파일 0경고·0오류, Core 1201, protocol 118, execution 36/36, 실제 소스 최소 World 어댑터 40/0 검사 로그를 고정했다. Unity 컴파일과 최종 독립검수 결과는 진행 중이며 완료 결과를 수용한 뒤 이 문서를 갱신한다.',validation:{compile:{passed:true,warnings:0,errors:0},core:{passed:1201},protocol:{passed:118},execution:{passed:36},adapter:{passed:40,failed:0,scope:'실제 제품 어댑터+수동 상태 최소 World; 게임 소비자 미실행'},unity:{status:'대기'},independentReview:{status:'진행 중'},liveGame:{observed:false}},inputs};
fs.writeFileSync(path.join(stage,'evidence-plan.json'),JSON.stringify(plan,null,2)+'\n');
