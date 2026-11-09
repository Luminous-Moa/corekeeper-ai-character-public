from pathlib import Path
p=Path('Tools/restriction-tests/prepare.mjs');s=p.read_text(encoding='utf-8-sig');s=s.replace("brain.indexOf('        public void Follow()')", "brain.indexOf('        public IEnumerable<int> FollowSteps()')")
s=s.replace("balanced(brain,'        public void RememberedOres()')", "balanced(brain,'        public IEnumerable<int> RememberedOresSteps()').replace('public IEnumerable<int> RememberedOresSteps()', 'public void RememberedOres()').replace(/\\s*foreach \\(var step in PreparePlan[^\\n]+yield return step;/g, '').replace('yield return 0;', '')")
p.write_text(s,encoding='utf-8')
