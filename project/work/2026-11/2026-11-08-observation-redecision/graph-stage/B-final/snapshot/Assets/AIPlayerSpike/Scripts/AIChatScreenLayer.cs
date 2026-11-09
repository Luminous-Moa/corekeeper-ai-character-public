using System;
using System.Collections.Generic;
using UnityEngine;

// The existing ChatWindow/PugText owns text, wrapping, input, effects and lifetime.
// Only its bitmap glyphs are painted after the game's low-resolution UI camera has been upscaled.
internal sealed class AIChatScreenLayer : MonoBehaviour
{
    private struct OwnedGlyph { internal PugText Text; internal bool WasOff; }
    private static readonly Dictionary<SpriteRenderer, OwnedGlyph> Owned = new Dictionary<SpriteRenderer, OwnedGlyph>();
    private static readonly HashSet<PugText> Texts = new HashSet<PugText>();
    private static readonly HashSet<PugText> NativeOnly = new HashSet<PugText>();
    private static AIChatScreenLayer _instance;
    private static bool _patched;
    private Vector4 _projectionSignature;
    private Rect _viewport;
    private int _cameraId;
    private float _metricsAfter;
    private bool _metricsPending;
    internal static bool Ready => _patched && _instance != null;

    internal static void Patched(bool ok) { _patched = ok; }

    internal static void Frame(bool hosted)
    {
        if (!hosted) { Release(); return; }
        if (!_patched) return;
        if (_instance == null)
        {
            var go = new GameObject("Existing chat screen glyphs");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AIChatScreenLayer>();
        }
        var camera = Manager.camera != null ? Manager.camera.uiCamera : null;
        var root = Manager.ui != null && Manager.ui.chatWindow != null ? Manager.ui.chatWindow.messageTextParent : null;
        float parentScale = root != null && root.parent != null ? Mathf.Abs(root.parent.lossyScale.y) : 1f;
        var signature = new Vector4(Screen.width, Screen.height, camera != null ? camera.orthographicSize : 0, parentScale);
        Rect viewport = camera != null ? camera.rect : default;
        int cameraId = camera != null ? camera.GetInstanceID() : 0;
        if (signature != _instance._projectionSignature || viewport != _instance._viewport || cameraId != _instance._cameraId)
        {
            _instance._projectionSignature = signature; _instance._viewport = viewport; _instance._cameraId = cameraId;
            _instance._metricsAfter = Time.unscaledTime + 0.25f;
            _instance._metricsPending = true;
        }
        // Measure after projection changes settle. The chat root's animated 0..1 fade scale is deliberately excluded.
        // A hide/show or loading fade therefore cannot rebuild 11k sprites on every frame.
        if (_instance._metricsPending && camera != null && root != null && Time.unscaledTime >= _instance._metricsAfter)
        {
            _instance._metricsPending = false;
            AICompactFont.RefreshScreenMetrics();
        }
        foreach (var text in AICompactFont.ChatTexts)
        {
            if (!CanUse(text)) BeforeRelease(text);
            else AfterRender(text);
        }
    }

    internal static float FontPixelsPerUnit(int compactCell)
    {
        if (!Ready || Manager.camera == null || Manager.ui == null || Manager.ui.chatWindow == null) return 16f;
        var camera = Manager.camera.uiCamera;
        var root = Manager.ui.chatWindow.messageTextParent;
        if (camera == null || !camera.orthographic || root == null || camera.orthographicSize <= 0) return 16f;
        // ChatWindow.LateUpdate assigns root.localScale = CalcGameplayUITargetScaleMultiplier (animated 0..1).
        // Its fully visible target is one; only the parent's scale belongs in the stable measurement.
        float parentScale = root.parent != null ? Mathf.Abs(root.parent.lossyScale.y) : 1f;
        float native = AIChatScreenMath.NativePixelsPerDot(Screen.height, camera.rect.height, camera.orthographicSize, parentScale);
        var original = Manager.text != null ? Manager.text.GetKoreanFont(TextManager.FontFace.thinSmall) : null;
        int originalCell = original != null ? original.charDims.y : compactCell;
        // The user found the currently installed compact chat still large (about 44px in the scaled capture).
        // Halve that measured display, not a historical font/desktop size from another resolution.
        int scale = AIChatScreenMath.PixelScale(native, compactCell, compactCell);
        float ppu = AIChatScreenMath.PixelsPerUnit(native, scale);
        AIPlayerSpikeMod.Log($"CHAT-SCREEN metrics: screen={Screen.width}x{Screen.height}; source dot={native:F3}px; original Korean cell={originalCell}; previous compact={compactCell * native:F1}px; target={compactCell * scale}px; ppu={ppu:F3}; viewport={camera.rect}");
        return ppu;
    }

    // Masked or non-bitmap paths stay with the game's renderer instead of silently losing clipping or glyphs.
    internal static bool CanUse(PugText text) => Ready && Manager.camera != null && Manager.camera.uiCamera != null &&
        Manager.camera.uiCamera.orthographic && text != null && text.usePooledResources &&
        !NativeOnly.Contains(text) && text.style.maskInteraction == SpriteMaskInteraction.None && text.overrideMaterial == null;

