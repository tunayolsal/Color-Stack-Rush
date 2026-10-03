using UnityEngine;
namespace ColorStackRush
{
    public enum TrackPattern { BlockRun, WallGap, CoinRun, Spinner, Slider, Slalom, RiskRoute, ColorTransition }
    public enum TrackKind { Block, Coin, Wall, Spinner, Slider, PowerUp }
    public struct TrackItem
    {
        public TrackKind kind;
        public float x, z, sweep, phase;
        public GameColor color;
        public PowerUpType power;
    }
    // A caller-owned reusable buffer: planning never allocates during streaming.
    public sealed class TrackSegment
    {
        public const float Length = 24f;
        public readonly TrackItem[] items = new TrackItem[24];
        public int count;
        public float startZ, safeX;
        public GameColor color;
        public TrackPattern pattern;
        public void Add(TrackKind kind, float x, float z, float sweep = 0, float phase = 0, GameColor? c = null, PowerUpType power = PowerUpType.Magnet)
        {
            items[count++] = new TrackItem { kind = kind, x = x, z = z, sweep = sweep, phase = phase, color = c ?? color, power = power };
        }
    }
    public static class TrackPlanner
    {
        public const float FirstZ = 18, ColorBand = 220, WarningDistance = 42, TransitionAfter = 24;
        static uint Hash(uint value) { value ^= value >> 16; value *= 0x7feb352d; value ^= value >> 15; value *= 0x846ca68b; return value ^ (value >> 16); }
        public static GameColor ColorAtDistance(RunConfig config, float z)
        {
            uint seed = Hash((uint)config.seed);
            int band = config.ChangesColor ? Mathf.Max(0, Mathf.FloorToInt(z / ColorBand)) : 0;
            return (GameColor)(((int)(seed % 4) + band * ((seed & 4) == 0 ? 1 : 3)) % 4);
        }
        public static float SafeX(int index) => index < 0 ? 0 : Mathf.Sin(index * .55f) * 1.2f;
        public static void Fill(RunConfig config, int index, TrackSegment segment)
        {
            segment.count = 0;
            segment.startZ = FirstZ + index * TrackSegment.Length;
            segment.safeX = SafeX(index);
            segment.color = ColorAtDistance(config, segment.startZ + 12);
            int band = Mathf.FloorToInt((segment.startZ + WarningDistance) / ColorBand);
            float boundary = band * ColorBand;
            if (config.ChangesColor && band > 0 && segment.startZ < boundary + TransitionAfter && segment.startZ + TrackSegment.Length > boundary - WarningDistance)
            { segment.pattern = TrackPattern.ColorTransition; return; }
            uint roll = Hash((uint)config.seed ^ ((uint)index + 1) * 0x9e3779b9);
            int maxPatterns = config.mode == RunMode.Endless ? (segment.startZ < 450 ? 5 : 7) : config.level == 1 ? 1 : config.level == 2 ? 3 : config.level < 5 ? 5 : config.level <= 6 ? 5 : 7;
            int selection = (int)(roll % (uint)maxPatterns);
            // Recovery every third segment; introductions restrict the moving obstacle set.
            segment.pattern = index % 3 == 0 ? TrackPattern.BlockRun : (TrackPattern)selection;
            if (config.mode == RunMode.Campaign && config.level < 5 && segment.pattern == TrackPattern.Spinner) segment.pattern = TrackPattern.Slider;
            if (config.mode == RunMode.Campaign && config.level < 3 && segment.pattern == TrackPattern.Slider) segment.pattern = TrackPattern.WallGap;
            if (config.mode == RunMode.Campaign)
            {
                if (config.level == 1 && index % 3 == 1) segment.pattern = TrackPattern.CoinRun;
                if (index == 1 && config.level == 2) segment.pattern = TrackPattern.WallGap;
                if (index == 1 && config.level == 3) segment.pattern = TrackPattern.Slider;
                if (index == 1 && config.level == 5) segment.pattern = TrackPattern.Spinner;
            }
            float side = segment.safeX > 0 ? -1 : 1;
            float phase = (roll >> 8) % 628 / 100f;
            switch (segment.pattern)
            {
                case TrackPattern.BlockRun:
                    for (int i = 0; i < 4; i++) segment.Add(TrackKind.Block, segment.safeX, 7 + i * 2.7f);
                    // A clearly separate wrong-color lane teaches avoidance without blocking the safe route.
                    if (config.level > 1 || config.mode == RunMode.Endless) segment.Add(TrackKind.Block, side * 2.2f, 12, .55f, c: (GameColor)(((int)segment.color + 1) % 4));
                    else if (index == 2) segment.Add(TrackKind.Block, side * 2.2f, 12, .55f, c: (GameColor)(((int)segment.color + 1) % 4));
                    break;
                case TrackPattern.WallGap:
                    for (float x = -2.8f; x <= 2.81f; x += 1.4f)
                        if (Mathf.Abs(x - segment.safeX) >= 1.4f) segment.Add(TrackKind.Wall, x, 12, .55f);
                    segment.Add(TrackKind.Block, segment.safeX, 16);
                    segment.Add(TrackKind.Block, segment.safeX, 20);
                    break;
                case TrackPattern.CoinRun:
                    for (int i = 0; i < 5; i++) segment.Add(TrackKind.Coin, segment.safeX, 6 + i * 2.5f);
                    segment.Add(TrackKind.Block, segment.safeX, 21);
                    break;
                case TrackPattern.Spinner:
                    segment.Add(TrackKind.Spinner, side * 2.1f, 12, .85f, phase);
                    segment.Add(TrackKind.Block, segment.safeX, 16);
                    segment.Add(TrackKind.Block, segment.safeX, 20);
                    break;
                case TrackPattern.Slider:
                    // The whole swept collider stays outside the protected corridor.
                    segment.Add(TrackKind.Slider, side * 1.95f, 12, .95f, phase);
                    for (int i = 0; i < 3; i++) segment.Add(TrackKind.Coin, segment.safeX, 15 + i * 2);
                    segment.Add(TrackKind.Block, segment.safeX, 21);
                    break;
                case TrackPattern.Slalom:
                    for (int i = 0; i < 4; i++) segment.Add(TrackKind.Block, segment.safeX + (i % 2 == 0 ? -.4f : .4f), 7 + i * 3.2f);
                    segment.Add(TrackKind.Wall, side * 2.4f, 12, .55f);
                    break;
                case TrackPattern.RiskRoute:
                    segment.Add(TrackKind.Block, segment.safeX, 7);
                    segment.Add(TrackKind.Block, segment.safeX, 20);
                    segment.Add(TrackKind.Slider, side * 1.95f, 12, .95f, phase);
                    for (int i = 0; i < 5; i++) segment.Add(TrackKind.Coin, side * 2.2f, 14 + i * 1.7f);
                    break;
            }
            // The final world combines two hazards on the optional side, keeping the safe corridor open.
            if ((config.mode == RunMode.Campaign && config.level >= 13) || (config.mode == RunMode.Endless && segment.startZ > 1200))
            {
                if (segment.pattern == TrackPattern.Spinner || segment.pattern == TrackPattern.Slider || segment.pattern == TrackPattern.RiskRoute)
                    segment.Add(TrackKind.Wall, side * 2.4f, 19, .55f);
            }
            if ((config.mode == RunMode.Endless || config.level >= 3) && (roll >> 16) % 100 < 8)
                segment.Add(TrackKind.PowerUp, segment.safeX, 22, power: (PowerUpType)((roll >> 24) % 5));
        }
        public static void Fallback(TrackSegment segment)
        {
            segment.count = 0;
            segment.pattern = TrackPattern.BlockRun;
            segment.safeX = Mathf.Clamp(segment.safeX, -1.2f, 1.2f);
            for (int i = 0; i < 4; i++) segment.Add(TrackKind.Block, segment.safeX, 8 + i * 3);
        }
    }
    public static class TrackValidator
    {
        // The player has radius .55; a .15 buffer reserves a full 1.4-wide corridor.
        public static bool Validate(TrackSegment segment, float previousSafeX, float maxSpeed)
        {
            if (segment.count < 0 || segment.count > segment.items.Length || Mathf.Abs(segment.safeX) > 2.6f || maxSpeed <= 0 || maxSpeed > 18) return false;
            if (Mathf.Abs(segment.safeX - previousSafeX) / 4f + .2f > 7f / maxSpeed) return false;
            for (int i = 0; i < segment.count; i++)
            {
                var item = segment.items[i];
                if (item.z < 0 || item.z >= TrackSegment.Length || Mathf.Abs(item.x) > 3.5f) return false;
                bool hazard = item.kind == TrackKind.Wall || item.kind == TrackKind.Spinner || item.kind == TrackKind.Slider || (item.kind == TrackKind.Block && item.color != segment.color);
                if (hazard && Mathf.Abs(item.x - segment.safeX) < item.sweep + .7f) return false;
            }
            return true;
        }
    }
}
