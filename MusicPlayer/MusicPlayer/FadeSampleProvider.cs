using NAudio.Wave;

namespace MusicPlayer
{
    /// <summary>
    /// Sample-accurate fade in / fade out and click-free seeking.
    /// The gain is changed per sample inside the audio thread, so the fade is
    /// smooth (no steps / clicks) and its length does not depend on the
    /// Windows timer resolution (Task.Delay(1) really waits ~15 ms).
    /// Seeking is also done inside the audio thread (under the same lock as Read),
    /// so the stream is never repositioned while it is being read.
    /// </summary>
    public class FadeSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly object lockObj = new object();

        // gain ramp
        private float gain = 1.0f;
        private float startGain = 1.0f;
        private float targetGain = 1.0f;
        private long fadePos;
        private long fadeLength;          // in sample frames, 0 = no ramp running
        private bool holdWhenSilent;      // after fade-out: output silence without consuming the source
        private TaskCompletionSource<bool>? fadeTcs;   // owner of a Play/Pause fade

        // seek: 0 = none, 1 = fading out before the seek, 2 = fading in after the seek
        private int seekPhase;
        private Action? seekAction;
        private float seekResumeGain = 1.0f;
        private int seekFadeInMs;
        private TaskCompletionSource<bool>? seekTcs;

        private long framesRendered;      // frames handed to the output device since the last device reset
        private long fadeEndFrame;        // framesRendered value when the last fade finished

        public FadeSampleProvider(ISampleProvider source)
        {
            this.source = source;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>Frame position (since the last device reset) where the last fade ended.</summary>
        public long FadeEndFrame { get { lock (lockObj) return fadeEndFrame; } }

        /// <summary>Frames handed to the output device since the last device reset.</summary>
        public long FramesRendered { get { lock (lockObj) return framesRendered; } }

        private volatile bool sourceEnded;

        /// <summary>
        /// True when the source gave no more samples (end of the song). This is the real end of the audio:
        /// it does not depend on TotalTime, which is only an estimate for resampled (converted) streams.
        /// The source must not pad with silence (WaveChannel32.PadWithZeroes = false).
        /// </summary>
        public bool SourceEnded => sourceEnded;

        /// <summary>Call after the stream was repositioned outside SeekSmooth (paused / stopped seek, Stop).</summary>
        public void ClearEnded() => sourceEnded = false;

        /// <summary>Call this whenever the output device position is reset (waveOutReset).</summary>
        public void ResetFrameCounter()
        {
            lock (lockObj)
            {
                framesRendered = 0;
                fadeEndFrame = 0;
            }
        }

        /// <summary>Set the gain immediately and cancel any running fade.</summary>
        public void SetGain(float value)
        {
            TaskCompletionSource<bool>? old, seekDone;
            lock (lockObj)
            {
                seekDone = FinishPendingSeekNow();
                gain = startGain = targetGain = value;
                fadeLength = 0;
                holdWhenSilent = false;
                old = fadeTcs;
                fadeTcs = null;
            }
            old?.TrySetResult(false);
            seekDone?.TrySetResult(true);
        }

        /// <summary>
        /// Start a fade from the current gain to <paramref name="target"/>.
        /// The returned task completes (true) when the last faded sample has been rendered,
        /// or with false if the fade was replaced by another call.
        /// </summary>
        public Task<bool> FadeTo(float target, int durationMs, bool holdSilenceWhenDone = false)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool>? old, seekDone;
            lock (lockObj)
            {
                seekDone = FinishPendingSeekNow();
                old = fadeTcs;
                fadeTcs = tcs;
                StartRamp(target, durationMs);
                holdWhenSilent = holdSilenceWhenDone;
            }
            old?.TrySetResult(false);
            seekDone?.TrySetResult(true);
            return tcs.Task;
        }

