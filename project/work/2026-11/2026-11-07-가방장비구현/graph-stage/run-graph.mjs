import fs from 'node:fs';import path from 'node:path';import {spawnSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),P='work/2026-11/2026-11-05-지식그래프-개편',L='work/2026-11/2026-11-05-지식그래프-실화면',G='work/지식그래프';
if(!fs.existsSync(path.join(stage,'final-pin.json')))throw Error('Final owner-confirmed pin required');
const from=Number(process.argv[2]||1),through=Number(process.argv[3]||13),prior=path.join(stage,'command-results.json');
const rows=from>1&&fs.existsSync(prior)?JSON.parse(fs.readFileSync(prior,'utf8')).filter(r=>Number(r.id.slice(0,2))<from):[];fs.mkdirSync(path.join(stage,'evidence'),{recursive:true});
function run(id,bin,args,cwd=stage){if(Number(id.slice(0,2))<from||Number(id.slice(0,2))>through)return;const r=spawnSync(bin,args,{cwd,encoding:'utf8',maxBuffer:64*1024*1024});const file='evidence/'+id+'.txt';fs.writeFileSync(path.join(stage,file),(r.stdout||'')+(r.stderr||''));rows.push({id,bin,args,cwd,exitCode:r.status,evidence:file});fs.writeFileSync(path.join(stage,'command-results.json'),JSON.stringify(rows,null,2)+'\n');console.log(id+': '+r.status+' '+(r.stdout||'').split(/\r?\n/).slice(0,3).join(' | '));if(r.error||r.status!==0)throw r.error||Error(id+' failed; '+file);}
run('01-extract','dotnet',['run','--project',path.join(stage,P,'B/extractor/Extract.csproj'),'--',path.join(stage,P,'B/snapshot'),path.join(stage,P,'B/csharp.raw.json')]);
run('02-code-build',process.execPath,[path.join(stage,P,'B/extractor/build.mjs')]);
run('03-code-verify',process.execPath,[path.join(stage,P,'B/extractor/verify.mjs')]);
run('04-code-determinism',process.execPath,[path.join(stage,P,'B/extractor/determinism.mjs')]);
run('05-development-build',process.execPath,['tools/build.mjs','--strict'],path.join(stage,G));
run('06-development-check',process.execPath,['tools/query.mjs','check'],path.join(stage,G));
run('07-planning-integrate',process.execPath,[path.join(stage,P,'C/integrate-c.mjs')]);
run('08-planning-build',process.execPath,[path.join(stage,P,'C/build-graph-C.mjs')]);
run('09-screen-remap',process.execPath,[path.join(stage,L,'B/remap-base.mjs')]);
run('10-screen-validate',process.execPath,[path.join(stage,L,'C/validate-C.mjs')]);
run('11-product-build',process.execPath,['tools/product-build.mjs'],path.join(stage,G));
run('12-product-check',process.execPath,['tools/product.mjs','check'],path.join(stage,G));
run('13-product-determinism',process.execPath,['tools/product-build.mjs','--check'],path.join(stage,G));
