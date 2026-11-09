// node send2.js ko|en [limit]  - one call per picked request; resumable; never prints or writes the key
const fs=require('fs');const tr=require('./tr2');const lang=process.argv[2];const limit=+process.argv[3]||1e9;
const P=require('./picked.json');const EN=lang=='en'?Object.fromEntries(require('./en.json').map(e=>[e.id,e])):null;
const out=__dirname+'/res-'+lang+'.jsonl';
const done=new Set(fs.existsSync(out)?fs.readFileSync(out,'utf8').trim().split('\n').filter(Boolean).map(l=>JSON.parse(l)).filter(r=>r.status==200).map(r=>r.id):[]);
const count=f=>fs.existsSync(f)?fs.readFileSync(f,'utf8').trim().split('\n').filter(Boolean).length:0;
const CAP=1000-26; // experiment cap (the 26 earlier calls counted against the total separately; we keep this run <= 974)
const todo=P.filter(x=>!done.has(x.id)).slice(0,limit);
async function one(x){
 const s=lang=='en'?EN[x.id]:{input:x.input,opts:x.opts};
 const instr=(lang=='en'?tr.INSTR_EN:tr.INSTR_KO).replace('{N}',x.who);
 const body={model:'gpt-6-luna',input:s.input,questions:[{type:'choice',name:'action',instructions:instr,choices:s.opts.map(o=>({value:o.value,description:o.desc}))}]};
 const t0=Date.now();let r,txt;
 try{r=await fetch('https://api.openai.com/v1/decisions',{method:'POST',headers:{'Content-Type':'application/json','Authorization':'Bearer '+process.env.OPENAI_API_KEY},body:JSON.stringify(body)});txt=await r.text();}catch(e){return {id:x.id,status:0,err:String(e).slice(0,200)}}
 let j=null;try{j=JSON.parse(txt)}catch{}
 const a=j&&j.answers&&j.answers[0];
 return {id:x.id,status:r.status,ms:Date.now()-t0,tokens:j&&j.usage&&j.usage.input_tokens,cached:j&&j.usage&&j.usage.input_tokens_details&&j.usage.input_tokens_details.cached_tokens,choice:a&&a.choice,conf:a&&a.confidence,probs:a&&a.probabilities&&Object.fromEntries(a.probabilities.map(p=>[p.value,p.probability])),err:j&&j.error&&j.error.message};
}
(async()=>{let i=0,tok=0,n=0;const C=4;
 const worker=async()=>{while(i<todo.length){const x=todo[i++];
  if(count(__dirname+'/res-ko.jsonl')+count(__dirname+'/res-en.jsonl')>=CAP){console.log('cap reached');return;}
  const r=await one(x);fs.appendFileSync(out,JSON.stringify(r)+'\n');n++;tok+=r.tokens||0;if(r.status!=200)console.log('ERR',r.id,r.status,r.err);}};
 await Promise.all([...Array(C)].map(worker));
 console.log(lang,'calls',n,'tokens',tok,'usd',(tok*0.1/1e6).toFixed(4));})();
