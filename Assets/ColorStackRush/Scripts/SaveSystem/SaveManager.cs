using System.IO;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// JSON save system. Loads lazily on first access and writes to
    /// Application.persistentDataPath so it works on iOS/Android/desktop.
    /// </summary>
    public static class SaveManager
    {
        const string FileName = "colorstackrush_save.json";

        static SaveData data;

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>The live save data. Mutate it, then call Save().</summary>
        public static SaveData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        /// <summary>Reads the save file from disk (creates fresh data if missing/corrupt).</summary>
        public static void Load()
        {
            data = new SaveData();
            try
            {
                if (File.Exists(Path))
                {
                    string json = File.ReadAllText(Path);
                    JsonUtility.FromJsonOverwrite(json, data);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveManager] Could not read save, starting fresh: {e.Message}");
                data = new SaveData();
            }
        }

        /// <summary>Writes the current data to disk as JSON.</summary>
        public static void Save()
        {
            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(Data, prettyPrint: true));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
            }
        }

        /// <summary>Deletes all progress (used by the settings menu reset button).</summary>
        public static void DeleteAll()
        {
            if (File.Exists(Path)) File.Delete(Path);
            data = new SaveData();
            GameEvents.RaiseCoinsChanged(data.coins);
            GameEvents.RaiseSkinSelected(data.selectedSkin);
        }
    }
}
