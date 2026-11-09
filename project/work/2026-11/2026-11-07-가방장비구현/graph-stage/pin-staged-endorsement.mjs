import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),parent=path.dirname(stage),expected='bda60df20b996d7d9ae8667a4e8266edbdd6faefd348e06da414fc7c8b840fe2',hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex'),review=path.join(parent,'review-integrated.md');
if(hash(review)!==expected)throw Error('Staged independent endorsement changed before pin');
fs.copyFileSync(review,path.join(parent,'review-integrated-staged-39b.md'));
for(const [file,args] of [['prepare-final-integration.mjs',[]],['collect-evidence.mjs',[]],['refresh-integrated-examples.mjs',[]],['run-graph.mjs',['7','13']],['integration-queries.mjs',[]],['audit.mjs',[]],['write-integration-result.mjs',['reviewed']]])execFileSync(process.execPath,[path.join(stage,file),...args],{cwd:stage,stdio:'inherit'});
if(hash(review)!==expected)throw Error('Endorsement changed during final staged check');
fs.writeFileSync(path.join(stage,'staged-review-gate.json'),JSON.stringify({sourceCommit:'39b22df96dffe869ab0ccd474b18d31462b71d70',independentStagedEndorsement:true,reviewSha256:expected,finalStagedPinAndCheck:true,readOnlyConfirmation:false,rootConditionalApplyAuthorized:true,source:'ROOT conditional apply instruction plus independent reviewer staged endorsement;read-only confirmation still required'},null,2)+'\n');
console.log('Final staged endorsement pinned; awaiting independent read-only confirmation.');
