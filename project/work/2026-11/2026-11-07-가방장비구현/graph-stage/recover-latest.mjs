import fs from 'node:fs';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url));
for(const name of ['canonical-before.json','baseline-summary.json','evidence/development-before.json']){
 const src=path.join(stage,name),dest=path.join(stage,'recovery-before',name);
 if(fs.existsSync(src)&&!fs.existsSync(dest)){fs.mkdirSync(path.dirname(dest),{recursive:true});fs.copyFileSync(src,dest);}
}
for(const name of ['prepare','source-prepare'])execFileSync(process.execPath,[path.join(stage,name+'.mjs')],{stdio:'inherit'});
fs.copyFileSync(path.join(stage,'work/지식그래프/graph/graph.json'),path.join(stage,'evidence/development-before.json'));
for(const name of ['patch-sources','refine-sources','connect-sources','policy-sources','evidence-sources','normalize-fields','common-entry-sources','rule-source-refine'])execFileSync(process.execPath,[path.join(stage,name+'.mjs')],{stdio:'inherit'});
const base='685dde4d27dd91a782a14d8ae33fb576160fa578';
const summary=JSON.parse(fs.readFileSync(path.join(stage,'baseline-summary.json'),'utf8'));summary.sourceCommit=base;
fs.writeFileSync(path.join(stage,'baseline-summary.json'),JSON.stringify(summary,null,2)+'\n');
fs.writeFileSync(path.join(stage,'recovery.json'),JSON.stringify({originalBaseline:'11eb461ae122f404acec94563c086b09e002987c',recoveredCanonicalBaseline:base,finalSourceCommit:'77484ec7f563f4169600a5b01aa66487cfbbb493',scope:'staging only; preserve current release-goal and common-execution authoring; reapply original bag authoring scripts',preservedOriginalInputs:'recovery-before/'},null,2)+'\n');
