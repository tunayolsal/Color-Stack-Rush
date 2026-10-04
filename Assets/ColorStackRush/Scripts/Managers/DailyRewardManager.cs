using System;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// 7-day cycling daily reward. Claiming on consecutive days advances the
    /// streak; missing a day resets it. Dates are stored as "yyyy-MM-dd".
    /// </summary>
    public class DailyRewardManager : MonoBehaviour
    {
        public static DailyRewardManager Instance { get; private set; }

        /// <summary>Coin reward for each streak day (loops after day 7).</summary>
        public static readonly int[] Rewards = { 25, 50, 75, 100, 150, 200, 300 };

        const string DateFormat = "yyyy-MM-dd";
        public static bool CanClaimOn(string last, DateTime today)
        {
            return string.IsNullOrEmpty(last) || (DateTime.TryParseExact(last, DateFormat, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date) && today.Date > date.Date);
        }

        void Awake() => Instance = this;

        /// <summary>Index (0-6) of the reward the player will claim next.</summary>
        public int NextRewardIndex
        {
            get
            {
                // If the streak chain was broken, the next claim restarts at day 1.
                return StreakBroken ? 0 : SaveManager.Data.dailyStreak % Rewards.Length;
            }
        }

        /// <summary>True when a reward can be claimed right now (no claim yet today).</summary>
        public bool CanClaim
        {
            get
            {
                string last = SaveManager.Data.lastDailyClaim;
                if (string.IsNullOrEmpty(last)) return true;
                return CanClaimOn(last, DateTime.Now.Date);
            }
        }

        /// <summary>True when more than one day has passed since the last claim.</summary>
        bool StreakBroken
        {
            get
            {
                string last = SaveManager.Data.lastDailyClaim;
                if (string.IsNullOrEmpty(last)) return false;
                if (!DateTime.TryParseExact(last, DateFormat, null,
                        System.Globalization.DateTimeStyles.None, out var lastDate))
                    return true;
                return (DateTime.Now.Date - lastDate.Date).TotalDays > 1.0;
            }
        }

        /// <summary>Claims today's reward. Returns coins granted (0 if not claimable).</summary>
        public int Claim()
        {
            int result = 0;
            ClaimAsync(value => result = value);
            return result;
        }

        /// <summary>Publishes the reward only after the complete transaction is durable.</summary>
        public bool ClaimAsync(Action<int> completed)
        {
            if (!CanClaim || !SaveManager.TryBeginTransaction()) return false;

            int index = NextRewardIndex;
            int coins = Rewards[index];

            var data = SaveManager.Data;
            int previousStreak = data.dailyStreak;
            string previousClaim = data.lastDailyClaim;
            data.dailyStreak = StreakBroken ? 1 : data.dailyStreak + 1;
            data.lastDailyClaim = DateTime.Now.ToString(DateFormat);
            data.coins += coins;
            SaveManager.SaveAsync(ok =>
            {
                if (!ok)
                {
                    data.coins -= coins;
                    data.dailyStreak = previousStreak;
                    data.lastDailyClaim = previousClaim;
                }
                SaveManager.EndTransaction();
                GameEvents.RaiseCoinsChanged(data.coins);
                if (ok) AudioManager.Instance?.PlaySfx(SfxId.Buy);
                completed?.Invoke(ok ? coins : 0);
            });
            return true;
        }
    }
}
