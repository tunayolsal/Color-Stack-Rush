namespace ColorStackRush
{
    /// <summary>
    /// Coin wallet. Total coins persist in save data; RunCoins tracks how many
    /// were earned in the current run (shown on the end screens).
    /// Static: it holds no scene state and needs no inspector tuning.
    /// </summary>
    public static class CurrencyManager
    {
        /// <summary>Coins earned during the current run.</summary>
        public static int RunCoins { get; private set; }

        /// <summary>Total coins in the wallet.</summary>
        public static int TotalCoins => SaveManager.Data.coins;

        /// <summary>Call at run start (subscribed by GameBootstrapper).</summary>
        public static void ResetRunCoins() => RunCoins = 0;

        /// <summary>Adds coins to the wallet and the current-run counter.</summary>
        public static void AddCoins(int amount)
        {
            RunCoins += amount;
            SaveManager.Data.coins += amount;
            GameEvents.RaiseCoinsChanged(SaveManager.Data.coins);
        }

        /// <summary>Grants coins outside a run (daily reward, lucky box). Saves immediately.</summary>
        public static void Grant(int amount)
        {
            SaveManager.Data.coins += amount;
            SaveManager.Save();
            GameEvents.RaiseCoinsChanged(SaveManager.Data.coins);
        }

        /// <summary>Attempts a purchase; saves and returns true on success.</summary>
        public static bool TrySpend(int amount)
        {
            if (SaveManager.Data.coins < amount) return false;
            SaveManager.Data.coins -= amount;
            SaveManager.Save();
            GameEvents.RaiseCoinsChanged(SaveManager.Data.coins);
            return true;
        }
    }
}
