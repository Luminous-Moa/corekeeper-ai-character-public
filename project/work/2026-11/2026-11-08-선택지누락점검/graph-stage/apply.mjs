// 빈 선택지 3범위·상자 공용·상자 열림 모드(공식 49bd748)의 정본 그래프 반영.
// 관측·재판단 반영(2026-11-08-observation-redecision/graph-stage/apply-final.mjs)과 같은 순서를 따른다.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const stage=path.dirname(fileURLToPath(import.meta.url)),result=path.dirname(stage),work=path.resolve(stage,'../../..');
const target='49bd748a8fcd390fc658166ca662ddb13b5c5c0d',previous='20b1aced7594a47eacb4be060405bcb53d38f48f';
const repo='C:/Dev/CoreKeeperModSDK',legacy=path.join(work,'지식그래프');
const planning=path.join(work,'2026-11/2026-11-07-기획연결');
const B=path.join(work,'2026-11/2026-11-05-지식그래프-개편/B'),C=path.join(work,'2026-11/2026-11-05-지식그래프-개편/C');
const live=path.join(work,'2026-11/2026-11-05-지식그래프-실화면');
const behaviorTopics=path.join(work,'2026-11/2026-11-08-행동판단프로젝트연계/topics.mjs');
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
const json=value=>JSON.stringify(value,null,2)+'\n';
const read=file=>fs.readFileSync(file,'utf8');
const git=(...args)=>execFileSync('git',['-C',repo,...args],{encoding:'utf8'}).trim();
if(git('rev-parse','HEAD')!==target)throw Error('Official HEAD changed');
if(fs.existsSync(path.join(stage,'applied-canonical.json')))throw Error('Already applied');
if(git('diff','--name-only','--diff-filter=DR',previous,target,'--','Assets/AIPlayerSpike','Assets/HeadlessTools','Tools/ai-bridge','Tools/knowledge'))throw Error('Deleted source needs snapshot cleanup');
const stagedB=path.join(stage,'B-final');
const code=JSON.parse(read(path.join(stagedB,'code.graph.json')));
if(code.meta.sourceCommit!==target)throw Error('Staged code version mismatch');
const backup=path.join(stage,'before-final-graph');
if(!fs.existsSync(path.join(backup,'legacy','README.md')))throw Error('Backup missing');
fs.copyFileSync(behaviorTopics,path.join(backup,'behavior-topics.mjs'));

