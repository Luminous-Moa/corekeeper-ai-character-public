using System;
using System.Collections.Generic;
class ReviewFlow {
 static void Main(){
  Harness.Reset();var w=Harness.World;var p=Harness.Player;
  AIGoals.SetGoal(w,p,GoalKind.Gather,ObjectID.CopperOre,"구리",50,"llm:gemini#1","tile:20,30");
  var oldGoal=AIGoals.Goal;
  var mine=new AICandidate{kind="mine",key="mine:20,30",itemId=ObjectID.CopperOre,progress=true,desc="원래 구리 광맥"};
  AIExpedition.Compose(w,p,new AIObservation(),new List<AICandidate>{mine});AIBrain.ApplyCandidate(mine,1);
  AIBody.Bag[ObjectID.CopperOre]=20;AIExpedition.Observe(w,p);
  Console.WriteLine("Original gathered="+AIExpedition.Gathered(oldGoal,20)+"; explicit where stored='"+oldGoal.where+"'");
  AIGoals.SetGoal(w,p,GoalKind.Home,ObjectID.None,"",1,"llm:gemini#2");
  Console.WriteLine("Home report="+AIExpedition.Report());
  Harness.Reset(); AIGoals.SetGoal(w,p,GoalKind.Gather,ObjectID.CopperOre,"구리",50,"llm:gemini#1");
  var path=new AICandidate{kind="explore",key="travel:explore:0,0:1,0",progress=true,desc="보이는 빈 통로"};
  var list=AIExpedition.Compose(w,p,new AIObservation(),new List<AICandidate>{path});
  AIGoals.CheckAfterBuild(w,p,1,new GoalBuildFacts{HasProgress=list.Exists(c=>c.progress),GatherFacts="관측한 광맥 고갈",BlockNote="구리 채집 후보 없음"});
  AIExpedition.AfterBuild(w,p,list,false);
  Console.WriteLine("Exhausted ore plus generic corridor: replans="+AIPlanner.Replans+"; goal observation='"+AIGoals.Observation+"'");
 }
}
