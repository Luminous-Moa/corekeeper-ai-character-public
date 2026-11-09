// AIBaseGuard — keeping the base (코어-안전지대 단계 3), Unity-free (core-tests GuardTests). The game side is AIEdge.cs
// (grid reading, candidates, skills) and AIBrain (self-defence, combat triggers). Agreed rules (12-단계3-구현.md):
// - The code makes options with facts; the action model chooses. Only what is physically impossible is left out; a risk
//   is a fact, never a reason for the code to avoid or choose.
// - Kill: every enemy in view — inside the base and outside it — gets facts: its distance from the edge and its side,
//   how the AI reaches it (an open walk of N tiles, through an own door N tiles, only through unseen tiles, or "behind a
//   wall: cannot reach now"), and whether its kind breaks walls / doors (Tools/knowledge/rules.json). An enemy no walk
//   can reach (walls all round, as far as is known) is no attack option (that cannot be done) and no threat for the
//   combat re-decision: self-defence and "ask again at once" are driven only by enemies that can come (단계 1 M4c: 287
//   calls in 100 s against an enemy behind a fence).
// - Repair: the remembered edge (placed walls, the door, the inside) against the tiles now: a placed wall gone, a
//   natural edge tile opened, a placed door gone. Repairs go per tile in batches of at most 7 (AIBaseEdgeCore.MaxBatch);
//   the tile that closes an opening is placed from inside.
// - Pick-up and place again: a placed object is hit twice with a pick or the empty hand (a melee weapon does not hit
//   placed objects, 11-단계2-후속.md) and comes back into the bag (PickupStep).
// - Repair materials: facts (blocks in the bag against the edge and doors). How much to keep is the model's call.
// - Back into the base: the retreat goes to a walkable inside tile (not the spawn pad, which the game's path search never
//   reaches), through an own door when the AI is outside.
// - Review G6 (단계 2): none of these options counts as progress of another goal (CountsAsGoalProgress).
// No radius decides "near": the enemies are those the AI sees, the base is its remembered inside.
using System;
using System.Collections.Generic;

// Wall meaning is independent of the displayed Korean sentence.
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

// One cell of the walk grid around the AI.
public enum GuardCell : byte
{
    Closed = 0,    // wall, object, pit edge... walkers and fliers stop (known)
    Open = 1,      // walkable (known)
    OwnDoor = 2,   // an own closed door: the AI opens and passes it; enemies do not open doors (rules door-enemy, estimate)
    Unknown = 3,   // never seen: it may lead anywhere
    Low = 4,       // fence, water, pit: walkers stop, fliers pass (rules enemy-path)
    ShutDoor = 5,  // 재검수 R4: someone else's closed door — closed for the AI and for walkers like a wall, but a door (it may open)
}

public sealed class GuardGrid
{
    public int n, loX, loY;     // window (loX + i, loY + j), index i * n + j
    public GuardCell[] cell;
    public bool[] view;         // 재실측 N-b: the cell is in view now (null: every cell counts as in view)
    public bool In(int x, int y) => x >= loX && y >= loY && x < loX + n && y < loY + n;
    public int Idx(int x, int y) => (x - loX) * n + (y - loY);
    public GuardCell At(int x, int y) => In(x, y) ? cell[Idx(x, y)] : GuardCell.Unknown;
    public static GuardGrid Make(int loX, int loY, int n, GuardCell fill)
    {
        var g = new GuardGrid { n = n, loX = loX, loY = loY, cell = new GuardCell[n * n] };
        for (int i = 0; i < g.cell.Length; i++) g.cell[i] = fill;
        return g;
    }
}

public enum ReachKind { Open, Door, Unseen, None }

// How an enemy gets at the AI besides walking (단계 3 review F1): a flier crosses fences and water; a ranged attacker
// shoots over them (1-게임조사 2.2: ranged enemies killed a cow behind a fence); a jumper jumps over them (a blue king
// slime crossed a pit). Fliers by name (estimate), ranged / jump by the enemy's RangeAttackStateCD / JumpAttackStateCD.
[Flags]
public enum EnemyMove { Walk = 0, Fly = 1, Ranged = 2, Jump = 4 }

public struct EnemyReach
{
    public ReachKind kind;
    public int steps;              // walk tiles (Open / Door)
    public (int x, int y) door;    // the first own door on the way (Door)
    public bool comes;             // the enemy can get at the AI: walk (an open or unseen way; no door: enemies do not open
                                   // doors), or fly / shoot / jump over fences and water (move)
    public bool walks;             // ... by walking
    public bool flyReach;          // its tile joins the AI's over open, unknown, fence and water tiles (no wall, no closed
                                   // door between): something there can hit the AI somehow (recheck R1)
    public EnemyMove move;
    public int memEdge, memBeyond;     // review S3: base edge tiles read from memory (never seen in the window) / beyond the window
    public bool memChanged;            // recheck N3: without the base memory this enemy would come (memory decided "cannot come")
    public int memHidden;              // 재실측 N-b / 검수 F2: edge tiles out of view now that were closed at the AI's last look
    public int memPlanned;             // 검수 F2: edge tiles out of view, seen before but with no look kept since: as the plan says (closed)
    public int memBig;                 // 검수 F3: large objects seen before and out of view now (their tiles read closed)
    public string memBigNames;         // ... their names
    public int memSeenOpen;            // 검수 F2: edge tiles out of view read open (open at the AI's last look)
    public bool doorBetween;           // 검수 F6: not touch, but within NearSteps when closed doors are passed too (a door between, not a wall)
    public bool otherDoor;             // 재검수 R4: ... and that door is not the AI's own (someone else's closed door)
    public bool touch;                 // 재실측 N-g: its tile joins the AI's within NearSteps over open, unknown, fence and water
                                       // tiles — no wall and no closed door between (the HitReach rule needs it)
    public bool breaker;               // ... its kind breaks walls / doors (IsBreaker; set by the game side from the id and its data)
    public TileBreak tileBreak;        // 실측결함수정3 N1: its tile damage from the game data (the game side reads it)
}

// 실측결함수정3 N1: an enemy kind's tile damage as the game data gives it (its ObjectPropertiesCD, read by the game side —
// the same keys the game's systems read): bump = what DamageObjectState hits a tile it ran into while chasing with
// (-302465456; only an enemy with that state; it hits a wall only when damage − the wall's reduction ≥ the wall's hp/5,
// DamageObjectStateRequest), melee = the tile damage of its melee hit (-1854640576, with the flag -2072102458:
// MeleeAttackStateSystem), charge = its charge's tile damage (1689738466 with the flag 2130508658:
// ChargeAttackStateSystem). known = the data was read (false: no entity or prefab to read).
// 합치기 전 수정 C1 (Review/fix3-review.md C1): obj = what DamageObjectState hits an object it ran into with — a door is
// an object, the game reads -636725815 for it (DamageObjectStateRequest: the tile key only when the hit is a tile; the
// object then takes -636725815 or the melee damage, DamageObjectStateSystem). chase = the kind has that state (without
// it bump and obj are 0: it does not hit what it runs into). A value null = its key was not read (모름).
public struct TileBreak
{
    public bool known, chase;
    public int? bump, obj, melee, charge;
    public bool Any => bump > 0 || obj > 0 || melee > 0 || charge > 0;
    public string Log => known ? $"{(chase ? "" : "no chase state; ")}bump {V(bump)} obj {V(obj)} melee {V(melee)} charge {V(charge)}" : "not read";
    private static string V(int? v) => v.HasValue ? v.Value.ToString() : "?";
}

// Walk distances from the AI over the grid: open tiles only, open tiles and own doors, and anything not known closed.
public sealed class ReachMap
{
    public GuardGrid g;
    public int[] open, door, via;   // steps (-1 unreached); via = index of the first own door on the door walk (-1 none)
    public bool[] maybe;            // reachable over open and unknown tiles, no door (an enemy's way to the AI as well)
    public bool[] maybeDoor;        // the same with own doors (the AI's way)
    public bool[] fly;              // (fliers) over open, unknown, fences, water and pits
    public bool baseMemory;         // live-run fix 1: built with the remembered base (BaseMemory)
    public int memEdge;             // review S3: unseen edge tiles in the window taken from memory
    public int memBeyond;           // ... and edge tiles beyond the window (the way round them is what memory says)
    public bool[] rawMaybe, rawFly; // recheck N3: the enemy ways over the grid as seen (no memory, the window-border rule): null without memory
    public int memHidden;           // 재실측 N-b / 검수 F2: edge tiles out of view read closed from the AI's last look there
    public int memPlanned;          // 검수 F2: ... read closed from the plan (seen before, no look kept since)
    public int memSeenOpen;         // 검수 F2: ... read open from the last look (a hole seen open stays open out of view)
    public int memBig;              // 검수 F3: large objects remembered out of view (their tiles read closed)
    public string memBigNames = "";
    public int[] near;              // 재실측 N-g: steps from the AI over open, unknown, fence and water tiles (-1 unreached)
    public int[] nearDoor;          // 검수 F6 / 재검수 R4: the same with closed doors passed too (own or not: a door between, not a wall)
    public int[] nearOwnDoor;       // 재검수 R4: ... own closed doors only
}

// Live-run fix 1 (14-단계3-측정 사실 1): the remembered base for the reach map. A base taller than the walk window had
// its inside running off the window's border, and the "a way may leave the window and come back" rule then joined the
// inside to every open / unknown border tile — an enemy past a shut door became a threat (30 calls in 12 s). With the
// base remembered: an unseen tile of the base is what memory says (inside: open; a placed wall or a natural edge tile:
// closed; an own door: a door; a gap tile not built yet: open), and the base's tiles take no part in the window-border
// rule (the way out of the base is through its edge, which memory knows). The unknown-tile rule stays for tiles that
// are not the base.
public sealed class BaseMemory
{
    public BaseShape shape;
    public readonly HashSet<long> walls = new HashSet<long>(), doors = new HashSet<long>();
    public EdgeLastSeen seen;   // 검수 F2: what each edge tile was at the AI's last look (null: none kept)
    public BaseMemory(BaseShape s, IEnumerable<(int x, int y)> ownWalls, IEnumerable<(int x, int y)> ownDoors, EdgeLastSeen lastSeen = null)
    {
        shape = s; seen = lastSeen;
        if (ownWalls != null) foreach (var t in ownWalls) walls.Add(EdgeGrid.Key(t.x, t.y));
        if (ownDoors != null) foreach (var t in ownDoors) doors.Add(EdgeGrid.Key(t.x, t.y));
    }
    public bool InBase(int x, int y) { long k = EdgeGrid.Key(x, y); return shape.inside.Contains(k) || shape.edgeSet.Contains(k); }
    // what memory says a tile of the base is (null: not the base)
    public GuardCell? Remembered(int x, int y)
    {
        long k = EdgeGrid.Key(x, y);
        if (doors.Contains(k)) return GuardCell.OwnDoor;
        if (walls.Contains(k)) return GuardCell.Closed;
        if (shape.inside.Contains(k)) return GuardCell.Open;
        if (!shape.edgeSet.Contains(k)) return null;
        return shape.gap.Contains(k) ? GuardCell.Open : GuardCell.Closed;   // a gap tile not built yet / a natural edge tile
    }
    // an edge tile beyond the window that memory says is open (a gap not built yet): then the base may lead out there
    public bool OpenBeyond(GuardGrid g)
    {
        foreach (var e in shape.edge) if (!g.In(e.x, e.y) && (Remembered(e.x, e.y) == GuardCell.Open || seen?.Last(e.x, e.y) == true)) return true;   // 검수 F2: or a hole seen open there
        return false;
    }
}

