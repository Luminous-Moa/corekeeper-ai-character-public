import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),L='work/2026-11/2026-11-05-지식그래프-실화면';
const read=p=>fs.readFileSync(path.join(stage,p),'utf8'),write=(p,s)=>fs.writeFileSync(path.join(stage,p),s);
const b=L+'/D/bag-source.mjs';let s=read(b).replace("import {abs} from './live-lib.mjs';","import {abs,hash,read} from './live-lib.mjs';");
s=s.replace('export function addBagImplementation(g){',`export function addBagImplementation(g){\n const evidencePath='../2026-11-07-가방장비구현/implementation-evidence.json',evidence=read(evidencePath);\n if(evidence.sourceCommit!==g.meta.sourceCommit||evidence.liveGameObserved!==false)throw Error('Bag evidence SHA/scope mismatch');\n for(const input of evidence.inputs)if(hash(input.stagedPath)!==input.sha256)throw Error('Bag evidence snapshot hash mismatch: '+input.stagedPath);\n g.bagImplementationEvidence={...evidence,sourceDocument:evidencePath,evidenceKind:'compile/offline/source-review; not live game',currentSourceCommit:g.meta.sourceCommit};`);
s=s.replace(/\n}\n$/,`\n return [evidencePath,...evidence.inputs.map(input=>input.stagedPath)];\n}\n`);write(b,s);
const p=L+'/D/planning.mjs';write(p,read(p).replace('  addBagImplementation(graph);','  const bagSources=addBagImplementation(graph);').replace("'../../지식그래프/기획/정책.md');","'../../지식그래프/기획/정책.md',...bagSources);"));
const i=L+'/D/integrate.mjs';write(i,read(i).replace(' return {g,validation};',' if(g.bagImplementationEvidence)validation.bagImplementation=g.bagImplementationEvidence;\n return {g,validation};'));
console.log('Prepared hash-checked scoped offline evidence integration.');
