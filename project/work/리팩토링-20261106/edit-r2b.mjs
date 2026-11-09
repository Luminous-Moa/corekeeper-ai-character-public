import fs from 'node:fs';
const root='C:/Dev/CoreKeeperModSDK-refactor-20261106/';
function edit(p,fn){let s=fs.readFileSync(root+p,'utf8').replaceAll('\r\n','\n');s=fn(s);fs.writeFileSync(root+p,s);}
function rep(s,a,b){if(!s.includes(a))throw Error('missing '+a);return s.replace(a,b);}
const S='Assets/AIPlayerSpike/Scripts/';
edit(S+'AIBrain.cs',s=>rep(rep(s,'public bool _needEmergency, _reqEmergency;','public bool _needBreach;   // breach meaning belongs to the pending reason\n        public bool _needEmergency, _reqEmergency;'),'S._needEmergency = false; } }','S._needEmergency = false; S._needBreach = false; } }'));
edit(S+'AIBrain.Loop.cs',s=>{
 s=rep(s,'if (br != null) { _needReason = _needCall ? AIBaseGuard.AddReason(_needReason, br) : br; _needCall = true; }','if (br != null) { _needReason = _needCall ? AIBaseGuard.AddReason(_needReason, br) : br; S._needBreach = true; _needCall = true; }');
 s=rep(s,'Send(server, player, now, _needReason, false, S._needEmergency);','Send(server, player, now, _needReason, false, S._needEmergency, S._needBreach);');
 s=rep(s,'bool prefetch = false, bool emergency = false)','bool prefetch = false, bool emergency = false, bool breach = false)');
 return rep(s,'_buildBreach = (trigger ?? "").Contains("기지 둘레 부서짐") || AIEdge.PlanActive && AIEdge.BreachPending;','_buildBreach = breach || AIEdge.PlanActive && AIEdge.BreachPending;');
});
edit(S+'Core/AIBaseGuard.cs',s=>{
 s=rep(s,'// One cell of the walk grid around the AI.',`// Wall meaning is independent of the displayed Korean sentence.
public enum EdgeWallKind { None, BoundaryWall, DoorSideWall, HungDoorSide }
public readonly struct EdgeWallFact
{
    public EdgeWallKind Kind { get; }
    public (int x, int y) Door { get; }
    public EdgeWallFact(EdgeWallKind kind, (int x, int y) door = default) { Kind = kind; Door = door; }
    public bool OpensEdge => Kind == EdgeWallKind.BoundaryWall || Kind == EdgeWallKind.HungDoorSide;
    public string Text => Kind == EdgeWallKind.BoundaryWall ? AIBaseGuard.EdgeWallText
        : Kind == EdgeWallKind.DoorSideWall ? AIBaseGuard.DoorSideWallText
        : Kind == EdgeWallKind.HungDoorSide ? AIBaseGuard.DoorFallText(Door) : "";
}

// One cell of the walk grid around the AI.`);
 s=rep(s,'public static string EdgeWallNote(BaseShape s, (int x, int y) t)\n    {\n        if (s == null) return "";','public static string EdgeWallNote(BaseShape s, (int x, int y) t) => WallFact(s, t).Text;\n    public static EdgeWallFact WallFact(BaseShape s, (int x, int y) t)\n    {\n        if (s == null) return default;');
 s=rep(s,'if (s.hungDoor.HasValue && s.doorSide.Contains(k)) return DoorFallText(s.hungDoor.Value);\n        return s.sides.Contains(k) ? DoorSideWallText : s.edgeSet.Contains(k) && !s.inside.Contains(k) ? EdgeWallText : "";','if (s.hungDoor.HasValue && s.doorSide.Contains(k)) return new EdgeWallFact(EdgeWallKind.HungDoorSide, s.hungDoor.Value);\n        return new EdgeWallFact(s.sides.Contains(k) ? EdgeWallKind.DoorSideWall : s.edgeSet.Contains(k) && !s.inside.Contains(k) ? EdgeWallKind.BoundaryWall : EdgeWallKind.None);');
 s=rep(s,'    // a mine / dig option on this tile opens the edge (counted for "N of them are edge walls")\n    public static bool OpensEdgeNote(string note) => note == EdgeWallText || note.StartsWith("기지 문 (");\n','');
 s=rep(s,'noteOf: EdgeWallNote of a tile ("" when no base wall).','factOf: the wall meaning of a tile (None when no base wall).');
 s=rep(s,'Func<(int x, int y), string> noteOf','Func<(int x, int y), EdgeWallFact> factOf');
 s=rep(s,'string n = noteOf(w) ?? "";\n            if (OpensEdgeNote(n)) res.Add((w, n));','var fact = factOf(w);\n            if (fact.OpensEdge) res.Add((w, fact.Text));');
 return rep(s,'public static string MineEdgeFact(string wallNote) => OpensEdgeNote(wallNote ?? "") ? wallNote + "." : "";','public static string MineEdgeFact(EdgeWallFact wall) => wall.OpensEdge ? wall.Text + "." : "";');
});
edit(S+'AIEdge.Guard.cs',s=>rep(s,'public static string EdgeWallNote(int2 t) => PlanActive ? AIBaseGuard.EdgeWallNote(ShapeCached(), (t.x, t.y)) : "";\n    public static bool OpensEdge(int2 t) => AIBaseGuard.OpensEdgeNote(EdgeWallNote(t));','public static EdgeWallFact WallFact(int2 t) => PlanActive ? AIBaseGuard.WallFact(ShapeCached(), (t.x, t.y)) : default;\n    public static string EdgeWallNote(int2 t) => WallFact(t).Text;\n    public static bool OpensEdge(int2 t) => WallFact(t).OpensEdge;'));
for(const p of ['AIPlaces.cs','AIExplore.cs'])edit(S+p,s=>s.replaceAll('AIEdge.EdgeWallNote(new int2(', 'AIEdge.WallFact(new int2('));
edit(S+'AIBrain.Candidates.cs',s=>s.replaceAll('AIBaseGuard.MineEdgeFact(AIEdge.EdgeWallNote(', 'AIBaseGuard.MineEdgeFact(AIEdge.WallFact('));
edit('Tools/core-tests/PlaceGoalTests.cs',s=>{
 s=rep(s,'Func<(int x, int y), string> note = q => q == (-9, -3) ? AIBaseGuard.EdgeWallText : q == (-5, -6) ? AIBaseGuard.DoorFallText((-4, -6)) : q == (-6, -6) ? AIBaseGuard.DoorSideWallText : "";','Func<(int x, int y), EdgeWallFact> note = q => q == (-9, -3) ? new EdgeWallFact(EdgeWallKind.BoundaryWall) : q == (-5, -6) ? new EdgeWallFact(EdgeWallKind.HungDoorSide, (-4, -6)) : q == (-6, -6) ? new EdgeWallFact(EdgeWallKind.DoorSideWall) : default;');
 s=rep(s,'AIBaseGuard.OpensEdgeNote(AIBaseGuard.EdgeWallText) && AIBaseGuard.OpensEdgeNote(AIBaseGuard.DoorFallText((1, 2))) && !AIBaseGuard.OpensEdgeNote(AIBaseGuard.DoorSideWallText)','new EdgeWallFact(EdgeWallKind.BoundaryWall).OpensEdge && new EdgeWallFact(EdgeWallKind.HungDoorSide, (1, 2)).OpensEdge && !new EdgeWallFact(EdgeWallKind.DoorSideWall).OpensEdge');
 s=s.replaceAll('(OpensEdgeNote)','(EdgeWallFact.OpensEdge)');
 s=rep(s,'AIBaseGuard.MineEdgeFact(AIBaseGuard.EdgeWallText)','AIBaseGuard.MineEdgeFact(new EdgeWallFact(EdgeWallKind.BoundaryWall))');
 s=rep(s,'AIBaseGuard.MineEdgeFact(AIBaseGuard.DoorSideWallText)','AIBaseGuard.MineEdgeFact(new EdgeWallFact(EdgeWallKind.DoorSideWall))');
 return rep(s,'AIBaseGuard.MineEdgeFact(AIBaseGuard.DoorFallText((-4, -6)))','AIBaseGuard.MineEdgeFact(new EdgeWallFact(EdgeWallKind.HungDoorSide, (-4, -6)))');
});
edit('Tools/core-tests/GuardLiveTests.cs',s=>{
 s=rep(s,'string note = AIBaseGuard.EdgeWallNote(shape, (-3, -6));','var fact = AIBaseGuard.WallFact(shape, (-3, -6));\n        string note = fact.Text;');
 s=rep(s,'AIBaseGuard.OpensEdgeNote(note)','fact.OpensEdge');
 s=rep(s,'        // as measured: (-5,-6) mined, the door gone','        Program.Check(fact.Kind == EdgeWallKind.HungDoorSide && fact.Door == (-4, -6) && fact.Text == AIBaseGuard.DoorFallText((-4, -6)), "R2b. hung door fact preserves coordinate and exact text");\n        // as measured: (-5,-6) mined, the door gone');
 return s;
});
edit('Tools/core-tests/GuardTests.cs',s=>rep(s,'        string jf = AIBaseGuard.KillFacts(null, mf[\'f\'], jumper, "KingSlime");',`        var none = AIBaseGuard.WallFact(null, (0, 0));
        var sideFact = AIBaseGuard.WallFact(shapeS, (5, 2));
        var wallFact = AIBaseGuard.WallFact(shapeS, (2, 4));
        Program.Check(none.Kind == EdgeWallKind.None && !none.OpensEdge && none.Text == ""
            && AIBaseGuard.WallFact(shapeS, (2, 2)).Kind == EdgeWallKind.None
            && AIBaseGuard.WallFact(shapeS, (100, 100)).Kind == EdgeWallKind.None, "R2b. null, inside and unrelated walls have no edge fact");
        Program.Check(sideFact.Kind == EdgeWallKind.DoorSideWall && !sideFact.OpensEdge && sideFact.Text == AIBaseGuard.DoorSideWallText
            && wallFact.Kind == EdgeWallKind.BoundaryWall && wallFact.OpensEdge && wallFact.Text == AIBaseGuard.EdgeWallText,
            "R2b. wall meaning and exact display remain compatible");
        string jf = AIBaseGuard.KillFacts(null, mf['f'], jumper, "KingSlime");`));
