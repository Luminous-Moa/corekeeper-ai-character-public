using Unity.Entities;
static partial class Program {
static void FinalCases(){
 var(capWorld,capPlayer,capBag)=Setup();capBag[12]=Item(ObjectID.CopperSword,2,300);var memory=new AIBase.ChestMem();memory.items[ObjectID.CopperSword]=1;
 Check(!ReviewBase.MayStackStored(capWorld,capPlayer,memory,(12,ObjectID.CopperSword,1)),"independent chest preparation: same nonstack equipment ID in a full chest is not stacking room");
 capBag[12]=Item(ObjectID.Wood,0,3);memory.items[ObjectID.Wood]=3;
 Check(ReviewBase.MayStackStored(capWorld,capPlayer,memory,(12,ObjectID.Wood,3)),"independent chest preparation: same stackable ID remains a possible remembered stacking route");
 var(fullWorld,fullPlayer,fullBag)=Setup();fullBag[0]=Item(ObjectID.Wood,0,10);fullBag[1]=Item(ObjectID.Chest);var(fullChest,fullBox)=Chest(fullWorld);for(int i=0;i<fullBox.Length;i++)fullBox[i]=Item(ObjectID.Wood,0,9999);var fullMemory=AIBase.Memories[0];fullMemory.free=0;fullMemory.slots=fullBox.Length;fullMemory.openedUnix=1;fullMemory.items[ObjectID.Wood]=9999*fullBox.Length;AIInventory.Opened(fullChest);Goal(fullWorld,fullPlayer,new(){kind=GoalKind.Store,item=ObjectID.Wood,count=5});
 var exact=AIInventory.Candidates(fullWorld,fullPlayer,Observe(fullWorld,fullPlayer),"");var hasRoom=ReviewBase.MayStoreKnownChest(fullWorld,fullPlayer,fullMemory,ReviewBase.Storable(fullWorld,fullPlayer,ObjectID.Wood),AIBase.ChestsInView(fullWorld,fullPlayer));
 Console.WriteLine("FULL STACK PREPARATION BOUNDARY: chest slots="+fullBox.Length+", every Wood=9999, free="+fullMemory.free+", exact moves="+exact.Count+", MayStackStored="+hasRoom+", spare Chest in bag="+fullBag[1].objectID);
 Check(!hasRoom&&exact.Count==0,"independent full-stack preparation: completely full maximum stacks must not suppress the spare chest placement route");
 fullBox[0]=Item(ObjectID.Wood,0,9998);hasRoom=ReviewBase.MayStoreKnownChest(fullWorld,fullPlayer,fullMemory,ReviewBase.Storable(fullWorld,fullPlayer,ObjectID.Wood),AIBase.ChestsInView(fullWorld,fullPlayer));exact=AIInventory.Candidates(fullWorld,fullPlayer,Observe(fullWorld,fullPlayer),"");
 Check(hasRoom&&exact.Count>0&&exact[0].inventory.count==1,"independent chest capacity: one remaining stack space is shared by preparation and exact quantity candidate");
 var single=fullWorld.EntityManager.GetBuffer<InventoryBuffer>(fullChest);var rule=single[0];rule.canOnlyContainOneItemPerSlot=true;single[0]=rule;
 hasRoom=ReviewBase.MayStoreKnownChest(fullWorld,fullPlayer,fullMemory,ReviewBase.Storable(fullWorld,fullPlayer,ObjectID.Wood),AIBase.ChestsInView(fullWorld,fullPlayer));
 Check(!hasRoom&&AIInventory.Candidates(fullWorld,fullPlayer,Observe(fullWorld,fullPlayer),"").Count==0,"independent chest capacity: occupied one-item slots cannot be treated as stack room");
 Check(ReviewBase.MayStoreKnownChest(fullWorld,fullPlayer,fullMemory,ReviewBase.Storable(fullWorld,fullPlayer,ObjectID.Wood),new()),"independent chest capacity: offscreen remembered same stackable remains a go-and-check possibility");
 var(w,p,b)=Setup(); b[12]=Item(ObjectID.CopperHelm,1); Goal(w,p,new(){kind=GoalKind.Equip,item=ObjectID.CopperHelm,slot=12,where="head",count=1});
 var move=AIInventory.Candidates(w,p,Observe(w,p),"")[0].inventory; var skill=Start(w,p,move);
 skill.Tick(w,p,4); skill.Tick(w,p,40);
 Check(skill.Status==SkillStatus.Running&&AICraft.Requests.Count==1&&skill.Facts.Count(x=>x.Contains("양 슬롯 변화를 아직"))==1,"independent late swap: elapsed time reports once, stays selected and never resends");
 Swap(w,move);skill.Tick(w,p,41);
 Check(skill.Status==SkillStatus.Done&&AIInventory.GoalDone(w,p,out _),"independent late swap: eventual exact pair observation still completes");
 (w,p,b)=Setup();b[12]=Item(ObjectID.CopperSword);Goal(w,p,new(){kind=GoalKind.Hold,item=ObjectID.CopperSword,slot=12,where="hotbar:5",count=1});
 move=AIInventory.Candidates(w,p,Observe(w,p),"")[0].inventory;skill=Start(w,p,move);Swap(w,move);var input=skill.Tick(w,p,4);
 Check(skill.Status==SkillStatus.Running&&input.slot==5&&AICraft.Requests.Count==1,"independent late hand: pair exchange without actual held state stays running after three seconds");
 w.EntityManager.Put(p,new EquippedObjectCD{equippedSlotIndex=5,containedObject=b[5]});skill.Tick(w,p,10);
 Check(skill.Status==SkillStatus.Done&&AIInventory.TryChosenHand(w,p,false,out int selected,out _,out _)&&selected==5,"independent late hand: actual held observation later establishes exact selection");
 (w,p,b)=Setup();b[0]=Item(ObjectID.Wood,0,20);var(chest,box)=Chest(w);AIInventory.Opened(chest);Goal(w,p,new(){kind=GoalKind.Store,item=ObjectID.Wood,count=5,keep=10});
 move=AIInventory.Candidates(w,p,Observe(w,p),"")[0].inventory;skill=Start(w,p,move);skill.Tick(w,p,4);skill.Tick(w,p,30);
 Check(skill.Status==SkillStatus.Running&&AICraft.Requests.Count==1,"independent late transfer: unchanged source and destination remain pending without duplicate move");
 b[0]=Item(ObjectID.Wood,0,15);box[0]=Item(ObjectID.Wood,0,5);skill.Tick(w,p,31);
 Check(skill.Status==SkillStatus.Done&&AIInventory.GoalDone(w,p,out _),"independent late transfer: delayed matching two-side quantity completes");
}
}

