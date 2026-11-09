// 자연어 명령 (2026-11-05 합의 0-합의.md): mentions, completion, the chat log the report carries, the inbox priority,
// the periodic rule and what the chat window shows.
using System.Collections.Generic;

public static class NlChatTests
{
    public static void Run()
    {
        var M = AIMention.Members(new[] { ("claude", "Claude"), ("gpt", "ChatGPT"), ("gemini", "Gemini"), ("grok", "Grok") });

        // ---- Parse: who a line goes to
        Program.Check(AIMention.Parse("오늘 뭐 먹지", M, out bool all0).Count == 0 && !all0, "no mention: ordinary chat, nobody");
        var p1 = AIMention.Parse("@Claude 구리 3개 캐 와", M, out bool all1);
        Program.Check(p1.Count == 1 && p1[0] == "claude" && !all1, "@Claude -> claude");
        var p2 = AIMention.Parse("@클로드야 이리 와 @그록, 너도", M, out _);
        Program.Check(p2.Count == 2 && p2[0] == "claude" && p2[1] == "grok", "Korean names with a particle or comma after them; table order");
        var p3 = AIMention.Parse("@모두 기지로 모여", M, out bool all3);
        Program.Check(all3 && p3.Count == 4, "@모두 -> every member");
        Program.Check(AIMention.Parse("메일 a@claude.com 확인", M, out _).Count == 0, "an e-mail address is not a mention");
        Program.Check(AIMention.Parse("@chatgpt 하이 @CHATGPT", M, out _).Count == 1, "case-insensitive, no repeats");
        Program.Check(AIMention.Parse("@누구 안녕", M, out _).Count == 0, "an unknown name is not a mention");
        var p4 = AIMention.Parse("@지피티 @제미나이", M, out _);
        Program.Check(p4.Count == 2 && p4[0] == "gpt" && p4[1] == "gemini", "other Korean names (지피티, 제미나이)");

        // ---- Completion: one letter is enough
        Program.Check(AIMention.Candidates("@", M).Count == 0, "'@' alone: no candidates yet");
        var c1 = AIMention.Candidates("@c", M);
        Program.Check(c1.Count == 2 && c1[0].Id == "claude" && c1[1].Id == "gpt", "@c -> Claude, ChatGPT (codex/chatgpt)");
        Program.Check(AIMention.Complete("@c", M) == "@Claude ", "Tab fills the first candidate and a space");
        Program.Check(AIMention.Complete("안녕 @g", M) == "안녕 @ChatGPT ", "completion keeps the text before the mention");
        var c2 = AIMention.Candidates("@모", M);
        Program.Check(c2.Count == 1 && c2[0].Id == "all" && AIMention.Complete("@모", M) == "@모두 ", "@모 -> 모두");
        Program.Check(AIMention.Candidates("@클", M).Count == 1 && AIMention.Complete("@클", M) == "@Claude ", "@클 (committed Hangul) -> Claude");
        Program.Check(AIMention.Candidates("@ㅋ", M).Count == 1 && AIMention.Candidates("@ㅋ", M)[0].Id == "claude", "@ㅋ (IME composing, lone consonant) -> Claude");
        Program.Check(AIMention.Candidates("@크", M).Count == 1, "@크 (IME composing, 클 without its final consonant) -> Claude");
        Program.Check(AIMention.Candidates("@ㄱ", M).Count == 1 && AIMention.Candidates("@ㄱ", M)[0].Id == "grok", "@ㄱ -> 그록");
        Program.Check(AIMention.Candidates("@Claude 구리", M).Count == 0, "after a space the mention is finished: no hint");
        Program.Check(AIMention.Candidates("a@c", M).Count == 0, "@ inside a word: no hint");
        Program.Check(AIMention.Hint("@g", M) == "@ChatGPT  @Gemini  @Grok   (Tab)", "hint line lists the candidates");
        Program.Check(AIMention.Hint("@x", M) == null && AIMention.Complete("@x", M) == null, "no candidate: no hint, Tab does nothing");

        // ---- Chat log in the report
        var log = new AIChatLog();
        log.Add(100f, "host", "호스트", new List<string> { "claude" }, false, "@Claude 구리 캐 와");
        log.Add(110f, "claude", "Claude", null, false, "알겠어, 구리 찾으러 갈게");
        int seen = log.LastSeq;
        log.Add(150f, "gpt", "ChatGPT", null, false, "나는 나무 모을게");
        log.Add(160f, "host", "호스트", new List<string> { "claude", "gpt", "gemini", "grok" }, true, "@모두 \"기지\"로 와");
        string j = log.ReportJson(170f, "claude", seen, s => s.Replace("\"", "\\\""), id => id == "gpt" ? "ChatGPT" : id);
        Program.Check(j.StartsWith("[{\"from\":\"호스트\",\"to\":\"claude\",\"to_me\":true,\"ago_s\":70,\"text\":"), "host line to me: to, to_me, ago, no 'new' (seen)");
        Program.Check(j.Contains("{\"from\":\"Claude(나)\",\"ago_s\":60,"), "my own say is marked (나) and never new");
        Program.Check(j.Contains("{\"from\":\"ChatGPT\",\"ago_s\":20,\"new\":true,\"text\":\"나는 나무 모을게\"}"), "a teammate's say after my last report is new, no to_me");
        Program.Check(j.Contains("\"to\":\"모두\",\"to_me\":true,\"ago_s\":10,\"new\":true,\"text\":\"@모두 \\\"기지\\\"로 와\""), "@모두 line: to 모두, to_me, new, escaped");
        for (int i = 0; i < 50; i++) log.Add(200f + i, "host", "호스트", new List<string> { "grok" }, false, "x" + i);
        Program.Check(log.Lines.Count == 54 && log.Lines[0].Text == "@Claude 구리 캐 와", "chat retains all lines beyond the old 40-line ceiling");
        string j2 = log.ReportJson(300f, "claude", 0, s => s, id => id);
        Program.Check(j2.Split(new[] { "{\"from\"" }, System.StringSplitOptions.None).Length - 1 == 54 && j2.Contains("x49") && j2.Contains("\"x0\""), "chat report includes the full retained talk, including older pending lines");
        Program.Check(j2.Contains("to_me"), "earlier host mentions remain in the complete report");

        // ---- Inbox priority (host speaking is not replaced by a periodic look or a goal event)
        var b = new PlanInbox();
        b.Put(new PlanTrigger("host_said")); b.Put(new PlanTrigger("periodic"));
        Program.Check(b.Pending.Kind == "host_said", "periodic does not replace host_said");
        b.Put(new PlanTrigger("goal_done"));
        Program.Check(b.Pending.Kind == "host_said", "a goal event does not replace host_said");
        b.Clear(); b.Put(new PlanTrigger("periodic")); b.Put(new PlanTrigger("goal_done"));
        Program.Check(b.Pending.Kind == "goal_done", "a goal event replaces periodic");
        b.Put(new PlanTrigger("host_goal"));
        Program.Check(b.Pending.Kind == "host_goal", "the host's /ai goal replaces a goal event");
        b.Put(new PlanTrigger("host_said"));
        Program.Check(b.Pending.Kind == "host_said", "same priority: the latest wins");

        // ---- Periodic rule (2 minutes, upperPeriodSeconds)
        Program.Check(PlanInbox.PeriodicDue(160f, 100f, 60f, false, false), "60 s after the last report: due");
        Program.Check(!PlanInbox.PeriodicDue(159f, 100f, 60f, false, false), "59 s: not yet");
        Program.Check(!PlanInbox.PeriodicDue(500f, 100f, 60f, true, false), "a request on the way or waiting: not due");
        Program.Check(!PlanInbox.PeriodicDue(500f, 100f, 60f, false, true), "upper LLM unavailable: not due (the retry time rules)");
        Program.Check(!PlanInbox.PeriodicDue(500f, 100f, 0f, false, false), "period 0: off");

        Program.Check(PlanInbox.PeriodicDue(220f, 100f, 120f, false, false) && !PlanInbox.PeriodicDue(219f, 100f, 120f, false, false), "the default 120 s period");

        // ---- Fight end: when it calls the upper LLM, and the report lines
        var fl = new AIFightLog();
        fl.Count(new[] { "MushroomBrute" }, true); fl.Count(new[] { "MushroomBrute", "Caveling" }, false);
        var f1 = new FightRecord { At = 100f, Secs = 21.4f, Downs = 0, Damage = 58, EndHp = 42, MaxHp = 100 };
        f1.Enemies["MushroomBrute"] = 1; f1.Fell["MushroomBrute"] = 1;
        Program.Check(AIFightLog.ShouldReport(f1), "ended below half (42/100): reported");
        var f2 = new FightRecord { At = 110f, Secs = 4f, Downs = 0, Damage = 10, EndHp = 50, MaxHp = 100 };
        f2.Enemies["Caveling"] = 2;
        Program.Check(!AIFightLog.ShouldReport(f2), "ended at half (50/100), no down: not reported");
        var f3 = new FightRecord { At = 120f, Secs = 9f, Downs = 1, Damage = 120, EndHp = 100, MaxHp = 100 };
        f3.Enemies["MushroomBrute"] = 1;
        Program.Check(AIFightLog.ShouldReport(f3), "a down in the fight: reported even at full health after standing up");
        fl.Add(f1); fl.Add(f2); fl.Add(f3);
        Program.Check(fl.Tally("MushroomBrute") == (4, 2) && fl.Tally("Caveling") == (2, 0), "tally per kind: saved fights + new ones, downs counted per fight");
        string fj = fl.ReportJson(130f, x => x);
        Program.Check(fj.StartsWith("[\"30초 전 끝, 21초: MushroomBrute. 쓰러진 적 MushroomBrute 1(누가 쳤는지 모름). 나 쓰러짐 0번, 받은 피해 58, 끝 체력 42/100. 이 월드 전적: MushroomBrute 싸움 4번 중 쓰러짐 2번\""), "a fight line: foes, fell, downs, damage, end health, record");
        Program.Check(fj.Contains("Caveling 2. 쓰러진 적 없음."), "several of one kind, none fell");
        Program.Check(!fj.Contains("좋") && !fj.Contains("추천") && !fj.Contains("하세요"), "facts only, no advice words");
        Program.Check(fl.ReportJson(1000f, x => x).Contains("MushroomBrute"), "older fights remain available beyond ten minutes");
        for (int i = 0; i < 8; i++) fl.Add(new FightRecord { At = 200f + i, MaxHp = 100, EndHp = 100 });
        Program.Check(fl.Recent.Count == 11, "all fights remain available beyond the former five-record limit");

        // ---- Chat window
        Program.Check(ChatPolicy.Show(ChatKind.Say, false) && ChatPolicy.Show(ChatKind.Error, false) && ChatPolicy.Show(ChatKind.Command, false), "say, errors, command answers: shown");
        Program.Check(!ChatPolicy.Show(ChatKind.Status, false) && ChatPolicy.Show(ChatKind.Status, true), "status lines: only with verbose");

        Rules(M);
        Plans();
        Live4();
    }

