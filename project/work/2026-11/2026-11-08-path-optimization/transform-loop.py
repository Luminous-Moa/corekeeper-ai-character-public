from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('    private sealed class State\n    {','    private sealed class State\n    {\n        public Preparation Preparation;')
s=s.replace('        public World _server;','        public World _server;\n        public readonly Dictionary<long, (float at, int2 from, int seen, TunnelPlan plan, string why)> Plans = new Dictionary<long, (float, int2, int, TunnelPlan, string)>();')
s=s.replace('    internal static void AbortRequest(string why)\n    {','    internal static void AbortRequest(string why)\n    {\n        CancelPreparation(why);')
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('                if (_req != null) AbortRequest("no goal");','                if (_req != null || S.Preparation != null) AbortRequest("no goal");')
s=s.replace('        if (_req != null)\n        {\n            if (_req.isDone)', '        if (S.Preparation != null)\n        {\n            StepPreparation(server, player, now);\n            return;\n        }\n        if (_req != null)\n        {\n            if (_req.isDone)',1)
start=s.index('        var prepSw = System.Diagnostics.Stopwatch.StartNew();',s.index('    private static void Send('));end=s.index('        if (prefetch &&',start)
s=s[:start]+'''        if (S.Preparation != null) return;
        S.Preparation = new Preparation(server, player, now, trigger, prefetch, emergency, breach, key);
        StepPreparation(server, player, now);
    }

    private static void CompletePreparation(World server, Entity player, float now, Preparation prepared)
    {
        var o = prepared.Observation;
        var cands = prepared.Candidates;
        string trigger = prepared.Trigger, key = prepared.Key;
        bool prefetch = prepared.Prefetch, emergency = prepared.Emergency;
        var prepSw = prepared.Work;
        _prepParts.Clear();
        _prepParts.Add($"frames {prepared.Frames}, wall {(Time.realtimeSinceStartup - prepared.Started) * 1000f:F1}ms, plans {_msPlans:F1} n{_nPlans} cached{_nPlansCached}");
''' +s[end:]
# preserve build-local plans across elapsed TTL; the running build owns and releases these results.
s=s.replace('        var plans = S._plans;','        var plans = _building != null ? _building.Plans : S._plans;')
s=s.replace('        bool okC = plans.TryGetValue(k, out var c) && (!detour || c.seen == 1);','''        bool okC = plans.TryGetValue(k, out var c) && (!detour || c.seen == 1);
        if (_building != null && okC) { _nPlansCached++; why = c.why; return c.plan; }''')
s=s.replace('            if (plans.Count > 64) plans.Clear();','            if (_building == null) PrunePlans(plans);')
p.write_text(s,encoding='utf-8')
