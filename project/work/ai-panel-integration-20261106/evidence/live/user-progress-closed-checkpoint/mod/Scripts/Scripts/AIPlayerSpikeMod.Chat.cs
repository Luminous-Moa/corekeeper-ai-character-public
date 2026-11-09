// AIPlayerSpikeMod.Chat — extracted from the existing implementation; behavior and state ownership are unchanged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Pug.Properties;
using PugMod;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using Unity.Transforms;
using UnityEngine;

public partial class AIPlayerSpikeMod
{

    // ---------------------------------------------------------------- per-world on/off (host chat command)

    // Called from the Harmony prefix on the host's own NetworkCommSystem.SendChatMessage. Returns true when the
    // message was an /ai command (then it is not sent to anyone). The game sends "<name>: <text>".
    internal static bool TryHandleChatCommand(string message)
    {
        string text = message.Trim();
        if (!IsAiCommand(text))
        {
            int idx = message.IndexOf(": ", StringComparison.Ordinal);
            if (idx < 0) return false;
            text = message.Substring(idx + 2).Trim();
            if (!IsAiCommand(text))
            {
                // not a command: a line with an @mention also goes to those members' upper LLM; the line itself is sent
                // to the other players as ordinary chat either way (자연어 명령 합의 1)
                try { TryHandleMention(text); } catch (Exception e) { LogError("mention handling failed", e); }
                return false;
            }
        }
        if (!InHostedGame(out World server))
        {
            return false; // not our world (joined someone else's): leave the message alone
        }
        string[] parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        Log($"chat command from host: '{text}'");
        if (WorldId == null)
        {
            Reply("AI: 아직 월드 정보를 읽는 중입니다. 잠시 뒤 다시 입력하세요.");
            return true;
        }
        // Which character: "/ai <member> <cmd ...>" (claude | gpt | gemini | grok, names too) or "/ai all <cmd>";
        // also "/ai on|off <member>". Without a member: the default character (Claude when it is in the world, else
        // the first character in the world); "/ai off" without a member turns every character off; "/ai" alone
        // shows every member.
        var targets = new List<AIAgent>();
        bool all = false;
        if (parts.Length > 1)
        {
            if (IsAll(parts[1])) { all = true; parts = Without(parts, 1); }
            else { var a = AIAgent.Find(parts[1]); if (a != null) { targets.Add(a); parts = Without(parts, 1); } }
        }
        string arg = parts.Length > 1 ? parts[1].ToLowerInvariant() : "status";
        bool onOff = arg == "on" || arg == "켜기" || arg == "off" || arg == "끄기";
        if (targets.Count == 0 && !all && onOff && parts.Length > 2)
        {
            if (IsAll(parts[2])) all = true;
            else { var a = AIAgent.Find(parts[2]); if (a != null) targets.Add(a); }
        }
        if (all) { targets.Clear(); targets.AddRange(AIAgent.All); }
        if (targets.Count == 0)
        {
            if (arg == "status" || arg == "상태") { Reply(StatusAll()); return true; }
            // stop and "goal 없음" without a member stop every member too (검수 F3, HEAD decision): members go on with
            // goals of their own (합의 3d), so stopping only the default one left the others running
            bool stopAll = arg == "stop" || arg == "멈춤" || ((arg == "goal" || arg == "목표") && parts.Length > 2 && AIGoals.IsNoGoalWord(parts[2]));
            if (arg == "off" || arg == "끄기" || stopAll) { foreach (var a in AIAgent.All) if (arg == "off" || arg == "끄기" || IsJoined(a)) targets.Add(a); }
            else targets.Add(DefaultAgent());
        }
        int order = 0;
        foreach (var a in targets)
        {
            // with "all", commands other than on/off go only to characters that are in the world
            if (all && !onOff && !IsJoined(a)) continue;
            using (AIAgent.Enter(a))
            {
                try { Dispatch(server, arg, parts, order++); }
                catch (Exception e) { LogError($"chat command for {a.Name} failed", e); }
            }
        }
        return true;
    }
    internal static List<MentionName> MentionNames => _mentionNames ?? (_mentionNames = AIMention.Members(Array.ConvertAll(AIAgent.All, a => (a.Id, a.Name))));