// HEAD decision 2026-11-06 (검수 F2): an edge tile out of view is what the AI saw there at its last look — a hole seen
// open (a root dug out, a placed wall broken) stays open out of view, a tile seen closed stays closed. Only a tile with
// no look kept (never seen, or not since the plan was read) is what the plan says, and the unknown-tile rule. A tile
// in view is what it is now (never memory). Door tiles are not kept (a door's state is read where it is). Noted in one
// place (재검수 R5), the breach scan's reading of the tiles in view (AIBaseGuard.Breaches); the reach map reads it.
// Kept with the edge memory (재검수 R3: memory.json edgeSeen, AIEdge.Export / Import; cleared with it): after a restart a
// hole seen open is still open.
public sealed class EdgeLastSeen
{
    private readonly Dictionary<long, (int x, int y, bool open)> _m = new Dictionary<long, (int, int, bool)>();
    public int Count => _m.Count;
    public int Version { get; private set; }   // changes with every new or changed look (the game side saves then)
    public void Note(int x, int y, bool open)
    {
        long k = EdgeGrid.Key(x, y);
        if (_m.TryGetValue(k, out var v) && v.open == open) return;
        _m[k] = (x, y, open); Version++;
    }
    public void Forget(int x, int y) { if (_m.Remove(EdgeGrid.Key(x, y))) Version++; }
    public bool? Last(int x, int y) => _m.TryGetValue(EdgeGrid.Key(x, y), out var v) ? v.open : (bool?)null;
    public void Clear() { if (_m.Count > 0) Version++; _m.Clear(); }
    // [x, y, open 0/1] per tile (memory.json)
    public int[] ToArray() { var l = new List<int>(); foreach (var v in _m.Values) { l.Add(v.x); l.Add(v.y); l.Add(v.open ? 1 : 0); } return l.ToArray(); }
    public void Load(int[] a) { _m.Clear(); for (int i = 0; a != null && i + 2 < a.Length; i += 3) _m[EdgeGrid.Key(a[i], a[i + 1])] = (a[i], a[i + 1], a[i + 2] != 0); Version++; }
}

// 재실측 N-b (17-재실측 4절): large objects seen before (more than one tile: the core, statues, boulders) with their
// tiles. The object list is read in view only, so a seen core 15 tiles off read as open ground in the reach map; these
// tiles count as closed out of view (검수 F4: a tile of it in view is what it is now; AIBaseGuard.Reach, the memory layer). An entry goes when its centre is in view again and the object
// is not there (a look in which it was not seen).
public sealed class BigObjectMemory
{
    private readonly Dictionary<long, (string name, float cx, float cy, List<(int x, int y)> tiles)> _m = new Dictionary<long, (string, float, float, List<(int, int)>)>();
    private Dictionary<long, (long id, string name)> _tiles;
    public int Count => _m.Count;
    public void Seen(long id, string name, float cx, float cy, List<(int x, int y)> tiles)
    {
        if (tiles == null || tiles.Count < 2) return;
        _m[id] = (name, cx, cy, tiles); _tiles = null;
    }
    // after a look: entries not seen in it whose centre was in view are gone
    public void Forget(ICollection<long> seenNow, Func<float, float, bool> inView)
    {
        List<long> gone = null;
        foreach (var kv in _m)
            if (!seenNow.Contains(kv.Key) && inView(kv.Value.cx, kv.Value.cy)) (gone ?? (gone = new List<long>())).Add(kv.Key);
        if (gone == null) return;
        foreach (var k in gone) _m.Remove(k);
        _tiles = null;
    }
    public bool Has(int x, int y) => At(x, y).HasValue;
    // the remembered object on (x, y): its key and name (null: none) — 검수 F3: the reach map names them in its facts
    public (long id, string name)? At(int x, int y)
    {
        if (_tiles == null) { _tiles = new Dictionary<long, (long, string)>(); foreach (var kv in _m) foreach (var t in kv.Value.tiles) _tiles[EdgeGrid.Key(t.x, t.y)] = (kv.Key, kv.Value.name); }
        return _tiles.TryGetValue(EdgeGrid.Key(x, y), out var v) ? v : ((long, string)?)null;
    }
    // 검수 F4: the AI stands inside its footprint (passable for it): not kept as closed
    public void Drop(long id) { if (_m.Remove(id)) _tiles = null; }
    public void Clear() { _m.Clear(); _tiles = null; }
}

// Live-run fix 3 (사실 3): an own door the AI went into (open) is owed a close; a skill that ends with it still open
// closes it from the side the AI is on before the end is reported (open -> through -> close is one bundle, user
// decision). A failed close is a fact on the skill's end and the door stays owed (the close option says so).
public sealed class DoorOwe
{
    public (int x, int y)? door;
    public bool tail;                        // the closing tail runs now
    public DoorPassState st;                 // the tail's door step (Close)
    public float dirX, dirY;                 // from the door towards the side the AI closes from
    public string fact = "";
    public (int x, int y)? from;             // 33-재검수 N3: the tile the AI came into the door from (null: not known)
}

public enum BreachKind { WallGone, NaturalGone, DoorGone }

public sealed class Breach
{
    public (int x, int y) tile;
    public BreachKind kind;
    public bool side;    // a door spot's side wall placed off the edge (F5): it opens no way in, the door needs it
    public bool remembered;   // 검수 F2: out of view now, open at the AI's last look (EdgeLastSeen)
    public string Text => Said + (remembered ? $"(기억: ({tile.x},{tile.y}) 마지막으로 볼 때 열림, 지금 안 보임)" : "");
    private string Said => side && kind == BreachKind.NaturalGone ? $"({tile.x},{tile.y}) 문 옆 자연 벽이 없어짐(문은 양옆이 벽이어야 함; 벽이 없어지면 달린 문이 떨어짐, 단계 3 실측)" : side ? $"({tile.x},{tile.y}) 문 자리 옆에 놓았던 벽이 없어짐(문은 양옆이 벽이어야 함)" : kind == BreachKind.WallGone ? $"({tile.x},{tile.y}) 놓았던 벽이 없어짐" : kind == BreachKind.DoorGone ? $"({tile.x},{tile.y}) 달았던 문이 없어짐" : $"({tile.x},{tile.y}) 자연 벽·물체가 있던 둘레 칸이 열림";
}

// What an edge tile is now (read by the game side).
public enum EdgeNow { Closed, Open, Unseen, OwnDoor }

public sealed class BreachScan
{
    public readonly List<Breach> list = new List<Breach>();
    public int edgeTiles, unseen, notBuilt, ownWalls, natural, doors;
    public int remembered;   // 검수 F2: of the unseen, open at the last look (in the list as remembered breaches)
}

// The remembered base: inside tiles and the edge (the plan's gap tiles and every non-inside 4-neighbour of the inside).
public sealed class BaseShape
{
    public readonly HashSet<long> inside = new HashSet<long>();
    public readonly HashSet<long> gap = new HashSet<long>();
    public readonly HashSet<long> sides = new HashSet<long>();   // placed walls off the cut (a door spot's side walls, F5): part of the edge memory
    public (int x, int y)? hungDoor;   // live-run fix 5: the plan's door when it is hung (its two side tiles are edge memory, natural or placed)
    public readonly HashSet<long> doorSide = new HashSet<long>();   // ... those two tiles (on the cut or off it)
    public readonly List<(int x, int y)> edge = new List<(int, int)>();
    public readonly HashSet<long> edgeSet = new HashSet<long>();
    public bool Inside(int x, int y) => inside.Contains(EdgeGrid.Key(x, y));
    public bool IsEdge(int x, int y) => edgeSet.Contains(EdgeGrid.Key(x, y));
}

public enum PickupPhase { Hold, Approach, Hit, Collect, Place, Done, Failed }

public sealed class PickupState
{
    public PickupPhase phase = PickupPhase.Hold;
    public bool replace;                 // place it again on a new tile after it is in the bag
    public bool goneSeen;                // the object was seen gone from its tile (review S3: forget it even on a failure)
    public float hitSince = -1f, goneAt = -1f;
    public int hp0 = -1, lastHp = -1, bag0 = -1;
    public string fail = "", facts = "";
    public const float NoChangeSeconds = 5f, CollectSeconds = 3f, HitRange = 1.4f;
}

public struct PickupObs
{
    public bool holdOk;        // a pick or an empty hand slot can be held
    public bool heldMelee;     // what would be held is a melee weapon (does not hit placed objects)
    public bool objectThere;   // the placed object still stands
    public int objHp;
    public float dist;         // to the object
    public int bagCount;       // of the object's item
    public float now;
}

public struct PickupOut { public bool walk, swing, place; }

public static class AIBaseGuard
{
    public const float HitReach = 1.5f;   // an enemy this close can be hit (and can hit) whatever the walls

    private static readonly int[] Dx = { 1, -1, 0, 0 }, Dy = { 0, 0, 1, -1 };

    // ------------------------------------------------------------------ reach

    // mem (optional, live-run fix 1): the remembered base — its unseen tiles from memory, no window-border rule for them.
    // big (optional, 검수 F3): large objects seen before — their tiles out of view are closed. Both are the memory layer:
    // the grid g is the tiles as read now (objects in view only), and the ways without the layer (rawMaybe) tell whether
    // memory decided an enemy's answer (MemoryNote), with a base or without one.
    public static ReachMap Reach(GuardGrid g, (int x, int y) from, BaseMemory mem = null, BigObjectMemory big = null)
    {
        Func<int, bool> noBorder = null;
        int memEdge = 0, memBeyond = 0, memHidden = 0, memPlanned = 0, memSeenOpen = 0, memBig = 0; string bigNames = "";
        bool[] rawMaybe = null, rawFly = null;
        bool bigLayer = big != null && big.Count > 0 && g.view != null;   // 검수 F4: out of view only, so only with the view known
        if (mem != null || bigLayer)
        {
            // recheck N3: the ways as they were without memory (whether memory changed an enemy's answer)
            rawMaybe = Spread(g, from, c => c == GuardCell.Open || c == GuardCell.Unknown);
            rawFly = Spread(g, from, c => c == GuardCell.Open || c == GuardCell.Unknown || c == GuardCell.Low);
            var g2 = new GuardGrid { n = g.n, loX = g.loX, loY = g.loY, cell = (GuardCell[])g.cell.Clone() };
            if (mem != null)
            {
                // 재검수 R5: the last looks are noted in one place, the breach scan (Breaches); the reach map only reads them
                for (int i = 0; i < g2.cell.Length; i++)
                {
                    if (g2.cell[i] != GuardCell.Unknown) continue;
                    int x = g.loX + i / g.n, y = g.loY + i % g.n;
                    var r = mem.Remembered(x, y);
                    if (r.HasValue) { g2.cell[i] = r.Value; if (mem.shape.IsEdge(x, y)) memEdge++; }
                }
                // 재실측 N-b, HEAD decision (검수 F2): an edge tile seen before but out of view now is what the AI saw there at its
                // last look — the objects are read in view only, so a natural edge of an object (TheCore between the inside and
                // the core room) read open ground once the AI was 15 tiles away; and a hole seen open (a root dug out, a placed
                // wall broken) stays open (it was hidden as "closed as planned"). No look kept: what the plan says (closed).
                // A door tile is kept as read.
                if (g.view != null)
                    for (int i = 0; i < g2.cell.Length; i++)
                    {
                        if (g.view[i] || g.cell[i] == GuardCell.Unknown) continue;
                        int x = g.loX + i / g.n, y = g.loY + i % g.n;
                        if (!mem.shape.IsEdge(x, y)) continue;
                        bool? last = mem.seen?.Last(x, y);
                        if (mem.doors.Contains(EdgeGrid.Key(x, y)))
                        {
                            // 실측결함수정3 N4 (HEAD decision: out of view = the last look): an own door seen gone (its tile seen
                            // open) is open out of view; seen hanging, it is the door as last seen (the game side reads a
                            // door that fell unseen as the remembered door, AIEdge.GuardGridAround)
                            if (last == true && g2.cell[i] != GuardCell.Open) { g2.cell[i] = GuardCell.Open; memSeenOpen++; }
                            continue;
                        }
                        if (g.cell[i] == GuardCell.OwnDoor || g.cell[i] == GuardCell.ShutDoor) continue;
                        if (last == true) { g2.cell[i] = GuardCell.Open; memSeenOpen++; continue; }
                        if (g2.cell[i] != GuardCell.Open) continue;
                        if (last == false) { g2.cell[i] = GuardCell.Closed; memHidden++; }
                        else if (mem.Remembered(x, y) == GuardCell.Closed) { g2.cell[i] = GuardCell.Closed; memPlanned++; }
                    }
            }
            if (bigLayer)
            {
                // 재실측 N-b, 검수 F3/F4: a large object seen before closes its tiles out of view only (a tile in view is what
                // it is now); an edge tile whose last look was open stays open (that look is of the tile itself)
                var ids = new HashSet<long>(); var names = new List<string>();
                for (int i = 0; i < g2.cell.Length; i++)
                {
                    if (g.view[i] || g2.cell[i] != GuardCell.Open || g.cell[i] == GuardCell.Unknown) continue;
                    int x = g.loX + i / g.n, y = g.loY + i % g.n;
                    var o = big.At(x, y);
                    if (!o.HasValue || mem?.seen?.Last(x, y) == true && mem.shape.IsEdge(x, y)) continue;
                    g2.cell[i] = GuardCell.Closed;
                    if (ids.Add(o.Value.id) && !names.Contains(o.Value.name)) names.Add(o.Value.name);
                }
                memBig = ids.Count; bigNames = string.Join(", ", names);
            }
            g2.view = g.view;
            g = g2;
            if (mem != null && !mem.OpenBeyond(g)) { noBorder = i => mem.InBase(g.loX + i / g.n, g.loY + i % g.n); foreach (var e in mem.shape.edge) if (!g.In(e.x, e.y)) memBeyond++; }
        }
        var m = new ReachMap { g = g, baseMemory = mem != null, memEdge = memEdge, memBeyond = memBeyond, memHidden = memHidden, memPlanned = memPlanned, memSeenOpen = memSeenOpen, memBig = memBig, memBigNames = bigNames, rawMaybe = rawMaybe, rawFly = rawFly };
        int T = g.n * g.n;
        m.open = Bfs(g, from, c => c == GuardCell.Open, null, out _);
        m.door = Bfs(g, from, c => c == GuardCell.Open || c == GuardCell.OwnDoor, m.via = new int[T], out _);
        m.maybe = Spread(g, from, c => c == GuardCell.Open || c == GuardCell.Unknown, noBorder);
        m.maybeDoor = Spread(g, from, c => c == GuardCell.Open || c == GuardCell.Unknown || c == GuardCell.OwnDoor, noBorder);
        m.fly = Spread(g, from, c => c == GuardCell.Open || c == GuardCell.Unknown || c == GuardCell.Low, noBorder);
        m.near = Bfs(g, from, c => c == GuardCell.Open || c == GuardCell.Unknown || c == GuardCell.Low, null, out _);
        m.nearDoor = Bfs(g, from, c => c == GuardCell.Open || c == GuardCell.Unknown || c == GuardCell.Low || c == GuardCell.OwnDoor || c == GuardCell.ShutDoor, null, out _);
        m.nearOwnDoor = Bfs(g, from, c => c == GuardCell.Open || c == GuardCell.Unknown || c == GuardCell.Low || c == GuardCell.OwnDoor, null, out _);
        return m;
    }