    // 실게임4 K1·K2 (11-실게임4.md §4·§5): a set answer with a step that cannot be taken is cut there, not thrown away
    // (the say and order still apply); a stuck goal or plan step leaves the host's instruction open as 막힘, apart from 멈춤.
    private static void Live4()
    {
        // K1 — the cut: the first step that cannot be taken
        Program.Check(AIPlanRules.PlanCut(new List<string> { null, null, null }) == -1, "K1: every step can be taken: no cut");
        Program.Check(AIPlanRules.PlanCut(new List<string> { "아는 제작법이 아님 'CopperHelm'", null }) == 0, "K1: step 1 cannot be taken: cut 0 (the goal stays)");
        Program.Check(AIPlanRules.PlanCut(new List<string> { null, null, "모르는 품목 'WoodPants'", null }) == 2, "K1: step 3 cannot: steps 1-2 are set");
        Program.Check(AIPlanRules.PlanCut(null) == -1, "K1: no list: no cut");
        // the note for the next report's last_answer_rejected (the 4판 Grok case: '구리 방어구' → CopperHelm first)
        var gs = new List<PlanStep> { new PlanStep("craft", "CopperHelm", 1), new PlanStep("craft", "CopperBreastArmor", 1) };
        string n0 = AIPlanRules.RefusedNote(gs, 0, "아는 제작법이 아님 'CopperHelm'(손 제작·보이는 제작대·나무 작업대 목록에 없음)");
        Program.Check(n0 != null && n0.Contains("목표는 그대로") && n0.Contains("1단계 'craft CopperHelm 1'") && n0.Contains("craft CopperBreastArmor 1") && n0.Contains("say와 order는 적용됨"), "K1: cut 0 note: where, why, the steps not taken, say and order kept");
        var ps = new List<PlanStep> { new PlanStep("gather", "Wood", 15), new PlanStep("craft", "WoodHelm", 1), new PlanStep("craft", "WoodPants", 1) };
        string n2 = AIPlanRules.RefusedNote(ps, 2, "모르는 품목 'WoodPants'");
        Program.Check(n2 != null && n2.StartsWith("계획을 2단계까지만 받음: 3단계 'craft WoodPants 1'부터") && !n2.Contains("목표는 그대로"), "K1: cut 2 note: steps 1-2 taken, from step 3 not");
        Program.Check(AIPlanRules.RefusedNote(ps, -1, "x") == null && AIPlanRules.RefusedNote(ps, 3, "x") == null, "K1: no cut, or a cut past the steps: no note");
        // a re-ask at once only when no step was taken and nothing runs, and once
        Program.Check(AIPlanRules.RefusedAskAgain(0, false, false, false, false), "K1: nothing taken, nothing runs, not asked yet: ask again now");
        Program.Check(!AIPlanRules.RefusedAskAgain(0, true, false, false, false), "K1: a goal runs: no re-ask, the next report carries the why");
        Program.Check(!AIPlanRules.RefusedAskAgain(0, false, true, false, false) && !AIPlanRules.RefusedAskAgain(0, false, false, true, false), "K1: a plan runs or a step is about to start: no re-ask");
        Program.Check(!AIPlanRules.RefusedAskAgain(0, false, false, false, true), "K1: asked again already: not a second time (no loop on the same impossible step)");
        Program.Check(!AIPlanRules.RefusedAskAgain(1, false, false, false, false) && !AIPlanRules.RefusedAskAgain(-1, false, false, false, false), "K1: some steps were taken, or none cut: no re-ask");

        // K1 — the order of a cut-0 answer applies like the other fields; a follow is not taken (no goal carries it)
        var log = new List<string>();
        var changes = new List<string>();
        OrderChanged cb = (o, was, why) => changes.Add($"#{o.Id} {was}->{o.State}");
        var b = new AIHostOrders();
        var g = new HostOrder { Id = 1, Text = "@그록 구리로 방어구 만들어 입어", State = "받음" }; b.Add(g, cb);
        var f = b.ApplyAnswer("follow", null, "host_said", 1, true, "모루부터 만들게", "cut 0", cb, log.Add);
        Program.Check(g.State == "받음" && f.Count == 0, "K1: cut 0 with order follow: the instruction stays 받음 (open), no source carries it");
        var b2 = new AIHostOrders();
        var g2 = new HostOrder { Id = 1, Text = "@그록 구리 방어구", State = "받음" }; b2.Add(g2, cb);
        b2.ApplyAnswer("defer", null, "host_said", 1, true, "주괴가 없어 나중에", "cut 0", cb, log.Add);
        Program.Check(g2.State == "미룸", "K1: cut 0 with order defer: applied");

        // K2 — a stuck goal or plan step: 막힘, open, apart from the host's 멈춤
        var b3 = new AIHostOrders();
        var h = new HostOrder { Id = 1, Text = "@ChatGPT 기지로 와서 상자 정리해", State = "따름", Source = "llm:gpt#3" }; b3.Add(h, cb);
        var q = new HostOrder { Id = 2, Text = "@ChatGPT 지금 뭐 해?", State = "답함" }; b3.Add(q, cb);
        changes.Clear();
        b3.GoalEnded("llm:gpt#3", false, "home: 막힘(기지로 가는 길 없음)", cb);
        Program.Check(h.State == "막힘" && h.Open && h.Source == null && changes.Contains("#1 따름->막힘"), "K2: the plan step stuck: its instruction is 막힘, open, not carried any more");
        Program.Check(b3.EarlierOpen().Count == 1 && b3.EarlierOpen()[0] == h && b3.ReportFields(30f, x => x).Contains("\"state\":\"막힘\""), "K2: a 막힘 instruction stays in host_orders_open for the next report");
        var fb = b3.ApplyAnswer(null, new Dictionary<int, string> { { 1, "follow" } }, "plan_step_failed", 2, false, "다른 길로 갈게", "retry", cb, log.Add);
        Program.Check(h.State == "따름" && fb.Count == 1 && fb[0] == h, "K2: a 막힘 instruction can be followed again (orders)");
        var b4 = new AIHostOrders();
        var k = new HostOrder { Id = 1, Text = "@Claude 구리 10개", State = "막힘" }; b4.Add(k, cb);
        b4.ApplyAnswer("drop", null, "periodic", 1, false, "구리가 안 보여 포기", "drop", cb, log.Add);
        Program.Check(k.State == "포기", "K2: a 막힘 instruction can be dropped");
        // the host's /ai stop is still 멈춤, closed for good, and a stuck one closes with it
        var b5 = new AIHostOrders();
        var s1 = new HostOrder { Id = 1, Text = "a", State = "막힘" }; b5.Add(s1, cb);
        var s2 = new HostOrder { Id = 2, Text = "b", State = "따름", Source = "llm:claude#4" }; b5.Add(s2, cb);
        string owed = null, ended = null; bool next = false, held = false;
        AIPlanStop.Apply(new AIPlanQueue(), b5, "/ai stop", cb, ref owed, ref ended, ref next, ref held);
        Program.Check(s1.State == "멈춤" && s2.State == "멈춤" && b5.OpenAll().Count == 0, "K2: /ai stop closes 막힘 and 따름 alike as 멈춤");
        // a done goal is still 끝남
        var b6 = new AIHostOrders();
        var d = new HostOrder { Id = 1, Text = "c", State = "따름", Source = "host" }; b6.Add(d, cb);
        b6.GoalEnded("host", true, "done", cb);
        Program.Check(d.State == "따름" && d.Open, "stage completion keeps the original instruction open until an upper order=done");
    }

