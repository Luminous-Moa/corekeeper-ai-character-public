// AIChatHint — @mention completion in the game's chat box (자연어 명령, 2026-11-05 합의 0-합의.md 1).
// - While the chat input is open and its text ends in "@" + at least one letter, a hint line below the input box lists the
//   matching members ("@Claude  @ChatGPT   (Tab)"; AIMention.Hint). Korean names match while the IME is still
//   composing ("@ㅋ", "@크" -> Claude), because the game puts the composition into the input text.
// - Tab fills in the first candidate ("@Claude "). The game's chat box closes on Tab (MenuManager calls
//   ChatWindow.Deactivate(commit: false)); the prefix below turns that one call into the completion when there is a
//   candidate, and leaves every other Deactivate alone (Enter sends, Esc closes, Tab with nothing to complete closes).
// The hint is a copy of the input field's own text object (same font, Korean included), moved one line up and kept
// outside the input mask. Nothing here touches what is sent: the line goes out as the host typed it.
using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public static class AIChatHint
{
    private static FieldInfo _active, _imeBuf, _imeCommitted, _imeAfter;
    private static PugText _hint;
    internal static bool OwnsText(PugText text) => text != null && text == _hint;
    private static string _shown;
    private static bool _failed;
    private static int _imeOffFrame = -1;
    private static IMECompositionMode _imeBefore = IMECompositionMode.Auto;   // put back after the one-frame off (검수 F6)

    private static bool InputActive(ChatWindow cw)
    {
        if (_active == null) _active = AccessTools.Field(typeof(ChatWindow), "textInputActive");
        return _active != null && (bool)_active.GetValue(cw);
    }

    private static bool _hosted;   // our own hosted world: hint and Tab completion only there
    public static void Frame(bool hosted)
    {
        if (_imeOffFrame >= 0 && Time.frameCount > _imeOffFrame) { _imeOffFrame = -1; Input.imeCompositionMode = _imeBefore; }
        var cw = Manager.ui != null ? Manager.ui.chatWindow : null;
        if (cw == null || cw.inputField == null) { Show(null, null); return; }
        string hint = null;
        _hosted = hosted;
        if (hosted && InputActive(cw)) hint = AIMention.Hint(cw.inputField.GetText() ?? "", AIPlayerSpikeMod.MentionNames);
        Show(cw, hint);
    }

    private static void Show(ChatWindow cw, string text)
    {
        if (text == null)
        {
            if (_hint != null && _hint.gameObject.activeSelf) _hint.gameObject.SetActive(false);
            _shown = null;
            return;
        }
        if (_failed) return;
        try
        {
            if (_hint == null)
            {
                var src = cw.inputField;
                Transform parent = cw.inputFieldPromptBackground != null && cw.inputFieldPromptBackground.parent != null ? cw.inputFieldPromptBackground.parent : cw.transform;
                var go = UnityEngine.Object.Instantiate(src.gameObject, parent);
                go.name = "AIMentionHint";
                _hint = go.GetComponent<PugText>();
                AIPlayerSpikeMod.Log($"CHAT-HINT: hint text made (parent {parent.name})");
            }
            if (!_hint.gameObject.activeSelf) _hint.gameObject.SetActive(true);
            // one line below the input box, at its left edge: above it the chat's newest line sits (실게임2 N5, shots/01);
            // below the box the screen is free
            var t = cw.inputField.transform;
            float y = t.position.y - 0.85f;   // the same line height that put it just above before (shots/01: ~42 px)
            _hint.transform.position = new Vector3(cw.inputFieldPrompt != null ? cw.inputFieldPrompt.transform.position.x : t.position.x, y, t.position.z);
            if (_shown != text)
            {
                _hint.Render(ChatPolicy.FontSafe(text));
                _hint.SetTempColor(new Color(1f, 0.85f, 0.4f));
                _shown = text;
                AIPlayerSpikeMod.Log($"CHAT-HINT: '{text}'");
            }
            foreach (var g in _hint.glyphs) if (g != null) g.maskInteraction = SpriteMaskInteraction.None;
        }
        catch (Exception e)
        {
            _failed = true;
            AIPlayerSpikeMod.LogError("CHAT-HINT: hint line could not be shown (completion with Tab still works)", e);
        }
    }

    // Harmony prefix on ChatWindow.Deactivate(bool commit). false = skip the original (the box stays open).
    public static bool DeactivatePrefix(ChatWindow __instance, bool commit)
    {
        try
        {
            if (commit || !_hosted || !Input.GetKeyDown(KeyCode.Tab) || __instance == null || __instance.inputField == null) return true;
            string text = __instance.inputField.GetText() ?? "";
            string done = AIMention.Complete(text, AIPlayerSpikeMod.MentionNames);
            if (done == null) return true;
            bool composing = !string.IsNullOrEmpty(Input.compositionString);
            ClearIme();
            __instance.SetInputText(done);
            AIPlayerSpikeMod.Log($"CHAT-HINT: Tab completed '{text}' -> '{done}'{(composing ? $" (IME was composing '{Input.compositionString}')" : "")}");
            return false;
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError("CHAT-HINT: Tab completion failed", e);
            return true;
        }
    }

    // A Korean IME composition ("ㅋ", "크") is part of the input text the game rebuilds every frame from its own buffers
    // (MenuManager.imeCompositionStringBuffer / fullyCommittedText / textAfterMarker). After completing, those buffers are
    // emptied and the IME is switched off for one frame so the composition does not come back after "@Claude ".
    private static void ClearIme()
    {
        var mm = Manager.menu;
        if (mm == null) return;
        if (_imeBuf == null)
        {
            _imeBuf = AccessTools.Field(typeof(MenuManager), "imeCompositionStringBuffer");
            _imeCommitted = AccessTools.Field(typeof(MenuManager), "fullyCommittedText");
            _imeAfter = AccessTools.Field(typeof(MenuManager), "textAfterMarker");
        }
        _imeBuf?.SetValue(mm, ""); _imeCommitted?.SetValue(mm, ""); _imeAfter?.SetValue(mm, "");
        if (!string.IsNullOrEmpty(Input.compositionString)) { if (_imeOffFrame < 0) _imeBefore = Input.imeCompositionMode; Input.imeCompositionMode = IMECompositionMode.Off; _imeOffFrame = Time.frameCount; }
    }
}