    // 검수 F9: the reach grid's reading of a (non-door) tile in view or seen before: fence / water are low, an object in
    // view closes it, a tile marked only because a walk got stuck there does not (the mark steers the walk's own path
    // search around a spot; it is no wall — a 1-tile passage read closed for 120 s made the enemies past it "cannot come")
    public static GuardCell CellOf(bool walkable, bool objectBlocked, bool stuckOnly, bool low)
        => low ? GuardCell.Low : walkable && (!objectBlocked || stuckOnly) ? GuardCell.Open : GuardCell.Closed;

    // 내문-판정 (31-내문-판정.md) 결함 1·2: the reach grid's reading of a tile with a door standing on it or an own door
    // remembered there. own = the AI's own door, an edge door or my door (the one judgement: AIEdge.IsWalkDoorTile);
    // lastOpen = the tile's last look kept (EdgeLastSeen: true = seen open, the door gone). An own door stands as
    // OwnDoorStands says (32-검수 L1: the one "stands" judgement — out of view, a tile seen open at its last look is no
    // door, even if a door is read there now): standing, open is open and closed is a door the AI opens (OwnDoor; out of
    // view one that fell unseen is still the door); not standing, the tile as a tile (null, CellOf: open ground once the
    // door is gone). Someone else's door: open is open, closed is ShutDoor. (The old reading: own = edge doors only — a
    // closed my door was someone else's door, one that fell unseen out of view open ground.)
    public static GuardCell? DoorCell(bool inView, bool doorThere, bool doorOpen, bool own, bool? lastOpen = null)
    {
        if (own)
        {
            if (!OwnDoorStands(inView, doorThere, lastOpen)) return null;
            return doorThere && doorOpen ? GuardCell.Open : GuardCell.OwnDoor;
        }
        if (doorThere) return doorOpen ? GuardCell.Open : GuardCell.ShutDoor;
        return null;
    }

    // 실측결함수정3 N4/N6, 내문-판정: a remembered own door (edge or mine) is there as the AI knows it now — in view, where a
    // door stands; out of view, unless its tile was seen open at its last look (it fell). The place grid's door passage
    // (AIEdge.WalkDoorTilesNow), the look that forgets my doors (AIEdge.LookMyDoors) and a new plan's old edge doors
    // (AIBaseEdgeCore.RepickDoors) all ask this.
    public static bool OwnDoorStands(bool inView, bool doorNow, bool? lastOpen) => inView ? doorNow : lastOpen != true;

    // 32-검수 M1: when the look forgets a my door (AIEdge.LookMyDoors; forgetting is saved and not undone). Only a look
    // that can see the door counts — in view, the AI's area loaded (the game's AreaLoadedCD on the player) and the tile
    // read as ground (an unloaded area reads as the default wall tile, AITileReader) — and only "in view, no door" in
    // every such look for at least ForgetAfter seconds (two or more looks: every 0.5 s from TrackDoor). A look that
    // sees the door, or one that cannot see it, starts again. Joining a world, a respawn or a jump in place before the
    // objects arrive then forgets nothing.
    public sealed class MyDoorLook
    {
        public const float ForgetAfter = 1f;
        private readonly Dictionary<long, float> _missSince = new Dictionary<long, float>();
        public int Watching => _missSince.Count;
        // one look at a my door: true = forget it now
        public bool Look(int x, int y, bool inView, bool loaded, bool doorNow, float now)
        {
            long k = EdgeGrid.Key(x, y);
            if (!inView || !loaded || OwnDoorStands(true, doorNow, null)) { _missSince.Remove(k); return false; }
            if (!_missSince.TryGetValue(k, out float t0) || now < t0) { _missSince[k] = now; return false; }
            if (now - t0 < ForgetAfter) return false;
            _missSince.Remove(k);
            return true;
        }
        // 33-재검수 N1: one look from what the game side reads — areaLoaded (AreaLoadedCD), the tile read as ground, as
        // water or lava (liquid), a door object there. A door does not stand on water or lava: a liquid tile is a look
        // with no door (a my door left there read as the door out of view hid fliers and shooters, which cross water). A
        // tile read as neither (a wall, or an area not loaded: the default wall tile) is no look (a wall hides no threat).
        public bool LookTile(int x, int y, bool inView, bool areaLoaded, bool ground, bool liquid, bool doorThere, float now)
            => Look(x, y, inView, areaLoaded && (ground || liquid), doorThere && !liquid, now);
        public void Drop(int x, int y) => _missSince.Remove(EdgeGrid.Key(x, y));
        public void Clear() => _missSince.Clear();
    }

