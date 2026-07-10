using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush
{
    /// <summary>
    /// In-game overlay: score, wallet, combo, stack health, the "collect this
    /// color" indicator, active power-up timers and the pause button.
    /// </summary>
    public class HUDPanel : MonoBehaviour
    {
        Text scoreText;
        Text coinsText;
        Text comboText;
        Text stackText;
        Text powerUpText;
        Image colorIndicator;

        float powerUpRefreshTimer;

        public void Build()
        {
            var root = transform;

            // Score, top-center.
            scoreText = UIFactory.CreateText(root, "Score", "0", 84, ColorPalette.UiText,
                new Vector2(0f, -110f), new Vector2(500f, 100f), new Vector2(0.5f, 1f));

            // Wallet, top-right.
            UIFactory.CreateImage(root, "CoinIcon", ColorPalette.Coin, new Vector2(-170f, -70f), Vector2.one * 40f, new Vector2(1f, 1f), circle: true);
            coinsText = UIFactory.CreateText(root, "Coins", "0", 42, ColorPalette.UiText,
                new Vector2(-95f, -70f), new Vector2(140f, 60f), new Vector2(1f, 1f));

            // Pause button under the wallet.
            UIFactory.CreateButton(root, "PauseButton", "II", new Vector2(-90f, -190f), new Vector2(110f, 110f),
                WithAlpha(ColorPalette.UiText, 0.65f), () => GameManager.Instance.PauseGame(), 44, null, new Vector2(1f, 1f));

            // "Collect this color" indicator, top-left.
            colorIndicator = UIFactory.CreateImage(root, "ColorIndicator", Color.white,
                new Vector2(110f, -110f), Vector2.one * 110f, new Vector2(0f, 1f));
            UIFactory.CreateText(root, "CollectLabel", "COLLECT", 28, ColorPalette.UiText,
                new Vector2(110f, -195f), new Vector2(200f, 40f), new Vector2(0f, 1f));

            // Combo banner under the score (hidden until combo >= 2).
            comboText = UIFactory.CreateText(root, "Combo", "", 56, ColorPalette.UiAccent,
                new Vector2(0f, -210f), new Vector2(500f, 70f), new Vector2(0.5f, 1f));

            // Stack health, bottom-left.
            stackText = UIFactory.CreateText(root, "Stack", "STACK 0", 46, ColorPalette.UiText,
                new Vector2(150f, 90f), new Vector2(280f, 60f), new Vector2(0f, 0f));

            // Active power-up timers, bottom-center.
            powerUpText = UIFactory.CreateText(root, "PowerUps", "", 34, ColorPalette.UiText,
                new Vector2(0f, 180f), new Vector2(800f, 50f), new Vector2(0.5f, 0f));

            SyncAll(); // elements exist now: show real values immediately
        }

        void OnEnable()
        {
            GameEvents.ScoreChanged += OnScoreChanged;
            GameEvents.CoinsChanged += OnCoinsChanged;
            GameEvents.ComboChanged += OnComboChanged;
            GameEvents.StackChanged += OnStackChanged;
            GameEvents.ActiveColorChanged += OnActiveColorChanged;
            SyncAll();
        }

        /// <summary>Syncs every readout so re-opening the HUD never shows stale values.</summary>
        void SyncAll()
        {
            if (scoreText == null) return; // Build() hasn't run yet (first OnEnable)
            if (ScoreManager.Instance != null) OnScoreChanged(ScoreManager.Instance.Score);
            OnCoinsChanged(SaveManager.Data.coins);
            if (ColorManager.Instance != null) OnActiveColorChanged(ColorManager.Instance.ActiveColor);
        }

        void OnDisable()
        {
            GameEvents.ScoreChanged -= OnScoreChanged;
            GameEvents.CoinsChanged -= OnCoinsChanged;
            GameEvents.ComboChanged -= OnComboChanged;
            GameEvents.StackChanged -= OnStackChanged;
            GameEvents.ActiveColorChanged -= OnActiveColorChanged;
        }

        void Update()
        {
            // Power-up timers change every second; refresh at 4 Hz to avoid
            // building strings every frame.
            powerUpRefreshTimer -= Time.unscaledDeltaTime;
            if (powerUpRefreshTimer <= 0f)
            {
                powerUpRefreshTimer = 0.25f;
                if (powerUpText != null && PowerUpManager.Instance != null)
                    powerUpText.text = PowerUpManager.Instance.GetActiveSummary();
            }
        }

        void OnScoreChanged(int score)
        {
            scoreText.text = score.ToString();
        }

        void OnCoinsChanged(int total)
        {
            coinsText.text = total.ToString();
            Juice.PunchScale(coinsText.transform, 0.2f, 0.15f);
        }

        void OnComboChanged(int combo)
        {
            if (combo >= 2)
            {
                comboText.text = $"COMBO x{ScoreManager.Instance.Multiplier}  ({combo})";
                Juice.PunchScale(comboText.transform, 0.3f, 0.2f);
            }
            else
            {
                comboText.text = "";
            }
        }

        void OnStackChanged(int size)
        {
            stackText.text = "STACK " + size;
            // Low stack = danger: turn the readout red and pop it.
            stackText.color = size <= 2 ? ColorPalette.UiBad : ColorPalette.UiText;
            Juice.PunchScale(stackText.transform, 0.25f, 0.18f);
        }

        void OnActiveColorChanged(GameColor color)
        {
            colorIndicator.color = ColorPalette.Get(color);
            Juice.PunchScale(colorIndicator.transform, 0.5f, 0.35f);
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
