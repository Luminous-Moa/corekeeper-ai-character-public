// AICandidateAssembler — the final assembly of a candidate build (refactor S6b, moved from AIBrain.BuildCandidates):
// regroup the running action, choose the set (no goal / close fight / all), drop what the body cannot do (Select), mark
// recent failures and reversals (Mark), keep every viable choice, add "wait" and give choice values (Finish). Unity-free (Tools/core-tests
// replays the L1 assembly records of the old code against it, AssemblerReplayTests). The coordinator (AIBrain.BuildCandidates)
// maps its candidates to AsmCand by object identity, evaluates the two body predicates for it, and keeps what lies
// between the stages (block notes, gather facts, the tool-short rebuild).
// (If Scripts/Core is ever flattened, this file becomes Scripts/AICoreCandidateAssembler.cs: a rename only.)
using System;
using System.Collections.Generic;
using System.Text;


// A candidate as the assembler sees it: the fields of AICandidate it reads or writes. One object per AICandidate.
public enum EdgeCandidateRole { None, DoorPlace, DoorRepair, TorchPlace }

public sealed class AsmCand
{
    public int id;
    public string kind, key, desc, value;
    public EdgeCandidateRole edgeRole;
    public bool progress, comesByItself;
    public bool edge;   // review F4 (2026-11-06): a base-edge option (fill, repair, door, materials, torch, pick-up; `:out` too) — set by Select from lEdge
    public bool detour; // 제작대-놓기 우회 사실: the "mine the edge" way stays last, beside the door way (Select).
    public bool keep;   // Historical priority metadata for continue/combat/door-close options; no count cut uses it.
}

// What Select reads besides the candidate lists (the coordinator fills it from the build; the replay from ASM-IN).
public sealed class AsmInput
{
    public GoalKind goal;
    public bool free, closeFight, closeFightOff;
    public bool combat;             // an enemy within combat range (combat stage 2: no cut to Limit, see LimitFor)
    public bool curCombat;          // the running skill is an attack, keep-away, retreat or eat (self-defence keeps "continue")
    public string curKey = "";
    public int near6;               // enemies within 6 tiles
    public string enemy0 = "", enemy0Dist = "";   // the nearest enemy and its distance as the build wrote it ({dist:F1})
    public List<AsmCand> lCont, lFollow, lEnemy, lCombat, lCraft, lLife, lMine, lDig, lRemember, lItem, lExplore, lDigExplore, lOther;
    public List<AsmCand> lBrute = new List<AsmCand>();   // combat stage 2: close-fight options against a charging enemy (fixed order, right after the attacks)
    // review F1 (2026-11-06): closing an own door that stands open (the last step of open -> through -> close): in every
    // set — no goal (self-defence), close fight, all — right after the combat options
    public List<AsmCand> lDoor = new List<AsmCand>();
    // review F4: the base-edge options (no goal progress). Out of a close fight they come after the goal's own options
    // without displacing any other option. edgeGoal: a goal whose work is the edge (none yet, 단계 4): then
    // they keep the craft place (before the goal lists).
    public List<AsmCand> lEdge = new List<AsmCand>();
    public bool edgeGoal;
    // recheck N4: a call for the "기지 둘레 부서짐" trigger: the repairs and door re-hangs go first among the edge options
    public bool breachCall;
    public HashSet<AsmCand> foodPicks;
}

public static class AICandidateAssembler
{
    public const int Limit = 0; // compatibility: 0 means no candidate count cut

