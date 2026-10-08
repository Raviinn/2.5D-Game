using System;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Synthesises placeholder music: four seamless loops for the title screen, day, night and combat. Small modal
    /// pieces on a plucked lute / harp (Karplus-Strong), a bowed drone, a breathy flute and a frame drum, with a light
    /// echo for space. Notes that ring past the end of the loop wrap round to its start, so the loops have no seam.
    /// Each track has a fixed seed: re-running gives the same file.
    /// </summary>
    public static class MusicSynth
    {
        const int Rate = SoundSynth.SampleRate;
        const float Tau = Mathf.PI * 2f;

        // Scales as semitone offsets from the root.
        static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        static readonly int[] Aeolian = { 0, 2, 3, 5, 7, 8, 10 };

        // ---------- Tracks ----------

        /// <summary>Title screen: slow D Dorian, a bowed drone, harp arpeggios, a flute line in the second half.</summary>
        public static float[] Menu()
        {
            var song = new Song(bpm: 64f, bars: 16, seed: 3301);
            const int root = 50; // D3
            int[][] chords = { new[] { 0, 3, 7 }, new[] { -2, 2, 5 }, new[] { 5, 9, 12 }, new[] { 0, 3, 7 } }; // Dm C G Dm
            for (int bar = 0; bar < 16; bar++)
            {
                var chord = chords[bar / 2 % 4];
                if (bar % 4 == 0) song.Add(bar * 4f, Bowed(Midi(root - 12), song.Beat * 16.5f, 0.6f), 0.22f);
                if (bar % 4 == 0) song.Add(bar * 4f, Bowed(Midi(root - 5), song.Beat * 16.5f, 0.6f), 0.12f);
                for (int i = 0; i < 6; i++)
                {
                    int note = root + 12 + chord[i % 3] + (i >= 3 ? 12 : 0);
                    song.Add(bar * 4f + i * 0.66f, Pluck(Midi(note), 2.6f, 0.55f, song.Next()), 0.16f);
                }
            }
            song.Melody(start: 32f, beats: 32f, root: root + 12, Dorian, flute: true, gain: 0.2f, rests: 0.25f, durations: new[] { 2f, 1f, 1f, 3f });
            return song.Finish(echo: 0.3f);
        }

        /// <summary>Daytime at the homestead: G major, a lute playing bass and chords, a flute tune over it.</summary>
        public static float[] Day()
        {
            var song = new Song(bpm: 96f, bars: 24, seed: 3302);
            const int root = 55; // G3
            int[][] chords =
            {
                new[] { 0, 4, 7 }, new[] { 5, 9, 12 }, new[] { 0, 4, 7 }, new[] { 7, 11, 14 },   // G C G D
                new[] { 9, 12, 16 }, new[] { 5, 9, 12 }, new[] { 7, 11, 14 }, new[] { 0, 4, 7 }, // Em C D G
            };
            for (int bar = 0; bar < 24; bar++)
            {
                var chord = chords[bar % 8];
                float b = bar * 4f;
                // Bass on 1 and 3 (root, then fifth), chord strums on 2 and 4.
                song.Add(b, Pluck(Midi(root - 12 + chord[0]), 1.6f, 0.4f, song.Next()), 0.3f);
                song.Add(b + 2f, Pluck(Midi(root - 12 + chord[0] + 7), 1.4f, 0.4f, song.Next()), 0.24f);
                foreach (float beat in new[] { 1f, 3f })
                    for (int n = 0; n < 3; n++)
                        song.Add(b + beat + n * 0.03f, Pluck(Midi(root + chord[n]), 1.1f, 0.65f, song.Next()), 0.12f);
            }
            song.Melody(start: 8f * 4f, beats: 32f, root: root + 12, Major, flute: true, gain: 0.22f, rests: 0.15f, durations: new[] { 1f, 1f, 0.5f, 0.5f, 2f });
            song.Melody(start: 16f * 4f, beats: 32f, root: root + 12, Major, flute: false, gain: 0.2f, rests: 0.2f, durations: new[] { 0.5f, 0.5f, 1f, 1f });
            return song.Finish(echo: 0.2f);
        }

        /// <summary>Night: A minor, very sparse — a low pad and a few high harp notes.</summary>
        public static float[] Night()
        {
            var song = new Song(bpm: 56f, bars: 12, seed: 3303);
            const int root = 57; // A3
            int[][] chords = { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { 3, 7, 10 }, new[] { -2, 2, 5 } }; // Am F C G
            for (int bar = 0; bar < 12; bar++)
            {
                var chord = chords[bar % 4];
                foreach (int n in chord) song.Add(bar * 4f, Bowed(Midi(root - 12 + n), song.Beat * 4.6f, 0.9f), 0.07f);
            }
            song.Melody(start: 0f, beats: 48f, root: root + 12, Aeolian, flute: false, gain: 0.17f, rests: 0.55f, durations: new[] { 1.5f, 1f, 2f, 0.5f });
            return song.Finish(echo: 0.4f);
        }

        /// <summary>Combat: D minor at a gallop — frame drum, a bowed ostinato and plucked stabs.</summary>
        public static float[] Combat()
        {
            var song = new Song(bpm: 132f, bars: 16, seed: 3304);
            const int root = 50; // D3
            int[] roots = { 0, -4, -2, 0 };           // Dm Bb C Dm
            int[] ostinato = { 0, 0, 3, 0, 5, 0, 3, -2 }; // eighth notes over each root
            for (int bar = 0; bar < 16; bar++)
            {
                float b = bar * 4f;
                int r = roots[bar / 2 % 4];
                foreach (float beat in new[] { 0f, 1.5f, 2f }) song.Add(b + beat, Drum(low: true, song.Next()), 0.5f);
                foreach (float beat in new[] { 1f, 3f }) song.Add(b + beat, Drum(low: false, song.Next()), 0.3f);
                if (bar % 2 == 1) song.Add(b + 3.5f, Drum(low: false, song.Next()), 0.2f);
                for (int i = 0; i < 8; i++)
                    song.Add(b + i * 0.5f, Bowed(Midi(root - 12 + r + ostinato[i]), song.Beat * 0.48f, 0.15f), 0.16f);
                if (bar % 2 == 0)
                    foreach (int n in new[] { 0, 3, 7 })
                        song.Add(b, Pluck(Midi(root + r + n), 0.9f, 0.8f, song.Next()), 0.12f);
            }
            song.Melody(start: 32f, beats: 32f, root: root + 12, Aeolian, flute: false, gain: 0.14f, rests: 0.1f, durations: new[] { 0.5f, 0.5f, 1f });
            return song.Finish(echo: 0.12f);
        }

        // ---------- Arrangement ----------

        sealed class Song
        {
            readonly float[] buffer;
            readonly System.Random rng;
            public readonly float Beat;

            public Song(float bpm, int bars, int seed)
            {
                Beat = 60f / bpm;
                buffer = new float[Mathf.RoundToInt(bars * 4 * Beat * Rate)];
                rng = new System.Random(seed);
            }

            public int Next() => rng.Next();

            /// <summary>Mixes a note in at a beat; anything past the end wraps round to the start (seamless loop).</summary>
            public void Add(float beat, float[] note, float gain)
            {
                int start = Mathf.RoundToInt(beat * Beat * Rate);
                for (int i = 0; i < note.Length; i++) buffer[(start + i) % buffer.Length] += note[i] * gain;
            }

            /// <summary>A wandering tune on the scale: mostly steps, sometimes a leap, rests, ending on the root.</summary>
            public void Melody(float start, float beats, int root, int[] scale, bool flute, float gain, float rests, float[] durations)
            {
                int degree = 7; // the root, an octave up from the scale's bottom
                float beat = start;
                while (beat < start + beats - 0.01f)
                {
                    float d = durations[rng.Next(durations.Length)];
                    d = Mathf.Min(d, start + beats - beat);
                    bool last = beat + d >= start + beats - 0.01f;
                    if (last) degree = 7;
                    if (last || rng.NextDouble() > rests)
                    {
                        int note = root + Degree(scale, degree - 7);
                        float seconds = d * Beat;
                        Add(beat, flute ? Flute(Midi(note), seconds * 0.95f, Next()) : Pluck(Midi(note), Mathf.Max(1.2f, seconds * 1.5f), 0.6f, Next()), gain);
                    }
                    beat += d;
                    int step = rng.Next(10) switch { 0 => -3, 1 or 2 => -2, 3 or 4 or 5 => -1, 6 or 7 => 1, 8 => 2, _ => 3 };
                    degree = Mathf.Clamp(degree + step, 3, 13);
                }
            }

            public float[] Finish(float echo)
            {
                if (echo > 0f)
                {
                    // A circular feedback delay, run twice so the start already carries the end's echoes.
                    int delay = Mathf.RoundToInt(Beat * 0.75f * Rate);
                    var wet = new float[buffer.Length];
                    for (int pass = 0; pass < 2; pass++)
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            int from = (i - delay + buffer.Length) % buffer.Length;
                            wet[i] = (buffer[from] + wet[from]) * 0.45f;
                        }
                    for (int i = 0; i < buffer.Length; i++) buffer[i] += wet[i] * echo * 2f;
                }
                return buffer;
            }
        }

        static int Degree(int[] scale, int degree)
        {
            int octave = Mathf.FloorToInt(degree / 7f);
            int index = degree - octave * 7;
            return scale[index] + octave * 12;
        }

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        // ---------- Instruments ----------

        /// <summary>A plucked string (Karplus-Strong): a noise burst ringing round a tuned delay line.</summary>
        static float[] Pluck(float freq, float seconds, float brightness, int seed)
        {
            var rng = new System.Random(seed);
            int period = Mathf.Max(2, Mathf.RoundToInt(Rate / freq));
            var line = new float[period];
            for (int i = 0; i < period; i++) line[i] = (float)rng.NextDouble() * 2f - 1f;
            var s = new float[Mathf.RoundToInt(seconds * Rate)];
            float decay = Mathf.Lerp(0.990f, 0.998f, brightness);
            float previous = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                int k = i % period;
                float v = line[k];
                s[i] = v;
                float averaged = 0.5f * (v + previous);
                previous = v;
                line[k] = Mathf.Lerp(averaged, v, brightness * 0.3f) * decay;
            }
            Fade(s, 0.03f);
            return s;
        }

        /// <summary>A bowed string or drone: a soft sawtooth through a low-pass, with vibrato and slow swells.</summary>
        static float[] Bowed(float freq, float seconds, float attack)
        {
            var s = new float[Mathf.RoundToInt(seconds * Rate)];
            float phase = 0f, low = 0f, a = 1f - Mathf.Exp(-Tau * Mathf.Min(1400f, freq * 5f) / Rate);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float vibrato = 1f + 0.004f * Mathf.Sin(t * Tau * 5f) * Mathf.Clamp01(t / 0.6f);
                phase = (phase + freq * vibrato / Rate) % 1f;
                float saw = phase * 2f - 1f;
                low += a * (saw - low);
                float env = Mathf.Clamp01(t / Mathf.Max(0.02f, attack)) * Mathf.Clamp01((seconds - t) / Mathf.Min(0.5f, seconds * 0.4f));
                s[i] = low * env;
            }
            return s;
        }

        /// <summary>A breathy flute: a sine with a touch of octave, vibrato and filtered breath noise.</summary>
        static float[] Flute(float freq, float seconds, int seed)
        {
            var rng = new System.Random(seed);
            var s = new float[Mathf.RoundToInt(seconds * Rate)];
            float phase = 0f, breath = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float vibrato = 1f + 0.006f * Mathf.Sin(t * Tau * 5.2f) * Mathf.Clamp01((t - 0.15f) / 0.3f);
                phase = (phase + freq * vibrato / Rate) % 1f;
                breath += 0.08f * (((float)rng.NextDouble() * 2f - 1f) - breath);
                float env = Mathf.Clamp01(t / 0.07f) * Mathf.Clamp01((seconds - t) / Mathf.Min(0.15f, seconds * 0.3f));
                s[i] = (Mathf.Sin(phase * Tau) + 0.18f * Mathf.Sin(phase * 2f * Tau) + breath * 0.35f) * env;
            }
            return s;
        }

        /// <summary>A frame drum: a low, falling thump (or a slap) with a touch of skin noise.</summary>
        static float[] Drum(bool low, int seed)
        {
            var rng = new System.Random(seed);
            float seconds = low ? 0.45f : 0.2f;
            var s = new float[Mathf.RoundToInt(seconds * Rate)];
            float phase = 0f, noise = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float freq = low ? Mathf.Lerp(105f, 52f, Mathf.Clamp01(t / 0.12f)) : Mathf.Lerp(240f, 170f, Mathf.Clamp01(t / 0.05f));
                phase = (phase + freq / Rate) % 1f;
                noise += (low ? 0.15f : 0.5f) * (((float)rng.NextDouble() * 2f - 1f) - noise);
                float body = Mathf.Sin(phase * Tau) * Mathf.Exp(-t * (low ? 9f : 22f));
                float skin = noise * Mathf.Exp(-t * (low ? 40f : 30f)) * (low ? 0.4f : 0.9f);
                s[i] = body + skin;
            }
            return s;
        }

        /// <summary>Fades the last part of a note out (no clicks when it's cut).</summary>
        static void Fade(float[] s, float seconds)
        {
            int n = Mathf.Min(s.Length, Mathf.RoundToInt(seconds * Rate));
            for (int i = 0; i < n; i++) s[s.Length - 1 - i] *= i / (float)n;
        }
    }
}
