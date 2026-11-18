using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MusicPlayer
{
    /// <summary>
    /// Shows the lyrics around the current playback position: the current line highlighted,
    /// 2 lines before and after, with a short smooth scroll when the line changes.
    /// The current line is always computed from the playback position (never counted on its own),
    /// so seeking, pausing, fading etc. can never make it drift.
    /// </summary>
    public sealed class LyricsView : Control
    {
        private const int VisibleAround = 2;          // lines shown above and below the current one
        private const double ScrollMs = 260;          // duration of the scroll animation

        private LyricsResult? lyrics;
        private string status = "";
        private int currentIndex = -1;                // -1 = before the first line (intro)
        private float shownIndex = -1;                // animated position (scrolls towards currentIndex)
        private float scrollFrom = -1;
        private readonly Stopwatch scrollClock = new Stopwatch();

        private readonly Font currentFont;
        private readonly Font otherFont;
        private readonly Font smallFont;
        private readonly Font buttonFont;

        // timing correction (synced lyrics only): [◀ earlier] [shift / reset] [later ▶] in the top right corner
        private const int BtnNone = -1, BtnEarlier = 0, BtnReset = 1, BtnLater = 2;
        private int hoverButton = BtnNone;
        private readonly ToolTip tip = new ToolTip { InitialDelay = 400 };

        /// <summary>The user changed the timing (the lines are already moved): the owner saves it.</summary>
        public event Action<LyricsResult>? ShiftChanged;

        public LyricsView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(24, 14, 38);   // matches the dark purple form background
            ForeColor = Color.White;
            currentFont = new Font("Segoe UI", 13f, FontStyle.Bold);
            otherFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            smallFont = new Font("Segoe UI", 8f, FontStyle.Italic);
            buttonFont = new Font("Segoe UI", 8f, FontStyle.Regular);
        }

        // ---------- timing buttons

        private bool ButtonsVisible => lyrics != null && lyrics.Synced && lyrics.Lines.Count > 0 && !lyrics.Instrumental;

        private Rectangle ButtonRect(int b)
        {
            const int w0 = 22, w1 = 62, h = 18, top = 3;
            int x = Width - 4 - (w0 + w1 + w0);
            return b switch
            {
                BtnEarlier => new Rectangle(x, top, w0, h),
                BtnReset => new Rectangle(x + w0, top, w1, h),
                _ => new Rectangle(x + w0 + w1, top, w0, h),
            };
        }

        private int HitButton(Point p)
        {
            if (!ButtonsVisible) return BtnNone;
            for (int b = BtnEarlier; b <= BtnLater; b++)
                if (ButtonRect(b).Contains(p)) return b;
            return BtnNone;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int b = HitButton(e.Location);
            if (b == hoverButton) return;
            hoverButton = b;
            Cursor = b == BtnNone ? Cursors.Default : Cursors.Hand;
            string text = b switch
            {
                BtnEarlier => "Lyrics come too late: show them earlier (0.5 s, Ctrl: 0.1 s)",
                BtnLater => "Lyrics come too early: show them later (0.5 s, Ctrl: 0.1 s)",
                BtnReset => "Timing correction (saved for this song). Click: reset",
                _ => "",
            };
            if (text.Length > 0) tip.SetToolTip(this, text); else tip.SetToolTip(this, null);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverButton != BtnNone) { hoverButton = BtnNone; Cursor = Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || lyrics == null) return;
            int b = HitButton(e.Location);
            if (b == BtnNone) return;

            double step = (ModifierKeys & Keys.Control) != 0 ? 0.1 : 0.5;
            double delta = b == BtnEarlier ? -step : b == BtnLater ? step : -lyrics.ShiftSec;
            if (delta == 0) return;
            lyrics.Shift(delta);
            currentIndex = -1000;          // recomputed (and jumped to) on the next position update
            ShiftChanged?.Invoke(lyrics);
            Invalidate();
        }

        private void DrawButtons(Graphics g)
        {
            if (!ButtonsVisible) return;
            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            double s = lyrics!.ShiftSec;
            string label = s == 0 ? "timing" : (s > 0 ? "+" : "−") + Math.Abs(s).ToString("0.0") + " s";
            string[] texts = { "◀", label, "▶" };
            for (int b = BtnEarlier; b <= BtnLater; b++)
            {
                var r = ButtonRect(b);
                bool hot = b == hoverButton;
                using var bg = new SolidBrush(Color.FromArgb(hot ? 110 : 45, Theme.Current.Accent));
                g.FillRectangle(bg, r);
                using var fg = new SolidBrush(Color.FromArgb(hot ? 255 : (b == BtnReset && s == 0 ? 120 : 200), 240, 230, 250));
                g.DrawString(texts[b], buttonFont, fg, r, center);
            }
        }

        /// <summary>Text shown when there are no lyrics (searching, not found, error...).</summary>
        public void SetStatus(string text)
        {
            lyrics = null;
            status = text;
            currentIndex = -1;
            shownIndex = -1;
            scrollClock.Reset();
            Invalidate();
        }

        public void SetLyrics(LyricsResult result)
        {
            lyrics = result;
            status = result.Instrumental ? "♪  Instrumental  ♪" :
                     result.Lines.Count == 0 ? "No lyrics found" : "";
            currentIndex = -1;
            shownIndex = -1;
            scrollClock.Reset();
            Invalidate();
        }

        /// <summary>Called often (timer): repaints only when the line changed or while scrolling.</summary>
        public void UpdatePosition(TimeSpan position, TimeSpan total)
        {
            if (lyrics == null || lyrics.Lines.Count == 0)
                return;

            int idx;
            if (lyrics.Synced)
            {
                // last line whose time <= position (binary search)
                var lines = lyrics.Lines;
                int lo = 0, hi = lines.Count - 1;
                idx = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    if (lines[mid].Time <= position) { idx = mid; lo = mid + 1; }
                    else hi = mid - 1;
                }
            }
            else
            {
                // not synced: only an estimate, proportional to the position in the song
                double frac = total.TotalSeconds > 0 ? position.TotalSeconds / total.TotalSeconds : 0;
                idx = Math.Clamp((int)(frac * lyrics.Lines.Count), 0, lyrics.Lines.Count - 1);
            }

            if (idx != currentIndex)
            {
                // small step (normal playback): animate; big jump (seek): jump directly
                scrollFrom = Math.Abs(idx - currentIndex) <= 2 ? shownIndex : idx;
                currentIndex = idx;
                scrollClock.Restart();
                Invalidate();
            }
            else if (scrollClock.IsRunning)
            {
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? BackColor);   // rounded panel on the card
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var panelBrush = new SolidBrush(BackColor))
            using (var panelPath = UiIcons.RoundRect(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), 12))
                g.FillPath(panelBrush, panelPath);

            var center = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            if (lyrics == null || lyrics.Lines.Count == 0 || lyrics.Instrumental)
            {
                using var b = new SolidBrush(Color.FromArgb(170, 255, 255, 255));
                g.DrawString(status, otherFont, b, ClientRectangle, center);
                return;
            }

            // scroll animation (ease-out)
            if (scrollClock.IsRunning)
            {
                double t = scrollClock.Elapsed.TotalMilliseconds / ScrollMs;
                if (t >= 1) { t = 1; scrollClock.Stop(); }
                double k = 1 - Math.Pow(1 - t, 3);
                shownIndex = (float)(scrollFrom + (currentIndex - scrollFrom) * k);
            }
            else
            {
                shownIndex = currentIndex;
            }

            float lineH = Height / (2f * VisibleAround + 1);
            float midY = Height / 2f;
            int first = (int)Math.Floor(shownIndex) - VisibleAround - 1;
            int last = (int)Math.Ceiling(shownIndex) + VisibleAround + 1;

            for (int i = first; i <= last; i++)
            {
                if (i < -1 || i >= lyrics.Lines.Count)
                    continue;
                float y = midY + (i - shownIndex) * lineH;
                if (y < -lineH || y > Height + lineH)
                    continue;

                string text = i == -1 ? "♪" : lyrics.Lines[i].Text;
                if (text.Length == 0) text = "♪";

                bool isCurrent = i == currentIndex;
                float dist = Math.Abs(i - shownIndex);
                int alpha = isCurrent ? 255 : (int)Math.Max(40, 170 - dist * 45);
                Color col = isCurrent ? Theme.Current.Lyric : Color.FromArgb(alpha, Theme.Current.Text);

                var rect = new RectangleF(8, y - lineH / 2, Width - 16, lineH);
                using var brush = new SolidBrush(col);
                g.DrawString(text, isCurrent ? currentFont : otherFont, brush, rect, center);
            }

            if (!lyrics.Synced)
            {
                using var b = new SolidBrush(Color.FromArgb(140, 255, 255, 255));
                g.DrawString("not synced (estimated position)", smallFont, b, new PointF(6, 3));
            }
            else if (lyrics.VersionDiffSec != 0 && lyrics.ShiftSec == 0)
            {
                // timestamps were made for another edit of the song: probably needs a timing correction
                using var b = new SolidBrush(Color.FromArgb(160, 255, 220, 140));
                g.DrawString($"timed for another version ({(lyrics.VersionDiffSec > 0 ? "+" : "−")}{Math.Abs(lyrics.VersionDiffSec):0} s)  –  adjust with ◀ ▶",
                             smallFont, b, new PointF(6, 3));
            }
            DrawButtons(g);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                currentFont.Dispose();
                buttonFont.Dispose();
                tip.Dispose();
                otherFont.Dispose();
                smallFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
