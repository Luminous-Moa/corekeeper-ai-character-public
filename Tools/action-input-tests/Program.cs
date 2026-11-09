using System;
using Unity.Mathematics;
partial class Program
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Main()
    {
        foreach(var type in new[]{"MeleeWeapon","RangeWeapon","SummoningWeapon","ThrowingWeapon","BeamWeapon"})Check(AIHandCapabilities.IsWeapon(type),type);
        Check(!AIHandCapabilities.IsWeapon("Eatable"),"Food not implicitly fired");
        foreach(var type in new[]{"MiningPick","DrillTool","Sledge"})Check(AIHandCapabilities.IsMiningTool(type),type);
        var control=new AIWeaponUseControl();bool done;
        var b=AIWeaponInput.Build(control,new float2(1,0),new float2(12,3),7,true,true,false,false,false,out done);
        Check(b.primary&&!b.secondary&&b.aim&&b.hasPoint&&b.point.x==12&&b.slot==7&&!done,"ranged use retains far observed target and selected slot");
        b=AIWeaponInput.Build(control,new float2(1,0),new float2(12,3),7,false,true,false,false,false,out done);
        Check(!b.primary&&!b.secondary&&!b.aim&&!b.hasPoint&&b.point.x==0,"lost target sends no hidden shot");
        b=AIWeaponInput.Build(control,new float2(1,0),new float2(12,3),7,true,false,false,false,false,out done);
        Check(!b.primary&&!b.secondary,"waits for actual equipment");
        control=new AIWeaponUseControl();
        b=AIWeaponInput.Build(control,new float2(1,0),new float2(3,3),4,true,true,true,false,true,out done);
        Check(b.secondary&&!b.primary&&!done,"secondary holds only secondary");
        b=AIWeaponInput.Build(control,new float2(1,0),new float2(3,3),4,true,true,true,true,true,out done);
        Check(!b.secondary&&!b.primary&&!done,"max windup releases normal input");
        b=AIWeaponInput.Build(control,new float2(1,0),new float2(3,3),4,true,true,true,false,true,out done);
        Check(!done&&!b.secondary,"does not restart during release processing");
        b=AIWeaponInput.Build(control,new float2(1,0),new float2(3,3),4,true,true,true,false,false,out done);
        Check(done&&!b.secondary,"completion only after game windup reset");
        CombatChecks();
        Console.WriteLine("PASS "+checks+" actual production input/capability checks");
    }
}
