// AIAgents — several AI characters in one hosted world, one per chatroom member (claude, gpt, gemini, grok).
// Report: work/2026-11/2026-11-04-멤버넷-캐릭터.md (session 코어키퍼-AI-캐릭터).
//
// Each character is a real player of its own: its own thin-client world, PugNetworkInterface on 127.0.0.1:<Port> and
// connection, its own character file (AIPlayers/<world>/<Name>.json) and playerGuid (Hash("AIPlayerSpike:"+world+":"+Name)),
// its own brain/body/memory/vitals/planner state, Decisions calls and goal.
//
// How the per-character state works: the existing classes (AIBrain, AIBody, AIMemory, ...) stay static, but every
// per-character field is a static property that reads and writes AIAgent.Cur's State object (Slots[k]). The mod ticks
// each joined character inside AIAgent.Enter(agent), and chat commands run inside the addressed character's scope. Code
// that runs outside any character (Harmony patches, packet pump) looks the character up and enters it explicitly.
// Outside a scope AIAgent.Cur is Claude (the old single-AI behaviour).
using System;
using Unity.Networking.Transport;

// Slot map: every per-character State has one slot, and the number is the only link, so keep this list complete.
internal static class AISlot
{
    public const int Mod = 0;              // AIPlayerSpikeMod (stage, thin world, network)
    public const int CharacterStore = 1;   // AICharacterStore
    public const int Body = 2;             // AIBody
    public const int Brain = 3;            // AIBrain
    public const int Memory = 4;           // AIMemory
    public const int Drops = 5;            // AIDrops
    public const int Tunnel = 6;           // AITunnel
    public const int Vitals = 7;           // AIVitals
    public const int Planner = 8;          // AIPlanner
    public const int BreakSkill = 9;       // BreakSkill (AIBreakSkills.cs)
    public const int Motion = 10;          // AIMotion
    public const int Teammates = 11;       // AITeammates
    public const int Base = 12;            // AIBase
    public const int Farm = 13;            // AIFarm
    public const int Cook = 14;            // AICook
    public const int Goals = 15;           // AIGoals
    public const int BaseEdge = 16;        // AIEdge (코어-안전지대 단계 2)
    public const int PlaceGoal = 17;       // AIPlaceGoal (제작대-놓기)
    public const int LifeTree = 18;
    public const int Count = 19;
}

public sealed class AIAgent
{
    public readonly int Index;
    public readonly string Id;            // chatroom member id: claude | gpt | gemini | grok
    public readonly string Name;          // in-game name (also the character file name and playerGuid seed)
    public readonly string CharacterGuid; // CharacterData.characterGuid of a newly created character
    public readonly ushort Port;          // thin-client interface endpoint 127.0.0.1:Port
    public readonly NetworkEndpoint Endpoint;
    // Per-character State objects, one slot per owning class (the slot map is AISlot above; each StateOf reads its slot
    // through Get<State>(AISlot.X)).
    //   Not in a slot: AIExperience (Dictionary<AIAgent, State>), AIKnowledge (Dictionary<member id, State>).
    internal readonly object[] Slots = new object[AISlot.Count];
    // The slot's State, created on first use.
    internal T Get<T>(int slot) where T : class, new() => (T)(Slots[slot] ?? (Slots[slot] = new T()));

    // per-character cost of the mod's own work (brain + body tick + input), measured in AIPlayerSpikeMod
    public double TickMsSum, TickMsMax; public long TickN;

    private AIAgent(int index, string id, string name, string guid, ushort port)
    {
        Index = index; Id = id; Name = name; CharacterGuid = guid; Port = port;
        Endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(port);
    }

    // Member table (same ids as the chatroom, lib/members.mjs). Claude keeps its old guid, name and port 7778.
    public static readonly AIAgent[] All =
    {
        new AIAgent(0, "claude", "Claude", "c1a0de0000000000000000000000a001", 7778),
        new AIAgent(1, "gpt", "ChatGPT", "c1a0de0000000000000000000000a002", 7779),
        new AIAgent(2, "gemini", "Gemini", "c1a0de0000000000000000000000a003", 7780),
        new AIAgent(3, "grok", "Grok", "c1a0de0000000000000000000000a004", 7781),
    };

    private static AIAgent _cur;
    public static AIAgent Cur => _cur ?? Outside();
    public static AIAgent InScope => _cur;   // null outside a character scope

