// node metrics.js <outdir> - per request: answers over the reps, mean probabilities of the option groups
const fs=require('fs'),path=require('path');const RQ=process.argv[2];
const R=require(path.resolve(RQ,'reqs.json'));
const res=fs.readFileSync(path.join(RQ,'res.jsonl'),'utf8').trim().split('\n').map(JSON.parse).filter(r=>r.status==200);
const grp=(r,v)=>{const o=r.opts.find(x=>x.value==v);if(!o)return '?';if(o.kind=='edgerepair'||o.kind=='edgemat')return 'repair';
 if(['root','mine','break','dig'].includes(o.kind))return /둘레가 열림/.test(o.desc)?'open':'keep';return 'other';};
let tok=0,n=0;const rows=[];
for(const r of R){const rs=res.filter(x=>x.id==r.id);n+=rs.length;tok+=rs.reduce((a,x)=>a+(x.tokens||0),0);
 const cnt={};for(const x of rs)cnt[x.choice]=(cnt[x.choice]||0)+1;
 const pg={repair:0,open:0,keep:0,other:0};for(const x of rs)for(const [v,p] of Object.entries(x.probs||{})){const g=grp(r,v);if(pg[g]!=null)pg[g]+=p/rs.length;}
 const cg={repair:0,open:0,keep:0,other:0};for(const x of rs)cg[grp(r,x.choice)]++;
 rows.push({id:r.id,n:rs.length,answers:cnt,choiceGroups:cg,meanProb:Object.fromEntries(Object.entries(pg).map(([k,v])=>[k,+v.toFixed(3)])),tokens:rs.length?Math.round(rs.reduce((a,x)=>a+x.tokens,0)/rs.length):0});}
fs.writeFileSync(path.join(RQ,'metrics.json'),JSON.stringify({calls:n,tokens:tok,usd:+(tok*0.1/1e6).toFixed(4),rows},null,1));
for(const x of rows)console.log(x.id.padEnd(8),'n',x.n,'choice',JSON.stringify(x.answers),'groups',JSON.stringify(x.choiceGroups),'P',JSON.stringify(x.meanProb),'tok',x.tokens);
console.log('calls',n,'tokens',tok,'usd',(tok*0.1/1e6).toFixed(4));
