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
    [MenuItem("Tools/Color Stack Rush/Build Browser Preview")]
    public static void BuildWebPreview()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("Install Web Build Support for Unity 6000.3.3f1.");
        Prepare();
        PlayerSettings.bundleVersion = "0.4.0";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
        PlayerSettings.WebGL.template = "PROJECT:ColorStackRush";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        // Sites static hosting does not apply _headers. Unity's bundled Brotli
        // decoder also works on hosts without custom response headers.
        PlayerSettings.WebGL.decompressionFallback = Argument("-nativeWebDecompression", "false") != "true";
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.threadsSupport = false;
        PlayerSettings.WebGL.initialMemorySize = 64;
        PlayerSettings.WebGL.maximumMemorySize = 512;
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;
        string output = Argument("-outputPath", "Builds/Web");
        Build(BuildTarget.WebGL, output);
        File.WriteAllText(Path.Combine(output, "_headers"),
            "/Build/*.wasm.br\n  Content-Type: application/wasm\n  Content-Encoding: br\n" +
            "/Build/*.js.br\n  Content-Type: application/javascript\n  Content-Encoding: br\n" +
            "/Build/*.data.br\n  Content-Type: application/octet-stream\n  Content-Encoding: br\n" +
            "/Build/*.unityweb\n  Content-Type: application/octet-stream\n" +
            "/Build/*\n  Cache-Control: public, max-age=31536000, immutable\n" +
            "/\n  Cache-Control: no-cache\n/index.html\n  Cache-Control: no-cache\n");
        long bytes = 0;
        foreach (var file in Directory.GetFiles(output, "*", SearchOption.AllDirectories))
        {
            long length = new FileInfo(file).Length;
            if (length > 25L * 1024 * 1024) throw new InvalidOperationException("Web asset exceeds 25 MiB: " + file);
            bytes += length;
        }
        if (bytes > 25L * 1024 * 1024) Debug.LogWarning("[CSR Build] Preview download exceeds the 25 MiB target; optimize imported textures/audio before delivery.");
    }
    static void Build(BuildTarget target, string output)
    {
        var buildGroup = BuildPipeline.GetBuildTargetGroup(target);
        var compileTarget = NamedBuildTarget.FromBuildTargetGroup(buildGroup);
        foreach (var symbol in PlayerSettings.GetScriptingDefineSymbols(compileTarget).Split(';'))
            if (symbol.Trim() == "CSR_JEV_TEST")
                throw new InvalidOperationException("Remove the local Jev test define before making a public player build.");
        Directory.CreateDirectory(target == BuildTarget.WebGL ? Path.GetFullPath(output) : Path.GetDirectoryName(Path.GetFullPath(output)));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = target, options = BuildOptions.None });
        Debug.Log($"[CSR Build] {target}: {report.summary.result}; errors={report.summary.totalErrors}; size={report.summary.totalSize}");
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Player build failed");
        bool directory = target == BuildTarget.WebGL;
        long outputBytes = 0;
        if (directory) foreach (var file in Directory.GetFiles(output, "*", SearchOption.AllDirectories)) outputBytes += new FileInfo(file).Length;
        else outputBytes = new FileInfo(output).Length;
        var namedTarget = target == BuildTarget.Android ? NamedBuildTarget.Android : directory ? NamedBuildTarget.WebGL : NamedBuildTarget.Standalone;
        File.WriteAllText(directory ? Path.Combine(output, "build-info.json") : Path.ChangeExtension(output, ".build.json"), JsonUtility.ToJson(new BuildFacts {
            target = target.ToString(), unity = Application.unityVersion, version = PlayerSettings.bundleVersion, product = PlayerSettings.productName,
            packageId = PlayerSettings.GetApplicationIdentifier(namedTarget),
            bytes = outputBytes, buildReportBytes = (long)report.summary.totalSize, errors = (int)report.summary.totalErrors, seconds = (float)report.summary.totalTime.TotalSeconds,
            builtAtUtc = DateTime.UtcNow.ToString("o")
        }, true));
    }
    [Serializable] class BuildFacts { public string target, unity, version, product, packageId, builtAtUtc; public long bytes, buildReportBytes; public int errors; public float seconds; }
}
#endif
