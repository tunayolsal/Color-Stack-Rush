using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ColorStackRush.Tests
{
    public class WebSaveQueueTests
    {
        string directory;
        readonly Queue<Action<bool>> syncs = new Queue<Action<bool>>();
        GameObject managers;
        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ColorStackRushWebSave-" + Guid.NewGuid().ToString("N"));
            SaveManager.SetStorageDirectoryForTests(directory);
            SaveManager.SetStorageSyncForTests(callback => syncs.Enqueue(callback));
            managers = new GameObject("web-save-tests");
        }
        [TearDown] public void TearDown()
        {
            // Drain delayed requests even after a failed assertion, then restore the test seam.
            while (syncs.Count > 0) syncs.Dequeue()(true);
            SaveManager.EndTransaction();
            while (syncs.Count > 0) syncs.Dequeue()(true);
            SaveManager.SetStorageDirectoryForTests(null);
            UnityEngine.Object.DestroyImmediate(managers);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [Test] public void SaveSnapshotsAreSerializedAndNotReportedBeforeSync()
        {
            var completed = new List<int>();
            SaveManager.Data.coins = 12;
            SaveManager.SaveAsync(ok => { Assert.That(ok); completed.Add(12); });
            SaveManager.Data.coins = 23;
            SaveManager.SaveAsync(ok => { Assert.That(ok); completed.Add(23); });
            Assert.That(completed, Is.Empty);
            Assert.That(syncs.Count, Is.EqualTo(1));
            Assert.That(SaveStore.Load(SaveManager.FilePath).coins, Is.EqualTo(12));
            syncs.Dequeue()(true);
            Assert.That(completed, Is.EqualTo(new[] { 12 }));
            Assert.That(syncs.Count, Is.EqualTo(1));
            Assert.That(SaveStore.Load(SaveManager.FilePath).coins, Is.EqualTo(23));
            syncs.Dequeue()(true);
            Assert.That(completed, Is.EqualTo(new[] { 12, 23 }));
            Assert.That(SaveManager.IsSaving, Is.False);
        }
        [Test] public void FailedSyncRestoresPrimaryAndBackupAndCallsOnce()
        {
            SaveManager.Data.coins = 30;
            SaveManager.Save(); syncs.Dequeue()(true);
            string before = File.ReadAllText(SaveManager.FilePath);
            string backup = File.ReadAllText(SaveManager.FilePath + ".bak");
            int callbacks = 0;
            SaveManager.Data.coins = 99;
            SaveManager.SaveAsync(ok => { Assert.That(ok, Is.False); callbacks++; });
            var sync = syncs.Dequeue();
            sync(false); sync(false);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(File.ReadAllText(SaveManager.FilePath), Is.EqualTo(before));
            Assert.That(File.ReadAllText(SaveManager.FilePath + ".bak"), Is.EqualTo(backup));
        }
        [Test] public void PurchaseFailureRefundsOnlyPurchaseAndDefersBackgroundSave()
        {
            var shop = managers.AddComponent<ShopManager>();
            SaveManager.Data.coins = 200;
            SaveManager.Save(); syncs.Dequeue()(true);
            int callbacks = 0;
            Assert.That(shop.TryBuyAsync(1, ok => { Assert.That(ok, Is.False); callbacks++; }));
            Assert.That(SaveManager.IsTransactionPending);
            Assert.That(shop.TryBuyAsync(2, null), Is.False);
            SaveManager.Data.coins += 7; // unrelated earned coins must survive the refund
            SaveManager.Save(); // background request must wait until rollback, not snapshot the unlock
            Assert.That(syncs.Count, Is.EqualTo(1));
            syncs.Dequeue()(false);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(SaveManager.Data.coins, Is.EqualTo(207));
            Assert.That(ShopManager.IsUnlocked(1), Is.False);
            Assert.That(SaveManager.IsTransactionPending, Is.False);
            Assert.That(syncs.Count, Is.EqualTo(1));
            syncs.Dequeue()(true);
            Assert.That(SaveStore.Load(SaveManager.FilePath).coins, Is.EqualTo(207));
            Assert.That(SaveStore.Load(SaveManager.FilePath).unlockedSkins.Contains(1), Is.False);
        }
        [Test] public void DailyRewardWaitsForSyncAndFailureAllowsRetry()
        {
            var daily = managers.AddComponent<DailyRewardManager>();
            SaveManager.Data.coins = 40;
            int granted = -1;
            Assert.That(daily.ClaimAsync(value => granted = value));
            Assert.That(granted, Is.EqualTo(-1));
            Assert.That(daily.ClaimAsync(null), Is.False);
            syncs.Dequeue()(false);
            Assert.That(granted, Is.Zero);
            Assert.That(SaveManager.Data.coins, Is.EqualTo(40));
            Assert.That(SaveManager.Data.lastDailyClaim, Is.Empty);
            Assert.That(daily.ClaimAsync(value => granted = value));
            syncs.Dequeue()(true);
            Assert.That(granted, Is.EqualTo(DailyRewardManager.Rewards[0]));
            Assert.That(daily.CanClaim, Is.False);
        }
        [Test] public void ResetRequiresDurableSyncRollsBackAndReplacesBackup()
        {
            SaveManager.Data.coins = 315;
            SaveManager.Data.dailyStreak = 3;
            SaveManager.Save(); syncs.Dequeue()(true);
            int callbacks = 0;
            bool reset = false;
            Assert.That(SaveManager.DeleteAllAsync(ok => { reset = ok; callbacks++; }));
            Assert.That(callbacks, Is.Zero);
            Assert.That(SaveManager.IsTransactionPending);
            Assert.That(SaveManager.DeleteAllAsync(null), Is.False);
            syncs.Dequeue()(false);
            Assert.That(callbacks, Is.EqualTo(1));
            Assert.That(reset, Is.False);
            Assert.That(SaveManager.Data.coins, Is.EqualTo(315));
            Assert.That(SaveManager.Data.dailyStreak, Is.EqualTo(3));
            Assert.That(SaveStore.Load(SaveManager.FilePath).coins, Is.EqualTo(315));
            Assert.That(SaveManager.DeleteAllAsync(ok => { reset = ok; callbacks++; }));
            var sync = syncs.Dequeue();
            sync(true); sync(true);
            Assert.That(callbacks, Is.EqualTo(2));
            Assert.That(reset);
            Assert.That(SaveManager.Data.coins, Is.Zero);
            File.WriteAllText(SaveManager.FilePath, "corrupt");
            Assert.That(SaveStore.Load(SaveManager.FilePath).coins, Is.Zero);
            Assert.That(SaveStore.Load(SaveManager.FilePath).dailyStreak, Is.Zero);
        }
    }
}
