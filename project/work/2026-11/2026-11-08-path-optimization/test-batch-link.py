from pathlib import Path
p=Path('Tools/path-preparation-tests/Program.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('public bool NeedCall, Throw, ContextCorrect = true;', 'public bool NeedCall, Throw, ChangeTerrain, ContextCorrect = true;')
s=s.replace('    private sealed class BuildContext\n    {', '    private sealed class BuildContext\n    {\n        public AITunnel.PlanBatch Batch;')
s=s.replace('        S.Completions++;', '''        if (S.ChangeTerrain) job.Context.Batch.Changed = true;
        foreach (var step in ValidatePreparation(job)) yield return step;
        S.Completions++;''')
s=s.replace('        var broken = new State(30)', '''        var changedTerrain = new State(61) { ChangeTerrain = true }; Begin(changedTerrain, world, player);
        while (changedTerrain.Preparation != null) Tick(changedTerrain, world, player);
        Program.Check(changedTerrain.Completions == 0 && changedTerrain.NeedCall && changedTerrain.Disposals == 1,
            "changed terrain cancels the whole preparation before sending instead of reporting no path");

        var broken = new State(30)''')
s=s.replace('public sealed class AIObservation\n{','public sealed class AIObservation\n{\n    public int2 pos;')
s=s.replace('    public static int LastToolShortRed;\n    public sealed class PlanJob', '''    public static int LastToolShortRed;
    public sealed class PlanBatch
    {
        public bool Changed;
        public string Reason => "terrain changed";
        public int2 Start => default;
        public bool StepCapture(int budget) => true;
        public PlanJob BeginPlan(int2 target, int damage, bool edge, bool detour) => new PlanJob();
        public void BeginValidation() { }
        public bool StepValidation(int budget) => true;
    }
    public static PlanBatch BeginBatch(World world, Entity player, int2 pos) => new PlanBatch();
    public sealed class PlanJob''')
s=s.replace('        public TunnelPlan Result => new TunnelPlan();', '        public bool Changed => false;\n        public TunnelPlan Result => new TunnelPlan();')
p.write_text(s,encoding='utf-8')
