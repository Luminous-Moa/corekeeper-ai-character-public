// Headless helpers that drive the Mod SDK's own editor code from the command line:
//   Unity.exe -batchmode -quit -projectPath <sdk> -executeMethod HeadlessSdk.ImportGameFiles
//   Unity.exe -batchmode -quit -projectPath <sdk> -executeMethod HeadlessSdk.CreateMod
//   Unity.exe -batchmode -quit -projectPath <sdk> -executeMethod HeadlessSdk.BuildAndInstall
//   Unity.exe -batchmode -quit -projectPath <sdk> -executeMethod HeadlessSdk.CompileCheck   (compile only, no install)
// Everything is resolved by reflection so this assembly compiles even before the
// game assemblies are imported (when the SDK's own editor assembly cannot compile yet).
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class HeadlessSdk
{
    private const string GamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Core Keeper";
    private const string GamePathPrefKey = "PugMod/SDKWindow/GamePath";
    private const string ModName = "AIPlayerSpike";

    private static Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName, false);
            if (t != null) return t;
        }
        throw new Exception("Type not found: " + fullName);
    }

    private static void Fail(Exception e)
    {
        Debug.LogError("[HeadlessSdk] FAILED: " + e);
        EditorApplication.Exit(1);
    }

    // Equivalent of Mod SDK window > Update SDK > Update Game Files.
    public static void ImportGameFiles()
    {
        try
        {
            EditorPrefs.SetString(GamePathPrefKey, GamePath);

            var settingsType = FindType("PugMod.ImporterSettings");
            var settings = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var importer = FindType("PugMod.ImporterWindow");
            var method = importer.GetMethod("UpdateFromGamePath", BindingFlags.Public | BindingFlags.Static);
            var args = new object[] { settings, GamePath, null };
            method.Invoke(null, args);
            var added = (System.Collections.Generic.List<string>)args[2];
            Debug.Log($"[HeadlessSdk] Imported {added?.Count ?? 0} game assemblies: {string.Join(", ", added ?? new System.Collections.Generic.List<string>())}");

            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            var defines = PlayerSettings.GetScriptingDefineSymbols(target);
            if (!defines.Contains("USE_PUG_OTHER"))
            {
                PlayerSettings.SetScriptingDefineSymbols(target, defines + ";USE_PUG_OTHER");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[HeadlessSdk] ImportGameFiles done");
        }
        catch (Exception e) { Fail(e); }
    }

    // Registers the game version tag (needs the SDK editor assembly, i.e. after import compiled).
    // Equivalent of Mod SDK window > Create Mod > Create, for a mod named AIPlayerSpike.
    public static void CreateMod()
    {
        try
        {
            EditorPrefs.SetString(GamePathPrefKey, GamePath);
            try
            {
                FindType("PugMod.GameVersionTagRegistry").GetMethod("TryRegisterFromGamePath").Invoke(null, new object[] { GamePath });
            }
            catch (Exception e) { Debug.LogWarning("[HeadlessSdk] version tag registration failed: " + e.Message); }

            if (FindSettings() != null)
            {
                Debug.Log("[HeadlessSdk] Mod already exists");
                return;
            }
            var window = FindType("PugMod.ModBuilderWindow");
            var settings = window.GetMethod("CreateNewMod", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { ModName });
            if (settings == null) throw new Exception("CreateNewMod returned null");
            Debug.Log("[HeadlessSdk] CreateMod done");
        }
        catch (Exception e) { Fail(e); }
    }

    private static ScriptableObject FindSettings()
    {
        var settingsType = FindType("ModBuilderSettings");
        foreach (var guid in AssetDatabase.FindAssets("t:ModBuilderSettings"))
        {
            var so = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), settingsType) as ScriptableObject;
            if (so == null) continue;
            var meta = settingsType.GetField("metadata").GetValue(so);
            var name = (string)meta.GetType().GetField("name").GetValue(meta);
            if (name == ModName) return so;
        }
        return null;
    }

    // Equivalent of Mod SDK window > Create Mod > "Install" (local export) into
    // <game>/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike.
    public static void BuildAndInstall()
    {
        try
        {
            var settings = FindSettings();
            if (settings == null) throw new Exception("Mod settings for " + ModName + " not found");
            var settingsType = settings.GetType();

            // Script-only mod: no asset bundles needed.
            settingsType.GetField("buildBundles").SetValue(settings, false);
            settingsType.GetField("buildLinux").SetValue(settings, false);
            var metaField = settingsType.GetField("metadata");
            var meta = metaField.GetValue(settings);
            var mt = meta.GetType();
            mt.GetField("skipSafetyChecks").SetValue(meta, true);
            mt.GetField("accessesExtraAssemblies").SetValue(meta, true);
            mt.GetField("displayName").SetValue(meta, "AI Player Spike");
            metaField.SetValue(settings, meta);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            var modsPath = Path.Combine(GamePath, "CoreKeeper_Data", "StreamingAssets", "Mods");
            Directory.CreateDirectory(modsPath);

            bool ok = false;
            var builder = FindType("PugMod.ModBuilder");
            var build = builder.GetMethod("BuildMod", BindingFlags.Public | BindingFlags.Static);
            build.Invoke(null, new object[] { settings, modsPath, (Action<bool>)(s => ok = s), true });
            if (!ok) throw new Exception("ModBuilder.BuildMod reported failure");

            var manifest = Path.Combine(modsPath, ModName, "ModManifest.json");
            Debug.Log("[HeadlessSdk] BuildAndInstall done: " + manifest + "\n" + File.ReadAllText(manifest));
        }
        catch (Exception e) { Fail(e); }
    }

    // Compile only: no build, no install, the game folder is not touched. Runs after the editor opened the project
    // (import + script compile). Exit 0 when every script assembly compiled, 1 otherwise; the log line says which.
    // If the scripts do not compile, Unity may also stop before calling this (non-zero exit): read the log's "error CS".
    public static void CompileCheck()
    {
        try
        {
            bool failed = EditorUtility.scriptCompilationFailed;
            Debug.Log("[HeadlessSdk] CompileCheck: " + (failed ? "FAILED (script compilation failed; see error CS above)" : "ok"));
            EditorApplication.Exit(failed ? 1 : 0);
        }
        catch (Exception e) { Fail(e); }
    }
}
