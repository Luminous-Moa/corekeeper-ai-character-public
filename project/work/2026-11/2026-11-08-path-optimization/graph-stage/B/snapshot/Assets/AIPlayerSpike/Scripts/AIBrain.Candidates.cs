// AIBrain's candidate generator (refactor S6b, review F11; moved unchanged from BuildCandidates in AIBrain.Loop.cs):
// the build's locals are fields of the same name, its local functions and blocks are methods, so the moved lines
// keep their text. BuildCandidates calls the blocks in the old order.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Networking;

public static partial class AIBrain
{
    // Kept as a production entry point so the offline adapter executes the same page assembly and controls.
    internal static List<AICandidate> AssembleLifePage(World server, Entity player, AIObservation o, string curKey, bool combat, List<AICandidate> candidates, AICandidate direct = null)
        => AILife.Compose(server, player, o, curKey, combat, candidates, direct);

    internal static void AddCommonControls(List<AICandidate> list, bool prefetch, AISkill cur)
    {
        if (!prefetch && cur != null)
            list.Add(new AICandidate { kind = "stop_action", key = "control:stop:" + cur.Run, run = cur.Run, value = "stop_action", desc = "지금 동작 중단 — 현재 몸 동작만 멈추고 원래 부탁과 목표는 유지. 다음 행동을 다시 선택." });
        if (Goal.IsReal)
        {
            list.Add(new AICandidate { kind = "finish_goal", key = "control:finish", value = "finish_goal", desc = $"현재 단계 완료 — '{Goal.Text}'를 완료로 보고 다음 계획 단계로 진행하거나 위쪽 LLM에게 다음 일을 요청. 관측: {AIGoals.Observation}. 원래 부탁 완료는 위쪽 LLM이 판단." });
            if (!AIPlanner.ReplanPending) list.Add(new AICandidate { kind = "request_plan", key = "control:plan", value = "request_plan", desc = "상위 재계획 요청 — 지금 상황과 막힌 이유를 위쪽 LLM에 알리고 작업 순서를 다시 판단하게 함. 응답 동안 실행 가능한 현재 동작 유지." });
        }
        else if (!list.Exists(c => c.kind == "stop_action"))
            list.Add(new AICandidate { kind = "stop_action", key = "control:stop", value = "stop_action", desc = "현재 행동 중단 — 몸을 멈추고 다음 행동을 다시 선택." });
    }

    private sealed class CandBuild
    {
        World server; Entity player; AIObservation o;
        readonly bool prefetch, breachCall;
        AITileReader tr; AISkill cur; string curKey; int est; string estNote; int2 me; HashSet<int2> reach;
        bool free; List<AICandidate> lCraft; List<ObjectID> wanted; bool canMine, canFight; string noMine, noFight;
        float tnow0; AsmRec asm; bool gatherOre; ObjectID wantedFirst; List<string> blocks; int toolRed; List<string> guardBlocks = new List<string>();
        List<AICandidate> lCont, lFollow, lEnemy, lMine, lDig, lRemember, lItem, lExplore, lDigExplore, lOther, lCombat, lLife;
        ObjectID weapon; string weaponNote; int2? curDig; List<SeenPlayer> mates; List<SeenEnemy> near3;
        bool inCombat; bool foodNeed; List<(float d, int2 p, ObjectID what)> digOre; HashSet<AICandidate> foodPicks;
        bool wantExplore; List<string> gfacts;
        HashSet<int2> objB; List<AICandidate> lBrute = new List<AICandidate>(); SeenEnemy brute;
        // review F1 / F4 (2026-11-06): own doors to close (every set) and the base-edge options (after the goal's own)
        List<AICandidate> lDoor = new List<AICandidate>(), lEdge = new List<AICandidate>(); ChargeView bv; float bDelay; Func<V2, bool> blockedV, chargeBlockedV; int bChargeN;
        // the continue option's parts (Continue), for the close-fight text of an action that ends before the answer (AIContinueText)
        string cSince = "", cExplore = "", cProg = "", cMotion = "";

        public CandBuild(World server, Entity player, AIObservation o, bool prefetch, bool breachCall) { this.server = server; this.player = player; this.o = o; this.prefetch = prefetch; this.breachCall = breachCall; }

