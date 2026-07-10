namespace ColorStackRush
{
    /// <summary>High-level state of the game loop. Drives UI panels, input and time scale.</summary>
    public enum GameState
    {
        MainMenu,   // Idle in menu, world visible in background
        Playing,    // Active run
        Paused,     // Time frozen, pause overlay shown
        Finish,     // Climbing the multiplier stairs (scripted sequence)
        GameOver,   // Stack reached zero
        Victory     // Finished the level stairs
    }

    /// <summary>The four collectible colors. Values map into ColorPalette.</summary>
    public enum GameColor
    {
        Pink = 0,
        Blue = 1,
        Yellow = 2,
        Green = 3
    }

    /// <summary>All available power-ups (bonus feature set).</summary>
    public enum PowerUpType
    {
        Magnet,      // Pulls matching blocks & coins toward the player
        DoubleCoins, // Coins are worth double
        Shield,      // Obstacles are smashed instead of damaging the stack
        SlowMotion,  // Time slows down for easier dodging
        LuckyBox     // Instant random reward
    }

    /// <summary>Identifiers for the procedurally generated sound effects.</summary>
    public enum SfxId
    {
        Collect,
        Wrong,
        Hit,
        Coin,
        Button,
        PowerUp,
        Stair,
        Win,
        Lose,
        ColorChange,
        Buy
    }

    /// <summary>Behaviour flavour of a pooled obstacle.</summary>
    public enum ObstacleKind
    {
        Wall,    // Static wall segment
        Spinner, // Rotating bar
        Slider   // Cube sliding left-right across the road
    }
}
