// node metrics3.js -> metrics3.json + tables: KO vs KO' and KO vs EN side by side, KO'·EN agreement
const fs=require('fs');const P=require('./picked.json');const PX=Object.fromEntries(P.map(x=>[x.id,x]));
const rd=f=>Object.fromEntries(fs.readFileSync(f,'utf8').trim().split('\n').map(JSON.parse).filter(r=>r.status==200).map(r=>[r.id,r]));
const KO=rd('../exp2/res-ko.jsonl'),EN=rd('../exp2/res-en.jsonl'),KP=rd('res-kop.jsonl');
const top=p=>Object.entries(p).sort((a,b)=>b[1]-a[1])[0];const H=p=>{let h=0;for(const v of Object.values(p))if(v>0)h-=v*Math.log2(v);return h};
const TV=(a,b)=>{const k=new Set([...Object.keys(a),...Object.keys(b)]);let s=0;for(const x of k)s+=Math.abs((a[x]||0)-(b[x]||0));return s/2};
const isC=(id,v)=>PX[id].opts.find(o=>o.value==v).desc.startsWith('계속하기');
const rows=P.filter(x=>KO[x.id]&&EN[x.id]&&KP[x.id]).map(x=>{const k=KO[x.id],e=EN[x.id],q=KP[x.id];return {id:x.id,cls:x.cls,goal:x.goal,bucket:x.bucket,
 ko:k.choice,en:e.choice,kp:q.choice,koP:top(k.probs)[1],enP:top(e.probs)[1],kpP:top(q.probs)[1],koH:H(k.probs),enH:H(e.probs),kpH:H(q.probs),
 tvKoEn:TV(k.probs,e.probs),tvKoKp:TV(k.probs,q.probs),tvKpEn:TV(q.probs,e.probs),koTok:k.tokens,enTok:e.tokens,kpTok:q.tokens,
 koC:isC(x.id,k.choice),enC:isC(x.id,e.choice),kpC:isC(x.id,q.choice),hasC:x.opts.some(o=>o.desc.startsWith('계속하기'))}});
fs.writeFileSync('metrics3.json',JSON.stringify(rows));
const m=a=>a.length?a.reduce((s,v)=>s+v,0)/a.length:NaN;const f=(v,d=3)=>isNaN(v)?'-':v.toFixed(d);
function table(keyf,title){const g={};for(const r of rows){const k=keyf(r);(g[k]=g[k]||[]).push(r)}
 console.log('\n## '+title);console.log("group | n | top1 KO=KO' | top1 KO=EN | top1 KO'=EN | topP KO / KO' / EN | H KO / KO' / EN | TV KO-KO' | TV KO-EN | TV KO'-EN | continue top1 KO / KO' / EN | both differ from KO: same choice | tok KO'/KO | tok EN/KO");
 for(const [k,a] of Object.entries(g).sort()){const bothDiff=a.filter(r=>r.kp!=r.ko&&r.en!=r.ko);
  console.log([k,a.length,f(m(a.map(r=>r.kp==r.ko?1:0))),f(m(a.map(r=>r.en==r.ko?1:0))),f(m(a.map(r=>r.kp==r.en?1:0))),
   [m(a.map(r=>r.koP)),m(a.map(r=>r.kpP)),m(a.map(r=>r.enP))].map(v=>f(v)).join(' / '),[m(a.map(r=>r.koH)),m(a.map(r=>r.kpH)),m(a.map(r=>r.enH))].map(v=>f(v,2)).join(' / '),
   f(m(a.map(r=>r.tvKoKp))),f(m(a.map(r=>r.tvKoEn))),f(m(a.map(r=>r.tvKpEn))),
   [a.filter(r=>r.koC).length,a.filter(r=>r.kpC).length,a.filter(r=>r.enC).length].join(' / ')+' (of '+a.filter(r=>r.hasC).length+')',
   bothDiff.filter(r=>r.kp==r.en).length+'/'+bothDiff.length,
   f(a.reduce((s,r)=>s+r.kpTok,0)/a.reduce((s,r)=>s+r.koTok,0)),f(a.reduce((s,r)=>s+r.enTok,0)/a.reduce((s,r)=>s+r.koTok,0))].join(' | '));}}
table(r=>'all','all');table(r=>r.cls,'class');table(r=>r.cls+' '+r.bucket,'class x options');table(r=>r.cls+' '+r.goal,'class x goal');
// 2x2 per class: KO' agrees with KO? x EN agrees with KO?
for(const c of ['brute','combat','calm']){const a=rows.filter(r=>r.cls==c);const t={'both same':0,"only KO' differs":0,'only EN differs':0,'both differ':0};
 for(const r of a){const kp=r.kp!=r.ko,en=r.en!=r.ko;t[kp&&en?'both differ':kp?"only KO' differs":en?'only EN differs':'both same']++}console.log(c,JSON.stringify(t));}
