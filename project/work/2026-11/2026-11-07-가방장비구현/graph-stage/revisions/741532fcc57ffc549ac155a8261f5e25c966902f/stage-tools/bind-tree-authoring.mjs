import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),G='work/지식그래프',P='work/2026-11/2026-11-07-기획연결',L='work/2026-11/2026-11-05-지식그래프-실화면',T='work/2026-11/2026-11-07-가방장비구현';
const read=p=>fs.readFileSync(path.join(stage,p),'utf8'),write=(p,s)=>fs.writeFileSync(path.join(stage,p),s);
function feature(id,fn){const file=G+'/기획/기능.md';write(file,read(file).replace(new RegExp('(### '+id.replaceAll('.','\\.')+' [\\s\\S]*?)(?=\\n### |\\n## |$)'),fn));}
const menuAnchors=['AIInventoryMenu.Candidates','AIInventoryMenu.Category','AIInventoryMenu.Items','AIInventoryMenu.ApplyNavigation','AIInventoryMenu.Stale','AIInventoryMenu.Reset','AIInventoryMenu.NoteLeafSelected','AIInventoryMenu.FinalMove','AIInventoryMenu.AddOperations','AIInventoryMenu.AddQuantities','AIInventoryMenu.AddDigits','AIInventoryMenuFrame.Copy','AIInventoryMenuChoice.AIInventoryMenuChoice','AIBrain.InventoryChoiceSurface','AIBrain.InventoryMenuControl','AIBrain.Apply','AIBrain.CancelPendingChoices'];
feature('F.bag-navigation',block=>block.replace('- 상태: 설계만 있음','- 상태: 구현됨·미확인').replace('- 관측 지점: 새 코드 SHA·메뉴 전이·실제 모델 전달 문구와 leaf 실행 근거 대기. flat 774/1fc 검증을 새 트리 통과로 승계하지 않는다.','- 관측 지점: 현재 메뉴 revision/노드·정확한 물품 snapshot·선택 경로·현재 자식 payload와 leaf 결과. 생산자 소스 링크 오프라인 검사만 연결했으며 연결 trace·독립검수·Unity·실게임·최종 수용은 별도다.').replace('- 메모: 현재는 기획·승인 연결 준비다. 최종 코드의 실제 앵커를 확인한 뒤 등록하고 재생성한다. 기존 여섯 F.bag 조작 primitive와 새 메뉴 상태를 구별한다.','- 코드 앵커: '+menuAnchors.join(', ')+'\n- 메모: 현재 고정 체크포인트의 정적 구현·생산자 오프라인 검사다. flat 774/1fc 부품 검사와 774 Unity를 새 트리 통과로 승계하지 않는다. 메뉴 탐색과 몸·목표·원래 부탁 제어를 구별한다.'));
for(const mode of ['equip','hold','store','take','drop','give'])feature('F.bag-'+mode,block=>block.replace(/^- 코드 앵커: (.*)$/m,(_,line)=>'- 코드 앵커: '+[...new Set([...line.split(',').map(x=>x.trim()).filter(x=>!['AIInventory.Candidates','AIInventory.ChestCandidates','AIInventory.DropCandidates'].includes(x)),'AIInventory.MenuMoves','AIInventoryMenu.FinalMove',...(mode==='store'?['AIBase.MayStoreKnownChest','AIInventory.HasStoreRoom']:[])])].join(', '))+'- 메모: tree-spec의 현재 메뉴 최종 leaf에서 정상 조작 primitive를 재사용한다. 초기 inventory-spec은 역사적 설계이며 현재 트리·수량·수령 경계는 tree-spec과 이번 체크포인트 근거를 함께 읽는다.\n');
feature('F.action-select',block=>block.replace(/^- 정책: (.*)$/m,(_,line)=>'- 정책: '+[...new Set([...line.split(',').map(x=>x.trim()),'pol.choice-no-cut'])].join(', ')).replace(/^- 코드 앵커: (.*)$/m,(_,line)=>'- 코드 앵커: '+[...new Set([...line.split(',').map(x=>x.trim()).filter(x=>x!=='AIInventory.Candidates'),'AIInventoryMenu.Candidates','AIInventoryMenu.Stale','AIBrain.InventoryChoiceSurface'])].join(', ')));
let s=read(P+'/source.mjs');s=s.replace("summary,'bagSpec',anchor)","summary,'bagSpec',anchor,[ev('bagTreeSpec','## Leaf와 목표 진척')])");
s=s.replace("'참고·새 코드 SHA/검증 대기','flat 774/1fc 부품 검증을 새 트리 통과로 승계하지 않는다. 뒤로/전체 선택 취소/오래된 메뉴 응답/8·14 초과 생성·전달 및 leaf 정상 실행을 새 고정판에서 검증한다.'","'참고·정적구현/생산자오프라인, 미수용·미실게임','현재 체크포인트의 실제 메뉴 심볼과 생산자 소스 링크 검사만 연결한다. 연결 trace·독립검수·Unity·실게임·최종 수용은 별도다. flat 774/1fc 부품 검증을 새 트리 통과로 승계하지 않는다.'");
if(!s.includes('// Bounded tree checkpoint sources'))s+=`
// Bounded tree checkpoint sources. References are not user decisions.
Object.assign(documents,{bagTreeSpec:{layer:'참고',path:'../2026-11-07-가방장비구현/tree-spec.md'},bagCapAudit:{layer:'참고',path:'../2026-11-07-가방장비구현/inventory-category-cap-audit.md'},bagCapResult:{layer:'참고',path:'../2026-11-07-가방장비구현/tree-cap-result.md'},bagMenuSamples:{layer:'참고',path:'../2026-11-07-가방장비구현/menu-examples.md'}});
nodes.push(n('R.bag-cap-audit','현장 후보 상한과 다른 소비자 제한의 구별','참고·정적 조사/HEAD 적용 판정','현장 Decisions의 개수 cut과 closeFight ONE quota는 제거 범위다. 상위 둘레 설계 CoreEdge.Options4·관측/solver/물리·보고/기억/로그·call/cost 제한은 다른 의미로 보존한다. 이는 전체 world 트리 재설계나 실행 성공 근거가 아니다.','bagCapAudit','## 8. HEAD 적용 판정',[ev('bagCapResult','## 변경한 경로'),ev('bagCapAudit','## 5. cap 지도 B')]),n('R.bag-menu-examples','현재 메뉴 노드 payload 표본','참고·단독 메뉴 어댑터, 미외부모델·미실게임','실제 menu/inventory 소스에 대역 World/수동 결과를 공급한 현재 노드 자식 예시다. 연결된 실제 Loop 전송·실제 외부 모델·게임 소비자 성공과 구별한다.','bagMenuSamples','# 현재 메뉴 노드 표본'));
edges.push(e('P.bag-no-cut','R.bag-cap-audit','참고: 소비 경로별 상한 적용 판정','bagCapAudit','## 8. HEAD 적용 판정'),e('P.bag-item-first','R.bag-menu-examples','참고: 현재 보유 노드 표본','bagMenuSamples','# 현재 메뉴 노드 표본'),e('R.bag-navigation','R.bag-menu-examples','참고: 현재 노드 자식·선택 경로 예시','bagMenuSamples','# 현재 메뉴 노드 표본'),e('R.bag-navigation','R.bag-cap-audit','참고: 상한 제거와 유지 경계','bagCapAudit','## 8. HEAD 적용 판정'));
`;
write(P+'/source.mjs',s);
let b=read(L+'/D/bag-source.mjs');b=b.replace("['pol.bag-agency','pol.bag-boundary']","['pol.bag-agency','pol.bag-boundary','pol.bag-navigation','pol.choice-no-cut']");
b=b.replace(" const code=g.nodes.filter(n=>n.type==='코드'&&n.kind==='MethodDeclaration'&&n.signature?.startsWith(symbol+'('));"," const currentSymbol={equip:'AIInventory.MenuMoves',hold:'AIInventory.TryChosenHand',store:'InventorySkill.TransferTick',take:'InventorySkill.TransferTick',drop:'InventorySkill.DropTick',give:'InventorySkill.DropTick'}[mode];\n  const code=g.nodes.filter(n=>n.type==='코드'&&n.kind==='MethodDeclaration'&&n.signature?.startsWith(currentSymbol+'('));");
b=b.replace("const id='R.bag-'+mode,loc={path:root+'inventory-spec.md',start:1};","const id='R.bag-'+mode,loc={path:root+'tree-spec.md',start:29};");
if(!b.includes('// Actual tree navigation identities'))b=b.replace(' return [evidencePath,',`
 // Actual tree navigation identities; source correspondence only, not runtime trace.
 const menuNode=g.nodes.find(n=>n.id==='R.bag-navigation');menuNode.developmentCapability='F.bag-navigation';
 for(const symbol of ['AIInventoryMenu.Candidates','AIInventoryMenu.Category','AIInventoryMenu.Items','AIInventoryMenu.ApplyNavigation','AIInventoryMenu.Stale','AIInventoryMenu.NoteLeafSelected','AIInventoryMenu.FinalMove','AIBrain.InventoryChoiceSurface','AIBrain.InventoryMenuControl','AIBrain.Apply','AIBrain.BuildJson']){
  const targets=g.nodes.filter(n=>n.type==='코드'&&n.kind==='MethodDeclaration'&&n.signature?.startsWith(symbol+'('));
  if(targets.length!==1)throw Error('Tree code anchor missing/ambiguous: '+symbol);
  add('R.bag-navigation',targets[0].id,'트리 상태·전달·leaf 구현 위치(정적)',menuNode.evidence);
 }
 for(const symbol of ['AICandidateAssembler.Finish','AICandidateAssembler.LimitFor','AIPickupOptions.Select']){
  const targets=g.nodes.filter(n=>n.type==='코드'&&n.kind==='MethodDeclaration'&&n.signature?.startsWith(symbol+'('));
  if(targets.length!==1)throw Error('No-cut code anchor missing/ambiguous: '+symbol);
  add('R.bag-cap-audit',targets[0].id,'후보 개수 상한 제거 구현 위치(정적)',g.nodes.find(n=>n.id==='R.bag-cap-audit').evidence);
 }
 for(const mode of ['equip','hold','store','take','drop','give'])add('R.bag-'+mode,menuNode.id,'현재 메뉴 leaf의 선택·실행 경계',menuNode.evidence);
 return [evidencePath,`);
