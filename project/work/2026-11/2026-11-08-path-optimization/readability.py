from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Preparation.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        if (_building != null && okC) { _nPlansCached++; AITunnel.LastToolShortRed = _building.PlanToolRed.TryGetValue(k, out int red) ? red : c.plan?.toolShortRed ?? 0; why = c.why; return c.plan; }','''        if (_building != null && okC)
        {
            _nPlansCached++;
            AITunnel.LastToolShortRed = _building.PlanToolRed.TryGetValue(k, out int red) ? red : c.plan?.toolShortRed ?? 0;
            why = c.why;
            return c.plan;
        }''');p.write_text(s.rstrip()+'\n',encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('if (dangerChanged || S._urgentCharge || S._needBreach && !preparing.Breach || S._needEmergency && preparing.NeedReason != _needReason)', 'if (dangerChanged || S._urgentCharge || (S._needBreach && !preparing.Breach) ||\n                (S._needEmergency && preparing.NeedReason != _needReason))');p.write_text(s.rstrip()+'\n',encoding='utf-8')
