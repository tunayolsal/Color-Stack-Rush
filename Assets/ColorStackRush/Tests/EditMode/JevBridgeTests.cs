#if CSR_JEV_TEST
using System.Collections.Generic;
using ColorStackRush.Testing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush.Tests
{
    public class JevBridgeTests
    {
        JevSnapshot Snapshot() => new JevSnapshot { runId = 7, snapshotId = 3, phase = "Playing", capturedRealtime = 10,
            observation = new JevObservation { activeColor = "Pink" } };
        JevDecision Decision() => new JevDecision { runId = 7, snapshotId = 3, phase = "Playing", activeColor = "Pink", model = "jev-1.13.0", action = "right_large" };
        [Test] public void FreshChoiceAcceptsAndStaleStateRejects()
        {
            var snapshot = Snapshot(); var decision = Decision();
            Assert.That(JevDecisionPolicy.TryAccept(snapshot, decision, 7, GameState.Playing, "Pink", 10.499f, out _), Is.True);
            Assert.That(JevDecisionPolicy.TryAccept(snapshot, decision, 7, GameState.Playing, "Pink", 10.501f, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("stale_time"));
            Assert.That(JevDecisionPolicy.TryAccept(snapshot, decision, 8, GameState.Playing, "Pink", 10.1f, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("stale_run"));
            Assert.That(JevDecisionPolicy.TryAccept(snapshot, decision, 7, GameState.Paused, "Pink", 10.1f, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("stale_phase"));
            Assert.That(JevDecisionPolicy.TryAccept(snapshot, decision, 7, GameState.Playing, "Blue", 10.1f, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("stale_color"));
        }
        [Test] public void UnknownActionsAndOtherModelsCannotControlPlayer()
        {
            var snapshot = Snapshot(); var decision = Decision(); decision.action = "teleport";
            Assert.That(JevDecisionPolicy.TryAccept(snapshot, decision, 7, GameState.Playing, "Pink", 10.1f, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("invalid_action"));
            decision.action = "hold"; decision.model = "JevUltrafast";
            Assert.That(JevDecisionPolicy.TryAccept(snapshot, decision, 7, GameState.Playing, "Pink", 10.1f, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("unexpected_model"));
        }
        [Test] public void ObservationSchemaContainsNoPlannerInformation()
        {
            var json = JsonUtility.ToJson(new JevObservation { activeColor = "Blue", visible = new[] { new JevVisibleObject { kind = "block", color = "Blue", ahead = 5 } } });
            foreach (string forbidden in new[] { "seed", "SafeX", "RoutePoints", "CoursePlan", "OpeningGates", "phase" })
                Assert.That(json, Does.Not.Contain(forbidden));
        }
        [Test] public void InjectedMistakeUsesVisibleBlockedLaneFromCurrentlySafePosition()
        {
            var observation = new JevObservation { playerX = 1.9f, visible = new[] {
                new JevVisibleObject { kind = "obstacle", x = -1.425f, width = 4.15f, ahead = 6 },
                new JevVisibleObject { kind = "obstacle", x = 3.325f, width = .35f, ahead = 6 } } };
            Assert.That(JevDecisionPolicy.TryPlanMistake(observation, out var action, out var wall, out float endpoint), Is.True);
            Assert.That(action, Is.EqualTo("left_large"));
            Assert.That(endpoint, Is.EqualTo(.55f).Within(.001f));
            Assert.That(endpoint - .55f, Is.LessThan(wall.x + wall.width * .5f));
            observation.playerX = 0;
            Assert.That(JevDecisionPolicy.TryPlanMistake(observation, out _, out _, out _), Is.False, "Already blocked player must recover without calling the correct move a test mistake");
        }
        [Test] public void FullyOccludedAndHudCoveredGeometryIsExcluded()
        {
            var cameraObject = new GameObject("TestCamera"); var target = GameObject.CreatePrimitive(PrimitiveType.Cube); var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var camera = cameraObject.AddComponent<Camera>(); camera.transform.position = Vector3.zero;
                target.transform.position = new Vector3(0, 0, 10); cover.transform.position = new Vector3(0, 0, 5); cover.transform.localScale = Vector3.one * 4;
                Physics.SyncTransforms();
                var bounds = target.GetComponent<Renderer>().bounds;
                Assert.That(JevVisibleObservation.HasUncoveredSample(camera, new List<Rect>(), target.transform, bounds), Is.False);
                cover.SetActive(false); Physics.SyncTransforms();
                Assert.That(JevVisibleObservation.HasUncoveredSample(camera, new List<Rect>(), target.transform, bounds), Is.True);
                Assert.That(JevVisibleObservation.HasUncoveredSample(camera, new List<Rect> { new Rect(0, 0, Screen.width, Screen.height) }, target.transform, bounds), Is.False);
            }
            finally { Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target); Object.DestroyImmediate(cover); }
        }
        [Test] public void TutorialBannerOutsideHudMasksWorldAndHonorsVisibility()
        {
            var cameraObject = new GameObject("TestCamera");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var overlayObject = new GameObject("Separate Tutorial Canvas", typeof(RectTransform), typeof(Canvas));
            var banner = new GameObject("TutorialBanner", typeof(RectTransform), typeof(Image));
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                target.transform.position = new Vector3(0, 0, 10);
                overlayObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                banner.transform.SetParent(overlayObject.transform, false);
                var rect = banner.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(Screen.width * .8f, Screen.height * .5f);
                banner.GetComponent<Image>().color = Color.white;
                Canvas.ForceUpdateCanvases(); Physics.SyncTransforms();
                var bounds = target.GetComponent<Renderer>().bounds;
                Assert.That(banner.GetComponentInParent<HUDPanel>(), Is.Null);
                Assert.That(JevVisibleObservation.HasUncoveredSample(camera, JevVisibleObservation.HudMasks(), target.transform, bounds), Is.False);
                var group = overlayObject.AddComponent<CanvasGroup>(); group.alpha = 0;
                Canvas.ForceUpdateCanvases();
                Assert.That(JevVisibleObservation.HasUncoveredSample(camera, JevVisibleObservation.HudMasks(), target.transform, bounds), Is.True);
                group.alpha = 1; banner.SetActive(false);
                Canvas.ForceUpdateCanvases();
                Assert.That(JevVisibleObservation.HasUncoveredSample(camera, JevVisibleObservation.HudMasks(), target.transform, bounds), Is.True);
            }
            finally { Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(target); Object.DestroyImmediate(overlayObject); }
        }
        [Test] public void RelativeOpeningComesOnlyFromVisibleWallFootprints()
        {
            var walls = new[] {
                new JevVisibleObject { kind = "obstacle", x = -1.425f, width = 4.15f, ahead = 20 },
                new JevVisibleObject { kind = "obstacle", x = 3.325f, width = .35f, ahead = 20 } };
            var gaps = JevVisibleObservation.BuildVisibleGaps(walls, -2.6f);
            Assert.That(gaps, Has.Length.EqualTo(1));
            Assert.That(gaps[0].side, Is.EqualTo("right"));
            Assert.That(gaps[0].relativeX, Is.EqualTo(4.5f).Within(.001f));
            Assert.That(gaps[0].leftEdge, Is.EqualTo(.65f).Within(.001f));
            Assert.That(gaps[0].rightEdge, Is.EqualTo(3.15f).Within(.001f));
            Assert.That(gaps[0].playerCentreMin, Is.EqualTo(1.2f).Within(.001f));
            Assert.That(JevVisibleObservation.BuildVisibleGaps(walls, 1.9f)[0].aligned, Is.True);
            Assert.That(JevVisibleObservation.BuildVisibleGaps(new[] { walls[0] }, -2.6f), Is.Empty, "The unseen opposite wall cannot be inferred from the planner");
        }
        [Test] public void LandscapeOrMismatchedCameraCannotPassWatchedSuite()
        {
            Assert.That(JevDecisionPolicy.IsPortraitViewport(540, 960, 9f / 16f), Is.True);
            Assert.That(JevDecisionPolicy.IsPortraitViewport(1024, 768, 4f / 3f), Is.False);
            Assert.That(JevDecisionPolicy.IsPortraitViewport(540, 960, 4f / 3f), Is.False);
        }
    }
}
#endif
