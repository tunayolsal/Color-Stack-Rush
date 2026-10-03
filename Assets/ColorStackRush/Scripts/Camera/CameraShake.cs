using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Trauma-based screen shake. Sits between the follow rig and the Camera
    /// so shake offsets never fight the follow smoothing.
    /// Subscribes to gameplay events itself, keeping callers decoupled.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("Shake feel")]
        [SerializeField] float maxOffset = 0.55f;   // world units at full trauma
        [SerializeField] float maxRollDegrees = 4f;
        [SerializeField] float decayPerSecond = 1.6f;

        float trauma; // 0..1, squared for a nicer falloff

        void Awake() => Instance = this;

        void OnEnable()
        {
            GameEvents.ObstacleHit += OnObstacleHit;
            GameEvents.PlayerDied += OnPlayerDied;
        }

        void OnDisable()
        {
            GameEvents.ObstacleHit -= OnObstacleHit;
            GameEvents.PlayerDied -= OnPlayerDied;
        }

        void OnObstacleHit(Vector3 pos) => Shake(0.55f);
        void OnPlayerDied() => Shake(0.9f);

        /// <summary>Adds shake energy (stacks with current shake, clamped).</summary>
        public void Shake(float amount) => trauma = SaveManager.Data.reducedMotion ? 0 : Mathf.Clamp(trauma + amount, 0, .65f);

        void LateUpdate()
        {
            if (SaveManager.Data.reducedMotion || trauma <= 0f)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }

            // Unscaled so death slow-motion still shakes at full energy.
            trauma = Mathf.Max(0f, trauma - decayPerSecond * Time.unscaledDeltaTime);
            float shake = trauma * trauma;

            transform.localPosition = new Vector3(
                (Mathf.PerlinNoise(Time.unscaledTime * 25f, 0f) * 2f - 1f),
                (Mathf.PerlinNoise(0f, Time.unscaledTime * 25f) * 2f - 1f),
                0f) * (maxOffset * shake);

            transform.localRotation = Quaternion.Euler(0f, 0f,
                (Mathf.PerlinNoise(Time.unscaledTime * 20f, 10f) * 2f - 1f) * maxRollDegrees * shake);
        }
    }
}
