using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorStackRush
{
    /// <summary>Shared smooth bevels: one mesh serves every block and obstacle.</summary>
    public static class ToyMeshes
    {
        static Mesh rounded, torus;
        public static Mesh Rounded => rounded != null ? rounded : rounded = BuildRounded();
        public static Mesh Torus => torus != null ? torus : torus = BuildTorus();

        public static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, Material material)
            => Create(parent, name, Rounded, position, size, material);

        public static GameObject Ring(Transform parent, string name, Vector3 position, Vector3 size, Material material)
            => Create(parent, name, Torus, position, size, material);

        static GameObject Create(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 size, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        static Mesh BuildRounded()
        {
            const float radius = .14f, inside = .5f - radius;
            float[] steps = { -.5f, -inside, 0, inside, .5f };
            var vertices = new List<Vector3>(150);
            var normals = new List<Vector3>(150);
            var uv = new List<Vector2>(150);
            var triangles = new List<int>(576);
            Vector3[] normal = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            for (int face = 0; face < 6; face++)
            {
                Vector3 n = normal[face];
                Vector3 u = Mathf.Abs(n.y) > .5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                int start = vertices.Count;
                for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
                {
                    Vector3 p = n * .5f + u * steps[x] + v * steps[y];
                    Vector3 q = new Vector3(Mathf.Clamp(p.x, -inside, inside), Mathf.Clamp(p.y, -inside, inside), Mathf.Clamp(p.z, -inside, inside));
                    Vector3 direction = (p - q).normalized;
                    vertices.Add(q + direction * radius);
                    normals.Add(direction);
                    uv.Add(new Vector2(x / 4f, y / 4f));
                }
                for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
                {
                    int a = start + y * 5 + x, b = a + 1, c = a + 5, d = c + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }
            var mesh = new Mesh { name = "SharedToyBevel" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }

        static Mesh BuildTorus()
        {
            const int around = 32, tube = 8;
            var vertices = new Vector3[(around + 1) * (tube + 1)];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[around * tube * 6];
            int t = 0;
            for (int i = 0; i <= around; i++) for (int j = 0; j <= tube; j++)
            {
                float a = i * Mathf.PI * 2 / around, b = j * Mathf.PI * 2 / tube;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                int k = i * (tube + 1) + j;
                normals[k] = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                vertices[k] = radial * .5f + normals[k] * .055f;
                uv[k] = new Vector2(i / (float)around, j / (float)tube);
                if (i < around && j < tube)
                {
                    int q = k + tube + 1;
                    triangles[t++] = k; triangles[t++] = k + 1; triangles[t++] = q;
                    triangles[t++] = k + 1; triangles[t++] = q + 1; triangles[t++] = q;
                }
            }
            var mesh = new Mesh { name = "SharedToyRing", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
