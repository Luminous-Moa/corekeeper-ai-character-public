import fs from 'node:fs';import path from 'node:path';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';import crypto from 'node:crypto';
const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../../..'),B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B'),live=path.join(work,'2026-11/2026-11-05-지식그래프-실화면');
const target=process.argv[2];if(!/^[0-9a-f]{40}$/.test(target??''))throw Error('final SHA required');
const g=JSON.parse(fs.readFileSync(path.join(B,'code.graph.json'),'utf8')),old=g.meta.sourceCommit;
const historyFile=path.join(stage,'extraction-history.json'),history=fs.existsSync(historyFile)?JSON.parse(fs.readFileSync(historyFile,'utf8')):[];
history.push({sourceCommit:old,reason:'독립검수에 따른 후속 제품 소스 수정으로 후보가 바뀌어 중간 추출을 최종 증거로 사용하지 않는다.',codeGraphSha256:crypto.createHash('sha256').update(fs.readFileSync(path.join(B,'code.graph.json'))).digest('hex'),nodes:g.nodes.length,edges:g.edges.length});
fs.writeFileSync(historyFile,JSON.stringify(history,null,2)+'\n');
for(const name of ['pin-snapshot.mjs','build.mjs','verify.mjs']){const f=path.join(B,'extractor',name),text=fs.readFileSync(f,'utf8');if(!text.includes(old))throw Error('extractor old SHA mismatch '+name);fs.writeFileSync(f,text.replaceAll(old,target));}
const overlayFile=path.join(live,'C/overlay-source.json'),overlay=JSON.parse(fs.readFileSync(overlayFile,'utf8'));overlay.baseCommit=target;fs.writeFileSync(overlayFile,JSON.stringify(overlay,null,2)+'\n');
const appliedFile=path.join(stage,'applied-canonical.json'),applied=JSON.parse(fs.readFileSync(appliedFile,'utf8'));applied.intermediateSourceCommit=old;applied.sourceCommit=target;applied.codeExtractionCountPlanned=history.length+1;fs.writeFileSync(appliedFile,JSON.stringify(applied,null,2)+'\n');console.log('final candidate pin '+target);
