using System;
using UnityEngine;

namespace ColorStackRush
{
    /// <summary>
    /// Procedural audio synthesizer. Generates every sound effect and the
    /// background music loop from pure math at startup, so the project ships
    /// with zero audio assets.
    /// </summary>
    public static class SfxSynth
    {
        const int SampleRate = 44100;

        /// <summary>Builds the AudioClip for a given sound effect id.</summary>
        public static AudioClip Generate(SfxId id)
        {
            switch (id)
            {
                case SfxId.Collect:
                    return Render("sfx_collect", 0.14f, t => Sine(Mathf.Lerp(620f, 980f, t / 0.14f), t) * Decay(t, 0.14f));
                case SfxId.Wrong:
                    return Render("sfx_wrong", 0.22f, t => Square(Mathf.Lerp(230f, 150f, t / 0.22f), t) * 0.5f * Decay(t, 0.22f));
                case SfxId.Hit:
                    return Render("sfx_hit", 0.28f, t =>
                        (Noise() * 0.55f + Sine(85f, t) * 0.7f) * Decay(t, 0.28f, 3f));
                case SfxId.Coin:
                    return Render("sfx_coin", 0.2f, t =>
                        t < 0.08f ? Sine(988f, t) * Decay(t, 0.08f)
                                  : Sine(1319f, t) * Decay(t - 0.08f, 0.12f));
                case SfxId.Button:
                    return Render("sfx_button", 0.07f, t => Sine(700f, t) * Decay(t, 0.07f));
                case SfxId.PowerUp:
                    return Render("sfx_powerup", 0.38f, t => Sine(Mathf.Lerp(420f, 1250f, t / 0.38f), t) * Decay(t, 0.38f, 1.4f));
                case SfxId.Stair:
                    return Render("sfx_stair", 0.1f, t => Sine(520f, t) * Decay(t, 0.1f));
                case SfxId.ColorChange:
                    return Render("sfx_colorchange", 0.34f, t =>
                        (t < 0.15f ? Sine(660f, t) : Sine(880f, t)) * 0.7f * Decay(t % 0.17f, 0.15f));
                case SfxId.Buy:
                    return Render("sfx_buy", 0.3f, t =>
                        t < 0.1f ? Sine(880f, t) * Decay(t, 0.1f)
                                 : Sine(1175f, t) * Decay(t - 0.1f, 0.2f));
                case SfxId.Win:
                    return Render("sfx_win", 0.9f, t =>
                    {
                        // Rising major arpeggio: C5 E5 G5 C6
                        float[] notes = { 523f, 659f, 784f, 1047f };
                        int step = Mathf.Min(3, (int)(t / 0.2f));
                        return Sine(notes[step], t) * Decay(t - step * 0.2f, 0.24f);
                    });
                case SfxId.Lose:
                    return Render("sfx_lose", 0.8f, t => Sine(Mathf.Lerp(392f, 147f, t / 0.8f), t) * 0.7f * Decay(t, 0.8f, 1.2f));
                default:
                    return Render("sfx_default", 0.1f, t => Sine(440f, t) * Decay(t, 0.1f));
            }
        }

        /// <summary>
        /// Generates a soft, loopable background track: a gentle pentatonic
        /// arpeggio over a 4-chord progression at 100 BPM (9.6 s loop).
        /// </summary>
        public static AudioClip GenerateMusicLoop()
        {
            const float bpm = 100f;
            const float beatsPerBar = 4f;
            const int bars = 4;
            float beatDur = 60f / bpm;
            float duration = beatDur * beatsPerBar * bars;

            // Chord roots: C - A minor - F - G (frequencies of root notes)
            float[][] chords =
            {
                new[] { 261.6f, 329.6f, 392.0f }, // C  E  G
                new[] { 220.0f, 261.6f, 329.6f }, // A  C  E
                new[] { 174.6f, 220.0f, 261.6f }, // F  A  C
                new[] { 196.0f, 246.9f, 293.7f }  // G  B  D
            };

            return Render("music_loop", duration, t =>
            {
                float beat = t / beatDur;
                int bar = Mathf.Min(bars - 1, (int)(beat / beatsPerBar));
                float[] chord = chords[bar];

                // Arpeggio: one chord tone per eighth note, one octave up.
                int eighth = (int)(beat * 2f);
                float noteT = (beat * 2f) - eighth;                // 0..1 within the eighth
                float arpFreq = chord[eighth % 3] * 2f;
                float arp = Sine(arpFreq, t) * Decay(noteT * beatDur * 0.5f, beatDur * 0.5f, 1.6f) * 0.35f;

                // Soft bass: chord root each beat.
                float beatT = beat - (int)beat;
                float bass = Sine(chord[0] * 0.5f, t) * Decay(beatT * beatDur, beatDur, 1.1f) * 0.3f;

                return arp + bass;
            }, volume: 0.6f);
        }

        // --- Synthesis primitives ---

        static float Sine(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);

        static float Square(float freq, float t) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.6f;

        static float Noise() => UnityEngine.Random.Range(-1f, 1f);

        /// <summary>Exponential-ish fade-out envelope with a tiny attack to avoid clicks.</summary>
        static float Decay(float t, float duration, float sharpness = 2f)
        {
            if (t < 0f) return 0f;
            float attack = Mathf.Clamp01(t / 0.005f); // 5 ms attack ramp
            float n = Mathf.Clamp01(t / duration);
            return attack * Mathf.Pow(1f - n, sharpness);
        }

        /// <summary>Renders a sampler function into an AudioClip.</summary>
        static AudioClip Render(string name, float duration, Func<float, float> sampler, float volume = 0.5f)
        {
            int samples = Mathf.CeilToInt(SampleRate * duration);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(sampler(t) * volume, -1f, 1f);
            }
            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
