using System;
using System.IO;
using System.Text;
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
            string musicPath = AssetDatabase.GetAssetPath(music);
            Assert.That(Path.GetFileNameWithoutExtension(musicPath), Is.EqualTo("ArcadeGrooveLoop"));
            Assert.That(Path.GetExtension(musicPath), Is.EqualTo(".wav"), "The WebGL loop workaround needs WAV smpl metadata.");
            Assert.That(music.length, Is.GreaterThan(1f));
            var musicImporter = (AudioImporter)AssetImporter.GetAtPath(musicPath);
            Assert.That(musicImporter.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), "Precise Web Audio buffer looping must honor the WAV's smpl bounds.");
            Assert.That(musicImporter.defaultSampleSettings.preloadAudioData, Is.True);
            Assert.That(musicImporter.forceToMono, Is.False);
            Assert.That(musicImporter.loadInBackground, Is.False);
            Assert.That(musicImporter.defaultSampleSettings.sampleRateSetting, Is.EqualTo(AudioSampleRateSetting.PreserveSampleRate));
            long decodedMusicBytes = (long)music.samples * music.channels * sizeof(float);
            Assert.That(decodedMusicBytes, Is.LessThanOrEqualTo(32L * 1024 * 1024), "The precise WebGL music buffer must stay within the 32 MiB budget at its preserved source rate.");
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

        [Test] public void ShippingMusicLoopSkipsAacGuardAndCoversTheEntireMusicalRegion()
        {
            var music = BundledAudio.LoadMusic();
            Assert.That(music, Is.Not.Null);
            byte[] wav = File.ReadAllBytes(AssetDatabase.GetAssetPath(music));
            Assert.That(Encoding.ASCII.GetString(wav, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(Encoding.ASCII.GetString(wav, 8, 4), Is.EqualTo("WAVE"));
            int fmt = FindChunk(wav, "fmt ");
            int data = FindChunk(wav, "data");
            int smpl = FindChunk(wav, "smpl");
            Assert.That(BitConverter.ToUInt16(wav, fmt), Is.EqualTo(1), "The inspected source must be integer PCM.");
            int channels = BitConverter.ToUInt16(wav, fmt + 2);
            int rate = checked((int)BitConverter.ToUInt32(wav, fmt + 4));
            int frameBytes = BitConverter.ToUInt16(wav, fmt + 12);
            int bits = BitConverter.ToUInt16(wav, fmt + 14);
            uint dataBytes = BitConverter.ToUInt32(wav, data - 4);
            uint frames = dataBytes / (uint)frameBytes;
            Assert.That(dataBytes % frameBytes, Is.Zero, "PCM must contain complete sample frames.");
            Assert.That(BitConverter.ToUInt32(wav, smpl + 28), Is.EqualTo(1), "Exactly one forward loop is required.");
            Assert.That(BitConverter.ToUInt32(wav, smpl + 40), Is.Zero, "The loop must play forward.");
            uint start = BitConverter.ToUInt32(wav, smpl + 44);
            uint end = BitConverter.ToUInt32(wav, smpl + 48);
            Assert.That(start, Is.GreaterThanOrEqualTo(1024u), "AAC's first 1,024 source frames must be outside the loop.");
            Assert.That(end, Is.EqualTo(frames - 1), "The inclusive end must preserve the last musical frame.");
            Assert.That(end, Is.GreaterThan(start));
            byte silence = bits == 8 ? (byte)128 : (byte)0;
            for (int i = 0; i < checked((int)start * frameBytes); i++)
                if (wav[data + i] != silence) Assert.Fail("The encoder guard must contain PCM silence.");
            Assert.That(music.channels, Is.EqualTo(channels));
            Assert.That(music.frequency, Is.EqualTo(rate), "Resampling must not alter smpl frame indices.");
            Assert.That(music.samples, Is.EqualTo(frames));
            Assert.That(music.length, Is.EqualTo(frames / (float)rate).Within(1f / rate));
        }

        static int FindChunk(byte[] wav, string id)
        {
            for (int offset = 12; offset + 8 <= wav.Length;)
            {
                uint length = BitConverter.ToUInt32(wav, offset + 4);
                Assert.That((long)offset + 8 + length, Is.LessThanOrEqualTo(wav.Length), "RIFF chunk is truncated.");
                if (Encoding.ASCII.GetString(wav, offset, 4) == id) return offset + 8;
                offset = checked(offset + 8 + (int)length + ((int)length & 1));
            }
            Assert.Fail("Required WAV chunk missing: " + id);
            return -1;
        }
    }
}