// 1. 근거 문서 고정 사본.
fs.mkdirSync(path.join(stage,'source-pins'),{recursive:true});
const documents={},pins=[];
for(const[id,name,layer]of [
  ['gapAgreement','agreement.md','재기획'],
  ['gapBagAudit','bag-equipment.md','참고'],
  ['gapLifeAudit','life.md','참고'],
  ['gapMaintAudit','maintenance.md','참고'],
  ['gapIntegration','integration.md','참고'],
  ['gapFixes','fixes.md','참고'],
  ['gapReview','review.md','참고'],
  ['gapReview5','review-5.md','참고'],
  ['gapReviewMerge','review-merge.md','참고']
]){
  const original=path.join(result,name),pinned=path.join(stage,'source-pins',name),bytes=fs.readFileSync(original);
  fs.writeFileSync(pinned,bytes);
  documents[id]={layer,path:path.relative(planning,pinned).replaceAll('\\','/'),originalPath:path.relative(planning,original).replaceAll('\\','/')};
  pins.push({id,original,pinned,sha256:hash(bytes)});
}
const ev=(doc,anchor)=>({doc,anchor});
const nodes=[
  {id:'P.choice-gaps',title:'가방·생활·정비 범위의 빈 선택지를 판단 모델 후보로 채움',layer:'재기획',status:'합의·2026-11-08 사용자 승인',summary:'5개 선택지 범위 중 전투(미구현)·채집이동귀환(별도 진행)을 뺀 가방·장비, 생활, 장비 정비에서 범위 안인데 빠진 선택지를 찾아 구현한다. 고르는 것은 판단 모델이며 스크립트가 대신 고르거나 후보를 지우지 않는다. 후보 상한·페이지를 다시 만들지 않고 기존 트리에 붙인다.',evidence:[ev('gapAgreement','빈 선택지는 구현까지 진행한다'),ev('gapAgreement','공통 조건: 고르는 것은 판단 모델이다.')]},
  {id:'P.gap-bag',title:'가방·장비의 빈 선택지(파우치·일괄 정리·프리셋·펫·꾸미기·사용·착용 장비 이동·자리 옮기기)',layer:'재기획',status:'합의·구현',summary:'파우치 안 물품 보기·옮기기, 근처 상자 빠른 쌓기·정렬, 착용 프리셋 전환과 비활성 프리셋 보기, 펫 칸·꾸미기 칸, 회복·허기와 무관한 물품 사용(버프·마나 물약·선물 상자·탐지기·악기), 착용 장비를 바로 보관·내려놓기·전달, 가방 안 자리 옮기기·나누기·합치기, 분류 정보 없는 물품의 기본 조작. 게임 단축 조작은 들기로 대신해 제외했다.',evidence:[ev('gapAgreement','### 가방·장비'),ev('gapBagAudit','## 2. 빠진 선택지')]},
  {id:'P.gap-life',title:'생활의 빈 선택지(솥 입력 정리·빈 땅 물주기·무괭이 수확·씨앗 뿌리개·침대)',layer:'재기획',status:'합의·구현',summary:'솥 입력 비대칭 상태의 채우기·입력 재료 빼기, 작물 없는 판 땅 물주기, 괭이 없이 거둔 수확의 농사 목표 진척, 씨앗 뿌리개 사용, 화면 밖 기억 시설 레시피가 확인 없이 제작 목록에 섞이는 문제 수정, 침대에서 자기. 가축 기르기는 사용자가 고르지 않아 제외했고 자동 가공·제련·거래·낚시·내구 강화는 기존 후속이다.',evidence:[ev('gapAgreement','### 생활'),ev('gapLifeAudit','## 2. 빠진 선택지')]},
  {id:'P.gap-maintenance',title:'장비 정비의 빈 선택지(화면 밖 대장간 회수·pending 중 수리·파우치 장비)',layer:'재기획',status:'합의·구현',summary:'화면 밖 대장간에 남은 자기 장비를 회수하러 가는 후보, 업그레이드 pending 중에도 투입 없는 일반 수리 허용, 파우치 안 장비 정비. 분해(수리대 salvage)는 범위 밖이다.',evidence:[ev('gapAgreement','### 장비 정비'),ev('gapMaintAudit','## 2. 빠진 선택지')]},
  {id:'P.maintenance-abandon',title:'회수 불가 정비 장비: 판단 모델의 포기 → 상위 LLM에 상황 알림',layer:'재기획',status:'합의·사용자 정정 반영',summary:'대장간이 없어지거나 입력 칸이 빈 것을 직접 보면 판단 모델에 돌려받을 수 없다는 사실과 포기 선택지를 준다. 포기하면 pending을 지우고 상위 LLM에 어떤 장비를 어느 대장간에서 잃었는지와 가져간 것으로 보이는 사람(대장간 근처 플레이어 추정, 추정임 명시)을 알린다. 따질지 넘어갈지는 LLM이 정하며 따지라는 지시는 넣지 않는다.',evidence:[ev('gapAgreement','- M2 회수 불가 pending:'),ev('gapAgreement','따질지 넘어갈지는 LLM이 정한다.')]},
  {id:'P.chests-shared',title:'모든 상자를 공용으로 사용(호스트·다른 AI 상자 포함)',layer:'재기획',status:'합의·기존 자기 상자 한정을 대체',summary:'보관·꺼내기·빠른 쌓기·제작 재료 계산이 모든 상자를 공용으로 쓴다. 기존 가방 합의의 자기 상자 접근 범위 유지와 호스트·다른 AI 상자 제외를 대체한다. 기지에 상자를 더 만들지 판단할 때 세는 상자는 직접 놓은 상자로 유지한다. 상자가 많으면 구역 단계로 나눠 모든 상자에 닿게 한다.',evidence:[ev('gapAgreement','### 상자 공용'),ev('gapAgreement','기존 가방 합의의')]},
  {id:'P.chest-open-mode',title:'상자 열림 모드: 연 상자의 일과 닫기만 선택',layer:'재기획',status:'합의·사용자 결정',summary:'상자를 연 상태의 판단 요청은 그 상자 안 물건 꺼내기, 내 가방·파우치·착용 물건 넣기, 이 상자 빠른 쌓기·정렬, 닫기만 제공한다. 다른 목록은 붙이지 않고 닫기를 고르면 원래 선택면으로 돌아간다. 상자가 없어지거나 보이지 않거나 손닿는 거리를 벗어나면 모드를 풀고 전투 상태에 들어가면 상자를 닫는다.',evidence:[ev('gapAgreement','## 상자 열림 모드'),ev('gapAgreement','AI가 상자를 연 상태이면, 그 판단 요청의 선택지를 그 상자 안의 일로 제한한다.')]},
  {id:'P.combat-entry',title:'전투 상태 진입 기준(전투 구현 시작 때 사용자 재확인)',layer:'재기획',status:'합의(사용자 구상)·전투 구현 시작 때 사용자에게 제시·확인 전 확정 설계 아님',summary:'공격당하거나 적이 나를 인식하는 순간 전투 모드로 바꾸고 전투 선택지만 띄운다. 적이 나를 인식했어도 도주 가능하거나 적이 나를 공격할 수 없는 상태면 진입하지 않는다. 상자 열림 모드는 전투 상태 진입 때만 끝나며 지금은 기존 피격 신호에 임시 연결했다. 전투 구현을 시작할 때 이 합의를 먼저 사용자에게 제시하고 수정할 내용을 묻는다.',evidence:[ev('gapAgreement','## 전투 상태 진입 기준'),ev('gapAgreement','전투 구현을 시작할 때는 이 절의 합의 내용을 먼저 사용자에게 제시하고 수정할 내용이 있는지 묻는다.')]},
  {id:'Q.choice-total-limit',title:'한 판단 요청의 전체 후보 합계 255 초과 가능성',layer:'재기획',status:'미정·사용자 판단 대기',summary:'상자 쪽 후보는 생산자마다 40개 이하로 묶였지만 최악 합계 약 125개에 상한 없는 벽 캐기·줍기·탐험·둘레 목록이 더해지면 한 요청이 255개를 넘어 판단 요청이 나가지 않을 수 있다. 이번 작업 전부터 있던 조립 구조 문제이며 해결 방식은 정하지 않았다.',evidence:[ev('gapReview5','#### G3.')]},
  {id:'Q.maintenance-unreachable',title:'길을 찾을 수 없는 화면 밖 대장간 pending의 출구',layer:'재기획',status:'미정',summary:'포기는 대장간 없음·칸 빔을 직접 본 경우로 한정했으므로 길이 없는 화면 밖 대장간에 장비가 남으면 막힘 사실만 보고하고 새 업그레이드가 계속 막힌다.',evidence:[ev('gapReview','#### L8.')]},
  {id:'Q.salvage',title:'수리대 분해(salvage) 선택지 포함 여부',layer:'재기획',status:'미정·할 일',summary:'장비를 부숴 수리 재료를 얻는 게임 행동이다. 시설생활 계획에 별도로 정한다고만 남아 있었고 이번 범위에 넣지 않았다.',evidence:[ev('gapMaintAudit','### 알려진 후속(기록상 미룬 것)')]},
  {id:'R.gap-implementation',title:'빈 선택지 3범위·상자 공용·상자 열림 모드 구현(공식 49bd748)',layer:'참고',status:'참고·공식 코드·오프라인/Unity 수용·실게임 미확인',sourceCommit:target,summary:'가방(파우치·일괄 정리·프리셋·펫·꾸미기·사용·착용 장비 이동·자리 옮기기), 생활(솥 입력 정리·빈 땅 물주기·무괭이 수확·씨앗 뿌리개·기억 시설 누출 수정·침대·근처 상자 재료 제작), 정비(화면 밖 대장간 회수·pending 중 수리·파우치·포기와 상위 알림), 상자 구역 단계(AIChestAreas)와 상자 열림 모드(AIChestMode)를 구현했다. 씨앗 뿌리개 범위, 침대 수면 처리, 착용 칸 직접 내려놓기, 펫·꾸미기 칸 교환, 근처 상자 빠른 쌓기 범위, 악기 연주는 실게임 확인 전 추정이다.',evidence:[ev('gapIntegration','## 충돌 해결'),ev('gapFixes','### 공통 규칙'),ev('gapFixes','### 상자 열림 모드')]},
  {id:'R.gap-review',title:'빈 선택지 통합판 독립 검수 5회와 공식 병합 검수',layer:'참고',status:'참고·독립 코드/오프라인 검수·실게임 별도',sourceCommit:target,summary:'통합판 검수와 재검수에서 상자 공용으로 생긴 후보 255 초과 경로를 반복 발견해 상자 후보 생산자 18곳을 공통 규칙으로 정리했다. 공식 61aa67b 병합 검수에서 목표 없을 때 상자가 열린 채 멈추는 문제를 고쳤다. 차단 발견은 0건이다. 전체 판단 조립 경로의 총 후보 수는 오프라인에서 직접 재지 못했다.',evidence:[ev('gapReview5','## 결론 요약'),ev('gapReviewMerge','## 2. 발견'),ev('gapFixes','## 병합 검수')]},
  {id:'R.gap-integration',title:'공식 ai-character 49bd748 반영(공식 61aa67b 병합 포함)',layer:'참고',status:'참고·공식 코드 통합·설치/실게임 미실시',sourceCommit:target,summary:'gap-integrated-20261108을 공식 61aa67b(이동전달 개선·호출 계기·자유 시간 제거·지역 이동·토큰 절감)와 병합하고 검수 뒤 ai-character를 49bd748로 fast-forward했다. 자유 시간 제거에 따라 자유 시간 보관 후보를 뺐다. 게임 설치와 실게임·실제 모델 검증은 하지 않았다.',evidence:[ev('gapFixes','## 공식 병합'),ev('gapFixes','### 뺀 것')]}
];
const edge=(from,to,type,doc,anchor,cls='해석')=>({from,to,type,class:cls,evidence:[ev(doc,anchor)]});
const edges=[
  edge('P.field-choice','P.choice-gaps','범위별 빈 선택지 보완','gapAgreement','빈 선택지는 구현까지 진행한다','합의'),
  edge('P.unrestricted-choice','P.choice-gaps','후보 상한·페이지를 다시 만들지 않음','gapAgreement','후보 상한·페이지 분할을 다시 만들지 않는다(제약 해제 합의).','합의'),
  edge('P.choice-gaps','P.gap-bag','가방·장비 범위','gapAgreement','### 가방·장비','합의'),
  edge('P.choice-gaps','P.gap-life','생활 범위','gapAgreement','### 생활','합의'),
  edge('P.choice-gaps','P.gap-maintenance','장비 정비 범위','gapAgreement','### 장비 정비','합의'),
  edge('P.bag-equipment','P.gap-bag','기존 가방 승인 범위의 보완','gapAgreement','### 가방·장비'),
  edge('P.life-tree','P.gap-life','기존 생활 트리의 보완','gapAgreement','### 생활'),
  edge('P.maintenance','P.gap-maintenance','기존 정비 범위의 보완','gapAgreement','### 장비 정비'),
  edge('P.gap-maintenance','P.maintenance-abandon','회수 불가 pending의 출구','gapAgreement','- M2 회수 불가 pending:','합의'),
  edge('P.maintenance-pending','P.maintenance-abandon','자기 pending 수명의 후속 결정','gapAgreement','- M2 회수 불가 pending:'),
  edge('P.agency','P.maintenance-abandon','상위 LLM이 대응을 스스로 정함','gapAgreement','따질지 넘어갈지는 LLM이 정한다.'),
  edge('P.gap-bag','P.chests-shared','상자 접근 기준','gapAgreement','### 상자 공용','합의'),
  edge('P.gap-life','P.chests-shared','근처 상자 재료 제작','gapAgreement','- 제작: 게임처럼 제작대 근처 상자 재료로 바로 제작(life M4).','합의'),
  edge('P.chests-shared','P.bag-item-first','대체: 자기 상자 꺼내기 경로를 모든 상자로','gapAgreement','기존 가방 합의의','합의'),
  edge('P.chests-shared','R.bag-store','대체: 자기 상자 보관 한정을 모든 상자로','gapAgreement','기존 가방 합의의','합의'),
  edge('P.chests-shared','R.bag-take','대체: 자기 상자 꺼내기 한정을 모든 상자로','gapAgreement','기존 가방 합의의','합의'),
  edge('P.gap-bag','P.chest-open-mode','상자를 연 동안의 선택면','gapAgreement','## 상자 열림 모드','합의'),
  edge('P.chest-open-mode','P.combat-entry','전투 상태 진입 때만 상자 모드 종료','gapAgreement','상자 열림 모드와의 관계:','합의'),
  edge('P.field-choice','P.combat-entry','전투 선택지로 바꾸는 시점','gapAgreement','## 전투 상태 진입 기준'),
  edge('P.choice-gaps','Q.choice-total-limit','남은 조립 구조 문제','gapReview5','#### G3.'),
  edge('P.maintenance-abandon','Q.maintenance-unreachable','포기 범위 밖의 막힘','gapReview','#### L8.'),
  edge('P.gap-maintenance','Q.salvage','범위 밖으로 남긴 정비 행동','gapMaintAudit','### 알려진 후속(기록상 미룬 것)'),
  edge('P.choice-gaps','R.gap-implementation','참고: 구현','gapIntegration','## 충돌 해결'),
  edge('P.choice-gaps','R.gap-review','참고: 독립 검수','gapReview5','## 결론 요약'),
  edge('P.choice-gaps','R.gap-integration','참고: 공식 반영','gapFixes','## 공식 병합'),
  edge('R.gap-implementation','R.gap-review','참고: 검수 범위','gapFixes','## 병합 검수')
];
const current=read(path.join(planning,'source.mjs'));
for(const node of nodes)if(current.includes(`'${node.id}'`)||current.includes(`"${node.id}"`))throw Error('Planning id exists '+node.id);
const addition='\n// 빈 선택지 3범위·상자 공용·상자 열림 모드·전투 진입 기준(공식 49bd748).\nObject.assign(documents,'+JSON.stringify(documents,null,2)+');\nnodes.push(...'+JSON.stringify(nodes,null,2)+');\nedges.push(...'+JSON.stringify(edges,null,2)+');\n';
fs.writeFileSync(path.join(stage,'final-addition.mjs.txt'),addition);
fs.writeFileSync(path.join(stage,'final-delta.json'),json({sourceCommit:target,documents,nodes,edges}));
fs.writeFileSync(path.join(stage,'final-source-pins.json'),json(pins));

