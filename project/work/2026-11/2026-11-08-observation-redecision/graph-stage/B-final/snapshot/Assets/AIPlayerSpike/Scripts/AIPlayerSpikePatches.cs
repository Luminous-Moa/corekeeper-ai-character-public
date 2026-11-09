// AIPlayerSpikePatches — extracted from the existing implementation; behavior and state ownership are unchanged.
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

// Gives the thin client world its own PugNetworkInterface (the game's constructor would re-initialise
// the host client's shared interface and reuse 127.0.0.1:7777).
public class ThinClientDriverConstructor : INetworkStreamDriverConstructor
{
    public void CreateClientDriver(World world, ref NetworkDriverStore driver, NetDebug netDebug)
    {
        AIPlayerSpikeMod.Log($"STEP 1: creating dedicated client driver for '{world.Name}' on {AIPlayerSpikeMod.ThinEndpoint}");
        var settings = new NetworkSettings(Allocator.Temp);
        settings.WithReliableStageParameters().WithFragmentationStageParameters(1048576);
        settings.WithNetworkConfigParameters(3000, 60, 30000, 500, 2000, 100);

        // the interface of the character being created (AIAgent.Cur), by ref into its state
        var st = AIPlayerSpikeMod.St;
        st.ThinIf = default;
        st.ThinIf.LocalEndpoint = AIPlayerSpikeMod.ThinEndpoint;
        NetworkDriver nd = NetworkDriver.Create(ref st.ThinIf, settings);

        var e = world.EntityManager.CreateEntity(typeof(WorldNetworkDriver));
        world.EntityManager.SetComponentData(e, new WorldNetworkDriver { driver = nd });

        NetworkDriverStore.NetworkDriverInstance inst = default;
        inst.driver = nd;
        inst.unreliablePipeline = nd.CreatePipeline(typeof(NullPipelineStage));
        inst.reliablePipeline = nd.CreatePipeline(typeof(ReliableSequencedPipelineStage));
        inst.unreliableFragmentedPipeline = nd.CreatePipeline(typeof(FragmentationPipelineStage));
        driver.RegisterDriver(TransportType.IPC, in inst);

        st.PumpEnabled = st.ThinIf.IsCreated;
        AIPlayerSpikeMod.Log($"STEP 1: thin driver created (interface initialised={st.ThinIf.IsCreated})");
    }

    public void CreateServerDriver(World world, ref NetworkDriverStore driver, NetDebug netDebug)
    {
        throw new InvalidOperationException("AIPlayerSpike driver constructor is only for the thin client world");
    }
}

public static class AIPlayerSpikePatches
{
    public static void ServerNetworkUpdatePrefix()
    {
        AIPlayerSpikeMod.Pump();
    }

    public static void UnloadWorldsPrefix()
    {
        AIPlayerSpikeMod.DisposeAll("ECSManager unloading worlds");
        AIChargeWatch.Reset("worlds unloading");   // review: its static state does not outlive the world
    }

    // Host's own outgoing chat: swallow "/ai ..." commands (return false = not sent to other players).
    public static bool ChatSendPrefix(string message, bool isStreamIntegrationMessage)
    {
        if (isStreamIntegrationMessage || string.IsNullOrEmpty(message)) return true;
        try
        {
            return !AIPlayerSpikeMod.TryHandleChatCommand(message);
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError("chat command handling failed", e);
            return true;
        }
    }

    // playerGuid of the StartGameRPC the server is currently processing (set around the spawn lambda; main thread).
    internal static bool InSpawnLambda;
    internal static StartGameRPC CurrentRequest;

    public static void SpawnLambdaPrefix(object[] __args)
    {
        InSpawnLambda = false;
        if (__args != null && __args.Length > 0 && __args[0] is StartGameRPC req)
        {
            CurrentRequest = req;
            InSpawnLambda = true;
        }
    }

    public static void SpawnLambdaPostfix()
    {
        InSpawnLambda = false;
    }

    // Server-side, inside StartGameRPCSystem: the thin client sent the host's character bytes; replace the
    // deserialised result with the AI's own character data — only when the request's playerGuid is the AI's.
    // Any other player's data (a human joining at the same moment, the host) is never touched.
    public static void CharacterDataPostfix(ref CharacterData __result)
    {
        if (__result == null || !InSpawnLambda) return;
        // which AI character (if any) this playerGuid belongs to
        AIAgent who = null;
        foreach (var a in AIAgent.All)
        {
            var st = AIPlayerSpikeMod.StateOf(a);
            if (st.AiGuidSet && CurrentRequest.playerGuid.Equals(st.AiGuidHolder.playerGuid)) { who = a; break; }
        }
        if (who == null)
        {
            AIPlayerSpikeMod.Log($"AI character: server deserialised character data for playerGuid {CurrentRequest.playerGuid} (not an AI's); left untouched");
            return;
        }
        using (AIAgent.Enter(who))
            SwapIn(ref __result);
    }

    private static void SwapIn(ref CharacterData __result)
    {
        try
        {
            string hostGuid = __result.characterGuid;
            CharacterData ai = AICharacterStore.CreateServerCopy();
            if (ai == null)
            {
                AIPlayerSpikeMod.LogError("AI character: no prepared AI character data; leaving the incoming data untouched");
                return;
            }
            __result = ai;
            AIPlayerSpikeMod.SwapDone = true;
            AIPlayerSpikeMod.RenamePatchHits++;
            AIPlayerSpikeMod.Log($"AI character: server received character data for the AI's playerGuid {CurrentRequest.playerGuid} (incoming characterGuid {hostGuid}); replaced it with the AI's own " +
                $"'{ai.CharacterCustomization.name}' ({AICharacterStore.Summary(ai)})");
        }
        catch (Exception e)
        {
            AIPlayerSpikeMod.LogError("AI character: swapping character data failed", e);
        }
    }
}