    // 검수 반영 (3-독립검수.md B1·B2·F1·F2·F4, 작은 지적 6·7·9): the planner's rules (AIPlanRules)
    private static void Rules(IList<MentionName> M)
    {
        // watching (B1): auto, a goal, a plan step about to start, or after any hold (on purpose until a set or /ai stop, 재검수 R4)
        Program.Check(!AIPlanRules.Watching(false, false, false, false), "nothing: not watching (no periodic look)");
        Program.Check(AIPlanRules.Watching(true, false, false, false) && AIPlanRules.Watching(false, true, false, false), "auto or a goal: watching");
        Program.Check(AIPlanRules.Watching(false, false, true, false), "between two plan steps (no goal for a frame): watching");
        Program.Check(AIPlanRules.Watching(false, false, false, true), "after a hold (auto off, goal put down): still watching, so the put-off instruction comes back (B1)");

        // stale answers (B2)
        Program.Check(AIPlanRules.OnStale(false, "periodic", "set", false) == StaleAction.Apply, "not stale: applied");
        Program.Check(AIPlanRules.OnStale(true, "periodic", "set", false) == StaleAction.Drop && AIPlanRules.OnStale(true, "goal_done", "keep", false) == StaleAction.Drop, "stale, not the host: dropped as before");
        Program.Check(AIPlanRules.OnStale(true, "host_said", "keep", false) == StaleAction.Apply && AIPlanRules.OnStale(true, "host_said", "ask", true) == StaleAction.Apply, "stale answer to the host: keep / ask applied (the say reaches the host)");
        Program.Check(AIPlanRules.OnStale(true, "host_said", "set", false) == StaleAction.AskAgain && AIPlanRules.OnStale(true, "host_goal", "hold", false) == StaleAction.AskAgain, "stale set / hold to the host: asked again once");
        Program.Check(AIPlanRules.OnStale(true, "host_said", "set", true) == StaleAction.TalkOnly, "stale twice: the say and order are kept, the goal is not changed");

        // order (F1): only the instruction the report carried
        Program.Check(AIPlanRules.OrderApplies(3, 3) && !AIPlanRules.OrderApplies(2, 3) && !AIPlanRules.OrderApplies(0, 0), "order applies only to the instruction the answer's report saw");

        // 실게임2 N1: plan steps going on while the report was on the way are not a new state
        Program.Check(!AIPlanRules.RevStale(19, 19, null, null), "same goal revision: not stale");
        Program.Check(!AIPlanRules.RevStale(19, 20, "llm:claude#11", "llm:claude#11") && !AIPlanRules.RevStale(19, 23, "llm:claude#11", "llm:claude#11"), "the same plan went on to the next steps: not stale (the fight_end answer of 5-실게임2 is applied)");
        Program.Check(AIPlanRules.RevStale(19, 20, null, null), "no plan and the goal changed: stale as before");
        Program.Check(AIPlanRules.RevStale(19, 20, "llm:claude#11", null), "the plan stopped or ended meanwhile (host goal, failed step, plan done): stale");
        Program.Check(AIPlanRules.RevStale(19, 20, "llm:claude#11", "llm:claude#14"), "another plan now: stale");
        Program.Check(AIPlanRules.RevStale(19, 20, null, "llm:claude#14"), "a plan started after the report was sent: stale");
        // 실게임3 M1: a plan that ended well while its report was on the way is that plan's last state, not a new one
        string ended = AIPlanRules.PlanNow(false, null, "llm:gpt#12", false);
        Program.Check(ended == "llm:gpt#12" && !AIPlanRules.RevStale(12, 13, "llm:gpt#12", ended), "the plan ended while the periodic answer was on the way: the answer is applied (not dropped)");
        Program.Check(AIPlanRules.PlanNow(false, null, "llm:gpt#12", true) == null && AIPlanRules.RevStale(12, 14, "llm:gpt#12", AIPlanRules.PlanNow(false, null, "llm:gpt#12", true)), "a goal set after the plan ended (host or another source): stale as before");
        Program.Check(AIPlanRules.PlanNow(true, "llm:gpt#15", "llm:gpt#12", false) == "llm:gpt#15" && AIPlanRules.RevStale(12, 16, "llm:gpt#12", AIPlanRules.PlanNow(true, "llm:gpt#15", "llm:gpt#12", false)), "another plan runs now: stale");
        Program.Check(AIPlanRules.PlanNow(false, null, null, false) == null && AIPlanRules.RevStale(12, 13, "llm:gpt#12", AIPlanRules.PlanNow(false, null, null, false)), "a plan that stopped (failed step, hold) is not an ended one: stale");
        Program.Check(!AIPlanRules.OwedCallGoes("set", false, true, true) && !AIPlanRules.OwedCallGoes("hold", false, false, false), "the owed plan_done: a set or hold answer replaces it");
        Program.Check(AIPlanRules.OwedCallGoes("keep", false, false, false) && AIPlanRules.OwedCallGoes("ask", false, false, false) && AIPlanRules.OwedCallGoes("set", true, false, false), "keep / ask / talk-only with nothing to go on: the owed plan_done goes once");
        Program.Check(!AIPlanRules.OwedCallGoes("keep", false, true, false) && !AIPlanRules.OwedCallGoes("keep", false, false, true), "keep while a goal or plan runs by then: no owed call");
        // 실게임3 M2 / 재검수 R2: only the order field changes the host's instruction; no order, no change
        Program.Check(AIPlanRules.OrderState("answered") == "답함" && AIPlanRules.OrderState("follow") == "따름" && AIPlanRules.OrderState(" Defer") == "미룸" && AIPlanRules.OrderState("drop") == "포기" && AIPlanRules.OrderState("done") == "끝남", "the order values");
        Program.Check(AIPlanRules.OrderState(null) == null && AIPlanRules.OrderState("") == null && AIPlanRules.OrderState("null") == null && AIPlanRules.OrderState("yes") == null, "no order (or an unknown one): no change — not 답함, not 따름 (R2)");
        // 재검수 R5 and 작은 지적 3·5
        Program.Check(AIPlanRules.EventKept(false, true) && AIPlanRules.EventKept(true, false) && !AIPlanRules.EventKept(false, false), "the planner's own events reach the report whatever the mode; others only while watching (R5)");
        Program.Check(AIPlanRules.PlanOrphaned(true, false, false, null, "llm:claude#4") && AIPlanRules.PlanOrphaned(true, false, true, "host", "llm:claude#4"), "a plan without its goal (cleared outside it, or another goal): stopped like a failed step");
        Program.Check(!AIPlanRules.PlanOrphaned(true, false, true, "llm:claude#4", "llm:claude#4") && !AIPlanRules.PlanOrphaned(true, true, false, null, "llm:claude#4") && !AIPlanRules.PlanOrphaned(false, false, false, null, null), "its step running, between two steps, or no plan: not orphaned");
        Program.Check(AIPlanRules.Text("null") == null && AIPlanRules.Text(null) == null && AIPlanRules.Text("네") == "네", "\"say\": null is no say (작은 지적 5)");
        Program.Check(AIPlanRules.EarlierOrderApplies(2, 4) && AIPlanRules.EarlierOrderApplies(4, 4) && !AIPlanRules.EarlierOrderApplies(5, 4) && !AIPlanRules.EarlierOrderApplies(0, 4), "an orders entry applies to instructions the report carried");
        Orders();
        // 실게임2 N2: a hold that follows the host's "wait" is not a put-off
        Program.Check(!AIPlanRules.HoldPutsOff("follow") && !AIPlanRules.HoldPutsOff(" Follow "), "hold with order follow: the instruction stays followed");
        Program.Check(AIPlanRules.HoldPutsOff(null) && AIPlanRules.HoldPutsOff("") && AIPlanRules.HoldPutsOff("defer"), "hold without follow: put off as before");
        // 실게임2 N5: the chat is written with signs the game font has
        Program.Check(ChatPolicy.FontSafe("투구→갑옷→바지") == "투구>갑옷>바지" && ChatPolicy.FontSafe("잠깐…") == "잠깐..." && ChatPolicy.FontSafe("a — b") == "a - b", "arrows, ellipsis and dashes become ASCII");
        Program.Check(ChatPolicy.FontSafe("[Claude] 체력 43/100 · 가방 (Tab) @모두") == "[Claude] 체력 43/100 · 가방 (Tab) @모두" && ChatPolicy.FontSafe(null) == null, "Hangul, ·, @ and brackets stay");
        Program.Check(AIMention.Hint("@c", new List<MentionName> { new MentionName("claude", "Claude", new[] { "claude", "클로드" }) }) is string h && ChatPolicy.FontSafe(h) == h, "the hint line needs no change for the font");
        // 실게임2 N6: the same error notice once per character in five minutes
        var gate = new ChatRepeatGate();
        Program.Check(gate.Allow("claude|decisions-fail", 0f) && !gate.Allow("claude|decisions-fail", 120f) && gate.Allow("gemini|decisions-fail", 120f), "same notice hidden within 5 min, another character's shown");
        Program.Check(gate.Allow("claude|decisions-fail", 301f) && gate.Hidden == 1, "shown again after 5 min; one was hidden");

        // retry (F2)
        Program.Check(AIPlanRules.MayRetry(true, "goal_done", 9), "watching: a failed report goes again");
        Program.Check(!AIPlanRules.MayRetry(false, "goal_done", 0) && !AIPlanRules.MayRetry(false, "retry", 0), "not watching: no endless retry of other reports");
        Program.Check(AIPlanRules.MayRetry(false, "host_said", 2) && !AIPlanRules.MayRetry(false, "host_said", AIPlanRules.HostTries), "not watching: the host's words up to 3 sends, then stop (and tell)");

        // one down told once (F4)
        Program.Check(!AIPlanRules.DownTold(1, 0) && AIPlanRules.DownTold(1, 1) && !AIPlanRules.DownTold(2, 1), "a down is told once (back_up or fight_end), a new down again");
        var fd = new FightRecord { Downs = 1, EndHp = 100, MaxHp = 100 };
        Program.Check(AIFightLog.ShouldReport(fd, false) && !AIFightLog.ShouldReport(fd, true), "a fight whose down back_up already told: no second call");
        var fl = new FightRecord { Downs = 0, EndHp = 30, MaxHp = 100 };
        Program.Check(AIFightLog.ShouldReport(fl, true), "a low fight without a down calls whatever back_up did");
        var fdl = new AIFightLog(); fdl.Add(new FightRecord { At = 0f, Downs = 1, EndHp = 100, MaxHp = 100 });
        Program.Check(fdl.ReportJson(1f, x => x).Contains("끝 체력 100/100(다시 일어난 뒤)"), "end health after a down is marked (작은 지적 9)");

        // English aliases need a word end (작은 지적 6); Korean particles still fine
        Program.Check(AIMention.Parse("@allright 가자", M, out bool allA).Count == 0 && !allA, "@allright is not @all");
        Program.Check(AIMention.Parse("@claudex 안녕", M, out _).Count == 0, "@claudex is not @claude");
        Program.Check(AIMention.Parse("@claude야 와", M, out _).Count == 1 && AIMention.Parse("@Claude, 와", M, out _).Count == 1, "@claude야 / @Claude, still Claude");

        // the inbox: what a report sent now takes along (작은 지적 7 is in AIPlanner.Report: the higher priority goes)
        Program.Check(PlanInbox.Priority("host_said") > PlanInbox.Priority("plan_done") && PlanInbox.Priority("plan_step_failed") > PlanInbox.Priority("periodic"), "plan triggers rank with the goal events");
        Program.Check(AIPlanRules.StepDone("goal_done") && AIPlanRules.StepDone("completed") && AIPlanRules.StepDone("explore_ended"), "a step is done on goal_done / completed / explore_ended");
        Program.Check(!AIPlanRules.StepDone("goal_stalled") && !AIPlanRules.StepDone("blocked") && !AIPlanRules.StepDone("only_wait") && !AIPlanRules.StepDone("cannot_craft"), "stalls stop the plan");
    }

