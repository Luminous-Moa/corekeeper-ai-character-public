// node send-en.js -> cases-en.json (English requests), results-en.json; keys from env OPENAI_API_KEY / CELERIS_API_KEY
const fs=require('fs'),path=require('path'),https=require('https');
const INSTR_EN="Given the goal and the current situation, choose the one next choice for Gemini. Looking up the life list keeps the current body action, pending choice and goal. Body actions and bag-menu choices are different. Entering the inventory, choosing a category, item or argument, going back, and cancelling the whole selection change only the selection step and keep the current body action and the original request. An item operation happens only when the final execute entry is chosen after the required arguments. Consider the current situation and risks together; if the current action (continue) still fits, keep it. A newly chosen body action starts from the beginning.";
const EN=JSON.parse(fs.readFileSync(path.join(__dirname,'tr','en-pre.json'),'utf8'));
const KO=JSON.parse(fs.readFileSync(path.join(__dirname,'..','10k','results.json'),'utf8'));
const cases=EN.map(e=>({case:e.case,trigger_ko:e.trigger,instructions:INSTR_EN,input:e.input,choices:e.choices}));
fs.writeFileSync(path.join(__dirname,'cases-en.json'),JSON.stringify(cases,null,1));
const agent=new https.Agent({keepAlive:true,maxSockets:1});
const P={openai:{host:'api.openai.com',path:'/v1/decisions',model:'gpt-6-luna',key:process.env.OPENAI_API_KEY},
 celeris:{host:'inference.celeris.ai',path:'/celeris-1-decision/v1/decisions',model:'celeris-1.1-decision',key:process.env.CELERIS_API_KEY}};
for(const [p,v] of Object.entries(P))if(!v.key){console.error('missing key '+p);process.exit(1)}
function call(prov,c){const X=P[prov];const payload=Buffer.from(JSON.stringify({model:X.model,input:c.input,questions:[{type:'choice',name:'action',instructions:c.instructions,choices:c.choices}]}),'utf8');
 return new Promise(res=>{const t=performance.now();const rq=https.request({host:X.host,path:X.path,method:'POST',agent,timeout:30000,headers:{Authorization:'Bearer '+X.key,'Content-Type':'application/json','Content-Length':payload.length}},r=>{const ch=[];r.on('data',d=>ch.push(d));r.on('end',()=>{const ms=Math.round(performance.now()-t);const s=Buffer.concat(ch).toString('utf8');if(r.statusCode!==200)return res({error:r.statusCode,body:s.slice(0,300),ms});const j=JSON.parse(s),a=j.answers[0];res({ms,choice:a.choice,confidence:a.confidence,top:[...a.probabilities].sort((x,y)=>y.probability-x.probability).slice(0,3).map(p=>[p.value,p.probability]),input_tokens:j.usage.input_tokens})})});
 rq.on('timeout',()=>rq.destroy(new Error('timeout')));rq.on('error',e=>res({error:String(e.message)}));rq.end(payload)})}
(async()=>{for(const p of Object.keys(P))await call(p,cases[0]);
 const out=[];for(const c of cases){const ko=KO.find(k=>k.decision===c.case);const row={case:c.case,ko:{openai:ko.runs.openai.map(r=>r.choice),celeris:ko.runs.celeris.map(r=>r.choice),openai_tokens:ko.runs.openai[0].input_tokens,celeris_tokens:ko.runs.celeris[0].input_tokens},en:{}};
  for(const p of Object.keys(P))row.en[p]=[await call(p,c),await call(p,c)];out.push(row);
  console.log(c.case,'| ko openai',row.ko.openai[0],'celeris',row.ko.celeris[0],'| en openai',row.en.openai.map(r=>r.choice+'('+(r.top?r.top[0][1]:r.error)+')').join(','),'celeris',row.en.celeris.map(r=>r.choice+'('+(r.top?r.top[0][1]:r.error)+')').join(','),'| tok en/ko openai',(row.en.openai[0].input_tokens/row.ko.openai_tokens).toFixed(3),'celeris',(row.en.celeris[0].input_tokens/row.ko.celeris_tokens).toFixed(3),'| ms o',row.en.openai.map(r=>r.ms).join('/'),'c',row.en.celeris.map(r=>r.ms).join('/'));}
 fs.writeFileSync(path.join(__dirname,'results-en.json'),JSON.stringify(out,null,1));
 const med=v=>{const s=[...v].sort((a,b)=>a-b);return s[Math.floor(s.length/2)]};
 for(const p of Object.keys(P)){const v=out.flatMap(r=>r.en[p]).filter(r=>!r.error).map(r=>r.ms);const tk=out.reduce((s,r)=>s+r.en[p][0].input_tokens,0)/out.reduce((s,r)=>s+r.ko[p+'_tokens'],0);
  const same=out.filter(r=>r.en[p][0].choice===r.ko[p][0]).length;console.log(p,'median',med(v),'max',Math.max(...v),'n',v.length,'token en/ko',tk.toFixed(3),'en=ko choice',same+'/'+out.length);}
 console.log('en openai=celeris',out.filter(r=>r.en.openai[0].choice===r.en.celeris[0].choice).length+'/'+out.length);
})();
