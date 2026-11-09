import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync,spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const T=path.dirname(fileURLToPath(import.meta.url)),S=T+'/graph-stage-b25099f';
const W='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work',repo='C:/Dev/CoreKeeperModSDK',sha='b25099f6fd31e4c4c3618357eb9e8101e0e17124';
const roots=['지식그래프','2026-11/2026-11-05-지식그래프-개편','2026-11/2026-11-05-지식그래프-실화면'];
const G=W+'/'+roots[0],P=W+'/'+roots[1],L=W+'/'+roots[2],backup=T+'/evidence/merge/graph-before';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const write=(p,o)=>fs.writeFileSync(p,JSON.stringify(o,null,2)+'\n');
if(execFileSync('git',['-C',repo,'rev-parse','HEAD'],{encoding:'utf8'}).trim()!==sha)throw Error('Main HEAD differs from approval');
const before=read(S+'/canonical-before.json');
for(const f of before)if(hash(f.path)!==f.sha256)throw Error('Concurrent canonical modification: '+f.path);
if(fs.existsSync(backup))throw Error('Backup exists; resume from recorded state instead of overwrite');
const saved=[];
function copy(from,to){fs.mkdirSync(to,{recursive:true});for(const e of fs.readdirSync(from,{withFileTypes:true})){
 if(['node_modules','bin','obj','.git'].includes(e.name))continue;const a=path.join(from,e.name),b=path.join(to,e.name);
 if(e.isDirectory())copy(a,b);else if(e.isFile()){fs.copyFileSync(a,b);const h=hash(a);if(hash(b)!==h)throw Error('Backup mismatch');saved.push({path:a,backup:b,sha256:h});}
}}
for(const r of roots)copy(W+'/'+r,backup+'/'+r);
write(backup+'/manifest.json',saved);
const sourceFiles=[roots[0]+'/tools/config.json',roots[0]+'/기획/기능.md',
 ...['extractor/pin-snapshot.mjs','extractor/build.mjs','extractor/verify.mjs','inference.graph.json'].map(f=>roots[1]+'/B/'+f),
 roots[2]+'/C/overlay-source.json',roots[2]+'/D/integrate.mjs'];
