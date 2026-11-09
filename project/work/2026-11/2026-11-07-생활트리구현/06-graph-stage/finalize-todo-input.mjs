import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../../..'),out=path.resolve(stage,'..');
const todo=path.resolve(work,'../todo.md'),graph=path.join(work,'지식그래프');
const hash=()=>crypto.createHash('sha256').update(fs.readFileSync(todo)).digest('hex'),before=hash();
const run=args=>execFileSync(process.execPath,args,{cwd:graph,encoding:'utf8',maxBuffer:12*1024*1024});
const build=run(['tools/product-build.mjs']);
const checked=run(['tools/product.mjs','check']);fs.writeFileSync(path.join(out,'graph-product-check.json'),checked);
const check=JSON.parse(checked),determinism=run(['tools/product-build.mjs','--check']);fs.writeFileSync(path.join(out,'graph-product-determinism.log'),determinism);
const after=hash(),result={ok:check.ok&&check.structuralErrors.length===0&&check.stale.length===0&&!check.baselineDrift.warning&&before===after,
 todoPreserved:before===after,todoSha256:after,sourceCommit:check.baselineDrift.baseline,structuralErrors:check.structuralErrors.length,stale:check.stale.length,baselineWarning:check.baselineDrift.warning,build:build.trim(),determinism:determinism.trim(),boundary:'HEAD가 추가한 최신todo를 그대로 입력으로 보존하고 제품 파생물만 재생성; 제품코드·게임·기획/기능 원본 변경 없음'};
fs.writeFileSync(path.join(out,'graph-final-todo-refresh.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify(result,null,2));if(!result.ok)process.exitCode=1;
