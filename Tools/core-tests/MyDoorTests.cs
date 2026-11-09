// G3md — 내문-판정 (31-내문-판정.md): the AI's doors off the edge plan ("my doors") in the reach grid and a re-picked plan's
// old edge doors. One group per defect; the old reading (copied from AIEdge.GuardGridAround before the fix: own = edge
// doors only) is shown next to the new one (AIBaseGuard.DoorCell with own = edge doors and my doors, AIEdge.IsWalkDoorTile).
using System;
using System.Collections.Generic;

public static class MyDoorTests
{
    public static void Run()
    {
        ClosedMyDoor(); UnseenMyDoor(); Repick(); Recheck();
    }

    // ------------------------------------------------------------------ 33-재검수 N1-N3

    static void Recheck()
    {
        // N2: an old plan's gap tile seen open (the breach scan's note), then a new plan, then my door placed there — the
        // stale look read the standing door as gone out of view (open ground: more threat, the door out of the walk);
        // NoteMyDoor / RepickOldDoors now forget the tile's look (a door stands there)
        var seen = new EdgeLastSeen(); seen.Note(-10, 3, true);
        var stale = AIBaseGuard.DoorCell(false, true, false, true, seen.Last(-10, 3));
        bool staleStands = AIBaseGuard.OwnDoorStands(false, true, seen.Last(-10, 3));
        seen.Forget(-10, 3);
        var fresh = AIBaseGuard.DoorCell(false, true, false, true, seen.Last(-10, 3));
        Program.Check(stale == null && !staleStands && fresh == GuardCell.OwnDoor && AIBaseGuard.OwnDoorStands(false, true, seen.Last(-10, 3)),
            $"MDN2. a my door placed on a tile with an old 'seen open' look: read gone out of view ({(stale?.ToString() ?? "tile")}); with the look forgotten on placing, the door ({fresh})");

        // N3: my door (0,0), passage east-west; the AI came in from the west (-1,0) and its skill ended in the door tile at
        // x = -0.2. The old side (no inside known) was the sign of that offset: west, back where it came from.
        var oldO = new DoorOwe { door = (0, 0) };
        AIBaseGuard.StartTail(oldO, true, -0.2f, 0f);
        var o = new DoorOwe();
        AIBaseGuard.NoteDoorTile(o, (0, 0), true, (-1, 0));
        int going = AIBaseGuard.PassSide((0, 0), o.from, true, true, true);
        AIBaseGuard.StartTail(o, true, -0.2f, 0f, going, true, true);
        Program.Check(oldO.dirX < 0 && o.from == (-1, 0) && going == 1 && o.dirX > 0,
            $"MDN3a. came in from the west: the old tail closed from the west (dir {oldO.dirX}), now from the going side, east (dir {o.dirX})");
        // the going side cannot be stood on (water, a wall beside it): the other side
        var o2 = new DoorOwe(); AIBaseGuard.NoteDoorTile(o2, (0, 0), true, (-1, 0));
        int g2 = AIBaseGuard.PassSide((0, 0), o2.from, true, false, true);
        AIBaseGuard.StartTail(o2, true, 0.2f, 0f, g2, false, true);
        // no tile known it came from (a teleport, a far jump): the side that can be stood on; north-south passage
        var o3 = new DoorOwe(); AIBaseGuard.NoteDoorTile(o3, (0, 0), true, (5, 5));
        int g3 = AIBaseGuard.PassSide((0, 0), o3.from, false, false, true);
        AIBaseGuard.StartTail(o3, false, 0f, 0.3f, g3, true, false);   // as TailStart: the chosen side (minus) can be stood on
        Program.Check(o2.dirX < 0 && o3.from == null && g3 == -1 && o3.dirY < 0 && AIBaseGuard.PassSide((0, 0), null, true, false, false) == 0,
            "MDN3b. going side not standable: the other side; no neighbour it came from: the standable side; nothing known: 0 (the offset, as before)");
        // a tail started off the door tile (half a tile or more on one side) keeps that side (unchanged)
        var o4 = new DoorOwe { door = (0, 0) };
        AIBaseGuard.StartTail(o4, true, -0.7f, 0f, 1, true, true);
        Program.Check(o4.dirX < 0, "MDN3c. unchanged: the AI already on one side (0.7 tiles west) closes from there");

        // N1: my door's tile turned to water or lava — a door does not stand there, so it is a look with no door (the
        // door is forgotten after a second; out of view the tile is low water then, fliers and shooters cross it). A wall
        // or an unloaded area (the default wall tile) is still no look.
        var look = new AIBaseGuard.MyDoorLook();
        bool w0 = look.LookTile(1, 1, true, true, false, true, true, 0f), w1 = look.LookTile(1, 1, true, true, false, true, true, 1f);
        bool wall = false; for (int i = 0; i < 6; i++) wall |= look.LookTile(2, 2, true, true, false, false, false, i * 0.5f);
        bool unl = false; for (int i = 0; i < 6; i++) unl |= look.LookTile(3, 3, true, false, false, true, false, i * 0.5f);
        bool g0 = look.LookTile(4, 4, true, true, true, false, false, 0f), g1 = look.LookTile(4, 4, true, true, true, false, false, 1f);
        bool d0 = look.LookTile(5, 5, true, true, true, false, true, 0f), d1 = look.LookTile(5, 5, true, true, true, false, true, 1f);
        Program.Check(!w0 && w1 && !wall && !unl && !g0 && g1 && !d0 && !d1,
            "MDN1. water / lava on my door's tile is a look with no door (forgotten after 1 s); a wall or an area not loaded is no look; ground with no door forgets, with a door keeps");
        // what the reach grid then reads out of view: the forgotten tile is low (fliers come), the kept door is not
        var gl = GuardGrid.Make(0, 0, 7, GuardCell.Closed);   // closed all round (no window-border way)
        for (int y = 1; y <= 5; y++) { gl.cell[gl.Idx(2, y)] = GuardCell.Open; gl.cell[gl.Idx(4, y)] = GuardCell.Open; }
        gl.cell[gl.Idx(3, 3)] = AIBaseGuard.DoorCell(false, false, false, false) ?? AIBaseGuard.CellOf(false, false, false, true);
        var rFly = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gl, (2, 3)), (4, 3), EnemyMove.Fly);
        gl.cell[gl.Idx(3, 3)] = AIBaseGuard.DoorCell(false, false, false, true) ?? GuardCell.Low;
        var rKept = AIBaseGuard.ReachOf(AIBaseGuard.Reach(gl, (2, 3)), (4, 3), EnemyMove.Fly);
        Program.Check(rFly.comes && !rKept.comes, "MDN1b. forgotten, the water tile lets a flier come (Low); kept as my door it hid it");
    }

    // AIEdge.GuardGridAround before 내문-판정: the door branch and the out-of-view branch asked S.doors (edge doors) only
    static GuardCell? OldCell(bool inView, bool doorThere, bool doorOpen, bool edgeDoor)
    {
        if (doorThere) return doorOpen ? GuardCell.Open : edgeDoor ? GuardCell.OwnDoor : GuardCell.ShutDoor;
        if (!inView && edgeDoor) return GuardCell.OwnDoor;
        return null;
    }

    // a grid from a function; the door tile through the given reading (null: a walkable tile, CellOf)
    static GuardGrid Grid(int n, Func<(int x, int y), GuardCell> world, (int x, int y) door, GuardCell? doorRead, Func<(int x, int y), bool> view = null)
    {
        var g = GuardGrid.Make(0, 0, n, GuardCell.Closed);
        if (view != null) g.view = new bool[n * n];
        for (int x = 0; x < n; x++)
            for (int y = 0; y < n; y++)
            {
                int i = g.Idx(x, y);
                g.cell[i] = (x, y) == door ? doorRead ?? AIBaseGuard.CellOf(true, false, false, false) : world((x, y));
                if (view != null) g.view[i] = view((x, y));
            }
        return g;
    }

    // ------------------------------------------------------------------ 결함 2: a closed my door is the AI's own door

    // a room x 1..5 (the AI at (5,3)) with my door (6,3) in its east wall (no plan); outside x 7..12. A MushroomEnemy
    // pressed on the far side of the closed door, 1.4 tiles off (tile (7,3)).
    static void ClosedMyDoor()
    {
        (int, int) door = (6, 3), me = (5, 3), foe = (7, 3);
        GuardCell World((int x, int y) t) => t.x >= 1 && t.x <= 5 && t.y >= 1 && t.y <= 5 || t.x >= 7 && t.x <= 12 && t.y >= 1 && t.y <= 5 ? GuardCell.Open : GuardCell.Closed;
        // in view, a door stands there, closed; it is my door (not an edge door)
        var oldC = OldCell(true, true, false, false);
        var newC = AIBaseGuard.DoorCell(true, true, false, true);
        Program.Check(oldC == GuardCell.ShutDoor && newC == GuardCell.OwnDoor,
            $"MD2a. a closed my door in view: the old reading made it someone else's door ({oldC}), now the AI's own door ({newC})");
        Program.Check(AIBaseGuard.DoorCell(true, true, false, false) == GuardCell.ShutDoor && AIBaseGuard.DoorCell(true, true, true, true) == GuardCell.Open && AIBaseGuard.DoorCell(false, true, true, false) == GuardCell.Open,
            "MD2b. unchanged: someone else's closed door is ShutDoor; an open door (anyone's, in view or not) is open");
        var rOld = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(15, World, door, oldC), me), foe);
        var rNew = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(15, World, door, newC), me), foe);
        // the attack option: "cannot" = no way and not touching (AIBrain.Candidates) — the old reading left it out
        bool cannotOld = rOld.kind == ReachKind.None && !rOld.touch, cannotNew = rNew.kind == ReachKind.None && !rNew.touch;
        Program.Check(cannotOld && rNew.kind == ReachKind.Door && rNew.door == door && rNew.steps == 2 && !cannotNew,
            $"MD2c. the AI's way to it: old 'behind a wall' ({rOld.kind}: no attack option), now through my door ({rNew.kind} via ({rNew.door.x},{rNew.door.y}) {rNew.steps} tiles: the attack 'out through the door')");
        Program.Check(rOld.otherDoor && rNew.doorBetween && !rNew.otherDoor,
            "MD2d. what is between: old 'someone else's closed door', now a closed door that is the AI's (otherDoor false)");
        string noteOld = AIBaseGuard.CloseNote(rOld, 1.4f), noteNew = AIBaseGuard.CloseNote(rNew, 1.4f);
        Program.Check(noteOld.Contains("남의 것") && !noteNew.Contains("남의 것") && noteNew.Contains("사이에 닫힌 문이 있음") && noteNew.Contains("문이 열리거나 부서지면"),
            $"MD2e. the close fact: old '{noteOld.Trim()}' → now '{noteNew.Trim()}'");
        string kf = AIBaseGuard.KillFacts(null, foe, rNew, "MushroomEnemy", 1.4f);
        Program.Check(kf.Contains("문 (6,3)을 지나 2칸") && kf.Contains("걸어올 길은 지금 없음") && kf.Contains(AIBaseGuard.DoorWeakNote),
            $"MD2f. its kill facts name my door as the way out and the door's weakness: '{kf}'");
        // the threat (self-defence, the combat re-decision: how often the action model is called) does not change — a
        // closed door, the AI's or not, is no way for a walker
        Program.Check(!rOld.comes && !rNew.comes && !AIBaseGuard.IsThreat(rOld, 1.4f, 8f) && !AIBaseGuard.IsThreat(rNew, 1.4f, 8f),
            "MD2g. unchanged: the walker past the closed door is no threat either way (no extra calls)");
        var cav = rNew; cav.breaker = AIBaseGuard.IsBreaker("Caveling", cav.tileBreak);
        Program.Check(AIBaseGuard.IsThreat(cav, 1.4f, 8f) && AIBaseGuard.CloseNote(cav, 1.4f).Contains("벽·문을 부수는 종류"),
            "MD2h. unchanged: a breaker 1.4 tiles past my door is a threat, with the fact why");
    }

    // ------------------------------------------------------------------ 결함 1: my door out of view is the last look

    // a long room x 1..14, y 1..3 (the AI at (2,2)); the wall row y = 4 with my door (14,4); outside y 5..7, x 1..14; a
    // MushroomEnemy at (2,6) (in view). The view reaches x <= 8: the door is out of view.
    static void UnseenMyDoor()
    {
        (int, int) door = (14, 4), me = (2, 2), foe = (2, 6);
        GuardCell World((int x, int y) t) => t.x >= 1 && t.x <= 14 && (t.y >= 1 && t.y <= 3 || t.y >= 5 && t.y <= 7) ? GuardCell.Open : GuardCell.Closed;
        bool View((int x, int y) t) => t.x <= 8;
        // seen standing (closed) at the last look; it fell (or was taken) while out of view: no door there now
        var oldC = OldCell(false, false, false, false);
        var newC = AIBaseGuard.DoorCell(false, false, false, true);
        var rOld = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(18, World, door, oldC, View), me), foe);
        var rNew = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(18, World, door, newC, View), me), foe);
        Program.Check(oldC == null && newC == GuardCell.OwnDoor,
            $"MD1a. my door out of view with no door read there: the old reading read the tile (open ground), now the door as last seen ({newC})");
        Program.Check(rOld.comes && AIBaseGuard.IsThreat(rOld, 4f, 8f) && !rNew.comes && !AIBaseGuard.IsThreat(rNew, 4f, 8f) && rNew.kind == ReachKind.Door && rNew.door == door,
            $"MD1b. the enemy outside: old 'it comes' (a threat: self-defence and calls), now not (the door as last seen; the AI's way is through it, {rNew.kind})");
        // still standing and closed out of view: the AI's door (the old reading: someone else's)
        Program.Check(OldCell(false, true, false, false) == GuardCell.ShutDoor && AIBaseGuard.DoorCell(false, true, false, true) == GuardCell.OwnDoor,
            "MD1c. my door out of view, standing closed: old ShutDoor, now OwnDoor");
        // seen gone: the look in view with no door forgets my door (AIEdge.LookMyDoors through AIBaseGuard.MyDoorLook);
        // out of view it is then the tile
        var look = new AIBaseGuard.MyDoorLook();
        var myDoors = new HashSet<(int, int)> { door };
        void Look(bool inView, bool loaded, bool doorNow, float now) { if (myDoors.Contains(door) && look.Look(door.Item1, door.Item2, inView, loaded, doorNow, now)) myDoors.Remove(door); }
        Look(false, true, false, 0f); Look(true, true, true, 0.5f);
        bool kept = myDoors.Contains(door);
        Look(true, true, false, 1f); Look(true, true, false, 1.5f); Look(true, true, false, 2.0f);
        var afterGone = AIBaseGuard.DoorCell(false, false, false, myDoors.Contains(door));
        var rGone = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(18, World, door, afterGone, View), me), foe);
        Program.Check(kept && !myDoors.Contains(door) && afterGone == null && rGone.comes,
            "MD1d. the look: out of view or a door standing keeps my door; in view with no door for a second forgets it — out of view it is open then (the enemy comes)");
        // 32-검수 M1: one look with no door is not enough (the old look forgot at once); the area not loaded (joining, a
        // respawn: the objects not there yet) or the tile not read as ground never forgets; a look that sees the door or
        // cannot see it starts again
        var l2 = new AIBaseGuard.MyDoorLook();
        bool once = l2.Look(1, 1, true, true, false, 0f);
        bool unloaded = false; for (int i = 0; i < 10; i++) unloaded |= l2.Look(2, 2, true, false, false, i * 0.5f);
        bool b1 = l2.Look(3, 3, true, true, false, 0f), b2 = l2.Look(3, 3, true, true, true, 0.5f), b3 = l2.Look(3, 3, true, true, false, 1f), b4 = l2.Look(3, 3, true, true, false, 1.5f);
        bool c1 = l2.Look(4, 4, true, true, false, 0f), c2 = l2.Look(4, 4, false, true, false, 5f), c3 = l2.Look(4, 4, true, true, false, 6f), c4 = l2.Look(4, 4, true, true, false, 7f);
        Program.Check(!once && !unloaded && !b1 && !b2 && !b3 && !b4 && !c1 && !c2 && !c3 && c4,
            "MD1g. (32-검수 M1) the look forgets only after no door in every look that could see it for 1 s or more (one miss, an unloaded area, a door seen between, a look out of view between: kept)");
        // with a base remembered (the room is the inside, the door tile an edge tile, not an edge door): the memory layer
        // keeps my door (the last look null); a last look open (the breach scan's note) with the door forgotten is open
        var ins = new List<(int x, int y)>(); for (int x = 1; x <= 14; x++) for (int y = 1; y <= 3; y++) ins.Add((x, y));
        var shape = AIBaseGuard.Shape(ins, new List<(int x, int y)>());
        var seen = new EdgeLastSeen();
        var rMem = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(18, World, door, newC, View), me, new BaseMemory(shape, null, null, seen)), foe);
        seen.Note(14, 4, true);
        var rMemGone = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(18, World, door, afterGone, View), me, new BaseMemory(shape, null, null, seen)), foe);
        Program.Check(shape.IsEdge(14, 4) && !rMem.comes && rMemGone.comes,
            $"MD1e. with a base: my door on an edge tile stays the door out of view ({rMem.kind}); seen gone it is open ({rMemGone.kind}, comes)");
        // the place grid / walk door passage asks the same: in view where a door stands, out of view unless seen open
        Program.Check(AIBaseGuard.OwnDoorStands(false, false, null) && AIBaseGuard.OwnDoorStands(false, false, false) && !AIBaseGuard.OwnDoorStands(false, true, true) && !AIBaseGuard.OwnDoorStands(true, false, null) && AIBaseGuard.OwnDoorStands(true, true, true),
            "MD1f. OwnDoorStands: out of view the last look (open: gone), in view the door now");
        // 32-검수 L1: a my door still remembered whose tile was seen open at the last look (the breach scan noted it before
        // the look forgot it, or an old save) — the first DoorCell read it as the door out of view (the enemy hidden)
        var l1Cell = AIBaseGuard.DoorCell(false, false, false, true, true);
        var rL1 = AIBaseGuard.ReachOf(AIBaseGuard.Reach(Grid(18, World, door, l1Cell, View), me), foe);
        Program.Check(l1Cell == null && rL1.comes && AIBaseGuard.DoorCell(false, true, false, true, true) == null && AIBaseGuard.DoorCell(false, false, false, true, false) == GuardCell.OwnDoor,
            $"MD1h. (32-검수 L1) out of view, last look open: no door (open ground, the enemy comes: {rL1.kind}), even with a door read there now; last look closed: the door");
        // the reach grid's own door and OwnDoorStands agree in every case (one "stands" judgement)
        bool agree = true;
        foreach (bool v in new[] { false, true }) foreach (bool d in new[] { false, true }) foreach (bool op in new[] { false, true }) foreach (bool? lo in new bool?[] { null, false, true })
        {
            var cell = AIBaseGuard.DoorCell(v, d, d && op, true, lo);
            if ((cell != null) != AIBaseGuard.OwnDoorStands(v, d, lo)) agree = false;
            if (cell == GuardCell.Open && !(d && op)) agree = false;
        }
        Program.Check(agree, "MD1i. own doors: DoorCell reads a door (OwnDoor, or open when it stands open) exactly where OwnDoorStands says it stands");
    }

    // ------------------------------------------------------------------ 결함 3: a re-picked plan's old edge doors

    // 제작대-놓기 실게임2: edge 3 hung its door (-10,0); edge 3' (gap (-10,3), no door spot) was picked; (-10,0) stayed an
    // edge door (repair, breach, the edge-door walk branch, the reach memory's door). HEAD decision: an old edge door off
    // the new gap tiles leaves the edge doors; standing, it is my door; gone, it is forgotten.
    static void Repick()
    {
        var o = new EdgeOption();
        var g1 = new EdgeGap(); g1.tiles.Add((-10, 3)); o.gaps.Add(g1);
        var g2 = new EdgeGap(); g2.tiles.Add((4, 7)); g2.tiles.Add((5, 7)); o.gaps.Add(g2);
        var old = new List<(int x, int y)> { (-10, 0), (5, 7), (8, 8), (9, 9) };
        // (-10,0) in view and standing; (8,8) out of view, last seen standing (no look kept: it fell unseen or not);
        // (9,9) out of view, seen gone at the last look (its tile seen open)
        bool Stands((int x, int y) t) => t == (-10, 0) ? AIBaseGuard.OwnDoorStands(true, true, null) : t == (8, 8) ? AIBaseGuard.OwnDoorStands(false, false, null) : AIBaseGuard.OwnDoorStands(false, false, true);
        AIBaseEdgeCore.RepickDoors(old, o, Stands, out var keep, out var toMine, out var drop);
        Program.Check(keep.Count == 1 && keep[0] == (5, 7),
            "MD3a. an old edge door on a gap tile of the new plan stays an edge door");
        Program.Check(toMine.Count == 2 && toMine.Contains((-10, 0)) && toMine.Contains((8, 8)) && drop.Count == 1 && drop[0] == (9, 9),
            $"MD3b. off the new gaps: standing (in view) or last seen standing → my door ({string.Join(" ", toMine)}); seen gone → forgotten ({string.Join(" ", drop)}); the old Pick kept all {old.Count} as edge doors");
        // the moved door is still the AI's door in the reach grid (it needs 결함 2: under the old reading it would have
        // turned into someone else's door by the move)
        Program.Check(AIBaseGuard.DoorCell(true, true, false, true) == GuardCell.OwnDoor && OldCell(true, true, false, false) == GuardCell.ShutDoor,
            "MD3c. (-10,0) as my door is read OwnDoor (old reading of a non-edge door: ShutDoor)");
        // the adoption rule (base-s3, user decision B) is unchanged: my door on a new gap tile becomes the edge's door
        var myDoors = new HashSet<(int, int)>(toMine) { (4, 7) };
        var adopted = AIBaseEdgeCore.AdoptDoors(o, t => myDoors.Contains(t));
        Program.Check(adopted.Count == 1 && adopted[0].gap == 1 && adopted[0].tile == (4, 7),
            "MD3d. unchanged: my door on a gap tile of the new plan is adopted as its edge door; the moved ones are not on gaps");
        AIBaseEdgeCore.RepickDoors(new List<(int x, int y)>(), o, Stands, out var k0, out var m0, out var d0);
        AIBaseEdgeCore.RepickDoors(old, null, t => true, out var kN, out var mN, out _);
        Program.Check(k0.Count + m0.Count + d0.Count == 0 && kN.Count == 0 && mN.Count == old.Count,
            "MD3e. no old doors: nothing; a plan with no gaps: every standing old door is my door");
    }
}
