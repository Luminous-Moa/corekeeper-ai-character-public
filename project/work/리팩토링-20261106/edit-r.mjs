import fs from 'node:fs';
import {randomUUID} from 'node:crypto';
const repo='C:/Dev/CoreKeeperModSDK-refactor-20261106/',root=repo+'Assets/AIPlayerSpike/Scripts/';
const read=p=>fs.readFileSync(root+p,'utf8').replaceAll('\r\n','\n');
function write(p,s){fs.writeFileSync(root+p,s);if(!fs.existsSync(root+p+'.meta'))fs.writeFileSync(root+p+'.meta','fileFormatVersion: 2\nguid: '+randomUUID().replaceAll('-','')+'\n');}
function rep(s,a,b){if(!s.includes(a))throw Error('missing '+a);return s.replace(a,b);}
write('Core/AIApproachSearch.cs',`using System.Collections.Generic;

public delegate TPlan TryApproach<TTile, TPlan>(TTile tile, out string why) where TPlan : class;

public static class AIApproachSearch
{
    public static TPlan FirstOfThree<TTile, TPlan>(IList<TTile> stands, TryApproach<TTile, TPlan> attempt, out TTile stand, out string why) where TPlan : class
    {
        TPlan plan = null; stand = default; why = "설 수 있는 아는 칸이 없음";
        for (int i = 0; i < stands.Count && i < 3 && plan == null; i++)
        {
            plan = attempt(stands[i], out why);
            stand = stands[i];
        }
        return plan;
    }
}
`);
write('AIApproach.cs',`using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

public static class AIApproach
{
    public static TunnelPlan FirstCached(World server, Entity player, IList<int2> stands, out int2 stand, out string why) =>
        AIApproachSearch.FirstOfThree<int2, TunnelPlan>(stands, (int2 tile, out string note) => AIBrain.PlanC(server, player, tile, out note), out stand, out why);

    public static TunnelPlan FirstImmediate(World server, Entity player, IList<int2> stands, out int2 stand, out string why) =>
        AIApproachSearch.FirstOfThree<int2, TunnelPlan>(stands, (int2 tile, out string note) => AITunnel.Plan(server, player, tile, out note), out stand, out why);

    public static AICandidate WalkCandidate(int2 stand, string what, bool progress, string description) =>
        new AICandidate { kind = "dig", key = $"dig:{stand.x},{stand.y}", tile = stand, digWall = false, targetLabel = what, progress = progress, desc = description };
}
`);
let loops=0,candidates=0;
for(const p of ['AIBase.cs','AICook.cs','AIBrain.Prep.cs','AIFarm.cs']){
 let s=read(p);
 const re=/TunnelPlan (\w+) = null; int2 (\w+) = default; string (\w+) = "설 수 있는 아는 칸이 없음";\n(\s*)(?:\/\/[^\n]*\n\s*)?for \(int (\w+) = 0; \5 < stands.Count && \5 < 3 && \1 == null; \5\+\+\) \{ \1 = (AIBrain.PlanC|PlanC|AITunnel.Plan)\(server, player, stands\[\5\], out \3\); \2 = stands\[\5\]; \}/g;
 s=s.replace(re,(all,plan,stand,why,indent,i,fn)=>{loops++;return `var ${plan} = AIApproach.${fn==='AITunnel.Plan'?'FirstImmediate':'FirstCached'}(server, player, stands, out int2 ${stand}, out string ${why});`;});
 const cr=/new AICandidate \{ kind = "dig", key = [^\n]+?, tile = (\w+), digWall = false, targetLabel = (.+), progress = ([^,\n]+),\n(\s*)desc = (.+) \}/g;
 s=s.replace(cr,(all,stand,what,progress,indent,desc)=>{candidates++;return `AIApproach.WalkCandidate(${stand}, ${what}, ${progress},\n${indent}${desc})`;});
 if(p==='AIBase.cs'){
  const old=/res.Add\(new AICandidate \{ kind = "dig", key = k, tile = st, digWall = false, targetLabel = "기지", progress = true,\n\s*digEdgeOk = (.+),\n\s*desc = (.+) \}\);/;
  if(!old.test(s))throw Error('home');
  s=s.replace(old,(all,edge,desc)=>{candidates++;return `var homeEdgeOk = ${edge};\n                        var homeWalk = AIApproach.WalkCandidate(st, "기지", true,\n                            ${desc});\n                        homeWalk.digEdgeOk = homeEdgeOk;\n                        res.Add(homeWalk);`;});
 }
 write(p,s);
}
let places=read('AIPlaces.cs');
const pg=/res.Add\(new AICandidate\n\s*\{\n\s*kind = "dig", key = k, tile = st, digWall = false, targetLabel = t.Label, progress = true,\n\s*digEdgeOk = (.+),\n\s*desc = (.+),\n\s*\}\);/;
if(!pg.test(places))throw Error('place Go');
places=places.replace(pg,(all,edge,desc)=>{candidates++;return `var walkEdgeOk = ${edge};\n            var walk = AIApproach.WalkCandidate(st, t.Label, true,\n                ${desc});\n            walk.digEdgeOk = walkEdgeOk;\n            res.Add(walk);`;});
write('AIPlaces.cs',places);
console.log({loops,candidates});if(loops!==5||candidates!==8)throw Error('coverage');
