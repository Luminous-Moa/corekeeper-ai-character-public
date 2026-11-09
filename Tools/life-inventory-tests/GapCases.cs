using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
// 2026-11-08 빈 선택지 구현(가방·장비): pouch inventory, carried move/split/merge/swap, worn-slot direct store/unequip,
// chest gear direct equip, use, bag/chest bulk requests and the equipment preset switch. Fixture adapters only.
static partial class Program {
static (World,Entity,DynamicBuffer<ContainedObjectsBuffer>) PouchSetup(){var(w,p,b)=Setup();for(int i=0;i<10;i++)b.Add(default);var iv=w.EntityManager.GetBuffer<InventoryBuffer>(p);iv.Add(new(){startIndex=70,size=5,sizeX=10,sizeY=0,maxSize=10,extraSize=5,extraInventorySizeSlot=49});b[49]=Item(ObjectID.Pouch);return(w,p,b);}
static void GapCases(){
 var(w,p,b)=PouchSetup();
 Check(AIInventory.Pouches(w,p).Count==1&&AIInventory.IsCarried(w,p,72)&&!AIInventory.IsMain(w,p,72),"gap B1: a worn pouch inventory is a carried slot range outside main");
 b[72]=Item(ObjectID.CopperOre,0,7);
 var moves=AIInventory.MenuMoves(w,p,p,72,"move",default,default,"",7);
 Check(moves.Any(m=>m.to==12&&!m.swap&&m.count==7),"gap B1/B8: pouch item can move its whole stack to an empty bag slot");
 var skill=Start(w,p,moves.First(m=>m.to==12));
 Check(AICraft.Requests.Last().mode=="move"&&AICraft.Requests.Last().from==72&&AICraft.Requests.Last().to==12&&AICraft.Requests.Last().count==7,"gap B1: pouch move is one MoveAmount request");
 b[12]=b[72];b[72]=default;skill.Tick(w,p,1);
 Check(skill.Status==SkillStatus.Done&&skill.ConfirmedWork==7,"gap B1: observed both-side change completes the pouch move");

 (w,p,b)=PouchSetup();b[12]=Item(ObjectID.Wood,0,20);b[13]=Item(ObjectID.Wood,0,3);b[14]=Item(ObjectID.CopperSword);
 Check(AIInventory.MenuMoves(w,p,p,12,"move",default,default,"",5).Any(m=>m.to==15&&!m.swap&&m.count==5),"gap B8: split five of twenty into an empty slot");
 Check(AIInventory.MenuMoves(w,p,p,12,"move",default,default,"",5).Any(m=>m.to==13&&!m.swap),"gap B8: merge into the same stack");
 var swap=AIInventory.MenuMoves(w,p,p,12,"move",default,default,"",1).FirstOrDefault(m=>m.to==14);
 Check(swap!=null&&swap.swap&&AIInventory.MenuMoves(w,p,p,12,"move",default,default,"",5).All(m=>m.to!=14),"gap B8: another item is only a whole-slot swap");
 Check(AIInventory.MenuMoves(w,p,p,12,"move",default,default,"",5).Any(m=>m.to==71),"gap B1/B8: an empty pouch slot is a move destination");

 (w,p,b)=PouchSetup();b[40]=Item(ObjectID.CopperHelm);
 Check(AIInventory.MenuMoves(w,p,p,40,"unequip",default,default,"",1).Any(m=>m.to==70),"gap B7: unequip may go to an empty pouch slot");
 var(chest,box)=Chest(w);AIInventory.Opened(chest);
 var store=AIInventory.MenuMoves(w,p,p,40,"store",chest,default,"",1);
 Check(store.Count==8&&store.All(m=>m.swap&&m.toEntity==chest),"gap B7: a worn item is stored directly as a whole-slot swap into each empty chest slot");
 skill=Start(w,p,store[0]);
 Check(AICraft.Requests.Last().mode=="swap"&&AICraft.Requests.Last().fromEntity==p&&AICraft.Requests.Last().toEntity==chest,"gap B7: direct store queues one player→chest Swap");
 Swap(w,store[0]);skill.Tick(w,p,1);
 Check(skill.Status==SkillStatus.Done&&skill.ConfirmedWork==1&&b[40].objectID==ObjectID.None,"gap B7: observed exchange completes the direct store");

 (w,p,b)=Setup();(chest,box)=Chest(w);AIInventory.Opened(chest);box[3]=Item(ObjectID.CopperHelm);
 var wear=AIInventory.MenuMoves(w,p,chest,3,"equip",chest,default,"",1);
 Check(wear.Count==1&&wear[0].to==40&&wear[0].swap&&wear[0].fromEntity==chest,"gap B7: chest gear can be worn directly");
 var hold=AIInventory.MenuMoves(w,p,chest,3,"hold",chest,default,"",1);
 Check(hold.Count==10&&hold.All(m=>m.fromEntity==chest&&m.to<10),"gap B7: chest item can be held directly into each hotbar slot");
 skill=Start(w,p,wear[0]);Swap(w,wear[0]);skill.Tick(w,p,1);
 Check(skill.Status==SkillStatus.Done&&b[40].objectID==ObjectID.CopperHelm,"gap B7: chest→head swap observed as worn");

 (w,p,b)=Setup();b[3]=Item(ObjectID.Mushroom,0,5);b[15]=Item(ObjectID.Guitar);
 var use=AIInventory.MenuMoves(w,p,p,3,"use",default,default,"",1);
 Check(use.Any(m=>m.from==3&&m.to==3)&&AIInventory.MenuMoves(w,p,p,12,"use",default,default,"",1).Count==0,"gap B4: eatable in the hotbar is usable in place; empty slot has no use");
 skill=Start(w,p,use.First(m=>m.to==3));var input=skill.Tick(w,p,0.1f);input=skill.Tick(w,p,0.6f);
 Check(input.slot==3&&input.secondary&&!input.primary,"gap B4: eat holds the slot and the secondary use");
 b[3]=Item(ObjectID.Mushroom,0,4);skill.Tick(w,p,0.8f);
 Check(skill.Status==SkillStatus.Done,"gap B4: observed count decrease completes the use");
 var play=AIInventory.MenuMoves(w,p,p,15,"use",default,default,"",1).First(m=>m.to==5);
 skill=Start(w,p,play);Swap(w,play);skill.Tick(w,p,0.05f);input=skill.Tick(w,p,0.5f);
 Check(input.slot==5&&input.primary&&!input.secondary,"gap B4: an instrument moves to the hotbar and gets one primary press");
 skill.Tick(w,p,1.0f);Check(skill.Status==SkillStatus.Done,"gap B4: instrument use completes after the press without claiming a sound result");

 (w,p,b)=Setup();b[12]=Item(ObjectID.Wood,0,20);b[13]=Item(ObjectID.CopperSword);
 var bag=AIInventoryBulk.Moves(w,p,"bag",default);
 Check(bag.Any(m=>m.mode=="sort_bag"),"gap B2: bag sort is offered with two bag items");
 Check(!bag.Any(m=>m.mode=="quickstack_nearby"),"gap B2: quick stack is not offered without a nearby chest holding the same kind");
 skill=Start(w,p,bag.First(m=>m.mode=="sort_bag"));
 Check(AICraft.Requests.Last().mode=="sort","gap B2: sort is one game Sort request");
 (b[12],b[13])=(b[13],b[12]);skill.Tick(w,p,1);
 Check(skill.Status==SkillStatus.Done,"gap B2: an observed slot change completes the sort");
 (chest,box)=Chest(w);box[0]=Item(ObjectID.Wood,0,5);
 Check(AIInventoryBulk.Moves(w,p,"bag",default).Any(m=>m.mode=="quickstack_nearby"),"gap B2: quick stack to nearby chests is offered when a near chest holds the kind");
 Check(AIInventoryBulk.Moves(w,p,"chest",chest).Count==0,"gap B2: opened-chest bulk needs the chest opened first");
 AIInventory.Opened(chest);
 var qs=AIInventoryBulk.Moves(w,p,"chest",chest).FirstOrDefault(m=>m.mode=="quickstack_chest");
 Check(qs!=null,"gap B2: opened chest offers quick stack of the same kind");
 skill=Start(w,p,qs);Check(AICraft.Requests.Last().mode=="quickstack"&&AICraft.Requests.Last().toEntity==chest,"gap B2: quick stack request names the opened chest");
 box[0]=Item(ObjectID.Wood,0,25);b[13]=default;skill.Tick(w,p,1);
 Check(skill.Status==SkillStatus.Done&&skill.ConfirmedWork==20,"gap B2: moved carried units are counted from the observed change");

 (w,p,b)=Setup();var presets=new DynamicBuffer<EquipmentPresetsBuffer>();var e0=w.EntityManager.GetComponentData<EquipmentCD>(p);var e1=e0;e1.helmSlotIndex=60;presets.Add(new(){equipment=e0});presets.Add(new(){equipment=e1});presets.Add(new(){equipment=e0});w.EntityManager.PutBuffer(p,presets);w.EntityManager.Put(p,new ActiveEquipmentPresetCD{Value=0});b[60]=Item(ObjectID.CopperHelm);
 var ps=AIInventoryBulk.Moves(w,p,"presets",default);
 Check(ps.Count==2&&ps.Any(m=>m.preset==1&&m.label.Contains("CopperHelm")),"gap B3: other presets are offered with their worn items visible");
 skill=Start(w,p,ps.First(m=>m.preset==1));input=skill.Tick(w,p,0.1f);
 Check(input.presetAsk==2,"gap B3: the switch is the preset input, not an inventory request");
 w.EntityManager.Put(p,new ActiveEquipmentPresetCD{Value=1});skill.Tick(w,p,0.3f);
 Check(skill.Status==SkillStatus.Done,"gap B3: observed active preset completes the switch");
 Check(AIInventory.GearSlots(w,p).Exists(x=>x.name=="head"&&x.slot==60),"gap B3: after the switch the active gear list reads the new preset");

 (w,p,b)=Setup();b[12]=Item(ObjectID.UnknownThing,0,3);(chest,box)=Chest(w);AIInventory.Opened(chest);
 Check(AIInventory.MenuMoves(w,p,p,12,"move",default,default,"",1).Any(m=>m.swap&&m.to==13)&&AIInventory.MenuMoves(w,p,p,12,"store",chest,default,"",1).Any(m=>m.swap&&m.toEntity==chest),"gap B9: an item without DB info still moves and stores as a whole slot");
 Check(AIInventory.MenuMoves(w,p,p,12,"drop",default,default,"",1).Count==0&&AIInventory.MenuMoves(w,p,p,12,"move",default,default,"",1).All(m=>m.swap),"gap B9: no amount-based move or drop is guessed for an unknown item");

 (w,p,b)=Setup();w.EntityManager.Put(p,new PetOwnerCD{SlotIndex=55});w.EntityManager.Put(p,new VanitySlotsCD{helmVanitySlotIndex=56,breastVanitySlotIndex=57,pantsVanitySlotIndex=58});b[12]=Item(ObjectID.PetDog);b[13]=Item(ObjectID.CopperHelm);
 Check(AIInventory.MenuMoves(w,p,p,12,"equip",default,default,"",1).Any(m=>m.to==55&&m.destination=="pet"),"gap B5: a pet can be equipped into the pet slot");
 Check(AIInventory.MenuMoves(w,p,p,13,"equip",default,default,"",1).Any(m=>m.to==56&&m.destination=="vanity_head"),"gap B6: a helm can go into the vanity head slot as its own choice");
 Goal(w,p,new(){kind=GoalKind.Equip,item=ObjectID.CopperHelm,slot=13,count=1});
 Check(AIInventory.Candidates(w,p,Observe(w,p),"").All(c=>c.inventory.destination!="vanity_head"),"gap B6: an equip goal without a named slot never counts the vanity slot");
 WarehouseCases();
}
// Review-4: every goal path over a 300-chest warehouse — packed into one area or spread over many — keeps one decision
// step far below 255 chest choices (the shared rule: ≤40 or one per area), and every chest stays reachable (the life tree
// walk below). The live chest is at (1,0); slot + area where reaches the exact move after it is opened (F1).
static void WarehouseLayout(bool packed){
 string tag=packed?"packed":"spread";
 var(w,p,b)=Setup();var(chest,box)=Chest(w);box[0]=Item(ObjectID.Wood,0,10);b[12]=Item(ObjectID.Wood,0,7);
 for(int i=0;i<299;i++){var t=packed?new int2(3+(i%20)*3,2+(i/20)*4):new int2(-600+(i%30)*40,-600+(i/30)*40);AIBase.Memories.Add(new(){id=ObjectID.Chest,tile=t});}
 var all=AIBase.OwnChests();var top=AIChestAreas.Split(all,AIInventory.ChestTile,null);
 string area=top.Single(a=>a.items.Any(m=>m.tile.Equals(new int2(1,0)))).id;
 int Count(AIGoal g){Goal(w,p,g);return AIInventory.Candidates(w,p,Observe(w,p),"").Count;}
 int nArea=Count(new(){kind=GoalKind.Take,item=ObjectID.Wood,count=3,where=area});
 int nAny=Count(new(){kind=GoalKind.Take,item=ObjectID.Wood,count=3});
 int nStore=Count(new(){kind=GoalKind.Store,item=ObjectID.Wood,count=2});
 Check(nArea<=AIChestAreas.SplitAbove&&nAny<=AIChestAreas.SplitAbove&&nStore<=AIChestAreas.SplitAbove,$"review-4 F2 ({tag}): area-where take {nArea}, no-where take {nAny}, store {nStore} chest choices, each within {AIChestAreas.SplitAbove} (one decision far below 255)");
 Goal(w,p,new(){kind=GoalKind.Take,item=ObjectID.Wood,count=3,where=area});
 var inspect=AIInventory.Candidates(w,p,Observe(w,p),"").FirstOrDefault(c=>c.inventory?.mode=="inspect"&&c.inventory.chest==chest);
 Check(inspect!=null,$"review-4 F2 ({tag}): the live chest in that area is offered (open chest first, then nearest)");
 Start(w,p,inspect.inventory);
 Check(AIInventory.Candidates(w,p,Observe(w,p),"").Any(c=>c.inventory?.mode=="take"&&c.inventory.fromEntity==chest),$"review-4 F2 ({tag}): after opening, the real take from that chest is offered");
 Goal(w,p,new(){kind=GoalKind.Store,item=ObjectID.Wood,slot=12,count=7,where=area});
 var exact=AIInventory.Candidates(w,p,Observe(w,p),"").FirstOrDefault(c=>c.inventory?.mode=="store"&&c.inventory.from==12&&c.inventory.toEntity==chest);
 Check(exact!=null,$"review-4 F1 ({tag}): slot + area where gives the exact store into the open chest inside that area");
 // Review-5 G1: the live chest is opened and found full; its small area hands over to another chest at the next decision.
 (w,p,b)=Setup();(chest,box)=Chest(w);for(int i=0;i<box.Length;i++)box[i]=Item(ObjectID.CopperOre,0,9999);b[12]=Item(ObjectID.Wood,0,7);
 for(int i=0;i<299;i++){var t=packed?new int2(3+(i%20)*3,2+(i/20)*4):new int2(-600+(i%30)*40,-600+(i/30)*40);AIBase.Memories.Add(new(){id=ObjectID.Chest,tile=t});}
 var live=AIBase.Memories[0];
 Goal(w,p,new(){kind=GoalKind.Store,item=ObjectID.Wood,slot=12,count=7,where=area});
 var first=AIInventory.Candidates(w,p,Observe(w,p),"").FirstOrDefault(c=>c.inventory?.mode=="inspect"&&c.inventory.chest==chest);
 Check(first!=null,$"review-5 G1 ({tag}): before opening, the unopened live chest represents its small area");
 Start(w,p,first.inventory);live.openedUnix=1;live.free=0;live.slots=box.Length;live.items[ObjectID.CopperOre]=9999*box.Length;
 var after=AIInventory.Candidates(w,p,Observe(w,p),"");
 var scopeAll=AIChestAreas.Within(AIBase.OwnChests(),AIInventory.ChestTile,area);var subs=AIChestAreas.Split(scopeAll,AIInventory.ChestTile,area);
 var mine=subs==null?scopeAll:subs.Single(a=>a.items.Any(m=>m.tile.Equals(new int2(1,0)))).items;
 bool handed=mine.Any(m=>!m.tile.Equals(new int2(1,0))&&after.Any(c=>c.key==$"dig:{m.tile.x},{m.tile.y}"||c.inventory?.chest!=null&&c.inventory.chest!=chest&&c.key.Contains("inspect")));
 Check(mine.Count<=1||handed,$"review-5 G1 ({tag}): after the live chest is seen full, another chest of its small area ({mine.Count} chests) is offered instead");
 Check(!after.Any(c=>c.inventory?.mode=="inspect"&&c.inventory.chest==chest),$"review-5 G1 ({tag}): the full open chest no longer represents its area");
 // every chest through the life tree (take branch), each step within the split size
 var reached=new System.Collections.Generic.HashSet<string>();int widest=0;var queue=new System.Collections.Generic.Queue<string>();queue.Enqueue("");
 while(queue.Count>0){var where=queue.Dequeue();var entries=AIInventory.LifeEntries(w,p,Observe(w,p),"",new LifeSelection{Branch="inventory",Mode="take",Where=where});widest=System.Math.Max(widest,entries.Count);foreach(var e in entries){if(e.Op!="where")continue;if(e.Id.StartsWith("area:"))queue.Enqueue(e.Id);else reached.Add(e.Id);}}
 Check(reached.Count==300&&widest<=AIChestAreas.SplitAbove,$"review-4 ({tag}): the life tree reaches all 300 chests, widest step {widest}");
}
// Review I3: a 300-chest shared warehouse is reached through area steps; no step is near the 255-choice request limit, no
// chest is cut, and a few chests are still listed directly.
static void WarehouseCases(){
 var(w,p,b)=Setup();
 for(int i=0;i<300;i++)AIBase.Memories.Add(new(){id=ObjectID.Chest,tile=new(-20+(i%25)*3,-10+(i/25)*4)});
 var reached=new System.Collections.Generic.HashSet<string>();int widest=0,steps=0;
 var queue=new System.Collections.Generic.Queue<string>();queue.Enqueue("");
 while(queue.Count>0){
  var where=queue.Dequeue();steps++;
  var entries=AIInventory.LifeEntries(w,p,Observe(w,p),"",new LifeSelection{Branch="inventory",Mode="take",Where=where});
  widest=System.Math.Max(widest,entries.Count);
  foreach(var e in entries){if(e.Op!="where")continue;if(e.Id.StartsWith("area:"))queue.Enqueue(e.Id);else reached.Add(e.Id);}
 }
 Check(widest<=AIChestAreas.SplitAbove&&steps>1,"review I3: every life storage step for a 300-chest warehouse stays within "+AIChestAreas.SplitAbove+" choices (widest "+widest+")");
 Check(reached.Count==300,"review I3: all 300 chests stay reachable through the area tree (reached "+reached.Count+")");
 var top=AIInventory.LifeEntries(w,p,Observe(w,p),"",new LifeSelection{Branch="inventory",Mode="take"});
 Check(top.All(e=>e.Id.StartsWith("area:")&&e.Text.Contains("상자 묶음")),"review I3: the first step names areas with their chest count, not chests");
 Check(AIInventory.ReportJson(w,p).Contains("\"chest_ids\":[{\"id\":\"area:")&&!AIInventory.ReportJson(w,p).Contains("own_chests"),"review I3/L12: the upper report sends areas for a big warehouse under chest_ids (not the top-level chests)");
 // Review-3 R1: an area id from chest_ids is accepted as an upper-plan take/store where and leads to the real take.
 (w,p,b)=Setup();var(rc,rbox)=Chest(w);rbox[0]=Item(ObjectID.Wood,0,10);
 for(int i=0;i<60;i++)AIBase.Memories.Add(new(){id=ObjectID.Chest,tile=new(200+(i%10)*40,200+(i/10)*40)});
 var areaOfChest=AIChestAreas.Split(AIBase.OwnChests(),AIInventory.ChestTile,null).Single(a=>a.items.Any(m=>m.tile.Equals(new int2(1,0)))).id;
 Check(AIInventory.ReportJson(w,p).Contains("{\"id\":\""+areaOfChest+"\",\"desc\":"),"review-3 R1: chest_ids gives the pure area id apart from its description");
 Check(AIInventory.CheckGoal(w,p,GoalKind.Take,ObjectID.Wood,3,areaOfChest,-1,-1)==null&&AIInventory.CheckGoal(w,p,GoalKind.Store,ObjectID.None,0,areaOfChest,-1,-1)==null,"review-3 R1: the plan check accepts an area holding a remembered chest for take and store");
 Check(AIInventory.CheckGoal(w,p,GoalKind.Take,ObjectID.Wood,3,"area:-9984,-9984,32",-1,-1)!=null,"review-3 R1: an area with no remembered chest is refused");
 Goal(w,p,new(){kind=GoalKind.Take,item=ObjectID.Wood,count=3,where=areaOfChest});
 var inspectArea=AIInventory.Candidates(w,p,Observe(w,p),"").FirstOrDefault(c=>c.inventory?.mode=="inspect"&&c.inventory.chest==rc);
 Check(inspectArea!=null,"review-3 R1: the area take goal offers opening the chest inside that area");
 Start(w,p,inspectArea.inventory);
 var takeArea=AIInventory.Candidates(w,p,Observe(w,p),"").FirstOrDefault(c=>c.inventory?.mode=="take"&&c.inventory.from==0&&c.inventory.fromEntity==rc);
 Check(takeArea!=null&&takeArea.inventory.count==3,"review-3 R1: after opening, the exact take of three from that chest is offered");
 Start(w,p,takeArea.inventory);
 Check(AICraft.Requests.Last().mode=="move"&&AICraft.Requests.Last().count==3,"review-3 R1: the area take runs as one normal move request");
 WarehouseLayout(true);WarehouseLayout(false);
 // Review-5 G5 (F5): under a base:area goal, a chest step inside the area matches only in the base context.
 (w,p,b)=Setup();var(bc,bbox)=Chest(w);AIBase.Memories.Add(new(){id=ObjectID.Chest,tile=new(50,50)});AILife.ObserveContext(w,p);
 Goal(w,p,new(){kind=GoalKind.Store,item=ObjectID.Wood,count=1,where="base:area:0,0,64"});
 Check(AILife.Resolve(new LifeSelection{Branch="inventory",Mode="store",Where="chest:1,0"}).MatchesGoal,"review-5 G5/F5: a reachable chest at base inside the base area counts toward the base-area goal");
 Check(!AILife.Resolve(new LifeSelection{Branch="inventory",Mode="store",Where="chest:50,50"}).MatchesGoal,"review-5 G5/F5: a chest inside the area but outside the base context is a separate action");
 Check(AILife.Resolve(new LifeSelection{Branch="inventory",Mode="store",Where="base:area:0,0,16"}).MatchesGoal,"review-5 G5/F5: a smaller base area step inside it counts toward the goal");
 (w,p,b)=Setup();for(int i=0;i<5;i++)AIBase.Memories.Add(new(){id=ObjectID.Chest,tile=new(i*2,0)});
 var few=AIInventory.LifeEntries(w,p,Observe(w,p),"",new LifeSelection{Branch="inventory",Mode="take"});
 Check(few.Count==5&&few.All(e=>e.Id.StartsWith("chest:")),"review I3: a few chests are still listed directly");
}
}
