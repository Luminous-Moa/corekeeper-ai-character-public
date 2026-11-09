import fs from 'node:fs';
import assert from 'node:assert/strict';
import {validate,stale,readSources} from '../D/lib.mjs';
const g=JSON.parse(fs.readFileSync('D/graph.json','utf8'));
const adjacency=new Map();for(const e of g.edges){if(e.type!=='기능→코드'&&!(e.type==='reference'&&['call','callback','framework-invoked'].includes(e.kind)))continue;if(!adjacency.has(e.from))adjacency.set(e.from,[]);adjacency.get(e.from).push(e.to);}
function findPath(start,partial){const queue=[[start]],seen=new Set([start]);while(queue.length){const p=queue.shift(),id=p.at(-1);if(id.includes(partial))return p;for(const to of adjacency.get(id)||[])if(!seen.has(to)){seen.add(to);queue.push([...p,to]);}}return null;}
const qs=[['slash:ai','AIPlayerSpikeMod.StatusText()'],['slash:ai','AIWorldSettings.Members(string)'],['slash:ai','AIGoals.HandleGoalCommand('],['hook:AIPlayerSpikeMod.Update','AIPlanner.CheckReason('],['hook:AIPlayerSpikeMod.Update','AIGoals.SetGoal('],['ai-bridge:POST /v1/plan','memgraph.mjs#runQueries@'],['ai-bridge:POST /v1/plan','memgraph.mjs#runQuery@']];
const paths=qs.map(([from,to])=>({from,to,path:findPath(from,to)}));assert(paths.every(x=>x.path));
const negative={duplicate:validate({...g,nodes:[...g.nodes,g.nodes[0]]}),broken:validate({...g,edges:[...g.edges,{from:g.nodes[0].id,to:'review:missing'}]}),hash:stale({meta:{sources:[{...g.meta.sources[0],sha256:'invalid'}]}}),missing:stale({meta:{sources:[{path:'Review/nonexistent-file',sha256:'invalid'}]}})};
assert(Object.values(negative).every(x=>x.length));
const policySources=g.nodes.filter(n=>n.type==='정책').map(n=>({id:n.id,sources:readSources(n,g),rule:n.rule}));assert(policySources.every(n=>n.sources.some(s=>s.text.includes('### '+n.id+' ')&&s.text.includes(n.rule))));
const out={paths,negative,policySources:policySources.length};fs.writeFileSync('Review/traversal-results.json',JSON.stringify(out,null,2));console.log(JSON.stringify(out,null,2));
