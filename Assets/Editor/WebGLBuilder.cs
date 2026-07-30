using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Headless WebGL build entry point for itch.io distribution.
/// Invoke via: Unity.exe -batchmode -nographics -quit -projectPath <path>
///             -executeMethod WebGLBuilder.BuildForItchIo
/// </summary>
public static class WebGLBuilder
{
    private const string OutputDir = "Builds/WebGL";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    public static void BuildForItchIo()
    {
        // Ensure the game scene is registered in the build settings.
        var scenes = EditorBuildSettings.scenes;
        if (scenes == null || scenes.Length == 0 || !scenes.Any(s => s.path == ScenePath))
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            scenes = EditorBuildSettings.scenes;
            Debug.Log($"[WebGLBuilder] Registered {ScenePath} in EditorBuildSettings.");
        }

        var enabledScenePaths = scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (enabledScenePaths.Length == 0)
        {
            Debug.LogError("[WebGLBuilder] No enabled scenes found in EditorBuildSettings. Aborting build.");
            EditorApplication.Exit(1);
            return;
        }

        // The game builds all materials at runtime via Shader.Find("Standard") /
        // Shader.Find("Sprites/Default") (MaterialCache, ParticleFactory) — there are
        // no scene/Resources references to these shaders anywhere, so Unity's shader
        // stripper can drop them entirely from a WebGL build even though they're listed
        // in Always Included Shaders, causing a null-shader crash at runtime. Force them
        // into that list programmatically right before building so the stripper keeps them.
        EnsureShaderAlwaysIncluded("Standard");
        EnsureShaderAlwaysIncluded("Sprites/Default");
        EnsureShaderAlwaysIncluded("Legacy Shaders/Diffuse");

        // Engine code stripping ("Strip Engine Code") was removing native modules
        // (e.g. Physics/CapsuleCollider) that this project only ever touches via
        // generic AddComponent<T>() calls at runtime, which its usage analysis missed —
        // symptom was "Can't add component because class 'CapsuleCollider' doesn't
        // exist!" at runtime. Disable it for this build so nothing used purely via
        // runtime-constructed GameObjects gets silently dropped.
        PlayerSettings.stripEngineCode = false;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Disabled);

        // Portrait hypercasual mobile game -> portrait WebGL canvas.
        PlayerSettings.defaultWebScreenWidth = 480;
        PlayerSettings.defaultWebScreenHeight = 800;

        // itch.io compatibility: gzip compression with a JS decompression fallback
        // is the safest option since itch.io's server doesn't always serve the
        // correct Content-Encoding headers for the HTML5 zip upload flow.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        // Leave Color Space as currently configured in ProjectSettings (no change).

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = enabledScenePaths,
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Debug.Log($"[WebGLBuilder] Building WebGL player -> {OutputDir} with scenes: {string.Join(", ", enabledScenePaths)}");

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        var summary = report.summary;

        Debug.Log($"[WebGLBuilder] Build result: {summary.result}, total size: {summary.totalSize} bytes, " +
                   $"errors: {summary.totalErrors}, warnings: {summary.totalWarnings}, time: {summary.totalTime}");

        if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Adds a shader to ProjectSettings' "Always Included Shaders" list (idempotent)
    /// so the build's shader stripper cannot drop it, even when nothing in a scene or
    /// Resources folder references it directly.</summary>
    private static void EnsureShaderAlwaysIncluded(string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[WebGLBuilder] Shader '{shaderName}' not found in the Editor; cannot force-include it.");
            return;
        }

        var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        var so = new SerializedObject(graphicsSettingsObj);
        var prop = so.FindProperty("m_AlwaysIncludedShaders");

        for (int i = 0; i < prop.arraySize; i++)
        {
            if (prop.GetArrayElementAtIndex(i).objectReferenceValue == shader)
            {
                return; // already present
            }
        }

        prop.arraySize++;
        prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = shader;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log($"[WebGLBuilder] Force-included shader in Always Included Shaders: {shaderName}");
    }
}
