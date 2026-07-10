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
            m = new Material(StandardShader) { color = color };
            m.SetFloat("_Glossiness", 0.25f); // low smoothness = soft look
            m.SetFloat("_Metallic", 0f);
            lit[color] = m;
            return m;
        }

        /// <summary>Material with a gentle emissive glow (coins, power-ups, color ring).</summary>
        public static Material GetEmissive(Color color, float intensity = 0.55f)
        {
            if (emissive.TryGetValue(color, out var m)) return m;
            m = new Material(StandardShader) { color = color };
            m.SetFloat("_Glossiness", 0.4f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            emissive[color] = m;
            return m;
        }

        /// <summary>See-through material (shield bubble). Uses a legacy transparent shader for simplicity.</summary>
        public static Material GetTransparent(Color color, float alpha = 0.3f)
        {
            var key = new Color(color.r, color.g, color.b, alpha);
            if (transparent.TryGetValue(key, out var m)) return m;
            m = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"))
            {
                color = key
            };
            transparent[key] = m;
            return m;
        }
    }
}
