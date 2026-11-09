from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('    private static double preparationFrameUsed;','    private static double preparationFrameUsed;\n    private static int activePreparations;')
s=s.replace('        public int Frames;', '        public int Frames;\n        public bool PrefetchCompleted;\n        private bool disposed;')
s=s.replace('            World = world;', '            activePreparations++;\n            World = world;')
s=s.replace('        public void Dispose() { Steps?.Dispose(); Steps = null; Context.Plans.Clear(); }','''        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            activePreparations--;
            Steps?.Dispose();
            Steps = null;
            Context.Plans.Clear();
            Context.PlanToolRed.Clear();
        }''')
s=s.replace('while (preparationFrameUsed + slice.Elapsed.TotalMilliseconds < PreparationFrameMs)','while (preparationFrameUsed + slice.Elapsed.TotalMilliseconds < PreparationFrameMs && slice.Elapsed.TotalMilliseconds < PreparationFrameMs / Math.Max(1, activePreparations))')
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.cs');s=p.read_text(encoding='utf-8-sig')
pos=s.index('        bool done = s.Status == SkillStatus.Done;')
s=s[:pos]+'''        // Preparation has the same prefetch lifetime as a request already on the wire.
        if (S.Preparation != null && S.Preparation.Prefetch && _prefetchFor == s)
        {
            if (s.Status == SkillStatus.Done) S.Preparation.PrefetchCompleted = true;
            else { PrefetchDropped++; CancelPreparation($"prefetch action {st}"); }
        }
'''+s[pos:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        _prefetchSkillDone = false;\n        if (prefetch) Prefetches++;','        _prefetchSkillDone = prepared.PrefetchCompleted;\n        if (prefetch) Prefetches++;');p.write_text(s,encoding='utf-8')
p=Path('Tools/core-tests/ContinueWarningTests.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('loop.Contains("Trim(AIContinueText.ForShow(c.desc), 120)")', 'loop.Contains("options={cands.Count} (full facts in request; decisionlog.on records them)")');s=s.replace('Loop.cs: SEND, RESULT, the down record\'s last choices and the \'AI 판단:\' chat use ForShow; the request text does not','Loop.cs: SEND summarizes count; RESULT, down record and AI chat use ForShow while request retains every fact');p.write_text(s,encoding='utf-8')
