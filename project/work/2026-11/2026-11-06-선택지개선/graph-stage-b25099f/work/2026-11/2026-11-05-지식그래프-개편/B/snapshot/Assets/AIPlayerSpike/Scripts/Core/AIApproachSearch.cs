using System.Collections.Generic;

public delegate TPlan TryApproach<TTile, TPlan>(TTile tile, out string why) where TPlan : class;

public static class AIApproachSearch
{
    public static TPlan FirstOfThree<TTile, TPlan>(IList<TTile> stands, TryApproach<TTile, TPlan> attempt, out TTile stand, out string why) where TPlan : class
    {
        TPlan plan = null; stand = default; why = "설 수 있는 아는 칸이 없음";
        for (int i = 0; i < stands.Count && i < 3 && plan == null; i++)
        {
            plan = attempt(stands[i], out why);
            stand = stands[i];
        }
        return plan;
    }
}
