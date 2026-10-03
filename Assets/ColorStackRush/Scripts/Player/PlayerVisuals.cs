using System.Collections;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// All cosmetic behaviour on the player: applies the selected shop skin,
    /// tints the "target color" ring under the ball, shows the shield bubble,
    /// and blinks during post-hit invincibility.
    /// </summary>
    public class PlayerVisuals : MonoBehaviour
    {
        MeshRenderer ballRenderer;
        MeshRenderer ringRenderer;
        GameObject shieldOrb;
        Transform ballTransform;
        Coroutine blinkRoutine;
        Vector3 ballRestScale, ringRestScale;
        Quaternion ballRestRotation;

        void Awake()
        {
            ballTransform = transform.Find("Ball");
            if (ballTransform != null) { ballRenderer = ballTransform.GetComponent<MeshRenderer>(); ballRestScale = ballTransform.localScale; ballRestRotation = ballTransform.localRotation; }

            var ring = transform.Find("ColorRing");
            if (ring != null) { ringRenderer = ring.GetComponent<MeshRenderer>(); ringRestScale = ring.localScale; }

            var orb = transform.Find("ShieldOrb");
            if (orb != null)
            {
                shieldOrb = orb.gameObject;
                shieldOrb.GetComponent<MeshRenderer>().sharedMaterial =
                    MaterialCache.GetTransparent(ColorPalette.GetPowerUp(PowerUpType.Shield), 0.35f);
                shieldOrb.SetActive(false);
            }
        }

        void ResetVisuals()
        {
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            if (ballRenderer != null) ballRenderer.enabled = true;
            if (ballTransform != null) { Juice.ForgetTransform(ballTransform); ballTransform.localScale = ballRestScale; ballTransform.localRotation = ballRestRotation; }
            if (ringRenderer != null) { Juice.ForgetTransform(ringRenderer.transform); ringRenderer.transform.localScale = ringRestScale; }
        }
        void Start() => ApplySkin(SaveManager.Data.selectedSkin);

        void OnEnable()
        {
            GameEvents.RunStarted += ResetVisuals;
            GameEvents.SkinSelected += ApplySkin;
            GameEvents.ActiveColorChanged += OnActiveColorChanged;
            GameEvents.ObstacleHit += OnObstacleHit;
            GameEvents.PowerUpStarted += OnPowerUpStarted;
            GameEvents.PowerUpEnded += OnPowerUpEnded;
            GameEvents.BlockCollected += OnBlockCollected;
        }

        void OnDisable()
        {
            GameEvents.RunStarted -= ResetVisuals;
            GameEvents.SkinSelected -= ApplySkin;
            GameEvents.ActiveColorChanged -= OnActiveColorChanged;
            GameEvents.ObstacleHit -= OnObstacleHit;
            GameEvents.PowerUpStarted -= OnPowerUpStarted;
            GameEvents.PowerUpEnded -= OnPowerUpEnded;
            GameEvents.BlockCollected -= OnBlockCollected;
        }

        /// <summary>Applies a shop skin's color to the ball.</summary>
        void ApplySkin(int skinIndex)
        {
            if (ballRenderer == null) return;
            var skin = ShopManager.GetSkin(skinIndex);
            ballRenderer.sharedMaterial = MaterialCache.GetEmissive(skin.primary, 0.15f);
        }

        /// <summary>The ring under the ball always shows which color to collect.</summary>
        void OnActiveColorChanged(GameColor color)
        {
            if (ringRenderer == null) return;
            ringRenderer.sharedMaterial = MaterialCache.GetEmissive(ColorPalette.Get(color), 0.8f);
            Juice.PunchScale(ringRenderer.transform, 0.6f, 0.35f);
        }

        void OnBlockCollected(bool correct, Vector3 pos)
        {
            // Squash & stretch pop on every correct collect.
            if (correct && ballTransform != null)
                Juice.PunchScale(ballTransform, 0.3f, 0.2f);
        }

        void OnObstacleHit(Vector3 pos)
        {
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        /// <summary>Classic invincibility blink after taking a hit.</summary>
        IEnumerator BlinkRoutine()
        {
            var wait = new WaitForSeconds(0.09f);
            for (int i = 0; i < 6; i++)
            {
                if (ballRenderer != null) ballRenderer.enabled = false;
                yield return wait;
                if (ballRenderer != null) ballRenderer.enabled = true;
                yield return wait;
            }
        }

        void OnPowerUpStarted(PowerUpType type, float duration)
        {
            if (type == PowerUpType.Shield && shieldOrb != null)
            {
                shieldOrb.SetActive(true);
                Juice.PunchScale(shieldOrb.transform, 0.4f, 0.3f);
            }
        }

        void OnPowerUpEnded(PowerUpType type)
        {
            if (type == PowerUpType.Shield && shieldOrb != null)
                shieldOrb.SetActive(false);
        }
    }
}
