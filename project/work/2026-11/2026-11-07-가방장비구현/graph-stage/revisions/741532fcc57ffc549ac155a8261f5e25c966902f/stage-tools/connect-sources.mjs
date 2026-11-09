import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),G='work/지식그래프',L='work/2026-11/2026-11-05-지식그래프-실화면',P='work/2026-11/2026-11-07-기획연결';
const read=p=>fs.readFileSync(path.join(stage,p),'utf8'),write=(p,s)=>fs.writeFileSync(path.join(stage,p),s);
const capabilities=JSON.parse(read('capabilities-source.json'));
write(P+'/source.mjs',read(P+'/source.mjs')+`\nfor(const [mode,title,summary,anchors,symbol,anchor] of ${JSON.stringify(capabilities)}){\n nodes.push(n('R.bag-'+mode,title,'참고·구현/오프라인, 실게임 미확인',summary,'bagSpec',anchor));\n edges.push(e('P.bag-equipment','R.bag-'+mode,'참고: 승인 범위의 구현能力','bagSpec',anchor),e('R.bag-'+mode,'R.bag-validation','참고: 검증 범위·미관측','bagStatus','# 가방·장비 구현 검증 범위'),e('R.bag-'+mode,'R.bag-rules','참고: 정상 게임 소비 규칙·경계','bagRules','## 요청 API와 실제 소비'));\n}\n`);
const b=L+'/D/bag-source.mjs';let s=read(b);
s=s.replace("g.nodes.push({id,type:'기획 참고',layer:'참고',title,status:'참고·구현/오프라인, 실게임 미확인',summary,developmentCapability:'F.bag-'+mode,sourceLocations:[loc],evidence:[loc]});","const capability=g.nodes.find(n=>n.id===id);if(!capability)throw Error('bag planning source missing: '+id);capability.developmentCapability='F.bag-'+mode;");
// The planning compiler supplies intent-path relations; this overlay only attaches machine-code identities.
s=s.replace("  add('P.bag-equipment',id,'참고: 승인 범위의 구현 능력',planEvidence);\n",'').replace("  add(id,'R.bag-validation','검증 범위·미관측',[loc]);\n",'').replace("  add(id,'R.bag-rules','정상 게임 소비 규칙·경계',[{path:root+'transfer-rules.md',start:11}]);\n",'');
write(b,s);
let f=read(G+'/기획/기능.md');
for(const [id,anchor,note] of [
 ['F.plan-queue','AIInventory.CheckGoal, AIInventoryGoalForm.Check','가방 equip/unequip/hold/store/take/drop/give 계획은 keep/slot/where 계약을 전달한다. 공통 finish_goal의 단계 종료와 개별 물품 관측을 구별한다.'],
 ['F.action-select','AIInventory.Candidates, AIInventory.Stale','가방·장비 후보는 구체 슬롯/개체 스냅샷을 담고, 적용 시 재검사한다. 이번 추가 경로는 실게임 미확인이다.']
])f=f.replace(new RegExp('(### '+id.replace('.','\\.')+' [\\s\\S]*?)(?=\\n### |$)'),(_,block)=>block+'\n- 코드 앵커: '+anchor+'\n- 메모: 2026-11-07 가방·장비 격리 후보: '+note+'\n');
write(G+'/기획/기능.md',f);
console.log('Connected intent traversal and existing plan/field-choice capabilities.');
