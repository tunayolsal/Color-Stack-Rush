using UnityEngine;

namespace ColorStackRush
{
    /// <summary>Resource paths shared by runtime loading and asset validation.</summary>
    public static class BundledAudio
    {
        public const string MusicPath = "Audio/ArcadeGrooveLoop";
        public static AudioClip LoadMusic() => Resources.Load<AudioClip>(MusicPath);
        public static AudioClip LoadEffect(SfxId id) => Resources.Load<AudioClip>("Audio/" + id);
    }
}
