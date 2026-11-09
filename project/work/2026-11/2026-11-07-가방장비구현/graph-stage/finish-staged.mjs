import path from 'node:path';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url));
for(const [file,args] of [['finalize-tree-evidence.mjs',[]],['collect-evidence.mjs',[]],['refresh-menu-evidence.mjs',[]],['bind-final-evidence.mjs',[]],['run-graph.mjs',['7','13']],['queries.mjs',[]],['audit.mjs',[]]])execFileSync(process.execPath,[path.join(stage,file),...args],{cwd:stage,stdio:'inherit'});
