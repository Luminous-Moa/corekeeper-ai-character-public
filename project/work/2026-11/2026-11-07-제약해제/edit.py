from pathlib import Path
import re
root=Path('C:/Dev/CoreKeeperModSDK-unrestricted')
def edit(p,fn):
 f=root/p; s=f.read_text(encoding='utf-8-sig'); t=fn(s); f.write_text(t,encoding='utf-8',newline='\n')
def rep(s,a,b):
 if a not in s: print('MISSING',a[:90])
 return s.replace(a,b)
S='Assets/AIPlayerSpike/Scripts/'
edit(S+'Core/AIGoalKinds.cs',lambda s:s.replace('GoalItemMode.Required, 30,','GoalItemMode.Required, int.MaxValue,').replace('GoalItemMode.Required, 10,','GoalItemMode.Required, int.MaxValue,'))
def planner(s):
 s=re.sub(r'        int max = math.max\(1, AIBrain.Settings\(\).upperPlanMaxSteps\);\n        if \(steps.Count > max\).*\n','',s)
 s=s.replace('int max = math.max(1, AIBrain.Settings().upperPlanMaxSteps);','int max = steps.Count;')
 s=s.replace('while (_events.Count > 6) _events.RemoveAt(0);','// Preserve every unreported event until it is reported.')
 s=s.replace('if (id != ObjectID.WallDirtBlock && id != ObjectID.CopperOre && id != ObjectID.TinOre && id != ObjectID.Wood && !S._gatherFood.Contains(id)) return $"보고의 can_do.gather에 없는 품목 \'{item}\'";','if (!AIKnowledge.KnownItems(server, player).Contains(id)) return $"아직 알게 되지 않은 품목 \'{item}\'";')
 return s
edit(S+'AIPlanner.cs',planner)
def host(s):
 start=s.index('        int open = 0;',s.index('public void Add(')); end=s.index('        Prune();',start)
 s=s[:start]+s[end:]; return s.replace('public const int MaxOpen = 3;','public const int MaxOpen = int.MaxValue; // compatibility; open orders are never evicted by count')
edit(S+'Core/AIHostOrders.cs',host)
edit(S+'AIPlanReport.cs',lambda s:s.replace('if (bag.Count < 10) ','').replace('var gather = new List<string> { "WallDirtBlock", "CopperOre", "TinOre", "Wood" };','var gather = AIKnowledge.KnownItems(server, player).ConvertAll(id => id.ToString());').replace('AIPlaces.CraftGroups(rows, 40,','AIPlaces.CraftGroups(rows, int.MaxValue,').replace('public const int CraftChars = 1800, PlaceChars = 400;','public const int CraftChars = int.MaxValue, PlaceChars = int.MaxValue;').replace('AIVitals.RecentDowns(3)','AIVitals.RecentDowns(int.MaxValue)').replace('math.max(1, AIBrain.Settings().upperPlanMaxSteps)','0'))
def life(s):
 s=s.replace('s.ViewSuspended = combat;','s.ViewSuspended = false;').replace('        if (combat) { actual.RemoveAll(c => c.life != null || c.lifeNav != null); return actual; }\n','').replace('        if (AIGoals.Goal.kind == GoalKind.None) return actual;\n','')
 s=s.replace('int start = s.Cursor.Page * 5;','int start = 0;').replace('i < selectable.Count && i < start + 5','i < selectable.Count')
 s=re.sub(r'        if \(s.Cursor.Page > 0\).*\n','',s);s=re.sub(r'        if \(\(long\)start \+ 5 < selectable.Count\).*\n','',s)
 s=s.replace('        if (AIGoals.Goal.kind == GoalKind.None) return "현재 목표가 없어 생활 탐색을 시작할 수 없음";\n','')
 return s
edit(S+'AILife.cs',life)
def places(s):
 s=s.replace('public const int MaxShapeTiles = 30;','public const int MaxShapeTiles = int.MaxValue;')
 s=s.replace('public const int PlacesMax = 12;','public const int PlacesMax = int.MaxValue;').replace('public const int MineMax = 5, OtherMax = 2, OreKinds = 3, WaterMax = 1;','public const int MineMax = int.MaxValue, OtherMax = int.MaxValue, OreKinds = int.MaxValue, WaterMax = int.MaxValue;').replace('public const int PlacesChars = 900;','public const int PlacesChars = int.MaxValue;')
 s=s.replace('n > MaxShapeTiles','n > int.MaxValue')
 s=s.replace('Take("fixed", 4, false);','Take("fixed", int.MaxValue, false);').replace('Take("ore", OreKinds, true);','Take("ore", OreKinds, false);')
 # Shape arithmetic stays bounded by the integer representation, not an arbitrary count.
 s=s.replace('(long)(x1 - x0 + 1) * (y1 - y0 + 1) > 4 * MaxShapeTiles','((long)x1 - x0 + 1) * ((long)y1 - y0 + 1) > int.MaxValue')
 s=s.replace('for (int i = 0; i <= MaxShapeTiles; i++) res.Add((x0 + i, y0)); return res;','throw new ArgumentOutOfRangeException(nameof(r), "shape exceeds integer tile count");') if 'nameof(r)' in s else s
 return s
