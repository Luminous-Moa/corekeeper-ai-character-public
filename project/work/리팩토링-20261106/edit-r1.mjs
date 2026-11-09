import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const root = 'C:/Dev/CoreKeeperModSDK-refactor-20261106';
const dir = path.join(root, 'Assets/AIPlayerSpike/Scripts');
const read = n => fs.readFileSync(path.join(dir,n),'utf8').replace(/\r\n/g,'\n');
const write = (n,s) => fs.writeFileSync(path.join(dir,n),s,'utf8');
function rep(s,a,b) { if(!s.includes(a)) throw Error('missing '+a.slice(0,100)); return s.replace(a,b); }
let s=read('Core/AICandidateAssembler.cs');
s=s.replace(/^public enum GoalKind.*\n/m,''); write('Core/AICandidateAssembler.cs',s);
s=read('AIDecisions.cs');
const text=s.slice(s.indexOf('    public string Text =>'),s.indexOf('    // a goal someone chose'));
let expr=text.slice(text.indexOf('=>')+2).trim();
for(const name of ['kind','where','count','itemName','item']) expr=expr.replace(new RegExp('\\b'+name+'\\b','g'),'g.'+name);
write('AIGoalText.cs','// Goal display only; the execution contract lives with each feature.\npublic static class AIGoalText\n{\n    public static string Format(AIGoal g) => '+expr+'\n}\n');
s=s.replace(text,'    public string Text => AIGoalText.Format(this);\n'); write('AIDecisions.cs',s);
s=read('AIExperience.cs');
const ks=s.indexOf('    public static string KeyOf('), ke=s.indexOf('    private static bool Alive',ks);
s=s.slice(0,ks)+'    public static string KeyOf(AIGoal g)\n    {\n        if (g == null) return "";\n        return AIGoalKinds.ExperienceKey(g.kind, g.item.ToString(), g.item != ObjectID.None);\n    }\n\n'+s.slice(ke);
write('AIExperience.cs',s);
s=read('AIGoals.cs');
for(const [kind,cond] of [
['Craft','a == "만들기" || a == "craft"'],['Goto','a == "가기" || a == "goto"'],['Place','a == "놓기" || a == "place"'],
['Explore','a == "탐험" || a == "explore"'],['Follow','a == "따라와" || a == "follow"'],['Home','a == "기지로" || a == "home"'],
['Farm','a == "농사" || a == "밭" || a == "farm"'],['Cook','a == "요리" || a == "cook"'],['Store','a == "보관" || a == "store"']])
 s=rep(s,cond,'AIGoalKinds.IsHostWord(GoalKind.'+kind+', a)');
s=rep(s,'Goal.kind == GoalKind.Follow && GoalSource','AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.FollowTimeout && GoalSource');
const doneStart=s.indexOf('        // goto / place goals');
const doneEnd=s.indexOf('        // goal completion (observed inventory)',doneStart);
let done=s.slice(doneStart,doneEnd);
done=done.slice(done.indexOf('        if ((Goal.kind == GoalKind.Store'));
done=done.replace('(Goal.kind == GoalKind.Store || Goal.kind == GoalKind.Home || Goal.kind == GoalKind.Farm || Goal.kind == GoalKind.Cook)','(AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Feature)');
done=rep(done,'Goal.kind == GoalKind.Farm ? AIFarm.GoalDone(server, player, out doneB) : Goal.kind == GoalKind.Cook ? AICook.GoalDone(server, player, out doneB) : AIBase.GoalDone(server, player, out doneB)','AIGoalFeatures.TryDone(server, player, Goal.kind, out doneB)');
s=s.slice(0,doneStart)+'        // Feature completion keeps the original observation cadence and end order.\n'+done+s.slice(doneEnd);
s=rep(s,'(Goal.kind == GoalKind.Gather || Goal.kind == GoalKind.Craft) && (now - _lastGoalCheck','(AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Inventory) && (now - _lastGoalCheck');
s=rep(s,'// The stall checks that need the candidate build (AIBrain.Send) stay there for now.','// CheckAfterBuild receives the completed build facts; request scheduling stays in AIBrain.Send.');
write('AIGoals.cs',s);
s=read('AIPlanner.cs');
const mapStart=s.indexOf('        GoalKind kind = d == "gather"'),mapEnd=s.indexOf('        ObjectID id',mapStart);
s=s.slice(0,mapStart)+'        if (!AIGoalKinds.TryWire(d, out GoalKind kind)) kind = GoalKind.Follow;\n'+s.slice(mapEnd);
const formStart=s.indexOf('    private static string StepForm('), formEnd=s.indexOf('    // TEST ONLY: one GET',formStart);
let checks=s.slice(formStart,formEnd);
for(const kind of ['Gather','Craft','Store','Home','Farm','Cook','Explore','Follow','Goto','Place'])
 checks=checks.replaceAll('d == "'+kind.toLowerCase()+'"','d == AIGoalKinds.Of(GoalKind.'+kind+').WireName');
