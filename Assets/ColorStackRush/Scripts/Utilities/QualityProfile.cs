using UnityEngine;
namespace ColorStackRush
{
    public static class QualityProfile
    {
        public static int ParticleCount(int count) => SaveManager.Data.lowQuality ? Mathf.Max(1, count / 3) : count;
        public static void Apply()
        {
            bool low = SaveManager.Data.lowQuality;
            Application.targetFrameRate = low ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.shadows = low ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = low ? 0 : 35;
        }
    }
    public class ThemePresentation : MonoBehaviour
    {
        public static string Name(int theme) => theme == 1 ? "SUNSET" : theme == 2 ? "MIDNIGHT" : "SKY GARDEN";
        public static Color Ground(int theme) => theme == 1 ? new Color(.87f, .69f, .61f) : theme == 2 ? new Color(.19f, .22f, .35f) : ColorPalette.Ground;
        void OnEnable() => GameEvents.RunConfigured += Apply;
        void OnDisable() => GameEvents.RunConfigured -= Apply;
        void Apply(RunConfig config)
        {
            var sky = config.Theme == 1 ? new Color(.96f, .67f, .58f) : config.Theme == 2 ? new Color(.12f, .15f, .28f) : ColorPalette.Sky;
            if (Camera.main != null) Camera.main.backgroundColor = sky;
            RenderSettings.fogColor = sky;
            RenderSettings.ambientLight = config.Theme == 2 ? new Color(.6f, .65f, .85f) : new Color(.72f, .72f, .78f);
        }
        public static void TintGround(GameObject tile, int theme)
        {
            var surface = tile.transform.Find("Surface");
            if (surface != null) surface.GetComponent<MeshRenderer>().sharedMaterial = MaterialCache.Get(Ground(theme));
        }
    }
}