    // A host chat line with @mentions (자연어 명령 합의 1): kept in the world talk (AIPlanner.Chat) and given to each named
    // member in the world as host_said. @모두: every member in the world. A named member not in the world is told once.
    private static void TryHandleMention(string text)
    {
        var ids = AIMention.Parse(text, MentionNames, out bool all);
        if (ids.Count == 0) return;
        if (!InHostedGame(out World server) || WorldId == null) return;
        var targets = new List<AIAgent>();
        foreach (var id in ids) { var a = AIAgent.ById(id); if (a != null && (!all || IsJoined(a))) targets.Add(a); }
        Log($"MENTION from host to [{string.Join(",", targets.ConvertAll(a => a.Id))}]{(all ? " (모두)" : "")}: '{text}'");
        AIPlanner.Chat.Add(Time.realtimeSinceStartup, "host", "호스트", targets.ConvertAll(a => a.Id), all, text);
        if (targets.Count == 0) { Reply("AI: 월드에 들어와 있는 AI가 없어 말을 전하지 못했습니다. (/ai <멤버> on)"); return; }
        foreach (var a in targets)
        {
            using (AIAgent.Enter(a))
            {
                if (_stage != Stage.Walking || _serverPlayer == Entity.Null) { Reply($"{a.Name}은(는) 아직 월드에 들어와 있지 않아 말을 전하지 못했습니다. ({StageText()})"); continue; }
                try { AIPlanner.OnHostSaid(server, _serverPlayer, text); } catch (Exception e) { LogError($"host_said for {a.Name} failed", e); }
            }
        }
    }

    private static bool IsAll(string t) { t = t.ToLowerInvariant(); return t == "all" || t == "모두" || t == "다"; }

    private static string[] Without(string[] a, int i)
    {
        var l = new List<string>(a); l.RemoveAt(i); return l.ToArray();
    }

    // The character a command without a member goes to: Claude when it is in the world, else the first one in the
    // world, else Claude.
    internal static AIAgent DefaultAgent()
    {
        if (IsJoined(AIAgent.All[0])) return AIAgent.All[0];
        foreach (var a in AIAgent.All) if (IsJoined(a)) return a;
        return AIAgent.All[0];
    }

    // One command for the character in scope (AIAgent.Cur).
    private static void Dispatch(World server, string arg, string[] parts, int order)
    {
        if (AIBody.IsTestCommand(arg) && !AIBody.TestCommandsOn)
        {
            Log($"chat command '/ai {arg}' refused: test-only commands are off (switch file {AIBody.TestSwitchPath} not present)");
            Reply($"'/ai {arg}'은(는) 시험용 명령이라 꺼져 있습니다(AIPlayers\\testcmds.on 파일이 있을 때만). 평소 명령: /ai on|off, /ai goal …, /ai stop");
            return;
        }
        switch (arg)
        {
            case "on":
            case "켜기":
                TurnOn(order);
                break;
            case "off":
            case "끄기":
                TurnOff(server);
                break;
            case "status":
            case "상태":
                Reply(StatusText());
                break;
            case "verbose":
            case "자세히":
                // the program's own status lines in the chat too (합의 4; same switch as /ai goal 자세히)
                if (order > 0) break;   // one switch for everyone: "/ai all verbose" toggles once
                AIBrain.Verbose = !AIBrain.Verbose;
                Reply($"AI 진행 알림(기술 끝·목표 받음/완료/멈춤·쓰러짐 등) 채팅 표시: {(AIBrain.Verbose ? "켬" : "끔")}");
                break;
            case "base":
            case "기지":
                // the AI's base (생활-기지, AIBase): /ai base · /ai base 여기 (the host's tile) · /ai base 코어
                if (_stage != Stage.Walking || _serverPlayer == Entity.Null) Reply($"AI {AiName}가 아직 들어와 있지 않습니다. ({StageText()})");
                else AIBase.Command(server, _serverPlayer, parts);
                break;
            case "kgdump":
                AIKnowledge.Dump(server);   // test-only: game database -> AIPlayers/knowledge-dump.json
                break;
            case "farm":
            case "밭":
                // the AI's own field (먹거리 자급 2, AIFarm): /ai farm (state: plots, crops, stage times, seeds, tools, water)
                // · /ai farm 잊기 (forget the plots, test)
                if (_stage != Stage.Walking || _serverPlayer == Entity.Null) Reply($"AI {AiName}가 아직 들어와 있지 않습니다. ({StageText()})");
                else AIFarm.Command(server, _serverPlayer, parts);
                break;
            case "cook":
            case "요리":
                // the AI's own cooking pot (먹거리 자급 3, AICook): /ai cook (pots, ingredients, every pair's prediction,
                // cooked food in the bag) · /ai cook 잊기 (forget the pots, test)
                if (_stage != Stage.Walking || _serverPlayer == Entity.Null) Reply($"AI {AiName}가 아직 들어와 있지 않습니다. ({StageText()})");
                else AICook.Command(server, _serverPlayer, parts);
                break;
            case "route":
                // Diagnostic only: send the AI's input via route A (thin-client commands, normal) or B (write the
                // server's input buffer directly).
                SetRoute(parts.Length > 2 && parts[2].Equals("b", StringComparison.OrdinalIgnoreCase));
                break;
            default:
                if (AIBody.IsSkillCommand(arg))
                {
                    if (_stage != Stage.Walking || _serverPlayer == Entity.Null)
                    {
                        Reply($"AI {AiName}가 아직 들어와 있지 않습니다. ({StageText()})");
                    }
                    else
                    {
                        AIBody.HandleCommand(server, _serverPlayer, arg, parts);
                    }
                    break;
                }
                Reply("AI 명령: /ai [claude|gpt|gemini|grok|all] <명령> · /ai on|off [멤버] · /ai (모두 상태) · 목표: /ai goal 흙 5|탐험|따라와|보관|기지로|알아서|없음 · /ai stop · 기지: /ai base [여기|코어]" + (AIBody.TestCommandsOn ? " · 시험용(testcmds.on 켜짐): " + string.Join(" ", AIBody.TestCommands) : ""));
                break;
        }
    }

