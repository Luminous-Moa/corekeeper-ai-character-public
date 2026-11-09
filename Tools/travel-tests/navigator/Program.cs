using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
public static class Program {
 static int tests,failed; static World world=new World(); static Entity player=new Entity();
 static void Check(bool ok,string name){tests++;if(!ok)failed++;Console.WriteLine((ok?"PASS ":"FAIL ")+name);}
 static int2 T(int x,int y)=>new int2(x,y);
 static void Reset(string id){AIAgent.Cur=new AIAgent(id);AIPlayerSpikeMod.WorldId=id;AITileReader.Open.Clear();AIObjects.Blocked.Clear();AIBody.Position=new float3(0,0,0);AIVision.Half=20;Time.realtimeSinceStartup=0;}
 static void RunObject(string id,float tolerance){
  Reset(id);for(int x=0;x<=5;x++)AITileReader.Open.Add(T(x,0));AIObjects.Blocked.Add(T(5,0));
  var nav=new AINavigator();nav.Start(world,player,T(5,0),tolerance,0);
  Check(!AITravel.KnownWalkable(T(5,0)),id+" target remains nonwalkable in observed memory");
  NavStatus status=NavStatus.Moving;float2 move=default;
  for(int i=1;i<=80&&status==NavStatus.Moving;i++){Time.realtimeSinceStartup=i*.1f;status=nav.Tick(world,player,Time.realtimeSinceStartup,out move);if(status==NavStatus.Moving)AIBody.Position+=new float3(move.x,0,move.y)*.1f;}
  Check(status==NavStatus.Arrived&&AIBody.Position.x<4.5f&&AIBody.Position.x>3,id+" actual navigator reaches open approach without entering target footprint");
 }
 public static void Main(){
  RunObject("store",1.6f);RunObject("take",1.6f);RunObject("break",1.2f);
  Reset("wall");for(int x=0;x<=6;x++)AITileReader.Open.Add(T(x,0));AITileReader.Open.Remove(T(4,0));AIObjects.Blocked.Add(T(6,0));
  var nav=new AINavigator();nav.Start(world,player,T(6,0),2.5f,0);var status=nav.Tick(world,player,.1f,out var move);
  Check(status==NavStatus.Failed&&move.Equals(float2.zero),"tolerance cannot create approach through intervening wall");
  Reset("already-near-wall");AITileReader.Open.UnionWith(new[]{T(0,0),T(2,0)});AIObjects.Blocked.Add(T(2,0));
  nav=new AINavigator();nav.Start(world,player,T(2,0),2.5f,0);status=nav.Tick(world,player,.1f,out move);
  Check(status!=NavStatus.Arrived,"already inside numeric tolerance does not claim arrival through wall");
  Reset("corner-wall");AITileReader.Open.UnionWith(new[]{T(0,0),T(1,0),T(1,1),T(2,1),T(2,2)});AIObjects.Blocked.Add(T(2,2));AITravel.ObserveView(world,player,true);
  var route=AITravel.KnownPath(T(0,0),T(2,2),null,1.6f);
  Check(route!=null&&route[route.Count-1].Equals(T(2,1)),"blocked diagonal approach selects real adjacent open face");
  Console.WriteLine($"Navigator production checks: {tests-failed}/{tests}");Environment.ExitCode=failed;
 }
}
