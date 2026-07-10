using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Wraps device vibration behind a settings check. Uses the built-in
    /// Handheld.Vibrate (available without plugins). Strength methods exist
    /// so a richer haptics plugin can be dropped in later with no call-site changes.
    /// </summary>
    public static class HapticsManager
    {
        public static void Light()  => Vibrate();
        public static void Medium() => Vibrate();
        public static void Heavy()  => Vibrate();

        static void Vibrate()
        {
            if (!SaveManager.Data.hapticsOn) return;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