// 2. 코드 대응(제품 그래프 planning.mjs 확장)용 서명 해석.
const signatureGroups={
  'P.chests-shared':['AIChestAreas.Step<T>(','AIChestAreas.Within<T>(','AIInventory.KnownChestWhere(','AICraft.CraftChests(','AICraft.MaterialCount('],
  'P.chest-open-mode':['AIChestMode.Active(','AIChestMode.End(','AIInventoryMenu.ChestModeCandidates(','AIInventoryMenu.LeaveChestMode('],
  'P.combat-entry':['AIChestMode.FightStarted('],
  'P.maintenance-abandon':['AIMaintenance.ObserveLoss(','AIMaintenance.AbandonCandidate(','AIMaintenance.Abandon(','MaintenanceAbandonSkill.Tick(','AIMaintenance.LostWho(','AIMaintenance.PlayersNear('],
  'P.gap-bag':['AIInventory.Pouches(','AIInventoryBulk.Moves(','AIInventoryBulk.ActivePreset(','AIInventory.UseKind(','InventorySkill.UseTick(','AIInventory.SwapMove(','AIInventory.CrossSwap('],
  'P.gap-life':['AIBed.ExactCandidate(','SleepSkill.Tick(','AIFarm.SeederCandidate(','AIFarm.AttackHarvestCandidate(','AICook.ExactCandidate('],
  'P.gap-maintenance':['AIMaintenance.Recovery(','AIMaintenance.Rebind(','AIMaintenance.ObservePending(']
};
const mappings=[];
for(const[from,signatures]of Object.entries(signatureGroups))for(const signature of signatures){
  const found=code.nodes.filter(node=>node.signature?.startsWith(signature));
  if(found.length!==1)throw Error('Missing or ambiguous anchor '+signature+' '+found.length);
  mappings.push([from,found[0].signature]);
}

