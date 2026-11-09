// picks ~300 diverse records: class quotas, spread over goal kinds and option-count buckets, dedup by signature and time
const r=require('../recs.json');const {goalKind,cls,ob}=require('./strata');const fs=require('fs');
let seed=20261106;const rnd=()=>{seed=(Math.imul(seed,1103515245)+12345)&0x7fffffff;return seed/0x80000000};
const Q={calm:95,combat:135,brute:70};
const idx=r.map((x,i)=>i);for(let i=idx.length-1;i>0;i--){const j=Math.floor(rnd()*(i+1));[idx[i],idx[j]]=[idx[j],idx[i]]}
const bsig=x=>{const g=(re)=>{const m=x.input.match(re);return m?m[1]:'?'};return ['B',g(/돌진 단계: ([^.(]*)/),g(/지금 하는 일: (\S+)/),Math.round(+g(/가장 가까운 적 \w+ ([\d.]+)칸/)),Math.round(+g(/체력 (\d+)\/\d+, 배고픔/)/20),x.nopts].join('|')};
const sig=x=>cls(x)=='brute'?bsig(x):[cls(x),goalKind(x),ob(x.nopts),[...new Set(x.opts.map(o=>o.value.split('_')[0]))].sort().join(','),x.trigger.replace(/[-\d.]+/g,'#').slice(0,30)].join('|');
const picked=[];const usedSig={};const times={};
for(const c of Object.keys(Q)){
 const pool=idx.filter(i=>cls(r[i])==c);
 const cells={};for(const i of pool){const k=goalKind(r[i])+'|'+ob(r[i].nopts);(cells[k]=cells[k]||[]).push(i)}
 const goals=[...new Set(pool.map(i=>goalKind(r[i])))];
 let got=0;const ok=i=>{const x=r[i],s=sig(x);if(usedSig[s])return false;const tk=x.file+x.who;const ts=times[tk]||[];if(ts.some(t=>Math.abs(t-x.t)<(cls(x)=='brute'?2:20)))return false;return true};
 // round robin goal -> bucket
 let progress=true;const ptr={};
 while(got<Q[c]&&progress){progress=false;
  for(const g of goals){if(got>=Q[c])break;
   for(const b of ['2-5','6-9','10-14','15+']){if(got>=Q[c])break;const k=g+'|'+b;const L=cells[k];if(!L)continue;
    while((ptr[k]||0)<L.length){const p0=ptr[k]||0;const i=L[p0];ptr[k]=p0+1;if(ok(i)){const x=r[i];usedSig[sig(x)]=1;(times[x.file+x.who]=times[x.file+x.who]||[]).push(x.t);picked.push(i);got++;progress=true;break}}
   }}}
}
const out=picked.map((i,n)=>{const x=r[i];return {id:'E'+String(n+1).padStart(3,'0'),rec:i,file:x.file,who:x.who,n:x.n,cls:cls(x),goal:goalKind(x),nopts:x.nopts,bucket:ob(x.nopts),trigger:x.trigger,tokens:x.tokens,answer:x.answer,probs:x.probs,input:x.input,opts:x.opts.map(o=>({value:o.value,kind:o.kind,desc:o.desc}))}});
fs.writeFileSync('exp2/picked.json',JSON.stringify(out,null,0));
const t={};for(const x of out){const k=x.cls+' '+x.goal;t[k]=(t[k]||0)+1};console.log(out.length,t);
const b={};for(const x of out){const k=x.cls+' '+x.bucket;b[k]=(b[k]||0)+1};console.log(b);
const f={};for(const x of out){f[x.file]=(f[x.file]||0)+1};console.log(f);
console.log('ko tokens total',out.reduce((a,x)=>a+x.tokens,0));
