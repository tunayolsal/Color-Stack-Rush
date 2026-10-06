using System;
using NUnit.Framework;
using UnityEngine;

namespace ColorStackRush.Tests
{
    public class CourseGenerationTests
    {
        [Test]
        public void ThousandActualFiniteCoursesHaveFourRequiredTurnsAndEnoughHealth()
        {
            int fallbackCount = 0;
            for (long id = 1; id <= 1000; id++)
            {
                var config = RunConfig.Level(id);
                var plan = TrackPlanner.CreateCourse(config);
                Assert.That(TrackValidator.ValidateCourse(plan, out string reason), Is.True, $"level {id}: {reason}");
                Assert.That(TrackValidator.AllConstantPathsDieInOpening(plan), Is.True, $"level {id}");
                Assert.That(plan.OpeningGates.Length, Is.EqualTo(4));
                Assert.That(plan.Config.contentVersion, Is.EqualTo(2));
                Assert.That(plan.Segments[plan.RewardSegmentIndex].count, Is.EqualTo(8));
                int correct = 0;
                foreach (var s in plan.Segments)
                {
                    Assert.That(s.startZ + TrackSegment.Length, Is.LessThan(config.Length));
                    if (TrackPlanner.IsTransition(config, s.startZ)) Assert.That(s.count, Is.Zero);
                    for (int j = 0; j < s.count; j++)
                    {
                        var item = s.items[j]; float z = s.startZ + item.z;
                        if (item.kind == TrackKind.Block && item.color == TrackPlanner.ColorAtDistance(config, z)) correct++;
                        if (id == 1) Assert.That(item.kind, Is.Not.EqualTo(TrackKind.Spinner).And.Not.EqualTo(TrackKind.Slider));
                        if (item.kind == TrackKind.PowerUp) Assert.That(z, Is.GreaterThan(plan.Segments[plan.RewardSegmentIndex].startZ + 24));
                    }
                }
                Assert.That(4 + correct, Is.GreaterThanOrEqualTo(14), $"level {id} cannot earn three stars");
                if (plan.UsedFallback) fallbackCount++;
            }
            Assert.That(fallbackCount, Is.LessThan(100), "Random candidates must normally pass rather than silently always fall back.");
        }

        [TestCase(1L)] [TestCase(2L)] [TestCase(6L)] [TestCase(18L)] [TestCase(19L)]
        [TestCase(20L)] [TestCase(100L)] [TestCase(1000L)] [TestCase(1000000L)]
        [TestCase(4294967315L)] [TestCase(long.MaxValue)]
        public void WholeCourseRetryIsDeterministicIncludingGeometryAndMotion(long id)
        {
            var a = TrackPlanner.CreateCourse(RunConfig.Level(id));
            var b = TrackPlanner.CreateCourse(RunConfig.Level(id));
            Assert.That(TrackValidator.ValidateCourse(a, out string reason), Is.True, reason);
            Assert.That(a.SegmentCount, Is.EqualTo(b.SegmentCount));
            Assert.That(a.RoutePoints.Length, Is.EqualTo(b.RoutePoints.Length));
            for (int i = 0; i < a.RoutePoints.Length; i++)
            {
                Assert.That(a.RoutePoints[i].x, Is.EqualTo(b.RoutePoints[i].x));
                Assert.That(a.RoutePoints[i].z, Is.EqualTo(b.RoutePoints[i].z));
            }
            for (int i = 0; i < a.SegmentCount; i++)
            {
                Assert.That(a.Segments[i].pattern, Is.EqualTo(b.Segments[i].pattern));
                Assert.That(a.Segments[i].count, Is.EqualTo(b.Segments[i].count));
                for (int j = 0; j < a.Segments[i].count; j++) Assert.That(a.Segments[i].items[j], Is.EqualTo(b.Segments[i].items[j]));
            }
        }

        [Test]
        public void GatesVaryBySeedWithoutManuallyAuthoredLevelCoordinates()
        {
            var baseline = TrackPlanner.CreateCourse(RunConfig.Level(1));
            int differences = 0;
            for (int i = 1; i < 20; i++)
            {
                var config = RunConfig.Level(1); config.seed = i;
                var plan = TrackPlanner.CreateCourse(config);
                if (plan.OpeningGates[1].z != baseline.OpeningGates[1].z) differences++;
            }
            Assert.That(differences, Is.GreaterThan(15));
        }