write(L+'/D/bag-source.mjs',b);
const samples=JSON.parse(read(T+'/evidence/tree-menu-adapter/payloads.json'));
const text='# 현재 메뉴 노드 표본\n\n고정 체크포인트 '+JSON.parse(read('final-pin.json')).sourceCommit+'의 단독 메뉴 어댑터 표본이다. 실제 menu/inventory 소스와 대역 World/큐·수동 결과를 사용했다. 실제 외부 모델 호출, 연결 Loop 전체 trace, 게임 소비자 실행 또는 최종 수용을 뜻하지 않는다. 원본 전체 payload와 로그는 implementation-evidence.json에 SHA-256로 고정되어 있다.\n\n| 표본 | 현재 자식 수 | 인벤토리 요청 수 |\n|---|---:|---:|\n'+samples.map(x=>'| '+x.sample+' | '+x.choices.length+' | '+x.inventoryRequests+' |').join('\n')+'\n\n## 원문 표본\n\n'+samples.filter((x,i)=>i<3||/leaf|cancel|back|quant|digits/.test(x.sample)).slice(0,10).map(x=>'### '+x.sample+'\n\n```json\n'+JSON.stringify(x,null,2)+'\n```').join('\n\n')+'\n';
write(T+'/menu-examples.md',text);
write(G+'/기획/미결.md',read(G+'/기획/미결.md').replace('최종 고정판에서 장착·손선택·수량 이동·전달을 실제 게임으로 확인하지 않았다.','현재 트리 고정판에서 분류·물품·인자·뒤로/선택 전체 취소·leaf 실행과 장착·손선택·수량 이동·전달을 실제 게임으로 확인하지 않았다.'));
console.log('Bound actual tree/menu/leaf and no-cut authoring; saved '+samples.length+' current-node samples.');
