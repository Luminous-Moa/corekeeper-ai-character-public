import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),file=path.join(stage,'work/지식그래프/기획/기능.md');let text=fs.readFileSync(file,'utf8');
const additions={
 'F.plan-queue':['AIGoalText.InventoryText'],
 'F.bag-hold':['AIHandLease.AIHandLease'],
 'F.bag-equip':['InventorySkill.InventorySkill','InventoryStamp.InventoryStamp','InventoryStamp.Exact','InventoryStamp.SameObject','InventorySwapProgress.InventorySwapProgress','InventorySwapProgress.MayRequest','InventorySwapProgress.Observe','InventorySwapProgress.RequestedOnce']
};
for(const [id,anchors] of Object.entries(additions))text=text.replace(new RegExp('(### '+id.replaceAll('.','\\.')+' [\\s\\S]*?)(?=\\n### |\\n## |$)'),block=>block.replace(/^- 코드 앵커: (.*)$/m,(_,existing)=>'- 코드 앵커: '+[...new Set([...existing.split(',').map(x=>x.trim()),...anchors])].join(', ')));
text+='\n<!-- 77484ec 가방 원본 재생성: 생성자·인스턴스 규칙 호출은 기존 추출기의 직접 정적 호출 선에서 빠질 수 있으므로 관련 능력의 명시 앵커로 등록했다. 동작 실행을 뜻하지 않는다. -->\n';
fs.writeFileSync(file,text);
