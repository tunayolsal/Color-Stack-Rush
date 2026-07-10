using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush
{
    /// <summary>Level-complete screen: score breakdown and next-level button.</summary>
    public class VictoryPanel : MonoBehaviour
    {
        Text titleText;
        Text scoreText;
        Text bonusText;
        Text coinsText;

        public void Build()
        {
            var root = transform;
            UIFactory.CreatePanel(root, "Dim", ColorPalette.UiDim);

            var card = UIFactory.CreateImage(root, "Card", ColorPalette.UiCard,
                Vector2.zero, new Vector2(760f, 950f)).transform;

            titleText = UIFactory.CreateText(card, "Title", "LEVEL 1 COMPLETE!", 60, ColorPalette.UiGood,
                new Vector2(0f, 350f), new Vector2(700f, 100f));

            UIFactory.CreateText(card, "ScoreLabel", "SCORE", 40, ColorPalette.UiText,
                new Vector2(0f, 230f), new Vector2(400f, 50f));
            scoreText = UIFactory.CreateText(card, "Score", "0", 96, ColorPalette.UiText,
                new Vector2(0f, 140f), new Vector2(500f, 110f));

            bonusText = UIFactory.CreateText(card, "Bonus", "STAIRS BONUS +0", 40, ColorPalette.Stairs,
                new Vector2(0f, 30f), new Vector2(600f, 50f));

            UIFactory.CreateImage(card, "CoinIcon", ColorPalette.Coin, new Vector2(-70f, -70f), Vector2.one * 44f, circle: true);
            coinsText = UIFactory.CreateText(card, "Coins", "+0", 44, ColorPalette.UiText,
                new Vector2(20f, -70f), new Vector2(200f, 60f));

            UIFactory.CreateButton(card, "NextButton", "NEXT LEVEL", new Vector2(0f, -230f), new Vector2(500f, 140f),
                ColorPalette.UiGood, () => GameManager.Instance.StartRun(), 52);

            UIFactory.CreateButton(card, "MenuButton", "MAIN MENU", new Vector2(0f, -390f), new Vector2(500f, 120f),
                ColorPalette.Get(GameColor.Blue), () => GameManager.Instance.GoToMenu(), 44);
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            if (scoreText == null || ScoreManager.Instance == null) return;

            // SaveData.level was already incremented, so the completed level is level - 1.
            titleText.text = $"LEVEL {Mathf.Max(1, SaveManager.Data.level - 1)} COMPLETE!";
            scoreText.text = ScoreManager.Instance.Score.ToString();
            bonusText.text = "STAIRS BONUS +" + ScoreManager.Instance.StairBonusTotal;
            coinsText.text = "+" + CurrencyManager.RunCoins;

            Juice.PunchScale(titleText.transform, 0.35f, 0.4f);
        }
    }
}
