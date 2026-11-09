using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
class Program {
 static World w=new World(); static Entity p=default;
 static void Reset(string name){AIAgent.Cur=new AIAgent(name);AIPlayerSpikeMod.WorldId="review";AIBody.Position=new float3();AIBody.Current=null;AITileReader.Open.Clear();AIObjects.Blocked.Clear();AIBrain.Observations.Clear();AIVision.Half=10;Time.realtimeSinceStartup=100;}
 static void Main(){
  Reset("wide"); for(int x=0;x<8;x++){AITileReader.Open.Add(new int2(x,0));AITileReader.Open.Add(new int2(x,1));}
  var s=new TravelSkill(new int2(1,0)){Run=1};AIBody.Current=s;s.Start(w,p,100);AIBody.Position=new float3(1,0,0);Time.realtimeSinceStartup=101;
  var input=s.Tick(w,p,101);var choices=AITravel.Compose(w,p,new AIObservation{pos=AIBody.Position},new List<AICandidate>(),1);
  Console.WriteLine("Two-wide straight corridor after one step: input="+input.move.x+","+input.move.y+"; turns="+choices.FindAll(c=>c.kind=="travel_turn").Count);
  foreach(var c in choices)Console.WriteLine(c.desc);
  Reset("ring"); var ring=new[]{new int2(0,0),new int2(1,0),new int2(2,0),new int2(2,1),new int2(2,2),new int2(1,2),new int2(0,2),new int2(0,1)};AITileReader.Open.UnionWith(ring);
  s=new TravelSkill(ring[1]){Run=2};AIBody.Current=s;s.Start(w,p,100);
  int moving=0;for(int i=1;i<=24;i++){var t=ring[i%8];AIBody.Position=new float3(t.x,0,t.y);Time.realtimeSinceStartup=100+i;input=s.Tick(w,p,100+i);if(input.move.x!=0||input.move.y!=0)moving++;}
  choices=AITravel.Compose(w,p,new AIObservation{pos=AIBody.Position},new List<AICandidate>(),1);
  Console.WriteLine("One-wide ring after 3 circuits: status="+s.Status+"; moving ticks="+moving+"/24; direction choices="+choices.Count+"; observations="+string.Join(" / ",AIBrain.Observations));
  var map=new Dictionary<AITravelRules.Cell,bool>();for(int x=0;x<=5;x++)map[new AITravelRules.Cell(x,0)]=x<5;
  var target=new AITravelRules.Cell(5,0);var path=AITravelRules.KnownPath(new AITravelRules.Cell(0,0),target,map,n=>true);
  var approach=AITravelRules.KnownPath(new AITravelRules.Cell(0,0),new AITravelRules.Cell(4,0),map,n=>true);
  Console.WriteLine("Blocked object goal with open adjacent approach: exact-target path="+(path==null?"null":path.Count.ToString())+"; reachable approach="+approach.Count+"; target-allow predicate=true cannot override map=false");
 }
}
