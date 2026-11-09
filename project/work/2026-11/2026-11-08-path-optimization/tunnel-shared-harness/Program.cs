
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
 static void SharedTests(){
 {
 var w=Map(150000);var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(1)){}
 Check(!b.SearchesReady&&b.SearchCount==0,"sparse batch starts cold");var j=b.BeginSinglePlan(new int2(5,0));Finish(j,7);
 Check(j.Result!=null&&!j.Result.sharedSearch&&!j.ValidationCompleted,"single batch result defers validation");Check(b.SearchCount==0&&b.ExpandedTotal==0&&b.CapturedTileCount<50,"sparse plan skips whole map warm");
 w.Position=new float3(10,0,0);Validate(b);Check(!b.Changed,"single batch keeps initial position scope");
 }
 {
 var w=Map(100);var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(1)){}
 var single=b.BeginSinglePlan(new int2(100,0));Finish(single,7);var legacy=LegacyAITunnel.Plan(w,new Entity(),new int2(100,0),out var why);
 Check(Summary(single.Result,single.Reason)==Summary(legacy,why),"single batch legacy parity");
 while(!b.StepSearches(7)){}Check(b.SearchesReady,"warm readiness exposed");var shared=b.BeginPlan(new int2(100,0));Finish(shared,7);Check(shared.Result.sharedSearch&&b.SearchCount==1,"single then shared same batch");Validate(b);Check(!b.Changed,"mixed batch validation");
 }

 {
 var w=Map(150000);w.EdgeActive=true;w.Cells[new int2(50,0)].Wall=true;w.Cells[new int2(50,0)].Edge=true;
 var b=AITunnel.BeginBatch(w,new Entity());Check(!b.StepSearches(0)&&b.LastStepWork==0,"warm zero budget");Check(!b.StepSearches(1),"warm before capture");while(!b.StepCapture(1)){}
 while(!b.StepSearches(32))Check(b.LastStepWork<=32,"warm budget");Check(b.SearchCount==2&&b.ExpandedTotal==120050,"closed exhausted and open capped warm");long expanded=b.ExpandedTotal;
 var far=b.BeginPlan(new int2(150000,0));Finish(far,7);Check(far.Result==null&&far.Reason.Contains("상한")&&far.Reason.Contains("미확정"),"open capped overrides closed exhaustion");Check(b.ExpandedTotal==expanded,"capped query does not resume search");
 Check(b.StepSearches(1)&&b.LastStepWork==0,"warm completion idempotent");
 }

 foreach(int budget in new[]{1,7,4096}){
 var w=new World();AIVision.Current=w;w.EdgeActive=true;
 for(int x=0;x<=20;x++){w.Cells[new int2(x,0)]=new Cell();w.Cells[new int2(x,10)]=new Cell();}
 for(int y=0;y<=10;y++){w.Cells[new int2(0,y)]=new Cell();w.Cells[new int2(20,y)]=new Cell();}
 w.Cells[new int2(10,0)].Wall=true;w.Cells[new int2(10,0)].Edge=true;w.Cells[new int2(10,0)].Tileset=2;
 var old=LegacyAITunnel.Plan(w,new Entity(),new int2(20,0),out var why,-1,false,true);
 var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(budget)){}var j=b.BeginPlan(new int2(20,0),-1,false,true);Finish(j,budget);Validate(b,budget);
 Check(!b.Changed&&j.Result.alt!=null&&j.Result.alt.sharedSearch,"detour alternative retained");Check(Summary(j.Result,j.Reason)==Summary(old,why),"detour selection legacy parity");Check(j.Result.RiskText(w)==""&&j.Result.alt.RiskText(w)=="explosive","separate detour risk");Check(b.SearchCount==2,"detour two shared trees");
 }
 {
 var w=new World();AIVision.Current=w;w.EdgeActive=true;
 for(int x=0;x<25;x++)for(int y=0;y<2;y++)w.Cells[new int2(x,y)]=new Cell();
 w.Cells[new int2(2,0)].Wall=true;w.Cells[new int2(2,0)].Edge=true;w.Cells[new int2(10,1)].Wall=true;w.Cells[new int2(10,1)].Edge=true;
 var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(1)){}var j=b.BeginPlan(new int2(2,0),-1,false,true);Finish(j,7);Validate(b);
 Check(j.Result!=null&&j.Result.passes==1&&j.Result.alt==null&&b.SearchCount==1,"target-only wall excluded from detour trigger");
 }

 {
 var w=Map();w.Cells[new int2(50,0)].Tileset=2;w.Damage=1;var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(1)){}
 var j=b.BeginPlan(new int2(100,0));Finish(j,7);Check(j.Result==null&&j.Reason.Contains("공유 검색")&&j.Reason.Contains("도구 부족")&&j.Reason.Contains("미관측"),"shared failure diagnostics");Check(b.SearchCount==1,"one tree without edges");Validate(b);
 }
 {
 var w=Map(150000);var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(1)){}
 var far=b.BeginPlan(new int2(150000,0));Finish(far,4096);Check(far.Result==null&&far.Reason.Contains("상한")&&far.Reason.Contains("미확정"),"shared capped uncertainty");
 var near=b.BeginPlan(new int2(10,0));Finish(near,7);Check(near.Result!=null&&!near.Result.firstCapped,"settled route valid after tree cap");Check(b.SearchCount==1&&b.ExpandedTotal==120000,"shared capped tree reuse");
 }
 {
 var w=new World();AIVision.Current=w;w.EdgeActive=true;var goals=new System.Collections.Generic.List<int2>();
 for(int y=0;y<175;y++)for(int x=0;x<174;x++){
 var t=new int2(x,y);var c=new Cell();if(y%4==1){c.Wall=true;c.Edge=true;if(y%12==9)c.Tileset=2;if(goals.Count<7526)goals.Add(t);}w.Cells[t]=c;
 }
 for(int x=0;x<42;x++)w.Cells[new int2(x,175)]=new Cell();
 Check(w.Cells.Count==30492&&goals.Count==7526,"requested stress dimensions");
 var b=AITunnel.BeginBatch(w,new Entity());while(!b.StepCapture(1)){}
 var watch=System.Diagnostics.Stopwatch.StartNew();long reconstruction=0;int beforeValidation=0;
 while(!b.StepSearches(32))Check(b.LastStepWork<=32,"stress warm budget");long warmed=b.ExpandedTotal;Check(b.SearchCount==2,"both production graphs warmed");
 foreach(var target in goals){
 var j=b.BeginPlan(target,-1,false,true);Finish(j,4096);var p=j.Result;
 Check(p!=null&&p.sharedSearch,"boundary target route exists");Check(p.path[p.path.Count-1]==target,"terminal target preserved");
 Check(p.totalHits==p.walls.Sum(v=>v.hits),"actual route hits");
 string risk=p.walls.Any(v=>v.obj==ObjectID.Explosive)?"explosive":"";Check(p.RiskText(w)==risk,"actual chosen route risk");
 Check(b.SearchCount<=2&&b.ExpandedTotal<=2L*w.Cells.Count,"two maps search bound");Check(b.ExpandedTotal==warmed,"no synchronous expansion after warm");reconstruction+=p.path.Count;
 }
 beforeValidation=w.TileReads;Validate(b,4096);watch.Stop();
 Check(!b.Changed&&b.SearchCount==2,"stress validation and two searches");Check(w.TileReads==beforeValidation*2,"stress unique fact validation");
 Console.WriteLine($"STRESS targets={goals.Count} known={w.Cells.Count} searches={b.SearchCount} expanded={b.ExpandedTotal} captured={b.CapturedTileCount} routeTiles={reconstruction} elapsedMs={watch.Elapsed.TotalMilliseconds:F1}");
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
 SharedTests();
 Console.WriteLine("PASS "+checks+" checks (actual PlanJob extracted; stub world, not Unity runtime)");
 }
}
