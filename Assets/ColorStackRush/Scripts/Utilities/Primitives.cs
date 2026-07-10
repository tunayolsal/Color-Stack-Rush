using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Helper for building the whole game out of Unity primitive meshes.
    /// Strips the auto-added collider by default (colliders are added
    /// explicitly where gameplay needs them).
    /// </summary>
    public static class Primitives
    {
        /// <summary>Creates a primitive, parents it, positions it locally and applies a material.</summary>
        public static GameObject Create(
            PrimitiveType type,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            string name = null,
            bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!string.IsNullOrEmpty(name)) go.name = name;

            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    col.enabled = false;
                    Object.Destroy(col);
                }
            }

            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = localScale;

            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

            return go;
        }
    }
}
