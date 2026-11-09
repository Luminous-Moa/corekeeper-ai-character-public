from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        public World _server;', '        public World _server;\n        public AITunnel.PlanBatch Batch;');p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('            Context.PlanToolRed.Clear();','            Context.PlanToolRed.Clear();\n            Context.Batch = null;')
s=s.replace('        catch (Exception e)\n        {\n            CancelPreparation("preparation failed");', '''        catch (PreparationChanged e)
        {
            CancelPreparation(e.Message);
            _retryAt = now;
        }
        catch (Exception e)
        {
            CancelPreparation("preparation failed");''')
s=s.replace('''        foreach (var step in BuildCandidateSteps(job.World''', '''        job.Context.Batch = AITunnel.BeginBatch(job.World, job.Player, job.Observation.pos);
        while (!job.Context.Batch.StepCapture(32)) yield return 1;
        if (job.Context.Batch.Changed) throw new PreparationChanged(job.Context.Batch.Reason);
        foreach (var step in BuildCandidateSteps(job.World''')
s=s.replace('        var job = AITunnel.BeginPlan(server, player, target, -1, false, detour);\n        int2 from = AIEdge.StandTile(server, AIBody.Pos(server, player));','        var batch = _building.Batch;\n        var job = batch != null ? batch.BeginPlan(target, -1, false, detour) : AITunnel.BeginPlan(server, player, target, -1, false, detour);\n        int2 from = batch != null ? batch.Start : AIEdge.StandTile(server, AIBody.Pos(server, player));')
s=s.replace('        plans[key] = (Time.realtimeSinceStartup,', '        if (job.Changed) throw new PreparationChanged(job.Reason);\n        plans[key] = (Time.realtimeSinceStartup,')
pos=s.index('    // The expensive candidate loops')
s=s[:pos]+'''    private sealed class PreparationChanged : Exception
    {
        public PreparationChanged(string reason) : base(reason) { }
    }

    private static IEnumerable<int> ValidatePreparation(Preparation job)
    {
        var batch = job.Context.Batch;
        batch.BeginValidation();
        while (!batch.StepValidation(32)) yield return 1;
        if (batch.Changed) throw new PreparationChanged(batch.Reason);
    }

'''+s[pos:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('        now = Time.realtimeSinceStartup;\n        if (!CostAllows(now)', '        foreach (var step in ValidatePreparation(prepared)) yield return step;\n        now = Time.realtimeSinceStartup;\n        if (!CostAllows(now)')
s=s.replace('            plan = AITunnel.Plan(server, player, target, out why, -1, false, detour);','''            if (_building?.Batch != null)
            {
                var batchJob = _building.Batch.BeginPlan(target, -1, false, detour);
                batchJob.RunUntilDone();
                if (batchJob.Changed) throw new PreparationChanged(batchJob.Reason);
                plan = batchJob.Result;
                why = batchJob.Reason;
            }
            else plan = AITunnel.Plan(server, player, target, out why, -1, false, detour);''')
p.write_text(s,encoding='utf-8')
