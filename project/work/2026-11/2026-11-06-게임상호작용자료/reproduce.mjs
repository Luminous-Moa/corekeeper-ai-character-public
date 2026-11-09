import {spawnSync} from 'node:child_process';
import fs from 'node:fs';
import crypto from 'node:crypto';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const manifest=path.join(root,'code-sources.json');
if(process.argv.includes('--pin-code')) {
 const found=new Map();
 function walk(x){if(!x||typeof x!=='object')return;if(Array.isArray(x)){x.forEach(walk);return;}if(typeof x.file==='string'&&x.sha256&&x.line){const h=hash(x.file);if(h!==x.sha256)throw Error(`Evidence hash mismatch: ${x.file}`);if(found.has(x.file)&&found.get(x.file)!==h)throw Error(`Conflicting hash: ${x.file}`);found.set(x.file,h);}Object.values(x).forEach(walk);}
 for(const group of ['equipment','world','creatures','items'])walk(JSON.parse(fs.readFileSync(path.join(root,`workers/${group}/results.json`),'utf8')));
 if(!found.size)throw Error('No code sources to pin');
 fs.writeFileSync(manifest,JSON.stringify({sources:[...found].sort(([a],[b])=>a.localeCompare(b)).map(([file,sha256])=>({file,sha256}))},null,2)+'\n');
 console.log('Code hashes pinned; review evidence again before use.');
 process.exit(0);
}
if(process.argv.length!==2)throw Error('Usage: node reproduce.mjs [--pin-code]');
const pins=JSON.parse(fs.readFileSync(manifest,'utf8')).sources;if(!Array.isArray(pins)||!pins.length)throw Error('Missing code source pins');
for(const s of pins)if(hash(s.file)!==s.sha256)throw Error(`Pinned code changed: ${s.file}`);
for(const script of ['collect.mjs',...['equipment','world','creatures','items'].map(g=>`workers/${g}/collect.mjs`),'integrate.mjs']){
 console.log(`\n> node ${script}`);
 const r=spawnSync(process.execPath,[path.join(root,script)],{cwd:root,stdio:'inherit'});
 if(r.error) throw r.error;
 if(r.status!==0) throw Error(`${script} failed: ${r.status ?? r.signal}`);
}
