using System.Drawing.Drawing2D;

namespace MusicPlayer
{
    public enum IconKind
    {
        None, Play, Pause, Stop, Prev, Next, Plus, Save, Trash, Search, Music, Equalizer, Palette,
        Volume, Reset, SaveEq
    }

    /// <summary>Simple vector icons (drawn with GDI+, so they are sharp at any size and take any colour).</summary>
    public static class UiIcons
    {
        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Draw an icon centred in <paramref name="box"/>; <paramref name="size"/> = icon size in pixels.</summary>
        public static void Draw(Graphics g, IconKind kind, RectangleF box, float size, Color color)
        {
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = size;
            float x = box.X + (box.Width - s) / 2f, y = box.Y + (box.Height - s) / 2f;
            using var b = new SolidBrush(color);
            using var pen = new Pen(color, Math.Max(1.6f, s / 11f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

            switch (kind)
            {
                case IconKind.Play:
                    using (var p = new GraphicsPath())
                    {
                        // slightly to the right: optical centre of a triangle
                        p.AddPolygon(new[] { new PointF(x + s * 0.24f, y + s * 0.12f), new PointF(x + s * 0.90f, y + s * 0.5f), new PointF(x + s * 0.24f, y + s * 0.88f) });
                        g.FillPath(b, p);
                    }
                    break;
                case IconKind.Pause:
                    using (var p1 = RoundRect(new RectangleF(x + s * 0.20f, y + s * 0.12f, s * 0.22f, s * 0.76f), s * 0.05f))
                    using (var p2 = RoundRect(new RectangleF(x + s * 0.58f, y + s * 0.12f, s * 0.22f, s * 0.76f), s * 0.05f))
                    { g.FillPath(b, p1); g.FillPath(b, p2); }
                    break;
                case IconKind.Stop:
                    using (var p = RoundRect(new RectangleF(x + s * 0.18f, y + s * 0.18f, s * 0.64f, s * 0.64f), s * 0.08f))
                        g.FillPath(b, p);
                    break;
                case IconKind.Prev:
                case IconKind.Next:
                    {
                        var st = g.Save();
                        if (kind == IconKind.Prev) { g.TranslateTransform(x + s, 0); g.ScaleTransform(-1, 1); g.TranslateTransform(-x, 0); }
                        using (var p = new GraphicsPath())
                        {
                            p.AddPolygon(new[] { new PointF(x + s * 0.12f, y + s * 0.14f), new PointF(x + s * 0.70f, y + s * 0.5f), new PointF(x + s * 0.12f, y + s * 0.86f) });
                            g.FillPath(b, p);
                        }
                        using (var bar = RoundRect(new RectangleF(x + s * 0.72f, y + s * 0.14f, s * 0.14f, s * 0.72f), s * 0.04f))
                            g.FillPath(b, bar);
                        g.Restore(st);
                    }
                    break;
                case IconKind.Plus:
                    g.DrawLine(pen, x + s * 0.5f, y + s * 0.15f, x + s * 0.5f, y + s * 0.85f);
                    g.DrawLine(pen, x + s * 0.15f, y + s * 0.5f, x + s * 0.85f, y + s * 0.5f);
                    break;
                case IconKind.Save:
                case IconKind.SaveEq:
                    using (var p = RoundRect(new RectangleF(x + s * 0.14f, y + s * 0.14f, s * 0.72f, s * 0.72f), s * 0.1f))
                        g.DrawPath(pen, p);
                    g.DrawRectangle(pen, x + s * 0.32f, y + s * 0.14f, s * 0.36f, s * 0.22f);
                    g.DrawLine(pen, x + s * 0.32f, y + s * 0.62f, x + s * 0.68f, y + s * 0.62f);
                    break;
                case IconKind.Trash:
                    g.DrawLine(pen, x + s * 0.16f, y + s * 0.26f, x + s * 0.84f, y + s * 0.26f);
                    g.DrawLine(pen, x + s * 0.40f, y + s * 0.14f, x + s * 0.60f, y + s * 0.14f);
                    using (var p = new GraphicsPath())
                    {
                        p.AddLines(new[] { new PointF(x + s * 0.24f, y + s * 0.26f), new PointF(x + s * 0.30f, y + s * 0.86f), new PointF(x + s * 0.70f, y + s * 0.86f), new PointF(x + s * 0.76f, y + s * 0.26f) });
                        g.DrawPath(pen, p);
                    }
                    g.DrawLine(pen, x + s * 0.44f, y + s * 0.40f, x + s * 0.44f, y + s * 0.72f);
                    g.DrawLine(pen, x + s * 0.56f, y + s * 0.40f, x + s * 0.56f, y + s * 0.72f);
                    break;
                case IconKind.Search:
                    g.DrawEllipse(pen, x + s * 0.14f, y + s * 0.14f, s * 0.52f, s * 0.52f);
                    g.DrawLine(pen, x + s * 0.60f, y + s * 0.60f, x + s * 0.86f, y + s * 0.86f);
                    break;
                case IconKind.Music:
                    DrawNote(g, b, pen, x, y, s);
                    break;
                case IconKind.Equalizer:
                    for (int i = 0; i < 3; i++)
                    {
                        float lx = x + s * (0.22f + 0.28f * i);
                        g.DrawLine(pen, lx, y + s * 0.14f, lx, y + s * 0.86f);
                        float ky = y + s * (i == 1 ? 0.64f : i == 0 ? 0.34f : 0.46f);
                        g.FillEllipse(b, lx - s * 0.11f, ky - s * 0.11f, s * 0.22f, s * 0.22f);
                    }
                    break;
                case IconKind.Palette:
                    using (var p = new GraphicsPath())
                    {
                        p.AddEllipse(x + s * 0.1f, y + s * 0.1f, s * 0.8f, s * 0.8f);
                        g.DrawPath(pen, p);
                    }
                    foreach (var (dx, dy) in new[] { (0.34f, 0.34f), (0.58f, 0.28f), (0.70f, 0.50f), (0.32f, 0.58f) })
                        g.FillEllipse(b, x + s * dx - s * 0.07f, y + s * dy - s * 0.07f, s * 0.14f, s * 0.14f);
                    break;
                case IconKind.Volume:
                    using (var p = new GraphicsPath())
                    {
                        p.AddPolygon(new[] { new PointF(x + s * 0.10f, y + s * 0.38f), new PointF(x + s * 0.28f, y + s * 0.38f), new PointF(x + s * 0.50f, y + s * 0.18f),
                                             new PointF(x + s * 0.50f, y + s * 0.82f), new PointF(x + s * 0.28f, y + s * 0.62f), new PointF(x + s * 0.10f, y + s * 0.62f) });
                        g.FillPath(b, p);
                    }
                    g.DrawArc(pen, x + s * 0.40f, y + s * 0.30f, s * 0.28f, s * 0.40f, -55, 110);
                    g.DrawArc(pen, x + s * 0.40f, y + s * 0.14f, s * 0.46f, s * 0.72f, -55, 110);
                    break;
                case IconKind.Reset:
                    g.DrawArc(pen, x + s * 0.16f, y + s * 0.16f, s * 0.68f, s * 0.68f, -60, 300);
                    using (var p = new GraphicsPath())
                    {
                        p.AddPolygon(new[] { new PointF(x + s * 0.62f, y + s * 0.06f), new PointF(x + s * 0.86f, y + s * 0.20f), new PointF(x + s * 0.62f, y + s * 0.34f) });
                        g.FillPath(b, p);
                    }
                    break;
            }
            g.SmoothingMode = old;
        }

        private static void DrawNote(Graphics g, Brush b, Pen pen, float x, float y, float s)
        {
            // two eighth notes joined by a beam
            g.FillEllipse(b, x + s * 0.08f, y + s * 0.62f, s * 0.30f, s * 0.24f);
            g.FillEllipse(b, x + s * 0.56f, y + s * 0.54f, s * 0.30f, s * 0.24f);
            using var stem = new Pen(((SolidBrush)b).Color, s * 0.08f);
            g.DrawLine(stem, x + s * 0.34f, y + s * 0.74f, x + s * 0.34f, y + s * 0.20f);
            g.DrawLine(stem, x + s * 0.82f, y + s * 0.66f, x + s * 0.82f, y + s * 0.12f);
            using var beam = new GraphicsPath();
            beam.AddPolygon(new[] { new PointF(x + s * 0.30f, y + s * 0.16f), new PointF(x + s * 0.86f, y + s * 0.06f),
                                    new PointF(x + s * 0.86f, y + s * 0.20f), new PointF(x + s * 0.30f, y + s * 0.30f) });
            g.FillPath(b, beam);
        }

        /// <summary>
        /// Default cover (song without its own picture): soft diagonal gradient in the theme colours
        /// with a large, quiet music note and a few concentric rings.
        /// </summary>
        public static Bitmap DefaultCover(int size, Theme t, bool rounded = false)
        {
            var bmp = new Bitmap(size, size);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(rounded ? t.Card : t.Card);
            var r = new RectangleF(0, 0, size, size);
            Color a = Theme.Mix(t.BgTop, t.Accent, 0.35f);
            Color c = Theme.Mix(t.BgBottom, t.Card, 0.5f);
            using var grad = new LinearGradientBrush(r, a, c, 45f);
            using (var path = RoundRect(r, rounded ? size * 0.14f : 0))
                g.FillPath(grad, path);

            // rings (like a record), very subtle
            using (var ring = new Pen(Color.FromArgb(28, 255, 255, 255), Math.Max(1f, size / 120f)))
                for (int i = 1; i <= 4; i++)
                {
                    float d = size * (0.30f + 0.16f * i);
                    g.DrawEllipse(ring, size * 0.62f - d / 2, size * 0.66f - d / 2, d, d);
                }

            using var nb = new SolidBrush(Color.FromArgb(225, 255, 255, 255));
            using var np = new Pen(nb.Color, 1);
            float ns = size * 0.42f;
            DrawNote(g, nb, np, (size - ns) / 2f, (size - ns) / 2f, ns);
            return bmp;
        }
    }
}
