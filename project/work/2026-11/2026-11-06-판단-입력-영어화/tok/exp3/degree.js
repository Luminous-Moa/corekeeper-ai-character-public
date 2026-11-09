// change-degree measures of the templates (weighted by how often each template is used in the 289 requests)
const fs=require('fs');const EN=require('../exp2/dict.json'),KP=require('./dict_kop.json'),HI=require('./hits.json');
const norm=k=>{let n=0;return k.replace(/\{\}/g,()=>'{'+(n++)+'}')};
const order=t=>(t.match(/\{(\d+)\}/g)||[]).map(s=>+s.slice(1,-1));
const reordered=t=>{const o=order(t);return o.some((v,i)=>i>0&&v<o[i-1])};
const sents=t=>(t.match(/[.。](?=\s|$)/g)||[]).length+(t.match(/:\s/g)||[]).length*0; // sentence ends
const lev=(a,b)=>{const m=a.length,n=b.length;let p=Array(n+1).fill(0).map((_,j)=>j);for(let i=1;i<=m;i++){const c=[i];for(let j=1;j<=n;j++)c[j]=Math.min(p[j]+1,c[j-1]+1,p[j-1]+(a[i-1]==b[j-1]?0:1));p=c}return p[n]};
const words=t=>new Set(t.replace(/\{\d+\}/g,' ').split(/[\s·,.:;()\[\]—/→'"]+/).filter(w=>/[가-힣]/.test(w)));
let W=0,ch=0,roK=0,roE=0,scK=0,scE=0,ed=0,jac=0,n=0,W2=0;
for(const [k,w] of Object.entries(HI)){if(!(k in KP))continue;const o=norm(k),kp=KP[k],en=EN[k];W+=w;n++;
 if(kp!=o)ch+=w;if(reordered(kp))roK+=w;if(reordered(en))roE+=w;if(sents(kp)!=sents(o))scK+=w;if(sents(en)!=sents(o))scE+=w;
 ed+=w*lev(o,kp)/Math.max(o.length,kp.length);const A=words(o),B=words(kp);if(A.size){const inter=[...A].filter(x=>B.has(x)).length;jac+=w*(1-inter/(A.size+B.size-inter));W2+=w;}}
const pc=v=>(v/W*100).toFixed(1)+'%';
console.log(JSON.stringify({templates:n,uses:W,"KO' changed":pc(ch),"slot order changed KO'":pc(roK),"slot order changed EN":pc(roE),"sentence count changed KO'":pc(scK),"sentence count changed EN":pc(scE),"char edit distance KO-KO' (normalized, mean)":(ed/W).toFixed(3),"word (eojeol) set change KO-KO' (1-Jaccard, mean)":(jac/W2).toFixed(3)},null,1));