checks=rep(checks,'int hi = d == AIGoalKinds.Of(GoalKind.Gather).WireName ? 30 : 10;','int hi = AIGoalKinds.Of(d == AIGoalKinds.Of(GoalKind.Gather).WireName ? GoalKind.Gather : GoalKind.Craft).PlannerMaxCount;');
checks=checks.replace('s.Count > AIPlaces.MaxShapeTiles','s.Count > AIGoalKinds.Of(GoalKind.Place).PlannerMaxCount')
 .replace('count > 30','count > AIGoalKinds.Of(GoalKind.Gather).PlannerMaxCount')
 .replace('count > 10','count > AIGoalKinds.Of(GoalKind.Craft).PlannerMaxCount');
s=s.slice(0,formStart)+checks+s.slice(formEnd); write('AIPlanner.cs',s);
s=read('AIBrain.Candidates.cs');
const ca=s.indexOf('            // base and chests (생활-기지)'), cb=s.indexOf('            // 코어-안전지대 단계 2:',ca);
let candidates=s.slice(ca,cb).replace(/^    /gm,'').replaceAll('lCraft.AddRange','dest.AddRange');
candidates=candidates.replaceAll('Goal.kind','AIGoals.Goal.kind').replace('free ? AIBase','AIGoals.Goal.kind == GoalKind.Free ? AIBase');
s=s.slice(0,ca)+'            AIGoalFeatures.AddCandidates(server, player, o, curKey, lCraft);\n'+s.slice(cb); write('AIBrain.Candidates.cs',s);
let loop=read('AIBrain.Loop.cs');
loop=rep(loop,'(Goal.kind == GoalKind.Gather || Goal.kind == GoalKind.Craft || Goal.kind == GoalKind.Store || Goal.kind == GoalKind.Home || Goal.kind == GoalKind.Farm || Goal.kind == GoalKind.Cook || Goal.kind == GoalKind.Goto || Goal.kind == GoalKind.Place)','(AIGoalKinds.Of(Goal.kind)?.NeedsProgress == true)');
const st=loop.indexOf('        if ((Goal.kind == GoalKind.Store',loop.indexOf('    private static void Send('));
const en=loop.indexOf('        if (cands.Count < 2)',st);
let stalled=loop.slice(st,en);
const whyStart=stalled.indexOf('            string why = Goal.kind == GoalKind.Goto'),whyEnd=stalled.indexOf('            AIPlayerSpikeMod.Log',whyStart);
const stallExpr=stalled.slice(whyStart,whyEnd).replace('            string why = ','        return ').replaceAll('Goal.kind','kind');
stalled=stalled.slice(0,whyStart)+'            string why = AIGoalFeatures.StallNote(server, player, Goal.kind);\n'+stalled.slice(whyEnd);
stalled=stalled.replace('(Goal.kind == GoalKind.Store || Goal.kind == GoalKind.Home || Goal.kind == GoalKind.Farm || Goal.kind == GoalKind.Cook || Goal.kind == GoalKind.Goto || Goal.kind == GoalKind.Place)','(AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Feature)')
 .replace('(Goal.kind == GoalKind.Gather || Goal.kind == GoalKind.Craft)','(AIGoalKinds.Of(Goal.kind)?.Completion == GoalCompletion.Inventory)');
