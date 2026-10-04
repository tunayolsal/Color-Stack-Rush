using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
namespace ColorStackRush
{
    public static class SaveManager
    {
        const string FileName = "colorstackrush_save.json";
        static SaveData data;
        static string testDirectory;
        static Action<Action<bool>> testSync;
        static readonly Queue<SaveRequest> requests = new Queue<SaveRequest>();
        static bool processing, transactionPending, deferredSave;
        public static bool IsTransactionPending => transactionPending;
        public static bool IsSaving => processing || requests.Count > 0;
        public static event Action<bool> SaveCompleted;
        // Fixtures opt into isolated storage and may supply a delayed IndexedDB substitute.
        public static void SetStorageDirectoryForTests(string directory)
        {
            if (IsSaving) throw new InvalidOperationException("A save is still pending");
            testDirectory = directory; data = null; transactionPending = deferredSave = false; testSync = null;
        }
        public static void SetStorageSyncForTests(Action<Action<bool>> sync) => testSync = sync;
        public static string FilePath => System.IO.Path.Combine(testDirectory ?? Application.persistentDataPath, FileName);
        public static SaveData Data { get { if (data == null) Load(); return data; } }
        public static void Load() => data = SaveStore.Load(FilePath);
        public static bool TryBeginTransaction()
        {
            if (transactionPending || IsSaving) return false;
            transactionPending = true;
            return true;
        }
        public static void EndTransaction()
        {
            transactionPending = false;
            if (!deferredSave) return;
            deferredSave = false;
            Save();
        }
        // On Web this reports queue acceptance. Durable transactions use SaveAsync instead.
        public static bool Save()
        {
            if (transactionPending) { deferredSave = true; return true; }
            bool result = true;
            SaveAsync(ok => result = ok);
            return result;
        }
        public static void SaveAsync(Action<bool> completed) => EnqueueSave(completed, false);
        static void EnqueueSave(Action<bool> completed, bool clearPreviousFiles)
        {
            try
            {
                string json = JsonUtility.ToJson(Data);
                requests.Enqueue(new SaveRequest(FilePath, json, completed, clearPreviousFiles));
                ProcessNext();
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveManager] Save failed: " + e.Message);
                completed?.Invoke(false);
            }
        }
        static void ProcessNext()
        {
            if (processing || requests.Count == 0) return;
            processing = true;
            var request = requests.Dequeue();
            FileSnapshot before = null;
            try
            {
                before = new FileSnapshot(request.path);
                var snapshot = new SaveData();
                JsonUtility.FromJsonOverwrite(request.json, snapshot);
                if (request.clearPreviousFiles)
                    foreach (string suffix in new[] { "", ".bak", ".tmp" })
                        if (File.Exists(request.path + suffix)) File.Delete(request.path + suffix);
                SaveStore.Save(request.path, snapshot);
                var restore = before;
                bool finished = false;
                Action<bool> finish = ok =>
                {
                    if (finished) return;
                    finished = true;
                    if (!ok) RestoreFiles(restore);
                    Complete(request, ok);
                };
                if (testSync != null) testSync(finish);
                else WebSaveBridge.Persist(finish);
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveManager] Save failed: " + e.Message);
                RestoreFiles(before);
                Complete(request, false);
            }
        }
        static void RestoreFiles(FileSnapshot snapshot)
        {
            try { snapshot?.Restore(); }
            catch (Exception e) { Debug.LogError("[SaveManager] Recovery failed: " + e.Message); }
        }
        static void Complete(SaveRequest request, bool ok)
        {
            processing = false;
            // Domain rollback runs before observers or the next queued write see the data.
            try { request.completed?.Invoke(ok); }
            catch (Exception e) { Debug.LogException(e); }
            try { SaveCompleted?.Invoke(ok); }
            catch (Exception e) { Debug.LogException(e); }
            ProcessNext();
        }
        public static void DeleteAll() => DeleteAllAsync(null);
        public static bool DeleteAllAsync(Action<bool> completed)
        {
            if (!TryBeginTransaction()) return false;
            var previous = Data;
            data = SaveStore.Normalize(new SaveData());
            EnqueueSave(ok =>
            {
                if (!ok) data = previous;
                EndTransaction();
                try
                {
                    GameEvents.RaiseCoinsChanged(data.coins);
                    GameEvents.RaiseSkinSelected(data.selectedSkin);
                }
                finally { completed?.Invoke(ok); }
            }, true);
            return true;
        }
        sealed class SaveRequest
        {
            public readonly string path, json;
            public readonly Action<bool> completed;
            public readonly bool clearPreviousFiles;
            public SaveRequest(string path, string json, Action<bool> completed, bool clearPreviousFiles)
            { this.path = path; this.json = json; this.completed = completed; this.clearPreviousFiles = clearPreviousFiles; }
        }
        sealed class FileSnapshot
        {
            static readonly string[] Suffixes = { "", ".bak", ".tmp" };
            readonly string path;
            readonly byte[][] bytes = new byte[3][];
            public FileSnapshot(string path)
            {
                this.path = path;
                for (int i = 0; i < Suffixes.Length; i++)
                    if (File.Exists(path + Suffixes[i])) bytes[i] = File.ReadAllBytes(path + Suffixes[i]);
            }
            public void Restore()
            {
                for (int i = 0; i < Suffixes.Length; i++)
                {
                    string file = path + Suffixes[i];
                    if (bytes[i] != null) File.WriteAllBytes(file, bytes[i]);
                    else if (File.Exists(file)) File.Delete(file);
                }
            }
        }
    }
}
