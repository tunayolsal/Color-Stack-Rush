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
        public bool ResultSaveFailed { get; private set; }
        public bool ResultSavePending => resultSaveInFlight || (pendingResultAvailable && (State == GameState.Finish || State == GameState.Dying));
        bool resultSaveInFlight, pendingResultAvailable;
        RunResult pendingResult;
        bool slowMotionActive;
        void Awake() { Instance = this; CurrentRun = RunConfig.Level(SaveManager.Data.highestUnlockedLevel); }
        void OnEnable() { GameEvents.PlayerDied += OnPlayerDied; GameEvents.FinishReached += OnFinishReached; }
        void OnDisable() { GameEvents.PlayerDied -= OnPlayerDied; GameEvents.FinishReached -= OnFinishReached; CancelSequences(); if (Instance == this) Instance = null; Time.timeScale = 1; Time.fixedDeltaTime = .02f; }
        public void StartRun() => StartRun(CurrentRun);
        public void StartRun(RunConfig config)
        {
            config = RunConfig.Level(config.levelId);
            if (!Progression.IsUnlocked(SaveManager.Data, config.levelId) || !SaveManager.TryBeginTransaction()) return;
            try
            {
                CancelSequences();
                RunId++;
                ResultCommitted = false;
                ResultSaveFailed = false;
                resultSaveInFlight = pendingResultAvailable = false;
                slowMotionActive = false;
                CurrentRun = config;
                SetState(GameState.MainMenu);
                GameEvents.RaiseRunConfigured(config);
                CurrencyManager.ResetRunCoins();
                GameEvents.RaiseRunStarted();
                SetState(GameState.Playing);
            }
            finally { SaveManager.EndTransaction(); }
        }
        void CancelSequences()
        {
            StopAllCoroutines();
            if (PlayerController.Instance != null) Juice.ForgetTransform(PlayerController.Instance.transform);
        }
        public void PauseGame() { if (State == GameState.Playing) SetState(GameState.Paused); }
        public void ResumeGame() { if (State == GameState.Paused) SetState(GameState.Playing); }
        public void GoToMenu() { CancelSequences(); slowMotionActive = false; SaveManager.Save(); SetState(GameState.MainMenu); }
        public void ContinueLevel()
        {
            if (!ResultCommitted || !LastResult.completed) return;
            long next = LastResult.config.levelId < long.MaxValue ? LastResult.config.levelId + 1 : long.MaxValue;
            StartRun(RunConfig.Level(next));
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
        }
        void OnFinishReached()
        {
            if (State != GameState.Playing) return;
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
        }
        void CommitResult(bool completed, int stairs)
        {
            if (ResultCommitted || pendingResultAvailable || resultSaveInFlight) return;
            var score = ScoreManager.Instance;
            score.CommitRunResults();
            pendingResult = new RunResult { config = CurrentRun, score = score.Score, stairBonus = score.StairBonusTotal, coins = CurrencyManager.RunCoins,
                distance = completed ? CurrentRun.Length : PlayerController.Instance.Distance, completed = completed, stairs = stairs, stars = completed ? RunResult.StarsFor(stairs) : 0 };
            pendingResultAvailable = true;
            StartCoroutine(PersistResultWhenReady(RunId));
        }
        public void RetryResultSave()
        {
            if (!ResultSaveFailed || !pendingResultAvailable || resultSaveInFlight) return;
            ResultSaveFailed = false;
            SetState(pendingResult.completed ? GameState.Finish : GameState.Dying);
            StartCoroutine(PersistResultWhenReady(RunId));
        }
        IEnumerator PersistResultWhenReady(int id)
        {
            while (true)
            {
                if (id != RunId || (State != GameState.Finish && State != GameState.Dying)) yield break;
                if (SaveManager.TryBeginTransaction()) break;
                yield return null;
            }
            resultSaveInFlight = true;
            var result = pendingResult;
            var data = SaveManager.Data;
            var snapshot = new Progression.Snapshot(data, result.config.levelId);
            Progression.Apply(data, result);
            SaveManager.SaveAsync(success =>
            {
                if (!success) snapshot.Restore(data);
                SaveManager.EndTransaction();
                if (this == null || Instance != this || !isActiveAndEnabled || id != RunId) return;
                resultSaveInFlight = false;
                if (State != GameState.Finish && State != GameState.Dying) return;
                if (!success)
                {
                    ResultSaveFailed = true;
                    SetState(GameState.GameOver);
                    return;
                }
                ResultCommitted = true;
                ResultSaveFailed = false;
                pendingResultAvailable = false;
                LastResult = result;
                GameEvents.RaiseRunCompleted(result);
                SetState(result.completed ? GameState.Victory : GameState.GameOver);
            });
        }
    }
}
