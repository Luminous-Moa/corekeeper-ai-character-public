// KO original vs C# text (268e676) top-1 disagreements among the 20 warned close-fight requests -> blind.jsonl + key.json
// Situation and choices are the ORIGINAL KO request's (the new wording would reveal the side). No API calls.
const fs=require('fs');const out=process.argv[2];const P=Object.fromEntries(require('./picked.json').map(x=>[x.id,x]));
const rd=f=>Object.fromEntries(fs.readFileSync(f,'utf8').trim().split('\n').map(JSON.parse).filter(r=>r.status==200).map(r=>[r.id,r]));
const KO=rd('../exp2/res-ko.jsonl'),CS=rd('res-csharp.jsonl');
const top=p=>Object.entries(p).sort((a,b)=>b[1]-a[1])[0];
const ids=Object.keys(CS).filter(id=>CS[id].choice!=KO[id].choice);
let seed=61106;const rnd=()=>{seed|=0;seed=seed+0x6D2B79F5|0;let t=Math.imul(seed^seed>>>15,1|seed);t=t+Math.imul(t^t>>>7,61|t)^t;return((t^t>>>14)>>>0)/4294967296};
for(let i=ids.length-1;i>0;i--){const j=Math.floor(rnd()*(i+1));[ids[i],ids[j]]=[ids[j],ids[i]]}
// sides: a shuffled half/half list (no side bias with few cases)
const sides=ids.map((_,i)=>i<Math.floor(ids.length/2));for(let i=sides.length-1;i>0;i--){const j=Math.floor(rnd()*(i+1));[sides[i],sides[j]]=[sides[j],sides[i]]}
const blind=[],key={};
ids.forEach((id,n)=>{const x=P[id],k=KO[id],c=CS[id];const swap=sides[n];const X=swap?c.choice:k.choice,Y=swap?k.choice:c.choice;const cid='W'+String(n+1).padStart(3,'0');
 const d=v=>x.opts.find(o=>o.value==v).desc;
 blind.push({case:cid,character:x.who,situation:x.input,choices:x.opts.map(o=>({value:o.value,description:o.desc})),X:{value:X,description:d(X)},Y:{value:Y,description:d(Y)}});
 key[cid]={experimentId:id,file:x.file,decision:x.n,X:swap?'csharp':'ko',Y:swap?'ko':'csharp',koTop:k.choice,koTopP:top(k.probs)[1],csTop:c.choice,csTopP:top(c.probs)[1],csP_ofKoTop:c.probs[k.choice]||0,cls:x.cls,nopts:x.nopts,commit:'268e676'};});
fs.mkdirSync(out,{recursive:true});fs.writeFileSync(out+'/blind.jsonl',blind.map(b=>JSON.stringify(b)).join('\n')+'\n');fs.writeFileSync(out+'/key.json',JSON.stringify(key,null,1));
console.log('cases',ids.length,'X=csharp',Object.values(key).filter(v=>v.X=='csharp').length);
