from pathlib import Path
p=Path('Tools/execution-tests/Stubs.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('    private sealed class ApplyState\n    {', '''    private sealed class PreparationProbe { public bool Prefetch, PrefetchCompleted; }
    private static void CancelPreparation(string why) { S.Preparation = null; _needCall = true; }
    public static void SeedPreparingPrefetch(AISkill skill) { _prefetchFor = skill; S.Preparation = new PreparationProbe { Prefetch = true }; }
    public static bool HasPreparingPrefetch => S.Preparation != null;
    public static bool PreparingPrefetchCompleted => S.Preparation?.PrefetchCompleted == true;
    private sealed class ApplyState
    {
        public PreparationProbe Preparation;''')
s=s.replace('        AbortedRequests = 0;', '        S.Preparation = null;\n        AbortedRequests = 0;');p.write_text(s,encoding='utf-8')
p=Path('Tools/execution-tests/Program.cs');s=p.read_text(encoding='utf-8-sig');pos=s.index('        ExpeditionFlowTests.Run(Check);');s=s[:pos]+'''        selected = BeginGather(); AIBrain.SeedPreparingPrefetch(selected);
        selected.Status = SkillStatus.Done; AIBrain.OnSkillEnded(selected, 10);
        Check(AIBrain.HasPreparingPrefetch && AIBrain.PreparingPrefetchCompleted,
            "production skill-ended callback preserves preparing prefetch and marks the original action completed");
        selected = BeginGather(); AIBrain.SeedPreparingPrefetch(selected);
        selected.Status = SkillStatus.Failed; AIBrain.OnSkillEnded(selected, 10);
        Check(!AIBrain.HasPreparingPrefetch, "production skill-ended callback cancels preparation after action failure");
'''+s[pos:];p.write_text(s,encoding='utf-8')
