using System;
using System.IO;
using System.Text;
using UnityEngine;
namespace ColorStackRush
{
    // File operations are isolated so migration/recovery can be tested without touching a player's save.
    public static class SaveStore
    {
        public static SaveData Normalize(SaveData d)
        {
            if (d.version > 2) throw new InvalidDataException("Unsupported save version");
            if (d.version < 2 && d.level > 18) d.campaignCompleted = true;
            d.version = 2;
            d.level = Mathf.Clamp(d.level, 1, 18);
            d.coins = Mathf.Max(0, d.coins);
            d.highScore = Mathf.Max(0, d.highScore); // legacy record is preserved, never assigned to either mode
            d.endlessBest = Mathf.Max(0, d.endlessBest);
            if (float.IsNaN(d.endlessDistance) || float.IsInfinity(d.endlessDistance)) d.endlessDistance = 0;
            d.endlessDistance = Mathf.Max(0, d.endlessDistance);
            Array.Resize(ref d.levelScores, 18);
            Array.Resize(ref d.levelStars, 18);
            for (int i = 0; i < 18; i++) { d.levelScores[i] = Mathf.Max(0, d.levelScores[i]); d.levelStars[i] = Mathf.Clamp(d.levelStars[i], 0, 3); }
            if (d.unlockedSkins == null) d.unlockedSkins = new System.Collections.Generic.List<int>();
            d.unlockedSkins.RemoveAll(i => i < 0 || i >= ShopManager.SkinCount);
            if (!d.unlockedSkins.Contains(0)) d.unlockedSkins.Add(0);
            if (!d.unlockedSkins.Contains(d.selectedSkin)) d.selectedSkin = 0;
            if (!string.IsNullOrEmpty(d.lastDailyClaim) && !DateTime.TryParseExact(d.lastDailyClaim, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)) d.lastDailyClaim = "";
            d.dailyStreak = Mathf.Max(0, d.dailyStreak);
            d.musicVolume = Mathf.Clamp01(d.musicVolume);
            d.sfxVolume = Mathf.Clamp01(d.sfxVolume);
            return d;
        }
        static bool TryRead(string path, out SaveData value)
        {
            value = null;
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path).Trim();
                if (!json.StartsWith("{") || !json.EndsWith("}")) return false;
                value = new SaveData();
                JsonUtility.FromJsonOverwrite(json, value);
                value = Normalize(value);
                return true;
            }
            catch (Exception) { return false; }
        }
        public static SaveData Load(string path)
        {
            if (TryRead(path, out var value)) return value;
            if (TryRead(path + ".bak", out value)) return value;
            return Normalize(new SaveData());
        }
        public static void Save(string path, SaveData value)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(Normalize(value)));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            bool validPrimary = TryRead(path, out _);
            if (validPrimary)
            {
                try { File.Replace(temp, path, path + ".bak"); return; }
                catch (PlatformNotSupportedException) { }
                catch (IOException) { }
                // Platforms without Replace still retain a verified recovery copy.
                File.Copy(path, path + ".bak", true);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            if (!File.Exists(path + ".bak")) File.Copy(path, path + ".bak");
        }
    }
    public static class SaveManager
    {
        const string FileName = "colorstackrush_save.json";
        static SaveData data;
        static string testDirectory;
        // Test fixtures must opt into isolated storage before creating the bootstrapper.
        public static void SetStorageDirectoryForTests(string directory) { testDirectory = directory; data = null; }
        public static string FilePath => System.IO.Path.Combine(testDirectory ?? Application.persistentDataPath, FileName);
        public static SaveData Data { get { if (data == null) Load(); return data; } }
        public static void Load() => data = SaveStore.Load(FilePath);
        public static bool Save()
        {
            try { SaveStore.Save(FilePath, Data); return true; }
            catch (Exception e) { Debug.LogError("[SaveManager] Save failed: " + e.Message); return false; }
        }
        public static void DeleteAll()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(FilePath + suffix)) File.Delete(FilePath + suffix);
            data = SaveStore.Normalize(new SaveData());
            Save();
            GameEvents.RaiseCoinsChanged(data.coins);
            GameEvents.RaiseSkinSelected(data.selectedSkin);
        }
    }
}
