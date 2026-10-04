using UnityEngine;
using UnityEngine.Rendering;

namespace ColorStackRush
{
    /// <summary>A small CC0 selection, loaded once and instantiated only during pool warmup.</summary>
    public static class PrefabArt
    {
        static GameObject[] nature;
        static Material natureMaterial;
        public static void Warmup()
        {
            if (nature != null) return;
            nature = Resources.LoadAll<GameObject>("Art/Nature");
            System.Array.Sort(nature, (a, b) => string.CompareOrdinal(a.name, b.name));
            natureMaterial = new Material(Shader.Find("Standard")) { name = "KenneyToyNature", mainTexture = Resources.Load<Texture2D>("Art/Nature/colormap"), color = Color.white, enableInstancing = true };
            natureMaterial.SetFloat("_Glossiness", .1f);
        }

        public static void DecorateTile(Transform parent, float length)
        {
            Warmup();
            if (nature.Length == 0) return;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    int index = (i + (side == 1 ? 4 : 0)) % nature.Length;
                    var go = Object.Instantiate(nature[index], parent, false);
                    go.name = "Decor_" + nature[index].name;
                    foreach (var collider in go.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
                    var renderers = go.GetComponentsInChildren<MeshRenderer>();
                    Bounds bounds = new Bounds(); bool first = true;
                    foreach (var renderer in renderers) { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
                    float height = Mathf.Max(bounds.size.y, .01f);
                    float desiredHeight = nature[index].name.Contains("tree") ? 2.6f : nature[index].name.Contains("patch") ? .08f : .9f;
                    float scale = desiredHeight / height;
                    go.transform.localScale *= scale;
                    go.transform.localPosition = new Vector3(side * (6.6f + i % 2 * 1.4f), -bounds.min.y * scale - .2f, (i - 1.5f) * length * .22f);
                    go.transform.localRotation = Quaternion.Euler(0, i * 71 + side * 20, 0);
                    foreach (var renderer in renderers)
                    {
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        var materials = renderer.sharedMaterials;
                        for (int m = 0; m < materials.Length; m++) materials[m] = natureMaterial;
                        renderer.sharedMaterials = materials;
                    }
                }
            }
        }
    }
}
