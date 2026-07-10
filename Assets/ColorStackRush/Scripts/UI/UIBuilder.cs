using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ColorStackRush
{
    /// <summary>
    /// Constructs the entire UI at startup: EventSystem (new Input System
    /// module), a mobile-scaled Canvas, and every panel. Panels are plain
    /// components that build their own children in Build().
    /// </summary>
    public class UIBuilder : MonoBehaviour
    {
        [Header("Reference resolution (portrait phone)")]
        [SerializeField] Vector2 referenceResolution = new Vector2(1080f, 1920f);

        void Awake()
        {
            BuildEventSystem();
            var canvas = BuildCanvas();

            // Order matters: later siblings render on top.
            var hud = BuildPanel<HUDPanel>(canvas, "HUD");
            hud.GetComponent<HUDPanel>().Build();

            var menu = BuildPanel<MainMenuPanel>(canvas, "MainMenu");
            menu.GetComponent<MainMenuPanel>().Build();

            var pause = BuildPanel<PausePanel>(canvas, "Pause");
            pause.GetComponent<PausePanel>().Build();

            var gameOver = BuildPanel<GameOverPanel>(canvas, "GameOver");
            gameOver.GetComponent<GameOverPanel>().Build();

            var victory = BuildPanel<VictoryPanel>(canvas, "Victory");
            victory.GetComponent<VictoryPanel>().Build();

            var shop = BuildPanel<ShopPanel>(canvas, "Shop");
            shop.GetComponent<ShopPanel>().Build();

            var daily = BuildPanel<DailyRewardPanel>(canvas, "DailyReward");
            daily.GetComponent<DailyRewardPanel>().Build();

            var settings = BuildPanel<SettingsPanel>(canvas, "Settings");
            settings.GetComponent<SettingsPanel>().Build();

            // Router takes over visibility from here.
            var manager = gameObject.AddComponent<UIManager>();
            manager.Init(menu, hud, pause, gameOver, victory, settings, shop, daily);
        }

        void BuildEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>(); // project uses the new Input System
        }

        Transform BuildCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f; // balanced phone/tablet scaling

            go.AddComponent<GraphicRaycaster>();
            return go.transform;
        }

        /// <summary>Creates a full-stretch panel root with the given component, initially inactive-safe.</summary>
        GameObject BuildPanel<T>(Transform canvas, string name) where T : Component
        {
            var rt = UIFactory.CreateRect(canvas, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.gameObject.AddComponent<T>();
            return rt.gameObject;
        }
    }
}
