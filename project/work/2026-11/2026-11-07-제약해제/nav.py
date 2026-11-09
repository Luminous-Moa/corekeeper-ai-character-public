exec(open(__file__.replace('nav.py','edit.py'),encoding='utf8').read().split("edit(S+'Core/AIGoalKinds.cs'")[0])
def nav(s):
 s=s.replace('    private int _pathIdx;','    private int _pathIdx;\n    private float _searchRetryAt;\n    private bool _knownSearchCapped;\n    internal static bool LastAStarCapped { get; private set; }')
 s=s.replace('        Target = target;','        Target = target;\n        _searchRetryAt = 0f;')
 s=s.replace('knownPathAtStart={_knownPathAtStart}','knownPathAtStart={(_knownSearchCapped ? "unknown(search budget)" : _knownPathAtStart.ToString())}')
 s=s.replace('        if (math.any(hi - lo > 160)) return false;','        _knownSearchCapped = false;')
 s=s.replace('int2 lo = math.min(start, goal) - 12, hi = math.max(start, goal) + 12;','int2 lo = new int2(int.MinValue,int.MinValue), hi = new int2(int.MaxValue,int.MaxValue);')
 # Known-path BFS exhaustion is uncertainty, not a proof that a known route does not exist.
 a=s.index('    private bool KnownPathExists(');b=s.index('\n    }',a)
 block=s[a:b];block=block.replace('        return false;','        _knownSearchCapped = q.Count > 0;\n        return false;')
 s=s[:a]+block+s[b:]
 s=s.replace('        if (math.any(hi - lo > 120)) return null;','        LastAStarCapped = false;')
 a=s.index('    internal static List<int2> AStar(');b=s.index('\n    }',a)
 block=s[a:b];block=block.replace('        return null;','        LastAStarCapped = open.Count > 0;\n        return null;')
 s=s[:a]+block+s[b:]
 s=s.replace('            var trA = new AITileReader(server);','            if (now < _searchRetryAt) return NavStatus.Moving;\n            var trA = new AITileReader(server);')
 s=s.replace('Func<int2, bool> allowed = n => (!_knownOnly || KnownAtStart(n))','Func<int2, bool> allowed = n => (AIVision.CanSee(pos, n) || AIMemory.SeenTile(n)) && (!_knownOnly || KnownAtStart(n))')
 needle='            _path = AStar(trA, from, _segGoal ?? Target, allowed);'
 s=s.replace(needle,needle+'''\n            if (_path == null && LastAStarCapped)
            {
                ObserveOnce("nav-search-budget", "이번 경로 검색 예산 안에 경로를 찾지 못함; 길이 없다고 확인한 것은 아님");
                _searchRetryAt = now + 0.5f;
                return NavStatus.Moving;
            }''')
 return s
edit(S+'AINavigator.cs',nav)
def tunnel(s):
 old='''            reason = toolShort.Count > 0
                ? $"뚫을 수 있는 길 없음: 도구 부족 벽 {toolShort.Count}개({plan.toolShortExample}, 추정 피해 {plan.damage})로 막힘"
                : allCapped ? "뚫을 수 있는 길을 찾기 범위 안에서 못 찾음(탐색 상한)"'''
 new='''            reason = allCapped ? $"이번 검색 예산 안에 경로를 찾지 못함(탐색 상한, 길 없음은 미확정); 관측한 도구 부족 벽 {toolShort.Count}개"
                : toolShort.Count > 0 ? $"뚫을 수 있는 길 없음: 도구 부족 벽 {toolShort.Count}개({plan.toolShortExample}, 추정 피해 {plan.damage})로 막힘"
                : false ? ""'''
 return s.replace(old,new)
edit(S+'AITunnel.cs',tunnel)
def cand(s):
 s=s.replace('if (isMatch) digOre.Add((d, w.p, ore != ObjectID.None ? ore : obj));','digOre.Add((d, w.p, ore != ObjectID.None ? ore : obj));')
 s=s.replace('progress = true,\n                        desc = $"뿌리 캐기','progress = gatherOre && wanted.Contains(ObjectID.Wood),\n                        desc = $"뿌리 캐기')
 s=s.replace('if (curDig.HasValue && math.cmax(math.abs(curDig.Value - r.p)) <= 3) continue;','')
 # Every observed wall is already offered by Walls/DigOre. Do not create four privileged snapped endpoints.
 a=s.index('            // A running tunnel is offered as "continue"; a new dig-explore target')
 b=s.index('            // Nothing new within one screen',a)
 s=s[:a]+'            // Visible wall/tunnel targets are provided individually by Walls/DigOre.\n'+s[b:]
 s=s.replace('digWall = true, targetLabel = $"목표 광석 {t.what}", progress = true','digWall = true, targetLabel = $"보이는 채집 대상 {t.what}", progress = gatherOre && wanted.Contains(t.what)')
 return s
edit(S+'AIBrain.Candidates.cs',cand)
def farm(s):
 a=s.index('        // keep the 12 nearest to base');b=s.index('        if (changed) AIMemory.MarkDirty();',a)
 s=s[:a]+s[b:]
 s=re.sub(r'    public const float PlotMinFromBase =.*\n','',s);s=re.sub(r'    public const float PlotMaxFromBase =.*\n','',s)
 return s
edit(S+'AIFarm.cs',farm)
