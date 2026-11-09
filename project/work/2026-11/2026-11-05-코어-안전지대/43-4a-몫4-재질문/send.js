// node send.js <outdir> [reps=20] - sends every request of reqs.json reps times (same body as AIBrain.BuildJson); resumable;
// cap 560 calls; never prints or writes the key. Modelled on 판단-입력-영어화 tok/exp2/send2.js.
const fs=require('fs'),path=require('path');const RQ=process.argv[2];const REPS=+process.argv[3]||20;
const INSTR='목표와 지금 상황에서 {N}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).';
const R=require(path.resolve(RQ,'reqs.json'));const out=path.join(RQ,'res.jsonl');
const lines=fs.existsSync(out)?fs.readFileSync(out,'utf8').trim().split('\n').filter(Boolean).map(JSON.parse):[];
const done={};for(const r of lines)if(r.status==200)done[r.id]=(done[r.id]||0)+1;
const todo=[];for(const r of R)for(let k=done[r.id]||0;k<REPS;k++)todo.push(r);
const CAP=560;let sent=lines.length;
async function one(x){
 const body={model:'gpt-6-luna',input:x.input,questions:[{type:'choice',name:'action',instructions:INSTR.replace('{N}',x.who),choices:x.opts.map(o=>({value:o.value,description:o.desc}))}]};
 const t0=Date.now();let r,txt;
 try{r=await fetch('https://api.openai.com/v1/decisions',{method:'POST',headers:{'Content-Type':'application/json','Authorization':'Bearer '+process.env.OPENAI_API_KEY},body:JSON.stringify(body)});txt=await r.text();}catch(e){return {id:x.id,status:0,err:String(e).slice(0,200)}}
 let j=null;try{j=JSON.parse(txt)}catch{}
 const a=j&&j.answers&&j.answers[0];
 return {id:x.id,status:r.status,ms:Date.now()-t0,tokens:j&&j.usage&&j.usage.input_tokens,cached:j&&j.usage&&j.usage.input_tokens_details&&j.usage.input_tokens_details.cached_tokens,choice:a&&a.choice,conf:a&&a.confidence,probs:a&&a.probabilities&&Object.fromEntries(a.probabilities.map(p=>[p.value,p.probability])),err:j&&j.error&&j.error.message};
}
(async()=>{let i=0,tok=0,n=0;
 const worker=async()=>{while(i<todo.length){const x=todo[i++];if(sent>=CAP){console.log('cap reached');return;}sent++;
  const r=await one(x);fs.appendFileSync(out,JSON.stringify(r)+'\n');n++;tok+=r.tokens||0;if(r.status!=200)console.log('ERR',r.id,r.status,r.err);}};
 await Promise.all([...Array(4)].map(worker));
 console.log('calls',n,'tokens',tok,'usd',(tok*0.1/1e6).toFixed(4));})();
