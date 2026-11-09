exec(open(__file__.replace('edit2.py','edit.py'),encoding='utf8').read().split("edit(S+'Core/AIGoalKinds.cs'")[0])
def base(s):
 s=s.replace('public const float BaseRadius = 10f;','public const float BaseRadius = 0f; // legacy field; no permission/range filter uses this value').replace('public const float HomeArrive = 4f;','public const float HomeArrive = AIPlaces.StandArrive;')
 s=s.replace('c.placed && BaseDistTile(c.tile) <= BaseRadius','c.placed')
 s=s.replace(' || (g.kind == GoalKind.Store && bd > BaseRadius)','')
 s=s.replace('g.kind == GoalKind.Store && bd <= BaseRadius','g.kind == GoalKind.Store')
 s=s.replace('                    if (BaseDistTile(AIBody.TileOf(c.pos)) > BaseRadius) continue;\n','')
 s=s.replace('if (chests.Count < 4) ','').replace('if (parts.Count < max) ','')
 s=s.replace('(BaseDistTile(c.tile) <= BaseRadius ? "기지 " : "") + ','')
 s=s.replace('(기지 = 그 둘레 {BaseRadius:F0}칸)','(기지 좌표는 귀환 기준점)')
 s=s.replace('if (h >= 50 || HasHungerFoodInBag(server, player)) return;','if (HasHungerFoodInBag(server, player)) return;')
 s=s.replace('if (h >= 75) return;','')
 s=s.replace('if (res.Count > max) res.RemoveRange(max, res.Count - max);','')
 return s
edit(S+'AIBase.cs',base)
edit(S+'AILife.cs',lambda s:s.replace('return math.distance((float2)new int2(x, y), (float2)AIBase.BaseTile) <= AIBase.BaseRadius;','return true; // any owned chest may satisfy an unspecified storage destination'))
edit(S+'AIPlace.cs',lambda s:s.replace('baseItem = baseItem && AIBase.AtBase(me),','baseItem = false, // placement has no artificial base-radius condition\n            '))
def farm(s):
 s=s.replace('math.max(0, math.min(MaxPlots - plots, seedsBag + seedsChest - empty.Count))','math.max(0, seedsBag + seedsChest - empty.Count)')
 s=s.replace('snap.plots < MaxPlots','true')
 s=s.replace('int2 b = AIBase.BaseTile;\n        int R = (int)PlotMaxFromBase;','int2 b = AIBody.TileOf(me);\n        var half = AIVision.HalfExtents();\n        int rx = (int)math.ceil(half.x), ry = (int)math.ceil(half.y);')
 s=s.replace('for (int x = -R; x <= R; x++)\n            for (int y = -R; y <= R; y++)','for (int x = -rx; x <= rx; x++)\n            for (int y = -ry; y <= ry; y++)')
 s=s.replace('                if (db < PlotMinFromBase || db > PlotMaxFromBase) continue;\n','')
 # Nearest order is execution convenience; every valid observed tile remains available.
 s=s.replace('else score = math.abs(math.distance((float2)t, (float2)b) - 4f) * 2f + 0.2f * math.distance(me.xz, (float2)t);','else score = math.distance(me.xz, (float2)t);')
 return s
edit(S+'AIFarm.cs',farm)
def candidates(s):
 s=s.replace('if (cur is GotoSkill && cur.Tag == "explore:" + e.dir) continue;','')
 s=s.replace('if (curWalk.HasValue && math.cmax(math.abs(curWalk.Value - e.t)) <= 4) continue;','')
 s=s.replace('if (op.nov <= 0) break;','')
 s=s.replace('if (!wanted.Contains(ObjectID.Wood)) return;','')
 s=s.replace('for (int dx = -14; dx <= 14; dx++)','for (int dx = -(int)math.ceil(o.half.x); dx <= (int)math.ceil(o.half.x); dx++)')
 s=s.replace('for (int dy = -10; dy <= 10; dy++)','for (int dy = -(int)math.ceil(o.half.y); dy <= (int)math.ceil(o.half.y); dy++)')
 return s
edit(S+'AIBrain.Candidates.cs',candidates)
# Production report selectors get every row. Helper explicit budgets remain available to callers/tests.
edit(S+'AIPlanReport.cs',lambda s:s.replace('AIPlaces.CraftGroups(rows, 40,','AIPlaces.CraftGroups(rows, int.MaxValue,'))
# The legacy reported maximum is zero = unbounded. The mod does not truncate any plan by it.
edit(S+'AIDecisions.cs',lambda s:s.replace('public int upperPlanMaxSteps = 5;','public int upperPlanMaxSteps = 0; // legacy compatibility; plan lists are not truncated'))
# No separate arbitrary number cap beyond signed integer wire representation.
edit('Tools/ai-bridge/protocol.mjs',lambda s:s.replace('g.count > 9999','g.count > 2147483647').replace('g[k] > 9999','g[k] > 2147483647').replace('1..9999','1..2147483647').replace('0..9999','0..2147483647'))
for p in ['Core/AILifeTree.cs','Core/AIInventoryGoal.cs']:
 if (root/S/p).exists(): edit(S+p,lambda s:s.replace('9999','int.MaxValue'))
