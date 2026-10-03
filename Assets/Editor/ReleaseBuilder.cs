#if UNITY_EDITOR
using System;
using System.IO;
using ColorStackRush;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Batch entry points also appear under Tools for local development builds.
public static class ReleaseBuilder
{
    const string ScenePath = "Assets/Scenes/ColorStackRushRelease.unity";
    static string Argument(string key, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
        return fallback;
    }
    static void Prepare()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) throw new OperationCanceledException();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Color Stack Rush").AddComponent<GameBootstrapper>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        foreach (string name in new[] { "Standard", "Sprites/Default", "Legacy Shaders/Diffuse" })
        {
            var shader = Shader.Find(name);
            if (shader == null) throw new InvalidOperationException("Missing procedural shader: " + name);
            var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.GraphicsSettings>("ProjectSettings/GraphicsSettings.asset"));
            var array = settings.FindProperty("m_AlwaysIncludedShaders");
            bool found = false;
            for (int i = 0; i < array.arraySize; i++) if (array.GetArrayElementAtIndex(i).objectReferenceValue == shader) found = true;
            if (!found) { array.arraySize++; array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = shader; settings.ApplyModifiedProperties(); }
        }
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        // Preserve application identity so existing installations retain their persistentDataPath.
        PlayerSettings.stripEngineCode = false;
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/Color Stack Rush/Build Android APK")]
    public static void BuildAndroid()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android)) throw new InvalidOperationException("Install Android Build Support (SDK/NDK + OpenJDK) for Unity 6000.3.3f1.");
        Prepare();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
        PlayerSettings.bundleVersion = "0.2.0";
        PlayerSettings.Android.bundleVersionCode = Mathf.Max(2, PlayerSettings.Android.bundleVersionCode);
        EditorUserBuildSettings.buildAppBundle = false;
        string editorProduct = PlayerSettings.productName;
        // Freeze the old auto-generated Android ID before changing the visible app label.
        // Android saves use the package ID; keep the Editor product so its save path also stays stable.
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
        PlayerSettings.productName = "Color Stack Rush";
        try { Build(BuildTarget.Android, Argument("-outputPath", "Builds/Android/ColorStackRush.apk")); }
        finally { PlayerSettings.productName = editorProduct; AssetDatabase.SaveAssets(); }
    }
    [MenuItem("Tools/Color Stack Rush/Build Windows Preview")]
    public static void BuildWindows()
    {
        Prepare();
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        Build(BuildTarget.StandaloneWindows64, Argument("-outputPath", "Builds/Windows/ColorStackRush.exe"));
    }
    static void Build(BuildTarget target, string output)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = target, options = BuildOptions.None });
        Debug.Log($"[CSR Build] {target}: {report.summary.result}; errors={report.summary.totalErrors}; size={report.summary.totalSize}");
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Player build failed");
        File.WriteAllText(Path.ChangeExtension(output, ".build.json"), JsonUtility.ToJson(new BuildFacts {
            target = target.ToString(), unity = Application.unityVersion, version = PlayerSettings.bundleVersion, product = PlayerSettings.productName,
            packageId = PlayerSettings.GetApplicationIdentifier(target == BuildTarget.Android ? NamedBuildTarget.Android : NamedBuildTarget.Standalone),
            bytes = new FileInfo(output).Length, buildReportBytes = (long)report.summary.totalSize, errors = (int)report.summary.totalErrors, seconds = (float)report.summary.totalTime.TotalSeconds,
            builtAtUtc = DateTime.UtcNow.ToString("o")
        }, true));
    }
    [Serializable] class BuildFacts { public string target, unity, version, product, packageId, builtAtUtc; public long bytes, buildReportBytes; public int errors; public float seconds; }
}
#endif
