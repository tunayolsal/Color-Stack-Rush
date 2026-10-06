using System;
using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorStackRush.Tests
{
    public class MusicPlaybackTests
    {
        GameObject audioObject;
        AudioManager audio;
        AudioSource music;
        string folder;

        [SetUp] public void Setup()
        {
            folder = Path.Combine(Path.GetTempPath(), "csr-music-" + Guid.NewGuid());
            SaveManager.SetStorageDirectoryForTests(folder);
            SaveManager.Data.musicOn = true;
            SaveManager.Data.sfxOn = false;
            audioObject = new GameObject("MusicPlaybackTest", typeof(AudioListener));
            audio = audioObject.AddComponent<AudioManager>();
            foreach (var source in audioObject.GetComponents<AudioSource>())
                if (source.clip == BundledAudio.LoadMusic()) music = source;
            Assert.That(music, Is.Not.Null);
        }

        [TearDown] public void Cleanup()
        {
            UnityEngine.Object.DestroyImmediate(audioObject);
            SaveManager.SetStorageDirectoryForTests(null);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [UnityTest] public IEnumerator MusicWaitsForFirstPlayAndUsesNativeLooping()
        {
            Assert.That(music.loop, Is.True);
            Assert.That(music.playOnAwake, Is.False);
            Assert.That(music.isPlaying, Is.False, "The menu must not autoplay audio.");
            audio.UnlockAudio();
            Assert.That(music.isPlaying, Is.False, "A gesture alone must not start music before the first run.");
            GameEvents.RaiseRunStarted();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(music.isPlaying, Is.True);
            Assert.That(music.time, Is.GreaterThan(.1f), "The first play must advance the real audio clock.");
        }

        [UnityTest] public IEnumerator ResultsRetriesAndVolumeChangesKeepTheCurrentMusicPosition()
        {
            GameEvents.RaiseRunStarted();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(music.time, Is.GreaterThan(.1f));
            float before = music.time;
            GameEvents.RaiseStateChanged(GameState.GameOver);
            GameEvents.RaiseStateChanged(GameState.MainMenu);
            GameEvents.RaiseStateChanged(GameState.Victory);
            GameEvents.RaiseRunStarted();
            audio.SetMusicVolume(.35f);
            Assert.That(music.time, Is.GreaterThanOrEqualTo(before - .03f), "Results, retries and the volume slider must not rewind the loop.");
            Assert.That(music.isPlaying, Is.True);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(music.time, Is.GreaterThan(before), "Music must continue across the run transitions.");
        }

        [UnityTest] public IEnumerator NativeMusicLoopSkipsTheEncoderGuardAcrossThreeWraps()
        {
            // Editor PlayMode can inspect the shipping WAV's actual smpl region;
            // the assertion is independent of the helper's guard-frame default.
            var region = ReadShippingLoopRegion();
            Assert.That(region.start, Is.GreaterThan(.025), "This fixture needs a measurable encoder guard.");
            GameEvents.RaiseRunStarted();
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(music.isPlaying, Is.True);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Seek through the public audio API, then let the real audio clock
                // cross the boundary. No manual replay or synthetic frame clock.
                music.time = (float)(region.end - .15);
                float previousPosition = music.time;
                double previousDsp = AudioSettings.dspTime;
                float deadline = Time.realtimeSinceStartup + 2f;
                bool wrapped = false;
                while (Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    Assert.That(music.isPlaying, Is.True, "The native loop must not stop at its boundary.");
                    float position = music.time;
                    double dsp = AudioSettings.dspTime;
                    if (position < previousPosition - .05f)
                    {
                        double afterBoundary = Math.Max(0, dsp - previousDsp - (region.end - previousPosition));
                        double inferredStart = position - afterBoundary;
                        // Account for one small mixer/query scheduling discrepancy,
                        // while still distinguishing the real guard from zero.
                        Assert.That(inferredStart, Is.EqualTo(region.start).Within(.02),
                            "The loop must wrap to smpl's musical start, not replay its leading encoder silence.");
                        Assert.That(position, Is.GreaterThanOrEqualTo((float)region.start - .002f));
                        Assert.That(position, Is.LessThan(1f));
                        wrapped = true;
                        break;
                    }
                    previousPosition = position;
                    previousDsp = dsp;
                }
                Assert.That(wrapped, Is.True, "The real audio clock must cross the loop boundary.");
            }
        }

        static (double start, double end) ReadShippingLoopRegion()
        {
            string path = Path.Combine(Application.dataPath, "Audio/Resources/" + BundledAudio.MusicPath + ".wav");
            Assert.That(File.Exists(path), Is.True, "This native Editor PlayMode test needs the source WAV metadata.");
            byte[] wav = File.ReadAllBytes(path);
            int rate = 0;
            uint start = 0, end = 0;
            bool hasLoop = false;
            for (int offset = 12; offset + 8 <= wav.Length;)
            {
                uint length = BitConverter.ToUInt32(wav, offset + 4);
                Assert.That((long)offset + 8 + length, Is.LessThanOrEqualTo(wav.Length));
                string id = Encoding.ASCII.GetString(wav, offset, 4);
                int payload = offset + 8;
                if (id == "fmt ") rate = checked((int)BitConverter.ToUInt32(wav, payload + 4));
                if (id == "smpl")
                {
                    Assert.That(BitConverter.ToUInt32(wav, payload + 28), Is.EqualTo(1));
                    start = BitConverter.ToUInt32(wav, payload + 44);
                    end = BitConverter.ToUInt32(wav, payload + 48);
                    hasLoop = true;
                }
                offset = checked(offset + 8 + (int)length + ((int)length & 1));
            }
            Assert.That(rate, Is.GreaterThan(0));
            Assert.That(hasLoop, Is.True);
            return (start / (double)rate, (end + 1) / (double)rate);
        }
    }
}
