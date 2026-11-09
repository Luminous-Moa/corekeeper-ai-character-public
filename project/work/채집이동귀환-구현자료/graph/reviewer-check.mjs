import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath,pathToFileURL} from 'node:url';
const dir=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(dir,'../..');
const B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B'),D=path.join(work,'2026-11/2026-11-05-지식그래프-실화면/D');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8')),hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const repo='C:/Dev/CoreKeeperModSDK',commit='7af8f91e5d26e4fd9ce5996809ae41e213340118';
function git(...args){const r=spawnSync('git',['-C',repo,...args],{encoding:'utf8'});if(r.status!==0)throw Error(r.stderr);return r.stdout.trim();}
const g=read(path.join(D,'graph.json')),b=read(path.join(B,'code.graph.json'));
const failures=[],check=(ok,msg)=>{if(!ok)failures.push(msg);};
check(git('rev-parse','HEAD')===commit&&g.meta.sourceCommit===commit&&b.meta.sourceCommit===commit,'revision mismatch');
const pins=read(path.join(dir,'final-source-pins.json')),pinResults=[];
for(const p of pins){const pinned=hash(p.pinned),original=hash(p.original);check(pinned===p.sha256,'pin '+p.id);check(original===pinned||!!p.snapshotNote,'unexplained original drift '+p.id);pinResults.push({id:p.id,pinMatch:pinned===p.sha256,originalMatch:original===pinned,snapshotNote:p.snapshotNote});}
const manifest=read(path.join(dir,'final-manifest.json'));for(const p of manifest.paths)check(hash(p.path)===p.sha256,'manifest '+p.path);
const mapping=read(path.join(dir,'new-code-mappings.json')).mapping,anchors=[];
for(const [from,sig] of mapping){const candidates=b.nodes.filter(n=>n.id.startsWith('code:')&&n.id.split('#')[1]?.startsWith(sig));check(candidates.length===1,'signature '+sig);if(candidates.length!==1)continue;const n=candidates[0],file=n.file,blob=git('rev-parse',commit+':'+file),snapshot=git('hash-object',path.join(B,'snapshot',file));const edges=g.edges.filter(e=>e.from===from&&e.to===n.id);check(blob===snapshot,'snapshot '+file);check(edges.length===1&&edges[0].runtimeTrace===false&&edges[0].sourceCommit===commit,'edge '+sig);anchors.push({from,id:n.id,file,blob,snapshot,edgeCount:edges.length});}
const delta=read(path.join(dir,'final-delta.json')),reached=new Set([g.planning.entry]),queue=[g.planning.entry];for(const id of queue)for(const e of g.edges.filter(e=>e.from===id)){if(!reached.has(e.to)){reached.add(e.to);queue.push(e.to);}}
for(const n of delta.nodes)check(reached.has(n.id),'unreachable '+n.id);
const old=await import(pathToFileURL(path.join(dir,'before-final-graph/기획연결/source.mjs'))),now=await import(pathToFileURL(path.join(work,'2026-11/2026-11-07-기획연결/source.mjs')));
for(const n of old.nodes)check(now.nodes.some(x=>JSON.stringify(x)===JSON.stringify(n)),'prior node changed '+n.id);
for(const e of old.edges)check(now.edges.some(x=>JSON.stringify(x)===JSON.stringify(e)),'prior relation changed '+e.from+' '+e.to);
const bag=g.bagImplementationEvidence;check(bag.sameRevision===false&&bag.evidenceAppliesToCurrentRevision===false,'old evidence promoted');check(bag.connectedSourceCorrespondence.sameRevision===false&&bag.connectedSourceCorrespondence.evidenceAppliesToCurrentRevision===false,'connected old evidence promoted');
const projection=read(path.join(dir,'successor-projection.json'));check(git('rev-parse',projection.priorEvidenceCommit+':'+projection.unchangedSource.file)===git('rev-parse',commit+':'+projection.unchangedSource.file),'old menu blob differs');
const deterministic=read(path.join(dir,'final-code-generation.json'));for(const [f,h]of Object.entries(deterministic.files))check(hash(path.join(B,f))===h,'determinism artifact drift '+f);
const queries=[['intent','P.expedition'],['intent','P.expedition-light-followup'],['source','P.expedition-light-followup'],['show','P.expedition-return'],['show','R.expedition-game-evidence'],['show','R.expedition-final-review'],['show','R.expedition-offline'],['show','pol.expedition-visibility-scope'],['check']];
fs.mkdirSync(path.join(dir,'reviewer-queries'),{recursive:true});const queryResults=[];
for(const args of queries){const r=spawnSync(process.execPath,[path.join(work,'지식그래프/tools/product.mjs'),...args],{encoding:'utf8',maxBuffer:8*1024*1024});check(r.status===0,'query '+args.join(' '));fs.writeFileSync(path.join(dir,'reviewer-queries',args.join('-')+'.txt'),r.stdout+r.stderr);queryResults.push({args,exit:r.status,stderr:r.stderr});}
const result={commit,ok:!failures.length,failures,pins:pinResults,anchors,priorNodes:old.nodes.length,priorRelations:old.edges.length,deltaNodes:delta.nodes.length,queries:queryResults,codeDeterminism:'author repeat outputs match current bytes; no full extraction rerun'};fs.writeFileSync(path.join(dir,'reviewer-verification.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify({...result,pins:pinResults,anchors:anchors.length},null,2));