    internal static void AfterRender(PugText __instance)
    {
        var text = __instance;
        if (!CanUse(text) || text.isUsingDynamicText || !AICompactFont.IsChatFont(text) || !text.gameObject.activeInHierarchy) return;
        // Validate the whole line before suppressing any native renderer.
        foreach (var glyph in text.glyphs)
            if (glyph != null && glyph.sprite != null && (glyph.sprite.packed || glyph.maskInteraction != SpriteMaskInteraction.None))
            {
                NativeOnly.Add(text);
                BeforeRelease(text);
                bool keep = text.dontResetEffectsOnRender;
                try { text.dontResetEffectsOnRender = true; text.Render(false, text.gameObject.activeSelf); }
                finally { text.dontResetEffectsOnRender = keep; }
                return;
            }
        Texts.Add(text);
        foreach (var glyph in text.glyphs)
        {
            if (glyph == null || Owned.ContainsKey(glyph)) continue;
            Owned[glyph] = new OwnedGlyph { Text = text, WasOff = glyph.forceRenderingOff };
            glyph.forceRenderingOff = true;
        }
    }

    // Both paths return glyphs to the global pool. Restore before the original method can reuse them elsewhere.
    internal static void BeforeRelease(PugText __instance)
    {
        if (__instance == null || !Texts.Contains(__instance)) return;
        foreach (var glyph in __instance.glyphs)
        {
            if (glyph == null || !Owned.TryGetValue(glyph, out var state) || state.Text != __instance) continue;
            glyph.forceRenderingOff = state.WasOff;
            Owned.Remove(glyph);
        }
        Texts.Remove(__instance);
    }

    private static Vector2 Project(Camera camera, Vector3 world)
    {
        // pixelHeight may be the low-resolution render target. A normalized viewport is mapped to the real window.
        var p = camera.WorldToViewportPoint(world);
        Rect viewport = camera.rect;
        return new Vector2(AIChatScreenMath.ScreenX(p.x, viewport.x, viewport.width, Screen.width),
            AIChatScreenMath.ScreenY(p.y, viewport.y, viewport.height, Screen.height));
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || Manager.camera == null || Manager.camera.uiCamera == null) return;
        var camera = Manager.camera.uiCamera;
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        int oldDepth = GUI.depth;
        try
        {
            GUI.matrix = Matrix4x4.identity;
            GUI.depth = 0; // the separate AI panel remains above the chat
            foreach (var text in Texts)
            {
                if (text == null || !text.gameObject.activeInHierarchy || text.isHidden || text.isUsingDynamicText || !CanUse(text)) continue;
                foreach (var glyph in text.glyphs)
                {
                    if (glyph == null || !glyph.enabled || !glyph.gameObject.activeInHierarchy || glyph.sprite == null ||
                        !Owned.TryGetValue(glyph, out var state) || state.WasOff || state.Text != text ||
                        (camera.cullingMask & (1 << glyph.gameObject.layer)) == 0) continue;
                    DrawGlyph(camera, text, glyph);
                }
            }
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError("CHAT-SCREEN painting failed; restoring native chat rendering", e);
            Release();
            _patched = false;
            AICompactFont.Shutdown();
        }
        finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.depth = oldDepth; }
    }

    private static void DrawGlyph(Camera camera, PugText text, SpriteRenderer glyph)
    {
        Sprite sprite = glyph.sprite;
        Rect source = sprite.rect;
        // Fallback language/control glyphs use the original font's sprite ppu. Match PugText's new advance metrics.
        float ratio = sprite.pixelsPerUnit / text.font.pixelsPerUnit;
        var bounds = sprite.bounds;
        Vector2 a = Project(camera, glyph.transform.TransformPoint(bounds.min * ratio));
        Vector2 b = Project(camera, glyph.transform.TransformPoint(bounds.max * ratio));
        Rect destination = Rect.MinMaxRect(Mathf.Round(Mathf.Min(a.x, b.x)), Mathf.Round(Mathf.Min(a.y, b.y)),
            Mathf.Round(Mathf.Max(a.x, b.x)), Mathf.Round(Mathf.Max(a.y, b.y)));
        if (destination.width <= 0 || destination.height <= 0 || !destination.Overlaps(new Rect(0, 0, Screen.width, Screen.height))) return;
        var texture = sprite.texture;
        Rect uv = new Rect(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height);
        if (glyph.flipX) { uv.x += uv.width; uv.width = -uv.width; }
        if (glyph.flipY) { uv.y += uv.height; uv.height = -uv.height; }
        Color color = glyph.color;
        if (text.style.UsesOutlines)
        {
            Color outline = text.style.outlineColor; outline.a *= color.a;
            GUI.color = outline;
            int step = Math.Max(1, Mathf.RoundToInt(destination.height / source.height));
            if ((text.style.outline & PugTextStyle.Outline.left) != 0) Paint(destination, uv, texture, -step, 0);
            if ((text.style.outline & PugTextStyle.Outline.right) != 0) Paint(destination, uv, texture, step, 0);
            if ((text.style.outline & PugTextStyle.Outline.top) != 0) Paint(destination, uv, texture, 0, -step);
            if ((text.style.outline & PugTextStyle.Outline.bottom) != 0) Paint(destination, uv, texture, 0, step);
        }
        GUI.color = color;
        Paint(destination, uv, texture, 0, 0);
    }

    private static void Paint(Rect rect, Rect uv, Texture texture, int dx, int dy)
    {
        rect.x += dx; rect.y += dy;
        GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
    }

    private static void Release()
    {
        foreach (var pair in Owned) if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value.WasOff;
        Owned.Clear(); Texts.Clear(); NativeOnly.Clear();
        if (_instance != null) Destroy(_instance.gameObject);
        _instance = null;
    }

    internal static void Shutdown() { Release(); _patched = false; }
}
