using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Tiny tween helper used for all "game feel" animations (scale punch,
    /// moves, fly-offs) without any external tween library.
    /// Uses unscaled time so UI feedback still works while paused.
    /// </summary>
    public static class Juice
    {
        // Hidden runner that hosts the coroutines.
        class Runner : MonoBehaviour { }
        static Runner runner;
        static readonly Dictionary<Transform, Coroutine> moves = new Dictionary<Transform, Coroutine>();

        static Runner R
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("[JuiceRunner]");
                    go.hideFlags = HideFlags.HideInHierarchy;
                    runner = go.AddComponent<Runner>();
                }
                return runner;
            }
        }

        // Remembers the "rest" scale per transform so repeated punches never drift.
        static readonly Dictionary<Transform, Vector3> baseScales = new Dictionary<Transform, Vector3>();
        static readonly Dictionary<Transform, Coroutine> activePunches = new Dictionary<Transform, Coroutine>();

        /// <summary>Pops the transform's scale up then springs back (classic scale punch).</summary>
        public static void PunchScale(Transform t, float amount = 0.25f, float duration = 0.25f)
        {
            if (t == null) return;
            if (!baseScales.TryGetValue(t, out var baseScale))
            {
                baseScale = t.localScale;
                baseScales[t] = baseScale;
            }
            if (activePunches.TryGetValue(t, out var running) && running != null)
                R.StopCoroutine(running);

            activePunches[t] = R.StartCoroutine(PunchRoutine(t, baseScale, amount, duration));
        }

        static IEnumerator PunchRoutine(Transform t, Vector3 baseScale, float amount, float duration)
        {
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.unscaledDeltaTime;
                float n = Mathf.Clamp01(time / duration);
                // Fast out, springy back in.
                float curve = Mathf.Sin(n * Mathf.PI) * (1f - n * 0.35f);
                t.localScale = baseScale * (1f + amount * curve);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        /// <summary>Smoothly moves a transform to a world position, then invokes a callback.</summary>
        public static void MoveTo(Transform t, Vector3 target, float duration, Action onDone = null)
        {
            if (moves.TryGetValue(t, out var old) && old != null) R.StopCoroutine(old);
            moves[t] = R.StartCoroutine(MoveRoutine(t, target, duration, onDone));
        }

        static IEnumerator MoveRoutine(Transform t, Vector3 target, float duration, Action onDone)
        {
            if (t == null) yield break;
            Vector3 start = t.position;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.unscaledDeltaTime;
                float n = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));
                t.position = Vector3.Lerp(start, target, n);
                yield return null;
            }
            if (t != null) t.position = target;
            onDone?.Invoke();
        }

        /// <summary>
        /// Throws a transform up and away with spin, shrinking to nothing,
        /// then invokes the callback (used for stack blocks flying off).
        /// </summary>
        public static void FlyOff(Transform t, Vector3 direction, Action onDone = null)
        {
            ForgetTransform(t);
            moves[t] = R.StartCoroutine(FlyOffRoutine(t, direction, onDone));
        }

        static IEnumerator FlyOffRoutine(Transform t, Vector3 dir, Action onDone)
        {
            if (t == null) yield break;
            Vector3 startScale = t.localScale;
            Vector3 velocity = dir.normalized * 6f + Vector3.up * 7f;
            float time = 0f;
            const float duration = 0.55f;
            while (time < duration && t != null)
            {
                float dt = Time.unscaledDeltaTime;
                time += dt;
                velocity += Vector3.down * 22f * dt;               // fake gravity
                t.position += velocity * dt;
                t.Rotate(320f * dt, 250f * dt, 0f);
                t.localScale = startScale * (1f - time / duration); // shrink out
                yield return null;
            }
            if (t != null) t.localScale = startScale;
            onDone?.Invoke();
        }

        /// <summary>Runs any coroutine on the hidden runner (for non-MonoBehaviour callers).</summary>
        public static Coroutine Run(IEnumerator routine) => R.StartCoroutine(routine);

        /// <summary>Clears cached base scales (call when objects are destroyed in bulk).</summary>
        public static void ForgetTransform(Transform t)
        {
            if (activePunches.TryGetValue(t, out var animation) && animation != null && runner != null) runner.StopCoroutine(animation);
            if (moves.TryGetValue(t, out var move) && move != null && runner != null) runner.StopCoroutine(move);
            moves.Remove(t);
            baseScales.Remove(t);
            activePunches.Remove(t);
        }
    }
}