    // Regroup, choose the set, drop what the body cannot do. The lists in a are changed as the build changed them.
    // Doable / BodyCan are the build's predicates (called exactly where and as often as the build called them).
    public static List<AsmCand> Select(AsmInput a, Func<AsmCand, bool> Doable, Func<AsmCand, bool> BodyCan, out string setLog, out bool closeSet, out int noBody)
    {
        var runningChoice = a.lCont.Find(c => c.kind == "continue");
        var lCont = a.lCont; var lFollow = a.lFollow; var lEnemy = a.lEnemy; var lCombat = a.lCombat; var lCraft = a.lCraft; var lLife = a.lLife;
        var lMine = a.lMine; var lDig = a.lDig; var lRemember = a.lRemember; var lItem = a.lItem; var lExplore = a.lExplore; var lDigExplore = a.lDigExplore; var lOther = a.lOther;
        var foodPicks = a.foodPicks; bool free = a.free; string curKey = a.curKey;
        var lDoor = a.lDoor ?? new List<AsmCand>(); var lEdge = a.lEdge ?? new List<AsmCand>();
        foreach (var c in lEdge) c.edge = !a.edgeGoal;
        // recheck N2: what the edge share never takes the place of
        foreach (var l in new[] { lCont, lEnemy, a.lBrute, lCombat, lLife, lDoor }) if (l != null) foreach (var c in l) c.keep = true;
        // recheck N4: a breach call puts the repairs and door re-hangs first (in the order and in the share)
        if (a.breachCall && lEdge.Count > 1)
        {
            var rep = lEdge.FindAll(IsRepair); var rest = lEdge.FindAll(c => !IsRepair(c));
            lEdge.Clear(); lEdge.AddRange(rep); lEdge.AddRange(rest);
        }
        string _setLog; bool _closeSet;
        // Stable list (2026-11-04, user: few chances to waver between similar options): the running action keeps its own
        // place and its own choice value ("keep doing X" instead of a separate first entry), so the same action has the
        // same value and position on every ~0.3 s call whether it is running or not. (With positional values c1..cN the
        // model answered "c4" call after call while c4 alternated between attack and retreat, ai2 run stab1.)
        if (lCont.Count == 1)
        {
            var cc = lCont[0];
            if (curKey.StartsWith("attack:")) { lEnemy.Add(cc); lEnemy.Sort((a1, b) => EntityOrder(a1).CompareTo(EntityOrder(b))); lCont.Clear(); }
            else if (curKey.StartsWith("away:") || curKey.StartsWith("retreat:") || curKey.StartsWith("eat:"))
            {
                lCombat.Add(cc);
                lCombat.Sort((a1, b) => CombatOrder(a1).CompareTo(CombatOrder(b)));
                lCont.Clear();
            }
            else if (curKey.StartsWith("follow:")) { lFollow.Insert(0, cc); lCont.Clear(); }
        }
        var list = new List<AsmCand>();
        _closeSet = false; _setLog = "";
        if (a.goal == GoalKind.None)
        {
            // self-defence: only combat options (and wait); "continue" only for a running combat action
            if (a.curCombat) list.AddRange(lCont);
            list.AddRange(lEnemy); list.AddRange(a.lBrute); list.AddRange(lCombat); list.AddRange(lDoor);

        }
        else if (a.closeFight && a.closeFightOff) { _setLog = " set=all(close-fight off)"; list.AddRange(lCont); list.AddRange(lFollow); list.AddRange(lEnemy); list.AddRange(a.lBrute); list.AddRange(lCombat); list.AddRange(lDoor); list.AddRange(lCraft); list.AddRange(lEdge); }
        else if (a.closeFight)
        {
            // Close fight still puts combat first. Every executable goal-progress alternative follows, so the
            // AI can choose whether and where to resume the goal; no one-goal candidate quota remains.
            var goalSide = new List<AsmCand>();
            if (a.goal == GoalKind.Follow) goalSide.AddRange(lFollow);
            goalSide.AddRange(lCont); goalSide.AddRange(lCraft); goalSide.AddRange(lMine); goalSide.AddRange(lDig); goalSide.AddRange(lRemember);
            // U.breakoff-pickup (2026-11-04): a pickup the game already does by itself (item flying in / inside the
            // auto-pickup radius) is not "continuing the goal" — offered as the one goal-side option it told the model
            // that leaving the fight for an item that arrives anyway was goal work (combat polish 2, ab3 #1579 #2196).
            // Such pickups are left out of the goal side; walking to a farther item stays (that is real goal work).
            goalSide.AddRange(lItem.FindAll(c => !c.comesByItself)); goalSide.AddRange(lExplore); goalSide.AddRange(lDigExplore);
            if (a.goal != GoalKind.Follow) goalSide.AddRange(lFollow);
            goalSide.AddRange(lOther);
            // U.breakoff-nongoal-pickup (2026-11-04 막힘-멈춤-정리): "break off and continue the goal" is only a real goal
            // step — a candidate marked progress (goal item, goal ore, explore target of an explore goal, the host for a
            // follow goal, a craft/store step of the goal) that the body can do and that is not about to be left out. A
            // walk to a non-goal item 11 tiles away (HeartBerrySeed under an explore goal) was this option 163 + 146 times
            // in the four-player runs; filler mining and a dig toward an enemy are not goal work either. No such step:
            // no break-off option (the combat options stay).
            goalSide.RemoveAll(c => !Doable(c));
            // Keep the running goal action first, then every other qualified goal-side action. The old one-option
            // quota hid valid targets even after the final count cut was removed.
            var continuingGoal = goalSide.Find(c => c.kind == "continue");
            if (continuingGoal != null) { goalSide.Remove(continuingGoal); goalSide.Insert(0, continuingGoal); }
            list.AddRange(lEnemy); list.AddRange(a.lBrute); list.AddRange(lCombat); list.AddRange(lDoor);
            if (a.goal != GoalKind.Follow && lFollow.Exists(c => c.kind == "continue")) list.AddRange(lFollow.FindAll(c => c.kind == "continue"));   // a running teammate follow stays as "continue"
            foreach (var keep in goalSide)
            {
                if (list.Contains(keep)) continue;
                keep.desc = free
                    ? $"싸움을 끊고 자유 시간 일 하기 — {keep.desc} 이것을 고르면 싸우지도 피하지도 않고 이 일을 함: 가는 동안 공격하지 않고, 6칸 안 적 {a.near6}마리(가장 가까운 {a.enemy0} {a.enemy0Dist}칸)는 따라오거나 칠 수 있음."
                    : $"싸움을 끊고 목표 계속하기 — {keep.desc} 이것을 고르면 싸우지도 피하지도 않고 목표 일을 함: 가는 동안 공격하지 않고, 6칸 안 적 {a.near6}마리(가장 가까운 {a.enemy0} {a.enemy0Dist}칸)는 따라오거나 칠 수 있음.";
                list.Add(keep);
            }
            if (goalSide.Count == 0) _setLog = " set=close-fight(no goal step to break off for)";
            _closeSet = true; if (_setLog == "") _setLog = " set=close-fight";
        }
        else { list.AddRange(lCont); list.AddRange(lFollow); list.AddRange(lEnemy); list.AddRange(a.lBrute); list.AddRange(lCombat); list.AddRange(lDoor); list.AddRange(lCraft); if (a.edgeGoal) list.AddRange(lEdge); list.AddRange(lLife); }
        if (!_closeSet)
        {
            list.AddRange(lMine); list.AddRange(lDig);
            // free time: no pickups except hunger food while the bag has none (먹거리 자급 1; breaking a ripe plant for
            // hunger comes in lMine the same way)
            list.AddRange(lRemember); list.AddRange(lItem); list.AddRange(lExplore); list.AddRange(lDigExplore); list.AddRange(lOther);
            if (!a.edgeGoal) list.AddRange(lEdge);   // edge options follow the goal's own choices without displacing them
        }
        // Selection policy does not hide other executable actions in None or combat states.
        foreach (var group in new[] { a.lCont, a.lFollow, a.lMine, a.lDig, a.lRemember, a.lItem, a.lExplore, a.lDigExplore, a.lOther, a.lCraft, a.lLife, a.lEdge })
            if (group != null) foreach (var c in group) if (!list.Contains(c)) list.Add(c);
        if (free) _setLog += " set=free-time";
        // 제작대-놓기 우회 사실 (코어-안전지대 조건): "mine the edge" ways go last, as edge options. Count limits no
        // longer remove them. Set eligibility still applies (the door way's option keeps its fact line).
        var det = list.FindAll(c => c.detour);
        if (det.Count > 0) { list.RemoveAll(c => c.detour); foreach (var c in det) c.edge = true; list.AddRange(det); }
        // what the body cannot do (no pick / weapon and no hand slot that can be made empty): not offered, said once
        noBody = list.RemoveAll(c => !BodyCan(c));
        if (runningChoice != null && !list.Contains(runningChoice) && BodyCan(runningChoice))
        {
            runningChoice.desc += $" 주변 적 {a.near6}마리. 현재 동작을 계속할지 직접 선택.";
            list.Add(runningChoice);
        }
        setLog = _setLog; closeSet = _closeSet;
        return list;
    }

