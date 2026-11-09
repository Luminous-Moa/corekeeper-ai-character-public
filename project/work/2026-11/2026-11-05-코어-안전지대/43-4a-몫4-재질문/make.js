// node make.js <outdir> - builds the old/new requests of the 4a share-4 re-ask (설계 3.7) from the logged inputs
const fs=require('fs'),path=require('path');
const RQ=process.argv[2];
function parse(file,who,n){
  const L=fs.readFileSync(path.join(RQ,file),'utf8').split(/\r?\n/);
  const line=L.find(l=>l.includes(`@${who} DECISION #${n} INPUT `));
  const m=line.match(/trigger='(.*?)' .*?input=\[(.*)\] options\((\d+)\): \|\| (.*)$/);
  const opts=m[4].split(' || ').map(s=>{const mm=s.match(/^(\S+) \[(\S+) (.*?)\] (.*)$/);return {value:mm[1],kind:mm[2],key:mm[3],desc:mm[4].replace(/\r$/,'')}});
  const ans=L.find(l=>l.includes(`@${who} DECISION #${n} ANSWER `));
  return {who,n,file,trigger:m[1],input:m[2].replace(/ ⏎ /g,'\n'),opts,logAnswer:ans&&ans.match(/choice=(\S+)/)[1]};
}
// --- the sentences of Core/AIEdgeFacts.cs (checked by core-tests EW; copied here word for word)
const W="둘레 칸을 캐거나 없애는 선택지에는 '둘레가 열림'이 붙어 있다. 열린 둘레로는 적이 기지 안까지 들어온다. 같은 목표를 둘레를 열지 않고 이룰 선택지가 있으면 그쪽이 낫고, 둘레를 열었으면 다시 막는 일(둘레 보수)을 이어서 고른다.";
const baseLine=(inside,open,ours)=>`기지 둘레(안쪽 ${inside}칸): ${open.length==0?'닫힘':`열린 곳 ${open.length}곳(${open.map(t=>`(${t})`).join(' ')}${ours>0?`, 그중 네가 연 곳 ${ours}`:''})`}; 안쪽 적 0. ${W}`;
const refill=b=>b>0?`다시 막기: 가방 벽 블록 ${b}개(연 뒤 '둘레 보수'로 그 칸을 막을 수 있음).`:'가방에 벽 블록 없음 — 열면 바로 막을 수 없음.';
const opened=t=>`네가 연 곳 (${t}) — 열린 채 남음.`;
function blocks0(s,open){ // the same state with no wall block in the bag (old code's words)
  const x=JSON.parse(JSON.stringify(s));
  x.input=x.input.replace(/WallDirtBlock \d+, /,'').replace('손에 든 것 WallDirtBlock','손에 든 것 맨손').replace(/가진 벽 블록 \d+개\)/,'가진 벽 블록 0개)');
  const rep=x.opts.filter(o=>o.kind=='edgerepair');
  if(rep.length){
    const mat=rep[0].desc.match(/보수 재료: 가진 벽 블록 \d+개\(.*?\), (가진 문 \d+개\. .*?보수할 블록은 따로 모아 둬야 함)\./);
    const note=`기지 둘레 보수: 부서진 곳 ${open.length}칸(${open.map(t=>`(${t})`).join(' ')}, 묶음 ${rep.filter(o=>!o.key.endsWith(':out')).length}개), 가진 벽 블록 0개 — 놓을 수 없음(보수 재료가 필요; 적이 부순 벽은 블록을 떨구지 않음, wall-no-drop). 보수 재료: 가진 벽 블록 0개, ${mat[1]}`;
    x.input=x.input.replace('갈 수 없는 대상: ','갈 수 없는 대상: '+note+' / ');
    x.opts=x.opts.filter(o=>o.kind!='edgerepair');
  }
  return x;
}
function toNew(s,{inside,open,ours,blocks,off},noRefill){
  const x=JSON.parse(JSON.stringify(s));
  const lines=x.input.split('\n');
  let i=lines.findIndex(l=>l.startsWith('기지: '));if(i<0)i=lines.findIndex(l=>l.startsWith('지금 다시 판단하는 이유:'));
  lines.splice(i,0,baseLine(inside,open,ours.length));
  x.input=lines.join('\n').replace('(기지 = 그 둘레 10칸)','(기지 상자를 찾는 반경 10칸)');
  // the breach trigger names our opened tiles
  const own=open.filter(t=>ours.includes(t));
  if(own.length&&/기지 둘레 부서짐/.test(x.trigger)){
    const t2=x.trigger+` — 그중 네가 연 곳 ${own.map(t=>`(${t})`).join(' ')}`;
    x.input=x.input.replace('지금 다시 판단하는 이유: '+x.trigger+'.','지금 다시 판단하는 이유: '+t2+'.');x.trigger=t2;
  }
  for(const o of x.opts){
    if(['root','mine','break','dig'].includes(o.kind)&&(o.kind=='dig'?o.desc.includes('기지 둘레 벽(뚫으면 둘레가 열림)'):/둘레가 열림/.test(o.desc))){
      if(off&&o.kind=='root')o.desc+=' '+off;
      if(!noRefill)o.desc+=' '+refill(blocks);
    }
    if(o.kind=='edgerepair'){
      const g=o.desc.match(/부서진 곳 \d+칸\((.*?)\): 블록/);const ts=g?[...g[1].matchAll(/\((-?\d+,-?\d+)\) /g)].map(m=>m[1]):[];
      const f=ts.filter(t=>ours.includes(t)).map(opened);if(f.length)o.desc+=' '+f.join(' ');
    }
  }
  return x;
}
const A1=parse('d115929.log','Gemini',36),A2=parse('d115929.log','Gemini',37),B=parse('d115929.log','Gemini',34);
const D1=parse('d062729.log','Claude',91),D2=parse('d062729.log','Claude',92),D3=parse('d062729.log','Claude',93),E=parse('d115929.log','Gemini',35);
const C1=blocks0(B,[]),C2=blocks0(A1,['-3,3']),E0=blocks0(E,[]);
const S=[
 ['A1','#36 연 뒤(블록 20)',A1,{inside:58,open:['-3,3'],ours:['-3,3'],blocks:20}],
 ['A2','#37 두 번째(블록 20)',A2,{inside:58,open:['-5,4','-3,3'],ours:['-5,4','-3,3'],blocks:20}],
 ['B','#34 열기 전(블록 20)',B,{inside:58,open:[],ours:[],blocks:20}],
 ['C1','#34 블록 0',C1,{inside:58,open:[],ours:[],blocks:0}],
 ['C2','#36 연 뒤 블록 0',C2,{inside:58,open:['-3,3'],ours:['-3,3'],blocks:0}],
 ['D1','22 #91 열기 전(블록 34)',D1,{inside:32,open:['-5,-6','-5,-5','-4,-6','-3,-6'],ours:[],blocks:34}],
 ['D2','22 #92 연 뒤(블록 34)',D2,{inside:32,open:['-5,-6','-5,-5','-4,-6','-3,-6','2,1'],ours:['2,1'],blocks:34,off:'둘레 밖 큰 뿌리 (3,1) 1.9칸(지금 보임, 선택지에 있음).'}],
 ['D3','22 #93 두 번째(블록 34)',D3,{inside:32,open:['-5,-6','-5,-5','-4,-6','-3,-6','2,1'],ours:['2,1'],blocks:34}],
 ['E1','#35 실제로 연 판단(블록 20)',E,{inside:58,open:[],ours:[],blocks:20}],
 ['E0','#35 블록 0',E0,{inside:58,open:[],ours:[],blocks:0}],
];
const out=[];
for(const [id,label,s,p] of S){out.push({id:id+'-old',label,who:s.who,input:s.input,opts:s.opts,logAnswer:s.logAnswer});const n=toNew(s,p);out.push({id:id+'-new',label,who:s.who,input:n.input,opts:n.opts});
 if(p.blocks>0){const m=toNew(s,p,true);out.push({id:id+'-nore',label:label+' 다시 막기 문장 뺌',who:s.who,input:m.input,opts:m.opts});}}
fs.writeFileSync(path.join(RQ,'reqs.json'),JSON.stringify(out,null,1));
for(const r of out)console.log(r.id,r.opts.length,'opts',r.input.length+r.opts.reduce((a,o)=>a+o.desc.length,0),'chars',r.logAnswer||'');
