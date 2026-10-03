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
                Vector2.zero, new Vector2(760f, 1100f)).transform;

            titleText = UIFactory.CreateText(card, "Title", "LEVEL 1 COMPLETE!", 48, ColorPalette.UiGood,
                new Vector2(0f, 430f), new Vector2(700f, 140f));

            UIFactory.CreateText(card, "ScoreLabel", "SCORE", 40, ColorPalette.UiText,
                new Vector2(0f, 290f), new Vector2(400f, 50f));
            scoreText = UIFactory.CreateText(card, "Score", "0", 96, ColorPalette.UiText,
                new Vector2(0f, 210f), new Vector2(500f, 110f));

            bonusText = UIFactory.CreateText(card, "Bonus", "STAIRS BONUS +0", 40, ColorPalette.Stairs,
                new Vector2(0f, 95f), new Vector2(680f, 50f));

            UIFactory.CreateImage(card, "CoinIcon", ColorPalette.Coin, new Vector2(-70f, 5f), Vector2.one * 44f, circle: true);
            coinsText = UIFactory.CreateText(card, "Coins", "+0", 44, ColorPalette.UiText,
                new Vector2(20f, 5f), new Vector2(200f, 60f));

            UIFactory.CreateButton(card, "NextButton", "NEXT LEVEL", new Vector2(0f, -145f), new Vector2(500f, 140f),
                ColorPalette.UiGood, () => GameManager.Instance.ContinueCampaign(), 52);
            UIFactory.CreateButton(card, "Retry", "RETRY LEVEL", new Vector2(0, -285), new Vector2(500, 90), ColorPalette.Get(GameColor.Pink), () => GameManager.Instance.StartRun(), 36);

            UIFactory.CreateButton(card, "MenuButton", "MAIN MENU", new Vector2(0f, -425f), new Vector2(500f, 100f),
                ColorPalette.Get(GameColor.Blue), () => GameManager.Instance.GoToMenu(), 44);
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            if (scoreText == null || ScoreManager.Instance == null) return;

            var result = GameManager.Instance.LastResult;
            titleText.text = $"LEVEL {result.config.level} COMPLETE!\n{result.stars} / 3 STARS";
            transform.Find("Card/NextButton/Label").GetComponent<Text>().text = result.config.level == 18 ? "BACK TO MENU" : "NEXT LEVEL";
            scoreText.text = ScoreManager.Instance.Score.ToString();
            bonusText.text = "RUN " + (ScoreManager.Instance.Score - ScoreManager.Instance.StairBonusTotal) + "  /  STAIRS +" + ScoreManager.Instance.StairBonusTotal;
            coinsText.text = "+" + CurrencyManager.RunCoins;

            Juice.PunchScale(titleText.transform, 0.35f, 0.4f);
        }
    }
}
