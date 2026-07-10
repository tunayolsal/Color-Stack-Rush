using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Central place for every color used by the game so the whole product
    /// keeps one consistent, bright pastel look.
    /// </summary>
    public static class ColorPalette
    {
        // --- Gameplay colors (indexed by GameColor) ---
        static readonly Color[] gameColors =
        {
            FromHex("FF9EC0"), // Pink
            FromHex("7EC8F5"), // Blue
            FromHex("FFD97A"), // Yellow
            FromHex("8FE3B0")  // Green
        };

        // --- Environment ---
        public static readonly Color Sky        = FromHex("DFF1FF");
        public static readonly Color Ground     = FromHex("F4EFFA");
        public static readonly Color GroundRail = FromHex("D9CBEE");
        public static readonly Color Fog        = FromHex("E7F2FD");

        // --- Objects ---
        public static readonly Color Obstacle   = FromHex("FF8B7E"); // soft coral
        public static readonly Color Coin       = FromHex("FFC84A");
        public static readonly Color Stairs     = FromHex("B9A6F2");

        // --- UI ---
        public static readonly Color UiBackground = FromHex("FFF7FA");
        public static readonly Color UiCard       = Color.white;
        public static readonly Color UiText       = FromHex("4A4460");
        public static readonly Color UiAccent     = FromHex("FF7DA9");
        public static readonly Color UiGood       = FromHex("5FCF8B");
        public static readonly Color UiBad        = FromHex("FF6B6B");
        public static readonly Color UiDim        = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>Returns the pastel Color for a gameplay GameColor.</summary>
        public static Color Get(GameColor color) => gameColors[(int)color];

        /// <summary>Distinct display color for each power-up pickup.</summary>
        public static Color GetPowerUp(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Magnet:      return FromHex("C89DF6");
                case PowerUpType.DoubleCoins: return FromHex("FFC84A");
                case PowerUpType.Shield:      return FromHex("6FE0DC");
                case PowerUpType.SlowMotion:  return FromHex("9FB7F0");
                default:                      return FromHex("FFFFFF"); // LuckyBox
            }
        }

        /// <summary>Parses an RRGGBB hex string into a Color. Editor-friendly helper.</summary>
        public static Color FromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
        }
    }
}
