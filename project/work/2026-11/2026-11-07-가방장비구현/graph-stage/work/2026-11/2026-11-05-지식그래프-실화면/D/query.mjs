#!/usr/bin/env node
import fs from 'node:fs';
import {spawnSync} from 'node:child_process';
import {ROOT,abs,read,hash,label,indexes,validate,stale,readSources,featureTree,userPaths} from './live-lib.mjs';
const [cmd='overview',...args]=process.argv.slice(2),q=args.join(' ');
try {
 if(cmd==='legacy'){const [sub='overview',...rest]=args;const g=read('preserved/legacy/graph/graph.json');if(sub==='overview'){console.log(JSON.stringify({nodes:g.nodes.length,edges:g.edges.length,types:counts(g.nodes),notice:'보존된 원형. F.*는 과거 기술 능력이며 새 표준 기능과 다름.'},null,2));}else if(['find','show','source','list'].includes(sub)){const ix=indexes(g);if(sub==='find')console.log(g.nodes.filter(n=>JSON.stringify(n).toLowerCase().includes(rest.join(' ').toLowerCase())).map(label).join('\n'));if(sub==='list')console.log(g.nodes.filter(n=>!rest.length||n.type===rest.join(' ')).map(label).join('\n'));if(sub==='show')console.log(JSON.stringify(ix.nodes.get(rest.join(' ')),null,2));if(sub==='source'){const n=ix.nodes.get(rest.join(' '));if(!n)throw Error('unknown legacy ID');console.log(JSON.stringify(readSources(n,g),null,2));}}else {const old=abs('../../지식그래프/tools/query.mjs');const r=spawnSync(process.execPath,[old,sub,...rest],{stdio:'inherit'});if(r.error)throw r.error;process.exitCode=r.status;}process.exit(process.exitCode||0);}
 const g=read('../2026-11-05-지식그래프-실화면/D/graph.json'),{nodes,out,inc}=indexes(g),changed=stale(g);
 if(cmd!=='check'&&changed.length)console.error(`주의: 입력 원문 ${changed.length}개 변경/없음. check 후 원본 재추출·build 필요.`);
 const pick=()=>{if(nodes.has(q))return q;const found=g.nodes.filter(n=>n.id.toLowerCase().includes(q.toLowerCase()));if(found.length!==1)throw Error(`ID ${q}: ${found.length}개 일치. find 사용.\n${found.slice(0,15).map(label).join('\n')}`);return found[0].id;};
 switch(cmd){
 case 'intent': {
   const entry=g.planning.entry, target=q?pick():entry;
   const parents=new Map([[entry,null]]), queue=[entry];
   for(const current of queue){
     if(current===target)break;
     for(const edge of out.get(current)||[]){
       if(!edge.id||parents.has(edge.to))continue;
       parents.set(edge.to,edge);queue.push(edge.to);
     }
   }
   if(!parents.has(target))throw Error('기획 경로가 없는 ID입니다. show 또는 source로 기존 제품 항목을 확인하세요.');
   const trail=[];
   for(let id=target;id;id=parents.get(id)?.from)trail.unshift([id,parents.get(id)]);
   for(const [id,edge] of trail){
     if(edge)console.log('  '+edge.kind+' ['+edge.class+']');
     console.log(label(nodes.get(id)));
   }
   const n=nodes.get(target);
   console.log(n.summary);
   for(const loc of n.sourceLocations)console.log('원문 '+abs(loc.path)+':'+loc.start);
   for(const e of out.get(target)||[])console.log('  '+e.kind+' ['+e.class+'] → '+label(nodes.get(e.to)));
   break;
 }
 case 'overview': console.log(JSON.stringify({제작의도:'intent → find <주제> → intent <기획 ID> → source <기획 ID>',planning:g.planning,meta:{...g.meta,sources:`${g.meta.sources.length}개 (전체 manifest는 graph.json)`},counts:counts(g.nodes),nodes:g.nodes.length,edges:g.edges.length,legacy:g.legacy,limitations:g.limitations},null,2));break;
 case 'find': console.log(g.nodes.filter(n=>JSON.stringify(n).toLowerCase().includes(q.toLowerCase())).map(label).join('\n'));break;
 case 'list': console.log(g.nodes.filter(n=>!q||n.type===q).map(label).join('\n'));break;
 case 'show': {const id=pick();console.log(JSON.stringify({node:nodes.get(id),out:out.get(id)||[],in:inc.get(id)||[]},null,2));break;}
 case 'source': {const id=pick(),sources=readSources(nodes.get(id),g);if(!sources.length)throw Error('직접 원문 위치 없음. show 연결/사용 코드의 source 조회.');for(const s of sources)console.log(`== ${s.path}:${s.start}-${s.end} sha256=${s.sha256}\n${s.text}`);break;}
 case 'feature': console.log(featureTree(g,pick()).lines.join('\n'));console.log('\n추출·실행 경계: B/boundaries.json 및 D/validation.json 참조. 정적 도달은 실행 증명이 아님.');break;
 case 'user': {const result=userPaths(g,pick());result.gaps=result.gaps.map(row=>({...row,reason:nodes.get(row.element)?.mappingBoundary||row.reason}));console.log(JSON.stringify(result,null,2));break;}
 case 'policy': {const id=pick(),n=nodes.get(id);const ids=n.type==='정책'?[id]:[...(inc.get(id)||[]).filter(e=>e.type==='정책→기능').map(e=>e.from)];console.log(JSON.stringify(ids.map(x=>({node:nodes.get(x),applications:out.get(x)||[],sources:readSources(nodes.get(x),g)})),null,2));break;}
 case 'impact': {const id=pick(),seen=new Set([id]),queue=[{id,depth:0}],rows=[],max=4;while(queue.length){const current=queue.shift();if(current.depth>=max)continue;for(const e of inc.get(current.id)||[]){rows.push({depth:current.depth+1,...e});if(!seen.has(e.from)){seen.add(e.from);queue.push({id:e.from,depth:current.depth+1});}}}console.log(JSON.stringify({id,depthLimit:max,edges:rows,notice:'역방향 4단계 후보 영향. 실제 변경 영향은 원문으로 판단.',legacy:'legacy impact <기존 ID>'},null,2));break;}
 case 'check': {const errors=validate(g),head=spawnSync('git',['-C','C:/Dev/CoreKeeperModSDK','rev-parse','HEAD'],{encoding:'utf8'}).stdout?.trim(),drift={baseline:g.meta.sourceCommit,workingTreeHead:head,warning:head!==g.meta.sourceCommit?'기준 이후 작업트리 HEAD 변경. 고정 snapshot 원문 조회는 유효함.':null};const result={ok:!errors.length&&!changed.length,structuralErrors:errors,stale:changed,baselineDrift:drift,validation:read('../2026-11-05-지식그래프-실화면/D/validation.json')};console.log(JSON.stringify(result,null,2));if(!result.ok)process.exitCode=1;break;}
 default: throw Error('intent [기획 ID] | overview | find <text> | show/user/feature/source/policy/impact <ID> | check | legacy <기존 명령>');
 }
} catch(e){console.error(e.message);process.exitCode=1;}
function counts(ns){const result={};for(const n of ns)result[n.type]=(result[n.type]||0)+1;return result;}
