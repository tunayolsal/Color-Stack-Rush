using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Single entry point that assembles the whole game at startup:
    /// managers, player, camera rig, lighting and UI — all from code and
    /// primitives, so the scene only needs this one component to be playable.
    /// </summary>
    [DefaultExecutionOrder(-100)] // build everything before other Start() calls
    public class GameBootstrapper : MonoBehaviour
    {
        [Header("Quality")]
        [SerializeField] float shadowDistance = 45f;

        void Awake()
        {
            SaveManager.Load();
            ApplyQualitySettings();
            for (int i = 0; i < 4; i++) MaterialCache.Get(ColorPalette.Get((GameColor)i));
            PrefabArt.Warmup();
            ParticleFactory.Burst(Vector3.zero, Color.white, 0);
            SetupEnvironment();

            // --- Managers (added in dependency order; Awake runs immediately per AddComponent) ---
            var managers = new GameObject("[Managers]");
            managers.AddComponent<AudioManager>();
            managers.AddComponent<GameManager>();
            managers.AddComponent<ColorManager>();
            managers.AddComponent<ScoreManager>();
            managers.AddComponent<PowerUpManager>();
            managers.AddComponent<ShopManager>();
            managers.AddComponent<DailyRewardManager>();
            managers.AddComponent<SwipeInput>();
            managers.AddComponent<ThemePresentation>();

            // --- World actors ---
            var player = BuildPlayer();
            new GameObject("[World]").AddComponent<SpawnManager>();
            BuildCameraRig(player.transform);

            // --- UI (built last so it can query manager state safely) ---
            var ui = new GameObject("[UI]");
            ui.AddComponent<FloatingTextManager>();
            ui.AddComponent<UIBuilder>();

            // Announce initial color so the ring/HUD are tinted before the first run.
            GameEvents.RaiseActiveColorChanged(ColorManager.Instance.ActiveColor);
            GameEvents.RaiseCoinsChanged(SaveManager.Data.coins);
        }

        void ApplyQualitySettings()
        {
            QualitySettings.shadows = ShadowQuality.All;      // soft shadows
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = shadowDistance;
            QualityProfile.Apply();
            QualitySettings.vSyncCount = 0;                   // let targetFrameRate rule on mobile
        }

        /// <summary>Pastel sky, soft distance fog and one soft-shadow sun.</summary>
        void SetupEnvironment()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = ColorPalette.Fog;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 105f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.38f, .43f, .50f);

            var light = FindFirstObjectByType<Light>();
            if (light == null)
                light = new GameObject("Directional Light").AddComponent<Light>();

            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            light.color = new Color(1f, 0.98f, 0.94f);
            light.intensity = .9f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = .6f; // soft, pastel-friendly shadows
        }

        /// <summary>Ball + collect ring + shield orb + gameplay components, all primitives.</summary>
        GameObject BuildPlayer()
        {
            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;

            // Kinematic rigidbody so trigger events fire against static colliders.
            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var col = player.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.5f, 0f);
            col.radius = 0.55f;

            // Rolling ball visual.
            Primitives.Create(PrimitiveType.Sphere, player.transform,
                new Vector3(0f, 0.5f, 0f), Vector3.one * 0.95f,
                MaterialCache.Get(ColorPalette.Get(GameColor.Pink)), "Ball");

            // Flat glowing ring under the ball showing the color to collect.
            ToyMeshes.Ring(player.transform, "ColorRing",
                new Vector3(0f, .08f, 0f), new Vector3(1.25f, 1f, 1.25f), MaterialCache.Get(ColorPalette.Get(GameColor.Pink)));
            ToyMeshes.Ring(player.transform, "CosmeticRim", new Vector3(0f, .58f, 0f), new Vector3(.94f, .35f, .94f), MaterialCache.Get(new Color(.95f, .96f, 1f)));
            var shadowMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"))
            { mainTexture = Resources.Load<Texture2D>("Art/Particles/circle_05"), color = new Color(0, 0, 0, .33f) };
            var contact = Primitives.Create(PrimitiveType.Quad, player.transform, new Vector3(0, .025f, 0), Vector3.one * 1.75f, shadowMaterial, "ContactShadow");
            contact.transform.localRotation = Quaternion.Euler(90, 0, 0);
            contact.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            contact.GetComponent<MeshRenderer>().receiveShadows = false;

            // Shield bubble (activated by PlayerVisuals when the power-up runs).
            Primitives.Create(PrimitiveType.Sphere, player.transform,
                new Vector3(0f, 0.5f, 0f), Vector3.one * 1.9f,
                null, "ShieldOrb");

            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerStack>();
            player.AddComponent<PlayerCollision>();
            player.AddComponent<PlayerVisuals>();
            return player;
        }

        /// <summary>Rig hierarchy: CameraRig (follow) → Shaker (shake) → Camera.</summary>
        void BuildCameraRig(Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ColorPalette.Sky;
            cam.fieldOfView = 45f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f; // matches fog end; keeps draw distance cheap

            var rig = new GameObject("CameraRig");
            var shaker = new GameObject("Shaker");
            shaker.transform.SetParent(rig.transform, false);
            cam.transform.SetParent(shaker.transform, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;

            var follow = rig.AddComponent<CameraFollow>();
            shaker.AddComponent<CameraShake>();
            follow.SetTarget(target);
        }
    }
}
