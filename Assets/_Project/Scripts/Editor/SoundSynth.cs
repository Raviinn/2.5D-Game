using System;
using System.IO;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Synthesises placeholder sounds (16-bit mono WAV) from noise, sines and envelopes: whooshes, thumps,
    /// clangs, chimes and seamless ambience loops. Each sound has a fixed seed, so re-running gives the same file.
    /// </summary>
    public static class SoundSynth
    {
        public const int SampleRate = 22050;
        const float Tau = Mathf.PI * 2f;

        // ---------- Effects ----------

        public static float[] Swing(int variant) => Whoosh(0.26f + variant * 0.03f, 650f, 3200f, 900f, 100 + variant);
        public static float[] HeavySwing(int variant)
        {
            var s = Whoosh(0.42f + variant * 0.04f, 280f, 1500f, 380f, 200 + variant);
            Mix(s, 0f, Sweep(0.4f, 95f, 55f, t => Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.4f)) * 0.35f));
            return s;
        }

        public static float[] Hit(int variant)
        {
            var rng = new System.Random(300 + variant);
            var s = Noise(0.2f, rng, 1700f + variant * 300f, t => Mathf.Exp(-t * 32f));
            Mix(s, 0f, Sweep(0.2f, 150f - variant * 15f, 55f, t => Mathf.Exp(-t * 18f) * 1.2f));
            return s;
        }

        public static float[] Block(int variant)
        {
            float f = 600f + variant * 70f;
            var s = Partials(0.5f, new[] { f, f * 2.41f, f * 3.77f, f * 5.1f }, new[] { 1f, 0.6f, 0.4f, 0.25f }, new[] { 8f, 10f, 13f, 16f });
            Mix(s, 0f, Noise(0.012f, new System.Random(400 + variant), 6000f, t => 1f - t / 0.012f));
            return s;
        }

        public static float[] Parry()
        {
            float f = 1240f;
            var s = Partials(0.95f, new[] { f, f * 2.01f, f * 2.98f, f * 4.1f, f * 1.005f }, new[] { 1f, 0.55f, 0.35f, 0.2f, 0.6f }, new[] { 4.5f, 5.5f, 7f, 9f, 4.5f });
            Mix(s, 0f, Noise(0.015f, new System.Random(450), 7000f, t => 1f - t / 0.015f));
            return s;
        }

        public static float[] Death()
        {
            var s = Sweep(0.6f, 95f, 40f, t => Mathf.Exp(-t * 6f) * 1.2f);
            Mix(s, 0f, Noise(0.5f, new System.Random(500), 550f, t => Mathf.Exp(-t * 9f) * 0.9f));
            Mix(s, 0.12f, Noise(0.25f, new System.Random(501), 380f, t => Mathf.Exp(-t * 14f) * 0.6f)); // second bump
            return s;
        }

        public static float[] Dodge() => Whoosh(0.3f, 500f, 1500f, 600f, 600, rustle: true);

        public static float[] Footstep(int variant)
        {
            var rng = new System.Random(700 + variant);
            var s = Noise(0.1f, rng, 700f + variant * 120f, t => Mathf.Exp(-t * 45f));
            Mix(s, 0f, Sweep(0.08f, 120f + variant * 10f, 70f, t => Mathf.Exp(-t * 55f) * 0.6f));
            return s;
        }

        public static float[] Climb(int variant) => Noise(0.15f, new System.Random(800 + variant), 2200f, t => Mathf.Exp(-t * 18f) * (0.6f + 0.4f * Mathf.Sin(t * 180f)), highPass: 500f);

        public static float[] Pickup()
        {
            var s = Tone(0.07f, 880f, Wave.Triangle, t => Env(t, 0.005f, 0.07f));
            Mix(s, 0.06f, Tone(0.12f, 1320f, Wave.Triangle, t => Env(t, 0.005f, 0.12f)));
            return s;
        }

        public static float[] Coins()
        {
            var rng = new System.Random(900);
            var s = new float[Samples(0.45f)];
            for (int i = 0; i < 6; i++)
            {
                float at = i * 0.045f + (float)rng.NextDouble() * 0.02f;
                float f = 2400f + (float)rng.NextDouble() * 1900f;
                Mix(s, at, Partials(0.25f, new[] { f, f * 2.7f }, new[] { 1f, 0.4f }, new[] { 22f, 30f }), 0.7f - i * 0.07f);
            }
            return s;
        }

        public static float[] Eat()
        {
            var s = new float[Samples(0.4f)];
            for (int i = 0; i < 3; i++)
                Mix(s, i * 0.12f, Noise(0.06f, new System.Random(1000 + i), 3500f, t => Mathf.Exp(-t * 50f), highPass: 900f), 1f - i * 0.15f);
            return s;
        }

        public static float[] Drink()
        {
            var s = new float[Samples(0.45f)];
            for (int i = 0; i < 3; i++)
                Mix(s, i * 0.13f, Sweep(0.1f, 260f - i * 15f, 150f, t => Env(t, 0.01f, 0.1f)), 0.9f);
            Mix(s, 0f, Noise(0.42f, new System.Random(1100), 900f, t => 0.15f));
            return s;
        }

        public static float[] Till()
        {
            var s = Sweep(0.25f, 90f, 50f, t => Mathf.Exp(-t * 16f));
            Mix(s, 0.01f, Noise(0.24f, new System.Random(1200), 1400f, t => Mathf.Exp(-t * 12f) * 0.8f));
            return s;
        }

        public static float[] Plant()
        {
            var s = Noise(0.12f, new System.Random(1300), 700f, t => Mathf.Exp(-t * 35f));
            Mix(s, 0.04f, Tone(0.08f, 600f, Wave.Sine, t => Env(t, 0.005f, 0.08f) * 0.3f));
            return s;
        }

        public static float[] Water()
        {
            var rng = new System.Random(1400);
            var s = Noise(0.65f, rng, 2600f, t => Env(t, 0.08f, 0.65f) * (0.55f + 0.25f * Mathf.Sin(t * Tau * 28f)));
            for (int i = 0; i < 7; i++)
            {
                float at = (float)rng.NextDouble() * 0.5f;
                Mix(s, at, Sweep(0.05f, 1100f + (float)rng.NextDouble() * 900f, 2200f, t => Env(t, 0.002f, 0.05f) * 0.35f));
            }
            return s;
        }

        public static float[] Harvest()
        {
            var s = Noise(0.06f, new System.Random(1500), 2500f, t => Mathf.Exp(-t * 60f) * 0.7f);
            Mix(s, 0.02f, Sweep(0.22f, 300f, 720f, t => Mathf.Exp(-t * 14f)));
            return s;
        }

        public static float[] UiClick()
        {
            var s = Tone(0.04f, 1800f, Wave.Sine, t => Mathf.Exp(-t * 140f));
            Mix(s, 0f, Noise(0.006f, new System.Random(1600), 5000f, t => 0.5f));
            return s;
        }

        // ---------- Chimes ----------

        const float C5 = 523.25f, E5 = 659.25f, G5 = 783.99f, C6 = 1046.5f, E6 = 1318.5f, G4 = 392f;

        public static float[] QuestAccepted() => Arpeggio(new[] { C5, E5, G5 }, 0.1f, 0.45f);
        public static float[] QuestReady() => Arpeggio(new[] { G5, C6 }, 0.11f, 0.4f);

        public static float[] QuestComplete()
        {
            var s = Arpeggio(new[] { C5, E5, G5, C6 }, 0.11f, 0.7f);
            Mix(s, 0.33f, Chord(new[] { C5, G5 }, 0.7f), 0.35f);
            return s;
        }

        public static float[] LevelUp()
        {
            var s = Arpeggio(new[] { C5, E5, G5, C6, E6 }, 0.07f, 0.5f);
            Mix(s, 0.3f, Chord(new[] { C5, E5, G5, C6 }, 1.1f), 0.45f);
            Mix(s, 0.3f, Tone(1.0f, 2093f, Wave.Sine, t => Env(t, 0.05f, 1f) * 0.15f * (0.5f + 0.5f * Mathf.Sin(t * Tau * 9f))));
            return s;
        }

        public static float[] Sleep()
        {
            var s = new float[Samples(1.4f)];
            float[] notes = { E5, C5, G4 };
            for (int i = 0; i < notes.Length; i++)
                Mix(s, i * 0.32f, Tone(0.7f, notes[i], Wave.Sine, t => Env(t, 0.03f, 0.7f)), 0.7f);
            return s;
        }

        static float[] Arpeggio(float[] notes, float step, float tail)
        {
            var s = new float[Samples(step * notes.Length + tail)];
            for (int i = 0; i < notes.Length; i++)
                Mix(s, i * step, Tone(step + tail, notes[i], Wave.Triangle, t => Env(t, 0.006f, step + tail)), 0.8f);
            return s;
        }

        static float[] Chord(float[] notes, float duration)
        {
            var s = new float[Samples(duration)];
            foreach (float n in notes)
                Mix(s, 0f, Tone(duration, n, Wave.Sine, t => Env(t, 0.02f, duration)), 1f / notes.Length);
            return s;
        }

        // ---------- Ambience loops ----------

        const float LoopSeconds = 12f;
        const float LoopFade = 1.5f;

        public static float[] AmbienceDay() => Loop(1700, (s, rng) =>
        {
            Mix(s, 0f, Wind(s.Length, rng, 380f, 0.55f));
            float t = 0.4f;
            while (t < LoopSeconds + LoopFade - 1f)
            {
                // A bird: a few quick chirps.
                float baseF = 2600f + (float)rng.NextDouble() * 1400f;
                int chirps = 2 + rng.Next(3);
                for (int i = 0; i < chirps; i++)
                {
                    float d = 0.06f + (float)rng.NextDouble() * 0.08f;
                    Mix(s, t + i * (d + 0.05f), Sweep(d, baseF, baseF * (1.15f + (float)rng.NextDouble() * 0.3f), x => Env(x, 0.01f, d) * 0.22f));
                }
                t += 0.9f + (float)rng.NextDouble() * 1.8f;
            }
        });

        public static float[] AmbienceNight() => Loop(1800, (s, rng) =>
        {
            Mix(s, 0f, Wind(s.Length, rng, 300f, 0.45f));
            foreach (var (freq, rate, offset) in new[] { (4400f, 3.1f, 0f), (4750f, 2.6f, 0.37f) })
            {
                for (float t = offset; t < LoopSeconds + LoopFade - 0.2f; t += 1f / rate)
                    for (int p = 0; p < 3; p++)
                        Mix(s, t + p * 0.02f, Tone(0.014f, freq, Wave.Sine, x => Env(x, 0.002f, 0.014f) * 0.09f));
            }
        });

        public static float[] AmbienceRain() => Loop(1900, (s, rng) =>
        {
            Mix(s, 0f, Noise(s.Length / (float)SampleRate, rng, 5500f, x => 0.5f, highPass: 350f));
            for (int i = 0; i < 160; i++)
            {
                float at = (float)rng.NextDouble() * (LoopSeconds + LoopFade - 0.1f);
                Mix(s, at, Noise(0.02f, rng, 4000f, x => Mathf.Exp(-x * 120f) * 0.35f, highPass: 1200f));
            }
        });

        public static float[] AmbienceMenu() => Loop(2000, (s, rng) =>
        {
            Mix(s, 0f, Wind(s.Length, rng, 260f, 0.6f));
            Mix(s, 0f, Tone(LoopSeconds + LoopFade, 55f, Wave.Sine, x => 0.05f));
        });

        /// <summary>Soft, gusting wind: brown noise through a low-pass, swelling slowly.</summary>
        static float[] Wind(int length, System.Random rng, float cutoff, float gain)
        {
            var s = new float[length];
            float brown = 0f, low = 0f, a = OnePole(cutoff);
            for (int i = 0; i < length; i++)
            {
                brown = brown * 0.995f + ((float)rng.NextDouble() * 2f - 1f) * 0.08f;
                low += a * (brown - low);
                float t = i / (float)SampleRate;
                float gust = 0.6f + 0.25f * Mathf.Sin(t * Tau / 6f) + 0.15f * Mathf.Sin(t * Tau / 2.3f + 1f);
                s[i] = low * gain * gust * 3f;
            }
            return s;
        }

        /// <summary>Draws LoopSeconds + LoopFade, then blends the extra tail over the start, so the loop has no seam.</summary>
        static float[] Loop(int seed, Action<float[], System.Random> draw)
        {
            var rng = new System.Random(seed);
            var full = new float[Samples(LoopSeconds + LoopFade)];
            draw(full, rng);
            int loop = Samples(LoopSeconds), fade = full.Length - loop;
            var s = new float[loop];
            Array.Copy(full, s, loop);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                s[i] = full[loop + i] * (1f - k) + full[i] * k;
            }
            return s;
        }

        // ---------- Building blocks ----------

        enum Wave { Sine, Triangle }

        static int Samples(float seconds) => Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate));

        static float OnePole(float cutoff) => 1f - Mathf.Exp(-Tau * cutoff / SampleRate);

        /// <summary>Attack, then a smooth fall to silence at 'duration'.</summary>
        static float Env(float t, float attack, float duration)
        {
            if (t < attack) return t / attack;
            float k = Mathf.Clamp01((t - attack) / Mathf.Max(0.0001f, duration - attack));
            return (1f - k) * (1f - k);
        }

        static float[] Tone(float duration, float freq, Wave wave, Func<float, float> envelope)
        {
            var s = new float[Samples(duration)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float phase = t * freq % 1f;
                float v = wave == Wave.Sine ? Mathf.Sin(phase * Tau) : 4f * Mathf.Abs(phase - 0.5f) - 1f;
                s[i] = v * envelope(t);
            }
            return s;
        }

        /// <summary>A sine gliding from one pitch to another.</summary>
        static float[] Sweep(float duration, float from, float to, Func<float, float> envelope)
        {
            var s = new float[Samples(duration)];
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                phase += Mathf.Lerp(from, to, t / duration) / SampleRate;
                s[i] = Mathf.Sin(phase * Tau) * envelope(t);
            }
            return s;
        }

        /// <summary>Inharmonic ringing (metal): each partial decays at its own rate.</summary>
        static float[] Partials(float duration, float[] freqs, float[] amps, float[] decays)
        {
            var s = new float[Samples(duration)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SampleRate;
                float v = 0f;
                for (int p = 0; p < freqs.Length; p++) v += Mathf.Sin(t * freqs[p] * Tau) * amps[p] * Mathf.Exp(-t * decays[p]);
                s[i] = v * Mathf.Min(1f, t / 0.002f);
            }
            return s;
        }

        /// <summary>White noise through a low-pass (and optionally a high-pass), shaped by an envelope.</summary>
        static float[] Noise(float duration, System.Random rng, float lowPass, Func<float, float> envelope, float highPass = 0f)
        {
            var s = new float[Samples(duration)];
            float low = 0f, lowForHigh = 0f, a = OnePole(lowPass), b = highPass > 0f ? OnePole(highPass) : 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float x = (float)rng.NextDouble() * 2f - 1f;
                low += a * (x - low);
                float v = low;
                if (highPass > 0f)
                {
                    lowForHigh += b * (v - lowForHigh);
                    v -= lowForHigh;
                }
                s[i] = v * envelope(i / (float)SampleRate);
            }
            return s;
        }

        /// <summary>Air rushing past: noise whose brightness rises then falls (a blade or a body moving fast).</summary>
        static float[] Whoosh(float duration, float startCutoff, float peakCutoff, float endCutoff, int seed, bool rustle = false)
        {
            var rng = new System.Random(seed);
            var s = new float[Samples(duration)];
            float low = 0f, lowForHigh = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)s.Length;
                float cutoff = t < 0.45f ? Mathf.Lerp(startCutoff, peakCutoff, t / 0.45f) : Mathf.Lerp(peakCutoff, endCutoff, (t - 0.45f) / 0.55f);
                float x = (float)rng.NextDouble() * 2f - 1f;
                low += OnePole(cutoff) * (x - low);
                lowForHigh += OnePole(cutoff * 0.35f) * (low - lowForHigh);
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t), 2f);
                if (rustle) env *= 0.7f + 0.3f * Mathf.Sin(t * duration * Tau * 40f);
                s[i] = (low - lowForHigh) * env * 2.5f;
            }
            return s;
        }

        /// <summary>Adds 'add' into 's' starting at 'at' seconds (clipped to the length of s).</summary>
        static void Mix(float[] s, float at, float[] add, float gain = 1f)
        {
            int start = Samples(at);
            if (at <= 0f) start = 0;
            for (int i = 0; i < add.Length && start + i < s.Length; i++) s[start + i] += add[i] * gain;
        }

        // ---------- Output ----------

        /// <summary>Scales to the given peak, fades the last few ms (no clicks) and writes a 16-bit mono WAV.</summary>
        public static void WriteWav(string path, float[] samples, float peak = 0.9f, bool fadeEnds = true)
        {
            float max = 0.0001f;
            foreach (float v in samples) max = Mathf.Max(max, Mathf.Abs(v));
            float scale = peak / max;
            int fade = fadeEnds ? Mathf.Min(samples.Length / 4, Samples(0.004f)) : 0;

            using var stream = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);          // PCM
            w.Write((short)1);          // mono
            w.Write(SampleRate);
            w.Write(SampleRate * 2);    // bytes per second
            w.Write((short)2);          // block align
            w.Write((short)16);         // bits per sample
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);
            for (int i = 0; i < samples.Length; i++)
            {
                float v = samples[i] * scale;
                if (fade > 0 && i >= samples.Length - fade) v *= (samples.Length - i) / (float)fade;
                w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            }
        }
    }
}
