using System;
using System.Collections.Generic;

// The input contains executable pickups in distance order. Preserve a goal pickup and a hunger-food pickup
// before filling the two slots. Within each need, walking for an item comes before an item arriving by itself.
public static class AIPickupOptions
{
    public static List<T> Select<T>(IList<T> items, Func<T, bool> goal, Func<T, bool> food,
        Func<T, bool> automatic, Func<T, string> key, Func<T, bool> eligible, bool free)
    {
        var selected = new List<T>();
        var keys = new HashSet<string>();
        void AddFirst(Func<T, bool> need)
        {
            for (int auto = 0; auto < 2; auto++)
                foreach (var item in items)
                    if (need(item) && automatic(item) == (auto == 1) && !keys.Contains(key(item)) && eligible(item))
                    { selected.Add(item); keys.Add(key(item)); return; }
        }
        if (!free) AddFirst(goal);
        // One item can satisfy both needs; keep the other slot available for a distinct choice.
        if (!selected.Exists(x => food(x))) AddFirst(food);
        while (selected.Count < 2)
        {
            int before = selected.Count;
            AddFirst(x => !free || food(x));
            if (selected.Count == before) break;
        }
        return selected;
    }
}
