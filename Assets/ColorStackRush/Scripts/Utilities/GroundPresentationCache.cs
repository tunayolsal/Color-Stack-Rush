using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>Prepared on the inactive template; Unity remaps child references when the pool clones it.</summary>
    public sealed class GroundPresentationCache : MonoBehaviour
    {
        [SerializeField] MeshRenderer surface;
        [SerializeField] MeshRenderer[] shoulders;
        [SerializeField] GameObject[] decorations;
        [SerializeField] byte[] decorationThemes;
        [SerializeField] Material[] surfaceMaterials;
        [SerializeField] Material[] shoulderMaterials;
        [SerializeField] bool prepared;

        public void Warmup()
        {
            if (prepared) return;
            var shoulderList = new List<MeshRenderer>(2);
            var decorationList = new List<GameObject>(8);
            var themeList = new List<byte>(8);
            Transform root = transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                string childName = child.name;
                if (childName == "Surface") surface = child.GetComponent<MeshRenderer>();
                else if (childName.StartsWith("Shoulder", System.StringComparison.Ordinal)) shoulderList.Add(child.GetComponent<MeshRenderer>());
                else if (childName.StartsWith("Decor_", System.StringComparison.Ordinal))
                {
                    byte themes = 0;
                    if (childName.Contains("tree") || childName.Contains("plant") || childName.Contains("patch")) themes |= 1;
                    if (childName.Contains("rocks") || childName.Contains("stones")) themes |= 2;
                    if (childName.Contains("tree-high") || childName.Contains("rocks-low") || childName.Contains("stones")) themes |= 4;
                    decorationList.Add(child.gameObject);
                    themeList.Add(themes);
                }
            }
            shoulders = shoulderList.ToArray();
            decorations = decorationList.ToArray();
            decorationThemes = themeList.ToArray();
            surfaceMaterials = new Material[3];
            shoulderMaterials = new Material[3];
            for (int theme = 0; theme < 3; theme++)
            {
                surfaceMaterials[theme] = MaterialCache.Get(ThemePresentation.Ground(theme));
                shoulderMaterials[theme] = MaterialCache.Get(ThemePresentation.Shoulder(theme));
            }
            prepared = true;
        }

        public void ApplyTheme(int theme)
        {
            if (!prepared) Warmup();
            theme = Mathf.Clamp(theme, 0, 2);
            if (surface != null) surface.sharedMaterial = surfaceMaterials[theme];
            for (int i = 0; i < shoulders.Length; i++)
                if (shoulders[i] != null) shoulders[i].sharedMaterial = shoulderMaterials[theme];
            int themeBit = 1 << theme;
            for (int i = 0; i < decorations.Length; i++)
            {
                bool show = (decorationThemes[i] & themeBit) != 0;
                if (decorations[i].activeSelf != show) decorations[i].SetActive(show);
            }
        }
    }
}
