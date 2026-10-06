using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Creates and caches materials so identically colored objects share one
    /// material instance (better batching, no per-frame allocations).
    /// Built-in Render Pipeline (Standard shader).
    /// </summary>
    public static class MaterialCache
    {
        static readonly Dictionary<Color, Material> lit = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> emissive = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> transparent = new Dictionary<Color, Material>();

        static Shader standardShader;
        static Shader StandardShader
        {
            get
            {
                if (standardShader == null) standardShader = Shader.Find("Standard");
                return standardShader;
            }
        }

        /// <summary>Soft matte pastel material.</summary>
        public static Material Get(Color color)
        {
            if (lit.TryGetValue(color, out var m)) return m;
            m = new Material(StandardShader) { color = color, enableInstancing = true };
            m.SetFloat("_Glossiness", 0.48f); // low smoothness = soft look
            m.SetFloat("_Metallic", 0f);
            lit[color] = m;
            return m;
        }

        /// <summary>Material with a gentle emissive glow (coins, power-ups, color ring).</summary>
        public static Material GetEmissive(Color color, float intensity = 0.55f)
        {
            if (emissive.TryGetValue(color, out var m)) return m;
            m = new Material(StandardShader) { color = color, enableInstancing = true };
            m.SetFloat("_Glossiness", 0.4f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * Mathf.Min(intensity, .12f));
            emissive[color] = m;
            return m;
        }

        /// <summary>See-through material (shield bubble). Uses the Standard shader in
        /// alpha-blended transparent mode — "Legacy Shaders/Transparent/Diffuse" is not
        /// guaranteed to survive shader stripping on platforms like WebGL (it isn't in
        /// Always Included Shaders), which caused a null-shader crash there.</summary>
        public static Material GetTransparent(Color color, float alpha = 0.3f)
        {
            var key = new Color(color.r, color.g, color.b, alpha);
            if (transparent.TryGetValue(key, out var m)) return m;
            m = new Material(StandardShader) { color = key };
            m.SetFloat("_Mode", 3f); // Transparent
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            transparent[key] = m;
            return m;
        }
    }
}
