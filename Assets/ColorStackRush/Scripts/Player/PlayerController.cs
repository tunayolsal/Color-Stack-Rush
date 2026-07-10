using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Moves the ball: constant auto-forward motion with gentle acceleration,
    /// plus swipe-driven horizontal steering clamped to the road.
    /// Movement is transform-based (kinematic) — cheap and deterministic on mobile.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Forward movement")]
        [SerializeField] float baseSpeed = 9f;
        [SerializeField] float acceleration = 0.08f;  // speed gained per second
        [SerializeField] float maxSpeed = 15f;

        [Header("Steering")]
        [SerializeField] float laneHalfWidth = 2.6f;  // how far left/right the ball may go
        [SerializeField] float steerSmoothing = 14f;  // how quickly the ball chases the input

        [Header("Visuals")]
        [SerializeField] float ballRadius = 0.5f;

        Transform ballVisual;   // spinning sphere child
        float currentSpeed;
        float targetX;

        /// <summary>Total distance travelled this run (world units).</summary>
        public float Distance => transform.position.z;
        public float CurrentSpeed => currentSpeed;
        public float LaneHalfWidth => laneHalfWidth;

        void Awake()
        {
            Instance = this;
            ballVisual = transform.Find("Ball");
            currentSpeed = baseSpeed;
        }

        void OnEnable() => GameEvents.RunStarted += ResetPlayer;
        void OnDisable() => GameEvents.RunStarted -= ResetPlayer;

        void Update()
        {
            // Only drive the ball during active play (the finish sequence moves it via tweens).
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            float dt = Time.deltaTime;

            // Forward: slowly accelerating auto-run.
            currentSpeed = Mathf.Min(maxSpeed, currentSpeed + acceleration * dt);

            // Steering: input moves an invisible target, ball smoothly chases it.
            if (SwipeInput.Instance != null)
                targetX = Mathf.Clamp(targetX + SwipeInput.Instance.ConsumeHorizontalDelta(), -laneHalfWidth, laneHalfWidth);

            Vector3 pos = transform.position;
            pos.z += currentSpeed * dt;
            pos.x = Mathf.Lerp(pos.x, targetX, steerSmoothing * dt);
            transform.position = pos;

            // Rolling animation: spin proportional to travel speed.
            if (ballVisual != null)
                ballVisual.Rotate(currentSpeed * dt / ballRadius * Mathf.Rad2Deg, 0f, 0f, Space.World);
        }

        /// <summary>Resets position and speed for a fresh run.</summary>
        void ResetPlayer()
        {
            transform.position = Vector3.zero;
            targetX = 0f;
            currentSpeed = baseSpeed;
        }
    }
}
