// KO' rewording of picked.json -> kop.json, hits.json (dict hits per key)
const fs=require('fs');const tr=require('./tr3');const P=require('./picked.json');
const out=P.map(x=>{const e=tr.translateRequest(x);return {id:x.id,input:e.input,opts:e.opts}});
fs.writeFileSync(__dirname+'/kop.json',JSON.stringify(out));fs.writeFileSync(__dirname+'/hits.json',JSON.stringify(tr.HITS));
console.log('dict',Object.keys(tr.DICT).length,'missing',Object.keys(tr.MISS).length,'keys hit',Object.keys(tr.HITS).length);
