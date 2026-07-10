using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush
{
    /// <summary>End screen after the stack empties: results + retry/menu.</summary>
    public class GameOverPanel : MonoBehaviour
    {
        Text scoreText;
        Text bestText;
        Text newBestText;
        Text coinsText;

        public void Build()
        {
            var root = transform;
            UIFactory.CreatePanel(root, "Dim", ColorPalette.UiDim);

            var card = UIFactory.CreateImage(root, "Card", ColorPalette.UiCard,
                Vector2.zero, new Vector2(760f, 950f)).transform;

            UIFactory.CreateText(card, "Title", "GAME OVER", 76, ColorPalette.UiBad,
                new Vector2(0f, 350f), new Vector2(700f, 100f));

            UIFactory.CreateText(card, "ScoreLabel", "SCORE", 40, ColorPalette.UiText,
                new Vector2(0f, 230f), new Vector2(400f, 50f));
            scoreText = UIFactory.CreateText(card, "Score", "0", 96, ColorPalette.UiText,
                new Vector2(0f, 140f), new Vector2(500f, 110f));

            newBestText = UIFactory.CreateText(card, "NewBest", "NEW BEST!", 44, ColorPalette.UiGood,
                new Vector2(0f, 55f), new Vector2(400f, 60f));
            bestText = UIFactory.CreateText(card, "Best", "BEST 0", 40, ColorPalette.UiText,
                new Vector2(0f, 0f), new Vector2(400f, 50f));

            UIFactory.CreateImage(card, "CoinIcon", ColorPalette.Coin, new Vector2(-70f, -90f), Vector2.one * 44f, circle: true);
            coinsText = UIFactory.CreateText(card, "Coins", "+0", 44, ColorPalette.UiText,
                new Vector2(20f, -90f), new Vector2(200f, 60f));

            UIFactory.CreateButton(card, "RestartButton", "TRY AGAIN", new Vector2(0f, -230f), new Vector2(500f, 140f),
                ColorPalette.UiAccent, () => GameManager.Instance.StartRun(), 52);

            UIFactory.CreateButton(card, "MenuButton", "MAIN MENU", new Vector2(0f, -390f), new Vector2(500f, 120f),
                ColorPalette.Get(GameColor.Blue), () => GameManager.Instance.GoToMenu(), 44);
        }

        void OnEnable() => Refresh();

        /// <summary>Fills in the run results whenever the panel is shown.</summary>
        void Refresh()
        {
            if (scoreText == null || ScoreManager.Instance == null) return;

            scoreText.text = ScoreManager.Instance.Score.ToString();
            bestText.text = "BEST " + SaveManager.Data.highScore;
            coinsText.text = "+" + CurrencyManager.RunCoins;

            bool newBest = ScoreManager.Instance.IsNewBest;
            newBestText.gameObject.SetActive(newBest);
            if (newBest) Juice.PunchScale(newBestText.transform, 0.5f, 0.4f);
        }
    }
}
