using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Currency pickup. Worth 1 coin (2 with the Double Coins power-up).
    /// </summary>
    public class Coin : Collectible
    {
        [SerializeField] int value = 1;

        protected override void OnCollect(PlayerCollision player)
        {
            int amount = PowerUpManager.IsActive(PowerUpType.DoubleCoins) ? value * 2 : value;

            CurrencyManager.AddCoins(amount);
            GameEvents.RaiseCoinCollected(amount, transform.position);
            ParticleFactory.Burst(transform.position, ColorPalette.Coin, 10, 4f, 0.22f);
            HapticsManager.Light();
        }
    }
}