// 3. 정본 적용.
fs.appendFileSync(path.join(planning,'source.mjs'),addition);
for(const name of ['pin-snapshot.mjs','build.mjs','verify.mjs']){
  const file=path.join(B,'extractor',name),source=read(file);
  if(!source.includes(previous))throw Error('Unexpected B baseline '+name);
}
for(const name of ['pin-snapshot.mjs','build.mjs','verify.mjs'])fs.copyFileSync(path.join(stagedB,'extractor',name),path.join(B,'extractor',name));
for(const name of ['snapshot','runtime-package']){fs.rmSync(path.join(B,name),{recursive:true,force:true});fs.cpSync(path.join(stagedB,name),path.join(B,name),{recursive:true});}
for(const name of ['csharp.raw.json','javascript.raw.json','code.graph.json','B.graph.json','validation.json','boundaries.json','call-forms.json','snapshot.manifest.json','snapshot-verification.json','external-runtime.manifest.json','question-coverage.json','determinism.json'])fs.copyFileSync(path.join(stagedB,name),path.join(B,name));

// 4. 기존 근거 그래프 원본(설정·정책·기능·미결).
const configFile=path.join(legacy,'tools/config.json'),config=JSON.parse(read(configFile));
for(const file of ['AIBed.cs','AIChestAreas.cs','AIChestMode.cs','AITestControl.cs','AITestWorldEdit.cs']){
  const p='Assets/AIPlayerSpike/Scripts/'+file;
  if(!config.codeFiles.some(f=>f.path===p))config.codeFiles.push({container:'모드(게임 안에서 실행)',path:p});
}
config.codeFiles.sort((a,b)=>a.path<b.path?-1:a.path>b.path?1:0);
fs.writeFileSync(configFile,json(config));
const A='work/2026-11/2026-11-08-선택지누락점검/agreement.md';
const policyFile=path.join(legacy,'기획/정책.md');
let policies=read(policyFile);
const boundaryAt=policies.indexOf('### pol.bag-boundary — ');
if(boundaryAt<0)throw Error('Missing pol.bag-boundary');
const boundaryEnd=policies.indexOf('\n\n',policies.indexOf('- 근거:',boundaryAt));
policies=policies.slice(0,boundaryEnd)+'\n- 메모: 2026-11-08 사용자 결정으로 상자 부분(자기 상자 접근 범위 유지, 상자 자원 공유 없음)은 pol.chests-shared가 대체했다. trash 삭제를 제공하지 않는 경계는 유지한다.'+policies.slice(boundaryEnd);
policies+=`

## 2026-11-08 빈 선택지·상자 공용·상자 열림 모드

### pol.choice-gaps — 범위 안의 빈 선택지를 판단 모델 후보로 채움
- 규칙: 가방·장비, 생활, 장비 정비에서 / 모드는 / 범위 안인데 빠진 행동을 판단 모델이 고를 수 있는 후보로 제공한다. 스크립트가 대신 고르거나 후보를 지우지 않고 후보 상한·페이지를 다시 만들지 않는다.
- 등급: 사용자 결정
- 확정도: 확정
- 종류: 처리
- 날짜: 2026-11-08
- 근거: ${A} "빈 선택지는 구현까지 진행한다"
- 근거: ${A} "스크립트가 대신 고르거나 후보를 지우지 않는다."
- 메모: 전투(미구현)와 채집·이동·귀환(별도 진행)은 이 결정의 범위가 아니다. 가축 기르기는 사용자가 고르지 않아 제외했다.

### pol.chests-shared — 모든 상자 공용
- 규칙: 상자를 쓸 때 / AI는 / 호스트·다른 AI 상자를 포함한 모든 상자를 보관·꺼내기·빠른 쌓기·제작 재료에 쓴다. 상자가 많으면 구역 단계로 나눠 모든 상자에 닿게 한다.
- 등급: 사용자 결정
- 확정도: 확정
- 종류: 처리
- 날짜: 2026-11-08
- 근거: ${A} "모든 상자를 공용으로 쓴다. 호스트 상자, 다른 AI 상자 포함"
- 근거: ${A} "보관·꺼내기·빠른 쌓기·제작 재료 계산 모두 이 기준을 따른다."
- 메모: pol.bag-boundary의 자기 상자 접근 범위 유지를 대체한다. 기지에 상자를 더 만들지 판단할 때 세는 상자는 직접 놓은 상자로 남겼다(HEAD 판단, 상자 사용과 별개).

### pol.chest-open-mode — 상자 열림 모드
- 규칙: 상자를 연 동안 / 모드는 / 그 상자의 꺼내기·넣기·빠른 쌓기·정렬과 닫기만 선택지로 준다. 상자가 없어지거나 보이지 않거나 손닿는 거리를 벗어나면 모드를 풀고, 전투 상태에 들어가면 상자를 닫는다.
- 등급: 사용자 결정
- 확정도: 확정
- 종류: 처리
- 날짜: 2026-11-08
- 근거: ${A} "AI가 상자를 연 상태이면, 그 판단 요청의 선택지를 그 상자 안의 일로 제한한다."
- 근거: ${A} "닫기를 고르면 원래 선택면으로 돌아간다."
- 메모: 지금 쓰는 전투 신호는 기존 피격 신호 하나이며 AIChestMode.FightStarted 한 곳에 있다.

### pol.combat-entry — 전투 상태 진입 기준
- 규칙: 공격당하거나 적이 나를 인식하면 / 모드는 / 전투 모드로 바꾸고 전투 선택지만 띄운다. 적이 나를 인식했어도 도주 가능하거나 적이 나를 공격할 수 없으면 전투 상태에 들어가지 않는다.
- 등급: 사용자 결정
- 확정도: 잠정
- 종류: 처리
- 날짜: 2026-11-08
- 근거: ${A} "적이 나를 인식했어도 도주 가능하거나 공격 불가 상태면 전투 상태에 들어가지 않는다."
- 근거: ${A} "전투 구현을 시작할 때는 이 절의 합의 내용을 먼저 사용자에게 제시하고 수정할 내용이 있는지 묻는다."
- 메모: 전투 모드는 아직 구현되지 않았다. 전투 구현을 시작할 때 이 기준을 사용자에게 제시하고 확인받기 전에는 확정 설계로 삼지 않는다.

### pol.maintenance-abandon — 회수 불가 정비 장비의 포기와 상위 알림
- 규칙: 맡긴 장비를 돌려받을 수 없음을 직접 보면 / 판단 모델은 / 포기를 고를 수 있고, 포기하면 상위 LLM에 상황만 알린다. 따질지는 상위 LLM이 정한다.
- 등급: 사용자 결정
- 확정도: 확정
- 종류: 처리
- 날짜: 2026-11-08
- 근거: ${A} "따질지 넘어갈지는 LLM이 정한다. 따지라는 지시는 넣지 않는다."
- 근거: ${A} "가져간 사람은 게임이 기록하지 않으므로 사라진 순간 대장간 근처 플레이어로 추정하고 추정임을 밝힌다."
- 메모: 길을 찾을 수 없는 화면 밖 대장간은 포기 범위가 아니다(U.maintenance-unreachable).
`;
fs.writeFileSync(policyFile,policies);
const featureFile=path.join(legacy,'기획/기능.md');
const G='work/2026-11/2026-11-08-선택지누락점검';
const feature=(id,title,line,pols,anchors,quote,memo)=>`
### ${id} — ${title}
- 상태: 구현됨·새 실게임 미확인
- 종류: 제품
- 한 줄: ${line}
- 정책: ${pols}
- 코드 앵커: ${anchors}
- 근거: ${A} "${quote}"
- 메모: 공식 49bd748의 정적 연결과 오프라인·Unity 근거다. ${memo}
`;
const featureText=`

## 2026-11-08 빈 선택지·상자 공용·상자 열림 모드 (ai-character 49bd748)
`+feature('F.chest-areas','상자 구역 단계와 모든 상자 공용','기억한 모든 상자를 공용으로 쓰고, 40개를 넘으면 구역→작은 구역→상자 단계로 나눠 한 단계가 40개 이하가 되게 한다. 구역 id를 보관·꺼내기 위치로 받는다.','pol.chests-shared, pol.choice-no-cut, pol.choice-gaps','AIChestAreas.Parse, AIChestAreas.IsArea, AIInventory.KnownChestWhere, AIInventory.InArea, AIInventory.ChestCandidates, AIBase.FoodTakeCandidates','모든 상자를 공용으로 쓴다. 호스트 상자, 다른 AI 상자 포함','근거는 '+G+'/fixes.md의 4차 공통 규칙과 전수 목록이다. 상자 밖 목록을 더한 한 요청의 총 후보 수는 U.choice-total-limit에 남는다.')
+feature('F.chest-open-mode','상자 열림 모드','상자를 연 동안 그 상자의 꺼내기·넣기·빠른 쌓기·정렬과 닫기만 판단 요청에 넣고, 상자 사라짐·시야 밖·2칸 밖·피격에서 모드를 푼다. 목표가 없어도 상자가 열려 있으면 닫을 때까지 판단을 잇는다.','pol.chest-open-mode, pol.combat-entry, pol.ai-action-control','AIChestMode.Active, AIChestMode.End, AIChestMode.FightStarted, AIChestMode.Reset, AIInventoryMenu.ChestModeCandidates, AIInventoryMenu.LeaveChestMode','AI가 상자를 연 상태이면, 그 판단 요청의 선택지를 그 상자 안의 일로 제한한다.','전투 신호는 기존 피격 하나이며 전투 모드 구현 때 진입점이 대신한다.')
+feature('F.bag-bulk','가방·상자 정렬과 빠른 쌓기','가방 정렬, 열린 상자 정렬, 열린 상자·근처 상자 빠른 쌓기를 게임 요청 한 번으로 보내고 실제 칸 변화로 완료를 판정한다.','pol.choice-gaps, pol.chests-shared, pol.bag-agency','AIInventoryBulk.Moves, AIInventoryBulk.Queue, AIInventoryBulk.NearbyChests, AIInventoryBulk.Diff, AIInventoryBulk.Stale','B2 일괄 보관·정리: 근처 상자로 빠른 쌓기','근처 상자 범위는 보이는 상자 중 10칸 안으로 근사했다(실게임 확인 필요).')
+feature('F.bag-slots','파우치·프리셋·펫·꾸미기 칸','파우치 칸을 메뉴와 옮기기의 출발·목적 칸으로 쓰고, 착용 프리셋 전환과 비활성 프리셋 장비 보기, 펫 칸·꾸미기 칸 장착을 제공한다. 파우치 안 장비도 정비 대상이다.','pol.choice-gaps, pol.bag-agency, pol.bag-navigation','AIInventory.Pouches, AIInventory.IsPouch, AIInventory.GearSlots, AIInventoryBulk.ActivePreset, AIInventoryBulk.PresetText, AIInventoryBulk.PresetCount','B3 착용 프리셋 전환, 비활성 프리셋 장비 보기. B5 펫 칸 장착. B6 꾸미기 칸.','펫·꾸미기 칸이 교환을 받는지는 실게임 확인 필요다.')
+feature('F.bag-use','회복·허기와 무관한 물품 사용','버프·마나 물약, 선물 상자·탐지기 같은 사용 물품, 악기를 쓴다. 먹기·쓰기는 수량 감소로, 악기는 결과를 볼 수 없어 한 일로 세지 않는다.','pol.choice-gaps, pol.bag-agency','AIInventory.UseKind, InventorySkill.UseTick','B4 물품 사용 넓히기','악기 연주를 주 사용 1회로 보내는 것은 실게임 확인 필요다.')
+feature('F.bag-rearrange','착용 장비 바로 옮기기와 가방 안 자리 옮기기','착용 장비를 바로 보관·내려놓기·전달하고 빈 가방·파우치 칸 어디로든 해제한다. 상자 장비를 바로 착용·들고, 가방·파우치 안에서 옮기기·나누기·합치기·자리 바꾸기를 한다. 분류 정보 없는 물품도 칸 통째 조작을 한다.','pol.choice-gaps, pol.bag-agency, pol.chests-shared','AIInventory.SwapMove, AIInventory.CrossSwap, AIInventory.CapacityAfterReplacement','B7 착용 장비를 바로 보관·내려놓기·전달, 해제 목적지 확대, 상자 장비 바로 착용/들기.','착용 칸에서 바로 내려놓기와 착용 칸·상자 교환의 실제 처리는 실게임 확인 필요다.')
+feature('F.bed-sleep','침대에서 자기','생활 트리의 휴식 갈래에서 침대 잠자리에 올라서 게임의 수면 상태를 관측하고, 체력 가득·60초·게임이 깨움에서 끝낸다. 남이 차지한 침대는 사실로만 보인다.','pol.choice-gaps, pol.behavior-roles','AIBed.LifeEntries, AIBed.ExactCandidate, AIBed.Asleep, SleepSkill.Start, SleepSkill.Tick','M2 침대에서 자기.','서버의 AI 플레이어에게 침대 차지·수면이 처리되는지는 실게임 확인 필요다.')
+feature('F.craft-chests','근처 상자 재료로 제작','게임 서버 규칙대로 가방 다음 제작대 10칸 안 상자를 가까운 순으로 최대 20개까지 재료로 세고, 이번 제작이 실제로 가져갈 칸만 검사하며 출력 관측으로 소비를 확정한다.','pol.chests-shared, pol.choice-gaps','AICraft.CraftChests, AICraft.MaterialCount, AICraft.ConsumedFromPlan','제작: 게임처럼 제작대 근처 상자 재료로 바로 제작(life M4).','화면 밖 기억 시설의 레시피는 제작 목록에 섞지 않는다.')
+feature('F.farm-cook-gaps','솥 입력 정리·빈 땅 물주기·무괭이 수확·씨앗 뿌리개','솥 입력 비대칭 상태의 채우기와 입력 재료 빼기, 작물 없는 판 땅 물주기, 무기·손 수확의 농사 진척, 씨앗 뿌리개 사용과 다른 종자를 심은 사실 보고를 제공한다.','pol.choice-gaps, pol.behavior-roles','AICook.ExactCandidate, AIFarm.SeederCandidate, AIFarm.FirstSeederSeed, AIFarm.AttackHarvestCandidate','M7 범위 농사 도구(씨앗 뿌리개 등) 사용 선택지.','씨앗 뿌리개의 실제 범위 크기는 실게임 확인 필요다(시험은 2x2 가정).')
+feature('F.maintenance-gaps','화면 밖 대장간 회수·pending 중 수리·회수 불가 포기','맡긴 장비가 있는 화면 밖 대장간으로 걷는 후보와 재로드된 대장간 재연결, 업그레이드 pending 중 일반 수리, 대장간 없음·칸 빔을 직접 본 뒤의 포기 선택지와 상위 LLM 상황 알림을 제공한다.','pol.maintenance-abandon, pol.choice-gaps, pol.ai-action-control','AIMaintenance.Recovery, AIMaintenance.Rebind, AIMaintenance.ObserveLoss, AIMaintenance.AbandonCandidate, AIMaintenance.Abandon, AIMaintenance.LostWho, AIMaintenance.PlayersNear, MaintenanceAbandonSkill.Tick','M1 화면 밖 대장간에 남은 자기 장비 회수: 기억한 대장간으로 가는 후보.','상위 알림은 AIPlanner.RequestReplan 경로로 가며 /ai hold에서 막힌다.');
fs.appendFileSync(featureFile,featureText);
const unresolvedFile=path.join(legacy,'기획/미결.md');
fs.appendFileSync(unresolvedFile,`

## 2026-11-08 빈 선택지·상자 공용 검증 경계

### U.gap-live — 빈 선택지 구현의 실제 게임 흐름 미확인
- 종류: 구현만 있음
- 한 줄: 49bd748의 새 선택지는 오프라인·Unity·독립 검수까지만 확인했다. 씨앗 뿌리개 범위, 침대 수면 처리, 착용 칸 직접 내려놓기, 펫·꾸미기 칸 교환, 근처 상자 빠른 쌓기 범위, 악기 연주, 상자 열림 모드의 실제 흐름은 실게임 미확인이다.
- 관련: F.chest-areas, F.chest-open-mode, F.bag-bulk, F.bag-slots, F.bag-use, F.bag-rearrange, F.bed-sleep, F.craft-chests, F.farm-cook-gaps, F.maintenance-gaps
- 닫는 법: 치트 환경 구축 뒤 묶음 실게임 검증에서 대표 흐름을 관측한다.
- 근거: ${A} "실게임은 치트 환경 이후 묶어 검증하는 기존 방침 유지."

### U.choice-total-limit — 한 판단 요청의 전체 후보 합계
- 종류: 미정 결정
- 한 줄: 상자 쪽 후보 최악 약 125개에 상한 없는 벽 캐기·줍기·탐험·둘레 목록이 더해지면 한 요청이 255개를 넘어 판단 요청이 나가지 않을 수 있다. 조립 구조 문제이며 사용자 판단 대기다.
- 관련: F.chest-areas, F.action-constraints-open
- 닫는 법: 사용자가 전체 조립 단계의 분할 방식을 정하면 구현하고 조립 경로 총수를 시험한다.
- 근거: ${G}/review-5.md "상자 쪽만으로는 255에 닿지 않는다."

### U.maintenance-unreachable — 길 없는 화면 밖 대장간 pending
- 종류: 미정 결정
- 한 줄: 포기는 대장간 없음·칸 빔을 직접 본 경우로 한정해, 길을 찾을 수 없는 화면 밖 대장간에 장비가 남으면 막힘 사실만 보고하고 새 업그레이드가 계속 막힌다.
- 관련: F.maintenance-gaps
- 닫는 법: 사용자가 출구를 정한다.
- 근거: ${G}/review.md "경로를 찾을 수 없는 화면 밖 대장간 pending은 포기 선택지가 없음"

### U.salvage — 수리대 분해 선택지
- 종류: 미정 결정
- 한 줄: 장비를 부숴 수리 재료를 얻는 분해의 포함 여부와 범위를 정하지 않았다.
- 관련: F.maintenance-gaps
- 닫는 법: 사용자가 포함 여부를 정한다.
- 근거: ${A} "분해(수리대 salvage)는 이번 범위가 아니다."
`);