    // TEST ONLY (refactor S7, behind AIPlayers/testcmds.on): count Cur reads outside a character scope (they fall back to
    // Claude) to list where they come from. One count line per 60 s window, written at the first such read after the window;
    // a stack only for the first 3 reads of a window (reading a stack is costly, review R11). Off: only the flag check.
    private static AIAgent Outside()
    {
        if (AITestSwitches.Test) NoteOutside();
        return All[0];
    }
    private const int OutsideWindowMs = 60000;
    private static int _outsideN, _outsideStart;
    private static bool _outsideStarted, _outsideBusy;
    private static void NoteOutside()
    {
        if (_outsideBusy) return;
        _outsideBusy = true;
        try
        {
            int now = Environment.TickCount;
            if (!_outsideStarted) { _outsideStarted = true; _outsideStart = now; }
            else if (now - _outsideStart >= OutsideWindowMs)
            {
                AIPlayerSpikeMod.Log($"TEST-ONLY Cur outside scope: {_outsideN} reads in {(now - _outsideStart) / 1000}s");
                _outsideN = 0; _outsideStart = now;
            }
            _outsideN++;
            if (_outsideN <= 3)
            {
                var st = new System.Diagnostics.StackTrace(2, false);
                var frames = new System.Collections.Generic.List<string>();
                for (int i = 0; i < st.FrameCount && frames.Count < 8; i++)
                {
                    var m = st.GetFrame(i)?.GetMethod();
                    if (m != null) frames.Add((m.DeclaringType != null ? m.DeclaringType.Name + "." : "") + m.Name);
                }
                AIPlayerSpikeMod.Log($"TEST-ONLY Cur outside scope #{_outsideN}: {string.Join(" < ", frames)}");
            }
        }
        catch { }
        finally { _outsideBusy = false; }
    }

    public struct Scope : IDisposable
    {
        internal AIAgent prev;
        public void Dispose() { _cur = prev; }
    }

    public static Scope Enter(AIAgent a)
    {
        var s = new Scope { prev = _cur };
        _cur = a;
        return s;
    }

    // A chat token naming a member: id, in-game name, CLI name or Korean name. null if none.
    public static AIAgent Find(string token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        string t = token.Trim().ToLowerInvariant();
        foreach (var a in All) if (t == a.Id || t == a.Name.ToLowerInvariant()) return a;
        switch (t)
        {
            case "클로드": return All[0];
            case "codex": case "openai": case "gpt5": case "지피티": case "챗지피티": return All[1];
            case "agy": case "제미나이": case "제미니": return All[2];
            case "그록": return All[3];
        }
        return null;
    }

    public static AIAgent ById(string id)
    {
        foreach (var a in All) if (a.Id == id) return a;
        return null;
    }

    public override string ToString() => Name;
}

// Performance of several AI characters (test aid, always on, one log line per 30 s while hosted): host frame time
// (Time.unscaledDeltaTime: the whole process, server and every thin client included), server ticks per second
// (NetworkTime.ServerTick progress in the server world), and each joined character's mod work per frame (brain + body +
// input, AIPlayerSpikeMod.Update).
public static class AIPerf
{
    private const float Window = 30f;
    private static readonly System.Collections.Generic.List<float> _dt = new System.Collections.Generic.List<float>(4096);
    private static float _start = -1f;
    private static uint _tick0;
    private static bool _tickOk;
    public static string Last = "";
    // every frame of the last few minutes (time, ms): hitches during one fight (COMBAT end line)
    private static readonly System.Collections.Generic.List<(float t, float ms)> _ring = new System.Collections.Generic.List<(float, float)>(30000);
    public static string HitchNote(float t0, float t1)
    {
        int n = 0, h25 = 0, h33 = 0, h50 = 0; float worst = 0f; double sum = 0;
        foreach (var f in _ring)
        {
            if (f.t < t0 || f.t > t1) continue;
            n++; sum += f.ms; if (f.ms > worst) worst = f.ms;
            if (f.ms > 25f) h25++; if (f.ms > 33.4f) h33++; if (f.ms > 50f) h50++;
        }
        float secs = UnityEngine.Mathf.Max(t1 - t0, 0.001f);
        return $"frames {n} ({n / secs:F0} fps, avg {(n > 0 ? sum / n : 0):F1}ms), hitches >25ms {h25} ({h25 / secs:F2}/s) >33ms {h33} >50ms {h50}, worst {worst:F0}ms";
    }

