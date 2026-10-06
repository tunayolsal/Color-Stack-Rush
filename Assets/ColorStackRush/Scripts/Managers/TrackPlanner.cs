using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    public enum TrackPattern { BlockRun, WallGap, CoinRun, Spinner, Slider, Slalom, RiskRoute, ColorTransition }
    public enum TrackKind { Block, Coin, Wall, Spinner, Slider, PowerUp, RouteGate }
    public struct TrackItem
    {
        public TrackKind kind;
        public float x, z, sweep, phase, gapWidth;
        public GameColor color;
        public PowerUpType power;
    }
    // Allocated once per course, or caller-owned as a streaming buffer.
    public sealed class TrackSegment
    {
        public const float Length = 24f;
        public readonly TrackItem[] items = new TrackItem[24];
        public int count;
        public float startZ, safeX;
        public GameColor color;
        public TrackPattern pattern;
        internal bool routeValidated;
        public void Add(TrackKind kind, float x, float z, float sweep = 0, float phase = 0,
            GameColor? c = null, PowerUpType power = PowerUpType.Magnet, float gapWidth = 0)
        {
            if (count >= items.Length) throw new InvalidOperationException("Track segment capacity exceeded.");
            items[count++] = new TrackItem { kind = kind, x = x, z = z, sweep = sweep,
                phase = phase, color = c ?? color, power = power, gapWidth = gapWidth };
        }
    }

    public static class TrackPlanner
    {
        public const float FirstZ = 18, ColorBand = 220, WarningDistance = 42, TransitionAfter = 24;
        public const float GateGapWidth = 2.5f, GateCentre = 1.9f, MinimumGateSpacing = 36f;
        const int CandidateLimit = 8;
        static CoursePlan cachedCourse;
        static uint Hash(uint value) => LevelCatalog.Hash(value);
        static uint Roll(RunConfig config, int index, uint salt = 0)
            => Hash((uint)config.seed ^ Hash((uint)config.contentVersion) ^ unchecked(((uint)index + 1) * 0x9e3779b9u) ^ salt);

        public static GameColor ColorAtDistance(RunConfig config, float z)
        {
            uint seed = Hash((uint)config.seed);
            int band = config.ChangesColor ? Mathf.Max(0, Mathf.FloorToInt(z / ColorBand)) : 0;
            return (GameColor)(((int)(seed % 4) + band * ((seed & 4) == 0 ? 1 : 3)) % 4);
        }
        public static bool IsTransition(RunConfig config, float startZ)
        {
            int band = Mathf.FloorToInt((startZ + WarningDistance) / ColorBand);
            float boundary = band * ColorBand;
            return config.ChangesColor && band > 0 && startZ < boundary + TransitionAfter
                && startZ + TrackSegment.Length > boundary - WarningDistance;
        }
        public static int SegmentCount(RunConfig config)
        {
            int count = 0;
            while (FirstZ + (count + 1) * TrackSegment.Length < config.Length) count++;
            return count;
        }
        public static CoursePlan CreateCourse(RunConfig config)
        {
            for (int attempt = 0; attempt < CandidateLimit; attempt++)
            {
                var course = Build(config, attempt, false);
                if (TrackValidator.ValidateCourse(course, out _)) return course;
            }
            // This route still contains all four compulsory turns and its health
            // budget; a fallback must never restore a passive straight-line win.
            return CreateFallbackCourse(config);
        }
        public static CoursePlan CreateFallbackCourse(RunConfig config)
        {
            var fallback = Build(config, 0, true);
            if (!TrackValidator.ValidateCourse(fallback, out string reason))
                throw new InvalidOperationException("Safe course generation failed: " + reason);
            return fallback;
        }
        static CoursePlan Cached(RunConfig config)
        {
            if (cachedCourse == null || cachedCourse.Config.levelId != config.levelId
                || cachedCourse.Config.seed != config.seed || cachedCourse.Config.contentVersion != config.contentVersion)
                cachedCourse = CreateCourse(config);
            return cachedCourse;
        }
        // Compatibility entry points use the actual finite plan, never generate
        // fictitious segments beyond its finish. These stay allocation-free.
        public static void Fill(RunConfig config, int index, TrackSegment segment) => Cached(config).CopySegment(index, segment);
        public static float SafeX(RunConfig config, int index)
            => index < 0 ? 0 : Cached(config).SampleRouteX(FirstZ + index * TrackSegment.Length + 12);
        public static int PowerSegment(RunConfig config) => Cached(config).PowerSegmentIndex;

        static CoursePlan Build(RunConfig config, int attempt, bool fallback)
        {
            int count = SegmentCount(config);
            var segments = new TrackSegment[count];
            for (int i = 0; i < count; i++)
            {
                float start = FirstZ + i * TrackSegment.Length;
                segments[i] = new TrackSegment { startZ = start, color = ColorAtDistance(config, start + 12),
                    pattern = IsTransition(config, start) ? TrackPattern.ColorTransition : TrackPattern.CoinRun };
            }
            float latest = Mathf.Min(config.Length - 48, segments[count - 1].startZ - 2);
            if (config.ChangesColor)
                for (int i = 0; i < count; i++)
                    if (segments[i].pattern == TrackPattern.ColorTransition) { latest = Mathf.Min(latest, segments[i].startZ - 6); break; }
            float slack = latest - 42 - 3 * MinimumGateSpacing;
            uint salt = unchecked((uint)(attempt + 1) * 0x632be59bu);
            float finalExtra = fallback ? 0 : (Roll(config, 0, salt) % 10001) / 10000f * slack;
            float firstExtra = fallback ? 0 : (Roll(config, 1, salt) % 10001) / 10000f * Mathf.Min(6, finalExtra);
            float a = fallback ? 0 : firstExtra + (Roll(config, 2, salt) % 10001) / 10000f * (finalExtra - firstExtra);
            float b = fallback ? 0 : firstExtra + (Roll(config, 3, salt) % 10001) / 10000f * (finalExtra - firstExtra);
            float[] offsets = { firstExtra, Mathf.Min(a, b), Mathf.Max(a, b), finalExtra };
            float sign = (Roll(config, 4, salt) & 1) == 0 ? -1 : 1;
            var opening = new TrackItem[4];
            var gates = new List<TrackItem>(12);
            var route = new List<RouteWaypoint>(28) { new RouteWaypoint(0, 0) };
            for (int i = 0; i < opening.Length; i++)
            {
                float z = 42 + i * MinimumGateSpacing + offsets[i];
                opening[i] = new TrackItem { kind = TrackKind.RouteGate, x = sign * GateCentre,
                    z = z, gapWidth = GateGapWidth };
                gates.Add(opening[i]);
                AddGateRoute(route, opening[i]);
                sign = -sign;
            }
            int reward = -1;
            for (int i = 0; i < count; i++)
                if (segments[i].startZ >= opening[3].z + 2 && segments[i].pattern != TrackPattern.ColorTransition) { reward = i; break; }
            if (reward < 0) throw new InvalidOperationException("No complete reward segment fits.");
            float lastX = opening[3].x;
            route.Add(new RouteWaypoint(lastX, segments[reward].startZ + 2));
            route.Add(new RouteWaypoint(lastX, segments[reward].startZ + 22));
            int eligible = 0, period = config.levelId <= 6 ? 3 : 2;
            if (config.levelId > 1)
                for (int i = reward + 1; i < count; i++)
                {
                    if (segments[i].pattern == TrackPattern.ColorTransition) continue;
                    if (++eligible % period != 0) continue;
                    var gate = new TrackItem { kind = TrackKind.RouteGate, x = -lastX,
                        z = segments[i].startZ + 12, gapWidth = GateGapWidth };
                    if (gate.z - gates[gates.Count - 1].z < MinimumGateSpacing) continue;
                    gates.Add(gate); AddGateRoute(route, gate); lastX = gate.x;
                }
            route.Add(new RouteWaypoint(lastX, config.Length + 6));
            var plan = new CoursePlan(config, segments, opening, gates.ToArray(), route.ToArray(), reward, fallback);
            foreach (var gate in opening)
            {
                AddGlobal(plan, TrackKind.Block, gate.x, gate.z - 6);
                AddGlobal(plan, TrackKind.RouteGate, gate.x, gate.z, gapWidth: gate.gapWidth);
            }
            var bonus = segments[reward];
            bonus.pattern = TrackPattern.BlockRun;
            for (int i = 0; i < 8; i++) bonus.Add(TrackKind.Block, opening[3].x, 4 + i * 2);
            for (int i = 4; i < gates.Count; i++)
            {
                AddGlobal(plan, TrackKind.Block, gates[i].x, gates[i].z - 6);
                AddGlobal(plan, TrackKind.RouteGate, gates[i].x, gates[i].z, gapWidth: gates[i].gapWidth);
            }
            int power = -1;
            for (int i = 0; i < count; i++)
            {
                var segment = segments[i];
                segment.safeX = plan.SampleRouteX(segment.startZ + 12);
                if (segment.pattern == TrackPattern.ColorTransition || i <= reward || segment.count > 0) continue;
                FillMotif(plan, i, fallback, salt);
                if (!fallback && config.levelId > 2 && power < 0 && (Roll(config, i, salt) >> 16) % 100 < 4)
                {
                    power = i;
                    segment.Add(TrackKind.PowerUp, plan.SampleRouteX(segment.startZ + 22), 22,
                        power: (PowerUpType)((Roll(config, i, salt) >> 24) % 5));
                }
            }
            plan.PowerSegmentIndex = power;
            // Coins guide the early turn without changing the anti-passive health budget.
            for (int i = 0; i < reward; i++)
            {
                var segment = segments[i];
                if (segment.pattern == TrackPattern.ColorTransition || segment.count > 0) continue;
                for (int j = 0; j < 3; j++) segment.Add(TrackKind.Coin, plan.SampleRouteX(segment.startZ + 6 + j * 5), 6 + j * 5);
            }
            foreach (var segment in segments) Array.Sort(segment.items, 0, segment.count, TrackItemZComparer.Instance);
            return plan;
        }
        static void AddGateRoute(List<RouteWaypoint> route, TrackItem gate)
        {
            route.Add(new RouteWaypoint(gate.x, gate.z - 7.4f));
            route.Add(new RouteWaypoint(gate.x, gate.z + 1.4f));
        }
        static void AddGlobal(CoursePlan plan, TrackKind kind, float x, float globalZ, float gapWidth = 0)
        {
            int index = Mathf.FloorToInt((globalZ - FirstZ) / TrackSegment.Length);
            var s = plan.Segments[index];
            if (s.pattern == TrackPattern.ColorTransition) throw new InvalidOperationException("Content in protected transition.");
            s.Add(kind, x, globalZ - s.startZ, c: ColorAtDistance(plan.Config, globalZ), gapWidth: gapWidth);
            if (kind == TrackKind.RouteGate) s.pattern = TrackPattern.WallGap;
        }
        static void FillMotif(CoursePlan plan, int index, bool fallback, uint salt)
        {
            var c = plan.Config; var s = plan.Segments[index]; uint roll = Roll(c, index, salt);
            int max = c.levelId == 1 ? 1 : c.levelId == 2 ? 3 : c.levelId <= 6 ? 5 : 7;
            s.pattern = fallback || index % c.Difficulty.RecoveryPeriod == 0 ? TrackPattern.BlockRun : (TrackPattern)(roll % (uint)max);
            if (c.levelId == 1 && !fallback && index % 3 == 1) s.pattern = TrackPattern.CoinRun;
            if (c.levelId < 5 && s.pattern == TrackPattern.Spinner) s.pattern = TrackPattern.Slider;
            if (c.levelId < 3 && s.pattern == TrackPattern.Slider) s.pattern = TrackPattern.WallGap;
            float phase = (roll >> 8) % 628 / 100f;
            switch (s.pattern)
            {
                case TrackPattern.BlockRun:
                    for (int j = 0; j < 4; j++) OnRoute(plan, s, TrackKind.Block, 7 + j * 2.7f);
                    break;
                case TrackPattern.WallGap:
                    for (float x = -2.8f; x <= 2.81f; x += 1.4f)
                        if (Mathf.Abs(x - plan.SampleRouteX(s.startZ + 12)) >= 1.45f) s.Add(TrackKind.Wall, x, 12, .55f);
                    OnRoute(plan, s, TrackKind.Block, 16); OnRoute(plan, s, TrackKind.Block, 20); break;
                case TrackPattern.CoinRun:
                    for (int j = 0; j < 5; j++) OnRoute(plan, s, TrackKind.Coin, 6 + j * 2.5f);
                    OnRoute(plan, s, TrackKind.Block, 21); break;
                case TrackPattern.Spinner:
                    s.Add(TrackKind.Spinner, Opposite(plan, s, 12), 12, .8f, phase);
                    OnRoute(plan, s, TrackKind.Block, 16); OnRoute(plan, s, TrackKind.Block, 20); break;
                case TrackPattern.Slider:
                    s.Add(TrackKind.Slider, Opposite(plan, s, 12), 12, .95f, phase);
                    for (int j = 0; j < 3; j++) OnRoute(plan, s, TrackKind.Coin, 15 + j * 2);
                    OnRoute(plan, s, TrackKind.Block, 21); break;
                case TrackPattern.Slalom:
                    for (int j = 0; j < 4; j++)
                    {
                        float z = 7 + j * 3.2f;
                        s.Add(TrackKind.Block, Mathf.Clamp(plan.SampleRouteX(s.startZ + z) + (j % 2 == 0 ? -.35f : .35f), -2.5f, 2.5f), z);
                    }
                    s.Add(TrackKind.Wall, Opposite(plan, s, 12), 12, .55f); break;
                case TrackPattern.RiskRoute:
                    OnRoute(plan, s, TrackKind.Block, 7); OnRoute(plan, s, TrackKind.Block, 20);
                    s.Add(TrackKind.Slider, Opposite(plan, s, 12), 12, .95f, phase);
                    for (int j = 0; j < 5; j++) s.Add(TrackKind.Coin, Opposite(plan, s, 14 + j * 1.7f), 14 + j * 1.7f);
                    break;
            }
            if (!fallback && c.levelId >= 13 && (s.pattern == TrackPattern.Spinner || s.pattern == TrackPattern.Slider || s.pattern == TrackPattern.RiskRoute))
                s.Add(TrackKind.Wall, Opposite(plan, s, 19), 19, .55f);
            if (!fallback && c.levelId > 1)
            {
                s.Add(TrackKind.Block, Opposite(plan, s, 9), 9, .55f, c: (GameColor)(((int)s.color + 1 + roll % 3) % 4));
                if (c.Difficulty.DecoyCount > 1) s.Add(TrackKind.Block, Opposite(plan, s, 20), 20, .55f, c: (GameColor)(((int)s.color + 2) % 4));
            }
        }
        static float Opposite(CoursePlan plan, TrackSegment segment, float z) => plan.SampleRouteX(segment.startZ + z) >= 0 ? -2.6f : 2.6f;
        static void OnRoute(CoursePlan plan, TrackSegment segment, TrackKind kind, float z)
            => segment.Add(kind, plan.SampleRouteX(segment.startZ + z), z);

        // Only for isolated diagnostic segments. Live courses use validated
        // whole-course fallback, including gates and the complete route.
        public static void Fallback(TrackSegment segment)
        {
            segment.count = 0; segment.routeValidated = false; segment.pattern = TrackPattern.BlockRun;
            segment.safeX = Mathf.Clamp(segment.safeX, -2.1f, 2.1f);
            for (int i = 0; i < 4; i++) segment.Add(TrackKind.Block, segment.safeX, 8 + i * 3);
        }
        sealed class TrackItemZComparer : IComparer<TrackItem>
        {
            public static readonly TrackItemZComparer Instance = new TrackItemZComparer();
            public int Compare(TrackItem a, TrackItem b) => a.z.CompareTo(b.z);
        }
    }
}
