import fs from 'node:fs';
import path from 'node:path';
import {abs,live,read} from './live-lib.mjs';

export function location(document, heading){
 const p=live(document),lines=fs.readFileSync(abs(p),'utf8').split(/\r?\n/);
 let start=0,end=lines.length;
 if(heading){
  const hits=lines.map((line,i)=>({line,i})).filter(x=>x.line===heading);
  if(hits.length!==1)throw Error('missing/ambiguous source heading: '+p+' '+heading);
  start=hits[0].i;const level=heading.match(/^#+/)[0].length;
  const next=lines.slice(start+1).findIndex(l=>new RegExp('^#{1,'+level+'} ').test(l));
  if(next>=0)end=start+next+1;
 }
 return {path:p,start:start+1,end};
}
export function screenLayer(){
 const manifest=read(live('A2/manifest.json')),nodes=[],edges=[];
 for(const s of manifest.screens){
  const doc='A2/'+s.id+'.md',evidence=manifest.evidence.filter(e=>e.screen_id===s.id||(e.common_regions||[]).includes(s.id));
  const images=evidence.map(e=>({id:e.id,raw:live(path.posix.normalize('A2/'+e.source)),annotation:live('A2/'+e.annotation_png),html:live('A2/'+e.annotation_html),observation:e.observation}));
  nodes.push({id:s.id,type:'화면',layer:'live-A2',title:s.name,state:s.state,kind:s.kind,images,sourceLocations:[location(doc)],authority:'observed screen sample; not exhaustive state coverage'});
  for(const common of s.common_regions||[])edges.push({from:s.id,to:common,type:'화면→화면',evidence:live(doc)});
  for(const el of s.elements){
   const heading='### '+el.number+'. '+el.name;
   const positions=evidence.flatMap(e=>{const own=(e.elements||[]).filter(x=>x.id===el.id);const common=!own.length&&(e.common_regions||[]).includes(s.id);return (common?[el]:own).map(x=>({evidence:e.id,bbox:x.bbox,raw:live(path.posix.normalize('A2/'+e.source)),...(common?{coordinateSource:'shared region definition',selectedSlot:e.selected_slot}: {})}));});
   nodes.push({...el,type:'화면요소',layer:'live-A2',summary:el.name,positions,sourceLocations:[location(doc,heading)]});
   edges.push({from:s.id,to:el.id,type:'화면→화면요소',evidence:live(doc)});
   if(el.parent)edges.push({from:el.parent,to:el.id,type:'화면요소→화면요소',evidence:live(doc)});
  }
  for(const policy of s.policies||[]){
   nodes.push({...policy,type:'정책',layer:'live-A2',summary:policy.rule,authority:'관측 범위 표시 규칙 후보; 사용자 확정 정책 아님',sourceLocations:[location(doc,'## 화면 정책')]});
   edges.push({from:s.id,to:policy.id,type:'화면→정책',evidence:live(doc)});
  }
 }
 return {manifest,nodes,edges};
}