// 5. 제품 그래프 planning.mjs 확장(정적 코드 대응과 새 정책 요약).
const planningFile=path.join(live,'D/planning.mjs');
const policyNodes=[['pol.chests-shared','모든 상자 공용','P.chests-shared'],['pol.chest-open-mode','상자 열림 모드','P.chest-open-mode'],['pol.combat-entry','전투 상태 진입 기준(전투 구현 시작 때 사용자 재확인)','P.combat-entry'],['pol.maintenance-abandon','회수 불가 정비 장비의 포기와 상위 알림','P.maintenance-abandon'],['pol.choice-gaps','범위 안의 빈 선택지를 판단 모델 후보로 채움','P.choice-gaps']];
const extension=`  // 빈 선택지·상자 공용·상자 열림 모드(49bd748) 정적 구현 대응과 현재 정책.\n  for (const [from, signature] of ${JSON.stringify(mappings)}) {\n    const targets=graph.nodes.filter(node=>node.type==='코드'&&node.signature===signature);\n    if(targets.length!==1)throw Error('Choice gap anchor absent or ambiguous '+signature);\n    graph.edges.push({from,to:targets[0].id,type:'reference',kind:'빈 선택지 구현 위치',class:'코드 대응',evidence:byId.get(from).sourceLocations,runtimeTrace:false,sourceCommit:graph.meta.sourceCommit});\n  }\n  for (const [id,title,from] of ${JSON.stringify(policyNodes)}) {\n    const plan=byId.get(from);\n    graph.nodes.push({id,type:'정책',title,summary:plan.summary,status:id==='pol.combat-entry'?'사용자 구상·전투 구현 시작 때 재확인':'사용자 결정',class:'사용자 결정 요약',sourceLocations:plan.sourceLocations,evidence:plan.evidence});\n    graph.edges.push({from,to:id,type:'reference',kind:'현재 결정의 정책 요약',class:'합의',evidence:plan.evidence});\n  }\n`;
const planningSource=read(planningFile);
if(!planningSource.includes('  graph.planning ='))throw Error('Missing planning hook');
fs.writeFileSync(planningFile,planningSource.replace('  graph.planning =',extension+'  graph.planning ='));
fs.writeFileSync(path.join(stage,'new-code-mappings.json'),json({sourceCommit:target,mapping:mappings,runtimeTrace:false}));
const overlayFile=path.join(live,'C/overlay-source.json'),overlay=JSON.parse(read(overlayFile).replace(/^\uFEFF/,''));
overlay.baseCommit=target;fs.writeFileSync(overlayFile,json(overlay));

