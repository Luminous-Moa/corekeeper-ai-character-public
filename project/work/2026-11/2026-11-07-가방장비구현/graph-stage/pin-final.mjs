import fs from 'node:fs';import path from 'node:path';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),repo=process.argv[3]||'C:/Dev/CoreKeeperModSDK-bag-common';
const G='work/지식그래프',P='work/2026-11/2026-11-05-지식그래프-개편',L='work/2026-11/2026-11-05-지식그래프-실화면';
const sha=process.argv[2];if(!/^[a-f0-9]{40}$/.test(sha||''))throw Error('Expected owner-confirmed full final SHA');
const actual=execFileSync('git',['-C',repo,'rev-parse',sha],{encoding:'utf8'}).trim();if(actual!==sha)throw Error('SHA resolution mismatch');
const read=p=>fs.readFileSync(path.join(stage,p),'utf8'),write=(p,s)=>fs.writeFileSync(path.join(stage,p),s);
const previous=fs.existsSync(path.join(stage,'final-pin.json'))?JSON.parse(read('final-pin.json')).sourceCommit:null;
const copiedSource=JSON.parse(read(P+'/B/code.graph.json')).meta.sourceCommit;
const baseCommit=fs.existsSync(path.join(stage,'rebase-baseline.json'))?JSON.parse(read('rebase-baseline.json')).sourceCommit:copiedSource;
for(const rel of [P+'/B/extractor/pin-snapshot.mjs',P+'/B/extractor/verify.mjs',P+'/B/extractor/build.mjs']){let s=read(rel);for(const prior of new Set(['685dde4d27dd91a782a14d8ae33fb576160fa578',previous,copiedSource].filter(Boolean)))s=s.replaceAll(prior,sha);write(rel,s.replaceAll("repo='C:/Dev/CoreKeeperModSDK'","repo='"+repo+"'"));}
write(L+'/B/remap-base.mjs',read(L+'/B/remap-base.mjs').replace("const REPO = 'C:/Dev/CoreKeeperModSDK';","const REPO = '"+repo+"';"));
for(const folder of ['snapshot','runtime-package']){const full=path.resolve(stage,P,'B',folder);if(!full.startsWith(path.resolve(stage)+path.sep)||!full.endsWith(path.sep+'B'+path.sep+folder))throw Error('Unsafe stage cleanup path');fs.rmSync(full,{recursive:true,force:true});}
execFileSync(process.execPath,[path.join(stage,P,'B/extractor/pin-snapshot.mjs')],{stdio:'inherit'});
const cfg=JSON.parse(read(G+'/tools/config.json')),snap=path.join(stage,P,'B/snapshot');
const files=[];function walk(d){for(const ent of fs.readdirSync(d,{withFileTypes:true})){const full=path.join(d,ent.name);if(ent.isDirectory())walk(full);else if(full.endsWith('.cs'))files.push(path.relative(snap,full).replaceAll('\\','/'));}}
for(const rel of cfg.codeDirs)walk(path.join(snap,rel));
cfg.codeFiles=files.sort().map(p=>({container:p.includes('HeadlessTools')?'빌드 도구(Unity Editor)':'모드(게임 안에서 실행)',path:p}));
write(G+'/tools/config.json',JSON.stringify(cfg,null,2)+'\n');
const overlay=JSON.parse(read(L+'/C/overlay-source.json'));overlay.baseCommit=sha;overlay.historicalUiObservationCommit='11eb461ae122f404acec94563c086b09e002987c';
overlay.boundaries=overlay.boundaries.filter(s=>!s.startsWith('이번 가방·장비 격리 코드 기준 '));
overlay.boundaries.push('이번 가방·장비 격리 코드 기준 '+sha+'; 컴파일/오프라인 검사만 근거로 사용한다. 과거 UI11eb461/리팩토링902721e 관측은 역사적 커밋에만 적용하며 이 후보의 실게임 확인·최종수용을 뜻하지 않는다.');
write(L+'/C/overlay-source.json',JSON.stringify(overlay,null,2)+'\n');
const int=L+'/D/integrate.mjs';let s=read(int);
s=s.replace("if(uiObservations.schemaVersion!==1||uiObservations.sourceCommit!==g.meta.sourceCommit)","if(uiObservations.schemaVersion!==1||(uiObservations.sourceCommit!==g.meta.sourceCommit&&uiObservations.sourceCommit!==c.historicalUiObservationCommit))");
s=s.replaceAll('sameRevision:true,evidenceAppliesToCurrentRevision:true','sameRevision:uiObservations.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:uiObservations.sourceCommit===g.meta.sourceCommit');
s=s.replace("sameRevision:true,status:uiObservations.status","sameRevision:uiObservations.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:uiObservations.sourceCommit===g.meta.sourceCommit,status:uiObservations.status");
write(int,s);
write('final-pin.json',JSON.stringify({sourceCommit:sha,repo,codeFiles:cfg.codeFiles.length,baseCommit,scope:'isolated staging only; no official graph or product code writes'},null,2)+'\n');
console.log('Pinned final '+sha+'; registered '+cfg.codeFiles.length+' C# files.');
