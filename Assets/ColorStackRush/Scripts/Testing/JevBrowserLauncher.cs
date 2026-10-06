#if CSR_JEV_TEST && DEVELOPMENT_BUILD && UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Scripting;

namespace ColorStackRush.Testing
{
    // Only the local Development WebGL player exposes this SendMessage target.
    // The public player cannot compile it, and no service credential is here.
    [Preserve]
    public sealed class JevBrowserLauncher : MonoBehaviour
    {
        const string LauncherName = "[LOCAL JEV LAUNCHER]";
        bool running, isolated;
        string previousDirectory, previousJson;

        [Preserve, RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindFirstObjectByType<JevBrowserLauncher>() != null) return;
            var root = new GameObject(LauncherName);
            DontDestroyOnLoad(root);
            root.AddComponent<JevBrowserLauncher>();
        }

        // The host invokes this method from its explicit user-gesture button.
        [Preserve]
        public void StartWatchedSuite()
        {
            if (running) { Debug.LogWarning("[Jev Browser] A watched suite is already running."); return; }
            if (!Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var page)
                || page.Scheme != "http" || page.Host != "127.0.0.1")
            {
                Debug.LogError("[Jev Browser] Serve this local test player over http://127.0.0.1; it cannot run on a public origin.");
                return;
            }
            running = true;
            AudioManager.Instance?.UnlockAudio();
            StartCoroutine(StartIsolatedSuite());
        }

        [Preserve]
        public void ResumeWatchedSuite()
        {
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Paused)
                GameManager.Instance.ResumeGame();
        }

        IEnumerator StartIsolatedSuite()
        {
            float deadline = Time.realtimeSinceStartup + 20;
            while (GameManager.Instance == null || SaveManager.IsSaving || SaveManager.IsTransactionPending)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogError("[Jev Browser] Existing save did not settle; the suite was not started.");
                    running = false; yield break;
                }
                yield return null;
            }
            bool menuReady = true;
            try
            {
                // GoToMenu flushes existing earnings into the original storage.
                // The isolation switch is made only after that durable write.
                GameManager.Instance.GoToMenu();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception); menuReady = false;
            }
            if (!menuReady) { running = false; yield break; }
            while (SaveManager.IsSaving || SaveManager.IsTransactionPending)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Debug.LogError("[Jev Browser] Existing menu save did not settle; the suite was not started.");
                    running = false; yield break;
                }
                yield return null;
            }
            try
            {
                string directory = Path.GetDirectoryName(SaveManager.FilePath);
                previousDirectory = string.Equals(directory, Application.persistentDataPath, StringComparison.Ordinal) ? null : directory;
                previousJson = JsonUtility.ToJson(SaveManager.Data);
                string fixture = Path.Combine(Application.persistentDataPath, "CSR-jev-test", Guid.NewGuid().ToString("N"));
                SaveManager.SetStorageDirectoryForTests(fixture);
                isolated = true;
                SaveManager.Load();
                Debug.Log("[Jev Browser] Watched suite uses isolated test storage.");
                JevTestBridge.BeginSuite(SuiteFinished);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                StartCoroutine(RestoreOriginalStorage());
            }
        }

        void SuiteFinished(int exit)
        {
            Debug.Log("[Jev Browser] Watched suite finished with status " + exit + ".");
            StartCoroutine(RestoreOriginalStorage());
        }
        IEnumerator RestoreOriginalStorage()
        {
            // A completion callback may occur inside a save notification. Wait
            // for all test writes before switching the durable path back.
            while (SaveManager.IsSaving || SaveManager.IsTransactionPending) yield return null;
            if (isolated)
            {
                SaveManager.SetStorageDirectoryForTests(previousDirectory);
                SaveManager.Load();
                if (!string.IsNullOrEmpty(previousJson))
                {
                    JsonUtility.FromJsonOverwrite(previousJson, SaveManager.Data);
                    SaveManager.Data.RebuildRecordIndex();
                }
                isolated = false;
                AudioManager.Instance?.ApplySettings();
                GameEvents.RaiseCoinsChanged(SaveManager.Data.coins);
                GameEvents.RaiseSkinSelected(SaveManager.Data.selectedSkin);
                GameManager.Instance?.GoToMenu();
                Debug.Log("[Jev Browser] Original local progress and storage restored.");
            }
            running = false;
        }
    }
}
#endif
