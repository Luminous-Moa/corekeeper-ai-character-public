// node check.js -> fact check: ASCII tokens / numbers multiset equal between Korean and English (input and each choice), choice values/order equal, no Hangul
const fs=require('fs'),path=require('path');
const KO=JSON.parse(fs.readFileSync(path.join(__dirname,'..','10k','judge-cases.json'),'utf8'));
const EN=JSON.parse(fs.readFileSync(path.join(__dirname,'tr','en-pre.json'),'utf8'));
const TOK=/[-+]?[A-Za-z0-9_][A-Za-z0-9_:.,\-+#]*[A-Za-z0-9_%]|[-+]?[A-Za-z0-9_]/g;
const DIRW=new Set(['east','west','south','north','E','W','S','N']);
// tokens that only exist in English (words) are ignored: compare the Korean-side ASCII tokens, which must all survive in English
const NUM=/\d+(?:\.\d+)?/g;const bag=s=>{const m={};for(const t of (s.match(NUM)||[]))m['#'+t]=(m['#'+t]||0)+1;for(const t of (s.match(TOK)||[]))if(/[A-Za-z]/.test(t)&&!/[가-힣]/.test(t)){for(const p of t.split(/[^A-Za-z_]+/).filter(x=>x.length>1))m['@'+p]=(m['@'+p]||0)+1;}return m};
let bad=[],nTok=0;
for(const k of KO){const e=EN.find(x=>x.case===k.case);
 if(/[가-힣]/.test(e.input+e.choices.map(c=>c.description).join('')))bad.push(k.case+': Hangul left');
 if(JSON.stringify(k.choices.map(c=>c.value))!==JSON.stringify(e.choices.map(c=>c.value)))bad.push(k.case+': choice values/order differ');
 const pairs=[['input',k.input,e.input],...k.choices.map((c,i)=>[c.value,c.description,e.choices[i].description])];
 for(const [nm,ko,en] of pairs){const a=bag(ko),b=bag(en);for(const w of Object.keys(b))if(w[0]=='@')b[w]=(en.match(new RegExp(w.slice(1),'g'))||[]).length;for(const w of Object.keys(a))if(w[0]=='@'&&!(w in b))b[w]=(en.match(new RegExp(w.slice(1),'g'))||[]).length;
  for(const [t,n] of Object.entries(a)){nTok+=n;if((b[t]||0)<n)bad.push(`${k.case} ${nm}: token '${t}' ko ${n} en ${b[t]||0}`);}}
}
console.log('korean-side ascii tokens checked',nTok);console.log(bad.length?bad.join('\n'):'OK');