    // 재검수 R1·R2·R3 (9-재검수.md): the host's instructions (AIHostOrders) and /ai stop (AIPlanStop), the calls AIPlanner
    // makes at those places, with the same arguments
    private static void Orders()
    {
        var log = new List<string>();
        var changes = new List<string>();
        OrderChanged cb = (o, was, why) => changes.Add($"#{o.Id} {was}->{o.State}");
        var b = new AIHostOrders();
        HostOrder Said(int id, string text) { var o = new HostOrder { Id = id, Text = text, At = id * 10f, State = "받음" }; b.Add(o, cb); return o; }

        // R2: the 3판 Claude #2 case — the same work said again, answered keep + say without order: nothing changes
        var o1 = Said(1, "@모두 나무 방어구 한 벌씩 맞추고, 끝나면 구리 곡괭이도");
        var f1 = b.ApplyAnswer("follow", null, "host_said", 1, false, "알겠어요", "방어구부터", cb, log.Add);
        Program.Check(o1.State == "따름" && f1.Count == 1 && f1[0] == o1, "order follow: followed, and the set answer's source will carry it");
        o1.Source = "llm:claude#2";
        var o2 = Said(2, "@모두 나무 방어구 한 벌씩 맞추고, 끝나면 구리 곡괭이도");
        log.Clear();
        var f2 = b.ApplyAnswer(null, null, "host_said", 2, false, "네, 지금 나무 모으는 중이에요", "같은 일", cb, log.Add);
        Program.Check(o2.State == "받음" && f2.Count == 0 && log.Count == 1 && log[0].Contains("no order"), "R2: keep + say without order leaves the instruction as it was (logged), not 답함");
        Program.Check(o1.State == "따름" && o1.Source == "llm:claude#2" && b.EarlierOpen().Count == 1 && b.EarlierOpen()[0] == o1, "R3: the earlier followed instruction is not overwritten and keeps its plan");
        b.GoalEnded("llm:claude#2", true, "구리 곡괭이: 완료", cb);
        Program.Check(o1.State == "따름" && b.EarlierOpen().Count == 1 && b.Latest == o2, "plan completion preserves the earlier original instruction for upper judgement");

        // R3: a put-off instruction survives a question
        var b3 = new AIHostOrders();
        var d = new HostOrder { Id = 1, Text = "@Claude 구리 10개 캐 와", State = "받음" }; b3.Add(d, cb);
        b3.ApplyAnswer("defer", null, "host_said", 1, false, "체력이 낮아 기지부터", "hp", cb, log.Add);
        var q = new HostOrder { Id = 2, Text = "@Claude 왜?", State = "받음" }; b3.Add(q, cb);
        b3.ApplyAnswer("answered", null, "host_said", 2, false, "체력이 20이라서요", "q", cb, log.Add);
        Program.Check(d.State == "미룸" && q.State == "답함" && b3.EarlierOpen().Count == 1 && b3.EarlierOpen()[0] == d, "R3: the question is answered and the put-off 구리 10개 is still in host_orders_open");
        string rj = b3.ReportFields(30f, x => x);
        Program.Check(rj.StartsWith("\"host_order\":{\"id\":2,") && rj.Contains("\"host_orders_open\":[{\"id\":1,\"text\":\"@Claude 구리 10개 캐 와\"") && rj.Contains("\"state\":\"미룸\""), "report: host_order is the latest, host_orders_open the earlier open ones");
        // coming back to it later: orders {"1": "follow"} on a set answer
        var f3 = b3.ApplyAnswer(null, new Dictionary<int, string> { { 1, "follow" } }, "periodic", 2, false, "이제 구리 캐러 가요", "회복됨", cb, log.Add);
        Program.Check(d.State == "따름" && f3.Count == 1 && f3[0] == d, "orders: the earlier put-off one is followed again (its source is set by the set answer)");
        var f4 = b3.ApplyAnswer(null, new Dictionary<int, string> { { 7, "drop" } }, "periodic", 2, false, "", "", cb, log.Add);
        Program.Check(f4.Count == 0 && d.State == "따름", "orders for an instruction the report did not carry: not applied");
        var b4 = new AIHostOrders();
        var t = new HostOrder { Id = 1, Text = "@Claude 나무 캐 와", State = "받음" }; b4.Add(t, cb);
        b4.ApplyAnswer("follow", null, "host_said", 1, true, "", "", cb, log.Add);
        Program.Check(t.State == "받음", "talk-only (stale twice, the goal not changed): a follow is not taken (작은 지적 4)");
        // the cap: three open at most, the oldest open one is pushed out
        var b5 = new AIHostOrders();
        var x1 = new HostOrder { Id = 1, Text = "a", State = "미룸" }; b5.Add(x1, cb);
        b5.Add(new HostOrder { Id = 2, Text = "b", State = "미룸" }, cb);
        b5.Add(new HostOrder { Id = 3, Text = "c", State = "받음" }, cb);
        changes.Clear();
        b5.Add(new HostOrder { Id = 4, Text = "d", State = "받음" }, cb);
        Program.Check(x1.State == "미룸" && b5.OpenAll().Count == 4 && b5.Find(1) == x1 && changes.Count == 0, "a fourth open instruction preserves every earlier open request");

        // a hold or a host /ai goal over a plan puts its followed instructions off (still open), except one the hold follows
        var b7 = new AIHostOrders();
        var c1 = new HostOrder { Id = 1, Text = "@Claude 방어구", State = "따름", Source = "llm:claude#5" }; b7.Add(c1, cb);
        var c2 = new HostOrder { Id = 2, Text = "@Claude 잠깐 기다려", State = "따름", Source = "llm:claude#5" }; b7.Add(c2, cb);
        Program.Check(b7.Carried("llm:claude#5").Count == 2 && b7.Carried("llm:claude#6").Count == 0 && b7.Carried(null).Count == 0, "Carried: the followed instructions a source carries");
        b7.PutOff("llm:claude#5", c2, "hold로 내려놓음", "hold", cb);
        Program.Check(c1.State == "미룸" && c1.Source == null && c2.State == "따름" && b7.EarlierOpen()[0] == c1, "PutOff: the plan put down leaves its instructions put off (open), the one the hold follows stays followed");

        // R1: /ai stop clears the owed plan call and closes the open instructions as 멈춤
        var plan = new AIPlanQueue();
        plan.Set("llm:claude#9", new List<PlanStep> { new PlanStep("gather", "Wood", 15), new PlanStep("craft", "WoodHelm", 1) });
        var b6 = new AIHostOrders();
        var s1 = new HostOrder { Id = 1, Text = "@Claude 방어구 맞춰", State = "따름", Source = "llm:claude#9" }; b6.Add(s1, cb);
        var s2 = new HostOrder { Id = 2, Text = "@Claude 그다음 구리", State = "미룸" }; b6.Add(s2, cb);
        string owed = "plan_done", ended = "llm:claude#9"; bool next = true, held = true;
        string ev = AIPlanStop.Apply(plan, b6, "/ai stop", cb, ref owed, ref ended, ref next, ref held);
        Program.Check(owed == null && ended == null && !next && !held, "R1: the owed plan_done, the ended plan, the next step and the hold watch go");
        Program.Check(s1.State == "멈춤" && s2.State == "멈춤" && s1.Source == null && b6.OpenAll().Count == 0 && !plan.Active && plan.ReportJson(x => x) == "null", "R1: every open instruction closes as 멈춤 and the plan goes");
        Program.Check(ev != null && ev.Contains("/ai stop") && ev.Contains("2개"), "R1: the stop is an event for the next report");
        // after the stop the next answer's follow cannot bring the stopped work back through the old instruction
        var f6 = b6.ApplyAnswer(null, new Dictionary<int, string> { { 1, "follow" } }, "host_said", 2, false, "", "", cb, log.Add);
        Program.Check(f6.Count == 0 && s1.State == "멈춤", "R1: a stopped instruction is closed for good");
        string owed2 = null, ended2 = null; bool n2 = false, h2 = false;
        Program.Check(AIPlanStop.Apply(new AIPlanQueue(), new AIHostOrders(), "/ai stop", cb, ref owed2, ref ended2, ref n2, ref h2) == null, "nothing open or running: no stop event");

        // the answer reader takes "orders"
        var ro = AIPlanProtocol.Read("{\"answer\":{\"decision\":\"set\",\"orders\":{\"1\":\"follow\",\"x\":\"drop\",\"3\":5},\"say\":null}}");
        Program.Check(ro.Answer.Orders != null && ro.Answer.Orders.Count == 1 && ro.Answer.Orders[1] == "follow", "orders: integer ids with string values");
        Program.Check(AIPlanRules.Text(ro.Answer.Say) == null, "a JSON null say is read as no say");
    }

