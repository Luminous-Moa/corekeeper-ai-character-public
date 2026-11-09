using System;
using Unity.Entities;
class ReviewLimit {
 static void Main(){
  AIGoals.Goal=new AIGoal{kind=GoalKind.Gather,item=ObjectID.CopperOre,count=10,maxCount=10};AIBody.Bag[ObjectID.CopperOre]=10;
  var world=new World(); var player=new Entity();
  foreach(bool progress in new[]{true,false}){
   var c=new AICandidate{kind="dig",progress=progress,itemId=ObjectID.None,desc="적 또는 다른 자원으로 파 들어가기"};
   Console.WriteLine("Explicit Copper cap10 acquired10, dig progress="+progress+", result="+(AIGathering.LimitReason(world,player,c)??"ALLOWED"));
  }
 }
}
