using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush
{
    /// <summary>
    /// Skin shop: a scrollable grid of 10 skin cards showing lock/unlock
    /// state, price, and select/selected status.
    /// </summary>
    public class ShopPanel : MonoBehaviour
    {
        const int Columns = 2;
        static readonly Vector2 CardSize = new Vector2(470f, 330f);
        const float CardSpacing = 24f;

        Text coinsText, message;
        bool pending;
        RectTransform content;

        public void Build()
        {
            var root = transform;
            UIFactory.CreatePanel(root, "Background", ColorPalette.UiBackground);

            UIFactory.CreateText(root, "Title", "Mağaza", 76, ColorPalette.UiText,
                new Vector2(0f, -90f), new Vector2(400f, 100f), new Vector2(0.5f, 1f));

            UIFactory.CreateImage(root, "CoinIcon", ColorPalette.Coin, new Vector2(-190f, -80f), Vector2.one * 44f, new Vector2(1f, 1f), circle: true);
            coinsText = UIFactory.CreateText(root, "Coins", "0", 46, ColorPalette.UiText,
                new Vector2(-100f, -80f), new Vector2(160f, 60f), new Vector2(1f, 1f));

            message = UIFactory.CreateText(root, "Message", "Kozmetikler yalnız görünüşü değiştirir.", 26, ColorPalette.UiText, new Vector2(0, -190), new Vector2(950, 65), new Vector2(.5f, 1));
            BuildScrollArea(root);

            UIFactory.CreateButton(root, "CloseButton", "Geri", new Vector2(0f, 90f), new Vector2(420f, 110f),
                ColorPalette.UiAccent, () => UIManager.Instance.CloseOverlays(), 44, null, new Vector2(0.5f, 0f));
        }

        void BuildScrollArea(Transform root)
        {
            // Viewport with a mask, filling the middle of the screen.
            var viewportRt = UIFactory.CreateRect(root, "Viewport");
            viewportRt.anchorMin = new Vector2(0.5f, 0f);
            viewportRt.anchorMax = new Vector2(0.5f, 1f);
            viewportRt.pivot = new Vector2(0.5f, 0.5f);
            viewportRt.sizeDelta = new Vector2(Columns * (CardSize.x + CardSpacing) + CardSpacing, -520f);
            viewportRt.anchoredPosition = new Vector2(0f, 30f);
            viewportRt.gameObject.AddComponent<RectMask2D>();
            var viewportImg = viewportRt.gameObject.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.01f); // near-invisible, catches drag input

            // Content sized to fit all cards.
            int rows = Mathf.CeilToInt(ShopManager.SkinCount / (float)Columns);
            content = UIFactory.CreateRect(viewportRt, "Content", new Vector2(0.5f, 1f));
            content.sizeDelta = new Vector2(0f, rows * (CardSize.y + CardSpacing) + CardSpacing);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);

            var scroll = viewportRt.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewportRt;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
        }

        /// <summary>Rebuilds all cards (called when the shop opens or after a purchase).</summary>
        public void Refresh()
        {
            if (content == null) return;
            coinsText.text = SaveManager.Data.coins.ToString();
            pending = SaveManager.IsTransactionPending;

            // Rebuild is rare (menu only), so destroying children is acceptable here.
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            for (int i = 0; i < ShopManager.SkinCount; i++)
                BuildCard(i);
        }

        void Transact(int index, bool buy)
        {
            if (pending || SaveManager.IsTransactionPending) return;
            pending = true; message.text = "Kaydediliyor…";
            foreach (var button in content.GetComponentsInChildren<Button>()) button.interactable = false;
            System.Action<bool> done = success => { pending = false; if (this == null) return; Refresh(); message.text = success ? "Stilin hazır!" : "İşlem tamamlanamadı. Tekrar dene."; };
            bool accepted = buy ? ShopManager.Instance.TryBuyAsync(index, done) : ShopManager.Instance.SelectAsync(index, done);
            if (!accepted) done(false);
        }
        void BuildCard(int index)
        {
            var skin = ShopManager.GetSkin(index);
            bool unlocked = ShopManager.IsUnlocked(index);
            bool selected = ShopManager.SelectedSkin == index;

            int row = index / Columns;
            int col = index % Columns;
            var pos = new Vector2(
                (col - 0.5f) * (CardSize.x + CardSpacing),
                -(CardSpacing + CardSize.y * 0.5f) - row * (CardSize.y + CardSpacing));

            var card = UIFactory.CreateImage(content, $"Card_{skin.name}", ColorPalette.UiCard, pos, CardSize, new Vector2(0.5f, 1f));

            // Skin preview ball.
            UIFactory.CreateImage(card.transform, "Preview", skin.primary, new Vector2(0f, 65f), Vector2.one * 130f, circle: true);

            UIFactory.CreateText(card.transform, "Name", UiLabels.SkinName(index), 36, ColorPalette.UiText,
                new Vector2(0f, -30f), new Vector2(400f, 44f));

            // Action button reflects the card state.
            if (selected)
            {
                UIFactory.CreateButton(card.transform, "Action", "Seçili", new Vector2(0f, -110f), new Vector2(300f, 80f),
                    ColorPalette.UiGood, () => { }, 32).interactable = false;
            }
            else if (unlocked)
            {
                int captured = index;
                UIFactory.CreateButton(card.transform, "Action", "Seç", new Vector2(0f, -110f), new Vector2(300f, 80f),
                    ColorPalette.Get(GameColor.Blue), () => Transact(captured, false), 32).interactable = !pending;
            }
            else
            {
                int captured = index;
                bool affordable = SaveManager.Data.coins >= skin.cost;
                var buyButton = UIFactory.CreateButton(card.transform, "Action", $"Al  {skin.cost}", new Vector2(0f, -110f), new Vector2(300f, 80f),
                    affordable ? ColorPalette.UiAccent : new Color(0.75f, 0.73f, 0.78f),
                    () =>
                    {
                        Transact(captured, true);
                    }, 32);

                buyButton.interactable = affordable && !pending;
                // Little coin icon inside the buy label.
                UIFactory.CreateImage(buyButton.transform, "Coin", ColorPalette.Coin, new Vector2(115f, 0f), Vector2.one * 30f, circle: true);
            }
        }
    }
}
