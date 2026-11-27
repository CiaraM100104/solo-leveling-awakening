using UnityEngine;

namespace SLA.Combat
{
    /// <summary>
    /// Generated placeholder sounds, so the combat loop has audio cues before the audio pass
    /// (docs/ART_PIPELINE.md) replaces them with real clips. Each clip is built once and shared.
    /// </summary>
    public static class PlaceholderSounds
    {
        private const int SampleRate = 22050;

        private static AudioClip _tell;
        private static AudioClip _impact;
        private static AudioClip _block;

        /// <summary>Rising buzz for an enemy windup (CMB-17).</summary>
        public static AudioClip Tell => _tell != null ? _tell : (_tell = Build("sfx_tell_placeholder", 0.6f, TellWave));

        /// <summary>Short thud for a weapon hit (CMB-20).</summary>
        public static AudioClip Impact => _impact != null ? _impact : (_impact = Build("sfx_impact_placeholder", 0.12f, ImpactWave));

        /// <summary>Bright clang for a blocked enemy strike.</summary>
        public static AudioClip Block => _block != null ? _block : (_block = Build("sfx_block_placeholder", 0.2f, ClangWave));

        private delegate float Wave(float t, float length, ref uint seed);

        private static AudioClip Build(string name, float seconds, Wave wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            uint seed = 2463534242u;
            for (int i = 0; i < count; i++)
            {
                data[i] = wave((float)i / SampleRate, seconds, ref seed);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float TellWave(float t, float length, ref uint seed)
        {
            float u = t / length;
            float frequency = Mathf.Lerp(180f, 520f, u);
            float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t));
            float envelope = Mathf.Min(1f, u * 8f) * (1f - u * u);
            return square * envelope * 0.25f;
        }

        private static float ImpactWave(float t, float length, ref uint seed)
        {
            float u = t / length;
            float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 60f, u) * t);
            float noise = Noise(ref seed);
            float envelope = (1f - u) * (1f - u);
            return (body * 0.7f + noise * 0.3f) * envelope * 0.8f;
        }

        private static float ClangWave(float t, float length, ref uint seed)
        {
            float u = t / length;
            float ring = Mathf.Sin(2f * Mathf.PI * 1320f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 1870f * t);
            float envelope = Mathf.Exp(-u * 6f);
            return (ring * 0.5f + Noise(ref seed) * 0.15f * (1f - u)) * envelope * 0.5f;
        }

        // xorshift: no System.Random allocation, same sound every run.
        private static float Noise(ref uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return (seed / (float)uint.MaxValue) * 2f - 1f;
        }
    }
}
