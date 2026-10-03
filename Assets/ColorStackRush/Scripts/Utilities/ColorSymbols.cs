using UnityEngine;
namespace ColorStackRush
{
    // Procedural geometry instead of font glyphs: symbols remain readable on every device.
    public static class ColorSymbols
    {
        public static string Name(GameColor color) => color == GameColor.Pink ? "CIRCLE" : color == GameColor.Blue ? "SQUARE" : color == GameColor.Yellow ? "DIAMOND" : "TRIANGLE";
        public static Mesh MeshFor(GameColor color)
        {
            int n = color == GameColor.Pink ? 20 : color == GameColor.Green ? 3 : 4;
            var mesh = new Mesh { name = "ColorSymbol" + color };
            var vertices = new Vector3[n + 1];
            var triangles = new int[n * 3];
            float angle = color == GameColor.Blue ? 45 : 90;
            for (int i = 0; i < n; i++)
            {
                float r = (angle + i * 360f / n) * Mathf.Deg2Rad;
                vertices[i + 1] = new Vector3(Mathf.Cos(r), Mathf.Sin(r), 0) * .35f;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % n + 1; triangles[i * 3 + 2] = i + 1;
            }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); return mesh;
        }
        static readonly Sprite[] sprites = new Sprite[4];
        public static Sprite SpriteFor(GameColor color)
        {
            int index = (int)color;
            if (sprites[index] != null) return sprites[index];
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float px = (x + .5f - 32) / 30, py = (y + .5f - 32) / 30;
                bool inside = color == GameColor.Pink ? px * px + py * py <= 1 : color == GameColor.Blue ? Mathf.Max(Mathf.Abs(px), Mathf.Abs(py)) <= .82f : color == GameColor.Yellow ? Mathf.Abs(px) + Mathf.Abs(py) <= 1 : py >= -.8f && py <= 1 && Mathf.Abs(px) <= (1 - py) / 2;
                tex.SetPixel(x, y, new Color(1, 1, 1, inside ? 1 : 0));
            }
            tex.Apply();
            sprites[index] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
            return sprites[index];
        }
        static readonly Mesh[] meshes = new Mesh[4];
        public static Mesh Shared(GameColor color) { int i = (int)color; if (meshes[i] == null) meshes[i] = MeshFor(color); return meshes[i]; }
        public static void AddWorld(Transform parent)
        {
            var go = new GameObject("Symbol", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, .65f, -.415f);
            go.transform.localRotation = Quaternion.Euler(25, 0, 0);
            go.GetComponent<MeshFilter>().sharedMesh = Shared(GameColor.Pink);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialCache.Get(Color.white);
        }
    }
}
