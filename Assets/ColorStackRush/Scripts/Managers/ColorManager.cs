using System.Collections;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Owns the "active color" the player must collect. Rotates to a new
    /// random color every 15-20 seconds while playing.
    /// </summary>
    public class ColorManager : MonoBehaviour
    {
        public static ColorManager Instance { get; private set; }

        [Header("Color cycling")]
        [SerializeField] float minInterval = 15f;
        [SerializeField] float maxInterval = 20f;

        public GameColor ActiveColor { get; private set; } = GameColor.Pink;

        Coroutine cycleRoutine;

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameEvents.RunStarted += OnRunStarted;
            GameEvents.StateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.RunStarted -= OnRunStarted;
            GameEvents.StateChanged -= OnStateChanged;
        }

        void OnRunStarted()
        {
            // Fresh random color each run, announced so UI/player ring update.
            SetActiveColor((GameColor)Random.Range(0, 4));
        }

        void OnStateChanged(GameState state)
        {
            if (state == GameState.Playing)
            {
                if (cycleRoutine == null) cycleRoutine = StartCoroutine(CycleRoutine());
            }
            else if (state != GameState.Paused) // pausing shouldn't kill the cycle
            {
                if (cycleRoutine != null)
                {
                    StopCoroutine(cycleRoutine);
                    cycleRoutine = null;
                }
            }
        }

        /// <summary>Waits 15-20 s, then switches to a different random color, forever while playing.</summary>
        IEnumerator CycleRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));

                if (GameManager.Instance.State != GameState.Playing) continue;

                // Always pick a *different* color so the change is meaningful.
                GameColor next;
                do { next = (GameColor)Random.Range(0, 4); }
                while (next == ActiveColor);

                SetActiveColor(next);
                HapticsManager.Light();
            }
        }

        void SetActiveColor(GameColor color)
        {
            ActiveColor = color;
            GameEvents.RaiseActiveColorChanged(color);
        }
    }
}
