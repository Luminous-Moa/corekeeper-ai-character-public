// usage: node parse.js <out.json> <log...>  - extracts DECISION INPUT + RESULT records
const fs=require('fs'),readline=require('readline');
const {getEncoding}=require('js-tiktoken');const enc=getEncoding('o200k_base');
const out=process.argv[2];const files=process.argv.slice(3);
const INSTR="목표와 지금 상황에서 {N}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).";
(async()=>{
 const recs=[];
 for(const f of files){
  const rl=readline.createInterface({input:fs.createReadStream(f,{encoding:'utf8'}),crlfDelay:Infinity});
  const pend={};
  for await(const line of rl){
   let m=line.match(/^t=([\d.]+) @(\S+) DECISION #(\d+) INPUT .*?trigger='(.*?)' .*?input=\[(.*)\] options\((\d+)\): \|\| (.*)$/);
   if(m){const opts=m[7].split(' || ').map(s=>{const mm=s.match(/^(\S+) \[(\S+) (.*?)\] (.*)$/);return mm?{value:mm[1],kind:mm[2],key:mm[3],desc:mm[4]}:{value:'?',kind:'?',key:'',desc:s}});
     pend[m[2]+m[3]]={file:f.split('/').pop(),t:+m[1],who:m[2],n:+m[3],trigger:m[4],input:m[5].replace(/ ⏎ /g,'\n'),opts};continue;}
   m=line.match(/^t=[\d.]+ @(\S+) DECISION #(\d+) RESULT .*?latency=([\d.]+)s tokens=(\d+) cached=(\d+)/);
   if(m&&pend[m[1]+m[2]]){const r=pend[m[1]+m[2]];r.tokens=+m[4];r.cached=+m[5];r.latency=+m[3];recs.push(r);delete pend[m[1]+m[2]];}
   m=line.match(/^t=[\d.]+ @(\S+) DECISION #(\d+) ANSWER choice=(\S+) .*?probs=\[(.*?)\]/);
   if(m&&pend[m[1]+m[2]]){pend[m[1]+m[2]].answer=m[3];pend[m[1]+m[2]].probs=m[4];}
  }
 }
 for(const r of recs){const instr=INSTR.replace('{N}',r.who);r.tokIn=enc.encode(r.input).length;r.tokInstr=enc.encode(instr).length;r.tokDesc=r.opts.reduce((a,o)=>a+enc.encode(o.desc).length+enc.encode(o.value).length,0);r.tokSum=r.tokIn+r.tokInstr+r.tokDesc;r.chars=r.input.length+r.opts.reduce((a,o)=>a+o.desc.length,0);
  r.combat=/전투 상황/.test(r.input);r.brute=/돌진|Brute|버섯 야수|MushroomBrute/.test(r.input);r.nopts=r.opts.length;}
 fs.writeFileSync(out,JSON.stringify(recs));
 console.log('records',recs.length);
})();
