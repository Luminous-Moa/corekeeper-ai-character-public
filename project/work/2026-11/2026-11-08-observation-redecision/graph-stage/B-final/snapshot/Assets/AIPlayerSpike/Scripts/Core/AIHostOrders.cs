// AIHostOrders — the host's instructions one member holds (자연어 명령 재검수 R1·R2·R3; work/2026-11/2026-11-05-자연어-명령/
// 9-재검수.md, 10-재검수-반영.md). Unity-free so Tools/core-tests can check the state changes; AIPlanner logs them.
//
// - The latest instruction (any state) and every earlier one that is still open (받음 / 따름 / 미룸 / 막힘) are kept, MaxOpen open
//   at most. A new mention no longer overwrites a put-off instruction (R3); past MaxOpen the oldest open one is pushed out
//   (밀려남). Closed earlier ones (끝남 / 포기 / 답함 / 멈춤 / 밀려남) drop out of the list; the latest stays for context.
// - States change only by the answer's fields (R2): "order" for the latest instruction, "orders" {"<id>": value} for an
//   earlier open one. No order: no change (the planner logs it). Never by the sentence.
// - A followed instruction is carried by a goal source ("host" for a /ai goal, an LLM goal or plan source): when that goal
//   or plan ends it is 끝남 (done) or 막힘 (실게임4 K2: the goal or a plan step got stuck; still open — the upper LLM
//   follows it again another way, puts it off or drops it).
// - 멈춤 is the host's own stop only: /ai stop closes every open instruction as 멈춤 (R1, AIPlanStop), and a new /ai goal
//   closes the host goal it replaces. A 멈춤 instruction is closed for good.
using System;
using System.Collections.Generic;
using System.Text;

public sealed class HostOrder
{
    public int Id;          // 1, 2, … per member (AIPlanner.State.HostOrderSeq)
    public string Text;     // the chat line or "/ai goal …"
    public float At;        // when it was given (realtime seconds)
    public string State;    // 받음 / 따름 / 미룸 / 막힘 (open) · 포기 / 끝남 / 답함 / 멈춤 / 밀려남 (closed)
    public string Note;     // why it is in that state
    public string Source;   // the goal source carrying it while followed, or null
    public bool Open => AIHostOrders.IsOpen(State);
}

// A state change that happened (the order already has its new state): was, why.
public delegate void OrderChanged(HostOrder o, string was, string why);

public sealed class AIHostOrders
{
    public const int MaxOpen = int.MaxValue; // compatibility; open orders are never evicted by count
    private readonly List<HostOrder> _list = new List<HostOrder>();   // oldest first; the last one is the latest

    public static bool IsOpen(string st) => st == "받음" || st == "따름" || st == "미룸" || st == "막힘";

    public HostOrder Latest => _list.Count > 0 ? _list[_list.Count - 1] : null;
    public int Count => _list.Count;
    public HostOrder Find(int id) { foreach (var o in _list) if (o.Id == id) return o; return null; }

    // The open instructions other than the latest, newest first.
    public List<HostOrder> EarlierOpen()
    {
        var r = new List<HostOrder>();
        for (int i = _list.Count - 2; i >= 0; i--) if (_list[i].Open) r.Add(_list[i]);
        return r;
    }

    public List<HostOrder> OpenAll()
    {
        var r = new List<HostOrder>();
        foreach (var o in _list) if (o.Open) r.Add(o);
        return r;
    }

    // One state change; the carrying source goes when the order is no longer followed. Returns whether it changed.
    public static bool Change(HostOrder o, string st, string note, string why, OrderChanged cb)
    {
        if (o == null || st == null || st == o.State) return false;
        string was = o.State;
        o.State = st; o.Note = note;
        if (st != "따름") o.Source = null;
        cb?.Invoke(o, was, why);
        return true;
    }

    // A new instruction becomes the latest. Earlier open ones stay; past MaxOpen open the oldest open is pushed out.
    public void Add(HostOrder o, OrderChanged cb)
    {
        _list.Add(o);
        Prune();
    }

    // Closed earlier instructions drop out; the latest stays whatever its state.
    public void Prune()
    {
        for (int i = _list.Count - 2; i >= 0; i--) if (!_list[i].Open) _list.RemoveAt(i);
    }

    public void Clear() => _list.Clear();

