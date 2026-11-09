using System.Collections.Generic;

// Previous assessments, scoped by the owning character state. Out-of-range observations do not call Record.
public sealed class AIThreatHistory<T>
{
    private readonly HashSet<T> cannotCome = new HashSet<T>();
    public void Record(T enemy, bool isThreat)
    {
        if (cannotCome.Count > 64) cannotCome.Clear();
        if (isThreat) cannotCome.Remove(enemy); else cannotCome.Add(enemy);
    }
    public bool LastAssessedCannotCome(T enemy) => cannotCome.Contains(enemy);
}
