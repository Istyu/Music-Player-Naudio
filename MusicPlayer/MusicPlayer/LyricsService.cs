using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MusicPlayer
{
    public sealed class LyricLine
    {
        public TimeSpan Time;   // start of the line (only meaningful for synced lyrics)
        public string Text = "";
    }

    public sealed class LyricsResult
    {
        public bool Synced;             // true: Lines have real timestamps (LRC)
        public bool Instrumental;
        public List<LyricLine> Lines = new List<LyricLine>();
        public string Source = "";      // "cache" / "lrclib"
        public string MatchInfo = "";   // which LRCLIB entry was used (artist - title)
        public double ShiftSec;         // timing correction set by the user (+ = lines come later)
        public double VersionDiffSec;   // file length - length of the version the timestamps were made for

        /// <summary>Move all timestamps (manual timing correction).</summary>
        public void Shift(double deltaSec)
        {
            var d = TimeSpan.FromSeconds(deltaSec);
            foreach (var l in Lines) l.Time += d;
            ShiftSec = Math.Round(ShiftSec + deltaSec, 2);
        }
    }

    /// <summary>
    /// Downloads lyrics from LRCLIB (https://lrclib.net, free, no API key) and caches them next to the exe
    /// (LyricsCache folder: .lrc = synced, .txt = plain, .none = not found / instrumental).
    /// Only artist, title and duration are sent to the service.
    /// </summary>
    public static class LyricsService
    {
        private const string ApiBase = "https://lrclib.net/api/";
        private static readonly TimeSpan NotFoundRetryAfter = TimeSpan.FromDays(7);
        private const double MaxDurationDiffSynced = 3.0;   // seconds; same recording
        private const double MaxDurationDiffOther = 15.0;   // other edit: timestamps usable with a timing correction
        private const int MaxRequests = 8;                  // per song (LRCLIB rate-limits clients that send too many)

        private static readonly HttpClient http = CreateClient();
        public static readonly string CacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LyricsCache");

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            // LRCLIB asks clients to identify themselves (generic name, no personal data)
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MusicPlayerCS/1.0 (+https://lrclib.net)");
            return c;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>
        /// Get lyrics for a song. Returns null when nothing was found.
        /// <paramref name="artist"/> / <paramref name="title"/> are tried first (the boxes), then variants made from
        /// the tags and the file name. <paramref name="forceOnline"/> skips the cache (manual search).
        /// Throws HttpRequestException / TaskCanceledException on network problems, BusyException on rate limit.
        /// </summary>
        public static async Task<LyricsResult?> GetAsync(string audioPath, string artist, string title, string album,
            double durationSec, bool forceOnline, CancellationToken ct, string tagArtist = "", string tagTitle = "")
        {
            string key = CacheKey(audioPath);

            if (!forceOnline)
            {
                var cached = ReadCache(key, out bool knownMissing);
                if (cached != null || knownMissing)
                    return cached;
            }

            var found = await SearchOnlineAsync(audioPath, artist, title, tagArtist, tagTitle, durationSec, ct);
            WriteCache(key, found);
            return found;
        }

        /// <summary>Only the local cache (no network). Returns true if the cache knows the answer (also "not found").</summary>
        public static bool TryGetCached(string audioPath, out LyricsResult? result)
        {
            result = ReadCache(CacheKey(audioPath), out bool knownMissing);
            return result != null || knownMissing;
        }

        /// <summary>Save the lyrics again (after a manual timing correction).</summary>
        public static void SaveToCache(string audioPath, LyricsResult result) => WriteCache(CacheKey(audioPath), result);

        /// <summary>
        /// Artist and title for searching: from the tags, or from the file name ("Artist - Title").
        /// A tag title like "Latto - Lottery (Lyrics)" (typical for downloaded videos, where the artist tag is the
        /// uploader channel) is split into artist and title.
        /// </summary>
        public static (string artist, string title) GuessArtistTitle(string fileName, string tagArtist, string tagTitle)
        {
            string artist = tagArtist?.Trim() ?? "";
            string title = tagTitle?.Trim() ?? "";
            string file = Normalize(Path.GetFileNameWithoutExtension(fileName));

            if (title.Length > 0 && SplitDash(title, out string left, out string right))
            {
                string nLeft = Normalize(MainArtist(left));
                bool leftInFile = nLeft.Length > 0 && file.Contains(nLeft);
                bool artistInFile = artist.Length > 0 && file.Contains(Normalize(artist));
                bool leftHasArtist = artist.Length > 0 && Normalize(left).Contains(Normalize(MainArtist(artist)));
                if (artist.Length == 0 || (leftInFile && (!artistInFile || leftHasArtist)))
                    return (left, right);
            }
            if (artist.Length > 0 && title.Length > 0)
                return (StripTrackNo(artist), title);

            var (fa, ft) = FromFileName(fileName);
            if (artist.Length == 0) artist = fa;
            if (title.Length == 0) title = ft;
            return (artist, title);
        }

        /// <summary>
        /// Remove the usual noise from titles: "(Official Video)", "[Bass Boosted]", "ft. X", "Lyrics", "35,39,50hz"...
        /// (the word list comes from the old lyrics code)
        /// </summary>
        public static string CleanTitle(string title)
        {
            string t = title;
            // "Title | Easy Lyrics ..." / "Title // channel": the rest is added by the uploader
            t = Regex.Replace(t, @"\s+(\||//|•)\s+.*$", "");
            // bracketed parts with noise words
            t = Regex.Replace(t, @"[\(\[][^\)\]]*\b(official|video|audio|lyrics?|music|clip|hd|hq|4k|bass\s*boost(ed)?|boosted|explicit|uncensored|dirty|clean|version|visuali[sz]er|full|vietsub|sub|letra|testo|paroles|slowed|reverb|sped\s*up|nightcore|8d)\b[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
            // featuring parts
            t = Regex.Replace(t, @"[\(\[]\s*(feat\.?|ft\.?|featuring)\s[^\)\]]*[\)\]]", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s(feat\.?|ft\.?|featuring)\s.*$", "", RegexOptions.IgnoreCase);
            // year in brackets: "(2017)"
            t = Regex.Replace(t, @"[\(\[]\s*(19|20)\d{2}\s*[\)\]]", "");
            // frequencies of bass boosted uploads: "35,39,50hz", "50 Hz"
            t = Regex.Replace(t, @"\b\d+(?:[,_.]\s?\d+)*\s*hz\b", "", RegexOptions.IgnoreCase);
            // "- Radio Edit", "- Extended Mix"... and file name leftovers ("FULL~1")
            t = Regex.Replace(t, @"\s+[-–]\s+(radio|extended|original|club|single|album)\s*(edit|mix|version)\b.*$", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"(\s+full)?\s*~\d+\s*$|\s+full\s*$", "", RegexOptions.IgnoreCase);
            // loose noise words at the end ("... Official Music Video", "... Lyrics", "... BassBoosted")
            t = Regex.Replace(t, @"(\s+(official|music|video|audio|lyrics?|hd|hq|bass\s*boost(ed)?|boosted))+\s*$", "", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\s{2,}", " ").Trim(' ', '-', '_', '|', ',');
            return t.Length > 0 ? t : title.Trim();
        }

        /// <summary>First artist only ("A, B & C feat. D" -> "A") for a broader search.</summary>
        public static string MainArtist(string artist)
        {
            var parts = Regex.Split(artist, @"\s*(,|&|/|\bx\b|\bvs\.?\b|\bfeat\.?\b|\bft\.?\b|\band\b|\bwith\b)\s*", RegexOptions.IgnoreCase);
            return parts.Length > 0 && parts[0].Trim().Length > 0 ? parts[0].Trim() : artist.Trim();
        }

        // ------------------------------------------------------------------ helpers for the variants

        /// <summary>"Artist - Title" -> parts (first " - ", also "–" and "_-_").</summary>
        private static bool SplitDash(string s, out string left, out string right)
        {
            var m = Regex.Match(s, @"^\s*(.+?)\s+[-–—]\s+(.+)$");
            if (m.Success)
            {
                left = m.Groups[1].Value.Trim();
                right = m.Groups[2].Value.Trim();
                return left.Length > 0 && right.Length > 0;
            }
            left = right = "";
            return false;
        }

        /// <summary>
        /// Track number at the start: "01. ", "01 - ", "02 " (zero padded). Not "50 Cent" / "99 Problems".
        /// </summary>
        private static string StripTrackNo(string s) =>
            Regex.Replace(s.Trim(), @"^(\d{1,3}\s*\.\s*|\d{1,3}\s+[-–]\s+|0\d{1,2}\s+)(?=\S)", "");

        private static (string artist, string title) FromFileName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName).Replace("_-_", " - ").Replace('_', ' ');
            name = StripTrackNo(name);
            name = Regex.Replace(name, @"\s{2,}", " ").Trim();
            if (SplitDash(name, out string a, out string t))
                return (a, t);
            return ("", name);
        }

        /// <summary>Everything in brackets removed (aggressive variant).</summary>
        private static string StripBrackets(string s)
        {
            string t = Regex.Replace(s, @"[\(\[\{][^\)\]\}]*[\)\]\}]", " ");
            t = Regex.Replace(t, @"\s{2,}", " ").Trim(' ', '-', '_', '|', ',');
            return t;
        }

        // ------------------------------------------------------------------ online search

        private sealed class Entry
        {
            public long Id;
            public string TrackName = "", ArtistName = "", AlbumName = "";
            public double Duration;
            public bool Instrumental;
            public string? Plain, Synced;
        }

        /// <summary>One search request: either by fields (title [+ artist]) or free text.</summary>
        private sealed record Variant(string Artist, string Title, string? FreeText);

        private static List<Variant> BuildVariants(string audioPath, string artist, string title, string tagArtist, string tagTitle)
        {
            var fields = new List<(string a, string t)>();
            void Add(string a, string t)
            {
                a = StripTrackNo(a ?? "");          // "01. Artist" (track number)
                t = CleanTitle(StripTrackNo(t ?? ""));
                if (Normalize(t).Length == 0) return;
                // a part that is only "Radio Edit 2000" / "Extended Mix" is not a title
                if (Regex.IsMatch(t, @"^(radio|extended|original|club|single|album)\s*(edit|mix|version)\b", RegexOptions.IgnoreCase)) return;
                if (!fields.Any(f => Normalize(MainArtist(f.a)) == Normalize(MainArtist(a)) && Normalize(f.t) == Normalize(t)))
                    fields.Add((a, t));
            }

            // 1) what is in the boxes (tags / file name / typed by the user)
            Add(artist, title);
            // 2) titles in "Artist - Title" form (tag artist is often the uploader channel)
            void AddSplit(string t)
            {
                // "Collection - Artist - Title": also the last two parts
                if (!SplitDash(t, out string l, out string r)) return;
                Add(l, r);
                if (SplitDash(r, out string l2, out string r2)) Add(l2, r2);
            }
            AddSplit(title);
            AddSplit(tagTitle);
            // 3) the file name
            var (fa, ft) = FromFileName(audioPath);
            if (fa.Length > 0) { Add(fa, ft); AddSplit(ft); }
            // 4) the same without anything in brackets, and cut at " _ " (a "|" in a file name; sometimes an "&" though,
            //    so only as an extra variant)
            foreach (var f in fields.ToList())
                Add(f.a, StripBrackets(f.t));
            string mainArtist0 = fields.Count > 0 ? fields[0].a : artist;
            foreach (var (t, isFileName) in new[] { (title, false), (tagTitle, false), (Path.GetFileNameWithoutExtension(audioPath), true) })
            {
                int us = (t ?? "").IndexOf(" _ ", StringComparison.Ordinal);
                if (us <= 0) continue;
                string before = t!.Substring(0, us);
                if (SplitDash(before, out string l, out string r)) Add(l, r);          // "Artist - Title _ junk"
                else if (!isFileName) Add(mainArtist0, before);                       // title tag "Title _ junk"
            }

            var list = fields.Select(f => new Variant(f.a, f.t, null)).ToList();

            // 5) free text: artist + title, then the cleaned file name (helps with tags that are completely wrong)
            if (fields.Count > 0)
                list.Add(new Variant("", "", (MainArtist(fields[0].a) + " " + fields[0].t).Trim()));
            string fileText = StripBrackets(Path.GetFileNameWithoutExtension(audioPath).Replace('_', ' '));
            fileText = Regex.Replace(fileText, @"\b\d+(?:[,.]\d+)*\s*hz\b", "", RegexOptions.IgnoreCase);
            fileText = Regex.Replace(fileText, @"\b(official|music|video|audio|lyrics?|bass\s*boosted|hd|hq)\b", "", RegexOptions.IgnoreCase);
            fileText = Regex.Replace(fileText, @"\s*-\s*", " ").Trim();
            list.Add(new Variant("", "", Regex.Replace(fileText, @"\s{2,}", " ")));

            // 6) title only (the artist in the tags may be wrong) - accepted only if the artist is in the file name / tags
            foreach (var f in fields.Where(f => f.a.Length > 0).ToList())
                list.Add(new Variant("", f.t, null));

            // 7) title without its last word or two ("Up N' Away 2K" -> "Up N' Away", "... Remix", "... 2000"),
            //    title only; accepted only with the same length (or the artist in the file name / tags)
            foreach (var f in fields.ToList())
            {
                var words = f.t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int drop = 1; drop <= 2 && words.Length - drop >= 2; drop++)
                {
                    string shorter = string.Join(" ", words.Take(words.Length - drop)).Trim(' ', '-', ',', '&');
                    if (Normalize(shorter).Length >= 5)
                        list.Add(new Variant("", shorter, null));
                }
            }

            return list;
        }

        private static async Task<LyricsResult?> SearchOnlineAsync(string audioPath, string artist, string title,
            string tagArtist, string tagTitle, double duration, CancellationToken ct)
        {
            // everything we know about the song: a found entry is accepted only if its artist and title appear here
            // (or are similar to one of the searched variants)
            string haystack = Normalize(Path.GetFileNameWithoutExtension(audioPath) + " " + artist + " " + title + " " + tagArtist + " " + tagTitle);
            var variants = BuildVariants(audioPath, artist, title, tagArtist, tagTitle);

            var accepted = new Dictionary<long, Entry>();   // artist and title confirmed
            var weak = new Dictionary<long, Entry>();       // only the title and the length match (wrong artist tag)
            var sent = new HashSet<string>();
            int requests = 0;

            foreach (var v in variants)
            {
                string query = v.FreeText != null
                    ? "q=" + Uri.EscapeDataString(v.FreeText)
                    : "track_name=" + Uri.EscapeDataString(v.Title) +
                      (v.Artist.Length > 0 ? "&artist_name=" + Uri.EscapeDataString(MainArtist(v.Artist)) : "");
                if (Normalize(Uri.UnescapeDataString(query)).Length < 3 || !sent.Add(query.ToLowerInvariant()))
                    continue;
                if (requests >= MaxRequests)
                    break;
                if (requests > 0)
                    await Task.Delay(300, ct);   // be gentle with the free service
                requests++;

                foreach (var e in await SearchAsync(query, ct))
                {
                    if (accepted.ContainsKey(e.Id)) continue;
                    if (IsSameSong(e, variants, haystack))
                        accepted[e.Id] = e;
                    else if (SameTitleAndLength(e, variants, duration))
                        weak[e.Id] = e;
                }

                // good enough: synced lyrics made for this recording
                if (accepted.Values.Any(e => e.Synced != null && DurationOk(e, duration, MaxDurationDiffSynced)))
                    break;
            }

            // weak matches only when nothing with the right artist was found
            return PickBest(accepted.Count > 0 ? accepted.Values.ToList() : weak.Values.ToList(), duration);
        }

        private static bool DurationOk(Entry e, double duration, double maxDiff) =>
            duration <= 0 || e.Duration <= 0 || Math.Abs(e.Duration - duration) <= maxDiff;

        /// <summary>Artist unknown / wrong in the tags (e.g. the album name): exactly the same title and the same length.</summary>
        private static bool SameTitleAndLength(Entry e, List<Variant> variants, double duration)
        {
            string exactTitle = Normalize(CleanTitle(e.TrackName));
            return duration > 0 && e.Duration > 0 && Math.Abs(e.Duration - duration) <= 2 && exactTitle.Length >= 4 &&
                   variants.Any(v => v.Title.Length > 0 && Normalize(v.Title) == exactTitle);
        }

        private static bool IsSameSong(Entry e, List<Variant> variants, string haystack)
        {
            string track = Normalize(StripBrackets(CleanTitle(e.TrackName)));
            string artistMain = Normalize(MainArtist(e.ArtistName));
            if (track.Length == 0 || artistMain.Length == 0)
                return false;

            bool titleOk = (track.Length >= 3 && haystack.Contains(track)) ||
                           variants.Any(v => v.Title.Length > 0 && Similar(e.TrackName, v.Title) && Similar(StripBrackets(CleanTitle(e.TrackName)), v.Title));
            bool artistOk = (artistMain.Length >= 2 && haystack.Contains(artistMain)) ||
                            variants.Any(v => v.Artist.Length > 0 && (Similar(e.ArtistName, v.Artist) || Similar(MainArtist(e.ArtistName), MainArtist(v.Artist))));
            return titleOk && artistOk;
        }

        private static LyricsResult? PickBest(List<Entry> ok, double duration)
        {
            if (ok.Count == 0)
                return null;

            // best: synced lyrics made for (almost) the same length -> timestamps fit this recording;
            // several uploads: the one most others agree with (user uploads are sometimes badly timed)
            var synced = ok.Where(c => !string.IsNullOrWhiteSpace(c.Synced) && DurationOk(c, duration, MaxDurationDiffSynced)).ToList();
            if (synced.Count > 0)
                return Build(Consensus(synced, duration), useSynced: true, duration);

            // another edit of the song (radio edit, video intro...): timestamps can be corrected with the timing buttons
            var other = ok.Where(c => !string.IsNullOrWhiteSpace(c.Synced) && DurationOk(c, duration, MaxDurationDiffOther)).ToList();
            if (other.Count > 0)
                return Build(Consensus(other, duration), useSynced: true, duration);

            // otherwise plain text
            var plain = ok.Where(c => !string.IsNullOrWhiteSpace(c.Plain) || !string.IsNullOrWhiteSpace(c.Synced))
                          .OrderBy(c => Math.Abs(c.Duration - duration)).FirstOrDefault();
            if (plain != null)
                return Build(plain, useSynced: false, duration);

            var instr = ok.FirstOrDefault(c => c.Instrumental);
            return instr != null ? new LyricsResult { Instrumental = true, Source = "lrclib", MatchInfo = $"{instr.ArtistName} - {instr.TrackName}" } : null;
        }

        /// <summary>
        /// The entry whose timestamps agree best with the others (medoid). Lines are matched by their text;
        /// a badly timed upload (e.g. everything 8 s early) is far from all the others and loses.
        /// </summary>
        private static Entry Consensus(List<Entry> list, double duration)
        {
            var byDuration = list.OrderBy(e => Math.Abs(e.Duration - duration)).ToList();
            if (list.Count <= 2)
                return byDuration[0];

            var parsed = byDuration.Select(e => ParseLrc(e.Synced!)).ToList();
            int best = 0;
            double bestScore = double.MaxValue;
            for (int i = 0; i < parsed.Count; i++)
            {
                double score = 0;
                for (int j = 0; j < parsed.Count; j++)
                    if (i != j) score += TimingDistance(parsed[i], parsed[j]) + TimingDistance(parsed[j], parsed[i]);
                if (score < bestScore - 1e-6) { bestScore = score; best = i; }   // tie: closest length (list order)
            }
            return byDuration[best];
        }

        /// <summary>Median time difference of the lines of <paramref name="a"/> to the same lines in <paramref name="b"/> (seconds).</summary>
        private static double TimingDistance(List<LyricLine> a, List<LyricLine> b)
        {
            const double Far = 30;
            var times = new Dictionary<string, List<double>>();
            foreach (var l in b)
            {
                string k = Normalize(l.Text);
                if (k.Length == 0) continue;
                if (!times.TryGetValue(k, out var t)) times[k] = t = new List<double>();
                t.Add(l.Time.TotalSeconds);
            }
            var diffs = new List<double>();
            foreach (var l in a)
            {
                string k = Normalize(l.Text);
                if (k.Length > 0 && times.TryGetValue(k, out var t))
                    diffs.Add(t.Min(x => Math.Abs(x - l.Time.TotalSeconds)));
            }
            if (diffs.Count < 3)
                return Far;
            diffs.Sort();
            return Math.Min(Far, diffs[diffs.Count / 2]);
        }

        private static LyricsResult Build(Entry e, bool useSynced, double duration)
        {
            var r = new LyricsResult { Source = "lrclib", MatchInfo = $"{e.ArtistName} - {e.TrackName}" };
            if (useSynced)
            {
                r.Synced = true;
                r.Lines = ParseLrc(e.Synced!);
                if (duration > 0 && e.Duration > 0 && Math.Abs(duration - e.Duration) > MaxDurationDiffSynced)
                    r.VersionDiffSec = Math.Round(duration - e.Duration);
            }
            else
            {
                string text = !string.IsNullOrWhiteSpace(e.Plain) ? e.Plain! : StripLrc(e.Synced!);
                r.Lines = text.Replace("\r", "").Split('\n').Select(l => new LyricLine { Text = l.Trim() }).ToList();
                TrimEmpty(r.Lines);
            }
            return r;
        }

        private static async Task<List<Entry>> SearchAsync(string query, CancellationToken ct)
        {
            var list = new List<Entry>();
            using var resp = await http.GetAsync($"{ApiBase}search?{query}", ct);
            ThrowIfBusy(resp);
            if (!resp.IsSuccessStatusCode)
                return list;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var el in doc.RootElement.EnumerateArray())
                    list.Add(ReadEntry(el));
            return list;
        }

        /// <summary>LRCLIB answered "too many requests": a temporary state, must not be cached as "not found".</summary>
        public sealed class BusyException : Exception
        {
            public BusyException() : base("lrclib.net is busy (too many requests)") { }
        }

        private static void ThrowIfBusy(HttpResponseMessage resp)
        {
            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode == 503)
                throw new BusyException();
        }

        private static Entry ReadEntry(JsonElement el)
        {
            string? Str(string name) => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return new Entry
            {
                Id = el.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : 0,
                TrackName = System.Net.WebUtility.HtmlDecode(Str("trackName") ?? ""),
                ArtistName = System.Net.WebUtility.HtmlDecode(Str("artistName") ?? ""),
                AlbumName = Str("albumName") ?? "",
                Duration = el.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetDouble() : 0,
                Instrumental = el.TryGetProperty("instrumental", out var i) && i.ValueKind == JsonValueKind.True,
                Plain = Str("plainLyrics"),
                Synced = Str("syncedLyrics"),
            };
        }

        /// <summary>Loose comparison: letters/digits only, case and accent insensitive, one may contain the other.</summary>
        private static bool Similar(string a, string b)
        {
            string na = Normalize(a), nb = Normalize(b);
            if (na.Length == 0 || nb.Length == 0)
                return false;
            return na == nb || na.Contains(nb) || nb.Contains(na);
        }

        private static string Normalize(string s)
        {
            string d = (s ?? "").Replace("&", " and ").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ LRC

        private static readonly Regex TimeTag = new Regex(@"\[(\d{1,3}):(\d{1,2}(?:[.:]\d{1,3})?)\]", RegexOptions.Compiled);
        private static readonly Regex WordTag = new Regex(@"<\d{1,3}:\d{1,2}(?:[.:]\d{1,3})?>", RegexOptions.Compiled);
        private static readonly Regex OffsetTag = new Regex(@"^\[offset:\s*([+-]?\d+)\s*\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex VersionTag = new Regex(@"^\[version-diff:\s*([+-]?\d+(?:\.\d+)?)\s*\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static List<LyricLine> ParseLrc(string lrc) => ParseLrc(lrc, out _, out _);

        /// <param name="offsetMs">value of the [offset:] tag (positive = lyrics come earlier)</param>
        public static List<LyricLine> ParseLrc(string lrc, out double offsetMs, out double versionDiff)
        {
            var lines = new List<LyricLine>();
            offsetMs = 0;
            versionDiff = 0;
            foreach (string raw in lrc.Replace("\r", "").Split('\n'))
            {
                string trimmed = raw.Trim();
                var off = OffsetTag.Match(trimmed);
                if (off.Success) { offsetMs = double.Parse(off.Groups[1].Value, CultureInfo.InvariantCulture); continue; }
                var ver = VersionTag.Match(trimmed);
                if (ver.Success) { versionDiff = double.Parse(ver.Groups[1].Value, CultureInfo.InvariantCulture); continue; }

                var tags = TimeTag.Matches(raw);
                if (tags.Count == 0) continue;
                string text = WordTag.Replace(TimeTag.Replace(raw, ""), "").Trim();
                foreach (Match m in tags) // "[00:12.00][01:30.00]text" = same text twice
                {
                    double min = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    double sec = double.Parse(m.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                    lines.Add(new LyricLine { Time = TimeSpan.FromSeconds(min * 60 + sec), Text = text });
                }
            }
            if (offsetMs != 0) // positive offset = lyrics come earlier
                foreach (var l in lines) l.Time -= TimeSpan.FromMilliseconds(offsetMs);
            lines.Sort((a, b) => a.Time.CompareTo(b.Time));
            return lines;
        }

        private static string StripLrc(string lrc) =>
            string.Join("\n", lrc.Replace("\r", "").Split('\n').Select(l => WordTag.Replace(TimeTag.Replace(l, ""), "").Trim()));

        private static void TrimEmpty(List<LyricLine> lines)
        {
            while (lines.Count > 0 && lines[0].Text.Length == 0) lines.RemoveAt(0);
            while (lines.Count > 0 && lines[^1].Text.Length == 0) lines.RemoveAt(lines.Count - 1);
        }

        // ------------------------------------------------------------------ cache

        private static string CacheKey(string audioPath)
        {
            string name = Path.GetFileNameWithoutExtension(audioPath);
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (name.Length > 80) name = name.Substring(0, 80);
            byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(audioPath.ToLowerInvariant()));
            return name + "_" + Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
        }

        private static LyricsResult? ReadCache(string key, out bool knownMissing)
        {
            knownMissing = false;
            try
            {
                string lrc = Path.Combine(CacheDir, key + ".lrc");
                string txt = Path.Combine(CacheDir, key + ".txt");
                string none = Path.Combine(CacheDir, key + ".none");

                if (File.Exists(lrc))
                {
                    string content = File.ReadAllText(lrc, Encoding.UTF8);
                    var lines = ParseLrc(content, out double offsetMs, out double versionDiff);
                    var re = Regex.Match(content, @"^\[re:lrclib\.net (.*)\]\s*$", RegexOptions.Multiline);
                    return new LyricsResult { Synced = true, Lines = lines, Source = "cache", ShiftSec = -offsetMs / 1000.0, VersionDiffSec = versionDiff,
                                              MatchInfo = re.Success ? re.Groups[1].Value.Trim() : "" };
                }
                if (File.Exists(txt))
                {
                    var r = new LyricsResult { Source = "cache" };
                    r.Lines = File.ReadAllLines(txt, Encoding.UTF8).Select(l => new LyricLine { Text = l.Trim() }).ToList();
                    TrimEmpty(r.Lines);
                    return r;
                }
                if (File.Exists(none) && DateTime.Now - File.GetLastWriteTime(none) < NotFoundRetryAfter)
                {
                    knownMissing = true;
                    if (File.ReadAllText(none).StartsWith("instrumental"))
                        return new LyricsResult { Instrumental = true, Source = "cache" };
                }
            }
            catch { /* unreadable cache entry: search online */ }
            return null;
        }

        private static void WriteCache(string key, LyricsResult? r)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);
                foreach (string ext in new[] { ".lrc", ".txt", ".none" })
                {
                    string f = Path.Combine(CacheDir, key + ext);
                    if (File.Exists(f)) File.Delete(f);
                }

                if (r == null || r.Instrumental)
                {
                    File.WriteAllText(Path.Combine(CacheDir, key + ".none"), r?.Instrumental == true ? "instrumental" : "not found");
                }
                else if (r.Synced)
                {
                    var sb = new StringBuilder();
                    if (r.MatchInfo.Length > 0) sb.AppendLine("[re:lrclib.net " + r.MatchInfo.Replace("]", ")") + "]");
                    if (r.VersionDiffSec != 0) sb.AppendLine("[version-diff:" + r.VersionDiffSec.ToString(CultureInfo.InvariantCulture) + "]");
                    // the timing correction is kept as a standard LRC [offset:] tag, the timestamps stay the original ones
                    if (r.ShiftSec != 0) sb.AppendLine("[offset:" + Math.Round(-r.ShiftSec * 1000).ToString(CultureInfo.InvariantCulture) + "]");
                    var shift = TimeSpan.FromSeconds(r.ShiftSec);
                    foreach (var l in r.Lines)
                    {
                        var t = l.Time - shift;
                        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
                        sb.AppendLine($"[{(int)t.TotalMinutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}]{l.Text}");
                    }
                    File.WriteAllText(Path.Combine(CacheDir, key + ".lrc"), sb.ToString(), new UTF8Encoding(false));
                }
                else
                {
                    File.WriteAllLines(Path.Combine(CacheDir, key + ".txt"), r.Lines.Select(l => l.Text), new UTF8Encoding(false));
                }
            }
            catch { /* cache is only an optimisation */ }
        }
    }
}
