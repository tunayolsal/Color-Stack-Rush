using UnityEngine;
namespace ColorStackRush
{
    public class ColorManager : MonoBehaviour
    {
        public static ColorManager Instance { get; private set; }
        public GameColor ActiveColor { get; private set; } = GameColor.Pink;
        public GameColor NextColor { get; private set; }
        public bool HasWarning { get; private set; }
        public float WarningSeconds { get; private set; }
        void Awake() => Instance = this;
        void OnEnable() => GameEvents.RunStarted += ResetColor;
        void OnDisable() { GameEvents.RunStarted -= ResetColor; if (Instance == this) Instance = null; }
        void ResetColor()
        {
            HasWarning = false;
            WarningSeconds = 0;
            ActiveColor = TrackPlanner.ColorAtDistance(GameManager.Instance.CurrentRun, 0);
            GameEvents.RaiseActiveColorChanged(ActiveColor);
            GameEvents.RaiseColorChangeWarning(ActiveColor, 0);
        }
        void Update()
        {
            if (GameManager.Instance.State != GameState.Playing) return;
            var config = GameManager.Instance.CurrentRun;
            var player = PlayerController.Instance;
            float z = player.Distance;
            GameColor color = TrackPlanner.ColorAtDistance(config, z);
            if (color != ActiveColor)
            {
                ActiveColor = color;
                HasWarning = false;
                GameEvents.RaiseActiveColorChanged(color);
                GameEvents.RaiseColorChangeWarning(color, 0);
                HapticsManager.Light();
            }
            if (!config.ChangesColor) return;
            float boundary = (Mathf.FloorToInt(z / TrackPlanner.ColorBand) + 1) * TrackPlanner.ColorBand;
            if (boundary >= config.Length || boundary - z > TrackPlanner.WarningDistance) return;
            WarningSeconds = (boundary - z) / Mathf.Max(.1f, player.CurrentSpeed * Time.timeScale);
            if (!HasWarning)
            {
                NextColor = TrackPlanner.ColorAtDistance(config, boundary + 1);
                HasWarning = true;
                GameEvents.RaiseColorChangeWarning(NextColor, WarningSeconds);
            }
        }
    }
}
