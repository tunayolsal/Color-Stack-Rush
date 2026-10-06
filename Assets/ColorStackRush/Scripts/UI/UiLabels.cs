namespace ColorStackRush
{
    public static class UiLabels
    {
        static readonly string[] skinNames = { "Klasik", "Gün Işığı", "Okyanus", "Şeker", "Nane", "Lavanta", "Mercan", "Gece", "Altın", "Gökkuşağı" };
        public static string ColorName(GameColor color)
        {
            switch (color) { case GameColor.Pink: return "PEMBE"; case GameColor.Blue: return "MAVİ"; case GameColor.Yellow: return "SARI"; default: return "YEŞİL"; }
        }
        public static string PowerName(PowerUpType type)
        {
            switch (type) { case PowerUpType.Magnet: return "MIKNATIS"; case PowerUpType.DoubleCoins: return "ÇİFT ALTIN"; case PowerUpType.Shield: return "KALKAN"; case PowerUpType.SlowMotion: return "YAVAŞLATMA"; default: return "SÜRPRİZ"; }
        }
        public static string SkinName(int index)
        {
            return index >= 0 && index < skinNames.Length ? skinNames[index] : "Stil " + (index + 1);
        }
    }
}
