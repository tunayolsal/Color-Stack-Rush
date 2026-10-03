using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class HUDPanel : MonoBehaviour
    {
        Text score, wallet, stack, combo, active, next, powers, progress, tutorial;
        Image progressFill, symbol;
        float timer;
        int lastMeters = -1, tutorialStep;
        float startX;
        bool showTutorial;
        Color Ink => GameManager.Instance != null && GameManager.Instance.CurrentRun.Theme == 2 ? new Color(.96f, .97f, 1) : ColorPalette.UiText;
        public void Build()
        {
            score = UIFactory.CreateText(transform, "Score", "0", 74, ColorPalette.UiText, new Vector2(0, -100), new Vector2(450, 90), new Vector2(.5f, 1));
            wallet = UIFactory.CreateText(transform, "Wallet", "0", 38, ColorPalette.Coin, new Vector2(-120, -60), new Vector2(190, 65), new Vector2(1, 1));
            UIFactory.CreateButton(transform, "Pause", "II", new Vector2(-85, -155), new Vector2(110, 100), ColorPalette.UiAccent, () => GameManager.Instance.PauseGame(), 38, anchor: new Vector2(1, 1));
            symbol = UIFactory.CreateImage(transform, "ColorSymbol", ColorPalette.UiText, new Vector2(95, -95), new Vector2(75, 75), new Vector2(0, 1));
            active = UIFactory.CreateText(transform, "Collect", "COLLECT", 25, ColorPalette.UiText, new Vector2(130, -165), new Vector2(260, 60), new Vector2(0, 1));
            next = UIFactory.CreateText(transform, "NextColor", "", 32, ColorPalette.UiText, new Vector2(0, -335), new Vector2(850, 75), new Vector2(.5f, 1));
            combo = UIFactory.CreateText(transform, "Combo", "", 40, ColorPalette.UiAccent, new Vector2(0, -190), new Vector2(520, 60), new Vector2(.5f, 1));
            var bar = UIFactory.CreateImage(transform, "ProgressTrack", new Color(0, 0, 0, .1f), new Vector2(0, -245), new Vector2(700, 14), new Vector2(.5f, 1));
            progressFill = UIFactory.CreateImage(bar.transform, "Fill", ColorPalette.UiAccent, Vector2.zero, Vector2.zero);
            progressFill.rectTransform.anchorMin = Vector2.zero;
            progressFill.rectTransform.anchorMax = Vector2.one;
            progressFill.rectTransform.offsetMin = progressFill.rectTransform.offsetMax = Vector2.zero;
            progress = UIFactory.CreateText(transform, "Progress", "", 26, ColorPalette.UiText, new Vector2(0, -285), new Vector2(700, 45), new Vector2(.5f, 1));
            stack = UIFactory.CreateText(transform, "Stack", "", 44, ColorPalette.UiText, new Vector2(180, 95), new Vector2(330, 70), new Vector2(0, 0));
            powers = UIFactory.CreateText(transform, "PowerUps", "", 30, ColorPalette.UiText, new Vector2(0, 200), new Vector2(850, 65), new Vector2(.5f, 0));
            tutorial = UIFactory.CreateText(transform, "Tutorial", "", 34, ColorPalette.UiText, new Vector2(0, 325), new Vector2(950, 100), new Vector2(.5f, 0));
            Sync();
        }
        void OnEnable()
        {
            GameEvents.ScoreChanged += Score; GameEvents.CoinsChanged += Wallet; GameEvents.StackChanged += Stack; GameEvents.ComboChanged += Combo;
            GameEvents.ActiveColorChanged += ColorChanged; GameEvents.RunStarted += BeginTutorial; GameEvents.BlockCollected += TutorialBlock; GameEvents.RunCompleted += EndTutorial;
            Sync();
        }
        void OnDisable()
        {
            GameEvents.ScoreChanged -= Score; GameEvents.CoinsChanged -= Wallet; GameEvents.StackChanged -= Stack; GameEvents.ComboChanged -= Combo;
            GameEvents.ActiveColorChanged -= ColorChanged; GameEvents.RunStarted -= BeginTutorial; GameEvents.BlockCollected -= TutorialBlock; GameEvents.RunCompleted -= EndTutorial;
        }
        void Sync()
        {
            if (score == null) return;
            if (ScoreManager.Instance != null) { Score(ScoreManager.Instance.Score); Combo(ScoreManager.Instance.Combo); }
            Wallet(SaveManager.Data.coins);
            if (PlayerController.Instance != null) Stack(PlayerController.Instance.GetComponent<PlayerStack>().Count);
            if (ColorManager.Instance != null) ColorChanged(ColorManager.Instance.ActiveColor);
            BeginTutorial();
            score.color = active.color = progress.color = tutorial.color = powers.color = Ink;
            RefreshProgress(); RefreshTimers();
        }
        void Score(int value) { if (score != null) score.text = value.ToString(); }
        void Wallet(int value) { if (wallet != null) wallet.text = value.ToString(); }
        void Stack(int value) { if (stack != null) { stack.text = "STACK " + value + " / 32"; stack.color = value <= 2 ? ColorPalette.UiBad : Ink; } }
        void Combo(int value) { if (combo != null) combo.text = value >= 2 ? "COMBO x" + ScoreManager.Instance.Multiplier : ""; }
        void ColorChanged(GameColor color) { if (active == null) return; active.text = "COLLECT\n" + ColorSymbols.Name(color); symbol.sprite = ColorSymbols.SpriteFor(color); symbol.type = Image.Type.Simple; symbol.color = ColorPalette.Get(color); }
        void BeginTutorial()
        {
            if (tutorial == null || GameManager.Instance == null) return;
            var c = GameManager.Instance.CurrentRun;
            showTutorial = c.mode == RunMode.Campaign && c.level == 1 && !SaveManager.Data.tutorialCompleted;
            tutorialStep = 0; startX = 0; lastMeters = -1;
            tutorial.text = showTutorial ? "DRAG LEFT & RIGHT TO STEER" : "";
        }
        void TutorialBlock(bool correct, Vector3 pos)
        {
            if (!showTutorial || !correct || tutorialStep < 1) return;
            tutorialStep = 2;
            tutorial.text = "OTHER COLORS COST A BLOCK\nSTEER CLEAR OF THEM";
        }
        void EndTutorial(RunResult result)
        {
            if (!showTutorial || !result.completed) return;
            SaveManager.Data.tutorialCompleted = true; SaveManager.Save(); tutorial.text = ""; showTutorial = false;
        }
        void Update()
        {
            var player = PlayerController.Instance;
            if (player == null || score == null) return;
            if (showTutorial && tutorialStep == 0 && Mathf.Abs(player.transform.position.x - startX) > .3f) { tutorialStep = 1; tutorial.text = "COLLECT THE MATCHING COLOR & SYMBOL"; }
            RefreshProgress();
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0) { timer = .25f; RefreshTimers(); }
        }
        void RefreshProgress()
        {
            int meters = (int)PlayerController.Instance.Distance;
            if (meters != lastMeters)
            {
                lastMeters = meters;
                var c = GameManager.Instance.CurrentRun;
                progress.text = c.mode == RunMode.Endless ? meters + " m  /  ENDLESS" : "LEVEL " + c.level + " / 18  -  " + Mathf.Clamp(Mathf.RoundToInt(meters / c.Length * 100), 0, 100) + "%";
                progressFill.rectTransform.anchorMax = new Vector2(c.mode == RunMode.Endless ? 1 : Mathf.Clamp01(meters / c.Length), 1);
            }
        }
        void RefreshTimers()
        {
            powers.text = PowerUpManager.Instance.GetActiveSummary();
            var color = ColorManager.Instance;
            next.text = color.HasWarning ? "NEXT: " + ColorSymbols.Name(color.NextColor) + "  " + Mathf.CeilToInt(color.WarningSeconds) + "s" : "";
            next.color = ColorPalette.Get(color.NextColor);
        }
    }
}
