from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('            lEdge.Exists(c => c.key == key) || lCont.Exists(c => c.key == key);','            lEdge.Exists(c => c.key == key) || lCont.Exists(c => c.key == key) ||\n            (lLife != null && lLife.Exists(c => c.key == key)) || lCombat.Exists(c => c.key == key);')
s=s.replace('HashSet<AICandidate> foodPicks;', 'HashSet<AICandidate> foodPicks = new HashSet<AICandidate>();')
s=s.replace('$"파 들어갈 수 있는 목표 광석·물체 {lDig.FindAll(c => c.progress).Count}개"', '$"보이는 채집 대상의 개별 경로는 채집·이동 목록에서 대상을 고른 뒤 확인"')
s=s.replace('$"기억한 목표 광석 {AIMemory.CountOres(wantedFirst)}곳(갈 길이 있는 것 {lRemember.Count}개)"', '$"기억한 목표 광석 {AIMemory.CountOres(wantedFirst)}곳(개별 경로는 목록에서 대상 선택 후 확인)"')
s=s.replace('_exploreOptions = lExplore.Count + lDigExplore.Count;', '_exploreOptions = lExplore.Count + lDigExplore.Count;\n            if (query.Count > 0) _exploreFrontierNote += " 개별 목적지는 채집·이동 목록에서 고른 뒤 경로 확인.";')
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        public Preparation Preparation;', '        public Preparation Preparation;\n        public string ChoiceLimitNote;');p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig')
needle='        _onlyWait = 0;\n        // live-run fix 7:'
replace='''        if (cands.Count > 255)
        {
            string note = $"직접 선택지가 API 허용량을 넘음({cands.Count}/255). 현재 행동 목록의 추가 세분화가 필요함.";
            if (S.ChoiceLimitNote != note)
            {
                S.ChoiceLimitNote = note;
                AIPlayerSpikeMod.Log("DECISION not sent: " + note);
                AIPlayerSpikeMod.Reply("AI 판단 목록이 너무 큽니다. " + note, ChatKind.Error);
            }
            _needCall = false;
            _retryAt = Time.realtimeSinceStartup + 30f;
            yield break;
        }
        S.ChoiceLimitNote = null;
        _onlyWait = 0;
        // live-run fix 7:'''
assert needle in s;s=s.replace(needle,replace)
# Keep the synchronous adapter usable by production diagnostic callers.
old='''        List<AICandidate> result = null;
        foreach (var step in BuildCandidateSteps(server, player, o, prefetch, breachCall, value => result = value)) { }
        return result;'''
new='''        List<AICandidate> result = null;
        var previous = _building;
        if (_building == null) _building = new BuildContext();
        try
        {
            foreach (var step in BuildCandidateSteps(server, player, o, prefetch, breachCall, value => result = value)) { }
            return result;
        }
        finally { _building = previous; }'''
assert old in s;s=s.replace(old,new)
p.write_text(s,encoding='utf-8')
p=Path('Tools/restriction-tests/prepare.mjs');s=p.read_text(encoding='utf-8-sig');s=s.replace(".replace('yield return 0;', '')", ".replace(/yield return 0;/g, '')");p.write_text(s,encoding='utf-8')
p=Path('Tools/restriction-tests/Program.cs');s=p.read_text(encoding='utf-8-sig');s+='\n// The leaf-eligibility adapter resolves registered routes immediately. Production catalog routing is checked separately.\npublic partial class FoodProbe { private void DeferRoute(string key,int2 at,string label,Action<Plan,string> complete){var p=PlanC(server,player,at,out var why);complete(p,why);} }\n';p.write_text(s,encoding='utf-8')
p=Path('Tools/path-preparation-tests/Program.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        public int _nPlans, _nPlansCached;', '        public int _nPlans, _nPlansCached, RouteCountHint;\n        public object Query;');p.write_text(s,encoding='utf-8')
