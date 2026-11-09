from pathlib import Path
root=Path('C:/Dev/CoreKeeperModSDK-unrestricted')
p=root/'Assets/AIPlayerSpike/Scripts/Core/AIGoalKinds.cs';s=p.read_text(encoding='utf-8').replace('GoalItemMode.Required, 9999,','GoalItemMode.Required, int.MaxValue,');p.write_text(s,encoding='utf-8')
p=root/'Tools/ai-bridge/test/fixtures/goal-contract.json';s=p.read_text(encoding='utf-8').replace('"max": 9999','"max": 2147483647').replace('"plannerMaxCount": 9999','"plannerMaxCount": 2147483647');p.write_text(s,encoding='utf-8')
for n in ['AIBase.cs','AIBrain.Candidates.cs']:
 p=root/'Assets/AIPlayerSpike/Scripts'/n;s=p.read_text(encoding='utf-8');p.write_text('\n'.join(l.rstrip() for l in s.split('\n')),encoding='utf-8')
