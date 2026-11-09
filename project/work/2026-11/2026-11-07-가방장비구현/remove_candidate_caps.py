from pathlib import Path

ROOT=Path('C:/Dev/CoreKeeperModSDK-bag-common')
def edit(rel, pairs):
    p=ROOT/rel
    s=p.read_text(encoding='utf-8-sig')
    for old,new in pairs:
        if old not in s: raise RuntimeError(f'missing marker in {rel}: {old[:110]}')
        s=s.replace(old,new,1)
    p.write_text(s,encoding='utf-8',newline='\n')

edit('Assets/AIPlayerSpike/Scripts/Core/AICandidateAssembler.cs',[
    ('// recent failures and reversals (Mark), cut to 14, add "wait" and give choice values (Finish).', '// recent failures and reversals (Mark), keep every viable choice, add "wait" and give choice values (Finish).'),
    ('public const int Limit = 14;', 'public const int Limit = 0; // compatibility: 0 means no candidate count cut'),
    ('public static int LimitFor(bool combat, int combatMax) => combat ? (combatMax > 0 ? combatMax : 0) : Limit;', 'public static int LimitFor(bool combat, int combatMax) => 0;'),
    ('public const int EdgeShare = 4;', 'public const int EdgeShare = 0; // compatibility only: all edge options are retained'),
    ('        if (limit > 0 && list.Count > limit) edgeCut = Cut(list, limit);\n', '')
])
p=ROOT/'Assets/AIPlayerSpike/Scripts/Core/AICandidateAssembler.cs'
s=p.read_text(encoding='utf-8')
start=s.index('    // The cut to limit: positional')
end=s.index('    // recheck N4: a repair',start)
s=s[:start]+s[end:]
s=s.replace('    // Cut to the limit, add "wait" (the coordinator\'s object), give each a choice value from its target key (the same', '    // Keep all viable candidates, add "wait" (the coordinator\'s object), give each a choice value from its target key (the same')
s=s.replace('    // The cut of one build: Limit (14) out of combat; in combat (an enemy within combat range) none, unless the setting\n    // combatMaxCandidates gives one (user decision 2026-11-05, combat stage 2: "전투 선택지를 늘릴려는 거야"). 0 = no cut.', '    // Candidate count limits are removed by the 2026-11-07 user decision. Old settings remain readable but do not cut.')
p.write_text(s,encoding='utf-8',newline='\n')

edit('Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs',[
    ('near3 = o.enemies.GetRange(0, math.min(3, o.enemies.Count));','near3 = new List<SeenEnemy>(o.enemies);'),
    ('near3 = new List<SeenEnemy>(ins.GetRange(0, math.min(4, ins.Count)));','near3 = new List<SeenEnemy>(ins);'),
    ('foreach (var x in o.enemies) if (near3.Count < 4 && !near3.Contains(x)) near3.Add(x);','foreach (var x in o.enemies) if (!near3.Contains(x)) near3.Add(x);'),
    ('i < match.Count && i < 3','i < match.Count'),
    ('i < other.Count && i < 2','i < other.Count'),
    ('            int digTried = 0;\n',''),
    ('                if (lDig.Count - digEnemy >= 2 || digTried >= 4) break;\n',''),
    ('                digTried++;\n',''),
    ('i < brk.Count && i < 2','i < brk.Count'),
    ('                int nRoot = 0;\n',''),
    ('                    if (nRoot >= 2) break;\n',''),
    ('                    nRoot++;\n',''),
    ('AIMemory.RememberedOres(o.pos, wantedFirst, 12)','AIMemory.RememberedOres(o.pos, wantedFirst, 0)'),
    ('                    if (lRemember.Count >= 2) break;\n',''),
    ('                    if (rememberTried.Count >= 4) break;\n',''),
    ('            // Read every item before taking two pickups: nearby filler must not hide goal material or hunger food.\n            int digItem = 0;','            // Keep every observed executable pickup, including distinct walled-in targets.'),
    ('                else if (digItem == 0)','                else'),
    ('                    digItem++;\n',''),
    ('                if (lFollow.Count >= 2) break;\n',''),
    ('if (op.nov <= 0 || lDigExplore.Count >= 2) break;','if (op.nov <= 0) break;'),
    ('                    if (added >= 2) break;\n',''),
    ('// combat stage 2 (user decision 2026-11-05): no cut in combat (setting combatMaxCandidates > 0 gives one); 14 otherwise','// User 2026-11-07: keep all viable choices in and out of combat; old candidate-cap settings no longer cut.')
])
edit('Assets/AIPlayerSpike/Scripts/Core/AIPickupOptions.cs',[
    ('// before filling the two slots. Within each need, walking for an item comes before an item arriving by itself.', '// before the remaining viable items. Within each need, walking comes before an item arriving by itself.'),
    ('// One item can satisfy both needs; keep the other slot available for a distinct choice.', '// One item can satisfy both needs without duplicating its action key.'),
    ('while (selected.Count < 2)','while (true)')
])
edit('Assets/AIPlayerSpike/Scripts/Core/AIPlaceGrid.cs',[
    ('public const int MaxOptions = 3;','public const int MaxOptions = 0; // compatibility: all consequence representatives are offered'),
    ('    // first, at most `max`. Picks by kind so no kind of consequence is hidden behind a nearer one of another kind.','    // first. Every consequence representative is kept; the legacy max argument no longer cuts choices.'),
    ('public const int EdgeKeep = 2;', 'public const int EdgeKeep = 0;')
])
p=ROOT/'Assets/AIPlayerSpike/Scripts/Core/AIPlaceGrid.cs'
s=p.read_text(encoding='utf-8')
start=s.index('        if (list.Count > max)',s.index('public static List<PlaceOption> Pick'))
end=s.index('        return list;',start)
s=s[:start]+s[end:]
p.write_text(s,encoding='utf-8',newline='\n')
edit('Assets/AIPlayerSpike/Scripts/AIMemory.cs',[
    ('i < l.Count && i < max','i < l.Count && (max <= 0 || i < max)')
])
edit('Assets/AIPlayerSpike/Scripts/AIBase.cs',[
    ('                    if (n >= 2) break;\n',''),
    ('            if (res.Count >= 6) break;\n',''),
    ('        int n = 0;\n        foreach (var f in food)\n        {\n            if (n >= 3) break;', '        foreach (var f in food)\n        {'),
    ('            if (k == curKey) { n++; continue; }','            if (k == curKey) continue;'),
    ('            if (dup != null) { dup.desc = d; n++; continue; }','            if (dup != null) { dup.desc = d; continue; }'),
    ('            res.Add(new AICandidate { kind = "eat", key = k, itemId = f.id, itemVariation = f.variation, desc = d });\n            n++;', '            res.Add(new AICandidate { kind = "eat", key = k, itemId = f.id, itemVariation = f.variation, desc = d });')
])
edit('Assets/AIPlayerSpike/Scripts/AIFarm.cs', [('i < l.Count && i < 2','i < l.Count')])
edit('Assets/AIPlayerSpike/Scripts/AIEdge.cs', [('i < all.Count && i < 2','i < all.Count')])
print('candidate count-cut edits applied; semantic representatives, physical limits and internal path guards preserved')
