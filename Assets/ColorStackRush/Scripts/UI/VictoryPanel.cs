using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class VictoryPanel : MonoBehaviour
    {
        Text titleText, scoreText, bonusText, coinsText, starsText;
        public void Build()
        {
            UIFactory.CreatePanel(transform, "Dim", ColorPalette.UiDim);
            var card = UIFactory.CreateImage(transform, "Card", ColorPalette.UiCard, Vector2.zero, new Vector2(850, 1110)).transform;
            titleText = UIFactory.CreateText(card, "Title", "Bölüm tamamlandı!", 54, ColorPalette.UiGood, new Vector2(0, 425), new Vector2(760, 115));
            starsText = UIFactory.CreateText(card, "Stars", "3 / 3 yıldız", 35, ColorPalette.UiText, new Vector2(0, 320), new Vector2(700, 60));
            scoreText = UIFactory.CreateText(card, "Score", "0", 88, ColorPalette.UiText, new Vector2(0, 210), new Vector2(700, 115));
            bonusText = UIFactory.CreateText(card, "Bonus", "", 29, ColorPalette.UiText, new Vector2(0, 105), new Vector2(760, 80), style: FontStyle.Normal);
            coinsText = UIFactory.CreateText(card, "Coins", "+0 altın", 35, ColorPalette.UiText, new Vector2(0, 10), new Vector2(650, 60));
            UIFactory.CreateButton(card, "NextButton", "Sonraki bölüm", new Vector2(0, -155), new Vector2(650, 135), ColorPalette.UiGood, () => GameManager.Instance.ContinueLevel(), 44);
            UIFactory.CreateButton(card, "Retry", "Tekrar oyna", new Vector2(0, -310), new Vector2(650, 105), ColorPalette.Get(GameColor.Pink), () => GameManager.Instance.StartRun(), 38);
            UIFactory.CreateButton(card, "MenuButton", "Ana menü", new Vector2(0, -445), new Vector2(650, 100), ColorPalette.Get(GameColor.Blue), () => GameManager.Instance.GoToMenu(), 38);
        }
        void OnEnable() => Refresh();
        void Refresh()
        {
            if (scoreText == null || ScoreManager.Instance == null) return;
            var result = GameManager.Instance.LastResult;
            titleText.text = "Bölüm " + result.config.levelId + " tamamlandı!";
            starsText.text = result.stars + " / 3 yıldız";
            scoreText.text = result.score.ToString();
            bonusText.text = "Koşu puanı " + (result.score - result.stairBonus) + "\nMerdiven bonusu +" + result.stairBonus;
            coinsText.text = "+" + result.coins + " altın";
            Juice.PunchScale(titleText.transform, .15f, .3f);
        }
    }
}
