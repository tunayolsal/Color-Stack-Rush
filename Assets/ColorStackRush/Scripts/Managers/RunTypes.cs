using System;
using UnityEngine;
namespace ColorStackRush
{
    public enum RunMode { Campaign, Endless }
    [Serializable] public struct RunConfig
    {
        public RunMode mode;
        public int level;
        public int seed;
        public RunConfig(RunMode mode, int level, int seed) { this.mode = mode; this.level = level; this.seed = seed; }
        public static RunConfig Campaign(int level) => new RunConfig(RunMode.Campaign, Mathf.Clamp(level, 1, 18), 7319 + Mathf.Clamp(level, 1, 18) * 104729);
        public static RunConfig Endless(int seed) => new RunConfig(RunMode.Endless, 1, seed);
        public float Length => mode == RunMode.Endless ? float.PositiveInfinity : 270f + (level - 1) * 18f;
        public float MaxSpeed => mode == RunMode.Endless ? 18f : 15f;
        public int Theme => mode == RunMode.Endless ? 0 : (level - 1) / 6;
        public bool ChangesColor => mode == RunMode.Endless || level >= 4;
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
        public const int LevelCount = 18;
        public static void Apply(SaveData data, RunResult result)
        {
            if (result.config.mode == RunMode.Endless)
            {
                data.endlessBest = Mathf.Max(data.endlessBest, result.score);
                data.endlessDistance = Mathf.Max(data.endlessDistance, result.distance);
                return;
            }
            int i = Mathf.Clamp(result.config.level, 1, LevelCount) - 1;
            data.levelScores[i] = Mathf.Max(data.levelScores[i], result.score);
            if (!result.completed) return;
            data.levelStars[i] = Mathf.Max(data.levelStars[i], result.stars);
            data.level = Mathf.Max(data.level, Mathf.Min(LevelCount, i + 2));
            if (i == LevelCount - 1) data.campaignCompleted = true;
        }
    }
}