    // 합의 3e: the plan queue and its report part; the answer reader takes "plan"
    private static void Plans()
    {
        var q = new AIPlanQueue();
        Program.Check(!q.Active && q.ReportJson(x => x) == "null", "no plan: plan null");
        q.Set("llm:claude#7", new List<PlanStep> { new PlanStep("gather", "Wood", 15), new PlanStep("craft", "WoodHelm", 1), new PlanStep("craft", "WoodPantsArmor", 1) });
        Program.Check(q.Active && q.Index == 0 && q.Owns("llm:claude#7") && !q.Owns("llm:claude#8") && !q.Owns("host"), "a plan owns the goals set with its source");
        Program.Check(q.ReportJson(x => x) == "{\"step\":1,\"of\":3,\"now\":\"gather Wood 15\",\"next\":[\"craft WoodHelm 1\",\"craft WoodPantsArmor 1\"]}", "report: step k of n, now, next");
        var s2 = q.Advance();
        Program.Check(s2 != null && s2.Item == "WoodHelm" && q.Index == 1 && q.HasNext, "a done step: the next one, no call");
        q.Stop("craft WoodHelm 1: 막힘(재료 없음)");
        Program.Check(!q.Active && q.ReportJson(x => x) == "{\"stopped\":{\"at\":\"craft WoodHelm 1\",\"why\":\"craft WoodHelm 1: 막힘(재료 없음)\",\"left\":[\"craft WoodPantsArmor 1\"]}}", "a failed step stops the rest; the report shows where and what is left");
        q.ForgetStopped();
        Program.Check(q.ReportJson(x => x) == "null", "the stopped plan is shown once");
        q.Set("llm:claude#9", new List<PlanStep> { new PlanStep("home", null, -1) });
        Program.Check(q.Advance() == null && !q.Active, "the last step done: the plan is over");
        q.Set("llm:claude#10", new List<PlanStep> { new PlanStep("gather", "Wood", 5), new PlanStep("home", null, -1) });
        q.Set("llm:claude#11", new List<PlanStep> { new PlanStep("explore", null, -1) });
        Program.Check(q.Owns("llm:claude#11") && !q.Owns("llm:claude#10") && q.Count == 1, "a new set answer replaces what was left");

        var rp = AIPlanProtocol.Read("{\"ok\":true,\"answer\":{\"decision\":\"set\",\"for\":{\"report\":4,\"goal_rev\":2},\"plan\":[{\"do\":\"gather\",\"item\":\"Wood\",\"count\":15},{\"do\":\"craft\",\"item\":\"WoodHelm\",\"count\":1},{\"do\":\"home\"},7],\"say\":\"방어구 맞출게요\"}}");
        var pl = rp.Answer?.Plan;
        Program.Check(pl != null && pl.Count == 4 && pl[0].Text == "gather Wood 15" && pl[1].Text == "craft WoodHelm 1" && pl[2].Text == "home" && pl[3].Do == null, "the reader takes plan steps in order (a non-object step stays as an empty step)");
        Program.Check(!rp.Answer.HasGoal && AIPlanProtocol.Read("{\"answer\":{\"decision\":\"keep\"}}").Answer.Plan == null, "no plan key: Plan null");
    }
}
