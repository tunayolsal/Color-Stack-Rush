using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Pooled world-space floating texts ("+30", "WRONG!", power-up names).
    /// Subscribes to gameplay events itself so no gameplay code knows about it.
    /// </summary>
    public class FloatingTextManager : MonoBehaviour
    {
        public static FloatingTextManager Instance { get; private set; }

        [Header("Animation")]
        [SerializeField] float riseDistance = 1.6f;
        [SerializeField] float lifetime = 0.8f;

        readonly Stack<TextMesh> pool = new Stack<TextMesh>(16);
        Transform container;

        void Awake()
        {
            Instance = this;
            container = new GameObject("FloatingTexts").transform;
        }

        void OnEnable()
        {
            GameEvents.ScorePopup += OnScorePopup;
            GameEvents.CoinCollected += OnCoinCollected;
            GameEvents.BlockCollected += OnBlockCollected;
        }

        void OnDisable()
        {
            GameEvents.ScorePopup -= OnScorePopup;
            GameEvents.CoinCollected -= OnCoinCollected;
            GameEvents.BlockCollected -= OnBlockCollected;
        }

        void OnScorePopup(int points, Vector3 pos) => Show("+" + points, pos + Vector3.up, Color.white, 1.1f);
        void OnCoinCollected(int amount, Vector3 pos) => Show("+" + amount, pos + Vector3.up, ColorPalette.Coin, 0.9f);

        void OnBlockCollected(bool correct, Vector3 pos)
        {
            if (!correct)
            {
                int damage = GameManager.Instance != null ? GameManager.Instance.CurrentRun.Difficulty.WrongColorDamage : 2;
                Show("-" + damage, pos + Vector3.up, ColorPalette.UiBad, 1f);
            }
        }

        /// <summary>Spawns a floating text that rises, faces the camera and fades out.</summary>
        public void Show(string text, Vector3 worldPos, Color color, float size = 1f)
        {
            TextMesh tm = pool.Count > 0 ? pool.Pop() : WorldText.Create(container, "", Vector3.zero, Color.white);
            tm.text = text;
            tm.characterSize = 0.035f * size;
            tm.transform.position = worldPos;
            tm.gameObject.SetActive(true);
            StartCoroutine(Animate(tm, color));
        }

        IEnumerator Animate(TextMesh tm, Color color)
        {
            Vector3 start = tm.transform.position;
            Transform cam = Camera.main != null ? Camera.main.transform : null;
            float time = 0f;

            while (time < lifetime)
            {
                time += Time.unscaledDeltaTime;
                float n = Mathf.Clamp01(time / lifetime);

                tm.transform.position = start + Vector3.up * (riseDistance * EaseOut(n));
                if (cam != null) tm.transform.rotation = cam.rotation; // billboard

                var c = color;
                c.a = 1f - n * n; // fade late
                tm.color = c;
                yield return null;
            }

            tm.gameObject.SetActive(false);
            pool.Push(tm);
        }

        static float EaseOut(float n) => 1f - (1f - n) * (1f - n);
    }
}
