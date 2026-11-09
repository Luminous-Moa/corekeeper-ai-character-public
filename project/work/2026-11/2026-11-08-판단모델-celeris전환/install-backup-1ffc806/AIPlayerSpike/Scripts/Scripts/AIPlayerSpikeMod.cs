// AIPlayerSpike — throwaway experiment.
// Question: can a mod spawn a second, connection-backed player (for an AI) inside the host's
// own process, name it "Claude", and make it walk — without a second game window?
//
// Route: Netcode thin-client world (the game's own unused ThinClientConnectSystem does the
// connect / PlayerConnectRequestRPC / StartGameRPC handshake). The game's driver constructor
// cannot be reused for a second client world (it re-initialises the host client's shared
// PugNetworkInterface and both would share 127.0.0.1:7777), so this mod gives the thin world its
// own PugNetworkInterface on 127.0.0.1:7778 and pumps its packets to/from the server interface
// in a Harmony prefix on NetworkingManager.ServerNetworkUpdate.
//
// Every step logs with the prefix [AIPlayerSpike] to Player.log.
//
// AI character data: the AI does NOT reuse the host's character. It has its own CharacterData per world
// (appearance, inventory, skills, ...) in <persistentDataPath>/AIPlayers/<world ServerGuid>/<name>.json, created
// like a brand-new character on first use, swapped in server-side when the thin client's StartGameRPC is
// deserialised, and written back from the server player entity periodically and on leave.
//
// Body skills (AIBody.cs): one input executor; idle = stand still. Test commands (host only): /ai look, inv,
// goto me|x z, mine [x z|ore], attack, stop, walktest (old STEP3 pattern), route a|b (diagnostic input route).
//
// Action selection (AIDecisions.cs): "/ai goal <item> N | 탐험 | 따라와 | 없음" — the decisions model (DecisionsSettings.provider) picks one candidate
// action at a time from what the AI sees; the matching body skill runs. No goal = no API calls.
//
// Several AI characters (AIAgents.cs, 2026-11-04): one per chatroom member (claude, gpt, gemini, grok), each its own thin
// world on 127.0.0.1:7778+i, character file, playerGuid, memory and brain. Commands take a member: "/ai gemini goal 탐험",
// "/ai on gemini", "/ai all stop"; without one they go to the default character (Claude when it is in the world).
// settings.json: {"members": ["claude", "gemini"]} (an old {"aiEnabled": true} reads as ["claude"]).
//
// Participation is per world and OFF by default: the host types "/ai on", "/ai off" or "/ai" (status) in chat.
// The setting is kept in <persistentDataPath>/AIPlayers/<world ServerGuid>/settings.json. The command is caught
// in the host's own process before it is sent, so only the host can use it and other players never see it.
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

public partial class AIPlayerSpikeMod : IMod
{
    // ---- per-character state (multi-AI): every field below lives in one State per AI character (AIAgent.Cur).
    internal sealed class State
    {
        public Stage _stage = Stage.WaitingForHostedGame;
        public float _stageSince;
        public float _countdownStart;
        public float _countdownDelay = StartDelaySeconds;
        public World _thinWorld;
        public PugNetworkInterface ThinIf;
        public bool PumpEnabled;
        public long PacketsServerToThin;
        public long PacketsThinToServer;
        public long PacketsDropped;
        public bool RenameWindowOpen;
        public bool SwapDone;
        public int RenamePatchHits;
        public UnityEngine.Hash128 AiPlayerGuid;
        public StartGameRPC AiGuidHolder;
        public bool AiGuidSet;
        public float _lastAiSave;
        public Entity _thinConnection = Entity.Null;
        public int _thinNetworkId = -1;
        public Entity _serverPlayer = Entity.Null;
        public Entity _thinInputEntity = Entity.Null;
        public float _walkStart;
        public float3 _walkStartPos;
        public bool _serverSideInputFallback;
        public bool _fallbackChecked;
    }
    internal static State StateOf(AIAgent a) => a.Get<State>(AISlot.Mod);
    private static State S => StateOf(AIAgent.Cur);

    // The character being ticked / addressed (multi-AI: AIAgents.cs). Claude outside a character scope.
    private static string AiName => AIAgent.Cur.Name;
    private static string AiCharacterGuid => AIAgent.Cur.CharacterGuid;
    private const float StartDelaySeconds = 10f;   // after entering a world where the AI is on
    private const float ToggleDelaySeconds = 3f;   // after "/ai on"
    private const float JoinStaggerSeconds = 4f;   // between characters joining at the same time
    private const float StageTimeoutSeconds = 30f;

    internal static NetworkEndpoint ThinEndpoint => AIAgent.Cur.Endpoint;   // 127.0.0.1:7778 + member index
    internal static State St => S;   // for the driver constructor (needs the interface by ref)

    internal enum Stage
    {
        WaitingForHostedGame,
        Disabled,           // in a hosted world where the AI is off; waiting for "/ai on"
        CountingDown,
        ThinWorldCreated,
        Connected,
        Accepted,
        StartGameSent,
        PlayerSpawned,
        Walking,
        Stopped,
    }

    private static Stage _stage { get => S._stage; set => S._stage = value; }
    private static float _stageSince { get => S._stageSince; set => S._stageSince = value; }
    private static float _countdownStart { get => S._countdownStart; set => S._countdownStart = value; }
    private static float _countdownDelay { get => S._countdownDelay; set => S._countdownDelay = value; }
    private static bool _worldIdWaitLogged;
    private static readonly Queue<string> _pendingReplies = new Queue<string>();
    private static MethodInfo _renderChatText;
    internal static bool ChatPatched;
    private static World _thinWorld { get => S._thinWorld; set => S._thinWorld = value; }
    private static Harmony _harmony;
    private static bool _patched;

    // Thin client transport state (shared with the Harmony pump).
    internal static PugNetworkInterface ThinIf { get => S.ThinIf; set => S.ThinIf = value; }
    internal static bool PumpEnabled { get => S.PumpEnabled; set => S.PumpEnabled = value; }
    internal static long PacketsServerToThin { get => S.PacketsServerToThin; set => S.PacketsServerToThin = value; }
    internal static long PacketsThinToServer { get => S.PacketsThinToServer; set => S.PacketsThinToServer = value; }
    internal static long PacketsDropped { get => S.PacketsDropped; set => S.PacketsDropped = value; }
    private static FieldInfo _serverNetIfField;
    private static bool _pumpErrorLogged;

    // Character-data swap window (open from thin world creation until the AI player spawns).
    internal static bool RenameWindowOpen { get => S.RenameWindowOpen; set => S.RenameWindowOpen = value; }
    internal static bool SwapDone { get => S.SwapDone; set => S.SwapDone = value; }
    internal static int RenamePatchHits { get => S.RenamePatchHits; set => S.RenamePatchHits = value; }

