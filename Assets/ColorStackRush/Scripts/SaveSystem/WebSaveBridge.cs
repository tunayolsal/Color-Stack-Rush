using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace ColorStackRush
{
    // The loader populates IDBFS before startup. Writes become durable only after this callback.
    public sealed class WebSaveBridge : MonoBehaviour
    {
        static WebSaveBridge instance;
        readonly Dictionary<int, Action<bool>> callbacks = new Dictionary<int, Action<bool>>();
        int nextId;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void CSR_Persist(int id, string receiver);
        [DllImport("__Internal")] static extern void CSR_RegisterBrowser(string receiver);
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EnsureInstance();
#endif
        }
        static WebSaveBridge EnsureInstance()
        {
            if (instance != null) return instance;
            var go = new GameObject("ColorStackRushWebBridge");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<WebSaveBridge>();
#if UNITY_WEBGL && !UNITY_EDITOR
            CSR_RegisterBrowser(go.name);
#endif
            return instance;
        }
        public static void Persist(Action<bool> completed)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var bridge = EnsureInstance();
            int id = ++bridge.nextId;
            bridge.callbacks.Add(id, completed);
            CSR_Persist(id, bridge.gameObject.name);
#else
            completed?.Invoke(true);
#endif
        }
        [Preserve] public void OnPersistCompleted(string payload)
        {
            var parts = payload.Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int id) || !callbacks.TryGetValue(id, out var callback)) return;
            callbacks.Remove(id);
            callback?.Invoke(parts[1] == "1");
        }
        [Preserve] public void OnBrowserHidden(string unused)
        {
            GameManager.Instance?.PauseGame();
            SaveManager.Save();
        }
        [Preserve] public void OnBrowserAudioUnlocked(string unused) => AudioManager.Instance?.UnlockAudio();
    }
}
