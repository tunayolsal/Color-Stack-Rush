using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class SettingsPanel : MonoBehaviour
    {
        Slider musicSlider, sfxSlider;
        Text musicLabel, sfxLabel, hapticsLabel, motionLabel, qualityLabel, resetLabel;
        bool resetConfirmed;
        bool operationPending;
        Button[] buttons;
        public void Build()
        {
            UIFactory.CreatePanel(transform, "Dim", ColorPalette.UiDim);
            var card = UIFactory.CreateImage(transform, "Card", ColorPalette.UiCard, Vector2.zero, new Vector2(840, 1440)).transform;
            UIFactory.CreateText(card, "Title", "Ayarlar", 62, ColorPalette.UiText, new Vector2(0, 610), new Vector2(700, 85));
            UIFactory.CreateText(card, "MusicVolume", "Müzik seviyesi", 30, ColorPalette.UiText, new Vector2(0, 505), new Vector2(600, 50));
            musicSlider = UIFactory.CreateSlider(card, "MusicSlider", new Vector2(0, 445), new Vector2(600, 55), SaveManager.Data.musicVolume, v => AudioManager.Instance.SetMusicVolume(v));
            musicLabel = Button(card, "Music", 355, () => { AudioManager.Instance.SetMusicOn(!SaveManager.Data.musicOn); Labels(); });
            UIFactory.CreateText(card, "SoundVolume", "Efekt seviyesi", 30, ColorPalette.UiText, new Vector2(0, 265), new Vector2(600, 50));
            sfxSlider = UIFactory.CreateSlider(card, "SfxSlider", new Vector2(0, 210), new Vector2(600, 55), SaveManager.Data.sfxVolume, v => AudioManager.Instance.SetSfxVolume(v));
            sfxLabel = Button(card, "Sfx", 130, () => { AudioManager.Instance.SetSfxOn(!SaveManager.Data.sfxOn); Labels(); });
            hapticsLabel = Button(card, "Haptics", 20, () => { SaveManager.Data.hapticsOn = !SaveManager.Data.hapticsOn; SaveManager.Save(); Labels(); });
            motionLabel = Button(card, "Motion", -90, () => { SaveManager.Data.reducedMotion = !SaveManager.Data.reducedMotion; SaveManager.Save(); Labels(); });
            qualityLabel = Button(card, "Quality", -200, () => { SaveManager.Data.lowQuality = !SaveManager.Data.lowQuality; SaveManager.Save(); QualityProfile.Apply(); Labels(); });
            Button(card, "Tutorial", -310, ReplayTutorial).text = "Öğreticiyi tekrar oyna";
            resetLabel = Button(card, "Reset", -420, Reset);
            UIFactory.CreateButton(card, "Close", "Kapat", new Vector2(0, -565), new Vector2(510, 100), ColorPalette.UiAccent, () => UIManager.Instance.CloseOverlays(), 40);
            Labels();
            buttons = card.GetComponentsInChildren<Button>();
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
            musicLabel.text = "Müzik: " + (d.musicOn ? "Açık" : "Kapalı"); sfxLabel.text = "Ses: " + (d.sfxOn ? "Açık" : "Kapalı");
            hapticsLabel.text = "Titreşim: " + (d.hapticsOn ? "Açık" : "Kapalı"); motionLabel.text = "Azaltılmış hareket: " + (d.reducedMotion ? "Açık" : "Kapalı");
            qualityLabel.text = d.lowQuality ? "Kalite: Hafif / 30 FPS" : "Kalite: Yüksek / 60 FPS";
            resetLabel.text = resetConfirmed ? "Sıfırlamayı onayla" : "İlerlemeyi sıfırla";
        }
        void SetPending(bool value)
        {
            operationPending = value;
            foreach (var button in buttons) button.interactable = !value;
            musicSlider.interactable = sfxSlider.interactable = !value;
        }
        void ReplayTutorial()
        {
            if (operationPending || !SaveManager.TryBeginTransaction()) return;
            bool previous = SaveManager.Data.tutorialCompleted;
            SaveManager.Data.tutorialCompleted = false;
            SetPending(true);
            SaveManager.SaveAsync(ok =>
            {
                if (!ok) SaveManager.Data.tutorialCompleted = previous;
                SaveManager.EndTransaction();
                if (this == null) return;
                SetPending(false);
                if (ok) StartCoroutine(StartTutorialWhenSaved());
                else resetLabel.text = "Kayıt tamamlanamadı. Tekrar dene.";
            });
        }
        IEnumerator StartTutorialWhenSaved()
        {
            while (SaveManager.IsSaving || SaveManager.IsTransactionPending) yield return null;
            UIManager.Instance.CloseOverlays();
            GameManager.Instance.StartRun(RunConfig.Level(1));
        }
        void Reset()
        {
            if (operationPending || SaveManager.IsSaving || SaveManager.IsTransactionPending) return;
            if (!resetConfirmed) { resetConfirmed = true; Labels(); return; }
            SetPending(true); resetLabel.text = "Kaydediliyor…";
            bool accepted = SaveManager.DeleteAllAsync(ok =>
            {
                if (this == null) return;
                SetPending(false);
                if (ok) { AudioManager.Instance.ApplySettings(); QualityProfile.Apply(); Refresh(); }
                else { Refresh(); resetLabel.text = "Sıfırlanamadı. Tekrar dene."; }
            });
            if (!accepted) { SetPending(false); Refresh(); resetLabel.text = "Başka bir kayıt sürüyor. Tekrar dene."; }
        }
    }
}