// 6. 과거 가방 39 실행 증거의 후속 투영: 메뉴 파일이 바뀌었으므로 같은 blob 주장을 하지 않는다.
const prior='39b22df96dffe869ab0ccd474b18d31462b71d70',menu='Assets/AIPlayerSpike/Scripts/AIInventoryMenu.cs';
const priorBlob=git('rev-parse',prior+':'+menu),currentBlob=git('rev-parse',target+':'+menu);
if(priorBlob===currentBlob)throw Error('Bag menu unexpectedly unchanged');
const projection={priorEvidenceCommit:prior,currentSourceCommit:target,sameRevision:false,evidenceAppliesToCurrentRevision:false,runtimeTrace:false,scope:'과거39 가방 실행 증거는 당시 판에 귀속된다. 49bd748은 가방 메뉴 파일이 바뀌었으므로 같은 blob 투영을 하지 않고 현재 정적 앵커만 잇는다. 새 가방 선택지는 빈 선택지 구현과 검수 근거를 따른다.',changedSource:{file:menu,priorBlob,currentBlob,sha256:hash(execFileSync('git',['-C',repo,'cat-file','blob',target+':'+menu]))},integrationSources:['agreement.md','integration.md','fixes.md','review-merge.md'].map(name=>{const file=path.join(stage,'source-pins',name);return {path:path.relative(path.dirname(B),file).replaceAll('\\','/'),sha256:hash(fs.readFileSync(file))};})};
fs.writeFileSync(path.join(stage,'successor-projection.json'),json(projection));
const bagFile=path.join(live,'D/bag-source.mjs');let bag=read(bagFile);
const changes=[
  [" const integrationPath=currentObservation?",` const currentChoiceGaps=g.meta.sourceCommit==='${target}';\n const integrationPath=currentChoiceGaps?'../2026-11-08-선택지누락점검/graph-stage/source-pins/integration.md':currentObservation?`],
  [" const projectionPath=currentObservation?"," const projectionPath=currentChoiceGaps?'../2026-11-08-선택지누락점검/graph-stage/successor-projection.json':currentObservation?"],
  ['||currentPathOptimization||currentObservation);','||currentPathOptimization||currentObservation||currentChoiceGaps);'],
  ["  if(projection.unchangedSource.priorBlob!==projection.unchangedSource.currentBlob||hash('B/snapshot/'+projection.unchangedSource.file)!==projection.unchangedSource.sha256)throw Error('Bag menu fixed blob correspondence mismatch');","  if(currentChoiceGaps){if(projection.changedSource.priorBlob===projection.changedSource.currentBlob||hash('B/snapshot/'+projection.changedSource.file)!==projection.changedSource.sha256)throw Error('Bag menu changed-source correspondence mismatch');}\n  else if(projection.unchangedSource.priorBlob!==projection.unchangedSource.currentBlob||hash('B/snapshot/'+projection.unchangedSource.file)!==projection.unchangedSource.sha256)throw Error('Bag menu fixed blob correspondence mismatch');"],
  ['!currentPathOptimization&&!currentObservation&&(','!currentPathOptimization&&!currentObservation&&!currentChoiceGaps&&('],
  ['scopeBoundary:currentObservation?',"scopeBoundary:currentChoiceGaps?'39의 가방 증거를 당시 판에 보존한다. 49bd748은 가방 메뉴가 바뀌어 같은 blob 투영을 하지 않으며 새 가방 선택지는 빈 선택지 구현·검수 근거를 따른다.':currentObservation?"]
];
for(const[before,after]of changes){if(!bag.includes(before))throw Error('Bag hook changed '+before.slice(0,60));bag=bag.replace(before,after);}
fs.writeFileSync(bagFile,bag);

