using NAudio.Wave;

namespace MusicPlayer
{
    /// <summary>
    /// Stereo balance inside the audio chain (instead of changing the Windows volume of the left / right channel).
    /// Turning to one side lowers the other channel; the centre leaves both untouched.
    /// The gain follows the target smoothly (per sample), so moving the slider never crackles.
    /// Works with any output (WaveOut, WASAPI, Android AudioTrack...) and the level meter after it sees the result.
    /// </summary>
    public sealed class BalanceSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private volatile float targetL = 1f, targetR = 1f;
        private float gainL = 1f, gainR = 1f;

        // per-sample smoothing: about 10 ms to reach the new value at 44.1 / 48 kHz
        private const float Smoothing = 0.0025f;

        public BalanceSampleProvider(ISampleProvider source)
        {
            if (source.WaveFormat.Channels != 2)
                throw new ArgumentException("BalanceSampleProvider needs a stereo source", nameof(source));
            this.source = source;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>-1 = left only ... 0 = centre ... +1 = right only.</summary>
        public float Balance
        {
            set
            {
                float b = Math.Clamp(value, -1f, 1f);
                targetL = b > 0 ? 1f - b : 1f;
                targetR = b < 0 ? 1f + b : 1f;
            }
        }

        /// <summary>Jump to the target at once (new song: no fade from the previous value).</summary>
        public void SnapToTarget()
        {
            gainL = targetL;
            gainR = targetR;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);
            float tl = targetL, tr = targetR;

            if (gainL == tl && gainR == tr)
            {
                if (tl == 1f && tr == 1f)
                    return read;                       // centre: nothing to do
                for (int n = 0; n + 1 < read; n += 2)
                {
                    buffer[offset + n] *= tl;
                    buffer[offset + n + 1] *= tr;
                }
                return read;
            }

            for (int n = 0; n + 1 < read; n += 2)
            {
                gainL += (tl - gainL) * Smoothing;
                gainR += (tr - gainR) * Smoothing;
                buffer[offset + n] *= gainL;
                buffer[offset + n + 1] *= gainR;
            }
            if (Math.Abs(gainL - tl) < 0.0005f) gainL = tl;   // settle exactly (fast path above)
            if (Math.Abs(gainR - tr) < 0.0005f) gainR = tr;
            return read;
        }
    }
}
