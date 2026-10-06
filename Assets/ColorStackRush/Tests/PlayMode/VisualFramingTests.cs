using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ColorStackRush.Tests
{
    public class VisualFramingTests
    {
        Scene scene;
        string folder;
        [UnitySetUp] public IEnumerator Setup()
        {
            folder = Path.Combine(Path.GetTempPath(), "csr-frame-" + Guid.NewGuid());
            SaveManager.SetStorageDirectoryForTests(folder);
            SaveManager.Data.reducedMotion = true;
            SaveManager.Save();
            scene = SceneManager.CreateScene("CSR-Framing");
            SceneManager.SetActiveScene(scene);
            new GameObject("Bootstrap").AddComponent<GameBootstrapper>();
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            yield return SceneManager.UnloadSceneAsync(scene);
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(ps.gameObject);
            SaveManager.SetStorageDirectoryForTests(null);
            Time.timeScale = 1; Time.fixedDeltaTime = .02f;
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        [UnityTest] public IEnumerator NormalSpeedPortraitKeepsPlayerAndRoadReadable()
        {
            GameManager.Instance.StartRun(RunConfig.Level(1));
            yield return new WaitForSecondsRealtime(.6f);
            var camera = Camera.main;
            var ball = PlayerController.Instance.transform.Find("Ball").GetComponent<MeshRenderer>();
            Assert.That(camera.fieldOfView, Is.EqualTo(45).Within(.01f));
            Assert.That(ball.bounds.size.x, Is.InRange(.94f, .96f));
            float originalAspect = camera.aspect;
            camera.aspect = 540f / 960;
            float screenY = camera.WorldToViewportPoint(ball.bounds.center).y;
            Assert.That(screenY, Is.InRange(.35f, .46f), "Player should sit near 58% from the top.");
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            long triangles = 0;
            int visibleMeshes = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                visibleMeshes++;
                var mesh = filter.sharedMesh;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles) triangles += mesh.GetIndexCount(i) / 3;
            }
            camera.aspect = originalAspect;
            Debug.Log($"[CSR Framing] portrait visibleTriangles={triangles}, visibleMeshRenderers={visibleMeshes}, playerViewportY={screenY:F3}, playerDiameter={ball.bounds.size.x:F3}");
            Assert.That(triangles, Is.LessThanOrEqualTo(100000));
        }

        [UnityTest] public IEnumerator RestartSnapsCameraBeforeTheFirstRenderedFrame()
        {
            var follow = CameraFollow.Instance;
            var shaker = CameraShake.Instance;
            var initialPosition = follow.transform.position;
            SaveManager.Data.reducedMotion = false;
            // Model the elevated end of the previous stairs and active feedback.
            PlayerController.Instance.transform.position = new Vector3(2, 8, 300);
            follow.transform.position += new Vector3(2, 8, 300);
            follow.Punch(.18f); shaker.Shake(.6f);
            shaker.transform.localPosition = Vector3.one * .2f;
            shaker.transform.localRotation = Quaternion.Euler(0, 0, 4);
            GameManager.Instance.StartRun(RunConfig.Level(1));

            Assert.That(PlayerController.Instance.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(Vector3.Distance(follow.transform.position, initialPosition), Is.LessThan(.001f));
            Assert.That(shaker.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(shaker.transform.localRotation, Quaternion.identity), Is.LessThan(.001f));
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(follow.transform.position.y, Is.EqualTo(initialPosition.y).Within(.005f), "A previous bounce must not resume after reset.");
            Assert.That(shaker.transform.localPosition, Is.EqualTo(Vector3.zero), "Old trauma must not resume after reset.");
        }

        [UnityTest] public IEnumerator HudControlsDoNotOverlapAndColorNameIsCentered()
        {
            GameManager.Instance.StartRun(RunConfig.Level(1));
            Canvas.ForceUpdateCanvases();
            var hud = UnityEngine.Object.FindFirstObjectByType<HUDPanel>().transform;
            Rect pause = WorldRect(hud.Find("Pause"));
            Rect progress = WorldRect(hud.Find("ProgressTrack"));
            Rect wallet = WorldRect(hud.Find("Wallet"));
            Rect coin = WorldRect(hud.Find("Coin"));
            Rect swatch = WorldRect(hud.Find("ActiveColor"));
            Rect label = WorldRect(hud.Find("Collect"));
            Rect header = WorldRect(hud.Find("Header"));
            Rect progressText = WorldRect(hud.Find("Progress"));
            Assert.That(pause.Overlaps(progress), Is.False, "Pause must be clear of the progress bar.");
            Assert.That(wallet.Overlaps(coin), Is.False, "Coin symbol must not cover the wallet amount.");
            Assert.That(label.center.x, Is.EqualTo(swatch.center.x).Within(.01f));
            Assert.That(header.Contains(progressText.min) && header.Contains(progressText.max), Is.True,
                "Progress text must stay on the bright HUD card in every theme.");
            Rect safe = WorldRect(hud.parent);
            foreach (string name in new[] { "Pause", "ProgressTrack", "Wallet", "Coin", "Collect", "Score", "Progress" })
            {
                var bounds = WorldRect(hud.Find(name));
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(safe.xMin - .01f), name);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(safe.xMax + .01f), name);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(safe.yMax + .01f), name);
            }
            Assert.That(hud.Find("Collect").GetComponent<Text>().fontSize, Is.GreaterThanOrEqualTo(32));
            yield return null;
        }

        [UnityTest] public IEnumerator StartingTrailUsesActiveColorWithoutRecoloringHistory()
        {
            SaveManager.Data.highestUnlockedLevel = 13;
            foreach (long level in new long[] { 1, 7, 13 })
            {
                GameManager.Instance.StartRun(RunConfig.Level(level));
                var trail = GameObject.Find("StackBlocks").transform;
                int activeCount = 0;
                var startingBlocks = new HashSet<Transform>();
                Color activeColor = ColorPalette.Get(ColorManager.Instance.ActiveColor);
                foreach (Transform block in trail)
                {
                    if (!block.gameObject.activeSelf) continue;
                    activeCount++;
                    startingBlocks.Add(block);
                    AssertColorChannels(block.GetComponent<MeshRenderer>().sharedMaterial.color, activeColor, "Initial trail color at level " + level);
                }
                Assert.That(activeCount, Is.EqualTo(4));
                Color historicalColor = ColorPalette.Get(GameColor.Green);
                PlayerController.Instance.GetComponent<PlayerStack>().AddBlock(historicalColor);
                var recordedColors = new Dictionary<Transform, Color>();
                Transform historicalBlock = null;
                foreach (Transform block in trail)
                {
                    if (!block.gameObject.activeSelf) continue;
                    Color color = block.GetComponent<MeshRenderer>().sharedMaterial.color;
                    recordedColors.Add(block, color);
                    if (!startingBlocks.Contains(block)) historicalBlock = block;
                }
                Assert.That(recordedColors.Count, Is.EqualTo(5));
                Assert.That(historicalBlock, Is.Not.Null, "Collection must add a new history block.");
                AssertColorChannels(recordedColors[historicalBlock], historicalColor, "Collected history color at level " + level);
                GameEvents.RaiseActiveColorChanged(GameColor.Blue);
                foreach (var recorded in recordedColors)
                {
                    Assert.That(recorded.Key.gameObject.activeSelf, Is.True);
                    AssertColorChannels(recorded.Key.GetComponent<MeshRenderer>().sharedMaterial.color, recorded.Value,
                        "A color change must not repaint existing trail blocks at level " + level);
                }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator WrongColorPopupMatchesTheDamageProfile()
        {
            GameManager.Instance.StartRun(RunConfig.Level(1));
            GameEvents.RaiseBlockCollected(false, Vector3.zero);
            string expected = "-" + GameManager.Instance.CurrentRun.Difficulty.WrongColorDamage;
            bool found = false;
            foreach (var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                if (text.gameObject.activeInHierarchy && text.text == expected) found = true;
            Assert.That(found, Is.True, "Wrong-color feedback must show the configured block loss.");
            yield return null;
        }

        static Rect WorldRect(Transform transform)
        {
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static void AssertColorChannels(Color actual, Color expected, string context)
        {
            // Material colors round-trip through Unity's native shader representation.
            const float tolerance = .00001f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), context + " (red)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), context + " (green)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), context + " (blue)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance), context + " (alpha)");
        }
    }
}
