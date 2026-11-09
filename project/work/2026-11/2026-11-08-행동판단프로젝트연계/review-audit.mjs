import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const folder=path.dirname(fileURLToPath(import.meta.url)),work=path.resolve(folder,'../..');
const project=path.join(work,'지식그래프'), game=path.join(work,'게임행동-그래프');
const evidence=path.join(folder,'review-evidence');fs.mkdirSync(evidence,{recursive:true});
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const graphPath=path.join(work,'2026-11/2026-11-05-지식그래프-실화면/D/graph.json');
const g=read(graphPath), canonical=read(path.join(game,'published/graph.json'));
const ns=new Map(g.nodes.map(n=>[n.id,n]));
const results={started:new Date().toISOString(),pins:[graphPath,path.join(folder,'link.mjs'),path.join(folder,'topics.mjs'),path.join(game,'published/graph.json')].map(p=>({path:p,sha256:hash(p)})),commands:[],topics:[]};
function run(name,args,parse=true){
 if(process.argv.includes('--reuse-sources')&&(name.startsWith('R.game-topic.')&&name.endsWith('-source')||name.startsWith('detail-')||['variation-target','variation-zero','remaining-gaps'].includes(name))&&fs.existsSync(path.join(evidence,name+'.stdout.txt'))){
  const stdout=fs.readFileSync(path.join(evidence,name+'.stdout.txt'),'utf8');results.commands.push({name,args,status:0,evidenceReuse:'앞선 독립 실조회 결과. 정본 원문 불변 및 현재 origin 재대조.',stdoutBytes:Buffer.byteLength(stdout)});return parse?JSON.parse(stdout):stdout;
 }
 const r=spawnSync(process.execPath,[path.join(project,'tools/product.mjs'),...args],{cwd:project,encoding:'utf8',maxBuffer:128*1024*1024});
 fs.writeFileSync(path.join(evidence,name+'.stdout.txt'),r.stdout||'');fs.writeFileSync(path.join(evidence,name+'.stderr.txt'),r.stderr||'');
 results.commands.push({name,args,status:r.status,stderr:r.stderr,stdoutBytes:Buffer.byteLength(r.stdout||'')});
 assert.equal(r.status,0,name+': '+r.stderr);return parse?JSON.parse(r.stdout):r.stdout;
}
const foreign=g.nodes.filter(n=>n.externalBehavior), mismatches=[];
for(const n of foreign){const c=canonical.nodes[n.externalBehavior.id];if(!c||n.id!=='game:'+c.id||JSON.stringify(n.externalBehavior.origin)!==JSON.stringify(c.origin??null)||n.externalBehavior.sourceKey!==(c.sourceKey??null)||n.externalBehavior.variation!==(c.variation??null)||n.runtimeTested!==false)mismatches.push(n.id);}
const primaryKinds=new Set(['verb','claim','action_completion','cooking_pair','source','conditional_relation','condition_consumer','configuration_consumer','configuration_rule','action_completion_rule','gap_assessment','boundary']);
results.population={canonicalNodes:Object.keys(canonical.nodes).length,foreignNodes:foreign.length,uniqueForeign:new Set(foreign.map(n=>n.id)).size,mismatches,missingSelected:Object.values(canonical.nodes).filter(n=>primaryKinds.has(n.kind)&&!ns.has('game:'+n.id)).map(n=>n.id),unmatched:g.behaviorLink.topics.flatMap(t=>t.unmatchedLabels),unclassified:g.behaviorLink.unclassified};
assert.deepEqual(mismatches,[]);assert.deepEqual(results.population.missingSelected,[]);
for(const t of g.behaviorLink.topics){
 const b=run(t.id+'-behavior',['behavior',t.id]);assert.equal(b.topics.length,1);assert.equal(b.topics[0].id,t.id);
 const targets=b.topics[0].targets.map(id=>ns.get(id));assert(targets.length);
 const candidates=targets.filter(n=>n.externalBehavior.origin);
 const sample=candidates.find(n=>n.externalBehavior.kind==='action_completion')||candidates.find(n=>n.externalBehavior.kind==='claim')||candidates.find(n=>n.externalBehavior.kind==='cooking_pair');
 assert(sample,'source representative '+t.id);
 const source=run(t.id+'-source',['source',sample.id]);assert.deepEqual(source.origin,sample.externalBehavior.origin);assert(source.value!==undefined);
 for(const anchor of [...t.planning,...t.code])assert(g.edges.some(e=>e.from===anchor&&e.to===t.id&&e.type==='reference'&&e.runtimeTrace===false));
 results.topics.push({id:t.id,title:t.title,planning:t.planning,code:t.code,targets:targets.length,sample:sample.id,origin:source.origin,valueKeys:Object.keys(source.value||{}),sourceStatus:0});
 console.log('PASS '+t.id+' '+sample.id);
}
for(const id of ['door','completion:B:melee-pick-2-0:cost-followup-v1','cooking:0','D102:cases:case']){
 const s=run('detail-'+id.replaceAll(/[^\w-]/g,'_'),['show','game:'+id]);assert.equal(s.node.id,id);results.commands.at(-1).fieldCount=s.fields?.length;results.commands.at(-1).ruleCount=s.rules?.length;
}
const variantSources=Object.values(canonical.nodes).filter(n=>n.kind==='source'&&n.objectId===4776);assert(new Set(variantSources.map(n=>n.sourceKey)).size>1);assert(new Set(variantSources.map(n=>n.variation)).size>1);
results.variationSources=variantSources.map(n=>({id:n.id,sourceKey:n.sourceKey,variation:n.variation}));
run('variation-target',['game','target','4776']);
run('variation-zero',['game','target','4776','--variation','0']);
run('remaining-gaps',['game','gaps','4600']);
run('legacy-show',['legacy','show','F.action-select']);
run('existing-source',['source','P.original-request'],false);
run('existing-feature',['feature','slash:ai'],false);
const check=run('final-check',['check']);results.check={ok:check.ok,errors:check.structuralErrors,stale:check.stale};
const label=run('label-without-origin',['show','game:verb:cooking']);assert(!label.node.origin);
const labelClaim=label.incoming.find(([relation,id])=>relation==='uses_label'&&canonical.nodes[id]?.origin);assert(labelClaim);
const linkedSource=run('label-to-claim-source',['game','source',labelClaim[1]]);assert.deepEqual(linkedSource.origin,canonical.nodes[labelClaim[1]].origin);
results.labelPath={label:'game:verb:cooking',labelHasOrigin:false,incomingRelation:labelClaim[0],claim:labelClaim[1],sourceOrigin:linkedSource.origin};
results.ended=new Date().toISOString();results.pinChanges=results.pins.filter(p=>hash(p.path)!==p.sha256);assert.deepEqual(results.pinChanges,[]);
fs.writeFileSync(path.join(folder,'review-evidence.json'),JSON.stringify(results,null,2)+'\n');
console.log(JSON.stringify({topics:results.topics.length,commands:results.commands.length,population:results.population,check:results.check},null,2));
