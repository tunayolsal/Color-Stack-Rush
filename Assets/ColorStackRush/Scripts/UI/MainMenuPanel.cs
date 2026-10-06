using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class MainMenuPanel : MonoBehaviour
    {
        Text levelText, coinsText, bestText;
        GameObject dailyDot;
        Button play;
        public void Build()
        {
            UIFactory.CreatePanel(transform, "Background", ColorPalette.UiBackground);
            for (int i = 0; i < 4; i++)
            {
                var block = UIFactory.CreateImage(transform, "ToyBlock" + i, ColorPalette.Get((GameColor)i), new Vector2((i - 1.5f) * 165, 660), new Vector2(140, 140));
                block.transform.localRotation = Quaternion.Euler(0, 0, i % 2 == 0 ? 8 : -8);
                var shadow = block.gameObject.AddComponent<Shadow>(); shadow.effectDistance = new Vector2(0, -12); shadow.effectColor = new Color(.1f, .2f, .3f, .12f);
            }
            UIFactory.CreateText(transform, "TitleTop", "COLOR", 110, ColorPalette.UiAccent, new Vector2(0, 435), new Vector2(900, 135));
            UIFactory.CreateText(transform, "TitleBottom", "STACK RUSH", 82, ColorPalette.UiText, new Vector2(0, 325), new Vector2(900, 115));
            levelText = UIFactory.CreateText(transform, "Level", "Bölüm 1", 42, ColorPalette.UiText, new Vector2(0, 195), new Vector2(900, 65));
            bestText = UIFactory.CreateText(transform, "Best", "", 28, ColorPalette.UiText, new Vector2(0, 130), new Vector2(900, 55));
            UIFactory.CreateImage(transform, "CoinIcon", ColorPalette.Coin, new Vector2(-200, -70), Vector2.one * 40, new Vector2(1, 1), circle: true);
            coinsText = UIFactory.CreateText(transform, "Coins", "0", 36, ColorPalette.UiText, new Vector2(-110, -70), new Vector2(140, 60), new Vector2(1, 1));
            play = UIFactory.CreateButton(transform, "PlayButton", "Oyna", new Vector2(0, -30), new Vector2(720, 150), ColorPalette.UiAccent,
                () => GameManager.Instance.StartRun(RunConfig.Level(SaveManager.Data.highestUnlockedLevel)), 60);
            UIFactory.CreateButton(transform, "LevelsButton", "Bölümler", new Vector2(0, -215), new Vector2(720, 115), ColorPalette.Get(GameColor.Blue), () => UIManager.Instance.OpenLevels(), 44);
            UIFactory.CreateButton(transform, "ShopButton", "Mağaza", new Vector2(-250, -390), new Vector2(230, 110), ColorPalette.Get(GameColor.Blue), () => UIManager.Instance.OpenShop(), 34);
            var daily = UIFactory.CreateButton(transform, "DailyButton", "Günlük ödül", new Vector2(0, -390), new Vector2(230, 110), ColorPalette.Get(GameColor.Green), () => UIManager.Instance.OpenDaily(), 29);
            UIFactory.CreateButton(transform, "SettingsButton", "Ayarlar", new Vector2(250, -390), new Vector2(230, 110), ColorPalette.Get(GameColor.Yellow), () => UIManager.Instance.OpenSettings(), 34);
            dailyDot = UIFactory.CreateImage(daily.transform, "Dot", ColorPalette.UiBad, new Vector2(97, 42), Vector2.one * 25, circle: true).gameObject;
            UIFactory.CreateText(transform, "Hint", "Sürükle, aynı rengi topla ve bölümü tamamla.", 28, ColorPalette.UiText, new Vector2(0, -545), new Vector2(900, 85), style: FontStyle.Normal);
            Refresh();
        }
        void OnEnable() { GameEvents.CoinsChanged += Coins; Refresh(); }
        void OnDisable() => GameEvents.CoinsChanged -= Coins;
        void Coins(int value) { if (coinsText != null) coinsText.text = value.ToString(); }
        void Update() { if (play != null) play.interactable = !SaveManager.IsSaving && !SaveManager.IsTransactionPending; }
        public void Refresh()
        {
            if (levelText == null) return;
            long level = SaveManager.Data.highestUnlockedLevel;
            levelText.text = "Bölüm " + level;
            int best = Progression.BestScore(SaveManager.Data, level);
            bestText.text = best > 0 ? "Bu bölümde en iyi: " + best : "Bir sonraki renk maceran hazır";
            Coins(SaveManager.Data.coins);
            if (DailyRewardManager.Instance != null) dailyDot.SetActive(DailyRewardManager.Instance.CanClaim);
        }
    }
}
