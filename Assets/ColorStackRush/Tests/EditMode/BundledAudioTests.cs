using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorStackRush.Tests
{
    public class BundledAudioTests
    {
        [Test] public void ShippingAudioUsesDistinctBundledClipsAndBoundedImportMemory()
        {
            var music = BundledAudio.LoadMusic();
            Assert.That(music, Is.Not.Null, "CC0 music must be bundled, not synthesized.");
            Assert.That(music.length, Is.InRange(17.3f, 17.6f));
            var musicImporter = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(music));
            Assert.That(musicImporter.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.CompressedInMemory));
            Assert.That(musicImporter.defaultSampleSettings.preloadAudioData, Is.True);
            Assert.That(musicImporter.forceToMono, Is.False);
            var paths = new System.Collections.Generic.HashSet<string>();
            float decodedBytes = 0;
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
            {
                var effect = BundledAudio.LoadEffect(id);
                Assert.That(effect, Is.Not.Null, id + " must use its bundled effect.");
                string path = AssetDatabase.GetAssetPath(effect);
                Assert.That(paths.Add(path), Is.True, id + " must have a distinct clip.");
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.forceToMono, Is.True, id.ToString());
                Assert.That(importer.defaultSampleSettings.preloadAudioData, Is.True, id.ToString());
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), id.ToString());
                decodedBytes += effect.samples * effect.channels * sizeof(float);
            }
            Assert.That(decodedBytes, Is.LessThan(2 * 1024 * 1024), "Short effects must not consume excessive decoded memory.");
        }
    }
}
