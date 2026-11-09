// AIDecisionsApi — the OpenAI Decisions call (refactor S5, moved from AIBrain): the endpoint, the settings and monthly
// usage files, the API key, sending a request and reading its answer. Settings, usage and key are shared by every AI
// character; the brain (AIBrain.Loop) decides when to call and what the answer means.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// What a finished request said (AIDecisionsApi.Read). The brain logs from it in the order it always did.
public sealed class DecisionReply
{
    public long Code;
    public string Text = "";
    public bool NetOk;
    public string NetErr;
    public string ErrorMessage = "";   // the API's error.message when not ok
    public DAnswer Answer;             // the first answer (null: none)
    public int Tokens, Cached;
    public Exception ParseError;       // JsonUtility threw on the body
    public bool Fallback;              // JsonUtility gave no answer; read with ParseAnswerFallback
    public bool Ok => NetOk && Code == 200;
}

public static class AIDecisionsApi
{
    public const string Url = "https://api.openai.com/v1/decisions";

    private static DecisionsSettings _settings;
    private static DecisionsUsage _usage;

    public static UnityWebRequest Post(string json, string key, int timeout)
    {
        var req = new UnityWebRequest(Url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + key);
        req.timeout = timeout;
        req.SendWebRequest();
        return req;
    }

    // Reads and disposes a finished request. Not ok: the API's error message. Ok: the answer, tokens and cached tokens
    // (JsonUtility first; when it leaves the nested answer empty, the few fields are read from the text).
    public static DecisionReply Read(UnityWebRequest req)
    {
        var d = new DecisionReply();
        d.Code = req.responseCode;
        d.Text = req.downloadHandler != null ? req.downloadHandler.text : "";
        d.NetOk = req.result == UnityWebRequest.Result.Success;
        d.NetErr = req.error;
        req.Dispose();
        string text = d.Text;
        if (!d.Ok)
        {
            string msg = "";
            try { var ew = JsonUtility.FromJson<DErrWrap>(text); if (ew != null && ew.error != null) msg = ew.error.message; } catch { }
            d.ErrorMessage = msg;
            return d;
        }
        DResp r = null;
        try { r = JsonUtility.FromJson<DResp>(text); } catch (Exception e) { d.ParseError = e; }
        DAnswer ans = r != null && r.answers != null && r.answers.Length > 0 ? r.answers[0] : null;
        int tokens = r != null && r.usage != null ? r.usage.input_tokens : 0;
        int cached = r != null && r.usage != null && r.usage.input_tokens_details != null ? r.usage.input_tokens_details.cached_tokens : 0;
        if (ans == null || string.IsNullOrEmpty(ans.type))
        {
            ans = ParseAnswerFallback(text, out tokens, out cached);
            d.Fallback = true;
        }
        d.Answer = ans; d.Tokens = tokens; d.Cached = cached;
        return d;
    }

    // Minimal reader for {"answers":[{"type":"choice","choice":"c3","probabilities":[{"value":"c1","probability":0.1},...],
    // "confidence":0.8}],"usage":{"input_tokens":N,"input_tokens_details":{"cached_tokens":M}}}.
    private static DAnswer ParseAnswerFallback(string t, out int tokens, out int cached)
    {
        tokens = IntAfter(t, "\"input_tokens\":");
        cached = IntAfter(t, "\"cached_tokens\":");
        if (string.IsNullOrEmpty(t)) return null;
        var a = new DAnswer { type = StrAfter(t, "\"type\":"), choice = StrAfter(t, "\"choice\":") };
        string conf = NumAfter(t, "\"confidence\":");
        if (conf != null) float.TryParse(conf, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out a.confidence);
        var probs = new List<DProb>();
        int i = t.IndexOf("\"probabilities\":[", StringComparison.Ordinal);
        int end = i >= 0 ? t.IndexOf(']', i) : -1;
        if (i >= 0 && end > i)
        {
            string seg = t.Substring(i, end - i);
            int p = 0;
            while ((p = seg.IndexOf("\"value\":", p, StringComparison.Ordinal)) >= 0)
            {
                string v = StrAfter(seg.Substring(p), "\"value\":");
                string pr = NumAfter(seg.Substring(p), "\"probability\":");
                float f = 0f;
                if (pr != null) float.TryParse(pr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f);
                probs.Add(new DProb { value = v, probability = f });
                p += 8;
            }
        }
        a.probabilities = probs.ToArray();
        return a;
    }

    private static string StrAfter(string t, string key)
    {
        int i = t.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        int q1 = t.IndexOf('"', i + key.Length);
        if (q1 < 0) return null;
        int q2 = t.IndexOf('"', q1 + 1);
        return q2 > q1 ? t.Substring(q1 + 1, q2 - q1 - 1) : null;
    }

