using System.Collections.Concurrent;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MusicPlayer
{
    /// <summary>
    /// Modern layout (v2): fixed size window, sidebar with pages (Player / Equalizer / Appearance),
    /// playlist with album cover thumbnails, "now playing" panel with the big animated cover.
    /// The controls created by the designer are kept (all the playback logic uses them), they are
    /// only moved into the new panels and restyled here.
    /// </summary>
    public partial class Form1
    {
        private const int WindowW = 1440, WindowH = 860;
        private const int SidebarW = 72;
        private const int RowH = 64;          // playlist row height
        private const int ThumbSize = 48;

        private Panel sidebar = null!;
        private IconButton navPlayer = null!, navEq = null!, navThemes = null!;
        private Label versionLabel = null!;
        private CardPanel playlistCard = null!, nowCard = null!, eqCard = null!, themeCard = null!;
        private Label playlistTitle = null!, playlistInfo = null!, nowTitle = null!, nowInfo = null!;
        private Label eqTitle = null!, eqHint = null!, themeTitle = null!, themeHint = null!, aboutLabel = null!;
        private TextBox searchBox = null!;
        private Label searchInfo = null!;
        private IconButton eqResetBtn = null!, quickEqBtn = null!, muteBtn = null!;
        private readonly List<ThemeSwatch> swatches = new List<ThemeSwatch>();
        private readonly ToolTip tips = new ToolTip { InitialDelay = 500 };
        private int hoverRow = -1;
        private int lastListCount = -1;
        private float volumeBeforeMute = 1f;

        // ------------------------------------------------------------------ layout

        private void ApplyModernLayout()
        {
            Theme.Load();
            SuspendLayout();

            // fixed window size, no background picture
            AutoScaleMode = AutoScaleMode.None;
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(WindowW, WindowH);
            StartPosition = FormStartPosition.CenterScreen;
            BackgroundImage = null;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 10f);
            Text = "Music Player";

            // not used any more (drag & drop reorders, tooltips replace the hint line, the window has its own close button)
            foreach (Control c in new Control[] { label1, horzRollingLabel, hintLbl, exit, moveSongFWDbtn, moveSongBWDbtn })
                c.Visible = false;

            BuildSidebar();
            BuildPlaylistCard();
            BuildNowPlayingCard();
            BuildEqCard();
            BuildThemeCard();
            AddTooltips();

            ApplyTheme();
            ShowPage(0);
            ResumeLayout(true);
        }

        private static Label MakeLabel(Control parent, string text, float size, bool bold, Rectangle bounds,
                                       ContentAlignment align = ContentAlignment.MiddleLeft)
        {
            var l = new Label
            {
                Text = text, AutoSize = false, Bounds = bounds, TextAlign = align, AutoEllipsis = true,
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), UseMnemonic = false,
            };
            parent.Controls.Add(l);
            return l;
        }

        private static void Place(Control parent, Control c, int x, int y, int w, int h)
        {
            parent.Controls.Add(c);
            c.SetBounds(x, y, w, h);
            c.Visible = true;
        }

        private static void StyleIcon(Button b, IconKind icon, float iconSize, bool round = false)
        {
            if (b is IconButton ib) { ib.Icon = icon; ib.IconSize = iconSize; ib.Round = round; }
            b.BackgroundImage = null;
            b.Image = null;
            b.Text = "";
        }

        private void BuildSidebar()
        {
            sidebar = new Panel { Bounds = new Rectangle(0, 0, SidebarW, WindowH) };
            Controls.Add(sidebar);

            IconButton Nav(IconKind icon, int y, int page, string name)
            {
                var b = new IconButton { Icon = icon, IconSize = 24, Bounds = new Rectangle(12, y, 48, 48), Name = name };
                b.Click += (s, e) => ShowPage(page);
                sidebar.Controls.Add(b);
                return b;
            }
            navPlayer = Nav(IconKind.Music, 24, 0, "navPlayer");
            navEq = Nav(IconKind.Equalizer, 84, 1, "navEq");
            navThemes = Nav(IconKind.Palette, 144, 2, "navThemes");

            versionLabel = MakeLabel(sidebar, "v" + Theme.Version, 8.5f, false, new Rectangle(0, WindowH - 36, SidebarW, 24), ContentAlignment.MiddleCenter);
        }

        private void BuildPlaylistCard()
        {
            playlistCard = new CardPanel { Bounds = new Rectangle(SidebarW + 20, 20, 608, WindowH - 40) };
            Controls.Add(playlistCard);
            int w = playlistCard.Width;

            playlistTitle = MakeLabel(playlistCard, "Playlist", 16f, true, new Rectangle(20, 14, 300, 32));
            playlistInfo = MakeLabel(playlistCard, "", 9.5f, false, new Rectangle(20, 46, 340, 22));

            StyleIcon(openAudio, IconKind.Plus, 18);
            StyleIcon(button2, IconKind.Save, 20);
            StyleIcon(deletePlaylist, IconKind.Trash, 20);
            Place(playlistCard, openAudio, w - 20 - 36 * 3 - 8 * 2, 22, 36, 36);
            Place(playlistCard, button2, w - 20 - 36 * 2 - 8, 22, 36, 36);
            Place(playlistCard, deletePlaylist, w - 20 - 36, 22, 36, 36);

            searchBox = new TextBox { PlaceholderText = "Search in the playlist  (Enter: next match, Esc: clear)", Font = new Font("Segoe UI", 10f) };
            Place(playlistCard, new RoundField(searchBox), 20, 78, w - 40 - 90, 32);
            searchInfo = MakeLabel(playlistCard, "", 9f, false, new Rectangle(w - 20 - 86, 80, 86, 28), ContentAlignment.MiddleRight);
            searchBox.TextChanged += (s, e) => { SearchPlaylist(fromNext: false); };
            searchBox.KeyDown += SearchBox_KeyDown;

            // the list: owner drawn rows with cover thumbnails
            musicList.OwnerDraw = true;
            musicList.HeaderStyle = ColumnHeaderStyle.None;
            musicList.FullRowSelect = true;
            musicList.BorderStyle = BorderStyle.None;
            musicList.SmallImageList = new ImageList { ImageSize = new Size(1, RowH) };   // row height
            musicList.Font = new Font("Segoe UI", 10f);
            Place(playlistCard, musicList, 8, 120, w - 16, playlistCard.Height - 132);
            columnHeader1.Width = musicList.ClientSize.Width;
            columnHeader2.Width = 0;
            columnHeader3.Width = 0;
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(musicList, true);
            musicList.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            musicList.DrawItem += MusicList_DrawItem;
            musicList.DrawSubItem += (s, e) => { };
            musicList.MouseMove += MusicList_MouseMove;
            musicList.MouseLeave += (s, e) => { if (hoverRow >= 0) { InvalidateRow(hoverRow); hoverRow = -1; } };
            musicList.Resize += (s, e) => columnHeader1.Width = musicList.ClientSize.Width;
            musicList.HandleCreated += (s, e) => SetDarkScrollbar(musicList);
            if (musicList.IsHandleCreated) SetDarkScrollbar(musicList);
        }

        private void BuildNowPlayingCard()
        {
            int x0 = playlistCard.Right + 20;
            nowCard = new CardPanel { Bounds = new Rectangle(x0, 20, WindowW - 20 - x0, WindowH - 40) };
            Controls.Add(nowCard);
            int w = nowCard.Width;            // 700
            int cw = w - 40;                  // content width

            const int cover = 320;
            Place(nowCard, coverBox, (w - cover) / 2, 22, cover, cover);
            coverBox.SizeMode = PictureBoxSizeMode.StretchImage;
            using (var path = UiIcons.RoundRect(new RectangleF(0, 0, cover, cover), 18))
                coverBox.Region = new Region(path);

            nowTitle = MakeLabel(nowCard, "No song selected", 17f, true, new Rectangle(20, 352, cw, 36), ContentAlignment.MiddleCenter);
            nowInfo = MakeLabel(nowCard, "Add music with the + button or drop files on the list", 10f, false, new Rectangle(20, 388, cw, 22), ContentAlignment.MiddleCenter);

            Place(nowCard, lyricsView, 20, 420, cw, 150);

            // lyrics search: always visible under the lyrics
            foreach (var tb in new[] { artistBox, titleBox })
                tb.Font = new Font("Segoe UI", 10f);
            Place(nowCard, new RoundField(artistBox), 20, 579, 236, 32);
            Place(nowCard, new RoundField(titleBox), 264, 579, 286, 32);
            artistBox.Visible = titleBox.Visible = true;
            if (getLyricsBtn is IconButton lb) { lb.Icon = IconKind.Search; lb.IconSize = 15; lb.Filled = true; }
            getLyricsBtn.Font = new Font("Segoe UI", 9.5f);
            getLyricsBtn.Text = "Search lyrics";
            getLyricsBtn.BackgroundImage = null;
            Place(nowCard, getLyricsBtn, 558, 579, cw - 538, 32);

            Place(nowCard, customPBarPos, 20, 620, cw, 30);

            // transport: stop | prev | PLAY | next | equalizer
            int cx = w / 2;
            StyleIcon(Play, IconKind.Play, 26, round: true);
            StyleIcon(pauseButton, IconKind.Pause, 24, round: true);
            StyleIcon(prevSongButton, IconKind.Prev, 24);
            StyleIcon(nextSongButton, IconKind.Next, 24);
            StyleIcon(Stop, IconKind.Stop, 18);
            Place(nowCard, Play, cx - 32, 660, 64, 64);
            Place(nowCard, pauseButton, cx - 32, 660, 64, 64);
            pauseButton.Visible = false;
            Place(nowCard, prevSongButton, cx - 32 - 20 - 48, 668, 48, 48);
            Place(nowCard, nextSongButton, cx + 32 + 20, 668, 48, 48);
            Place(nowCard, Stop, cx - 32 - 20 - 48 - 24 - 44, 670, 44, 44);
            quickEqBtn = new IconButton { Icon = IconKind.Equalizer, IconSize = 20 };
            quickEqBtn.Click += (s, e) => ShowPage(1);
            Place(nowCard, quickEqBtn, cx + 32 + 20 + 48 + 24, 670, 44, 44);

            // volume (click the speaker: mute) and balance
            muteBtn = new IconButton { Icon = IconKind.Volume, IconSize = 20 };
            muteBtn.Click += MuteBtn_Click;
            Place(nowCard, muteBtn, 16, 738, 32, 32);
            Place(nowCard, volumeSlider1, 50, 740, 330, 28);
            Place(nowCard, customHorzScrollBar1, 410, 740, cw - 390, 28);

            // level meter (left / right channel)
            volumeMeter1.Label = "L";
            volumeMeter2.Label = "R";
            Place(nowCard, volumeMeter1, 20, 780, cw, 10);
            Place(nowCard, volumeMeter2, 20, 794, cw, 10);
        }

        private void BuildEqCard()
        {
            eqCard = new CardPanel { Bounds = new Rectangle(SidebarW + 20, 20, WindowW - SidebarW - 40, WindowH - 40) };
            Controls.Add(eqCard);
            int w = eqCard.Width;

            eqTitle = MakeLabel(eqCard, "Equalizer", 16f, true, new Rectangle(28, 18, 400, 32));
            eqHint = MakeLabel(eqCard, "Drag a band up / down to change the gain, left / right to change its bandwidth.", 9.5f, false, new Rectangle(28, 50, 700, 22));

            eqResetBtn = new IconButton { Icon = IconKind.Reset, IconSize = 16, Filled = true, Text = "Reset to 0", Font = new Font("Segoe UI", 9.5f) };
            eqResetBtn.Click += (s, e) => { foreach (var bar in EqBars) bar.Value = 0; };
            Place(eqCard, eqResetBtn, w - 28 - 140 - 12 - 130, 24, 130, 36);
            if (button1 is IconButton sb) { sb.Icon = IconKind.SaveEq; sb.IconSize = 16; sb.Filled = true; }
            button1.Text = "Save EQ";
            button1.Font = new Font("Segoe UI", 9.5f);
            button1.BackgroundImage = null;
            Place(eqCard, button1, w - 28 - 140, 24, 140, 36);

            var bars = EqBars;
            var labels = new[] { hz31, hz62, hz125, hz250, hz500, hz1K, hz2K, hz4K, hz8K, hz16K };
            int left = 60, right = w - 60;
            float slot = (right - left) / 10f;
            for (int i = 0; i < 10; i++)
            {
                int bx = (int)(left + slot * i + (slot - 76) / 2);
                Place(eqCard, bars[i], bx, 110, 76, 600);
                var l = labels[i];
                l.AutoSize = false;
                l.TextAlign = ContentAlignment.MiddleCenter;
                l.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                Place(eqCard, l, (int)(left + slot * i), 720, (int)slot, 26);
            }
        }

        private CustomScrollbar[] EqBars => new[] { band1CustomBar, band2CustomBar, band3CustomBar, band4CustomBar, band5CustomBar,
                                                    band6CustomBar, band7CustomBar, band8CustomBar, band9CustomBar, band10CustomBar };

        private void BuildThemeCard()
        {
            themeCard = new CardPanel { Bounds = new Rectangle(SidebarW + 20, 20, WindowW - SidebarW - 40, WindowH - 40) };
            Controls.Add(themeCard);

            themeTitle = MakeLabel(themeCard, "Appearance", 16f, true, new Rectangle(28, 18, 400, 32));
            themeHint = MakeLabel(themeCard, "Choose a background. The choice is remembered.", 9.5f, false, new Rectangle(28, 50, 700, 22));

            const int sw = 270, sh = 200, gap = 26;
            for (int i = 0; i < Theme.All.Length; i++)
            {
                var s = new ThemeSwatch(Theme.All[i]);
                int col = i % 4, row = i / 4;
                int startX = (themeCard.Width - (4 * sw + 3 * gap)) / 2;
                Place(themeCard, s, startX + col * (sw + gap), 100 + row * (sh + gap), sw, sh);
                s.Click += (o, e) =>
                {
                    Theme.Select(((ThemeSwatch)o!).Theme);
                    ApplyTheme();
                };
                swatches.Add(s);
            }

            aboutLabel = MakeLabel(themeCard,
                $"Music Player  v{Theme.Version}\nAudio: NAudio  ·  Tags: TagLib#  ·  Lyrics: lrclib.net",
                10f, false, new Rectangle(28, themeCard.Height - 80, 700, 52), ContentAlignment.MiddleLeft);
        }

        private void AddTooltips()
        {
            tips.SetToolTip(openAudio, "Add .mp3 / .wav files");
            tips.SetToolTip(button2, "Save the playlist");
            tips.SetToolTip(deletePlaylist, "Delete all songs from the playlist");
            tips.SetToolTip(Play, "Play");
            tips.SetToolTip(pauseButton, "Pause");
            tips.SetToolTip(Stop, "Stop");
            tips.SetToolTip(prevSongButton, "Previous song");
            tips.SetToolTip(nextSongButton, "Next song");
            tips.SetToolTip(quickEqBtn, "Equalizer");
            tips.SetToolTip(muteBtn, "Mute / unmute");
            tips.SetToolTip(customHorzScrollBar1, "Balance (double click: centre)");
            tips.SetToolTip(button1, "Save the equalizer settings");
            tips.SetToolTip(navPlayer, "Player");
            tips.SetToolTip(navEq, "Equalizer");
            tips.SetToolTip(navThemes, "Appearance");
        }

        // ------------------------------------------------------------------ pages and theme

        private void ShowPage(int page)
        {
            playlistCard.Visible = nowCard.Visible = page == 0;
            eqCard.Visible = page == 1;
            themeCard.Visible = page == 2;
            navPlayer.Selected = page == 0;
            navEq.Selected = page == 1;
            navThemes.Selected = page == 2;
            foreach (var b in new[] { navPlayer, navEq, navThemes }) b.Invalidate();
        }

        private void ApplyTheme()
        {
            var t = Theme.Current;
            sidebar.BackColor = t.Sidebar;
            versionLabel.ForeColor = t.SubText;
            versionLabel.BackColor = t.Sidebar;

            foreach (var card in new CardPanel[] { playlistCard, nowCard, eqCard, themeCard })
            {
                card.BackColor = t.Card;
                foreach (Control c in card.Controls)
                {
                    if (c is Label l)
                    {
                        l.BackColor = t.Card;
                        l.ForeColor = t.Text;
                    }
                    else if (c is RoundField rf)
                    {
                        rf.Box.BackColor = t.Field;
                        rf.Box.ForeColor = t.Text;
                    }
                }
            }
            foreach (var l in new[] { playlistInfo, nowInfo, eqHint, themeHint, aboutLabel, searchInfo })
                l.ForeColor = t.SubText;

            musicList.BackColor = t.Card;
            musicList.ForeColor = t.Text;
            lyricsView.BackColor = Theme.Mix(t.Card, Color.Black, 0.28f);
            ResetMissingColors();

            coverBox.BackColor = t.Card;
            DefaultThumb?.Dispose();
            DefaultThumb = RoundThumb(UiIcons.DefaultCover(ThumbSize * 2, t), ThumbSize);
            var oldCover = coverBox.InitialImage;
            coverBox.InitialImage = UiIcons.DefaultCover(coverBox.Width, t);
            coverAnim?.SetBackColor(t.Card);
            if (coverAnim != null && !coverAnim.IsActive)
                coverAnim.ShowDefault();
            else if (coverAnim == null)
                coverBox.Image = coverBox.InitialImage;
            if (oldCover != null && !ReferenceEquals(oldCover, coverBox.Image)) oldCover.Dispose();

            foreach (var s in swatches) s.Selected = s.Theme == t;
            Invalidate(true);
        }

        /// <summary>Window background: vertical gradient (also painted in the rounded corners of the cards).</summary>
        protected override void OnPaintBackground(PaintEventArgs e) => PaintBackgroundFor(this, e.Graphics);

        public void PaintBackgroundFor(Control c, Graphics g)
        {
            var t = Theme.Current;
            var rc = new Rectangle(0, 0, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
            using var brush = new LinearGradientBrush(rc, t.BgTop, t.BgBottom, 90f);
            if (!ReferenceEquals(c, this))
                brush.TranslateTransform(-c.Left, -c.Top, MatrixOrder.Append);
            g.FillRectangle(brush, new Rectangle(0, 0, c.Width, c.Height));
        }

        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

        /// <summary>Dark scrollbar for the list (Windows 10 1809+; ignored on older systems).</summary>
        private static void SetDarkScrollbar(Control c)
        {
            try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { }
        }

        private void MuteBtn_Click(object? sender, EventArgs e)
        {
            if (volumeSlider1.Volume > 0.0001f)
            {
                volumeBeforeMute = volumeSlider1.Volume;
                volumeSlider1.Volume = 0f;
            }
            else
            {
                volumeSlider1.Volume = volumeBeforeMute > 0.0001f ? volumeBeforeMute : 1f;
            }
        }

        // ------------------------------------------------------------------ now playing / playlist header

        /// <summary>Title and details of the loaded song in the "now playing" panel.</summary>
        private void UpdateNowPlaying(string fileName, string title, string artist, string bitrate)
        {
            nowTitle.Text = title.Trim().Length > 0 ? title : Path.GetFileNameWithoutExtension(fileName);
            var parts = new List<string>();
            if (artist.Trim().Length > 0) parts.Add(artist.Trim());
            if (bitrate != "0" && bitrate.Length > 0) parts.Add(bitrate + " kb/s");
            nowInfo.Text = parts.Count > 0 ? string.Join("   ·   ", parts) : Path.GetFileName(fileName);
            musicList.Invalidate();   // highlight of the current song
        }

        /// <summary>Song count and total length (cheap: only when the number of songs changed).</summary>
        private void UpdatePlaylistHeaderIfNeeded()
        {
            int n = musicList.Items.Count;
            if (n == lastListCount) return;
            lastListCount = n;
            var total = TimeSpan.Zero;
            foreach (ListViewItem it in musicList.Items)
                if (it.SubItems.Count > 2 && TimeSpan.TryParseExact(it.SubItems[2].Text, @"mm\:ss", null, out var d))
                    total += d;
            playlistInfo.Text = n == 0 ? "Empty  –  add music with +" :
                $"{n} songs  ·  {(int)total.TotalHours}:{total.Minutes:00}:{total.Seconds:00}";
            if (searchBox.Text.Length > 0) SearchPlaylist(false);
        }

        // ------------------------------------------------------------------ playlist drawing

        private Image? DefaultThumb;

        private void InvalidateRow(int index)
        {
            if (index >= 0 && index < musicList.Items.Count)
                musicList.Invalidate(musicList.Items[index].Bounds);
        }

        private void MusicList_MouseMove(object? sender, MouseEventArgs e)
        {
            int idx = musicList.HitTest(e.Location).Item?.Index ?? -1;
            if (idx == hoverRow) return;
            InvalidateRow(hoverRow);
            hoverRow = idx;
            InvalidateRow(hoverRow);
        }

        private void MusicList_DrawItem(object? sender, DrawListViewItemEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            var item = e.Item;
            int i = e.ItemIndex;
            var r = new Rectangle(0, e.Bounds.Y, musicList.ClientSize.Width, e.Bounds.Height);

            bool current = i == currentSongIndex;
            bool selected = item.Selected;
            bool hovered = i == hoverRow;
            bool missing = item.ForeColor == Color.Gray;
            bool match = searchBox.Text.Length > 0 && RowMatches(item, searchBox.Text);

            using (var bg = new SolidBrush(t.Card)) g.FillRectangle(bg, r);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rowRect = new RectangleF(r.X + 4, r.Y + 2, r.Width - 8, r.Height - 4);
            if (current || selected || hovered || match)
            {
                Color c = current ? t.CardHover : match ? Color.FromArgb(60, t.Accent) : selected ? Theme.Mix(t.Card, t.CardHover, 0.7f) : Theme.Mix(t.Card, t.CardHover, 0.55f);
                using var b = new SolidBrush(c);
                using var p = UiIcons.RoundRect(rowRect, 10);
                g.FillPath(b, p);
            }
            if (current)
            {
                using var ab = new SolidBrush(t.Accent);
                using var ap = UiIcons.RoundRect(new RectangleF(rowRect.X, rowRect.Y + 12, 4, rowRect.Height - 24), 2);
                g.FillPath(ab, ap);
            }

            // cover thumbnail
            string path = i < songPaths.Count ? songPaths[i] : "";
            var thumb = GetThumb(path) ?? DefaultThumb;
            var tr = new Rectangle(r.X + 14, r.Y + (r.Height - ThumbSize) / 2, ThumbSize, ThumbSize);
            if (thumb != null)
            {
                if (missing)
                {
                    var cm = new ColorMatrix { Matrix33 = 0.35f };
                    using var ia = new ImageAttributes();
                    ia.SetColorMatrix(cm);
                    g.DrawImage(thumb, tr, 0, 0, thumb.Width, thumb.Height, GraphicsUnit.Pixel, ia);
                }
                else g.DrawImage(thumb, tr);
            }

            // texts
            string title = item.Text;
            string artist = item.SubItems.Count > 1 ? item.SubItems[1].Text : "";
            string dur = item.SubItems.Count > 2 ? item.SubItems[2].Text : "";
            if (dur == "N/A") dur = "";
            int tx = tr.Right + 14;
            int durW = 56;
            Color titleColor = missing ? Color.FromArgb(110, t.Text) : current ? t.Accent : t.Text;
            using var titleFont = new Font("Segoe UI", 10.5f, current ? FontStyle.Bold : FontStyle.Regular);
            using var subFont = new Font("Segoe UI", 9f);
            var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center };
            using (var b = new SolidBrush(titleColor))
                g.DrawString(title, titleFont, b, new RectangleF(tx, r.Y + 8, r.Width - tx - durW - 12, 26), sf);
            string sub = missing ? "File not found" : artist.Length > 0 ? artist : "Unknown artist";
            using (var b = new SolidBrush(missing ? Color.FromArgb(230, 120, 110) : t.SubText))
                g.DrawString(sub, subFont, b, new RectangleF(tx, r.Y + 33, r.Width - tx - durW - 12, 22), sf);

            // duration (or a small "playing" marker)
            var dr = new RectangleF(r.Right - durW - 12, r.Y, durW, r.Height);
            if (current && output != null && output.PlaybackState == NAudio.Wave.PlaybackState.Playing && !stopped)
                UiIcons.Draw(g, IconKind.Volume, dr, 18, t.Accent);
            else
                using (var b = new SolidBrush(t.SubText))
                    g.DrawString(dur, subFont, b, dr, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
        }

        // ------------------------------------------------------------------ cover thumbnails (loaded in the background)

        private readonly ConcurrentDictionary<string, Image?> thumbs = new ConcurrentDictionary<string, Image?>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentStack<string> thumbQueue = new ConcurrentStack<string>();
        private readonly ConcurrentDictionary<string, byte> thumbQueued = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim thumbSignal = new SemaphoreSlim(0);
        private bool thumbWorkerStarted;

        /// <summary>Thumbnail of the song cover, or null (not loaded yet / no cover). Loading is queued.</summary>
        private Image? GetThumb(string path)
        {
            if (path.Length == 0) return null;
            if (thumbs.TryGetValue(path, out var img)) return img;
            if (thumbQueued.TryAdd(path, 0))
            {
                thumbQueue.Push(path);   // newest first: the rows that are visible right now
                thumbSignal.Release();
                if (!thumbWorkerStarted)
                {
                    thumbWorkerStarted = true;
                    var worker = new Thread(ThumbWorker) { IsBackground = true, Name = "Cover thumbnails", Priority = ThreadPriority.BelowNormal };
                    worker.Start();
                }
            }
            return null;
        }

        private void ThumbWorker()
        {
            while (true)
            {
                thumbSignal.Wait();
                if (!thumbQueue.TryPop(out var path)) continue;
                Image? result = null;
                try
                {
                    if (File.Exists(path))
                    {
                        using var f = TagLib.File.Create(path);
                        var pic = f.Tag.Pictures?.FirstOrDefault(p => p.Data != null && p.Data.Count > 0);
                        if (pic != null)
                        {
                            using var ms = new MemoryStream(pic.Data.Data);
                            using var src = Image.FromStream(ms);
                            result = RoundThumb(src, ThumbSize);
                        }
                    }
                }
                catch { result = null; }
                thumbs[path] = result;
                try
                {
                    if (IsHandleCreated && !IsDisposed)
                        BeginInvoke(new Action(() => musicList.Invalidate()));
                }
                catch { /* closing */ }
            }
        }

        /// <summary>Square (centre cropped) thumbnail with rounded corners; disposes nothing.</summary>
        private static Bitmap RoundThumb(Image src, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            int side = Math.Min(src.Width, src.Height);
            var srcRect = new Rectangle((src.Width - side) / 2, (src.Height - side) / 2, side, side);
            using var scaled = new Bitmap(size, size);
            using (var sg = Graphics.FromImage(scaled))
            {
                sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                sg.DrawImage(src, new Rectangle(0, 0, size, size), srcRect, GraphicsUnit.Pixel);
            }
            using var tex = new TextureBrush(scaled);
            using var path = UiIcons.RoundRect(new RectangleF(0, 0, size, size), size * 0.18f);
            g.FillPath(tex, path);
            return bmp;
        }

        // ------------------------------------------------------------------ search in the playlist

        private static bool RowMatches(ListViewItem it, string q)
        {
            q = q.Trim();
            if (q.Length == 0) return false;
            return it.Text.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
                   (it.SubItems.Count > 1 && it.SubItems[1].Text.Contains(q, StringComparison.CurrentCultureIgnoreCase));
        }

        private int searchPos = -1;

        /// <summary>Highlights the matching songs and scrolls to the first (or next) one. Does not load any song.</summary>
        private void SearchPlaylist(bool fromNext)
        {
            string q = searchBox.Text;
            var matches = new List<int>();
            if (q.Trim().Length > 0)
                foreach (ListViewItem it in musicList.Items)
                    if (RowMatches(it, q)) matches.Add(it.Index);

            searchInfo.Text = q.Trim().Length == 0 ? "" : matches.Count == 0 ? "no match" : matches.Count == 1 ? "1 match" : $"{matches.Count} matches";
            if (matches.Count > 0)
            {
                int next = fromNext ? matches.FirstOrDefault(m => m > searchPos, matches[0]) : matches[0];
                searchPos = next;
                musicList.EnsureVisible(next);
                if (fromNext) searchInfo.Text = $"{matches.IndexOf(next) + 1} / {matches.Count}";
            }
            musicList.Invalidate();
        }

        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SearchPlaylist(fromNext: true);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                searchBox.Text = "";
            }
        }

        /// <summary>Missing songs are marked with Color.Gray; others follow the list colour after a theme change.</summary>
        private void ResetMissingColors()
        {
            foreach (ListViewItem it in musicList.Items)
                if (it.ForeColor != Color.Gray) it.ForeColor = musicList.ForeColor;
        }
    }
}
