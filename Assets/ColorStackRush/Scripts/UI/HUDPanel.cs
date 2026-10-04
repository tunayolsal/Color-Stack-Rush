using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class HUDPanel : MonoBehaviour
    {
        Text score, wallet, stack, combo, active, next, progress, tutorial;
        Image progressFill, colorFill, nextFill;
        readonly float[] powerSeconds = new float[4];
        readonly Text[] powerLabels = new Text[4];
        readonly GameObject[] powerCards = new GameObject[4];
        float timer;
        int lastMeters = -1, tutorialStep;
        bool showTutorial;
        GameObject tutorialCard;
        public void Build()
        {
            UIFactory.CreateImage(transform, "Header", new Color(1, 1, 1, .96f), new Vector2(0, -14), new Vector2(1015, 218), new Vector2(.5f, 1));
            colorFill = UIFactory.CreateImage(transform, "ActiveColor", ColorPalette.Get(GameColor.Pink), new Vector2(112, -55), new Vector2(82, 82), new Vector2(0, 1));
            active = UIFactory.CreateText(transform, "Collect", "PEMBE", 22, ColorPalette.UiText, new Vector2(112, -148), new Vector2(180, 38), new Vector2(0, 1));
            score = UIFactory.CreateText(transform, "Score", "0", 54, ColorPalette.UiText, new Vector2(0, -70), new Vector2(440, 72), new Vector2(.5f, 1));
            UIFactory.CreateText(transform, "ScoreCaption", "PUAN", 20, ColorPalette.UiText, new Vector2(0, -28), new Vector2(220, 32), new Vector2(.5f, 1));
            combo = UIFactory.CreateText(transform, "Combo", "", 26, ColorPalette.UiAccent, new Vector2(0, -145), new Vector2(360, 35), new Vector2(.5f, 1));
            wallet = UIFactory.CreateText(transform, "Wallet", "0", 27, ColorPalette.UiText, new Vector2(-200, -60), new Vector2(145, 45), new Vector2(1, 1));
            UIFactory.CreateImage(transform, "Coin", ColorPalette.Coin, new Vector2(-290, -60), Vector2.one * 25, new Vector2(1, 1), circle: true);
            UIFactory.CreateButton(transform, "Pause", "II", new Vector2(-95, -99), new Vector2(100, 115), ColorPalette.Get(GameColor.Blue), () => GameManager.Instance.PauseGame(), 35, anchor: new Vector2(1, 1));
            var bar = UIFactory.CreateImage(transform, "ProgressTrack", new Color(.77f, .85f, .91f), new Vector2(0, -190), new Vector2(945, 10), new Vector2(.5f, 1));
            progressFill = UIFactory.CreateImage(bar.transform, "Fill", ColorPalette.Get(GameColor.Green), Vector2.zero, Vector2.zero);
            progressFill.rectTransform.anchorMin = Vector2.zero; progressFill.rectTransform.anchorMax = Vector2.one;
            progressFill.rectTransform.offsetMin = progressFill.rectTransform.offsetMax = Vector2.zero;
            progress = UIFactory.CreateText(transform, "Progress", "", 27, ColorPalette.UiText, new Vector2(0, -245), new Vector2(850, 45), new Vector2(.5f, 1));
            nextFill = UIFactory.CreateImage(transform, "NextColor", ColorPalette.Get(GameColor.Blue), new Vector2(0, -310), new Vector2(365, 68), new Vector2(.5f, 1));
            next = UIFactory.CreateText(nextFill.transform, "Label", "", 27, ColorPalette.UiText, Vector2.zero, new Vector2(345, 60));
            UIFactory.CreateImage(transform, "StackCard", new Color(1, 1, 1, .94f), new Vector2(190, 90), new Vector2(315, 90), new Vector2(0, 0));
            stack = UIFactory.CreateText(transform, "Stack", "", 31, ColorPalette.UiText, new Vector2(190, 90), new Vector2(300, 75), new Vector2(0, 0));
            tutorialCard = UIFactory.CreateImage(transform, "TutorialCard", new Color(1, 1, 1, .94f), new Vector2(0, -400), new Vector2(950, 105), new Vector2(.5f, 1)).gameObject;
            tutorial = UIFactory.CreateText(transform, "Tutorial", "", 29, ColorPalette.UiText, new Vector2(0, -405), new Vector2(920, 95), new Vector2(.5f, 1));
            for (int i = 0; i < 4; i++)
            {
                var card = UIFactory.CreateImage(transform, "Power" + i, Color.white, new Vector2(-195, 85 + i * 76), new Vector2(315, 65), new Vector2(1, 0));
                powerCards[i] = card.gameObject;
                powerLabels[i] = UIFactory.CreateText(card.transform, "Label", "", 24, ColorPalette.UiText, Vector2.zero, new Vector2(300, 58));
                card.gameObject.SetActive(false);
            }
            Sync();
        }
        void OnEnable()
        {
            GameEvents.ScoreChanged += Score; GameEvents.CoinsChanged += Wallet; GameEvents.StackChanged += Stack; GameEvents.ComboChanged += Combo;
            GameEvents.ActiveColorChanged += ColorChanged; GameEvents.RunStarted += BeginTutorial; GameEvents.BlockCollected += TutorialBlock; GameEvents.RunCompleted += EndTutorial;
            GameEvents.PowerUpStarted += PowerStarted; GameEvents.PowerUpEnded += PowerEnded; Sync();
        }
        void OnDisable()
        {
            GameEvents.ScoreChanged -= Score; GameEvents.CoinsChanged -= Wallet; GameEvents.StackChanged -= Stack; GameEvents.ComboChanged -= Combo;
            GameEvents.ActiveColorChanged -= ColorChanged; GameEvents.RunStarted -= BeginTutorial; GameEvents.BlockCollected -= TutorialBlock; GameEvents.RunCompleted -= EndTutorial;
            GameEvents.PowerUpStarted -= PowerStarted; GameEvents.PowerUpEnded -= PowerEnded;
        }
        void Sync()
        {
            if (score == null) return;
            if (ScoreManager.Instance != null) { Score(ScoreManager.Instance.Score); Combo(ScoreManager.Instance.Combo); }
            Wallet(SaveManager.Data.coins);
            if (PlayerController.Instance != null) Stack(PlayerController.Instance.GetComponent<PlayerStack>().Count);
            if (ColorManager.Instance != null) ColorChanged(ColorManager.Instance.ActiveColor);
            BeginTutorial(); RefreshProgress(); RefreshTimers();
        }
        void Score(int value) { if (score != null) score.text = value.ToString(); }
        void Wallet(int value) { if (wallet != null) wallet.text = value.ToString(); }
        void Stack(int value) { if (stack != null) { stack.text = "BLOK " + value + " / 32"; stack.color = value <= 2 ? ColorPalette.UiBad : ColorPalette.UiText; } }
        void Combo(int value) { if (combo != null) combo.text = value >= 2 ? "KOMBO ×" + ScoreManager.Instance.Multiplier : ""; }
        void ColorChanged(GameColor color) { if (active == null) return; active.text = UiLabels.ColorName(color); colorFill.color = ColorPalette.Get(color); }
        void BeginTutorial()
        {
            if (tutorial == null || GameManager.Instance == null) return;
            showTutorial = GameManager.Instance.CurrentRun.levelId == 1 && !SaveManager.Data.tutorialCompleted;
            tutorialCard.SetActive(showTutorial);
            tutorialStep = 0; lastMeters = -1;
            tutorial.text = showTutorial ? "Sağa ve sola sürükle" : "";
        }
        void TutorialBlock(bool correct, Vector3 position)
        {
            if (!showTutorial || !correct || tutorialStep < 1) return;
            tutorialStep = 2; tutorial.text = "Diğer renklerden uzak dur\nHer yanlış renk iki blok götürür";
        }
        void EndTutorial(RunResult result)
        {
            if (!showTutorial || !result.completed) return;
            SaveManager.Data.tutorialCompleted = true; SaveManager.Save(); tutorial.text = ""; showTutorial = false; tutorialCard.SetActive(false);
        }
        void PowerStarted(PowerUpType type, float duration) { if ((int)type < 4) powerSeconds[(int)type] = duration; }
        void PowerEnded(PowerUpType type) { if ((int)type < 4) powerSeconds[(int)type] = 0; }
        void Update()
        {
            var player = PlayerController.Instance;
            if (player == null || score == null) return;
            if (showTutorial && tutorialStep == 0 && Mathf.Abs(player.transform.position.x) > .3f) { tutorialStep = 1; tutorial.text = "Oyuncuyla aynı renkteki blokları topla"; }
            if (GameManager.Instance.State == GameState.Playing) for (int i = 0; i < 4; i++) powerSeconds[i] = Mathf.Max(0, powerSeconds[i] - Time.deltaTime);
            RefreshProgress(); timer -= Time.unscaledDeltaTime;
            if (timer <= 0) { timer = .2f; RefreshTimers(); }
        }
        void RefreshProgress()
        {
            if (PlayerController.Instance == null || GameManager.Instance == null) return;
            int meters = (int)PlayerController.Instance.Distance;
            if (meters == lastMeters) return;
            lastMeters = meters;
            var config = GameManager.Instance.CurrentRun;
            progress.text = "Bölüm " + config.levelId + "  ·  " + Mathf.Clamp(Mathf.RoundToInt(meters / config.Length * 100), 0, 100) + "%";
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(meters / config.Length), 1);
        }
        void RefreshTimers()
        {
            var color = ColorManager.Instance;
            if (color != null)
            {
                nextFill.gameObject.SetActive(color.HasWarning);
                next.text = "Sıradaki: " + UiLabels.ColorName(color.NextColor) + "  " + Mathf.CeilToInt(color.WarningSeconds) + " sn";
                nextFill.color = Color.Lerp(ColorPalette.Get(color.NextColor), Color.white, .3f);
            }
            for (int i = 0; i < 4; i++)
            {
                powerSeconds[i] = PowerUpManager.Instance != null ? PowerUpManager.Instance.Remaining((PowerUpType)i) : 0;
                bool activePower = powerSeconds[i] > 0;
                powerCards[i].SetActive(activePower);
                if (activePower) powerLabels[i].text = UiLabels.PowerName((PowerUpType)i) + " " + Mathf.CeilToInt(powerSeconds[i]) + " sn";
            }
        }
    }
}
