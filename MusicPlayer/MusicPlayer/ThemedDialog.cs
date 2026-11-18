using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MusicPlayer
{
    /// <summary>
    /// Message / question box in the look of the player (theme colours, rounded, icon, flat buttons).
    /// Same use as MessageBox.Show: ThemedDialog.Show(this, text, caption, buttons, icon) returns a DialogResult.
    /// Enter = the main button, Esc = Cancel / No. The window can be moved by dragging it anywhere.
    /// </summary>
    public sealed class ThemedDialog : Form
    {
        private const int W = 460, Pad = 24, IconSize = 40, BtnH = 38;
        private readonly string caption, text;
        private readonly MessageBoxIcon icon;
        private readonly Font titleFont = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        private readonly Font textFont = new Font("Segoe UI", 10.5f);
        private Rectangle textRect;

        public static DialogResult Show(IWin32Window? owner, string text, string caption = "",
            MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            using var dlg = new ThemedDialog(text, caption, buttons, icon);
            return owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
        }

        private ThemedDialog(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            this.text = text ?? "";
            this.caption = caption ?? "";
            this.icon = icon;
            Text = this.caption;   // taskbar / accessibility name
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Theme.Current.Card;
            Font = textFont;

            // size from the text
            int textX = Pad + (icon != MessageBoxIcon.None ? IconSize + 16 : 0);
            int textW = W - textX - Pad;
            int textH;
            using (var g = CreateGraphics())
                textH = (int)Math.Ceiling(g.MeasureString(this.text, textFont, textW).Height);
            int titleH = this.caption.Length > 0 ? 30 : 0;
            textRect = new Rectangle(textX, Pad + titleH, textW, textH);
            int contentBottom = Math.Max(textRect.Bottom, Pad + IconSize);
            ClientSize = new Size(W, contentBottom + 26 + BtnH + Pad);

            // buttons (right aligned, the main one last = rightmost)
            var defs = buttons switch
            {
                MessageBoxButtons.YesNo => new[] { ("No", DialogResult.No), ("Yes", DialogResult.Yes) },
                MessageBoxButtons.YesNoCancel => new[] { ("Cancel", DialogResult.Cancel), ("No", DialogResult.No), ("Yes", DialogResult.Yes) },
                MessageBoxButtons.OKCancel => new[] { ("Cancel", DialogResult.Cancel), ("OK", DialogResult.OK) },
                MessageBoxButtons.RetryCancel => new[] { ("Cancel", DialogResult.Cancel), ("Retry", DialogResult.Retry) },
                _ => new[] { ("OK", DialogResult.OK) },
            };
            int x = W - Pad;
            int y = ClientSize.Height - Pad - BtnH;
            for (int i = defs.Length - 1; i >= 0; i--)
            {
                var (label, result) = defs[i];
                bool primary = i == defs.Length - 1;
                var b = new IconButton
                {
                    Text = label, Primary = primary, Filled = !primary, DialogResult = result,
                    Font = new Font("Segoe UI", 10f, primary ? FontStyle.Bold : FontStyle.Regular),
                    Size = new Size(104, BtnH),
                };
                x -= b.Width;
                b.Location = new Point(x, y);
                x -= 10;
                Controls.Add(b);
                if (primary) AcceptButton = b;
            }
            // Esc: Cancel if there is one, else No, else the only button
            DialogResult escResult = defs.Any(d => d.Item2 == DialogResult.Cancel) ? DialogResult.Cancel :
                                     defs.Any(d => d.Item2 == DialogResult.No) ? DialogResult.No : defs[0].Item2;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DialogResult = escResult; Close(); } };

            // rounded window
            using (var path = UiIcons.RoundRect(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 16))
                Region = new Region(path);

            if (icon == MessageBoxIcon.Error || icon == MessageBoxIcon.Warning) System.Media.SystemSounds.Exclamation.Play();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x00020000;   // CS_DROPSHADOW
                return cp;
            }
        }

        // move the dialog by dragging it anywhere
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);   // WM_NCLBUTTONDOWN, HTCAPTION
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // background: card colour with a thin accent line on top and a soft border
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var bg = new LinearGradientBrush(r, Theme.Mix(t.Card, t.BgTop, 0.35f), t.Card, 90f))
            using (var p = UiIcons.RoundRect(r, 16))
            {
                g.FillPath(bg, p);
                using var border = new Pen(Color.FromArgb(60, t.Text), 1f);
                g.DrawPath(border, p);
            }

            // icon
            if (icon != MessageBoxIcon.None)
            {
                Color c = icon switch
                {
                    MessageBoxIcon.Error => Color.FromArgb(232, 84, 84),
                    MessageBoxIcon.Warning => Color.FromArgb(240, 180, 60),
                    MessageBoxIcon.Question => t.Accent,
                    _ => Color.FromArgb(90, 170, 240),
                };
                var ir = new RectangleF(Pad, Pad + (caption.Length > 0 ? 2 : 0), IconSize, IconSize);
                using (var b = new SolidBrush(Color.FromArgb(45, c))) g.FillEllipse(b, ir);
                using (var pen = new Pen(c, 2f)) g.DrawEllipse(pen, ir.X + 1, ir.Y + 1, ir.Width - 2, ir.Height - 2);
                string glyph = icon switch
                {
                    MessageBoxIcon.Error => "✕",
                    MessageBoxIcon.Warning => "!",
                    MessageBoxIcon.Question => "?",
                    _ => "i",
                };
                using var gf = new Font("Segoe UI", 16f, FontStyle.Bold);
                using var gb = new SolidBrush(c);
                g.DrawString(glyph, gf, gb, ir, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }

            // title and text
            if (caption.Length > 0)
                using (var tb = new SolidBrush(t.Text))
                    g.DrawString(caption, titleFont, tb, new RectangleF(textRect.X, Pad - 2, textRect.Width, 30),
                                 new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
            using (var sb = new SolidBrush(caption.Length > 0 ? Theme.Mix(t.Text, t.SubText, 0.35f) : t.Text))
                g.DrawString(text, textFont, sb, textRect);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { titleFont.Dispose(); textFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
