from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        while (!job.Context.Batch.StepSearches(32)) yield return 1;\n        if (job.Context.Batch.Changed) throw new PreparationChanged(job.Context.Batch.Reason);\n','',1)
s=s.replace('        var job = batch != null ? batch.BeginPlan(target, -1, false, detour) : AITunnel.BeginPlan(server, player, target, -1, false, detour);','''        // Small queries keep a local search. Once a build requests many distinct routes,
        // finish the two shared trees cooperatively and reuse them for every later target.
        if (batch != null && !batch.SearchesReady && _building.Plans.Count >= 8)
            while (!batch.StepSearches(32)) yield return 1;
        var job = batch == null ? AITunnel.BeginPlan(server, player, target, -1, false, detour)
            : batch.SearchesReady ? batch.BeginPlan(target, -1, false, detour)
            : batch.BeginSinglePlan(target, -1, false, detour);''')
s=s.replace('                var batchJob = _building.Batch.BeginPlan(target, -1, false, detour);','''                var batchJob = _building.Batch.SearchesReady
                    ? _building.Batch.BeginPlan(target, -1, false, detour)
                    : _building.Batch.BeginSinglePlan(target, -1, false, detour);''')
p.write_text(s,encoding='utf-8')
p=Path('Tools/path-preparation-tests/Program.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        public bool StepSearches(int budget) => true;', '        public bool SearchesReady;\n        public bool StepSearches(int budget) { SearchesReady = true; return true; }\n        public PlanJob BeginSinglePlan(int2 target, int damage, bool edge, bool detour) { Calls++; return new PlanJob(); }');p.write_text(s,encoding='utf-8')
