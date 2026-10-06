using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class GameOverPanel : MonoBehaviour
    {
        Text title, scoreText, bestText, coinsText, retryLabel, info;
        Button retry;
        public void Build()
        {
            UIFactory.CreatePanel(transform, "Dim", ColorPalette.UiDim);
            var card = UIFactory.CreateImage(transform, "Card", ColorPalette.UiCard, Vector2.zero, new Vector2(850, 1010)).transform;
            title = UIFactory.CreateText(card, "Title", "Bir daha dene!", 60, ColorPalette.UiBad, new Vector2(0, 365), new Vector2(770, 125));
            UIFactory.CreateText(card, "ScoreLabel", "PUAN", 29, ColorPalette.UiText, new Vector2(0, 250), new Vector2(500, 45));
            scoreText = UIFactory.CreateText(card, "Score", "0", 88, ColorPalette.UiText, new Vector2(0, 160), new Vector2(700, 115));
            bestText = UIFactory.CreateText(card, "Best", "", 32, ColorPalette.UiText, new Vector2(0, 65), new Vector2(700, 65));
            coinsText = UIFactory.CreateText(card, "Coins", "+0 altın", 35, ColorPalette.UiText, new Vector2(0, -20), new Vector2(650, 60));
            info = UIFactory.CreateText(card, "Info", "", 28, ColorPalette.UiText, new Vector2(0, -105), new Vector2(730, 90), style: FontStyle.Normal);
            retry = UIFactory.CreateButton(card, "RestartButton", "Tekrar dene", new Vector2(0, -245), new Vector2(650, 130), ColorPalette.UiAccent, Retry, 44);
            retryLabel = retry.GetComponentInChildren<Text>();
            UIFactory.CreateButton(card, "MenuButton", "Ana menü", new Vector2(0, -400), new Vector2(650, 105), ColorPalette.Get(GameColor.Blue), () => GameManager.Instance.GoToMenu(), 38);
        }
        void OnEnable() => Refresh();
        void Retry() { if (GameManager.Instance.ResultSaveFailed) GameManager.Instance.RetryResultSave(); else GameManager.Instance.StartRun(); }
        void Refresh()
        {
            if (scoreText == null || ScoreManager.Instance == null) return;
            bool failed = GameManager.Instance.ResultSaveFailed;
            title.text = failed ? "Kayıt tamamlanamadı" : "Bir daha dene!";
            title.fontSize = failed ? 49 : 60;
            scoreText.text = ScoreManager.Instance.Score.ToString();
            bestText.text = "Bölüm " + GameManager.Instance.CurrentRun.levelId + " · En iyi " + Progression.BestScore(SaveManager.Data, GameManager.Instance.CurrentRun.levelId);
            coinsText.text = "+" + CurrencyManager.RunCoins + " altın";
            info.text = failed ? "İlerlemeni korumak için kaydı tekrar dene." : "Her denemede renkleri biraz daha iyi yakala.";
            retryLabel.text = failed ? "Kaydı tekrar dene" : "Tekrar dene";
            retry.interactable = !SaveManager.IsTransactionPending;
        }
    }
}
