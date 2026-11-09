import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const E=path.dirname(fileURLToPath(import.meta.url)),B=path.resolve(E,'..');
const files=['csharp.raw.json','javascript.raw.json','code.graph.json','validation.json','boundaries.json','snapshot.manifest.json','call-forms.json'];
function pass(){execFileSync('dotnet',['run','--project',path.join(E,'Extract.csproj'),'--',path.join(B,'snapshot'),path.join(B,'csharp.raw.json')],{stdio:'pipe',maxBuffer:1024*1024});execFileSync(process.execPath,[path.join(E,'build.mjs')],{stdio:'pipe'});return Object.fromEntries(files.map(f=>[f,crypto.createHash('sha256').update(fs.readFileSync(path.join(B,f))).digest('hex')]));}
const first=pass(),second=pass(),equal=files.every(f=>first[f]===second[f]);
fs.writeFileSync(path.join(B,'determinism.json'),JSON.stringify({method:'Two fresh Roslyn and Acorn extraction runs over exact Git blob snapshot',equal,first,second,tools:{node:process.version,dotnet:execFileSync('dotnet',['--version'],{encoding:'utf8'}).trim()},excluded:'B7 inference and semantic meanings are separately authored input, not extraction output'},null,2)+'\n');
if(!equal)throw Error('Non-deterministic output');console.log('All '+files.length+' extraction files are byte-identical across both runs.');
