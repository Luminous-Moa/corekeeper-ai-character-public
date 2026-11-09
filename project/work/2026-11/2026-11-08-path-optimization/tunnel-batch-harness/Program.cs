
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
 static void Validate(AITunnel.PlanBatch b,int budget=1){b.BeginValidation();int n=0;while(!b.StepValidation(budget)){Check(b.LastStepWork<=budget,"batch validation budget");if(++n>100000)throw new Exception("validation stuck");}}
 static void BatchTests(){
 foreach(bool allow in new[]{false,true}) foreach(bool detour in new[]{false,true}){
 var w=Map();w.EdgeActive=true;w.Cells[new int2(100,0)].Wall=true;w.Cells[new int2(100,0)].Edge=true;
 var expected=LegacyAITunnel.Plan(w,new Entity(),new int2(100,0),out var why,-1,allow,detour);
 var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(1)){}var j=b.BeginPlan(new int2(100,0),-1,allow,detour);Finish(j,7);Validate(b);
 Check(!b.Changed&&Summary(j.Result,j.Reason)==Summary(expected,why),"target edge exception parity");
 }

 foreach(int budget in new[]{1,7,4096}){
 var w=Map(400);var b=AITunnel.BeginBatch(w,new Entity());Check(!b.StepCapture(0),"capture zero");while(!b.StepCapture(budget)) Check(b.LastStepWork<=budget,"capture budget");
 var j=b.BeginPlan(new int2(400,0));Finish(j,budget);int reads=w.TileReads;Check(!b.ValidationCompleted&&w.ObjectReads==1,"deferred validation");
 var j2=b.BeginPlan(new int2(400,0));Finish(j2,budget);Check(w.TileReads==reads,"shared tile facts");Check(Summary(j.Result,j.Reason)==Summary(j2.Result,j2.Reason),"shared parity");
 w.Position=new float3(10,0,0);Validate(b,budget);Check(!b.Changed&&b.ValidationCompleted,"moving batch valid");Check(w.LastObjectPosition==new float3(),"original object scope");Check(w.ObjectReads==2,"one batch object validation");Check(w.TileReads==reads*2,"unique validation once");
 Check(b.StepValidation(1)&&b.LastStepWork==0,"validation idempotent");
 }
 foreach(string change in new[]{"wall","object","damage","edge","door","tileset","risk"}){
 var w=Map();w.EdgeActive=true;var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(1)){}var j=b.BeginPlan(new int2(100,0));Finish(j,7);
 if(change=="wall")w.Cells[new int2(20,0)].Wall=true;
 if(change=="object")w.Blocked.Add(new int2(20,0));
 if(change=="damage")w.Damage++;
 if(change=="edge")w.Cells[new int2(50,0)].Edge=true;
 if(change=="door"){w.Cells[new int2(20,0)].Door=true;w.Cells[new int2(20,0)].DoorThere=true;}
 if(change=="tileset")w.Cells[new int2(50,0)].Tileset=2;
 if(change=="risk")w.Walls[1]=(20,0,ObjectID.Explosive);
 Validate(b);Check(b.Changed&&j.Changed&&j.Result==null&&j.Reason==b.Reason,"batch change "+change);
 }
 {
 var w=Map();w.Cells[new int2(50,0)].Tileset=2;var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(1)){}
 var low=b.BeginPlan(new int2(100,0),1);Finish(low,7);var high=b.BeginPlan(new int2(100,0),30);Finish(high,7);var lowAgain=b.BeginPlan(new int2(100,0),1);Finish(lowAgain,7);
 Check(low.Result==null&&high.Result!=null&&low.Reason==lowAgain.Reason,"override and per job diagnostics");Check(AITunnel.LastToolShortRed==2,"failure diagnostics restored");Validate(b);Check(!b.Changed,"override batch valid");
 }
 {
 var w=Map();w.Position=new float3(10,0,0);var b=AITunnel.BeginBatch(w,new Entity(),new float3());while(!b.StepCapture(1)){}Check(b.Start==new int2(0,0),"explicit captured position");var j=b.BeginPlan(new int2(100,0));Finish(j,7);Validate(b);Check(!b.Changed,"explicit position validates");
 }
 }
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
 BatchTests();
 Console.WriteLine("PASS "+checks+" checks (actual PlanJob extracted; stub world, not Unity runtime)");
 }
}
