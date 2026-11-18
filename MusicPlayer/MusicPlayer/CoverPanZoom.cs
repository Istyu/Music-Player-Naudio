using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace MusicPlayer
{
    /// <summary>
    /// "Ken Burns" effect for the album cover: a slight zoom, then the view slowly wanders
    /// over the picture on both axes along a smooth random path (like in many music videos).
    /// Only runs when the song has its own cover; otherwise the PictureBox shows its InitialImage.
    /// </summary>
    public sealed class CoverPanZoom : IDisposable
    {
        // ---- tuning ----
        private const float MinZoom = 1.15f;          // how much it is zoomed in (1 = whole picture)
        private const float MaxZoom = 1.40f;
        private const double SegmentSeconds = 7.0;    // time between two random way-points
        private const double IntroSeconds = 2.0;      // initial zoom-in from the whole picture
        private const int FrameIntervalMs = 15;       // ~60 fps

        private readonly PictureBox box;
        private readonly System.Windows.Forms.Timer timer;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Random rnd = new Random();
        private readonly ImageAttributes edgeMode = new ImageAttributes();

        private Color originalBackColor;
        private Bitmap? art;              // pre-scaled cover
        private SizeF baseView;           // largest box-shaped rectangle inside the picture (zoom = 1)

        // Catmull-Rom way-points: (u, v, zoom); u, v = 0..1 position of the view inside the picture
        private readonly (float u, float v, float z)[] pts = new (float, float, float)[4];
        private double segmentStart;      // clock time where the current segment (pts[1] -> pts[2]) began

        public CoverPanZoom(PictureBox box)
        {
            this.box = box;
            originalBackColor = box.BackColor;
            edgeMode.SetWrapMode(WrapMode.TileFlipXY); // no dark fringe at the picture edges
            box.Paint += Box_Paint;
            timer = new System.Windows.Forms.Timer { Interval = FrameIntervalMs };
            timer.Tick += (s, e) => Tick();
        }

        /// <summary>True while a real cover is animated.</summary>
        public bool IsActive => art != null;

        /// <summary>Background of the box (theme colour); also shown at the edges of non-square covers.</summary>
        public void SetBackColor(Color c)
        {
            originalBackColor = c;
            box.BackColor = c;
            box.Invalidate();
        }

        /// <summary>
        /// Show and animate this cover. The image is copied (pre-scaled), the caller may dispose it.
        /// </summary>
        public void SetCover(Image cover)
        {
            var scaled = PreScale(cover);
            Stop();
            art = scaled;

            // the largest rectangle with the box's aspect ratio that fits in the picture
            float boxAspect = (float)box.Width / Math.Max(1, box.Height);
            float w = art.Width, h = art.Width / boxAspect;
            if (h > art.Height) { h = art.Height; w = h * boxAspect; }
            baseView = new SizeF(w, h);

            // random path: start with zero speed (pts[0] == pts[1])
            pts[1] = RandomPoint(0.5f, 0.5f);
            pts[0] = pts[1];
            pts[2] = RandomPoint(pts[1].u, pts[1].v);
            pts[3] = RandomPoint(pts[2].u, pts[2].v);
            segmentStart = 0;

            box.Image = null;          // we paint the picture ourselves
            // The cover fills the whole box: an opaque background avoids repainting the form's
            // background behind the (transparent) PictureBox 60 times a second.
            box.BackColor = originalBackColor;
            clock.Restart();
            timer.Start();
            box.Invalidate();
        }

        /// <summary>No own cover: stop the animation and show the default picture.</summary>
        public void ShowDefault()
        {
            Stop();
            box.BackColor = originalBackColor;
            box.Image = box.InitialImage;
        }

        private void Stop()
        {
            timer.Stop();
            clock.Reset();
            art?.Dispose();
            art = null;
        }

        public void Dispose()
        {
            Stop();
            timer.Dispose();
            edgeMode.Dispose();
            box.Paint -= Box_Paint;
        }

        // ---------------------------------------------------------------

        private void Tick()
        {
            if (art == null)
                return;
            var form = box.FindForm();
            if (form == null || form.WindowState == FormWindowState.Minimized || !box.Visible)
                return; // nothing to see: save CPU (the clock keeps running, so it continues smoothly)
            box.Invalidate();
        }

        /// <summary>Random way-point, reasonably far from the previous one so the motion is visible.</summary>
        private (float u, float v, float z) RandomPoint(float prevU, float prevV)
        {
            float u = 0, v = 0;
            for (int i = 0; i < 10; i++)
            {
                u = (float)rnd.NextDouble();
                v = (float)rnd.NextDouble();
                float du = u - prevU, dv = v - prevV;
                if (du * du + dv * dv > 0.35f * 0.35f)
                    break;
            }
            float z = MinZoom + (float)rnd.NextDouble() * (MaxZoom - MinZoom);
            return (u, v, z);
        }

        private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
        }

        /// <summary>Current view rectangle inside the pre-scaled picture.</summary>
        private RectangleF CurrentView()
        {
            double now = clock.Elapsed.TotalSeconds;

            // advance to the next segment(s) when needed
            while (now - segmentStart >= SegmentSeconds)
            {
                segmentStart += SegmentSeconds;
                pts[0] = pts[1];
                pts[1] = pts[2];
                pts[2] = pts[3];
                pts[3] = RandomPoint(pts[2].u, pts[2].v);
            }

            float t = (float)((now - segmentStart) / SegmentSeconds);
            float u = Math.Clamp(CatmullRom(pts[0].u, pts[1].u, pts[2].u, pts[3].u, t), 0f, 1f);
            float v = Math.Clamp(CatmullRom(pts[0].v, pts[1].v, pts[2].v, pts[3].v, t), 0f, 1f);
            float z = Math.Clamp(CatmullRom(pts[0].z, pts[1].z, pts[2].z, pts[3].z, t), 1f, MaxZoom);

            // intro: zoom in from the whole (centred) picture
            if (now < IntroSeconds)
            {
                float k = (float)(now / IntroSeconds);
                k = k * k * (3 - 2 * k); // smoothstep
                u = 0.5f + (u - 0.5f) * k;
                v = 0.5f + (v - 0.5f) * k;
                z = 1f + (z - 1f) * k;
            }

            float vw = baseView.Width / z, vh = baseView.Height / z;
            float x = u * (art!.Width - vw);
            float y = v * (art.Height - vh);
            return new RectangleF(x, y, vw, vh);
        }

        private void Box_Paint(object? sender, PaintEventArgs e)
        {
            if (art == null)
                return;

            RectangleF src = CurrentView();
            var g = e.Graphics;
            g.InterpolationMode = InterpolationMode.Bilinear; // the picture is pre-scaled, so this is enough
            g.PixelOffsetMode = PixelOffsetMode.HighQuality; // sub-pixel accurate: no 1-pixel jumps
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.DrawImage(art, new Rectangle(0, 0, box.Width, box.Height),
                        src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, edgeMode);
        }

        /// <summary>
        /// Scale the cover once so that even at the strongest zoom there are about as many
        /// picture pixels as screen pixels (sharp, but cheap to draw 60 times a second).
        /// </summary>
        private Bitmap PreScale(Image src)
        {
            float boxAspect = (float)box.Width / Math.Max(1, box.Height);
            // width of the zoom-1 view inside the source picture
            float viewW = Math.Min(src.Width, src.Height * boxAspect);
            float wantViewW = box.Width * MaxZoom;
            float scale = wantViewW / viewW;

            int w = Math.Max(1, (int)Math.Round(src.Width * scale));
            int h = Math.Max(1, (int)Math.Round(src.Height * scale));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb); // fastest format for GDI+ drawing
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using var attr = new ImageAttributes();
                attr.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
            }
            return bmp;
        }
    }
}
