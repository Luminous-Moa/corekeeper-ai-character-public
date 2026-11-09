using System;
using Unity.Entities;
using UnityEngine;

// Original compact IMGUI interface. Only the host's live ECS data is read.
internal sealed class AIInfoPanel : MonoBehaviour
{
    private static AIInfoPanel _instance;
    private static bool _inputPatched;
    private readonly AIUiPointerCapture _capture = new AIUiPointerCapture();
    private World _server;
    private bool _hosted;
    private int _selected = -1;
    private bool _equipment;
    private float _refreshAt;
    private AIInfoSnapshot _snapshot = new AIInfoSnapshot();
    private GUIStyle _button;
    private const float Width = 292, BarHeight = 28, PanelHeight = 246;
    private Rect Bar => new Rect(Mathf.Max(8, Screen.width - Width - 12), Mathf.Max(8, Screen.height - BarHeight - 12), Width, BarHeight);
    private Rect Panel => new Rect(Bar.x, Mathf.Max(8, Bar.y - PanelHeight - 4), Width, PanelHeight);

    internal static void Patched(bool success) { _inputPatched = success; }

    internal static void Frame(bool hosted, World server)
    {
        if (!hosted)
        {
            if (_instance != null) { _instance._hosted = false; _instance._capture.Reset(); Destroy(_instance.gameObject); _instance = null; }
            if (AICompactFont.Ready) AICompactFont.Shutdown();
            return;
        }
        if (hosted)
        {
            AICompactFont.Ensure();
            if (_instance == null && _inputPatched && AICompactFont.Ready)
            {
                var go = new GameObject("AI member information");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<AIInfoPanel>();
            }
        }
        if (_instance == null) return;
        _instance._hosted = hosted;
        _instance._server = server;
        _instance.SampleCapture();
        if (_instance.Visible && _instance._selected >= 0 && Time.unscaledTime >= _instance._refreshAt)
        {
            bool first = _instance._refreshAt == 0;
            _instance._refreshAt = Time.unscaledTime + 0.5f;
            try
            {
                _instance._snapshot = AIInfoSnapshot.Read(server, AIAgent.All[_instance._selected]);
                if (first) AIPlayerSpikeMod.Log("AI-UI selected " + AIAgent.All[_instance._selected].Name + ": " + _instance._snapshot.Status + "; skills=" + _instance._snapshot.Skills.Length + "; equipment=" + _instance._snapshot.Equipment.Length);
            }
            catch (Exception e) { _instance._snapshot = new AIInfoSnapshot { Status = "정보를 읽는 중" }; AIPlayerSpikeMod.LogError("AI-UI snapshot failed", e); }
        }
    }

    private bool Visible => _hosted && _inputPatched && AICompactFont.Ready && Manager.main != null && Manager.main.player != null
        && Manager.ui != null && !Manager.ui.isAnyInventoryShowing && !Manager.ui.isPlayerEquipmentShowing && !Manager.ui.isVanitySlotsShowing && !Manager.ui.isShowingMap
        && (Manager.menu == null || !Manager.menu.IsAnyMenuActive());

    private bool SampleCapture()
    {
        var mouse = Input.mousePosition;
        var point = new Vector2(mouse.x, Screen.height - mouse.y);
        bool inside = Bar.Contains(point) || (_selected >= 0 && Panel.Contains(point));
        int buttons = (Input.GetMouseButton(0) ? 1 : 0) | (Input.GetMouseButton(1) ? 2 : 0) | (Input.GetMouseButton(2) ? 4 : 0);
        int pressed = (Input.GetMouseButtonDown(0) ? 1 : 0) | (Input.GetMouseButtonDown(1) ? 2 : 0) | (Input.GetMouseButtonDown(2) ? 4 : 0);
        return _capture.Evaluate(Visible, inside, buttons, Time.frameCount, pressed);
    }

    private static bool Block(PlayerInput input) => _instance != null && Manager.main != null && Manager.main.player != null
        && ReferenceEquals(input, Manager.main.player.inputModule) && _instance.SampleCapture();
    internal static bool ButtonPrefix(PlayerInput __instance, ref bool __result) { if (!Block(__instance)) return true; __result = false; return false; }
    internal static bool VectorPrefix(PlayerInput __instance, ref Vector2 __result) { if (!Block(__instance)) return true; __result = Vector2.zero; return false; }
    internal static bool AxisPrefix(PlayerInput __instance, ref float __result) { if (!Block(__instance)) return true; __result = 0; return false; }

    private void OnGUI()
    {
        if (!Visible) return;
        GUI.depth = -50;
        if (_button == null) _button = new GUIStyle(GUI.skin.button) { padding = new RectOffset(0, 0, 0, 0) };
        Rect bar = Bar;
        Background(bar);
        for (int i = 0; i < AIAgent.All.Length; i++)
        {
            Rect r = new Rect(bar.x + 4 + i * 71, bar.y + 3, 70, 22);
            if (Button(r, AIAgent.All[i].Name, _selected == i))
            {
                _selected = _selected == i ? -1 : i;
                _snapshot = new AIInfoSnapshot { Status = "정보를 읽는 중" };
                _refreshAt = 0;
            }
        }
        if (_selected < 0) return;
        Rect panel = Panel;
        Background(panel);
        Label(new Rect(panel.x + 10, panel.y + 6, Width - 45, 22), AIAgent.All[_selected].Name + " · 정보", Color.white);
        if (Button(new Rect(panel.xMax - 28, panel.y + 5, 22, 22), "X")) { _selected = -1; return; }
        if (Button(new Rect(panel.x + 8, panel.y + 31, 134, 24), "스킬", !_equipment)) _equipment = false;
        if (Button(new Rect(panel.x + 150, panel.y + 31, 134, 24), "착용 장비", _equipment)) _equipment = true;
        Label(new Rect(panel.x + 10, panel.y + 58, Width - 20, 18), _snapshot.Status, new Color(.65f, .75f, .8f));
        var rows = _equipment ? _snapshot.Equipment : _snapshot.Skills;
        if (rows.Length == 0) Label(new Rect(panel.x + 10, panel.y + 87, Width - 20, 20), "표시할 정보가 없습니다", Color.white);
        for (int i = 0; i < rows.Length; i++)
        {
            int column = _equipment ? 0 : i / 6, row = _equipment ? i : i % 6;
            Label(new Rect(panel.x + 10 + column * 140, panel.y + 81 + row * (_equipment ? 15 : 24), _equipment ? Width - 20 : 132, _equipment ? 15 : 24), rows[i], Color.white);
        }
    }

    private static void Background(Rect rect)
    {
        Color old = GUI.color;
        GUI.color = new Color(.04f, .06f, .08f, .92f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
    private static void Label(Rect rect, string text, Color color) => AICompactFont.Draw(rect, text, 1, color);
    private bool Button(Rect rect, string text, bool selected = false)
    {
        bool clicked = GUI.Button(rect, GUIContent.none, _button);
        float w = AICompactFont.TextWidth(text);
        Label(new Rect(rect.x + Mathf.Max(3, (rect.width - w) / 2), rect.y, rect.width - 6, rect.height), text, selected ? new Color(.4f, 1, .75f) : Color.white);
        return clicked;
    }
    internal static void Shutdown()
    {
        if (_instance != null) { _instance._capture.Reset(); Destroy(_instance.gameObject); }
        _instance = null; _inputPatched = false;
    }
}
