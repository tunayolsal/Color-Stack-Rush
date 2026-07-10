using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Panel router. Maps game states to screens and manages the overlay
    /// panels (shop / settings / daily reward) that sit on top of the menu.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        // Screens (state-driven)
        GameObject mainMenu, hud, pause, gameOver, victory;
        // Overlays (opened on demand)
        GameObject settings, shop, daily;

        ShopPanel shopPanel;
        DailyRewardPanel dailyPanel;
        SettingsPanel settingsPanel;

        void Awake() => Instance = this;

        void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        /// <summary>Called once by UIBuilder after all panels are constructed.</summary>
        public void Init(GameObject mainMenu, GameObject hud, GameObject pause,
            GameObject gameOver, GameObject victory,
            GameObject settings, GameObject shop, GameObject daily)
        {
            this.mainMenu = mainMenu;
            this.hud = hud;
            this.pause = pause;
            this.gameOver = gameOver;
            this.victory = victory;
            this.settings = settings;
            this.shop = shop;
            this.daily = daily;

            shopPanel = shop.GetComponent<ShopPanel>();
            dailyPanel = daily.GetComponent<DailyRewardPanel>();
            settingsPanel = settings.GetComponent<SettingsPanel>();

            OnStateChanged(GameManager.Instance != null ? GameManager.Instance.State : GameState.MainMenu);
        }

        void OnStateChanged(GameState state)
        {
            if (mainMenu == null) return; // not initialized yet

            mainMenu.SetActive(state == GameState.MainMenu);
            hud.SetActive(state == GameState.Playing || state == GameState.Paused || state == GameState.Finish);
            pause.SetActive(state == GameState.Paused);
            gameOver.SetActive(state == GameState.GameOver);
            victory.SetActive(state == GameState.Victory);

            CloseOverlays();
        }

        // --- Overlay control (called by menu buttons) ---

        public void OpenShop()
        {
            shop.SetActive(true);
            shopPanel.Refresh();
        }

        public void OpenSettings()
        {
            settings.SetActive(true);
            settingsPanel.Refresh();
        }

        public void OpenDaily()
        {
            daily.SetActive(true);
            dailyPanel.Refresh();
        }

        public void CloseOverlays()
        {
            if (settings != null) settings.SetActive(false);
            if (shop != null) shop.SetActive(false);
            if (daily != null) daily.SetActive(false);
        }
    }
}
