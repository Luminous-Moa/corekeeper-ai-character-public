from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs')
s=p.read_text(encoding='utf-8-sig')
# Preserve synchronous adapters; production drains these iterators under the per-frame scheduler.
methods=['DigOre','Breakables','RememberedOres','Items','Follow','Explore']
for name in methods:
 start=s.index('        public void '+name+'()')
 end=s.find('\n        public ',start+1)
 if name=='Explore': end=s.index('\n        void FindBrute',start) if '\n        void FindBrute' in s[start:] else s.index('\n        private ',start)
 body=s[start:end]
 body=body.replace('public void '+name+'()', 'public IEnumerable<int> '+name+'Steps()')
 import re
 # These blocks contain no early method return and every PlanC is a simple local assignment.
 body=re.sub(r'(\s*)(var \w+ = PlanC\(server, player, (.+?), out string \w+\);)',lambda m:m.group(1)+'foreach (var step in PreparePlan(server, player, '+m.group(3)+')) yield return step;'+m.group(1)+m.group(2),body)
 # Always yield at least once even on an empty block.
 pos=body.rfind('        }')
 body=body[:pos]+'            yield return 0;\n'+body[pos:]
 s=s[:start]+body+s[end:]
# Assemble has a callback result. Warm every surviving route before the final fact passes.
s=s.replace('public List<AICandidate> Assemble()', 'public IEnumerable<int> AssembleSteps(Action<List<AICandidate>> complete)')
s=s.replace('try { return BuildCandidates(server, player, o, prefetch, breachCall); } finally { _rebuildingForTool = false; }','try { foreach (var step in BuildCandidateSteps(server, player, o, prefetch, breachCall, complete)) yield return step; } finally { _rebuildingForTool = false; }\n                yield break;')
s=s.replace('            // 제작대-놓기 실게임2 P2:', '            foreach (var candidate in list)\n                if (candidate.kind == "dig")\n                    foreach (var step in PreparePlan(server, player, candidate.tile, candidate.digDetour)) yield return step;\n            // 제작대-놓기 실게임2 P2:')
s=s.replace('            return list;\n        }\n    }', '            complete(list);\n            yield return 0;\n        }\n    }')
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig')
start=s.index('    private static List<AICandidate> BuildCandidates(');end=s.index('    // This is the public choice surface.',start)
body=s[start:end]
body=body.replace('private static List<AICandidate> BuildCandidates(World server, Entity player, AIObservation o, bool prefetch, bool breachCall)','private static IEnumerable<int> BuildCandidateSteps(World server, Entity player, AIObservation o, bool prefetch, bool breachCall, Action<List<AICandidate>> complete)')
for name in methods:body=body.replace('b.'+name+'();','foreach (var step in b.'+name+'Steps()) yield return step;')
body=body.replace('var candidates = b.Assemble();','List<AICandidate> candidates = null;\n        foreach (var step in b.AssembleSteps(result => candidates = result)) yield return step;')
body=body.replace('return candidates;', 'complete(candidates);')
wrapper='''    private static List<AICandidate> BuildCandidates(World server, Entity player, AIObservation o, bool prefetch, bool breachCall)
    {
        List<AICandidate> result = null;
        foreach (var step in BuildCandidateSteps(server, player, o, prefetch, breachCall, value => result = value)) { }
        return result;
    }

'''
s=s[:start]+wrapper+body+s[end:]
p.write_text(s,encoding='utf-8')
