using UnityEngine;

namespace Fighter403.Audio
{
    internal sealed class SfxEnvelope : MonoBehaviour
    {
        private sealed class Playback
        {
            internal readonly int AttackSamples;
            internal readonly double DuckStep;
            internal readonly double RecoverStep;
            internal volatile int ReleaseSamples;
            internal volatile float TargetGain = 1f;
            internal volatile bool Released;
            internal int AttackPosition;
            internal int ReleasePosition;
            internal double Gain = 1d;

            internal Playback(int sampleRate, float attackSeconds)
            {
                AttackSamples = (int)(sampleRate * attackSeconds);
                DuckStep = 1d / (sampleRate * 0.018d);
                RecoverStep = 1d / (sampleRate * 0.115d);
            }
        }

        private volatile Playback playback;
        private int sampleRate;

        internal bool Released => playback?.Released == true;

        internal void Begin(float attackSeconds)
        {
            sampleRate = AudioSettings.outputSampleRate;
            playback = new Playback(sampleRate, attackSeconds);
        }

        internal void SetGain(float gain)
        {
            var state = playback;
            if (state != null) state.TargetGain = gain;
        }

        internal void Release(float seconds)
        {
            var state = playback;
            if (state != null && state.ReleaseSamples == 0)
                state.ReleaseSamples = Mathf.Max(1, (int)(sampleRate * seconds));
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            // Only the audio thread writes playback cursors. The main thread sends commands through the volatile fields.
            var state = playback;
            if (state == null) return;

            int releaseLength = state.ReleaseSamples;
            float targetGain = state.TargetGain;
            if (state.AttackPosition >= state.AttackSamples && releaseLength == 0 && state.Gain == targetGain &&
                targetGain == 1f) return;

            for (int i = 0; i < data.Length; i += channels)
            {
                double fade = 1d;
                if (state.AttackPosition < state.AttackSamples)
                {
                    fade = Smooth((double)state.AttackPosition / state.AttackSamples);
                    // Releasing during the attack leaves the attack level at its current value.
                    if (releaseLength == 0) state.AttackPosition++;
                }

                if (releaseLength > 0)
                {
                    fade *= 1d - Smooth((double)state.ReleasePosition / releaseLength);
                    if (state.ReleasePosition < releaseLength) state.ReleasePosition++;
                }

                if (state.Gain > targetGain) state.Gain = System.Math.Max(targetGain, state.Gain - state.DuckStep);
                else if (state.Gain < targetGain) state.Gain = System.Math.Min(targetGain, state.Gain + state.RecoverStep);
                float level = (float)(fade * state.Gain);
                for (int channel = 0; channel < channels; channel++) data[i + channel] *= level;
            }

            if (releaseLength > 0 && state.ReleasePosition >= releaseLength) state.Released = true;
        }

        internal static double Smooth(double fraction) => fraction * fraction * (3d - 2d * fraction);
    }
}
