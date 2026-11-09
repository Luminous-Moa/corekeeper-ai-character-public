import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url));
const work=path.resolve(stage,'../../..');
const month=path.join(work,'2026-11');
const B=path.join(month,'2026-11-05-지식그래프-개편/B');
const C=path.join(month,'2026-11-05-지식그래프-개편/C');
const live=path.join(month,'2026-11-05-지식그래프-실화면');
const legacy=path.join(work,'지식그래프');
const logs=path.join(stage,'final-logs');
fs.mkdirSync(logs,{recursive:true});
const phases=[
  ['code-verify',B,['extractor/verify.mjs']],
  ['planning',path.join(month,'2026-11-07-기획연결'),['graph.mjs','build']],
  ['legacy-build',legacy,['tools/build.mjs','--strict']],
  ['legacy-check',legacy,['tools/query.mjs','check']],
  ['C-integrate',C,['integrate-c.mjs']],
  ['C-build',C,['build-graph-C.mjs']],
  ['screen-remap',path.join(live,'B'),['remap-base.mjs']],
  ['screen-validate',path.join(live,'C'),['validate-C.mjs']],
  ['product-build',legacy,['tools/product-build.mjs']],
  ['product-check',legacy,['tools/product.mjs','check']],
  ['product-determinism',legacy,['tools/product-build.mjs','--check']]
];
const start=process.argv[2];
let active=!start;
for(const[name,cwd,args]of phases){
  if(name===start)active=true;
  if(!active)continue;
  const result=spawnSync(process.execPath,args,{cwd,encoding:'utf8',maxBuffer:128*1024*1024});
  fs.writeFileSync(path.join(logs,name+'.log'),(result.stdout??'')+(result.stderr??''));
  console.log(name+' exit='+result.status);
  if(result.error||result.status!==0)throw Error(name+' failed, see final-logs');
}
