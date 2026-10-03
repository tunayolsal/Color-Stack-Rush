using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush
{
    /// <summary>
    /// Title screen: play, shop, daily reward and settings entry points,
    /// plus best score / level / wallet display.
    /// </summary>
    public class MainMenuPanel : MonoBehaviour
    {
        Text bestText;
        Text levelText;
        Text coinsText;
        GameObject dailyDot; // red notification dot when a reward is claimable

        /// <summary>Builds the whole panel hierarchy (called once by UIBuilder).</summary>
        public void Build()
        {
            var root = transform;
            UIFactory.CreatePanel(root, "Background", ColorPalette.UiBackground);

            // Soft decorative circles in the four game colors.
            UIFactory.CreateImage(root, "Deco1", WithAlpha(ColorPalette.Get(GameColor.Pink), 0.45f),   new Vector2(-380f, 700f), Vector2.one * 260f, circle: true);
            UIFactory.CreateImage(root, "Deco2", WithAlpha(ColorPalette.Get(GameColor.Blue), 0.45f),   new Vector2(420f, 460f),  Vector2.one * 180f, circle: true);
            UIFactory.CreateImage(root, "Deco3", WithAlpha(ColorPalette.Get(GameColor.Yellow), 0.45f), new Vector2(-350f, -650f), Vector2.one * 200f, circle: true);
            UIFactory.CreateImage(root, "Deco4", WithAlpha(ColorPalette.Get(GameColor.Green), 0.45f),  new Vector2(390f, -520f), Vector2.one * 150f, circle: true);

            // Title.
            UIFactory.CreateText(root, "TitleTop", "COLOR", 120, ColorPalette.UiAccent, new Vector2(0f, 540f), new Vector2(900f, 140f));
            UIFactory.CreateText(root, "TitleBottom", "STACK RUSH", 96, ColorPalette.UiText, new Vector2(0f, 430f), new Vector2(900f, 120f));

            // Stats.
            bestText = UIFactory.CreateText(root, "Best", "BEST 0", 52, ColorPalette.UiText, new Vector2(0f, 260f), new Vector2(600f, 70f));
            levelText = UIFactory.CreateText(root, "Level", "LEVEL 1", 44, WithAlpha(ColorPalette.UiText, 0.7f), new Vector2(0f, 190f), new Vector2(600f, 60f));

            // Wallet, top-right corner.
            UIFactory.CreateImage(root, "CoinIcon", ColorPalette.Coin, new Vector2(-190f, -70f), Vector2.one * 44f, new Vector2(1f, 1f), circle: true);
            coinsText = UIFactory.CreateText(root, "Coins", "0", 46, ColorPalette.UiText, new Vector2(-100f, -70f), new Vector2(160f, 60f), new Vector2(1f, 1f));

            // Main actions.
            UIFactory.CreateButton(root, "PlayButton", "PLAY", new Vector2(0f, -60f), new Vector2(460f, 150f),
                ColorPalette.UiAccent, () => GameManager.Instance.StartRun(RunConfig.Campaign(SaveManager.Data.level)), 64);
            UIFactory.CreateButton(root, "LevelsButton", "LEVELS", new Vector2(-240f, -235f), new Vector2(430f, 115f), ColorPalette.Get(GameColor.Blue), () => UIManager.Instance.OpenLevels(), 42);
            UIFactory.CreateButton(root, "EndlessButton", "ENDLESS", new Vector2(240f, -235f), new Vector2(430f, 115f), ColorPalette.Get(GameColor.Pink), () => GameManager.Instance.StartRun(RunConfig.Endless(System.Environment.TickCount)), 42);

            UIFactory.CreateButton(root, "ShopButton", "SHOP", new Vector2(-300f, -405f), new Vector2(270f, 110f),
                ColorPalette.Get(GameColor.Blue), () => UIManager.Instance.OpenShop(), 40);

            var dailyBtn = UIFactory.CreateButton(root, "DailyButton", "DAILY", new Vector2(0f, -405f), new Vector2(270f, 110f),
                ColorPalette.Get(GameColor.Green), () => UIManager.Instance.OpenDaily(), 40);

            UIFactory.CreateButton(root, "SettingsButton", "SETTINGS", new Vector2(300f, -405f), new Vector2(270f, 110f),
                ColorPalette.Get(GameColor.Yellow), () => UIManager.Instance.OpenSettings(), 36);

            // Notification dot on the daily button.
            dailyDot = UIFactory.CreateImage(dailyBtn.transform, "Dot", ColorPalette.UiBad,
                new Vector2(125f, 45f), Vector2.one * 36f, circle: true).gameObject;

            UIFactory.CreateText(root, "Hint", "SWIPE LEFT & RIGHT TO STEER", 30,
                WithAlpha(ColorPalette.UiText, 0.5f), new Vector2(0f, -520f), new Vector2(800f, 50f));
            Refresh();
        }

        void OnEnable()
        {
            GameEvents.CoinsChanged += OnCoinsChanged;
            Refresh();
        }

        void OnDisable() => GameEvents.CoinsChanged -= OnCoinsChanged;

        void OnCoinsChanged(int total)
        {
            if (coinsText != null) coinsText.text = total.ToString();
        }

        /// <summary>Updates the stats each time the menu becomes visible.</summary>
        public void Refresh()
        {
            if (bestText == null) return; // not built yet
            bestText.text = "ENDLESS BEST " + SaveManager.Data.endlessBest;
            levelText.text = SaveManager.Data.campaignCompleted ? "ALL 18 LEVELS COMPLETE" : "LEVEL " + SaveManager.Data.level + " / 18";
            coinsText.text = SaveManager.Data.coins.ToString();
            if (dailyDot != null && DailyRewardManager.Instance != null)
                dailyDot.SetActive(DailyRewardManager.Instance.CanClaim);
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