for(const [a,b] of [['cands.Exists(c => c.progress)','facts.HasProgress'],['_inCombat','facts.InCombat'],['S._fPending','facts.FrontierPending'],['_exploreOptions','facts.ExploreOptions'],['_exploreNoPath','facts.ExploreNoPath'],['_exploreFrontierNote','facts.ExploreFrontierNote'],['_skippedNote','facts.SkippedNote'],['_prepNote','facts.PrepNote'],['S._gatherFacts','facts.GatherFacts'],['_blockNote','facts.BlockNote'],['FailMemorySeconds','facts.FailMemorySeconds']]) stalled=stalled.replaceAll(a,b);
stalled=stalled.replaceAll('            return;','            return true;');
const factsCall=[
'        if (AIGoals.CheckAfterBuild(server, player, now, new GoalBuildFacts',
'        {',
'            HasProgress = cands.Exists(c => c.progress), InCombat = _inCombat, FrontierPending = S._fPending,',
'            ExploreOptions = _exploreOptions, ExploreNoPath = _exploreNoPath, ExploreFrontierNote = _exploreFrontierNote,',
'            SkippedNote = _skippedNote, PrepNote = _prepNote, GatherFacts = S._gatherFacts,',
'            BlockNote = _blockNote, FailMemorySeconds = FailMemorySeconds',
'        })) return;',
''].join('\n');
loop=loop.slice(0,st)+factsCall+loop.slice(en);write('AIBrain.Loop.cs',loop);
s=read('AIGoals.cs');const insert=s.indexOf('    // The common end sequence');
const actual=insert>=0?insert:s.lastIndexOf('\n}');
s=s.slice(0,actual)+'    // Build facts are consumed now, not retained as a cache.\n    public static bool CheckAfterBuild(World server, Entity player, float now, GoalBuildFacts facts)\n    {\n'+stalled+'        return false;\n    }\n\n'+s.slice(actual);
s+='\npublic struct GoalBuildFacts\n{\n    public bool HasProgress, InCombat, FrontierPending;\n    public int ExploreOptions, ExploreNoPath;\n    public string ExploreFrontierNote, SkippedNote, PrepNote, GatherFacts, BlockNote;\n    public float FailMemorySeconds;\n}\n';write('AIGoals.cs',s);
write('AIGoalFeatures.cs',[
'// Feature entry points for a goal. Algorithms and persistent state stay with their owners.',
'using System;','using System.Collections.Generic;','using Unity.Entities;','',
'public static class AIGoalFeatures','{',
'    public static void AddCandidates(World server, Entity player, AIObservation o, string curKey, List<AICandidate> dest)','    {',candidates,'    }','',
'    public static bool TryDone(World server, Entity player, GoalKind kind, out string note)','    {',
'        switch (AIGoalKinds.Of(kind)?.Feature)','        {',
'            case GoalFeature.Base: return AIBase.GoalDone(server, player, out note);',
'            case GoalFeature.Farm: return AIFarm.GoalDone(server, player, out note);',
'            case GoalFeature.Cook: return AICook.GoalDone(server, player, out note);',
'            case GoalFeature.Place: return AIPlaceGoal.GoalDone(server, player, out note);',
'            default: note = ""; return false;','        }','    }','',
'    public static string StallNote(World server, Entity player, GoalKind kind)','    {',stallExpr,'    }','}',''].join('\n'));
s=read('AIPlanReport.cs');
// Preserve report field order and evaluation order; only replace the shared vocabulary.
for(const kind of ['Gather','Explore','Follow','Goto','Craft','Place','Store','Home','Farm','Cook'])
 s=s.replaceAll('\\"'+kind.toLowerCase()+'\\":','\\"{AIGoalKinds.Of(GoalKind.'+kind+').WireName}\\":');
// The can_do line is interpolated; no other static-string line currently holds these exact keys.
for(const kind of ['Gather','Craft','Place']) s=s.replaceAll('"'+kind.toLowerCase()+':"','AIGoalKinds.Of(GoalKind.'+kind+').WireName + ":"');
for(const kind of ['Explore','Follow','Farm','Cook','Goto']) s=s.replaceAll('expKeys.Add("'+kind.toLowerCase()+'")','expKeys.Add(AIGoalKinds.Of(GoalKind.'+kind+').WireName)');
write('AIPlanReport.cs',s);
for(const n of ['Core/AIGoalKinds.cs','AIGoalText.cs','AIGoalFeatures.cs']) write(n+'.meta','fileFormatVersion: 2\nguid: '+crypto.randomUUID().replaceAll('-','')+'\n');
console.log('R1 source edits written');
