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
        // GameObject.CreatePrimitive(type) auto-adds a native collider (e.g. Cylinder
        // and Capsule both get a CapsuleCollider) with no direct AddComponent<...>() call
        // anywhere in our own code — every call site here immediately discards it anyway
        // (no call site passes keepCollider: true). On WebGL that auto-add can fail with
        // "Can't add component because class 'CapsuleCollider' doesn't exist!" since the
        // build's physics-shape stripping only looks at explicit user code usage. Building
        // primitives from Unity's builtin meshes directly sidesteps CreatePrimitive (and
        // its auto-collider) entirely — colliders are added explicitly elsewhere only
        // where gameplay actually needs them.
        static Mesh GetBuiltinMesh(PrimitiveType type)
        {
            string resourceName = type switch
            {
                PrimitiveType.Cube => "Cube.fbx",
                PrimitiveType.Sphere => "Sphere.fbx",
                PrimitiveType.Cylinder => "Cylinder.fbx",
                PrimitiveType.Capsule => "Capsule.fbx",
                PrimitiveType.Plane => "Plane.fbx",
                PrimitiveType.Quad => "Quad.fbx",
                _ => "Cube.fbx"
            };
            return Resources.GetBuiltinResource<Mesh>(resourceName);
        }

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
            var go = new GameObject(string.IsNullOrEmpty(name) ? type.ToString() : name);
            var mesh = GetBuiltinMesh(type);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();

            if (keepCollider)
            {
                // Callers that actually want physics here should add a collider
                // explicitly (BoxCollider/SphereCollider) — no call site currently does,
                // this branch is kept only for API compatibility.
            }

            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            // The resource FBX and CreatePrimitive meshes may use different units.
            // Normalize from bounds without reading vertices (safe for unreadable Web meshes).
            Vector3 expected = type == PrimitiveType.Cylinder || type == PrimitiveType.Capsule ? new Vector3(1, 2, 1)
                : type == PrimitiveType.Plane ? new Vector3(10, 0, 10)
                : type == PrimitiveType.Quad ? new Vector3(1, 1, 0) : Vector3.one;
            Vector3 actual = mesh.bounds.size;
            t.localScale = Vector3.Scale(localScale, new Vector3(
                actual.x > .0001f ? expected.x / actual.x : 1,
                actual.y > .0001f ? expected.y / actual.y : 1,
                actual.z > .0001f ? expected.z / actual.z : 1));

            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

            return go;
        }
    }
}
