import path from 'node:path';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url));
for(const [file,args] of [['prepare-final-integration.mjs',[]],['collect-evidence.mjs',[]],['refresh-integrated-examples.mjs',[]],['bind-final-integration.mjs',[]],['finalize-authoring.mjs',[]],['run-graph.mjs',['5','13']],['integration-queries.mjs',[]],['audit.mjs',[]]])execFileSync(process.execPath,[path.join(stage,file),...args],{cwd:stage,stdio:'inherit'});
