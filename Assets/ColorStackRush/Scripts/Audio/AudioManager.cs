using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Central audio hub. Loads the bundled CC0 music and effects, applies
    /// saved volume/mute settings, and reacts
    /// to gameplay events so other systems never talk to audio directly.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mix levels")]
        [SerializeField] float musicBaseLevel = 0.25f;
        [SerializeField] float sfxBaseLevel = 0.75f;

        AudioSource musicSource;
        AudioSource sfxSource;
        readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        bool audioUnlocked;
        bool runHasStarted;

        void Awake()
        {
            Instance = this;

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            // Load once at startup; synthesis only keeps an incomplete asset
            // checkout playable, never the normal shipping audio path.
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
                clips[id] = BundledAudio.LoadEffect(id) ?? SfxSynth.Generate(id);

            musicSource.clip = BundledAudio.LoadMusic() ?? SfxSynth.GenerateMusicLoop();
            ApplySettings();
        }

        void OnEnable()
        {
            GameEvents.BlockCollected += OnBlockCollected;
            GameEvents.CoinCollected += OnCoinCollected;
            GameEvents.ObstacleHit += OnObstacleHit;
            GameEvents.PowerUpStarted += OnPowerUpStarted;
            GameEvents.ActiveColorChanged += OnColorChanged;
            GameEvents.StateChanged += OnStateChanged;
            GameEvents.RunStarted += OnRunStarted;
        }

        void OnDisable()
        {
            GameEvents.BlockCollected -= OnBlockCollected;
            GameEvents.CoinCollected -= OnCoinCollected;
            GameEvents.ObstacleHit -= OnObstacleHit;
            GameEvents.PowerUpStarted -= OnPowerUpStarted;
            GameEvents.ActiveColorChanged -= OnColorChanged;
            GameEvents.StateChanged -= OnStateChanged;
            GameEvents.RunStarted -= OnRunStarted;
        }

        // --- Event reactions ---

        void OnBlockCollected(bool correct, Vector3 pos)
        {
            if (correct)
            {
                // Pitch rises with the combo for satisfying escalation.
                int combo = ScoreManager.Instance != null ? ScoreManager.Instance.Combo : 0;
                PlaySfx(SfxId.Collect, 1f + Mathf.Min(combo, 12) * 0.045f);
            }
            else
            {
                PlaySfx(SfxId.Wrong);
            }
        }

        void OnCoinCollected(int amount, Vector3 pos) => PlaySfx(SfxId.Coin, Random.Range(0.95f, 1.1f));
        void OnObstacleHit(Vector3 pos) => PlaySfx(SfxId.Hit);
        void OnPowerUpStarted(PowerUpType t, float d) => PlaySfx(SfxId.PowerUp);
        void OnColorChanged(GameColor c) => PlaySfx(SfxId.ColorChange);

        void OnStateChanged(GameState state)
        {
            if (state == GameState.Victory) PlaySfx(SfxId.Win);
            else if (state == GameState.GameOver) PlaySfx(SfxId.Lose);
        }

        // --- Public API ---

        void OnRunStarted()
        {
            runHasStarted = true;
            UnlockAudio();
        }

        /// <summary>Called only after a play button/browser gesture; never autoplay at startup.</summary>
        public void UnlockAudio()
        {
            audioUnlocked = true;
            ApplySettings();
            if (musicSource != null && runHasStarted && SaveManager.Data.musicOn && !musicSource.isPlaying) musicSource.Play();
        }

        /// <summary>Plays a one-shot sound effect with optional pitch variation.</summary>
        public void PlaySfx(SfxId id, float pitch = 1f)
        {
            if (!audioUnlocked || !SaveManager.Data.sfxOn || sfxSource == null) return;
            if (!clips.TryGetValue(id, out var clip) || clip == null) return;
            sfxSource.pitch = Mathf.Clamp(pitch, .5f, 2.2f);
            sfxSource.PlayOneShot(clip);
        }

        public void SetMusicVolume(float value)
        {
            SaveManager.Data.musicVolume = Mathf.Clamp01(value);
            SaveManager.Save();
            ApplySettings();
        }

        public void SetSfxVolume(float value)
        {
            SaveManager.Data.sfxVolume = Mathf.Clamp01(value);
            SaveManager.Save();
            ApplySettings();
        }

        public void SetMusicOn(bool on)
        {
            SaveManager.Data.musicOn = on;
            SaveManager.Save();
            ApplySettings();
        }

        public void SetSfxOn(bool on)
        {
            SaveManager.Data.sfxOn = on;
            SaveManager.Save();
            ApplySettings();
        }

        /// <summary>Pushes saved settings into the live audio sources.</summary>
        public void ApplySettings()
        {
            var d = SaveManager.Data;
            musicSource.volume = d.musicOn ? d.musicVolume * musicBaseLevel : 0f;
            sfxSource.volume = d.sfxOn ? d.sfxVolume * sfxBaseLevel : 0f;
            if (audioUnlocked && runHasStarted && d.musicOn && !musicSource.isPlaying) musicSource.Play();
            if (!d.musicOn && musicSource.isPlaying) musicSource.Stop();
        }
    }
}