    private static bool IsAiCommand(string text)
    {
        return text.Equals("/ai", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("/ai ", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("/ai\t", StringComparison.OrdinalIgnoreCase);
    }

    private static string StageText()
    {
        switch (_stage)
        {
            case Stage.Disabled: return "들어와 있지 않음";
            case Stage.CountingDown: return "곧 들어옴";
            case Stage.ThinWorldCreated:
            case Stage.Connected:
            case Stage.Accepted:
            case Stage.StartGameSent: return "들어오는 중";
            case Stage.PlayerSpawned:
            case Stage.Walking: return "들어와 있음";
            case Stage.Stopped: return "오류로 멈춤, 로그 확인";
            default: return _stage.ToString();
        }
    }

    private static string StatusText()
    {
        bool on = AIWorldSettings.Members(WorldId).Contains(AIAgent.Cur.Id);
        return $"AI {AiName}({AIAgent.Cur.Id}): 이 월드에서 {(on ? "켜짐" : "꺼짐")} · 지금 {StageText()}{(_stage == Stage.Walking ? " · 목표 " + AIBrain.Goal.Text : "")} · 명령: /ai {AIAgent.Cur.Id} on|off|goal …";
    }

    private static string StatusAll()
    {
        var l = new List<string>();
        foreach (var a in AIAgent.All)
            using (AIAgent.Enter(a))
            {
                bool on = AIWorldSettings.Members(WorldId).Contains(a.Id);
                l.Add($"{a.Name} {(on ? "켜짐" : "꺼짐")}/{StageText()}{(_stage == Stage.Walking ? "/" + AIBrain.Goal.Text : "")}");
            }
        return "AI: " + string.Join(" · ", l) + " · 명령: /ai <멤버> on|off|goal …";
    }
    internal static void Reply(string msg, ChatKind kind = ChatKind.Command, string repeatKey = null)
    {
        var who = AIAgent.InScope;
        if (who != null && !msg.StartsWith("[") && (who.Index != 0 || Joined().Count > 1)) msg = $"[{who.Name}] " + msg;
        msg = ChatPolicy.FontSafe(msg);   // the game font has no arrows (실게임2 N5)
        if (!ChatPolicy.Show(kind, AIBrain.Verbose)) { Log("chat hidden (status): " + msg); return; }
        if (repeatKey != null && !_repeatGate.Allow((who?.Id ?? "-") + "|" + repeatKey, Time.realtimeSinceStartup)) { Log("chat hidden (same notice within 5 min): " + msg); return; }
        Log("chat reply: " + msg);
        _pendingReplies.Enqueue(msg);
    }

    private static void FlushReplies()
    {
        while (_pendingReplies.Count > 0)
        {
            string msg = _pendingReplies.Dequeue();
            try
            {
                var cw = Manager.ui != null ? Manager.ui.chatWindow : null;
                if (_renderChatText == null) _renderChatText = AccessTools.Method(typeof(ChatWindow), "RenderText");
                if (cw == null || _renderChatText == null)
                {
                    LogError($"chat reply not shown (chatWindow={(cw != null)}, RenderText={(_renderChatText != null)}): {msg}");
                    continue;
                }
                _renderChatText.Invoke(cw, new object[] { msg });
            }
            catch (Exception e)
            {
                LogError("chat reply failed: " + msg, e);
            }
        }
    }
}