edit(S+'Core/AIPlaces.cs',places)
edit('Tools/ai-bridge/protocol.mjs',lambda s:s.replace('export const PLAN_MAX = 10;','export const PLAN_MAX = Infinity;').replace(' || a.plan.length > PLAN_MAX','').replace('a list of 1..${PLAN_MAX} goals','a nonempty list of goals'))
edit(S+'AIBrain.Prep.cs',lambda s:s.replace('private static readonly ObjectID[] Gatherable = { ObjectID.WallDirtBlock, ObjectID.CopperOre, ObjectID.TinOre, ObjectID.Wood };','private static bool Gatherable(ObjectID id, World world, Entity player) => AIKnowledge.KnownItems(world, player).Contains(id);').replace('Array.IndexOf(Gatherable, x.id) >= 0','Gatherable(x.id, server, player)'))
def candidates(s):
 s=s.replace('if (Goal.kind != GoalKind.Explore && Goal.kind != GoalKind.Free) for','for')
 s=s.replace('if (gatherOre || foodNeed)','if (true)')
 s=s.replace('if (!forGoal && !foodGoal && !foodHunger) continue;','if (!forGoal && !foodGoal && !foodHunger && !learned && !woody && !fs.plant && fs.food == ObjectID.None && !em0.HasComponent<DestructibleObjectCD>(e)) continue;')
 s=s.replace('if (!wanted.Contains(ObjectID.Wood)) return;','')
 s=re.sub(r'\s*if \(lDig.Exists\(x => x.digWall && math.cmax\(math.abs\(x.tile - t.p\)\) <= 3\)\) continue;[^\n]*','',s)
 s=re.sub(r'\s*if \(curDig.HasValue && math.cmax\(math.abs\(curDig.Value - t.p\)\) <= 3\) continue;[^\n]*','',s)
 s=re.sub(r'\s*if \(lRemember.Exists\([^\n]*\) continue;[^\n]*','',s)
 s=re.sub(r'wantExplore = Goal.kind == GoalKind.Explore \|\| free \|\| \(gatherOre && .*?;','wantExplore = true;',s)
 s=s.replace('if (wantExplore && !free','if (wantExplore')
 return s
edit(S+'AIBrain.Candidates.cs',candidates)
def loop(s):
 start=s.index('        var best = new Dictionary<string, (int2 t, int nov, float d)>();',s.index('private static List<(int2 t, string dir, int novelty)> ExploreTargets'))
 end=s.index('        return res;',start)
 s=s[:start]+'''        // Every reachable observed destination remains selectable; direction is a label, not a quota.
        var res = new List<(int2, string, int)>();
        foreach (var t in reach)
        {
            if (math.all(t == me)) continue;
            int2 v = t - me;
            string dir = math.abs(v.x) >= math.abs(v.y) ? (v.x > 0 ? "동" : "서") : (v.y > 0 ? "북" : "남");
            res.Add((t, dir, AIMemory.Novelty(t)));
        }
        res.Sort((a,b) => b.Item3.CompareTo(a.Item3));
'''+s[end:]
 s=s.replace('if (invs.Count < 12) ','')
 return s
edit(S+'AIBrain.Loop.cs',loop)
edit(S+'AITunnel.cs',lambda s:re.sub(r'        if \(math.any\(hi - lo > 260\)\).*\n','',s))
def assembler(s):
 start=s.index('        if (a.goal == GoalKind.None)');end=s.index('        if (free) _setLog',start)
 s=s[:start]+'''        // The model sees every supported executable alternative, including optional work during combat.
        list.AddRange(lCont); list.AddRange(lFollow); list.AddRange(lEnemy); list.AddRange(a.lBrute);
        list.AddRange(lCombat); list.AddRange(lDoor); list.AddRange(lCraft); list.AddRange(lEdge); list.AddRange(lLife);
        list.AddRange(lMine); list.AddRange(lDig); list.AddRange(lRemember); list.AddRange(lItem);
        list.AddRange(lExplore); list.AddRange(lDigExplore); list.AddRange(lOther);
'''+s[end:]
 return s
edit(S+'Core/AICandidateAssembler.cs',assembler)