    public static void Frame(Unity.Entities.World server)
    {
        float now = UnityEngine.Time.realtimeSinceStartup;
        float dtMs = UnityEngine.Time.unscaledDeltaTime * 1000f;
        _ring.Add((now, dtMs)); if (_ring.Count > 30000) _ring.RemoveRange(0, 10000);
        if (_start < 0f) { Begin(server, now); return; }
        _dt.Add(dtMs);
        if (now - _start < Window) return;
        float secs = now - _start;
        var l = new System.Collections.Generic.List<float>(_dt); l.Sort();
        float P(float q) => l.Count == 0 ? 0f : l[UnityEngine.Mathf.Clamp(UnityEngine.Mathf.CeilToInt(q * l.Count) - 1, 0, l.Count - 1)];
        double sum = 0; foreach (var x in l) sum += x;
        uint t1 = ServerTick(server, out bool ok1);
        string tps = _tickOk && ok1 ? $"{(t1 - _tick0) / secs:F1}" : "?";
        var parts = new System.Collections.Generic.List<string>();
        int joined = 0;
        foreach (var a in AIAgent.All)
        {
            if (!AIPlayerSpikeMod.IsJoined(a)) { a.TickMsSum = 0; a.TickN = 0; a.TickMsMax = 0; continue; }
            joined++;
            parts.Add($"{a.Name} {(a.TickN > 0 ? a.TickMsSum / a.TickN : 0):F2}ms avg/{a.TickMsMax:F1}ms max");
            a.TickMsSum = 0; a.TickN = 0; a.TickMsMax = 0;
        }
        Last = $"PERF {secs:F0}s: AIs in world {joined}; frames {l.Count} ({l.Count / secs:F1} fps); frame ms avg {(l.Count > 0 ? sum / l.Count : 0):F2} p50 {P(0.5f):F2} p95 {P(0.95f):F2} p99 {P(0.99f):F2} max {P(1f):F1}; server ticks/s {tps}; mod per AI [{string.Join(", ", parts)}]";
        using (AIAgent.Enter(null)) AIPlayerSpikeMod.Log(Last);
        Begin(server, now);
    }

    private static void Begin(Unity.Entities.World server, float now)
    {
        _start = now; _dt.Clear();
        _tick0 = ServerTick(server, out _tickOk);
    }

    private static uint ServerTick(Unity.Entities.World server, out bool ok)
    {
        ok = false;
        try
        {
            using (var q = server.EntityManager.CreateEntityQuery(Unity.Entities.ComponentType.ReadOnly<Unity.NetCode.NetworkTime>()))
            {
                if (q.CalculateEntityCount() != 1) return 0;
                var nt = q.GetSingleton<Unity.NetCode.NetworkTime>();
                if (!nt.ServerTick.IsValid) return 0;
                ok = true;
                return nt.ServerTick.TickIndexForValidTick;
            }
        }
        catch { return 0; }
    }
}

// Other players on this character's own screen (the host and the other AI characters), as a human player sees them
// (work/2026-11/2026-11-04-전투안정-동료인식.md §2): the name tag, where they are, their health bar, whether they walk
// or swing (visible animation), and an enemy right next to them. Only what is inside this character's human-sized view
// (AIVision); nothing is shared between characters (no memory, no goals, no messages).
public class SeenPlayer
{
    public Unity.Entities.Entity entity;
    public string name;
    public bool isAi;            // another AI character (one of AIAgent.All)
    public Unity.Mathematics.float3 pos;
    public float dist;
    public string dir;           // 동/서/북/남 from this character
    public int health, maxHealth;
    public string doing = "";    // observable: walking (direction) / swinging / standing / down
    public string role => isAi ? "AI 동료" : "호스트";
}

public static class AITeammates
{
    private sealed class State
    {
        public readonly System.Collections.Generic.Dictionary<Unity.Entities.Entity, (Unity.Mathematics.float3 pos, float t)> last = new System.Collections.Generic.Dictionary<Unity.Entities.Entity, (Unity.Mathematics.float3, float)>();
        public System.Collections.Generic.List<SeenPlayer> seen = new System.Collections.Generic.List<SeenPlayer>();
        public float at = -1f;
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Teammates);
    private static State S => StateOf(AIAgent.Cur);

