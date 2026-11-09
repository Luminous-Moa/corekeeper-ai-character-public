import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url));
const read=f=>JSON.parse(fs.readFileSync(path.join(stage,f),'utf8'));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const before=read('stage-manifest.json'),delta=read('delta-source.json'),g=read('planning/graph.json');
const mutated=before.canonicalFiles.filter(f=>hash(fs.readFileSync(f.path))!==f.sha256);
const pinFailures=read('source-pins.json').filter(p=>hash(fs.readFileSync(p.pinned))!==p.sha256);
const reached=new Set([g.entry]),todo=[g.entry];
while(todo.length){const from=todo.shift();for(const e of g.edges.filter(e=>e.from===from))if(!reached.has(e.to)){reached.add(e.to);todo.push(e.to);}}
const unreachable=delta.nodes.filter(n=>!reached.has(n.id)).map(n=>n.id);
const r=spawnSync(process.execPath,['planning/graph.mjs','check'],{cwd:stage,encoding:'utf8'});
const result={ok:mutated.length===0&&pinFailures.length===0&&unreachable.length===0&&r.status===0,canonicalChanged:mutated,pinFailures,unreachable,planningCheck:r.status,newNodes:delta.nodes.length,newEdges:delta.edges.length,codeExtractionRun:false,implementationAccepted:false};
fs.writeFileSync(path.join(stage,'preparation-verification.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify(result,null,2));
if(!result.ok)process.exitCode=1;
