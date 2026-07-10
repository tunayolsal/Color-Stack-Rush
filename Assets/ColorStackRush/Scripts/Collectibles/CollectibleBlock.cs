using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// A colored block on the road. Collecting the active color grows the
    /// stack (and combo); collecting a wrong color costs a stack block.
    /// </summary>
    public class CollectibleBlock : Collectible
    {
        public GameColor BlockColor { get; private set; }

        MeshRenderer visualRenderer;

        protected override void Awake()
        {
            base.Awake();
            if (visual != null) visualRenderer = visual.GetComponent<MeshRenderer>();
        }

        // The magnet only pulls blocks that currently help the player.
        protected override bool MagnetAttractable =>
            ColorManager.Instance != null && ColorManager.Instance.ActiveColor == BlockColor;

        /// <summary>Configures the block's color when spawned from the pool.</summary>
        public void Setup(GameColor color)
        {
            BlockColor = color;
            if (visualRenderer != null)
                visualRenderer.sharedMaterial = MaterialCache.Get(ColorPalette.Get(color));
        }

        protected override void OnCollect(PlayerCollision player)
        {
            bool correct = ColorManager.Instance != null && ColorManager.Instance.ActiveColor == BlockColor;
            Color displayColor = ColorPalette.Get(BlockColor);

            if (correct)
            {
                player.Stack.AddBlock(displayColor);
                ParticleFactory.Burst(transform.position + Vector3.up * 0.5f, displayColor, 12);
                HapticsManager.Light();
            }
            else
            {
                player.Stack.RemoveBlocks(1);
                ParticleFactory.Burst(transform.position + Vector3.up * 0.5f, ColorPalette.UiBad, 10);
                HapticsManager.Medium();
            }

            GameEvents.RaiseBlockCollected(correct, transform.position);
        }
    }
}
