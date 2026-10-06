using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorStackRush.Testing
{
    // Separate from ReleaseBuilder: opt-in local instrumentation must never be
    // allowed through the public player's guard. Run without -quit so the
    // pending build can survive the define's Editor domain reload.
    [InitializeOnLoad]
    public static class JevWebPreviewBuilder
    {
        const string Prefix = "CSR.JevWebBuild.";
        const string Symbol = "CSR_JEV_TEST";
        static JevWebPreviewBuilder() => EditorApplication.update += Poll;

        public static void Build()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use a fresh batch-mode Editor for the local Jev WebGL build.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install Web Build Support for Unity 6000.3.3f1.");
            string output = Argument("-outputPath");
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output))
                throw new InvalidOperationException("Pass an absolute local -outputPath for the Jev test build.");
            output = Path.GetFullPath(output);
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (output.Equals(assets, StringComparison.OrdinalIgnoreCase)
                || output.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The local test player must be outside the project's Assets directory.");

            string before = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL);
            SessionState.SetString(Prefix + "defines", before);
            SessionState.SetString(Prefix + "output", output);
            SessionState.SetString(Prefix + "started", DateTime.UtcNow.ToString("O"));
            SessionState.SetBool(Prefix + "pending", true);
            SessionState.SetBool(Prefix + "building", false);
            SessionState.SetBool(Prefix + "finalizing", false);
            if (Array.IndexOf(before.Split(';'), Symbol) < 0)
                PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL,
                    string.IsNullOrEmpty(before) ? Symbol : before + ";" + Symbol);
            AssetDatabase.SaveAssets();
        }

        static void Poll()
        {
            if (SessionState.GetBool(Prefix + "finalizing", false))
            {
                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    SessionState.SetBool(Prefix + "finalizing", false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Prefix + "exit", 1));
                }
                return;
            }
            if (!SessionState.GetBool(Prefix + "pending", false) || SessionState.GetBool(Prefix + "building", false)) return;
            if (DateTime.TryParse(SessionState.GetString(Prefix + "started", ""), out var started)
                && DateTime.UtcNow - started.ToUniversalTime() > TimeSpan.FromMinutes(20))
            {
                Debug.LogError("[CSR Jev Web Build] Editor compilation exceeded the local build time limit.");
                Finish(1); return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            SessionState.SetBool(Prefix + "building", true);
            BuildNow();
        }

        static void BuildNow()
        {
            string scenePath = "Assets/Scenes/__LocalJevWebPreview_" + Guid.NewGuid().ToString("N") + ".unity";
            var graphics = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.GraphicsSettings>("ProjectSettings/GraphicsSettings.asset"));
            var included = graphics.FindProperty("m_AlwaysIncludedShaders");
            var beforeShaders = new UnityEngine.Object[included.arraySize];
            for (int i = 0; i < beforeShaders.Length; i++) beforeShaders[i] = included.GetArrayElementAtIndex(i).objectReferenceValue;
            try
            {
                // Same bootstrap and render requirements as the release scene,
                // serialized temporarily so its source scene is not replaced.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Color Stack Rush").AddComponent<GameBootstrapper>();
                Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
                if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath))
                    throw new InvalidOperationException("Could not serialize the local Jev test scene.");
                foreach (string name in new[] { "Standard", "Sprites/Default", "Legacy Shaders/Diffuse" })
                {
                    var shader = Shader.Find(name);
                    if (shader == null) throw new InvalidOperationException("Missing procedural shader: " + name);
                    bool found = false;
                    for (int i = 0; i < included.arraySize; i++)
                        if (included.GetArrayElementAtIndex(i).objectReferenceValue == shader) found = true;
                    if (!found)
                    {
                        included.arraySize++;
                        included.GetArrayElementAtIndex(included.arraySize - 1).objectReferenceValue = shader;
                    }
                }
                graphics.ApplyModifiedProperties();
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                PlayerSettings.allowedAutorotateToLandscapeLeft = false;
                PlayerSettings.allowedAutorotateToLandscapeRight = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.stripEngineCode = false;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
                PlayerSettings.WebGL.template = "PROJECT:ColorStackRush";
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.decompressionFallback = true;
                PlayerSettings.WebGL.nameFilesAsHashes = true;
                PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
                PlayerSettings.WebGL.dataCaching = true;
                PlayerSettings.WebGL.threadsSupport = false;
                PlayerSettings.WebGL.initialMemorySize = 64;
                PlayerSettings.WebGL.maximumMemorySize = 512;
                PlayerSettings.defaultScreenWidth = 540;
                PlayerSettings.defaultScreenHeight = 960;
                AssetDatabase.SaveAssets();
                string output = SessionState.GetString(Prefix + "output", "");
                Directory.CreateDirectory(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath }, locationPathName = output, target = BuildTarget.WebGL,
                    options = BuildOptions.Development,
                    extraScriptingDefines = new[] { Symbol }
                });
                Debug.Log($"[CSR Jev Web Build] LOCAL DEVELOPMENT ONLY: {report.summary.result}; errors={report.summary.totalErrors}; size={report.summary.totalSize}; output={output}");
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Local Jev WebGL build failed.");
                File.WriteAllText(Path.Combine(output, "LOCAL_JEV_TEST_BUILD.txt"),
                    "Local Jev AI instrumentation; serve on loopback only. Do not publish this Development player.\n");
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception); Finish(1);
            }
            finally
            {
                // Leave source scenes/shader references intact. The public build
                // still independently rejects an opt-in test define.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
                graphics.Update(); included = graphics.FindProperty("m_AlwaysIncludedShaders");
                included.arraySize = beforeShaders.Length;
                for (int i = 0; i < beforeShaders.Length; i++) included.GetArrayElementAtIndex(i).objectReferenceValue = beforeShaders[i];
                graphics.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }

        static void Finish(int exit)
        {
            SessionState.SetBool(Prefix + "pending", false);
            SessionState.SetBool(Prefix + "building", false);
            SessionState.SetInt(Prefix + "exit", exit);
            SessionState.SetBool(Prefix + "finalizing", true);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, SessionState.GetString(Prefix + "defines", ""));
            AssetDatabase.SaveAssets();
        }
        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
