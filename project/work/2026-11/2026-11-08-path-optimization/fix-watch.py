from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('''        if (S.Preparation != null)
        {
            StepPreparation(server, player, now);
            return;
        }
''','',1)
s=s.replace('if (running && !AIInventoryMenu.Active && !_needCall', 'if (S.Preparation == null && running && !AIInventoryMenu.Active && !_needCall',1)
mark='        if (!_needCall || (now < _retryAt'
pos=s.index(mark)
s=s[:pos]+'''        if (S.Preparation != null)
        {
            var preparing = S.Preparation;
            int drop = _inCombat ? Settings().combatHpDropTrigger : Settings().hpDropTrigger;
            bool dangerChanged = preparing.Observation != null &&
                (preparing.Combat != _inCombat || AIVitals.LostSince(preparing.Observation.health, hpNow) >= drop);
            if (dangerChanged || S._urgentCharge || S._needBreach && !preparing.Breach)
                CancelPreparation("danger changed while preparing");
            else { StepPreparation(server, player, now); return; }
        }

'''+s[pos:]
s=s.replace('        _needCall = false;\n        _hpAtLastCall = o.health;', '        _needCall = prepared.NeedReason != _needReason;\n        _hpAtLastCall = o.health;')
# The normal operational log is a summary, full facts remain on the wire and in the opt-in decision log.
s=s.replace('        foreach (var c in cands) sb.Append($" | {c.value} {Trim(AIContinueText.ForShow(c.desc), 120)}");','        sb.Append($" | options={cands.Count} (full facts in request; decisionlog.on records them)");')
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('public readonly bool Prefetch, Emergency, Breach;', 'public readonly bool Prefetch, Emergency, Breach, Combat;\n        public readonly string NeedReason;')
s=s.replace('GoalRevision = Revision; ExecutionRevision = S.ExecutionRevision;', 'GoalRevision = Revision; ExecutionRevision = S.ExecutionRevision; Combat = _inCombat; NeedReason = _needReason;')
p.write_text(s,encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('found {fr.Count} [{string.Join("; ", fr.ConvertAll(x => $"{x.dir} ({x.t.x},{x.t.y}) cost {x.cost} nov {x.novelty}"))}], offered {added}', 'found {fr.Count}, offered {added}')
s=s.replace('catch (Exception e) { AIPlayerSpikeMod.LogError("brain: edge fact failed", e); }','catch (Exception e) { AIPlayerSpikeMod.LogError("brain: edge fact failed", e); }\n                    yield return 0;')
s=s.replace('catch (Exception e) { AIPlayerSpikeMod.LogError("brain: wall risk failed", e); }','catch (Exception e) { AIPlayerSpikeMod.LogError("brain: wall risk failed", e); }\n                    yield return 0;')
p.write_text(s,encoding='utf-8')
