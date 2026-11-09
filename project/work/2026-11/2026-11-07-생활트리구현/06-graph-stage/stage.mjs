import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {compile} from './compile-stage.mjs';
const root=path.dirname(fileURLToPath(import.meta.url));
const json=g=>JSON.stringify(g,null,2)+'\n';
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const manifest=JSON.parse(fs.readFileSync(path.join(root,'stage-inputs.json'),'utf8'));
const before=await import(pathToFileURL(manifest.canonicalSource));
const staged=await import('./source.mjs');
const g=compile(),[cmd='check',...args]=process.argv.slice(2);
const pick=id=>{const n=g.nodes.find(n=>n.id===id);if(!n)throw Error('없는 ID '+id);return n;};
if(cmd==='build'){fs.writeFileSync(path.join(root,'planning-preview.json'),json(g));console.log('stage planning-preview.json 생성: '+g.nodes.length+'노드/'+g.edges.length+'관계');}
else if(cmd==='check'){
 const preserved=before.entry===staged.entry&&json(before.nodes)===json(staged.nodes.slice(0,before.nodes.length))&&json(before.edges)===json(staged.edges.slice(0,before.edges.length));
 const baselineStable=sha(fs.readFileSync(manifest.canonicalSource,'utf8'))===manifest.canonicalSourceSha256;
 const compilerStable=sha(fs.readFileSync(manifest.canonicalCompiler,'utf8'))===manifest.canonicalCompilerSha256;
 const deterministic=fs.readFileSync(path.join(root,'planning-preview.json'),'utf8')===json(g)&&json(g)===json(compile());
 const implementationNotClaimed=!g.nodes.filter(n=>manifest.addedNodes.includes(n.id)).some(n=>/실게임 확인|구현 완료|검증 통과/.test(n.status));
 const ok=preserved&&baselineStable&&compilerStable&&deterministic&&implementationNotClaimed;
 console.log(json({ok,preserved,baselineStable,compilerStable,deterministic,implementationNotClaimed,entry:g.entry,nodes:g.nodes.length,edges:g.edges.length,sources:Object.keys(g.sources).length,boundary:'기획 사본의 승인 설계·격리 후보 참고 지위만 검증; 공식 제품 코드층/최종 수용/실게임 검증 아님'}));if(!ok)process.exitCode=1;
}else if(cmd==='find')console.log(g.nodes.filter(n=>JSON.stringify(n).includes(args.join(' '))).map(n=>n.id+' ['+n.status+'] '+n.title).join('\n'));
else if(cmd==='show'){const n=pick(args[0]);console.log(json({node:n,out:g.edges.filter(e=>e.from===n.id),in:g.edges.filter(e=>e.to===n.id)}));}
else if(cmd==='source'){for(const e of pick(args[0]).evidence){const p=path.resolve(path.dirname(manifest.canonicalSource),e.path),lines=fs.readFileSync(p,'utf8').split('\n');console.log(p+':'+e.line+'\n'+lines.slice(e.line-1,e.end).map((s,i)=>(e.line+i)+': '+s).join('\n'));}}
else if(cmd==='path'){pick(args[0]);pick(args[1]);const q=[[args[0]]],seen=new Set([args[0]]);let found;while(q.length){const p=q.shift(),last=p.at(-1);if(last===args[1]){found=p;break;}for(const e of g.edges.filter(e=>e.from===last))if(!seen.has(e.to)){seen.add(e.to);q.push([...p,e.to]);}}if(!found)throw Error('이 방향의 경로 없음');console.log(found.map(id=>id+' ['+pick(id).status+'] '+pick(id).title).join('\n → '));}
else throw Error('build | check | find | show | source | path');
