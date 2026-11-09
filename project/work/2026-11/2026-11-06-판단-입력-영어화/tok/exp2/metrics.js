// node metrics.js -> metrics.json + console tables; blind.jsonl / key.json for top-1 disagreements
const fs=require('fs');const P=require('./picked.json');
const rd=f=>Object.fromEntries(fs.readFileSync(f,'utf8').trim().split('\n').map(JSON.parse).filter(r=>r.status==200).map(r=>[r.id,r]));
const KO=rd('res-ko.jsonl'),EN=fs.existsSync('res-en.jsonl')?rd('res-en.jsonl'):{};
const logP=s=>Object.fromEntries(s.split(', ').map(p=>{const i=p.lastIndexOf('=');return [p.slice(0,i),+p.slice(i+1)]}));
const top=p=>Object.entries(p).sort((a,b)=>b[1]-a[1])[0];
const H=p=>{let h=0;for(const v of Object.values(p))if(v>0)h-=v*Math.log2(v);return h};
const TV=(a,b)=>{const k=new Set([...Object.keys(a),...Object.keys(b)]);let s=0;for(const x of k)s+=Math.abs((a[x]||0)-(b[x]||0));return s/2};
const rows=[];
for(const x of P){const k=KO[x.id],e=EN[x.id];if(!k)continue;const L=logP(x.probs);
 const row={id:x.id,cls:x.cls,goal:x.goal,bucket:x.bucket,nopts:x.nopts,logTop:x.answer,koTop:k.choice,logTopP:L[x.answer],koTopP_ofLogTop:k.probs[x.answer]||0,tvLogKo:TV(L,k.probs),koTok:k.tokens};
 row.koTopP=top(k.probs)[1];row.koH=H(k.probs);
 if(e){row.enTop=e.choice;row.enTopP=top(e.probs)[1];row.enH=H(e.probs);row.enTok=e.tokens;row.tvKoEn=TV(k.probs,e.probs);row.enP_ofKoTop=e.probs[k.choice]||0;}
 rows.push(row);}
fs.writeFileSync('metrics.json',JSON.stringify(rows,null,0));
const mean=a=>a.length?a.reduce((s,v)=>s+v,0)/a.length:NaN;const f=(v,d=3)=>isNaN(v)?'-':v.toFixed(d);
function table(keyf,title){const g={};for(const r of rows){const k=keyf(r);(g[k]=g[k]||[]).push(r)}
 console.log('\n## '+title);console.log('group | n | log=ko top1 | TV(log,ko) | ko=en top1 | ko topP | en topP | ko H | en H | TV(ko,en) | en/ko tok');
 for(const [k,a] of Object.entries(g).sort()){const e=a.filter(r=>r.enTop);
  console.log([k,a.length,f(mean(a.map(r=>r.logTop==r.koTop?1:0))),f(mean(a.map(r=>r.tvLogKo))),e.length?f(mean(e.map(r=>r.koTop==r.enTop?1:0))):'-',f(mean(a.map(r=>r.koTopP))),f(mean(e.map(r=>r.enTopP))),f(mean(a.map(r=>r.koH)),2),f(mean(e.map(r=>r.enH)),2),f(mean(e.map(r=>r.tvKoEn))),f(e.reduce((s,r)=>s+r.enTok,0)/e.reduce((s,r)=>s+r.koTok,0))].join(' | '));}}
table(r=>'all','all');table(r=>r.cls,'class');table(r=>r.cls+' '+r.goal,'class x goal');table(r=>r.cls+' '+r.bucket,'class x options');
// blind files
const dis=rows.filter(r=>r.enTop&&r.enTop!=r.koTop);
let seed=7741;const rnd=()=>{seed=(Math.imul(seed,1103515245)+12345)&0x7fffffff;return seed/0x80000000};
const order=dis.map(r=>r.id);for(let i=order.length-1;i>0;i--){const j=Math.floor(rnd()*(i+1));[order[i],order[j]]=[order[j],order[i]]}
const PX=Object.fromEntries(P.map(x=>[x.id,x]));const blind=[],key={};
order.forEach((id,n)=>{const x=PX[id],r=rows.find(q=>q.id==id);const swap=rnd()<0.5;const X=swap?r.enTop:r.koTop,Y=swap?r.koTop:r.enTop;const cid='B'+String(n+1).padStart(3,'0');
 const d=v=>x.opts.find(o=>o.value==v).desc;
 blind.push({case:cid,character:x.who,situation:x.input,choices:x.opts.map(o=>({value:o.value,description:o.desc})),X:{value:X,description:d(X)},Y:{value:Y,description:d(Y)}});
 key[cid]={experimentId:id,file:x.file,decision:x.n,X:swap?'en':'ko',Y:swap?'ko':'en',koTop:r.koTop,koTopP:r.koTopP,enTop:r.enTop,enTopP:r.enTopP,enP_ofKoTop:r.enP_ofKoTop,cls:r.cls,goal:r.goal,nopts:r.nopts};});
fs.mkdirSync('out',{recursive:true});fs.writeFileSync('out/blind.jsonl',blind.map(b=>JSON.stringify(b)).join('\n')+'\n');fs.writeFileSync('out/key.json',JSON.stringify(key,null,1));
console.log('\ndisagreements',dis.length,'X=en share',Object.values(key).filter(k=>k.X=='en').length);
