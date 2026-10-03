using System.Collections;
using UnityEngine;
namespace ColorStackRush
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        [SerializeField] float slowMotionScale = .65f;
        [SerializeField] float deathSlowMoScale = .3f;
        [SerializeField] float stairStepDuration = .24f;
        public GameState State { get; private set; } = GameState.MainMenu;
        public RunConfig CurrentRun { get; private set; }
        public RunResult LastResult { get; private set; }
        public int RunId { get; private set; }
        public bool ResultCommitted { get; private set; }
        bool slowMotionActive;
        void Awake() { Instance = this; CurrentRun = RunConfig.Campaign(SaveManager.Data.level); }
        void OnEnable() { GameEvents.PlayerDied += OnPlayerDied; GameEvents.FinishReached += OnFinishReached; }
        void OnDisable() { GameEvents.PlayerDied -= OnPlayerDied; GameEvents.FinishReached -= OnFinishReached; CancelSequences(); if (Instance == this) Instance = null; Time.timeScale = 1; Time.fixedDeltaTime = .02f; }
        public void StartRun() => StartRun(CurrentRun);
        public void StartRun(RunConfig config)
        {
            if (config.mode == RunMode.Campaign)
            {
                config = RunConfig.Campaign(config.level);
                if (config.level > SaveManager.Data.level) return;
            }
            CancelSequences();
            RunId++;
            ResultCommitted = false;
            slowMotionActive = false;
            CurrentRun = config;
            SetState(GameState.MainMenu);
            GameEvents.RaiseRunConfigured(config);
            CurrencyManager.ResetRunCoins();
            GameEvents.RaiseRunStarted();
            SetState(GameState.Playing);
        }
        void CancelSequences()
        {
            StopAllCoroutines();
            if (PlayerController.Instance != null) Juice.ForgetTransform(PlayerController.Instance.transform);
        }
        public void PauseGame() { if (State == GameState.Playing) SetState(GameState.Paused); }
        public void ResumeGame() { if (State == GameState.Paused) SetState(GameState.Playing); }
        public void GoToMenu() { CancelSequences(); slowMotionActive = false; SaveManager.Save(); SetState(GameState.MainMenu); }
        public void ContinueCampaign()
        {
            if (LastResult.config.level >= 18) { GoToMenu(); return; }
            StartRun(RunConfig.Campaign(LastResult.config.level + 1));
        }
        public void SetSlowMotion(bool active) { slowMotionActive = active; ApplyTimeScale(); }
        void SetState(GameState state) { State = state; ApplyTimeScale(); GameEvents.RaiseStateChanged(state); }
        void ApplyTimeScale()
        {
            Time.timeScale = State == GameState.Paused ? 0 : State == GameState.Dying ? deathSlowMoScale : State == GameState.Playing && slowMotionActive ? slowMotionScale : 1;
            Time.fixedDeltaTime = .02f * (Time.timeScale > 0 ? Time.timeScale : 1);
        }
        void OnApplicationPause(bool paused) { if (paused) { PauseGame(); SaveManager.Save(); } }
        void OnApplicationFocus(bool focused) { if (!focused) { PauseGame(); SaveManager.Save(); } }
        void OnApplicationQuit() => SaveManager.Save();
        void OnPlayerDied()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Dying);
            StartCoroutine(DeathSequence(RunId));
        }
        IEnumerator DeathSequence(int id)
        {
            yield return new WaitForSecondsRealtime(.7f);
            if (id != RunId || State != GameState.Dying) yield break;
            CommitResult(false, 0);
            SetState(GameState.GameOver);
        }
        void OnFinishReached()
        {
            if (State != GameState.Playing || CurrentRun.mode == RunMode.Endless) return;
            SetState(GameState.Finish);
            StartCoroutine(FinishSequence(RunId));
        }
        IEnumerator FinishSequence(int id)
        {
            var player = PlayerController.Instance;
            var stack = player.GetComponent<PlayerStack>();
            var steps = SpawnManager.Instance.FinishSteps;
            int climbed = 0;
            for (int i = 0; i < steps.Count && id == RunId; i++)
            {
                if (!stack.ConsumeTop()) break;
                bool arrived = false;
                Juice.MoveTo(player.transform, steps[i], stairStepDuration, () => arrived = true);
                while (!arrived && id == RunId) yield return null;
                if (id != RunId) yield break;
                climbed++;
                ScoreManager.Instance.AddStairBonus(SpawnManager.StepValue(i), steps[i] + Vector3.up);
                AudioManager.Instance?.PlaySfx(SfxId.Stair, 1 + i * .06f);
                Juice.PunchScale(player.transform, .3f, .15f);
                CameraFollow.Instance?.Punch(.25f);
                HapticsManager.Light();
            }
            ParticleFactory.Confetti(player.transform.position);
            yield return new WaitForSecondsRealtime(1.1f);
            if (id != RunId || State != GameState.Finish) yield break;
            CommitResult(true, climbed);
            SetState(GameState.Victory);
        }
        void CommitResult(bool completed, int stairs)
        {
            if (ResultCommitted) return;
            ResultCommitted = true;
            var score = ScoreManager.Instance;
            score.CommitRunResults();
            LastResult = new RunResult { config = CurrentRun, score = score.Score, stairBonus = score.StairBonusTotal, coins = CurrencyManager.RunCoins,
                distance = completed ? CurrentRun.Length : PlayerController.Instance.Distance, completed = completed, stairs = stairs, stars = completed ? RunResult.StarsFor(stairs) : 0 };
            Progression.Apply(SaveManager.Data, LastResult);
            SaveManager.Save();
            GameEvents.RaiseRunCompleted(LastResult);
        }
    }
}
