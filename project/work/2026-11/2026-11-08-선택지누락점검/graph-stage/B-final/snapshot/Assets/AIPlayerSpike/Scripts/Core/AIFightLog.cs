// AIFightLog — what the upper LLM is told about fights (자연어 명령, 2026-11-05 user decisions 3·4). Unity-free
// (Tools/core-tests). The fight itself (hit, dodge, retreat, eat) is the action model's; the upper LLM decides what to
// do after it. One log per character:
//   - every fight AIExperience records (an enemy within 7 tiles on screen, ended when none for 5 s) with its enemies,
//     the enemies that fell in view (who hit them is not known), downs, damage taken, health at the end;
//   - a tally per enemy kind over the character's whole record in this world (fights, fights with a down), filled
//     from the saved fight events once and then from each new fight.
// ShouldReport: a fight that ended below half health or with a down calls the upper LLM once (fight_end); other
// fights do not. ReportJson: the recent fights, one line each, facts only.
using System.Collections.Generic;
using System.Linq;
using System.Text;

public sealed class FightRecord
{
    public float At;                                   // when it ended (the caller's clock)
    public Dictionary<string, int> Enemies = new Dictionary<string, int>();   // kind -> most seen at once
    public Dictionary<string, int> Fell = new Dictionary<string, int>();      // kind -> fell in view during the fight
    public int Downs, Damage, EndHp, MaxHp;
    public float Secs;
}

public sealed class AIFightLog
{
    private readonly List<FightRecord> _recent = new List<FightRecord>();
    private readonly Dictionary<string, (int fights, int downs)> _tally = new Dictionary<string, (int, int)>();
    public bool TallyLoaded;
    public IReadOnlyList<FightRecord> Recent => _recent;

    public static bool ShouldReport(FightRecord f) => ShouldReport(f, false);
    // downReported: the down of this fight already went to the upper LLM (back_up sent or waiting, AIPlanner): then the
    // fight does not call again (검수 F4: one down called back_up and fight_end). Health at the end of a fight with a
    // down is the health after getting back up, so it does not count either.
    public static bool ShouldReport(FightRecord f, bool downReported)
        => f.Downs > 0 ? !downReported : (f.MaxHp > 0 && f.EndHp * 2 < f.MaxHp);

    // A saved fight event (from the events file): kinds and whether it had a down.
    public void Count(IEnumerable<string> kinds, bool down)
    {
        foreach (var k in kinds.Distinct())
        {
            _tally.TryGetValue(k, out var t);
            _tally[k] = (t.fights + 1, t.downs + (down ? 1 : 0));
        }
    }

    public void Add(FightRecord f)
    {
        Count(f.Enemies.Keys, f.Downs > 0);
        _recent.Add(f);
    }

    public (int fights, int downs) Tally(string kind) => _tally.TryGetValue(kind, out var t) ? t : (0, 0);

    // The historical field name is kept; every recorded fight remains available, oldest first.
    public string ReportJson(float now, System.Func<string, string> esc)
    {
        var sb = new StringBuilder("[");
        bool first = true;
        foreach (var f in _recent)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(esc(Line(f, now))).Append('"');
        }
        sb.Append(']');
        return sb.ToString();
    }

    public string Line(FightRecord f, float now)
    {
        string foes = string.Join(", ", f.Enemies.Select(kv => kv.Value > 1 ? $"{kv.Key} {kv.Value}" : kv.Key));
        string fell = f.Fell.Count == 0 ? "쓰러진 적 없음" : "쓰러진 적 " + string.Join(", ", f.Fell.Select(kv => $"{kv.Key} {kv.Value}")) + "(누가 쳤는지 모름)";
        string past = string.Join(", ", f.Enemies.Keys.Select(k => { var t = Tally(k); return $"{k} 싸움 {t.fights}번 중 쓰러짐 {t.downs}번"; }));
        return $"{(int)(now - f.At)}초 전 끝, {f.Secs:F0}초: {foes}. {fell}. 나 쓰러짐 {f.Downs}번, 받은 피해 {f.Damage}, 끝 체력 {f.EndHp}/{f.MaxHp}{(f.Downs > 0 ? "(다시 일어난 뒤)" : "")}. 이 월드 전적: {past}";
    }
}
