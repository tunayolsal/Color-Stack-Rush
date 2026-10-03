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
        [SerializeField] Vector3 offset = new Vector3(0f, 7f, -9.5f);
        [SerializeField] float pitchAngle = 32f;
        [SerializeField] float xFollowFactor = 0.55f; // how much the camera tracks sideways

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

        void OnEnable() => GameEvents.BlockCollected += OnBlockCollected;
        void OnDisable() => GameEvents.BlockCollected -= OnBlockCollected;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
                transform.position = DesiredPosition();
        }

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, smoothTime);
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
