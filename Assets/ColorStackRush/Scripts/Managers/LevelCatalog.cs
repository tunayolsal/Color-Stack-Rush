using System;
using UnityEngine;

namespace ColorStackRush
{
    public readonly struct DifficultyProfile
    {
        public readonly float Speed, InvincibilitySeconds, CorridorAmplitude;
        public readonly int RecoveryPeriod, ObstacleDamage, WrongColorDamage, DecoyCount;
        public DifficultyProfile(float speed, int recoveryPeriod, int obstacleDamage, int wrongColorDamage,
            float invincibilitySeconds, float corridorAmplitude, int decoyCount)
        {
            Speed = Mathf.Min(18, speed);
            RecoveryPeriod = recoveryPeriod;
            ObstacleDamage = obstacleDamage;
            WrongColorDamage = wrongColorDamage;
            InvincibilitySeconds = invincibilitySeconds;
            CorridorAmplitude = corridorAmplitude;
            DecoyCount = decoyCount;
        }
        public static DifficultyProfile ForLevel(long levelId)
        {
            int level = (int)Math.Min(Math.Max(1L, levelId), 18L);
            if (level <= 6)
                return new DifficultyProfile(Mathf.Lerp(10, 12, (level - 1) / 5f), 3, 3, 2, .8f,
                    Mathf.Lerp(.9f, 1.7f, (level - 1) / 5f), level == 1 ? 0 : 1);
            if (level <= 12)
                return new DifficultyProfile(Mathf.Lerp(12.5f, 14, (level - 7) / 5f), 4, 3, 2, .8f, 2.1f, 2);
            return new DifficultyProfile(Mathf.Lerp(14.5f, 16, (level - 13) / 5f), 5, 3, 2, .8f, 2.1f, 2);
        }
    }

    public readonly struct LevelDefinition
    {
        public readonly RunConfig Config;
        public readonly float Length;
        public readonly int Theme;
        public readonly DifficultyProfile Difficulty;
        public LevelDefinition(RunConfig config)
        {
            Config = config;
            Length = LevelCatalog.GetLength(config.levelId);
            Theme = LevelCatalog.GetTheme(config.levelId);
            Difficulty = DifficultyProfile.ForLevel(config.levelId);
        }
    }

    public static class LevelCatalog
    {
        public const int CurrentContentVersion = 2;
        public static uint Hash(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7feb352d;
                value ^= value >> 15;
                value *= 0x846ca68b;
                return value ^ (value >> 16);
            }
        }
        public static int SeedFor(long levelId)
        {
            ulong id = (ulong)Math.Max(1L, levelId);
            return unchecked((int)Hash((uint)id ^ Hash((uint)(id >> 32) + 0x9e3779b9u) ^ 0x43535231u));
        }
        public static LevelDefinition Get(long levelId)
        {
            levelId = Math.Max(1L, levelId);
            return new LevelDefinition(new RunConfig { levelId = levelId, seed = SeedFor(levelId), contentVersion = CurrentContentVersion });
        }
        public static float GetLength(long levelId)
        {
            levelId = Math.Max(1L, levelId);
            return levelId <= 18 ? 270f + (levelId - 1) * 18f : 480f + (Hash((uint)SeedFor(levelId) ^ 0xa511e9b3u) % 7) * 24f;
        }
        public static int GetTheme(long levelId) => (int)(((Math.Max(1L, levelId) - 1) / 6) % 3);
    }
}
