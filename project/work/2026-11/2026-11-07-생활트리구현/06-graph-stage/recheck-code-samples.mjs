import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),month=path.resolve(stage,'../..'),B=path.join(month,'2026-11-05-지식그래프-개편/B'),old=path.join(stage,'official-before/B');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const current=read(path.join(B,'code.graph.json')),prior=read(path.join(old,'code.graph.json')),review=read(path.join(B,'validation-review.json'));
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const body=(root,node)=>{const text=fs.readFileSync(path.join(root,'snapshot',node.file),'utf8');return text.slice(node.start,node.end);};
const rows=[];
for(const s of review.samples)for(const h of s.handlers){const before=prior.nodes.find(n=>n.id===h.to),after=current.nodes.find(n=>n.id===h.to);if(!before||!after)throw Error('표본 심볼 없음 '+h.to);const a=body(old,before),b=body(B,after);rows.push({feature:s.id,id:h.to,file:after.file,lineStart:after.lineStart,lineEnd:after.lineEnd,oldLine:h.line,lineSame:h.line===after.lineStart,sameBody:a===b,currentBodySha256:sha(b)});}
const result={sourceCommit:current.meta.sourceCommit,samples:review.samples.length,handlers:rows.length,allBodiesSame:rows.every(r=>r.sameBody),allLinesSame:rows.every(r=>r.lineSame),rows,scope:'기존 코드 표본8개의 신원·현재 source범위·이전본문 동일성을 대조; 새 runtime 관측 아님'};
fs.writeFileSync(path.resolve(stage,'../graph-code-sample-recheck.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({...result,rows:rows.length},null,2));if(!result.allBodiesSame)process.exitCode=1;