    // Per-world AI identity. The thin client's playerGuid is Hash128.Parse(thinWorld.Name) (ThinClientConnectSystem),
    // so the thin world is named after a guid derived from (world ServerGuid, AI name): the same AI rejoins a given
    // world as the same server player entity. The server swaps character data only for this guid.
    internal static string WorldId;
    internal static UnityEngine.Hash128 AiPlayerGuid { get => S.AiPlayerGuid; set => S.AiPlayerGuid = value; }
    internal static StartGameRPC AiGuidHolder { get => S.AiGuidHolder; set => S.AiGuidHolder = value; } // AiPlayerGuid converted exactly as the game stores it in StartGameRPC
    internal static bool AiGuidSet { get => S.AiGuidSet; set => S.AiGuidSet = value; }

    // AI character persistence.
    private const float SaveIntervalSeconds = 60f;
    private static float _lastAiSave { get => S._lastAiSave; set => S._lastAiSave = value; }

    // Spawn / walk state.
    private static Entity _thinConnection { get => S._thinConnection; set => S._thinConnection = value; }
    private static int _thinNetworkId { get => S._thinNetworkId; set => S._thinNetworkId = value; }
    private static Entity _serverPlayer { get => S._serverPlayer; set => S._serverPlayer = value; }
    private static Entity _thinInputEntity { get => S._thinInputEntity; set => S._thinInputEntity = value; }
    private static float _walkStart { get => S._walkStart; set => S._walkStart = value; }
    private static float3 _walkStartPos { get => S._walkStartPos; set => S._walkStartPos = value; }
    private static bool _serverSideInputFallback { get => S._serverSideInputFallback; set => S._serverSideInputFallback = value; }
    private static bool _fallbackChecked { get => S._fallbackChecked; set => S._fallbackChecked = value; }

    // Inside a character scope every line carries "@<Name>" (several AIs write to the same Player.log).
    private static string Who => AIAgent.InScope != null ? "@" + AIAgent.InScope.Name + " " : "";

    public static void Log(string msg)
    {
        Debug.Log($"[AIPlayerSpike] t={Time.realtimeSinceStartup:F1} {Who}{msg}");
        // TEST ONLY decision log (AIPlayers/decisionlog.on): the decision, switch and skill lines in the same file as the
        // whole inputs, so one file tells what was asked, answered and done
        if (AITestSwitches.DecisionLog && (msg.StartsWith("DECISION #") || msg.StartsWith("SWITCH ") || msg.StartsWith("SKILL "))) AITestSwitches.Write(msg);
    }

    public static void LogError(string msg, Exception e = null)
    {
        Debug.LogError($"[AIPlayerSpike] t={Time.realtimeSinceStartup:F1} {Who}ERROR {msg}" + (e != null ? "\n" + e : ""));
    }

    // Is this server entity one of the AI characters' players (any member)? Used to find the human host.
    internal static bool IsAiPlayer(Entity e)
    {
        if (e == Entity.Null) return false;
        foreach (var a in AIAgent.All) if (StateOf(a)._serverPlayer == e) return true;
        return false;
    }

    // Members whose character is in the world now (joined and driven).
    internal static List<AIAgent> Joined()
    {
        var l = new List<AIAgent>();
        foreach (var a in AIAgent.All) if (StateOf(a)._stage == Stage.Walking) l.Add(a);
        return l;
    }

    internal static bool IsJoined(AIAgent a) => StateOf(a)._stage == Stage.Walking && StateOf(a)._serverPlayer != Entity.Null;
    internal static Entity ServerPlayerOf(AIAgent a) => StateOf(a)._serverPlayer;

    public void EarlyInit()
    {
        Log("EarlyInit: mod loaded. In a world this process hosts, the AI joins only if it is turned on for that world " +
            "(chat: /ai on | /ai off | /ai). Default is off.");
        AIReportRelevance.Info = ReportItemFacts;
        AIExpedition.ValuablePath = () => string.IsNullOrEmpty(WorldId) ? null : Path.Combine(AIMemory.CharDir(WorldId), ValuableStore.FileName);
    }

    // Game object data for the automatic report filter (Core/AIExpeditionRules.cs AIReportRelevance).
    private static AIReportRelevance.ItemFacts ReportItemFacts(string item)
    {
        try
        {
            if (!Enum.TryParse(item, out ObjectID id) || id == ObjectID.None) return null;
            ObjectInfo info = PugDatabase.GetObjectInfo(id);
            if (info == null) return null;
            var requires = new List<string>();
            if (info.requiredObjectsToCraft != null) foreach (var r in info.requiredObjectsToCraft) requires.Add(r.objectID.ToString());
            return new AIReportRelevance.ItemFacts { Type = info.objectType.ToString(), Rarity = (int)info.rarity, Requires = requires.ToArray() };
        }
        catch { return null; }
    }

    public void Init()
    {
    }

    public void ModObjectLoaded(UnityEngine.Object obj)
    {
    }

    public bool CanBeUnloaded()
    {
        return true;
    }

    public void Shutdown()
    {
        AIChatScreenLayer.Shutdown();
        AIInfoPanel.Shutdown();
        DisposeAll("mod shutdown");
        if (_harmony != null)
        {
            try { _harmony.UnpatchSelf(); } catch (Exception e) { LogError("unpatch failed", e); }
            _harmony = null;
            _patched = false;
        }
        AICompactFont.Shutdown();
    }