        public void Prep()
        {
            _server = server;
            tr = new AITileReader(server);
            cur = AIBody.Current != null && AIBody.Current.Status == SkillStatus.Running ? AIBody.Current : null;
            curKey = cur?.Key ?? "";
            // damage with the tool MineSkill will pick (best pick in the hotbar; remembered bonus when not held), not with
            // whatever is in hand now (a sword in hand made every wall look "tool short")
            est = AITunnel.PlannedDamage(server, player, out int eff, out estNote);
            // Another damage estimate: the cached "no way" plans (kept 10 s) are planned again — a better pick may open a way.
            // Found routes stay (they expire within 5 s anyway). Clearing every route here made Claude plan 5-11 routes per
            // decision with almost nothing cached (prep 120-290 ms, 12 of 47 PERF batches over 180 ms, 2026-11-04 막힘-멈춤-정리
            // 2단계): the estimate flips between the live value (pick in hand) and the remembered bonus (sword in hand).
            if (est != S._planDamage)
            {
                var noWay = new List<long>();
                foreach (var kv in S._plans) if (kv.Value.plan == null) noWay.Add(kv.Key);
                foreach (var key in noWay) S._plans.Remove(key);
                S._planDamageFlips++;
                if (S._planDamage >= 0 && Time.realtimeSinceStartup - S._planDamageLogAt > 10f)
                {
                    S._planDamageLogAt = Time.realtimeSinceStartup;
                    AIPlayerSpikeMod.Log($"BRAIN: planned mining damage {S._planDamage} -> {est} ({estNote}); {noWay.Count} cached no-way plans dropped, {S._plans.Count} found routes kept; changes so far {S._planDamageFlips}");
                }
                S._planDamage = est;
            }
            me = AIEdge.StandTile(server, o.pos);   // live-run fix 2: pressed against a shut door is not "on the door tile"
            var objBlocked = AIObjects.BlockedInView(server, player, out string objNote); _objNote = objNote;
            objB = objBlocked;
            reach = ReachableInView(tr, o, me, objBlocked);
            // what to gather: the gather item, or the missing gatherable materials of a craft goal (preparation path)
            // (free time gathers and makes nothing: its options are the free-time set below)
            free = Goal.kind == GoalKind.Free;
            lCraft = null;
            wanted = free ? new List<ObjectID>() : WantedItems(server, player, out lCraft);
            if (free) { lCraft = new List<AICandidate>(); _prepNote = ""; AIBase.ResetMissing(); }
            AIGoalFeatures.AddCandidates(server, player, o, curKey, lCraft);
            // 코어-안전지대 단계 2: the base edge's actions while a plan is set (chosen by the test command until 단계 4)
            // (단계 3 review F3: what cannot be done now and why goes into the blocked-target notes, guardBlocks -> blocks)
            guardBlocks = new List<string>();
            // (review F4: in their own list lEdge, after the goal's own options; no count quota now removes either)
            void AddEdge(List<AICandidate> add) { foreach (var c in add) if (!lCraft.Exists(x => x.key == c.key) && !lEdge.Exists(x => x.key == c.key)) lEdge.Add(c); }
            if (AIEdge.PlanActive) { try { AddEdge(AIEdge.Candidates(server, player, o, curKey, guardBlocks)); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: edge candidates failed", e); } }
            // 코어-안전지대 단계 3: keeping the base (repair, a gone door back, pick-up, repair materials; no goal progress, G6)
            if (AIEdge.PlanActive) { try { AddEdge(AIEdge.GuardCandidates(server, player, o, curKey, guardBlocks)); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: guard candidates failed", e); } }
            // live-run fix 3 / review F1: an own door left open is a close option in every set — with no goal (a
            // self-defence call) and in a close fight too (lDoor; the assembler puts it right after the combat options)
            if (AIEdge.PlanActive || AIEdge.HasMyDoors) { try { lDoor = AIEdge.DoorCloseCandidates(server, o); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: door close candidates failed", e); } }
            // What the body can hold (U.no-pick-full-hotbar): mining, digging through walls and root digging need a pick or
            // an empty hand; attacking and breaking need a weapon or an empty hand. AIBody.HandSlot moves a pick or weapon
            // from the bag, or a hotbar item into the bag, as a player would; when even that is impossible such options are
            // not offered and the reason is in the block note (the stall text and the input say it).
            canMine = AIBody.CanHold(server, player, true, out _, out noMine);
            canFight = AIBody.CanHold(server, player, false, out _, out noFight);
            S._noMineWhy = canMine ? "" : noMine; S._noFightWhy = canFight ? "" : noFight;
            tnow0 = Time.realtimeSinceStartup;
            // refactor S6a: the assembly recorder of this build (test switch decisionlog.on only; null otherwise)
            asm = AITestSwitches.DecisionLog ? new AsmRec { Build = ++_asmBuilds, Depth = _rebuildingForTool ? 1 : 0, Tnow0 = tnow0 } : null;
            gatherOre = wanted.Count > 0;
            wantedFirst = gatherOre ? wanted[0] : ObjectID.None;
            blocks = new List<string>(guardBlocks);
            toolRed = 0;   // easiest tool-short goal wall in this build (visible walls and tunnel plans to goal ores; 0 = none)

            lCont = new List<AICandidate>(); lFollow = new List<AICandidate>(); lEnemy = new List<AICandidate>();
            lMine = new List<AICandidate>(); lDig = new List<AICandidate>(); lRemember = new List<AICandidate>();
            lItem = new List<AICandidate>(); lExplore = new List<AICandidate>(); lDigExplore = new List<AICandidate>();
            lOther = new List<AICandidate>();
            lCombat = new List<AICandidate>();
        }

        bool Reachable(int2 t) => reach.Contains(t);
        bool StandReachable(int2 w) => Reachable(w + new int2(1, 0)) || Reachable(w + new int2(-1, 0)) || Reachable(w + new int2(0, 1)) || Reachable(w + new int2(0, -1));
        // Historical failures annotate candidates; they do not revoke a new choice.
        bool LeftOut(string key) => false; // Failure history is evidence for the model.
        bool NeedsPick(AICandidate c) => c.kind == "mine" || c.kind == "root" || (c.kind == "dig" && (PlanC(server, player, c.tile, out _)?.walls.Count ?? 1) > 0);
        bool BodyCan(AICandidate c) => c.kind == "continue" || ((canMine || !NeedsPick(c)) && (canFight || (c.kind != "attack" && c.kind != "attacknow" && c.kind != "break")));
        bool Doable(AICandidate c) => c.kind == "continue" || (!LeftOut(c.key) && BodyCan(c));
        int MinRed(int a, int b) => b <= 0 ? a : a <= 0 ? b : math.min(a, b);

        public void Continue()
        {
            if (cur != null && !prefetch)   // a prefetch asks what comes after the running action: no "continue"
            {
                string prog = "";
                try { prog = cur.Progress(server, player); } catch { }
                float ran = Time.realtimeSinceStartup - cur.StartTime;
                string since = ran < 1f ? $"방금({ran:F1}초 전) 시작한" : $"{ran:F0}초째 하고 있는";
                string motion = AIMotion.AtBoundary ? "" : $" 지금 동작: {AIMotion.Mid}.";
                motion += AIBody.DoorStepNote;   // 검수 F7: in the middle of a door step a new choice leaves the door open
                cSince = since; cProg = prog; cMotion = motion; cExplore = cur.Tag.StartsWith("explore:") ? $"({cur.Tag.Substring(8)}쪽 탐험 걷기)" : "";
                // progress: whether the running action was a goal step when it was chosen (a walk to a non-goal pickup is not)
                lCont.Add(new AICandidate { kind = "continue", key = curKey, run = cur.Run, progress = S._runKey != curKey || S._runProgress, desc = $"계속하기 — {since} '{cur.Name}'({curKey}){cExplore} 이어 하기{(string.IsNullOrEmpty(prog) ? "" : " (" + prog + ")")}.{motion} 바꾸는 비용: 다른 행동을 고르면 지금 동작이 끝나는 대로 이 일은 멈추고, 지금까지의 진행은 이어지지 않으며 새 행동은 처음부터(다가가기·무기 들기·길 찾기부터) 시작함." });
            }
        }

        public void Enemies()
        {
            // prefetch for an attack: it ends when the target dies, so "after it" the target is not an option (a queued
            // "keep away from that enemy" was dropped as "대상 적이 없음" in ai2)
            if (prefetch && cur is AttackSkill && curKey.StartsWith("attack:")) o.enemies.RemoveAll(x => "attack:" + x.entity.Index == curKey);

            weapon = AIBody.FindWeapon(server, player, out bool wBroken); _weaponBroken = wBroken;
            FindBrute();
            weaponNote = weapon != ObjectID.None ? $"무기 {weapon}를 손에 들고 근접 공격(가방에 있으면 손 칸으로 옮김).{(wBroken ? $" 이 {weapon}는 내구도 0(부서짐, 다른 쓸 수 있는 무기 없음): 게임에서 부서진 무기는 피해가 크게 줄어듦." : "")}" : "무기가 없어 맨손으로 근접 공격(약 10 피해/0.4초, 빈 손 칸이 있어야 함).";
            // enemies (max 3 reachable-or-not as attack; the attack skill reports no path itself) + one dig toward an
            // enemy that has no open path
            // target of a running tunnel (dig:x,y), to avoid re-offering the same ore cluster as a new tunnel
            curDig = null;
            if (cur is TunnelSkill && (curKey.StartsWith("dig:") || curKey.StartsWith("digedge:")))
            {
                var sp = curKey.Substring(curKey.IndexOf(':') + 1).Split(',');
                if (sp.Length == 2 && int.TryParse(sp[0], out int dx) && int.TryParse(sp[1], out int dy)) curDig = new int2(dx, dy);
            }
            // other players on this character's own screen (host, other AI characters); never attack options
            mates = AITeammates.Visible(server, player, o.enemies);
            _curMoveName = cur?.Name; _curMoveTo = null;
            if (!prefetch && cur != null)
            {
                if (cur is KeepAwaySkill ka) _curMoveTo = ka.To;
                else if (cur is RetreatSkill rs) { var rp0 = PlanC(server, player, rs.Point, out _); _curMoveTo = rp0 != null && rp0.path.Count > 0 ? rp0.path[math.min(5, rp0.path.Count - 1)] : rs.Point; }
                else if (cur is FollowSkill && curKey.StartsWith("follow:")) { var mt = mates.Find(p => "follow:" + p.entity.Index == curKey); if (mt != null) _curMoveTo = AIBody.TileOf(mt.pos); }
                else if ((cur is GotoSkill || cur is TunnelSkill) && (curKey.StartsWith("goto:") || curKey.StartsWith("dig:") || curKey.StartsWith("digedge:")))
                {
                    var sp = curKey.Substring(curKey.IndexOf(':') + 1).Split(',');
                    if (sp.Length == 2 && int.TryParse(sp[0], out int mx) && int.TryParse(sp[1], out int my)) _curMoveTo = new int2(mx, my);
                }
            }
            // Every observed enemy, listed in a stable order (entity index) so the same enemy
            // keeps its place in the list between the ~0.3 s combat calls (the order by distance flipped as they moved).
            near3 = new List<SeenEnemy>(o.enemies);
            // 코어-안전지대 단계 3 (kill, user decision): with a base the enemies inside it come first, then the nearest
            // outside ones too (the AI's view decides "near", no radius); the facts say which side each is
            if (AIEdge.PlanActive)
            {
                var ins = o.enemies.FindAll(x => AIEdge.IsInside(AIEdge.EnemyTile(server, x.pos)));   // 검수 F8
                near3 = new List<SeenEnemy>(ins);
                foreach (var x in o.enemies) if (!near3.Contains(x)) near3.Add(x);
            }
            near3.Sort((a, b) => a.entity.Index.CompareTo(b.entity.Index));
            string doorBlocked = null;   // 재검수 R2b: read once per build
            foreach (var e in near3)
            {
                string k = "attack:" + e.entity.Index;
                int2 et = AIEdge.EnemyTile(server, e.pos);   // 검수 F8: the one reading of an enemy's tile (facts, attack, its walk, PlanC)
                bool open = Reachable(et) || StandReachable(et);
                string mate = AITeammates.NearEnemyNote(e.pos);
                // 단계 3: the way to it (open / through an own door / unseen / behind a wall), its side of the base, its kind.
                // No walk reaches it and it is out of hit reach: an attack cannot be done — not offered, said in the notes
                // (단계 1 M4c: 'keep attacking' an enemy behind a fence, 287 calls in 100 s)
                string kf = ""; EnemyReach er = new EnemyReach { kind = ReachKind.Unseen, comes = true };
                try { kf = AIEdge.KillFacts(server, player, e, out er); } catch (Exception ex) { AIPlayerSpikeMod.LogError("brain: kill facts failed", ex); }
                bool cannot = er.kind == ReachKind.None && !(e.dist <= AIBaseGuard.HitReach && er.touch);   // 재실측 N-g: not through a wall / shut door
                if (cannot && k != curKey) blocks.Add($"공격 — 적 {e.id} ({et.x},{et.y}) {e.dist:F1}칸: {kf}");
                // the full fight facts of each enemy kind, once per 20 s (the SEND line shows only the first 120 characters)
                if (!S._fightLog.TryGetValue(e.id, out float fl) || Time.realtimeSinceStartup - fl >= 20f)
                {
                    S._fightLog[e.id] = Time.realtimeSinceStartup;
                    AIPlayerSpikeMod.Log($"BRAIN: fight facts {e.id} hp {e.health}/{e.maxHealth} at {e.dist:F1} tiles, my hp {o.health}: {FightNote(weapon, e, o.health)}{mate}");
                }
                if (k != curKey && !cannot)
                {
                    // combat stage 2: against the charging enemy of a close fight, the attack option says what an attack
                    // does now (damage limit, time, the charge) instead of this character's fight history (step-1 part)
                    bool isBrute = brute != null && e.entity == brute.entity; string bpred = "";
                    lEnemy.Add(new AICandidate { kind = "attack", key = k, entity = e.entity, tile = et,
                        desc = isBrute ? BruteAttackDesc(e, open, mate, out bpred) + (kf != "" ? " " + kf : "") : $"{(er.kind == ReachKind.Door ? "문으로 나가서 치기" : "공격")} — 적 {e.id} ({e.pos.x:F0},{e.pos.z:F0}) {e.dist:F1}칸, 적 체력 {e.health}/{e.maxHealth}. 내 체력 {o.health}/{o.maxHealth}. {weaponNote} {FightNote(weapon, e, o.health)}{mate} 처치·대상 소실 또는 직접 중단·전환을 고를 때까지 계속.{(kf != "" ? " " + kf : open ? "" : " 지금 보이는 열린 길로는 닿지 않음.")}{(er.kind == ReachKind.Door ? " " + AIDoorPass.NoSwingNote : "")}", pred = bpred });
                }
                // 재검수 R2b: the running attack on this enemy waits for its door step's close and the close is blocked (an
                // enemy's body in the door or on the press spot, or hit since the door opened): the code does not end the
                // wait — "hit now" is an option of its own (choosing the same attack would only continue the wait)
                if (k == curKey && cur is AttackSkill ak && !ak.NoDoorWait)
                {
                    if (doorBlocked == null)
                    {
                        var pos = new List<(float x, float y)>(); foreach (var x in o.enemies) pos.Add((x.pos.x, x.pos.z));
                        doorBlocked = AIBody.DoorBlockedNote(pos, o.health);
                    }
                    if (doorBlocked != "")
                        lEnemy.Add(new AICandidate { kind = "attacknow", key = "attacknow:" + e.entity.Index, entity = e.entity, tile = et,
                            desc = $"지금 치기(문 열린 채) — 적 {e.id} {e.dist:F1}칸, 적 체력 {e.health}/{e.maxHealth}, 내 체력 {o.health}/{o.maxHealth}. {doorBlocked} 지금 하는 공격은 닫기를 먼저 마치려고 치지 않고 기다리는 중. 이것을 고르면 문 지나기를 멈추고 닿는 대로 바로 침; 문은 열린 채 남음(열린 문은 적도 지나감; 나중에 문 닫기 후보로 닫을 수 있음).{(er.touch ? " 사이에 막힌 것 없음." : " 지금은 사이가 막혀 있어 닿으면 침.")}" });
                }
                if (!open)
                {
                    // a tunnel already running toward this enemy is the "continue" option (the enemy's tile changes as it
                    // moves, which made a new dig key every call)
                    if (cur is TunnelSkill && curDig.HasValue && math.cmax(math.abs(curDig.Value - et)) <= 4) continue;
                    var plan = PlanC(server, player, et, out string why, true);   // 검수 H1: the detour check (the enemy dig offers the edge way)
                    if (plan != null && plan.walls.Count > 0)
                    {
                        // 단계 3: digging toward it through the base's own edge opens the edge (a fact, not left out)
                        var edgeW = AIPlaceGoal.DigEdge(plan);   // recheck R5 (AIEdge.OpensEdge); 제작대-놓기 실게임 D1: one sentence with the goto / place walk
                        lDig.Add(new AICandidate { kind = "dig", key = $"dig:{et.x},{et.y}", tile = et, entity = e.entity, digOpenEnough = true, targetLabel = $"적 {e.id}",
                            digEdgeOk = edgeW.ConvertAll(q => new int2(q.t.x, q.t.y)),   // 실게임 D1 추가: a replan onto edge walls not stated here stops
                            desc = $"파 들어가기 — 적 {e.id} ({et.x},{et.y}) 쪽으로 벽 {plan.walls.Count}개({plan.WallSummary()}) 뚫어 길 열기, 추정 타격 {plan.totalHits}회. 길이 열리면 끝(공격은 따로).{AIPlaceGoal.EdgeText(plan, edgeW)}{AIPlaceGoal.DetourNote(plan)}" });
                        var altE = AIPlaceGoal.DetourOption(plan, et, $"적 {e.id}", $"파 들어가기 — 적 {e.id} ({et.x},{et.y}) 쪽", "길이 열리면 끝(공격은 따로).", e.entity, true);   // 우회 사실 (assembler: last, never takes a place)
                        if (altE != null) lDig.Add(altE);
                    }
                }
            }
        }

        public void CombatOptions()
        {
            // combat options (v2 §4): keep distance from the nearest enemy, retreat to the core spawn, eat a healing
            // item. Offered whenever an enemy is in view (or health is not full, for eating); the model chooses. The running
            // one is offered only as "continue" (no second copy under its own name), and each says what it is good for so
            // the short step-away and the leave-the-fight retreat are not two names for the same move.
            inCombat = AIEdge.ObserveAnyThreat(server, player, o.enemies, Settings().combatRange);   // 단계 3: only enemies that can come (or can hit now)
            // one keep-away option for the whole fight (away from every enemy in view), whichever enemy is nearest
            if (o.enemies.Count > 0 && o.enemies[0].dist < 4.5f)
            {
                var e = o.enemies[0];
                string k = KeepAwaySkill.NearKey;
                int close = o.enemies.FindAll(x => x.dist < 5f).Count;
                string apred = "";
                if (k != curKey && KeepAwaySkill.PickTile(server, player, 5f, out int2 to, out float gain))
                    lCombat.Add(new AICandidate { kind = "away", key = k, tile = to,
                        desc = brute != null ? $"거리 벌리기(짧게, 싸움터에 남음) — 3초 동안 보이는 적 모두에게서 5칸 이상 떨어져 있기: 먼저 열린 칸 ({to.x},{to.y})로 감(가장 가까운 적 {e.id} {e.dist:F1}칸; 약 {gain:F1}칸 더 멀어짐), 적이 다시 다가오면 다른 열린 칸으로. 그동안 공격 안 함. 3초가 지나거나 {Settings().combatRange:F0}칸 안에 적이 없어지거나 더 멀어질 칸이 없으면 끝(언제든 다른 행동으로 바꿀 수 있음).{Toward(o, to)} {BruteFacts(new V2(to.x, to.y), -1f, "첫 칸까지", out apred)}" : $"거리 벌리기(짧게, 싸움터에 남음) — 3초 동안 보이는 적 모두에게서 5칸 이상 떨어져 있기: 먼저 열린 칸 ({to.x},{to.y})로 감(지금 가장 가까운 적 {e.id} {e.dist:F1}칸{(close > 1 ? $", 5칸 안 적 {close}마리" : "")}; 가장 가까운 적이 약 {gain:F1}칸 더 멀어짐), 적이 다시 다가오면 다른 열린 칸으로. 그동안 공격 안 함. 3초가 지나거나 {Settings().combatRange:F0}칸 안에 적이 없어지거나 더 멀어질 칸이 없으면 끝 — 잠깐 틈을 만들어 다시 싸우거나 먹을 때(언제든 다른 행동으로 바꿀 수 있음). {AIVitals.AwayNote(e.id)} {TakenNote(e.id)}{Toward(o, to)}" });
                if (apred != "" && lCombat.Count > 0 && lCombat[lCombat.Count - 1].kind == "away") lCombat[lCombat.Count - 1].pred = apred;
            }
            if (o.enemies.Count > 0)
            {
                // 코어-안전지대 단계 3: the retreat point is a walkable tile — with a base, inside it (through an own door when
                // the AI is outside); without, the tile just off the spawn pad nearest the AI. The old point (0,-1) is a pad
                // tile the game's path search never reaches ("열린 길 없음", 단계 1 M4e).
                int2 spawn = AIEdge.RetreatPoint(server, player, out string rlabel, out EnemyReach rhow);
                bool baseIn = AIEdge.PlanActive && AIEdge.IsInside(spawn);
                bool meIn = baseIn && AIEdge.IsInside(me);
                string k = $"retreat:{spawn.x},{spawn.y}";
                if ((math.distance(o.pos.xz, (float2)spawn) > 4f || baseIn && !meIn) && k != curKey)
                {
                    var rp = PlanC(server, player, spawn, out string rwhy);
                    bool openWay = rp != null && rp.walls.Count == 0;
                    bool doorWay = !openWay && baseIn && !meIn && rhow.kind == ReachKind.Door;
                    string where = baseIn ? (meIn ? $"기지 안쪽 코어 쪽 ({spawn.x},{spawn.y})" : $"기지 안으로 돌아가기 — 안쪽 칸 ({spawn.x},{spawn.y})") : $"{rlabel} ({spawn.x},{spawn.y})";
                    if (openWay || doorWay)
                        lCombat.Add(new AICandidate { kind = "retreat", key = k, tile = spawn, targetLabel = rlabel, retreatInside = baseIn, progress = AIBaseGuard.CountsAsGoalProgress("retreat"),
                            desc = $"물러나기(싸움을 떠남) — {where}까지 {(openWay ? $"열린 아는 길 {rp.path.Count}칸, 예상 약 {rp.path.Count / AIBody.WalkSpeed:F0}초" : $"{AIBaseGuard.ReachText(rhow, false)}, 예상 약 {math.max(1, rhow.steps) / AIBody.WalkSpeed:F0}초")}. 가는 동안 공격·목표 일 안 함. {(baseIn ? "기지 안쪽에 들어간 뒤 " : "")}10칸 안에 보이는 적이 2초 없으면(4칸 이상 움직인 뒤) 또는 도착하면 멈춤 — 이 싸움을 이기기 어려워 벗어날 때. 적이 따라오는지는 모름.{Toward(o, openWay && rp.path.Count > 0 ? rp.path[math.min(5, rp.path.Count - 1)] : spawn)}" });
                    if (openWay && brute != null && rp.path.Count > 0)
                    {
                        var rc = lCombat[lCombat.Count - 1]; int2 r3 = rp.path[math.min(2, rp.path.Count - 1)];
                        rc.desc += " " + BruteFacts(new V2(r3.x, r3.y), -1f, "처음 몇 칸까지", out string rpred); rc.pred = rpred;
                    }
                    else if (!openWay && !doorWay) blocks.Add($"{(baseIn ? "기지 안으로 물러나기" : "코어로 물러나기")} ({spawn.x},{spawn.y}): {(rp == null ? rwhy : $"열린 길 없음(벽 {rp.walls.Count}개)")}{(baseIn ? $"; {AIBaseGuard.ReachText(rhow, false)}" : "")}");
                }
            }
            if (o.health < o.maxHealth)
            {
                foreach (var hi in AIItems.HealingInInventory(server, player))
                {
                    string k = $"eat:{hi.id}";
                    if (k == curKey) continue;
                    lCombat.Add(new AICandidate { kind = "eat", key = k, itemId = hi.id, itemVariation = hi.variation,
                        desc = $"먹기 — {hi.id}({hi.info}), 가방에 {hi.count}개. 손에 들고 먹는 데 약 0.5~1초, 그동안 공격 안 함. 내 체력 {o.health}/{o.maxHealth}.{(AIVitals.AteRecently(hi.id, Time.realtimeSinceStartup, out float ago) && ago < 25f ? $" 같은 것을 {ago:F0}초 전에 먹었음(효과가 아직 이어지는 중일 수 있고, 겹쳐지는지는 모름)." : "")}" });
                    if (brute != null)
                    {
                        var ec = lCombat[lCombat.Count - 1];
                        ec.desc += " " + BruteFacts(new V2(o.pos.x, o.pos.z), bDelay + 1.0f, "먹는 동안 제자리", out string epred); ec.pred = epred;
                    }
                }
            }
            _inCombat = inCombat;
        }

        public void Food()
        {
            // Hunger food / own-chest supplies remain choices with no goal and during combat too.
            lLife = new List<AICandidate>();
            try { lLife = AIFood.Candidates(server, player, o, curKey, lCombat); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: food candidates failed", e); }
            // hungry with no food in the bag (먹거리 자급): visible food sources are offered below (break / pick up)
            foodNeed = false;
            { try { foodNeed = AIFood.NeedsFood(server, player, o); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: food need failed", e); } }
        }

        public void Walls()
        {
            // walls in view        // walls in view: direct mining when a standing tile is reachable; goal ore without a reachable stand -> dig
            var match = new List<(float d, AICandidate c)>();
            var other = new List<(float d, AICandidate c)>();
            digOre = new List<(float d, int2 p, ObjectID what)>();
            // prefetch: the wall the running action is about to break is not an option for "after it"
            int2? ending = null;
            if (prefetch && cur != null && (curKey.StartsWith("mine:") || curKey.StartsWith("dig:")))
            {
                var sp = curKey.Substring(curKey.IndexOf(':') + 1).Split(',');
                if (sp.Length == 2 && int.TryParse(sp[0], out int endX) && int.TryParse(sp[1], out int endY)) ending = new int2(endX, endY);
            }
            foreach (var w in o.wallTiles)
            {
                if (ending.HasValue && math.all(w.p == ending.Value)) continue;
                if (!AIWallData.TryGet(server, w.tileset, out int hp, out int red, out ObjectID obj)) continue;
                ObjectID ore = ObjectID.None;
                if (w.ore) TryOreObject(server, w.oreTileset, out ore);
                float d = math.distance(o.pos.xz, (float2)w.p);
                bool isMatch = gatherOre && (wanted.Contains(obj) || wanted.Contains(ore));
                if (!StandReachable(w.p))
                {
                    digOre.Add((d, w.p, ore != ObjectID.None ? ore : obj));
                    continue;
                }
                string k = $"mine:{w.p.x},{w.p.y}";
                if (k == curKey) continue;
                string tool = est <= red ? $" 도구 부족 추정(피해 {est} ≤ 감소 {red}, {estNote})." : $" 추정 피해 {est} vs 감소 {red}.";
                if (isMatch && est <= red) toolRed = MinRed(toolRed, red);
                if (isMatch && est <= red && !blocks.Exists(b => b.StartsWith("보이는 목표 품목 벽")))
                    blocks.Add($"보이는 목표 품목 벽 {obj}{(ore != ObjectID.None ? "+" + ore : "")} ({w.p.x},{w.p.y}): 도구 부족(추정 피해 {est} ≤ 감소 {red}, {estNote})");
                string risk = AIWallData.RiskText(server, obj);
                var c = new AICandidate { kind = "mine", key = k, tile = w.p, progress = isMatch && est > red,
                    desc = $"캐기 — 벽 {obj} ({w.p.x},{w.p.y}) {d:F1}칸, 벽 체력 {hp}.{tool} 부수면 {obj}{(w.ore ? $" + 광석 {(ore != ObjectID.None ? ore.ToString() : "종류 모름")}" : "")} 이(가) 나올 것으로 봄{(isMatch ? " — 목표 품목" : "")}. 길이 막히면 실패.{risk}{(AIEdge.EdgeWallNote(w.p) != "" ? " " + AIEdge.EdgeWallNote(w.p) + "." : "")}" };
                (isMatch ? match : other).Add((d, c));
            }
            match.Sort((a, b) => a.d.CompareTo(b.d));
            other.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < match.Count; i++) lMine.Add(match[i].c);
            // explore goal: no "mine some other wall" filler (user decision 2026-11-04; nothing to explore ends the goal)
            for (int i = 0; i < other.Count; i++) lOther.Add(other[i].c);   // free time: no filler mining either
        }

        public IEnumerable<int> DigOreSteps()
        {
            digOre.Sort((a, b) => a.d.CompareTo(b.d));
            foreach (var t in digOre)
            {
                string k = $"dig:{t.p.x},{t.p.y}";
                if (k == curKey) continue;
                foreach (var step in PreparePlan(server, player, t.p)) yield return step;
                var plan = PlanC(server, player, t.p, out string why);
                if (plan == null) { blocks.Add($"보이는 목표 광석 ({t.p.x},{t.p.y}): {why}"); if (why.Contains("도구 부족")) toolRed = MinRed(toolRed, AITunnel.LastToolShortRed); continue; }
                lDig.Add(new AICandidate { kind = "dig", key = k, tile = t.p, digWall = true, targetLabel = $"보이는 채집 대상 {t.what}", progress = gatherOre && wanted.Contains(t.what),
                    desc = $"파 들어가기 — 보이는 목표 광석 {t.what} ({t.p.x},{t.p.y}) {t.d:F1}칸. 열린 길이 없어 벽 {plan.walls.Count}개({plan.WallSummary()})를 뚫으며 감, 추정 타격 {plan.totalHits}회({plan.damageNote}){(plan.toolShortSeen > 0 ? $", 도구 부족 벽 {plan.toolShortSeen}개는 피함" : "")}. 광석 벽을 부수면 끝." });
            }
            yield return 0;
        }

        public IEnumerable<int> BreakablesSteps()
        {
            // breakable objects that may give a wanted material: learned drops (BreakSkill.Drops) include it, or (not yet
            // learned) a wooden object while wood is wanted, or a visible food source (ripe plant, food growing on the ground,
            // AIFoodSource) that gives the wanted item or, while hungry with no food in the bag, any hunger food
            if (true)
            {
                var em0 = server.EntityManager;
                var brk = new List<(float d, Entity e, ObjectID id, float3 p, int hp, string drop, bool prog, string why)>();
                using (var q = em0.CreateEntityQuery(ComponentType.ReadOnly<ObjectDataCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>()))
                using (var ents = q.ToEntityArray(Unity.Collections.Allocator.Temp))
                {
                    foreach (var e in ents)
                    {
                        if (e == player || em0.HasComponent<PlayerGhost>(e) || em0.HasComponent<EnemyCD>(e) || em0.HasComponent<TileCD>(e) || em0.HasComponent<PickUpItemCD>(e)) continue;
                        // Earlier no-damage observations remain available for comparison.
                        var id = em0.GetComponentData<ObjectDataCD>(e).objectID;
                        bool learned = AIDrops.Known((int)id);
                        string drop = learned ? AIDrops.Text((int)id) : "";
                        // only things the game marks as destructible (crates etc.) or roots — never a placed station
                        // (a WoodenWorkBench was offered as a "wooden object" once, ai2 run craft18)
                        if (em0.HasBuffer<CanCraftObjectsBuffer>(e)) continue;
                        bool woody = (id.ToString().StartsWith("Wooden") && em0.HasComponent<DestructibleObjectCD>(e)) || em0.HasComponent<RootCD>(e);
                        var fs = AIFoodSource.Of(server, e, id);
                        if (fs.plant && AIFarm.ForeignCrop(server, e)) continue;   // someone's crop on dug-up ground (own plots only)
                        // an own crop still growing is never broken (a dirt goal broke two own HeartBerryPlants through a
                        // polluted learned drop, ai2 2026-11-04 E7); ripe ones are the field's harvest
                        if ((fs.plant || em0.HasComponent<GrowingCD>(e)) && !fs.ripe && AIFarm.IsOwnPlot(AIBody.TileOf(em0.GetComponentData<LocalTransform>(e).Position))) continue;
                        // A wild unripe plant remains an option; the missing harvest is a fact, not a goal-dependent exclusion.
                        bool forGoal = !(fs.plant && !fs.ripe) && gatherOre && (learned ? wanted.Exists(w => AIDrops.Gave((int)id, w)) : (wanted.Contains(ObjectID.Wood) && woody));
                        bool foodGoal = !(fs.plant && !fs.ripe) && fs.food != ObjectID.None && wanted.Contains(fs.food);
                        bool foodHunger = fs.food != ObjectID.None && foodNeed;
                        if (!forGoal && !foodGoal && !foodHunger && !learned && !woody && !fs.plant && fs.food == ObjectID.None && !em0.HasComponent<DestructibleObjectCD>(e)) continue;
                        float3 p = em0.GetComponentData<LocalTransform>(e).Position;
                        if (!AIVision.CanSee(o.pos, p.xz)) continue;
                        int hp = em0.GetComponentData<HealthCD>(e).health;
                        if (hp <= 0) continue;
                        string nd = AIMemory.NoDamageAt(id, p, out long ndWhen) ? $" 이 자리의 {id}는 {AIMemory.Ago(ndWhen)}에 5초 휘둘러도 체력이 줄지 않았음(그때 기준)." : "";
                        string dtext = fs.plant && !fs.ripe ? "아직 자라는 야생 식물: 지금 부수면 수확물은 나오지 않음" : fs.food != ObjectID.None ? fs.how + (learned ? $"; {drop}" : "")
                            : learned ? drop : "아직 부숴 본 적 없음(나무 물체·뿌리로 보여 나무가 나올 수 있음)";
                        string why = forGoal || foodGoal ? $"필요한 재료: {string.Join(", ", wanted)}." : foodNeed ? $"배고픔 {o.hunger}/100({AIFood.Meaning(o.hunger)})인데 가방에 음식이 없음 — 목표 일은 아님." : "목표와 별개로 부술 수 있는 관측 물체.";
                        brk.Add((math.distance(o.pos.xz, p.xz), e, id, p, hp, dtext + nd, forGoal || foodGoal, why));
                    }
                }
                brk.Sort((a, b) => a.d.CompareTo(b.d));
                for (int i = 0; i < brk.Count; i++)
                {
                    var b = brk[i];
                    if ("break:" + b.e.Index == curKey) continue;
                    int2 bt = AIBody.TileOf(b.p);
                    bool bOpen = Reachable(bt) || StandReachable(bt);
                    if (!bOpen)
                    {
                        // walled in (seen, but no open path): dig a way to it, like the walled-in enemy and ore cases
                        // the object's own tile is blocked by it: dig to the side tile with the fewest walls
                        TunnelPlan plan = null; int2 side = bt;
                        foreach (var dlt in new[] { new int2(0, 1), new int2(0, -1), new int2(1, 0), new int2(-1, 0) })
                        {
                            foreach (var step in PreparePlan(server, player, bt + dlt)) yield return step;
                            var pl = PlanC(server, player, bt + dlt, out string pwhy);
                            if (pl != null && pl.walls.Count > 0 && (plan == null || pl.walls.Count < plan.walls.Count)) { plan = pl; side = bt + dlt; }
                        }
                        if (plan != null && !lDig.Exists(x => x.key == $"dig:{side.x},{side.y}"))
                        {
                            lDig.Add(new AICandidate { kind = "dig", key = $"dig:{side.x},{side.y}", tile = side, digWall = new AITileReader(server).HasWall(side, out _, out _, out _), digOpenEnough = true, targetLabel = $"{b.id}", progress = b.prog,
                                desc = $"파 들어가기 — 부술 물체 {b.id} ({bt.x},{bt.y}) {b.d:F1}칸 옆 ({side.x},{side.y})까지 벽 {plan.walls.Count}개({plan.WallSummary()}) 뚫어 길 열기, 추정 타격 {plan.totalHits}회. 길이 열리면 끝(부수기는 따로). 나오는 것: {b.drop}.{(b.prog ? "" : " " + b.why)}" });
                        }
                        continue;
                    }
                    lMine.Add(new AICandidate { kind = "break", key = "break:" + b.e.Index, entity = b.e, tile = bt, progress = b.prog,
                        desc = $"부수기 — {b.id} ({b.p.x:F0},{b.p.z:F0}) {b.d:F1}칸, 체력 {b.hp}. 나오는 것: {b.drop}. {b.why}{(AIEdge.EdgeTileNote(bt, b.id.ToString()) is string bn && bn != "" ? " " + bn + "." : "")}" });   // live-run fix 7
                }
            }
            yield return 0;
        }

        public void Roots()
        {
            // big roots in view (bigRoot tile layer) while wood is wanted: every reachable observed root
            if (true)
            {
                var trr = new AITileReader(server);
                var roots = new List<(float d, int2 t)>();
                int2 c0 = AIBody.TileOf(o.pos);
                for (int dx = -(int)math.ceil(o.half.x); dx <= (int)math.ceil(o.half.x); dx++) for (int dy = -(int)math.ceil(o.half.y); dy <= (int)math.ceil(o.half.y); dy++)
                {
                    int2 t = c0 + new int2(dx, dy);
                    if (!AIVision.CanSee(o.pos, (float2)t) || !RootSkill.IsRoot(trr, t)) continue;
                    roots.Add((math.distance(o.pos.xz, (float2)t), t));
                }
                roots.Sort((a, b) => a.d.CompareTo(b.d));
                foreach (var r in roots)
                {
                    string k = $"root:{r.t.x},{r.t.y}";
                    if (k == curKey || !(Reachable(r.t) || StandReachable(r.t))) continue;
                    string drop = AIDrops.Known(AIDrops.RootSource) ? AIDrops.Text(AIDrops.RootSource) : "아직 캐 본 적 없음(뿌리라 나무가 나올 수 있음)";
                    lMine.Add(new AICandidate { kind = "root", key = k, tile = r.t, progress = gatherOre && wanted.Contains(ObjectID.Wood),
                        desc = $"뿌리 캐기 — 큰 뿌리 ({r.t.x},{r.t.y}) {r.d:F1}칸, 곡괭이로 캠. 나오는 것: {drop}. 필요한 재료: {string.Join(", ", wanted)}.{(AIEdge.EdgeTileNote(r.t, "큰 뿌리") is string rn && rn != "" ? " " + rn + "." : "")}" });   // live-run fix 7: an edge root
                }
            }
        }

        public IEnumerable<int> RememberedOresSteps()
        {
            // Every ore in this character's own memory remains a choice; goal matching only marks progress.
            {
                var rememberTried = new List<int2>();
                foreach (var r in AIMemory.RememberedOres(o.pos, ObjectID.None, 0))
                {

                    rememberTried.Add(r.p);
                    string k = $"dig:{r.p.x},{r.p.y}";
                    if (k == curKey) continue;
                    foreach (var step in PreparePlan(server, player, r.p)) yield return step;
                    var plan = PlanC(server, player, r.p, out string why);
                    if (plan == null) { blocks.Add($"기억한 광석 ({r.p.x},{r.p.y}): {why}"); if (gatherOre && wanted.Contains(r.o.ore) && why.Contains("도구 부족")) toolRed = MinRed(toolRed, AITunnel.LastToolShortRed); continue; }
                    lRemember.Add(new AICandidate { kind = "dig", key = k, tile = r.p, digWall = true, targetLabel = $"기억한 광석 {r.o.ore}", progress = gatherOre && wanted.Contains(r.o.ore),
                        desc = $"기억한 광석 쪽으로 — {r.o.ore} ({r.p.x},{r.p.y}) {math.distance(o.pos.xz, (float2)r.p):F1}칸, {AIMemory.Ago(r.o.seenUnix)}에 봄(지금은 화면 밖이라 지금 상태는 모름). 길이 있으면 걷고 막히면 벽 {plan.walls.Count}개({plan.WallSummary()})를 뚫으며 감, 추정 타격 {plan.totalHits}회." });
                }
            }
            yield return 0;
        }

        public IEnumerable<int> ItemsSteps()
        {
            // Keep every observed executable pickup, including distinct walled-in targets.
            foodPicks = new HashSet<AICandidate>();   // hunger food on the ground while the bag has none (free time offers these pickups only)
            for (int i = 0; i < o.items.Count; i++)
            {
                var it = o.items[i];
                int2 t = AIBody.TileOf(it.pos);
                bool isMatch = gatherOre && wanted.Contains(it.id);
                // the bag cannot take it (no empty slot, no stack of it): the body cannot pick it up — not offered, said once
                if (!AIBody.BagHasRoomFor(server, player, it.id))
                {
                    if (!blocks.Exists(b => b.StartsWith("떨어진 물건 줍기"))) blocks.Add($"떨어진 물건 줍기: 가방(손 칸 포함)에 빈 칸이 없고 쌓을 같은 물건도 없어 주울 수 없음({it.id} 등, 가방을 비우면 됨)");
                    continue;
                }
                if (Reachable(t) || it.dist < 1.5f)
                {
                    string k = $"goto:{t.x},{t.y}";
                    if (k == curKey) continue;
                    // what a player sees about it: flying into Claude already, or lying within the game's auto-pickup
                    // radius (it comes in by itself), or farther (walk there); the model chooses
                    float pr = o.pickupDistance > 0.1f ? o.pickupDistance : 1.4f;
                    string pickFact = it.pull == 1 ? $" 지금 {AIAgent.Cur.Name} 쪽으로 날아오는 중(게임 자동 줍기가 시작됨) — 아무것도 안 해도 곧 가방에 들어옴. 이것을 고르면 그 사이 다른 일을 못 하고, 도착할 때쯤 이미 없어 판단을 한 번 더 함."
                        : it.pull == 2 ? " 다른 플레이어 쪽으로 날아가는 중(그쪽이 주움)."
                        : it.dist <= pr ? $" 게임 자동 줍기 거리({pr:F1}칸) 안 — 가방에 자리가 있고 가리는 것이 없으면 가만히 있거나 다른 일을 해도 곧 저절로 들어옴(고를 필요가 거의 없음)."
                        : $" 자동 줍기 거리 {pr:F1}칸까지 다가가면 저절로 주움(가방에 자리가 있으면).";
                    bool hungerFood = foodNeed && AIFood.HungerAdd(server, it.id, 0) > 0;
                    var pc = new AICandidate { kind = "pickup", key = k, tile = t, itemId = it.id, itemCount = it.amount, progress = isMatch, comesByItself = it.pull != 0 || it.dist <= pr,
                        desc = $"줍기 — 떨어진 {it.id} x{it.amount} ({it.pos.x:F0},{it.pos.z:F0}) {it.dist:F1}칸{(isMatch ? " — 목표 품목" : "")}{(hungerFood ? $" — 음식(하나에 배고픔 +{AIFood.HungerAdd(server, it.id, 0)}), 지금 배고픔 {o.hunger}에 가방에 음식 없음" : "")}.{pickFact}" };
                    lItem.Add(pc);
                    if (hungerFood) foodPicks.Add(pc);
                }
                else
                {
                    foreach (var step in PreparePlan(server, player, t)) yield return step;
                    var plan = PlanC(server, player, t, out string why);
                    if (plan == null || plan.walls.Count == 0) continue;
                    lDig.Add(new AICandidate { kind = "dig", key = $"dig:{t.x},{t.y}", tile = t, targetLabel = $"아이템 {it.id}", progress = isMatch,
                        desc = $"파 들어가기 — 떨어진 {it.id} x{it.amount} ({t.x},{t.y}) {it.dist:F1}칸, 열린 길 없음. 벽 {plan.walls.Count}개({plan.WallSummary()}) 뚫고 가서 주움, 추정 타격 {plan.totalHits}회{(isMatch ? " — 목표 품목" : "")}." });
                }
            }
            lItem = AIPickupOptions.Select(lItem, c => c.progress, foodPicks.Contains,
                c => c.comesByItself, c => c.key, Doable, free);
            foodPicks.IntersectWith(lItem);
            yield return 0;
        }

        public IEnumerable<int> FollowSteps()
        {
            // follow: the host (follow goal) or a teammate on screen (explore goal: exploring together). One stable key per
            // player ("follow:<entity>"; the walk re-aims as they move, so choosing it again continues it).
            foreach (var p in mates)
            {
                if (p.maxHealth > 0 && p.health <= 0) continue;
                if (p.dist <= 3f) continue;
                string k = "follow:" + p.entity.Index;
                if (k == curKey) continue;
                lFollow.Add(new AICandidate { kind = "follow", key = k, entity = p.entity, tile = AIBody.TileOf(p.pos), targetLabel = p.name, progress = Goal.kind == GoalKind.Follow && !p.isAi,
                    desc = $"{(p.isAi ? "동료" : "호스트")} 따라가기 — {p.role} {p.name} ({p.pos.x:F0},{p.pos.z:F0}) {p.dir}쪽 {p.dist:F1}칸, {p.doing}. 움직이면 따라 방향을 바꾸며 걸어가 2.5칸 안에 들면 끝(최대 30초).{(Goal.kind == GoalKind.Explore ? " 함께 다니면 새로 보는 곳은 그쪽 화면과 겹칠 수 있음." : "")}{Toward(o, AIBody.TileOf(p.pos))}" });
            }
            // free time: the host off this screen — walk toward where the host is (the game's map shows every player)
            if (free && !mates.Exists(p => !p.isAi))
            {
                float3 hp = AIBody.HostPosition(server, player);
                float hd = math.distance(o.pos.xz, hp.xz);
                int2 ht = AIBody.TileOf(hp);
                string k = $"dig:{ht.x},{ht.y}";
                if (math.lengthsq(hp) > 0f && hd > 6f && k != curKey && !(cur is TunnelSkill && curDig.HasValue && math.cmax(math.abs(curDig.Value - ht)) <= 6))
                {
                    foreach (var step in PreparePlan(server, player, ht)) yield return step;
                    var hplan = PlanC(server, player, ht, out string hwhy);
                    if (hplan != null)
                    {
                        float secs = hplan.EstimatedSeconds;
                        lFollow.Add(new AICandidate { kind = "dig", key = k, tile = ht, digWall = false, digOpenEnough = false, targetLabel = "호스트가 있던 곳", progress = true,
                            desc = $"호스트 쪽으로 가기 — 호스트는 이 화면 밖 {DirName(hp.xz - o.pos.xz)}쪽 {hd:F0}칸(지도에 보이는 자리, ({ht.x},{ht.y})). 아는 길 {hplan.path.Count}칸{(hplan.walls.Count > 0 ? $", 부술 벽 {hplan.walls.Count}개({hplan.WallSummary()})" : ", 부술 벽 0개")}, 예상 약 {secs:F0}초. 그 자리에 닿으면 끝(호스트가 그사이 움직이면 그 자리에 없을 수 있음)." });
                    }
                    else blocks.Add($"호스트 쪽으로 가기 ({ht.x},{ht.y}) {hd:F0}칸: {hwhy}");
                }
            }
            yield return 0;
        }

        public void Help()
        {
            // help a teammate: an enemy next to a visible teammate that is not among the three nearest attack options
            if (Goal.kind != GoalKind.None || _sd)
            {
                foreach (var p in mates)
                {
                    if (p.maxHealth > 0 && p.health <= 0) continue;
                    SeenEnemy ne = null; float nd = 3f;
                    foreach (var en in o.enemies) { float dd = math.distance(en.pos.xz, p.pos.xz); if (dd <= nd) { nd = dd; ne = en; } }
                    if (ne == null || near3.Contains(ne) || "attack:" + ne.entity.Index == curKey) continue;
                    lEnemy.Add(new AICandidate { kind = "attack", key = "attack:" + ne.entity.Index, entity = ne.entity, tile = AIEdge.EnemyTile(server, ne.pos),   // 검수 F8
                        desc = $"동료 돕기(공격) — {p.role} {p.name}({p.dist:F1}칸, 체력 바 {p.health}/{p.maxHealth}) 옆 {nd:F1}칸의 적 {ne.id}, 나와 {ne.dist:F1}칸, 적 체력 {ne.health}/{ne.maxHealth}. {weaponNote} {FightNote(weapon, ne, o.health)} 처치·대상 소실·45초까지 계속." });
                    break;
                }
            }
        }

        public IEnumerable<int> ExploreSteps()
        {
            // explore: walking to reachable open tiles in view AND digging toward unseen area are offered together with
            // comparable facts (path length, walls, estimated hits and seconds, cells that would come into view); the
            // decision model chooses (user decision 2026-11-03: no program rule that hides digging when a walk exists).
            // A gather goal looks for new ground when nothing in view can move it forward now: no mine/root/break or dig
            // toward a goal item that is a goal step, not about to be left out (two failures) and doable by the body.
            // Counting such options made a goal stall with "탐험할 새 구역 없음" although exploring was never looked at
            // (2026-11-04 넷이 함께 플레이 2차 t=746.5: both big roots in view left out after "길 없음" twice; tool-short goal
            // walls in view did the same). Free time explores nearby (in-view walks and screen-edge digs, no far frontier).
            // Hunger-only food options (break a plant / dig to one, not the goal's) carry progress=false, so they do not
            // stand in for the goal here either (먹거리 자급 1).
            // Availability is independent of the current goal. Generic exploration is progress only for Explore.
            wantExplore = true;
            gfacts = new List<string>();
            if (gatherOre)
            {
                int mineN = lMine.FindAll(c => c.kind == "mine").Count, mineOk = lMine.FindAll(c => c.kind == "mine" && c.progress).Count;
                int objN = lMine.FindAll(c => c.kind != "mine" && c.progress).Count;   // goal objects only (not hunger-only food)
                gfacts.Add($"보이는 목표 품목 벽 {mineN}개(닿는 것{(mineN > mineOk ? $", 그중 도구 부족 {mineN - mineOk}개" : "")})");
                if (objN > 0) gfacts.Add($"부수거나 캘 물체·뿌리 {objN}개");
                gfacts.Add($"파 들어갈 수 있는 목표 광석·물체 {lDig.FindAll(c => c.progress).Count}개");
                gfacts.Add($"기억한 목표 광석 {AIMemory.CountOres(wantedFirst)}곳(갈 길이 있는 것 {lRemember.Count}개)");
                int lo = lMine.FindAll(c => c.progress && LeftOut(c.key)).Count + lDig.FindAll(c => c.progress && LeftOut(c.key)).Count + lRemember.FindAll(c => LeftOut(c.key)).Count;
                if (lo > 0) gfacts.Add($"그중 되풀이 실패로 잠시 뺀 것 {lo}개");
                if (!canMine) gfacts.Add($"캐기·파기 불가: {noMine}");
            }
            const float WalkTilesPerSecond = AIMoveEstimate.WalkSpeed, SecondsPerHit = AIMoveEstimate.SecondsPerHit;
            _exploreNoPath = 0;
            var ex = ExploreTargets(reach, o, me);
            int2? curWalk = null;
            if (cur is GotoSkill && curKey.StartsWith("goto:")) { var sp = curKey.Substring(5).Split(','); if (sp.Length == 2 && int.TryParse(sp[0], out int wx) && int.TryParse(sp[1], out int wy)) curWalk = new int2(wx, wy); }
            foreach (var e in ex)
            {
                string k = $"goto:{e.t.x},{e.t.y}";
                if (k == curKey) continue;
                // the best tile per direction moves with the AI, so "walk east" got a new target (and key) on every call and
                // choosing it again restarted the walk: while an explore walk runs, its direction is the "continue" option

                // nor a walk to (nearly) the same tile picked for another direction (goto:68,-102 -> goto:68,-103 -> goto:67,-101,
                // ai2 swarm3): the same move under another key

                // path length from the in-view walk flood (BFS steps), not a tunnel plan per target (~5-10 ms each)
                int len = _reachDist.TryGetValue(e.t, out int bfs) ? bfs : (int)math.ceil(math.distance(o.pos.xz, (float2)e.t));
                lExplore.Add(new AICandidate { kind = "explore", key = k, tile = e.t, progress = Goal.kind == GoalKind.Explore, exploreDirection = e.dir,
                    desc = $"탐험 이동(걷기) — {e.dir}쪽 열린 길 ({e.t.x},{e.t.y}). 길 {len}칸, 부술 벽 0개, 예상 약 {len / WalkTilesPerSecond:F0}초. 거기 서면 새로 보일 구역 {e.novelty}칸(4x4 단위, 탐색 기억 기준).{Toward(o, e.t)}" });
            }
            // Visible wall/tunnel targets are provided individually by Walls/DigOre.
            // Nothing new within one screen (e.g. at the core: the seen area reaches farther than a screen everywhere):
            // offer the nearest remembered frontier per direction by known route (U.explore-core-start, 2026-11-04).
            _exploreFrontierNote = "";
            // A running explore walk is new ground in view (offered as "continue"), so "nothing new within one screen" would be
            // false: Claude flipped between its walk and a remembered frontier 32 tiles away that said so (ai2 swarm1 #6).
            bool walkingToNew = !prefetch && cur is GotoSkill && (cur.Tag ?? "").StartsWith("explore:");   // a prefetch asks what comes after it
            if (wantExplore && !(cur is TunnelSkill) && !walkingToNew)   // free time: nearby only, no far remembered frontier
            {
                float tf = Time.realtimeSinceStartup;
                var t0 = System.Diagnostics.Stopwatch.StartNew();
                // The search took 60-100 ms (~9000 tiles) and ran on every combat call (one frame each, ai2 2026-11-04).
                // Now: fresh result reused (3 s / 2 tiles; in combat 15 s / 10 tiles), otherwise a new search runs as a
                // background job a few hundred tiles per frame (AIBrain.Tick) and this call uses the last result if it is
                // not too old (60 s / 20 tiles: the remembered frontier is far away and changes slowly). Only with no usable
                // result at all does the search run here at once.
                var st = S;
                List<(int2 t, string dir, int novelty, int cost)> fr = null; int expanded = 0;
                bool fight = o.enemies.Exists(x => x.dist <= Settings().combatRange);
                bool have = st._frontier != null;
                float age = tf - st._frontierAt; int moved = have ? math.cmax(math.abs(st._frontierTile - me)) : 999;
                bool fresh = have && age < (fight ? 15f : 3f) && moved <= (fight ? 10 : 2);
                Func<int2, bool> skipF = t => false;
                if (fresh) fr = st._frontier;
                else
                {
                    if (st._fJob == null) { st._fJob = new AITunnel.FrontierJob(server, player, skipF); st._fJobStarted = tf; st._fJobFrames = 0; }
                    if (have && age < 60f && moved <= 20) fr = st._frontier;   // stale but usable while the job runs
                    else
                    {
                        // No usable result (first decision after a new goal or a respawn): the whole search in this frame was
                        // the 70-104 ms prep spike. Now the job runs in the background (~0.5 s) and this decision has no
                        // remembered-frontier option; the goal does not end meanwhile, and the brain asks again when the job
                        // is done (AIBrain.Tick, "탐색 끝 찾기 끝남").
                        st._fPending = true;
                        fr = new List<(int2 t, string dir, int novelty, int cost)>();
                    }
                }
                _msFrontier += t0.Elapsed.TotalMilliseconds;
                int added = 0;
                foreach (var f in fr)
                {
                    string k = $"dig:{f.t.x},{f.t.y}";
                    if (k == curKey) continue;
                    foreach (var step in PreparePlan(server, player, f.t)) yield return step;
                    var plan = PlanC(server, player, f.t, out string why);
                    if (plan == null) { blocks.Add($"{f.dir}쪽 기억한 탐색 끝 ({f.t.x},{f.t.y}): {why}"); continue; }
                    bool isWall = tr.HasWall(f.t, out _, out _, out _);
                    float secs = plan.path.Count / WalkTilesPerSecond + plan.totalHits * SecondsPerHit;
                    lDigExplore.Add(new AICandidate { kind = "dig", key = k, tile = f.t, digWall = isWall, targetLabel = $"{f.dir}쪽 기억한 탐색 끝", progress = Goal.kind == GoalKind.Explore,
                        desc = $"탐험 이동(기억한 탐색 끝으로) — 화면 안에는 새로 보일 곳이 없음. {f.dir}쪽 ({f.t.x},{f.t.y}) {math.distance(o.pos.xz, (float2)f.t):F0}칸, 지금 화면 밖(전에 본 구역의 끝). 아는 길 {plan.path.Count}칸, 부술 벽 {plan.walls.Count}개{(plan.walls.Count > 0 ? $"({plan.WallSummary()})" : "")}, 예상 약 {secs:F0}초. 거기 서면 새로 보일 구역 {f.novelty}칸(4x4 단위, 탐색 기억 기준).{Toward(o, f.t)}" });
                    added++;
                }
                _exploreFrontierNote = $"frontier search: expanded {expanded} tiles in {t0.ElapsedMilliseconds} ms, found {fr.Count}, offered {added}";
                AIPlayerSpikeMod.Log($"BRAIN: nothing new within one screen; {_exploreFrontierNote}");
            }
            _exploreOptions = lExplore.Count + lDigExplore.Count;
            yield return 0;
        }

        // ---- combat stage 2: the close fight with a charging enemy (now: MushroomBrute) within combat range
        // (work/2026-11/2026-11-05-전투-적용-계획.md). Only then: the charge observation, the knowledge line and the short
        // options (one swing / walk into reach / hold / enemy-relative moves of 1 tile in 8 directions and 2-3 tiles), each
        // with the same facts: time to its end, the vulnerability left then and the distance then, and the predicted charge
        // outcome with its assumptions. Options are left out only when the way is blocked or the end point is the enemy's
        // body or another option's end point; never for being dangerous. The program does not dodge by itself.
        void FindBrute()
        {
            brute = null; bv = null; BC._bruteObs = "";
            var e = o.enemies.Find(x => AIChargeWatch.IsCharger(x.id) && x.dist <= Settings().combatRange);
            if (e == null) return;
            bv = AIChargeWatch.View(e.entity, player, out bChargeN);
            if (bv == null) return;
            brute = e;
            float lat = Latencies.Count > 0 ? Pct(0.5f) : 0.27f;
            bDelay = lat + AIChargeMath.StartDelay;
            var trr = tr; var ob = objB; var memo = new Dictionary<int2, bool>();   // the predictions read the same tiles many times
            blockedV = p =>
            {
                var t = new int2((int)math.round(p.x), (int)math.round(p.y));
                if (!memo.TryGetValue(t, out bool bl)) { bl = !trr.Walkable(t) || (ob != null && ob.Contains(t)); memo[t] = bl; }
                return bl;
            };
            // the charge prediction: the same set without tiles held only by critters (fireflies: R3 run, a YellowFirefly in front
            // of the brute made the prediction stop after 0.6 tiles); walking keeps them (blockedV)
            var pl = player; var cmemo = new Dictionary<int2, bool>();
            chargeBlockedV = p =>
            {
                var t = new int2((int)math.round(p.x), (int)math.round(p.y));
                if (!cmemo.TryGetValue(t, out bool bl)) { bl = blockedV(p) && !(trr.Walkable(t) && AIObjects.CritterOnly(pl, t)); cmemo[t] = bl; }
                return bl;
            };
        }

        // Review F2: what a predicted charge runs into at p (the same blocked set: a non-walkable tile or a tile of an object
        // with a collider, or a tile where a walk got stuck), for the sentence and the code.
        AIChargeMath.StopWhat StopAt(V2 p)
        {
            var t = new int2((int)math.round(p.x), (int)math.round(p.y));
            if (objB != null && objB.Contains(t))
                return AIObjects.ObjectAt(player, t, out ObjectID id) ? new AIChargeMath.StopWhat(AIChargeMath.StopKind.Object, id.ToString()) : new AIChargeMath.StopWhat(AIChargeMath.StopKind.Stuck);
            if (tr.HasWall(t, out _, out _, out _)) return new AIChargeMath.StopWhat(AIChargeMath.StopKind.Wall);
            if (!tr.Walkable(t)) return new AIChargeMath.StopWhat(AIChargeMath.StopKind.Tile, tr.Top(t).tileType.ToString());
            return new AIChargeMath.StopWhat(AIChargeMath.StopKind.Unknown);
        }

        // TEST ONLY: a predicted stop within a tile of the brute (S2 ② R2 #1: predicted 0.0-0.1 tiles, it ran 4.7) — what
        // was there, once per charge and tile, to find out what the prediction counts as a stop.
        static readonly HashSet<string> _nearStops = new HashSet<string>();
        void NoteNearStop(ChargePrediction r, AIChargeMath.StopWhat w)
        {
            if (!AITestSwitches.Test || !r.stops || r.stopsAtMe || r.runTiles >= 1f) return;
            var t = new int2((int)math.round(r.stopAt.x), (int)math.round(r.stopAt.y));
            string k = $"{brute.entity.Index}:{bChargeN}:{t.x},{t.y}";
            if (_nearStops.Count > 500) _nearStops.Clear();
            if (!_nearStops.Add(k)) return;
            AIPlayerSpikeMod.Log($"TEST-ONLY charge stop predicted near the brute: e={brute.entity.Index} #{bChargeN} brute ({bv.pos.x:F2},{bv.pos.y:F2}) run {r.runTiles:F2} at ({r.stopAt.x:F2},{r.stopAt.y:F2}) tile ({t.x},{t.y}) what={w.kind}:{w.name ?? "-"} walkable={tr.Walkable(t)} top={tr.Top(t).tileType} object={(objB != null && objB.Contains(t))} critterOnly={AIObjects.CritterOnly(player, t)}");
        }

        static string DirOf(V2 v) => math.abs(v.x) >= math.abs(v.y) ? (v.x >= 0 ? "동" : "서") : (v.y >= 0 ? "북" : "남");

        bool ClearWay(V2 a, V2 b)
        {
            float len = (b - a).Len;
            if (len < 1e-3f) return true;
            for (float s = 0.25f; s < len + 0.13f; s += 0.25f) { var p = a + (b - a) * (math.min(s, len) / len); if (blockedV(p)) return false; }
            return true;
        }

        // "돌진선: … 시간: … 예상: …" for an option that ends at `dest` (seconds < 0: walk time from now incl. the delay).
        string BruteFacts(V2 dest, float seconds, string label, out string code, float delay = -1f, bool chase = false)
        {
            code = "";
            if (brute == null) return "";
            if (delay < 0f) delay = bDelay;
            V2 me = new V2(o.pos.x, o.pos.z), bp = bv.pos;
            if (seconds < 0f) seconds = delay + (dest - me).Len / AIChargeMath.Walk;
            var sb = new StringBuilder();
            if (bv.phase == ChargePhase.Anticipation || bv.phase == ChargePhase.Charging)
            {
                float gNow = AIChargeMath.LineGap(bv, me, out _), gDest = AIChargeMath.LineGap(bv, dest, out bool ahead);
                sb.Append($"돌진선(지금 방향 기준): 지금 선 중심에서 {AIChargeMath.F1(gNow)}칸 → {label} {AIChargeMath.F1(gDest)}칸{(ahead ? "" : "(야수 뒤쪽)")}. ");
            }
            sb.Append(AIChargeMath.TimeText(seconds, bv, (dest - bp).Len, HitOnceSkill.Reach));
            if (bv.phase == ChargePhase.Anticipation || bv.phase == ChargePhase.Charging)
            {
                // an action that follows the enemy (the continuing attack) is predicted along its own path (③ fix)
                var r = chase ? AIChargeMath.PredictChase(bv, me, delay, HitOnceSkill.Reach, chargeBlockedV) : AIChargeMath.Predict(bv, me, dest, delay, chargeBlockedV);
                bool follows = bv.phase == ChargePhase.Anticipation && bv.aimAtMe && AIChargeMath.LockIn(bv) > 0f;
                var what = r.stops && !r.stopsAtMe ? StopAt(r.stopAt) : default;
                NoteNearStop(r, what);
                sb.Append(' ').Append(AIChargeMath.OutcomeText(r, delay, follows, true, chase ? $"공격은 적을 따라 걸어 {HitOnceSkill.Reach:F1}칸 안에서 휘두름" : null, what));
                code = AIChargeMath.Code(r, what);
            }
            else if (bv.phase == ChargePhase.Ready || bv.phase == ChargePhase.Recover)
            {
                V2 d = (dest - bp).Len > 0.3f ? (dest - bp).Norm : (me - bp).Norm;
                float open = AIChargeMath.OpenBehind(dest, d, 8f, chargeBlockedV);
                var what = open > 8f ? default : StopAt(dest + d * open);
                sb.Append(' ').Append(AIChargeMath.NextChargeText(open, 8f, true, what));
                code = open > 8f ? "next:none" : $"next:{AIChargeMath.StopCode(what)}{AIChargeMath.F1(open)}";
            }
            else code = bv.phase == ChargePhase.Vulnerable ? $"vuln{AIChargeMath.F1(bv.vulnLeft - seconds)}" : "other";
            return sb.ToString();
        }

        // Along the straight move me -> dst after `delay`: when the body is first farther than the hit reach from the charge
        // line as it points now (anticipation / charging only).
        string ExitText(V2 me, V2 dst, float delay)
        {
            if (bv.phase != ChargePhase.Anticipation && bv.phase != ChargePhase.Charging) return "";
            float len = (dst - me).Len;
            if (AIChargeMath.LineGap(bv, me, out _) > AIChargeMath.HitReach) return "지금 이미 돌진선(지금 방향 기준) 밖.";
            for (float sDist = 0.05f; sDist <= len + 1e-3f; sDist += 0.05f)
            {
                var p = me + (dst - me) * (sDist / math.max(len, 1e-3f));
                if (AIChargeMath.LineGap(bv, p, out _) > AIChargeMath.HitReach) return $"약 {delay + sDist / AIChargeMath.Walk:F2}초 뒤 지금 돌진선(지금 방향 기준)에서 {AIChargeMath.HitReach:F2}칸 밖으로 나감.";
            }
            return "도착해도 지금 돌진선(지금 방향 기준) 안.";
        }

        string BruteDmgText()
        {
            if (bv.phase != ChargePhase.Vulnerable) return "1(취약 아님: 한 번에 최대 1)";
            if (weapon != ObjectID.None && AIVitals.WeaponData(server, weapon, out int dmg, out _)) return $"약 {dmg}(취약 중: 제한 없음, {weapon} 게임 자료 기본 피해{(_weaponBroken ? ", 부서진 무기라 크게 줄어듦" : "")})";
            return "취약 중이라 제한 없음(무기 피해 모름)";
        }

        string BruteAttackDesc(SeenEnemy e, bool open, string mate, out string code)
        {
            V2 me = new V2(o.pos.x, o.pos.z), bp = new V2(e.pos.x, e.pos.z);
            V2 dest = e.dist <= HitOnceSkill.Reach ? me : bp + (me - bp).Norm * 1.1f;
            float swing = AIMotion.CooldownOf(weapon) > 0f ? AIMotion.CooldownOf(weapon) : 0.4f;
            float first = bDelay + (dest - me).Len / AIChargeMath.Walk + AIMotion.SwingLeft + swing;
            return $"공격(계속) — 적 {e.id} ({e.pos.x:F0},{e.pos.z:F0}) {e.dist:F1}칸, 적 체력 {e.health}/{e.maxHealth}. 내 체력 {o.health}/{o.maxHealth}. {weaponNote} 휘두르는 거리 밖이면 다가가고 안이면 계속 휘두름(돌진이 와도 계속), 처치·대상 소실 또는 직접 중단·전환을 고를 때까지 계속. 한 타 예상 피해: 지금 {BruteDmgText()}.{mate}{(open ? "" : " 지금 보이는 열린 길로는 닿지 않음.")} {BruteFacts(dest, first, e.dist <= HitOnceSkill.Reach ? "제자리(첫 휘두르기 끝까지)" : "다가간 자리(첫 휘두르기 끝까지)", out code, -1f, true)}";
        }

        public void Brute()
        {
            lBrute = new List<AICandidate>();
            lBrute.AddRange(AICombatOptions.Candidates(server, player, o, curKey));
            if (brute == null) return;
            var e = brute;
            V2 me = new V2(o.pos.x, o.pos.z), bp = new V2(e.pos.x, e.pos.z);
            V2 fwd = (bp - me).Norm; if (fwd.Len < 0.5f) fwd = new V2(1f, 0f);
            V2 left = fwd.Left;
            // a running short action keeps its place (as "continue") in the fixed order
            string ck = curKey.Contains(":") ? curKey.Substring(0, curKey.IndexOf(':')) : curKey;
            AICandidate cont = lCont.Count == 1 && AIRunGate.IsShort(ck) ? lCont[0] : null;
            if (cont != null) lCont.Clear();
            bool placed = false;
            void Put(AICandidate c)
            {
                if (cont != null && c.key == curKey) { if (!placed) { lBrute.Add(cont); placed = true; } return; }
                if (c.key == curKey) return;
                lBrute.Add(c);
            }
            string wname = weapon == ObjectID.None ? "맨손" : weapon.ToString();
            if (e.dist <= HitOnceSkill.Reach)
            {
                float swing = AIMotion.CooldownOf(weapon) > 0f ? AIMotion.CooldownOf(weapon) : 0.4f, sl = AIMotion.SwingLeft;
                var c = new AICandidate { kind = "hit", key = "hit:" + e.entity.Index, entity = e.entity, tile = AIBody.TileOf(e.pos), obsAt = o.time };
                c.desc = $"한 번 치기 — 버섯 야수 {e.dist:F1}칸에게 {wname} 휘두르기 1회(맞든 빗나가든 1회로 끝나고 바로 다시 판단). 휘두르기 {swing:F2}초{(sl > 0.01f ? $", 앞 휘두르기가 {sl:F2}초 남아 그 뒤 시작" : "")}. 이번 타격 예상 피해: {BruteDmgText()}. 그동안 제자리. {BruteFacts(me, bDelay + sl + swing, "제자리", out string p)}";
                c.pred = p; Put(c);
            }
            else
            {
                V2 dest = bp - fwd * 1.1f;
                if (ClearWay(me, dest))
                {
                    var c = new AICandidate { kind = "approach", key = "approach:" + e.entity.Index, entity = e.entity, tile = AIBody.TileOf(e.pos), dest = new float2(dest.x, dest.y), obsAt = o.time };
                    c.desc = $"사거리까지 접근 — 버섯 야수 쪽으로 약 {(dest - me).Len:F1}칸 걸어 휘두르는 거리({HitOnceSkill.Reach:F1}칸) 안에 들어가면 끝(치기는 따로 고름). 그동안 공격 안 함. {BruteFacts(dest, -1f, "도착지", out string p)}";
                    c.pred = p; Put(c);
                }
            }
            {
                var c = new AICandidate { kind = "hold", key = "hold", obsAt = o.time };
                c.desc = $"제자리 유지 — 서서 다음 판단까지 기다림(최대 {HoldSkill.Seconds:F0}초). 공격 안 함. {BruteFacts(me, bDelay + 0.3f, "제자리", out string p)}";
                c.pred = p; Put(c);
            }
            // enemy-relative moves: fixed order and keys (left before right, always)
            var dirs = new List<(string k, string name, V2 d, int n)>
            {
                ("toward", "적 쪽(다가가기)", fwd, 1), ("away", "뒤(멀어지기)", fwd * -1f, 1), ("left", "왼쪽", left, 1), ("right", "오른쪽", left * -1f, 1),
                ("frontleft", "왼쪽 앞(대각)", (fwd + left).Norm, 1), ("frontright", "오른쪽 앞(대각)", (fwd - left).Norm, 1),
                ("backleft", "왼쪽 뒤(대각)", (left - fwd).Norm, 1), ("backright", "오른쪽 뒤(대각)", (left * -1f - fwd).Norm, 1),
                ("left", "왼쪽", left, 2), ("right", "오른쪽", left * -1f, 2), ("left", "왼쪽", left, 3), ("right", "오른쪽", left * -1f, 3),
                ("away", "뒤(멀어지기)", fwd * -1f, 2), ("away", "뒤(멀어지기)", fwd * -1f, 3),
                ("backleft", "왼쪽 뒤(대각)", (left - fwd).Norm, 2), ("backright", "오른쪽 뒤(대각)", (left * -1f - fwd).Norm, 2),
            };
            var ends = new List<V2>();
            var stepOk = new Dictionary<string, (V2 dest, string name, int n)>();
            foreach (var d in dirs)
            {
                V2 dest = me + d.d * d.n;
                string key = $"step:{d.k}:{d.n}";
                if ((dest - bp).Len < 0.9f) continue;                       // the enemy's body
                if (!ClearWay(me, dest)) continue;                           // blocked on the way or at the end
                if (ends.Exists(x => (x - dest).Len < 0.01f)) continue;      // the same move as another option (same start, straight, same end)
                ends.Add(dest);
                float secs = bDelay + d.n / AIChargeMath.Walk;
                var c = new AICandidate { kind = "step", key = key, dest = new float2(dest.x, dest.y), tile = new int2((int)math.round(dest.x), (int)math.round(dest.y)), targetLabel = $"적 기준 {d.name} {d.n}칸", entity = e.entity, obsAt = o.time };
                c.desc = $"{(d.n == 1 ? "짧은 이동" : "긴 이동")}: 적 기준 {d.name} {d.n}칸 — ({dest.x:F1},{dest.y:F1})로 감({DirOf(d.d)}쪽). 걷기 약 {d.n / AIChargeMath.Walk:F2}초. 적과 거리 {e.dist:F1}→{(dest - bp).Len:F1}칸(적이 제자리일 때). {BruteFacts(dest, secs, "도착지", out string p)} 도착하거나 막히면 끝나고 바로 다시 판단(가는 중에도 바꿀 수 있음). 그동안 공격 안 함.";
                c.pred = p; Put(c);
                stepOk[key] = (dest, d.name, d.n);
            }
            Combos(e, me, bp, stepOk, Put);
            if (cont != null && !placed) lBrute.Insert(0, cont);
            // an action that ends before this request's answer comes: say so (its "continue" answer would then be dropped and
            // the body stands until the next answer — 50 times in the S2 run). A fact; the model chooses.
            bool endsFirst = false;
            if (cont != null && cur != null)
            {
                float rem = -1f; try { rem = cur.RemainingSeconds(server, player); } catch { }
                float ans = math.max(0.05f, bDelay - AIChargeMath.StartDelay);
                // 2026-11-06 계속하기 경고 문구: an action that ends first gets its own text (warning first, no "이어 하기" or
                // switching cost); the others keep the old remaining-time sentence
                if (AIContinueText.EndsFirst(rem, ans)) { cont.desc = AIContinueText.EndsFirstHead(cSince, cur.Name, curKey, cExplore, cProg, cMotion, rem, ans); endsFirst = true; }
                else if (rem >= 0f) cont.desc += AIContinueText.RemainingNote(rem, ans);
            }
            if (cont != null && cur is StepSkill st)
            {
                V2 dst = new V2(st.Dest.x, st.Dest.y), from = new V2(st.From.x, st.From.y);
                float leftT = (dst - me).Len / AIChargeMath.Walk;
                cont.desc += $" 진행: 지금까지 {(me - from).Len:F1}칸 옴, 남은 {(dst - me).Len:F1}칸(약 {leftT:F2}초).{(endsFirst ? AIContinueText.ForecastLabelEndsFirst : AIContinueText.ForecastLabel)}{ExitText(me, dst, 0f)} {BruteFacts(dst, leftT, "도착지", out string cp, 0f)}";
                cont.pred = cp;
            }
            if (cur is HitOnceSkill && curKey == "hit:" + e.entity.Index && e.dist <= HitOnceSkill.Reach)
            {
                float swing = AIMotion.CooldownOf(weapon) > 0f ? AIMotion.CooldownOf(weapon) : 0.4f, sl = AIMotion.SwingLeft;
                var c = new AICandidate { kind = "hit", key = AIRunGate.HitAgainPrefix + e.entity.Index, entity = e.entity, tile = AIBody.TileOf(e.pos), obsAt = o.time };
                c.desc = $"한 번 더 치기 — 지금 휘두르기(실행 #{cur.Run})가 끝나면 바로 이어 {wname} 휘두르기 1회(새 실행, 끝나면 바로 다시 판단). 지금 휘두르기 남은 약 {sl:F2}초, 다음 휘두르기 {swing:F2}초. 다음 타격 예상 피해: {BruteDmgText()}(지금 기준). 그동안 제자리. {BruteFacts(me, sl + swing, "제자리", out string hp2, 0f)}";
                c.pred = hp2;
                int at = lBrute.IndexOf(cont); lBrute.Insert(at >= 0 ? at + 1 : 0, c);
            }
            BruteObs(me, bp, fwd, left);
        }

        // Expected damage of a swing that lands `t` seconds from now (vulnerability as it is now).
        string DmgAt(float t)
        {
            if (bv.phase == ChargePhase.Vulnerable && bv.vulnLeft > t) return BruteDmgText();
            return bv.phase == ChargePhase.Vulnerable ? "1(그때는 취약이 끝남: 한 번에 최대 1)" : "1(취약 아님: 한 번에 최대 1)";
        }

        // Composite actions (user decision 2026-11-05: "때리고 이동, 이동 후 때리기"): fixed stages, no wait between them, no
        // condition inside. The facts cover the whole order in the same format as a single action: when each stage ends,
        // whether the swing reaches (distance, vulnerability, expected damage), and the move's predicted charge outcome
        // (the trajectory, with the move starting after the swing). Only meaningful ones: hit then the sideways / back
        // moves; walk into reach then hit; a 1-tile move whose end is in reach then hit.
        void Combos(SeenEnemy e, V2 me, V2 bp, Dictionary<string, (V2 dest, string name, int n)> stepOk, Action<AICandidate> put)
        {
            string wname = weapon == ObjectID.None ? "맨손" : weapon.ToString();
            float swing = AIMotion.CooldownOf(weapon) > 0f ? AIMotion.CooldownOf(weapon) : 0.4f, sl = AIMotion.SwingLeft;
            const float land = 0.07f;
            string tail = " 단계 사이에 판단을 기다리지 않고 바로 다음 단계로 감. 순서는 고정(돌진이 와도 스스로 바꾸지 않음), 중간에 다른 선택으로 언제든 바꿀 수 있음. 끝나면 바로 다시 판단.";
            if (e.dist <= HitOnceSkill.Reach)
            {
                foreach (var k in new[] { "step:left:2", "step:right:2", "step:left:3", "step:right:3", "step:away:2" })
                {
                    if (!stepOk.TryGetValue(k, out var sd)) continue;
                    float tSwing = bDelay + sl, tMove = tSwing + AIActions.ComboHitEnd, tEnd = tMove + sd.n / AIChargeMath.Walk;
                    var c = new AICandidate { kind = "combo", combo = "hit-move", key = "combo:hit-" + k.Substring(5), entity = e.entity, dest = new float2(sd.dest.x, sd.dest.y), tile = AIBody.TileOf(e.pos), targetLabel = $"치고 {sd.name} {sd.n}칸", obsAt = o.time };
                    c.desc = $"복합(치고 이동): 1) {wname} 휘두르기 1회 — 휘두르기 시작 약 {tSwing:F2}초, 맞는 때 약 {tSwing + land:F2}초, 적과 {e.dist:F1}칸(휘두르는 거리 안, 적이 제자리일 때), 예상 피해 {DmgAt(tSwing + land)}. 2) 휘두르기가 시작되고 {AIActions.ComboHitEnd:F2}초 뒤 바로 적 기준 {sd.name} {sd.n}칸 ({sd.dest.x:F1},{sd.dest.y:F1})로 감(도착 약 {tEnd:F2}초).{tail} {BruteFacts(sd.dest, tEnd, "2단계 도착지", out string p, tMove)}";
                    c.pred = p; put(c);
                }
            }
            else
            {
                V2 fwd = (bp - me).Norm, dest = bp - fwd * 1.1f;
                if (ClearWay(me, dest))
                {
                    float tArr = bDelay + (dest - me).Len / AIChargeMath.Walk, tEnd = tArr + swing;
                    var c = new AICandidate { kind = "combo", combo = "approach-hit", key = "combo:approach-hit", entity = e.entity, dest = new float2(dest.x, dest.y), tile = AIBody.TileOf(e.pos), targetLabel = "접근 후 치기", obsAt = o.time };
                    c.desc = $"복합(접근 후 치기): 1) 버섯 야수 쪽으로 약 {(dest - me).Len:F1}칸 걸어 휘두르는 거리 안으로(도착 약 {tArr:F2}초). 2) 도착하면 바로 {wname} 휘두르기 1회 — 맞는 때 약 {tArr + land:F2}초, 예상 피해 {DmgAt(tArr + land)}(그때 적이 휘두르는 거리 밖이면 2단계는 못 하고 끝남).{tail} {BruteFacts(dest, tEnd, "1단계 도착지", out string p)}";
                    c.pred = p; put(c);
                }
            }
            foreach (var kv in stepOk)
            {
                if (kv.Value.n != 1 || (kv.Value.dest - bp).Len > HitOnceSkill.Reach - 0.1f) continue;   // only a 1-tile move that ends in reach
                float tArr = bDelay + 1f / AIChargeMath.Walk, tEnd = tArr + swing;
                var sd = kv.Value;
                var c = new AICandidate { kind = "combo", combo = "move-hit", key = "combo:" + kv.Key.Substring(5) + "-hit", entity = e.entity, dest = new float2(sd.dest.x, sd.dest.y), tile = AIBody.TileOf(e.pos), targetLabel = $"{sd.name} 1칸 뒤 치기", obsAt = o.time };
                c.desc = $"복합(이동 후 치기): 1) 적 기준 {sd.name} 1칸 ({sd.dest.x:F1},{sd.dest.y:F1})로 감(도착 약 {tArr:F2}초, 그때 적과 {(sd.dest - bp).Len:F1}칸, 적이 제자리일 때). 2) 도착하면 바로 {wname} 휘두르기 1회 — 맞는 때 약 {tArr + land:F2}초, 예상 피해 {DmgAt(tArr + land)}(그때 적이 휘두르는 거리 밖이면 2단계는 못 하고 끝남).{tail} {BruteFacts(sd.dest, tEnd, "1단계 도착지", out string p)}";
                c.pred = p; put(c);
            }
        }

        void BruteObs(V2 me, V2 bp, V2 fwd, V2 left)
        {
            var e = brute; var v = bv;
            var sb = new StringBuilder();
            string phase;
            switch (v.phase)
            {
                case ChargePhase.Ready: phase = "평소(돌아다님, 돌진 아님)"; break;
                case ChargePhase.Anticipation: phase = $"돌진 준비(전조) {v.phaseElapsed:F2}초째 — 조준 고정까지 {AIChargeMath.LockIn(v):F2}초, 출발까지 {AIChargeMath.GoIn(v):F2}초"; break;
                case ChargePhase.Charging: phase = $"돌진 중 {v.phaseElapsed:F2}초째(약 {AIChargeMath.Speed:F0}칸/초 직선, 방향 고정)"; break;
                case ChargePhase.Vulnerable: phase = "부딪힌 뒤 취약 중"; break;
                case ChargePhase.Recover: phase = $"헛돌진 뒤 회복 중(취약 아님), 남은 약 {v.recoverLeft:F1}초"; break;
                default: phase = "그 밖(돌진 단계 모름)"; break;
            }
            bool active = v.phase == ChargePhase.Anticipation || v.phase == ChargePhase.Charging;
            string aim = !active ? "없음(돌진 중 아님)" : v.aimAtMe ? "나" : v.targetKnown ? "다른 대상" : "모름";
            float gap = AIChargeMath.LineGap(v, me, out bool ahead);
            string line = !active ? "돌진선: 해당 없음" : gap <= AIChargeMath.HitReach && ahead ? $"나는 돌진선 위(선 중심에서 {gap:F1}칸, 맞는 폭 선 양옆 약 {AIChargeMath.HitReach:F2}칸)" : $"나는 돌진선 밖(선 중심에서 {gap:F1}칸{(ahead ? "" : ", 야수 뒤쪽")})";
            var stay = active ? AIChargeMath.Predict(v, me, me, 0f, chargeBlockedV) : default;
            string arr = !active ? "해당 없음" : stay.hitsMe ? $"지금 자리에 그대로 있으면 약 {stay.tHit:F2}초 뒤 나에게 닿음"
                // review F2: on the line but predicted to stop first — say what it stops at and what if it does not
                : stay.shortOfMe && stay.stops ? $"지금 자리에 그대로 있으면 돌진선 위지만 돌진이 약 {AIChargeMath.F1(stay.runTiles)}칸 가서 {AIChargeMath.StopText(StopAt(stay.stopAt))}에 먼저 부딪혀 멈춘다고 봄{(stay.elseTHit >= 0f ? $"(멈추지 않으면 약 {stay.elseTHit:F2}초 뒤 나에게 닿음)" : "")}"
                : "지금 자리에 그대로 있으면 닿지 않음(지금 방향 기준)";
            string vul = v.phase == ChargePhase.Vulnerable ? $"취약 중, 남은 약 {v.vulnLeft:F1}초(피해 제한 없음)" : "아님(한 번에 최대 1 피해)";
            string next = v.phase == ChargePhase.Ready ? (v.cooldownLeft > 0f ? $"약 {v.cooldownLeft:F1}초 뒤부터 가능(대상이 보이면 곧 시작)" : v.cooldownLeft == 0f ? "지금 가능(대상이 보이면 곧 시작)" : "모름")
                : active ? "지금 진행 중" : v.phase == ChargePhase.Vulnerable ? $"약 {v.vulnLeft + AIChargeMath.CooldownAfter:F1}초 뒤부터(취약 끝나고 약 {AIChargeMath.CooldownAfter:F2}초)"
                : v.phase == ChargePhase.Recover ? $"약 {v.recoverLeft + AIChargeMath.CooldownAfter:F1}초 뒤부터" : "모름";
            if (KillIntent) sb.Append("이번 교전 목표: 이 버섯 야수 처치(호스트 지시).\n");   // test switch: /ai arena spawn … kill
            sb.Append($"적 관측(서버 상태, 이 요청을 만들 때): 버섯 야수 MushroomBrute {DirOf(bp - me)}쪽 {e.dist:F1}칸 ({e.pos.x:F1},{e.pos.z:F1}), 체력 {e.health}/{e.maxHealth}. 돌진 단계: {phase}. 겨누는 대상: {aim}. 돌진 방향: {(v.dir.Len > 0.5f ? DirOf(v.dir) + "쪽" : "모름")}. {line}. 돌진 도착 예상: {arr}. 취약: {vul}. 다음 돌진: {next}.\n");
            float swing = AIMotion.CooldownOf(weapon) > 0f ? AIMotion.CooldownOf(weapon) : 0.4f;
            int toDown = (int)math.ceil(o.health / AIChargeMath.Damage);
            sb.Append($"나: 체력 {o.health}/{o.maxHealth}, 무기 {(weapon == ObjectID.None ? "맨손" : weapon.ToString())}(휘두르는 거리 {HitOnceSkill.Reach:F1}칸, 휘두르기 {swing:F2}초, {(AIMotion.SwingLeft > 0.01f ? $"앞 휘두르기 {AIMotion.SwingLeft:F2}초 남음" : "지금 휘두를 수 있음")}), 걷기 약 {AIChargeMath.Walk:F1}칸/초, 지금 동작: {(cur == null ? "서 있음" : AIMotion.AtBoundary ? cur.Name + "(끊기 좋은 때)" : AIMotion.Mid)}. 돌진에 한 번 맞으면 피해 약 {AIChargeMath.Damage:F0}(게임 값) → 지금 체력이면 {toDown}번 맞으면 쓰러짐.\n");
            string Open(V2 d) { float f = AIChargeMath.OpenBehind(me, d, 8f, blockedV); return f > 8f ? "8칸 넘게" : $"{math.max(0f, f - 0.5f):F1}칸"; }
            float back = AIChargeMath.OpenBehind(me, fwd * -1f, 8f, chargeBlockedV);
            sb.Append($"주변(적 기준 방향별, 내 자리에서 막히기 전까지): 왼쪽({DirOf(left)}) {Open(left)}, 오른쪽({DirOf(left * -1f)}) {Open(left * -1f)}, 뒤({DirOf(fwd * -1f)}) {Open(fwd * -1f)}, 왼쪽 뒤 {Open((left - fwd).Norm)}, 오른쪽 뒤 {Open((left * -1f - fwd).Norm)}. 내 뒤(적 반대쪽) 벽: {(back > 8f ? "8칸 넘게 열림" : $"약 {back:F1}칸 다음 벽·물체")}.\n");
            if (cur is StepSkill rs)
            {
                V2 dst = new V2(rs.Dest.x, rs.Dest.y), from = new V2(rs.From.x, rs.From.y);
                sb.Append($"진행 중 이동: {rs.Key}(실행 #{rs.Run}) 지금까지 {(me - from).Len:F1}칸 옴, 남은 {(dst - me).Len:F1}칸(약 {(dst - me).Len / AIChargeMath.Walk:F2}초). 계속하면: {(ExitText(me, dst, 0f) == "" ? "돌진 중 아님." : ExitText(me, dst, 0f))}\n");
            }
            sb.Append($"시간 값은 지금부터 잰다: 이 요청의 답이 오고 몸이 움직이기 시작하기까지 약 {bDelay:F2}초(최근 판단 지연 중앙값 {bDelay - AIChargeMath.StartDelay:F2}초 + 출발 약 {AIChargeMath.StartDelay:F2}초)를 넣었다.\n");
            sb.Append("알려진 사실(버섯 야수, 게임에서 잰 값): 취약 밖에서는 한 번에 1 피해만 받는다. 돌진이 무엇에든(벽·물체·플레이어) 부딪히면 3.6초 동안 취약(피해 제한 없음)하고, 부딪힌 둘레 벽은 부서진다. 마지막으로 피해를 받고 약 12초 동안 더 받지 않으면 최대 체력의 절반을 회복한다(그 뒤 5초마다).\n");
            BC._bruteObs = sb.ToString();
        }

        public IEnumerable<int> AssembleSteps(Action<List<AICandidate>> complete)
        {
            if (asm != null) AsmIn(asm, o, cur, curKey, free, canMine, canFight, noMine, noFight, foodPicks,
                lCont, lFollow, lEnemy, lCombat, lCraft, lLife, lMine, lDig, lRemember, lItem, lExplore, lDigExplore, lOther, lBrute, lDoor, lEdge, inCombat, prefetch);
            // refactor S6b: the final assembly is AICandidateAssembler (Unity-free; replayed offline against the old code's
            // L1 records). The candidates go in as AsmCand by object identity; the lists, desc and value come back here.
            var amap = new Dictionary<AICandidate, AsmCand>();
            var aback = new Dictionary<AsmCand, AICandidate>();
            AsmCand A(AICandidate c) { if (!amap.TryGetValue(c, out var x)) { x = new AsmCand { id = amap.Count + 1, kind = c.kind, key = c.key, desc = c.desc, edgeRole = c.edgeRole, progress = c.progress, comesByItself = c.comesByItself, detour = c.digDetour }; amap[c] = x; aback[x] = c; } return x; }
            List<AsmCand> As(List<AICandidate> l) => l.ConvertAll(A);
            void Back(List<AICandidate> l, List<AsmCand> al) { l.Clear(); foreach (var x in al) l.Add(aback[x]); }
            void Descs() { foreach (var kv in amap) kv.Key.desc = kv.Value.desc; }
            var ain = new AsmInput
            {
                goal = Goal.kind, free = free, closeFight = CloseFight(o), closeFightOff = AITestSwitches.CloseFightOff,
                curCombat = cur is AttackSkill || cur is KeepAwaySkill || cur is RetreatSkill || cur is EatSkill, curKey = curKey,
                near6 = o.enemies.FindAll(x => x.dist <= 6f).Count,
                enemy0 = o.enemies.Count > 0 ? o.enemies[0].id.ToString() : "", enemy0Dist = o.enemies.Count > 0 ? $"{o.enemies[0].dist:F1}" : "",
                lCont = As(lCont), lFollow = As(lFollow), lEnemy = As(lEnemy), lCombat = As(lCombat), lCraft = As(lCraft), lLife = As(lLife),
                lMine = As(lMine), lDig = As(lDig), lRemember = As(lRemember), lItem = As(lItem), lExplore = As(lExplore), lDigExplore = As(lDigExplore), lOther = As(lOther),
                lBrute = As(lBrute), lDoor = As(lDoor), lEdge = As(lEdge), combat = inCombat, breachCall = breachCall,
                foodPicks = new HashSet<AsmCand>(),
            };
            foreach (var c in foodPicks) ain.foodPicks.Add(A(c));
            var alist = AICandidateAssembler.Select(ain, c => AsmNote(asm, asm?.Doable, aback[c], Doable(aback[c])), c => AsmNote(asm, asm?.BodyCan, aback[c], BodyCan(aback[c])),
                out string setLog, out bool closeSet, out int noBody);
            _setLog = setLog; _closeSet = closeSet;
            Back(lCont, ain.lCont); Back(lFollow, ain.lFollow); Back(lEnemy, ain.lEnemy); Back(lCombat, ain.lCombat); Back(lCraft, ain.lCraft); Back(lLife, ain.lLife);
            Back(lMine, ain.lMine); Back(lDig, ain.lDig); Back(lRemember, ain.lRemember); Back(lItem, ain.lItem); Back(lExplore, ain.lExplore); Back(lDigExplore, ain.lDigExplore); Back(lOther, ain.lOther);
            Back(lBrute, ain.lBrute); Back(lDoor, ain.lDoor); Back(lEdge, ain.lEdge);
            Descs();
            var list = alist.ConvertAll(x => aback[x]);
            if (noBody > 0)
            {
                if (!canMine) blocks.Add($"캐기·파기 {noBody}개 뺌: {noMine}");
                else blocks.Add($"공격·부수기 뺌: {noFight}");
            }
            // stall facts of a gather goal: what was looked at (the stall text says only these)
            if (gatherOre)
            {
                gfacts.Add(!wantExplore ? "탐험은 따지지 않음(보이는 곳에 목표 일이 있었음)"
                    : _exploreOptions > 0 ? $"탐험 후보 {_exploreOptions}개"
                    : !canMine && lExplore.Count == 0 ? $"걸어서 갈 새 구역 없음(파서 가는 탐험은 곡괭이·빈 손 칸이 없어 못 함)"
                    : "탐험할 새 구역 없음(걷기·파기·기억한 탐색 끝 모두)");
                S._gatherFacts = string.Join(", ", gfacts);
            }
            else S._gatherFacts = "";

            // recent failures: annotate once, leave out after 2 within the memory window
            float tnow = Time.realtimeSinceStartup;
            if (asm != null) asm.Tnow = tnow;
            var beforeMark = new List<AsmCand>(alist);
            var skipped = AICandidateAssembler.Mark(alist, tnow, _fails, FailMemorySeconds, S._left);
            // 검수 Q2: a left-out dig / mine option whose way opens the base edge keeps that fact in the stall sentence of the
            // gather / craft goal ("되풀이 실패로 뺀 행동: …"), the same words as the options (P2)
            if (AIEdge.PlanActive && skipped.Count > 0)
                foreach (var ac in beforeMark)
                {
                    if (alist.Contains(ac) || (ac.kind != "dig" && ac.kind != "mine")) continue;
                    int si = skipped.FindIndex(s => s.StartsWith(ac.key + "("));
                    if (si < 0) continue;
                    try
                    {
                        var oc = aback[ac];
                        string fact = oc.kind == "dig" ? (oc.digEdgeOk == null ? AIPlaceGoal.EdgeText(PlanC(server, player, oc.tile, out _)).Trim() : "")
                            : AIBaseGuard.MineEdgeFact(AIEdge.WallFact(oc.tile));
                        skipped[si] = AIBaseGuard.WithEdgeFact(skipped[si], fact);
                    }
                    catch (Exception e) { AIPlayerSpikeMod.LogError("brain: edge fact (left out) failed", e); }
                }
            Back(list, alist); Descs();
            _skippedNote = skipped.Count == 0 ? "" : string.Join(", ", skipped);
            if (skipped.Count > 0) AIPlayerSpikeMod.Log($"BRAIN: left out after repeated failures (for {FailMemorySeconds:F0}s): {_skippedNote}");
            _toolShortRed = toolRed;
            string prevBlock = _blockNote; bool prevToolShort = S._blockToolShort;
            blocks.AddRange(AIPlaceSpot.TakeNoSpot(player));   // 코어-안전지대 단계 1 review F3: an item to place with no spot around (why)
            SetBlockNote(blocks.Count == 0 ? "" : string.Join(" / ", blocks), blocks.Exists(AIOutcomes.ToolShortText));
            if (blocks.Count > 0) AIPlayerSpikeMod.Log($"BRAIN: blocked targets: {_blockNote}");
            // The preparation path (WantedItems) reads the block note from the start of this build: when a tool-short
            // block shows up for the first time, build once more so the better pick is a step now, not one call later
            // (otherwise a goal whose only wall is tool-short stalls before preparation is offered).
            if (Goal.kind == GoalKind.Gather && ToolShortRule(_blockNote, S._blockToolShort, "rebuild") && !ToolShortRule(prevBlock, prevToolShort, "rebuild-prev") && !_rebuildingForTool)
            {
                AIPlayerSpikeMod.Log("BRAIN: tool-short block appeared; rebuilding candidates with the better-pick preparation");
                _rebuildingForTool = true;
                if (asm != null) AITestSwitches.Write($"ASM-OUT #{asm.Build} recursed");
                try { foreach (var step in BuildCandidateSteps(server, player, o, prefetch, breachCall, complete)) yield return step; } finally { _rebuildingForTool = false; }
                yield break;
            }

            var wait = new AICandidate { kind = "wait", key = "wait", desc = $"기다리기 — {Settings().waitSeconds:F0}초 제자리에 서 있기(할 만한 것이 없을 때)." };
            var directLife = lCraft.Find(c => c.life != null && c.life.MatchesGoal) ?? lLife.Find(c => c.life != null && c.life.MatchesGoal);
                        // User 2026-11-07: keep all viable choices in and out of combat; old candidate-cap settings no longer cut.
            AICandidateAssembler.Finish(alist, A(wait), AICandidateAssembler.LimitFor(inCombat, Settings().combatMaxCandidates), out var edgeCut);
            _edgeMoreNote = AICandidateAssembler.EdgeCutNote(edgeCut);   // recheck N4: the edge options left out, said in one line
            if (_edgeMoreNote != "") AIPlayerSpikeMod.Log($"BRAIN: {_edgeMoreNote}");
            // 실측결함수정3 N2: while the running action's door step holds its door open, each other option says that
            // choosing it leaves that door open (a prefetch asks what comes after the action: the door is closed by then)
            string leftOpen = prefetch ? "" : AIBody.DoorLeftOpenNote;
            if (leftOpen != "") { foreach (var x in alist) if (AIDoorPass.GetsLeftOpenNote(x.kind, x.key, curKey)) x.desc += leftOpen; Descs(); }
            Back(list, alist);
            foreach (var x in alist) aback[x].value = x.value;
            list = AssembleLifePage(server, player, o, curKey, inCombat, list, directLife);
            foreach (var candidate in list)
                if (candidate.kind == "dig")
                    foreach (var step in PreparePlan(server, player, candidate.tile, candidate.digDetour)) yield return step;
            // 제작대-놓기 실게임2 P2: every dig / mine option whose way opens the base edge says so (the detour-off walks too:
            // ore, explore, remembered station, farm, pot / chest ... — the fallback second pass opened the edge without a
            // word and the decision mined the wall just placed). The plan is the cached one the option was built from.
            if (AIEdge.PlanActive)
                foreach (var c in list)
                {
                    try
                    {
                        if (c.kind == "dig" && c.digEdgeOk == null) c.desc = AIBaseGuard.WithEdgeFact(c.desc, AIPlaceGoal.EdgeText(PlanC(server, player, c.tile, out _)).Trim());
                        else if (c.kind == "mine") c.desc = AIBaseGuard.WithEdgeFact(c.desc, AIBaseGuard.MineEdgeFact(AIEdge.WallFact(c.tile)));
                    }
                    catch (Exception e) { AIPlayerSpikeMod.LogError("brain: edge fact failed", e); }
                    yield return 0;
                }
            // Every surviving route gets the same known hazard fact, including remembered places and detours.
            foreach (var c in list)
                if (c.kind == "dig")
                {
                    try
                    {
                        var plan = PlanC(server, player, c.tile, out _, c.digDetour);
                        c.desc += AIWallRisk.RouteText(plan, c.digDetour, p => p.alt, p => p.RiskText(server));
                    }
                    catch (Exception e) { AIPlayerSpikeMod.LogError("brain: wall risk failed", e); }
                    yield return 0;
                }
            AddCommonControls(list, prefetch, cur);
            if (asm != null) AsmOut(asm, list, noBody);
            complete(list);
            yield return 0;
        }
    }
}
