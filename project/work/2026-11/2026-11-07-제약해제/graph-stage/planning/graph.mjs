#!/usr/bin/env node
// 로컬 문서만 읽는다. build만 이 폴더 안의 파생 산출물을 쓴다.
import fs from 'node:fs';
import path from 'node:path';
import {compile} from './compile.mjs';
import {fileURLToPath} from 'node:url';
import {documents} from './source.mjs';
const root=path.dirname(fileURLToPath(import.meta.url));

const file=p=>path.resolve(root,p);
const read=p=>fs.readFileSync(file(p),'utf8');
const [cmd='overview',...args]=process.argv.slice(2);
const json=g=>JSON.stringify(g,null,2)+'\n';
function sourceLink(e){return `[${e.doc}:${e.line}](<${file(e.path).replaceAll('\\','/')}:${e.line}>)`;}
function render(g){let s='# 재기획 노드와 관계\n\n[큰 그림으로 돌아가기](README.md) · 편집 원본 [source.mjs](source.mjs) · [graph.json](graph.json)\n\n이 표는 원문에서 재생성한 색인이다. 합의는 방향 합의이며 구현 완료를 뜻하지 않는다. 관계의 ‘해석’은 문서 사이 연결을 위한 정리자의 판단이다.\n\n';
 for(const layer of ['재기획','참고']){s+=`## ${layer}\n\n`;for(const n of g.nodes.filter(n=>n.layer===layer)){
 s+=`<a id="${n.id.toLowerCase().replaceAll('.','')}"></a>\n\n### ${n.id} · ${n.title}\n\n**${n.status}** — ${n.summary}\n\n근거: ${n.evidence.map(sourceLink).join(', ')}\n\n`;
 const out=g.edges.filter(e=>e.from===n.id);if(out.length)s+='관계:\n\n'+out.map(e=>`- **${e.type}** → [${e.to} · ${g.nodes.find(n=>n.id===e.to).title}](#${e.to.toLowerCase().replaceAll('.','')}) · ${e.class} · ${e.evidence.map(sourceLink).join(', ')}`).join('\n')+'\n\n';
 }}return s;
}
try{
 const current=compile();
 if(cmd==='build'){fs.writeFileSync(file('graph.json'),json(current));fs.writeFileSync(file('관계목록.md'),render(current));console.log(`생성: ${current.nodes.length} 노드, ${current.edges.length} 관계, ${Object.keys(current.sources).length} 원문. 기존 그래프 변경 없음.`);}
 else if(cmd==='check'){const actual=read('graph.json'),expected=json(current),md=read('관계목록.md');const ok=actual===expected&&md===render(current);console.log(JSON.stringify({ok,nodes:current.nodes.length,edges:current.edges.length,sources:Object.keys(current.sources).length,deterministic:actual===expected,mapMatches:md===render(current),referenceBoundary:`재기획 근거는 등록된 합의 ${Object.values(documents).filter(d=>d.layer==='재기획').length}문서만; 참고→재기획 정책 관계 없음`},null,2));if(!ok)process.exitCode=1;}
 else{
  const saved=JSON.parse(read('graph.json'));
  if(json(saved)!==json(current))console.error('주의: 원문/선언이 변경됨. build 후 check 필요. 아래 조회는 현재 원문으로 계산한 결과입니다.');
  const g=current,ix=new Map(g.nodes.map(n=>[n.id,n]));
  const pick=id=>{if(!ix.has(id))throw Error(`없는 ID ${id}; find <검색어> 사용`);return ix.get(id);};
  if(cmd==='overview')console.log(JSON.stringify({scope:g.scope,counts:{재기획:g.nodes.filter(n=>n.layer==='재기획').length,참고:g.nodes.filter(n=>n.layer==='참고').length,관계:g.edges.length},entry:g.entry,questions:g.nodes.filter(n=>n.status==='미정').map(n=>({id:n.id,title:n.title})),commands:'find <말> | show <ID> | source <ID> | path <시작ID> <끝ID> | check | build'},null,2));
  else if(cmd==='find')console.log(g.nodes.filter(n=>JSON.stringify(n).includes(args.join(' '))).map(n=>`${n.id} [${n.layer}/${n.status}] ${n.title}`).join('\n'));
  else if(cmd==='show'){const n=pick(args[0]);console.log(JSON.stringify({node:n,out:g.edges.filter(e=>e.from===n.id),in:g.edges.filter(e=>e.to===n.id)},null,2));}
  else if(cmd==='source'){const n=pick(args[0]);for(const e of n.evidence){const lines=read(e.path).split('\n'),start=e.line-1;console.log(`${path.resolve(root,e.path)}:${e.line}\n`+lines.slice(Math.max(0,start-1),start+8).map((l,i)=>`${Math.max(0,start-1)+i+1}: ${l}`).join('\n'));}}
  else if(cmd==='path'){const [a,b]=args;pick(a);pick(b);const queue=[[a]],seen=new Set([a]);let found;while(queue.length){const p=queue.shift(),last=p.at(-1);if(last===b){found=p;break;}for(const e of g.edges.filter(e=>e.from===last)){if(!seen.has(e.to)){seen.add(e.to);queue.push([...p,e.to]);}}}
   if(!found)throw Error('이 방향으로 연결된 경로가 없습니다. show로 들어오고 나가는 관계를 확인하세요.');
   console.log(found.map((id,i)=>{const n=pick(id),e=i?g.edges.find(e=>e.from===found[i-1]&&e.to===id):null;return `${e?`  └─ ${e.type} (${e.class}) → `:''}${id} [${n.status}] ${n.title}\n     ${n.evidence.map(x=>`${x.path}:${x.line}`).join(', ')}`;}).join('\n'));
  }else throw Error('overview | find <말> | show/source <ID> | path <시작ID> <끝ID> | build | check');
 }
}catch(e){console.error(e.message);process.exitCode=1;}
