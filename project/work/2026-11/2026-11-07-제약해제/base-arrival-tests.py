from pathlib import Path
r=Path('C:/Dev/CoreKeeperModSDK-unrestricted/Tools/restriction-tests')
p=r/'prepare.mjs';s=p.read_text(encoding='utf-8');s=s.replace('function block(signature){const a=src.indexOf(signature);','function block(signature,source=src){const a=source.indexOf(signature);').replace('const line=src.slice(a,src.indexOf','const line=source.slice(a,source.indexOf').replace('const b=src.indexOf','const b=source.indexOf').replace('return src.slice(a,b+6);','return source.slice(a,b+6);')
s=s.replace("fs.writeFileSync(new URL('Generated.cs',import.meta.url),code);","""const base=fs.readFileSync(new URL('Assets/AIPlayerSpike/Scripts/AIBase.cs',root),'utf8');
code+='\\npublic static class BaseArrivalProbe { public const float HomeArrive=AIPlaces.StandArrive; private static float BaseDist(float3 p)=>math.distance(p.xz,new float2(0,0)); private static bool BaseStand(World w,Entity p,out int2 s){s=w.Stand;return w.HasStand;}\\n'+block('    public static bool ArrivedAtBase(World server, Entity player)',base)+'\\n'+block('    public static bool ArrivedAtBase(World server, Entity player, out float dist)',base)+'\\n}';
fs.writeFileSync(new URL('Generated.cs',import.meta.url),code);""")
p.write_text(s,encoding='utf-8')
p=r/'Program.cs';s=p.read_text(encoding='utf-8').replace('public float3 Pos;public int Bag;','public float3 Pos;public int2 Stand;public bool HasStand;public int Bag;');s=s.replace('Console.WriteLine("ALL PASS "+checks);','''var blockedHome=new World{Pos=new float3(3,0,0),HasStand=true,Stand=new int2(3,0)};
Check(BaseArrivalProbe.ArrivedAtBase(blockedHome,default),"production shared home arrival accepts actual stand three tiles from blocked anchor");
blockedHome.Pos=new float3(100,0,0);Check(!BaseArrivalProbe.ArrivedAtBase(blockedHome,default),"shared stand predicate still rejects100tile remote context");
Console.WriteLine("ALL PASS "+checks);''');p.write_text(s,encoding='utf-8')
