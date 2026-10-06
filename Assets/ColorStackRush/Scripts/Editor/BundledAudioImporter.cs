using UnityEditor;
using UnityEngine;
using System.IO;

namespace ColorStackRush.Editor
{
    /// <summary>Preload precise music-loop buffers and short effects for immediate playback.</summary>
    public sealed class BundledAudioImporter : AssetPostprocessor
    {
        // Bump when import policy changes so existing asset metadata is refreshed.
        public override uint GetVersion() => 3;

        /// <summary>Explicit batch repair for an existing checkout's cached music import.</summary>
        public static void ReimportMusic()
        {
            string path = "Assets/Audio/Resources/" + BundledAudio.MusicPath + ".wav";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null || importer.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad)
                throw new UnityException("Music reimport did not apply the precise WebGL loop policy: " + path);
            Debug.Log("Music reimport applied WebGL decompressed loop playback: " + path);
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/Resources/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool music = Path.GetFileNameWithoutExtension(assetPath) == Path.GetFileName(BundledAudio.MusicPath);
            importer.forceToMono = !music;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = true;
            // Unity WebGL's compressed media-element path ignores smpl loop bounds.
            // Decompressed buffers use native Web Audio loopStart/loopEnd instead.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = music ? .45f : .7f;
            // Preserve the WAV's exact frame indices and smpl loop boundaries.
            // WebGL encoding can alter its first 1,024 frames, so the source loop
            // contains a guard before the musical region rather than a fade.
            settings.sampleRateSetting = music ? AudioSampleRateSetting.PreserveSampleRate : AudioSampleRateSetting.OverrideSampleRate;
            if (!music) settings.sampleRateOverride = 22050u;
            importer.defaultSampleSettings = settings;
        }
    }
}
