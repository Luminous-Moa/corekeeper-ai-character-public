// AIDrops — extracted from the existing implementation; behavior and state ownership are unchanged.
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

// What breaking an object kind (crate, wooden thing) or cutting a big root gave, observed from the bag change after
// each break. Kept per world with AIMemory (memory.json "drops") so it survives a game restart, like the combat
// numbers. Source key: the object's ObjectID, or RootSource for a big root tile.
public static class AIDrops
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    private sealed class State
    {
        public readonly Dictionary<int, Stat> _d = new Dictionary<int, Stat>();
    }
    private static State StateOf(AIAgent a) => a.Get<State>(AISlot.Drops);
    private static State S => StateOf(AIAgent.Cur);

    public const int RootSource = -1;
    private class Stat { public int breaks; public readonly Dictionary<ObjectID, (int times, int total)> items = new Dictionary<ObjectID, (int, int)>(); }
    private static Dictionary<int, Stat> _d => S._d;

    public static void Clear() { _d.Clear(); }

    public static void Note(int source, Dictionary<ObjectID, int> before, Dictionary<ObjectID, int> after)
    {
        if (!_d.TryGetValue(source, out var s)) { s = new Stat(); _d[source] = s; }
        s.breaks++;
        foreach (var kv in after)
        {
            before.TryGetValue(kv.Key, out int b);
            if (kv.Value <= b) continue;
            s.items.TryGetValue(kv.Key, out var x);
            s.items[kv.Key] = (x.times + 1, x.total + kv.Value - b);
        }
        AIMemory.MarkDirty();
    }

    public static bool Known(int source) => _d.TryGetValue(source, out var s) && s.breaks > 0;
    public static bool Gave(int source, ObjectID item) => _d.TryGetValue(source, out var s) && s.items.ContainsKey(item);

    // "관찰 3번: Wood 2번(합 3개), ScrapPart 1번(합 4개)"
    public static string Text(int source)
    {
        if (!_d.TryGetValue(source, out var s) || s.breaks == 0) return "";
        var parts = new List<string>();
        foreach (var kv in s.items) parts.Add($"{kv.Key} {kv.Value.times}번(합 {kv.Value.total}개)");
        return $"관찰 {s.breaks}번: {(parts.Count == 0 ? "가방 변화 없음" : string.Join(", ", parts))}";
    }

    // Every (source, item) seen so far, for "what gave food" (AIFoodSource, 먹거리 자급).
    public static List<(int source, ObjectID item)> Seen()
    {
        var l = new List<(int, ObjectID)>();
        foreach (var kv in _d) foreach (var it in kv.Value.items) l.Add((kv.Key, it.Key));
        return l;
    }

    public static string Summary()
    {
        var parts = new List<string>();
        foreach (var kv in _d) parts.Add($"{(kv.Key == RootSource ? "bigRoot" : ((ObjectID)kv.Key).ToString())}: {Text(kv.Key)}");
        return string.Join(" / ", parts);
    }

    public static int[] Export()
    {
        var l = new List<int>();
        foreach (var kv in _d)
        {
            if (kv.Value.items.Count == 0) { l.Add(kv.Key); l.Add(0); l.Add(0); l.Add(0); l.Add(kv.Value.breaks); }
            foreach (var it in kv.Value.items) { l.Add(kv.Key); l.Add((int)it.Key); l.Add(it.Value.times); l.Add(it.Value.total); l.Add(kv.Value.breaks); }
        }
        return l.ToArray();
    }

    public static void Import(int[] a)
    {
        if (a == null) return;
        for (int i = 0; i + 4 < a.Length; i += 5)
        {
            if (!_d.TryGetValue(a[i], out var s)) { s = new Stat(); _d[a[i]] = s; }
            s.breaks = Math.Max(s.breaks, a[i + 4]);
            if (a[i + 1] != 0) s.items[(ObjectID)a[i + 1]] = (a[i + 2], a[i + 3]);
        }
    }
}
