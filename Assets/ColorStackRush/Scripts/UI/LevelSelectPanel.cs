using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class LevelSelectPanel : MonoBehaviour
    {
        readonly Button[] buttons = new Button[18];
        readonly Text[] labels = new Text[18];
        public void Build()
        {
            UIFactory.CreatePanel(transform, "Dim", ColorPalette.UiDim);
            var card = UIFactory.CreateImage(transform, "Card", ColorPalette.UiCard, Vector2.zero, new Vector2(880, 1370)).transform;
            UIFactory.CreateText(card, "Title", "CHOOSE YOUR RUN", 58, ColorPalette.UiText, new Vector2(0, 575), new Vector2(800, 85));
            for (int i = 0; i < 18; i++)
            {
                int level = i + 1;
                int row = i / 3;
                buttons[i] = UIFactory.CreateButton(card, "Level" + level, "", new Vector2((i % 3 - 1) * 265, 360 - row * 132), new Vector2(235, 112),
                    ColorPalette.Get((GameColor)((i / 6) % 4)), () => GameManager.Instance.StartRun(RunConfig.Campaign(level)), 34, ColorPalette.UiText);
                labels[i] = buttons[i].GetComponentInChildren<Text>();
            }
            UIFactory.CreateText(card, "Themes", "1-6 SKY GARDEN  /  7-12 SUNSET  /  13-18 MIDNIGHT", 25, ColorPalette.UiText, new Vector2(0, 475), new Vector2(810, 50));
            UIFactory.CreateText(card, "Stars", "FINISH / 8 STAIRS / 14 STAIRS", 29, ColorPalette.UiText, new Vector2(0, -420), new Vector2(810, 50));
            UIFactory.CreateButton(card, "Close", "BACK", new Vector2(0, -555), new Vector2(440, 105), ColorPalette.UiAccent, () => UIManager.Instance.CloseOverlays(), 42);
        }
        public void Refresh()
        {
            for (int i = 0; i < 18; i++)
            {
                bool open = i + 1 <= SaveManager.Data.level;
                buttons[i].interactable = open;
                labels[i].text = open ? "LEVEL " + (i + 1) + "\n" + SaveManager.Data.levelStars[i] + " / 3 STARS" : "LEVEL " + (i + 1) + "\nLOCKED";
            }
        }
    }
}
