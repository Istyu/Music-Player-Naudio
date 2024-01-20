namespace MusicPlayer
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components=new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            label1=new Label();
            timer1=new System.Windows.Forms.Timer(components);
            openAudio=new Button();
            Play=new Button();
            pauseButton=new Button();
            exit=new Button();
            band1CustomBar=new CustomScrollbar();
            hz31=new Label();
            band2CustomBar=new CustomScrollbar();
            hz62=new Label();
            band3CustomBar=new CustomScrollbar();
            hz125=new Label();
            band4CustomBar=new CustomScrollbar();
            hz250=new Label();
            band5CustomBar=new CustomScrollbar();
            hz500=new Label();
            band6CustomBar=new CustomScrollbar();
            hz1K=new Label();
            band7CustomBar=new CustomScrollbar();
            hz2K=new Label();
            band8CustomBar=new CustomScrollbar();
            hz4K=new Label();
            band9CustomBar=new CustomScrollbar();
            hz8K=new Label();
            band10CustomBar=new CustomScrollbar();
            hz16K=new Label();
            Stop=new Button();
            customPBarPos=new CustomProgressBar();
            coverBox=new PictureBox();
            button1=new Button();
            musicList=new ListView();
            columnHeader1=new ColumnHeader();
            columnHeader2=new ColumnHeader();
            columnHeader3=new ColumnHeader();
            volumeSlider1=new NAudio.Gui.VolumeSlider();
            volumeMeter1=new NAudio.Gui.VolumeMeter();
            volumeMeter2=new NAudio.Gui.VolumeMeter();
            deletePlaylist=new Button();
            customHorzScrollBar1=new CustomHorzScrollBar();
            horzRollingLabel=new Label();
            prevSongButton=new Button();
            nextSongButton=new Button();
            artistBox=new TextBox();
            titleBox=new TextBox();
            getLyricsBtn=new Button();
            moveSongFWDbtn=new Button();
            moveSongBWDbtn=new Button();
            hintLbl=new Label();
            button2=new Button();
            ((System.ComponentModel.ISupportInitialize)coverBox).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize=true;
            label1.BackColor=Color.Transparent;
            label1.Font=new Font("Times New Roman", 12F, FontStyle.Regular, GraphicsUnit.Point);
            label1.ForeColor=SystemColors.ControlLightLight;
            label1.Location=new Point(24, 449);
            label1.Margin=new Padding(4, 0, 4, 0);
            label1.Name="label1";
            label1.Size=new Size(87, 19);
            label1.TabIndex=2;
            label1.Text="Master Peak";
            // 
            // timer1
            // 
            timer1.Enabled=true;
            timer1.Interval=10;
            timer1.Tick+=timer1_Tick;
            // 
            // openAudio
            // 
            openAudio.BackColor=SystemColors.ActiveBorder;
            openAudio.BackgroundImage=(Image)resources.GetObject("openAudio.BackgroundImage");
            openAudio.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            openAudio.ForeColor=SystemColors.ActiveCaptionText;
            openAudio.Location=new Point(678, 536);
            openAudio.Margin=new Padding(4);
            openAudio.Name="openAudio";
            openAudio.Size=new Size(46, 44);
            openAudio.TabIndex=3;
            openAudio.UseVisualStyleBackColor=false;
            openAudio.Click+=openAudio_Click;
            openAudio.MouseEnter+=openAudio_MouseEnter;
            openAudio.MouseLeave+=openAudio_MouseLeave;
            // 
            // Play
            // 
            Play.BackColor=SystemColors.ActiveBorder;
            Play.BackgroundImage=(Image)resources.GetObject("Play.BackgroundImage");
            Play.Enabled=false;
            Play.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            Play.ForeColor=SystemColors.ActiveCaptionText;
            Play.Location=new Point(570, 533);
            Play.Margin=new Padding(4);
            Play.Name="Play";
            Play.Size=new Size(46, 48);
            Play.TabIndex=4;
            Play.UseVisualStyleBackColor=false;
            Play.Click+=Play_Click;
            Play.MouseEnter+=Play_MouseEnter;
            Play.MouseLeave+=Play_MouseLeave;
            // 
            // pauseButton
            // 
            pauseButton.BackColor=SystemColors.ActiveBorder;
            pauseButton.BackgroundImage=(Image)resources.GetObject("pauseButton.BackgroundImage");
            pauseButton.Enabled=false;
            pauseButton.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            pauseButton.ForeColor=SystemColors.ActiveCaptionText;
            pauseButton.Location=new Point(570, 533);
            pauseButton.Margin=new Padding(4);
            pauseButton.Name="pauseButton";
            pauseButton.Size=new Size(46, 47);
            pauseButton.TabIndex=5;
            pauseButton.UseVisualStyleBackColor=false;
            pauseButton.Visible=false;
            pauseButton.Click+=pauseButton_Click;
            pauseButton.MouseEnter+=pauseButton_MouseEnter;
            pauseButton.MouseLeave+=pauseButton_MouseLeave;
            // 
            // exit
            // 
            exit.BackColor=SystemColors.ControlDark;
            exit.BackgroundImage=(Image)resources.GetObject("exit.BackgroundImage");
            exit.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            exit.ForeColor=SystemColors.ActiveCaptionText;
            exit.Location=new Point(784, 537);
            exit.Margin=new Padding(4);
            exit.Name="exit";
            exit.Size=new Size(46, 44);
            exit.TabIndex=6;
            exit.UseVisualStyleBackColor=false;
            exit.Click+=exit_Click;
            exit.MouseEnter+=exit_MouseEnter;
            exit.MouseLeave+=exit_MouseLeave;
            // 
            // band1CustomBar
            // 
            band1CustomBar.BandW=1F;
            band1CustomBar.BandWmax=10F;
            band1CustomBar.BandWmin=1F;
            band1CustomBar.Location=new Point(667, 18);
            band1CustomBar.Maximum=10;
            band1CustomBar.Minimum=-10;
            band1CustomBar.Name="band1CustomBar";
            band1CustomBar.Size=new Size(46, 307);
            band1CustomBar.TabIndex=47;
            band1CustomBar.Text="band1CustomBar";
            band1CustomBar.Value=0;
            band1CustomBar.ValueChanged+=band1CustomBar_ValueChanged;
            // 
            // hz31
            // 
            hz31.AutoSize=true;
            hz31.BackColor=Color.Transparent;
            hz31.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz31.ForeColor=SystemColors.ControlLightLight;
            hz31.Location=new Point(667, 342);
            hz31.Margin=new Padding(4, 0, 4, 0);
            hz31.Name="hz31";
            hz31.Size=new Size(32, 14);
            hz31.TabIndex=12;
            hz31.Text="31Hz";
            // 
            // band2CustomBar
            // 
            band2CustomBar.BandW=1F;
            band2CustomBar.BandWmax=10F;
            band2CustomBar.BandWmin=1F;
            band2CustomBar.Location=new Point(717, 18);
            band2CustomBar.Maximum=10;
            band2CustomBar.Minimum=-10;
            band2CustomBar.Name="band2CustomBar";
            band2CustomBar.Size=new Size(46, 307);
            band2CustomBar.TabIndex=48;
            band2CustomBar.Text="customScrollbar1";
            band2CustomBar.Value=0;
            band2CustomBar.ValueChanged+=band2CustomBar_ValueChanged;
            // 
            // hz62
            // 
            hz62.AutoSize=true;
            hz62.BackColor=Color.Transparent;
            hz62.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz62.ForeColor=SystemColors.ControlLightLight;
            hz62.Location=new Point(717, 342);
            hz62.Margin=new Padding(4, 0, 4, 0);
            hz62.Name="hz62";
            hz62.Size=new Size(32, 14);
            hz62.TabIndex=14;
            hz62.Text="62Hz";
            // 
            // band3CustomBar
            // 
            band3CustomBar.BandW=1F;
            band3CustomBar.BandWmax=10F;
            band3CustomBar.BandWmin=1F;
            band3CustomBar.Location=new Point(768, 18);
            band3CustomBar.Maximum=10;
            band3CustomBar.Minimum=-10;
            band3CustomBar.Name="band3CustomBar";
            band3CustomBar.Size=new Size(46, 307);
            band3CustomBar.TabIndex=49;
            band3CustomBar.Text="customScrollbar1";
            band3CustomBar.Value=0;
            band3CustomBar.ValueChanged+=band3CustomBar_ValueChanged;
            // 
            // hz125
            // 
            hz125.AutoSize=true;
            hz125.BackColor=Color.Transparent;
            hz125.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz125.ForeColor=SystemColors.ControlLightLight;
            hz125.Location=new Point(768, 342);
            hz125.Margin=new Padding(4, 0, 4, 0);
            hz125.Name="hz125";
            hz125.Size=new Size(38, 14);
            hz125.TabIndex=16;
            hz125.Text="125Hz";
            // 
            // band4CustomBar
            // 
            band4CustomBar.BandW=1F;
            band4CustomBar.BandWmax=10F;
            band4CustomBar.BandWmin=1F;
            band4CustomBar.Location=new Point(818, 18);
            band4CustomBar.Maximum=10;
            band4CustomBar.Minimum=-10;
            band4CustomBar.Name="band4CustomBar";
            band4CustomBar.Size=new Size(46, 307);
            band4CustomBar.TabIndex=50;
            band4CustomBar.Text="customScrollbar1";
            band4CustomBar.Value=0;
            band4CustomBar.ValueChanged+=band4CustomBar_ValueChanged;
            // 
            // hz250
            // 
            hz250.AutoSize=true;
            hz250.BackColor=Color.Transparent;
            hz250.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz250.ForeColor=SystemColors.ControlLightLight;
            hz250.Location=new Point(818, 342);
            hz250.Margin=new Padding(4, 0, 4, 0);
            hz250.Name="hz250";
            hz250.Size=new Size(38, 14);
            hz250.TabIndex=18;
            hz250.Text="250Hz";
            // 
            // band5CustomBar
            // 
            band5CustomBar.BandW=1F;
            band5CustomBar.BandWmax=10F;
            band5CustomBar.BandWmin=1F;
            band5CustomBar.Location=new Point(869, 18);
            band5CustomBar.Maximum=10;
            band5CustomBar.Minimum=-10;
            band5CustomBar.Name="band5CustomBar";
            band5CustomBar.Size=new Size(46, 307);
            band5CustomBar.TabIndex=51;
            band5CustomBar.Text="customScrollbar1";
            band5CustomBar.Value=0;
            band5CustomBar.ValueChanged+=band5CustomBar_ValueChanged;
            // 
            // hz500
            // 
            hz500.AutoSize=true;
            hz500.BackColor=Color.Transparent;
            hz500.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz500.ForeColor=SystemColors.ControlLightLight;
            hz500.Location=new Point(869, 342);
            hz500.Margin=new Padding(4, 0, 4, 0);
            hz500.Name="hz500";
            hz500.Size=new Size(38, 14);
            hz500.TabIndex=20;
            hz500.Text="500Hz";
            // 
            // band6CustomBar
            // 
            band6CustomBar.BandW=1F;
            band6CustomBar.BandWmax=10F;
            band6CustomBar.BandWmin=1F;
            band6CustomBar.Location=new Point(920, 18);
            band6CustomBar.Maximum=10;
            band6CustomBar.Minimum=-10;
            band6CustomBar.Name="band6CustomBar";
            band6CustomBar.Size=new Size(46, 307);
            band6CustomBar.TabIndex=52;
            band6CustomBar.Text="customScrollbar1";
            band6CustomBar.Value=0;
            band6CustomBar.ValueChanged+=band6CustomBar_ValueChanged;
            // 
            // hz1K
            // 
            hz1K.AutoSize=true;
            hz1K.BackColor=Color.Transparent;
            hz1K.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz1K.ForeColor=SystemColors.ControlLightLight;
            hz1K.Location=new Point(920, 342);
            hz1K.Margin=new Padding(4, 0, 4, 0);
            hz1K.Name="hz1K";
            hz1K.Size=new Size(34, 14);
            hz1K.TabIndex=22;
            hz1K.Text="1KHz";
            // 
            // band7CustomBar
            // 
            band7CustomBar.BandW=1F;
            band7CustomBar.BandWmax=10F;
            band7CustomBar.BandWmin=1F;
            band7CustomBar.Location=new Point(970, 18);
            band7CustomBar.Maximum=10;
            band7CustomBar.Minimum=-10;
            band7CustomBar.Name="band7CustomBar";
            band7CustomBar.Size=new Size(46, 307);
            band7CustomBar.TabIndex=53;
            band7CustomBar.Text="customScrollbar1";
            band7CustomBar.Value=0;
            band7CustomBar.ValueChanged+=band7CustomBar_ValueChanged;
            // 
            // hz2K
            // 
            hz2K.AutoSize=true;
            hz2K.BackColor=Color.Transparent;
            hz2K.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz2K.ForeColor=SystemColors.ControlLightLight;
            hz2K.Location=new Point(970, 342);
            hz2K.Margin=new Padding(4, 0, 4, 0);
            hz2K.Name="hz2K";
            hz2K.Size=new Size(34, 14);
            hz2K.TabIndex=24;
            hz2K.Text="2KHz";
            // 
            // band8CustomBar
            // 
            band8CustomBar.BandW=1F;
            band8CustomBar.BandWmax=10F;
            band8CustomBar.BandWmin=1F;
            band8CustomBar.Location=new Point(1019, 18);
            band8CustomBar.Maximum=10;
            band8CustomBar.Minimum=-10;
            band8CustomBar.Name="band8CustomBar";
            band8CustomBar.Size=new Size(46, 307);
            band8CustomBar.TabIndex=54;
            band8CustomBar.Text="customScrollbar1";
            band8CustomBar.Value=0;
            band8CustomBar.ValueChanged+=band8CustomBar_ValueChanged;
            // 
            // hz4K
            // 
            hz4K.AutoSize=true;
            hz4K.BackColor=Color.Transparent;
            hz4K.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz4K.ForeColor=SystemColors.ControlLightLight;
            hz4K.Location=new Point(1019, 342);
            hz4K.Margin=new Padding(4, 0, 4, 0);
            hz4K.Name="hz4K";
            hz4K.Size=new Size(34, 14);
            hz4K.TabIndex=26;
            hz4K.Text="4KHz";
            // 
            // band9CustomBar
            // 
            band9CustomBar.BandW=1F;
            band9CustomBar.BandWmax=10F;
            band9CustomBar.BandWmin=1F;
            band9CustomBar.Location=new Point(1069, 18);
            band9CustomBar.Maximum=10;
            band9CustomBar.Minimum=-10;
            band9CustomBar.Name="band9CustomBar";
            band9CustomBar.Size=new Size(46, 307);
            band9CustomBar.TabIndex=55;
            band9CustomBar.Text="customScrollbar1";
            band9CustomBar.Value=0;
            band9CustomBar.ValueChanged+=band9CustomBar_ValueChanged;
            // 
            // hz8K
            // 
            hz8K.AutoSize=true;
            hz8K.BackColor=Color.Transparent;
            hz8K.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz8K.ForeColor=SystemColors.ControlLightLight;
            hz8K.Location=new Point(1069, 342);
            hz8K.Margin=new Padding(4, 0, 4, 0);
            hz8K.Name="hz8K";
            hz8K.Size=new Size(34, 14);
            hz8K.TabIndex=28;
            hz8K.Text="8KHz";
            // 
            // band10CustomBar
            // 
            band10CustomBar.BandW=1F;
            band10CustomBar.BandWmax=10F;
            band10CustomBar.BandWmin=1F;
            band10CustomBar.Location=new Point(1119, 18);
            band10CustomBar.Maximum=10;
            band10CustomBar.Minimum=-10;
            band10CustomBar.Name="band10CustomBar";
            band10CustomBar.Size=new Size(46, 307);
            band10CustomBar.TabIndex=56;
            band10CustomBar.Text="customScrollbar1";
            band10CustomBar.Value=0;
            band10CustomBar.ValueChanged+=band10CustomBar_ValueChanged;
            // 
            // hz16K
            // 
            hz16K.AutoSize=true;
            hz16K.BackColor=Color.Transparent;
            hz16K.Font=new Font("Times New Roman", 8F, FontStyle.Regular, GraphicsUnit.Point);
            hz16K.ForeColor=SystemColors.ControlLightLight;
            hz16K.Location=new Point(1119, 342);
            hz16K.Margin=new Padding(4, 0, 4, 0);
            hz16K.Name="hz16K";
            hz16K.Size=new Size(40, 14);
            hz16K.TabIndex=30;
            hz16K.Text="16KHz";
            // 
            // Stop
            // 
            Stop.BackColor=SystemColors.ActiveBorder;
            Stop.BackgroundImage=(Image)resources.GetObject("Stop.BackgroundImage");
            Stop.Enabled=false;
            Stop.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            Stop.ForeColor=SystemColors.ActiveCaptionText;
            Stop.Location=new Point(462, 536);
            Stop.Margin=new Padding(4);
            Stop.Name="Stop";
            Stop.Size=new Size(46, 44);
            Stop.TabIndex=31;
            Stop.UseVisualStyleBackColor=false;
            Stop.Click+=Stop_Click;
            Stop.MouseEnter+=Stop_MouseEnter;
            Stop.MouseLeave+=Stop_MouseLeave;
            // 
            // customPBarPos
            // 
            customPBarPos.CurrentDuration=0;
            customPBarPos.Location=new Point(385, 607);
            customPBarPos.Maximum=100;
            customPBarPos.Minimum=0;
            customPBarPos.Name="customPBarPos";
            customPBarPos.Size=new Size(417, 22);
            customPBarPos.TabIndex=57;
            customPBarPos.Text="customProgressBar1";
            customPBarPos.TotalDuration=0;
            customPBarPos.Value=0;
            customPBarPos.ValueChanged+=customPBarPos_ValueChanged;
            customPBarPos.PositionChanged+=customPBarPos_PositionChanged;
            // 
            // coverBox
            // 
            coverBox.BackColor=Color.Transparent;
            coverBox.InitialImage=(Image)resources.GetObject("coverBox.InitialImage");
            coverBox.Location=new Point(909, 424);
            coverBox.Name="coverBox";
            coverBox.Size=new Size(250, 160);
            coverBox.TabIndex=39;
            coverBox.TabStop=false;
            // 
            // button1
            // 
            button1.BackColor=SystemColors.ActiveBorder;
            button1.BackgroundImage=(Image)resources.GetObject("button1.BackgroundImage");
            button1.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            button1.ForeColor=SystemColors.ActiveCaptionText;
            button1.Location=new Point(409, 536);
            button1.Name="button1";
            button1.Size=new Size(46, 44);
            button1.TabIndex=40;
            button1.UseVisualStyleBackColor=false;
            button1.Click+=button1_Click;
            button1.MouseEnter+=button1_MouseEnter;
            button1.MouseLeave+=button1_MouseLeave;
            // 
            // musicList
            // 
            musicList.BackColor=SystemColors.ButtonShadow;
            musicList.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2, columnHeader3 });
            musicList.Location=new Point(76, 18);
            musicList.MultiSelect=false;
            musicList.Name="musicList";
            musicList.Size=new Size(584, 340);
            musicList.TabIndex=42;
            musicList.UseCompatibleStateImageBehavior=false;
            musicList.View=View.Details;
            musicList.SelectedIndexChanged+=musicList_SelectedIndexChanged;
            // 
            // columnHeader1
            // 
            columnHeader1.Text="Name";
            columnHeader1.Width=450;
            // 
            // columnHeader2
            // 
            columnHeader2.Text="Artist";
            columnHeader2.Width=100;
            // 
            // columnHeader3
            // 
            columnHeader3.Text="Duration";
            columnHeader3.Width=80;
            // 
            // volumeSlider1
            // 
            volumeSlider1.Location=new Point(24, 604);
            volumeSlider1.Name="volumeSlider1";
            volumeSlider1.Size=new Size(208, 25);
            volumeSlider1.TabIndex=43;
            volumeSlider1.VolumeChanged+=OnVolumeSliderChanged;
            // 
            // volumeMeter1
            // 
            volumeMeter1.Amplitude=0F;
            volumeMeter1.ForeColor=Color.FromArgb(0, 192, 0);
            volumeMeter1.Location=new Point(24, 471);
            volumeMeter1.MaxDb=3F;
            volumeMeter1.MinDb=-60F;
            volumeMeter1.Name="volumeMeter1";
            volumeMeter1.Size=new Size(20, 109);
            volumeMeter1.TabIndex=44;
            volumeMeter1.Text="volumeMeter1";
            // 
            // volumeMeter2
            // 
            volumeMeter2.Amplitude=0F;
            volumeMeter2.ForeColor=Color.FromArgb(0, 192, 0);
            volumeMeter2.Location=new Point(66, 472);
            volumeMeter2.MaxDb=3F;
            volumeMeter2.MinDb=-60F;
            volumeMeter2.Name="volumeMeter2";
            volumeMeter2.Size=new Size(20, 109);
            volumeMeter2.TabIndex=45;
            volumeMeter2.Text="volumeMeter2";
            // 
            // deletePlaylist
            // 
            deletePlaylist.BackColor=SystemColors.ActiveBorder;
            deletePlaylist.BackgroundImage=(Image)resources.GetObject("deletePlaylist.BackgroundImage");
            deletePlaylist.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            deletePlaylist.ForeColor=SystemColors.ActiveCaptionText;
            deletePlaylist.Location=new Point(356, 536);
            deletePlaylist.Margin=new Padding(4);
            deletePlaylist.Name="deletePlaylist";
            deletePlaylist.Size=new Size(46, 44);
            deletePlaylist.TabIndex=46;
            deletePlaylist.UseVisualStyleBackColor=false;
            deletePlaylist.Click+=deletePlaylist_Click;
            deletePlaylist.MouseEnter+=deletePlaylist_MouseEnter;
            deletePlaylist.MouseLeave+=deletePlaylist_MouseLeave;
            // 
            // customHorzScrollBar1
            // 
            customHorzScrollBar1.Location=new Point(951, 606);
            customHorzScrollBar1.Name="customHorzScrollBar1";
            customHorzScrollBar1.Size=new Size(208, 23);
            customHorzScrollBar1.TabIndex=58;
            customHorzScrollBar1.Text="customHorzScrollBar1";
            customHorzScrollBar1.PanChanged+=customHorzScrollBar1_PanChanged;
            // 
            // horzRollingLabel
            // 
            horzRollingLabel.AutoSize=true;
            horzRollingLabel.BackColor=Color.Transparent;
            horzRollingLabel.Font=new Font("Times New Roman", 12F, FontStyle.Regular, GraphicsUnit.Point);
            horzRollingLabel.Location=new Point(1180, 378);
            horzRollingLabel.Name="horzRollingLabel";
            horzRollingLabel.Size=new Size(274, 19);
            horzRollingLabel.TabIndex=59;
            horzRollingLabel.Text="Name:    Title:    Artist:    Bitrate:    Duration:";
            // 
            // prevSongButton
            // 
            prevSongButton.BackColor=SystemColors.ActiveBorder;
            prevSongButton.BackgroundImage=(Image)resources.GetObject("prevSongButton.BackgroundImage");
            prevSongButton.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            prevSongButton.ForeColor=SystemColors.ActiveCaptionText;
            prevSongButton.Location=new Point(516, 536);
            prevSongButton.Margin=new Padding(4);
            prevSongButton.Name="prevSongButton";
            prevSongButton.Size=new Size(46, 44);
            prevSongButton.TabIndex=60;
            prevSongButton.UseVisualStyleBackColor=false;
            prevSongButton.Click+=prevSongButton_Click;
            prevSongButton.MouseEnter+=prevSongButton_MouseEnter;
            prevSongButton.MouseLeave+=prevSongButton_MouseLeave;
            // 
            // nextSongButton
            // 
            nextSongButton.BackColor=SystemColors.ActiveBorder;
            nextSongButton.BackgroundImage=(Image)resources.GetObject("nextSongButton.BackgroundImage");
            nextSongButton.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            nextSongButton.ForeColor=SystemColors.ActiveCaptionText;
            nextSongButton.Location=new Point(624, 536);
            nextSongButton.Margin=new Padding(4);
            nextSongButton.Name="nextSongButton";
            nextSongButton.Size=new Size(46, 44);
            nextSongButton.TabIndex=61;
            nextSongButton.UseVisualStyleBackColor=false;
            nextSongButton.Click+=nextSongButton_Click;
            nextSongButton.MouseEnter+=nextSongButton_MouseEnter;
            nextSongButton.MouseLeave+=nextSongButton_MouseLeave;
            // 
            // artistBox
            // 
            artistBox.Location=new Point(104, 471);
            artistBox.Name="artistBox";
            artistBox.Size=new Size(128, 26);
            artistBox.TabIndex=62;
            artistBox.Text="Type artist";
            // 
            // titleBox
            // 
            titleBox.Location=new Point(104, 503);
            titleBox.Name="titleBox";
            titleBox.Size=new Size(128, 26);
            titleBox.TabIndex=63;
            titleBox.Text="Type title";
            // 
            // getLyricsBtn
            // 
            getLyricsBtn.ForeColor=SystemColors.MenuText;
            getLyricsBtn.Location=new Point(104, 551);
            getLyricsBtn.Name="getLyricsBtn";
            getLyricsBtn.Size=new Size(128, 29);
            getLyricsBtn.TabIndex=64;
            getLyricsBtn.Text="Go to lyrics";
            getLyricsBtn.UseVisualStyleBackColor=true;
            getLyricsBtn.Click+=getLyricsBtn_Click;
            // 
            // moveSongFWDbtn
            // 
            moveSongFWDbtn.BackColor=SystemColors.ActiveBorder;
            moveSongFWDbtn.BackgroundImage=(Image)resources.GetObject("moveSongFWDbtn.BackgroundImage");
            moveSongFWDbtn.ForeColor=SystemColors.ActiveCaptionText;
            moveSongFWDbtn.Location=new Point(24, 314);
            moveSongFWDbtn.Name="moveSongFWDbtn";
            moveSongFWDbtn.Size=new Size(46, 44);
            moveSongFWDbtn.TabIndex=46;
            moveSongFWDbtn.UseVisualStyleBackColor=false;
            moveSongFWDbtn.Click+=moveSongFWDbtn_Click;
            moveSongFWDbtn.MouseEnter+=moveSongFWDbtn_MouseEnter;
            moveSongFWDbtn.MouseLeave+=moveSongFWDbtn_MouseLeave;
            // 
            // moveSongBWDbtn
            // 
            moveSongBWDbtn.BackColor=SystemColors.ActiveBorder;
            moveSongBWDbtn.BackgroundImage=(Image)resources.GetObject("moveSongBWDbtn.BackgroundImage");
            moveSongBWDbtn.ForeColor=SystemColors.ActiveCaptionText;
            moveSongBWDbtn.Location=new Point(24, 18);
            moveSongBWDbtn.Name="moveSongBWDbtn";
            moveSongBWDbtn.Size=new Size(46, 44);
            moveSongBWDbtn.TabIndex=65;
            moveSongBWDbtn.UseVisualStyleBackColor=false;
            moveSongBWDbtn.Click+=moveSongBWDbtn_Click;
            moveSongBWDbtn.MouseEnter+=moveSongBWDbtn_MouseEnter;
            moveSongBWDbtn.MouseLeave+=moveSongBWDbtn_MouseLeave;
            // 
            // hintLbl
            // 
            hintLbl.Anchor=AnchorStyles.Bottom;
            hintLbl.BackColor=Color.Transparent;
            hintLbl.ForeColor=SystemColors.Info;
            hintLbl.Location=new Point(409, 584);
            hintLbl.Name="hintLbl";
            hintLbl.Size=new Size(368, 19);
            hintLbl.TabIndex=66;
            hintLbl.TextAlign=ContentAlignment.MiddleCenter;
            // 
            // button2
            // 
            button2.BackColor=SystemColors.ActiveBorder;
            button2.BackgroundImage=(Image)resources.GetObject("button2.BackgroundImage");
            button2.Font=new Font("Times New Roman", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            button2.ForeColor=SystemColors.ActiveCaptionText;
            button2.Location=new Point(731, 536);
            button2.Name="button2";
            button2.Size=new Size(46, 44);
            button2.TabIndex=67;
            button2.UseVisualStyleBackColor=false;
            button2.Click+=button2_Click;
            button2.MouseEnter+=button2_MouseEnter;
            button2.MouseLeave+=button2_MouseLeave;
            // 
            // Form1
            // 
            AutoScaleDimensions=new SizeF(9F, 19F);
            AutoScaleMode=AutoScaleMode.Font;
            BackColor=SystemColors.GrayText;
            BackgroundImage=(Image)resources.GetObject("$this.BackgroundImage");
            BackgroundImageLayout=ImageLayout.Stretch;
            ClientSize=new Size(1184, 641);
            Controls.Add(button2);
            Controls.Add(hintLbl);
            Controls.Add(moveSongBWDbtn);
            Controls.Add(moveSongFWDbtn);
            Controls.Add(getLyricsBtn);
            Controls.Add(titleBox);
            Controls.Add(artistBox);
            Controls.Add(nextSongButton);
            Controls.Add(prevSongButton);
            Controls.Add(horzRollingLabel);
            Controls.Add(customHorzScrollBar1);
            Controls.Add(deletePlaylist);
            Controls.Add(volumeMeter2);
            Controls.Add(volumeMeter1);
            Controls.Add(volumeSlider1);
            Controls.Add(musicList);
            Controls.Add(button1);
            Controls.Add(coverBox);
            Controls.Add(customPBarPos);
            Controls.Add(Stop);
            Controls.Add(hz16K);
            Controls.Add(band10CustomBar);
            Controls.Add(hz8K);
            Controls.Add(band9CustomBar);
            Controls.Add(hz4K);
            Controls.Add(band8CustomBar);
            Controls.Add(hz2K);
            Controls.Add(band7CustomBar);
            Controls.Add(hz1K);
            Controls.Add(band6CustomBar);
            Controls.Add(hz500);
            Controls.Add(band5CustomBar);
            Controls.Add(hz250);
            Controls.Add(band4CustomBar);
            Controls.Add(hz125);
            Controls.Add(band3CustomBar);
            Controls.Add(hz62);
            Controls.Add(band2CustomBar);
            Controls.Add(hz31);
            Controls.Add(band1CustomBar);
            Controls.Add(exit);
            Controls.Add(pauseButton);
            Controls.Add(Play);
            Controls.Add(openAudio);
            Controls.Add(label1);
            Font=new Font("Times New Roman", 12F, FontStyle.Regular, GraphicsUnit.Point);
            ForeColor=SystemColors.ControlLightLight;
            Icon=(Icon)resources.GetObject("$this.Icon");
            Margin=new Padding(4);
            MaximumSize=new Size(1200, 720);
            MinimumSize=new Size(1200, 680);
            Name="Form1";
            Text="Music Player";
            ((System.ComponentModel.ISupportInitialize)coverBox).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        //private ComboBox comboBox2;
        //private ProgressBar progressBar2;
        private Label hz31;
        private System.Windows.Forms.Timer timer1;
        private Button openAudio;
        private Button Play;
        private Button pauseButton;
        private Button exit;
        private CustomScrollbar band1CustomBar;
        private CustomScrollbar band2CustomBar;
        private Label hz62;
        private CustomScrollbar band3CustomBar;
        private Label hz125;
        private CustomScrollbar band4CustomBar;
        private Label hz250;
        private CustomScrollbar band5CustomBar;
        private Label hz500;
        private CustomScrollbar band6CustomBar;
        private Label hz1K;
        private CustomScrollbar band7CustomBar;
        private Label hz2K;
        private CustomScrollbar band8CustomBar;
        private Label hz4K;
        private CustomScrollbar band9CustomBar;
        private Label hz8K;
        private CustomScrollbar band10CustomBar;
        private Label hz16K;
        private Button Stop;
        private CustomProgressBar customPBarPos;
        private PictureBox coverBox;
        private Button button1;
        private ListView musicList;
        private ColumnHeader columnHeader1;
        private ColumnHeader columnHeader2;
        private ColumnHeader columnHeader3;
        private NAudio.Gui.VolumeSlider volumeSlider1;
        private NAudio.Gui.VolumeMeter volumeMeter1;
        private NAudio.Gui.VolumeMeter volumeMeter2;
        private Button deletePlaylist;
        private CustomHorzScrollBar customHorzScrollBar1;
        private Label horzRollingLabel;
        private Button prevSongButton;
        private Button nextSongButton;
        private TextBox artistBox;
        private TextBox titleBox;
        private Button getLyricsBtn;
        private Button moveSongFWDbtn;
        private Button moveSongBWDbtn;
        private Label hintLbl;
        private Button button2;
        //private TrackBar Band1;
    }
}