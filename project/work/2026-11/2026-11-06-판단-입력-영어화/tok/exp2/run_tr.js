// translates picked.json with dict.json; writes en.json, missing.json; prints leftover Hangul share
const fs=require('fs');const tr=require('./tr2');const P=require('./picked.json');
const out=[];let hk=0,he=0;
for(const x of P){const e=tr.translateRequest(x);out.push({id:x.id,input:e.input,opts:e.opts});
 hk+=((x.input+x.opts.map(o=>o.desc).join(''))).match(tr.HG).length;he+=((e.input+e.opts.map(o=>o.desc).join('')).match(tr.HG)||[]).length;}
fs.writeFileSync(__dirname+'/en.json',JSON.stringify(out));
const M=Object.entries(tr.MISS).map(([k,v])=>({key:k,n:v.n,ch:(v.ex.match(tr.HG)||[]).length*v.n,ex:v.ex,ctx:v.ctx})).sort((a,b)=>b.ch-a.ch);
fs.writeFileSync(__dirname+'/missing.json',JSON.stringify(M,null,1));
console.log('dict',Object.keys(tr.DICT).length,'missing keys',M.length,'leftover hangul',he,'/',hk,(he/hk*100).toFixed(2)+'%');
