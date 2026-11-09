using System.Collections.Generic;
public static class ExplicitGatherLimitTests
{
    public static void Run()
    {
        var answer=AIPlanProtocol.Read("{\"answer\":{\"goal\":{\"do\":\"gather\",\"item\":\"CopperOre\",\"count\":9,\"max_count\":10},\"plan\":[{\"do\":\"gather\",\"item\":\"CopperOre\",\"where\":\"tile:3,4\",\"count\":9,\"max_count\":10},{\"do\":\"gather\",\"item\":\"Wood\",\"count\":200}]}}").Answer;
        Program.Check(answer.GoalMaxCount==10&&answer.GoalCount==9,"upper goal explicit max is separate from requested target");
        Program.Check(answer.Plan[0].MaxCount==10&&answer.Plan[0].Count==9&&answer.Plan[0].Where=="tile:3,4","plan preserves explicit max and work location");
        Program.Check(answer.Plan[1].MaxCount==-1&&answer.Plan[1].Count==200,"omitted limit stays absent, including large requests");
        var queue=new AIPlanQueue();queue.Set("limit-test",answer.Plan);
        Program.Check(queue.Current.MaxCount==10&&queue.ReportJson(s=>s).Contains("max_count=10"),"queued plan and next report preserve explicit ceiling");
        foreach(string value in new[]{"0","-1","1.5","\"10\"","true","{}","2147483648"})
        {
            var invalid=AIPlanProtocol.Read("{\"answer\":{\"plan\":[{\"do\":\"gather\",\"item\":\"CopperOre\",\"count\":1,\"max_count\":"+value+"}]}}").Answer.Plan[0];
            Program.Check(invalid.MaxCount==-2&&AIGatheringRules.LimitForm(invalid.Do,invalid.Count,invalid.MaxCount)!=null,"invalid explicit ceiling distinguishable from omission: "+value);
        }
        Program.Check(AIGatheringRules.LimitForm("craft",1,10)!=null,"explicit gather ceiling not silently accepted by another action");
        Program.Check(AIGatheringRules.LimitForm("gather",11,10)!=null,"target above explicit max refused");
        var owned=AIPlanProtocol.Read("{\"answer\":{\"goal\":{\"do\":\"gather\",\"max_count\":10,\"max_order\":17},\"plan\":[{\"do\":\"gather\",\"max_count\":10,\"max_order\":17}]}}").Answer;
        Program.Check(owned.GoalMaxOrder==17&&owned.Plan[0].MaxOrder==17&&owned.Plan[0].Text.Contains("max_order=17"),"goal and plan retain original HostOrder identity independently from plan source");
        Program.Check(AIGatheringRules.LimitForm("gather",9,10,17)==null&&AIGatheringRules.LimitForm("gather",9,-1,17)!=null,"explicit ceiling owner requires accompanying max_count");
        foreach(string value in new[]{"0","-1","1.5","\"17\"","true","{}","2147483648"})
        {
            var invalid=AIPlanProtocol.Read("{\"answer\":{\"plan\":[{\"do\":\"gather\",\"count\":1,\"max_count\":10,\"max_order\":"+value+"}]}}").Answer.Plan[0];
            Program.Check(invalid.MaxOrder==-2&&AIGatheringRules.LimitForm(invalid.Do,invalid.Count,invalid.MaxCount,invalid.MaxOrder)!=null,"invalid owner ID is rejected rather than resolved as missing: "+value);
        }
    }
}