    // The last list made for this character (Visible), for notes made later in the same decision.
    public static System.Collections.Generic.List<SeenPlayer> Last => S.seen;

    public static System.Collections.Generic.List<SeenPlayer> Visible(Unity.Entities.World server, Unity.Entities.Entity player, System.Collections.Generic.List<SeenEnemy> enemies = null)
    {
        var s = S;
        float now = UnityEngine.Time.realtimeSinceStartup;
        var res = new System.Collections.Generic.List<SeenPlayer>();
        var em = server.EntityManager;
        if (!em.Exists(player)) return res;
        var me = em.GetComponentData<Unity.Transforms.LocalTransform>(player).Position;
        using (var q = em.CreateEntityQuery(Unity.Entities.ComponentType.ReadOnly<PlayerGhost>(), Unity.Entities.ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()))
        using (var ents = q.ToEntityArray(Unity.Collections.Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (e == player) continue;
                var p = em.GetComponentData<Unity.Transforms.LocalTransform>(e).Position;
                if (!AIVision.CanSee(me, p.xz)) continue;
                var sp = new SeenPlayer { entity = e, pos = p, dist = Unity.Mathematics.math.distance(p.xz, me.xz), isAi = AIPlayerSpikeMod.IsAiPlayer(e) };
                string name = "";
                try { if (em.HasComponent<PlayerCustomizationCD>(e)) name = em.GetComponentData<PlayerCustomizationCD>(e).customization.name.ToString(); } catch { }
                if (string.IsNullOrEmpty(name)) try { name = em.GetComponentData<PlayerGhost>(e).onlineName.ToString(); } catch { }
                sp.name = string.IsNullOrEmpty(name) ? "이름 모름" : name;
                var v = p.xz - me.xz;
                sp.dir = Unity.Mathematics.math.abs(v.x) >= Unity.Mathematics.math.abs(v.y) ? (v.x > 0 ? "동" : "서") : (v.y > 0 ? "북" : "남");
                if (em.HasComponent<HealthCD>(e)) { var h = em.GetComponentData<HealthCD>(e); sp.health = h.health; sp.maxHealth = h.maxHealth; }
                // what can be seen: down, swinging (the swing animation), walking (moved since the last look)
                string doing;
                if (sp.maxHealth > 0 && sp.health <= 0) doing = "쓰러져 있음";
                else
                {
                    bool swing = false;
                    try { swing = AIMotion.Swing(server, e, out _, out _, out _); } catch { }
                    string walk = "";
                    if (s.last.TryGetValue(e, out var l) && now - l.t >= 0.2f && now - l.t < 3f)
                    {
                        var d = p.xz - l.pos.xz;
                        float sp2 = Unity.Mathematics.math.length(d) / (now - l.t);
                        if (sp2 > 1f) walk = $"{(Unity.Mathematics.math.abs(d.x) >= Unity.Mathematics.math.abs(d.y) ? (d.x > 0 ? "동" : "서") : (d.y > 0 ? "북" : "남"))}쪽으로 걷는 중";
                    }
                    doing = swing ? (walk == "" ? "무기·도구를 휘두르는 중" : walk + ", 휘두르는 중") : walk == "" ? "서 있음" : walk;
                }
                if (enemies != null)
                {
                    SeenEnemy ne = null; float nd = float.MaxValue;
                    foreach (var en in enemies) { float dd = Unity.Mathematics.math.distance(en.pos.xz, p.xz); if (dd < nd) { nd = dd; ne = en; } }
                    if (ne != null && nd <= 3f) doing += $", 적 {ne.id}이(가) {nd:F1}칸 옆에 있음";
                }
                sp.doing = doing;
                if (!s.last.TryGetValue(e, out var old) || now - old.t >= 0.3f) s.last[e] = (p, now);
                res.Add(sp);
            }
        }
        res.Sort((a, b) => a.dist.CompareTo(b.dist));
        s.seen = res; s.at = now;
        return res;
    }

