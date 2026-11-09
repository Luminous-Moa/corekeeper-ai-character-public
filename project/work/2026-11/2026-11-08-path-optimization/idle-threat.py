from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('        public AIObservation Observation;', '        public AIObservation Observation;\n        public readonly HashSet<Entity> Threats = new HashSet<Entity>();')
s=s.replace('                job.Observation = AIObservation.Take(server, player, true);','''                job.Observation = AIObservation.Take(server, player, true);
                foreach (var enemy in job.Observation.enemies)
                    if (enemy.dist <= Settings().combatRange && !AIEdge.LastAssessedCannotCome(enemy.entity)) job.Threats.Add(enemy.entity);''')
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig')
old='            _inCombat = EnemiesWithin(server, player, Settings().combatRange).Exists(x => AIEdge.ObserveThreat(server, player, x.e, x.id, server.EntityManager.GetComponentData<LocalTransform>(x.e).Position, x.d, Settings().combatRange));   // 단계 3'
new='''            var threats = new HashSet<Entity>();
            foreach (var enemy in EnemiesWithin(server, player, Settings().combatRange))
                if (AIEdge.ObserveThreat(server, player, enemy.e, enemy.id, server.EntityManager.GetComponentData<LocalTransform>(enemy.e).Position, enemy.d, Settings().combatRange)) threats.Add(enemy.e);
            _inCombat = threats.Count > 0;
            if (S.Preparation != null && S.Preparation.Observation != null && !S.Preparation.Threats.SetEquals(threats))
                CancelPreparation("idle threat set changed");'''
assert old in s;s=s.replace(old,new)
s=s.replace('if (dangerChanged || S._urgentCharge || S._needBreach && !preparing.Breach)', 'if (dangerChanged || S._urgentCharge || S._needBreach && !preparing.Breach || S._needEmergency && preparing.NeedReason != _needReason)')
p.write_text(s,encoding='utf-8')
# update the production scheduler test dependencies
p=Path('Tools/path-preparation-tests/Program.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('public float waitSeconds = 2f;', 'public float waitSeconds = 2f, combatRange = 8f;')
s=s.replace('    public int2 pos;', '    public int2 pos;\n    public readonly List<SeenEnemy> enemies = new List<SeenEnemy>();')
s=s.replace('public static class AIMemory', 'public sealed class SeenEnemy { public Entity entity; public float dist; }\npublic static class AIMemory')
s=s.replace('    public static bool PlanActive, BreachPending;', '    public static bool PlanActive, BreachPending;\n    public static bool LastAssessedCannotCome(Entity entity) => false;')
p.write_text(s,encoding='utf-8')
