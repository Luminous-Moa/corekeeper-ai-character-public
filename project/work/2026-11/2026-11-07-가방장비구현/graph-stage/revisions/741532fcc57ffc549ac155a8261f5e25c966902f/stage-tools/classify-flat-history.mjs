import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),file=path.join(stage,'work/지식그래프/기획/기능.md');let text=fs.readFileSync(file,'utf8');
if(!text.includes('| `AIInventory.Candidates` | 죽은 코드 |'))text=text.replace('| 함수 | 분류 | 이유 |\n|---|---|---|','| 함수 | 분류 | 이유 |\n|---|---|---|\n'+['Candidates','ChestCandidates','DropCandidates','IsChosen'].map(name=>'| `AIInventory.'+name+'` | 죽은 코드 | b1facaa 정적 원본에서 기존 flat 후보군 내부에만 남음. 현재 공개 선택은 AIInventoryMenu→MenuMoves이며 새 메뉴 능력의 실행 앵커로 승격하지 않는다 |').join('\n'));
fs.writeFileSync(file,text);console.log('Classified four preserved flat helpers separately from current menu routes.');