        [Test]
        public void CorruptRouteOrGateSpacingCannotPassTheWholeCourseValidator()
        {
            var plan = TrackPlanner.CreateCourse(RunConfig.Level(19));
            var original = plan.Gates[1];
            plan.Gates[1].z = plan.Gates[0].z + 10;
            Assert.That(TrackValidator.ValidateCourse(plan, out _), Is.False);
            plan.Gates[1] = original;
            // The gate occupies a real collider footprint, not just a lane-centre check.
            var segment = plan.Segments[plan.RewardSegmentIndex + 1];
            segment.Add(TrackKind.Slider, plan.SampleRouteX(segment.startZ + 12), 12, .95f);
            Assert.That(TrackValidator.ValidateCourse(plan, out _), Is.False);
        }

        [Test]
        public void GuaranteedFallbackStillRequiresSteeringAndAllowsThreeStars()
        {
            foreach (long id in new long[] { 1, 4, 7, 18, 19, 1000, 1000000 })
            {
                var course = TrackPlanner.CreateFallbackCourse(RunConfig.Level(id));
                Assert.That(course.UsedFallback, Is.True);
                Assert.That(TrackValidator.ValidateCourse(course, out string reason), Is.True, reason);
                Assert.That(TrackValidator.AllConstantPathsDieInOpening(course), Is.True);
                Assert.That(course.PowerSegmentIndex, Is.EqualTo(-1));
            }
        }

        [Test]
        public void StreamingValidatedCourseAllocatesNothingAfterWarmup()
        {
            var plan = TrackPlanner.CreateCourse(RunConfig.Level(1000000));
            var buffer = new TrackSegment();
            for (int i = 0; i < plan.SegmentCount; i++) plan.CopySegment(i, buffer);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                plan.CopySegment(i % plan.SegmentCount, buffer);
                TrackValidator.Validate(buffer, 0, 18);
                plan.SampleRouteX(buffer.startZ + 12);
            }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        [Test]
        public void RecycledGateKeepsExactVisualAndColliderOpeningAndOneObstacleOwner()
        {
            var parent = new GameObject("GateGeometryTest");
            try
            {
                var template = new GameObject("Gate"); template.transform.SetParent(parent.transform, false);
                foreach (string side in new[] { "Left", "Right" })
                {
                    var wall = Primitives.Create(PrimitiveType.Cube, template.transform, Vector3.zero, Vector3.one, null, side);
                    wall.AddComponent<BoxCollider>().isTrigger = true;
                    Primitives.Create(PrimitiveType.Cube, template.transform, Vector3.zero, Vector3.one, null, side + "Bumper");
                }
                template.AddComponent<Obstacle>().SetKind(ObstacleKind.Wall);
                template.AddComponent<RouteGate>();
                var pool = new ObjectPool(template, parent.transform, 1);
                for (int pass = 0; pass < 2; pass++)
                {
                    var go = pool.Get(Vector3.zero, Quaternion.identity);
                    float centre = pass == 0 ? -1.9f : 1.9f;
                    go.GetComponent<RouteGate>().Setup(centre, 2.5f);
                    Physics.SyncTransforms();
                    var left = go.transform.Find("Left").GetComponent<BoxCollider>();
                    var right = go.transform.Find("Right").GetComponent<BoxCollider>();
                    Assert.That(right.bounds.min.x - left.bounds.max.x, Is.EqualTo(2.5f).Within(.001f));
                    Assert.That((right.bounds.min.x + left.bounds.max.x) * .5f, Is.EqualTo(centre).Within(.001f));
                    Assert.That(left.bounds.min.x, Is.EqualTo(-3.5f).Within(.001f));
                    Assert.That(right.bounds.max.x, Is.EqualTo(3.5f).Within(.001f));
                    Assert.That(left.bounds.size.z, Is.EqualTo(.85f).Within(.001f));
                    Assert.That(Vector3.Distance(left.GetComponent<MeshRenderer>().bounds.size, left.bounds.size), Is.LessThan(.001f));
                    Assert.That(left.GetComponentInParent<Obstacle>(), Is.SameAs(right.GetComponentInParent<Obstacle>()));
                    pool.Release(go); pool.Release(go);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }
    }
}
