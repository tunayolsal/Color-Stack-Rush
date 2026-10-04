using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class LevelSelectPanel : MonoBehaviour
    {
        readonly Button[] buttons = new Button[12];
        readonly Text[] labels = new Text[12];
        Text pageLabel;
        Button previous, next;
        long page;
        public void Build()
        {
            UIFactory.CreatePanel(transform, "Dim", ColorPalette.UiDim);
            var card = UIFactory.CreateImage(transform, "Card", ColorPalette.UiCard, Vector2.zero, new Vector2(930, 1360)).transform;
            UIFactory.CreateText(card, "Title", "Bölümler", 60, ColorPalette.UiText, new Vector2(0, 565), new Vector2(800, 90));
            UIFactory.CreateText(card, "Hint", "Tamamla, yenisini aç. İstediğin bölümü tekrar oyna.", 29, ColorPalette.UiText, new Vector2(0, 470), new Vector2(820, 75), style: FontStyle.Normal);
            for (int i = 0; i < 12; i++)
            {
                int index = i;
                buttons[i] = UIFactory.CreateButton(card, "Level" + i, "", new Vector2((i % 3 - 1) * 285, 325 - i / 3 * 155), new Vector2(255, 132), ColorPalette.Get((GameColor)(i % 4)), () => Choose(index), 29, ColorPalette.UiText);
                labels[i] = buttons[i].GetComponentInChildren<Text>();
            }
            previous = UIFactory.CreateButton(card, "Previous", "Önceki", new Vector2(-290, -360), new Vector2(220, 90), ColorPalette.Get(GameColor.Blue), () => { if (page > 0) page--; DrawPage(); }, 32);
            next = UIFactory.CreateButton(card, "Next", "Sonraki", new Vector2(290, -360), new Vector2(220, 90), ColorPalette.Get(GameColor.Blue), () => { if (page < MaxPage) page++; DrawPage(); }, 32);
            pageLabel = UIFactory.CreateText(card, "Page", "", 28, ColorPalette.UiText, new Vector2(0, -360), new Vector2(335, 85));
            UIFactory.CreateButton(card, "Close", "Geri", new Vector2(0, -540), new Vector2(540, 105), ColorPalette.UiAccent, () => UIManager.Instance.CloseOverlays(), 42);
        }
        long MaxPage => (System.Math.Max(1L, SaveManager.Data.highestUnlockedLevel) - 1) / 12;
        public void Refresh() { page = MaxPage; DrawPage(); }
        public static bool TryLevelAt(long pageIndex, int slot, out long level)
        {
            level = 0;
            if (pageIndex < 0 || pageIndex > (long.MaxValue - 1) / 12 || slot < 0 || slot >= 12) return false;
            long first = pageIndex * 12 + 1;
            if (first > long.MaxValue - slot) return false;
            level = first + slot;
            return true;
        }
        void Choose(int index)
        {
            if (TryLevelAt(page, index, out long level) && Progression.IsUnlocked(SaveManager.Data, level)) GameManager.Instance.StartRun(RunConfig.Level(level));
        }
        void DrawPage()
        {
            if (pageLabel == null) return;
            page = System.Math.Max(0L, System.Math.Min(page, MaxPage));
            long first = page * 12 + 1;
            long last = first > long.MaxValue - 11 ? long.MaxValue : first + 11;
            pageLabel.text = first + " – " + last;
            previous.interactable = page > 0; next.interactable = page < MaxPage;
            for (int i = 0; i < 12; i++)
            {
                bool valid = TryLevelAt(page, i, out long level);
                buttons[i].gameObject.SetActive(valid);
                if (!valid) { buttons[i].interactable = false; labels[i].text = ""; continue; }
                bool open = Progression.IsUnlocked(SaveManager.Data, level);
                buttons[i].interactable = open;
                labels[i].text = "Bölüm " + level + "\n" + (open ? Progression.Stars(SaveManager.Data, level) + " / 3 yıldız" : "Kilitli");
                buttons[i].targetGraphic.color = open ? Color.Lerp(ColorPalette.Get((GameColor)(RunConfig.Level(level).Theme % 4)), Color.white, .28f) : new Color(.77f, .82f, .87f);
            }
        }
    }
}
