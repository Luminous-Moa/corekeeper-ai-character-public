from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('$", 도구 부족 벽 {plan.toolShortSeen}개는 피함"', '(plan.sharedSearch ? $", 공유 검색에서 도구 부족 벽 {plan.toolShortSeen}개 관측" : $", 도구 부족 벽 {plan.toolShortSeen}개는 피함")');p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        public double MaxSliceMs;', '        public double MaxSliceMs;\n        public int SearchCount, CapturedTiles;\n        public long Expanded;')
s=s.replace('            Context.Batch = null;', '''            if (Context.Batch != null)
            {
                SearchCount = Context.Batch.SearchCount;
                CapturedTiles = Context.Batch.CapturedTileCount;
                Expanded = Context.Batch.ExpandedTotal;
            }
            Context.Batch = null;''')
s=s.replace('                job.LastProgressLogAt = now;', '''                job.LastProgressLogAt = now;
                if (job.Context.Batch != null)
                {
                    job.SearchCount = job.Context.Batch.SearchCount;
                    job.CapturedTiles = job.Context.Batch.CapturedTileCount;
                    job.Expanded = job.Context.Batch.ExpandedTotal;
                }''')
s=s.replace('work={job.Work.Elapsed.TotalMilliseconds:F1}ms maxSlice={job.MaxSliceMs:F2}ms', 'work={job.Work.Elapsed.TotalMilliseconds:F1}ms maxSlice={job.MaxSliceMs:F2}ms trees={job.SearchCount} expanded={job.Expanded} tiles={job.CapturedTiles}')
p.write_text(s,encoding='utf-8')
p=Path('Tools/path-preparation-tests/Program.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        public bool Changed;\n        public string Reason', '        public bool Changed;\n        public int SearchCount, CapturedTileCount;\n        public long ExpandedTotal;\n        public string Reason');p.write_text(s,encoding='utf-8')
