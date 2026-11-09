// KO vs KO' top-1 disagreements -> 7-대조판정/blind.jsonl + key.json (same format as 2-비교실험; no API calls)
const fs=require('fs');const out=process.argv[2];
const P=require('./picked.json');const PX=Object.fromEntries(P.map(x=>[x.id,x]));
const rd=f=>Object.fromEntries(fs.readFileSync(f,'utf8').trim().split('\n').map(JSON.parse).filter(r=>r.status==200).map(r=>[r.id,r]));
const KO=rd('../exp2/res-ko.jsonl'),KP=rd('res-kop.jsonl');
const top=p=>Object.entries(p).sort((a,b)=>b[1]-a[1])[0];
const dis=P.filter(x=>KO[x.id]&&KP[x.id]&&KO[x.id].choice!=KP[x.id].choice).map(x=>x.id);
let seed=90611;const rnd=()=>{seed|=0;seed=seed+0x6D2B79F5|0;let t=Math.imul(seed^seed>>>15,1|seed);t=t+Math.imul(t^t>>>7,61|t)^t;return ((t^t>>>14)>>>0)/4294967296}; // mulberry32
for(let i=dis.length-1;i>0;i--){const j=Math.floor(rnd()*(i+1));[dis[i],dis[j]]=[dis[j],dis[i]]}
const blind=[],key={};
dis.forEach((id,n)=>{const x=PX[id],k=KO[id],q=KP[id];const swap=rnd()<0.5;const X=swap?q.choice:k.choice,Y=swap?k.choice:q.choice;const cid='C'+String(n+1).padStart(3,'0');
 const d=v=>x.opts.find(o=>o.value==v).desc;
 blind.push({case:cid,character:x.who,situation:x.input,choices:x.opts.map(o=>({value:o.value,description:o.desc})),X:{value:X,description:d(X)},Y:{value:Y,description:d(Y)}});
 key[cid]={experimentId:id,file:x.file,decision:x.n,X:swap?'kop':'ko',Y:swap?'ko':'kop',koTop:k.choice,koTopP:top(k.probs)[1],kopTop:q.choice,kopTopP:top(q.probs)[1],kopP_ofKoTop:q.probs[k.choice]||0,cls:x.cls,goal:x.goal,nopts:x.nopts};});
fs.mkdirSync(out,{recursive:true});fs.writeFileSync(out+'/blind.jsonl',blind.map(b=>JSON.stringify(b)).join('\n')+'\n');fs.writeFileSync(out+'/key.json',JSON.stringify(key,null,1));
const c={};for(const v of Object.values(key))c[v.cls]=(c[v.cls]||0)+1;console.log('cases',dis.length,c,'X=kop',Object.values(key).filter(v=>v.X=='kop').length);
