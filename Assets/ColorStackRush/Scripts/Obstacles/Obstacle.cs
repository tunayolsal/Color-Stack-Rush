using System.Collections;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Pooled road obstacle. One class covers the three behaviours (static
    /// wall, rotating spinner, sliding cube) to keep the pool setup simple.
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        [Header("Behaviour")]
        [SerializeField] ObstacleKind kind = ObstacleKind.Wall;
        [SerializeField] float spinSpeed = 130f;      // Spinner: degrees/second
        [SerializeField] float slideAmplitude = 2.1f; // Slider: world units
        [SerializeField] float slideSpeed = 1.6f;     // Slider: cycles/second-ish

        Transform movingPart;   // bar for spinner, whole visual for slider
        MeshRenderer[] renderers;
        Material[] originalMaterials; // captured once so the flash can always be undone
        float slidePhase;
        float baseX;
        Coroutine flashRoutine;

        void Awake()
        {
            movingPart = transform.Find("Moving");
            renderers = GetComponentsInChildren<MeshRenderer>(true);
            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                originalMaterials[i] = renderers[i].sharedMaterial;
        }

        void OnEnable()
        {
            baseX = transform.position.x;
            slidePhase = Random.Range(0f, Mathf.PI * 2f); // desync sliders
        }

        /// <summary>Lets the spawner switch behaviour when reusing pooled instances.</summary>
        public void SetKind(ObstacleKind newKind) => kind = newKind;

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
                return;

            switch (kind)
            {
                case ObstacleKind.Spinner:
                    if (movingPart != null)
                        movingPart.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
                    break;

                case ObstacleKind.Slider:
                    slidePhase += slideSpeed * Time.deltaTime;
                    Vector3 pos = transform.position;
                    pos.x = baseX + Mathf.Sin(slidePhase * Mathf.PI) * slideAmplitude;
                    transform.position = pos;
                    break;
            }
        }

        /// <summary>Angry wobble + red flash when the player crashes into this obstacle.</summary>
        public void PlayHitReaction()
        {
            Juice.PunchScale(transform, 0.35f, 0.3f);
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            if (gameObject.activeInHierarchy) flashRoutine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            // Temporary shared-material swap for the flash, restored after.
            var flashMat = MaterialCache.Get(Color.white);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = flashMat;

            yield return new WaitForSeconds(0.08f);

            RestoreMaterials();
        }

        void RestoreMaterials()
        {
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = originalMaterials[i];
        }

        // Pool release can interrupt the flash coroutine; never let a recycled
        // obstacle come back white.
        void OnDisable()
        {
            if (originalMaterials != null) RestoreMaterials();
        }

        /// <summary>Shield power-up destroys the obstacle in a satisfying burst.</summary>
        public void Smash()
        {
            ParticleFactory.Burst(transform.position + Vector3.up * 0.6f, ColorPalette.Obstacle, 22, 6f);
            CameraShake.Instance?.Shake(0.25f);

            var pooled = GetComponent<PooledObject>();
            if (pooled != null) pooled.Release();
            else gameObject.SetActive(false);
        }
    }
}
