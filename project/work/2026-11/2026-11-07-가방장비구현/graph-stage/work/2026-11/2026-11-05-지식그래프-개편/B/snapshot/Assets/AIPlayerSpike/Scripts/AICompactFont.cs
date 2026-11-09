// Local chat font and screen-space labels. Font license and source are embedded in AICompactFontData.cs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

internal static class AICompactFont
{
    private static PugFont _font;
    private static Texture2D _texture;
    private static readonly Dictionary<char, Rect> _uv = new Dictionary<char, Rect>();
    private static readonly Dictionary<char, int> _width = new Dictionary<char, int>();
    private static readonly HashSet<PugText> _changed = new HashSet<PugText>();
    private static bool _failed;
    private static bool _enabled;
    private static bool _loggedMetrics;
    internal static int Height { get; private set; } = 10;
    internal static bool Ready => _font != null;
    internal static IEnumerable<PugText> ChatTexts => _changed;
    internal static bool IsChatFont(PugText text) => text != null && Ready && text.font == _font;

    internal static void RefreshScreenMetrics()
    {
        if (!Ready || !AIChatScreenLayer.Ready) return;
        float ppu = AIChatScreenLayer.FontPixelsPerUnit(Height);
        if (Mathf.Abs(ppu - _font.pixelsPerUnit) < 0.001f) return;
        var texts = new List<PugText>();
        var messages = new List<PugText>();
        var oldSprites = new List<Sprite>();
        var chat = Manager.ui != null ? Manager.ui.chatWindow : null;
        foreach (var text in _changed)
        {
            if (!IsChatFont(text)) continue;
            texts.Add(text);
            if (chat != null && text.gameObject.activeInHierarchy && text.transform.parent == chat.messageTextParent &&
                text != chat.inputField && text != chat.inputFieldPrompt && !AIChatHint.OwnsText(text)) messages.Add(text);
        }
        messages.Sort((a, b) => a.transform.localPosition.y.CompareTo(b.transform.localPosition.y));
        foreach (var glyph in _font.glyphData) if (glyph.volatileSprite != null) oldSprites.Add(glyph.volatileSprite);
        _font.pixelsPerUnit = ppu;
        _font.InitCodePoints();
        foreach (var text in texts)
        {
            bool keep = text.dontResetEffectsOnRender;
            Color color = text.GetGlyphsColor();
            try { text.dontResetEffectsOnRender = true; text.Render(false, text.gameObject.activeSelf); text.SetTempColor(color); }
            finally { text.dontResetEffectsOnRender = keep; }
        }
        float y = 0;
        foreach (var text in messages)
        {
            var position = text.transform.localPosition; position.y = y; text.transform.localPosition = position;
            y += text.dimensions.height;
        }
        if (chat != null) HarmonyLib.AccessTools.Method(typeof(ChatWindow), "AdjustInputFieldPosition")?.Invoke(chat, null);
        foreach (var sprite in oldSprites) UnityEngine.Object.Destroy(sprite);
    }

