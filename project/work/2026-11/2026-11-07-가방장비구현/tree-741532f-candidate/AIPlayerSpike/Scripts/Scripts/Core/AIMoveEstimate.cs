// Display estimates only. Route search costs and the detour-offer threshold stay in AITunnelSearch.
public static class AIMoveEstimate
{
    public const float WalkSpeed = 5.7f; // open ground, movetrace 2026-11-04 p50 5.72 tiles/s
    public const float SecondsPerHit = 0.5f;
    public static float Seconds(float tiles, int hits = 0) => tiles / WalkSpeed + hits * SecondsPerHit;
}