    private static string NumAfter(string t, string key)
    {
        int i = t.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        int s = i + key.Length, e = s;
        while (e < t.Length && ("0123456789.-eE+".IndexOf(t[e]) >= 0)) e++;
        return e > s ? t.Substring(s, e - s) : null;
    }

    private static int IntAfter(string t, string key)
    {
        string n = NumAfter(t ?? "", key);
        return n != null && int.TryParse(n, out int v) ? v : 0;
    }

    // ------------------------------------------------------------------ settings, usage, key

    private static string Dir => Path.Combine(Application.persistentDataPath, "AIPlayers");

    public static DecisionsSettings Settings()
    {
        if (_settings != null) return _settings;
        string p = Path.Combine(Dir, "decisions-settings.json");
        try
        {
            if (File.Exists(p)) _settings = JsonUtility.FromJson<DecisionsSettings>(File.ReadAllText(p));
        }
        catch (Exception e) { AIPlayerSpikeMod.LogError("decisions settings read failed; using defaults", e); }
        if (_settings == null)
        {
            _settings = new DecisionsSettings();
            try { Directory.CreateDirectory(Dir); File.WriteAllText(p, JsonUtility.ToJson(_settings, true)); } catch { }
        }
        return _settings;
    }

    internal static DecisionsUsage Usage()
    {
        string month = DateTime.Now.ToString("yyyy-MM");
        if (_usage == null)
        {
            string p = Path.Combine(Dir, "decisions-usage.json");
            try { if (File.Exists(p)) _usage = JsonUtility.FromJson<DecisionsUsage>(File.ReadAllText(p)); } catch { }
            if (_usage == null) _usage = new DecisionsUsage();
        }
        if (_usage.month != month) { _usage = new DecisionsUsage { month = month }; }
        return _usage;
    }

    internal static void AddUsage(int tokens, double usd)
    {
        var u = Usage();
        u.calls++;
        u.inputTokens += tokens;
        u.usd += usd;
        // per character this month (multi-AI): parallel arrays keyed by member id; the cap stays on the total
        int k = WhoIndex(u, AIAgent.Cur.Id);
        u.whoCalls[k]++;
        u.whoTokens[k] += tokens;
        u.whoUsd[k] += usd;
        try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, "decisions-usage.json"), JsonUtility.ToJson(u, true)); }
        catch (Exception e) { AIPlayerSpikeMod.LogError("decisions usage write failed", e); }
    }

    private static int WhoIndex(DecisionsUsage u, string id)
    {
        if (u.who == null) u.who = new string[0];
        int n = u.who.Length;
        for (int i = 0; i < n; i++) if (u.who[i] == id) { Fit(u, n); return i; }
        Fit(u, n + 1);
        u.who[n] = id;
        return n;
    }

    private static void Fit(DecisionsUsage u, int n)
    {
        if (u.who.Length < n) Array.Resize(ref u.who, n);
        if (u.whoCalls == null) u.whoCalls = new int[0];
        if (u.whoTokens == null) u.whoTokens = new long[0];
        if (u.whoUsd == null) u.whoUsd = new double[0];
        if (u.whoCalls.Length < n) Array.Resize(ref u.whoCalls, n);
        if (u.whoTokens.Length < n) Array.Resize(ref u.whoTokens, n);
        if (u.whoUsd.Length < n) Array.Resize(ref u.whoUsd, n);
    }

    // this character's calls and cost this month
    internal static string WhoText(DecisionsUsage u)
    {
        int k = WhoIndex(u, AIAgent.Cur.Id);
        return $"{AIAgent.Cur.Name} {u.whoCalls[k]}회 ${u.whoUsd[k]:F4}";
    }

    private static string _key;
    private static bool _keyLogged;

    internal static bool KeyMissing() => string.IsNullOrEmpty(ApiKey());

    internal static string ApiKey()
    {
        if (!string.IsNullOrEmpty(_key)) return _key;
        string src = "";
        try { _key = Environment.GetEnvironmentVariable("OPENAI_API_KEY"); if (!string.IsNullOrEmpty(_key)) src = "process env"; } catch { }
        if (string.IsNullOrEmpty(_key))
        {
            try
            {
                _key = Environment.GetEnvironmentVariable("OPENAI_API_KEY", EnvironmentVariableTarget.User);
                if (!string.IsNullOrEmpty(_key)) src = "user env";
            }
            catch (Exception e) { AIPlayerSpikeMod.LogError("reading OPENAI_API_KEY from the user environment failed", e); }
        }
        if (!_keyLogged)
        {
            _keyLogged = true;
            AIPlayerSpikeMod.Log(string.IsNullOrEmpty(_key) ? "OPENAI_API_KEY not found" : $"OPENAI_API_KEY found ({src}, length {_key.Length})");
        }
        return _key;
    }
}
