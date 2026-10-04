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
        static uint Hash(uint value) => LevelCatalog.Hash(value);
        public static GameColor ColorAtDistance(RunConfig config, float z)
        {
            uint seed = Hash((uint)config.seed);
            int band = config.ChangesColor ? Mathf.Max(0, Mathf.FloorToInt(z / ColorBand)) : 0;
            return (GameColor)(((int)(seed % 4) + band * ((seed & 4) == 0 ? 1 : 3)) % 4);
        }
        public static float SafeX(RunConfig config, int index)
        {
            if (index < 0) return 0;
            float phase = (Hash((uint)config.seed ^ 0x51ed270bu) % 6283) / 1000f;
            // Ease out from the spawn point; adjacent corridor centres move less
            // than .71 units, including at the maximum supported speed of 18.
            float introduction = Mathf.Min(1, (index + 1) / 8f);
            return Mathf.Sin(index * .21f + phase) * config.Difficulty.CorridorAmplitude * introduction;
        }
        static uint Roll(RunConfig config, int index) => Hash((uint)config.seed ^ unchecked(((uint)index + 1) * 0x9e3779b9u));
        static bool IsTransition(RunConfig config, float startZ)
        {
            int band = Mathf.FloorToInt((startZ + WarningDistance) / ColorBand);
            float boundary = band * ColorBand;
            return config.ChangesColor && band > 0 && startZ < boundary + TransitionAfter && startZ + TrackSegment.Length > boundary - WarningDistance;
        }
        public static int PowerSegment(RunConfig config)
        {
            if (config.levelId <= 2) return -1;
            // One first successful 4% roll per finite course. Recomputing this
            // small bounded scan keeps Fill stateless and deterministic.
            for (int i = 2; FirstZ + (i + 1) * TrackSegment.Length < config.Length; i++)
                if (!IsTransition(config, FirstZ + i * TrackSegment.Length) && (Roll(config, i) >> 16) % 100 < 4) return i;
            return -1;
        }
        public static void Fill(RunConfig config, int index, TrackSegment segment)
        {
            segment.count = 0;
            segment.startZ = FirstZ + index * TrackSegment.Length;
            segment.safeX = SafeX(config, index);
            segment.color = ColorAtDistance(config, segment.startZ + 12);
            if (IsTransition(config, segment.startZ))
            { segment.pattern = TrackPattern.ColorTransition; return; }
            uint roll = Roll(config, index);
            var difficulty = config.Difficulty;
            int maxPatterns = config.levelId == 1 ? 1 : config.levelId == 2 ? 3 : config.levelId <= 6 ? 5 : 7;
            int selection = (int)(roll % (uint)maxPatterns);
            if (config.levelId >= 7) selection = 1 + (int)(roll % 6); // recovery is explicit, rather than a second random easy lane
            segment.pattern = index % difficulty.RecoveryPeriod == 0 ? TrackPattern.BlockRun : (TrackPattern)selection;
            if (config.levelId < 5 && segment.pattern == TrackPattern.Spinner) segment.pattern = TrackPattern.Slider;
            if (config.levelId < 3 && segment.pattern == TrackPattern.Slider) segment.pattern = TrackPattern.WallGap;
            if (config.levelId == 1 && index % 3 == 1) segment.pattern = TrackPattern.CoinRun;
            if (index == 1 && config.levelId == 2) segment.pattern = TrackPattern.WallGap;
            if (index == 1 && config.levelId == 3) segment.pattern = TrackPattern.Slider;
            if (index == 1 && config.levelId == 5) segment.pattern = TrackPattern.Spinner;
            float side = segment.safeX > 0 ? -1 : 1;
            float phase = (roll >> 8) % 628 / 100f;
            switch (segment.pattern)
            {
                case TrackPattern.BlockRun:
                    for (int i = 0; i < 4; i++) segment.Add(TrackKind.Block, segment.safeX, 7 + i * 2.7f);
                    // A clearly separate wrong-color lane teaches avoidance without blocking the safe route.
                    if (config.levelId > 1) segment.Add(TrackKind.Block, side * 2.2f, 12, .55f, c: (GameColor)(((int)segment.color + 1) % 4));
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
                    for (int i = 0; i < 4; i++) segment.Add(TrackKind.Block, Mathf.Clamp(segment.safeX + (i % 2 == 0 ? -.35f : .35f), -2.5f, 2.5f), 7 + i * 3.2f);
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
            if (config.levelId >= 13)
            {
                if (segment.pattern == TrackPattern.Spinner || segment.pattern == TrackPattern.Slider || segment.pattern == TrackPattern.RiskRoute)
                    segment.Add(TrackKind.Wall, side * 2.4f, 19, .55f);
            }
            if (difficulty.DecoyCount > 1 && segment.pattern != TrackPattern.BlockRun)
            {
                segment.Add(TrackKind.Block, side * 2.3f, 9, .55f, c: (GameColor)(((int)segment.color + 1 + (roll % 3)) % 4));
                float decoyX = side * 1.55f;
                if (Mathf.Abs(decoyX - segment.safeX) >= 1.25f)
                    segment.Add(TrackKind.Block, decoyX, 20, .55f, c: (GameColor)(((int)segment.color + 2) % 4));
            }
            if (index == PowerSegment(config))
                segment.Add(TrackKind.PowerUp, segment.safeX, 22, power: (PowerUpType)((roll >> 24) % 5));
        }
        public static void Fallback(TrackSegment segment)
        {
            segment.count = 0;
            segment.pattern = TrackPattern.BlockRun;
            segment.safeX = Mathf.Clamp(segment.safeX, -2.1f, 2.1f);
            for (int i = 0; i < 4; i++) segment.Add(TrackKind.Block, segment.safeX, 8 + i * 3);
        }
    }
    public static class TrackValidator
    {
        // The player has radius .55; a .15 buffer reserves a full 1.4-wide corridor.
        public static bool Validate(TrackSegment segment, float previousSafeX, float maxSpeed)
        {
            if (segment.count < 0 || segment.count > segment.items.Length || !Finite(segment.safeX) || !Finite(maxSpeed) || !Finite(previousSafeX) || Mathf.Abs(segment.safeX) > 2.6f || maxSpeed <= 0 || maxSpeed > 18) return false;
            if (Mathf.Abs(segment.safeX - previousSafeX) / 4f + .2f > 7f / maxSpeed) return false;
            for (int i = 0; i < segment.count; i++)
            {
                var item = segment.items[i];
                if (!Finite(item.x) || !Finite(item.z) || !Finite(item.sweep) || item.sweep < 0 || item.z < 0 || item.z >= TrackSegment.Length || Mathf.Abs(item.x) > 3.5f) return false;
                bool hazard = item.kind == TrackKind.Wall || item.kind == TrackKind.Spinner || item.kind == TrackKind.Slider || (item.kind == TrackKind.Block && item.color != segment.color);
                if (hazard && Mathf.Abs(item.x - segment.safeX) < item.sweep + .7f) return false;
            }
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
