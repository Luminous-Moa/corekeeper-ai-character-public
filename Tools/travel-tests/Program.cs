using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
class Program
{
    static World world=new World(); static Entity player=default;static int failures,checks;
    static void Check(bool ok,string name){checks++;Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
    static int2 T(int x,int y)=>new int2(x,y);
    static AITravelRules.Cell C(int x,int y)=>new AITravelRules.Cell(x,y);
    static void Reset(string name,float half=1,float halfY=-1)
    {
        AIAgent.Cur=new AIAgent(name);AIPlayerSpikeMod.WorldId="world";AIBody.Position=new float3();AIBody.Current=null;
        AITileReader.Open.Clear();AIObjects.Blocked.Clear();AIBrain.Observations.Clear();Time.realtimeSinceStartup=100;AIVision.Half=half;AIVision.HalfY=halfY;
        AIBrain.Goal=new AIGoal { kind=GoalKind.Explore };AIBrain.AreaQuestions=0;
    }
    static List<AICandidate> Choices()
        =>AITravel.Compose(world,player,new AIObservation{pos=AIBody.Position},new List<AICandidate>());
    static BodyInput Tick(TravelSkill skill)
    {
        skill.BeginObservationTick();
        return skill.Tick(world,player,Time.realtimeSinceStartup);
    }
    static void Move(TravelSkill s,int x,int y)
    { AIBody.Position=new float3(x,0,y);Time.realtimeSinceStartup+=1;Tick(s); }
    static TravelSkill Start(int x,int y)
    {var s=new TravelSkill(T(x,y)){Run=1};AIBody.Current=s;s.BindObservationGoal();Check(s.Start(world,player,Time.realtimeSinceStartup),"production corridor starts on selected visible tile");return s;}
    // rows: top row is the largest y; S is placed at (sx,sy); '.' and 'S' are open, everything else is wall when seen
    static void Open(int x,int y)=>AITileReader.Open.Add(T(x,y));
    static TravelSkill StartOn(int2 tile,string key=null)
    {var s=new TravelSkill(tile){Run=1};if(key!=null)s.Key=key;AIBody.Current=s;s.BindObservationGoal();Check(s.Start(world,player,Time.realtimeSinceStartup),"production travel starts on the chosen exit or way");return s;}
    // physical walking: each 0.1 s tick moves the body by the navigator's input (fixture navigator, at most 0.5 tile per axis)
    static void Walk(TravelSkill s,int ticks,Func<bool> until=null)
    {
        for(int i=0;i<ticks;i++)
        {
            Time.realtimeSinceStartup+=0.1f;var st=Tick(s);
            AIBody.Position=new float3(AIBody.Position.x+st.move.x,0,AIBody.Position.z+st.move.y);
            if(until!=null&&until())return;
        }
    }
    static bool Answer(Func<AICandidate,bool> pick)
    {var c=Choices().Find(x=>x.kind=="travel_turn"&&pick(x));return c!=null&&AITravel.TryApplyTurn(world,player,c,out _);}
    static bool Halted(TravelSkill s)=>s.Progress(world,player).StartsWith("멈춰서");
    static void Grid(string[] rows,int sx,int sy)
    {
        int rs=-1,cs=-1;for(int r=0;r<rows.Length;r++){int i=rows[r].IndexOf('S');if(i>=0){rs=r;cs=i;}}
        for(int r=0;r<rows.Length;r++)for(int c=0;c<rows[r].Length;c++)if(rows[r][c]=='.'||rows[r][c]=='S')AITileReader.Open.Add(T(c-cs+sx,(rs-r)+sy));
    }
    static void Main()
    {
        Reset("memory",1);
        for(int x=0;x<=5;x++)AITileReader.Open.Add(T(x,0));
        Choices();
        Check(AITravel.KnownWalkable(T(1,0))&&!AITravel.Known(T(2,0)),"only exact visible cells enter geometry memory");
        Check(AITravel.KnownPath(T(0,0),T(5,0))==null,"unseen shortcut is not a known destination path");
        AIBody.Position=new float3(4,0,0);Time.realtimeSinceStartup+=1;Choices();
        AITileReader.Open.Remove(T(1,0));
        Check(AITravel.KnownWalkable(T(1,0)),"out-of-view change remains last observation, not omniscient update");
        AIBody.Position=new float3(0,0,0);Time.realtimeSinceStartup+=1;Choices();
        Check(!AITravel.KnownWalkable(T(1,0)),"visible closure replaces remembered open geometry");
        AITileReader.Open.Add(T(1,0));Time.realtimeSinceStartup+=1;Choices();
        Check(AITravel.KnownWalkable(T(1,0)),"visible mining/reopening updates blocked memory");
        var original=AIAgent.Cur; AIAgent.Cur=new AIAgent("other");Check(!AITravel.Known(T(1,0)),"character memory isolated");
        AIAgent.Cur=original;AIPlayerSpikeMod.WorldId="other-world";Check(!AITravel.Known(T(1,0)),"world memory isolated");
        AIPlayerSpikeMod.WorldId="world";Check(AITravel.KnownWalkable(T(1,0)),"persisted exact observation reloads for original world");
        AITileReader.Open.Remove(T(1,0));Time.realtimeSinceStartup+=1;Choices();
        AIPlayerSpikeMod.WorldId="another";AITravel.Known(T(1,0));AIPlayerSpikeMod.WorldId="world";
        Check(!AITravel.KnownWalkable(T(1,0)),"world switch flushes recent closure before periodic save");
        var unknownGoto=new GotoSkill(T(99,99),"known name without route");
        Check(!unknownGoto.Start(world,player,Time.realtimeSinceStartup),"destination name alone does not authorize unseen geometry");
        AITileReader.Open.Add(T(1,0));Time.realtimeSinceStartup+=1;Choices();
        AIBody.Position=new float3(4,0,0);Time.realtimeSinceStartup+=1;
        var rememberedGoto=new GotoSkill(T(1,0),"return");
        Check(rememberedGoto.Start(world,player,Time.realtimeSinceStartup)&&rememberedGoto.Facts[0].Contains("현재 상태 미확인"),"known destination uses last observation and labels current state unknown");

        TravelSkill skill;BodyInput step;
        Reset("deadend");AITileReader.Open.UnionWith(new[]{T(0,0),T(1,0)});skill=Start(1,0);Move(skill,1,0);
        Check(Choices().Exists(c=>c.kind=="explore"&&c.tile.Equals(T(0,0))),"dead end exposes explicit return choice");
        Check(skill.Status==SkillStatus.Running,"dead end observation does not complete user's goal");
        Check(!skill.Moving,"a run halted at a dead end is not moving (resource pacing and choice filtering stop)");
        // 2026-11-08 이동·전달 개선 2/4: the exit result survives a new run and a reload, and stays a choice.
        skill.Cleanup(world);AIBody.Current=null;AIBody.Position=new float3(0,0,0);Time.realtimeSinceStartup+=1;
        var again=Choices().Find(c=>c.kind=="explore"&&c.tile.Equals(T(1,0)));
        Check(again!=null&&again.desc.Contains("1번 감")&&again.desc.Contains("막다른 길"),
            "a new run's choice of an already visited dead-end exit states its visit count and dead end");
        skill=Start(1,0);Move(skill,1,0);skill.Cleanup(world);AIBody.Current=null;AIBody.Position=new float3(0,0,0);
        AIPlayerSpikeMod.WorldId="flush";AITravel.Known(T(0,0));AIPlayerSpikeMod.WorldId="world";
        again=Choices().Find(c=>c.kind=="explore"&&c.tile.Equals(T(1,0)));
        Check(again!=null&&again.desc.Contains("2번 감")&&again.desc.Contains("막다른 길"),"exit experience persists across a reload and counts each visit");
        Check(Choices().FindAll(c=>c.kind=="explore").Count==1,"visited exits are described, never removed");
        skill=Start(1,0);skill.Cleanup(world);AIBody.Current=null;AIBody.Position=new float3(0,0,0);
        again=Choices().Find(c=>c.kind=="explore"&&c.tile.Equals(T(1,0)));
        Check(again!=null&&again.desc.Contains("3번 감")&&again.desc.Contains("막다른 길"),"검수 S5: leaving a just-taken exit before a step keeps its earlier dead-end result");
        Reset("v1-file");
        var v1=System.IO.Path.Combine(AIMemory.CharDir("v1world"),"travel-memory.json");
        System.IO.File.WriteAllText(v1,"{\"version\":1,\"xy\":[0,0,1,0],\"walk\":[true,true]}");
        AIPlayerSpikeMod.WorldId="v1world";AITileReader.Open.UnionWith(new[]{T(0,0),T(1,0)});
        Check(AITravel.KnownWalkable(T(1,0))&&Choices().Exists(c=>c.kind=="explore"&&c.desc.Contains("미확인")&&!c.desc.Contains("번 감")),"version 1 travel memory still loads, with no exit experience");

        Reset("loop",8);
        AITileReader.Open.UnionWith(new[]{T(0,0),T(1,0),T(2,0),T(2,1),T(2,2),T(1,2),T(0,2),T(0,1)});
        skill=Start(1,0);Move(skill,1,0);Move(skill,2,0);Move(skill,2,1);Move(skill,2,2);Move(skill,1,2);Move(skill,0,2);Move(skill,0,1);
        AIBody.Position=new float3(0,0,0);Time.realtimeSinceStartup+=1;step=Tick(skill);
        Check(step.move.x==0&&step.move.y==0&&AIBrain.Observations.Exists(s=>s.Contains("순환")),"one-cell loop reports repeated directed edge instead of endless autonomous cycling");
        Check(skill.Status==SkillStatus.Running&&Choices().Exists(c=>c.kind=="explore"),"loop preserves original intent and gives model a fresh explicit direction choice");
        AIBrain.Goal.kind=GoalKind.Gather;
        Check(!Choices().Exists(c=>c.kind=="explore"&&c.progress),"new exploration is not reported as a known gather-work alternative");


        // ---- 공간·출구 단위 이동 (2026-11-08 area-exit-design.md): the seven design cases and their question counts.
        // Asks = area questions (AIBrain.MarkAreaQuestion, sent without the peaceful floor); Calls = every execution
        // observation of the run (each one is a reason to call the action model).
        int Asks()=>AIBrain.AreaQuestions;
        int Calls()=>AIBrain.Observations.Count;
        void Report(string name,int asks,int calls){Console.WriteLine($"AREA CASE {name}: area questions {asks}, execution observations {calls}");foreach(var o in AIBrain.Observations)Console.WriteLine("   obs: "+o);}

        // (1) a plain 13x9 room with three doorways, entered from the south corridor: one question, none while crossing
        Reset("area-room",12);
        for(int x=-6;x<=6;x++)for(int y=1;y<=9;y++)Open(x,y);
        Open(0,0);Open(2,10);Open(7,5);for(int y=-40;y<=-1;y++)Open(0,y);for(int y=11;y<=40;y++)Open(2,y);for(int x=8;x<=40;x++)Open(x,5);
        AIBody.Position=new float3(0,0,-6);
        var cStart=Choices().FindAll(c=>c.kind=="explore");
        Check(cStart.Exists(c=>c.tile.Equals(T(0,-5))),"corridor start: the visible ways on along the corridor");
        skill=StartOn(T(0,-5));
        Walk(skill,6);
        Check(Asks()==1&&AIBrain.Observations.Exists(s=>s.Contains("다음 공간 출구 선택 필요")),"plain room: asked once as soon as the room is in the observed map, before the entrance");
        Walk(skill,6);
        var roomExits=Choices().FindAll(c=>c.kind=="travel_turn");
        Check(roomExits.Count==3&&roomExits.Exists(c=>c.desc.Contains("(들어온 길)")),"plain room: 3 exits (2 doorways + the way in), not one per tile ("+roomExits.Count+")");
        Check(Answer(c=>c.tile.x>=7),"the model's exit answer is reserved while the corridor is walked");
        int asksBefore=Asks(),callsBefore=Calls();
        Walk(skill,200,()=>AIBody.Position.x>=12);
        Check(AIBody.Position.x>=12&&!Halted(skill),"the room is crossed to the chosen exit and its corridor followed");
        Check(Asks()==asksBefore&&Calls()==callsBefore,"crossing the room and following the next corridor asks nothing (expected 0)");
        Report("plain room",Asks(),Calls());

        // (2) a jagged cave behind a width-2 neck: one question for the cave; its bumps are not exits; while the answer is
        // on the way the body walks into the cave (never into an unchosen exit), then crosses when the answer comes
        Reset("area-cave",30);
        Grid(new[]{
            "###########.############",
            "###########.############",
            "#######.###.###..#######",
            "#####..............#####",
            "####.................###",
            "###...................##",
            "##.....................#",
            "#......................#",
            ".......................#",
            "##.....................#",
            "###...................##",
            "####.................###",
            "#####..##.......##..####",
            "#########..##.##########",
            "##########..############",
            "##########..############",
            "##########S.############"},0,0);
        for(int x=-90;x<=-11;x++)Open(x,8);for(int y=17;y<=90;y++)Open(1,y);for(int y=-40;y<0;y++){Open(0,y);Open(1,y);}
        AIBody.Position=new float3(0,0,0);
        skill=StartOn(T(0,1));
        Walk(skill,4);
        Check(Asks()==1,"jagged cave: one question for the cave (expected 1), asked from the neck");
        var caveExits=Choices().FindAll(c=>c.kind=="travel_turn");
        Check(caveExits.Count==3,"jagged cave: 3 exits (neck, west corridor, north corridor), not its wall bumps ("+caveExits.Count+": "+string.Join(" | ",caveExits.ConvertAll(c=>c.desc))+")");
        bool walkedIn=false,intoExit=false;var caveMap=AITravel.Areas(C(0,0),true);
        for(int i=0;i<150;i++)
        {
            Walk(skill,1);var here=AITravel.Cell(AIBody.TileOf(AIBody.Position));
            if(skill.Progress(world,player).Contains("안쪽으로")&&skill.Moving)walkedIn=true;
            if(walkedIn&&caveMap.AreaAt(here)==null)intoExit=true;
        }
        Check(walkedIn&&!intoExit,"waiting for the cave's answer the body walks into the cave, not into an unchosen exit");
        Check(Asks()==1,"walking in while waiting asks nothing more");
        Check(Answer(c=>c.desc.Contains("출구 서")),"the west exit is chosen while waiting");
        Walk(skill,300,()=>AIBody.Position.x<=-20);
        Check(AIBody.Position.x<=-20&&Asks()==1,"the cave is crossed to the west corridor without another question");
        Report("jagged cave",Asks(),Calls());

        // (3) a width-1 T junction: a small area with three exits, asked once
        Reset("area-t",4);
        Grid(new[]{
            ".........",
            "####.####",
            "####.####",
            "####.####",
            "####.####",
            "####S####"},0,-6);
        AIBody.Position=new float3(0,0,-6);
        skill=StartOn(T(0,-5));
        Walk(skill,60);
        var tExits=Choices().FindAll(c=>c.kind=="travel_turn");
        Check(Asks()==1&&tExits.Count==3,"narrow T junction: asked once as a small area with 3 exits ("+Asks()+" questions, "+tExits.Count+" exits)");
        Check(!skill.Moving&&AITravel.Cell(AIBody.TileOf(AIBody.Position)).Equals(C(0,-1)),"a junction has no inside to walk into: the body waits on it, not in an arm");
        Report("narrow T",Asks(),Calls());

        // (4) new exits come into view while crossing: listed, no extra question, same area id
        Reset("area-new-exit",6);
        Grid(new[]{
            "####################",
            "#..................#",
            "#..................#",
            "#S.................#",
            "#..................#",
            "#..................#",
            "##########.#########"},1,-3);
        for(int y=-12;y<=-7;y++)Open(10,y);
        AIBody.Position=new float3(1,0,-3);
        var startChoices=Choices().FindAll(c=>c.kind=="explore");
        var eastSide=startChoices.Find(c=>c.desc.Contains("못 본 쪽")&&c.exploreDirection=="동");
        Check(eastSide!=null&&!startChoices.Exists(c=>c.tile.x==10&&c.tile.y<=-6),"the unseen east side is a start choice; the south doorway is not seen yet");
        skill=StartOn(eastSide.tile,eastSide.key);
        int firstArea=skill.VisitArea,startExits=Choices().FindAll(c=>c.kind=="travel_turn").Count;
        Walk(skill,400,()=>AIBody.Position.x>=9);
        var laterExits=Choices().FindAll(c=>c.kind=="travel_turn");
        Check(Asks()==0&&laterExits.Exists(c=>c.tile.x==10&&c.tile.y<=-6&&c.desc.Contains("통로")),"crossing while more of the room comes into view adds the doorway without a question (expected 0)");
        Check(skill.VisitArea==firstArea&&laterExits.Exists(c=>c.desc.Contains("지금 가는 출구")),"the area keeps its id as it grows, the exit being crossed is marked ("+startExits+" -> "+laterExits.Count+" exits)");
        Report("new exits while crossing",Asks(),Calls());

        // (5) a dead-end room: one question listing its single exit (the way in)
        Reset("area-dead",12);
        for(int x=-3;x<=3;x++)for(int y=1;y<=5;y++)Open(x,y);
        for(int y=-40;y<=0;y++)Open(0,y);
        AIBody.Position=new float3(0,0,-4);
        skill=StartOn(T(0,-3));
        Walk(skill,60);
        var deadExits=Choices().FindAll(c=>c.kind=="travel_turn");
        Check(Asks()==1&&deadExits.Count==1&&deadExits[0].desc.Contains("(들어온 길)"),"dead-end room: one question with its single exit, the way in (expected 1 question, 1 exit)");
        bool stayedIn=true;for(int i=0;i<60;i++){Walk(skill,1);if(AIBody.Position.z<0.6f)stayedIn=false;}
        Check(stayedIn,"waiting in a dead-end room never walks back into its only exit unasked");
        Report("dead-end room",Asks(),Calls());

        // (6) the same area again: room -> corridor -> the same room; same id, visit 2, one question on re-entry with the
        // earlier exit's result
        Reset("area-again",12);
        for(int x=-3;x<=3;x++)for(int y=1;y<=5;y++)Open(x,y);
        Open(4,3);for(int x=5;x<=8;x++)Open(x,3);for(int y=-3;y<=2;y++)Open(8,y);for(int x=0;x<=7;x++)Open(x,-3);Open(0,-2);Open(0,-1);Open(0,0);
        AIBody.Position=new float3(0,0,3);
        var again2=Choices().FindAll(c=>c.kind=="explore");
        var eastDoor=again2.Find(c=>c.tile.x>=4);
        Check(again2.Count==2&&eastDoor!=null&&again2.Exists(c=>c.desc.Contains("이 공간으로 돌아오는 통로")),"a corridor looping back is two exits of the room, described as coming back ("+again2.Count+")");
        skill=StartOn(eastDoor.tile,eastDoor.key);int roomId=skill.VisitArea;
        Check(Asks()==0,"a start choice is itself the exit answer: no question while crossing");
        Walk(skill,300,()=>AIBody.Position.x>=6);Walk(skill,400,()=>AIBody.Position.z>=1.5f&&AIBody.Position.x<1);
        var reExits=Choices().FindAll(c=>c.kind=="travel_turn");
        Check(Asks()==1&&skill.VisitArea==roomId&&AITravel.AreaVisits(roomId)==2,"re-entering the room: the same id, its second visit, one question");
        Check(reExits.Count>0&&reExits[0].desc.Contains("2번째 방문"),"the question's area line (with the first exit choice) says it is the second visit");
        Check(reExits.Exists(c=>c.tile.Equals(eastDoor.tile)&&c.desc.Contains("1번 감")&&c.desc.Contains("공간 "+roomId+"로 이어짐")),"the earlier exit's result is shown on re-entry");
        Check(reExits.Exists(c=>c.desc.Contains("(들어온 길)")&&c.tile.y<=0),"the way in is marked");
        Report("re-entry",Asks(),Calls());
        // travel-memory keeps the known areas, their visits and the exit records (gate + direction) across a reload
        AIPlayerSpikeMod.WorldId="area-again-flush";AITravel.Known(T(0,0));AIPlayerSpikeMod.WorldId="world";
        AIAgent.Cur=new AIAgent("area-again");
        Check(AITravel.AreaVisits(roomId)==2,"travel-memory keeps the known areas and their visits across a reload");
        var saved=System.IO.File.ReadAllText(System.IO.Path.Combine(AIMemory.CharDir("world"),"travel-memory.json"));
        Check(saved.Contains("\"version\":4")&&saved.Contains("g:"+eastDoor.tile.x+","+eastDoor.tile.y+":E"),"travel-memory is written as version 4 with exit records keyed by gate cell and outward direction");

        // TEST ONLY /ai hold (merged ae09acf): a held run neither walks in, crosses, follows nor asks. In play the hold also ends
        // the run (AIBody.Stop hard + HeldTick) and the brain decides afresh after the release; this harness clears only the
        // flag on a live run, to check that the gate itself holds nothing back once cleared.
        Reset("area-hold",12);
        for(int x=-3;x<=3;x++)for(int y=1;y<=5;y++)Open(x,y);
        for(int y=-40;y<=0;y++)Open(0,y);
        AIBody.Position=new float3(0,0,-6);
        skill=StartOn(T(0,-5));
        AITestControl.Held=true;Walk(skill,40);
        Check(Math.Abs(AIBody.Position.z+6)<0.01f&&Asks()==0&&Calls()==0,"while held the travel run stands and asks nothing");
        AITestControl.Held=false;Walk(skill,40);
        Check(Asks()==1&&AIBody.Position.z>-6,"with only the hold flag cleared (no hard stop in this harness) the same run walks on and asks the room once");

        // dead-end corridor and loop in the area analysis (lane-robust following)
        Reset("area-wide-lane",6);
        for(int x=0;x<=30;x++){Open(x,0);Open(x,1);}
        AIBody.Position=new float3(0,0,0);
        skill=StartOn(T(1,0));
        Walk(skill,200,()=>AIBody.Position.x>=20);
        Check(AIBody.Position.x>=20&&!Halted(skill),"a width-2 corridor is followed as one way (its parallel lane is not a fork)");
        Walk(skill,200);
        Check(Halted(skill)&&AIBrain.Observations.Exists(s=>s.Contains("통로 끝")),"its seen end stops the run as a dead end");


        // ---- 최종 검수 G1: the area under the character is relabelled with nothing crossed (split, merge, asked ahead)
        bool Wandering(TravelSkill s)=>s.Progress(world,player).Contains("안쪽으로");
        void Trace(string tag,TravelSkill s){Console.WriteLine($"   [{tag}] asks={Asks()} pos=({AIBody.Position.x:F1},{AIBody.Position.z:F1}) visit={s.VisitArea} {s.Progress(world,player)}");}
        // (a) split (probe2 P2b): room L (7 wide) beside a big room B through a 4-tall waist whose upper wall is first off
        // screen, so L+waist+B are one area; crossing north in L the waist's wall comes into view: B keeps the id, L gets
        // a new one while the character stands in L
        Reset("g1-split",20,6);
        for(int x=-8;x<=-2;x++)for(int y=-5;y<=12;y++)Open(x,y);
        for(int x=2;x<=20;x++)for(int y=-5;y<=12;y++)Open(x,y);
        for(int x=-1;x<=1;x++)for(int y=4;y<=7;y++)Open(x,y);
        for(int y=-30;y<=-6;y++)Open(-5,y);
        AIBody.Position=new float3(-5,0,-14);
        var splitStart=Choices().FindAll(c=>c.kind=="explore").Find(c=>c.tile.y>-14);
        skill=StartOn(splitStart.tile,splitStart.key);
        Walk(skill,400,()=>AIBody.Position.z>=-4);
        int splitAsks=Asks(),splitVisit=skill.VisitArea;
        Check(splitAsks==1&&Answer(c=>c.exploreDirection=="북"&&c.tile.x<0),"G1 split: the room is asked once and its north side chosen");
        bool splitWander=false;int splitRelabel=0;
        // while the body stays in L (x <= -2; walking on through the waist into B afterwards is a real entry)
        int splitNow=splitVisit;
        for(int i=0;i<200&&AIBody.Position.x<=-1.6f&&!Halted(skill);i++){Walk(skill,1);if(Wandering(skill))splitWander=true;if(skill.VisitArea!=splitVisit&&splitRelabel==0){splitRelabel=i;splitNow=skill.VisitArea;}}
        Trace("split",skill);foreach(var o in AIBrain.Observations)Console.WriteLine("   obs: "+o);
        Check(splitRelabel>0,"G1 split: the fixture does relabel the area under the character ("+splitVisit+" -> "+splitNow+")");
        Check(Asks()==splitAsks&&!AIBrain.Observations.Exists(o=>o.Contains("새 공간 출구 선택 필요")),"G1 split: no second question in the same visit");
        Check(!splitWander,"G1 split: the chosen crossing is not dropped for a walk into the 'new' area");
        Check(!Choices().Exists(c=>System.Text.RegularExpressions.Regex.IsMatch(c.desc,"공간 \\d+로 이어짐")),"G1 split: no 'leads into area N' record is written for the relabel");

        // (b) merge (probe P1): room B, a corridor to room L, then L's wide west passage back to B comes into view while
        // crossing L: L and B become one area under B's older id
        Reset("g1-merge",7);
        for(int x=-50;x<=-20;x++)for(int y=0;y<=10;y++)Open(x,y);   // B, its far west part not seen from where the run starts
        for(int x=0;x<=10;x++)for(int y=0;y<=10;y++)Open(x,y);
        for(int x=-19;x<=-1;x++)for(int y=4;y<=8;y++)Open(x,y);
        for(int y=-6;y<=-1;y++)Open(-25,y);for(int x=-25;x<=5;x++)Open(x,-6);for(int y=-5;y<=-1;y++)Open(5,y);
        AIBody.Position=new float3(-25,0,5);
        var mergeSouth=Choices().FindAll(c=>c.kind=="explore").Find(c=>c.tile.y<0);
        skill=StartOn(mergeSouth.tile,mergeSouth.key);int mergeB=skill.VisitArea;
        Walk(skill,600,()=>AIBody.Position.z>=1&&AIBody.Position.x>=4);Walk(skill,40);
        int mergeL=skill.VisitArea,mergeAsks=Asks();
        var lChoices=Choices().FindAll(c=>c.kind=="travel_turn");
        foreach(var c in lChoices)Console.WriteLine("   L choice: "+c.desc);
        // 최종 검수 S3 (probe P1): L was first seen in part from the corridor end and looked like a corridor junction with
        // 6 exits; the area facts reach the model as L is when the call is prepared, not as it was when asked
        Check(!AIBrain.Observations.Exists(o=>o.Contains("통로 갈림길")||o.Contains("출구 6개"))&&lChoices.Count>0&&lChoices[0].desc.Contains("출구 "+lChoices.Count+"개")&&!lChoices[0].desc.Contains("통로 갈림길"),
            "S3: the area line matches the area at preparation ("+(lChoices.Count>0?lChoices[0].desc:"none")+")");
        Check(mergeL!=mergeB&&mergeAsks==1&&Answer(c=>c.desc.Contains("출구 서 못 본 쪽")),"G1 merge: room L is a new area, asked once, its unseen west side chosen");
        bool mergeWander=false;
        Walk(skill,300,()=>{if(Wandering(skill))mergeWander=true;return skill.VisitArea!=mergeL;});
        Walk(skill,10,()=>{if(Wandering(skill))mergeWander=true;return false;});
        Trace("merge",skill);foreach(var o in AIBrain.Observations)Console.WriteLine("   obs: "+o);
        Check(skill.VisitArea==mergeB,"G1 merge: the fixture does merge L into the older area under the character ("+mergeL+" -> "+skill.VisitArea+")");
        Check(Asks()==mergeAsks&&!mergeWander&&skill.Progress(world,player).Contains("건너는"),"G1 merge: joining the older area while crossing asks nothing and keeps the crossing");
        Check(AITravel.AreaVisits(mergeB)==2,"G1 merge: the merged area counts the earlier visit of B and this one ("+AITravel.AreaVisits(mergeB)+")");

        // (c) asked ahead, then the asked area splits before the entrance: the answer already given is kept
        Reset("g1-ahead",20,6);
        for(int x=-8;x<=-2;x++)for(int y=-5;y<=12;y++)Open(x,y);
        for(int x=2;x<=20;x++)for(int y=-5;y<=12;y++)Open(x,y);
        for(int x=-1;x<=1;x++)for(int y=-5;y<=-2;y++)Open(x,y);
        for(int y=-30;y<=-6;y++)Open(-5,y);
        AIBody.Position=new float3(-5,0,-20);
        var aheadStart=Choices().FindAll(c=>c.kind=="explore").Find(c=>c.tile.y>-20);
        skill=StartOn(aheadStart.tile,aheadStart.key);
        Walk(skill,400,()=>Asks()>0);
        Trace("ahead asked",skill);
        foreach(var c in Choices().FindAll(c=>c.kind=="travel_turn"))Console.WriteLine("   ahead choice: "+c.desc);
        int aheadId=skill.AskedArea;
        Check(Asks()==1&&AIBody.Position.z<=-7.5f&&Answer(c=>c.desc.Contains("출구 서 ")),"G1 ahead: the next area is asked from the corridor and answered before the entrance");
        // a slower walk up to the entrance: the waist's upper wall is seen and the analysis split before the entrance
        Move(skill,-5,-7);Move(skill,-5,-7);
        Check(skill.VisitArea==0&&skill.AskedArea!=aheadId&&skill.AskedArea!=0&&Asks()==1,"G1 ahead: the asked area got another id before the entrance and is not asked again ("+aheadId+" -> "+skill.AskedArea+")");
        bool aheadWander=false,aheadWait=false;var aheadIds=new HashSet<int>();
        for(int i=0;i<200;i++){Walk(skill,1);if(Wandering(skill))aheadWander=true;if(skill.Progress(world,player).StartsWith("출구 선택을 기다림"))aheadWait=true;if(skill.VisitArea!=0)aheadIds.Add(skill.VisitArea);}
        Trace("ahead",skill);foreach(var o in AIBrain.Observations)Console.WriteLine("   obs: "+o);
        Check(Asks()==1&&!aheadWander&&!aheadWait,"G1 ahead: the asked area relabelled before the entrance is not asked again and its answer is taken");

        // ---- 최종 검수 G2 (probe P4): a 61x25 open room, view half 6, the north unseen side chosen. The bearing stays north:
        // the body may not drift north-east, east, south-east... from edge to edge; when the north wedge is seen it stops
        Reset("g2-drift",6);
        for(int x=-30;x<=30;x++)for(int y=-4;y<=20;y++)Open(x,y);
        AIBody.Position=new float3(0,0,0);
        var g2North=Choices().FindAll(c=>c.kind=="explore").Find(c=>c.exploreDirection=="북"&&c.desc.Contains("못 본 쪽"));
        skill=StartOn(g2North.tile,g2North.key);
        float g2MaxX=0,g2MinZ=0,g2MaxZ=0;
        for(int i=0;i<600&&!Halted(skill);i++)
        {
            Walk(skill,1);
            g2MaxX=Math.Max(g2MaxX,Math.Abs(AIBody.Position.x));g2MaxZ=Math.Max(g2MaxZ,AIBody.Position.z);
            if(AIBody.Position.z<g2MaxZ-0.01f)g2MinZ=Math.Min(g2MinZ,AIBody.Position.z-g2MaxZ);
        }
        Trace("g2",skill);foreach(var o in AIBrain.Observations)Console.WriteLine("   obs: "+o);
        Check(g2MaxZ>=15,"G2: the chosen north side is walked to its end (max y "+g2MaxZ+")");
        Check(g2MaxX<=9.5f&&g2MinZ>=-1,"G2: the bearing stays north — no drift to the east/west edges or back south (max |x| "+g2MaxX+", largest step back "+(-g2MinZ)+")");
        Check(Halted(skill)&&AIBrain.Observations.Exists(o=>o.Contains("고른 못 본 쪽이 다 보였고")),"G2: when the north side is seen to its end the run stops and asks");
        Check(Asks()==0,"G2: no area question while the chosen side is crossed");

        // ---- G2 passage on (사용자 결정 2026-11-08): when the chosen unseen side is seen to its end, only passages whose
        // gate is in the chosen 45° wedge (±22.5°, from the same fixed origin) count; a neighbouring wedge's passage does
        // not. A 21x19 room, view 12 wide x 4 tall, north chosen from the origin (0,0); dead-end corridors leave the north
        // wall at x=0 (north wedge, gate bearing 0°) and/or the east wall at y=10 (north-east wedge, gate bearing ~45°)
        TravelSkill G2Wedge(string name,bool north,bool northEast)
        {
            Reset(name,12,4);
            for(int x=-10;x<=10;x++)for(int y=-4;y<=14;y++)Open(x,y);
            if(north)for(int y=15;y<=25;y++)Open(0,y);
            if(northEast)for(int x=11;x<=22;x++)Open(x,10);
            AIBody.Position=new float3(0,0,0);
            var pick=Choices().FindAll(c=>c.kind=="explore").Find(c=>c.exploreDirection=="북"&&c.desc.Contains("못 본 쪽"));
            var s=StartOn(pick.tile,pick.key);
            Walk(s,600,()=>Halted(s)||AIBody.Position.z>=18);
            Trace(name,s);foreach(var o in AIBrain.Observations)Console.WriteLine("   obs: "+o);
            return s;
        }
        bool SideSeenNone()=>AIBrain.Observations.Exists(o=>o.Contains("고른 못 본 쪽이 다 보였고 그쪽으로 이어지는 통로가 없음"));
        bool SideSeenAny()=>AIBrain.Observations.Exists(o=>o.Contains("고른 못 본 쪽이 다 보였고"));
        skill=G2Wedge("g2-wedge-neighbour",false,true);
        Check(Halted(skill)&&SideSeenNone()&&AIBody.Position.x<10.5f&&Asks()==0,
            "G2 passage on: a passage only in the neighbouring (north-east) wedge is not counted — the run stops and asks ('통로가 없음'), it does not enter it ("+AIBody.Position.x+","+AIBody.Position.z+")");
        skill=G2Wedge("g2-wedge-one",true,false);
        Check(!Halted(skill)&&!SideSeenAny()&&AIBody.Position.z>=18&&Math.Abs(AIBody.Position.x)<1&&Asks()==0,
            "G2 passage on: the one passage in the chosen (north) wedge is the way on — the run goes into it without asking ("+AIBody.Position.x+","+AIBody.Position.z+")");
        skill=G2Wedge("g2-wedge-one-and-neighbour",true,true);
        Check(!Halted(skill)&&!SideSeenAny()&&AIBody.Position.z>=18&&Math.Abs(AIBody.Position.x)<1&&Asks()==0,
            "G2 passage on: with one passage in the chosen wedge and one in the neighbouring wedge, only the chosen wedge's counts — it goes on into that one, not 'several' ("+AIBody.Position.x+","+AIBody.Position.z+")");

        // ---- 최종 검수 S4: standing on a cell the memory holds as blocked (an object cell) — the analysis from there is
        // empty; it used to be re-run on every call (every frame). Now: not again from the same cell with an unchanged map,
        // and from new cells at most every OutsideSeconds
        Reset("s4-blocked",6);
        for(int x=-6;x<=6;x++)for(int y=-6;y<=6;y++)Open(x,y);
        for(int x=0;x<=5;x++)AIObjects.Blocked.Add(T(x,0));
        AIBody.Position=new float3(0,0,0);Choices();
        Check(!AITravel.KnownWalkable(T(0,0)),"S4: the object cell under the character is remembered as blocked");
        int s4Before=AITravel.AnalysisCount;
        for(int i=0;i<100;i++){Time.realtimeSinceStartup+=0.01f;AITravel.Areas(C(0,0));}
        int s4Same=AITravel.AnalysisCount-s4Before;
        s4Before=AITravel.AnalysisCount;
        for(int i=0;i<100;i++){Time.realtimeSinceStartup+=0.01f;AITravel.Areas(C(i/20,0));}   // 1 s over 5 blocked cells
        int s4Walk=AITravel.AnalysisCount-s4Before;
        Check(s4Same<=1,"S4: 100 calls in 1 s on the same blocked cell re-run the analysis at most once ("+s4Same+")");
        Check(s4Walk<=(int)(1f/AITravel.OutsideSeconds)+1,"S4: 100 calls in 1 s across 5 blocked cells re-run it at most every "+AITravel.OutsideSeconds+" s ("+s4Walk+")");

        // (7) performance: a 97x97 window, open and cave-like, analysis + id assignment
        var openAll=new Func<AITravelRules.Cell,bool?>(c=>true);
        var noisy=new Func<AITravelRules.Cell,bool?>(c=>((c.X*7+c.Y*13)%11==0||(c.X*3-c.Y*5)%17==0)?false:true);
        var rooms=new Func<AITravelRules.Cell,bool?>(c=>{int mx=((c.X%16)+16)%16,my=((c.Y%12)+12)%12;bool wall=(mx==0&&my!=6)||(my==0&&mx!=8);return !wall;});
        double Measure(Func<AITravelRules.Cell,bool?> f,bool assign)
        {
            var tracker=new AreaTracker();AIAreaRules.Analyze(C(1,1),f);
            var sw=System.Diagnostics.Stopwatch.StartNew();
            for(int i=0;i<30;i++){var m=AIAreaRules.Analyze(C(1+i%3,1),f);if(assign)tracker.Assign(m);}
            sw.Stop();return sw.Elapsed.TotalMilliseconds/30;
        }
        var seenMap=new Dictionary<AITravelRules.Cell,bool>();for(int x=-60;x<=60;x++)for(int y=-60;y<=60;y++)seenMap[C(x,y)]=noisy(C(x,y))==true;
        var blocks=new ObservedGrid();foreach(var kv in seenMap)blocks.Set(kv.Key,kv.Value);
        double memAs;{var tracker=new AreaTracker();AIAreaRules.Analyze(C(1,1),blocks);var sw=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<30;i++)tracker.Assign(AIAreaRules.Analyze(C(1+i%3,1),blocks));sw.Stop();memAs=sw.Elapsed.TotalMilliseconds/30;}
        var viaBlocks=AIAreaRules.Analyze(C(1,1),blocks);var viaFunc=AIAreaRules.Analyze(C(1,1),noisy);
        Check(viaBlocks.Areas.Count==viaFunc.Areas.Count&&viaBlocks.Connectors.Count==viaFunc.Connectors.Count&&viaBlocks.Cells==viaFunc.Cells,"the block copy of the observed map gives the same analysis as reading cell by cell");
        double openMs=Measure(openAll,false),noisyMs=Measure(noisy,false),roomsMs=Measure(rooms,false),openAs=Measure(openAll,true),noisyAs=Measure(noisy,true),roomsAs=Measure(rooms,true);
        Console.WriteLine($"AREA ANALYSIS 97x97 (ms, analysis / +id assignment): open {openMs:F2}/{openAs:F2}, noisy cave {noisyMs:F2}/{noisyAs:F2}, rooms grid {roomsMs:F2}/{roomsAs:F2}, noisy cave read from the observed-map blocks (as in play) +ids {memAs:F2} (target < 3 ms)");
        Check(openAs<10&&noisyAs<10&&roomsAs<10&&memAs<10,"area analysis of a full 97x97 window stays within a few milliseconds");

        // no read beyond the observed map: cells beyond view never enter the map or the analysis
        Reset("area-no-offscreen",3);
        Grid(new[]{"S.........."},0,0);
        AIBody.Position=new float3(0,0,0);
        Choices();
        Check(!AITravel.Known(T(6,0))&&!AITravel.Known(T(10,0))&&AITravel.Areas(C(0,0),true).ConnectorAt(C(6,0))==null,"cells beyond view are never read into the map or the area analysis");
        // memory v3: v2 exit keys migrate to gates; areas persist with their visits
        Reset("v2-file");
        var v2=System.IO.Path.Combine(AIMemory.CharDir("v2world"),"travel-memory.json");
        System.IO.File.WriteAllText(v2,"{\"version\":2,\"xy\":[0,0,1,0],\"walk\":[true,true],\"exitKeys\":[\"0,0>1,0\"],\"exitVisits\":[2],\"exitSteps\":[1],\"exitSeq\":[3],\"exitOutcomes\":[\"막다른 길\"]}");
        AIPlayerSpikeMod.WorldId="v2world";AITileReader.Open.UnionWith(new[]{T(0,0),T(1,0)});
        Check(Choices().Exists(c=>c.kind=="explore"&&c.desc.Contains("2번 감")&&c.desc.Contains("막다른 길")),"version 2 exit records migrate to gate cells");
        // 최종 검수 G3: a v3 record ("g:x,y", no direction) cannot tell the way out from the way back: the file still
        // reads (map kept), that record is dropped
        Reset("v3-file");
        var v3=System.IO.Path.Combine(AIMemory.CharDir("v3world"),"travel-memory.json");
        System.IO.File.WriteAllText(v3,"{\"version\":3,\"xy\":[0,0,1,0],\"walk\":[true,true],\"exitKeys\":[\"g:1,0\"],\"exitVisits\":[2],\"exitSteps\":[1],\"exitSeq\":[3],\"exitOutcomes\":[\"막다른 길\"],\"areaIds\":[],\"areaAnchors\":[],\"areaVisits\":[],\"areaSeq\":[]}");
        AIPlayerSpikeMod.WorldId="v3world";AITileReader.Open.UnionWith(new[]{T(0,0),T(1,0)});
        Check(AITravel.KnownWalkable(T(1,0))&&Choices().Exists(c=>c.kind=="explore"&&c.tile.Equals(T(1,0))&&!c.desc.Contains("번 감")),"최종 검수 G3: a version 3 file still reads; its direction-less exit record is dropped");
        // 최종 검수 G3 (probe3 P5): a corridor start just south of a room's doorway records 'north into area 1' on that
        // cell; the room's own south exit (the way back out) must not show it as its fact
        Reset("g3-reverse",12);
        for(int x=-3;x<=3;x++)for(int y=1;y<=7;y++)Open(x,y);
        for(int y=-20;y<=0;y++)Open(0,y);
        AIBody.Position=new float3(0,0,-2);
        var g3Up=Choices().Find(c=>c.kind=="explore"&&c.tile.Equals(T(0,-1)));
        skill=StartOn(g3Up.tile,g3Up.key);
        Walk(skill,200,()=>AIBody.Position.z>=4);Walk(skill,30);
        skill.Cleanup(world);AIBody.Current=null;Time.realtimeSinceStartup+=1;
        var g3Room=Choices().FindAll(c=>c.kind=="explore");
        var g3South=g3Room.Find(c=>c.exploreDirection=="남");
        Check(g3South!=null&&!g3South.desc.Contains("번 감")&&!g3South.desc.Contains("로 이어짐"),"최종 검수 G3: the room's south exit does not carry the corridor's 'into this room' record ("+(g3South?.desc??"none")+")");
        AIBody.Position=new float3(0,0,-2);Time.realtimeSinceStartup+=1;
        var g3Back=Choices().Find(c=>c.kind=="explore"&&c.tile.Equals(T(0,-1)));
        Check(g3Back!=null&&g3Back.desc.Contains("1번 감")&&g3Back.desc.Contains("로 이어짐"),"최종 검수 G3: the corridor start northward keeps its own true record ("+(g3Back?.desc??"none")+")");

        var map=new Dictionary<AITravelRules.Cell,bool>();for(int i=0;i<600;i++)map[C(i,0)]=true;
        var known=AITravelRules.KnownPath(C(0,0),C(599,0),map);
        Check(known!=null&&known.Count==599,"known destination path has no distance cap");
        map[C(200,0)]=false;Check(AITravelRules.KnownPath(C(0,0),C(599,0),map)==null,"remembered blockage prevents fabricated route");
        Console.WriteLine($"Travel production checks: {checks-failures}/{checks}");Environment.ExitCode=failures;
    }
}
