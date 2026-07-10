using System.Collections;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Owns the game state machine and time scale. Coordinates the death
    /// sequence and the end-of-level multiplier-stairs sequence.
    /// All other systems react to GameEvents.StateChanged / RunStarted.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Feel")]
        [SerializeField] float slowMotionScale = 0.65f;   // SlowMotion power-up
        [SerializeField] float deathSlowMoScale = 0.3f;   // brief dramatic slow-mo on death
        [SerializeField] float stairStepDuration = 0.24f; // seconds per stair climbed

        public GameState State { get; private set; } = GameState.MainMenu;

        bool slowMotionActive;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60; // mobile: lock to 60 FPS target
        }

        void OnEnable()
        {
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.FinishReached += OnFinishReached;
        }

        void OnDisable()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.FinishReached -= OnFinishReached;
        }

        // --- Public flow API (called by UI buttons) ---

        /// <summary>Starts a fresh run: everything resets via RunStarted, then play begins.</summary>
        public void StartRun()
        {
            GameEvents.RaiseRunStarted();
            SetState(GameState.Playing);
        }

        public void PauseGame()
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
        }

        public void ResumeGame()
        {
            if (State == GameState.Paused) SetState(GameState.Playing);
        }

        public void GoToMenu() => SetState(GameState.MainMenu);

        /// <summary>Called by the SlowMotion power-up.</summary>
        public void SetSlowMotion(bool active)
        {
            slowMotionActive = active;
            ApplyTimeScale();
        }

        // --- State machine ---

        void SetState(GameState newState)
        {
            State = newState;
            ApplyTimeScale();
            GameEvents.RaiseStateChanged(newState);
        }

        void ApplyTimeScale()
        {
            float scale = 1f;
            if (State == GameState.Paused) scale = 0f;
            else if (State == GameState.Playing && slowMotionActive) scale = slowMotionScale;

            Time.timeScale = scale;
            if (scale > 0f) Time.fixedDeltaTime = 0.02f * scale;
        }

        // Auto-pause when the app loses focus (phone call, home button...).
        void OnApplicationPause(bool paused)
        {
            if (paused && State == GameState.Playing) PauseGame();
        }

        // --- Death ---

        void OnPlayerDied()
        {
            if (State != GameState.Playing) return;
            StartCoroutine(DeathSequence());
        }

        /// <summary>Brief dramatic slow-motion, then the Game Over screen.</summary>
        IEnumerator DeathSequence()
        {
            Time.timeScale = deathSlowMoScale;
            yield return new WaitForSecondsRealtime(0.7f);

            CommitRunAndSave();
            SetState(GameState.GameOver);
        }

        // --- Finish / victory ---

        void OnFinishReached()
        {
            if (State != GameState.Playing) return;
            StartCoroutine(FinishSequence());
        }

        /// <summary>
        /// The level-end payoff: the ball hops up the multiplier stairs,
        /// converting one stack block per step into escalating bonus points.
        /// </summary>
        IEnumerator FinishSequence()
        {
            SetState(GameState.Finish);

            var player = PlayerController.Instance;
            var stack = player.GetComponent<PlayerStack>();
            var steps = SpawnManager.Instance.FinishSteps;

            for (int i = 0; i < steps.Count; i++)
            {
                if (!stack.ConsumeTop()) break; // out of blocks: stop climbing

                // Hop onto the next step.
                bool arrived = false;
                Juice.MoveTo(player.transform, steps[i], stairStepDuration, () => arrived = true);
                while (!arrived) yield return null;

                // Escalating reward per step: step 1 = 10, step 2 = 20 ...
                int points = SpawnManager.StepValue(i);
                ScoreManager.Instance.AddStairBonus(points, steps[i] + Vector3.up);

                AudioManager.Instance?.PlaySfx(SfxId.Stair, 1f + i * 0.06f);
                Juice.PunchScale(player.transform, 0.3f, 0.15f);
                CameraFollow.Instance?.Punch(0.25f);
                HapticsManager.Light();
            }

            // Celebration!
            ParticleFactory.Confetti(player.transform.position);
            yield return new WaitForSecondsRealtime(1.1f);

            SaveManager.Data.level++;
            CommitRunAndSave();
            SetState(GameState.Victory);
        }

        /// <summary>Persists high score + coins at the end of every run.</summary>
        void CommitRunAndSave()
        {
            ScoreManager.Instance?.CommitRunResults();
            SaveManager.Save();
        }
    }
}
