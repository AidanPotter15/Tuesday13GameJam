using UnityEngine;

namespace XIII
{
    // Every sound in the game is synthesised here at startup. No audio files.
    public static class Sfx
    {
        const int SR = 22050;
        const float TAU = Mathf.PI * 2f;

        public static AudioClip Hum, Step, Heel, Clink, Door, Stinger, Chime, Whisper, Tick, Thud;

        static System.Random rng = new System.Random(13);
        static float Noise() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

        public static void Init()
        {
            if (Hum != null) return;
            Hum = MakeHum();
            Step = MakeStep();
            Heel = MakeHeel();
            Clink = MakeClink();
            Door = MakeDoor();
            Stinger = MakeStinger();
            Chime = MakeChime();
            Whisper = MakeWhisper();
            Tick = MakeTick();
            Thud = MakeThud();
        }

        static AudioClip Clip(string name, float[] d)
        {
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
            var c = AudioClip.Create(name, d.Length, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        // Fluorescent-light mains hum. 4 s, every partial has a whole number of cycles so it loops cleanly.
        static AudioClip MakeHum()
        {
            int n = SR * 4;
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float wob = 0.85f + 0.15f * Mathf.Sin(TAU * 0.25f * t);
                lp += (Noise() - lp) * 0.05f;
                d[i] = wob * (0.22f * Mathf.Sin(TAU * 50f * t) + 0.12f * Mathf.Sin(TAU * 100f * t)
                             + 0.05f * Mathf.Sin(TAU * 150f * t) + 0.025f * Mathf.Sin(TAU * 120.25f * t)) + lp * 0.15f;
            }
            return Clip("hum", d);
        }

        // Your own steps: soft, muffled, on carpet.
        static AudioClip MakeStep()
        {
            int n = (int)(SR * 0.18f);
            var d = new float[n];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                lp += (Noise() - lp) * 0.08f;
                lp2 += (lp - lp2) * 0.08f;
                float env = Mathf.Min(1f, t * 150f) * Mathf.Exp(-t * 28f);
                d[i] = (lp2 * 3f + Mathf.Sin(TAU * 70f * t) * 0.25f) * env;
            }
            return Clip("step", d);
        }

        // The follower: hard heels, which is wrong on a carpeted floor.
        static AudioClip MakeHeel()
        {
            int n = (int)(SR * 0.25f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float x = Noise();
                lp += (x - lp) * 0.3f;
                d[i] = (x - lp) * Mathf.Exp(-t * 110f) * 0.55f
                     + Mathf.Sin(TAU * 140f * t) * Mathf.Exp(-t * 25f) * 0.45f
                     + Mathf.Sin(TAU * 430f * t) * Mathf.Exp(-t * 60f) * 0.25f;
            }
            return Clip("heel", d);
        }

        // Metal hitting the floor and bouncing twice.
        static AudioClip MakeClink()
        {
            int n = (int)(SR * 1.4f);
            var d = new float[n];
            float[] hits = { 0f, 0.21f, 0.34f };
            float[] amps = { 1f, 0.45f, 0.2f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float s = 0f;
                for (int h = 0; h < hits.Length; h++)
                {
                    float tt = t - hits[h];
                    if (tt < 0f) continue;
                    float ring = Mathf.Sin(TAU * 1650f * tt) * 0.5f + Mathf.Sin(TAU * 2490f * tt) * 0.3f + Mathf.Sin(TAU * 4130f * tt) * 0.2f;
                    s += amps[h] * (ring * Mathf.Exp(-tt * (h == 0 ? 6f : 12f)) + Noise() * Mathf.Exp(-tt * 200f) * 0.6f);
                }
                d[i] = s * 0.6f;
            }
            return Clip("clink", d);
        }

        static AudioClip MakeDoor()
        {
            int n = (int)(SR * 1.0f);
            var d = new float[n];
            float ph = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                // creak: buzzy, wavering pitch
                float f = Mathf.Lerp(320f, 170f, t / 0.55f) * (1f + 0.04f * Mathf.Sin(TAU * 11f * t));
                ph += f / SR;
                float saw = (ph % 1f) * 2f - 1f;
                float creakEnv = t < 0.55f ? Mathf.Sin(Mathf.PI * t / 0.55f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(TAU * 23f * t))) : 0f;
                // latch thump
                float tt = t - 0.6f;
                lp += (Noise() - lp) * 0.2f;
                float thump = tt > 0f ? (Mathf.Sin(TAU * 60f * tt) * 0.8f + lp * 0.5f) * Mathf.Exp(-tt * 18f) : 0f;
                d[i] = saw * creakEnv * 0.18f + thump;
            }
            return Clip("door", d);
        }

        static AudioClip MakeStinger()
        {
            int n = (int)(SR * 2.5f);
            var d = new float[n];
            float[] freqs = { 110f, 116.5f, 155.6f, 233.1f, 246.9f, 311.1f };
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float bend = 1f - 0.08f * t;
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++) s += Mathf.Sin(TAU * freqs[k] * bend * t + k);
                lp += (Noise() - lp) * 0.3f;
                float env = Mathf.Min(1f, t * 80f) * Mathf.Exp(-t * 1.4f);
                d[i] = (s / freqs.Length * 0.9f + lp * 0.6f * Mathf.Exp(-t * 4f)) * env;
            }
            return Clip("stinger", d);
        }

        // Low, slightly-off hotel bell.
        static AudioClip MakeChime()
        {
            int n = (int)(SR * 2f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float b = 392f;
                d[i] = (Mathf.Sin(TAU * b * t) * 0.5f + Mathf.Sin(TAU * b * 2.76f * t) * 0.2f * Mathf.Exp(-t * 3f)
                        + Mathf.Sin(TAU * b * 5.4f * t) * 0.08f * Mathf.Exp(-t * 6f)) * Mathf.Exp(-t * 2.2f) * Mathf.Min(1f, t * 200f) * 0.6f;
            }
            return Clip("chime", d);
        }

        // Breathy band-passed noise with a slow, uneven pulse. 3 s loop.
        static AudioClip MakeWhisper()
        {
            int n = SR * 3;
            var d = new float[n];
            float a = 0f, b = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float x = Noise();
                a += (x - a) * 0.35f;
                b += (x - b) * 0.04f;
                float band = a - b;
                float env = Mathf.Max(0f, Mathf.Sin(TAU * t * 2f / 3f) * 0.6f + Mathf.Sin(TAU * t * 7f / 3f) * 0.5f + Mathf.Sin(TAU * t * 5f / 3f) * 0.3f);
                d[i] = band * env * 0.9f;
            }
            return Clip("whisper", d);
        }

        static AudioClip MakeTick()
        {
            int n = (int)(SR * 0.06f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                d[i] = (Noise() * 0.5f + Mathf.Sin(TAU * 2400f * t) * 0.5f) * Mathf.Exp(-t * 220f);
            }
            return Clip("tick", d);
        }

        static AudioClip MakeThud()
        {
            int n = (int)(SR * 0.6f);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                lp += (Noise() - lp) * 0.15f;
                d[i] = (Mathf.Sin(TAU * 55f * t) + lp * 0.7f) * Mathf.Exp(-t * 9f);
            }
            return Clip("thud", d);
        }
    }
}
