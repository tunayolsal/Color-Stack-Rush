using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Moves the kinematic ball forward at the level's pace and applies each
    /// steering delta once. Rigidbody interpolation smooths the rendered motion.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Steering")]
        [SerializeField] float laneHalfWidth = 2.6f;  // how far left/right the ball may go

        [Header("Visuals")]
        [SerializeField] float ballRadius = 0.5f;

        Rigidbody body;
        Transform ballVisual;   // spinning sphere child
        float currentSpeed;

        /// <summary>Total distance travelled this run (world units).</summary>
        public float Distance => body != null ? body.position.z : transform.position.z;
        public float CurrentSpeed => currentSpeed;
        public float LaneHalfWidth => laneHalfWidth;

        void Awake()
        {
            Instance = this;
            body = GetComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            ballVisual = transform.Find("Ball");
            currentSpeed = GameManager.Instance != null ? GameManager.Instance.CurrentRun.BaseSpeed : 10f;
        }

        void OnEnable() => GameEvents.RunStarted += ResetPlayer;
        void OnDisable() => GameEvents.RunStarted -= ResetPlayer;

        void FixedUpdate()
        {
            // Only drive the ball during active play (the finish sequence moves it via tweens).
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            float dt = Time.fixedDeltaTime;

            // Difficulty belongs to the finite level, with a bounded fixed pace.
            currentSpeed = Mathf.Min(18, GameManager.Instance.CurrentRun.BaseSpeed);

            Vector3 pos = body.position;
            pos.z += currentSpeed * dt;
            // Apply relative movement to the actual position. An old target must
            // never pull the player against a new swipe or keep steering after release.
            float delta = SwipeInput.Instance != null ? SwipeInput.Instance.ConsumeHorizontalDelta() : 0;
            pos.x = Mathf.Clamp(pos.x + delta, -laneHalfWidth, laneHalfWidth);
            body.MovePosition(pos);

            // Rolling animation: spin proportional to travel speed.
            if (ballVisual != null)
                ballVisual.Rotate(currentSpeed * dt / ballRadius * Mathf.Rad2Deg, 0f, 0f, Space.World);
        }

        /// <summary>Resets position and speed for a fresh run.</summary>
        void ResetPlayer()
        {
            Juice.ForgetTransform(transform);
            transform.localScale = Vector3.one;
            body.position = Vector3.zero;
            body.rotation = Quaternion.identity;
            transform.position = Vector3.zero;
            SwipeInput.Instance?.ResetInput();
            currentSpeed = GameManager.Instance != null ? GameManager.Instance.CurrentRun.BaseSpeed : 10f;
        }
    }
}
