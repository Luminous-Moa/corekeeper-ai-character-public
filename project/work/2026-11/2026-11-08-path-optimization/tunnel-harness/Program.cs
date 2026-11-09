
using System;
using System.Linq;
using System.Reflection;
class Program {
 static int checks; static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static World Map(int length=100) {var w=new World();for(int x=0;x<=length;x++) w.Cells[new int2(x,0)]=new Cell();w.Cells[new int2(length/2,0)].Wall=true;AIVision.Current=w;return w;}
 static string Summary(TunnelPlan p,string reason) => p==null?"null:"+reason:
 string.Join(";",p.path.Select(t=>$"{t.x},{t.y}"))+"|"+string.Join(";",p.walls.Select(t=>$"{t.p.x},{t.p.y}:{t.obj}:{t.hits}"))+
 $"|{p.totalHits}|{p.damage}|{p.toolShortSeen}|{p.toolShortRed}|{p.edgeRoute}|{p.edgeClosed}|{p.passes}|{p.firstCapped}|{reason}|{(p.alt==null?"":Summary(p.alt,""))}";
 static int Phase(AITunnel.PlanJob j)=>(int)typeof(AITunnel.PlanJob).GetField("phase",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(j);
 static void Finish(AITunnel.PlanJob j,int budget){int n=0;while(!j.Step(budget)){Check(j.LastStepWork<=budget,"budget");if(++n>100000)throw new Exception("no progress");} Check(j.LastStepWork<=budget,"final budget");}
 static void ToFill(AITunnel.PlanJob j){int n=0;while(Phase(j)!=6){j.Step(1);if(++n>100000)throw new Exception("no fill");}}
 static void Mutation(string name,Action<World> mutate){
 var w=Map();var j=AITunnel.BeginPlan(w,new Entity(),new int2(100,0));ToFill(j);mutate(w);Finish(j,1);
 Check(j.Changed&&j.Result==null&&j.ValidationCompleted,name); }
 static void Main(){
 foreach(int budget in new[]{1,7,32,4096}){
 var w=Map(400);var legacy=LegacyAITunnel.Plan(w,new Entity(),new int2(400,0),out var reason);
 var j=AITunnel.BeginPlan(w,new Entity(),new int2(400,0));Check(!j.Step(0)&&j.LastStepWork==0,"zero");Finish(j,budget);
 Check(!j.Changed,"stable");Check(Summary(j.Result,j.Reason)==Summary(legacy,reason),"legacy parity");
 Check(j.Result.RiskText(w)==legacy.RiskText(w),"risk parity");
 }
 Mutation("wall creation",w=>w.Cells[new int2(30,0)].Wall=true);
 Mutation("tileset replacement",w=>w.Cells[new int2(50,0)].Tileset=2);
 Mutation("off-route observation",w=>w.Cells[new int2(1,1)]=new Cell());
 Mutation("object footprint",w=>w.Blocked.Add(new int2(30,0)));
 Mutation("damage",w=>w.Damage++);
 Mutation("position",w=>w.Position=new float3(1,0,0));
 Mutation("edge mode",w=>w.EdgeActive=true);
 {
 var w=Map();w.EdgeActive=true;var j=AITunnel.BeginPlan(w,new Entity(),new int2(100,0));ToFill(j);w.Cells[new int2(50,0)].Edge=true;Finish(j,7);Check(j.Changed,"edge fact");
 }
 {
 var w=Map();var j=AITunnel.BeginPlan(w,new Entity(),new int2(100,0));Finish(j,7);Check(!j.Changed,"initial valid");
 w.Cells[new int2(25,0)].Wall=true;j.BeginValidation();int n=0;while(!j.StepValidation(1)){Check(j.LastValidationWork<=1,"validation budget");if(++n>10000)throw new Exception("validation no progress");}Check(j.Changed,"revalidation");
 }
 {
 var w=Map();w.Cells[new int2(50,0)].Tileset=2;w.Damage=1;var legacy=LegacyAITunnel.Plan(w,new Entity(),new int2(100,0),out var reason);
 var j=AITunnel.BeginPlan(w,new Entity(),new int2(100,0));Finish(j,1);Check(!j.Changed&&Summary(j.Result,j.Reason)==Summary(legacy,reason),"null parity");
 }
 Console.WriteLine("PASS "+checks+" checks (actual PlanJob extracted; stub world, not Unity runtime)");
 }
}
