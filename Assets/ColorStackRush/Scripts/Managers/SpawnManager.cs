using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Endless world generator. Builds every template (blocks, coins,
    /// obstacles, ground, power-ups) from primitives at startup, then streams
    /// pooled "chunks" of content ahead of the player and recycles everything
    /// that falls behind. Also spawns the finish gate + multiplier stairs.
    /// </summary>
    public class SpawnManager : MonoBehaviour
    {
        public static SpawnManager Instance { get; private set; }

        [Header("Road")]
        [SerializeField] float roadWidth = 7f;
        [SerializeField] float groundTileLength = 30f;

        [Header("Streaming")]
        [SerializeField] float spawnAheadDistance = 80f;
        [SerializeField] float despawnBehindDistance = 18f;


        [Header("Finish stairs")]
        [SerializeField] int stairCount = 18;
        [SerializeField] float stairHeight = 0.45f;
        [SerializeField] float stairDepth = 1.6f;


        // Pools
        ObjectPool blockPool, coinPool, wallPool, spinnerPool, sliderPool, powerUpPool, groundPool;

        readonly List<PooledObject> activeItems = new List<PooledObject>(128);
        readonly List<PooledObject> activeGround = new List<PooledObject>(8);

        /// <summary>Top-surface world positions of each finish stair (player path).</summary>
        public List<Vector3> FinishSteps { get; } = new List<Vector3>(24);

        Transform templateRoot;
        GameObject finishRoot;
        float spawnZ;
        float groundFrontZ;
        float levelLength;
        int segmentIndex;
        RunConfig config;
        readonly TrackSegment segment = new TrackSegment();
        bool finishSpawned;

        float LaneHalf => roadWidth * 0.5f - 0.9f; // safe spawn band inside the rails

        /// <summary>Bonus points for climbing stair index i (escalates per step).</summary>
        public static int StepValue(int stepIndex) => (stepIndex + 1) * 10;

        void Awake()
        {
            Instance = this;
            BuildTemplates();
            ResetStreamingState();
            EnsureGround(60f); // menu backdrop
        }

        void OnEnable() => GameEvents.RunStarted += ResetWorld;
        void OnDisable() => GameEvents.RunStarted -= ResetWorld;

        void Update()
        {
            float playerZ = PlayerController.Instance != null
                ? PlayerController.Instance.transform.position.z : 0f;

            EnsureGround(playerZ + 120f);

            var state = GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu;
            if (state == GameState.Playing)
            {
                // Stream content ahead of the player until the finish line.
                while (!finishSpawned && spawnZ < playerZ + spawnAheadDistance)
                {
                    // Stop a full chunk early so nothing overlaps the finish gate.
                    if (config.mode == RunMode.Campaign && spawnZ + TrackSegment.Length >= levelLength) { SpawnFinish(); break; }
                    SpawnChunk();
                    spawnZ += TrackSegment.Length;
                    segmentIndex++;
                }
            }

            if (state == GameState.Playing || state == GameState.Finish)
                DespawnBehind(playerZ);
        }

        // ------------------------------------------------------------------
        //  World reset
        // ------------------------------------------------------------------

        /// <summary>Clears the whole track and prepares a fresh level (RunStarted).</summary>
        void ResetWorld()
        {
            for (int i = activeItems.Count - 1; i >= 0; i--) activeItems[i].Release();
            activeItems.Clear();
            for (int i = activeGround.Count - 1; i >= 0; i--) activeGround[i].Release();
            activeGround.Clear();

            if (finishRoot != null) Destroy(finishRoot);
            FinishSteps.Clear();

            ResetStreamingState();
            CurrencyManager.ResetRunCoins();
            EnsureGround(120f);
        }

        void ResetStreamingState()
        {
            config = GameManager.Instance != null ? GameManager.Instance.CurrentRun : RunConfig.Campaign(1);
            segmentIndex = 0;
            spawnZ = TrackPlanner.FirstZ;
            groundFrontZ = -groundTileLength;
            finishSpawned = false;
            levelLength = config.Length;
        }

        // ------------------------------------------------------------------
        //  Streaming
        // ------------------------------------------------------------------

        void EnsureGround(float untilZ)
        {
            while (groundFrontZ < untilZ)
            {
                var tile = groundPool.Get(new Vector3(0f, 0f, groundFrontZ + groundTileLength * 0.5f), Quaternion.identity);
                ThemePresentation.TintGround(tile, config.Theme);
                activeGround.Add(tile.GetComponent<PooledObject>());
                groundFrontZ += groundTileLength;
            }
        }

        void DespawnBehind(float playerZ)
        {
            // Reverse sweep: release passed/collected items without allocations.
            for (int i = activeItems.Count - 1; i >= 0; i--)
            {
                var item = activeItems[i];
                if (!item.gameObject.activeSelf)
                {
                    activeItems.RemoveAt(i); // was collected/smashed and self-released
                }
                else if (item.transform.position.z < playerZ - despawnBehindDistance)
                {
                    item.Release();
                }
            }

            for (int i = activeGround.Count - 1; i >= 0; i--)
            {
                if (activeGround[i].transform.position.z < playerZ - groundTileLength * 1.5f)
                {
                    activeGround[i].Release();
                }
            }
        }

        // ------------------------------------------------------------------
        //  Chunk patterns
        // ------------------------------------------------------------------

        /// <summary>Spawns one randomized gameplay pattern into [spawnZ, spawnZ + chunkLength].</summary>
        void SpawnChunk()
        {
            TrackPlanner.Fill(config, segmentIndex, segment);
            if (!TrackValidator.Validate(segment, TrackPlanner.SafeX(segmentIndex - 1), config.MaxSpeed)) TrackPlanner.Fallback(segment);
            for (int i = 0; i < segment.count; i++)
            {
                var item = segment.items[i];
                var pos = new Vector3(item.x, 0, segment.startZ + item.z);
                GameObject go = null;
                switch (item.kind)
                {
                    case TrackKind.Block: SpawnBlock(pos, item.color); break;
                    case TrackKind.Coin: go = coinPool.Get(pos, Quaternion.identity); break;
                    case TrackKind.Wall: go = wallPool.Get(pos, Quaternion.identity); break;
                    case TrackKind.Spinner: go = spinnerPool.Get(pos, Quaternion.identity); break;
                    case TrackKind.Slider: go = sliderPool.Get(pos, Quaternion.identity); break;
                    case TrackKind.PowerUp: go = powerUpPool.Get(pos, Quaternion.identity); go.GetComponent<PowerUpPickup>().Setup(item.power); break;
                }
                if (go != null)
                {
                    var obstacle = go.GetComponent<Obstacle>();
                    if (obstacle != null) obstacle.SetupMotion(item.phase);
                    Track(go);
                }
            }
        }

        void SpawnBlock(Vector3 pos, GameColor color)
        {
            var block = blockPool.Get(pos, Quaternion.identity);
            block.GetComponent<CollectibleBlock>().Setup(color);
            Track(block);
        }

        void Track(GameObject go)
        {
            var item = go.GetComponent<PooledObject>();
            if (!activeItems.Contains(item)) activeItems.Add(item);
        }
        void Untrack(PooledObject item) { activeItems.Remove(item); activeGround.Remove(item); }

        // ------------------------------------------------------------------
        //  Finish gate + multiplier stairs
        // ------------------------------------------------------------------

        /// <summary>Builds the finish gate and the rising multiplier staircase.</summary>
        void SpawnFinish()
        {
            finishSpawned = true;
            finishRoot = new GameObject("Finish");
            float z = levelLength + 6f;

            // --- Gate: two posts + crossbar + trigger volume ---
            var postMat = MaterialCache.Get(ColorPalette.Stairs);
            Primitives.Create(PrimitiveType.Cube, finishRoot.transform,
                new Vector3(-roadWidth * 0.5f, 1.6f, z), new Vector3(0.4f, 3.2f, 0.4f), postMat, "PostL");
            Primitives.Create(PrimitiveType.Cube, finishRoot.transform,
                new Vector3(roadWidth * 0.5f, 1.6f, z), new Vector3(0.4f, 3.2f, 0.4f), postMat, "PostR");
            Primitives.Create(PrimitiveType.Cube, finishRoot.transform,
                new Vector3(0f, 3.2f, z), new Vector3(roadWidth + 0.4f, 0.4f, 0.4f), postMat, "Crossbar");

            var triggerGo = new GameObject("FinishTrigger");
            triggerGo.transform.SetParent(finishRoot.transform, false);
            triggerGo.transform.position = new Vector3(0f, 1.5f, z);
            var trigger = triggerGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(roadWidth, 3f, 0.6f);
            triggerGo.AddComponent<FinishTrigger>();

            // --- Multiplier stairs: rising steps with a pastel gradient + labels ---
            FinishSteps.Clear();
            float stairsStartZ = z + 4f;
            for (int i = 0; i < stairCount; i++)
            {
                float top = (i + 1) * stairHeight;
                float stepZ = stairsStartZ + i * stairDepth;

                Color stepColor = Color.Lerp(
                    ColorPalette.Get((GameColor)(i % 4)),
                    Color.white, 0.35f);

                Primitives.Create(PrimitiveType.Cube, finishRoot.transform,
                    new Vector3(0f, top * 0.5f, stepZ),
                    new Vector3(roadWidth + 2f, top, stairDepth * 0.96f),
                    MaterialCache.Get(stepColor), $"Step{i + 1}");

                // Multiplier label floating above the step.
                var label = WorldText.Create(finishRoot.transform,
                    $"+{StepValue(i)}", new Vector3(2.6f, top + 0.55f, stepZ), ColorPalette.UiText, 0.9f);
                label.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

                // Where the player's root should land when climbing this step.
                FinishSteps.Add(new Vector3(0f, top, stepZ));
            }
        }

        // ------------------------------------------------------------------
        //  Template construction (primitives only, built once)
        // ------------------------------------------------------------------

        void BuildTemplates()
        {
            templateRoot = new GameObject("Templates").transform;
            templateRoot.SetParent(transform, false);

            blockPool   = new ObjectPool(BuildBlockTemplate(), transform, 48);
            coinPool    = new ObjectPool(BuildCoinTemplate(), transform, 40);
            wallPool    = new ObjectPool(BuildWallTemplate(), transform, 24);
            spinnerPool = new ObjectPool(BuildSpinnerTemplate(), transform, 4);
            sliderPool  = new ObjectPool(BuildSliderTemplate(), transform, 4);
            powerUpPool = new ObjectPool(BuildPowerUpTemplate(), transform, 3);
            groundPool  = new ObjectPool(BuildGroundTemplate(), transform, 8);
            blockPool.Released += Untrack; coinPool.Released += Untrack; wallPool.Released += Untrack;
            spinnerPool.Released += Untrack; sliderPool.Released += Untrack; powerUpPool.Released += Untrack; groundPool.Released += Untrack;
        }

        GameObject NewTemplate(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(templateRoot, false);
            return go;
        }

        GameObject BuildBlockTemplate()
        {
            var root = NewTemplate("Block");
            Primitives.Create(PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.5f, 0f), Vector3.one * 0.8f,
                MaterialCache.Get(ColorPalette.Get(GameColor.Pink)), "Visual");

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(1.1f, 1.1f, 1.1f); // slightly generous pickup

            ColorSymbols.AddWorld(root.transform);
            root.AddComponent<CollectibleBlock>();
            return root;
        }

        GameObject BuildCoinTemplate()
        {
            var root = NewTemplate("Coin");
            // Flattened cylinder = coin disc, floating at chest height.
            var visual = Primitives.Create(PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.07f, 0.7f),
                MaterialCache.GetEmissive(ColorPalette.Coin, 0.5f), "Visual");
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.9f, 0f);
            col.radius = 0.65f;

            root.AddComponent<Coin>();
            return root;
        }

        GameObject BuildWallTemplate()
        {
            var root = NewTemplate("Wall");
            Primitives.Create(PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.6f, 0f), new Vector3(1.18f, 1.2f, 0.9f),
                MaterialCache.Get(ColorPalette.Obstacle), "Visual");

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(1.1f, 1.2f, 0.85f);

            root.AddComponent<Obstacle>().SetKind(ObstacleKind.Wall);
            return root;
        }

        GameObject BuildSpinnerTemplate()
        {
            var root = NewTemplate("Spinner");
            var mat = MaterialCache.Get(ColorPalette.Obstacle);

            // Center pole.
            Primitives.Create(PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.8f, 0f), new Vector3(0.3f, 0.8f, 0.3f), mat, "Pole");

            // Rotating bar ("Moving" is what Obstacle.Update rotates).
            var bar = new GameObject("Moving");
            bar.transform.SetParent(root.transform, false);
            bar.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            Primitives.Create(PrimitiveType.Cube, bar.transform,
                Vector3.zero, new Vector3(1.4f, 0.45f, 0.45f), mat, "Bar");

            var col = bar.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.4f, 0.45f, 0.45f);

            root.AddComponent<Obstacle>().SetKind(ObstacleKind.Spinner);
            return root;
        }

        GameObject BuildSliderTemplate()
        {
            var root = NewTemplate("Slider");
            Primitives.Create(PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.75f, 0f), Vector3.one * 1.5f,
                MaterialCache.Get(ColorPalette.Obstacle), "Visual");

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 0.75f, 0f);
            col.size = new Vector3(1.4f, 1.5f, 1.4f);

            root.AddComponent<Obstacle>().SetKind(ObstacleKind.Slider);
            return root;
        }

        GameObject BuildPowerUpTemplate()
        {
            var root = NewTemplate("PowerUp");
            Primitives.Create(PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 1f, 0f), Vector3.one * 0.9f,
                MaterialCache.GetEmissive(Color.white, 0.6f), "Visual");

            // Letter label so the type is readable at a glance.
            var label = WorldText.Create(root.transform, "?", new Vector3(0f, 1.9f, 0f), ColorPalette.UiText, 0.7f);
            label.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1f, 0f);
            col.radius = 0.75f;

            root.AddComponent<PowerUpPickup>();
            return root;
        }

        GameObject BuildGroundTemplate()
        {
            var root = NewTemplate("GroundTile");

            // Road surface (top sits at y = 0).
            Primitives.Create(PrimitiveType.Cube, root.transform,
                new Vector3(0f, -0.25f, 0f), new Vector3(roadWidth + 1.4f, 0.5f, groundTileLength),
                MaterialCache.Get(ColorPalette.Ground), "Surface");

            // Soft rails on both edges so the road reads clearly.
            var railMat = MaterialCache.Get(ColorPalette.GroundRail);
            Primitives.Create(PrimitiveType.Cube, root.transform,
                new Vector3(-(roadWidth * 0.5f + 0.55f), -0.05f, 0f), new Vector3(0.5f, 0.7f, groundTileLength), railMat, "RailL");
            Primitives.Create(PrimitiveType.Cube, root.transform,
                new Vector3(roadWidth * 0.5f + 0.55f, -0.05f, 0f), new Vector3(0.5f, 0.7f, groundTileLength), railMat, "RailR");

            return root;
        }
    }
}
