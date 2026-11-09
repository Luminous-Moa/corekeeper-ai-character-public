import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';import {execFileSync} from 'node:child_process';
const stage=path.dirname(fileURLToPath(import.meta.url)),result=path.dirname(stage),month=path.dirname(result),file=path.join(stage,'successor-projection.json');
const p=JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,'')),repo='C:/Dev/CoreKeeperModSDK',hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const b=c=>execFileSync('git',['-C',repo,'rev-parse',c+':'+p.unchangedSource.file],{encoding:'utf8'}).trim();if(b(p.priorEvidenceCommit)!==b(p.currentSourceCommit))throw Error('menu blob changed');
p.unchangedSource.priorBlob=b(p.priorEvidenceCommit);p.unchangedSource.currentBlob=b(p.currentSourceCommit);p.unchangedSource.sha256=hash(execFileSync('git',['-C',repo,'cat-file','blob',p.currentSourceCommit+':'+p.unchangedSource.file]));
for(const input of p.integrationSources)input.sha256=hash(fs.readFileSync(path.resolve(month,'2026-11-05-지식그래프-개편',input.path)));
fs.writeFileSync(file,JSON.stringify(p,null,2)+'\n');console.log('current projection source hashes refreshed; historical evidence unchanged');
