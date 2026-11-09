// AIVision — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using System.Text;
using Pug.UnityExtensions;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

// ------------------------------------------------------------------------------------------ vision

// The one place that decides what the AI can see. Rule v1 (user: "identical to the human player's view"): the
// rectangle visible on the host's screen, centred on the AI, including tiles behind walls as drawn on screen.
// Darkness/light is NOT applied yet (later step).
public static class AIVision
{
    public const string RuleName = "사람 화면 크기(호스트 카메라 실측), 벽 너머 포함, 빛·어둠 미반영";
    private static float2 _halfExtents = new float2(0f, 0f);
    private static float _measuredAt = -999f;
    public static string MeasureNote = "";

    public static float2 HalfExtents()
    {
        float now = Time.realtimeSinceStartup;
        if (now - _measuredAt < 2f && _halfExtents.x > 0f) return _halfExtents;
        _measuredAt = now;
        try
        {
            var cam = Manager.camera != null ? Manager.camera.gameCamera : null;
            if (cam != null)
            {
                var plane = new UnityEngine.Plane(Vector3.up, Vector3.zero);
                var corners = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 1f, 0f) };
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                bool ok = true;
                foreach (var c in corners)
                {
                    UnityEngine.Ray r = cam.ViewportPointToRay(c);
                    if (!plane.Raycast(r, out float enter)) { ok = false; break; }
                    Vector3 p = r.GetPoint(enter);
                    minX = math.min(minX, p.x); maxX = math.max(maxX, p.x);
                    minZ = math.min(minZ, p.z); maxZ = math.max(maxZ, p.z);
                }
                if (ok && maxX > minX && maxZ > minZ)
                {
                    _halfExtents = new float2((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
                    MeasureNote = $"host camera ortho={cam.orthographicSize:F2} aspect={cam.aspect:F3} px={cam.pixelWidth}x{cam.pixelHeight} ground rect {maxX - minX:F1}x{maxZ - minZ:F1} tiles";
                    return _halfExtents;
                }
            }
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError("vision: camera measurement failed", e);
        }
        // Fallback when the camera can't be read: a 16:9 screen ~ 30x17 tiles.
        _halfExtents = new float2(15f, 8.5f);
        MeasureNote = "camera unavailable; fallback 30x17 tiles";
        return _halfExtents;
    }

    public static bool CanSee(float3 aiPos, float2 point)
    {
        float2 h = HalfExtents();
        float2 d = math.abs(point - aiPos.xz);
        return d.x <= h.x && d.y <= h.y;
    }
}
