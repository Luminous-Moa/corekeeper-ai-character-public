using System;
using System.Collections.Generic;

// Spatial grouping only: eligibility and value remain with the observation/action producers and the model.
public static class AIGatheringRules
{
    public static string LimitForm(string action, int target, int maximum, int order = -1)
    {
        if (order != -1 && order < 1) return "max_order는 생략하거나 원래 HostOrder의 양의 정수 ID여야 함";
        if (order >= 1 && maximum == -1) return "max_order는 max_count와 함께만 사용";
        if (maximum == -1) return null;
        if (action != "gather") return "max_count는 gather의 명시 상한에만 사용";
        if (maximum < 1) return "max_count는 생략하거나 양의 정수여야 함";
        if (target > maximum) return "채집 목표량이 명시 상한보다 큼";
        return null;
    }
    public static string LimitReason(int maximum, long acquired, int nextMaximum)
    {
        if (maximum < 0) return null;
        long left = (long)maximum - acquired;
        if (left <= 0) return $"명시 확보 상한 {maximum}개에 도달함(관측 {acquired}). 새 채집 대상 시작 보류";
        if (nextMaximum < 0) return $"명시 상한까지 {left}개 남음. 다음 단일 채집의 최대 산출량을 확인하지 못해 초과하지 않는다고 보장할 수 없음: 실행 보류";
        if (nextMaximum > left) return $"명시 상한까지 {left}개 남지만 다음 단일 물품은 최대 {nextMaximum}개. 부분 회수가 보장되지 않아 실행 보류";
        return null;
    }
    public static List<List<T>> Components<T>(IList<T> candidates, Func<T, string> family, Func<T, int> x, Func<T, int> y)
    {
        var result = new List<List<T>>();
        var seen = new bool[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            if (seen[i]) continue;
            var cluster = new List<T>(); var pending = new Queue<int>();
            pending.Enqueue(i); seen[i] = true;
            while (pending.Count > 0)
            {
                int at = pending.Dequeue(); var a = candidates[at]; cluster.Add(a);
                for (int j = 0; j < candidates.Count; j++)
                {
                    if (seen[j] || family(a) != family(candidates[j])) continue;
                    long dx = (long)x(a) - x(candidates[j]), dy = (long)y(a) - y(candidates[j]);
                    // Same or touching grid cells form one visible patch, including diagonal neighbours.
                    if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1) continue;
                    seen[j] = true; pending.Enqueue(j);
                }
            }
            result.Add(cluster);
        }
        return result;
    }

    // Positive observed inventory deltas are distinct from completed target counts, and survive later spending.
    public static void Observe<T>(Dictionary<T, int> previous, IDictionary<T, int> current, Dictionary<T, long> received)
    {
        foreach (var kv in current)
        {
            previous.TryGetValue(kv.Key, out int before);
            long add = (long)kv.Value - before;
            if (add > 0) { received.TryGetValue(kv.Key, out long old); received[kv.Key] = old + add; }
        }
        previous.Clear(); foreach (var kv in current) previous[kv.Key] = kv.Value;
    }
}
