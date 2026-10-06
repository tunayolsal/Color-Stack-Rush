using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush
{
    /// <summary>
    /// Daily reward overlay: a 7-day calendar with claimed/next/future states
    /// and a claim button. Streak logic lives in DailyRewardManager.
    /// </summary>
    public class DailyRewardPanel : MonoBehaviour
    {
        Transform card;
        Transform cellsRoot;
        Button claimButton;
        Text claimLabel;
        Text infoText;

        public void Build()
        {
            var root = transform;
            UIFactory.CreatePanel(root, "Dim", ColorPalette.UiDim);

            card = UIFactory.CreateImage(root, "Card", ColorPalette.UiCard,
                Vector2.zero, new Vector2(920f, 1050f)).transform;

            UIFactory.CreateText(card, "Title", "Günlük ödül", 64, ColorPalette.UiText,
                new Vector2(0f, 430f), new Vector2(700f, 90f));

            infoText = UIFactory.CreateText(card, "Info", "", 34, new Color(0.4f, 0.38f, 0.5f, 0.85f),
                new Vector2(0f, 340f), new Vector2(700f, 50f));

            cellsRoot = UIFactory.CreateRect(card, "Cells");

            claimButton = UIFactory.CreateButton(card, "ClaimButton", "Ödülü al", new Vector2(0f, -330f), new Vector2(460f, 130f),
                ColorPalette.UiGood, OnClaim, 52);
            claimLabel = claimButton.GetComponentInChildren<Text>();

            UIFactory.CreateButton(card, "CloseButton", "Kapat", new Vector2(0f, -460f), new Vector2(460f, 100f),
                ColorPalette.UiAccent, () => UIManager.Instance.CloseOverlays(), 40);
        }

        /// <summary>Rebuilds the calendar to match the current streak state.</summary>
        public void Refresh()
        {
            if (cellsRoot == null) return;

            for (int i = cellsRoot.childCount - 1; i >= 0; i--)
                Destroy(cellsRoot.GetChild(i).gameObject);

            var mgr = DailyRewardManager.Instance;
            int nextIndex = mgr.NextRewardIndex;
            bool canClaim = mgr.CanClaim;

            // 7 cells: 4 in the top row, 3 in the bottom row.
            var cellSize = new Vector2(195f, 230f);
            for (int i = 0; i < DailyRewardManager.Rewards.Length; i++)
            {
                int row = i < 4 ? 0 : 1;
                int col = i < 4 ? i : i - 4;
                int rowCount = row == 0 ? 4 : 3;
                float x = (col - (rowCount - 1) * 0.5f) * 215f;
                float y = 160f - row * 260f;

                // A day is 'done' when it comes before the next claimable index,
                // or when it's today's already-claimed reward.
                bool done = i < nextIndex || (!canClaim && i == ((nextIndex + DailyRewardManager.Rewards.Length - 1) % DailyRewardManager.Rewards.Length) && SaveManager.Data.dailyStreak > 0);
                bool isNext = canClaim && i == nextIndex;

                Color cellColor = done ? ColorPalette.UiGood
                    : isNext ? ColorPalette.Get(GameColor.Yellow)
                    : new Color(0.93f, 0.91f, 0.96f);

                var cell = UIFactory.CreateImage(cellsRoot, $"Day{i + 1}", cellColor, new Vector2(x, y), cellSize);

                UIFactory.CreateText(cell.transform, "Day", $"GÜN {i + 1}", 30,
                    done ? Color.white : ColorPalette.UiText, new Vector2(0f, 70f), new Vector2(180f, 40f));

                UIFactory.CreateImage(cell.transform, "Coin", ColorPalette.Coin, new Vector2(0f, 5f), Vector2.one * 56f, circle: true);

                UIFactory.CreateText(cell.transform, "Amount", done ? "Alındı" : DailyRewardManager.Rewards[i].ToString(), 34,
                    done ? Color.white : ColorPalette.UiText, new Vector2(0f, -65f), new Vector2(180f, 44f));

                if (isNext) Juice.PunchScale(cell.transform, 0.15f, 0.5f);
            }

            claimButton.interactable = canClaim && !SaveManager.IsTransactionPending;
            claimLabel.text = canClaim ? "Ödülü al" : "Yarın yeniden gel";
            claimLabel.fontSize = canClaim ? 52 : 34;
            infoText.text = $"{SaveManager.Data.dailyStreak} günlük seri";
        }

        void OnClaim()
        {
            if (SaveManager.IsTransactionPending) return;
            claimButton.interactable = false; claimLabel.text = "Kaydediliyor…";
            bool accepted = DailyRewardManager.Instance.ClaimAsync(coins =>
            {
            if (this == null) return;
            if (coins <= 0) { Refresh(); infoText.text = "Ödül kaydedilemedi. Tekrar dene."; return; }

            // Celebrate: confetti in the world + refreshed calendar.
            if (Camera.main != null)
                ParticleFactory.Confetti(Camera.main.transform.position + Camera.main.transform.forward * 8f, 60);
            Refresh();

            var menu = FindFirstObjectByType<MainMenuPanel>();
            if (menu != null) menu.Refresh();
            });
            if (!accepted) { Refresh(); infoText.text = "İşlem tamamlanamadı. Tekrar dene."; }
        }
    }
}
