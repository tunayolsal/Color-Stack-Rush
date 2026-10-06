using System;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Global static event hub. All systems communicate through these events
    /// instead of holding hard references to each other (loose coupling).
    /// Subscribers must unsubscribe in OnDisable/OnDestroy.
    /// </summary>
    public static class GameEvents
    {
        // --- Game flow ---
        public static event Action<GameState> StateChanged;
        public static event Action RunStarted;             // Fired right before entering Playing (reset hooks)
        public static event Action PlayerDied;             // Stack hit zero
        public static event Action FinishReached;          // Player crossed the finish line

        public static event Action<RunConfig> RunConfigured;
        public static event Action<RunResult> RunCompleted;
        public static event Action<GameColor, float> ColorChangeWarning;
        public static void RaiseRunConfigured(RunConfig config) => RunConfigured?.Invoke(config);
        public static void RaiseRunCompleted(RunResult result) => RunCompleted?.Invoke(result);
        public static void RaiseColorChangeWarning(GameColor color, float seconds) => ColorChangeWarning?.Invoke(color, seconds);

        // --- Color / stack ---
        public static event Action<GameColor> ActiveColorChanged;
        public static event Action<int> StackChanged;                 // new stack size
        public static event Action<bool, Vector3> BlockCollected;     // correct color?, world position

        // --- Score / economy ---
        public static event Action<int> ScoreChanged;                 // new total score
        public static event Action<int> ComboChanged;                 // new combo count
        public static event Action<int, Vector3> ScorePopup;          // points gained, world position
        public static event Action<int, Vector3> CoinCollected;       // amount, world position
        public static event Action<int> CoinsChanged;                 // new total coins

        // --- Combat / power-ups ---
        public static event Action<Vector3> ObstacleHit;              // world position of hit
        public static event Action<PowerUpType, float> PowerUpStarted;// type, duration
        public static event Action<PowerUpType> PowerUpEnded;

        // --- Meta ---
        public static event Action<int> SkinSelected;                 // skin index

        // --- Raise helpers (null-safe) ---
        public static void RaiseStateChanged(GameState s)              => StateChanged?.Invoke(s);
        public static void RaiseRunStarted()                           => RunStarted?.Invoke();
        public static void RaisePlayerDied()                           => PlayerDied?.Invoke();
        public static void RaiseFinishReached()                        => FinishReached?.Invoke();
        public static void RaiseActiveColorChanged(GameColor c)        => ActiveColorChanged?.Invoke(c);
        public static void RaiseStackChanged(int size)                 => StackChanged?.Invoke(size);
        public static void RaiseBlockCollected(bool ok, Vector3 pos)   => BlockCollected?.Invoke(ok, pos);
        public static void RaiseScoreChanged(int score)                => ScoreChanged?.Invoke(score);
        public static void RaiseComboChanged(int combo)                => ComboChanged?.Invoke(combo);
        public static void RaiseScorePopup(int pts, Vector3 pos)       => ScorePopup?.Invoke(pts, pos);
        public static void RaiseCoinCollected(int amt, Vector3 pos)    => CoinCollected?.Invoke(amt, pos);
        public static void RaiseCoinsChanged(int total)                => CoinsChanged?.Invoke(total);
        public static void RaiseObstacleHit(Vector3 pos)               => ObstacleHit?.Invoke(pos);
        public static void RaisePowerUpStarted(PowerUpType t, float d) => PowerUpStarted?.Invoke(t, d);
        public static void RaisePowerUpEnded(PowerUpType t)            => PowerUpEnded?.Invoke(t);
        public static void RaiseSkinSelected(int index)                => SkinSelected?.Invoke(index);
    }
}
