import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
const root=path.dirname(fileURLToPath(import.meta.url));
const queries=[['check'],['path','P.release-goal','P.experience'],['source','P.release-goal'],['overview'],['path','P.experience','P.execution'],['path','P.experience','Q.memory-rules'],['path','P.experience','R.missing'],['show','P.agency'],['source','P.agency'],['source','R.missing']];
const results=queries.map(args=>{const r=spawnSync(process.execPath,['graph.mjs',...args],{cwd:root,encoding:'utf8'});return {args,exit:r.status,stdout:r.stdout,stderr:r.stderr,error:r.error?.message};});
const g=JSON.parse(fs.readFileSync(path.join(root,'graph.json'),'utf8'));
const seen=new Set([g.entry]),queue=[g.entry];
while(queue.length){const current=queue.shift();for(const e of g.edges.filter(e=>e.from===current))if(!seen.has(e.to)){seen.add(e.to);queue.push(e.to);}}
const unreachable=g.nodes.filter(n=>!seen.has(n.id)).map(n=>n.id);
const report={ok:results.every(r=>r.exit===0)&&!unreachable.length,counts:{nodes:g.nodes.length,relations:g.edges.length,planning:g.nodes.filter(n=>n.layer==='재기획').length,references:g.nodes.filter(n=>n.layer==='참고').length,sources:Object.keys(g.sources).length},unreachable,queries:results};
fs.writeFileSync(path.join(root,'verification.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({ok:report.ok,counts:report.counts,unreachable,queryResults:results.map(r=>({args:r.args,exit:r.exit}))},null,2));
if(!report.ok)process.exitCode=1;