    // " 동료 Gemini 바로 옆(1.1칸) — 이 적을 치면 Gemini를 돕는 셈." for the nearest visible teammate within 3 tiles of the enemy.
    public static string NearEnemyNote(Unity.Mathematics.float3 enemyPos)
    {
        SeenPlayer best = null; float bd = float.MaxValue;
        foreach (var p in S.seen) { if (p.maxHealth > 0 && p.health <= 0) continue; float d = Unity.Mathematics.math.distance(p.pos.xz, enemyPos.xz); if (d < bd) { bd = d; best = p; } }
        return best != null && bd <= 3f ? $" {best.role} {best.name}와 {bd:F1}칸 — 이 적을 치면 {best.name}를 돕는 셈." : "";
    }

    // One line for the decision input / upper-LLM report.
    public static string Summary(System.Collections.Generic.List<SeenPlayer> l)
    {
        if (l.Count == 0) return "없음";
        var parts = new System.Collections.Generic.List<string>();
        foreach (var p in l) parts.Add($"{p.name}({p.role}) {p.dir}쪽 {p.dist:F1}칸, 체력 바 {p.health}/{p.maxHealth}, {p.doing}");
        return string.Join("; ", parts);
    }
}

// Follow a player (host or teammate) the AI can see: walk toward them, re-aim the walk as they move, done within 2.5
// tiles. One stable key per player ("follow:<entity>") so choosing it again on the next call continues it.
public class FollowSkill : AISkill
{
    private readonly Unity.Entities.Entity _who;
    private readonly string _name;
    private GotoSkill _child;
    private Unity.Mathematics.int2 _childTarget;
    private float _lastSeen;
    public FollowSkill(Unity.Entities.Entity who, string name) { Name = "따라가기"; _who = who; _name = name; Key = "follow:" + who.Index; }

    private bool Where(Unity.Entities.World server, out Unity.Mathematics.float3 p)
    {
        p = default;
        var em = server.EntityManager;
        if (!em.Exists(_who) || !em.HasComponent<Unity.Transforms.LocalTransform>(_who)) return false;
        p = em.GetComponentData<Unity.Transforms.LocalTransform>(_who).Position;
        return true;
    }

    public override bool Start(Unity.Entities.World server, Unity.Entities.Entity player, float now)
    {
        if (!Where(server, out var p)) { Status = SkillStatus.Failed; Reason = $"{_name}이(가) 없음"; return false; }
        _lastSeen = now;
        Facts.Add($"follow {_name} at ({p.x:F1},{p.z:F1})");
        return true;
    }

    public override BodyInput Tick(Unity.Entities.World server, Unity.Entities.Entity player, float now)
    {
        if (!Where(server, out var p)) return Finish(SkillStatus.Failed, $"{_name}이(가) 없어짐");
        var me = AIBody.Pos(server, player);
        float d = Unity.Mathematics.math.distance(me.xz, p.xz);
        if (AIVision.CanSee(me, p.xz)) _lastSeen = now;
        else if (now - _lastSeen > 3f) return Finish(SkillStatus.Failed, $"{_name}이(가) 화면 밖으로 감");
        if (d <= 2.5f) return Finish(SkillStatus.Done, $"{_name} 곁에 옴({d:F1}칸)");
        if (now - StartTime > 30f) return Finish(SkillStatus.Done, $"30초 따라감, {_name}와 {d:F1}칸");
        var t = AIBody.TileOf(p);
        if (_child == null || _child.Status != SkillStatus.Running || Unity.Mathematics.math.cmax(Unity.Mathematics.math.abs(t - _childTarget)) >= 2)
        {
            if (_child != null) { try { _child.Cleanup(server); } catch { } }
            _child = new GotoSkill(t, _name);
            _child.StartTime = now; _child.StartPos = me; _childTarget = t;
            if (!_child.Start(server, player, now)) { string r = _child.Reason; _child = null; return Finish(SkillStatus.Failed, "걷기 시작 실패: " + r); }
        }
        var bi = _child.Tick(server, player, now);
        if (_child.Status == SkillStatus.Failed) { string r = _child.Reason; try { _child.Cleanup(server); } catch { } _child = null; return Finish(SkillStatus.Failed, "걷기 실패: " + r); }
        return bi;
    }

    public override void Cleanup(Unity.Entities.World server) { if (_child != null) { try { _child.Cleanup(server); } catch { } _child = null; } }

    public override string Progress(Unity.Entities.World server, Unity.Entities.Entity player)
    {
        return Where(server, out var p) ? $"{_name}까지 {Unity.Mathematics.math.distance(AIBody.Pos(server, player).xz, p.xz):F1}칸" : $"{_name} 안 보임";
    }
}
