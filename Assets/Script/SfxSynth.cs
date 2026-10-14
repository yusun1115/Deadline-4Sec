using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Synthesized placeholder sounds for the power-up presentation. Each one is
    // built once per session from a small recipe (inharmonic partials for metal
    // and ice, pitch-dropping sines for thumps, filtered noise for cracks and
    // whooshes). Authored clips in SoundLibrary always win over these.
    public static class SfxSynth
    {
        private const int Rate = 32000;
        private static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => Cache.Clear();

        // Every recipe, so the loading screen can build them before the first run.
        public static readonly string[] All =
        {
            "FreezeStart", "FreezeShatter", "AmplifierStart", "RushStart", "RushDrone", "RushSlash",
            "MagnetHum", "MagnetZip", "ChainLock", "ChainBreak", "Heartbeat", "HeartSurge",
            "NearChime", "Tick", "DeathImpact",
        };

        public static AudioClip Get(string name)
        {
            if (Cache.TryGetValue(name, out AudioClip clip) && clip != null)
                return clip;
            float[] samples;
            switch (name)
            {
                case "FreezeStart": samples = FreezeStart(); break;
                case "FreezeShatter": samples = Shatter(); break;
                case "AmplifierStart": samples = Arpeggio(); break;
                case "RushStart": samples = RushStart(); break;
                case "RushDrone": samples = Drone(); break;
                case "RushSlash": samples = Slash(); break;
                case "MagnetHum": samples = Hum(); break;
                case "MagnetZip": samples = Zip(); break;
                case "ChainLock": samples = ChainLock(); break;
                case "ChainBreak": samples = ChainBreak(); break;
                case "Heartbeat": samples = Heartbeat(); break;
                case "HeartSurge": samples = Surge(); break;
                case "NearChime": samples = NearChime(); break;
                case "Tick": samples = Tick(); break;
                case "DeathImpact": samples = DeathImpact(); break;
                default: throw new ArgumentException("Unknown synth sound " + name);
            }
            clip = AudioClip.Create("Synth " + name, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            Cache[name] = clip;
            return clip;
        }

        // ---------------- recipes ----------------

        // "쩡!" — a struck ice crystal: bright inharmonic partials over a click.
        private static float[] FreezeStart()
        {
            float[] s = Buffer(0.9f);
            Partials(s, 0f, new[] { 2093f, 3301f, 4699f, 6271f, 7902f }, new[] { 0.5f, 0.35f, 0.3f, 0.2f, 0.12f }, 0.45f);
            Partials(s, 0f, new[] { 1046f, 1568f }, new[] { 0.25f, 0.15f }, 0.25f);
            NoiseBurst(s, 0f, 0.035f, 0.5f, 0.85f, 1);
            return Finish(s, 0.9f);
        }

        // "파삭!" — ice shattering: a crack, then a shower of tiny glass tinkles.
        private static float[] Shatter()
        {
            float[] s = Buffer(0.7f);
            var random = new System.Random(11);
            NoiseBurst(s, 0f, 0.06f, 0.9f, 0.7f, 2);
            for (int i = 0; i < 26; i++)
            {
                float t = (float)Math.Pow(random.NextDouble(), 1.6) * 0.5f;
                float f = 2800f + (float)random.NextDouble() * 4600f;
                Partials(s, t, new[] { f, f * 1.47f }, new[] { 0.16f * (1f - t), 0.08f * (1f - t) }, 0.04f);
                NoiseBurst(s, t, 0.008f, 0.25f * (1f - t), 0.9f, i + 3);
            }
            return Finish(s, 0.9f);
        }

        // Clear power-up: C6-E6-G6-C7 arpeggio with an airy tail.
        private static float[] Arpeggio()
        {
            float[] s = Buffer(0.85f);
            float[] notes = { 1046.5f, 1318.5f, 1568f, 2093f };
            for (int i = 0; i < notes.Length; i++)
                Partials(s, i * 0.07f, new[] { notes[i], notes[i] * 2f, notes[i] * 3f },
                    new[] { 0.35f, 0.12f, 0.05f }, i == notes.Length - 1 ? 0.45f : 0.16f);
            NoiseSweep(s, 0f, 0.35f, 0.08f, 0.15f, 0.9f, 5);
            return Finish(s, 0.85f);
        }

        // Mechanical acceleration: a gritty saw revving up, over a thump.
        private static float[] RushStart()
        {
            float[] s = Buffer(0.9f);
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float k = Mathf.Clamp01(t / 0.7f);
                float f = Mathf.Lerp(70f, 430f, k * k);
                phase += f / Rate;
                float saw = (float)(phase - Math.Floor(phase)) * 2f - 1f;
                float env = Mathf.Min(1f, t * 20f) * Mathf.Clamp01((0.9f - t) * 6f);
                s[i] += (float)Math.Tanh(saw * 2.5f) * 0.32f * env;
            }
            Thump(s, 0f, 110f, 40f, 0.18f, 0.7f);
            NoiseSweep(s, 0.05f, 0.8f, 0.05f, 0.25f, 0.6f, 7);
            return Finish(s, 0.85f);
        }

        // Low loop under Reaper Rush (2 s, every component fits whole cycles).
        private static float[] Drone()
        {
            float[] s = Buffer(2f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float lfo = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                float v = Saw(55f, t) * 0.5f + Saw(55.5f, t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.4f * lfo;
                s[i] = (float)Math.Tanh(v * 1.4f) * 0.35f;
            }
            return s;
        }

        // Rush kill: a fast slicing whoosh with a metallic glint.
        private static float[] Slash()
        {
            float[] s = Buffer(0.32f);
            NoiseSweep(s, 0f, 0.22f, 0.7f, 0.05f, 1f, 9);
            Partials(s, 0.02f, new[] { 1760f, 2637f, 3951f }, new[] { 0.25f, 0.15f, 0.08f }, 0.12f);
            return Finish(s, 0.9f);
        }

        // "웅—" magnetic field: low chord with a fast tremolo, swelling in and out.
        private static float[] Hum()
        {
            float[] s = Buffer(1f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.SmoothStep(0f, 1f, t / 0.18f) * Mathf.SmoothStep(0f, 1f, (1f - t) / 0.45f);
                float bend = 1f - 0.06f * Mathf.Clamp01((t - 0.5f) * 2f);
                float trem = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                float v = Mathf.Sin(2f * Mathf.PI * 98f * bend * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 147f * bend * t) +
                          0.35f * Saw(196f * bend, t);
                s[i] = v * env * trem * 0.3f;
            }
            return Finish(s, 0.8f);
        }

        // Coin flying in: a tiny rising zip.
        private static float[] Zip()
        {
            float[] s = Buffer(0.1f);
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                phase += Mathf.Lerp(900f, 2800f, t / 0.1f) / Rate;
                s[i] = Mathf.Sin((float)(phase * 2.0 * Math.PI)) * Mathf.Min(1f, t * 300f) * (1f - t / 0.1f) * 0.4f;
            }
            return s;
        }

        // Chains wrapping on: three rising clinks and a heavy lock clunk.
        private static float[] ChainLock()
        {
            float[] s = Buffer(0.75f);
            for (int i = 0; i < 3; i++)
                Clink(s, i * 0.085f, 1.0f + i * 0.12f, 0.45f);
            Thump(s, 0.3f, 180f, 90f, 0.09f, 0.5f);
            Partials(s, 0.3f, new[] { 520f, 1240f, 1980f }, new[] { 0.3f, 0.2f, 0.12f }, 0.2f);
            NoiseBurst(s, 0.3f, 0.02f, 0.4f, 0.6f, 13);
            return Finish(s, 0.9f);
        }

        // Chains snapping: a sharp crack, then links clattering away.
        private static float[] ChainBreak()
        {
            float[] s = Buffer(0.8f);
            NoiseBurst(s, 0f, 0.03f, 1f, 0.8f, 17);
            Clink(s, 0f, 1.35f, 0.6f);
            var random = new System.Random(19);
            for (int i = 0; i < 6; i++)
            {
                float t = 0.08f + i * 0.08f + (float)random.NextDouble() * 0.04f;
                Clink(s, t, 0.8f + (float)random.NextDouble() * 0.6f, 0.35f * (1f - i / 7f));
            }
            return Finish(s, 0.9f);
        }

        // Strong "lub-dub".
        private static float[] Heartbeat()
        {
            float[] s = Buffer(0.65f);
            Thump(s, 0f, 95f, 42f, 0.16f, 1f);
            Thump(s, 0.22f, 85f, 40f, 0.14f, 0.75f);
            return Finish(s, 0.95f);
        }

        // Time energy rising: a swelling sweep with shimmer.
        private static float[] Surge()
        {
            float[] s = Buffer(0.8f);
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float k = t / 0.8f;
                phase += Mathf.Lerp(220f, 880f, k * k) / Rate;
                float env = Mathf.Min(1f, t * 8f) * Mathf.Clamp01((0.8f - t) * 5f);
                float v = Mathf.Sin((float)(phase * 2.0 * Math.PI)) + 0.3f * Mathf.Sin((float)(phase * 4.0 * Math.PI));
                s[i] += v * env * 0.28f * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 14f * t));
            }
            NoiseSweep(s, 0f, 0.75f, 0.05f, 0.4f, 0.4f, 23);
            return Finish(s, 0.85f);
        }

        // Near miss success: two quick bright notes (E6 → B6).
        private static float[] NearChime()
        {
            float[] s = Buffer(0.32f);
            Partials(s, 0f, new[] { 1318.5f, 2637f }, new[] { 0.35f, 0.1f }, 0.05f);
            Partials(s, 0.055f, new[] { 1975.5f, 3951f }, new[] { 0.4f, 0.12f }, 0.11f);
            return Finish(s, 0.8f);
        }

        // Clock tick: a short woody click with a high ping.
        private static float[] Tick()
        {
            float[] s = Buffer(0.08f);
            NoiseBurst(s, 0f, 0.012f, 0.7f, 0.9f, 29);
            Partials(s, 0f, new[] { 2200f, 3300f }, new[] { 0.35f, 0.15f }, 0.012f);
            Thump(s, 0f, 900f, 500f, 0.01f, 0.3f);
            return Finish(s, 0.8f);
        }

        // Collision death: a hard crack over a deep, saturated low impact.
        private static float[] DeathImpact()
        {
            float[] s = Buffer(1.1f);
            NoiseBurst(s, 0f, 0.07f, 1f, 0.75f, 31);
            Thump(s, 0f, 140f, 34f, 0.32f, 1.4f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                s[i] += Mathf.Sin(2f * Mathf.PI * 41f * t) * Mathf.Exp(-t / 0.35f) * Mathf.Min(1f, t * 80f) * 0.35f;
                s[i] = (float)Math.Tanh(s[i] * 1.6f);
            }
            NoiseSweep(s, 0f, 0.6f, 0.2f, 0.02f, 0.5f, 37);
            return Finish(s, 0.95f);
        }

        // ---------------- building blocks ----------------

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        private static float Saw(float frequency, float t)
        {
            float p = frequency * t;
            return (p - Mathf.Floor(p)) * 2f - 1f;
        }

        // Exponentially decaying sines starting at `start` seconds.
        private static void Partials(float[] s, float start, float[] frequencies, float[] gains, float decay)
        {
            int from = Mathf.FloorToInt(start * Rate);
            int length = Mathf.Min(s.Length - from, Mathf.CeilToInt(decay * 7f * Rate));
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t * 2000f) * Mathf.Exp(-t / decay);
                float v = 0f;
                for (int k = 0; k < frequencies.Length; k++)
                    v += Mathf.Sin(2f * Mathf.PI * frequencies[k] * t) * gains[k] * Mathf.Exp(-t * k * 3f);
                s[from + i] += v * env;
            }
        }

        // Metal link: inharmonic bar partials, pitch-scaled.
        private static void Clink(float[] s, float start, float pitch, float gain)
        {
            Partials(s, start, new[] { 1180f * pitch, 2740f * pitch, 4130f * pitch, 5620f * pitch },
                new[] { gain, gain * 0.6f, gain * 0.4f, gain * 0.25f }, 0.06f);
        }

        // Kick-like thump: sine with a falling pitch.
        private static void Thump(float[] s, float start, float fromHz, float toHz, float decay, float gain)
        {
            int from = Mathf.FloorToInt(start * Rate);
            int length = Mathf.Min(s.Length - from, Mathf.CeilToInt(decay * 6f * Rate));
            double phase = 0;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)Rate;
                phase += Mathf.Lerp(toHz, fromHz, Mathf.Exp(-t / (decay * 0.4f))) / Rate;
                float env = Mathf.Min(1f, t * 600f) * Mathf.Exp(-t / decay);
                s[from + i] += (float)Math.Tanh(Mathf.Sin((float)(phase * 2.0 * Math.PI)) * 1.8f) * env * gain * 0.6f;
            }
        }

        // High-passed noise click/crack. `brightness` 0..1 sets the high-pass amount.
        private static void NoiseBurst(float[] s, float start, float length, float gain, float brightness, int seed)
        {
            var random = new System.Random(seed);
            int from = Mathf.FloorToInt(start * Rate);
            int count = Mathf.Min(s.Length - from, Mathf.CeilToInt(length * Rate));
            float previousIn = 0f, previousOut = 0f;
            for (int i = 0; i < count; i++)
            {
                float x = (float)random.NextDouble() * 2f - 1f;
                float y = brightness * (previousOut + x - previousIn);
                previousIn = x;
                previousOut = y;
                s[from + i] += y * gain * Mathf.Exp(-i / (count * 0.3f));
            }
        }

        // Noise through a low-pass whose cutoff glides; swelling or fading by gain.
        private static void NoiseSweep(float[] s, float start, float length, float fromCut, float toCut, float gain, int seed)
        {
            var random = new System.Random(seed);
            int from = Mathf.FloorToInt(start * Rate);
            int count = Mathf.Min(s.Length - from, Mathf.CeilToInt(length * Rate));
            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                float k = i / (float)count;
                float x = (float)random.NextDouble() * 2f - 1f;
                y += Mathf.Lerp(fromCut, toCut, k) * (x - y);
                s[from + i] += y * gain * Mathf.Sin(k * Mathf.PI) * 0.6f;
            }
        }

        // Normalizes to `peak` with a short fade-out so nothing clicks.
        private static float[] Finish(float[] s, float peak)
        {
            float max = 0.0001f;
            foreach (float v in s)
                max = Mathf.Max(max, Mathf.Abs(v));
            float scale = peak / max;
            int fade = Mathf.Min(s.Length, Rate / 100);
            for (int i = 0; i < s.Length; i++)
            {
                s[i] *= scale;
                int fromEnd = s.Length - 1 - i;
                if (fromEnd < fade)
                    s[i] *= fromEnd / (float)fade;
            }
            return s;
        }
    }
}
