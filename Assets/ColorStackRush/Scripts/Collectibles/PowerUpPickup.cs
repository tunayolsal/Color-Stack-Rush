using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Floating power-up orb. Activates its power-up type on pickup.
    /// The type/appearance is configured by the spawner when taken from the pool.
    /// </summary>
    public class PowerUpPickup : Collectible
    {
        public PowerUpType Type { get; private set; }

        MeshRenderer orbRenderer;
        TextMesh label;

        protected override void Awake()
        {
            base.Awake();
            if (visual != null)
            {
                orbRenderer = visual.GetComponent<MeshRenderer>();
                label = GetComponentInChildren<TextMesh>(true);
            }
        }

        /// <summary>Configures type, color and letter label when spawned.</summary>
        public void Setup(PowerUpType type)
        {
            Type = type;
            Color c = ColorPalette.GetPowerUp(type);
            if (orbRenderer != null) orbRenderer.sharedMaterial = MaterialCache.GetEmissive(c, 0.7f);
            if (label != null) label.text = LabelFor(type);
        }

        static string LabelFor(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Magnet:      return "M";
                case PowerUpType.DoubleCoins: return "2X";
                case PowerUpType.Shield:      return "SH";
                case PowerUpType.SlowMotion:  return "SL";
                default:                      return "?"; // LuckyBox
            }
        }

        protected override void OnCollect(PlayerCollision player)
        {
            if (PowerUpManager.Instance != null)
                PowerUpManager.Instance.Activate(Type);

            ParticleFactory.Burst(transform.position + Vector3.up, ColorPalette.GetPowerUp(Type), 18, 5f);
            HapticsManager.Medium();
        }
    }
}
