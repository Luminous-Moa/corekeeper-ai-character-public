// (a) continue top-1 among the 20 warned, (b) unwarned close-fight requests whose top-1 stays the original's, (c) what the warned picked instead
const fs=require('fs');const {RE}=require('./variants');const P=require('./picked.json').filter(x=>x.cls=='brute');
const rd=f=>fs.existsSync(f)?Object.fromEntries(fs.readFileSync(f,'utf8').trim().split('\n').map(JSON.parse).filter(r=>r.status==200).map(r=>[r.id,r])):{};
const KO=rd('../exp2/res-ko.jsonl'),KP=rd('../exp3/res-kop.jsonl'),EN=rd('../exp2/res-en.jsonl');
const cont=x=>x.opts.find(o=>o.desc.startsWith('계속하기'));const warned=x=>{const c=cont(x);return c&&/답이 오기 전에 끝남/.test(c.desc)};
const kind=(x,v)=>{if(!v)return '?';const c=cont(x);if(c&&c.value==v)return 'continue';return v.replace(/_.*$/,'')+(v.startsWith('step_')?'_'+v.split('_')[1]:'')};
const W=P.filter(warned),N=P.filter(x=>!warned(x));const rows=[];
function row(name,R,fallbackKO){const pick=x=>{const r=R[x.id]||(fallbackKO?KO[x.id]:null);if(!r)return r;const c=cont(x);if(r.choice=='continue_running'&&c)return {...r,choice:c.value,probs:{...r.probs,[c.value]:r.probs.continue_running}};return r;};const wa=W.filter(x=>pick(x));
 const a=wa.filter(x=>pick(x).choice==cont(x).value).length;const nb=N.filter(x=>pick(x));const b=nb.filter(x=>pick(x).choice==KO[x.id].choice).length;
 const dist={};for(const x of wa){const k=kind(x,pick(x).choice);if(k!='continue')dist[k]=(dist[k]||0)+1}
 const pc=wa.map(x=>pick(x).probs[cont(x).value]||0);rows.push({name,a:a+'/'+wa.length,b:b+'/'+nb.length,meanPcont:(pc.reduce((s,v)=>s+v,0)/pc.length).toFixed(3),dist:Object.entries(dist).sort((p,q)=>q[1]-p[1]).map(e=>e[0]+' '+e[1]).join(', ')});}
row('KO 원문',KO,false);row("KO' 바꿔 쓰기",KP,false);row('EN',EN,false);for(const v of ['V1','V2','V3','V4','V5','V6','V7','V8','V9','V10','V11','VX','VX7','csharp'])row(v,rd('res-'+v+'.jsonl'),true);
console.log('| 안 | (a) 경고 20건 계속하기 1순위 | (b) 경고 없는 39건 원문과 같음 | 경고 20건 계속하기 평균 확률 | (c) 대신 고른 것 |');for(const r of rows)console.log(`| ${r.name} | ${r.a} | ${r.b} | ${r.meanPcont} | ${r.dist} |`);
fs.writeFileSync('metrics4.json',JSON.stringify(rows,null,1));
