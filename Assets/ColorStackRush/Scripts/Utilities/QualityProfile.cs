using UnityEngine;
namespace ColorStackRush
{
    public static class QualityProfile
    {
        public static int ParticleCount(int count) => SaveManager.Data.lowQuality ? Mathf.Max(0, count / 3) : Mathf.Max(0, count);
        public static void Apply()
        {
            bool low = SaveManager.Data.lowQuality;
            Application.targetFrameRate = low ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadows = low ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = low ? 0 : 28;
            QualitySettings.shadowCascades = 0;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.antiAliasing = low ? 0 : 2;
        }
    }
    public class ThemePresentation : MonoBehaviour
    {
        public static string Name(int theme) => theme == 1 ? "Gün Batımı" : theme == 2 ? "Ay Işığı" : "Gökyüzü Bahçesi";
        public static Color Ground(int theme) => theme == 1 ? new Color(.69f, .65f, .58f) : theme == 2 ? new Color(.40f, .48f, .62f) : ColorPalette.Ground;
        public static Color Shoulder(int theme) => theme == 1 ? new Color(.69f, .59f, .43f) : theme == 2 ? new Color(.26f, .37f, .47f) : new Color(.50f, .73f, .60f);
        void OnEnable() => GameEvents.RunConfigured += Apply;
        void OnDisable() => GameEvents.RunConfigured -= Apply;
        void Apply(RunConfig config)
        {
            var sky = config.Theme == 1 ? new Color(.98f, .71f, .52f) : config.Theme == 2 ? new Color(.21f, .30f, .46f) : ColorPalette.Sky;
            if (Camera.main != null) Camera.main.backgroundColor = sky;
            RenderSettings.fogColor = sky;
            RenderSettings.ambientLight = config.Theme == 2 ? new Color(.43f, .49f, .61f) : new Color(.38f, .43f, .50f);
        }
        public static void TintGround(GameObject tile, int theme)
        {
            if (tile.TryGetComponent<GroundPresentationCache>(out var cache)) cache.ApplyTheme(theme);
        }
    }
}
