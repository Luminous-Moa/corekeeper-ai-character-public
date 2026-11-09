import fs from 'node:fs';import path from 'node:path';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const B=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),repo='C:/Dev/CoreKeeperModSDK',commit='20b1aced7594a47eacb4be060405bcb53d38f48f';
for(const [dest,paths] of [['snapshot',['Assets/AIPlayerSpike','Assets/HeadlessTools','Tools/ai-bridge']],['runtime-package',['Tools/ai-bridge','Tools/knowledge']]]){
 const rows=execFileSync('git',['-C',repo,'ls-tree','-r',commit,...paths],{encoding:'utf8'}).trim().split(/\r?\n/);
 for(const row of rows){const [,blob,rel]=row.match(/^\d+ blob ([a-f0-9]+)\t(.+)$/)||[];if(!rel)continue;const full=path.join(B,dest,rel);fs.mkdirSync(path.dirname(full),{recursive:true});fs.writeFileSync(full,execFileSync('git',['-C',repo,'cat-file','blob',blob],{maxBuffer:16*1024*1024}));}
}
console.log('Pinned snapshot and runtime-package to exact Git blob bytes (no working-tree filters).');