// 7. 행동 판단 그래프 주제 연결(정본 graph.json은 바꾸지 않는다).
let topics=read(behaviorTopics);
const topicAdd={
  craft:{planning:['P.chests-shared'],signatures:['AICraft.CraftChests(']},
  cook:{planning:['P.gap-life'],signatures:[]},
  farm:{planning:['P.gap-life'],signatures:['AIFarm.SeederCandidate(','AIFarm.AttackHarvestCandidate(']},
  inventory:{planning:['P.gap-bag','P.chests-shared','P.chest-open-mode'],signatures:['AIInventoryBulk.Moves(','AIInventoryMenu.ChestModeCandidates(','InventorySkill.UseTick(']},
  maintenance:{planning:['P.gap-maintenance','P.maintenance-abandon'],signatures:['AIMaintenance.AbandonCandidate(','MaintenanceAbandonSkill.Tick(']},
  food:{planning:['P.gap-life'],signatures:['SleepSkill.Tick(','AIBed.ExactCandidate(']},
  interact:{planning:['P.gap-bag'],signatures:['AIInventory.UseKind(']},
  combat:{planning:['P.combat-entry'],signatures:[]}
};
for(const[id,add]of Object.entries(topicAdd)){
  const re=new RegExp("\\['"+id+"','([^']*)',\\[([^\\]]*)\\],\\[([^\\]]*)\\]");
  const m=topics.match(re);if(!m)throw Error('Topic row missing '+id);
  const planningList=m[2]+add.planning.map(p=>",'"+p+"'").join('');
  const sigList=add.signatures.length?(m[3]?m[3]+',':'')+add.signatures.map(s=>"'"+s+"'").join(','):m[3];
  topics=topics.replace(m[0],"['"+id+"','"+m[1]+"',["+planningList+"],["+sigList+"]");
}
topics=topics.replace('// 주제 관련 개발 참고. 각 행은 구현 지원/실행 성공을 주장하지 않는다.','// 주제 관련 개발 참고. 각 행은 구현 지원/실행 성공을 주장하지 않는다.\n// 2026-11-08 빈 선택지(49bd748): 가방·생활·정비·상자·침대·전투 진입 기준의 기획과 코드 검토 위치를 더했다.');
fs.writeFileSync(behaviorTopics,topics);

