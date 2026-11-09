from pathlib import Path
root=Path('C:/Dev/CoreKeeperModSDK-unrestricted')
p=root/'Assets/AIPlayerSpike/Scripts/AITunnel.cs'; s=p.read_text(encoding='utf-8');s='\n'.join(l for l in s.split('\n') if ': false ? ""' not in l);s=s.replace('int2 nb = c + d;','if ((d.x > 0 && c.x == int.MaxValue) || (d.x < 0 && c.x == int.MinValue) || (d.y > 0 && c.y == int.MaxValue) || (d.y < 0 && c.y == int.MinValue)) continue;\n                    int2 nb = c + d;');p.write_text(s,encoding='utf-8')
p=root/'Assets/AIPlayerSpike/Scripts/Core/AITunnelSearch.cs';s=p.read_text(encoding='utf-8');s=s.replace('var n = (c.Item1 + dx[d], c.Item2 + dy[d]);','long nx = (long)c.Item1 + dx[d], ny = (long)c.Item2 + dy[d];\n                if (nx < int.MinValue || nx > int.MaxValue || ny < int.MinValue || ny > int.MaxValue) continue;\n                var n = ((int)nx, (int)ny);');p.write_text(s,encoding='utf-8')
p=root/'Assets/AIPlayerSpike/Scripts/AINavigator.cs';s=p.read_text(encoding='utf-8');s=s.replace('int bi = 0, bf = int.MaxValue;','int bi = 0; long bf = long.MaxValue;');s=s.replace('int f = g[open[i]] + math.abs(open[i].x - goal.x) + math.abs(open[i].y - goal.y);','long f = g[open[i]] + System.Math.Abs((long)open[i].x - goal.x) + System.Math.Abs((long)open[i].y - goal.y);');s=s.replace('int2 n = c + d;','if ((d.x > 0 && c.x == int.MaxValue) || (d.x < 0 && c.x == int.MinValue) || (d.y > 0 && c.y == int.MaxValue) || (d.y < 0 && c.y == int.MinValue)) continue;\n                int2 n = c + d;');p.write_text(s,encoding='utf-8')
p=root/'Assets/AIPlayerSpike/Scripts/AIPlaces.cs';s=p.read_text(encoding='utf-8');s=s.replace('foreach (var p in shape) if (!Holds(server, tr, item, p)) res.Add(p);','''foreach (var p in shape)
        {
            if (!Holds(server, tr, item, p)) { res.Add(p); continue; }
            if (!S.Placed.Exists(k => k.item == item && math.all(k.at == p))) NoteSkip(p, $"이미 {item} 놓여 있음");
        }''');p.write_text(s,encoding='utf-8')
