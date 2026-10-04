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
            FromHex("FF4F87"), // Pink
            FromHex("279CF4"), // Blue
            FromHex("FFD43B"), // Yellow
            FromHex("35CD86")  // Green
        };

        // --- Environment ---
        public static readonly Color Sky        = FromHex("BCE9F5");
        public static readonly Color Ground     = FromHex("A8BCCA");
        public static readonly Color GroundRail = FromHex("728DA7");
        public static readonly Color Fog        = FromHex("BCE9F5");

        // --- Objects ---
        public static readonly Color Obstacle   = FromHex("263D59"); // soft coral
        public static readonly Color Coin       = FromHex("E8A21C");
        public static readonly Color Stairs     = FromHex("8772E7");

        // --- UI ---
        public static readonly Color UiBackground = FromHex("EDF5FC");
        public static readonly Color UiCard       = Color.white;
        public static readonly Color UiText       = FromHex("19324D");
        public static readonly Color UiAccent     = FromHex("FF4F87");
        public static readonly Color UiGood       = FromHex("20996B");
        public static readonly Color UiBad        = FromHex("ED594E");
        public static readonly Color UiDim        = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>Returns the pastel Color for a gameplay GameColor.</summary>
        public static Color Get(GameColor color) => gameColors[(int)color];

        /// <summary>Distinct display color for each power-up pickup.</summary>
        public static Color GetPowerUp(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Magnet:      return FromHex("C89DF6");
                case PowerUpType.DoubleCoins: return FromHex("E8A21C");
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
