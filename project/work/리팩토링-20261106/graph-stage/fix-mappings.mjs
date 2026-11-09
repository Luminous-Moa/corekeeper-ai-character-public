import fs from 'node:fs';import path from 'node:path';
const root=path.dirname(new URL(import.meta.url).pathname).replace(/^\/([A-Z]:)/,'$1');
const stage=decodeURIComponent(root),P=stage+'/work/2026-11/2026-11-05-지식그래프-개편',L=stage+'/work/2026-11/2026-11-05-지식그래프-실화면';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));const write=(p,v)=>fs.writeFileSync(p,JSON.stringify(v,null,2)+'\n');
const old=read('C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-05-지식그래프-개편/B/code.graph.json'),now=read(P+'/B/code.graph.json'),ids=new Set(now.nodes.map(n=>n.id)),map=new Map();
for(const n of old.nodes){if(ids.has(n.id))continue;const key=n.id.slice(n.id.indexOf('#'));if(!key.startsWith('#'))continue;const found=now.nodes.filter(x=>x.id.endsWith(key)&&x.kind===n.kind);if(found.length===1)map.set(n.id,found[0].id);}
const inf=read(P+'/B/inference.graph.json'),bad=[];
for(const e of inf.edges)if(e.to==='code:Assets/AIPlayerSpike/Scripts/AIBodySkills.cs')e.to='code:Assets/AIPlayerSpike/Scripts/AIBody.Commands.cs';
for(const e of inf.edges)for(const k of ['from','to'])if(map.has(e[k]))e[k]=map.get(e[k]);else if(e[k].startsWith('code:')&&!ids.has(e[k]))bad.push(e[k]);
console.log(JSON.stringify({movedIds:map.size,unresolved:[...new Set(bad)]}));
write(stage+'/symbol-moves.json',[...map].map(([oldId,newId])=>({oldId,newId})));
for(const x of [...inf.nodes,...inf.edges])if(x.evidence){x.historicalEvidence=x.evidence;x.historicalEvidenceCommit=inf.meta.commit;delete x.evidence;}
inf.meta.currentCodeAssociations={commit:now.meta.sourceCommit,basis:'Same qualified symbol identity across responsibility/file separation. Historical output inference is preserved, not re-observed; historicalEvidence ranges refer only to original inference commit.'};
write(P+'/B/inference.graph.json',inf);
let verify=fs.readFileSync(P+'/B/extractor/verify.mjs','utf8').replace('hostCallLines:[144,168],hostInterruptionLine:196,freeSourceLine:81','hostCallLines:[147,156,168,192],hostInterruptionLine:224,freeSourceLine:84').replace('llmSetGoalLine:353,llmRejectHostGoalLine:374','llmSetGoalLine:726,llmHostProtectionFunction:find(\'OnHostGoal\',\'AIPlanner.cs\').id');fs.writeFileSync(P+'/B/extractor/verify.mjs',verify);
let remap=fs.readFileSync(L+'/B/remap-base.mjs','utf8');
remap=remap.replace('const rows = [];',`const rows = [];
const previousFile = n => {
 const file=n.file, name=path.basename(file);
 if(['AIBody.cs','AIBody.Commands.cs','AISkill.cs','AIVision.cs','AITileReader.cs','AIObservation.cs','AINavigator.cs','AIInspectSkills.cs','AIGotoSkill.cs','AIMineSkill.cs','AIAttackSkill.cs','AIMoveTrace.cs'].includes(name))return file.replace(name,'AIBodySkills.cs');
 if(['AIPlayerSpikeMod.Chat.cs','AIPlayerSpikePatches.cs','AIWorldSettings.cs','AICharacterStore.cs'].includes(name))return file.replace(name,'AIPlayerSpikeMod.cs');
 if(['AIMemory.cs','AIDrops.cs','AIObjects.cs','AITunnel.cs'].includes(name))return file.replace(name,'AIExplore.cs');
 return file;
};`);
remap=remap.replace('if (!runFiles.has(n.file)) continue;', 'const runFile=previousFile(n); if (!runFiles.has(runFile)) continue;').replaceAll('blob(RUN, n.file)','blob(RUN, previousFile(n))').replaceAll('file: n.file, baseLines:', 'file: n.file, runFile:previousFile(n), baseLines:');
remap=remap.replace("((n.file || '').endsWith('AIPlayerSpikeMod.cs') && n.kind === 'MethodDeclaration' && (n.lineStart ?? 99999) <= 1090 && 1090 <= (n.lineEnd ?? 0))","(n.signature || '').startsWith('AIPlayerSpikeMod.Step2_NameAndPrepareInput(')");
fs.writeFileSync(L+'/B/remap-base.mjs',remap);