    // recent failures: annotate once, leave out after 2 within the memory window; then an action this character left
    // less than 3 s ago. Returns the left-out keys with their reasons.
    public static List<string> Mark(List<AsmCand> list, float tnow, IDictionary<string, (int count, float last, string reason)> _fails, float FailMemorySeconds, IList<(string key, float t)> left)
    {
        var skipped = new List<string>();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var c = list[i];
            if (c.kind == "continue" || !_fails.TryGetValue(c.key, out var f) || tnow - f.last > FailMemorySeconds) continue;
            c.desc += $" 최근 이 대상에서 실패 {f.count}회: {f.reason}. 현재 상태에서 다시 시도할지는 직접 선택.";
        }
        // an action this character left less than 3 s ago (a reversal would start it again from the beginning)
        foreach (var c in list)
        {
            if (c.kind == "continue") continue;
            var lf = FindLast(left, c.key);
            if (lf.key != null && tnow - lf.t <= 3f) c.desc += $" 이 행동은 {tnow - lf.t:F1}초 전에 하다가 그만둔 것(다시 고르면 처음부터).";
        }
        return skipped;
    }

    // S._left.FindLast(x => x.key == key), default (null key) when none
    private static (string key, float t) FindLast(IList<(string key, float t)> left, string key)
    {
        for (int i = left.Count - 1; i >= 0; i--) if (left[i].key == key) return left[i];
        return default;
    }

    // Keep all viable candidates, add "wait" (the coordinator's object), give each a choice value from its target key (the same
    // action keeps its value between calls).
    public static void Finish(List<AsmCand> list, AsmCand wait) => Finish(list, wait, Limit);

    // Candidate count limits are removed by the 2026-11-07 user decision. Old settings remain readable but do not cut.
    public static int LimitFor(bool combat, int combatMax) => 0;

    // Historical field retained for source compatibility. No separate edge quota or reallocation remains.
    public const int EdgeShare = 0; // compatibility only: all edge options are retained

    // The legacy limit parameter is ignored: every viable choice and wait are retained.
    public static void Finish(List<AsmCand> list, AsmCand wait, int limit) => Finish(list, wait, limit, out _);

    // edgeCut remains empty for caller compatibility; no candidate is omitted by a numeric limit.
    public static void Finish(List<AsmCand> list, AsmCand wait, int limit, out List<AsmCand> edgeCut)
    {
        edgeCut = new List<AsmCand>();
        var runningChoice = list.Find(c => c.kind == "continue");
        if (runningChoice != null && !list.Contains(runningChoice)) list.Add(runningChoice);
        list.Add(wait);
        // choice values from the target key (the same action keeps its value between calls)
        var used = new HashSet<string>();
        foreach (var c in list)
        {
            string v = KeyLabel(c.key);
            string u = v; int n = 2;
            while (!used.Add(u)) u = v + "_" + n++;
            c.value = u;
        }
    }

    // recheck N4: a repair of the edge or a door hung again (the breach trigger's own options)
    public static bool IsRepair(AsmCand c) => c.kind == "edgerepair" || c.kind == "place" && c.edgeRole == EdgeCandidateRole.DoorRepair;

    // recheck N4: what an edge option is, in a word
    public static string EdgeKindWord(AsmCand c)
    {
        switch (c.kind)
        {
            case "edgefill": return "메우기";
            case "edgerepair": return "보수";
            case "edgemat": return "보수 재료";
            case "edgepickup": return "회수";
            case "place": return c.edgeRole == EdgeCandidateRole.DoorRepair ? "문 다시 달기" : c.edgeRole == EdgeCandidateRole.DoorPlace ? "문 달기" : c.edgeRole == EdgeCandidateRole.TorchPlace ? "횃불" : "놓기";
            default: return c.kind;
        }
    }

    // recheck N4: one line for the edge options the cut left out ("" when none): "둘레 후보 N개 더 있음(보수 2, 메우기 1)"
    public static string EdgeCutNote(List<AsmCand> cut)
    {
        if (cut == null || cut.Count == 0) return "";
        var order = new List<string>(); var n = new Dictionary<string, int>();
        foreach (var c in cut) { string w = EdgeKindWord(c); if (!n.ContainsKey(w)) { n[w] = 0; order.Add(w); } n[w]++; }
        return $"둘레 후보 {cut.Count}개 더 있음({string.Join(", ", order.ConvertAll(w => $"{w} {n[w]}"))}) — 후보 수 상한으로 이번 목록에서 빠짐";
    }

    public static string KeyLabel(string key)
    {
        if (string.IsNullOrEmpty(key)) return "x";
        var sb = new StringBuilder();
        foreach (char ch in key) sb.Append(char.IsLetterOrDigit(ch) || ch == '-' ? ch : '_');
        return sb.ToString();
    }

    public static int EntityOrder(AsmCand c) => c.key.StartsWith("attack:") && int.TryParse(c.key.Substring(7), out int i) ? i : int.MaxValue;
    public static int CombatOrder(AsmCand c)
    {
        string k = c.key;
        return k.StartsWith("away:") ? 0 : k.StartsWith("retreat:") ? 1 : k.StartsWith("eat:") ? 2 : 3;
    }
}
