import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url));
const canonical='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터';
const G='work/지식그래프',P='work/2026-11/2026-11-05-지식그래프-개편',L='work/2026-11/2026-11-05-지식그래프-실화면';
const read=p=>fs.readFileSync(path.join(stage,p),'utf8').replace(/^\uFEFF/,'');
const write=(p,s)=>fs.writeFileSync(path.join(stage,p),s);
const json=(p,o)=>write(p,JSON.stringify(o,null,2)+'\n');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const manifest=JSON.parse(read('canonical-before.json'));
const dev=JSON.parse(read(G+'/graph/graph.json'));
for(const source of dev.nodes.flatMap(n=>n.sources||[])){
 if(!source.path||path.isAbsolute(source.path))continue;
 const src=path.resolve(canonical,source.path),dest=path.resolve(stage,source.path);
 if(!fs.existsSync(src)||!dest.startsWith(stage+path.sep))continue;
 fs.mkdirSync(path.dirname(dest),{recursive:true});fs.copyFileSync(src,dest);
 manifest.files.push({path:source.path,sha256:hash(src)});
}
manifest.files=[...new Map(manifest.files.map(r=>[r.path,r])).values()];json('canonical-before.json',manifest);
// The existing exact Git-blob snapshot is used for a reproducible before comparison.
const cfg=JSON.parse(read(G+'/tools/config.json'));
cfg.codeRoot=path.join(stage,P,'B/snapshot').replaceAll('\\','/');
json(G+'/tools/config.json',cfg);
json('baseline-summary.json',{sourceCommit:JSON.parse(read(L+'/D/graph.json')).meta.sourceCommit,development:{nodes:dev.nodes.length,edges:dev.edges.length,unresolved:dev.meta.unresolvedByKind},product:(()=>{const g=JSON.parse(read(L+'/D/graph.json'));return{nodes:g.nodes.length,edges:g.edges.length,sourceCommit:g.meta.sourceCommit};})()});
console.log('Prepared citation copies and baseline snapshot config.');
