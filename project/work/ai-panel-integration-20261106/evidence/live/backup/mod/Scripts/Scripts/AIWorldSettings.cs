// AIWorldSettings — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Pug.Properties;
using PugMod;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using Unity.Transforms;
using UnityEngine;

// Per-world AI participation setting: <persistentDataPath>/AIPlayers/<world ServerGuid>/settings.json.
// Absent file (every world before the host first uses /ai on) = off.
public static class AIWorldSettings
{
    // members: which AI characters join this world (claude | gpt | gemini | grok). aiEnabled is kept for older builds
    // (= any member on). A file from before members ({"aiEnabled": true}) reads as ["claude"].
    [Serializable]
    private class Data
    {
        public bool aiEnabled;
        public string[] members;
    }

    public static List<string> Members(string worldId)
    {
        var l = new List<string>();
        if (worldId == null) return l;
        string p = PathFor(worldId);
        try
        {
            if (!File.Exists(p)) return l;
            string text = File.ReadAllText(p, Encoding.UTF8);
            var d = JsonUtility.FromJson<Data>(text);
            if (d == null) return l;
            bool hasMembers = text.IndexOf("\"members\"", StringComparison.Ordinal) >= 0;
            if (hasMembers && d.members != null) { foreach (var m in d.members) if (AIAgent.ById(m) != null && !l.Contains(m)) l.Add(m); }
            else if (!hasMembers && d.aiEnabled) l.Add("claude");   // file from before members
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError($"AI setting: reading {p} failed; treating as off", e);
        }
        return l;
    }

    public static void SetMember(string worldId, string id, bool on)
    {
        var l = Members(worldId);
        if (on && !l.Contains(id)) l.Add(id);
        if (!on) l.Remove(id);
        // keep the member table order (claude, gpt, gemini, grok)
        var ordered = new List<string>();
        foreach (var a in AIAgent.All) if (l.Contains(a.Id)) ordered.Add(a.Id);
        Write(worldId, ordered);
    }

    private static void Write(string worldId, List<string> members)
    {
        string p = PathFor(worldId);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(p));
            File.WriteAllText(p, JsonUtility.ToJson(new Data { aiEnabled = members.Count > 0, members = members.ToArray() }, true), new UTF8Encoding(false));
            AIPlayerSpikeMod.Log($"AI setting: world {worldId} members [{string.Join(",", members)}] saved to {p}");
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError($"AI setting: writing {p} failed", e);
        }
    }

    public static string PathFor(string worldId)
    {
        return Path.Combine(Application.persistentDataPath, "AIPlayers", worldId, "settings.json");
    }

    public static bool Exists(string worldId)
    {
        return worldId != null && File.Exists(PathFor(worldId));
    }
}
