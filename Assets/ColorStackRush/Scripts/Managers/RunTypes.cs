using System;
using UnityEngine;
namespace ColorStackRush
{
    [Serializable] public struct RunConfig
    {
        public long levelId;
        public int seed;
        public int contentVersion;
        public static RunConfig Level(long levelId) => LevelCatalog.Get(levelId).Config;
        public float Length => LevelCatalog.GetLength(levelId);
        public int Theme => LevelCatalog.GetTheme(levelId);
        public bool ChangesColor => levelId >= 4;
        public int DifficultyLevel => (int)Math.Min(Math.Max(1L, levelId), 18L);
        public DifficultyProfile Difficulty => DifficultyProfile.ForLevel(levelId);
        public float BaseSpeed => Difficulty.Speed;
        public float MaxSpeed => Difficulty.Speed;
    }
    [Serializable] public struct RunResult
    {
        public RunConfig config;
        public int score, coins, stairBonus, stairs, stars;
        public float distance;
        public bool completed;
        public static int StarsFor(int stairs) => stairs >= 14 ? 3 : stairs >= 8 ? 2 : 1;
    }
    public static class Progression
    {
        public static bool IsUnlocked(SaveData data, long levelId) => levelId >= 1 && levelId <= data.highestUnlockedLevel;
        public static int BestScore(SaveData data, long levelId) => data.FindLevelRecord(levelId)?.bestScore ?? 0;
        public static int Stars(SaveData data, long levelId) => data.FindLevelRecord(levelId)?.bestStars ?? 0;
        public static void Apply(SaveData data, RunResult result)
        {
            if (result.config.levelId < 1) return;
            var record = data.GetOrCreateLevelRecord(result.config.levelId);
            record.bestScore = Mathf.Max(record.bestScore, result.score);
            if (!result.completed) return;
            record.bestStars = Mathf.Max(record.bestStars, Mathf.Clamp(result.stars, 0, 3));
            long next = result.config.levelId < long.MaxValue ? result.config.levelId + 1 : long.MaxValue;
            data.highestUnlockedLevel = Math.Max(data.highestUnlockedLevel, next);
        }
        // The transaction holds SaveManager's lock while this snapshot is live.
        public struct Snapshot
        {
            readonly long levelId, highestUnlockedLevel;
            readonly bool hadRecord;
            readonly int bestScore, bestStars;
            public Snapshot(SaveData data, long levelId)
            {
                this.levelId = levelId;
                highestUnlockedLevel = data.highestUnlockedLevel;
                var record = data.FindLevelRecord(levelId);
                hadRecord = record != null;
                bestScore = record?.bestScore ?? 0;
                bestStars = record?.bestStars ?? 0;
            }
            public void Restore(SaveData data)
            {
                data.highestUnlockedLevel = highestUnlockedLevel;
                if (!hadRecord) data.RemoveLevelRecord(levelId);
                else
                {
                    var record = data.GetOrCreateLevelRecord(levelId);
                    record.bestScore = bestScore;
                    record.bestStars = bestStars;
                }
            }
        }
    }
}
