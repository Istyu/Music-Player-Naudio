using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicPlayer
{
    public partial class CustomScrollbar : Control
    {
        [Category("Custom")]
        [Browsable(true)]
        [Description("Custom scrollbar")]
        [Editor(typeof(System.Windows.Forms.Design.WindowsFormsComponentEditor),typeof(System.Drawing.Design.UITypeEditor))]
        private int minimum = -10;
        private int maximum = 10;
        private int value = 0;
        private float bandW = 1.0f;
        private float bandWmin = 1.0f;
        private float bandWmax = 10.0f;
        private bool isDragging = false;

        public event EventHandler ValueChanged;

        public CustomScrollbar()
        {
            InitializeComponent();
            //customVerticalScrollBar = new CustomScrollbar();
            //this.Controls.Add(customVerticalScrollBar);
            SetStyle(ControlStyles.ResizeRedraw |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Up / down: gain (fill from the centre line), left / right: bandwidth (width of the fill)
            var t = Theme.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? t.Card);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            const int textH = 22;
            var col = new RectangleF(0.5f, textH, Width - 1.5f, Height - 2 * textH);
            using (var bg = new SolidBrush(t.Field))
            using (var p = UiIcons.RoundRect(col, 10))
                g.FillPath(bg, p);

            float midY = col.Y + col.Height / 2f;
            using (var zero = new Pen(Color.FromArgb(70, t.Text), 1f))
                g.DrawLine(zero, col.X + 6, midY, col.Right - 6, midY);

            float bwRatio = (bandW - (bandWmin - 1f)) / (bandWmax - (bandWmin - 1f));
            float fillW = Math.Max(10f, (col.Width - 8) * bwRatio);
            float valY = midY - (float)value / Math.Max(1, maximum) * (col.Height / 2f - 4);
            if (value != 0)
            {
                var fill = new RectangleF(col.X + (col.Width - fillW) / 2f, Math.Min(midY, valY), fillW, Math.Abs(valY - midY));
                using var fb = new SolidBrush(Color.FromArgb(200, t.Accent));
                using var fp = UiIcons.RoundRect(fill, 4);
                g.FillPath(fb, fp);
            }
            // handle
            using (var hb = new SolidBrush(t.Text))
            using (var hp = UiIcons.RoundRect(new RectangleF(col.X + (col.Width - fillW) / 2f, valY - 3, fillW, 6), 3))
                g.FillPath(hb, hp);

            var center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            using var small = new Font("Segoe UI", 8f);
            using var tb = new SolidBrush(t.Text);
            using var sb = new SolidBrush(t.SubText);
            g.DrawString(value > 0 ? $"+{value} dB" : $"{value} dB", font, tb, new RectangleF(0, 0, Width, textH), center);
            g.DrawString($"BW {bandW:0}", small, sb, new RectangleF(0, Height - textH, Width, textH), center);
        }

        private int CalculateThumbWidth()
        {
            double ratio = (double)(bandW - (bandWmin - 1.0f)) / (bandWmax - (bandWmin - 1.0f));
            /*double ratio;
            if( BandW > 0.0f )
                ratio = (double)bandW / (bandW - bandWmin);
            else
                ratio = (double)bandW / (bandW - bandWmax);*/
            return (int)(ratio * Width);
        }

        private int CalculateThumbXPosition( int thumbWidth )
        {
            int trackWidth = ClientRectangle.Width - thumbWidth;
            double valueRange = bandWmax - (bandWmin - 1.0f);
            double relativeValue = (bandWmin - 1.0f);
            double ratio = relativeValue / valueRange;
            int thumbPosition = (int)(ratio * trackWidth);
            return thumbPosition;
        }

        private int CalculateThumbHeight()
        {
            //double ratio = (double)(value - minimum) / (maximum - minimum);
            double ratio;
            if( Value > 0 )
                ratio = (double)value / (value - minimum);
            else
                ratio = (double)value / (value - maximum);
            return (int)(ratio * Height);
        }

        private int CalculateThumbPosition(int thumbHeight)
        {
            int trackHeight = ClientRectangle.Height - thumbHeight;
            double valueRange = maximum - minimum;
            double relativeValue = value - minimum;
            double ratio = relativeValue / valueRange;
        //    double ratio = (double)value / (maximum - minimum);
            int thumbPosition = (int)((1 - ratio) * trackHeight);
            return thumbPosition;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                HandleMouseInput(e.Y);
                HandleMouseXInput(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (isDragging && e.Button == MouseButtons.Left)
            {
                HandleMouseInput(e.Y);
                HandleMouseXInput(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (isDragging)
            {
                isDragging = false;
                HandleMouseInput(e.Y);
                HandleMouseXInput(e.X);
            }
        }

        private void HandleMouseInput(int mouseY)
        {
            // the column is between the two text rows (22 px each)
            double f = Math.Clamp((mouseY - 22.0) / Math.Max(1, Height - 44), 0, 1);
            int newValue = (int)Math.Round(f * (minimum - maximum) + maximum);
            Value = Math.Max(minimum, Math.Min(maximum, newValue));
        }
        private void HandleMouseXInput(int mouseX)
        {
            //int newBandW = (int)((double)mouseX / Width * (BandWmax - bandWmin) + bandWmin);
            //BandW = Math.Max(bandWmin, Math.Min(BandWmax, newBandW));

            int newBandW = (int)((double)mouseX / Width * (bandWmax - bandWmin) + bandWmin);
            BandW = Math.Max(bandWmin, Math.Min(bandWmax, newBandW));
        }

        public int Minimum
        {
            get { return minimum; }
            set
            {
                minimum = value;
                if (this.value < minimum)
                    this.value = minimum;
                if (this.maximum < minimum)
                    this.maximum = minimum;
                Refresh();
            }
        }

        public int Maximum
        {
            get { return maximum; }
            set
            {
                maximum = value;
                if (this.value > maximum)
                    this.value = maximum;
                if (this.minimum > maximum)
                    this.minimum = maximum;
                Refresh();
            }
        }

        public int Value
        {
            get { return value; }
            set
            {
                if (value < minimum)
                    this.value = minimum;
                else if (value > maximum)
                    this.value = maximum;
                else
                    this.value = value;

                OnValueChanged(EventArgs.Empty);
                Refresh();
            }
        }

        public float BandWmin
        {
            get { return bandWmin; }
            set
            {
                bandWmin = value;
                if (this.bandW < bandWmin)
                    this.bandW = bandWmin;
                if (this.bandWmax < bandWmin)
                    this.bandWmax = bandWmin;
                Refresh();
            }
        }

        public float BandWmax
        {
            get { return bandWmax; }
            set
            {
                bandWmax = value;
                if (this.bandW > bandWmax)
                    this.bandW = bandWmax;
                if (this.bandWmin > bandWmax)
                    this.bandWmin = bandWmax;
                Refresh();
            }
        }

        public float BandW
        {
            get { return bandW; }
            set
            {
                if (value < bandWmin)
                    this.bandW = bandWmin;
                if (value > bandWmax)
                    this.bandW = bandWmax;
                if( value != this.bandW )
                {
                    bandW = value;
                    OnValueChanged(EventArgs.Empty);
                    Refresh();
                }
            }

            /*get { return bandW; }
            set
            {
                if (value < bandWmin)
                    bandW = bandWmin;
                else if (value > bandWmax)
                    bandW = bandWmax;
                else
                    bandW = value;

                OnValueChanged(EventArgs.Empty);
                Refresh();
            }*/
        }

        protected virtual void OnValueChanged(EventArgs e)
        {
            ValueChanged?.Invoke(this, e);
        }
    }

    public class CustomHorzScrollBar : Control
    {
        private float minimum = -10.0f;
        private float maximum = 10.0f;
        private float pan = 0.0f;

        public event EventHandler PanChanged;

        public CustomHorzScrollBar()
        {
            SetStyle(ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);
        }

        [DefaultValue(0.0f)]
        public float Pan
        {
            get { return pan; }
            set
            {
                if (value < minimum)
                    value = minimum;
                if (value > maximum)
                    value = maximum;
                if (pan != value)
                {
                    pan = value;
                    if (OnPanChanged != null)
                        OnPanChanged(EventArgs.Empty);
                    //this.Invalidate();
                    Refresh();
                }
            }
        }

        protected virtual void OnPanChanged(EventArgs e)
        {
            PanChanged?.Invoke(this, e);
        }

        private const int EdgeW = 16;   // room for the "L" / "R" letters
        private RectangleF Track => new RectangleF(EdgeW, Height / 2f - 2, Width - 2 * EdgeW, 4);

        protected override void OnPaint(PaintEventArgs e)
        {
            // balance: L ----|---- R, the fill goes from the centre to the current position
            var t = Theme.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? t.Card);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var tr = Track;
            using (var b = new SolidBrush(t.Field)) using (var p = UiIcons.RoundRect(tr, 2)) g.FillPath(b, p);
            float mid = tr.X + tr.Width / 2f;
            float pos = tr.X + tr.Width * (pan - minimum) / (maximum - minimum);
            using (var b = new SolidBrush(t.Accent))
                g.FillRectangle(b, Math.Min(mid, pos), tr.Y, Math.Abs(pos - mid), tr.Height);
            using (var tick = new Pen(t.SubText, 1f)) g.DrawLine(tick, mid, tr.Y - 4, mid, tr.Bottom + 4);
            using (var b = new SolidBrush(t.Text)) g.FillEllipse(b, pos - 6, Height / 2f - 6, 12, 12);
            using var sb = new SolidBrush(t.SubText);
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            var c = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("L", font, sb, new RectangleF(0, 0, EdgeW, Height), c);
            g.DrawString("R", font, sb, new RectangleF(Width - EdgeW, 0, EdgeW, Height), c);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            Pan = 0; // double click: centre
            base.OnMouseDoubleClick(e);
        }

        private int CalculateScrollHorzBarWidth()
        {
            double ratio;
            if( Pan > 0.0f )
                ratio = (double)pan / (pan - minimum);
            else
                ratio = (double)pan / (pan - maximum);
            return (int)(ratio * Width);
        }

        private int CalculateScrollHorzBarPosition( int scrollWidth )
        {
            int trackWidth = ClientRectangle.Width - scrollWidth;
            double valueRange = maximum - minimum;
            double relativeValue = pan - minimum;
            double ratio = relativeValue / valueRange;
            int scrollPosition = (int)(ratio * trackWidth);
            return scrollPosition;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            HandleMouseInput(e.X);
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                HandleMouseInput(e.X);
            }
            base.OnMouseMove(e);
        }

        /*protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (isDragging)
            {
                isDragging = false;
                HandleMouseInput(e.X);
            }
        }*/

        private void HandleMouseInput(int mouseX)
        {
            var tr = Track;
            double f = Math.Clamp((mouseX - tr.X) / tr.Width, 0, 1);
            int newValue = (int)Math.Round(f * (maximum - minimum) + minimum);
            Pan = Math.Max(minimum, Math.Min(maximum, newValue));
        }
    }

    public class CustomProgressBar : Control
    {
        private int minimum = 0;
        private int maximum = 100;
        private int value = 0;
        private Color progressBarColor = Color.LightGreen;
        private bool isDragging = false;
        private int currentDuration;
        private int totalDuration;

        public event EventHandler ValueChanged;
        public event EventHandler PositionChanged;

        public CustomProgressBar()
        {
            SetStyle(ControlStyles.ResizeRedraw |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);
        }

        public int Minimum
        {
            get { return minimum; }
            set
            {
                minimum = value;
                if (this.value < minimum)
                    this.value = minimum;
                if (this.maximum < minimum)
                    this.maximum = minimum;
                //Refresh();
                this.Invalidate();
            }
        }

        public int Maximum
        {
            get { return maximum; }
            set
            {
                maximum = value;
                if (this.value > maximum)
                    this.value = maximum;
                if (this.minimum > maximum)
                    this.minimum = maximum;
                //Refresh();
                this.Invalidate();
            }
        }

        public int Value
        {
            get { return value; }
            set
            {
                if (value < minimum)
                    this.value = minimum;
                else if (value > maximum)
                    this.value = maximum;
                else
                    this.value = value;
                if( OnValueChanged != null )
                    this.OnValueChanged(EventArgs.Empty);
                //Refresh();
                this.Invalidate();
            }
        }

        public int CurrentDuration
        {
            get { return currentDuration; }
            set
            {
                currentDuration = value;
                this.Invalidate();
                //Refresh();
            }
        }

        public int TotalDuration
        {
            get { return totalDuration; }
            set
            {
                totalDuration = value;
                this.Invalidate();
                //Refresh();
            }
        }

        private const int TimeW = 50;   // elapsed time on the left, total on the right
        private bool hover;
        private RectangleF Track => new RectangleF(TimeW, Height / 2f - 2.5f, Width - 2 * TimeW, 5);

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = Theme.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? t.Card);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var tr = Track;
            double ratio = maximum > minimum ? (double)(value - minimum) / (maximum - minimum) : 0;
            float fx = tr.X + (float)(tr.Width * Math.Clamp(ratio, 0, 1));
            using (var b = new SolidBrush(t.Field)) using (var p = UiIcons.RoundRect(tr, 2.5f)) g.FillPath(b, p);
            if (fx > tr.X + 1)
                using (var b = new SolidBrush(t.Accent)) using (var p = UiIcons.RoundRect(new RectangleF(tr.X, tr.Y, fx - tr.X, tr.Height), 2.5f)) g.FillPath(b, p);
            float knob = hover || isDragging ? 16 : 12;
            using (var b = new SolidBrush(hover || isDragging ? t.Text : t.Accent))
                g.FillEllipse(b, fx - knob / 2, Height / 2f - knob / 2, knob, knob);

            using var font = new Font("Segoe UI", 9f);
            using var sb = new SolidBrush(t.SubText);
            g.DrawString(FormatTime(currentDuration), font, sb, new RectangleF(0, 0, TimeW - 6, Height),
                         new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
            g.DrawString(FormatTime(totalDuration), font, sb, new RectangleF(Width - TimeW + 6, 0, TimeW - 6, Height),
                         new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center });
        }

        private string FormatTime(int seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            return $"{(int)timeSpan.TotalMinutes:D2}:{timeSpan.Seconds:D2}";
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                isDragging = true;
                HandleMouseInput(e.X);
                //OnPositionChanged(EventArgs.Empty);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (isDragging && e.Button == MouseButtons.Left)
            {
                // While dragging only the bar and the time display follow the mouse,
                // the actual seek happens once, on release (no choppy audio while dragging).
                HandleMouseInput(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (isDragging)
            {
                HandleMouseInput(e.X);
                isDragging = false;
                OnPositionChanged(EventArgs.Empty); // seek (also for a simple click)
            }
        }

        /// <summary>True while the user drags the position with the mouse.</summary>
        public bool IsDragging => isDragging;

        /// <summary>The position selected with the mouse, with fractions (not rounded to whole seconds).</summary>
        public double ExactValue { get; private set; }

        private void HandleMouseInput(int mouseX)
        {
            var tr = Track;
            double exact = Math.Clamp((mouseX - tr.X) / tr.Width, 0, 1) * (maximum - minimum) + minimum;
            ExactValue = Math.Max(minimum, Math.Min(maximum, exact));
            Value = (int)ExactValue;
            CurrentDuration = Value; // show the selected time while dragging
        }

        protected virtual void OnPositionChanged(EventArgs e)
        {
            PositionChanged?.Invoke(this, e);
        }

        protected virtual void OnValueChanged(EventArgs e)
        {
            ValueChanged?.Invoke(this, e);
        }
    }
}
