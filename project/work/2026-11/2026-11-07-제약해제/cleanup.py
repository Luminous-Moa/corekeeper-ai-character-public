exec(open(__file__.replace('cleanup.py','edit.py'),encoding='utf8').read().split("edit(S+'Core/AIGoalKinds.cs'")[0])
edit(S+'AIPlanReport.cs',lambda s:s.replace('public const int CraftMax = 40;','public const int CraftMax = int.MaxValue;'))
edit(S+'Core/AIPlanProtocol.cs',lambda s:s.replace('n <= 9999','n <= int.MaxValue'))
edit(S+'AIBrain.cs',lambda s:s.replace('Trim(s.Reason, 90)','s.Reason').replace('        while (_recent.Count > 4) _recent.RemoveAt(0);\n',''))
edit(S+'AIBrain.Candidates.cs',lambda s:s.replace('if (gatherOre && wanted.Contains(ObjectID.Wood))','if (true)').replace('if (Goal.kind != GoalKind.None && !inCombat) { try { foodNeed','{ try { foodNeed'))
edit(S+'AIBase.cs',lambda s:s.replace('public static bool AtBase(float3 pos) => BaseDist(pos) <= BaseRadius;','public static bool AtBase(float3 pos) => BaseDist(pos) <= HomeArrive;'))
edit(S+'AICook.cs',lambda s:s.replace('if (AIBase.AtBase(pos))','if (true) // pot placement has no implicit base-area permission'))
def places(s):
 s=s.replace('        if (s.Placed.Count > 64) s.Placed.RemoveAt(0);\n','')
 s=s.replace('t.AreaR = AIBase.BaseRadius;','t.AreaR = 0f; // base is an anchor, not an artificial permission radius')
 s=s.replace('if (t.Ref.Kind == "base") return AIBase.AtBase(me) || AIEdge.PlanActive && AIEdge.IsInside(AIEdge.StandTile(server, me));','if (t.Ref.Kind == "base") return true; // no inferred base-area boundary')
 start=s.index('        // a station / pot already placed at base by this AI:')
 end=s.index('        return null;',start)
 s=s[:start]+s[end:]
 s=s.replace('bool AtBase(int2 t) => AIBase.BaseDistTile(t) <= AIBase.BaseRadius;','bool AtBase(int2 t) => math.all(t == AIBase.BaseTile);')
 return s
edit(S+'AIPlaces.cs',places)
# Restore original combat ordering and danger facts, then make every other supported alternative available too.
import subprocess
old=subprocess.check_output(['git','show','5a05b80:'+S+'Core/AICandidateAssembler.cs'],cwd=root,text=True,encoding='utf8')
old=old.replace('goalSide.RemoveAll(c => !c.progress || !Doable(c));','goalSide.RemoveAll(c => !Doable(c));')
old=old.replace('if (!free) list.AddRange(lItem); else list.AddRange(lItem.FindAll(foodPicks.Contains));','list.AddRange(lItem);')
needle='        if (free) _setLog += " set=free-time";'
old=old.replace(needle,'''        // Selection policy does not hide other executable actions in None or combat states.
        foreach (var group in new[] { a.lCont, a.lFollow, a.lMine, a.lDig, a.lRemember, a.lItem, a.lExplore, a.lDigExplore, a.lOther, a.lCraft, a.lLife, a.lEdge })
            if (group != null) foreach (var c in group) if (!list.Contains(c)) list.Add(c);
'''+needle)
# None used to clear shared lists; leave their source candidates available for the final full append.
old=old.replace('            lFollow.Clear(); lMine.Clear(); lDig.Clear(); lRemember.Clear(); lItem.Clear(); lExplore.Clear(); lDigExplore.Clear(); lOther.Clear();','')
edit(S+'Core/AICandidateAssembler.cs',lambda s:old)
