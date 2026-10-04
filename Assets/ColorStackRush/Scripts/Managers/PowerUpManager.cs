using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Tracks active power-ups and their timers. Other systems query
    /// PowerUpManager.IsActive(type) — e.g. coins check DoubleCoins,
    /// collectibles check Magnet, collisions check Shield.
    /// </summary>
    public class PowerUpManager : MonoBehaviour
    {
        public static PowerUpManager Instance { get; private set; }

        [Header("Durations (seconds)")]
        [SerializeField] float magnetDuration = 8f;
        [SerializeField] float doubleCoinsDuration = 10f;
        [SerializeField] float shieldDuration = 8f;
        [SerializeField] float slowMotionDuration = 5f;

        [Header("Lucky box")]
        [SerializeField] int luckyCoinsMin = 15;
        [SerializeField] int luckyCoinsMax = 50;

        // Remaining time per active power-up.
        readonly Dictionary<PowerUpType, float> active = new Dictionary<PowerUpType, float>();
        static readonly List<PowerUpType> expiredBuffer = new List<PowerUpType>(4);

        readonly StringBuilder summaryBuilder = new StringBuilder(64);

        void Awake() => Instance = this;

        void OnEnable() => GameEvents.RunStarted += ClearAll;
        void OnDisable() => GameEvents.RunStarted -= ClearAll;

        /// <summary>True if the given power-up is currently running.</summary>
        public static bool IsActive(PowerUpType type)
        {
            return Instance != null && Instance.active.ContainsKey(type);
        }

        /// <summary>Activates a power-up (refreshes the timer if already active).</summary>
        public void Activate(PowerUpType type)
        {
            if (type == PowerUpType.LuckyBox)
            {
                OpenLuckyBox();
                return;
            }

            float duration = DurationFor(type);
            bool wasActive = active.ContainsKey(type);
            active[type] = duration;

            if (type == PowerUpType.SlowMotion)
                GameManager.Instance.SetSlowMotion(true);

            if (!wasActive)
                GameEvents.RaisePowerUpStarted(type, duration);
        }

        /// <summary>Lucky box: instant random reward — coins or a random power-up.</summary>
        void OpenLuckyBox()
        {
            GameEvents.RaisePowerUpStarted(PowerUpType.LuckyBox, 0f);

            if (Random.value < 0.5f)
            {
                int coins = Random.Range(luckyCoinsMin, luckyCoinsMax + 1);
                CurrencyManager.AddCoins(coins);
                if (PlayerController.Instance != null)
                    GameEvents.RaiseCoinCollected(coins, PlayerController.Instance.transform.position);
            }
            else
            {
                // Any timed power-up, never another lucky box.
                Activate((PowerUpType)Random.Range(0, 4));
            }
        }

        void Update()
        {
            if (active.Count == 0) return;
            if (GameManager.Instance.State != GameState.Playing) return;

            expiredBuffer.Clear();
            // Tick down every active timer (scaled time: slow-mo affects itself, which feels fair).
            foreach (var type in active.Keys)
                expiredBuffer.Add(type);

            foreach (var type in expiredBuffer)
            {
                active[type] -= Time.deltaTime;
                if (active[type] <= 0f) Expire(type);
            }
        }

        void Expire(PowerUpType type)
        {
            active.Remove(type);
            if (type == PowerUpType.SlowMotion)
                GameManager.Instance.SetSlowMotion(false);
            GameEvents.RaisePowerUpEnded(type);
        }

        void ClearAll()
        {
            expiredBuffer.Clear();
            foreach (var type in active.Keys) expiredBuffer.Add(type);
            foreach (var type in expiredBuffer) Expire(type);
        }

        float DurationFor(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Magnet:      return magnetDuration;
                case PowerUpType.DoubleCoins: return doubleCoinsDuration;
                case PowerUpType.Shield:      return shieldDuration;
                case PowerUpType.SlowMotion:  return slowMotionDuration;
                default:                      return 0f;
            }
        }

        /// <summary>Builds a short HUD string like "SHIELD 5s  MAGNET 2s". Reuses one StringBuilder.</summary>
        public string GetActiveSummary()
        {
            if (active.Count == 0) return string.Empty;
            summaryBuilder.Length = 0;
            foreach (var kvp in active)
            {
                if (summaryBuilder.Length > 0) summaryBuilder.Append("   ");
                summaryBuilder.Append(NameFor(kvp.Key)).Append(' ').Append(Mathf.CeilToInt(kvp.Value)).Append('s');
            }
            return summaryBuilder.ToString();
        }

        public float Remaining(PowerUpType type) => active.TryGetValue(type, out float seconds) ? seconds : 0;

        public static string NameFor(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Magnet:      return "MAGNET";
                case PowerUpType.DoubleCoins: return "2X COINS";
                case PowerUpType.Shield:      return "SHIELD";
                case PowerUpType.SlowMotion:  return "SLOW-MO";
                default:                      return "LUCKY BOX";
            }
        }
    }
}
