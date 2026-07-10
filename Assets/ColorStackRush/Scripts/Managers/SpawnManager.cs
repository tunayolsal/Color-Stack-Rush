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
        [SerializeField] float chunkLength = 11f;
        [SerializeField] float spawnAheadDistance = 80f;
        [SerializeField] float despawnBehindDistance = 18f;
        [SerializeField] float firstChunkZ = 18f; // clear runway at the start

        [Header("Level length")]
        [SerializeField] float baseLevelLength = 220f;
        [SerializeField] float lengthPerLevel = 40f;
        [SerializeField] float maxLevelLength = 600f;

        [Header("Finish stairs")]
        [SerializeField] int stairCount = 18;
        [SerializeField] float stairHeight = 0.45f;
        [SerializeField] float stairDepth = 1.6f;

        [Header("Spawn chances")]
        [Range(0f, 1f)] [SerializeField] float powerUpChance = 0.08f;
        [Range(0f, 1f)] [SerializeField] float activeColorBias = 0.6f; // % of block runs matching active color

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
                    if (spawnZ + chunkLength >= levelLength) { SpawnFinish(); break; }
                    SpawnChunk();
                    spawnZ += chunkLength;
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
            spawnZ = firstChunkZ;
            groundFrontZ = -groundTileLength;
            finishSpawned = false;
            levelLength = Mathf.Min(maxLevelLength,
                baseLevelLength + (SaveManager.Data.level - 1) * lengthPerLevel);
        }

        // ------------------------------------------------------------------
        //  Streaming
        // ------------------------------------------------------------------

        void EnsureGround(float untilZ)
        {
            while (groundFrontZ < untilZ)
            {
                var tile = groundPool.Get(new Vector3(0f, 0f, groundFrontZ + groundTileLength * 0.5f), Quaternion.identity);
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
                    activeItems.RemoveAt(i);
                }
            }

            for (int i = activeGround.Count - 1; i >= 0; i--)
            {
                if (activeGround[i].transform.position.z < playerZ - groundTileLength * 1.5f)
                {
                    activeGround[i].Release();
                    activeGround.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------
        //  Chunk patterns
        // ------------------------------------------------------------------

        /// <summary>Spawns one randomized gameplay pattern into [spawnZ, spawnZ + chunkLength].</summary>
        void SpawnChunk()
        {
            int roll = Random.Range(0, 100);

            if (roll < 30) SpawnBlockRun();
            else if (roll < 52) SpawnWallWithGap();
            else if (roll < 66) SpawnCoinRun();
            else if (roll < 81) SpawnSpinnerPattern();
            else SpawnSliderPattern();

            if (Random.value < powerUpChance) SpawnPowerUp();
        }

        GameColor PickBlockColor()
        {
            if (ColorManager.Instance != null && Random.value < activeColorBias)
                return ColorManager.Instance.ActiveColor;
            return (GameColor)Random.Range(0, 4);
        }

        /// <summary>A run of 4 blocks, straight or drifting diagonally across the road.</summary>
        void SpawnBlockRun()
        {
            GameColor color = PickBlockColor();
            float startX = Random.Range(-LaneHalf, LaneHalf);
            float endX = Random.value < 0.5f ? startX : Random.Range(-LaneHalf, LaneHalf);

            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                SpawnBlock(new Vector3(Mathf.Lerp(startX, endX, t), 0f, spawnZ + 1.5f + i * 2.4f), color);
            }
        }

        /// <summary>A wall across the road with one safe gap; a bonus block sits in the gap.</summary>
        void SpawnWallWithGap()
        {
            float z = spawnZ + chunkLength * 0.5f;
            float gapCenter = Random.Range(-LaneHalf + 0.6f, LaneHalf - 0.6f);
            const float gapHalfWidth = 1.35f;
            const float pieceWidth = 1.2f;

            for (float x = -roadWidth * 0.5f + pieceWidth * 0.5f; x < roadWidth * 0.5f; x += pieceWidth)
            {
                if (Mathf.Abs(x - gapCenter) < gapHalfWidth) continue; // leave the gap open
                var wall = wallPool.Get(new Vector3(x, 0f, z), Quaternion.identity);
                Track(wall);
            }

            // Reward for threading the needle.
            SpawnBlock(new Vector3(gapCenter, 0f, z + 2.5f), PickBlockColor());
        }

        /// <summary>A line of 5 coins, straight or gently sine-weaving.</summary>
        void SpawnCoinRun()
        {
            float x = Random.Range(-LaneHalf, LaneHalf);
            bool weave = Random.value < 0.4f;

            for (int i = 0; i < 5; i++)
            {
                float cx = weave ? Mathf.Sin(i * 0.9f) * LaneHalf * 0.7f : x;
                var coin = coinPool.Get(new Vector3(cx, 0f, spawnZ + 1f + i * 1.8f), Quaternion.identity);
                Track(coin);
            }
        }

        /// <summary>A rotating bar in the middle with tempting blocks on the edges.</summary>
        void SpawnSpinnerPattern()
        {
            float z = spawnZ + chunkLength * 0.5f;
            var spinner = spinnerPool.Get(new Vector3(Random.Range(-1f, 1f), 0f, z), Quaternion.identity);
            Track(spinner);

            GameColor color = PickBlockColor();
            SpawnBlock(new Vector3(-LaneHalf, 0f, z + 3.5f), color);
            SpawnBlock(new Vector3(LaneHalf, 0f, z + 3.5f), color);
        }

        /// <summary>A cube sliding across the road, followed by a short coin trail.</summary>
        void SpawnSliderPattern()
        {
            float z = spawnZ + 3f;
            var slider = sliderPool.Get(new Vector3(0f, 0f, z), Quaternion.identity);
            Track(slider);

            for (int i = 0; i < 3; i++)
            {
                var coin = coinPool.Get(new Vector3(0f, 0f, z + 3f + i * 1.6f), Quaternion.identity);
                Track(coin);
            }
        }

        void SpawnPowerUp()
        {
            // LuckyBox is rare; the four timed power-ups share the rest evenly.
            PowerUpType type = Random.value < 0.12f
                ? PowerUpType.LuckyBox
                : (PowerUpType)Random.Range(0, 4);

            var pos = new Vector3(Random.Range(-LaneHalf, LaneHalf), 0f, spawnZ + Random.Range(2f, chunkLength - 1f));
            var pickup = powerUpPool.Get(pos, Quaternion.identity);
            pickup.GetComponent<PowerUpPickup>().Setup(type);
            Track(pickup);
        }

        void SpawnBlock(Vector3 pos, GameColor color)
        {
            var block = blockPool.Get(pos, Quaternion.identity);
            block.GetComponent<CollectibleBlock>().Setup(color);
            Track(block);
        }

        void Track(GameObject go) => activeItems.Add(go.GetComponent<PooledObject>());

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
                    $"x{i + 1}", new Vector3(2.6f, top + 0.55f, stepZ), ColorPalette.UiText, 0.9f);
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

            blockPool   = new ObjectPool(BuildBlockTemplate(), transform, 24);
            coinPool    = new ObjectPool(BuildCoinTemplate(), transform, 16);
            wallPool    = new ObjectPool(BuildWallTemplate(), transform, 24);
            spinnerPool = new ObjectPool(BuildSpinnerTemplate(), transform, 4);
            sliderPool  = new ObjectPool(BuildSliderTemplate(), transform, 4);
            powerUpPool = new ObjectPool(BuildPowerUpTemplate(), transform, 3);
            groundPool  = new ObjectPool(BuildGroundTemplate(), transform, 8);
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
                Vector3.zero, new Vector3(4.4f, 0.45f, 0.45f), mat, "Bar");

            var col = bar.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(4.4f, 0.45f, 0.45f);

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