    public void Update()
    {
        bool hosted = false;
        World server = null;
        try { hosted = InHostedGame(out server); } catch (Exception e) { LogError("hosted-game check failed", e); }
        // Every member has its own state machine; members that are off just sit in Stage.Disabled.
        foreach (var a in AIAgent.All)
        {
            using (AIAgent.Enter(a))
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    Tick(hosted, server);
                }
                catch (Exception e)
                {
                    LogError($"exception in stage {_stage}; stopping this character", e);
                    SetStage(Stage.Stopped);
                }
                if (_stage == Stage.Walking)
                {
                    double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    a.TickMsSum += ms; a.TickN++; if (ms > a.TickMsMax) a.TickMsMax = ms;
                }
            }
        }
        try { AIChatHint.Frame(hosted); } catch (Exception e) { LogError("chat hint failed", e); }   // hint only in our own world (검수 작은 지적 5)
        try { AIChatScreenLayer.Frame(hosted); } catch (Exception e) { LogError("chat screen layer failed", e); }
        try { AIInfoPanel.Frame(hosted, server); } catch (Exception e) { LogError("AI info UI failed", e); }
        if (hosted)
        {
            FlushReplies();
            try { AIPerf.Frame(server); } catch (Exception e) { LogError("perf sample failed", e); }
        }
    }

    // Leave with every character (save + dispose each thin world).
    internal static void DisposeAll(string reason)
    {
        foreach (var a in AIAgent.All)
        {
            using (AIAgent.Enter(a))
            {
                try { DisposeThinWorld(reason); } catch (Exception e) { LogError("dispose failed", e); }
            }
        }
        // the world's talk goes with the world (검수 F5); the line numbers keep counting, so "new" stays right
        AIPlanner.Chat.Clear();
    }

    private static void SetStage(Stage s)
    {
        if (_stage != s)
        {
            Log($"stage {_stage} -> {s}");
        }
        _stage = s;
        _stageSince = Time.realtimeSinceStartup;
    }

    private static bool InHostedGame(out World server)
    {
        server = null;
        if (Manager.main == null || Manager.ecs == null) return false;
        server = Manager.ecs.ServerWorld;
        return server != null && server.IsCreated
            && Manager.ecs.ClientWorld != null
            && Manager.sceneHandler != null && Manager.sceneHandler.isInGame
            && Manager.main.player != null;
    }

    private static void Tick(bool hosted, World server)
    {
        if (!hosted)
        {
            if (_thinWorld != null)
            {
                DisposeThinWorld("left the hosted game");
            }
            if (_stage != Stage.WaitingForHostedGame)
            {
                ResetState();
                SetStage(Stage.WaitingForHostedGame);
            }
            return;
        }

        float now = Time.realtimeSinceStartup;
        switch (_stage)
        {
            case Stage.WaitingForHostedGame:
            {
                // Patch once per game session as soon as a hosted world is up, so the chat command works right away.
                InstallPatches();
                if (!TryGetWorldId(server, out string wid))
                {
                    if (!_worldIdWaitLogged)
                    {
                        _worldIdWaitLogged = true;
                        Log("hosted world is up but its ServerGuidCD is not readable yet; waiting");
                    }
                    break;
                }
                _worldIdWaitLogged = false;
                WorldId = wid;
                var members = AIWorldSettings.Members(wid);
                bool on = members.Contains(AIAgent.Cur.Id);
                Log($"local player is in a hosted world (server world '{server.Name}', client world '{Manager.ecs.ClientWorld.Name}', world id={wid}). " +
                    $"AI '{AiName}' ({AIAgent.Cur.Id}) in this world: {(on ? "ON" : "OFF")} (members [{string.Join(",", members)}], setting {AIWorldSettings.PathFor(wid)}{(AIWorldSettings.Exists(wid) ? "" : ", absent = default off")}). " +
                    $"Chat command patch {(ChatPatched ? "installed" : "NOT installed")}.");
                if (on)
                {
                    // members join one after another (not in the same frame)
                    StartCountdown(StartDelaySeconds + JoinStaggerSeconds * members.IndexOf(AIAgent.Cur.Id));
                }
                else
                {
                    SetStage(Stage.Disabled);
                }
                break;
            }

            case Stage.Disabled:
                break;

            case Stage.CountingDown:
                if (now - _countdownStart >= _countdownDelay)
                {
                    Step1_CreateThinWorld(server);
                }
                break;

            case Stage.ThinWorldCreated:
            case Stage.Connected:
            case Stage.Accepted:
            case Stage.StartGameSent:
                Step1b_TrackHandshake(server);
                break;

            case Stage.PlayerSpawned:
                // Let the spawn-from-core animation finish before judging movement.
                if (now - _stageSince >= 5f)
                {
                    // The server applies the inventory one frame after spawning; by now it is in place.
                    AICharacterStore.LogInventories(server, _serverPlayer);
                    AICharacterStore.EnsureWeaponOnce(server, _serverPlayer);
                    Step2_NameAndPrepareInput(server);
                }
                break;

            case Stage.Walking:
                Step3_Drive(server);
                if (now - _lastAiSave >= SaveIntervalSeconds)
                {
                    _lastAiSave = now;
                    AICharacterStore.SaveFromServer(server, _serverPlayer, "periodic");
                }
                break;

            case Stage.Stopped:
                break;
        }
    }

    private static void ResetState()
    {
        _thinConnection = Entity.Null;
        _thinNetworkId = -1;
        _serverPlayer = Entity.Null;
        _thinInputEntity = Entity.Null;
        _serverSideInputFallback = false;
        _fallbackChecked = false;
        RenameWindowOpen = false;
        AIBrain.Reset();
        AIBody.Reset();
        AICharacterStore.ResetWeaponCheck();
        AIVitals.Reset();
        AIPlanner.Reset();
        PacketsServerToThin = PacketsThinToServer = PacketsDropped = 0;
    }

    private static void StartCountdown(float delay)
    {
        _countdownStart = Time.realtimeSinceStartup;
        _countdownDelay = delay;
        Log($"AI '{AiName}' will join in {delay}s");
        SetStage(Stage.CountingDown);
    }

    // The member names a mention can use (AIMention; in-game names, ids, Korean names) and 모두.
    private static List<MentionName> _mentionNames;

    private static void SetRoute(bool routeB)
    {
        _serverSideInputFallback = routeB;
        if (_thinWorld != null && _thinWorld.IsCreated)
        {
            var em = _thinWorld.EntityManager;
            if (em.Exists(_thinConnection) && em.HasComponent<CommandTarget>(_thinConnection))
            {
                em.SetComponentData(_thinConnection, new CommandTarget { targetEntity = routeB ? Entity.Null : _thinInputEntity });
            }
        }
        Log($"input route set to {(routeB ? "B (server-side input buffer)" : "A (thin-client commands)")}");
        Reply($"AI 입력 경로: {(routeB ? "B(서버 입력 버퍼, 진단용)" : "A(정상)")}");
    }

    private static void TurnOn(int order = 0)
    {
        AIWorldSettings.SetMember(WorldId, AIAgent.Cur.Id, true);
        if (_stage == Stage.Disabled || _stage == Stage.Stopped)
        {
            if (_thinWorld != null)
            {
                DisposeThinWorld("restart after /ai on");
            }
            ResetState();
            float delay = ToggleDelaySeconds + JoinStaggerSeconds * order;
            StartCountdown(delay);
            Reply($"AI {AiName} 켜짐: {delay:F0}초 뒤 들어옵니다. 이 월드에 저장됨.");
        }
        else
        {
            Reply($"AI {AiName}는 이미 켜져 있습니다. ({StageText()})");
        }
    }

    private static void TurnOff(World server)
    {
        bool wasOn = AIWorldSettings.Members(WorldId).Contains(AIAgent.Cur.Id);
        AIWorldSettings.SetMember(WorldId, AIAgent.Cur.Id, false);
        bool wasIn = _thinWorld != null;
        if (!wasOn && !wasIn && (_stage == Stage.Disabled || _stage == Stage.WaitingForHostedGame)) return;   // "/ai off" for all: quiet for members that were off
        if (wasIn)
        {
            // Kick the AI's connection on the server (the game then disables its player entity as for any player
            // who leaves), and save + dispose the thin world. DisposeThinWorld saves before tearing anything down.
            RequestServerDisconnect(server);
            DisposeThinWorld("turned off by host (/ai off)");
        }
        ResetState();
        SetStage(Stage.Disabled);
        Reply(wasIn ? $"AI {AiName} 꺼짐: 저장하고 나갔습니다. 이 월드에 저장됨." : $"AI {AiName} 꺼짐. 이 월드에 저장됨.");
    }

    private static void RequestServerDisconnect(World server)
    {
        try
        {
            if (server == null || !server.IsCreated || _serverPlayer == Entity.Null) return;
            var sem = server.EntityManager;
            if (!sem.Exists(_serverPlayer) || !sem.HasComponent<PlayerGhost>(_serverPlayer)) return;
            Entity conn = sem.GetComponentData<PlayerGhost>(_serverPlayer).connection;
            if (conn != Entity.Null && sem.Exists(conn) && !sem.HasComponent<NetworkStreamRequestDisconnect>(conn))
            {
                sem.AddComponentData(conn, new NetworkStreamRequestDisconnect { Reason = NetworkStreamDisconnectReason.ConnectionClose });
                Log($"AI leave: requested server-side disconnect of the AI connection {conn}");
            }
        }
        catch (Exception e)
        {
            LogError("AI leave: server-side disconnect request failed", e);
        }
    }

    // Replies are shown on the host's chat on the next frame (after the game has echoed the typed command).
    // With several characters, a character's own messages start with "[Name] ".
    // kind (자연어 명령 합의 4, ChatPolicy): Status lines (the program's own progress: skill ended, goal set/done/stopped,
    // down, self-defence, "thinking", free time) go to the log only unless verbose is on; say, errors and answers to the
    // host's commands are shown. The default is Command (an answer to something the host typed).
    // repeatKey (실게임2 N6): an error notice that can come again and again (a retry) names its kind; the same kind is
    // shown once per character in five minutes, the rest go to the log only.
    private static readonly ChatRepeatGate _repeatGate = new ChatRepeatGate();

    // ---------------------------------------------------------------- Step 1: thin client world

    private static bool InstallPatches()
    {
        if (_harmony != null) return _patched; // patch at most once per game session
        _harmony = new Harmony("AIPlayerSpike");
        bool ok = true;

        _serverNetIfField = AccessTools.Field(typeof(NetworkingManager), "serverNetIf");
        Log($"patch: NetworkingManager.serverNetIf field {(_serverNetIfField != null ? "found" : "NOT FOUND")}");
        ok &= _serverNetIfField != null;

        ok &= TryPatch(AccessTools.Method(typeof(NetworkingManager), "ServerNetworkUpdate"),
            prefix: AccessTools.Method(typeof(AIPlayerSpikePatches), nameof(AIPlayerSpikePatches.ServerNetworkUpdatePrefix)));
        ok &= TryPatch(AccessTools.Method(typeof(ECSManager), "UnloadWorldsInternal"),
            prefix: AccessTools.Method(typeof(AIPlayerSpikePatches), nameof(AIPlayerSpikePatches.UnloadWorldsPrefix)));
        // Required: know which playerGuid the server is spawning (StartGameRPCSystem's spawn lambda), then swap the
        // deserialised character data only for the AI's guid.
        MethodInfo spawnLambda = FindStartGameSpawnLambda();
        Log($"patch: StartGameRPCSystem spawn lambda {(spawnLambda != null ? spawnLambda.DeclaringType.Name + "." + spawnLambda.Name : "NOT FOUND")}");
        ok &= TryPatch(spawnLambda,
            prefix: AccessTools.Method(typeof(AIPlayerSpikePatches), nameof(AIPlayerSpikePatches.SpawnLambdaPrefix)),
            postfix: AccessTools.Method(typeof(AIPlayerSpikePatches), nameof(AIPlayerSpikePatches.SpawnLambdaPostfix)));
        ok &= TryPatch(AccessTools.Method(typeof(SaveManager), nameof(SaveManager.GetCharacterDataFromSerialized)),
            postfix: AccessTools.Method(typeof(AIPlayerSpikePatches), nameof(AIPlayerSpikePatches.CharacterDataPostfix)));
        // Host chat command (/ai on|off|status). Not required for joining; without it the AI can only be switched by the file.
        ChatPatched = TryPatch(AccessTools.Method(typeof(NetworkCommSystem), nameof(NetworkCommSystem.SendChatMessage)),
            prefix: AccessTools.Method(typeof(AIPlayerSpikePatches), nameof(AIPlayerSpikePatches.ChatSendPrefix)));
        // @mention completion (자연어 명령 합의 1): Tab in the chat box completes the mention instead of closing the box.
        // Not required: without it the hint line still shows and the host types the name.
        TryPatch(AccessTools.Method(typeof(ChatWindow), nameof(ChatWindow.Deactivate)),
            prefix: AccessTools.Method(typeof(AIChatHint), nameof(AIChatHint.DeactivatePrefix)));
        // Chat lines stay clear 7 s, then vanish over 1 s (채팅창 개선, AIChatWindow.cs). Not required: without it the
        // lines fade as the game has it.
        AIChatWindow.Patched(TryPatch(AccessTools.Method(typeof(ChatWindow), "LateUpdate"),
            postfix: AccessTools.Method(typeof(AIChatWindow), nameof(AIChatWindow.LateUpdatePostfix))));

        TryPatch(AccessTools.Method(typeof(PugText), "SetPugFont"),
            prefix: AccessTools.Method(typeof(AICompactFont), nameof(AICompactFont.SetPugFontPrefix)));
        bool chatScreen = TryPatch(AccessTools.Method(typeof(PugText), "OnProfanityChecked"),
            postfix: AccessTools.Method(typeof(AIChatScreenLayer), nameof(AIChatScreenLayer.AfterRender)));
        chatScreen &= TryPatch(AccessTools.Method(typeof(PugText), "Clear"),
            prefix: AccessTools.Method(typeof(AIChatScreenLayer), nameof(AIChatScreenLayer.BeforeRelease)));
        chatScreen &= TryPatch(AccessTools.Method(typeof(PugText), "OnDisable"),
            prefix: AccessTools.Method(typeof(AIChatScreenLayer), nameof(AIChatScreenLayer.BeforeRelease)));
        AIChatScreenLayer.Patched(chatScreen);
        // Gate actual local player queries, including the first mouse-down frame and release outside the UI.
        // Fail closed: if any input hook cannot install, no interactive panel is created.
        bool uiInput = true;
        foreach (string name in new[] { "IsButtonCurrentlyDown", "WasButtonPressedDownThisFrame", "WasButtonReleasedThisFrame" })
            uiInput &= TryPatch(AccessTools.Method(typeof(PlayerInput), name),
                prefix: AccessTools.Method(typeof(AIInfoPanel), nameof(AIInfoPanel.ButtonPrefix)));
        uiInput &= TryPatch(AccessTools.Method(typeof(PlayerInput), "GetRawAxisInput"),
            prefix: AccessTools.Method(typeof(AIInfoPanel), nameof(AIInfoPanel.VectorPrefix)));
        uiInput &= TryPatch(AccessTools.Method(typeof(PlayerInput), "GetInputAxisValue", new[] { typeof(PlayerInput.InputAxisType), typeof(PlayerInput.InputAxisType) }),
            prefix: AccessTools.Method(typeof(AIInfoPanel), nameof(AIInfoPanel.VectorPrefix)));
        uiInput &= TryPatch(AccessTools.Method(typeof(PlayerInput), "GetInputAxisValue", new[] { typeof(PlayerInput.InputAxisType) }),
            prefix: AccessTools.Method(typeof(AIInfoPanel), nameof(AIInfoPanel.AxisPrefix)));
        uiInput &= TryPatch(AccessTools.Method(typeof(PlayerInput), "GetYAxisInput"),
            prefix: AccessTools.Method(typeof(AIInfoPanel), nameof(AIInfoPanel.AxisPrefix)));
        AIInfoPanel.Patched(uiInput);

        _patched = ok;
        return ok;
    }

    // The generated job in StartGameRPCSystem whose body is (in StartGameRPC req, in ReceiveRpcCommandRequest reqSrc)
    // and which calls SaveManager.GetCharacterDataFromSerialized. Found by signature, not by its generated name.
    private static MethodInfo FindStartGameSpawnLambda()
    {
        foreach (var t in typeof(StartGameRPCSystem).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (m.Name != "OriginalLambdaBody") continue;
                var ps = m.GetParameters();
                if (ps.Length == 2
                    && ps[0].ParameterType.IsByRef && ps[0].ParameterType.GetElementType() == typeof(StartGameRPC)
                    && ps[1].ParameterType.IsByRef && ps[1].ParameterType.GetElementType() == typeof(ReceiveRpcCommandRequest))
                {
                    return m;
                }
            }
        }
        return null;
    }

    private static bool TryPatch(MethodInfo target, MethodInfo prefix = null, MethodInfo postfix = null)
    {
        if (target == null)
        {
            LogError("patch target not found (prefix=" + prefix?.Name + " postfix=" + postfix?.Name + ")");
            return false;
        }
        try
        {
            _harmony.Patch(target,
                prefix: prefix != null ? new HarmonyMethod(prefix) : null,
                postfix: postfix != null ? new HarmonyMethod(postfix) : null);
            Log($"patch: {target.DeclaringType?.Name}.{target.Name} patched");
            return true;
        }
        catch (Exception e)
        {
            LogError($"patch: {target.DeclaringType?.Name}.{target.Name} failed", e);
            return false;
        }
    }

    private static void Step1_CreateThinWorld(World server)
    {
        Log("STEP 1: create thin client world");
        if (!InstallPatches())
        {
            LogError("STEP 1 FAILED: required Harmony patches could not be installed");
            SetStage(Stage.Stopped);
            return;
        }

        Log($"STEP 1: DefaultGameObjectInjectionWorld='{World.DefaultGameObjectInjectionWorld?.Name}' existing thin worlds={ClientServerBootstrap.ThinClientWorlds.Count}");

        // Per-world identity: the world's own ServerGuidCD (generated once per world, saved inside the world).
        if (!TryGetWorldId(server, out string worldId))
        {
            LogError("STEP 1 FAILED: the server world has no ServerGuidCD; cannot tell which world this is, not joining");
            SetStage(Stage.Stopped);
            return;
        }
        WorldId = worldId;
        AiPlayerGuid = UnityEngine.Hash128.Compute("AIPlayerSpike:" + worldId + ":" + AiName);
        StartGameRPC holder = default;
        holder.playerGuid = AiPlayerGuid; // same conversion as ThinClientConnectSystem
        AiGuidHolder = holder;
        AiGuidSet = true;
        WorldInfo wi = null;
        try { wi = Manager.saves.GetWorldInfo(); } catch { }
        Log($"AI identity: world id (ServerGuidCD)={worldId} world name='{wi?.name}' worldInfo.guid={wi?.guid} world slot={Manager.saves.GetWorldId()} " +
            $"AI '{AiName}' playerGuid={AiPlayerGuid} (as StartGameRPC: {AiGuidHolder.playerGuid})");

        // The AI must join with its own (per-world) character, never with a copy of the host's.
        if (!AICharacterStore.Prepare(AiName, AiCharacterGuid, worldId))
        {
            LogError("STEP 1 FAILED: AI character data could not be loaded or created; not joining (would copy the host's character)");
            SetStage(Stage.Stopped);
            return;
        }

        LogAllServerPlayers(server, "before the AI joins");

        var previous = NetworkStreamReceiveSystem.DriverConstructor;
        RenameWindowOpen = true;
        SwapDone = false;
        try
        {
            NetworkStreamReceiveSystem.DriverConstructor = new ThinClientDriverConstructor();
            _thinWorld = CreateNamedThinClientWorld(AiPlayerGuid.ToString());
        }
        catch (Exception e)
        {
            LogError("STEP 1 FAILED: ClientServerBootstrap.CreateThinClientWorld threw", e);
            SetStage(Stage.Stopped);
            return;
        }
        finally
        {
            NetworkStreamReceiveSystem.DriverConstructor = previous;
        }

        if (_thinWorld == null || !PumpEnabled)
        {
            LogError($"STEP 1 FAILED: thin world={_thinWorld?.Name ?? "null"} pumpEnabled={PumpEnabled} (driver constructor not called?)");
            SetStage(Stage.Stopped);
            return;
        }

        bool hasConnectSystem = _thinWorld.GetExistingSystem(typeof(ThinClientConnectSystem)) != SystemHandle.Null;
        bool hasSendCmd = _thinWorld.GetExistingSystem(typeof(CommandSendSystemGroup)) != SystemHandle.Null;
        var guid = UnityEngine.Hash128.Parse(_thinWorld.ToString());
        Log($"STEP 1 OK: created '{_thinWorld.Name}' flags={_thinWorld.Flags} systems={_thinWorld.Systems.Count} " +
            $"ThinClientConnectSystem={(hasConnectSystem ? "present" : "MISSING")} CommandSendSystemGroup={(hasSendCmd ? "present" : "MISSING")} " +
            $"thinEndpoint={ThinEndpoint} playerGuid(from world name)={guid} expected={AiPlayerGuid} hostGuid={Manager.saves.GetCharacterGuid()}");
        if (guid != AiPlayerGuid)
        {
            LogError($"STEP 1 FAILED: thin world name does not parse to the AI playerGuid ({guid} != {AiPlayerGuid}); not joining");
            DisposeThinWorld("playerGuid mismatch");
            return;
        }
        SetStage(Stage.ThinWorldCreated);
    }

    // Same as ClientServerBootstrap.CreateThinClientWorld, but with our own name: the game derives the thin
    // client's playerGuid from the world name, so a fixed name gives a fixed playerGuid.
    private static World CreateNamedThinClientWorld(string name)
    {
        var world = new World(name, WorldFlags.GameThinClient);
        var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ThinClientSimulation);
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
        ClientServerBootstrap.ThinClientWorlds.Add(world);
        return world;
    }

    private static bool TryGetWorldId(World server, out string worldId)
    {
        worldId = null;
        using (var q = server.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ServerGuidCD>()))
        {
            if (q.CalculateEntityCount() != 1) return false;
            worldId = q.GetSingleton<ServerGuidCD>().Value.ToString();
        }
        return !string.IsNullOrEmpty(worldId);
    }

    // All player entities on the server, including disabled ones (players who left), for pile-up checks.
    internal static void LogAllServerPlayers(World server, string label)
    {
        try
        {
            var em = server.EntityManager;
            var desc = new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PlayerGhost>() },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            };
            using (var q = em.CreateEntityQuery(desc))
            using (var ents = q.ToEntityArray(Allocator.Temp))
            {
                var parts = new List<string>();
                int aiLike = 0;
                foreach (var e in ents)
                {
                    var pg = em.GetComponentData<PlayerGhost>(e);
                    string name = em.HasComponent<PlayerCustomizationCD>(e) ? em.GetComponentData<PlayerCustomizationCD>(e).customization.name.ToString() : "?";
                    bool disabled = em.HasComponent<Disabled>(e);
                    var pos = em.HasComponent<LocalTransform>(e) ? em.GetComponentData<LocalTransform>(e).Position : float3.zero;
                    if (name == AiName) aiLike++;
                    parts.Add($"{e} '{name}' guid={pg.playerGuid} {(disabled ? "disabled" : "enabled")} pos=({pos.x:F2},{pos.z:F2})");
                }
                Log($"server players ({label}): total={ents.Length} named '{AiName}'={aiLike}: " + string.Join(" | ", parts));
            }
        }
        catch (Exception e)
        {
            LogError("listing server players failed", e);
        }
    }

    private static void Step1b_TrackHandshake(World server)
    {
        if (_thinWorld == null || !_thinWorld.IsCreated)
        {
            LogError("thin world vanished");
            SetStage(Stage.Stopped);
            return;
        }
        var em = _thinWorld.EntityManager;

        if (_stage == Stage.ThinWorldCreated)
        {
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>()))
            {
                if (q.CalculateEntityCount() > 0)
                {
                    _thinConnection = q.GetSingletonEntity();
                    var c = em.GetComponentData<NetworkStreamConnection>(_thinConnection);
                    Log($"STEP 1: thin world issued Connect to {Manager.networking.GetLocalEndpoint()} (connection state {c.CurrentState})");
                    SetStage(Stage.Connected);
                }
            }
        }
        else if (_stage == Stage.Connected)
        {
            if (em.Exists(_thinConnection) && em.HasComponent<NetworkId>(_thinConnection))
            {
                _thinNetworkId = em.GetComponentData<NetworkId>(_thinConnection).Value;
                Log($"STEP 1: Netcode connection established, NetworkId={_thinNetworkId} (packets s->t={PacketsServerToThin} t->s={PacketsThinToServer})");
                SetStage(Stage.Accepted);
            }
        }
        else if (_stage == Stage.Accepted)
        {
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ServerSeedCD>()))
            {
                bool accepted = q.CalculateEntityCount() > 0;
                bool inGame = em.Exists(_thinConnection) && em.HasComponent<NetworkStreamInGame>(_thinConnection);
                if (inGame)
                {
                    Log($"STEP 1: server accepted PlayerConnectRequestRPC (ServerSeedCD={accepted}); thin client sent StartGameRPC and is NetworkStreamInGame");
                    SetStage(Stage.StartGameSent);
                }
            }
        }
        else if (_stage == Stage.StartGameSent)
        {
            Entity player = FindServerPlayerFor(server, _thinNetworkId);
            if (player != Entity.Null)
            {
                _serverPlayer = player;
                var sem = server.EntityManager;
                var pos = sem.GetComponentData<LocalTransform>(player).Position;
                var pg = sem.GetComponentData<PlayerGhost>(player);
                var cust = sem.GetComponentData<PlayerCustomizationCD>(player);
                Log($"STEP 1 OK: SECOND PLAYER SPAWNED on server: entity={player} pos={pos} playerIndex={pg.playerIndex} " +
                    $"playerGuid={pg.playerGuid} name='{cust.customization.name}' onlineName='{pg.onlineName}' renamePatchHits={RenamePatchHits}");
                RenameWindowOpen = false;
                bool guidOk = pg.playerGuid.Equals(AiGuidHolder.playerGuid);
                Log($"AI identity: spawned AI player guid={pg.playerGuid} expected={AiGuidHolder.playerGuid} match={guidOk} world={WorldId}");
                if (!SwapDone)
                {
                    LogError("AI character: the server did NOT receive the AI's own character data (swap patch did not run); this player is a copy of the host");
                }
                LogAllServerPlayers(server, "AI spawned");
                _lastAiSave = Time.realtimeSinceStartup;
                        Log("waiting 5s for the spawn animation before naming check and walking");
                SetStage(Stage.PlayerSpawned);
                return;
            }
        }

        if (Time.realtimeSinceStartup - _stageSince > StageTimeoutSeconds)
        {
            LogError($"STEP 1 STALLED in stage {_stage} for {StageTimeoutSeconds}s. Diagnostics follow.");
            LogDiagnostics(server);
            RenameWindowOpen = false;
            SetStage(Stage.Stopped);
        }
    }

    private static Entity FindServerPlayerFor(World server, int networkId)
    {
        if (networkId < 0) return Entity.Null;
        var sem = server.EntityManager;
        using (var q = sem.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>(), ComponentType.ReadOnly<GhostOwner>(), ComponentType.ReadOnly<LocalTransform>()))
        using (var ents = q.ToEntityArray(Allocator.Temp))
        {
            foreach (var e in ents)
            {
                if (sem.GetComponentData<GhostOwner>(e).NetworkId == networkId)
                {
                    return e;
                }
            }
        }
        return Entity.Null;
    }

    private static void LogDiagnostics(World server)
    {
        try
        {
            Log($"diag: packets server->thin={PacketsServerToThin} thin->server={PacketsThinToServer} dropped={PacketsDropped} pumpEnabled={PumpEnabled}");
            if (_thinWorld != null && _thinWorld.IsCreated)
            {
                var em = _thinWorld.EntityManager;
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>()))
                {
                    Log($"diag: thin connections={q.CalculateEntityCount()}");
                    if (q.CalculateEntityCount() == 1)
                    {
                        var e = q.GetSingletonEntity();
                        var c = em.GetComponentData<NetworkStreamConnection>(e);
                        Log($"diag: thin connection state={c.CurrentState} protocolVersionReceived={c.ProtocolVersionReceived} " +
                            $"hasNetworkId={em.HasComponent<NetworkId>(e)} inGame={em.HasComponent<NetworkStreamInGame>(e)}");
                    }
                }
                LogProtocol("thin", _thinWorld);
                using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<ServerSeedCD>()))
                {
                    Log($"diag: thin got PlayerConnectResponse accept (ServerSeedCD)={q.CalculateEntityCount() > 0}");
                }
            }
            LogProtocol("server", server);
            var sem = server.EntityManager;
            using (var q = sem.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>()))
            {
                Log($"diag: server connections={q.CalculateEntityCount()}");
            }
            using (var q = sem.CreateEntityQuery(ComponentType.ReadOnly<PlayerGhost>()))
            {
                Log($"diag: server PlayerGhost entities={q.CalculateEntityCount()}");
            }
        }
        catch (Exception e)
        {
            LogError("diagnostics failed", e);
        }
    }

    private static void LogProtocol(string label, World w)
    {
        using (var q = w.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkProtocolVersion>()))
        {
            if (q.CalculateEntityCount() == 1)
            {
                var v = q.GetSingleton<NetworkProtocolVersion>();
                Log($"diag: {label} protocol netcode={v.NetCodeVersion} game={v.GameVersion} rpc={v.RpcCollectionVersion} components={v.ComponentCollectionVersion}");
            }
            else
            {
                Log($"diag: {label} has {q.CalculateEntityCount()} NetworkProtocolVersion singletons");
            }
        }
    }

    // ---------------------------------------------------------------- Step 2: name + input target

    private static void Step2_NameAndPrepareInput(World server)
    {
        Log("STEP 2: name the second player");
        var sem = server.EntityManager;
        if (!sem.Exists(_serverPlayer))
        {
            LogError("STEP 2 FAILED: server player entity no longer exists");
            SetStage(Stage.Stopped);
            return;
        }

        var cust = sem.GetComponentData<PlayerCustomizationCD>(_serverPlayer);
        if (cust.customization.name.ToString() != AiName)
        {
            cust.customization.name = new FixedString32Bytes(AiName);
            cust.triggerCount++;
            sem.SetComponentData(_serverPlayer, cust);
            Log($"STEP 2: name was not '{AiName}' at spawn (character-data patch hits={RenamePatchHits}); set PlayerCustomizationCD.name server-side");
        }
        var pg = sem.GetComponentData<PlayerGhost>(_serverPlayer);
        if (pg.onlineName.ToString() != AiName)
        {
            pg.onlineName = new FixedString32Bytes(AiName);
            sem.SetComponentData(_serverPlayer, pg);
        }
        Log($"STEP 2 OK: server player name='{sem.GetComponentData<PlayerCustomizationCD>(_serverPlayer).customization.name}' onlineName='{sem.GetComponentData<PlayerGhost>(_serverPlayer).onlineName}'");

        Log("STEP 3: prepare thin-client input (InputBufferData<ClientInputData> + CommandTarget)");
        var em = _thinWorld.EntityManager;
        _thinInputEntity = em.CreateEntity();
        em.AddBuffer<InputBufferData<ClientInputData>>(_thinInputEntity);
        if (em.HasComponent<CommandTarget>(_thinConnection))
        {
            em.SetComponentData(_thinConnection, new CommandTarget { targetEntity = _thinInputEntity });
        }
        else
        {
            em.AddComponentData(_thinConnection, new CommandTarget { targetEntity = _thinInputEntity });
        }
        _walkStart = Time.realtimeSinceStartup;
        _walkStartPos = sem.GetComponentData<LocalTransform>(_serverPlayer).Position;
        AIBody.Reset();
        Log($"STEP 3: input executor ready at {_walkStartPos}: idle = stand still; skills via /ai look|inv|goto|mine|attack|stop; old walking pattern via /ai walktest");
        SetStage(Stage.Walking);
    }

    // ---------------------------------------------------------------- Step 3: the single input executor

    // Fills in the network fields of the one ClientInput the body executor (AIBody.Tick) produced for this tick.
    private static ClientInputData MakeInput(NetworkTick tick, NetworkTick interpolationTick, ClientInput ci)
    {
        ci.Tick = tick;
        ci.prefersKeyboardAndMouse = false;
        ci.deterministicInterpolationDelay = 0;
        if (interpolationTick.IsValid)
        {
            ci.deterministicInterpolationDelay = (byte)math.clamp(tick.TicksSince(interpolationTick), 0, 255);
        }
        return UnsafeUtility.As<ClientInput, ClientInputData>(ref ci);
    }

    // Every tick: ask the executor for the one input (idle = stand still) and send it via route A (thin-client
    // commands over Netcode). Route B (write the server's input buffer) is only a diagnostic fallback that the
    // walking test can switch on when route A does not move the player.
    private static void Step3_Drive(World server)
    {
        var sem = server.EntityManager;
        if (!sem.Exists(_serverPlayer) || _thinWorld == null || !_thinWorld.IsCreated)
        {
            AIBrain.CancelPendingChoices("server player or thin world gone");
            LogError("STEP 3: server player or thin world gone; stopping");
            SetStage(Stage.Stopped);
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (now - _lastAiSave >= SaveIntervalSeconds)
        {
            _lastAiSave = now;
            AICharacterStore.SaveFromServer(server, _serverPlayer, "periodic");
        }

        try { AIBrain.Tick(server, _serverPlayer, now); }
        catch (Exception e) { LogError("brain tick failed", e); }
        ClientInput ci = AIBody.Tick(server, _serverPlayer, now);
        uint traceTick = 0;

        if (!_serverSideInputFallback)
        {
            var em = _thinWorld.EntityManager;
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<NetworkTime>()))
            {
                if (q.CalculateEntityCount() == 1 && em.Exists(_thinInputEntity))
                {
                    var nt = q.GetSingleton<NetworkTime>();
                    if (nt.ServerTick.IsValid)
                    {
                        // 코어-안전지대 단계 2: an interact-with-object press (a door) for this whole server tick
                        bool press = AIBody.UseBitFor(nt.ServerTick.TickIndexForValidTick);
                        // 검수 M1 (25-문단계멈춰서기): a settle correction moves the body this one tick only
                        AIBody.NudgeFor(nt.ServerTick.TickIndexForValidTick, ref ci, out bool nudgeAhead);
                        var ciOff = ci;
                        if (nudgeAhead) ciOff.movementDirection = Unity.Mathematics.float2.zero;
                        if (press) ci.SetButtonState(CommandInputButtonStateNames.InteractWithObject_Pressed, true);
                        var buf = em.GetBuffer<InputBufferData<ClientInputData>>(_thinInputEntity);
                        // review G4: the next two ticks get the same input with the bit off now, so a tick without a
                        // new frame does not reuse the pressed input (GetDataAtTick takes the latest older one);
                        // 검수 M1: the same for a correction's move (the next two ticks without it)
                        if (press || nudgeAhead)
                            for (uint k = 1; k <= 2; k++)
                            {
                                var nextTick = nt.ServerTick; nextTick.Add(k);
                                buf.AddCommandData(new InputBufferData<ClientInputData> { Tick = nextTick, InternalInput = MakeInput(nextTick, nt.InterpolationTick, ciOff) });
                            }
                        buf.AddCommandData(new InputBufferData<ClientInputData>
                        {
                            Tick = nt.ServerTick,
                            InternalInput = MakeInput(nt.ServerTick, nt.InterpolationTick, ci),
                        });
                        traceTick = nt.ServerTick.TickIndexForValidTick;
                    }
                }
            }
        }
        else
        {
            if (!sem.HasBuffer<InputBufferData<ClientInputData>>(_serverPlayer))
            {
                LogError("STEP 3 FAILED: server player has no InputBufferData<ClientInputData> buffer; route B impossible");
                SetStage(Stage.Stopped);
                return;
            }
            using (var q = sem.CreateEntityQuery(ComponentType.ReadOnly<NetworkTime>()))
            {
                if (q.CalculateEntityCount() == 1)
                {
                    var nt = q.GetSingleton<NetworkTime>();
                    if (nt.ServerTick.IsValid)
                    {
                        var buf = sem.GetBuffer<InputBufferData<ClientInputData>>(_serverPlayer);
                        // review G12: the press on route B too (this tick only; the two ticks ahead without it)
                        bool pressB = AIBody.UseBitFor(nt.ServerTick.TickIndexForValidTick);
                        AIBody.NudgeFor(nt.ServerTick.TickIndexForValidTick, ref ci, out bool nudgeAheadB);   // 검수 M1
                        for (uint i = 0; i < 3; i++)
                        {
                            var tick = nt.ServerTick;
                            tick.Add(i);
                            var ciB = ci;
                            if (nudgeAheadB && i > 0) ciB.movementDirection = Unity.Mathematics.float2.zero;
                            if (pressB && i == 0) ciB.SetButtonState(CommandInputButtonStateNames.InteractWithObject_Pressed, true);
                            buf.AddCommandData(new InputBufferData<ClientInputData>
                            {
                                Tick = tick,
                                InternalInput = MakeInput(tick, NetworkTick.Invalid, ciB),
                            });
                        }
                    }
                }
            }
        }
        if (AIAgent.Cur.Index == 0) AIMoveTrace.Frame(server, _serverPlayer, ci, traceTick, now);   // test-only (Claude only); off unless AIPlayers/movetrace.on exists

        // Route check only during the walking test (idle standing still is not a movement failure).
        if (AIBody.WalkTestActive)
        {
            float t = now - AIBody.Current.StartTime;
            var pos = sem.GetComponentData<LocalTransform>(_serverPlayer).Position;
            if (t < 0.5f) _fallbackChecked = false;
            if (!_fallbackChecked && t >= 2.8f)
            {
                _fallbackChecked = true;
                float moved = math.distance(pos.xz, AIBody.Current.StartPos.xz);
                if (moved > 0.3f)
                {
                    Log($"STEP 3 OK (walktest): player moved {moved:F2} units after {t:F1}s via route " +
                        (_serverSideInputFallback ? "B (server-side input buffer write)" : "A (thin-client input commands over Netcode)"));
                }
                else
                {
                    Log($"STEP 3 (walktest): moved only {moved:F2} units in {t:F1}s via route {(_serverSideInputFallback ? "B" : "A")} " +
                        "(the first phase walks right; a wall on the right also gives this). Route unchanged; see the later phases in the log.");
                }
            }
        }
    }
    // ---------------------------------------------------------------- cleanup

    internal static void DisposeThinWorld(string reason)
    {
        if (_thinWorld == null) return;
        // Save the AI character before anything is torn down (the server player entity still exists here).
        try
        {
            var server = Manager.ecs != null ? Manager.ecs.ServerWorld : null;
            if (server != null && server.IsCreated && _serverPlayer != Entity.Null && server.EntityManager.Exists(_serverPlayer))
            {
                AIBody.Stop(server, _serverPlayer, Time.realtimeSinceStartup, "AI leaving (" + reason + ")", hard: true);   // review F2: leaving cuts the closing tail
            }
            AICharacterStore.SaveFromServer(server, _serverPlayer, "leaving (" + reason + ")");
        }
        catch (Exception e)
        {
            LogError("AI character: save on leave failed", e);
        }
        Log($"disposing thin world ({reason})");
        PumpEnabled = false; // the driver disposes ThinIf's queues on world disposal
        var w = _thinWorld;
        _thinWorld = null;
        try
        {
            if (w.IsCreated)
            {
                if (ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(w))
                {
                    ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(w);
                }
                ClientServerBootstrap.ThinClientWorlds.Remove(w);
                w.Dispose();
            }
            Log("thin world disposed");
        }
        catch (Exception e)
        {
            LogError("disposing thin world failed", e);
        }
        ThinIf = default;
        ResetState();
        if (_stage != Stage.WaitingForHostedGame)
        {
            SetStage(Stage.Stopped);
        }
    }

    // ---------------------------------------------------------------- packet pump

    // One pass per server network update for every character's thin interface: server packets are handed to the
    // interface whose endpoint (127.0.0.1:7778+i) they are addressed to, and each thin interface's packets go to the
    // server. Runs outside any character scope (Harmony prefix), so it reads each character's state directly.
    private static readonly List<State> _pumpList = new List<State>();
    internal static void Pump()
    {
        if (_serverNetIfField == null || Manager.networking == null) return;
        _pumpList.Clear();
        foreach (var a in AIAgent.All) { var st = StateOf(a); if (st.PumpEnabled && st.ThinIf.IsCreated) _pumpList.Add(st); }
        if (_pumpList.Count == 0) return;
        try
        {
            var serverIf = (PugNetworkInterface)_serverNetIfField.GetValue(Manager.networking);
            if (!serverIf.IsCreated) return;

            serverIf.CompleteSend();
            serverIf.CompleteReceive();
            foreach (var st in _pumpList) { st.ThinIf.CompleteSend(); st.ThinIf.CompleteReceive(); }

            // server -> thin: replies addressed to a thin endpoint would otherwise go to Steam/PlayFab and be lost.
            int n = serverIf.sendQueue.Count;
            for (int i = 0; i < n; i++)
            {
                QueuedSendMessage m = serverIf.sendQueue.Dequeue();
                State to = null;
                foreach (var st in _pumpList) if (m.Dest == st.ThinIf.LocalEndpoint) { to = st; break; }
                if (to != null)
                {
                    to.ThinIf.receiveQueue.Enqueue(m);
                    to.PacketsServerToThin++;
                }
                else
                {
                    serverIf.sendQueue.Enqueue(m);
                }
            }

            // thin -> server
            foreach (var st in _pumpList)
            {
                while (st.ThinIf.sendQueue.TryDequeue(out QueuedSendMessage m))
                {
                    if (m.Dest == serverIf.LocalEndpoint)
                    {
                        serverIf.receiveQueue.Enqueue(m);
                        st.PacketsThinToServer++;
                    }
                    else
                    {
                        st.PacketsDropped++;
                    }
                }
                while (st.ThinIf.localQueue.TryDequeue(out QueuedSendMessage _))
                {
                    st.PacketsDropped++;
                }
            }
        }
        catch (Exception e)
        {
            if (!_pumpErrorLogged)
            {
                _pumpErrorLogged = true;
                LogError("packet pump failed", e);
            }
        }
    }
}
