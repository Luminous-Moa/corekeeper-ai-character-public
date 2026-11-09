from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs');s=p.read_text(encoding='utf-8-sig')
needle='        bool wantExplore; List<string> gfacts;'
s=s.replace(needle,needle+'''
        readonly ExpeditionQuery query = AIExpedition.BeginBuild();
        private sealed class DeferredRoute
        {
            public string Key;
            public int2 Target;
            public Action<TunnelPlan, string> Complete;
        }
        readonly List<DeferredRoute> deferredRoutes = new List<DeferredRoute>();
        private bool DirectRoute(string key) =>
            lCraft.Exists(c => c.key == key) || lEnemy.Exists(c => c.key == key) ||
            lEdge.Exists(c => c.key == key) || lCont.Exists(c => c.key == key);
        private void DeferRoute(string key, int2 target, string label, Action<TunnelPlan, string> complete)
        {
            if (DirectRoute(key)) return;
            var route = new DeferredRoute { Key = key, Target = target, Complete = complete };
            deferredRoutes.Add(route);
            query.Add(key, "travel", "dig", target.x, target.y, label, route);
        }
        public IEnumerable<int> PreparePublishedRoutes()
        {
            foreach (var pool in new[] { lMine, lOther, lItem, lDig })
                foreach (var candidate in pool)
                {
                    if (DirectRoute(candidate.key) || candidate.kind == "gather_group" || candidate.entity != Entity.Null) continue;
                    bool gather = candidate.kind == "mine" || candidate.kind == "root" || candidate.kind == "break" || candidate.kind == "pickup" || candidate.kind == "edgepickup";
                    if (gather || candidate.kind == "dig")
                        query.Add(candidate.key, gather ? "gather" : "travel", gather ? candidate.itemId.ToString() : candidate.kind,
                            candidate.tile.x, candidate.tile.y, candidate.targetLabel ?? candidate.desc, candidate);
                    yield return 0;
                }
            query.Seal(128);
            _building.Query = query;
            int requested = 0;
            foreach (var route in deferredRoutes) if (query.ShouldPrepare(route.Key)) requested++;
            _building.RouteCountHint = requested;
            foreach (var route in deferredRoutes)
            {
                if (!query.ShouldPrepare(route.Key)) continue;
                foreach (var step in PreparePlan(server, player, route.Target)) yield return step;
                var plan = PlanC(server, player, route.Target, out string why);
                route.Complete(plan, why);
                yield return 0;
            }
        }
''')
# Replace only the large route producers. Their original result rendering remains the callback.
for begin, end, key, target, label in [
 ('                    foreach (var step in PreparePlan(server, player, r.p))', '                }\n            }\n            yield return 0;', 'k','r.p','$"기억한 광석 {r.o.ore} ({r.p.x},{r.p.y})"'),
 ('                    foreach (var step in PreparePlan(server, player, f.t))', '                }\n                _exploreFrontierNote', 'k','f.t','$"{f.dir}쪽 기억한 탐색 끝 ({f.t.x},{f.t.y})"'),
 ('                foreach (var step in PreparePlan(server, player, t.p))', '            }\n            yield return 0;', 'k','t.p','$"보이는 채집 대상 {t.what} ({t.p.x},{t.p.y})"')]:
 a=s.index(begin);b=s.index(end,a);part=s[a:b]
 lines=part.splitlines();indent=lines[0][:len(lines[0])-len(lines[0].lstrip())]
 # Remove warming/read, carry the existing failure and success body into a deferred callback.
 body='\n'.join(lines[2:]).replace(' continue; }', ' return; }')
 if target=='f.t': body=body.replace('                    added++;','')
 replacement=indent+f'DeferRoute({key}, {target}, {label}, (plan, why) =>\n'+indent+'{\n'+body+'\n'+indent+'});\n'+indent+'yield return 0;\n'
 if target=='f.t': replacement+=indent+'added++;\n'
 s=s[:a]+replacement+s[b:]
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        public AITunnel.PlanBatch Batch;', '        public AITunnel.PlanBatch Batch;\n        public ExpeditionQuery Query;\n        public int RouteCountHint;');p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        List<AICandidate> candidates = null;','        foreach (var step in b.PreparePublishedRoutes()) yield return step;\n        List<AICandidate> candidates = null;',1)
s=s.replace('candidates = AIExpedition.Compose(server, player, o, candidates);','candidates = AIExpedition.Compose(server, player, o, candidates, _building?.Query);')
s=s.replace('InventorySelectionPending = AIInventoryMenu.Active || AIExpedition.Active,','InventorySelectionPending = AIInventoryMenu.Active || AIExpedition.Active || (_building?.Query?.Count ?? 0) > 0,')
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('_building.Plans.Count >= 8', '(_building.Plans.Count >= 8 || _building.RouteCountHint >= 8)');s=s.replace('            Context.Batch = null;', '            Context.Batch = null;\n            Context.Query = null;');p.write_text(s,encoding='utf-8')
