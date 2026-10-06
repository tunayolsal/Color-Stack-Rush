#if CSR_JEV_TEST && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace ColorStackRush.Testing
{
    public static class JevVisibleObservation
    {
        static readonly Vector3[] corners = new Vector3[4];
        static readonly Dictionary<int, Vector2> lastVisiblePositions = new Dictionary<int, Vector2>();
        static int observedRun = -1;
        public static JevObservation Capture()
        {
            var player = PlayerController.Instance;
            var color = ColorManager.Instance;
            var camera = Camera.main;
            if (observedRun != GameManager.Instance.RunId) { observedRun = GameManager.Instance.RunId; lastVisiblePositions.Clear(); }
            var objects = new List<JevVisibleObject>();
            var masks = HudMasks();
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            foreach (var item in Object.FindObjectsByType<Collectible>(FindObjectsSortMode.None))
            {
                if (!item.isActiveAndEnabled) continue;
                var renderer = item.GetComponentInChildren<MeshRenderer>();
                if (renderer == null || !renderer.enabled) continue;
                string kind = item is CollectibleBlock ? "block" : item is Coin ? "coin" : "power_up";
                string tint = item is CollectibleBlock block ? block.BlockColor.ToString() : item is PowerUpPickup power ? power.Type.ToString() : "";
                AddVisible(objects, camera, planes, masks, player, item.transform, renderer.bounds, kind, tint, renderer.GetInstanceID());
            }
            foreach (var obstacle in Object.FindObjectsByType<Obstacle>(FindObjectsSortMode.None))
            {
                if (!obstacle.isActiveAndEnabled) continue;
                // Each physical wall part is described separately, so an opening
                // is observable without exposing its planned route or future phase.
                foreach (var collider in obstacle.GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    AddVisible(objects, camera, planes, masks, player, obstacle.transform, collider.bounds, "obstacle", "dark", collider.GetInstanceID());
                }
            }
            objects.Sort((a, b) => a.ahead.CompareTo(b.ahead));
            if (objects.Count > 32) objects.RemoveRange(32, objects.Count - 32);
            return new JevObservation
            {
                activeColor = color.ActiveColor.ToString(), upcomingColor = color.HasWarning ? color.NextColor.ToString() : "",
                upcomingSeconds = color.HasWarning ? color.WarningSeconds : 0,
                playerX = player.transform.position.x, forwardSpeed = player.CurrentSpeed,
                playerPosition = PlayerPosition(player.transform.position.x),
                blocks = player.GetComponent<PlayerStack>().Count, score = ScoreManager.Instance.Score,
                powerUps = PowerUpManager.Instance != null ? PowerUpManager.Instance.GetActiveSummary() : "", visible = objects.ToArray(),
                visibleGaps = BuildVisibleGaps(objects, player.transform.position.x)
            };
        }
        public static List<Rect> HudMasks()
        {
            var masks = new List<Rect>();
            // Tutorial, save-status and later overlays may live outside HUDPanel.
            // Every visible overlay Image covers the world regardless of its parent.
            foreach (var image in Object.FindObjectsByType<Image>(FindObjectsSortMode.None))
            {
                var canvas = image.canvas != null ? image.canvas.rootCanvas : null;
                if (!image.isActiveAndEnabled || canvas == null || !canvas.isActiveAndEnabled || canvas.renderMode != RenderMode.ScreenSpaceOverlay || image.canvasRenderer.cull) continue;
                float alpha = image.color.a;
                bool ignoreParents = false;
                for (Transform parent = image.transform; parent != null && !ignoreParents; parent = parent.parent)
                    foreach (var group in parent.GetComponents<CanvasGroup>())
                    {
                        alpha *= group.alpha;
                        if (group.ignoreParentGroups) ignoreParents = true;
                    }
                if (alpha < .85f) continue;
                image.rectTransform.GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                var b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                masks.Add(Rect.MinMaxRect(a.x, a.y, b.x, b.y));
            }
            return masks;
        }
        static void AddVisible(List<JevVisibleObject> objects, Camera camera, Plane[] planes, List<Rect> masks, PlayerController player, Transform root, Bounds bounds, string kind, string color, int visibleId)
        {
            float ahead = bounds.center.z - player.Distance;
            if (ahead < -.6f || !GeometryUtility.TestPlanesAABB(planes, bounds)) return;
            if (!HasUncoveredSample(camera, masks, root, bounds)) return;
            var viewport = camera.WorldToViewportPoint(bounds.center);
            float velocity = 0, now = Time.realtimeSinceStartup;
            if (lastVisiblePositions.TryGetValue(visibleId, out var previous) && now - previous.y > .01f && now - previous.y < 1)
                velocity = (bounds.center.x - previous.x) / (now - previous.y);
            // This estimate comes only from two visible frames. No movement phase
            // or hidden future sweep is queried from the obstacle component.
            lastVisiblePositions[visibleId] = new Vector2(bounds.center.x, now);
            float relative = bounds.center.x - player.transform.position.x;
            objects.Add(new JevVisibleObject { kind = kind, color = color, x = bounds.center.x, ahead = ahead,
                relativeX = relative, side = Side(relative), leftEdge = bounds.min.x, rightEdge = bounds.max.x,
                width = bounds.size.x, depth = bounds.size.z, viewportX = viewport.x, viewportY = viewport.y, observedVelocityX = velocity });
        }
        static string Side(float relative) => relative < -.3f ? "left" : relative > .3f ? "right" : "aligned";
        static string PlayerPosition(float x) => x < -2.4f ? "left edge" : x > 2.4f ? "right edge" : x < -.3f ? "left side" : x > .3f ? "right side" : "centre";
        public static JevVisibleGap[] BuildVisibleGaps(IList<JevVisibleObject> visible, float playerX)
        {
            // Geometric differences between walls visible in this frame only.
            // Nothing is read from RouteGate, CoursePlan, seeds or future motion.
            var obstacles = new List<JevVisibleObject>();
            foreach (var item in visible) if (item.kind == "obstacle" && item.ahead > 0) obstacles.Add(item);
            obstacles.Sort((a, b) => a.ahead.CompareTo(b.ahead));
            var gaps = new List<JevVisibleGap>();
            for (int begin = 0; begin < obstacles.Count;)
            {
                int end = begin + 1;
                while (end < obstacles.Count && obstacles[end].ahead - obstacles[begin].ahead < .8f) end++;
                var group = obstacles.GetRange(begin, end - begin);
                group.Sort((a, b) => (a.x - a.width * .5f).CompareTo(b.x - b.width * .5f));
                if (group.Count > 1)
                {
                    float occupiedRight = group[0].x + group[0].width * .5f;
                    for (int index = 1; index < group.Count; index++)
                    {
                        float nextLeft = group[index].x - group[index].width * .5f;
                        float centreMin = Mathf.Max(-2.6f, occupiedRight + .55f), centreMax = Mathf.Min(2.6f, nextLeft - .55f);
                        if (centreMin <= centreMax && nextLeft - occupiedRight >= 1.1f)
                        {
                            float centre = (occupiedRight + nextLeft) * .5f, relative = centre - playerX;
                            bool aligned = playerX >= centreMin && playerX <= centreMax;
                            string side = aligned ? "aligned" : Side(relative);
                            gaps.Add(new JevVisibleGap { leftEdge = occupiedRight, rightEdge = nextLeft, centreX = centre,
                                relativeX = relative, ahead = group[0].ahead, playerCentreMin = centreMin, playerCentreMax = centreMax, aligned = aligned, side = side,
                                description = string.Format(CultureInfo.InvariantCulture, "Visible opening {0:0.0} units ahead is {1}; its edges are x={2:0.00} to {3:0.00}. Player centres x={4:0.00} to {5:0.00} fit. You are {6} with this opening.",
                                    group[0].ahead, side, occupiedRight, nextLeft, centreMin, centreMax, aligned ? "already aligned" : "not aligned") });
                        }
                        occupiedRight = Mathf.Max(occupiedRight, group[index].x + group[index].width * .5f);
                    }
                }
                begin = end;
            }
            return gaps.ToArray();
        }
        public static bool HasUncoveredSample(Camera camera, List<Rect> masks, Transform root, Bounds bounds)
        {
            // Test actual line of sight, including trigger walls. Offscreen or
            // entirely HUD-covered objects never reach the model.
            for (int sample = 0; sample < 9; sample++)
            {
                var point = bounds.center;
                if (sample > 0)
                    point += Vector3.Scale(bounds.extents * .8f, new Vector3(((sample - 1) & 1) == 0 ? -1 : 1, ((sample - 1) & 2) == 0 ? -1 : 1, ((sample - 1) & 4) == 0 ? -1 : 1));
                var view = camera.WorldToViewportPoint(point);
                if (view.z <= camera.nearClipPlane || view.z >= camera.farClipPlane || view.x < 0 || view.x > 1 || view.y < 0 || view.y > 1) continue;
                var screen = camera.WorldToScreenPoint(point);
                bool covered = false;
                foreach (var mask in masks) if (mask.Contains(new Vector2(screen.x, screen.y))) { covered = true; break; }
                if (covered) continue;
                var direction = point - camera.transform.position;
                bool occluded = false;
                foreach (var hit in Physics.RaycastAll(camera.transform.position, direction.normalized, direction.magnitude - .02f, ~0, QueryTriggerInteraction.Collide))
                {
                    if (hit.transform == root || hit.transform.IsChildOf(root)) continue;
                    occluded = true; break;
                }
                if (!occluded) return true;
            }
            return false;
        }
    }
}
#endif
