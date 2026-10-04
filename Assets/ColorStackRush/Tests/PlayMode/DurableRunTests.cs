using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ColorStackRush.Tests
{
    public class DurableRunTests
    {
        readonly Queue<Action<bool>> syncs = new Queue<Action<bool>>();
        Scene scene;
        string folder;

        [UnitySetUp] public IEnumerator Setup()
        {
            folder = Path.Combine(Path.GetTempPath(), "csr-durable-" + Guid.NewGuid());
            SaveManager.SetStorageDirectoryForTests(folder);
            SaveManager.Data.highestUnlockedLevel = 18;
            SaveManager.Data.tutorialCompleted = true;
            Assert.That(SaveManager.Save(), Is.True);
            scene = SceneManager.CreateScene("CSR-Durable");
            SceneManager.SetActiveScene(scene);
            new GameObject("Bootstrap").AddComponent<GameBootstrapper>();
            yield return null;
            SaveManager.SetStorageSyncForTests(callback => syncs.Enqueue(callback));
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            while (syncs.Count > 0) syncs.Dequeue()(true);
            SaveManager.EndTransaction();
            while (syncs.Count > 0) syncs.Dequeue()(true);
            yield return SceneManager.UnloadSceneAsync(scene);
            SaveManager.SetStorageDirectoryForTests(null);
            Time.timeScale = 1; Time.fixedDeltaTime = .02f;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        IEnumerator FinishUntilSync()
        {
            GameManager.Instance.StartRun(RunConfig.Level(18));
            GameEvents.RaiseFinishReached();
            float deadline = Time.realtimeSinceStartup + 6;
            while (syncs.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(syncs.Count, Is.EqualTo(1), "Finish never reached durable save");
        }

        [UnityTest] public IEnumerator FailedDurableResultRollsBackAndRetryUnlocks19ExactlyOnce()
        {
            int published = 0;
            Action<RunResult> count = _ => published++;
            GameEvents.RunCompleted += count;
            try
            {
                yield return FinishUntilSync();
                Assert.That(GameManager.Instance.ResultSavePending, Is.True);
                Assert.That(GameManager.Instance.ResultCommitted, Is.False);
                Assert.That(published, Is.Zero);
                var failedSync = syncs.Dequeue(); failedSync(false); failedSync(false);
                Assert.That(GameManager.Instance.ResultSaveFailed, Is.True);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.GameOver));
                Assert.That(SaveManager.Data.highestUnlockedLevel, Is.EqualTo(18));
                Assert.That(SaveStore.Load(SaveManager.FilePath).highestUnlockedLevel, Is.EqualTo(18));
                Assert.That(published, Is.Zero);
                GameManager.Instance.RetryResultSave();
                yield return null;
                Assert.That(syncs.Count, Is.EqualTo(1));
                var successfulSync = syncs.Dequeue(); successfulSync(true); successfulSync(true);
                Assert.That(GameManager.Instance.ResultCommitted, Is.True);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Victory));
                Assert.That(SaveManager.Data.highestUnlockedLevel, Is.EqualTo(19));
                Assert.That(published, Is.EqualTo(1));
                GameManager.Instance.ContinueLevel();
                Assert.That(GameManager.Instance.CurrentRun.levelId, Is.EqualTo(19));
            }
            finally { GameEvents.RunCompleted -= count; }
        }

        [UnityTest] public IEnumerator DisabledManagerCompletesDurableWriteWithoutPublishingStaleResult()
        {
            int published = 0;
            Action<RunResult> count = _ => published++;
            GameEvents.RunCompleted += count;
            try
            {
                yield return FinishUntilSync();
                var oldManager = GameManager.Instance;
                oldManager.enabled = false;
                Assert.That(GameManager.Instance, Is.Null);
                // A replacement manager represents the new active scene while
                // the old component still exists and its storage callback lives.
                var replacement = new GameObject("ReplacementManager").AddComponent<GameManager>();
                syncs.Dequeue()(true);
                Assert.That(SaveManager.IsTransactionPending, Is.False);
                Assert.That(SaveManager.Data.highestUnlockedLevel, Is.EqualTo(19));
                Assert.That(SaveStore.Load(SaveManager.FilePath).highestUnlockedLevel, Is.EqualTo(19));
                Assert.That(oldManager.ResultCommitted, Is.False);
                Assert.That(oldManager.State, Is.EqualTo(GameState.Finish));
                Assert.That(replacement.State, Is.EqualTo(GameState.MainMenu));
                Assert.That(published, Is.Zero);
            }
            finally { GameEvents.RunCompleted -= count; }
        }

        [UnityTest] public IEnumerator PendingResultCannotStartNewRunOrReopenResultAfterMenu()
        {
            int published = 0;
            Action<RunResult> count = _ => published++;
            GameEvents.RunCompleted += count;
            try
            {
                yield return FinishUntilSync();
                int id = GameManager.Instance.RunId;
                GameManager.Instance.StartRun(RunConfig.Level(18));
                Assert.That(GameManager.Instance.RunId, Is.EqualTo(id));
                GameManager.Instance.GoToMenu();
                syncs.Dequeue()(true);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.MainMenu));
                Assert.That(published, Is.Zero);
                while (syncs.Count > 0) syncs.Dequeue()(true);
                GameManager.Instance.StartRun(RunConfig.Level(19));
                Assert.That(GameManager.Instance.CurrentRun.levelId, Is.EqualTo(19));
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing));
            }
            finally { GameEvents.RunCompleted -= count; }
        }
    }
}
