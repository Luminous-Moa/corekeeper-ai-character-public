from pathlib import Path
import re, uuid
root=Path('C:/Dev/CoreKeeperModSDK-choice-fixes')
s=root/'Assets/AIPlayerSpike/Scripts'
for p in s.glob('*.cs'):
    t=p.read_text(encoding='utf-8-sig')
    old=t
    t=re.sub(r'(\w+)\.path\.Count / 2\.4f \+ \1\.totalHits \* 0\.5f',r'\1.EstimatedSeconds',t)
    if p.name=='AIBody.cs':
        t=t.replace('const float WalkSpeed = 5.7f;', 'const float WalkSpeed = AIMoveEstimate.WalkSpeed;')
    if p.name=='AIBrain.Candidates.cs':
        t=t.replace('const float WalkTilesPerSecond = 2.4f, SecondsPerHit = 0.5f;   // observed in ai2 (report §1, §4.4)',
                    'const float WalkTilesPerSecond = AIMoveEstimate.WalkSpeed, SecondsPerHit = AIMoveEstimate.SecondsPerHit;')
        t=t.replace('var c = new AICandidate { kind = "mine", key = k, tile = w.p, progress = isMatch && est > red,',
                    'string risk = AIWallData.RiskText(server, obj);\n                var c = new AICandidate { kind = "mine", key = k, tile = w.p, progress = isMatch && est > red,')
        t=t.replace('길이 막히면 실패.{(AIEdge.EdgeWallNote(w.p)', '길이 막히면 실패.{risk}{(AIEdge.EdgeWallNote(w.p)')
    if p.name=='AIPlaces.cs':
        t=t.replace('AITunnelSearch.EstTiles(plan.path.Count, plan.totalHits), plan.alt.path.Count, AITunnelSearch.EstTiles(plan.alt.path.Count, plan.alt.totalHits)',
                    'plan.EstimatedSeconds, plan.alt.path.Count, plan.alt.EstimatedSeconds')
    if p.name=='AIMineSkill.cs':
        t=t.replace('if (_phase == Phase.Approach) return $"벽 {_wallObj} ({_target.x},{_target.y})로 다가가는 중 {d:F1}칸";',
                    'string risk = AIWallData.RiskText(server, _wallObj);\n        if (_phase == Phase.Approach) return $"벽 {_wallObj} ({_target.x},{_target.y})로 다가가는 중 {d:F1}칸{risk}";')
        t=t.replace('캐는 중, 벽 체력 {(hp >= 0 ? hp : _hp)}/{_hp}";', '캐는 중, 벽 체력 {(hp >= 0 ? hp : _hp)}/{_hp}{risk}";')
    if t!=old: p.write_text(t,encoding='utf-8')
p=s/'Core/AIBaseGuard.cs'
t=p.read_text(encoding='utf-8-sig')
t=t.replace('// both (est in walking tiles, AITunnelSearch.EstTiles; 2.4 tiles a second), the edge sentences of the walls it mines.',
            '// both (seconds from AIMoveEstimate, independent of route selection scores), and the edge facts.')
t=t.replace('int firstTiles, float firstEst, int edgeTiles, float edgeEst,', 'int firstTiles, float firstSeconds, int edgeTiles, float edgeSeconds,')
t=t.replace('{firstEst / 2.4f:F0}', '{firstSeconds:F0}').replace('{edgeEst / 2.4f:F0}', '{edgeSeconds:F0}')
p.write_text(t,encoding='utf-8')
p=s/'Core/AITunnelSearch.cs'
t=p.read_text(encoding='utf-8-sig').replace('// one hit (~0.5 s) in walking tiles (~2.4 tiles/s), as the options\' time estimate',
    '// legacy route-selection weight; displayed seconds are separate (AIMoveEstimate)')
p.write_text(t,encoding='utf-8')
for n in ['AIMoveEstimate','AIPickupOptions','AIWallRisk']:
    (s/f'Core/{n}.cs.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
p=root/'Tools/core-tests/Program.cs'
t=p.read_text(encoding='utf-8-sig').replace('("CW", ContinueWarningTests.Run),','("CW", ContinueWarningTests.Run),\n            ("CHOICE", ChoiceFactsTests.Run),')
p.write_text(t,encoding='utf-8')
p=root/'Tools/core-tests/PlaceGoalTests.cs'
t=p.read_text(encoding='utf-8-sig').replace('DetourFact(true, 30, 31f, 12, 14f,', 'DetourFact(true, 30, AIMoveEstimate.Seconds(30, 2), 12, AIMoveEstimate.Seconds(12, 4),')
t=t.replace('문으로 돌아감 30칸·약 13초(둘레 (10,0)를 파면 12칸·약 6초,', '문으로 돌아감 30칸·약 6초(둘레 (10,0)를 파면 12칸·약 4초,')
p.write_text(t,encoding='utf-8')
