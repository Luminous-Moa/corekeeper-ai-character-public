import fs from 'node:fs';
const root=new URL('../../',import.meta.url);const src=fs.readFileSync(new URL('Assets/AIPlayerSpike/Scripts/AIPlaces.cs',root),'utf8');
function block(signature,source=src){const a=source.indexOf(signature);if(a<0)throw Error('missing '+signature);const line=source.slice(a,source.indexOf('\n',a));if(line.includes('=>'))return line;const b=source.indexOf('\n    }',a);if(b<0)throw Error('end '+signature);return source.slice(a,b+6);}
let code='using System;using System.Collections.Generic;using Unity.Entities;using Unity.Mathematics;using UnityEngine;using PugTilemap;\npublic static class AIPlaceGoal {\n';
for(const sig of ['    private sealed class State','    private static bool IsSkipped','    private static List<int2> ShapeList','    public static void NotePlaced','    public static void NoteSkip','    public static bool GoalDone','    private static bool Holds','    private static List<int2> Remaining','    public static bool Arrived','    private static bool InArea'])code+=block(sig)+'\n';
code+=`private static State S=new State();
public static void Begin(PlaceRef r){S=new State{Ref=r};}
public static List<int2> Batch()=>ShapeList(S.Ref);
public static int Receipts=>S.Placed.Count;
public static List<int2> Inspect(World w){Remaining(w,default,ObjectID.Wood,ShapeList(S.Ref));return ShapeList(S.Ref);}
private static PlaceTarget Target(World w,Entity p,out string err){err=null;return new PlaceTarget{Ref=S.Ref,Shape=ShapeList(S.Ref),Label=AIPlaces.Format(S.Ref)};}
public static bool Ready(World w,PlaceTarget t)=>InArea(w,default,t);
private static bool CountsIn(PlaceTarget t,ObjectID i,int2 at)=>true;
private static List<(int x,int y,string why)> SkipList(State s)=>s.Skipped.ConvertAll(k=>(k.at.x,k.at.y,k.why));
public static void NoteStop(string why){S.LastWhy=why;}
}\n`;
code+=src.slice(src.indexOf('public class PlaceTilesSkill : AISkill'));
const base=fs.readFileSync(new URL('Assets/AIPlayerSpike/Scripts/AIBase.cs',root),'utf8');
code+='\npublic static class BaseArrivalProbe { public const float HomeArrive=AIPlaces.StandArrive; private static float BaseDist(float3 p)=>math.distance(p.xz,new float2(0,0)); private static bool BaseStand(World w,Entity p,out int2 s){s=w.Stand;return w.HasStand;}\n'+block('    public static bool ArrivedAtBase(World server, Entity player)',base)+'\n'+block('    public static bool ArrivedAtBase(World server, Entity player, out float dist)',base)+'\n}';
function balanced(source,signature){const a=source.indexOf(signature);if(a<0)throw Error(signature);const b=source.indexOf('{',a);let d=1,e=b+1;for(;e<source.length&&d;e++){if(source[e]==='{')d++;if(source[e]==='}')d--;}return source.slice(a,e);}
const brain=fs.readFileSync(new URL('Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs',root),'utf8');
code+='\npublic partial class FoodProbe {\n'+balanced(brain,'        public void Food()')+'\n}';
code+='\npublic static partial class AIFood {\n'+balanced(base,'    public static List<AICandidate> Candidates(World server, Entity player, AIObservation o, string curKey, List<AICandidate> combat)')+'\n}';

const edge=brain.slice(brain.indexOf('            guardBlocks = new List<string>();'),brain.indexOf('            // live-run fix 3 / review F1:'));
const followStart=brain.indexOf('            foreach (var p in mates)',brain.indexOf('        public IEnumerable<int> FollowSteps()'));
const follow=brain.slice(followStart,brain.indexOf('            yield return 0;',followStart));   // the free-time host walk after it was removed (0c8a3e8)
const plants=brain.slice(brain.indexOf('                        if (fs.plant && AIFarm.ForeignCrop'),brain.indexOf('                        bool forGoal =',brain.indexOf('                        if (fs.plant && AIFarm.ForeignCrop')));
const progress=brain.slice(brain.indexOf('                        bool forGoal ='),brain.indexOf('                        bool foodHunger ='));
code+='\npublic partial class FoodProbe { public void Edge(){'+edge+'} public void FollowVisible(){'+follow+'}\n'+balanced(brain,'        public IEnumerable<int> RememberedOresSteps()').replace('public IEnumerable<int> RememberedOresSteps()', 'public void RememberedOres()').replace(/\s*foreach \(var step in PreparePlan[^\n]+yield return step;/g, '').replace(/yield return 0;/g, '')+'\npublic bool PlantEntry(FoodPlant fs,bool own,bool foreign){AIFarm.Own=own;AIFarm.Foreign=foreign;var em0=new PlantEcs();var e=default(Entity);foreach(var unused in new[]{0}){'+plants+'return true;}return false;}\npublic bool PlantProgress(FoodPlant fs){bool learned=true,woody=false;var id=ObjectID.Wood;'+progress+'return forGoal||foodGoal;}\n}';
code+='\npublic static partial class AIBase {\n'+balanced(base,'    public static List<AICandidate> FoodTakeCandidates(')+'\n'+balanced(base,'    private static bool TakeOrGo(')+'\n}';
const memory=fs.readFileSync(new URL('Assets/AIPlayerSpike/Scripts/AIMemory.cs',root),'utf8');
code+='\npublic static partial class AIMemory {\n'+balanced(memory,'    public static List<(int2 p, Ore o)> RememberedOres(')+'\n}';
fs.writeFileSync(new URL('Generated.cs',import.meta.url),code);
