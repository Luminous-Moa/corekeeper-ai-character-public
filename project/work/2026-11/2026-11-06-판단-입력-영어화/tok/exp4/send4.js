// node send4.js <variant>  - sends every close-fight request whose options the variant changes; resumable; cap 400 calls in exp4
// (variant "csharp" sends csharp.jsonl: requests whose continue text the C# code produced). Never prints or writes the key.
const fs=require('fs');const v=process.argv[2];
const INSTR='목표와 지금 상황에서 {N}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).';
let reqs;
if(v=='csharp')reqs=fs.readFileSync(__dirname+'/csharp.jsonl','utf8').trim().split('\n').map(JSON.parse);
else{const {apply}=require('./variants');reqs=require('./picked.json').filter(x=>x.cls=='brute').map(x=>{const o=apply(x,v);return JSON.stringify(o)==JSON.stringify(x.opts)?null:{id:x.id,who:x.who,input:x.input,opts:o}}).filter(Boolean);}
const out=__dirname+'/res-'+v+'.jsonl';const done=new Set(fs.existsSync(out)?fs.readFileSync(out,'utf8').trim().split('\n').filter(Boolean).map(l=>JSON.parse(l)).filter(r=>r.status==200).map(r=>r.id):[]);
const total=()=>fs.readdirSync(__dirname).filter(f=>/^res-.*\.jsonl$/.test(f)).reduce((a,f)=>a+fs.readFileSync(__dirname+'/'+f,'utf8').trim().split('\n').filter(Boolean).length,0);
(async()=>{let n=0,tok=0;for(const x of reqs){if(done.has(x.id))continue;if(total()>=400){console.log('cap');break;}
 const body={model:'gpt-6-luna',input:x.input,questions:[{type:'choice',name:'action',instructions:INSTR.replace('{N}',x.who),choices:x.opts.map(o=>({value:o.value,description:o.desc}))}]};
 const t0=Date.now();let r,j=null;try{r=await fetch('https://api.openai.com/v1/decisions',{method:'POST',headers:{'Content-Type':'application/json','Authorization':'Bearer '+process.env.OPENAI_API_KEY},body:JSON.stringify(body)});j=JSON.parse(await r.text())}catch(e){}
 const a=j&&j.answers&&j.answers[0];const rec={id:x.id,status:r?r.status:0,ms:Date.now()-t0,tokens:j&&j.usage&&j.usage.input_tokens,choice:a&&a.choice,probs:a&&a.probabilities&&Object.fromEntries(a.probabilities.map(p=>[p.value,p.probability])),err:j&&j.error&&j.error.message};
 fs.appendFileSync(out,JSON.stringify(rec)+'\n');n++;tok+=rec.tokens||0;if(rec.status!=200)console.log('ERR',rec.id,rec.status,rec.err);}
 console.log(v,'requests',reqs.length,'sent',n,'tokens',tok,'usd',(tok*1e-7).toFixed(4));})();
