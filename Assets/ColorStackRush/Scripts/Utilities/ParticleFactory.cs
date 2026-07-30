using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// One shared, pre-configured ParticleSystem per effect style.
    /// Effects are fired through Emit() with per-particle colors, so a single
    /// system serves every burst in the game — zero instantiation at runtime.
    /// </summary>
    public static class ParticleFactory
    {
        static ParticleSystem burstSystem;    // round soft puffs (collect, hit)
        static ParticleSystem confettiSystem; // square falling confetti (victory)
        static ParticleSystem.EmitParams emit;

        /// <summary>Soft round burst at a world position (collect / hit feedback).</summary>
        public static void Burst(Vector3 position, Color color, int count, float speed = 4.5f, float size = 0.3f)
        {
            EnsureSystems();
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f; // bias upward
                emit.position = position;
                emit.velocity = dir * Random.Range(speed * 0.5f, speed);
                emit.startColor = color;
                emit.startSize = Random.Range(size * 0.6f, size * 1.3f);
                emit.startLifetime = Random.Range(0.35f, 0.7f);
                burstSystem.Emit(emit, 1);
            }
        }

        /// <summary>Multicolor confetti rain (victory celebration).</summary>
        public static void Confetti(Vector3 position, int count = 120)
        {
            EnsureSystems();
            for (int i = 0; i < count; i++)
            {
                var color = ColorPalette.Get((GameColor)Random.Range(0, 4));
                emit.position = position + new Vector3(Random.Range(-3f, 3f), Random.Range(2f, 6f), Random.Range(-2f, 2f));
                emit.velocity = new Vector3(Random.Range(-2f, 2f), Random.Range(1f, 5f), Random.Range(-2f, 2f));
                emit.startColor = color;
                emit.startSize = Random.Range(0.15f, 0.3f);
                emit.startLifetime = Random.Range(1.2f, 2.2f);
                emit.rotation = Random.Range(0f, 360f);
                confettiSystem.Emit(emit, 1);
            }
        }

        static void EnsureSystems()
        {
            if (burstSystem != null) return;
            burstSystem = BuildSystem("[FX_Burst]", MakeCircleTexture(), gravity: 0.6f);
            confettiSystem = BuildSystem("[FX_Confetti]", null, gravity: 0.9f); // squares = paper
            emit = new ParticleSystem.EmitParams();
        }

        /// <summary>Creates a world-space particle system that only emits on demand.</summary>
        static ParticleSystem BuildSystem(string name, Texture2D texture, float gravity)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 600;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;

            var emission = ps.emission;
            emission.enabled = false; // manual Emit() only

            var shape = ps.shape;
            shape.enabled = false;

            // Sprites/Default supports vertex colors + alpha with no extra setup, but it
            // isn't guaranteed to survive shader stripping on every platform (e.g. WebGL)
            // unless explicitly added to Always Included Shaders. Fall back to the
            // Standard shader (guaranteed present — MaterialCache depends on it) so a
            // stripped Sprites/Default never crashes particle creation.
            var particleShader = Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            var mat = new Material(particleShader);
            if (texture != null) mat.mainTexture = texture;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return ps;
        }

        /// <summary>Generates a small soft-edged circle texture so bursts look round.</summary>
        static Texture2D MakeCircleTexture()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep falloff
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
