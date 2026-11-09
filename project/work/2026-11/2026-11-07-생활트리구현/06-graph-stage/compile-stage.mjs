import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {documents,nodes,edges,entry} from './source.mjs';
// Stage 사본만 import하고 근거 문서는 정식 원문 위치에서 읽는다.
const root="C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-07-기획연결";
const hash=s=>crypto.createHash('sha256').update(s).digest('hex');
const file=p=>path.resolve(root,p);
const read=p=>fs.readFileSync(file(p),'utf8');

export function compile(){
 const errors=[],sources={};
 for(const [id,d] of Object.entries(documents)){
  try {const raw=read(d.path);sources[id]={...d,sha256:hash(raw),raw};}catch(e){errors.push(`문서 ${id}: ${e.message}`);}
 }
 function resolve(e,owner){const d=sources[e.doc];if(!d){errors.push(`${owner}: 원문 없음 ${e.doc}`);return e;}const at=d.raw.indexOf(e.anchor);if(at<0){errors.push(`${owner}: 원문 앵커 없음 ${e.doc} ${e.anchor}`);return e;}
  const start=d.raw.slice(0,at).split('\n').length;
  const lines=d.raw.split(/\r?\n/);
  let end=start+e.anchor.split('\n').length-1;
  if(/^#+\s/.test(lines[start-1])){
   while(end<lines.length && !/^#+\s/.test(lines[end]))end++;
   while(end>start && !lines[end-1].trim())end--;
  }
  return {...e,path:d.path,line:start,end,sha256:d.sha256};
 }
 const ids=new Set();
 const ns=nodes.map(n=>{if(ids.has(n.id))errors.push(`중복 ID ${n.id}`);ids.add(n.id);if(!n.evidence.length)errors.push(`근거 없음 ${n.id}`);
  if(n.layer==='재기획'&&n.evidence.some(e=>documents[e.doc]?.layer!=='재기획'))errors.push(`재기획에 참고 문서 승계 ${n.id}`);
  return {...n,evidence:n.evidence.map(e=>resolve(e,n.id))};});
 const es=edges.map((e,i)=>{if(!ids.has(e.from)||!ids.has(e.to))errors.push(`관계 끝점 없음 ${i}`);
  const from=ns.find(n=>n.id===e.from),to=ns.find(n=>n.id===e.to);
  if(from?.layer==='참고'&&to?.layer==='재기획')errors.push(`참고가 재기획을 규정하는 역방향 관계 ${i}`);
  if(to?.layer==='참고'&&!e.type.startsWith('참고'))errors.push(`참고 경계 관계명 누락 ${i}`);
  return {id:`E${String(i+1).padStart(3,'0')}`,...e,evidence:e.evidence.map(x=>resolve(x,`관계 ${i+1}`))};});
 if(errors.length)throw Error(errors.join('\n'));
 if(!ids.has(entry))throw Error('기획 진입점 없음 '+entry);
 return {schemaVersion:1,entry,scope:'프로젝트 그래프의 재기획 원본. 참고 자료의 지위와 기획 합의를 구분한다.',sources:Object.fromEntries(Object.entries(sources).map(([id,{raw,...d}])=>[id,d])),nodes:ns,edges:es};
}
