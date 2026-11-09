import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),p=path.join(stage,'work/지식그래프/기획/기능.md');let text=fs.readFileSync(p,'utf8');
for(const id of ['F.plan-queue','F.action-select',...['equip','hold','store','take','drop','give'].map(mode=>'F.bag-'+mode)]){
 const escaped=id.replaceAll('.','\\.');
 text=text.replace(new RegExp('(### '+escaped+' [\\s\\S]*?)(?=\\n### |\\n## |$)'),block=>{
  const lines=block.split('\n'),extraAnchors=lines.filter(line=>line.startsWith('- 코드 앵커: ')).map(line=>line.slice(9));
  const first=lines.findIndex(line=>line.startsWith('- 코드 앵커: '));
  if(id==='F.bag-equip')extraAnchors.push('AIInventory.Reset, AIInventory.GoalSet, AIInventory.GoalDone, AIInventory.StallNote, AIInventory.PromptLine, AIInventory.ReportJson, InventorySkill.Cleanup, InventorySkill.Progress');
  const anchors=[...new Set(extraAnchors.join(',').split(',').map(s=>s.trim()).filter(Boolean))].join(', ');
  const out=lines.filter((line,index)=>!line.startsWith('- 코드 앵커: ')||index===first);
  const at=out.findIndex(line=>line.startsWith('- 코드 앵커: '));if(at>=0)out[at]='- 코드 앵커: '+anchors;
  if(id.startsWith('F.bag-'))return out.filter((line,index)=>index===0||line.trim()).join('\n')+'\n';
  // Added role-boundary notes/anchors must remain fields before the first blank line.
  return out.join('\n');
 });
}
fs.writeFileSync(p,text);console.log('Normalized parser fields and attached framework/delegate inventory callbacks.');
