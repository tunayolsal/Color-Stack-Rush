using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// The block trail behind the ball. It is both the visual "snake" of
    /// collected blocks and the player's health: when it empties, the run ends.
    /// Blocks are pooled cubes updated with a cheap follow-chain (no physics).
    /// </summary>
    public class PlayerStack : MonoBehaviour
    {
        [Header("Stack")]
        [SerializeField] int startBlocks = 4;
        [SerializeField] float blockSize = 0.55f;
        [SerializeField] float spacing = 0.72f;

        [Header("Follow feel")]
        [SerializeField] float followSpeed = 14f; // higher = tighter snake

        readonly List<Transform> segments = new List<Transform>(64);
        readonly Stack<Transform> pool = new Stack<Transform>(64);
        Transform container; // world-space parent for trail blocks

        public int Count => segments.Count;

        void Awake()
        {
            container = new GameObject("StackBlocks").transform;
            for (int i = 0; i < 36; i++) { var block = CreateBlock(); block.gameObject.SetActive(false); pool.Push(block); }
        }

        void OnEnable() => GameEvents.RunStarted += ResetStack;
        void OnDisable() => GameEvents.RunStarted -= ResetStack;

        void LateUpdate()
        {
            var state = GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu;
            if (state != GameState.Playing && state != GameState.Finish) return;

            // Follow chain: each block chases the one ahead with growing lag,
            // producing a springy snake that whips around corners.
            float dt = Time.deltaTime;
            Vector3 ahead = transform.position;
            for (int i = 0; i < segments.Count; i++)
            {
                Transform seg = segments[i];
                float lag = followSpeed / (1f + i * 0.12f);
                Vector3 p = seg.position;
                p.x = Mathf.Lerp(p.x, ahead.x, lag * dt);
                p.y = Mathf.Lerp(p.y, ahead.y + blockSize * 0.5f, lag * dt);
                p.z = ahead.z - spacing;
                seg.position = p;
                ahead = p;
            }
        }

        /// <summary>Adds a block of the given color to the tail with a juicy pop.</summary>
        public void AddBlock(Color color)
        {
            if (Count >= 32) return;
            Transform block = pool.Count > 0 ? pool.Pop() : CreateBlock();
            block.gameObject.SetActive(true);
            block.GetComponent<MeshRenderer>().sharedMaterial = MaterialCache.Get(color);

            // Spawn at the current tail end so it doesn't teleport in.
            Vector3 tail = segments.Count > 0
                ? segments[segments.Count - 1].position
                : transform.position;
            block.position = tail + Vector3.back * spacing;
            block.localScale = Vector3.one * blockSize;
            block.rotation = Quaternion.identity;

            segments.Add(block);
            Juice.PunchScale(block, 0.5f, 0.25f);
            GameEvents.RaiseStackChanged(Count);
        }

        /// <summary>
        /// Removes blocks from the tail with a fly-off animation.
        /// Fires PlayerDied if the stack empties.
        /// </summary>
        public void RemoveBlocks(int amount)
        {
            for (int i = 0; i < amount && segments.Count > 0; i++)
            {
                Transform block = segments[segments.Count - 1];
                segments.RemoveAt(segments.Count - 1);

                Vector3 dir = new Vector3(Random.Range(-1f, 1f), 0.4f, Random.Range(-1f, -0.3f));
                Juice.FlyOff(block, dir, () => Recycle(block));
            }

            GameEvents.RaiseStackChanged(Count);
            if (Count == 0) GameEvents.RaisePlayerDied();
        }

        /// <summary>
        /// Consumes one block during the finish stairs sequence (silent removal
        /// with a small burst instead of the panic fly-off). Returns false when empty.
        /// </summary>
        public bool ConsumeTop()
        {
            if (segments.Count == 0) return false;

            Transform block = segments[segments.Count - 1];
            segments.RemoveAt(segments.Count - 1);

            var renderer = block.GetComponent<MeshRenderer>();
            ParticleFactory.Burst(block.position, renderer.sharedMaterial.color, 8, 3f, 0.22f);
            Recycle(block);

            GameEvents.RaiseStackChanged(Count);
            return true;
        }

        /// <summary>Clears the trail and grants the starting health blocks.</summary>
        void ResetStack()
        {
            // Reclaim pending fly-off blocks before a rapid restart.
            foreach (Transform child in container)
            {
                Juice.ForgetTransform(child);
                child.gameObject.SetActive(false);
                child.localScale = Vector3.one * blockSize;
                child.rotation = Quaternion.identity;
            }
            segments.Clear();
            pool.Clear();
            foreach (Transform child in container) pool.Push(child);
            while (segments.Count > 0)
            {
                Transform block = segments[segments.Count - 1];
                segments.RemoveAt(segments.Count - 1);
                Recycle(block);
            }

            for (int i = 0; i < startBlocks; i++)
                AddBlock(ColorPalette.Get((GameColor)Random.Range(0, 4)));
        }

        Transform CreateBlock()
        {
            var go = Primitives.Create(PrimitiveType.Cube, container,
                Vector3.zero, Vector3.one * blockSize, MaterialCache.Get(Color.white), "StackBlock");
            return go.transform;
        }

        void Recycle(Transform block)
        {
            Juice.ForgetTransform(block);
            block.gameObject.SetActive(false);
            block.localScale = Vector3.one * blockSize;
            pool.Push(block);
        }
    }
}
