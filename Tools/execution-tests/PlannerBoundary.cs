using System;
using Unity.Entities;
using UnityEngine.Networking;
// Real planner continuation, request scheduling and HTTP construction are extracted in run.ps1.
// Only game feasibility/report serialization and transport are offline boundaries.
public static partial class AIPlanner
{
    private static State S => Cur;
    private static UnityWebRequest _req;
    public static int Dropped;   // AIPlanner.DropRequestForHold (ae09acf /ai hold)
    private static string _pendingTrigger { get => S.Inbox.HasPending ? S.Inbox.Pending.Kind : null; set { if (value == null) S.Inbox.Clear(); else S.Inbox.Put(new PlanTrigger(value)); } }
    private static string _lastGoalText, _lastWhy, _lastSource, _reqTrigger, _rejected;
    private static float _retryAt, _reqStart;
    private static int _reqReport, _reqRev, _reqDowns, Calls;
    private static bool Auto;
    private static bool Watching => AIPlanRules.Watching(Auto,AIGoals.Goal.IsReal,S._planNext,false);
    private const string BridgeUrl = "offline://planner";
    private static readonly System.Collections.Generic.List<string> _events = new System.Collections.Generic.List<string>();
    private static class Chat { public static int LastSeq; }
    private static string Trim(string text, int n) => text == null || text.Length <= n ? text : text.Substring(0,n);
    private static string Esc(string text) { var json = System.Text.Json.JsonSerializer.Serialize(text ?? ""); return json.Substring(1,json.Length-2); }
    private static void OnOrderChanged(HostOrder order, string was, string why) { }
    private static void OwedSettle(string decision, bool talkOnly, float now) { }
    private static string CheckGoal(World w, Entity p, string d, string item, int count, string where, int keep, int slot, string mode, string second, int maxCount = -1, int maxOrder = -1) => null;
    private static string ReportJson(World w, Entity p, string trigger, out int id)
    {
        id = Calls + 1; if (trigger == "action_replan") Replans++; else EndReports++;
        AIExpedition.Sent(id); Harness.Trace.Add("report " + trigger + " " + AIExpedition.Report());
        return "{" + ReportFields() + "}";
    }
    public static bool Pending => _pendingTrigger != null;
    public static void CompleteTransport() { _req = null; }
    public static void ResetTransport() { _req = null; _pendingTrigger = null; _retryAt = 0; _rejected = null; Calls = 0; _events.Clear(); LastRequestBody = null; }
    public static string LastRequestBody;
}
public static partial class AIBrain
{
    public static int GoalRevision => AIGoals.GoalRevision;
    public static bool TryItemPublic(string s, out ObjectID id, out string name) => AIGoals.TryItemPublic(s,out id,out name);
}
namespace UnityEngine.Networking
{
    public class UploadHandlerRaw { public readonly byte[] Bytes; public UploadHandlerRaw(byte[] bytes) { Bytes=bytes; } }
    public class DownloadHandlerBuffer { }
    public class UnityWebRequest
    {
        public object uploadHandler, downloadHandler; public int timeout;
        public UnityWebRequest(string url, string method) { if (!url.StartsWith("offline://")) throw new Exception("Live request forbidden in execution tests"); }
        public void SetRequestHeader(string key, string value) { }
        public void Abort() { } public void Dispose() { }   // AIPlanner.DropRequestForHold (ae09acf /ai hold)
        public void SendWebRequest() { AIPlanner.LastRequestBody=System.Text.Encoding.UTF8.GetString(((UploadHandlerRaw)uploadHandler).Bytes); Harness.Trace.Add("offline transport called"); }
    }
}