    // Live-run fix 2 (사실 2): the tile the AI stands on. Its rounded position pressed against a shut door is the door
    // tile (e.g. (-3.85,-5.65) -> (-4,-6)); a shut door is no tile to stand on, so the nearest tile it can stand on (by
    // the distance from the position to the tile centre, 4- then diagonal neighbours) is its tile. Reach, the own-door
    // branch of a walk and the inside / outside facts all start from it.
    public static (int x, int y) StandTile(float px, float py, Func<(int x, int y), bool> canStand)
    {
        var t = ((int)Math.Round(px), (int)Math.Round(py));   // as AIBody.TileOf (math.round)
        if (canStand(t)) return t;
        (int, int) best = t; double bd = double.MaxValue;
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var u = (t.Item1 + dx, t.Item2 + dy);
                if (!canStand(u)) continue;
                double d = (u.Item1 - px) * (u.Item1 - px) + (u.Item2 - py) * (u.Item2 - py) + (dx != 0 && dy != 0 ? 1e-3 : 0);
                if (d < bd) { bd = d; best = u; }
            }
        return best;
    }

    // 재실측 결함 2 (17-재실측 2절): the tile a walk's tile A* starts from. Every skill's walk (걷기, 치우기, 캐기, 공격, ...)
    // plans from the stand tile — pressed against a shut door the rounded position is the door tile, and the walk that
    // may plan through own doors allowed that tile, so the A* to the door was 0 tiles and the door step never ran. When
    // the plan does not allow the stand tile (an object it is pressed against, a tile the walk got stuck on), the
    // nearest 4-neighbour it allows. canStand: walkable and no shut door; target: the walk's target (always allowed).
    public static (int x, int y) PlanFrom(float px, float py, Func<(int x, int y), bool> canStand, Func<(int x, int y), bool> allowed, (int x, int y)? target = null)
    {
        var st = StandTile(px, py, canStand);
        if (allowed(st) || target.HasValue && target.Value == st) return st;
        var best = st; double bd = double.MaxValue;
        for (int k = 0; k < 4; k++)
        {
            var u = (st.x + Dx[k], st.y + Dy[k]);
            if (!canStand(u) || !allowed(u)) continue;
            double d = (u.Item1 - px) * (u.Item1 - px) + (u.Item2 - py) * (u.Item2 - py);
            if (d < bd) { bd = d; best = u; }
        }
        return best;
    }

    // 4-way BFS from `from` (its own cell always counts); via (optional) = the first OwnDoor cell on the path
    private static int[] Bfs(GuardGrid g, (int x, int y) from, Func<GuardCell, bool> ok, int[] via, out bool border)
    {
        int n = g.n, T = n * n;
        var d = new int[T];
        for (int i = 0; i < T; i++) { d[i] = -1; if (via != null) via[i] = -1; }
        border = false;
        if (!g.In(from.x, from.y)) return d;
        var q = new Queue<int>();
        int s = g.Idx(from.x, from.y); d[s] = 0; q.Enqueue(s);
        while (q.Count > 0)
        {
            int i = q.Dequeue(); int x = g.loX + i / n, y = g.loY + i % n;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Dx[k], ny = y + Dy[k];
                if (!g.In(nx, ny)) { border = true; continue; }
                int j = g.Idx(nx, ny);
                if (d[j] >= 0 || !ok(g.cell[j])) continue;
                d[j] = d[i] + 1;
                if (via != null) via[j] = via[i] >= 0 ? via[i] : (g.cell[j] == GuardCell.OwnDoor ? j : -1);
                q.Enqueue(j);
            }
        }
        return d;
    }

    // reachable cells where a way may also leave the window and come back (the window's border is one place outside);
    // noBorder (live-run fix 1): cells (the remembered base) that neither leave by nor come back through the border
    private static bool[] Spread(GuardGrid g, (int x, int y) from, Func<GuardCell, bool> ok, Func<int, bool> noBorder = null)
    {
        int n = g.n, T = n * n;
        var r = new bool[T];
        if (!g.In(from.x, from.y)) return r;
        var q = new Queue<int>();
        int s = g.Idx(from.x, from.y); r[s] = true; q.Enqueue(s);
        bool borderDone = false;
        while (q.Count > 0)
        {
            int i = q.Dequeue(); int x = g.loX + i / n, y = g.loY + i % n;
            bool atBorder = false;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Dx[k], ny = y + Dy[k];
                if (!g.In(nx, ny)) { atBorder = true; continue; }
                int j = g.Idx(nx, ny);
                if (r[j] || !ok(g.cell[j])) continue;
                r[j] = true; q.Enqueue(j);
            }
            if (atBorder && !borderDone && (noBorder == null || !noBorder(i)))
            {
                borderDone = true;
                for (int a = 0; a < n; a++)
                    foreach (var (bx, by) in new[] { (a, 0), (a, n - 1), (0, a), (n - 1, a) })
                    {
                        int j = bx * n + by;
                        if (!r[j] && ok(g.cell[j]) && (noBorder == null || !noBorder(j))) { r[j] = true; q.Enqueue(j); }
                    }
            }
        }
        return r;
    }

    // How the AI reaches tile t (its tile or a 4-neighbour as the stand), and whether something there can come to the AI.
    public static EnemyReach ReachOf(ReachMap m, (int x, int y) t, bool flier = false) => ReachOf(m, t, flier ? EnemyMove.Fly : EnemyMove.Walk);

    public static EnemyReach ReachOf(ReachMap m, (int x, int y) t, EnemyMove move)
    {
        var g = m.g;
        var r = new EnemyReach { kind = ReachKind.None, steps = -1 };
        int bestOpen = int.MaxValue, bestDoor = int.MaxValue, doorVia = -1; bool maybe = false, maybeDoor = false, fly = false;
        for (int k = -1; k < 4; k++)
        {
            int x = k < 0 ? t.x : t.x + Dx[k], y = k < 0 ? t.y : t.y + Dy[k];
            if (!g.In(x, y)) continue;
            int i = g.Idx(x, y);
            // a neighbour stand counts one more step (the enemy's own tile is where it stands)
            int add = k < 0 ? 0 : 1;
            if (m.open[i] >= 0 && m.open[i] + add < bestOpen) bestOpen = m.open[i] + add;
            if (m.door[i] >= 0 && m.via[i] >= 0 && m.door[i] + add < bestDoor) { bestDoor = m.door[i] + add; doorVia = m.via[i]; }
            if (m.maybe[i]) maybe = true;
            if (m.maybeDoor[i]) maybeDoor = true;
            if (m.fly[i]) fly = true;
        }
        if (bestOpen < int.MaxValue) { r.kind = ReachKind.Open; r.steps = bestOpen; }
        else if (bestDoor < int.MaxValue) { r.kind = ReachKind.Door; r.steps = bestDoor; r.door = (g.loX + doorVia / g.n, g.loY + doorVia % g.n); }
        else if (maybeDoor) r.kind = ReachKind.Unseen;
        // an enemy walks only over open (or unseen) tiles, not through a closed door; a flier, a ranged attacker and a
        // jumper also get over fences / water (review F1; pits are closed cells here, S6)
        r.walks = maybe; r.move = move; r.flyReach = fly;
        r.comes = maybe || move != EnemyMove.Walk && fly;
        r.memEdge = m.memEdge; r.memBeyond = m.memBeyond; r.memHidden = m.memHidden; r.memPlanned = m.memPlanned; r.memSeenOpen = m.memSeenOpen; r.memBig = m.memBig; r.memBigNames = m.memBigNames;
        // 재실측 N-g: no wall and no closed door between it and the AI within a few steps
        if (m.near != null && g.In(t.x, t.y))
        {
            r.touch = Within(m, m.near, t);
            // 검수 F6 / 재검수 R4: what is between is a closed door (it would touch through the door), not a wall; whose door
            r.doorBetween = !r.touch && m.nearDoor != null && Within(m, m.nearDoor, t);
            r.otherDoor = r.doorBetween && !(m.nearOwnDoor != null && Within(m, m.nearOwnDoor, t));
        }
        // recheck N3: did memory decide it? (the same neighbour rule over the ways without memory)
        if (m.rawMaybe != null && !r.comes)
        {
            bool rm = false, rf = false;
            for (int k = -1; k < 4; k++)
            {
                int x = k < 0 ? t.x : t.x + Dx[k], y = k < 0 ? t.y : t.y + Dy[k];
                if (!g.In(x, y)) continue;
                int i = g.Idx(x, y);
                if (m.rawMaybe[i]) rm = true;
                if (m.rawFly[i]) rf = true;
            }
            r.memChanged = rm || move != EnemyMove.Walk && rf;
        }
        return r;
    }

    // review F1: damage within this many seconds (as self-defence's "hurt") makes every enemy in range a threat
    public const float HurtSeconds = 1.5f;

    // Whether an enemy is a threat for the combat re-decision and self-defence: within range and able to get at the AI
    // (walk, or fly / shoot / jump over fences and water), or so close it can hit now, or the AI was hurt within
    // HurtSeconds (review F1: an enemy that is hitting the AI is no "enemy that cannot reach"; 단계 1 M4c — an enemy
    // behind a fence that does not hit — stays no threat). An enemy behind walls or a closed door is not: nothing the AI
    // does changes there until a wall breaks (then it can come and is a threat again).
    // Recheck R1: "hurt" adds only enemies that could be the ones hitting (their tile joins the AI's without a wall or a
    // closed door: walk, flight, a projectile or a jump gets across); a walker sealed behind walls stays no threat while
    // the AI loses health to something else (fire, poison).
    // 재실측 N-g (17-재실측 4절): the HitReach rule only with nothing shut between — a walker pressed against the far side
    // of a closed door 1.2-1.5 tiles off hit nobody (health unchanged) but kept self-defence and the combat re-decision on
    // (28 calls). An enemy that hits through anyway — a ranged one, or a kind that breaks walls / doors — stays a threat
    // within HitReach (the facts say why: CloseNote).
    public static bool IsThreat(EnemyReach r, float dist, float range, bool hurt = false) => dist <= range && (r.comes || dist <= HitReach && HitsAcross(r) || hurt && r.flyReach);
    public static bool HitsAcross(EnemyReach r) => r.touch || r.breaker || (r.move & EnemyMove.Ranged) != 0;
    public const int NearSteps = 3;   // walk steps that make "nothing between" for an enemy within HitReach (a diagonal neighbour is 2)
    // the enemy's tile within NearSteps of the AI over the near walk d; its tile itself not walkable there (an object it
    // is pressed against, a stuck mark): a 4-neighbour one step closer (재검수 R1 / 검수 S5: the attack's swing asks this,
    // and an enemy against an object next to the AI was never "nothing between")
    private static bool Within(ReachMap m, int[] d, (int x, int y) t)
    {
        var g = m.g; int i = g.Idx(t.x, t.y);
        if (d[i] >= 0) return d[i] <= NearSteps;
        var c = g.cell[i];
        if (c != GuardCell.Closed) return false;   // a door, a fence: what is between, not where it stands
        for (int k = 0; k < 4; k++)
        {
            int nx = t.x + Dx[k], ny = t.y + Dy[k];
            if (!g.In(nx, ny)) continue;
            int j = g.Idx(nx, ny);
            if (d[j] >= 0 && d[j] <= NearSteps - 1) return true;
        }
        return false;
    }

    // the fact of an enemy within HitReach ("" when it is farther): nothing between, or what is between and whether it counts
    // 검수 F6: the words follow IsThreat — an enemy with a walk back to the AI (comes) is counted whatever is between, and
    // "if the door breaks" only when a closed door is what is between (a wall: no door words)
    public static string CloseNote(EnemyReach r, float dist)
    {
        if (dist < 0f || dist > HitReach) return "";
        if (r.touch) return $" {HitReach:F1}칸 안, 사이에 막힌 것 없음: 지금 서로 칠 수 있음.";
        string between = r.otherDoor ? "사이에 닫힌 문(남의 것)이 있음" : r.doorBetween ? "사이에 닫힌 문이 있음" : "사이에 벽이나 막힌 칸이 있음";
        string why = (r.move & EnemyMove.Ranged) != 0 ? "원거리 공격을 하는 종류" : r.breaker ? (r.doorBetween ? "벽·문을 부수는 종류" : "벽을 부수는 종류") : null;
        if (why != null) return $" {HitReach:F1}칸 안이지만 {between}. {why}라 그 너머로도 칠 수 있는 것으로 보고 위협으로 셈(추정).";
        if (r.comes) return $" {HitReach:F1}칸 안이지만 {between}: 지금은 서로 칠 수 없음(걸어서 돌아올 길은 있어 위협으로 셈).";
        return $" {HitReach:F1}칸 안이지만 {between}: 서로 칠 수 없음(위협으로 세지 않음{(r.otherDoor ? "; 문이 열리면 달라짐" : r.doorBetween ? "; 문이 열리거나 부서지면 달라짐" : "")}).";
    }

    // Recheck R1: health lost between two samples that a lower max health does not explain (hunger below 25 and the end
    // of the well-fed bonus lower the max; the health above the new max goes with it — estimate)
    // Review S2 (2026-11-06): only the health above the new max goes with a lower max: min(hp0, max1) - hp1 > 0. (The
    // old "(hp0 - hp1) - (max0 - max1)" took the whole max drop off: health 80 of 150, the bonus ends (max 100), a hit
    // of 20 was hidden.) max0 is kept for the callers.
    public static bool HurtBy(int hp0, int max0, int hp1, int max1) => HurtAmount(hp0, hp1, max1) > 0;
    // 합치기 전 수정 6 (22-짧은확인 t=1047.5): the one rule for "how much was lost to a hit" — health above the new max
    // (max with conditions: the well-fed bonus) going with it is no hit. Every health-drop check counts with this (the
    // vitals' drop line and hit sums, the brain's health-drop triggers), not with the raw difference: after a stand-up
    // 105/100 -> 100 was logged "hurt with no enemy in view" (and a 105 -> 100 by the test command, t=461.2, started
    // self-defence #3 "체력이 줄어듦(-5)" — that one set the health below the max with conditions: AIVitals.ArenaGo).
    public static int HurtAmount(int hp0, int hp1, int max1) => Math.Max(0, Math.Min(hp0, max1) - hp1);

    // the fight state for the combat re-decision ("ask again as soon as the answer is in"): a threat within range
    public static bool CombatNow(IEnumerable<(EnemyReach r, float dist)> enemies, float range, bool hurt = false)
    {
        foreach (var e in enemies) if (IsThreat(e.r, e.dist, range, hurt)) return true;
        return false;
    }

    public enum DefenceEvent { None, Started, Running, Ended }

    // The self-defence episode (no goal; AIBrain.SelfDefence): starts on a threat or damage, ends after `quiet` seconds
    // with neither. An enemy that cannot come (walls, a closed door) neither starts nor keeps it (단계 1 M4c).
    public static DefenceEvent DefenceStep(ref bool on, ref float lastThreat, bool threat, bool hurt, float now, float quiet = 3f)
    {
        if (threat || hurt) lastThreat = now;
        if (!on)
        {
            if (!threat && !hurt) return DefenceEvent.None;
            on = true; return DefenceEvent.Started;
        }
        if (now - lastThreat >= quiet) { on = false; return DefenceEvent.Ended; }
        return DefenceEvent.Running;
    }

    // ------------------------------------------------------------------ the remembered base

    // sideWalls (optional): placed walls of the edge memory the repair covers (the game side passes those off the inside
    // and a door's side walls, also inside ones, recheck R4); those not on the cut (a door spot's side walls off the plan,
    // review F5) join the edge as side tiles, so a broken one is found and repaired
    // hungDoor / sidesEastWest (live-run fix 5): the plan's door once it is hung and its wall axis — its two side tiles
    // are edge memory whether natural or placed (mining a natural one let the door fall into the bag, 14-단계3-측정 사실 5)
    public static BaseShape Shape(IEnumerable<(int x, int y)> inside, IEnumerable<(int x, int y)> gapTiles, IEnumerable<(int x, int y)> sideWalls = null, (int x, int y)? hungDoor = null, bool? sidesEastWest = null)
    {
        var s = new BaseShape();
        foreach (var t in inside) s.inside.Add(EdgeGrid.Key(t.x, t.y));
        if (hungDoor.HasValue && sidesEastWest.HasValue)
        {
            var d = hungDoor.Value; s.hungDoor = d;
            var two = sidesEastWest.Value ? new[] { (d.x + 1, d.y), (d.x - 1, d.y) } : new[] { (d.x, d.y + 1), (d.x, d.y - 1) };
            var sw = new List<(int x, int y)>(); if (sideWalls != null) sw.AddRange(sideWalls);
            foreach (var t in two)
            {
                s.doorSide.Add(EdgeGrid.Key(t.Item1, t.Item2));
                // an inside tile is a side wall only when one was placed there (sideWalls has it then)
                if (!sw.Contains(t) && !s.inside.Contains(EdgeGrid.Key(t.Item1, t.Item2))) sw.Add(t);
            }
            sideWalls = sw;
        }
        if (gapTiles != null) foreach (var t in gapTiles) { s.gap.Add(EdgeGrid.Key(t.x, t.y)); if (s.edgeSet.Add(EdgeGrid.Key(t.x, t.y))) s.edge.Add(t); }
        foreach (var key in s.inside)
        {
            int x = (int)(key >> 32), y = (int)(uint)key;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + Dx[k], ny = y + Dy[k];
                long nk = EdgeGrid.Key(nx, ny);
                if (s.inside.Contains(nk) || !s.edgeSet.Add(nk)) continue;
                s.edge.Add((nx, ny));
            }
        }
        if (sideWalls != null)
            foreach (var t in sideWalls)
            {
                long k = EdgeGrid.Key(t.x, t.y);
                if (!s.edgeSet.Add(k)) continue;   // (recheck R4: an inside tile here is a door's side wall: repaired too)
                s.edge.Add(t); s.sides.Add(k);
            }
        s.edge.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return s;
    }

    // Recheck R5: the fact on a mine / dig option for a wall of the base. A door's side wall off the cut opens no way in
    // when mined: the door loses a side (it goes in only between two walls).
    public const string EdgeWallText = "기지 둘레 벽 — 캐면 둘레가 열림(적 지나감)";
    public const string DoorSideWallText = "기지 문 자리 옆 벽 — 캐면 문 양옆이 빔(둘레는 열리지 않음; 문은 양옆이 벽일 때만 놓임, door-between-walls)";
    public static string EdgeWallNote(BaseShape s, (int x, int y) t) => WallFact(s, t).Text;
    public static EdgeWallFact WallFact(BaseShape s, (int x, int y) t)
    {
        if (s == null) return default;
        long k = EdgeGrid.Key(t.x, t.y);
        // live-run fix 5: a side wall of the hung door (natural or placed): mining it let the door fall (into the bag) and
        // its tile opened
        if (s.hungDoor.HasValue && s.doorSide.Contains(k)) return new EdgeWallFact(EdgeWallKind.HungDoorSide, s.hungDoor.Value);
        return new EdgeWallFact(s.sides.Contains(k) ? EdgeWallKind.DoorSideWall : s.edgeSet.Contains(k) && !s.inside.Contains(k) ? EdgeWallKind.BoundaryWall : EdgeWallKind.None);
    }
    public static string DoorFallText((int x, int y) d) => $"기지 문 ({d.x},{d.y}) 옆 벽 — 캐면 문이 떨어짐(문은 가방으로 들어감, 단계 3 실측) — 문 칸이 열려 둘레가 열림(적 지나감)";

    // 제작대-놓기 실게임 D1: the walls of a dig route that open the edge, with their EdgeWallNote sentence, leaving out
    // `ok` (edge walls the chosen option already stated). factOf: the wall meaning of a tile (None when no base wall).
    public static List<((int x, int y) t, string note)> EdgeWallsOn(IEnumerable<(int x, int y)> walls, Func<(int x, int y), EdgeWallFact> factOf, ICollection<(int x, int y)> ok = null)
    {
        var res = new List<((int, int), string)>();
        foreach (var w in walls)
        {
            if (ok != null && ok.Contains(w)) continue;
            var fact = factOf(w);
            if (fact.OpensEdge) res.Add((w, fact.Text));
        }
        return res;
    }

    // the dig option's fact (the enemy dig option's words, with each wall's sentence): "" when no wall opens the edge
    // onlyWay (검수 E1): the route came from the second pass — no way without mining the edge; capped (재검수 G3): the
    // first pass stopped at its search cap, so only "not found within the search" is known
    public static string DigEdgeText(List<((int x, int y) t, string note)> edge, int max = 3, bool onlyWay = false, bool capped = false)
    {
        if (edge == null || edge.Count == 0) return "";
        if (onlyWay) return DigEdgeText(edge, max) + (capped ? " 둘레를 파지 않는 길은 찾기 범위 안에서 못 찾음." : " 둘레를 파지 않는 길(문·돌아가는 길)은 아는 칸에 없음.");
        var parts = new List<string>();
        for (int i = 0; i < edge.Count && i < max; i++) parts.Add($"({edge[i].t.x},{edge[i].t.y}) {edge[i].note}");
        return $" 그중 {edge.Count}개는 기지 둘레 벽(뚫으면 둘레가 열림): {string.Join("; ", parts)}{(edge.Count > max ? $" 외 {edge.Count - max}개" : "")}.";
    }

    // 우회 사실 (재검수 G1): the first way's fact when mining the edge is much quicker — tiles and estimated seconds of
    // both (seconds from AIMoveEstimate, independent of route selection scores), and the edge facts.
    // viaDoor: the first way goes through a door ("문으로 돌아감"), else around ("둘레를 피해 감")
    public static string DetourFact(bool viaDoor, int firstTiles, float firstSeconds, int edgeTiles, float edgeSeconds, List<((int x, int y) t, string note)> edge, int max = 2)
    {
        var tiles = new List<string>(); var notes = new List<string>();
        for (int i = 0; edge != null && i < edge.Count; i++) { if (i < 3) tiles.Add($"({edge[i].t.x},{edge[i].t.y})"); if (notes.Count < max && !notes.Contains(edge[i].note)) notes.Add(edge[i].note); }
        return $"{(viaDoor ? "문으로 돌아감" : "둘레를 피해 감")} {firstTiles}칸·약 {firstSeconds:F0}초(둘레 {string.Join(" ", tiles)}{(edge != null && edge.Count > 3 ? " …" : "")}를 파면 {edgeTiles}칸·약 {edgeSeconds:F0}초, 둘레가 열림{(notes.Count > 0 ? " — " + string.Join("; ", notes) : "")})";
    }

    // 실게임2 P2: the edge fact on any dig / mine option (desc unchanged when it already says it or there is none)
    public static string WithEdgeFact(string desc, string fact)
    {
        if (string.IsNullOrEmpty(fact)) return desc ?? "";
        string d = desc ?? "";
        string core = fact.Trim();
        if (d.Contains(core) || d.Contains("기지 둘레 벽(뚫으면 둘레가 열림)")) return d;
        return d + (core.StartsWith(" ") || d.EndsWith(" ") ? "" : " ") + core;
    }
    // the mine option's edge fact of its wall ("" when it opens no edge)
    public static string MineEdgeFact(EdgeWallFact wall) => wall.OpensEdge ? wall.Text + "." : "";

    // a running dig route replanned onto edge walls the option did not state: stop there with this sentence
    public static string DigEdgeStopText(List<((int x, int y) t, string note)> edge) =>
        $"멈춤: 길을 다시 찾으니 기지 둘레를 파는 길만 남음(고를 때 없던 둘레 벽).{DigEdgeText(edge)} 문으로 가는 길이 없음";

    // Live-run fix 7: a big root (or a placed object) on an edge tile closes the edge like a wall; cutting it opens the
    // edge (the wood goal cut 7 edge roots and opened the base). "" when t is no edge tile.
    public static string EdgeTileNote(BaseShape s, (int x, int y) t, string what)
    {
        if (s == null) return "";
        long k = EdgeGrid.Key(t.x, t.y);
        if (s.hungDoor.HasValue && s.doorSide.Contains(k)) return DoorFallText(s.hungDoor.Value).Replace("옆 벽 — 캐면", $"옆 {what} — 없애면");
        return s.edgeSet.Contains(k) && !s.inside.Contains(k) && !s.sides.Contains(k) ? $"기지 둘레 칸의 {what} — 없애면 둘레가 열림(적 지나감)" : "";
    }

    // tiles (Chebyshev) from t to the nearest edge tile (0: on the edge)
    public static int EdgeDistance(BaseShape s, (int x, int y) t)
    {
        int best = int.MaxValue;
        foreach (var e in s.edge) best = Math.Min(best, Math.Max(Math.Abs(e.x - t.x), Math.Abs(e.y - t.y)));
        return best == int.MaxValue ? -1 : best;
    }

    // ------------------------------------------------------------------ kill facts

    // Which enemy kinds break walls / doors (Tools/knowledge/rules.json; the level of each rule is said).
    public const string KnownNoBreaker = "벽을 부수는 종류로 알려지지 않음(쫓다가 막히면 약한 벽은 때릴 수 있음, 규칙 wall-break-ways (1); 문은 체력 2라 타일 피해가 1이라도 있으면 그 조건을 채움, door-enemy, 추정)";
    // review S1: a closed own door between (enemies do not open doors — estimate) is the weakest part of the way
    public const string DoorWeakNote = "다만 문은 체력 2·피해 감소 0이라, 쫓다가 문에 막힌 적이 치면 곧 부서질 수 있음(규칙 wall-break-ways (1)·door-enemy, 추정). 부서지면 그 칸은 열린 길이 됨";
    public static string MoveNote(EnemyReach r)
    {
        var l = new List<string>();
        if ((r.move & EnemyMove.Ranged) != 0) l.Add(r.walks ? "원거리 공격을 하는 종류(울타리·물 너머로도 쏨, 투사체가 넘어옴, 추정)" : "원거리 공격을 하는 종류: 걸어올 길은 없지만 울타리·물 너머로 쏠 수 있음(투사체가 넘어옴, 추정)");
        if ((r.move & EnemyMove.Jump) != 0) l.Add("뛰어 덮치는 종류: 울타리·물을 뛰어넘을 수 있음(추정; 구덩이를 넘는다는 관찰도 있으나(1-게임조사 2.2) 이 계산은 구덩이를 막힘으로 침)");
        if ((r.move & EnemyMove.Fly) != 0 && !r.walks && r.comes) l.Add("나는 종류: 울타리·물 위로 날아옴(규칙 enemy-path; 나는 종류인지는 이름으로 추정)");
        return l.Count == 0 ? "" : " " + string.Join(". ", l) + ".";
    }
    public static bool IsBreaker(string enemyId) => IsBreaker(enemyId, default);   // 재실측 N-g
    public static bool IsBreaker(string enemyId, TileBreak tb) { Breaker(enemyId, tb, out bool b); return b; }
    public static string BreakerNote(string enemyId) => BreakerNote(enemyId, default);
    public static string BreakerNote(string enemyId, TileBreak tb) => Breaker(enemyId, tb, out _);

    // 실측결함수정3 N1: the walls an enemy's bump damage gets at (DamageObjectStateRequest: damage − reduction ≥ hp/5);
    // hp and reduction from the game data (1-게임조사 2.3 table: Dirt Block 135/1, Wood Wall 150/1, Clay Block 179/22, Stone
    // Block 300/55, Scarlet Wall 550/135, Galaxite Wall 850/355)
    public static readonly (string name, int hp, int red)[] WallKinds =
    {
        ("진흙 블록", 135, 1), ("나무 벽", 150, 1), ("점토 블록", 179, 22), ("돌 블록", 300, 55), ("진홍색 벽", 550, 135), ("갈락사이트 벽", 850, 355),
    };
    public static bool BumpHits(int damage, int hp, int red) => damage - red >= hp / 5f;
    // 실측결함수정3 N1: kinds seen breaking the edge in a live run (no data read then; the data, when read, comes first)
    public static readonly Dictionary<string, string> MeasuredBreakers = new Dictionary<string, string>
    {
        ["Caveling"] = "벽을 부수는 종류(실측): 1마리가 소환 1초 뒤 자연 벽, 2.5초 뒤 놓은 흙 벽을 부수고 수십 초 안에 둘레 14곳을 뚫음(2026-11-06 실측, 규칙 wall-break-ways)",
    };
    // 실측결함수정3 N1: the kind decided from the game data first (TileBreak), then what was measured, then the reports
    // by name; a kind with data read and no tile damage at all is said so (not "not known")
    public static string Breaker(string enemyId, TileBreak tb, out bool breaker)
    {
        string n = enemyId ?? "";
        var parts = new List<string>();
        if (tb.known && tb.Any)
        {
            var d = new List<string>();
            if (tb.bump > 0)
            {
                var walls = new List<string>(); foreach (var w in WallKinds) if (BumpHits(tb.bump.Value, w.hp, w.red)) walls.Add(w.name);
                d.Add($"쫓다가 부딪힌 벽을 타일 피해 {tb.bump}로 때림(벽 감소를 뺀 값이 벽 체력/5 이상일 때만: {(walls.Count > 0 ? string.Join("·", walls) + "은 때림" : "흙 벽도 못 때림")})");
            }
            // C1: the door from the object key
            if (tb.obj > 0) d.Add($"쫓다가 부딪힌 물체(문 등)를 피해 {tb.obj}로 때림(문은 체력 2·피해 감소 0이라 때림)");
            else if (tb.chase && tb.obj == 0) d.Add("부딪힌 물체(문) 피해는 0(문은 직접 안 때림; 문 옆 벽이 부서지면 달린 문이 떨어짐)");
            else if (tb.chase) d.Add("부딪힌 물체(문) 피해 값은 못 읽음(모름)");
            if (tb.melee > 0) d.Add($"근접 공격이 칸에도 타일 피해 {tb.melee}(벽·바닥 덮개도 맞음, 체력/5 조건 없음)");
            if (tb.charge > 0) d.Add($"돌진이 부딪힌 자리 둘레에 타일 피해 {tb.charge}(체력/5 조건 없음)");
            parts.Add($"{(tb.obj > 0 && !(tb.bump > 0 || tb.melee > 0 || tb.charge > 0) ? "문을 부수는 종류" : "벽을 부수는 종류")}(게임 데이터): {string.Join(", ", d)}");
        }
        if (MeasuredBreakers.TryGetValue(n, out var measured)) parts.Add(measured);
        if (n == "MushroomBrute") parts.Add("벽을 부수는 종류: 돌진으로 벽을 부숨(흙 등급은 한 번에, 돌 이상은 대체로 버팀; 규칙 brute-breaks-walls, 게임+실측). 문·울타리·뿌리가 길 위에 있으면 돌진을 시작하지 않았음(단계 1 실측)");
        else if (n == "CavelingBrute") parts.Add("벽을 부수는 종류: 근접 공격의 타일 피해로 대부분의 벽과 바닥 덮개를 부숨(규칙 caveling-brute-breaks, 게임+웹)");
        else if (n == "BombScarab") parts.Add("벽을 부수는 종류: 자폭으로 벽을 부숨(규칙 other-breakers, 웹)");
        else if (n.Contains("Larva")) parts.Add("벽·횃불을 부순다는 보고가 있음(규칙 other-breakers, 웹 한 곳)");
        else if (n.Contains("Boss")) parts.Add("보스: 일부 보스가 블록·다리를 부순다는 보고가 있음(규칙 other-breakers, 웹)");
        breaker = parts.Count > 0;
        if (breaker) return string.Join(". ", parts);
        if (tb.known) return NoDataBreakNote(tb);
        return KnownNoBreaker;
    }

    // 합치기 전 수정 C1 / 검수 D6: data read and nothing in it hits a wall or a door — only the keys read are said (a key
    // not read is "모름", and then the sentence does not say "없음")
    public static string NoDataBreakNote(TileBreak tb)
    {
        var read = new List<string>(); var unread = new List<string>();
        void Ch(string name, int? v) { if (v.HasValue) read.Add($"{name} {v.Value}"); else unread.Add(name); }
        if (tb.chase) { Ch("부딪힌 벽 타일 피해", tb.bump); Ch("부딪힌 물체(문) 피해", tb.obj); }
        else read.Add("쫓다가 부딪힌 것 때리기 없음(그 상태가 없음)");
        Ch("근접 타일 피해", tb.melee); Ch("돌진 타일 피해", tb.charge);
        string vals = string.Join("·", read) + (unread.Count > 0 ? $"; 못 읽음(모름): {string.Join("·", unread)}" : "");
        return unread.Count > 0
            ? $"게임 데이터에서 읽은 값으로는 벽·문을 안 때림({vals}; 규칙 wall-break-ways)"
            : $"게임 데이터에 벽·문 피해 없음({vals}; 규칙 wall-break-ways)";
    }

    // fliers cross fences, water and pits (rules enemy-path: 날개박쥐·용암 나비·반딧불; the names are an estimate)
    public static bool IsFlier(string enemyId) { string n = enemyId ?? ""; return n.Contains("Bat") || n.Contains("Butterfly") || n.Contains("Firefly"); }

    // kill = the way to hit an enemy (the attack wording); otherwise a walk (the retreat: no kill wording, live-run fix)
    public static string ReachText(EnemyReach r, bool kill = true)
    {
        switch (r.kind)
        {
            case ReachKind.Open: return $"열린 길 {r.steps}칸";
            case ReachKind.Door: return kill ? $"문 ({r.door.x},{r.door.y})을 지나 {r.steps}칸(문을 열고 나가서 침; 지나간 뒤 문은 닫음)" : $"문 ({r.door.x},{r.door.y})을 지나 {r.steps}칸(문을 열고 지나감; 지나간 뒤 문은 닫음)";
            case ReachKind.Unseen: return "보이지 않는 곳을 지나야 닿음(길이 있는지 모름)";
            default: return "벽 너머라 지금 닿을 수 없음(열린 길도 자기 문도 없음)";
        }
    }

    // The facts of one enemy for the base: side and distance from the edge (when there is a base), the way, its kind.
    public static string KillFacts(BaseShape s, (int x, int y) enemyTile, EnemyReach r, string enemyId, float dist = -1f)
    {
        string side = "";
        if (s != null && s.inside.Count > 0)
        {
            bool ins = s.Inside(enemyTile.x, enemyTile.y);
            int ed = EdgeDistance(s, enemyTile);
            side = ins ? $"기지 안쪽, 둘레에서 {ed}칸. " : s.IsEdge(enemyTile.x, enemyTile.y) ? "기지 둘레 칸 위. " : $"기지 바깥, 둘레에서 {ed}칸. ";
        }
        string comes = r.comes ? "" : " 이 적이 나에게 걸어올 길은 지금 없음(걷는 적은 닫힌 문을 못 여는 것으로 봄, 추정)." + MemoryNote(r) + (r.kind == ReachKind.Door ? " " + DoorWeakNote + "." : "");
        // without a base only a known wall-breaking kind is worth the words (every attack option carries these facts)
        string bn = Breaker(enemyId, r.tileBreak, out bool brk);   // 실측결함수정3 N1: the game data first
        string kind = s != null && s.inside.Count > 0 || brk ? " " + bn + "." : "";
        return $"{side}닿는 길: {ReachText(r)}.{comes}{CloseNote(r, dist)}{MoveNote(r)}{kind}";
    }

    // Review S3: "cannot come" decided over remembered edge tiles says so (an unseen or far edge tile may have broken)
    // (recheck N3: only for an enemy whose answer memory changed — it would come by the tiles as seen — so an enemy far
    // from the base, sealed off anyway, gets no extra words)
    // 검수 F2/F3: each part says what the memory is (the last look, the plan, a large object seen) — no "if it broke
    // meanwhile" on a tile the AI saw broken (a hole seen open is read open, HEAD decision), and the large objects too
    // (they decided "cannot come" without a word, with a base and without one)
    public static string MemoryNote(EnemyReach r)
    {
        if (!r.memChanged || r.memEdge + r.memBeyond + r.memHidden + r.memPlanned + r.memBig == 0) return "";
        var parts = new List<string>();
        if (r.memHidden > 0) parts.Add($"마지막으로 볼 때 막혀 있던 둘레 {r.memHidden}칸은 그대로 막힘");
        if (r.memPlanned > 0) parts.Add($"이번에 다시 보지 못한 둘레 {r.memPlanned}칸은 계획대로 막힘");
        if (r.memEdge > 0) parts.Add($"본 적 없는 둘레 {r.memEdge}칸은 계획대로 막힘·문");
        if (r.memBeyond > 0) parts.Add($"멀어서 안 보이는 둘레 {r.memBeyond}칸은 계획대로");
        if (r.memBig > 0) parts.Add($"본 적 있는 큰 물체 {r.memBig}개({r.memBigNames})의 칸은 막힘");
        return $" 이 답은 지금 안 보이는 곳의 기억으로 정함({string.Join("; ", parts)}으로 봄; 지금 그곳은 안 보여 바뀌었는지 모름).";
    }

    // ------------------------------------------------------------------ broken parts

    // The remembered edge against the tiles now. gapTiles = the plan's cut tiles; doorSpot the plan's door tile;
    // ownWalls / ownDoors = what was placed for the edge (memory). An edge tile now open is: a placed wall gone, a placed
    // door gone, a planned gap tile not built yet (filling, not repair), or a natural edge tile opened.
    // seen (검수 F2, optional): the last looks — a tile in view is noted, a tile out of view that was open at its last look
    // stays a breach (remembered), so the known hole is still a repair option with its fact
    public static BreachScan Breaches(BaseShape s, ICollection<(int x, int y)> ownWalls, ICollection<(int x, int y)> ownDoors, Func<(int x, int y), EdgeNow> now, EdgeLastSeen seen = null)
    {
        var res = new BreachScan();
        var walls = new HashSet<long>(); foreach (var t in ownWalls) walls.Add(EdgeGrid.Key(t.x, t.y));
        var doors = new HashSet<long>(); foreach (var t in ownDoors) doors.Add(EdgeGrid.Key(t.x, t.y));
        foreach (var e in s.edge)
        {
            long k = EdgeGrid.Key(e.x, e.y);
            res.edgeTiles++;
            bool isDoor = doors.Contains(k), isWall = walls.Contains(k), isGap = s.gap.Contains(k);
            if (isDoor) res.doors++; else if (isWall) res.ownWalls++; else if (!isGap) res.natural++;
            var st = now(e);
            bool mem = false;
            if (st == EdgeNow.Unseen)
            {
                res.unseen++;
                // a gap tile not built is open by plan (no repair); 실측결함수정3 N4: a gap tile with an own door or wall on
                // it is built — the door hung in a one-tile gap (결정 B) that fell in view was dropped here out of view
                if (isGap && !isDoor && !isWall || seen?.Last(e.x, e.y) != true) continue;
                mem = true; res.remembered++;   // 검수 F2: seen open, out of view now
            }
            else if (seen != null) { if (st == EdgeNow.OwnDoor) seen.Forget(e.x, e.y); else seen.Note(e.x, e.y, st == EdgeNow.Open); }
            if (!mem && st != EdgeNow.Open) continue;
            if (isDoor) res.list.Add(new Breach { tile = e, kind = BreachKind.DoorGone, remembered = mem });
            else if (isWall) res.list.Add(new Breach { tile = e, kind = BreachKind.WallGone, side = s.sides.Contains(k), remembered = mem });
            else if (isGap) res.notBuilt++;
            else res.list.Add(new Breach { tile = e, kind = BreachKind.NaturalGone, side = s.sides.Contains(k), remembered = mem });   // live-run fix 5: a natural side wall of the hung door
        }
        return res;
    }

    // Repair batches: the broken tiles (a gone door is put back as a door, not here) grouped as openings (8-connected,
    // ordered along the run), each split into runs of at most MaxBatch; the last run of an opening closes it. stand(t) =
    // an inside walkable neighbour to place from (null: none; the game side then fails "closing from outside").
    public static List<EdgeBatch> RepairBatches(BreachScan b, Func<(int x, int y), (int x, int y)?> stand)
    {
        var tiles = new List<(int x, int y)>(); var sideTiles = new List<(int x, int y)>();
        foreach (var x in b.list) if (x.kind != BreachKind.DoorGone) (x.side ? sideTiles : tiles).Add(x.tile);
        var res = new List<EdgeBatch>();
        var groups = tiles.Count > 0 ? AIBaseEdgeCore.Group(new EdgeGrid(), tiles) : new List<EdgeGap>();
        for (int gi = 0; gi < groups.Count; gi++)
        {
            var run = groups[gi].tiles;
            for (int s0 = 0; s0 < run.Count; s0 += AIBaseEdgeCore.MaxBatch)
            {
                var eb = new EdgeBatch { gap = gi, repair = true };
                for (int i = s0; i < Math.Min(run.Count, s0 + AIBaseEdgeCore.MaxBatch); i++) eb.tiles.Add(run[i]);
                foreach (var t in run) eb.group.Add(t);
                eb.closesGap = s0 + AIBaseEdgeCore.MaxBatch >= run.Count;
                foreach (var t in eb.tiles) eb.stands.Add(stand(t) ?? t);
                res.Add(eb);
            }
        }
        // a door spot's side walls (off the cut, F5): their own batches; they close no opening (empty group: the game
        // side never takes one as the closing tile), so any stand will do
        for (int s0 = 0; s0 < sideTiles.Count; s0 += AIBaseEdgeCore.MaxBatch)
        {
            var eb = new EdgeBatch { gap = groups.Count, repair = true, closesGap = false };
            for (int i = s0; i < Math.Min(sideTiles.Count, s0 + AIBaseEdgeCore.MaxBatch); i++) { eb.tiles.Add(sideTiles[i]); eb.doorSides.Add(sideTiles[i]); eb.stands.Add(stand(sideTiles[i]) ?? sideTiles[i]); }
            res.Add(eb);
        }
        return res;
    }

    // ------------------------------------------------------------------ repair materials

    public static string MaterialFacts(int haveBlocks, string blockKinds, BreachScan b, int haveDoors, int gapLeft)
    {
        int broken = 0, doorsGone = 0; foreach (var x in b.list) { if (x.kind == BreachKind.DoorGone) doorsGone++; else broken++; }
        return $"보수 재료: 가진 벽 블록 {haveBlocks}개{(string.IsNullOrEmpty(blockKinds) ? "" : $"({blockKinds})")}, 가진 문 {haveDoors}개. "
            + $"둘레 {b.edgeTiles}칸(놓은 벽 {b.ownWalls}칸, 자연 벽·물체 {b.natural}칸, 문 {b.doors}개{(gapLeft > 0 ? $", 아직 안 막은 틈 칸 {gapLeft}칸" : "")}); "
            + $"지금 부서진 곳 {broken}칸{(doorsGone > 0 ? $"·없어진 문 {doorsGone}개" : "")}{(b.unseen > 0 ? $", 지금 안 보여 확인 못 한 둘레 {b.unseen}칸" : "")}. "
            + "적이 부순 벽은 아이템을 떨구지 않음(규칙 wall-no-drop): 보수할 블록은 따로 모아 둬야 함.";
    }

    // ------------------------------------------------------------------ what cannot be done now (review F3)

    // An option left out because it cannot be done (nothing to place, no stand, the game refuses the door) still tells
    // the action model that there is something to do and why it cannot be done now: these go into the blocked-target
    // notes of the input (AIBrain.Candidates blocks), not only the log.
    public static string RepairBlockedNote(BreachScan b, int haveBlocks, int batches)
    {
        var tiles = new List<string>(); int broken = 0;
        foreach (var x in b.list) { if (x.kind == BreachKind.DoorGone) continue; broken++; if (tiles.Count < 6) tiles.Add($"({x.tile.x},{x.tile.y})"); }
        return $"기지 둘레 보수: 부서진 곳 {broken}칸({string.Join(" ", tiles)}{(broken > tiles.Count ? " …" : "")}, 묶음 {batches}개), 가진 벽 블록 {haveBlocks}개 — 놓을 수 없음(보수 재료가 필요; 적이 부순 벽은 블록을 떨구지 않음, wall-no-drop)";
    }

    public static string DoorBackBlockedNote((int x, int y) t, bool noDoor, bool noStand, string sidesNeed)
    {
        var why = new List<string>();
        if (noDoor) why.Add("가방에 문 없음(문을 만들거나 가져와야 함)");
        if (noStand) why.Add("안쪽에 설 칸이 없음(문은 안쪽에서만 놓음)");
        if (!string.IsNullOrEmpty(sidesNeed)) why.Add(sidesNeed);
        return $"기지 문 다시 달기 ({t.x},{t.y}): {string.Join("; ", why)} — 지금은 이 칸이 열려 있어 적도 지나감";
    }

    // ------------------------------------------------------------------ repair materials: where (review F4, S8)

    public struct MatSpot { public (int x, int y) tile; public int steps; public ReachKind kind; public (int x, int y) door; }

    // Walls the AI can mine for blocks, nearest first (at most `max`): reached by an open walk, or through an own door
    // (a base closed all round: every wall reached by an open walk from inside is an edge wall, so the materials lie
    // outside the door). The caller leaves out edge walls and walls that give no wall block.
    public static List<MatSpot> MaterialSpots(ReachMap m, IEnumerable<(int x, int y)> walls, int max = 3)
    {
        var res = new List<MatSpot>();
        foreach (var w in walls)
        {
            MatSpot? best = null;
            for (int k = 0; k < 4; k++)
            {
                int x = w.x + Dx[k], y = w.y + Dy[k];
                if (!m.g.In(x, y)) continue;
                int i = m.g.Idx(x, y);
                MatSpot? c = null;
                if (m.open[i] >= 0) c = new MatSpot { tile = w, steps = m.open[i], kind = ReachKind.Open };
                else if (m.door[i] >= 0 && m.via[i] >= 0) c = new MatSpot { tile = w, steps = m.door[i], kind = ReachKind.Door, door = (m.g.loX + m.via[i] / m.g.n, m.g.loY + m.via[i] % m.g.n) };
                if (c.HasValue && (!best.HasValue || c.Value.steps < best.Value.steps || c.Value.steps == best.Value.steps && c.Value.kind == ReachKind.Open)) best = c;
            }
            if (best.HasValue) res.Add(best.Value);
        }
        res.Sort((a, b) => a.steps != b.steps ? a.steps.CompareTo(b.steps) : a.tile.x != b.tile.x ? a.tile.x.CompareTo(b.tile.x) : a.tile.y.CompareTo(b.tile.y));
        if (res.Count > max) res.RemoveRange(max, res.Count - max);
        return res;
    }

    public static string MatWayText(MatSpot s) => s.kind == ReachKind.Door ? $"문 ({s.door.x},{s.door.y})을 지나 {s.steps}칸(나가서 캠; 지나간 뒤 문은 닫음)" : $"열린 길 {s.steps}칸";

    // ------------------------------------------------------------------ the breach trigger (review S4)

    public sealed class BreachWatch { public int count; public int plan = -1; public string pending = ""; }

    // The reason for one action-model call when the broken parts in view grew since the last look; a new plan starts
    // the count again (the old plan's count no longer means anything).
    // Live-run fix 7 (사실 7: breaches 1 -> 3 logged, the trigger in 0 calls): the reason was put into the waiting call
    // reason, and the next "skill ended" (AIBrain.OnSkillEnded) overwrote it before the call went out (the gap between
    // calls while a skill runs is 2.5 s), with the count already moved on. Now the reason also waits in `pending` until a
    // call carries it (TakePending when the input is built).
    public static string BreachTrigger(BreachWatch w, int plan, List<Breach> list)
    {
        if (w.plan != plan) { w.plan = plan; w.count = 0; w.pending = ""; }
        int n = list == null ? 0 : list.Count;
        int before = w.count; w.count = n;
        if (n <= before) return null;
        string r = $"기지 둘레 부서짐 {n}곳({string.Join(", ", list.ConvertAll(b => b.Text))})";
        w.pending = r;   // the latest count says it all (an older pending one is replaced)
        return r;
    }

    // the breach reason waiting for a call: added to the call's trigger once, then cleared. plan (review S5): the plan
    // now; a reason of another plan (the memory cleared, a new plan picked) is dropped
    public static string TakePending(BreachWatch w, string trigger, int? plan = null)
    {
        if (w == null || string.IsNullOrEmpty(w.pending)) return trigger;
        if (plan.HasValue && w.plan != plan.Value) { w.pending = ""; return trigger; }
        string r = AddReason(trigger, w.pending); w.pending = "";
        return r;
    }

    // a trigger that comes while another reason is already waiting is added to it (it used to be dropped while its
    // count had already moved on, so it never rang again)
    public static string AddReason(string cur, string add)
    {
        if (string.IsNullOrEmpty(add)) return cur ?? "";
        if (string.IsNullOrEmpty(cur)) return add;
        return cur.Contains(add) ? cur : cur + "; " + add;
    }

    // ------------------------------------------------------------------ placing from the chosen side (fix 8, decision A)

    // Live-run fix 8 (사실 8): a tile placed from a given stand only is placed from that side: the AI stands past the
    // tile's edge towards the stand (more than half a tile from the tile centre along tile -> stand). Being within reach
    // from the other side is not enough (the door (-10,-18) went in from outside at (-10,-18.86)).
    public static bool OnStandSide((int x, int y) tile, (int x, int y) stand, float mx, float my)
    {
        float sx = stand.x - tile.x, sy = stand.y - tile.y, l2 = sx * sx + sy * sy;
        if (l2 < 1e-6f) return true;
        return ((mx - tile.x) * sx + (my - tile.y) * sy) / (float)Math.Sqrt(l2) >= 0.5f;
    }

    // User decision A (2026-11-05): the tile (or door) that closes an opening may be placed from outside too — both sides
    // are options and the action model chooses. The AI is never shut out for good: it comes in through the edge's door or
    // by breaking one wall. door = an own door on the edge now (or the door being hung), null: none.
    // Review F3 (2026-11-06, HEAD decision): the two sentences only when closing it shuts the edge; when open places stay
    // (a gap tile not built, the door spot without its door, a broken part), how many and the nearest: the way in.
    public static string OutsideCloseText((int x, int y)? door) => OutsideCloseText(door, null, (0, 0));
    public static string OutsideCloseText((int x, int y)? door, ICollection<(int x, int y)> openAfter, (int x, int y) from)
    {
        if (openAfter != null && openAfter.Count > 0)
        {
            var places = AIBaseEdgeCore.Group(new EdgeGrid(), new List<(int x, int y)>(openAfter));
            (int x, int y) best = (0, 0); long bd = long.MaxValue;
            foreach (var t in openAfter) { long d = (long)(t.x - from.x) * (t.x - from.x) + (long)(t.y - from.y) * (t.y - from.y); if (d < bd) { bd = d; best = t; } }
            return $"바깥에서 닫음, 둘레에 아직 열린 곳 {places.Count}곳 있음(가장 가까운 ({best.x},{best.y})) — 그쪽으로 드나들 수 있음";
        }
        return door.HasValue ? $"바깥에서 닫음, 둘레 문 ({door.Value.x},{door.Value.y})로 드나듦" : "바깥에서 닫음, 둘레에 문 없음, 들어가려면 벽 하나를 부숴야 함";
    }

    // Review F3: the edge tiles open now (gap tiles not built, the door spot without its door among them, and the
    // broken parts that open a way: a gone wall / natural tile / door; a door spot's side wall off the cut is no way in),
    // less the ones the option closes
    public static List<(int x, int y)> OpenAfter(IEnumerable<(int x, int y)> gapOpen, BreachScan scan, IEnumerable<(int x, int y)> closes)
    {
        var shut = new HashSet<(int, int)>(); if (closes != null) foreach (var t in closes) shut.Add(t);
        var seen = new HashSet<(int, int)>(); var res = new List<(int x, int y)>();
        void Add((int x, int y) t) { if (!shut.Contains(t) && seen.Add(t)) res.Add(t); }
        if (gapOpen != null) foreach (var t in gapOpen) Add(t);
        if (scan != null) foreach (var b in scan.list) if (!(b.side && b.kind == BreachKind.WallGone)) Add(b.tile);
        return res;
    }

    // ------------------------------------------------------------------ an own door passed: closed after (fix 3)

    public const float DoorTailReach = 2.5f;   // the AI closes an owed door from this near (tiles to its centre) when a skill ends

    // the AI's stand tile this frame: in an own door that stands open = that door is owed a close (it went into it)
    // 33-재검수 N3: cameFrom = the last tile the AI stood on off the door (kept only as a neighbour of the door; null: not known)
    public static void NoteDoorTile(DoorOwe o, (int x, int y) standTile, bool ownOpenDoorHere, (int x, int y)? cameFrom = null)
    {
        if (!ownOpenDoorHere) return;
        o.door = standTile;
        o.from = cameFrom.HasValue && cameFrom.Value != standTile && Math.Abs(cameFrom.Value.x - standTile.x) <= 1 && Math.Abs(cameFrom.Value.y - standTile.y) <= 1 ? cameFrom : null;
    }

    // 33-재검수 N3: the side to close a passed door from when no inside is known (my doors; an edge door with no plan), +1 /
    // -1 along the passage axis: the side the AI was going to (away from the tile it came into the door from), else the
    // side that can be stood on (plusStand: door + axis; minusStand: door − axis); 0 when neither is known. StartTail
    // takes it as the side in the door tile, the other side only when this one cannot be stood on and that one can.
    // (Before, with no inside known, the side was the sign of the AI's offset in the door tile, under half a tile.)
    public static int PassSide((int x, int y) door, (int x, int y)? from, bool passageEW, bool plusStand, bool minusStand)
    {
        int s = !from.HasValue ? 0 : passageEW ? from.Value.x - door.x : from.Value.y - door.y;
        if (s != 0) return s > 0 ? -1 : 1;
        return plusStand ? 1 : minusStand ? -1 : 0;
    }

    // Recheck N1: the close option of the door the tail is closing now says so; choosing it continues the tail (no new
    // close from the start)
    public static bool TailContinues(DoorOwe o, string key) => o != null && o.tail && o.door.HasValue && key == $"doorclose:{o.door.Value.x},{o.door.Value.y}";
    public static string TailNote(DoorOwe o)
    {
        if (o == null || !o.tail || !o.door.HasValue) return "";
        string left = o.st == null || o.st.presses == 0 ? "문 앞 자리로 가서 E로 닫기" : $"E {o.st.presses}번 누름, 닫힘 확인";
        return $" 지금 이 문을 닫는 중(앞 기술이 끝난 뒤의 닫기; 남은 단계: {left}). 이것을 고르면 이어서 닫음(처음부터 다시 하지 않음); 다른 행동을 고르면 닫기가 끊김.";
    }

    // a door seen shut (or gone) is owed nothing
    public static void NoteDoorState(DoorOwe o, bool exists, bool open)
    {
        if (o.door.HasValue && (!exists || !open) && !o.tail) o.door = null;
    }

    // At a skill's end: run the closing tail? (an owed own door still open, the AI near it, not down)
    public static bool TailNeeded(DoorOwe o, bool open, float distToDoor, bool down)
        => o.door.HasValue && open && !down && distToDoor <= DoorTailReach;

    // The tail closes from the side the AI is on (passageEW: the door's passage runs east-west). Returns the first step.
    // Review S1 (2026-11-06): in the door tile itself (less than half a tile off its centre along the passage) the side
    // is no fact of where the AI is: it closes from the inside stand (insideSign: +1 / -1 along the passage axis
    // towards the inside, 0 unknown; insideStand / outsideStand: that tile can be stood on); the outside close (it
    // failed in the live run, 14-단계3-측정 4절) only when the inside cannot be used.
    public static void StartTail(DoorOwe o, bool passageEW, float meX, float meY, int insideSign = 0, bool insideStand = true, bool outsideStand = true)
    {
        var d = o.door.Value;
        float ax = passageEW ? 1f : 0f, ay = passageEW ? 0f : 1f;
        float side = (meX - d.x) * ax + (meY - d.y) * ay;
        if (Math.Abs(side) < 0.5f && insideSign != 0)
            side = insideStand || !outsideStand ? insideSign : -insideSign;
        o.dirX = side >= 0f ? ax : -ax; o.dirY = side >= 0f ? ay : -ay;   // from the door towards the side it closes from
        o.st = new DoorPassState { phase = DoorPhase.Close };
        o.tail = true; o.fact = "";
    }

    // one frame of the tail; done = finished (closed or failed); the fact goes on the skill's end
    public static DoorStepOut TailStep(DoorOwe o, bool open, bool pressDone, float meX, float meY, float now, out bool done)
    {
        var d = o.door.Value;
        var r = AIDoorPass.Step(o.st, DoorKind.Manual, d.x, d.y, o.dirX, o.dirY, meX, meY, open, pressDone, now);
        done = o.st.phase == DoorPhase.Done || o.st.phase == DoorPhase.Failed;
        if (done)
        {
            o.fact = o.st.phase == DoorPhase.Done ? $"지난 문 ({d.x},{d.y}) 닫음({o.st.facts})" : $"지난 문 ({d.x},{d.y}) 닫기 실패: {o.st.fail} — 열린 채(적도 지나감)";
            if (o.st.phase == DoorPhase.Done) o.door = null;
            o.tail = false;
        }
        return r;
    }

    // ------------------------------------------------------------------ back into the base

    // The retreat point: a walkable inside tile (stand(t): walkable, not the spawn pad). Inside already: the one nearest
    // the base seed. Outside: the inside tile the AI reaches with the fewest steps (open, else through an own door);
    // none reached in the window: the one nearest the seed (its way is not known here).
    public static (int x, int y)? InsideTarget(BaseShape s, Func<(int x, int y), bool> stand, (int x, int y) seed, ReachMap m, bool aiInside, out EnemyReach how)
    {
        how = new EnemyReach { kind = ReachKind.None, steps = -1 };
        (int, int)? bestSeed = null; long bsd = long.MaxValue;
        (int, int)? bestWalk = null; int bw = int.MaxValue; EnemyReach bwHow = how;
        foreach (var key in s.inside)
        {
            int x = (int)(key >> 32), y = (int)(uint)key;
            if (!stand((x, y))) continue;
            long sd = (long)(x - seed.x) * (x - seed.x) + (long)(y - seed.y) * (y - seed.y);
            if (sd < bsd || sd == bsd && (x < bestSeed.Value.Item1 || x == bestSeed.Value.Item1 && y < bestSeed.Value.Item2)) { bsd = sd; bestSeed = (x, y); }
            if (aiInside || m == null || !m.g.In(x, y)) continue;
            int i = m.g.Idx(x, y);
            int st = m.open[i] >= 0 ? m.open[i] : m.door[i] >= 0 && m.via[i] >= 0 ? m.door[i] : -1;
            if (st < 0 || st >= bw) continue;
            bw = st; bestWalk = (x, y);
            bwHow = m.open[i] >= 0 ? new EnemyReach { kind = ReachKind.Open, steps = st } : new EnemyReach { kind = ReachKind.Door, steps = st, door = (m.g.loX + m.via[i] / m.g.n, m.g.loY + m.via[i] % m.g.n) };
        }
        if (bestWalk.HasValue) { how = bwHow; return bestWalk; }
        if (bestSeed.HasValue && m != null && m.g.In(bestSeed.Value.Item1, bestSeed.Value.Item2))
        {
            int i = m.g.Idx(bestSeed.Value.Item1, bestSeed.Value.Item2);
            if (m.open[i] >= 0) how = new EnemyReach { kind = ReachKind.Open, steps = m.open[i] };
            else if (m.door[i] >= 0 && m.via[i] >= 0) how = new EnemyReach { kind = ReachKind.Door, steps = m.door[i], door = (m.g.loX + m.via[i] / m.g.n, m.g.loY + m.via[i] % m.g.n) };
            else if (m.maybe[i]) how = new EnemyReach { kind = ReachKind.Unseen, steps = -1 };
        }
        return bestSeed;
    }

    // ------------------------------------------------------------------ pick a placed object up (and place it again)

    // One step of picking up a placed object: hold a pick or the empty hand (never a melee weapon), stand next to it and
    // swing; its health must drop within NoChangeSeconds of each hit; once it is gone, wait CollectSeconds for the bag
    // to gain it; then (replace) place it again (the game side runs the place). No branch of its own beyond these ends.
    public static PickupOut PickupStep(PickupState s, PickupObs o)
    {
        var r = new PickupOut();
        if (!o.objectThere) s.goneSeen = true;
        switch (s.phase)
        {
            case PickupPhase.Hold:
                if (!o.holdOk) return Fail(s, "곡괭이도 비울 손 칸도 없음(놓은 물체는 곡괭이나 빈손으로 쳐야 함)");
                if (o.heldMelee) return Fail(s, "손에 들 것이 근접 무기뿐(근접 무기는 놓은 물체를 맞히지 못함)");
                s.bag0 = o.bagCount;
                s.phase = PickupPhase.Approach;
                return PickupStep(s, o);
            case PickupPhase.Approach:
                if (!o.objectThere) { s.phase = PickupPhase.Collect; s.goneAt = o.now; return PickupStep(s, o); }
                if (o.dist <= PickupState.HitRange) { s.phase = PickupPhase.Hit; s.hitSince = o.now; if (s.hp0 < 0) s.hp0 = o.objHp; s.lastHp = o.objHp; return PickupStep(s, o); }
                r.walk = true; return r;
            case PickupPhase.Hit:
                if (!o.objectThere) { s.facts += $"체력 {s.lastHp}→0 "; s.phase = PickupPhase.Collect; s.goneAt = o.now; return PickupStep(s, o); }
                if (o.objHp < s.lastHp) { s.facts += $"체력 {s.lastHp}→{o.objHp} "; s.lastHp = o.objHp; s.hitSince = o.now; }
                if (o.now - s.hitSince > PickupState.NoChangeSeconds) return Fail(s, $"{PickupState.NoChangeSeconds:F0}초 동안 쳐도 체력이 줄지 않음(체력 {s.hp0}→{o.objHp}; 같은 휘두름에 적이 맞으면 물체는 안 맞음)");
                if (o.dist > PickupState.HitRange + 0.3f) { s.phase = PickupPhase.Approach; r.walk = true; return r; }
                r.swing = true; return r;
            case PickupPhase.Collect:
                if (o.bagCount > s.bag0) { s.facts += "가방 +1 "; s.phase = s.replace ? PickupPhase.Place : PickupPhase.Done; if (s.replace) r.place = true; return r; }
                if (o.now - s.goneAt > PickupState.CollectSeconds) return Fail(s, $"물체는 없어졌는데 {PickupState.CollectSeconds:F0}초 안에 가방에 들어오지 않음(땅에 떨어졌을 수 있음)");
                return r;
            case PickupPhase.Place:
                r.place = true; return r;
        }
        return r;
    }

    private static PickupOut Fail(PickupState s, string why) { s.phase = PickupPhase.Failed; s.fail = why; return new PickupOut(); }

    // review S3: the edge memory forgets the old tile once the object is gone from it — also when the pick-up then fails
    // (not in the bag within 3 s): otherwise the next look calls it "a placed door gone" and offers a door back there
    public static bool ShouldForget(PickupState s) => s.phase == PickupPhase.Place || s.phase == PickupPhase.Done || s.goneSeen;

    // review S3: what to hit a placed object with: a working pick, else the empty hand (an empty hotbar slot, or one made
    // empty by moving a hotbar item into the bag), a broken pick (durability 0) only when neither is possible — whether a
    // broken tool still hits is not measured
    public enum PickupHold { WorkingPick, EmptyHand, FreeHand, BrokenPick, None }
    public static PickupHold PickupHoldChoice(bool workingPick, bool emptyHand, bool canFreeHand, bool brokenPick)
        => workingPick ? PickupHold.WorkingPick : emptyHand ? PickupHold.EmptyHand : canFreeHand ? PickupHold.FreeHand : brokenPick ? PickupHold.BrokenPick : PickupHold.None;

    // the tool facts of a pick-up option; a pick swing is not filtered like a melee weapon's, so it can also hit the
    // edge walls next to the object (estimate)
    public static string PickupToolNote(string holdWhat, bool isPick)
        => $"{holdWhat}(곡괭이나 빈손; 근접 무기로는 놓은 물체가 맞지 않음)로 두 번 쳐서 가방에 넣기"
           + (isPick ? ". 곡괭이 휘두름은 근접 무기처럼 대상을 가리지 않아 바로 옆 둘레 벽도 맞을 수 있음(벽 체력이 줄 수 있음, 추정)" : "");

    // ------------------------------------------------------------------ review G6

    // The base options (kill facts on attacks, repair, door back, pick-up, repair materials, back into the base) are never
    // progress of another goal: "continue the goal" in a close fight and the goal-stall checks look only at real goal
    // steps (단계 2 review G6). The game side sets AICandidate.progress from this.
    public static bool CountsAsGoalProgress(string kind) => false;

    // Review F2 (2026-11-06): what a stop does to the closing tail of open -> through -> close. A hard stop (the host's
    // /ai stop, the AI leaving) cuts a running tail (CutTail) or just ends (End); a stop the program makes lets a running
    // tail finish (KeepTail) or, with none running, ends the skill through the tail check first (TryTail: TailNeeded,
    // an owed own door still open next to the AI is closed before the end is reported). A new choice of the action
    // model is no stop: it starts a new skill and cuts the tail (the door stays owed, the close option is offered).
    public enum TailOnStop { End, CutTail, KeepTail, TryTail }
    public static TailOnStop OnStop(bool hard, bool tailOn) => hard ? (tailOn ? TailOnStop.CutTail : TailOnStop.End) : tailOn ? TailOnStop.KeepTail : TailOnStop.TryTail;
    public static readonly string[] Kinds = { "edgefill", "edgerepair", "edgemat", "edgepickup", "doorclose", "place", "retreat" };
}
