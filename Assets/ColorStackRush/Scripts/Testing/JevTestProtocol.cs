#if CSR_JEV_TEST && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using System;
using UnityEngine;

namespace ColorStackRush.Testing
{
    // Compiled only for the explicitly opted-in local test player.
    [Serializable] public sealed class JevVisibleObject
    {
        public string kind, color, side;
        public float x, relativeX, leftEdge, rightEdge, ahead, width, depth, viewportX, viewportY, observedVelocityX;
    }
    [Serializable] public sealed class JevVisibleGap
    {
        public string side, description;
        public bool aligned;
        public float leftEdge, rightEdge, centreX, relativeX, ahead, playerCentreMin, playerCentreMax;
    }
    [Serializable] public sealed class JevObservation
    {
        public string activeColor, upcomingColor, powerUps, playerPosition;
        public float upcomingSeconds, playerX, forwardSpeed, horizontalLimit = 2.6f;
        public int blocks, score;
        public JevVisibleObject[] visible;
        public JevVisibleGap[] visibleGaps;
    }
    [Serializable] public sealed class JevSnapshot
    {
        public int runId, snapshotId;
        public string suiteId;
        public string phase, profile;
        public long level;
        public float capturedRealtime, capturedPlayerDistance;
        public int viewportWidth, viewportHeight;
        public float cameraAspect;
        public JevObservation observation;
    }
    [Serializable] public sealed class JevDecision
    {
        public int runId, snapshotId;
        public string suiteId, phase, activeColor, action, model, error;
        public float latencyMs, confidence;
        public int inputTokens;
    }
    [Serializable] public sealed class JevActionAck
    {
        public int runId, snapshotId;
        public string suiteId;
        public bool applied;
        public string reason, action;
        public float ageMs;
        public string modelAction;
        public bool testOverride;
        public float predictedX, targetWallAhead, targetWallLeft, targetWallRight;
    }
    [Serializable] public sealed class JevRunReport
    {
        public long level;
        public string suiteId;
        public int runId, stars, score, obstacleHits, wrongColors, steeringActions, decisions, applied, stale, serviceFailures, inputTokens;
        public bool completed, injectedMistake, injectedGestureApplied, injectedDamageObserved;
        public string profile, model = "jev-1.13.0", failureClass, failureDetail;
        public float seconds, meanLatencyMs, maximumLatencyMs;
        public int viewportWidth, viewportHeight;
        public float cameraAspect;
    }
    [Serializable] public sealed class JevSuiteReport
    {
        public string model = "jev-1.13.0", observation = "Camera frustum, HUD masks and geometry occlusion; text/JSON only; not image understanding";
        public string suiteId;
        public string note = "Normal-speed synthetic mouse gestures through SwipeInput and Rigidbody physics. Model tests are not a human difficulty study.";
        public bool finished, normalGoalMet;
        public int normalWins;
        public int viewportWidth, viewportHeight;
        public float cameraAspect;
        public JevRunReport[] runs;
    }
    public static class JevDecisionPolicy
    {
        public const float MaximumAgeSeconds = .5f;
        public static bool TryAccept(JevSnapshot snapshot, JevDecision answer, int currentRun, GameState currentPhase, string currentColor, float now, out string reason)
        {
            reason = "accepted";
            if (answer == null || snapshot == null) reason = "missing_answer";
            else if (!string.IsNullOrEmpty(answer.error)) reason = "service_failure";
            else if (answer.suiteId != snapshot.suiteId) reason = "stale_suite";
            else if (answer.runId != currentRun || snapshot.runId != currentRun) reason = "stale_run";
            else if (answer.snapshotId != snapshot.snapshotId) reason = "stale_snapshot";
            else if (currentPhase != GameState.Playing || answer.phase != snapshot.phase || answer.phase != currentPhase.ToString()) reason = "stale_phase";
            else if (answer.activeColor != currentColor || snapshot.observation.activeColor != currentColor) reason = "stale_color";
            else if (now - snapshot.capturedRealtime > MaximumAgeSeconds || now < snapshot.capturedRealtime) reason = "stale_time";
            else if (answer.model != "jev-1.13.0") reason = "unexpected_model";
            else if (!IsAction(answer.action)) reason = "invalid_action";
            return reason == "accepted";
        }
        public static bool IsAction(string action) => action == "left_small" || action == "left_large" || action == "hold" || action == "right_small" || action == "right_large";
        public static float Delta(string action) => action == "left_small" ? -.55f : action == "left_large" ? -1.35f : action == "right_small" ? .55f : action == "right_large" ? 1.35f : 0;
        public static bool IsPortraitViewport(int width, int height, float cameraAspect)
        {
            if (width <= 0 || height <= 0 || height < width * 1.35f) return false;
            float ratio = (float)width / height;
            return cameraAspect < .75f && Mathf.Abs(cameraAspect - ratio) < .03f;
        }
        public static bool TryPlanMistake(JevObservation observation, out string action, out JevVisibleObject target, out float predictedX)
        {
            action = null; target = null; predictedX = observation.playerX;
            float nearest = float.PositiveInfinity;
            foreach (var item in observation.visible)
                if (item.kind == "obstacle" && item.ahead > 0 && item.ahead < 8) nearest = Mathf.Min(nearest, item.ahead);
            if (float.IsInfinity(nearest)) return false;
            // Perturb a currently safe lane, rather than labelling an existing
            // model mistake or the correct recovery direction as a bad action.
            foreach (var item in observation.visible)
                if (item.kind == "obstacle" && Mathf.Abs(item.ahead - nearest) < 1 && Overlaps(observation.playerX, item)) return false;
            foreach (string candidate in new[] { "left_large", "right_large" })
            {
                float endpoint = Mathf.Clamp(observation.playerX + Delta(candidate), -observation.horizontalLimit, observation.horizontalLimit);
                foreach (var item in observation.visible)
                    if (item.kind == "obstacle" && Mathf.Abs(item.ahead - nearest) < 1 && Overlaps(endpoint, item))
                    { action = candidate; target = item; predictedX = endpoint; return true; }
            }
            return false;
        }
        static bool Overlaps(float x, JevVisibleObject item) => x + .55f > item.x - item.width * .5f && x - .55f < item.x + item.width * .5f;
    }
}
#endif
