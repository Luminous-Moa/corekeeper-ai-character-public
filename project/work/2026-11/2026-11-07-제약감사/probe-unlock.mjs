import {loadGraph,openView} from 'file:///C:/Dev/CoreKeeperModSDK/Tools/knowledge/kg.mjs';
import fs from 'node:fs';
const g=loadGraph('C:/Dev/CoreKeeperModSDK/Tools/knowledge/data');
const [iron]=Object.entries(g.nodes).find(([k,n])=>k.startsWith('o:')&&n.name==='IronOre');
const id=Number(iron.slice(2));
const cases=[['미관측',{}],['철을 봄',{seenId:[id]}],['철을 보유',{heldId:[id]}]];
const results=cases.map(([name,k])=>({name,ironKnowledgeOpen:openView(g,k).open.has(iron)}));
const recipe=g.edges.find(e=>e[0]==='ingredient'&&e[1]===iron&&g.edges.some(z=>z[0]==='crafted_at'&&z[1]===e[2]));
if(recipe){const station=g.edges.find(e=>e[0]==='crafted_at'&&e[1]===recipe[2])[2];const v=openView(g,{seenId:[Number(station.slice(2))]});results.push({name:'제작대를 봄',station:g.nodes[station]?.name,recipe:g.nodes[recipe[2]]?.name,ironKnowledgeOpen:v.open.has(iron),recipeOpen:v.recipes.has(recipe[2])});}
const planner=fs.readFileSync('C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanner.cs','utf8');
const gate=planner.split('\n').find(l=>l.includes('id != ObjectID.WallDirtBlock')&&l.includes('_gatherFood.Contains(id)'));
console.log(JSON.stringify({iron,id,results,independentGatherGate:gate.trim()},null,2));