    // What an answer's fields do (R2, R3). order: for the latest instruction, if its report carried it (reqSeq, F1).
    // earlier: id -> value for earlier open ones the report carried (id <= reqSeq). talkOnly (the goal was not changed):
    // a follow is not taken. Returns the instructions this answer follows (the set branch makes its source carry them).
    public List<HostOrder> ApplyAnswer(string order, IDictionary<int, string> earlier, string trigger, int reqSeq, bool talkOnly,
        string note, string why, OrderChanged cb, Action<string> log)
    {
        var followed = new List<HostOrder>();
        var latest = Latest;
        if (latest != null)
        {
            string st = AIPlanRules.OrderState(order);
            if (!AIPlanRules.OrderApplies(reqSeq, latest.Id))
            {
                if (st != null) log?.Invoke($"'{order}' not applied (it saw instruction #{reqSeq}, now #{latest.Id})");
            }
            else
            {
                if (st == null) { if (trigger == "host_said") log?.Invoke($"no order in the answer to host_said: instruction #{latest.Id} stays {latest.State}"); }
                else if (talkOnly && st == "따름") log?.Invoke($"follow of #{latest.Id} not taken (talk-only: the goal was not changed)");
                else Change(latest, st, note, why, cb);
                if (latest.State == "따름") followed.Add(latest);
            }
        }
        if (earlier != null)
            foreach (var kv in earlier)
            {
                var o = Find(kv.Key);
                string st = AIPlanRules.OrderState(kv.Value);
                if (o == null || o == latest || !o.Open || !AIPlanRules.EarlierOrderApplies(kv.Key, reqSeq) || st == null)
                { log?.Invoke($"orders #{kv.Key} '{kv.Value}' not applied (not an earlier open instruction of that report)"); continue; }
                if (talkOnly && st == "따름") { log?.Invoke($"follow of #{o.Id} not taken (talk-only)"); continue; }
                Change(o, st, note, why, cb);
                if (o.State == "따름") followed.Add(o);
            }
        Prune();
        return followed;
    }

    // A goal ended: the instructions it carried are done (끝남) or stuck (막힘, still open: 실게임4 K2 — not 멈춤, which is
    // the host's own stop).
    public void GoalEnded(string goalSource, bool done, string note, OrderChanged cb)
    {
        if (string.IsNullOrEmpty(goalSource)) return;
        foreach (var o in OpenAll())
            if (!done && o.State == "따름" && o.Source == goalSource) Change(o, "막힘", note, "goal stuck", cb);
        Prune();
    }

    // The followed instructions a goal source carries now.
    public List<HostOrder> Carried(string source)
    {
        var r = new List<HostOrder>();
        if (string.IsNullOrEmpty(source)) return r;
        foreach (var o in _list) if (o.State == "따름" && o.Source == source) r.Add(o);
        return r;
    }

    // The plan carrying followed instructions was put down without ending (a hold, a host /ai goal over it): they are put
    // off (미룸), so the next report shows them open to come back to. except: one the answer keeps followed.
    public void PutOff(string source, HostOrder except, string note, string why, OrderChanged cb)
    {
        foreach (var o in Carried(source)) if (o != except) Change(o, "미룸", note, why, cb);
        Prune();
    }

    // Every open instruction closes as state (the host's /ai stop: 멈춤). Returns how many closed.
    public int CloseAll(string st, string note, string why, OrderChanged cb)
    {
        int n = 0;
        foreach (var o in OpenAll()) if (Change(o, st, note, why, cb)) n++;
        Prune();
        return n;
    }

    // The report's "host_order" (the latest, or null) and "host_orders_open" (earlier open ones, newest first).
    public string ReportFields(float now, Func<string, string> esc)
    {
        var sb = new StringBuilder();
        sb.Append("\"host_order\":").Append(Latest == null ? "null" : Json(Latest, now, esc)).Append(',');
        sb.Append("\"host_orders_open\":[");
        var e = EarlierOpen();
        for (int i = 0; i < e.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Json(e[i], now, esc)); }
        sb.Append(']');
        return sb.ToString();
    }

    private static string Json(HostOrder o, float now, Func<string, string> esc)
        => $"{{\"id\":{o.Id},\"text\":\"{esc(o.Text)}\",\"ago_s\":{(int)(now - o.At)},\"state\":\"{esc(o.State)}\"{(string.IsNullOrEmpty(o.Note) ? "" : $",\"note\":\"{esc(o.Note)}\"")}}}";
}

// /ai stop (재검수 R1): what goes and what closes, in one place. The plan and its stopped record go, the owed plan call
// goes (it must not come back with the next answer), the hold watch goes, and every open host instruction closes as 멈춤.
// Returns the event line for the next report (null when nothing was open or running).
public static class AIPlanStop
{
    public static string Apply(AIPlanQueue plan, AIHostOrders orders, string why, OrderChanged cb,
        ref string owedTrigger, ref string endedSource, ref bool planNext, ref bool heldWatch)
    {
        bool hadPlan = plan.Active || owedTrigger != null;
        plan.Clear(); plan.ForgetStopped();
        owedTrigger = null; endedSource = null; planNext = false; heldWatch = false;
        int closed = orders.CloseAll("멈춤", $"호스트가 {why}로 멈춤", why, cb);
        if (closed == 0 && !hadPlan) return null;
        return $"호스트가 멈춤({why}{(closed > 0 ? $", 열린 지시 {closed}개 닫음" : "")}{(hadPlan ? ", 계획 버림" : "")})";
    }
}
