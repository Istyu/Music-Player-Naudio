using NAudio.Wave;

namespace MusicPlayer
{
    /// <summary>
    /// Opens MP3 files with NAudio's Mp3FileReader, also when the file contains "frames" in another format.
    ///
    /// Mp3FileReader refuses the whole file if any frame has another sample rate ("does not support sample rate
    /// changes"). In practice this is almost always NOT real audio: garbage after the song (an appended picture,
    /// a damaged tag...) where a random byte pattern looks like an MP3 frame header. Such files play fine in
    /// other players.
    ///
    /// Fallback (only when the normal open fails): all frames are scanned, the format of the song is the most
    /// common one (sample rate + MPEG version + layer + mono/stereo), and only frames of that format are kept
    /// (copied into memory), so the stray frames are skipped.
    /// </summary>
    public static class Mp3Tolerant
    {
        public static Mp3FileReader Open(string path)
        {
            try
            {
                return new Mp3FileReader(path);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("sample rate", StringComparison.OrdinalIgnoreCase))
            {
                return new Mp3FileReader(CleanStream(path));
            }
        }

        private static MemoryStream CleanStream(string path)
        {
            var frames = new List<(long pos, int len, (int rate, MpegVersion ver, MpegLayer layer, bool mono) fmt)>();
            using (var fs = File.OpenRead(path))
            {
                fs.Position = AudioStart(fs);
                while (true)
                {
                    Mp3Frame? frame;
                    try { frame = Mp3Frame.LoadFromStream(fs); }
                    catch (EndOfStreamException) { break; }
                    if (frame == null) break;
                    long pos = fs.Position - frame.FrameLength;
                    frames.Add((pos, frame.FrameLength,
                        (frame.SampleRate, frame.MpegVersion, frame.MpegLayer, frame.ChannelMode == ChannelMode.Mono)));
                }

                if (frames.Count == 0)
                    throw new InvalidDataException("No MP3 frames found in the file.");

                // the song's format: the one most frames have
                var songFormat = frames.GroupBy(f => f.fmt).OrderByDescending(g => g.Count()).First().Key;

                var ms = new MemoryStream();
                var buf = new byte[4096];
                foreach (var f in frames.Where(f => f.fmt.Equals(songFormat)))
                {
                    fs.Position = f.pos;
                    int left = f.len;
                    while (left > 0)
                    {
                        int n = fs.Read(buf, 0, Math.Min(buf.Length, left));
                        if (n <= 0) break;
                        ms.Write(buf, 0, n);
                        left -= n;
                    }
                }
                ms.Position = 0;
                return ms;
            }
        }

        /// <summary>Position after an ID3v2 tag at the start of the file (0 if there is none).</summary>
        private static long AudioStart(Stream s)
        {
            var h = new byte[10];
            s.Position = 0;
            if (s.Read(h, 0, 10) == 10 && h[0] == 'I' && h[1] == 'D' && h[2] == '3')
            {
                long size = (h[6] & 0x7F) << 21 | (h[7] & 0x7F) << 14 | (h[8] & 0x7F) << 7 | (h[9] & 0x7F);
                bool footer = (h[5] & 0x10) != 0;
                return 10 + size + (footer ? 10 : 0);
            }
            return 0;
        }
    }
}
