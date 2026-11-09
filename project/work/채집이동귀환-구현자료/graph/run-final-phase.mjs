import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {spawnSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../..'),month=path.join(work,'2026-11'),B=path.join(month,'2026-11-05-지식그래프-개편/B'),C=path.join(month,'2026-11-05-지식그래프-개편/C'),live=path.join(month,'2026-11-05-지식그래프-실화면'),legacy=path.join(work,'지식그래프'),logs=path.join(stage,'final-logs');
fs.mkdirSync(logs,{recursive:true});
function run(name,cmd,args,cwd){const r=spawnSync(cmd,args,{cwd,encoding:'utf8',maxBuffer:128*1024*1024});fs.writeFileSync(path.join(logs,name+'.log'),(r.stdout??'')+(r.stderr??''));console.log(name+' exit='+r.status);if(r.error||r.status!==0)throw Error(name+' '+(r.error?.message??'see log'));}
const mode=process.argv[2];
if(mode==='code'){
 run('code-pin',process.execPath,['extractor/pin-snapshot.mjs'],B);
 run('code-roslyn','dotnet',['run','--project','extractor/Extract.csproj','--','snapshot','csharp.raw.json'],B);
 run('code-build',process.execPath,['extractor/build.mjs'],B);
 run('code-verify',process.execPath,['extractor/verify.mjs'],B);
 const files=['code.graph.json','B.graph.json','javascript.raw.json','validation.json','boundaries.json','call-forms.json','snapshot.manifest.json'],hash=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(B,p))).digest('hex'),before=Object.fromEntries(files.map(p=>[p,hash(p)]));
 run('code-build-repeat',process.execPath,['extractor/build.mjs'],B);
 const changed=files.filter(p=>before[p]!==hash(p));fs.writeFileSync(path.join(stage,'final-code-generation.json'),JSON.stringify({ok:changed.length===0,changed,files:before,roslynRuns:1,scope:'same fixed Roslyn/JS source generation'},null,2)+'\n');if(changed.length)throw Error('code generation changed');
}else if(mode==='integrate'){
 run('planning-build',process.execPath,['graph.mjs','build'],path.join(month,'2026-11-07-기획연결'));
 run('legacy-build',process.execPath,['tools/build.mjs','--strict'],legacy);
 run('legacy-check',process.execPath,['tools/query.mjs','check'],legacy);
 run('C-integrate',process.execPath,['integrate-c.mjs'],C);
 run('C-build',process.execPath,['build-graph-C.mjs'],C);
 run('screen-remap',process.execPath,['remap-base.mjs'],path.join(live,'B'));
 run('screen-C-validate',process.execPath,['validate-C.mjs'],path.join(live,'C'));
 run('product-build',process.execPath,['tools/product-build.mjs'],legacy);
 run('product-check',process.execPath,['tools/product.mjs','check'],legacy);
 run('product-determinism',process.execPath,['tools/product-build.mjs','--check'],legacy);
}else throw Error('code | integrate');
