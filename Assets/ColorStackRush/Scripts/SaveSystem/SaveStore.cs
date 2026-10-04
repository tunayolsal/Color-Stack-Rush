using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
namespace ColorStackRush
{
    // File operations are isolated so migration/recovery can be tested without touching a player's save.
    public static class SaveStore
    {
        public static SaveData Normalize(SaveData d)
        {
            if (d == null) d = new SaveData();
            if (d.version > 3) throw new InvalidDataException("Unsupported save version");
            bool migrate = d.version < 3;
            d.highestUnlockedLevel = Math.Max(1L, d.highestUnlockedLevel);
            var records = new Dictionary<long, LevelRecord>();
            var cleanRecords = new List<LevelRecord>();
            if (d.levelRecords != null)
                foreach (var record in d.levelRecords)
                {
                    if (record == null || record.levelId < 1) continue;
                    record.bestScore = Mathf.Max(0, record.bestScore);
                    record.bestStars = Mathf.Clamp(record.bestStars, 0, 3);
                    if (records.TryGetValue(record.levelId, out var previous))
                    {
                        previous.bestScore = Mathf.Max(previous.bestScore, record.bestScore);
                        previous.bestStars = Mathf.Max(previous.bestStars, record.bestStars);
                    }
                    else { records.Add(record.levelId, record); cleanRecords.Add(record); }
                }
            d.levelRecords = cleanRecords;
            d.RebuildRecordIndex();
            if (migrate)
            {
                bool finished = d.campaignCompleted || d.level > 18 ||
                    (d.levelStars != null && d.levelStars.Length >= 18 && d.levelStars[17] > 0);
                d.highestUnlockedLevel = Math.Max(d.highestUnlockedLevel, finished ? 19 : Mathf.Clamp(d.level, 1, 18));
                for (int i = 0; i < 18; i++)
                {
                    int score = d.levelScores != null && i < d.levelScores.Length ? Mathf.Max(0, d.levelScores[i]) : 0;
                    int stars = d.levelStars != null && i < d.levelStars.Length ? Mathf.Clamp(d.levelStars[i], 0, 3) : 0;
                    if (score == 0 && stars == 0) continue;
                    var record = d.GetOrCreateLevelRecord(i + 1);
                    record.bestScore = Mathf.Max(record.bestScore, score);
                    record.bestStars = Mathf.Max(record.bestStars, stars);
                }
            }
            foreach (var record in d.levelRecords)
                if (record.bestStars > 0)
                    d.highestUnlockedLevel = Math.Max(d.highestUnlockedLevel, record.levelId < long.MaxValue ? record.levelId + 1 : long.MaxValue);
            d.version = 3;
            d.coins = Mathf.Max(0, d.coins);
            d.highScore = Mathf.Max(0, d.highScore); // legacy record is preserved, never assigned to either mode
            d.endlessBest = Mathf.Max(0, d.endlessBest);
            if (float.IsNaN(d.endlessDistance) || float.IsInfinity(d.endlessDistance)) d.endlessDistance = 0;
            d.endlessDistance = Mathf.Max(0, d.endlessDistance);
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
            {
                stream.Write(bytes, 0, bytes.Length);
#if UNITY_WEBGL && !UNITY_EDITOR
                // The asynchronous IndexedDB barrier lives in SaveManager.
                stream.Flush();
#else
                stream.Flush(true);
#endif
            }
            bool validPrimary = TryRead(path, out _);
            if (validPrimary)
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                try { File.Replace(temp, path, path + ".bak"); return; }
                catch (PlatformNotSupportedException) { }
                catch (IOException) { }
#endif
                // Platforms without Replace still retain a verified recovery copy.
                File.Copy(path, path + ".bak", true);
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
            if (!File.Exists(path + ".bak")) File.Copy(path, path + ".bak");
        }
    }
}
