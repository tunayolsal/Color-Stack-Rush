using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColorStackRush.Testing
{
    // This entrypoint can enable the opt-in symbol before the bridge is compiled.
    [InitializeOnLoad]
    public static class JevSuiteEditorRunner
    {
        const string Prefix = "CSR.JevSuite.";
        const string Symbol = "CSR_JEV_TEST";
        static JevSuiteEditorRunner()
        {
            EditorApplication.playModeStateChanged += PlayState;
            EditorApplication.update += Poll;
        }
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run the Jev suite from a fresh batch-mode Editor");
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string before = PlayerSettings.GetScriptingDefineSymbols(target);
            SessionState.SetString(Prefix + "defines", before);
            SessionState.SetInt(Prefix + "group", (int)EditorUserBuildSettings.selectedBuildTargetGroup);
            SessionState.SetString(Prefix + "save", Path.Combine(Path.GetTempPath(), "csr-jev-" + Guid.NewGuid().ToString("N")));
            SessionState.SetBool(Prefix + "pending", true);
            SessionState.SetBool(Prefix + "finalizing", false);
            SessionState.SetInt(Prefix + "exit", 2);
            SessionState.SetString(Prefix + "started", DateTime.UtcNow.ToString("O"));
            if (Array.IndexOf(before.Split(';'), Symbol) < 0)
                PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(before) ? Symbol : before + ";" + Symbol);
            else EditorApplication.delayCall += StartPlay;
        }
        // A separate compile/test opt-in; it never launches Play Mode or paid calls.
        public static void EnableTestDefineForChecks()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string before = PlayerSettings.GetScriptingDefineSymbols(target);
            if (Array.IndexOf(before.Split(';'), Symbol) < 0)
                PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(before) ? Symbol : before + ";" + Symbol);
            AssetDatabase.SaveAssets();
        }
        static void Poll()
        {
            if (SessionState.GetBool(Prefix + "finalizing", false))
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                { SessionState.SetBool(Prefix + "finalizing", false); EditorApplication.Exit(SessionState.GetInt(Prefix + "exit", 2)); }
                return;
            }
            if (!SessionState.GetBool(Prefix + "pending", false)) return;
            if (DateTime.TryParse(SessionState.GetString(Prefix + "started", ""), out var start) && DateTime.UtcNow - start.ToUniversalTime() > TimeSpan.FromMinutes(45))
            { Debug.LogError("Jev suite exceeded the harness time limit"); Finish(2); return; }
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var bridge = Type.GetType("ColorStackRush.Testing.JevTestBridge, ColorStackRush.Runtime");
                if (bridge != null && !SessionState.GetBool(Prefix + "entered", false)) StartPlay();
            }
        }
        static void StartPlay()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Prefix + "entered", false)) return;
            SessionState.SetBool(Prefix + "entered", true);
            // A temporary unsaved scene; the repository's scenes remain untouched.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
        static void PlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "pending", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                try
                {
                    SaveManager.SetStorageDirectoryForTests(SessionState.GetString(Prefix + "save", ""));
                    new GameObject("Jev Test Bootstrap").AddComponent<GameBootstrapper>();
                    var bridge = Type.GetType("ColorStackRush.Testing.JevTestBridge, ColorStackRush.Runtime");
                    if (bridge == null) throw new InvalidOperationException("CSR_JEV_TEST bridge did not compile");
                    bridge.GetMethod("BeginSuite", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { (Action<int>)Finish });
                }
                catch (Exception exception) { Debug.LogException(exception); Finish(2); }
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SaveManager.SetStorageDirectoryForTests(null);
                SaveManager.Load();
                SessionState.SetBool(Prefix + "finalizing", true);
                RestoreDefines();
                SessionState.SetBool(Prefix + "pending", false);
                SessionState.SetBool(Prefix + "entered", false);
            }
        }
        static void Finish(int exit)
        {
            SessionState.SetInt(Prefix + "exit", exit);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else
            {
                RestoreDefines();
                SessionState.SetBool(Prefix + "pending", false);
                SessionState.SetBool(Prefix + "entered", false);
                EditorApplication.Exit(exit);
            }
        }
        static void RestoreDefines()
        {
            var target = NamedBuildTarget.FromBuildTargetGroup((BuildTargetGroup)SessionState.GetInt(Prefix + "group", (int)BuildTargetGroup.Standalone));
            PlayerSettings.SetScriptingDefineSymbols(target, SessionState.GetString(Prefix + "defines", ""));
        }
        // Root calls this before the release build even if a test was interrupted.
        public static void StripTestDefine()
        {
            foreach (var target in new[] { NamedBuildTarget.Standalone, NamedBuildTarget.WebGL, NamedBuildTarget.Android })
            {
                string before = PlayerSettings.GetScriptingDefineSymbols(target);
                string after = string.Join(";", Array.FindAll(before.Split(';'), value => value != Symbol && !string.IsNullOrEmpty(value)));
                if (before != after) PlayerSettings.SetScriptingDefineSymbols(target, after);
            }
            SessionState.SetBool(Prefix + "pending", false);
            SessionState.SetBool(Prefix + "entered", false);
            SessionState.SetBool(Prefix + "finalizing", false);
        }
    }
}
