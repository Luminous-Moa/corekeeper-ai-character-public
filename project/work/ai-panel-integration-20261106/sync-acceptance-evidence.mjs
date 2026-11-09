import fs from 'node:fs';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
const W='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work',T=W+'/ai-panel-integration-20261106',L=W+'/2026-11/2026-11-05-지식그래프-실화면',G=W+'/지식그래프';
const manifestPath=L+'/C/ui-20261106/observations.json',manifest=JSON.parse(fs.readFileSync(manifestPath,'utf8'));
const entry=manifest.inputs.find(x=>x.stagedPath==='C/ui-20261106/08-acceptance.md');
const source=fs.readFileSync(entry.originalPath),previous={sha256:entry.sha256,bytes:entry.bytes};
fs.writeFileSync(L+'/'+entry.stagedPath,source);
entry.sha256=crypto.createHash('sha256').update(source).digest('hex');entry.bytes=source.length;
fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2)+'\n');
const sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
for(const row of manifest.inputs)if(sha(row.originalPath)!==row.sha256||sha(L+'/'+row.stagedPath)!==row.sha256)throw Error('evidence mismatch '+row.stagedPath);
const result={previous,current:{sha256:entry.sha256,bytes:entry.bytes},evidenceOriginalAndCopyVerified:manifest.inputs.length,commands:[]};
for(const args of [['tools/query.mjs','check'],['tools/product-build.mjs'],['tools/product.mjs','check'],['tools/product-build.mjs','--check']]){
 const r=spawnSync(process.execPath,args,{cwd:G,encoding:'utf8',maxBuffer:16*1024*1024});
 const log='acceptance-sync-'+result.commands.length+'.log';fs.writeFileSync(T+'/evidence/'+log,r.stdout+'\n'+r.stderr);result.commands.push({args,exitCode:r.status,log});
 fs.writeFileSync(T+'/evidence/acceptance-evidence-sync.json',JSON.stringify(result,null,2)+'\n');
 console.log(args.join(' ')+' => '+r.status);if(r.status!==0){console.log((r.stdout+'\n'+r.stderr).slice(-4000));process.exit(1);}
}
console.log(JSON.stringify(result));
