const {split,mask,H}=require('./seg');const P=require('./picked.json');const fs=require('fs');
const cnt={},chars={},ex={};
for(const x of P){const texts=[...x.input.split('\n'),...x.opts.map(o=>o.desc)];
 for(const t of texts)for(const s of split(t)){if(typeof s!='string'||!H.test(s))continue;const {key}=mask(s);cnt[key]=(cnt[key]||0)+1;chars[key]=(chars[key]||0)+(s.match(/[가-힣]/g)||[]).length;if(!ex[key])ex[key]=s;}}
const keys=Object.keys(cnt).sort((a,b)=>chars[b]-chars[a]);const tot=keys.reduce((a,k)=>a+chars[k],0);
let acc=0;const marks=[100,200,400,600,800,1000,1500,2000];keys.forEach((k,i)=>{acc+=chars[k];if(marks.includes(i+1))console.log(i+1,'keys cover',(acc/tot*100).toFixed(1)+'%')});
console.log('distinct keys',keys.length,'hangul chars',tot);
fs.writeFileSync('exp2/keys.json',JSON.stringify(keys.map(k=>({key:k,n:cnt[k],ch:chars[k],ex:ex[k]})),null,0));
