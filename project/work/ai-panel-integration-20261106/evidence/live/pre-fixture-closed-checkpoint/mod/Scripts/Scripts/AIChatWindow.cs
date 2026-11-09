// AIChatWindow — chat lines stay fully clear for fadeSeconds (default 7), then vanish over 1 s (채팅창 개선, 2026-11-05
// 합의 work/2026-11/2026-11-05-채팅창-개선/0-합의.md; user decision 2026-11-06 '7초 선명 → 짧게 사라짐'; letter size and
// line count are left for later, the chat-ui branch keeps that work).
// One Harmony postfix on ChatWindow.LateUpdate. Every frame, after the game's Update (which puts 4 s on every line when
// the chat box closes), each fade in the chat window's fadeEffects queue gets fadeOutTime = fadeSeconds + 1 and a curve
// that is 1 up to fadeSeconds/(fadeSeconds+1), then straight down to 0. The game reads both values every frame
// (PugTextEffectMaxFade.PugTextEffectLateUpdate), so at most one frame runs at the game's pace. Nothing else is touched:
// not the lines' positions, size, font, activation or the line limit, not when a fade starts or is reset (opening the
// box still shows every kept line again). Every chat line goes through that queue: the host's, other players', the
// game's notices and the AI characters' replies (AIPlayerSpikeMod.FlushReplies -> ChatWindow.RenderText).
// A failure turns it off and puts every fade's own time and curve back.
// Settings: <persistentDataPath>/AIPlayers/chat-settings.json {"fadeSeconds": 7} (written with the default when absent,
// re-read when the file changes).
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

[Serializable]
public class ChatUiSettings
{
    public float fadeSeconds = AIChatFade.DefaultFadeSeconds;   // how long a line stays fully clear (then 1 s to vanish)
}

public static class AIChatWindow
{
    private static ChatUiSettings _s;
    private static float _fade = AIChatFade.DefaultFadeSeconds;
    private static DateTime _fileTime, _failedTime;
    private static float _nextFileCheck;
    private static bool _ready;          // the patch is in (InstallPatches -> Patched); until then nothing changes
    private static bool _off;            // a failure turned it off (the game's own fades keep working)

    private static FieldInfo _fadesF;
    private static ChatWindow _cw;
    private static Queue<PugTextEffectMaxFade> _fades;

    // one shared curve for the current fadeSeconds; each effect's own time and curve, to put back on Fail
    private struct FadeOrig { public float Time; public AnimationCurve Curve; }
    private static readonly Dictionary<PugTextEffectMaxFade, FadeOrig> _orig = new Dictionary<PugTextEffectMaxFade, FadeOrig>();
    private static AnimationCurve _curve;
    private static float _curveFor = -1f;

    private static string FilePath => Path.Combine(Application.persistentDataPath, "AIPlayers", "chat-settings.json");

    public static void Patched(bool lateUpdate)
    {
        _ready = lateUpdate;
        if (!lateUpdate) AIPlayerSpikeMod.Log("CHAT-UI: ChatWindow.LateUpdate not patched; chat lines fade as the game has it");
    }

    // ---------------------------------------------------------------- settings

    private static void Refresh()
    {
        float now = Time.realtimeSinceStartup;
        if (_s != null && now < _nextFileCheck) return;
        _nextFileCheck = now + 2f;
        string p = FilePath;
        DateTime t = default(DateTime);
        try
        {
            if (!File.Exists(p))
            {
                if (_s == null)
                {
                    Apply(new ChatUiSettings(), "defaults");
                    Directory.CreateDirectory(Path.GetDirectoryName(p));
                    File.WriteAllText(p, JsonUtility.ToJson(_s, true), new UTF8Encoding(false));
                    _fileTime = File.GetLastWriteTimeUtc(p);
                }
                return;
            }
            t = File.GetLastWriteTimeUtc(p);
            if (_s != null && t == _fileTime) return;
            var s = JsonUtility.FromJson<ChatUiSettings>(File.ReadAllText(p, Encoding.UTF8));
            _fileTime = t;   // only after a good read: a locked or half-written file is read again at the next check
            Apply(s ?? new ChatUiSettings(), p);
        }
        catch (Exception e)
        {
            if (_s == null) Apply(new ChatUiSettings(), "defaults (settings file unreadable)");
            if (t != _failedTime)   // the error once per file version, not every 2 s while the file stays broken
            {
                _failedTime = t;
                AIPlayerSpikeMod.LogError($"CHAT-UI: reading {p} failed; keeping fadeSeconds {_fade} (read again every 2 s)", e);
            }
        }
    }

    private static void Apply(ChatUiSettings s, string from)
    {
        _s = s;
        _fade = AIChatFade.FadeSeconds(s.fadeSeconds);
        AIPlayerSpikeMod.Log($"CHAT-UI: chat lines stay clear {_fade} s, then vanish over {AIChatFade.VanishSeconds} s (fadeOutTime {AIChatFade.FadeOutTime(_fade)}) from {from}");
    }

    // ---------------------------------------------------------------- patch

    // Harmony postfix on ChatWindow.LateUpdate.
    public static void LateUpdatePostfix(ChatWindow __instance)
    {
        if (!_ready || _off || __instance == null) return;
        try
        {
            if (!Bind(__instance)) return;
            Refresh();
            Shape();
        }
        catch (Exception e)
        {
            Fail("LateUpdate postfix failed", e);
        }
    }

    private static bool Bind(ChatWindow cw)
    {
        if (cw == _cw && _fades != null) return true;
        if (_fadesF == null) _fadesF = AccessTools.Field(typeof(ChatWindow), "fadeEffects");
        _fades = _fadesF != null ? _fadesF.GetValue(cw) as Queue<PugTextEffectMaxFade> : null;
        if (_fades == null) { Fail($"ChatWindow.fadeEffects {(_fadesF == null ? "not found" : "has an unexpected type")}", null); return false; }
        if (_cw != cw) _orig.Clear();   // a new chat window (scene change): the old lines went with the old one
        _cw = cw;
        return true;
    }

    private static void Shape()
    {
        if (_curve == null || _curveFor != _fade)
        {
            float h = AIChatFade.HoldFraction(_fade);
            float slope = -1f / (1f - h);   // straight line from (h, 1) to (1, 0)
            _curve = new AnimationCurve(new Keyframe(0f, 1f, 0f, 0f), new Keyframe(h, 1f, 0f, slope), new Keyframe(1f, 0f, slope, 0f));
            _curveFor = _fade;
        }
        float total = AIChatFade.FadeOutTime(_fade);
        foreach (var fe in _fades)
        {
            if (fe == null) continue;
            if (!_orig.ContainsKey(fe)) _orig[fe] = new FadeOrig { Time = fe.fadeOutTime, Curve = fe.fadeOutCurve };
            if (fe.fadeOutTime != total) fe.fadeOutTime = total;
            if (fe.fadeOutCurve != _curve) fe.fadeOutCurve = _curve;
        }
    }

    private static void Fail(string what, Exception e)
    {
        _off = true;
        try
        {
            foreach (var kv in _orig)
            {
                if (kv.Key == null) continue;   // destroyed with its chat window
                kv.Key.fadeOutTime = kv.Value.Time;
                kv.Key.fadeOutCurve = kv.Value.Curve;
            }
            _orig.Clear();
        }
        catch (Exception e2) { AIPlayerSpikeMod.LogError("CHAT-UI: putting the fades back failed", e2); }
        AIPlayerSpikeMod.LogError("CHAT-UI: " + what + "; chat lines fade as the game has it", e);
    }
}
