from pathlib import Path
root=Path('C:/Dev/CoreKeeperModSDK-bag-common')
p=root/'Assets/AIPlayerSpike/Scripts/Core/AICandidateAssembler.cs'
s=p.read_text(encoding='utf-8')
start=s.index('            // Close fight with a goal')
end=s.index('            var goalSide',start)
s=s[:start]+'''            // Close fight still puts combat first. Every executable goal-progress alternative follows, so the
            // AI can choose whether and where to resume the goal; no one-goal candidate quota remains.
'''+s[end:]
s=s.replace('    // and have their own share of the cut (EdgeShare), so they never push a progress option out of the 14. edgeGoal: a\n    // goal whose work is the edge (none yet, 단계 4): then they keep the craft place (before the goal lists).', '    // without displacing any other option. edgeGoal: a goal whose work is the edge (none yet, 단계 4): then\n    // they keep the craft place (before the goal lists).')
s=s.replace('// the edge share never takes the place of', '// historical priority metadata; choices are no longer displaced by a count quota')
s=s.replace('// and have their own share of the cut (EdgeShare)', '// and are kept without a count quota')
s=s.replace('// "continue" (a progress option is never pushed out by an edge option).', '// "continue". Historical quota metadata is retained for record compatibility only.')
s=s.replace('// limit <= 0: no cut ("wait" is added all the same).', '// The legacy limit parameter is ignored: every viable choice and wait are retained.')
s=s.replace('// edgeCut (recheck N4): the edge options the cut left out (the build says how many and of what kind, EdgeCutNote)', '// edgeCut remains empty for caller compatibility; no candidate is omitted by a numeric limit.')
s=s.replace('// In combat (an enemy within combat range (combat stage 2: no cut to Limit, see LimitFor)', '// In combat (an enemy within combat range (see LimitFor)')
p.write_text(s,encoding='utf-8',newline='\n')
p=root/'Assets/AIPlayerSpike/Scripts/Core/AIPlaceGrid.cs';s=p.read_text(encoding='utf-8').replace('public const int EdgeKeep = 0;   // 실게임2 S-c: edge-tile kinds kept past MaxOptions','public const int EdgeKeep = 0;   // legacy field; every consequence is retained')
p.write_text(s,encoding='utf-8',newline='\n')
p=root/'Tools/core-tests/AssemblerReplayTests.cs';s=p.read_text(encoding='utf-8')
a=s.index('// S6b:');b=s.index('using System;',a)
s='''// Historical L1 inputs/outputs remain unchanged. This replay projects the new no-count-cut / all-qualified-goal
// contract onto fields the old one-goal/14-cut execution actually recorded. Newly retained goal-side physical
// feasibility is not inferred from the old log; explicit current-contract cases below check it independently.
'''+s[b:]
s=s.replace('L1 corpus replay: {same} builds exactly the same as the old code, {diff} different','L1 historical projection: {same} recorded builds retain the recorded choices and fields, {diff} different')
s=s.replace('out-of-combat builds exactly the same as the old code (cut 14)','out-of-combat historical projections retain the old recorded fields')
s=s.replace('LimitFor(...) = combat stage 2). With a limit other than the old one, a combat build whose','LimitFor(...) now returns no cut). An old build whose')
p.write_text(s,encoding='utf-8',newline='\n')