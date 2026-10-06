using UnityEngine;
using UnityEngine.UI;
namespace ColorStackRush
{
    public class SaveStatusPanel : MonoBehaviour
    {
        Text label;
        bool busy;
        float failureSeconds;
        void Awake() => label = UIFactory.CreateText(transform, "SaveStatus", "", 25, ColorPalette.UiText, new Vector2(0, 25), new Vector2(950, 55), new Vector2(.5f, 0));
        void OnEnable() => SaveManager.SaveCompleted += Completed;
        void OnDisable() => SaveManager.SaveCompleted -= Completed;
        void Completed(bool success) { if (!success) { failureSeconds = 5; label.text = "Kayıt tamamlanamadı. Tekrar dene."; label.color = ColorPalette.UiBad; } }
        void Update()
        {
            failureSeconds = Mathf.Max(0, failureSeconds - Time.unscaledDeltaTime);
            bool saving = SaveManager.IsSaving || SaveManager.IsTransactionPending;
            if (saving != busy || (failureSeconds == 0 && !saving && label.text.Length != 0))
            {
                busy = saving;
                if (failureSeconds == 0) { label.text = saving ? "Kaydediliyor…" : ""; label.color = ColorPalette.UiText; }
            }
        }
    }
}
