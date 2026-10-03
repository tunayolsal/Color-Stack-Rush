using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using System.Text;
namespace ColorStackRush.Tests
{
    public class RunFlowTests
    {
        Scene scene;
        string folder;
        [UnitySetUp] public IEnumerator Setup()
        {
            folder = Path.Combine(Path.GetTempPath(), "csr-flow-" + Guid.NewGuid());
            SaveManager.SetStorageDirectoryForTests(folder);
            scene = SceneManager.CreateScene("CSR-Test"); SceneManager.SetActiveScene(scene);
            new GameObject("Bootstrap").AddComponent<GameBootstrapper>();
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(ps.gameObject);
            SaveManager.SetStorageDirectoryForTests(null);
            Time.timeScale = 1; Time.fixedDeltaTime = .02f;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        [UnityTest] public IEnumerator DeathLocksImmediatelyAndCommitsExactlyOnce()
        {
            int results = 0; Action<RunResult> count = _ => results++; GameEvents.RunCompleted += count;
            try
            {
                GameManager.Instance.StartRun(RunConfig.Endless(9));
                GameEvents.RaisePlayerDied(); GameEvents.RaisePlayerDied();
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Dying));
                float z = PlayerController.Instance.Distance; yield return new WaitForSecondsRealtime(.8f);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.GameOver)); Assert.That(results, Is.EqualTo(1)); Assert.That(PlayerController.Instance.Distance, Is.EqualTo(z));
            }
            finally { GameEvents.RunCompleted -= count; }
        }
        [UnityTest] public IEnumerator RapidRestartCancelsPreviousTerminalSequence()
        {
            int results = 0; Action<RunResult> count = _ => results++; GameEvents.RunCompleted += count;
            try
            {
                GameManager.Instance.StartRun(); GameEvents.RaisePlayerDied(); GameManager.Instance.StartRun(RunConfig.Endless(42));
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Playing)); Assert.That(results, Is.Zero); Assert.That(GameManager.Instance.ResultCommitted, Is.False);
            }
            finally { GameEvents.RunCompleted -= count; }
        }
        [UnityTest] public IEnumerator FinishCancellationCannotUnlockNextLevel()
        {
            GameManager.Instance.StartRun(); GameEvents.RaiseFinishReached();
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Finish)); GameManager.Instance.GoToMenu();
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.MainMenu)); Assert.That(SaveManager.Data.level, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator PauseFreezesPhysicsAndRestartRestoresStackLimit()
        {
            GameManager.Instance.StartRun(RunConfig.Endless(1)); yield return new WaitForFixedUpdate();
            GameManager.Instance.PauseGame(); float z = PlayerController.Instance.Distance;
            yield return new WaitForSecondsRealtime(.15f); Assert.That(PlayerController.Instance.Distance, Is.EqualTo(z));
            var stack = PlayerController.Instance.GetComponent<PlayerStack>();
            for (int i = 0; i < 50; i++) stack.AddBlock(Color.white);
            Assert.That(stack.Count, Is.EqualTo(32));
            stack.RemoveBlocks(10); GameManager.Instance.StartRun();
            yield return new WaitForSecondsRealtime(.7f); Assert.That(stack.Count, Is.GreaterThanOrEqualTo(4)); Assert.That(stack.Count, Is.LessThanOrEqualTo(32));
        }
        [UnityTest] public IEnumerator BackgroundingSavesEarnedCoinsWithoutRegrantingThem()
        {
            GameManager.Instance.StartRun(RunConfig.Endless(3));
            CurrencyManager.AddCoins(12);
            GameManager.Instance.SendMessage("OnApplicationPause", true);
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Paused));
            var saved = SaveStore.Load(Path.Combine(folder, "colorstackrush_save.json"));
            Assert.That(saved.coins, Is.EqualTo(12));
            GameManager.Instance.ResumeGame(); GameEvents.RaisePlayerDied();
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(SaveManager.Data.coins, Is.EqualTo(12));
        }
        [UnityTest] public IEnumerator ReusedPickupRestoresVisualPoseAndRootScale()
        {
            var parent = new GameObject("PoolTest"); var template = new GameObject("Template"); template.transform.parent = parent.transform;
            var visual = new GameObject("Visual"); visual.transform.SetParent(template.transform, false);
            visual.transform.localRotation = Quaternion.Euler(90, 0, 0); visual.transform.localScale = Vector3.one * .8f;
            template.AddComponent<CollectibleBlock>();
            var pool = new ObjectPool(template, parent.transform, 1);
            var item = pool.Get(Vector3.zero, Quaternion.identity);
            var child = item.transform.Find("Visual"); child.localRotation = Quaternion.Euler(0, 33, 0); child.localScale = Vector3.one * 2;
            item.transform.localScale = Vector3.one * 3; pool.Release(item);
            var reused = pool.Get(Vector3.one, Quaternion.identity);
            yield return null;
            Assert.That(reused, Is.SameAs(item)); Assert.That(reused.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Quaternion.Angle(child.localRotation, Quaternion.Euler(90, 0, 0)), Is.LessThan(.01f));
            Assert.That(child.localScale, Is.EqualTo(Vector3.one * .8f));
        }
        [UnityTest] public IEnumerator NightHudAndFinalActionRemainReadable()
        {
            SaveManager.Data.level = 18;
            GameManager.Instance.StartRun(RunConfig.Campaign(18));
            var hud = UnityEngine.Object.FindFirstObjectByType<HUDPanel>();
            var score = hud.transform.Find("Score").GetComponent<Text>();
            Assert.That(score.color.r, Is.GreaterThan(.9f));
            Assert.That(hud.transform.Find("Stack").GetComponent<Text>().text, Is.EqualTo("STACK 4 / 32"));
            Assert.That(hud.transform.Find("Progress").GetComponent<Text>().text, Does.Contain("0%"));
            yield return null;
            string snapshots = Argument("-snapshotPath");
            if (snapshots != null) Capture(snapshots, "night-hud");
            GameEvents.RaiseFinishReached();
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Victory));
            var panel = UnityEngine.Object.FindFirstObjectByType<VictoryPanel>();
            Assert.That(panel.transform.Find("Card/NextButton/Label").GetComponent<Text>().text, Is.EqualTo("BACK TO MENU"));
            if (snapshots != null) Capture(snapshots, "final-action");
        }
        [UnityTest, Timeout(360000)] public IEnumerator EveryCampaignRouteCanFinishThroughRealPhysicsAndInput()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var report = new StringBuilder("level,score,stars,stairs,runSeconds,wrongColors,obstacleHits\n");
            int wrong = 0, hits = 0;
            Action<bool, Vector3> collect = (correct, _) => { if (!correct) wrong++; };
            Action<Vector3> hit = _ => hits++;
            GameEvents.BlockCollected += collect; GameEvents.ObstacleHit += hit;
            string snapshots = Argument("-snapshotPath");
            try
            {
                if (snapshots != null)
                {
                    Capture(snapshots, "menu");
                    UIManager.Instance.OpenLevels(); yield return null; Capture(snapshots, "levels");
                    UIManager.Instance.CloseOverlays(); UIManager.Instance.OpenSettings(); yield return null; Capture(snapshots, "settings");
                    UIManager.Instance.CloseOverlays();
                }
                for (int level = 1; level <= 18; level++)
                {
                    wrong = hits = 0;
                    GameManager.Instance.StartRun(RunConfig.Campaign(level));
                    MouseState state = new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .45f) };
                    InputSystem.QueueStateEvent(mouse, state); yield return null; yield return null;
                    state = state.WithButton(MouseButton.Left);
                    InputSystem.QueueStateEvent(mouse, state); yield return null;
                    float target = 0, started = Time.realtimeSinceStartup, seconds = 0;
                    bool captured = false;
                    while (GameManager.Instance.State == GameState.Playing)
                    {
                        Assert.That(Time.realtimeSinceStartup - started, Is.LessThan(30), "Course stalled at level " + level);
                        seconds += Time.deltaTime;
                        Time.timeScale = 12; Time.fixedDeltaTime = .02f;
                        int index = Mathf.Max(0, Mathf.FloorToInt((PlayerController.Instance.Distance - TrackPlanner.FirstZ) / TrackSegment.Length));
                        float nextTarget = TrackPlanner.SafeX(index);
                        state.position.x += (nextTarget - target) * Screen.width / 8.5f;
                        target = nextTarget;
                        InputSystem.QueueStateEvent(mouse, state);
                        if (!captured && PlayerController.Instance.Distance > 45 && snapshots != null && (level == 1 || level == 7 || level == 13))
                        { Capture(snapshots, "world-" + ((level - 1) / 6 + 1)); captured = true; }
                        yield return null;
                    }
                    Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Finish), "Safe route died at level " + level);
                    Time.timeScale = 1; Time.fixedDeltaTime = .02f;
                    while (GameManager.Instance.State == GameState.Finish) yield return null;
                    Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Victory));
                    var result = GameManager.Instance.LastResult;
                    Assert.That(result.config.level, Is.EqualTo(level)); Assert.That(result.stars, Is.GreaterThanOrEqualTo(1));
                    Assert.That(wrong, Is.Zero, "Wrong color on safe route " + level); Assert.That(hits, Is.Zero, "Obstacle on safe route " + level);
                    Assert.That(SaveManager.Data.level, Is.EqualTo(Mathf.Min(18, level + 1)));
                    report.AppendLine($"{level},{result.score},{result.stars},{result.stairs},{seconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},{wrong},{hits}");
                    if (snapshots != null && level == 18) Capture(snapshots, "victory");
                }
                Assert.That(SaveManager.Data.campaignCompleted, Is.True);
                if (snapshots != null) File.WriteAllText(Path.Combine(snapshots, "campaign-physics.csv"), report.ToString());
            }
            finally
            {
                GameEvents.BlockCollected -= collect; GameEvents.ObstacleHit -= hit;
                InputSystem.RemoveDevice(mouse); Time.timeScale = 1; Time.fixedDeltaTime = .02f;
            }
        }
        static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
        static void Capture(string directory, string name)
        {
            Directory.CreateDirectory(directory);
            var camera = Camera.main;
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var target = new RenderTexture(540, 960, 24);
            var pixels = new Texture2D(540, 960, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 540, 960), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            UnityEngine.Object.Destroy(pixels); target.Release(); UnityEngine.Object.Destroy(target);
        }
    }
}
