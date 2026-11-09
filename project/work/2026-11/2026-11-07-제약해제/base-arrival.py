from pathlib import Path
p=Path('C:/Dev/CoreKeeperModSDK-unrestricted/Assets/AIPlayerSpike/Scripts/AIBase.cs')
s=p.read_text(encoding='utf-8')
start=s.index('            float3 me = AIBody.Pos(server, player);',s.index('if (g.kind == GoalKind.Home)'))
end=s.index('            text =',start)
s=s[:start]+'            if (!ArrivedAtBase(server, player, out float d)) return false;\n'+s[end:]
marker='    public static bool AtBase(float3 pos) => BaseDist(pos) <= HomeArrive;'
add='''
    // Shared named-base arrival: the walkable stand chosen around an occupied anchor is arrival too.
    public static bool ArrivedAtBase(World server, Entity player) => ArrivedAtBase(server, player, out _);
    public static bool ArrivedAtBase(World server, Entity player, out float dist)
    {
        float3 me = AIBody.Pos(server, player);
        dist = BaseDist(me);
        float standDist = dist > HomeArrive && BaseStand(server, player, out int2 stand) ? math.distance(me.xz, (float2)stand) : float.MaxValue;
        return AIPlaces.ArrivedAt(dist, HomeArrive, standDist);
    }'''
s=s.replace(marker,marker+add)
p.write_text(s,encoding='utf-8')
