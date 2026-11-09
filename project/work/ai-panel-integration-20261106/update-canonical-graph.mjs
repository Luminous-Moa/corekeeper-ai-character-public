import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
const work='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work';
const task=path.join(work,'ai-panel-integration-20261106');
const G=path.join(work,'지식그래프'), P=path.join(work,'2026-11/2026-11-05-지식그래프-개편'), L=path.join(work,'2026-11/2026-11-05-지식그래프-실화면');
const sha='11eb461ae122f404acec94563c086b09e002987c';
const read=p=>fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'');
const json=p=>JSON.parse(read(p));
const put=(p,s)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,s);};
const putJson=(p,v)=>put(p,JSON.stringify(v,null,2)+'\n');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const backup=path.join(task,'canonical-before-ui');
if(fs.existsSync(backup)) throw Error('Backup already exists: do not overwrite');
const manifest=[];
function copyTree(root,tag,sub=''){
 for(const e of fs.readdirSync(path.join(root,sub),{withFileTypes:true})){
  if(['node_modules','bin','obj','.git'].includes(e.name))continue;
  const rel=path.join(sub,e.name),src=path.join(root,rel);
  if(e.isDirectory())copyTree(root,tag,rel);
  else if(e.isFile()){const bytes=fs.readFileSync(src),dest=path.join(backup,tag,rel);put(dest,bytes);manifest.push({source:src,backup:dest,sha256:hash(bytes),bytes:bytes.length});}
 }
}
copyTree(G,'G');copyTree(P,'P');copyTree(L,'L');putJson(path.join(backup,'manifest.json'),manifest);
if(manifest.some(x=>hash(fs.readFileSync(x.backup))!==x.sha256))throw Error('Backup mismatch');
const cfg=json(path.join(G,'tools/config.json'));
const files=execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','ls-tree','-r','--name-only',sha,'Assets/AIPlayerSpike/Scripts','Assets/HeadlessTools/Editor'],{encoding:'utf8'}).trim().split(/\r?\n/).filter(f=>f.endsWith('.cs'));
for(const f of files)if(!cfg.codeFiles.some(x=>x.path===f))cfg.codeFiles.push({container:f.includes('HeadlessTools')?'빌드 도구(Unity Editor)':'모드(게임 안에서 실행)',path:f});
cfg.codeFiles.sort((a,b)=>a.path.localeCompare(b.path));putJson(path.join(G,'tools/config.json'),cfg);
const featurePath=path.join(G,'기획/기능.md');
let features=read(featurePath).replace('이 모드에는 화면 UI가 없다. 그래서 관측 지점은 게임 화면과 Player.log 줄이다','모드는 작은 AI 정보 패널과 기본 채팅의 표시층을 포함한다. 관측 지점은 해당 화면과 Player.log 줄이다');
features+=`\n\n### F.ai-info-ui — AI 스킬과 실제 착용 장비 조회\n- 상태: 실게임 확인\n- 종류: 제품\n- 한 줄: 작은 선택부에서 멤버를 고르면 실제 서버 스킬 레벨과 착용 슬롯을 읽는다. 호스트 읽기 전용이며 모델 호출이나 자동착용을 추가하지 않는다. 실제 빈 슬롯과 장착 fixture 양성 표시, 참가 해제/복귀 갱신을 확인했다.\n- 관측 지점: AI 선택부·스킬/착용 장비 탭; AI-UI equipment source 로그\n- 정책: pol.ui-embedded-readonly\n- 코드 앵커: AIInfoPanel.OnGUI, AIInfoPanel.Frame, AIInfoSnapshot.Read\n- 근거: work/ai-panel-integration-20261106/08-acceptance.md "실제 방어구를 세 부위에 장착한 시험"\n- 메모: fixture는 표시 검증이며 제품 자동착용 성공이 아니다. 원래 프로세스의 상태를 소급 확정하지 않는다.\n\n### F.chat-compact-screen — 기본 채팅 출력 본문을 작게 읽기\n- 상태: 실게임 확인\n- 종류: 제품\n- 한 줄: 기본 ChatWindow/PugText와 입력·멘션·Tab·알림·fade를 유지하고 출력 glyph만 화면 해상도로 작게 표시한다. 최종 실게임은 본문43~44→24 캡처px와 줄간격26~29px를 확인했다. 입력행·멘션 힌트 크기는 원래대로 보존한다.\n- 관측 지점: 실제 AI 발언 화면38, CHAT-SCREEN metrics, fade 캡처\n- 정책: pol.ui-embedded-readonly\n- 코드 앵커: AICompactFont.SetPugFontPrefix, AIChatScreenLayer.AfterRender, AIChatScreenLayer.OnGUI, AIChatScreenLayer.BeforeRelease, AICompactFont.RefreshScreenMetrics\n- 근거: work/ai-panel-integration-20261106/08-acceptance.md "입력행과 멘션 힌트의 기존 크기는 유지하며 추가 축소 대상은 출력 본문이다."\n- 메모: 실제 OS IME 조합·다중버튼 동시 유지·다른 해상도 실게임은 미시험. 렌더 실패/미지원 마스크·재질은 원래 게임 표시로 돌아간다.\n`;
put(featurePath,features);
put(path.join(G,'기획/정책.md'),read(path.join(G,'기획/정책.md'))+`\n\n### pol.ui-embedded-readonly — 기본 채팅과 읽기 전용 AI 정보 UI를 모드에 흡수\n- 규칙: 채팅/AI 정보 UI를 만들 때 / 이 모드는 / 별도 모드 설치 없이 작은 채팅 출력과 실제 스킬·착용 장비 조회를 제공하고 기존 입력·멘션·Tab·알림·fade와 다른 게임 UI를 보존한다. 추가 모델 호출·자동착용·행동 선택지 수정은 포함하지 않는다.\n- 등급: 사용자 결정\n- 확정도: 확정\n- 종류: 범위\n- 날짜: 2026-11-06\n- 근거: work/ai-panel-integration-20261106/00-agreement.md "외부 모드를 별도로 구독·설치하게 만드는 완료 방식은 목표와 맞지 않는다."\n- 근거: work/ai-panel-integration-20261106/08-acceptance.md "AI 자동착용·행동 선택지 변경은 사용자 결정에 따라 별도 작업이다."\n`);
for(const file of ['extractor/pin-snapshot.mjs','extractor/verify.mjs','extractor/build.mjs']){
 const p=path.join(P,'B',file);put(p,read(p).replace(/b25099f6fd31e4c4c3618357eb9e8101e0e17124/g,sha));
}
const build=path.join(P,'B/extractor/build.mjs');let bs=read(build);
bs=bs.replace("  if(base.includes('IMod')", "  if(base.includes('MonoBehaviour')&&m.name==='OnGUI')hook(m,'Unity MonoBehaviour.OnGUI message; statically identified, not a runtime trace');\n  if(base.includes('IMod')");put(build,bs);
const current=`> 공식 코드 기준: \`${sha}\` (2026-11-06 사용자 승인 후 UI 흡수판). 본 ai-character와 일치. 현재 실게임·수용 근거는 work/ai-panel-integration-20261106/04-live-verification.md 및08-acceptance.md, 통합 결과는09-final-integration.md. 아래 과거 기록은 당시 근거로 보존한다.\n\n`;
for(const p of [path.join(G,'README.md'),path.join(G,'제품그래프-사용법.md'),path.join(P,'B/README.md'),path.join(L,'B/code-version-mapping.md')])put(p,current+read(p));
const overlay=json(path.join(L,'C/overlay-source.json'));overlay.baseCommit=sha;
overlay.boundaries.push('11eb461 UI 흡수 관측은 uiRuntimeObservations에 별도 수록한다. 작은 출력 본문/읽기 전용 패널과 시험 장착 fixture의 표시 확인이며 자동착용·행동 변경이 아니다. 입력행/멘션 힌트의 기존 크기, OS IME 조합·다중버튼·다른 해상도 미시험을 보존한다.');
const obsFolder='C/ui-20261106';
const inputs=[];
for(const rel of ['00-agreement.md','04-live-verification.md','08-acceptance.md','07-chat-screen-review.md','evidence/live/34-fixture-equipped-panel-avatar.png','evidence/live/38-final-chat-real-ai-reply.png','evidence/live/43-final-skills.png','evidence/live/chat-installed-source-check.json','evidence/live/fixture-equipment-source-raw.log','evidence/live/final-chat-row-measurement.json','evidence/live/fixture-undo-verification.json']){
 const original=path.join(task,rel);if(!fs.existsSync(original)){if(rel.includes('43-final-skills'))continue;throw Error('Evidence missing '+original);}
 const bytes=fs.readFileSync(original),staged=obsFolder+'/'+path.basename(rel);put(path.join(L,staged),bytes);inputs.push({originalPath:original,stagedPath:staged,sha256:hash(bytes),bytes:bytes.length});
}
const observations={schemaVersion:1,sourceCommit:sha,status:'accepted-bounded-live-verification',inputs,authority:'04 live report and 08 HEAD acceptance; no inferred function trace and no auto-equip claim',observations:[
 {id:'UI-CHAT-11eb461',capability:'작은 기본 채팅 출력',developmentCapabilities:['F.chat-compact-screen'],relatedProductFeatures:['hook:AIChatScreenLayer.OnGUI'],status:'observed',boundary:'Actual Korean AI reply 43~44 to24 capture px, 7s+1s fade; input and mention hint remain native size. IME composition not exercised.'},
 {id:'UI-INFO-11eb461',capability:'스킬·착용 장비 읽기',developmentCapabilities:['F.ai-info-ui'],relatedProductFeatures:['hook:AIInfoPanel.OnGUI'],status:'observed',boundary:'Skill XP conversion, empty slots, explicit three-slot armor fixture, off/on clearing/restoring. Fixture is test data, not product auto-equip.'}
 ],limitations:['No OS IME composition test','No simultaneous mouse buttons test','No extra resolution live test','No quantitative performance benchmark'],functionTraceObserved:false};