for(const rel of sourceFiles){const from=S+'/work/'+rel,to=W+'/'+rel;if(!fs.existsSync(to))throw Error('Unexpected missing target: '+to);fs.copyFileSync(from,to);}
const cfg=read(G+'/tools/config.json');cfg.codeRoot=repo;cfg.sessionRoot='../..';delete cfg.stagedDocumentRoot;write(G+'/tools/config.json',cfg);
for(const f of ['pin-snapshot.mjs','verify.mjs']){const p=P+'/B/extractor/'+f;fs.writeFileSync(p,fs.readFileSync(p,'utf8').replaceAll("repo='C:/Dev/CoreKeeperModSDK-choice-fixes'","repo='C:/Dev/CoreKeeperModSDK'"));}
const fp=G+'/기획/기능.md';let feature=fs.readFileSync(fp,'utf8').replace('판단 선택지 개선 후보 b25099f','판단 선택지 개선 적용판 b25099f');
let current='';feature=feature.split('\n').map(l=>{const m=l.match(/^### (F\.[\w-]+)/);if(m)current=m[1];
 if(l.startsWith('- 코드 앵커:')&&current==='F.candidates')return l+'\n- 근거: work/2026-11/2026-11-06-선택지개선/06-live-evidence-review.md "제공된 대표 증거는 수정판의 폭발 위험 경고 전달과 정상 선택→실행 연결을 뒷받침한다."\n- 메모: b25099f의 INPUT #64/#116에서 자연 폭발벽 위험문구가 모델에 도달했고 일반 흙 경로와 구분됨. ETA 계산 기준 및 대표 선택→실행을 확인. 필요한 줍기 경계와 직접 캐기/계속하기/본·대안 위험은 오프라인·코드 검수 범위다. 과거 확인한 선 전체를 승격하지 않는다.';
 return l;
}).join('\n');fs.writeFileSync(fp,feature);
const overlay=read(L+'/C/overlay-source.json');
const choiceReports=['../2026-11-06-선택지개선/03-live-validation.md','../2026-11-06-선택지개선/06-live-evidence-review.md'];
for(const r of choiceReports)if(!overlay.evidenceReferences.includes(r))overlay.evidenceReferences.push(r);
overlay.boundaries.push('b25099f 선택지 개선의 새 실게임 근거는 ../2026-11-06-선택지개선/03-live-validation.md 및 06-live-evidence-review.md: 자연 폭발벽 후보2건의 위험문구 전달·일반 흙 경로 구분, 동일속도 ETA 계산 및 대표 선택→실행을 확인했다. 필요한 줍기 경계·직접캐기/계속하기/본대안 위험 전수·생존전략 개선은 이 실게임으로 입증하지 않았다. 설치는 시험 후 복원되어 현재 설치판을 뜻하지 않는다.');
write(L+'/C/overlay-source.json',overlay);
const header='> 공식 코드 기준: `'+sha+'` (2026-11-06 사용자 병합 승인 후 적용). 본 저장소 ai-character와 기준이 같다. 공식 적용 결과는 work/2026-11/2026-11-06-선택지개선/08-merge-and-graph.md를 따른다. 과거76563bf/e82eb87/902721e 증거와 이번 b25099f 한정 실게임 관측은 구별한다.\n\n';
for(const rel of [roots[0]+'/README.md',roots[0]+'/제품그래프-사용법.md',roots[1]+'/B/README.md']){const p=W+'/'+rel;fs.writeFileSync(p,header+fs.readFileSync(p,'utf8').replace(/^> 공식 코드 기준:[^\n]*\r?\n\r?\n/,''));sourceFiles.push(rel);}
const mapdoc=roots[2]+'/B/code-version-mapping.md';fs.writeFileSync(W+'/'+mapdoc,header+fs.readFileSync(W+'/'+mapdoc,'utf8'));sourceFiles.push(mapdoc);
const steps=[];
function run(label,exe,args,cwd=W){const r=spawnSync(exe,args,{cwd,encoding:'utf8',maxBuffer:48*1024*1024,windowsHide:true});steps.push({label,exe,args,cwd,exitCode:r.status,stdout:r.stdout??'',stderr:r.stderr??''});write(T+'/evidence/merge/graph-commands.json',steps);console.log(label+': '+r.status);if(r.status!==0)throw Error(label+' failed');}
run('CHOICE on merged source','dotnet',['run','-c','Release','--project',repo+'/Tools/core-tests','--','CHOICE'],repo);
run('G strict','node',[G+'/tools/build.mjs','--strict']);
run('G check','node',[G+'/tools/query.mjs','check']);
run('pin approved Git blobs','node',[P+'/B/extractor/pin-snapshot.mjs']);
run('Roslyn','dotnet',['run','--project',P+'/B/extractor/Extract.csproj','--',P+'/B/snapshot',P+'/B/csharp.raw.json']);
run('B build','node',[P+'/B/extractor/build.mjs']);
run('B verify','node',[P+'/B/extractor/verify.mjs']);
run('historical screen remap','node',[L+'/B/remap-base.mjs','--base',sha]);
run('P C integrate','node',[P+'/C/integrate-c.mjs']);
run('P C graph','node',[P+'/C/build-graph-C.mjs']);
run('L C validate','node',[L+'/C/validate-C.mjs']);
run('product build','node',[G+'/tools/product-build.mjs']);
run('product questions','node',[L+'/D/questions.mjs']);
run('product check','node',[G+'/tools/product.mjs','check']);
run('product identical regeneration','node',[G+'/tools/product-build.mjs','--check']);
for(const rel of [roots[0]+'/tools/lib.mjs',roots[0]+'/tools/query.mjs']){const b=before.find(x=>x.path.replaceAll('\\','/')===W+'/'+rel);if(!b||hash(W+'/'+rel)!==b.sha256)throw Error('Canonical resolver changed');}
write(T+'/evidence/merge/applied-graph-sources.json',{sourceCommit:sha,backupFiles:saved.length,files:sourceFiles.map(rel=>({path:rel,sha256:hash(W+'/'+rel)})),canonicalResolversUnchanged:true,stagedOverrideRemoved:true,steps:steps.map(({label,exitCode})=>({label,exitCode}))});
console.log(JSON.stringify({sourceCommit:sha,backupFiles:saved.length,sourceFiles:sourceFiles.length}));
