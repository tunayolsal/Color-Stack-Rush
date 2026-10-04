using UnityEngine;

namespace ColorStackRush
{
    /// <summary>Pause overlay: resume, restart, settings and quit-to-menu.</summary>
    public class PausePanel : MonoBehaviour
    {
        public void Build()
        {
            var root = transform;
            UIFactory.CreatePanel(root, "Dim", ColorPalette.UiDim);

            var card = UIFactory.CreateImage(root, "Card", ColorPalette.UiCard,
                Vector2.zero, new Vector2(700f, 900f)).transform;

            UIFactory.CreateText(card, "Title", "Duraklatıldı", 72, ColorPalette.UiText,
                new Vector2(0f, 330f), new Vector2(600f, 100f));

            UIFactory.CreateButton(card, "ResumeButton", "Devam et", new Vector2(0f, 140f), new Vector2(480f, 130f),
                ColorPalette.UiGood, () => GameManager.Instance.ResumeGame(), 48);

            UIFactory.CreateButton(card, "RestartButton", "Tekrar başla", new Vector2(0f, -30f), new Vector2(480f, 130f),
                ColorPalette.Get(GameColor.Blue), () => GameManager.Instance.StartRun(), 48);

            UIFactory.CreateButton(card, "SettingsButton", "Ayarlar", new Vector2(0f, -200f), new Vector2(480f, 130f),
                ColorPalette.Get(GameColor.Yellow), () => UIManager.Instance.OpenSettings(), 48);

            UIFactory.CreateButton(card, "MenuButton", "Ana menü", new Vector2(0f, -370f), new Vector2(480f, 130f),
                ColorPalette.UiAccent, () => GameManager.Instance.GoToMenu(), 48);
        }
    }
}
