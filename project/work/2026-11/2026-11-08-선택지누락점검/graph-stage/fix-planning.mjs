// apply.mjs 1차 적용 뒤 기획 compile 규칙(재기획은 재기획 문서만, 참고로 가는 관계명은 '참고'로 시작)에 맞춘 보정.
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(stage,'../../..');
const planning=path.join(work,'2026-11/2026-11-07-기획연결');
const delta=JSON.parse(fs.readFileSync(path.join(stage,'final-delta.json'),'utf8'));
const node=id=>delta.nodes.find(n=>n.id===id);
for(const id of ['P.gap-bag','P.gap-life','P.gap-maintenance'])node(id).evidence=node(id).evidence.filter(e=>delta.documents[e.doc].layer==='재기획');
for(const id of ['Q.choice-total-limit','Q.maintenance-unreachable'])node(id).layer='참고';
node('Q.salvage').evidence=[{doc:'gapAgreement',anchor:'분해(수리대 salvage)는 이번 범위가 아니다.'}];
for(const e of delta.edges){
  const to=node(e.to);const toLayer=to?to.layer:(e.to.startsWith('R.')?'참고':'재기획');
  if(toLayer==='참고'&&!e.type.startsWith('참고'))e.type='참고: '+e.type;
  if(e.to==='Q.salvage')e.evidence=[{doc:'gapAgreement',anchor:'분해(수리대 salvage)는 이번 범위가 아니다.'}];
}
const addition='\n// 빈 선택지 3범위·상자 공용·상자 열림 모드·전투 진입 기준(공식 49bd748).\nObject.assign(documents,'+JSON.stringify(delta.documents,null,2)+');\nnodes.push(...'+JSON.stringify(delta.nodes,null,2)+');\nedges.push(...'+JSON.stringify(delta.edges,null,2)+');\n';
fs.copyFileSync(path.join(stage,'before-final-graph/planning/source.mjs'),path.join(planning,'source.mjs'));
fs.appendFileSync(path.join(planning,'source.mjs'),addition);
fs.writeFileSync(path.join(stage,'final-addition.mjs.txt'),addition);
fs.writeFileSync(path.join(stage,'final-delta.json'),JSON.stringify(delta,null,2)+'\n');
console.log('planning delta re-appended');
