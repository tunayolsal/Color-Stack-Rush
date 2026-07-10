using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Creates simple 3D text objects (TextMesh) for world-space labels such
    /// as the multiplier numbers on the finish stairs and floating score text.
    /// Uses the engine's built-in runtime font, so no assets are needed.
    /// </summary>
    public static class WorldText
    {
        static Font cachedFont;

        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cachedFont;
            }
        }

        /// <summary>Creates a world-space text object and returns its TextMesh.</summary>
        public static TextMesh Create(Transform parent, string text, Vector3 localPosition, Color color, float size = 1f)
        {
            var go = new GameObject("WorldText");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var tm = go.AddComponent<TextMesh>();
            tm.font = DefaultFont;
            tm.text = text;
            tm.color = color;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;                    // big font size + small characterSize = crisp text
            tm.characterSize = 0.1f * size;
            tm.fontStyle = FontStyle.Bold;

            // TextMesh needs the font's material on its renderer to display.
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = DefaultFont.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return tm;
        }
    }
}
