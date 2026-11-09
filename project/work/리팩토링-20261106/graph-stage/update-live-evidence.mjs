import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
const S=path.dirname(fileURLToPath(import.meta.url)),W='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/리팩토링-20261106',L=S+'/work/2026-11/2026-11-05-지식그래프-실화면',dir=L+'/C/refactor-20261106';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'')),write=(p,v)=>fs.writeFileSync(p,JSON.stringify(v,null,2)+'\n'),hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const doc=read(dir+'/observations.json');
for(const [source,name]of [[W+'/18-review-live-observation.md','18-review-live-observation.md'],['C:/Dev/_scratch/refactor-maint-live-20261106/persistence-comparison.json','persistence-comparison.json']]){const bytes=fs.readFileSync(source);fs.writeFileSync(dir+'/'+name,bytes);const input={originalPath:source,stagedPath:'C/refactor-20261106/'+name,sha256:hash(bytes),bytes:bytes.length};doc.inputs=doc.inputs.filter(x=>x.stagedPath!==input.stagedPath);doc.inputs.push(input);}
const comparison=read(dir+'/persistence-comparison.json');if(comparison.length!==30||comparison.some(x=>x.same!==true))throw Error('Expected 30 matching recorded fields');
const save=doc.observations.find(x=>x.id==='RF902-save-reentry');
save.status='reentry-observed-final-environment-restoration-pending';
save.evidence.comparison='C/refactor-20261106/persistence-comparison.json';
save.facts=['Normal save and complete game process exit completed per HEAD/environment observation.','Same slot7/world reentered; both characters autojoined with existing GUIDs.','Recorded comparison: 15 fields per member, 30/30 same, including characterGuid, inventory, inventoryObjectNames, inventoryAuxData, skills, seen, visited, oreXY, oreId, stationId, stationXY, stationPlaced, edgeMyDoors, edgeDoors and drops.','After reentry the existing door opened, was crossed and closed; goto completed per HEAD/environment observation.'];
save.boundary='The comparison JSON is attached. Final report has not yet incorporated exit/reentry timestamps and logs. Trial-world deletion and original environment restoration are in progress; no restoration pass is asserted. The compared subset is not every persistent field or every save/reentry scenario.';
const down=doc.observations.find(x=>x.id==='RF902-down-unclassified');
down.status='observed-cause-unresolved-no-new-refactor-blocker-identified';down.evidence.review='C/refactor-20261106/18-review-live-observation.md';
down.reviewConclusion='HEAD accepted limited a07166f..902721e source/log review: relevant mining/tunnel/damage instrumentation behavior preserved; no evidence identifying a new refactor regression blocker. Actual damage source remains unresolved.';
down.boundary='Explosion wall remains a cause candidate; not proven cause, normal outcome or safety-policy success. No same-scene baseline run or complete damage-source trace was supplied. The review did not change code or require a new game run.';
doc.limitations=doc.limitations.filter(x=>x!=='Final live report and restoration proof still pending.');doc.limitations.push('Final live report, final all-stop raw-log comparison and environment restoration proof still pending. Reentry comparison evidence and limited DOWN review are now attached.');
write(dir+'/observations.json',doc);
const overlay=read(L+'/C/overlay-source.json');overlay.evidenceReferences=[...new Set([...overlay.evidenceReferences,...doc.inputs.map(x=>x.stagedPath)])];write(L+'/C/overlay-source.json',overlay);
console.log('Prepared 30/30 reentry field comparison and limited DOWN review; no graph regeneration performed.');
