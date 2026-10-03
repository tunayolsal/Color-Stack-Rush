using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
namespace ColorStackRush.Tests
{
    public class QualityTests
    {
        string folder, path;
        [SetUp] public void Setup() { folder = Path.Combine(Path.GetTempPath(), "csr-tests-" + Guid.NewGuid()); Directory.CreateDirectory(folder); path = Path.Combine(folder, "save.json"); }
        [TearDown] public void Cleanup() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        [Test] public void LegacySavePreservesWalletSkinsSettingsAndRecord()
        {
            File.WriteAllText(path, "{\"coins\":246,\"highScore\":7890,\"level\":9,\"unlockedSkins\":[0,2],\"selectedSkin\":2,\"musicVolume\":0.4,\"hapticsOn\":false}");
            var d = SaveStore.Load(path);
            Assert.That(d.version, Is.EqualTo(2)); Assert.That(d.coins, Is.EqualTo(246)); Assert.That(d.highScore, Is.EqualTo(7890));
            Assert.That(d.level, Is.EqualTo(9)); Assert.That(d.selectedSkin, Is.EqualTo(2)); Assert.That(d.unlockedSkins, Does.Contain(2));
            Assert.That(d.musicVolume, Is.EqualTo(.4f)); Assert.That(d.hapticsOn, Is.False); Assert.That(d.endlessBest, Is.Zero); Assert.That(d.levelScores[0], Is.Zero);
            Assert.That(d.sfxVolume, Is.EqualTo(1));
        }
        [Test] public void CorruptPrimaryRecoversLastGoodSaveAndSurvivesAnotherWrite()
        {
            var d = SaveStore.Normalize(new SaveData { coins = 100 }); SaveStore.Save(path, d);
            d.coins = 200; SaveStore.Save(path, d);
            File.WriteAllText(path, "{broken");
            var recovered = SaveStore.Load(path); Assert.That(recovered.coins, Is.EqualTo(100));
            SaveStore.Save(path, recovered); Assert.That(SaveStore.Load(path).coins, Is.EqualTo(100));
            File.WriteAllText(path, "{broken again"); Assert.That(SaveStore.Load(path).coins, Is.EqualTo(100));
        }
        [Test] public void SuccessfulSaveLeavesNoTemporaryFile() { SaveStore.Save(path, new SaveData()); Assert.That(File.Exists(path), Is.True); Assert.That(File.Exists(path + ".bak"), Is.True); Assert.That(File.Exists(path + ".tmp"), Is.False); }
        [Test] public void ModesNeverMixRecordsOrRegrantCoins()
        {
            var d = SaveStore.Normalize(new SaveData { coins = 50, highScore = 9000 });
            Progression.Apply(d, new RunResult { config = RunConfig.Endless(7), score = 400, distance = 123 });
            Progression.Apply(d, new RunResult { config = RunConfig.Campaign(1), score = 250, stars = 2, completed = true, coins = 100 });
            Assert.That(d.endlessBest, Is.EqualTo(400)); Assert.That(d.endlessDistance, Is.EqualTo(123)); Assert.That(d.levelScores[0], Is.EqualTo(250));
            Assert.That(d.level, Is.EqualTo(2)); Assert.That(d.levelStars[0], Is.EqualTo(2)); Assert.That(d.coins, Is.EqualTo(50)); Assert.That(d.highScore, Is.EqualTo(9000));
        }
        [TestCase(7, 1)] [TestCase(8, 2)] [TestCase(13, 2)] [TestCase(14, 3)] public void StarThresholds(int stairs, int stars) => Assert.That(RunResult.StarsFor(stairs), Is.EqualTo(stars));
        [Test] public void AllCampaignSeedsAnd1000EndlessSeedsHaveReachableRoutes()
        {
            var a = new TrackSegment(); var b = new TrackSegment();
            for (int seed = 0; seed < 1000; seed++) ValidateRun(RunConfig.Endless(seed), 150, a, b);
            for (int level = 1; level <= 18; level++) ValidateRun(RunConfig.Campaign(level), 25, a, b);
        }
        static void ValidateRun(RunConfig c, int count, TrackSegment a, TrackSegment b)
        {
            for (int i = 0; i < count; i++)
            {
                TrackPlanner.Fill(c, i, a); TrackPlanner.Fill(c, i, b);
                Assert.That(TrackValidator.Validate(a, TrackPlanner.SafeX(i - 1), c.MaxSpeed), Is.True, $"seed {c.seed} segment {i}");
                Assert.That(a.count, Is.EqualTo(b.count)); Assert.That(a.pattern, Is.EqualTo(b.pattern));
                for (int j = 0; j < a.count; j++) { Assert.That(a.items[j].x, Is.EqualTo(b.items[j].x)); Assert.That(a.items[j].phase, Is.EqualTo(b.items[j].phase)); Assert.That(a.items[j].color, Is.EqualTo(b.items[j].color)); }
            }
        }
        [Test] public void ColorTransitionHasNoPickupsOrHazardsAndWarningAtLeastTwoSeconds()
        {
            var buffer = new TrackSegment(); var c = RunConfig.Endless(13);
            Assert.That(TrackPlanner.WarningDistance / c.MaxSpeed, Is.GreaterThanOrEqualTo(2));
            for (int i = 0; i < 100; i++) { TrackPlanner.Fill(c, i, buffer); if (buffer.pattern == TrackPattern.ColorTransition) Assert.That(buffer.count, Is.Zero); }
            Assert.That(TrackPlanner.ColorAtDistance(c, 219), Is.Not.EqualTo(TrackPlanner.ColorAtDistance(c, 220)));
        }
        [Test] public void SweptObstacleInSafeCorridorIsRejectedAndFallbackPasses()
        {
            var a = new TrackSegment(); a.Add(TrackKind.Slider, 0, 12, .95f);
            Assert.That(TrackValidator.Validate(a, 0, 18), Is.False); TrackPlanner.Fallback(a); Assert.That(TrackValidator.Validate(a, 0, 18), Is.True);
        }
        [Test] public void ClockRollbackNeverRegrantsDailyReward()
        {
            var day = new DateTime(2026, 10, 4);
            Assert.That(DailyRewardManager.CanClaimOn("2026-10-04", day), Is.False);
            Assert.That(DailyRewardManager.CanClaimOn("2026-10-05", day), Is.False);
            Assert.That(DailyRewardManager.CanClaimOn("2026-10-03", day), Is.True);
            Assert.That(DailyRewardManager.CanClaimOn("", day), Is.True);
        }
        [Test] public void PlannerAndValidatorAllocateNothingAfterWarmup()
        {
            var c = RunConfig.Endless(77); var segment = new TrackSegment(); bool valid = true;
            for (int i = 0; i < 100; i++) { TrackPlanner.Fill(c, i, segment); valid &= TrackValidator.Validate(segment, TrackPlanner.SafeX(i - 1), 18); }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) { TrackPlanner.Fill(c, i, segment); valid &= TrackValidator.Validate(segment, TrackPlanner.SafeX(i - 1), 18); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(valid, Is.True); Assert.That(bytes, Is.Zero);
        }
        [Test] public void DuplicatePoolReleaseCannotRentSameObjectTwice()
        {
            var parent = new GameObject("PoolTest"); var template = new GameObject("Template"); template.transform.parent = parent.transform;
            var pool = new ObjectPool(template, parent.transform, 2);
            var first = pool.Get(Vector3.zero, Quaternion.identity); pool.Release(first); pool.Release(first);
            var a = pool.Get(Vector3.zero, Quaternion.identity); var b = pool.Get(Vector3.zero, Quaternion.identity);
            Assert.That(a, Is.Not.SameAs(b)); UnityEngine.Object.DestroyImmediate(parent);
        }
        [Test] public void ShopDebitUnlockAndSelectionPersistTogether()
        {
            var shop = new GameObject("ShopTest").AddComponent<ShopManager>(); SaveManager.SetStorageDirectoryForTests(folder);
            try
            {
                SaveManager.Data.coins = 200;
                Assert.That(shop.TryBuy(1), Is.True); Assert.That(shop.TryBuy(1), Is.False);
                var saved = SaveStore.Load(SaveManager.FilePath);
                Assert.That(saved.coins, Is.EqualTo(100)); Assert.That(saved.unlockedSkins, Does.Contain(1)); Assert.That(saved.selectedSkin, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(shop.gameObject); SaveManager.SetStorageDirectoryForTests(null); }
        }
        [Test] public void FailedShopSaveRollsBackAllPurchaseChanges()
        {
            var shop = new GameObject("ShopTest").AddComponent<ShopManager>(); var blocked = Path.Combine(folder, "not-a-directory"); File.WriteAllText(blocked, "file"); SaveManager.SetStorageDirectoryForTests(blocked);
            try
            {
                SaveManager.Data.coins = 200;
                LogAssert.Expect(LogType.Error, new Regex("\\[SaveManager\\] Save failed:.*"));
                Assert.That(shop.TryBuy(1), Is.False); Assert.That(SaveManager.Data.coins, Is.EqualTo(200));
                Assert.That(SaveManager.Data.unlockedSkins.Contains(1), Is.False); Assert.That(SaveManager.Data.selectedSkin, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(shop.gameObject); SaveManager.SetStorageDirectoryForTests(null); }
        }
    }
}
