import fs from 'node:fs';import path from 'node:path';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../../..'),target=process.argv[2];
const B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B'),official=execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','rev-parse','HEAD'],{encoding:'utf8'}).trim();
for(const file of [path.join(work,'지식그래프/README.md'),path.join(work,'지식그래프/제품그래프-사용법.md'),path.join(B,'README.md')]){
 let text=fs.readFileSync(file,'utf8');
 text=text.replace(/^> 제약해제 현재 코드층.*\n\n/,'').replace(/^> 현재 공식 소스와 코드층 기준은/m,'> 과거 공식 소스와 코드층 기준은');
 const link=file.startsWith(B)?'../../2026-11-07-제약해제/graph.md':'../2026-11/2026-11-07-제약해제/graph.md';
 const note='> 제약해제 현재 코드층 기준은 `'+target+'`다. 공식 소스는 `'+official+'`'+(official===target?'로 같다.':'이며 현재 코드층은 아직 후보 기준이다.')+' 임의 제약 해제와 새 지원동작의 정적 연결/오프라인 근거는 [이번 반영]('+link+')을 따른다. 과거 실게임·모델 증거를 새 코드판 성공으로 승격하지 않는다.\n\n';
 fs.writeFileSync(file,note+text);
}
console.log('stamped code='+target+' official='+official);
