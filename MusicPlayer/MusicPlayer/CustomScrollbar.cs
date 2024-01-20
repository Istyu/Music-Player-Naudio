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
            base.OnPaint(e);

            StringFormat format = new StringFormat();
            format.LineAlignment = StringAlignment.Center;
            format.Alignment = StringAlignment.Center;
            string barValue = String.Format("{0} Gian", Value);

            string bandwidthValue = String.Format("BandW.: \n{0}", BandW);

            int thumbHeight = CalculateThumbHeight();
            int thumbPosition = CalculateThumbPosition(thumbHeight);

            int thumbWidth = CalculateThumbWidth();
            int thumbXPosition = CalculateThumbXPosition(thumbWidth);

            //e.Graphics.FillRectangle(Brushes.LightGreen, 1, thumbPosition, this.Width - 2, thumbHeight);
            //Color thumbColor = Color.FromArgb(128, Color.LightGreen);
            Color thumbColor = Color.LightGreen;
            using (SolidBrush brush = new SolidBrush(thumbColor))
            {
                //e.Graphics.FillRectangle(brush, 1, thumbPosition, this.Width - 2, thumbHeight);
                //e.Graphics.FillRectangle(brush, thumbXPosition, thumbPosition, thumbWidth, thumbHeight);

                e.Graphics.FillRectangle(brush, thumbXPosition, thumbPosition, thumbWidth, thumbHeight);
            }

            e.Graphics.DrawRectangle(Pens.Black, 0, 0, this.Width - 1, this.Height - 1);
            //e.Graphics.DrawString(barValue, this.Font, Brushes.Black, this.ClientRectangle, format);
            e.Graphics.DrawString(barValue, new Font(this.Font.FontFamily, 7), Brushes.Black, this.ClientRectangle, format);

            //e.Graphics.DrawRectangle(Pens.Black, 0, 0, this.Width - 1, this.Height - 1);
            e.Graphics.DrawString(bandwidthValue, new Font(this.Font.FontFamily, 7), Brushes.Black, 20, 180, format);
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
            int newValue = (int)((double)mouseY / Height * (minimum - maximum) + maximum);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            StringFormat format = new StringFormat();
            format.LineAlignment = StringAlignment.Center;
            format.Alignment = StringAlignment.Center;
            string panText = String.Format("{0} pan", Pan);

            int scrollHorzBarWidth = CalculateScrollHorzBarWidth();
            int scrollPosition = CalculateScrollHorzBarPosition(scrollHorzBarWidth);

            Color scrollColor = Color.LightGreen;

            e.Graphics.FillRectangle(Brushes.Gray, 0, 0, Width, Height); // Teljes háttér
            using (SolidBrush brush = new SolidBrush(scrollColor))
            {
                e.Graphics.FillRectangle(brush, scrollPosition, 0, scrollHorzBarWidth, Height); // Progress bar
            }

            e.Graphics.DrawString(panText, Font, Brushes.Black, this.ClientRectangle, format);
            //e.Graphics.DrawString(panText, new Font(this.Font.FontFamily, 7), Brushes.Black, this.ClientRectangle, format);
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
            int newValue = (int)((double)mouseX / Width * (maximum - minimum) + minimum);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int progressBarWidth = CalculateProgressBarWidth();

            e.Graphics.FillRectangle(Brushes.Gray, 0, 0, Width, Height); // Teljes háttér
            using (SolidBrush brush = new SolidBrush(progressBarColor))
            {
                e.Graphics.FillRectangle(brush, 0, 0, progressBarWidth, Height); // Progress bar
            }

            //string progressText = $"{FormatTime(currentDuration)} / {FormatTime(totalDuration)}";
            //string currDur = String.Format("{0:D2}:{0:D2} / {0:D2}:{0:D2}", currentDuration, totalDuration );
        //    string currDur = String.Format("{0:D2}:{0:D2}", currentDuration );
            string currDur = $"{FormatTime(currentDuration)} /";
            string totDur = $"  {FormatTime(totalDuration)}";
            StringFormat format = new StringFormat();
            format.LineAlignment = StringAlignment.Center;
            format.Alignment = StringAlignment.Center;
            // Az alábbiakban megadhatja az X és Y koordinátákat a pozícionáláshoz
            float curX = 190; // példaérték, pozíció az X tengelyen
            float curY = 12; // példaérték, pozíció az Y tengelyen
            float totX = 230;
            float totY = 12;
            e.Graphics.DrawString(currDur, Font, Brushes.Black, curX, curY, format);
            e.Graphics.DrawString(totDur, Font, Brushes.Black, totX, totY, format);
        }

        private int CalculateProgressBarWidth()
        {
            double ratio = (double)(value - minimum) / (maximum - minimum);
            return (int)(ratio * Width);
        }

        private string FormatTime(int seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
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
                HandleMouseInput(e.X);
                OnPositionChanged(EventArgs.Empty);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (isDragging)
            {
                isDragging = false;
                HandleMouseInput(e.X);
            }
        }

        private void HandleMouseInput(int mouseX)
        {
            int newValue = (int)((double)mouseX / Width * (maximum - minimum) + minimum);
            Value = Math.Max(minimum, Math.Min(maximum, newValue));
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
