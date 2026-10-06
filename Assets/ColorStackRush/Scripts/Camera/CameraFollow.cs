using System.Collections;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Smooth chase camera rig. Follows the player's Z fully and X partially,
    /// with a vertical "bounce" punch on collects for extra juice.
    /// Lives on the rig root; the actual Camera sits on a child (CameraShake).
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        [Header("Framing")]
        [SerializeField] Vector3 offset = new Vector3(0f, 10f, -10.5f);
        [SerializeField] float pitchAngle = 38f;
        [SerializeField] float xFollowFactor = 0.35f; // how much the camera tracks sideways

        [Header("Smoothing")]
        [SerializeField] float smoothTime = 0.18f;

        Transform target;
        Vector3 velocity;
        float bounceOffset; // additive Y from Punch()

        void Awake()
        {
            Instance = this;
            transform.rotation = Quaternion.Euler(pitchAngle, 0f, 0f);
        }

        void OnEnable()
        {
            GameEvents.BlockCollected += OnBlockCollected;
            // The rig is built after the player, so this callback sees the
            // already-reset Rigidbody/transform on RunStarted.
            GameEvents.RunStarted += ResetForRun;
        }
        void OnDisable()
        {
            GameEvents.BlockCollected -= OnBlockCollected;
            GameEvents.RunStarted -= ResetForRun;
            StopAllCoroutines();
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            ResetForRun();
        }

        /// <summary>Discard the previous stairs, follow momentum and feedback before a new run.</summary>
        public void ResetForRun()
        {
            StopAllCoroutines();
            bounceOffset = 0;
            velocity = Vector3.zero;
            transform.rotation = Quaternion.Euler(pitchAngle, 0, 0);
            if (target != null) transform.position = DesiredPosition();
            GetComponentInChildren<CameraShake>()?.ResetForRun();
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = DesiredPosition();
            Vector3 position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            // Forward lag changes the player's screen position with speed.
            // Keep the planned framing while smoothing lateral motion and bounce.
            position.z = desired.z;
            velocity.z = 0;
            transform.position = position;
        }

        Vector3 DesiredPosition()
        {
            return new Vector3(
                target.position.x * xFollowFactor,
                target.position.y + offset.y + bounceOffset,
                target.position.z + offset.z);
        }

        void OnBlockCollected(bool correct, Vector3 pos)
        {
            if (correct) Punch(0.35f);
        }

        /// <summary>Quick vertical camera bounce (kick up, spring back).</summary>
        public void Punch(float strength)
        {
            if (SaveManager.Data.reducedMotion) { bounceOffset = 0; return; }
            StopAllCoroutines();
            StartCoroutine(PunchRoutine(Mathf.Min(strength, .18f)));
        }

        IEnumerator PunchRoutine(float strength)
        {
            float time = 0f;
            const float duration = 0.3f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float n = Mathf.Clamp01(time / duration);
                bounceOffset = Mathf.Sin(n * Mathf.PI) * strength;
                yield return null;
            }
            bounceOffset = 0f;
        }
    }
}
