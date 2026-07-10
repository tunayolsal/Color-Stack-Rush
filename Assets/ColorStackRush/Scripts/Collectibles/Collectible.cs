using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Base class for everything the player can pick up (blocks, coins,
    /// power-ups). Handles idle spin, magnet attraction and pooled release.
    /// </summary>
    public abstract class Collectible : MonoBehaviour
    {
        [Header("Collectible")]
        [SerializeField] float spinDegreesPerSecond = 90f;
        [SerializeField] float magnetRadius = 5f;
        [SerializeField] float magnetPullSpeed = 16f;

        protected Transform visual; // child that spins/bobs
        bool collected;

        /// <summary>Whether the magnet power-up should pull this item (blocks override this).</summary>
        protected virtual bool MagnetAttractable => true;

        protected virtual void Awake()
        {
            visual = transform.Find("Visual");
        }

        void OnEnable() => collected = false;

        protected virtual void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            // Idle spin makes pickups readable at speed.
            if (visual != null)
                visual.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);

            // Magnet power-up: glide toward the player when in range.
            if (MagnetAttractable && PowerUpManager.IsActive(PowerUpType.Magnet) && PlayerController.Instance != null)
            {
                Vector3 playerPos = PlayerController.Instance.transform.position + Vector3.up * 0.5f;
                if ((playerPos - transform.position).sqrMagnitude < magnetRadius * magnetRadius)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position, playerPos, magnetPullSpeed * Time.deltaTime);
                }
            }
        }

        /// <summary>Called by PlayerCollision. Guarantees single collection, then returns to pool.</summary>
        public void Collect(PlayerCollision player)
        {
            if (collected || !gameObject.activeSelf) return;
            collected = true;
            OnCollect(player);

            var pooled = GetComponent<PooledObject>();
            if (pooled != null) pooled.Release();
            else gameObject.SetActive(false);
        }

        /// <summary>Type-specific pickup behaviour.</summary>
        protected abstract void OnCollect(PlayerCollision player);
    }
}
