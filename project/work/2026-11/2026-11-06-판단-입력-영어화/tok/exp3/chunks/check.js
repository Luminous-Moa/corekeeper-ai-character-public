// node check.js chunk.json out.json -> OK or problems (slots exact; Korean output; reports unchanged entries)
const fs=require('fs');const c=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));const o=JSON.parse(fs.readFileSync(process.argv[3],'utf8'));
const bad=[],same=[];for(const x of c){const e=o[String(x.i)];if(typeof e!='string'){bad.push(x.i+': missing');continue;}
 const used=(e.match(/\{(\d+)\}/g)||[]).map(s=>+s.slice(1,-1)).sort((a,b)=>a-b);const want=[...Array(x.slots).keys()];
 if(JSON.stringify(used)!=JSON.stringify(want))bad.push(x.i+': slots '+JSON.stringify(used)+' want '+JSON.stringify(want));
 if(/[가-힣]/.test(x.key)&&!/[가-힣]/.test(e))bad.push(x.i+': no Korean in output');
 let n=0;const plain=x.key.replace(/\{\}/g,()=>'{'+(n++)+'}');if(plain==e)same.push(x.i);}
for(const k of Object.keys(o))if(!c.find(x=>String(x.i)==k))bad.push(k+': not in chunk');
console.log(bad.length?bad.join('\n'):'OK');console.log('unchanged (allowed only if no natural rewording exists):',same.length?same.join(','):'none');
