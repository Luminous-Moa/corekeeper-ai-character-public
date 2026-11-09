// usage: node call.js <sample> <ko|en> <reps>   appends results to calls.jsonl; never prints the key
const fs=require('fs');const [k,lang,reps]=[process.argv[2],process.argv[3],+process.argv[4]||1];
const KI="목표와 지금 상황에서 {N}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).";
const ko=require('./samples/'+k+'.ko.json');const s=lang=='ko'?ko:require('./samples/'+k+'.en.json');
const instr=(lang=='ko'?KI:s.instructions).replace('{N}',ko.who);
const body={model:'gpt-6-luna',input:s.input,questions:[{type:'choice',name:'action',instructions:instr,choices:s.opts.map(o=>({value:o.value,description:o.desc}))}]};
(async()=>{for(let i=0;i<reps;i++){const t0=Date.now();
 const r=await fetch('https://api.openai.com/v1/decisions',{method:'POST',headers:{'Content-Type':'application/json','Authorization':'Bearer '+process.env.OPENAI_API_KEY},body:JSON.stringify(body)});
 const txt=await r.text();const dt=Date.now()-t0;let j=null;try{j=JSON.parse(txt)}catch{}
 const a=j&&j.answers&&j.answers[0];const rec={k,lang,i,status:r.status,ms:dt,tokens:j&&j.usage&&j.usage.input_tokens,cached:j&&j.usage&&j.usage.input_tokens_details&&j.usage.input_tokens_details.cached_tokens,choice:a&&a.choice,conf:a&&a.confidence,probs:a&&a.probabilities&&a.probabilities.map(p=>p.value+'='+p.probability.toFixed(2)).join(' '),err:j&&j.error&&j.error.message};
 fs.appendFileSync('calls.jsonl',JSON.stringify(rec)+'\n');console.log(JSON.stringify(rec));}})();
