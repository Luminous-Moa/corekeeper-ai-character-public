using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static partial class AIBrain
{
    // One scheduler budget is shared by all characters in this Unity frame. Each yielded
    // unit includes at most 32 search expansions; a route is never reported as absent
    // merely because its allotted work ended.
    private const double PreparationFrameMs = 3.0;
    private static int preparationFrame = -1;
    private static double preparationFrameUsed;
    private static int activePreparations;
    private static Preparation waitingForBudget;

    private sealed class Preparation : IDisposable
    {
        public readonly World World;
        public readonly Entity Player;
        public readonly string Trigger, Key;
        public readonly bool Prefetch, Emergency, Breach, Combat;
        public readonly string NeedReason;
        public readonly int GoalRevision, ExecutionRevision;
        public readonly float Started;
        public readonly BuildContext Context = new BuildContext();
        public readonly Stopwatch Work = new Stopwatch();
        public AIObservation Observation;
        public readonly HashSet<Entity> Threats = new HashSet<Entity>();
        public List<AICandidate> Candidates;
        public IEnumerator<int> Steps;
        public int Frames;
        public double MaxSliceMs;
        public int SearchCount, CapturedTiles;
        public long Expanded;
        public float LastProgressLogAt;
        public bool Finished;
        public bool PrefetchCompleted;
        internal bool Disposed;
        internal int LastTickFrame;
        public Preparation(World world, Entity player, float now, string trigger, bool prefetch, bool emergency, bool breach, string key)
        {
            activePreparations++;
            World = world; Player = player; Started = now; LastProgressLogAt = now; Trigger = trigger; Key = key;
            Prefetch = prefetch; Emergency = emergency; Breach = breach;
            GoalRevision = Revision; ExecutionRevision = S.ExecutionRevision; Combat = _inCombat; NeedReason = _needReason;
        }
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            if (waitingForBudget == this) waitingForBudget = null;
            activePreparations--;
            Steps?.Dispose();
            Steps = null;
            Context.Plans.Clear();
            Context.PlanToolRed.Clear();
            if (Context.Batch != null)
            {
                SearchCount = Context.Batch.SearchCount;
                CapturedTiles = Context.Batch.CapturedTileCount;
                Expanded = Context.Batch.ExpandedTotal;
            }
            Context.Batch = null;
            Context.Query = null;
        }
    }

    private static void CancelPreparation(string why)
    {
        var job = S.Preparation;
        if (job == null) return;
        S.Preparation = null;
        job.Dispose();
        _needCall = true;
        AIPlayerSpikeMod.Log($"BRAIN: preparation cancelled ({why}), frames {job.Frames}, work {job.Work.Elapsed.TotalMilliseconds:F1}ms");
    }

    private static void StepPreparation(World server, Entity player, float now)
    {
        var job = S.Preparation;
        if (job == null) return;
        job.LastTickFrame = Time.frameCount;
        if (job.World != server || job.Player != player || job.GoalRevision != Revision || job.ExecutionRevision != S.ExecutionRevision)
        { CancelPreparation("world, goal or execution changed"); return; }
        if (preparationFrame != Time.frameCount) { preparationFrame = Time.frameCount; preparationFrameUsed = 0; }
        // An AI denied the previous frame gets the first turn this frame. Skip a
        // departed owner after a frame so a stopped/disconnected AI cannot hold the queue.
        if (waitingForBudget != null && (waitingForBudget.Disposed || Time.frameCount - waitingForBudget.LastTickFrame > 1)) waitingForBudget = null;
        if (waitingForBudget != null && waitingForBudget != job) return;
        if (preparationFrameUsed >= PreparationFrameMs)
        {
            if (waitingForBudget == null) waitingForBudget = job;
            return;
        }
        waitingForBudget = null;
        var slice = Stopwatch.StartNew();
        var previous = _building;
        _building = job.Context;
        job.Work.Start();
        job.Frames++;
        try
        {
            if (job.Steps == null)
            {
                try { AIMemory.Observe(server, player, now, true); }
                catch (Exception e) { AIPlayerSpikeMod.LogError("memory: observe before decision failed", e); }
                job.Observation = AIObservation.Take(server, player, true);
                AIGoals.RefreshGuidance(server, player);   // 검수 F3: the current item/menu relation for this judgment
                foreach (var enemy in job.Observation.enemies)
                    if (enemy.dist <= Settings().combatRange && !AIEdge.LastAssessedCannotCome(enemy.entity)) job.Threats.Add(enemy.entity);
                bool breach = job.Breach || AIEdge.PlanActive && AIEdge.BreachPending;
                job.Steps = PreparationSteps(job, breach).GetEnumerator();
            }
            while (preparationFrameUsed + slice.Elapsed.TotalMilliseconds < PreparationFrameMs && slice.Elapsed.TotalMilliseconds < PreparationFrameMs / Math.Max(1, activePreparations))
            {
                if (job.Steps.MoveNext()) continue;
                // The request still belongs to the original goal/execution. Actual actions
                // retain their existing live target/resource/route checks when selected.
                job.Finished = true;
                S.Preparation = null;
                job.Dispose();
                break;
            }
        }
        catch (PreparationChanged e)
        {
            CancelPreparation(e.Message);
            _retryAt = now;
        }
        catch (Exception e)
        {
            CancelPreparation("preparation failed");
            AIPlayerSpikeMod.LogError("brain: incremental preparation failed", e);
            _retryAt = now + Settings().waitSeconds;
        }
        finally
        {
            job.Work.Stop();
            _building = previous;
            double sliceMs = slice.Elapsed.TotalMilliseconds;
            preparationFrameUsed += sliceMs;
            job.MaxSliceMs = Math.Max(job.MaxSliceMs, sliceMs);
            if (job.Finished || (S.Preparation == job && now - job.LastProgressLogAt >= 2f))
            {
                job.LastProgressLogAt = now;
                if (job.Context.Batch != null)
                {
                    job.SearchCount = job.Context.Batch.SearchCount;
                    job.CapturedTiles = job.Context.Batch.CapturedTileCount;
                    job.Expanded = job.Context.Batch.ExpandedTotal;
                }
                AIPlayerSpikeMod.Log($"PREP {(job.Finished ? "finished" : "progress")}: rev={job.GoalRevision} frames={job.Frames} plans={job.Context._nPlans} cached={job.Context._nPlansCached} work={job.Work.Elapsed.TotalMilliseconds:F1}ms maxSlice={job.MaxSliceMs:F2}ms trees={job.SearchCount} expanded={job.Expanded} tiles={job.CapturedTiles}");
            }
        }
    }

    private static IEnumerable<int> PreparationSteps(Preparation job, bool breach)
    {
        job.Context.Batch = AITunnel.BeginBatch(job.World, job.Player, job.Observation.pos);
        while (!job.Context.Batch.StepCapture(32)) yield return 1;
        if (job.Context.Batch.Changed) throw new PreparationChanged(job.Context.Batch.Reason);
        foreach (var step in BuildCandidateSteps(job.World, job.Player, job.Observation, job.Prefetch, breach, value => job.Candidates = value)) yield return step;
        foreach (var step in CompletePreparationSteps(job.World, job.Player, Time.realtimeSinceStartup, job)) yield return step;
    }

    private sealed class PreparationChanged : Exception
    {
        public PreparationChanged(string reason) : base(reason) { }
    }

    private static IEnumerable<int> ValidatePreparation(Preparation job)
    {
        var batch = job.Context.Batch;
        batch.BeginValidation();
        while (!batch.StepValidation(32)) yield return 1;
        if (batch.Changed) throw new PreparationChanged(batch.Reason);
    }

    // The expensive candidate loops warm their route before consuming it. The second
    // call (description / filtering / risk facts) uses the exact same completed plan.
    private static IEnumerable<int> PreparePlan(World server, Entity player, int2 target, bool detour = false)
    {
        if (_building == null) { PlanC(server, player, target, out _, detour); yield break; }
        long key = ((long)target.x << 32) ^ (uint)target.y;
        var plans = _building.Plans;
        if (plans.TryGetValue(key, out var old) && (!detour || old.seen == 1)) { yield return 0; yield break; }
        var batch = _building.Batch;
        // Small queries keep a local search. Once a build requests many distinct routes,
        // finish the two shared trees cooperatively and reuse them for every later target.
        if (batch != null && !batch.SearchesReady && (_building.Plans.Count >= 8 || _building.RouteCountHint >= 8))
            while (!batch.StepSearches(32)) yield return 1;
        var job = batch == null ? AITunnel.BeginPlan(server, player, target, -1, false, detour)
            : batch.SearchesReady ? batch.BeginPlan(target, -1, false, detour)
            : batch.BeginSinglePlan(target, -1, false, detour);
        int2 from = batch != null ? batch.Start : AIEdge.StandTile(server, AIBody.Pos(server, player));
        var sw = new Stopwatch();
        while (true)
        {
            sw.Start();
            bool done = job.Step(32);
            sw.Stop();
            if (done) break;
            yield return 1;
        }
        if (job.Changed) throw new PreparationChanged(job.Reason);
        plans[key] = (Time.realtimeSinceStartup, from, detour ? 1 : 0, job.Result, job.Reason);
        _building.PlanToolRed[key] = AITunnel.LastToolShortRed;
        _nPlans++;
        _msPlans += sw.Elapsed.TotalMilliseconds;
        yield return 1;
    }

    internal static TunnelPlan PlanC(World server, Entity player, int2 target, out string why, bool detour = false)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float now = Time.realtimeSinceStartup;
        int2 me = AIEdge.StandTile(server, AIBody.Pos(server, player));   // 재실측 결함 2
        long k = ((long)target.x << 32) ^ (uint)target.y;
        var plans = _building != null ? _building.Plans : S._plans;
        TunnelPlan plan;
        // tolerance grows with the route (a 90-tile retreat route is the same route one or two steps later).
        // Out of a fight a plan is kept 5 s (2 s in a fight): with four AIs the peaceful decisions every ~3 s missed the
        // 2 s cache and recomputed 8-10 plans (~16 ms each), 200-250 ms in one frame (2026-11-04 넷이 함께 플레이, Claude
        // gathering TinOre). The walk/dig skill plans its route again when it starts, so a 5 s old count only describes.
        // Chest, base and remembered-station routes (AIBase, craft) also come through here now (they were uncached).
        // A plan that found no way (tool-short walls all round, unseen tiles) searched every known tile in its box
        // (up to 120000 steps, 20-30 ms) and was kept only while the AI stayed within 2 tiles: in a fight Claude moved,
        // and two remembered TinOre spots behind temple walls were searched again on every 0.3 s call (10 plans,
        // 110-190 ms per decision, 2026-11-04 넷이 함께 플레이 2차 t=429-930). "No way" does not change by moving a few
        // tiles: kept 10 s and within an eighth of the distance to the target (at least 6 tiles).
        bool okC = plans.TryGetValue(k, out var c) && (!detour || c.seen == 1);
        if (_building != null && okC)
        {
            _nPlansCached++;
            AITunnel.LastToolShortRed = _building.PlanToolRed.TryGetValue(k, out int red) ? red : c.plan?.toolShortRed ?? 0;
            why = c.why;
            return c.plan;
        }
        if (okC && c.plan == null && now - c.at < 10f && math.cmax(math.abs(c.from - me)) <= math.max(6, (int)(math.distance((float2)me, (float2)target) / 8f)))
        {
            plan = null; why = c.why; _nPlansCached++;
        }
        else if (okC && now - c.at < (_inCombat ? 2f : 5f) &&math.cmax(math.abs(c.from - me)) <= math.max(2, (c.plan != null ? c.plan.path.Count : 0) / 10))
        {
            plan = c.plan; why = c.why; _nPlansCached++;
        }
        else
        {
            if (_building?.Batch != null)
            {
                var batchJob = _building.Batch.SearchesReady
                    ? _building.Batch.BeginPlan(target, -1, false, detour)
                    : _building.Batch.BeginSinglePlan(target, -1, false, detour);
                batchJob.RunUntilDone();
                if (batchJob.Changed) throw new PreparationChanged(batchJob.Reason);
                plan = batchJob.Result;
                why = batchJob.Reason;
            }
            else plan = AITunnel.Plan(server, player, target, out why, -1, false, detour);
            if (_building == null) PrunePlans(plans);
            plans[k] = (now, me, detour ? 1 : 0, plan, why);
            if (_building != null) _building.PlanToolRed[k] = AITunnel.LastToolShortRed;
            _nPlans++;
        }
        _msPlans += sw.Elapsed.TotalMilliseconds;
        return plan;
    }

    // Long-lived hints use bounded oldest-entry eviction rather than flushing every
    // useful plan at once. Build-local results have the lifetime of that preparation.
    private static void PrunePlans(Dictionary<long, (float at, int2 from, int seen, TunnelPlan plan, string why)> plans)
    {
        if (plans.Count < 256) return;
        long oldest = 0; float at = float.MaxValue;
        foreach (var pair in plans) if (pair.Value.at < at) { at = pair.Value.at; oldest = pair.Key; }
        plans.Remove(oldest);
    }
}
