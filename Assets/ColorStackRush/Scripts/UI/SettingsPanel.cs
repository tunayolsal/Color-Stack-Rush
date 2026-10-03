using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class SettingsPanel : MonoBehaviour
    {
        Slider musicSlider, sfxSlider;
        Text musicLabel, sfxLabel, hapticsLabel, motionLabel, qualityLabel, resetLabel;
        bool resetConfirmed;
        public void Build()
        {
            UIFactory.CreatePanel(transform, "Dim", ColorPalette.UiDim);
            var card = UIFactory.CreateImage(transform, "Card", ColorPalette.UiCard, Vector2.zero, new Vector2(840, 1440)).transform;
            UIFactory.CreateText(card, "Title", "SETTINGS", 62, ColorPalette.UiText, new Vector2(0, 610), new Vector2(700, 85));
            UIFactory.CreateText(card, "MusicVolume", "MUSIC VOLUME", 30, ColorPalette.UiText, new Vector2(0, 505), new Vector2(600, 50));
            musicSlider = UIFactory.CreateSlider(card, "MusicSlider", new Vector2(0, 445), new Vector2(600, 55), SaveManager.Data.musicVolume, v => AudioManager.Instance.SetMusicVolume(v));
            musicLabel = Button(card, "Music", 355, () => { AudioManager.Instance.SetMusicOn(!SaveManager.Data.musicOn); Labels(); });
            UIFactory.CreateText(card, "SoundVolume", "SOUND VOLUME", 30, ColorPalette.UiText, new Vector2(0, 265), new Vector2(600, 50));
            sfxSlider = UIFactory.CreateSlider(card, "SfxSlider", new Vector2(0, 210), new Vector2(600, 55), SaveManager.Data.sfxVolume, v => AudioManager.Instance.SetSfxVolume(v));
            sfxLabel = Button(card, "Sfx", 130, () => { AudioManager.Instance.SetSfxOn(!SaveManager.Data.sfxOn); Labels(); });
            hapticsLabel = Button(card, "Haptics", 20, () => { SaveManager.Data.hapticsOn = !SaveManager.Data.hapticsOn; SaveManager.Save(); Labels(); });
            motionLabel = Button(card, "Motion", -90, () => { SaveManager.Data.reducedMotion = !SaveManager.Data.reducedMotion; SaveManager.Save(); Labels(); });
            qualityLabel = Button(card, "Quality", -200, () => { SaveManager.Data.lowQuality = !SaveManager.Data.lowQuality; SaveManager.Save(); QualityProfile.Apply(); Labels(); });
            Button(card, "Tutorial", -310, () => { SaveManager.Data.tutorialCompleted = false; SaveManager.Save(); UIManager.Instance.CloseOverlays(); GameManager.Instance.StartRun(RunConfig.Campaign(1)); }).text = "REPLAY TUTORIAL";
            resetLabel = Button(card, "Reset", -420, Reset);
            UIFactory.CreateButton(card, "Close", "CLOSE", new Vector2(0, -565), new Vector2(510, 100), ColorPalette.UiAccent, () => UIManager.Instance.CloseOverlays(), 40);
            Labels();
        }
        Text Button(Transform card, string name, float y, UnityEngine.Events.UnityAction action) => UIFactory.CreateButton(card, name, "", new Vector2(0, y), new Vector2(600, 90), ColorPalette.Get(GameColor.Blue), action, 32).GetComponentInChildren<Text>();
        public void Refresh()
        {
            if (musicSlider == null) return;
            resetConfirmed = false;
            musicSlider.SetValueWithoutNotify(SaveManager.Data.musicVolume); sfxSlider.SetValueWithoutNotify(SaveManager.Data.sfxVolume); Labels();
        }
        void Labels()
        {
            var d = SaveManager.Data;
            musicLabel.text = "MUSIC: " + (d.musicOn ? "ON" : "OFF"); sfxLabel.text = "SOUND: " + (d.sfxOn ? "ON" : "OFF");
            hapticsLabel.text = "HAPTICS: " + (d.hapticsOn ? "ON" : "OFF"); motionLabel.text = "REDUCED MOTION: " + (d.reducedMotion ? "ON" : "OFF");
            qualityLabel.text = d.lowQuality ? "QUALITY: LOW / 30 FPS" : "QUALITY: HIGH / 60 FPS";
            resetLabel.text = resetConfirmed ? "CONFIRM RESET" : "RESET PROGRESS";
        }
        void Reset() { if (!resetConfirmed) { resetConfirmed = true; Labels(); return; } SaveManager.DeleteAll(); AudioManager.Instance.ApplySettings(); QualityProfile.Apply(); Refresh(); }
    }
}
