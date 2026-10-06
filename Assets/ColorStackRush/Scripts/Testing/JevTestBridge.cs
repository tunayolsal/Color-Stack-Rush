#if CSR_JEV_TEST && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Networking;

namespace ColorStackRush.Testing
{
    /// <summary>Local test instrumentation. Never included in the public player.</summary>
    [DefaultExecutionOrder(-60)]
    public sealed class JevTestBridge : MonoBehaviour
    {
        static readonly long[] Levels = { 1, 4, 6, 7, 12, 13, 19, 1000 };
        static readonly string[] Profiles = { "normal", "extra_150ms", "extra_300ms", "one_bad_then_recover" };
        string endpoint, reportPath;
        string suiteId;
        Mouse mouse;
        bool gestureActive;
        int snapshotId;
        Action<int> finished;
        JevRunReport run;
        readonly List<JevRunReport> reports = new List<JevRunReport>();
        readonly List<InputDevice> suppressedPointers = new List<InputDevice>();
        float totalLatency;
        float injectedWallZ;
        float pausedSeconds, pauseBegan = -1;
        InputSettings.BackgroundBehavior previousBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
#endif
        public static void BeginSuite(Action<int> completed = null)
        {
            if (FindFirstObjectByType<JevTestBridge>() != null) throw new InvalidOperationException("A Jev suite is already running");
            var bridge = new GameObject("[LOCAL JEV TEST ONLY]").AddComponent<JevTestBridge>();
            bridge.finished = completed;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var pageUri)) throw new InvalidOperationException("Jev test player needs a local page URL");
            bridge.endpoint = pageUri.GetLeftPart(UriPartial.Authority);
#else
            bridge.endpoint = Argument("-jevEndpoint") ?? Environment.GetEnvironmentVariable("CSR_JEV_ENDPOINT") ?? "http://127.0.0.1:8877";
#endif
            if (!Uri.TryCreate(bridge.endpoint, UriKind.Absolute, out var uri) || uri.Host != "127.0.0.1" || uri.Scheme != "http")
                throw new InvalidOperationException("Jev tests require an explicitly local HTTP endpoint");
            bridge.endpoint = bridge.endpoint.TrimEnd('/');
#if UNITY_WEBGL && !UNITY_EDITOR
            bridge.reportPath = Path.Combine(Application.temporaryCachePath, "jev-suite.json");
#else
            bridge.reportPath = Argument("-jevReport") ?? Path.Combine(Application.temporaryCachePath, "jev-suite.json");
#endif
            bridge.suiteId = Guid.NewGuid().ToString("N");
            bridge.StartCoroutine(bridge.Suite());
        }
        IEnumerator Suite()
        {
            while (GameManager.Instance == null || SwipeInput.Instance == null || Camera.main == null) yield return null;
            yield return null;
            if (!JevDecisionPolicy.IsPortraitViewport(Screen.width, Screen.height, Camera.main.aspect))
            {
                run = new JevRunReport { suiteId = suiteId, failureClass = "test_harness", failureDetail = "portrait_viewport_required",
                    viewportWidth = Screen.width, viewportHeight = Screen.height, cameraAspect = Camera.main.aspect };
                reports.Add(run);
                yield return FinishSuite(false, 2);
                yield break;
            }
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            mouse = InputSystem.AddDevice<Mouse>();
            foreach (var device in InputSystem.devices)
                if (device != mouse && device.enabled && (device is Pointer || device is Touchscreen))
                { suppressedPointers.Add(device); InputSystem.DisableDevice(device); }
            GameEvents.BlockCollected += Collected;
            GameEvents.ObstacleHit += Hit;
            GameEvents.StateChanged += StateChanged;
            // Do not alter the shipped difficulty, timestep or movement controller.
            Application.runInBackground = true;
            Time.timeScale = 1; Time.fixedDeltaTime = .02f;
            foreach (string profile in Profiles)
                foreach (long level in Levels)
                {
                    yield return PlayOne(level, profile);
                    if (run.failureDetail == "portrait_viewport_required") { yield return FinishSuite(false, 2); yield break; }
                    if (run.serviceFailures >= 3 && run.applied == 0)
                    {
                        // Stop wasting calls when the provider is unavailable.
                        yield return FinishSuite(false, 2); yield break;
                    }
                }
            var suite = WriteReport(true);
            Debug.Log("JEV_SUITE_FINISHED runs=" + reports.Count + " normalWins=" + suite.normalWins + "/8 goalMet=" + suite.normalGoalMet);
            int exit = reports.Exists(r => r.serviceFailures > 0 || r.failureClass == "test_harness") ? 2 : suite.normalGoalMet ? 0 : 3;
            yield return FinishSuite(true, exit);
        }
        IEnumerator PlayOne(long level, string profile)
        {
            while (SaveManager.IsSaving) yield return null;
            GameManager.Instance.GoToMenu();
            SaveManager.Data.highestUnlockedLevel = level; // Isolated fixture storage only.
            run = new JevRunReport { level = level, profile = profile, suiteId = suiteId,
                viewportWidth = Screen.width, viewportHeight = Screen.height, cameraAspect = Camera.main.aspect };
            if (!JevDecisionPolicy.IsPortraitViewport(Screen.width, Screen.height, Camera.main.aspect))
            {
                run.failureClass = "test_harness"; run.failureDetail = "portrait_viewport_required";
                reports.Add(run); WriteReport(false);
                yield break;
            }
            totalLatency = 0; gestureActive = false;
            pausedSeconds = 0; pauseBegan = -1;
            MouseState released = new MouseState { position = PointerStart() };
            InputSystem.QueueStateEvent(mouse, released);
            yield return null;
            GameManager.Instance.StartRun(RunConfig.Level(level));
            run.runId = GameManager.Instance.RunId;
            yield return null; yield return null; // Allow the usual all-pointers-released gate.
            float start = Time.realtimeSinceStartup, next = start;
            while ((GameManager.Instance.State == GameState.Playing || GameManager.Instance.State == GameState.Paused) && ActiveSeconds(start) < 75 && run.decisions < 256)
            {
                if (GameManager.Instance.State == GameState.Paused) { yield return null; continue; }
                if (Time.realtimeSinceStartup < next) { yield return null; continue; }
                // A response can only use this visible frame, never a planner oracle.
                var snapshot = new JevSnapshot { runId = run.runId, snapshotId = ++snapshotId,
                    suiteId = suiteId, phase = GameManager.Instance.State.ToString(), profile = profile, level = level,
                    viewportWidth = Screen.width, viewportHeight = Screen.height, cameraAspect = Camera.main.aspect,
                    capturedRealtime = Time.realtimeSinceStartup, capturedPlayerDistance = PlayerController.Instance.Distance,
                    observation = JevVisibleObservation.Capture() };
                next = snapshot.capturedRealtime + .25f;
                run.decisions++;
                JevDecision answer = null;
                using (var request = Post("/api/decision", JsonUtility.ToJson(snapshot)))
                {
                    yield return request.SendWebRequest();
                    if (request.result != UnityWebRequest.Result.Success) run.serviceFailures++;
                    else
                    {
                        try { answer = JsonUtility.FromJson<JevDecision>(request.downloadHandler.text); }
                        catch (Exception) { run.serviceFailures++; }
                    }
                }
                float now = Time.realtimeSinceStartup;
                bool valid = JevDecisionPolicy.TryAccept(snapshot, answer, GameManager.Instance.RunId, GameManager.Instance.State,
                    ColorManager.Instance.ActiveColor.ToString(), now, out string reason);
                if (answer != null)
                {
                    totalLatency += answer.latencyMs; run.inputTokens += answer.inputTokens;
                    run.maximumLatencyMs = Mathf.Max(run.maximumLatencyMs, answer.latencyMs);
                    if (!string.IsNullOrEmpty(answer.error)) run.serviceFailures++;
                }
                string action = answer != null ? answer.action : "hold";
                if (valid && gestureActive) { valid = false; reason = "gesture_busy"; }
                bool inject = false;
                JevVisibleObject mistakeWall = null;
                float mistakeX = 0;
                if (valid && profile == "one_bad_then_recover" && !run.injectedMistake &&
                    JevDecisionPolicy.TryPlanMistake(snapshot.observation, out string mistakeAction, out mistakeWall, out mistakeX))
                {
                    action = mistakeAction;
                    inject = true;
                    reason = "injected_test_mistake";
                }
                if (valid)
                {
                    run.applied++;
                    if (action != "hold")
                    {
                        run.steeringActions++;
                        if (inject) { run.injectedMistake = true; injectedWallZ = snapshot.capturedPlayerDistance + mistakeWall.ahead; }
                        StartCoroutine(Gesture(JevDecisionPolicy.Delta(action), run.runId, inject));
                    }
                }
                else run.stale++;
                var ack = new JevActionAck { runId = snapshot.runId, snapshotId = snapshot.snapshotId, suiteId = suiteId, action = action,
                    applied = valid, reason = reason, ageMs = (now - snapshot.capturedRealtime) * 1000,
                    modelAction = answer != null ? answer.action : "hold", testOverride = inject,
                    predictedX = mistakeX, targetWallAhead = mistakeWall != null ? mistakeWall.ahead : 0,
                    targetWallLeft = mistakeWall != null ? mistakeWall.x - mistakeWall.width * .5f : 0,
                    targetWallRight = mistakeWall != null ? mistakeWall.x + mistakeWall.width * .5f : 0 };
                using (var request = Post("/api/action-ack", JsonUtility.ToJson(ack))) yield return request.SendWebRequest();
                if (run.serviceFailures >= 3 && run.applied == 0) break;
            }
            float waitUntil = Time.realtimeSinceStartup + 12;
            while ((GameManager.Instance.State == GameState.Finish || GameManager.Instance.State == GameState.Dying) && Time.realtimeSinceStartup < waitUntil) yield return null;
            run.seconds = ActiveSeconds(start);
            run.meanLatencyMs = run.decisions > 0 ? totalLatency / run.decisions : 0;
            var game = GameManager.Instance;
            if (game.ResultCommitted && game.LastResult.config.levelId == level)
            {
                run.completed = game.LastResult.completed; run.stars = game.LastResult.stars; run.score = game.LastResult.score;
            }
            if (run.serviceFailures > 0) { run.failureClass = "connection"; run.failureDetail = "One or more API/transport responses failed; inspect decisions.jsonl."; }
            else if (run.applied == 0 && run.stale > 0) { run.failureClass = "connection_latency"; run.failureDetail = "No model response met the unchanged 500ms freshness limit"; }
            else if (game.ResultSaveFailed) { run.failureClass = "test_harness"; run.failureDetail = "save_failure"; }
            else if (game.State != GameState.Victory && game.State != GameState.GameOver)
            { run.failureClass = "test_harness"; run.failureDetail = run.decisions >= 256 ? "decision_budget" : "run_timeout_or_save_failure"; }
            else if (profile == "one_bad_then_recover" && (!run.injectedMistake || !run.injectedGestureApplied))
            { run.failureClass = "test_harness"; run.failureDetail = "mistake_gesture_not_applied_no_recovery_trial"; }
            else if (!run.completed) { run.failureClass = "model_or_game"; run.failureDetail = "Observed decisions could not complete the run; compare the route physics test and decision trace."; }
            else run.failureClass = "none";
            reports.Add(run); WriteReport(false);
            Debug.Log("JEV_RUN level=" + level + " profile=" + profile + " win=" + run.completed + " stars=" + run.stars + " hits=" + run.obstacleHits + " wrong=" + run.wrongColors + " calls=" + run.decisions + " stale=" + run.stale);
            using (var request = Post("/api/run-result", JsonUtility.ToJson(run))) yield return request.SendWebRequest();
            while (gestureActive) yield return null;
            InputSystem.QueueStateEvent(mouse, released); yield return null;
        }
        IEnumerator Gesture(float delta, int runId, bool injected = false)
        {
            gestureActive = true;
            float initialX = PlayerController.Instance.transform.position.x;
            Vector2 start = PointerStart();
            var state = new MouseState { position = start };
            InputSystem.QueueStateEvent(mouse, state);
            yield return null;
            state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
            yield return null;
            float began = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - began < .2f && GameManager.Instance.RunId == runId && GameManager.Instance.State == GameState.Playing)
            {
                float fraction = Mathf.Clamp01((Time.realtimeSinceStartup - began) / .2f);
                state.position.x = start.x + delta * Screen.width / 8.5f * fraction;
                InputSystem.QueueStateEvent(mouse, state);
                yield return null;
            }
            if (GameManager.Instance.RunId == runId && GameManager.Instance.State == GameState.Playing)
            {
                state.position.x = start.x + delta * Screen.width / 8.5f;
                InputSystem.QueueStateEvent(mouse, state); yield return null;
            }
            InputSystem.QueueStateEvent(mouse, new MouseState { position = state.position });
            yield return null;
            if (injected && run != null && run.runId == runId)
                run.injectedGestureApplied = (PlayerController.Instance.transform.position.x - initialX) * Mathf.Sign(delta) > .2f;
            gestureActive = false;
        }
        static Vector2 PointerStart() => new Vector2(Screen.width * .5f, Screen.height * .45f);
        void Collected(bool correct, Vector3 position) { if (run != null && !correct) run.wrongColors++; }
        void Hit(Vector3 position)
        {
            if (run == null) return;
            run.obstacleHits++;
            if (run.injectedMistake && Mathf.Abs(position.z - injectedWallZ) < 2) run.injectedDamageObserved = true;
        }
        void StateChanged(GameState state)
        {
            if (state == GameState.Paused)
            {
                if (pauseBegan < 0) pauseBegan = Time.realtimeSinceStartup;
                // The viewer must still be able to press the ordinary Resume UI.
                foreach (var device in suppressedPointers) if (device.added && !device.enabled) InputSystem.EnableDevice(device);
            }
            else
            {
                if (pauseBegan >= 0) { pausedSeconds += Time.realtimeSinceStartup - pauseBegan; pauseBegan = -1; }
                foreach (var device in suppressedPointers) if (device.added && device.enabled) InputSystem.DisableDevice(device);
            }
        }
        float ActiveSeconds(float started) => Mathf.Max(0, Time.realtimeSinceStartup - started - pausedSeconds - (pauseBegan >= 0 ? Time.realtimeSinceStartup - pauseBegan : 0));
        UnityWebRequest Post(string path, string json)
        {
            var request = new UnityWebRequest(endpoint + path, "POST") { uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)), downloadHandler = new DownloadHandlerBuffer(), timeout = 3 };
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }
        JevSuiteReport WriteReport(bool done)
        {
            var suite = new JevSuiteReport { finished = done, runs = reports.ToArray(), suiteId = suiteId,
                viewportWidth = Screen.width, viewportHeight = Screen.height, cameraAspect = Camera.main != null ? Camera.main.aspect : 0 };
            bool first = false, fourth = false;
            foreach (var record in reports) if (record.profile == "normal" && record.completed)
            { suite.normalWins++; if (record.level == 1) first = true; if (record.level == 4) fourth = true; }
            suite.normalGoalMet = suite.normalWins >= 6 && first && fourth;
            string directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            Directory.CreateDirectory(directory);
            File.WriteAllText(reportPath, JsonUtility.ToJson(suite, true));
            return suite;
        }
        IEnumerator FinishSuite(bool complete, int exit)
        {
            var report = WriteReport(complete);
            using (var request = Post("/api/suite-result", JsonUtility.ToJson(report)))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { Debug.LogError("Jev suite report could not reach the local driver"); exit = 2; }
            }
            Cleanup();
            while (SaveManager.IsSaving || SaveManager.IsTransactionPending) yield return null;
            finished?.Invoke(exit);
            Destroy(gameObject);
        }
        void Cleanup()
        {
            GameEvents.BlockCollected -= Collected; GameEvents.ObstacleHit -= Hit;
            GameEvents.StateChanged -= StateChanged;
            if (mouse == null) return;
            InputSystem.RemoveDevice(mouse); mouse = null;
            foreach (var device in suppressedPointers) if (device.added && !device.enabled) InputSystem.EnableDevice(device);
            suppressedPointers.Clear();
            InputSystem.settings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
#endif
            Time.timeScale = 1; Time.fixedDeltaTime = .02f;
        }
        void OnDestroy() => Cleanup();
        static string Argument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++) if (arguments[i] == name) return arguments[i + 1];
            return null;
        }
    }
}
#endif
