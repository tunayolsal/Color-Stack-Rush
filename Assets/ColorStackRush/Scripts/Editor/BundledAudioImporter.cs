using UnityEditor;
using UnityEngine;

namespace ColorStackRush.Editor
{
    /// <summary>Keep the loop compressed and short effects ready for immediate playback.</summary>
    public sealed class BundledAudioImporter : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = assetPath.EndsWith("/HappyClappyLoop.ogg");
            importer.forceToMono = !music;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? .45f : .7f;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = music ? 44100u : 22050u;
            importer.defaultSampleSettings = settings;
        }
    }
}
