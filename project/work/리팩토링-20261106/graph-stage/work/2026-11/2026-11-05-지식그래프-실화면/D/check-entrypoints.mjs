import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {abs,live,write} from './live-lib.mjs';
const wrapper=abs('../../지식그래프/tools/product.mjs'),build=abs('../../지식그래프/tools/product-build.mjs');
const cases=[['overview'],['check'],['user','CKAI-ACT-01'],['source','CKAI-02#2-7'],['feature','slash:ai'],['legacy','show','F.action-select']];
const results=[];
for(const args of cases){const r=spawnSync(process.execPath,[wrapper,...args],{encoding:'utf8',maxBuffer:16*1024*1024});assert.equal(r.status,0,args.join(' ')+' '+r.stderr);assert.equal(r.stderr,'');results.push({args,exitCode:r.status,stdout:r.stdout});}
const check=JSON.parse(results.find(r=>r.args[0]==='check').stdout);assert(check.ok);
const user=JSON.parse(results.find(r=>r.args[0]==='user').stdout);assert(user.features.some(r=>r.feature.id==='slash:ai'));
assert(user.elements.some(r=>r.element.id==='CKAI-02#2-7'&&r.featureRelations.some(e=>e.traversal.startsWith('reverse'))));
const r=spawnSync(process.execPath,[build,'--check'],{encoding:'utf8'});assert.equal(r.status,0,r.stderr);results.push({args:['product-build','--check'],exitCode:r.status,stdout:r.stdout});
write(live('D/entrypoint-checks.json'),{passed:true,results});
console.log(JSON.stringify({passed:true,commands:results.map(r=>r.args.join(' ')),check:{ok:check.ok,structuralErrors:check.structuralErrors.length,stale:check.stale.length,D4:check.validation.D4},user:{elements:user.elements.length,features:user.features.length,gaps:user.gaps.length}},null,2));
