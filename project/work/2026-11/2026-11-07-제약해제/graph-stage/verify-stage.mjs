import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {spawnSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url)),folder=path.join(root,'planning');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const manifest=JSON.parse(fs.readFileSync(path.join(root,'stage-manifest.json'),'utf8'));
const canonicalUnchanged=hash(fs.readFileSync(manifest.canonicalSource))===manifest.canonicalSourceSha256;
const queries=[['check'],['path','P.release-goal','P.unrestricted-actions'],['path','P.release-goal','P.unrestricted-support'],['source','P.unrestricted-guards'],['source','P.unrestricted-work'],['show','R.bag-life-consumers'],['show','R.maintenance-consumers'],['show','R.bag-cap-audit'],['show','R.life-tree-spec'],['show','R.unrestriction-candidate'],['show','R.unrestriction-info']];
const results=queries.map(args=>{const p=spawnSync(process.execPath,['graph.mjs',...args],{cwd:folder,encoding:'utf8'});return {args,exit:p.status,stdout:p.stdout,stderr:p.stderr};});
const g=JSON.parse(fs.readFileSync(path.join(folder,'graph.json'),'utf8'));
const seen=new Set([g.entry]),q=[g.entry];while(q.length){const id=q.shift();for(const e of g.edges.filter(e=>e.from===id))if(!seen.has(e.to)){seen.add(e.to);q.push(e.to);}}
const delta=JSON.parse(fs.readFileSync(path.join(root,'delta-source.json'),'utf8'));const unreachable=delta.nodes.filter(n=>!seen.has(n.id)).map(n=>n.id);
const after=hash(fs.readFileSync(path.join(folder,'graph.json')));
const rebuild=spawnSync(process.execPath,['graph.mjs','build'],{cwd:folder,encoding:'utf8'});
const deterministic=after===hash(fs.readFileSync(path.join(folder,'graph.json')));
const report={ok:canonicalUnchanged&&deterministic&&rebuild.status===0&&results.every(r=>r.exit===0)&&unreachable.length===0,canonicalUnchanged,deterministic,unreachable,newNodes:delta.nodes.length,newEdges:delta.edges.length,planningNodeCount:g.nodes.length,planningEdgeCount:g.edges.length,codeExtractionRun:false,implementationAccepted:false,queries:results};
fs.writeFileSync(path.join(root,'verification.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({...report,queries:results.map(({args,exit})=>({args,exit}))},null,2));if(!report.ok)process.exitCode=1;
