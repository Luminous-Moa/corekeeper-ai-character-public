import fs from 'fs'; import readline from 'readline';
const rl = readline.createInterface({input: fs.createReadStream(process.argv[2],{encoding:'utf8'})});
const send = new Map(); const rows=[];
for await (const l of rl){
  let m = l.match(/@(\w+) DECISION #(\d+) SEND .*?candidates=(\d+) inputChars=(\d+)/);
  if (m) { send.set(m[1]+m[2], {c:+m[3], ch:+m[4], combat: /set=close-fight|전투 중/.test(l)}); continue; }
  m = l.match(/@(\w+) DECISION #(\d+) RESULT .*?latency=([\d.]+)s tokens=(\d+)/);
  if (m) { const s = send.get(m[1]+m[2]); if (s) rows.push({...s, lat:+m[3], tok:+m[4]}); }
}
const pct=(a,q)=>{const s=[...a].sort((x,y)=>x-y);const i=(s.length-1)*q;const lo=Math.floor(i),hi=Math.ceil(i);return s[lo]+(s[hi]-s[lo])*(i-lo);};
const bin = (r)=> r.c<=5?'2-5':r.c<=8?'6-8':r.c<=11?'9-11':'12-15';
const g={}; for(const r of rows){(g[bin(r)]=g[bin(r)]||[]).push(r);}
for (const k of Object.keys(g).sort()) { const a=g[k]; console.log(k,'n',a.length,'tok mean',(a.reduce((s,r)=>s+r.tok,0)/a.length).toFixed(0),'p50',pct(a.map(r=>r.lat),.5).toFixed(2),'p95',pct(a.map(r=>r.lat),.95).toFixed(2)); }
const tb=(r)=>r.tok<1500?'<1500':r.tok<2000?'1500-1999':r.tok<2500?'2000-2499':r.tok<3000?'2500-2999':'>=3000';
const h={}; for(const r of rows){(h[tb(r)]=h[tb(r)]||[]).push(r);}
for (const k of Object.keys(h).sort()) { const a=h[k]; console.log('tok',k,'n',a.length,'p50',pct(a.map(r=>r.lat),.5).toFixed(2),'p95',pct(a.map(r=>r.lat),.95).toFixed(2)); }
console.log('max candidates', Math.max(...rows.map(r=>r.c)), 'n', rows.length);
