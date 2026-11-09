from pathlib import Path
p=Path('Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('        if (!em.Exists(player)) return;', '        if (!em.Exists(player)) { CancelPendingChoices("player entity gone"); return; }',1);p.write_text(s.rstrip()+'\n',encoding='utf-8')
p=Path('Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('            LogError("STEP 3: server player or thin world gone; stopping");', '            AIBrain.CancelPendingChoices("server player or thin world gone");\n            LogError("STEP 3: server player or thin world gone; stopping");');p.write_text(s,encoding='utf-8')
for name in ['Assets/AIPlayerSpike/Scripts/AIBrain.cs','Assets/AIPlayerSpike/Scripts/AIDecisionsApi.cs','Tools/core-tests/ChoiceFactsTests.cs']:
 p=Path(name);p.write_text(p.read_text(encoding='utf-8-sig').rstrip()+'\n',encoding='utf-8')
p=Path('Tools/path-preparation-tests/PathPreparationTests.csproj');s=p.read_text();s=s.replace('<Nullable>disable</Nullable>', '<Nullable>disable</Nullable>\n    <NoWarn>CS0649;CS8981</NoWarn>');p.write_text(s)