// 8. 현재 기준 안내.
const intro=`> 현재 코드층 기준은 \`${target}\`이며 공식 ai-character와 같다. 가방·생활·정비의 빈 선택지, 모든 상자 공용, 상자 열림 모드, 전투 상태 진입 기준(전투 구현 시작 때 사용자 재확인)을 반영했다. 공식 61aa67b(이동전달 개선·호출 계기·자유 시간 제거·지역 이동·토큰 절감)와 ae09acf(시험 명령)는 코드층에만 들어 있고 그 기획 반영은 각 담당 몫이다. 설치·실게임은 하지 않았다. [빈 선택지 반영](../2026-11/2026-11-08-선택지누락점검/graph-update.md)을 따른다.\n\n`;
for(const name of ['README.md','제품그래프-사용법.md']){const file=path.join(legacy,name);fs.writeFileSync(file,intro+read(file));}
const bReadme=path.join(B,'README.md');
fs.writeFileSync(bReadme,`> 현재 고정 코드 기준은 \`${target}\`다. 공식 자유 시간 제거(0c8a3e8)에 맞춰 verify의 목표 구분 질문에서 FreeTime 경로를 IdleHold로 바꾸고 증거 줄 번호를 snapshot에서 계산하게 했다. [이번 반영](../../2026-11-08-선택지누락점검/graph-update.md).\n\n`+read(bReadme));
fs.writeFileSync(path.join(stage,'applied-canonical.json'),json({sourceCommit:target,previous,backup,canonicalApplied:true,officialHead:git('rev-parse','HEAD'),newPlanningNodes:nodes.length,newPlanningEdges:edges.length,newMappings:mappings.length,newPolicies:5,newFeatures:10,newUnresolved:4,installation:'not installed; live game not run'}));
console.log('Applied choice-gap delta: '+nodes.length+' nodes, '+edges.length+' edges, '+mappings.length+' code mappings.');