        /// <summary>
        /// Seek without clicks: short fade-out, run <paramref name="seek"/> (e.g. set CurrentTime)
        /// inside the audio thread, then fade back in. If the output is silent (paused / holding),
        /// the seek is done immediately. Repeated calls during the fade-out only replace the target.
        /// The task completes when the seek has been done.
        /// </summary>
        public Task SeekSmooth(Action seek, int fadeOutMs = 25, int fadeInMs = 60)
        {
            TaskCompletionSource<bool>? oldSeek = null, oldFade = null;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (lockObj)
            {
                if (seekPhase == 1)
                {
                    // still fading out: just replace the target position
                    oldSeek = seekTcs;
                    seekAction = seek;
                    seekTcs = tcs;
                    seekFadeInMs = fadeInMs;
                }
                else if ((fadeLength == 0 && gain <= 0.0f) || (fadeTcs != null && targetGain <= 0.0f))
                {
                    // silent or fading out for Pause: nothing to protect, seek right now
                    RunSeek(seek);
                    sourceEnded = false;
                    tcs.TrySetResult(true);
                }
                else
                {
                    // where to return after the seek: the end of a running fade-in, else the current gain
                    seekResumeGain = fadeLength > 0 ? targetGain : gain;
                    if (fadeTcs != null)
                    {
                        oldFade = fadeTcs;   // a running Play fade-in is taken over by the seek
                        fadeTcs = null;
                    }
                    seekAction = seek;
                    seekTcs = tcs;
                    seekFadeInMs = fadeInMs;
                    seekPhase = 1;
                    holdWhenSilent = false;
                    StartRamp(0.0f, fadeOutMs);
                }
            }
            oldSeek?.TrySetResult(true);
            oldFade?.TrySetResult(true);
            return tcs.Task;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            TaskCompletionSource<bool>? finished = null, seekDone = null;
            int read;
            int channels = WaveFormat.Channels;

            lock (lockObj)
            {
                // Fully faded out (paused): give silence and do not advance the song position,
                // so nothing is lost while the device is pausing.
                if (fadeLength == 0 && holdWhenSilent && gain <= 0.0f)
                {
                    ClearBuffer(buffer, offset, count);
                    framesRendered += count / channels;
                    return count;
                }

                read = source.Read(buffer, offset, count);

                if (read == 0 && seekPhase != 1)
                {
                    // end of the song: the device plays what is still queued, then stops by itself
                    sourceEnded = true;
                    finished = fadeTcs;    // a running Play fade cannot finish any more: do not leave it waiting
                    fadeTcs = null;
                    fadeLength = 0;
                }
                else if (read > 0)
                {
                    sourceEnded = false;   // e.g. seeking back after the end
                }

                if (read == 0 && seekPhase == 1)
                {
                    // end of stream while fading out for a seek: seek now and give silence this time
                    seekDone = DoSeekAndStartFadeIn();
                    ClearBuffer(buffer, offset, count);
                    framesRendered += count / channels;
                    read = count;
                }
                else
                {
                    bool muteRest = false;
                    for (int n = 0; n < read; n += channels)
                    {
                        if (muteRest)
                        {
                            // samples of the OLD position after the seek point: silence
                            for (int c = 0; c < channels && n + c < read; c++)
                                buffer[offset + n + c] = 0.0f;
                            continue;
                        }

                        if (fadeLength > 0)
                        {
                            double t = (double)fadePos / fadeLength;
                            // S-curve (raised cosine): soft start and soft end of the fade
                            double shape = 0.5 - 0.5 * Math.Cos(Math.PI * t);
                            gain = (float)(startGain + (targetGain - startGain) * shape);
                            if (++fadePos >= fadeLength)
                            {
                                gain = targetGain;
                                fadeLength = 0;

                                if (seekPhase == 1)
                                {
                                    seekDone = DoSeekAndStartFadeIn();
                                    muteRest = true;   // the new position is read in the next Read
                                }
                                else if (seekPhase == 2)
                                {
                                    seekPhase = 0;
                                }
                                else
                                {
                                    fadeEndFrame = framesRendered + n / channels + 1;
                                    finished = fadeTcs;
                                    fadeTcs = null;
                                }
                            }
                        }

                        if (gain != 1.0f)
                        {
                            for (int c = 0; c < channels && n + c < read; c++)
                                buffer[offset + n + c] *= gain;
                        }
                    }
                    framesRendered += read / channels;
                }
            }

            finished?.TrySetResult(true);
            seekDone?.TrySetResult(true);
            return read;
        }

        // ---- helpers (call only inside lock) ----

        private void StartRamp(float target, int durationMs)
        {
            startGain = gain;
            targetGain = target;
            fadePos = 0;
            fadeLength = Math.Max(1, (long)WaveFormat.SampleRate * durationMs / 1000);
        }

        private TaskCompletionSource<bool>? DoSeekAndStartFadeIn()
        {
            var action = seekAction;
            var tcs = seekTcs;
            seekAction = null;
            seekTcs = null;
            if (action != null)
                RunSeek(action);
            sourceEnded = false;
            gain = 0.0f;
            seekPhase = 2;
            StartRamp(seekResumeGain, seekFadeInMs);
            return tcs;
        }

        /// <summary>A Play/Pause fade interrupts a seek: do the pending seek immediately.</summary>
        private TaskCompletionSource<bool>? FinishPendingSeekNow()
        {
            TaskCompletionSource<bool>? tcs = null;
            if (seekPhase == 1 && seekAction != null)
            {
                RunSeek(seekAction);
                sourceEnded = false;
                tcs = seekTcs;
            }
            seekAction = null;
            seekTcs = null;
            seekPhase = 0;
            return tcs;
        }

        private static void RunSeek(Action seek)
        {
            try { seek(); }
            catch { /* invalid position etc. - keep playing from where we are */ }
        }

        // NOTE: do NOT use Array.Clear here. NAudio passes a byte[] reinterpreted as float[]
        // (WaveBuffer), and Array.Clear would clear only count BYTES (1/4 of the data),
        // leaving old audio in the buffer -> audible "blip".
        private static void ClearBuffer(float[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
                buffer[offset + i] = 0.0f;
        }
    }
}
