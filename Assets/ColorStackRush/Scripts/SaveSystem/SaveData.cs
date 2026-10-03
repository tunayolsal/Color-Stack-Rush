using System;
using System.Collections.Generic;

namespace ColorStackRush
{
    /// <summary>
    /// Plain serializable container for everything the game persists.
    /// Serialized to JSON by SaveManager.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        // Missing version in legacy JSON deliberately deserializes as zero.
        public int version = 0;
        public int[] levelScores = new int[18];
        public int[] levelStars = new int[18];
        public int endlessBest;
        public float endlessDistance;
        public bool campaignCompleted;
        public bool tutorialCompleted;
        public bool reducedMotion;
        public bool lowQuality;

        // --- Economy / progress ---
        public int coins = 0;
        public int highScore = 0;
        public int level = 1;

        // --- Cosmetics ---
        public List<int> unlockedSkins = new List<int> { 0 }; // first skin is free
        public int selectedSkin = 0;

        // --- Settings ---
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public bool musicOn = true;
        public bool sfxOn = true;
        public bool hapticsOn = true;

        // --- Daily reward ---
        public string lastDailyClaim = ""; // yyyy-MM-dd of last claim
        public int dailyStreak = 0;        // consecutive-day counter (0-6 loops)
    }
}
