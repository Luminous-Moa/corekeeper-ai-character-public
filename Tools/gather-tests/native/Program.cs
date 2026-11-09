using System;
using Unity.Entities;
using Unity.Mathematics;
internal static class Program
{
 static int checks;static readonly World world=new World();static readonly Entity player=new Entity{Index=1};
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;Console.WriteLine("PASS "+why);}
 static void Cap(bool on){AIGoals.Goal=new AIGoal{kind=GoalKind.Gather,item=ObjectID.CopperOre,count=10,maxCount=on?10:-1};}
 public static int Main(string[] args)
 {
  var tile=new int2(1,0);AITileReader.Walls[tile]=new AITileReader.Wall{tileset=1,ore=true,oreTileset=4};Cap(true);
  if(Array.IndexOf(args,"--baseline")>=0)
  {
   var old=new MineSkill(tile,false);
   if(!old.Start(world,player,0))throw new Exception("Expected pre-fix bypass did not start");
   old.Tick(world,player,0);
   if(!old.Tick(world,player,1).primary)throw new Exception("Expected pre-fix native primary bypass did not reproduce");
   AITunnel.Next=new TunnelPlan();AITunnel.Next.path.Add(tile);AITunnel.Next.walls.Add((tile,ObjectID.None,1));
   var oldTunnel=new TunnelSkill(new int2(5,0),false,false,"false-progress route");
   if(!oldTunnel.Start(world,player,0))throw new Exception("Expected pre-fix tunnel bypass did not start");
   oldTunnel.Tick(world,player,0);
   if(!oldTunnel.Tick(world,player,1).primary)throw new Exception("Expected pre-fix tunnel primary bypass did not reproduce");
   Console.WriteLine("REPRODUCED pre-fix e1cedcf defect: explicit ceiling still emits actual MineSkill and TunnelSkill native primary input. Offline reproduction only.");return 0;
  }
  var mine=new MineSkill(tile,false);Check(!mine.Start(world,player,0)&&mine.Reason.Contains("실제 벽"),"actual MineSkill.Start blocks unknown native wall yield before hand/navigation inputs");
  Cap(false);mine=new MineSkill(tile,false);Check(mine.Start(world,player,0),"ordinary target leaves native MineSkill start unchanged");
  mine.Tick(world,player,0);Check(mine.Tick(world,player,1).primary,"ordinary goal still produces actual normal mining primary input");
  Cap(true);var input=mine.Tick(world,player,2);Check(!input.primary&&mine.Status==SkillStatus.Failed&&mine.Reason.Contains("MineSkill.Tick"),"ceiling arriving during native swing suppresses next destructive input and reports actual tile");
  AITileReader.Roots[tile]=1;var root=new RootSkill(tile);Check(!root.Start(world,player,0),"actual RootSkill.Start rejects uncertain result under explicit ceiling");
  Cap(false);root=new RootSkill(tile);root.Start(world,player,0);Check(root.Tick(world,player,1).primary,"ordinary target retains native root primary input");
  Cap(true);Check(!root.Tick(world,player,2).primary&&root.Status==SkillStatus.Failed,"actual root input boundary catches newly added ceiling");
  var target=new Entity{Index=7,Version=2};world.EntityManager.Set(target,new HealthCD{health=10});world.EntityManager.Set(target,new ObjectDataCD{objectID=ObjectID.Wood});world.EntityManager.Set(target,new Unity.Transforms.LocalTransform{Position=new float3{x=1}});
  var breaker=new BreakSkill(target);Check(!breaker.Start(world,player,0)&&breaker.Reason.Contains("이외 품목만"),"different source object ID is not accepted as proof of different output");
  Cap(false);breaker=new BreakSkill(target);breaker.Start(world,player,0);Check(breaker.Tick(world,player,1).primary,"ordinary target retains actual native break primary input");
  Cap(true);Check(!breaker.Tick(world,player,2).primary&&breaker.Status==SkillStatus.Failed,"actual break input boundary catches newly added ceiling");
  AITunnel.Next=new TunnelPlan();AITunnel.Next.path.Add(tile);AITunnel.Next.walls.Add((tile,ObjectID.None,1));
  var tunnel=new TunnelSkill(new int2(5,0),false,false,"path");Check(!tunnel.Start(world,player,0)&&tunnel.Reason.Contains("다음 파괴 자식"),"actual TunnelSkill rejects wall child even when route has no resource metadata");
  AITunnel.Next.walls.Clear();var walk=new TunnelSkill(new int2(5,0),false,false,"walk");Check(walk.Start(world,player,0)&&GotoSkill.Starts>0,"confirmed pure walk path is allowed under explicit ceiling");
  AITunnel.Next.walls.Add((tile,ObjectID.None,1));input=walk.Tick(world,player,1);Check(!input.primary&&walk.Status==SkillStatus.Failed&&walk.Reason.Contains("다음 파괴 자식"),"replanned next wall is checked after walking before a new mining child can start");
  Cap(false);tunnel=new TunnelSkill(new int2(5,0),false,false,"path");Check(tunnel.Start(world,player,0),"no explicit ceiling preserves normal tunnel child start");
  Console.WriteLine("PASS "+checks+" production native input-boundary checks");return 0;
 }
}
