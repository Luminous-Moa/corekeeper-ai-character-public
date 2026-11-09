import fs from 'node:fs';
import {randomUUID} from 'node:crypto';
const root='C:/Dev/CoreKeeperModSDK-refactor-20261106/Assets/AIPlayerSpike/Scripts/';
const read=p=>fs.readFileSync(root+p,'utf8').replaceAll('\r\n','\n');
function write(p,s){fs.writeFileSync(root+p,s);if(!fs.existsSync(root+p+'.meta'))fs.writeFileSync(root+p+'.meta','fileFormatVersion: 2\nguid: '+randomUUID().replaceAll('-','')+'\n');}
function rep(s,a,b){if(!s.includes(a))throw Error('missing '+a);return s.replace(a,b);}
let brain=read('AIBrain.cs');
brain=brain.split('\n').filter(l=>!l.includes('bool _buildPrefetch')&&!l.includes('bool _buildBreach')).join('\n');
write('AIBrain.cs',brain);
let cand=read('AIBrain.Candidates.cs');
cand=rep(cand,'World server; Entity player; AIObservation o;','World server; Entity player; AIObservation o;\n        readonly bool prefetch, breachCall;');
cand=rep(cand,'public CandBuild(World server, Entity player, AIObservation o) { this.server = server; this.player = player; this.o = o; }','public CandBuild(World server, Entity player, AIObservation o, bool prefetch, bool breachCall) { this.server = server; this.player = player; this.o = o; this.prefetch = prefetch; this.breachCall = breachCall; }');
cand=cand.replaceAll('_buildPrefetch','prefetch').replaceAll('_buildBreach','breachCall');
cand=rep(cand,'lOther, lBrute, lDoor, lEdge, inCombat);','lOther, lBrute, lDoor, lEdge, inCombat, prefetch);');
cand=rep(cand,'return BuildCandidates(server, player, o);','return BuildCandidates(server, player, o, prefetch, breachCall);');
write('AIBrain.Candidates.cs',cand);
let loop=read('AIBrain.Loop.cs');
loop=rep(loop,'        _buildPrefetch = prefetch;\n','');
loop=rep(loop,'_buildBreach = breach || AIEdge.PlanActive && AIEdge.BreachPending;','bool breachCall = breach || AIEdge.PlanActive && AIEdge.BreachPending;');
loop=rep(loop,'try { cands = BuildCandidates(server, player, o); } finally { _buildPrefetch = false; _building = null; }','try { cands = BuildCandidates(server, player, o, prefetch, breachCall); } finally { _building = null; }');
loop=rep(loop,'private static List<AICandidate> BuildCandidates(World server, Entity player, AIObservation o)','private static List<AICandidate> BuildCandidates(World server, Entity player, AIObservation o, bool prefetch, bool breachCall)');
loop=rep(loop,'new CandBuild(server, player, o);','new CandBuild(server, player, o, prefetch, breachCall);');
loop=rep(loop,'List<AICandidate> lEdge, bool combat)','List<AICandidate> lEdge, bool combat, bool prefetch)');
loop=loop.replaceAll('_buildPrefetch','prefetch');write('AIBrain.Loop.cs',loop);
write('Core/AIThreatHistory.cs',`using System.Collections.Generic;

// Previous assessments, scoped by the owning character state. Out-of-range observations do not call Record.
public sealed class AIThreatHistory<T>
{
    private readonly HashSet<T> cannotCome = new HashSet<T>();
    public void Record(T enemy, bool isThreat)
    {
        if (cannotCome.Count > 64) cannotCome.Clear();
        if (isThreat) cannotCome.Remove(enemy); else cannotCome.Add(enemy);
    }
    public bool LastAssessedCannotCome(T enemy) => cannotCome.Contains(enemy);
}
`);
write('AIEdge.cs',rep(read('AIEdge.cs'),'public readonly HashSet<Entity> cantCome = new HashSet<Entity>();','public readonly AIThreatHistory<Entity> threatHistory = new AIThreatHistory<Entity>();'));
let reach=read('AIEdge.Reach.cs');
reach=rep(reach,'public static bool Threat(World server, Entity player, Entity e, ObjectID id, float3 pos, float dist, float range)','public static bool? AssessThreatWithinRange(World server, Entity player, Entity e, ObjectID id, float3 pos, float dist, float range)');
reach=rep(reach,'if (dist > range) return false;','if (dist > range) return null;');
reach=rep(reach,'        if (S.cantCome.Count > 64) S.cantCome.Clear();\n        if (t) S.cantCome.Remove(e); else S.cantCome.Add(e);\n','');
reach=rep(reach,'    public static bool AnyThreat(',`    public static bool ObserveThreat(World server, Entity player, Entity e, ObjectID id, float3 pos, float dist, float range)
    {
        var assessment = AssessThreatWithinRange(server, player, e, id, pos, dist, range);
        if (!assessment.HasValue) return false;
        S.threatHistory.Record(e, assessment.Value);
        return assessment.Value;
    }

    public static bool ObserveAnyThreat(`);
reach=rep(reach,'if (Threat(server, player, e.entity, e.id, e.pos, e.dist, range))','if (ObserveThreat(server, player, e.entity, e.id, e.pos, e.dist, range))');
reach=rep(reach,'    public static bool CannotCome(Entity e) => S.cantCome.Contains(e);',`    public static bool LastAssessedCannotCome(Entity e) => S.threatHistory.LastAssessedCannotCome(e);

    // Source-compatible entry points; internal callers name whether they observe or read history.
    public static bool Threat(World server, Entity player, Entity e, ObjectID id, float3 pos, float dist, float range) => ObserveThreat(server, player, e, id, pos, dist, range);
    public static bool AnyThreat(World server, Entity player, List<SeenEnemy> enemies, float range) => ObserveAnyThreat(server, player, enemies, range);
    public static bool CannotCome(Entity e) => LastAssessedCannotCome(e);`);
write('AIEdge.Reach.cs',reach);
for(const p of ['AIBrain.Candidates.cs','AIBrain.Loop.cs'])write(p,read(p).replaceAll('AIEdge.AnyThreat(', 'AIEdge.ObserveAnyThreat(').replaceAll('AIEdge.Threat(', 'AIEdge.ObserveThreat(').replaceAll('AIEdge.CannotCome(', 'AIEdge.LastAssessedCannotCome('));
write('AIEdge.Guard.cs',rep(read('AIEdge.Guard.cs'),'bool th = Threat(server, player, e.entity, e.id, e.pos, e.dist, AIBrain.Settings().combatRange);','bool th = ObserveThreat(server, player, e.entity, e.id, e.pos, e.dist, AIBrain.Settings().combatRange);'));
