using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Score + combo system. Points come from distance, collected blocks
    /// (multiplied by the combo) and the finish-stairs bonus.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Scoring")]
        [SerializeField] int pointsPerBlock = 10;
        [SerializeField] int comboPerMultiplier = 4; // every 4 combo = +1x
        [SerializeField] int maxMultiplier = 5;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int StairBonusTotal { get; private set; }
        public bool IsNewBest { get; private set; }

        /// <summary>Current score multiplier derived from the combo streak.</summary>
        public int Multiplier => Mathf.Min(maxMultiplier, 1 + Combo / comboPerMultiplier);

        int lastDistanceScored;

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameEvents.RunStarted += OnRunStarted;
            GameEvents.BlockCollected += OnBlockCollected;
            GameEvents.ObstacleHit += OnObstacleHit;
        }

        void OnDisable()
        {
            GameEvents.RunStarted -= OnRunStarted;
            GameEvents.BlockCollected -= OnBlockCollected;
            GameEvents.ObstacleHit -= OnObstacleHit;
        }

        void OnRunStarted()
        {
            Score = 0;
            StairBonusTotal = 0;
            lastDistanceScored = 0;
            IsNewBest = false;
            SetCombo(0);
            GameEvents.RaiseScoreChanged(Score);
        }

        void Update()
        {
            // +1 point per meter travelled (cheap: only fires on whole meters).
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;
            if (PlayerController.Instance == null) return;

            int meters = (int)PlayerController.Instance.Distance;
            if (meters > lastDistanceScored)
            {
                Score += meters - lastDistanceScored;
                lastDistanceScored = meters;
                GameEvents.RaiseScoreChanged(Score);
            }
        }

        void OnBlockCollected(bool correct, Vector3 pos)
        {
            if (correct)
            {
                SetCombo(Combo + 1);
                int points = pointsPerBlock * Multiplier;
                Score += points;
                GameEvents.RaiseScoreChanged(Score);
                GameEvents.RaiseScorePopup(points, pos); // floating "+N" text
            }
            else
            {
                SetCombo(0); // wrong color breaks the streak
            }
        }

        void OnObstacleHit(Vector3 pos) => SetCombo(0);

        void SetCombo(int value)
        {
            Combo = value;
            GameEvents.RaiseComboChanged(Combo);
        }

        /// <summary>Adds finish-stairs bonus points (called per step by GameManager).</summary>
        public void AddStairBonus(int points, Vector3 worldPos)
        {
            StairBonusTotal += points;
            Score += points;
            GameEvents.RaiseScoreChanged(Score);
            GameEvents.RaiseScorePopup(points, worldPos);
        }

        /// <summary>Writes the high score into save data at the end of a run.</summary>
        public void CommitRunResults()
        {
            var config = GameManager.Instance.CurrentRun;
            int best = config.mode == RunMode.Endless ? SaveManager.Data.endlessBest : SaveManager.Data.levelScores[config.level - 1];
            if (Score > best)
            {
                // The legacy record is retained separately; Progression commits the mode record.
                IsNewBest = true;
            }
        }
    }
}
