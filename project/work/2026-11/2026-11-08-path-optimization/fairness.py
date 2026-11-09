from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('    private static int activePreparations;', '    private static int activePreparations;\n    private static Preparation waitingForBudget;')
s=s.replace('        private bool disposed;', '        internal bool Disposed;\n        internal int LastTickFrame;')
s=s.replace('if (disposed) return;', 'if (Disposed) return;').replace('            disposed = true;', '            Disposed = true;\n            if (waitingForBudget == this) waitingForBudget = null;')
s=s.replace('        if (job == null) return;\n        if (job.World', '        if (job == null) return;\n        job.LastTickFrame = Time.frameCount;\n        if (job.World')
s=s.replace('        if (preparationFrameUsed >= PreparationFrameMs) return;', '''        // An AI denied the previous frame gets the first turn this frame. Skip a
        // departed owner after a frame so a stopped/disconnected AI cannot hold the queue.
        if (waitingForBudget != null && (waitingForBudget.Disposed || Time.frameCount - waitingForBudget.LastTickFrame > 1)) waitingForBudget = null;
        if (waitingForBudget != null && waitingForBudget != job) return;
        if (preparationFrameUsed >= PreparationFrameMs)
        {
            if (waitingForBudget == null) waitingForBudget = job;
            return;
        }
        waitingForBudget = null;''')
p.write_text(s,encoding='utf-8')
# Fixed-order regression test executes the actual scheduler with deliberately over-budget callbacks.
p=Path('Tools/path-preparation-tests/Program.cs');s=p.read_text(encoding='utf-8-sig');mark='        foreach (string change in new[]'
pos=s.index(mark)
s=s[:pos]+'''        var fixedA = new State(51); var fixedB = new State(52);
        Begin(fixedA, world, player); Begin(fixedB, world, new Entity(2));
        for (int frame = 0; frame < 8; frame++)
        {
            Tick(fixedA, world, player);
            Tick(fixedB, world, new Entity(2), false);
        }
        Program.Check(fixedA.Completions == 1 && fixedB.Completions == 1 && fixedA.Moves == 2 && fixedB.Moves == 2,
            "fixed a-then-b frame order gives both AIs progress despite indivisible 5ms units");

'''+s[pos:]
p.write_text(s,encoding='utf-8')
