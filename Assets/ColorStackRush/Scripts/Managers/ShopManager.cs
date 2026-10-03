using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>One purchasable ball skin.</summary>
    [System.Serializable]
    public struct SkinDefinition
    {
        public string name;
        public int cost;
        public Color primary; // ball color

        public SkinDefinition(string name, int cost, string hex)
        {
            this.name = name;
            this.cost = cost;
            this.primary = ColorPalette.FromHex(hex);
        }
    }

    /// <summary>
    /// Skin catalog + purchase/select logic. The catalog is defined in code so
    /// no ScriptableObject assets are needed; unlock state lives in save data.
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        public static ShopManager Instance { get; private set; }

        // 10 skins with escalating prices. Index 0 is the free default.
        static readonly List<SkinDefinition> skins = new List<SkinDefinition>
        {
            new SkinDefinition("Classic",   0,    "FFFFFF"),
            new SkinDefinition("Bubblegum", 100,  "FF9EC0"),
            new SkinDefinition("Mint",      150,  "8FE3B0"),
            new SkinDefinition("Ocean",     200,  "7EC8F5"),
            new SkinDefinition("Lemon",     250,  "FFE066"),
            new SkinDefinition("Lavender",  350,  "C6B5F2"),
            new SkinDefinition("Sunset",    500,  "FFA26B"),
            new SkinDefinition("Coral",     650,  "FF7B7B"),
            new SkinDefinition("Grape",     800,  "9D6BD9"),
            new SkinDefinition("Midnight",  1000, "3E3A5C"),
        };

        public static int SkinCount => skins.Count;

        void Awake() => Instance = this;

        /// <summary>Returns the definition for a skin index (clamped for safety).</summary>
        public static SkinDefinition GetSkin(int index)
        {
            return skins[Mathf.Clamp(index, 0, skins.Count - 1)];
        }

        public static bool IsUnlocked(int index) => SaveManager.Data.unlockedSkins.Contains(index);

        public static int SelectedSkin => SaveManager.Data.selectedSkin;

        /// <summary>Buys a skin if affordable. Auto-selects it on success.</summary>
        public bool TryBuy(int index)
        {
            if (index < 0 || index >= SkinCount || IsUnlocked(index)) return false;
            int cost = GetSkin(index).cost;
            if (SaveManager.Data.coins < cost) return false;
            int previousSkin = SaveManager.Data.selectedSkin;
            SaveManager.Data.coins -= cost;

            SaveManager.Data.unlockedSkins.Add(index);
            SaveManager.Data.selectedSkin = index;
            if (!SaveManager.Save())
            {
                SaveManager.Data.coins += cost;
                SaveManager.Data.unlockedSkins.Remove(index);
                SaveManager.Data.selectedSkin = previousSkin;
                return false;
            }
            GameEvents.RaiseCoinsChanged(SaveManager.Data.coins);
            AudioManager.Instance?.PlaySfx(SfxId.Buy);
            GameEvents.RaiseSkinSelected(index);
            return true;
        }

        /// <summary>Equips an unlocked skin and persists the choice.</summary>
        public void Select(int index)
        {
            if (!IsUnlocked(index)) return;
            SaveManager.Data.selectedSkin = index;
            SaveManager.Save();
            GameEvents.RaiseSkinSelected(index);
        }
    }
}
