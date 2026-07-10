using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush
{
    /// <summary>
    /// Settings overlay: music/SFX volume sliders, mute toggles, haptics
    /// toggle and a progress-reset button. Everything persists via SaveManager.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        Slider musicSlider;
        Slider sfxSlider;
        Text musicToggleLabel;
        Text sfxToggleLabel;
        Text hapticsToggleLabel;

        public void Build()
        {
            var root = transform;
            UIFactory.CreatePanel(root, "Dim", ColorPalette.UiDim);

            var card = UIFactory.CreateImage(root, "Card", ColorPalette.UiCard,
                Vector2.zero, new Vector2(800f, 1100f)).transform;

            UIFactory.CreateText(card, "Title", "SETTINGS", 68, ColorPalette.UiText,
                new Vector2(0f, 440f), new Vector2(600f, 90f));

            // --- Music ---
            UIFactory.CreateText(card, "MusicLabel", "MUSIC VOLUME", 38, ColorPalette.UiText,
                new Vector2(0f, 330f), new Vector2(500f, 50f));
            musicSlider = UIFactory.CreateSlider(card, "MusicSlider", new Vector2(0f, 260f), new Vector2(600f, 60f),
                SaveManager.Data.musicVolume, v => AudioManager.Instance.SetMusicVolume(v));

            var musicToggle = UIFactory.CreateButton(card, "MusicToggle", "", new Vector2(0f, 165f), new Vector2(320f, 90f),
                ColorPalette.Get(GameColor.Blue), ToggleMusic, 36);
            musicToggleLabel = musicToggle.GetComponentInChildren<Text>();

            // --- SFX ---
            UIFactory.CreateText(card, "SfxLabel", "SFX VOLUME", 38, ColorPalette.UiText,
                new Vector2(0f, 60f), new Vector2(500f, 50f));
            sfxSlider = UIFactory.CreateSlider(card, "SfxSlider", new Vector2(0f, -10f), new Vector2(600f, 60f),
                SaveManager.Data.sfxVolume, v => AudioManager.Instance.SetSfxVolume(v));

            var sfxToggle = UIFactory.CreateButton(card, "SfxToggle", "", new Vector2(0f, -105f), new Vector2(320f, 90f),
                ColorPalette.Get(GameColor.Blue), ToggleSfx, 36);
            sfxToggleLabel = sfxToggle.GetComponentInChildren<Text>();

            // --- Haptics ---
            var hapticsToggle = UIFactory.CreateButton(card, "HapticsToggle", "", new Vector2(0f, -240f), new Vector2(420f, 90f),
                ColorPalette.Get(GameColor.Green), ToggleHaptics, 36);
            hapticsToggleLabel = hapticsToggle.GetComponentInChildren<Text>();

            // --- Danger zone ---
            UIFactory.CreateButton(card, "ResetButton", "RESET PROGRESS", new Vector2(0f, -370f), new Vector2(420f, 80f),
                ColorPalette.UiBad, ResetProgress, 30);

            UIFactory.CreateButton(card, "CloseButton", "CLOSE", new Vector2(0f, -490f), new Vector2(420f, 100f),
                ColorPalette.UiAccent, () => UIManager.Instance.CloseOverlays(), 40);
        }

        /// <summary>Syncs the widgets with saved values when the overlay opens.</summary>
        public void Refresh()
        {
            if (musicSlider == null) return;
            var d = SaveManager.Data;
            musicSlider.SetValueWithoutNotify(d.musicVolume);
            sfxSlider.SetValueWithoutNotify(d.sfxVolume);
            UpdateToggleLabels();
        }

        void UpdateToggleLabels()
        {
            var d = SaveManager.Data;
            musicToggleLabel.text = d.musicOn ? "MUSIC: ON" : "MUSIC: OFF";
            sfxToggleLabel.text = d.sfxOn ? "SFX: ON" : "SFX: OFF";
            hapticsToggleLabel.text = d.hapticsOn ? "HAPTICS: ON" : "HAPTICS: OFF";
        }

        void ToggleMusic()
        {
            AudioManager.Instance.SetMusicOn(!SaveManager.Data.musicOn);
            UpdateToggleLabels();
        }

        void ToggleSfx()
        {
            AudioManager.Instance.SetSfxOn(!SaveManager.Data.sfxOn);
            UpdateToggleLabels();
        }

        void ToggleHaptics()
        {
            SaveManager.Data.hapticsOn = !SaveManager.Data.hapticsOn;
            SaveManager.Save();
            UpdateToggleLabels();
        }

        void ResetProgress()
        {
            SaveManager.DeleteAll();
            AudioManager.Instance.ApplySettings();
            Refresh();
        }
    }
}
