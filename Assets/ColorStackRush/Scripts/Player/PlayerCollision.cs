using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Trigger hub on the player. Routes collisions to collectibles, obstacles
    /// and the finish line. Owns the post-hit invincibility window.
    /// </summary>
    [RequireComponent(typeof(PlayerStack))]
    public class PlayerCollision : MonoBehaviour
    {
        PlayerStack stack;
        float invincibleUntil;
        bool finishTriggered;

        public PlayerStack Stack => stack;
        public bool IsInvincible => Time.time < invincibleUntil;

        void Awake() => stack = GetComponent<PlayerStack>();

        void OnEnable() => GameEvents.RunStarted += OnRunStarted;
        void OnDisable() => GameEvents.RunStarted -= OnRunStarted;

        void OnRunStarted()
        {
            finishTriggered = false;
            invincibleUntil = 0f;
        }

        void OnTriggerEnter(Collider other)
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            // Collectibles (blocks, coins, power-ups) share one base class.
            var collectible = other.GetComponentInParent<Collectible>();
            if (collectible != null)
            {
                collectible.Collect(this);
                return;
            }

            // Obstacles (colliders may sit on child parts, e.g. spinner bars).
            var obstacle = other.GetComponentInParent<Obstacle>();
            if (obstacle != null)
            {
                HandleObstacle(obstacle);
                return;
            }

            // Finish line.
            if (!finishTriggered && other.GetComponentInParent<FinishTrigger>() != null)
            {
                finishTriggered = true;
                GameEvents.RaiseFinishReached();
            }
        }

        void HandleObstacle(Obstacle obstacle)
        {
            // Shield power-up smashes obstacles instead of taking damage.
            if (PowerUpManager.IsActive(PowerUpType.Shield))
            {
                obstacle.Smash();
                HapticsManager.Light();
                return;
            }

            if (IsInvincible) return;
            var difficulty = GameManager.Instance.CurrentRun.Difficulty;
            invincibleUntil = Time.time + difficulty.InvincibilitySeconds;

            obstacle.PlayHitReaction();
            stack.RemoveBlocks(difficulty.ObstacleDamage);
            GameEvents.RaiseObstacleHit(transform.position);
            ParticleFactory.Burst(transform.position + Vector3.up * 0.5f, ColorPalette.Obstacle, 16, 5f);
            HapticsManager.Heavy();
        }
    }
}