    internal static void Ensure()
    {
        if (Ready || _failed) return;
        var watch = Stopwatch.StartNew();
        try
        {
            using (var bytes = new MemoryStream(Convert.FromBase64String(AICompactFontData.Packed)))
            using (var zip = new GZipStream(bytes, CompressionMode.Decompress))
            using (var reader = new BinaryReader(zip))
            {
                int count = reader.ReadUInt16();
                Height = reader.ReadByte();
                reader.ReadByte(); // descent is already reflected in the packed bitmap rows
                int maxWidth = reader.ReadByte(), space = reader.ReadByte();
                reader.ReadUInt16(); // reserved
                const int columns = 128;
                int cellW = maxWidth + 4, cellH = Height + 3;
                int tw = columns * cellW, th = ((count + columns - 1) / columns) * cellH;
                var pixels = new Color32[tw * th];
                var glyphs = new PugFont.GlyphData[count];
                var chars = new char[count];
                for (int i = 0; i < count; i++)
                {
                    char c = (char)reader.ReadUInt16();
                    int width = reader.ReadByte(), x = (i % columns) * cellW + 1, y = (i / columns) * cellH;
                    chars[i] = c;
                    // InitCodePoints omits the bottom row and adds transparent outline columns on both sides.
                    glyphs[i] = new PugFont.GlyphData { rect = new RectInt(x, y, width, Height + 1) };
                    for (int row = 0; row < Height; row++)
                    {
                        int bits = reader.ReadUInt16();
                        for (int col = 0; col < width; col++)
                            if ((bits & (1 << col)) != 0) pixels[(y + 1 + row) * tw + x + col] = new Color32(255, 255, 255, 255);
                    }
                    _width[c] = width;
                    _uv[c] = new Rect(x / (float)tw, (y + 1) / (float)th, width / (float)tw, Height / (float)th);
                }
                _texture = new Texture2D(tw, th, TextureFormat.RGBA32, false) { name = "AI Compact Korean atlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                _texture.SetPixels32(pixels);
                _texture.Apply(false, true);
                _font = ScriptableObject.CreateInstance<PugFont>();
                _font.name = "AI Compact Korean";
                _font.texture = _texture;
                _font.charDims = new Vector2Int(maxWidth, Height);
                _font.pixelsPerUnit = AIChatScreenLayer.FontPixelsPerUnit(Height);
                _font.charSpacing = 0;
                _font.spaceWidth = space;
                _font.lineSpacing = 1;
                _font.emptyLineSpacing = Height + 1;
                _font.proportionalFont = true;
                _font.enableKerning = false;
                _font._customCharset = new string(chars);
                _font.glyphData = glyphs;
                _font.InitCodePoints();
                _enabled = true;
                AIPlayerSpikeMod.Log($"AI-UI font ready: {count} glyphs, cell {Height}px, atlas {tw}x{th}, {watch.ElapsedMilliseconds}ms; chat only");
            }
        }
        catch (Exception e)
        {
            _failed = true;
            ReleaseFont();
            AIPlayerSpikeMod.LogError("AI-UI font initialization failed; keeping the game's chat font", e);
        }
    }

    // PugText's bitmap font path calls SetPugFont before rendering, including Korean IME and pooled chat lines.
    // No TextManager font is mutated, and button/control glyphs keep their own font.
    internal static void SetPugFontPrefix(PugText __instance, TextManager.FontFace newFontFace, ref PugFont fontToUse)
    {
        if (!_enabled || !Ready || __instance == null || newFontFace == TextManager.FontFace.button || Manager.ui == null) return;
        if (AIChatScreenLayer.Ready && !AIChatScreenLayer.CanUse(__instance)) return;
        var chat = Manager.ui.chatWindow;
        if (chat == null) return;
        bool output = chat.messageTextParent != null && __instance.transform.IsChildOf(chat.messageTextParent);
        if (!output && __instance != chat.inputField && __instance != chat.inputFieldPrompt && !AIChatHint.OwnsText(__instance)) return;
        if (!_loggedMetrics && output && fontToUse != null)
        {
            _loggedMetrics = true;
            AIPlayerSpikeMod.Log($"AI-UI chat metrics: original {fontToUse.name} cell={fontToUse.charDims.y} spacing={fontToUse.lineSpacing} ppu={fontToUse.pixelsPerUnit}; compact cell={_font.charDims.y} spacing={_font.lineSpacing} ppu={_font.pixelsPerUnit}");
        }
        fontToUse = _font;
        _changed.Add(__instance);
        // Bound references across world changes, not across each message. Destroyed pooled objects are removed.
        if (_changed.Count > 128) _changed.RemoveWhere(t => t == null);
    }

    internal static int TextWidth(string text)
    {
        int w = 0;
        foreach (char c in text ?? "") w += _width.TryGetValue(c, out int v) ? v : 7;
        return w;
    }

    internal static string Fit(string text, int width)
    {
        if (TextWidth(text) <= width) return text;
        int used = 0, end = 0, ellipsis = _width.TryGetValue('…', out int e) ? e : 7;
        foreach (char c in text ?? "")
        {
            int w = _width.TryGetValue(c, out int v) ? v : 7;
            if (used + w + ellipsis > width) break;
            used += w; end++;
        }
        return (text ?? "").Substring(0, end) + "…";
    }

    // Integer scaling of the original bitmap keeps small Hangul sharp without relying on an OS-installed font.
    internal static void Draw(Rect rect, string text, int scale, Color color)
    {
        if (!Ready || Event.current.type != EventType.Repaint) return;
        Color old = GUI.color; GUI.color = color;
        float x = Mathf.Round(rect.x), y = Mathf.Round(rect.y + (rect.height - Height * scale) / 2f);
        foreach (char original in Fit(text, Mathf.FloorToInt(rect.width / scale)))
        {
            char c = _uv.ContainsKey(original) ? original : '?';
            int width = _width[c];
            if (c != ' ') GUI.DrawTextureWithTexCoords(new Rect(x, y, width * scale, Height * scale), _texture, _uv[c], true);
            x += width * scale;
        }
        GUI.color = old;
    }

    // Disable our hook first so Render resolves the original font even on a world exit before mod unload.
    internal static void Shutdown()
    {
        _enabled = false;
        foreach (var t in _changed)
        {
            if (t == null) continue;
            try { t.Render(false, t.gameObject.activeSelf); }
            catch (Exception e) { AIPlayerSpikeMod.LogError("AI-UI restoring chat font failed", e); }
        }
        _changed.Clear();
        ReleaseFont();
        _failed = false;
        _loggedMetrics = false;
    }

    private static void ReleaseFont()
    {
        if (_font != null)
        {
            if (_font.glyphData != null)
                foreach (var g in _font.glyphData) if (g?.volatileSprite != null) UnityEngine.Object.Destroy(g.volatileSprite);
            UnityEngine.Object.Destroy(_font);
        }
        if (_texture != null) UnityEngine.Object.Destroy(_texture);
        _font = null; _texture = null; _uv.Clear(); _width.Clear();
    }
}
