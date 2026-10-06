using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorStackRush
{
    public readonly struct RouteWaypoint
    {
        public readonly float x, z;
        public RouteWaypoint(float x, float z) { this.x = x; this.z = z; }
    }

    // Constructed and validated before streaming. The public player build never
    // sends this hidden route or its seed to the visible-observation AI driver.
    public sealed class CoursePlan
    {
        public RunConfig Config { get; }
        public TrackSegment[] Segments { get; }
        public TrackItem[] OpeningGates { get; }
        public TrackItem[] Gates { get; }
        public RouteWaypoint[] RoutePoints { get; }
        public int SegmentCount => Segments.Length;
        public int RewardSegmentIndex { get; }
        public int PowerSegmentIndex { get; internal set; } = -1;
        public bool UsedFallback { get; }
        internal CoursePlan(RunConfig config, TrackSegment[] segments, TrackItem[] opening,
            TrackItem[] gates, RouteWaypoint[] route, int reward, bool fallback)
        {
            Config = config; Segments = segments; OpeningGates = opening; Gates = gates;
            RoutePoints = route; RewardSegmentIndex = reward; UsedFallback = fallback;
        }
        public float SampleRouteX(float z)
        {
            if (z <= RoutePoints[0].z) return RoutePoints[0].x;
            for (int i = 1; i < RoutePoints.Length; i++)
                if (z <= RoutePoints[i].z)
                {
                    var before = RoutePoints[i - 1]; var after = RoutePoints[i];
                    return Mathf.Lerp(before.x, after.x, (z - before.z) / (after.z - before.z));
                }
            return RoutePoints[RoutePoints.Length - 1].x;
        }
        public void CopySegment(int index, TrackSegment buffer)
        {
            if (index < 0 || index >= Segments.Length)
            {
                buffer.count = 0; buffer.startZ = TrackPlanner.FirstZ + index * TrackSegment.Length;
                buffer.safeX = SampleRouteX(buffer.startZ + 12);
                buffer.color = TrackPlanner.ColorAtDistance(Config, buffer.startZ + 12);
                buffer.pattern = TrackPattern.ColorTransition; buffer.routeValidated = true;
                return;
            }
            var source = Segments[index];
            buffer.count = source.count; buffer.startZ = source.startZ; buffer.safeX = source.safeX;
            buffer.color = source.color; buffer.pattern = source.pattern; buffer.routeValidated = source.routeValidated;
            Array.Copy(source.items, buffer.items, source.count);
        }
    }

    public static class TrackValidator
    {
        public const float PlayerRadius = .55f, Clearance = .15f, HorizontalSpeed = 4f;
        public const float ReactionAndSettleSeconds = .5f;
        public static bool Validate(TrackSegment segment, float previousSafeX, float maxSpeed)
        {
            if (!BasicSegment(segment) || !Finite(previousSafeX) || !Finite(maxSpeed) || maxSpeed <= 0 || maxSpeed > 18) return false;
            if (segment.routeValidated) return true;
            // Standalone test segments have a constant corridor; complete
            // courses instead prove travel and collision clearance in world Z.
            if (Mathf.Abs(segment.safeX - previousSafeX) / HorizontalSpeed + .2f > 7f / maxSpeed) return false;
            for (int i = 0; i < segment.count; i++)
            {
                var item = segment.items[i];
                if (item.kind == TrackKind.RouteGate)
                {
                    if (Mathf.Abs(segment.safeX - item.x) > item.gapWidth * .5f - PlayerRadius - Clearance) return false;
                }
                else if (IsHazard(item, segment.color) && Mathf.Abs(item.x - segment.safeX) < SweptHalfWidth(item) + PlayerRadius + Clearance) return false;
            }
            return true;
        }
        public static bool ValidateCourse(CoursePlan course, out string reason)
        {
            reason = null;
            if (course == null || course.SegmentCount != TrackPlanner.SegmentCount(course.Config) || course.OpeningGates.Length != 4)
                return Fail("Invalid finite course size", out reason);
            foreach (var segment in course.Segments) segment.routeValidated = false;
            var points = course.RoutePoints;
            for (int i = 0; i < points.Length; i++)
            {
                if (!Finite(points[i].x) || !Finite(points[i].z) || Mathf.Abs(points[i].x) > 2.6f)
                    return Fail("Invalid route point", out reason);
                if (i == 0) continue;
                float dz = points[i].z - points[i - 1].z, dx = Mathf.Abs(points[i].x - points[i - 1].x);
                if (dz <= 0 || (dx > .001f && dz / 18f + .0001f < dx / HorizontalSpeed + ReactionAndSettleSeconds))
                    return Fail("Insufficient steering time", out reason);
            }
            for (int i = 0; i < course.Gates.Length; i++)
            {
                var gate = course.Gates[i];
                if (gate.gapWidth != TrackPlanner.GateGapWidth || Mathf.Abs(gate.x) != TrackPlanner.GateCentre
                    || (i > 0 && gate.z - course.Gates[i - 1].z + .001f < TrackPlanner.MinimumGateSpacing))
                    return Fail("Invalid gate spacing or opening", out reason);
                if (i < 4 && i > 0 && gate.x == course.Gates[i - 1].x)
                    return Fail("Opening gates must alternate", out reason);
                if (i < 4)
                {
                    int pickups = 0;
                    foreach (var segment in course.Segments)
                        for (int j = 0; j < segment.count; j++)
                        {
                            var pickup = segment.items[j];
                            if (pickup.kind == TrackKind.Block && Mathf.Abs(segment.startZ + pickup.z - (gate.z - 6)) < .001f
                                && Mathf.Abs(pickup.x - gate.x) < .001f
                                && pickup.color == TrackPlanner.ColorAtDistance(course.Config, gate.z - 6)) pickups++;
                        }
                    if (pickups != 1) return Fail("Opening gate needs exactly one arrival-color pickup", out reason);
                }
            }
            int beforeGateFourBlocks = 0, intendedHealth = 4, powers = 0;
            var reward = course.Segments[course.RewardSegmentIndex];
            if (reward.startZ < course.OpeningGates[3].z + 2 || reward.count != 8)
                return Fail("Invalid complete reward segment", out reason);
            for (int index = 0; index < course.SegmentCount; index++)
            {
                var segment = course.Segments[index];
                if (!BasicSegment(segment)) return Fail("Invalid segment geometry", out reason);
                if (TrackPlanner.IsTransition(course.Config, segment.startZ) && segment.count != 0)
                    return Fail("Content in protected color transition", out reason);
                for (int i = 0; i < segment.count; i++)
                {
                    var item = segment.items[i]; float z = segment.startZ + item.z;
                    var color = TrackPlanner.ColorAtDistance(course.Config, z);
                    if (item.kind == TrackKind.PowerUp)
                    {
                        if (++powers > 1 || course.Config.levelId <= 2 || z <= reward.startZ + TrackSegment.Length)
                            return Fail("Power can invalidate opening challenge", out reason);
                    }
                    if (item.kind == TrackKind.Block && item.color == color)
                    {
                        if (z < course.OpeningGates[3].z) beforeGateFourBlocks++;
                        if (Mathf.Abs(item.x - course.SampleRouteX(z)) > 1.1f)
                            return Fail("Correct pickup outside intended route", out reason);
                        intendedHealth = Mathf.Min(32, intendedHealth + 1);
                    }
                    if (IsHazard(item, color) && !ClearThroughout(course, item, z))
                        return Fail("Swept collider enters intended route", out reason);
                }
            }
            if (beforeGateFourBlocks != 4 || intendedHealth < 14)
                return Fail("Invalid opening or three-star health budget", out reason);
            if (!AllConstantPathsDieInOpening(course))
                return Fail("Passive input can survive opening challenge", out reason);
            foreach (var segment in course.Segments) segment.routeValidated = true;
            return true;
        }
        static bool ClearThroughout(CoursePlan course, TrackItem item, float z)
        {
            float reachZ = HalfDepth(item) + PlayerRadius + Clearance;
            float from = z - reachZ, to = z + reachZ;
            if (!ClearAt(course.SampleRouteX(from), item) || !ClearAt(course.SampleRouteX(to), item)) return false;
            float minimum = Mathf.Min(course.SampleRouteX(from), course.SampleRouteX(to));
            float maximum = Mathf.Max(course.SampleRouteX(from), course.SampleRouteX(to));
            foreach (var point in course.RoutePoints)
                if (point.z > from && point.z < to)
                {
                    if (!ClearAt(point.x, item)) return false;
                    minimum = Mathf.Min(minimum, point.x); maximum = Mathf.Max(maximum, point.x);
                }
            // Absolute separation can attain its minimum between endpoint
            // samples when a path crosses the obstacle centre.
            if (item.kind != TrackKind.RouteGate && minimum <= item.x && maximum >= item.x) return false;
            return true;
        }
        static bool ClearAt(float routeX, TrackItem item)
            => item.kind == TrackKind.RouteGate
                ? Mathf.Abs(routeX - item.x) <= item.gapWidth * .5f - PlayerRadius - Clearance + .0001f
                : Mathf.Abs(routeX - item.x) >= SweptHalfWidth(item) + PlayerRadius + Clearance - .0001f;
        static bool IsHazard(TrackItem item, GameColor color)
            => item.kind == TrackKind.RouteGate || item.kind == TrackKind.Wall || item.kind == TrackKind.Spinner
                || item.kind == TrackKind.Slider || (item.kind == TrackKind.Block && item.color != color);
        static float SweptHalfWidth(TrackItem item)
            => item.kind == TrackKind.Slider ? .95f : item.kind == TrackKind.Spinner ? .8f : .55f;
        static float HalfDepth(TrackItem item)
            => item.kind == TrackKind.Slider ? .7f : item.kind == TrackKind.Spinner ? .8f
                : item.kind == TrackKind.Wall || item.kind == TrackKind.RouteGate ? .425f : .55f;
        static bool BasicSegment(TrackSegment segment)
        {
            if (segment == null || segment.count < 0 || segment.count > segment.items.Length
                || !Finite(segment.safeX) || Mathf.Abs(segment.safeX) > 2.6f || !Finite(segment.startZ)) return false;
            for (int i = 0; i < segment.count; i++)
            {
                var item = segment.items[i];
                if (!Finite(item.x) || !Finite(item.z) || !Finite(item.sweep) || !Finite(item.phase)
                    || item.sweep < 0 || item.z < 0 || item.z >= TrackSegment.Length || Mathf.Abs(item.x) > 3.5f) return false;
                if (item.kind == TrackKind.RouteGate && (!Finite(item.gapWidth) || item.gapWidth < 1.4f || item.gapWidth > 7)) return false;
            }
            return true;
        }
        // Evaluate every interval of constant X for actual sphere/box contacts,
        // including boundary cases. Only guaranteed static opening collisions
        // count as damage; moving obstacles and power luck are irrelevant here.
        public static bool AllConstantPathsDieInOpening(CoursePlan course)
        {
            var boundaries = new List<float>(18) { -2.6f, 2.6f };
            foreach (var gate in course.OpeningGates)
            {
                boundaries.Add(Mathf.Clamp(gate.x - 1.1f, -2.6f, 2.6f));
                boundaries.Add(Mathf.Clamp(gate.x + 1.1f, -2.6f, 2.6f));
                float openingHalf = gate.gapWidth * .5f - PlayerRadius;
                boundaries.Add(Mathf.Clamp(gate.x - openingHalf, -2.6f, 2.6f));
                boundaries.Add(Mathf.Clamp(gate.x + openingHalf, -2.6f, 2.6f));
            }
            boundaries.Sort();
            for (int i = 0; i < boundaries.Count; i++)
            {
                if (OpeningHealthAtConstantX(course, boundaries[i]) > 0) return false;
                if (i > 0 && OpeningHealthAtConstantX(course, (boundaries[i] + boundaries[i - 1]) * .5f) > 0) return false;
            }
            return true;
        }
        public static int OpeningHealthAtConstantX(CoursePlan course, float x)
        {
            int health = 4;
            foreach (var gate in course.OpeningGates)
            {
                if (Mathf.Abs(x - gate.x) <= 1.1f) health++;
                // Treat exact tangency as a possible safe pass, conservatively.
                if (Mathf.Abs(x - gate.x) > gate.gapWidth * .5f - PlayerRadius)
                    health -= course.Config.Difficulty.ObstacleDamage;
                if (health <= 0) return 0;
            }
            return health;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Fail(string error, out string reason) { reason = error; return false; }
    }
}
