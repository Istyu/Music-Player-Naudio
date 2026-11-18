using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace MusicPlayer
{
    /// <summary>
    /// Flat button that draws a vector icon (or a text) in the theme colours.
    /// Derives from Button, so Click, Enabled, PerformClick etc. work as before.
    /// </summary>
    public class IconButton : Button
    {
        private bool hover, pressed;

        [DefaultValue(IconKind.None)] public IconKind Icon { get; set; }
        [DefaultValue(20f)] public float IconSize { get; set; } = 20f;
        /// <summary>Filled accent circle (the big play / pause button).</summary>
        [DefaultValue(false)] public bool Round { get; set; }
        /// <summary>Navigation button of the current page.</summary>
        [DefaultValue(false)] public bool Selected { get; set; }
        /// <summary>Text button with a field-coloured background (Search lyrics, Save EQ...).</summary>
        [DefaultValue(false)] public bool Filled { get; set; }
        /// <summary>Main action of a dialog: accent coloured rounded button.</summary>
        [DefaultValue(false)] public bool Primary { get; set; }

        public IconButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override bool ShowFocusCues => false;

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? t.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            bool on = Enabled;
            Color fg = on ? t.Text : Color.FromArgb(90, t.Text);

            if (Round)
            {
                Color c = !on ? Color.FromArgb(110, t.Accent) : pressed ? Theme.Mix(t.Accent, Color.Black, 0.15f) :
                          hover ? Theme.Mix(t.Accent, Color.White, 0.15f) : t.Accent;
                using var b = new SolidBrush(c);
                g.FillEllipse(b, r);
                fg = Theme.Mix(t.Accent, Color.Black, 0.82f);
            }
            else if (Primary)
            {
                Color c = pressed ? Theme.Mix(t.Accent, Color.Black, 0.15f) : hover && on ? Theme.Mix(t.Accent, Color.White, 0.15f) : t.Accent;
                using var b = new SolidBrush(c);
                using var p = UiIcons.RoundRect(r, 8);
                g.FillPath(b, p);
                fg = Theme.Mix(t.Accent, Color.Black, 0.82f);
            }
            else if (Filled)
            {
                Color c = pressed ? Theme.Mix(t.Field, Color.Black, 0.2f) : hover && on ? Theme.Mix(t.Field, Color.White, 0.1f) : t.Field;
                using var b = new SolidBrush(c);
                using var p = UiIcons.RoundRect(r, 6);
                g.FillPath(b, p);
            }
            else if (Selected || (hover && on))
            {
                using var b = new SolidBrush(Selected ? Color.FromArgb(40, t.Accent) : Color.FromArgb(pressed ? 40 : 24, 255, 255, 255));
                using var p = UiIcons.RoundRect(r, 8);
                g.FillPath(b, p);
            }
            if (Selected) fg = t.Accent;

            if (Icon != IconKind.None)
            {
                if (string.IsNullOrEmpty(Text) || Round)
                    UiIcons.Draw(g, Icon, r, IconSize, fg);
                else
                {
                    // icon + text
                    var iconBox = new RectangleF(r.X + 8, r.Y, IconSize + 4, r.Height);
                    UiIcons.Draw(g, Icon, iconBox, IconSize, fg);
                    using var tb = new SolidBrush(fg);
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    g.DrawString(Text, Font, tb, new RectangleF(iconBox.Right + 4, r.Y, r.Width - iconBox.Right - 6, r.Height), sf);
                }
            }
            else if (!string.IsNullOrEmpty(Text))
            {
                using var tb = new SolidBrush(fg);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                g.DrawString(Text, Font, tb, r, sf);
            }
        }
    }

    /// <summary>Horizontal level meter with a green-yellow-red scale and a short peak hold.</summary>
    public class LevelMeter : Control
    {
        private float amplitude;
        private float shown;          // smoothed fraction 0..1
        private float peak;           // peak hold fraction
        private DateTime peakTime;

        [DefaultValue(-60f)] public float MinDb { get; set; } = -60f;
        [DefaultValue(3f)] public float MaxDb { get; set; } = 3f;
        /// <summary>Channel label drawn at the left ("L" / "R").</summary>
        [DefaultValue("")] public string Label { get; set; } = "";

        public LevelMeter()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        [DefaultValue(0f)]
        public float Amplitude
        {
            get => amplitude;
            set
            {
                amplitude = value;
                float db = value <= 0 ? MinDb : 20f * (float)Math.Log10(value);
                float f = Math.Clamp((db - MinDb) / (MaxDb - MinDb), 0f, 1f);
                shown = f > shown ? f : shown + (f - shown) * 0.35f;   // fast attack, slower release
                if (shown >= peak || (DateTime.Now - peakTime).TotalMilliseconds > 900)
                {
                    peak = shown;
                    peakTime = DateTime.Now;
                }
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? t.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int labelW = Label.Length > 0 ? 14 : 0;
            if (labelW > 0)
            {
                using var lb = new SolidBrush(t.SubText);
                using var f = new Font("Segoe UI", 7.5f);
                g.DrawString(Label, f, lb, new RectangleF(0, 0, labelW, Height), new StringFormat { LineAlignment = StringAlignment.Center });
            }
            var track = new RectangleF(labelW, 0, Width - labelW - 1, Height - 1);
            using (var tb = new SolidBrush(t.Field))
            using (var tp = UiIcons.RoundRect(track, Height / 2f))
                g.FillPath(tb, tp);

            if (shown > 0.002f)
            {
                var fill = new RectangleF(track.X, track.Y, Math.Max(Height, track.Width * shown), track.Height);
                using var grad = new LinearGradientBrush(track, Color.Empty, Color.Empty, 0f)
                {
                    InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { Color.FromArgb(70, 200, 110), Color.FromArgb(70, 200, 110), Color.FromArgb(240, 210, 70), Color.FromArgb(235, 80, 70) },
                        Positions = new[] { 0f, 0.62f, 0.86f, 1f }
                    }
                };
                using var fp = UiIcons.RoundRect(fill, Height / 2f);
                g.FillPath(grad, fp);
            }
            if (peak > 0.01f)
            {
                float px = track.X + track.Width * peak;
                using var pp = new Pen(Color.FromArgb(220, 255, 255, 255), 2f);
                g.DrawLine(pp, Math.Min(px, track.Right - 2), track.Y + 1, Math.Min(px, track.Right - 2), track.Bottom - 1);
            }
        }
    }

    /// <summary>Volume slider (dB scale, like NAudio's VolumeSlider: Volume = linear gain 0..1).</summary>
    public class ThemedVolumeSlider : Control
    {
        private const float MinDb = -48f;
        private float db = 0f;
        private bool dragging;

        public event EventHandler? VolumeChanged;

        public ThemedVolumeSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        [DefaultValue(1f)]
        public float Volume
        {
            get => db <= MinDb ? 0f : (float)Math.Pow(10, db / 20);
            set
            {
                db = value <= 0 ? MinDb : Math.Clamp(20f * (float)Math.Log10(value), MinDb, 0f);
                Invalidate();
                VolumeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private RectangleF Track => new RectangleF(8, Height / 2f - 2, Width - 16 - 52, 4);

        private void SetFromMouse(int x)
        {
            var tr = Track;
            float f = Math.Clamp((x - tr.X) / tr.Width, 0f, 1f);
            db = MinDb + (0f - MinDb) * f;
            if (f <= 0.001f) db = MinDb;
            Invalidate();
            VolumeChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = true; SetFromMouse(e.X); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromMouse(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            db = Math.Clamp(db + (e.Delta > 0 ? 1.5f : -1.5f), MinDb, 0f);
            Invalidate();
            VolumeChanged?.Invoke(this, EventArgs.Empty);
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? t.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var tr = Track;
            float f = (db - MinDb) / (0f - MinDb);
            using (var b = new SolidBrush(t.Field)) using (var p = UiIcons.RoundRect(tr, 2)) g.FillPath(b, p);
            using (var b = new SolidBrush(t.Text)) using (var p = UiIcons.RoundRect(new RectangleF(tr.X, tr.Y, Math.Max(4, tr.Width * f), tr.Height), 2)) g.FillPath(b, p);
            using (var b = new SolidBrush(t.Text)) g.FillEllipse(b, tr.X + tr.Width * f - 6, Height / 2f - 6, 12, 12);
            using var sb = new SolidBrush(t.SubText);
            using var font = new Font("Segoe UI", 8.5f);
            string txt = db <= MinDb ? "mute" : $"{db:0.0} dB";
            g.DrawString(txt, font, sb, new RectangleF(tr.Right + 8, 0, 52, Height), new StringFormat { LineAlignment = StringAlignment.Center });
        }
    }

    /// <summary>Clickable preview of a theme on the Themes page.</summary>
    public class ThemeSwatch : Control
    {
        private bool hover;
        public Theme Theme { get; }
        public bool Selected { get; set; }

        public ThemeSwatch(Theme theme)
        {
            Theme = theme;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var cur = MusicPlayer.Theme.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? cur.Card);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(3, 3, Width - 7, Height - 34);
            using (var grad = new LinearGradientBrush(r, Theme.BgTop, Theme.BgBottom, 90f))
            using (var p = UiIcons.RoundRect(r, 10))
            {
                g.FillPath(grad, p);
                // mini layout: card + accent dot
                using var card = new SolidBrush(Theme.Card);
                using var cp = UiIcons.RoundRect(new RectangleF(r.X + r.Width * 0.12f, r.Y + r.Height * 0.2f, r.Width * 0.76f, r.Height * 0.6f), 6);
                g.FillPath(card, cp);
                using var acc = new SolidBrush(Theme.Accent);
                float d = r.Height * 0.26f;
                g.FillEllipse(acc, r.X + r.Width / 2 - d / 2, r.Y + r.Height / 2 - d / 2, d, d);
                if (Selected || hover)
                {
                    using var pen = new Pen(Selected ? cur.Accent : Color.FromArgb(120, 255, 255, 255), Selected ? 3f : 1.5f);
                    g.DrawPath(pen, p);
                }
            }
            using var tb = new SolidBrush(Selected ? cur.Accent : cur.Text);
            using var f = new Font("Segoe UI", 10f, Selected ? FontStyle.Bold : FontStyle.Regular);
            g.DrawString(Theme.Name, f, tb, new RectangleF(0, Height - 28, Width, 26), new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
        }
    }

    /// <summary>Rounded input field: hosts a borderless TextBox (clicking anywhere in the field focuses it).</summary>
    public class RoundField : Panel
    {
        public TextBox Box { get; }

        public RoundField(TextBox box)
        {
            Box = box;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            box.BorderStyle = BorderStyle.None;
            Controls.Add(box);
            Cursor = Cursors.IBeam;
            Click += (s, e) => box.Focus();
            box.GotFocus += (s, e) => Invalidate();
            box.LostFocus += (s, e) => Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Box.SetBounds(10, (Height - Box.PreferredHeight) / 2 + 1, Width - 20, Box.PreferredHeight);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var t = Theme.Current;
            e.Graphics.Clear(Parent?.BackColor ?? t.Card);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(t.Field);
            using var p = UiIcons.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 8);
            e.Graphics.FillPath(b, p);
            if (Box.Focused)
            {
                using var pen = new Pen(Color.FromArgb(160, t.Accent), 1.2f);
                e.Graphics.DrawPath(pen, p);
            }
        }
    }

    /// <summary>Panel that paints rounded "cards" behind its children (solid colour, no transparency costs).</summary>
    public class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // rounded corners: the corners show the window background gradient
            var form = FindForm();
            if (form is Form1 f1) f1.PaintBackgroundFor(this, e.Graphics);
            else e.Graphics.Clear(Theme.Current.BgBottom);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(BackColor);
            using var p = UiIcons.RoundRect(new RectangleF(0, 0, Width - 0.5f, Height - 0.5f), 16);
            e.Graphics.FillPath(b, p);
        }
    }
}