putJson(path.join(L,obsFolder,'observations.json'),observations);overlay.uiObservationSource=obsFolder+'/observations.json';
overlay.evidenceReferences.push(overlay.uiObservationSource,...inputs.map(x=>x.stagedPath));
const doc=obsFolder+'/ui-flow.md';
put(path.join(L,doc),'# AI 정보와 작은 채팅 출력\n\n호스트는 작은 AI 선택부를 눌러 실제 스킬/착용 장비를 조회하고 탭과 X로 닫는다. 기본 채팅에서 AI 발언을 작은 본문으로 읽으며 입력/멘션/Tab은 기존 방식과 크기로 유지한다. 별도 설치, 모델 호출, 자동착용은 없다.\n\n근거: 이 폴더의04-live-verification.md,08-acceptance.md와34/38 실제 화면. 화면요소 ID: CKAI-UI-01#select, CKAI-UI-01#details, CKAI-UI-02#output. 실제 함수 trace는 관측하지 않았다.\n');
overlay.evidenceReferences.push(doc);
for(const [id,type,title]of [['CKAI-UI-ACT-01','유저 활동','AI 정보 조회와 작은 채팅 읽기'],['CKAI-UI-01','화면','AI 선택과 정보 패널'],['CKAI-UI-02','화면','기본 채팅의 작은 출력'],['CKAI-UI-01#select','화면요소','AI 선택부'],['CKAI-UI-01#details','화면요소','스킬과 실제 착용 장비'],['CKAI-UI-02#output','화면요소','작은 한국어 AI 발언']])overlay.nodes.push({id,type,title,summary:title,doc,source:{document:doc,section:'AI 정보와 작은 채팅 출력'}});
for(const [source,type,target]of [
 ['CKAI-UI-ACT-01','유저 활동→화면요소','CKAI-UI-01#select'],['CKAI-UI-ACT-01','유저 활동→화면요소','CKAI-UI-01#details'],['CKAI-UI-ACT-01','유저 활동→화면요소','CKAI-UI-02#output'],
 ['CKAI-UI-01','화면→화면요소','CKAI-UI-01#select'],['CKAI-UI-01','화면→화면요소','CKAI-UI-01#details'],['CKAI-UI-02','화면→화면요소','CKAI-UI-02#output'],
 ['CKAI-UI-01#select','화면요소→기능','hook:AIInfoPanel.OnGUI'],['CKAI-UI-01#details','화면요소→기능','hook:AIInfoPanel.OnGUI'],['CKAI-UI-02#output','화면요소→기능','hook:AIChatScreenLayer.OnGUI']])overlay.edges.push({source,type,target,evidence:doc});
put(path.join(L,'C/활동-동료확인.md'),read(path.join(L,'C/활동-동료확인.md'))+'\n\n## 2026-11-06 추가 UI 활동\n\n새 활동 CKAI-UI-ACT-01에서 CKAI-UI-01#select, CKAI-UI-01#details, CKAI-UI-02#output을 사용한다. 별도 근거와 범위는 C/ui-20261106/ui-flow.md 및 observations.json을 따른다. 과거 화면 관측을 새 실행으로 승격하지 않는다.\n');
putJson(path.join(L,'C/overlay-source.json'),overlay);
console.log(JSON.stringify({backupFiles:manifest.length,backupBytes:manifest.reduce((n,x)=>n+x.bytes,0),codeFiles:cfg.codeFiles.length,uiInputs:inputs.length,commit:sha}));
