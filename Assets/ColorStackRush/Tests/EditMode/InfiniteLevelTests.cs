using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ColorStackRush.Tests
{
    public class InfiniteLevelTests
    {
        [TestCase(1L, 10f, 3)]
        [TestCase(6L, 12f, 3)]
        [TestCase(7L, 12.5f, 4)]
        [TestCase(12L, 14f, 4)]
        [TestCase(13L, 14.5f, 5)]
        [TestCase(18L, 16f, 5)]
        [TestCase(1000000L, 16f, 5)]
        public void DifficultyRampsThenCapsWithoutIncreasingDamage(long level, float speed, int recovery)
        {
            var c = RunConfig.Level(level);
            Assert.That(c.BaseSpeed, Is.EqualTo(speed).Within(.0001f));
            Assert.That(c.MaxSpeed, Is.LessThanOrEqualTo(18));
            Assert.That(c.Difficulty.RecoveryPeriod, Is.EqualTo(recovery));
            Assert.That(c.Difficulty.ObstacleDamage, Is.EqualTo(3));
            Assert.That(c.Difficulty.WrongColorDamage, Is.EqualTo(2));
            Assert.That(c.Difficulty.InvincibilitySeconds, Is.EqualTo(.8f));
        }

        [Test]
        public void LargeLevelIdsHaveFiniteBoundedCoursesAndCyclingThemes()
        {
            long[] ids = { 19, 20, 100, 1000, 1000000, (1L << 32) + 19, long.MaxValue };
            foreach (long id in ids)
            {
                var c = RunConfig.Level(id);
                Assert.That(c.levelId, Is.EqualTo(id));
                Assert.That(c.Length, Is.InRange(480, 624));
                Assert.That(c.Length % TrackSegment.Length, Is.Zero);
                Assert.That(c.Theme, Is.InRange(0, 2));
                Assert.That(c.seed, Is.EqualTo(RunConfig.Level(id).seed));
            }
            Assert.That(RunConfig.Level(19).Theme, Is.Zero);
            Assert.That(RunConfig.Level(25).Theme, Is.EqualTo(1));
            Assert.That(RunConfig.Level(31).Theme, Is.EqualTo(2));
            Assert.That(RunConfig.Level(37).Theme, Is.Zero);
            Assert.That(RunConfig.Level(1).seed, Is.Not.EqualTo(RunConfig.Level((1L << 32) + 1).seed));
            Assert.That(RunConfig.Level(0).levelId, Is.EqualTo(1));
        }

        [Test]
        public void V2MigrationPreservesRecordsAndOpensNineteenForCompletedCampaign()
        {
            var old = new SaveData { version = 2, level = 18, campaignCompleted = true, coins = 246,
                highScore = 7890, endlessBest = 420, endlessDistance = 1234, tutorialCompleted = true,
                reducedMotion = true, lowQuality = true, selectedSkin = 2, musicVolume = .4f };
            old.unlockedSkins.Add(2);
            old.levelScores[0] = 250;
            old.levelStars[0] = 2;
            old.levelScores[17] = 1200;
            old.levelStars[17] = 3;
            var d = SaveStore.Normalize(old);
            Assert.That(d.version, Is.EqualTo(3));
            Assert.That(d.highestUnlockedLevel, Is.EqualTo(19));
            Assert.That(Progression.BestScore(d, 1), Is.EqualTo(250));
            Assert.That(Progression.Stars(d, 18), Is.EqualTo(3));
            Assert.That(d.levelRecords.Count, Is.EqualTo(2));
            Assert.That(d.coins, Is.EqualTo(246));
            Assert.That(d.endlessBest, Is.EqualTo(420));
            Assert.That(d.endlessDistance, Is.EqualTo(1234));
            Assert.That(d.highScore, Is.EqualTo(7890));
            Assert.That(d.selectedSkin, Is.EqualTo(2));
            Assert.That(d.musicVolume, Is.EqualTo(.4f));
            Assert.That(d.tutorialCompleted && d.reducedMotion && d.lowQuality, Is.True);
            Assert.That(Progression.BestScore(d, 19), Is.Zero);
            Assert.That(SaveStore.Normalize(d).levelRecords.Count, Is.EqualTo(2));
        }

        [Test]
        public void IncompleteLegacyProgressStaysAtCurrentLevelAndRecordsRemainSparse()
        {
            var d = SaveStore.Normalize(new SaveData { level = 9, coins = 70, highScore = 9900 });
            Assert.That(d.highestUnlockedLevel, Is.EqualTo(9));
            Assert.That(d.levelRecords, Is.Empty);
            long id = (1L << 32) + 21;
            d.highestUnlockedLevel = id;
            Progression.Apply(d, new RunResult { config = RunConfig.Level(id), completed = true, stars = 2, score = 300, coins = 100 });
            Assert.That(d.highestUnlockedLevel, Is.EqualTo(id + 1));
            Assert.That(d.levelRecords.Count, Is.EqualTo(1));
            Assert.That(Progression.BestScore(d, id), Is.EqualTo(300));
            Assert.That(Progression.Stars(d, id), Is.EqualTo(2));
            Assert.That(d.coins, Is.EqualTo(70));
            Assert.That(d.highScore, Is.EqualTo(9900));
        }

        [Test]
        public void DuplicateResultsAndFailedRunsDoNotRegrantProgressOrStars()
        {
            var d = SaveStore.Normalize(new SaveData());
            var win = new RunResult { config = RunConfig.Level(18), completed = true, score = 100, stars = 3, coins = 50 };
            Progression.Apply(d, win);
            Progression.Apply(d, win);
            Progression.Apply(d, new RunResult { config = RunConfig.Level(18), completed = false, score = 200, stars = 3 });
            Assert.That(d.highestUnlockedLevel, Is.EqualTo(19));
            Assert.That(d.levelRecords.Count, Is.EqualTo(1));
            Assert.That(Progression.Stars(d, 18), Is.EqualTo(3));
            Assert.That(Progression.BestScore(d, 18), Is.EqualTo(200));
            Assert.That(d.coins, Is.Zero);
        }

        [Test]
        public void ProgressionSnapshotRollsBackBothNewAndExistingRecordChanges()
        {
            var d = SaveStore.Normalize(new SaveData());
            var before = new Progression.Snapshot(d, 1);
            Progression.Apply(d, new RunResult { config = RunConfig.Level(1), completed = true, stars = 3, score = 500 });
            before.Restore(d);
            Assert.That(d.highestUnlockedLevel, Is.EqualTo(1));
            Assert.That(d.levelRecords, Is.Empty);
            Progression.Apply(d, new RunResult { config = RunConfig.Level(1), completed = true, stars = 1, score = 50 });
            before = new Progression.Snapshot(d, 1);
            Progression.Apply(d, new RunResult { config = RunConfig.Level(1), completed = true, stars = 3, score = 500 });
            SaveStore.Normalize(d);
            before.Restore(d);
            Assert.That(Progression.Stars(d, 1), Is.EqualTo(1));
            Assert.That(Progression.BestScore(d, 1), Is.EqualTo(50));
            Assert.That(d.highestUnlockedLevel, Is.EqualTo(2));
        }

        [Test]
        public void NormalizationMergesSparseDuplicatesAndLargeIdsRoundTrip()
        {
            long id = (1L << 32) + 19;
            var d = new SaveData { version = 3, highestUnlockedLevel = id };
            d.levelRecords.Add(new LevelRecord { levelId = id, bestScore = 300, bestStars = 2 });
            d.levelRecords.Add(new LevelRecord { levelId = id, bestScore = 500, bestStars = 1 });
            d.levelRecords.Add(null);
            d.levelRecords.Add(new LevelRecord { levelId = -1, bestScore = 100 });
            SaveStore.Normalize(d);
            var reloaded = new SaveData();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(d), reloaded);
            SaveStore.Normalize(reloaded);
            Assert.That(reloaded.levelRecords.Count, Is.EqualTo(1));
            Assert.That(Progression.BestScore(reloaded, id), Is.EqualTo(500));
            Assert.That(Progression.Stars(reloaded, id), Is.EqualTo(2));
            Assert.That(reloaded.highestUnlockedLevel, Is.EqualTo(id + 1));
        }

        [Test]
        public void ThousandFiniteLevelsRepeatDeterministicallyWithReachableOffCentreRoutes()
        {
            var a = new TrackSegment();
            var b = new TrackSegment();
            int centreHazards = 0;
            for (long level = 1; level <= 1000; level++)
            {
                var config = RunConfig.Level(level);
                int powerCount = 0;
                for (int i = 0; TrackPlanner.FirstZ + (i + 1) * TrackSegment.Length < config.Length; i++)
                {
                    TrackPlanner.Fill(config, i, a);
                    TrackPlanner.Fill(config, i, b);
                    Assert.That(TrackValidator.Validate(a, TrackPlanner.SafeX(config, i - 1), 18), Is.True, $"level {level} segment {i}");
                    Assert.That(a.safeX, Is.EqualTo(b.safeX));
                    Assert.That(a.count, Is.EqualTo(b.count));
                    Assert.That(a.pattern, Is.EqualTo(b.pattern));
                    Assert.That(Mathf.Abs(a.safeX), Is.LessThanOrEqualTo(2.1f));
                    if (a.pattern == TrackPattern.ColorTransition) Assert.That(a.count, Is.Zero);
                    for (int j = 0; j < a.count; j++)
                    {
                        Assert.That(a.items[j].x, Is.EqualTo(b.items[j].x));
                        Assert.That(a.items[j].phase, Is.EqualTo(b.items[j].phase));
                        Assert.That(a.items[j].color, Is.EqualTo(b.items[j].color));
                        if (a.items[j].kind == TrackKind.PowerUp) powerCount++;
                        if (a.items[j].kind == TrackKind.Wall && Mathf.Abs(a.items[j].x) < .7f) centreHazards++;
                        if (level == 1) Assert.That(a.items[j].kind, Is.Not.EqualTo(TrackKind.Slider).And.Not.EqualTo(TrackKind.Spinner));
                    }
                }
                Assert.That(powerCount, Is.LessThanOrEqualTo(1));
                if (level <= 2) Assert.That(powerCount, Is.Zero);
            }
            Assert.That(centreHazards, Is.GreaterThan(100));
            Assert.That(TrackPlanner.WarningDistance / 18f, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void PlannerAndValidatorRemainAllocationFreeAfterWarmup()
        {
            var c = RunConfig.Level(1000000);
            var segment = new TrackSegment();
            bool valid = true;
            for (int i = 0; i < 100; i++) { TrackPlanner.Fill(c, i, segment); valid &= TrackValidator.Validate(segment, TrackPlanner.SafeX(c, i - 1), 18); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) { TrackPlanner.Fill(c, i, segment); valid &= TrackValidator.Validate(segment, TrackPlanner.SafeX(c, i - 1), 18); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(valid, Is.True);
            Assert.That(bytes, Is.Zero);
        }
    }
}
