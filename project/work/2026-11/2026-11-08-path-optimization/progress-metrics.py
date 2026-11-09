from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('        public int Frames;', '        public int Frames;\n        public double MaxSliceMs;\n        public float LastProgressLogAt;\n        public bool Finished;')
s=s.replace('World = world; Player = player; Started = now;', 'World = world; Player = player; Started = now; LastProgressLogAt = now;')
s=s.replace('                S.Preparation = null;\n                job.Dispose();', '                job.Finished = true;\n                S.Preparation = null;\n                job.Dispose();')
s=s.replace('            preparationFrameUsed += slice.Elapsed.TotalMilliseconds;', '''            double sliceMs = slice.Elapsed.TotalMilliseconds;
            preparationFrameUsed += sliceMs;
            job.MaxSliceMs = Math.Max(job.MaxSliceMs, sliceMs);
            if (job.Finished || (S.Preparation == job && now - job.LastProgressLogAt >= 2f))
            {
                job.LastProgressLogAt = now;
                AIPlayerSpikeMod.Log($"PREP {(job.Finished ? "finished" : "progress")}: rev={job.GoalRevision} frames={job.Frames} plans={job.Context._nPlans} cached={job.Context._nPlansCached} work={job.Work.Elapsed.TotalMilliseconds:F1}ms maxSlice={job.MaxSliceMs:F2}ms");
            }''')
p.write_text(s,encoding='utf-8')
# Match fields used by the actual scheduling source.
p=Path('Tools/path-preparation-tests/Program.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        public AITunnel.PlanBatch Batch;', '        public AITunnel.PlanBatch Batch;\n        public int _nPlans, _nPlansCached;');p.write_text(s,encoding='utf-8')
