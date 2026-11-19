using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
//using System.Net; // Lyrics
using System.Text.RegularExpressions; // Regex
using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Mixer;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TagLib;
using System.Runtime.InteropServices;
using DiscordRPC;
using DiscordRPC.Logging;

namespace MusicPlayer
{
    public partial class Form1 : Form
    {
        private NAudio.Extras.Equalizer? equalizer;
        private NAudio.Extras.EqualizerBand[]? bands;

        //private NAudio.Wave.BlockAlignReductionStream stream = null;
        //private NAudio.Wave.DirectSoundOut output = null;
        private NAu.Wave.WaveOutEvent? output; // With this possible the sound volume controlling
        private NAudio.Wave.WaveStream? pcm;
        private List<string> songPaths = new List<string>();
        private List<string> SfileName = new List<string>();

        private MeteringSampleProvider? postVolumeMeter;
        private Action<float>? setVolumeDelegate;
        private FadeSampleProvider? fader;   // Play/Pause fade in / fade out
        private int fadeVersion = 0;         // Changes on every Play/Pause/Stop, so an old fade cannot pause a new playback

        private const int FadeInMs = 250;    // Fade-in length on Play
        private const int FadeOutMs = 200;   // Fade-out length on Pause
        private const int OutputLatencyMs = 120; // WaveOut buffer latency (was 300 ms) -> faster reaction

        public float[] eqValues;
        public int[] eqScrollValues;

        public float[] bwValues;
        public float[] bwScrollValues;

        public bool decrementVol = false;
        public bool incrementVol = false;
        public bool stopped = false;
        //public float currentVol;
        //public bool audioFileLoaded;
        public bool autoSkip = false;

        const string closeCaption = "Music Player Closing";


        private DiscordRpcClient? client; // null = Discord Rich Presence is not used (no / invalid Discord.txt)

        // Settings file next to the exe (not relative to the current working directory)
        private static readonly string EqSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "eqsettings.txt");

        private string DCtitle = "";
        private string DCartist = "";
        private DateTime now;

        private string DCappClientID;

        public Form1()
        {
            InitializeComponent();

            SetupLyricsUi();
            ApplyModernLayout(); // v2 layout: sidebar, pages, playlist with covers, themes (Form1.Modern.cs)

            eqValues = new float[10];
            eqScrollValues = new int[10];

            bwValues = new float[10];
            bwScrollValues = new float[10];

            //audioFileLoaded = false;
            //currentVol = 1.0f;

            musicList.AllowDrop = true;
            musicList.ShowItemToolTips = true; // shows the path of a missing song
            coverAnim = new CoverPanZoom(coverBox); // slow zoom + pan over the album cover
            musicList.ItemDrag += musicList_ItemDrag;     // reorder songs with drag & drop
            musicList.DragOver += musicList_DragOver;
            musicList.DragLeave += musicList_DragLeave;
            musicList.DragEnter += musicList_DragEnter;
            musicList.DragDrop += musicList_DragDrop;

            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string fileName = "eqsettings.txt";
            string filePath = Path.Combine(executablePath, fileName);

            string playList = "myPlaylist.txt";
            string listPath = Path.Combine(executablePath, playList);

            string DCID = "Discord.txt";
            string DCpath = Path.Combine(executablePath, DCID);

            if (!System.IO.File.Exists(filePath))
                eqSettingsFirstTime();
            LoadEqSettings(); // once at startup; afterwards the bars hold the current EQ (also for every new song)

            if (System.IO.File.Exists(listPath))
                LoadSavedSongs();


            InitDiscord(DCpath);

            // Taskbar progress (green = playing, yellow = paused) + play/pause overlay icon.
            // Created when the window (and so its taskbar button) exists.
            Shown += (s, e) => taskbarStatus = new TaskbarStatus(Handle);
        }

        private TaskbarStatus? taskbarStatus;
        private CoverPanZoom coverAnim;
        private bool listShowsPlaying;   // the playlist row of the current song shows the "playing" marker

        /// <summary>Called from the timer: shows the playback state and position on the taskbar button.</summary>
        private void UpdateTaskbarStatus()
        {
            if (taskbarStatus == null)
                return;

            var state = TaskbarStatus.State.None;
            double progress = 0;

            if (output != null && pcm != null && !stopped)
            {
                var ps = output.PlaybackState;
                if (ps == NAudio.Wave.PlaybackState.Playing && !decrementVol)
                    state = TaskbarStatus.State.Playing;
                else if (ps == NAudio.Wave.PlaybackState.Paused ||
                         (ps == NAudio.Wave.PlaybackState.Playing && decrementVol)) // fading out for pause
                    state = TaskbarStatus.State.Paused;

                double total = pcm.TotalTime.TotalSeconds;
                if (total > 0)
                    progress = pcm.CurrentTime.TotalSeconds / total;
            }

            taskbarStatus.Update(state, progress);
        }

        /// <summary>
        /// Discord Rich Presence is optional: if Discord.txt is missing, has no ID, or the client
        /// cannot be created, the player simply runs without it (client stays null).
        /// </summary>
        private void InitDiscord(string DCpath)
        {
            try
            {
                if (!System.IO.File.Exists(DCpath))
                    return; // no Discord.txt -> no Discord status, not an error

                foreach (string line in System.IO.File.ReadAllLines(DCpath))
                {
                    if (line.StartsWith("DiscordClientID:"))
                        DCappClientID = line.Substring(16).Trim();
                }

                if (string.IsNullOrWhiteSpace(DCappClientID))
                    return; // file exists, but there is no "DiscordClientID:" line in it

                /*
                Create a Discord client
                NOTE:   If you are using Unity3D, you must use the full constructor and define
                         the pipe connection.
                */
                var dc = new DiscordRpcClient(DCappClientID);

                //Set the logger
                dc.Logger = new ConsoleLogger() { Level = LogLevel.Warning };

                //Subscribe to events
                dc.OnReady += (sender, e) =>
                {
                    Console.WriteLine("Received Ready from user {0}", e.User.Username);
                };

                dc.OnPresenceUpdate += (sender, e) =>
                {
                    Console.WriteLine("Received Update! {0}", e.Presence);
                };

                //Connect to the RPC
                dc.Initialize();

                //Set the rich presence
                //Call this as many times as you want and anywhere in your code.
                dc.SetPresence(new RichPresence()
                {
                    Details = "Song",
                    State = "No song playing",
                    Timestamps = Timestamps.Now,
                    Type = ActivityType.Listening,
                    Assets = new Assets()
                    {
                        LargeImageKey = "musical_note",
                        LargeImageText = "Music Player",
                        SmallImageKey = "musical_note",
                        SmallImageText = "Enjoying great tunes"
                    }
                });

                client = dc;
            }
            catch (Exception ex)
            {
                client = null;
                Console.WriteLine("Discord Rich Presence disabled: " + ex.Message);
            }
        }

        private void exit_Click(object sender, EventArgs e)
        {
            this.Close(); // Notify the OnFormClosing() event handler
        }

        // Unsaved changes: the closing question is asked only when one of these is true.
        // (The playlist is saved automatically after every change, so it is "dirty" only if that save failed.)
        private bool eqDirty = false;
        private bool playlistDirty = false;

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            bool close = true;

            // Windows shutdown / logoff, Task Manager: never block it with a question
            bool systemClose = e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing;

            if (!systemClose && (eqDirty || playlistDirty))
            {
                var what = new List<string>();
                if (eqDirty) what.Add("•  EQ settings");
                if (playlistDirty) what.Add("•  playlist");
                var result = ThemedDialog.Show(this, 
                    "There are unsaved changes:\n\n" + string.Join("\n", what) + "\n\nSave them before closing?",
                    closeCaption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                    close = false;
                else if (result == DialogResult.Yes)
                {
                    // a failed save keeps the window open (the error was shown), nothing is lost
                    if (eqDirty && !SaveEqSettings()) close = false;
                    if (close && playlistDirty && !SaveSongsToFile()) close = false;
                }
            }

            if (!close)
                e.Cancel = true;
            if( close )
            {
                client?.Dispose();
                taskbarStatus?.Dispose();
                taskbarStatus = null;
                try
                {
                    DisposeWave(true);
                    Environment.Exit(0); // Application.Exit(); if there are more forms
                }
                catch (Exception ex)
                {
                    ThemedDialog.Show(this, $"An error occurred while exiting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            base.OnFormClosing(e);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            client?.Invoke(); // can be null if the timer ticks before the constructor finished (e.g. during a MessageBox)
            UpdateTaskbarStatus();
            if (pcm != null && lyricsView != null)
                lyricsView.UpdatePosition(GetAudiblePosition(), pcm.TotalTime);
            UpdatePlaylistHeaderIfNeeded();
            bool playingNow = output != null && !stopped && output.PlaybackState == NAudio.Wave.PlaybackState.Playing;
            if (playingNow != listShowsPlaying)
            {
                listShowsPlaying = playingNow;
                if (currentSongIndex >= 0 && currentSongIndex < musicList.Items.Count)
                    musicList.Invalidate(musicList.Items[currentSongIndex].Bounds); // "playing" marker of the current row
            }
            if (output != null)
            {
                horzRollingLabel.Left -= 2;
                //horzRollingLabel.Text = String.Format( "{0}", horzRollingLabel.Left );
                if (horzRollingLabel.Right < 0)
                    horzRollingLabel.Left = 1169;
            }

            if (output != null && !stopped && output.PlaybackState == NAudio.Wave.PlaybackState.Playing &&
                !customPBarPos.IsDragging) // don't pull the bar back while the user drags it
            {
                //customPBarPos.Value = (int)this.pcm.CurrentTime.TotalMinutes;
                //    customPBarPos.CurrentDuration = (int)this.pcm.CurrentTime.TotalMinutes;
                //customPBarPos.CurrentDuration++; // test
                double totalSeconds = this.pcm.CurrentTime.TotalSeconds;
                int seconds = (int)totalSeconds;
                customPBarPos.Value = seconds;
                customPBarPos.CurrentDuration = seconds;

                /*double totalMinutes = this.pcm.CurrentTime.TotalMinutes;
                int minutes = (int)totalMinutes;

                TimeSpan durMinutes = this.pcm.TotalTime;

                client.SetPresence(new RichPresence()
                {
                    Details = DCtitle,
                    State = DCartist,
                    Type = ActivityType.Listening,
                    Assets = new Assets()
                    {
                        LargeImageKey = "musical_note",
                        LargeImageText = String.Format("{0:00}:{1:00} / {2:00}:{3:00}", (int)durMinutes.TotalMinutes, (int)durMinutes.TotalSeconds % 60, minutes, seconds % 60),
                        SmallImageKey = "musical_note",
                        SmallImageText = "Enjoying great tunes"
                    }
                });*/
            }

            if( output != null && output.PlaybackState != NAudio.Wave.PlaybackState.Playing )
            {
                volumeMeter1.Amplitude = 0.0f;
                volumeMeter2.Amplitude = 0.0f;
            }

            // End of the song: the source has no more samples AND the device has played everything that was
            // queued (it stops by itself). Not "CurrentTime >= TotalTime": for resampled (converted) files
            // TotalTime is only an estimate and may never be reached; and the queued audio is not cut off.
            if (output != null && fader != null && !stopped && fader.SourceEnded &&
                output.PlaybackState == NAudio.Wave.PlaybackState.Stopped)
            {
                OnSongEnded();
            }
        }

        private bool SaveSongsToFile()
        {
            // Create "myPlaylist.txt" file in the application folder
            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string playList = "myPlaylist.txt";
            string listPath = Path.Combine(executablePath, playList);

            // (the file is created when it does not exist yet: first start / new folder)
            try
            {
                // StreamWriter is used in a using block to make sure it closes the file.
                using (StreamWriter sw = new StreamWriter(listPath))
                {
                    // We merge all the music into one text file with a path on each line
                    foreach (string path in songPaths)
                    {
                        sw.WriteLine(path);
                    }
                }
                playlistDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                playlistDirty = true;   // asked again when closing
                ThemedDialog.Show(this, $"Error occurred during save songs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void LoadSavedSongs()
        {
            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string playList = "myPlaylist.txt";
            string listPath = Path.Combine(executablePath, playList);
            try
            {
                var missing = new List<string>();

                // We load the saved musics from the file
                using (StreamReader sr = new StreamReader(listPath))
                {
                    while (!sr.EndOfStream)
                    {
                        string? line = sr.ReadLine()?.Trim();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        // songPaths, SfileName and the ListView must always stay in sync (same index)
                        songPaths.Add(line);
                        SfileName.Add(System.IO.Path.GetFileName(line));
                        string songTitle = System.IO.Path.GetFileNameWithoutExtension(line);

                        if (!System.IO.File.Exists(line))
                        {
                            // Moved or deleted: keep it in the list (e.g. an unplugged USB drive), but mark it
                            musicList.Items.Add(new ListViewItem(new string[] { songTitle, "", MissingText }));
                            MarkSongMissing(musicList.Items.Count - 1, true);
                            missing.Add(line);
                            continue;
                        }

                        // The given music is added to the ListView
                        string artist = GetArtist(line);
                        string songDuration = GetDuration(line);
                        musicList.Items.Add(new ListViewItem(new string[] { songTitle, artist, songDuration }));
                    }
                }

                // Ask only when the window is already shown (a MessageBox in the constructor
                // lets the timer run before the form is fully initialised)
                if (missing.Count > 0)
                    Shown += (s, e) => AskRemoveMissingSongs(missing);
            }
            catch (Exception ex)
            {
                ThemedDialog.Show(this, $"Error occurred during load songs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private const string MissingText = "Missing";

        /// <summary>True if the song at this index does not exist on the disk (moved / deleted).</summary>
        private bool IsSongMissing(int index)
        {
            return index < 0 || index >= songPaths.Count || !System.IO.File.Exists(songPaths[index]);
        }

        /// <summary>Grey out a missing song in the list (or restore it when it is back).</summary>
        private void MarkSongMissing(int index, bool missing)
        {
            if (index < 0 || index >= musicList.Items.Count)
                return;

            var item = musicList.Items[index];
            item.ForeColor = missing ? Color.Gray : musicList.ForeColor;
            item.ToolTipText = missing ? "File not found: " + songPaths[index] : "";
            if (item.SubItems.Count > 2)
            {
                if (missing)
                    item.SubItems[2].Text = MissingText;
                else if (item.SubItems[2].Text == MissingText)
                    item.SubItems[2].Text = GetDuration(songPaths[index]);
            }
        }

        /// <summary>Tell the user which songs are missing and offer to remove them from the playlist.</summary>
        private void AskRemoveMissingSongs(List<string> missing)
        {
            const int maxShown = 15;
            var lines = missing.Take(maxShown).Select(p => "• " + p);
            string list = string.Join("\n", lines);
            if (missing.Count > maxShown)
                list += $"\n... and {missing.Count - maxShown} more";

            var answer = ThemedDialog.Show(this, 
                $"{missing.Count} song(s) of the playlist were not found (moved or deleted):\n\n{list}\n\n" +
                "Remove them from the playlist?\n(No = keep them greyed out, e.g. if the drive is just not connected.)",
                "Missing songs", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes)
                return;

            for (int i = songPaths.Count - 1; i >= 0; i--)
            {
                if (missing.Contains(songPaths[i]))
                {
                    songPaths.RemoveAt(i);
                    SfileName.RemoveAt(i);
                    musicList.Items.RemoveAt(i);
                }
            }
            SaveSongsToFile();
        }

        private void ClearPlaylist()
        {
            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string playList = "myPlaylist.txt";
            string listPath = Path.Combine(executablePath, playList);

            try
            {
                // Delete the contents of the file (create an empty file)
                System.IO.File.WriteAllText(listPath, string.Empty);

                // Updating ListView and internal lists
                musicList.Items.Clear();
                songPaths.Clear();
                SfileName.Clear();
                currentSongIndex = -1;
            }
            catch (Exception ex)
            {
                ThemedDialog.Show(this, $"Error occurred during playlist delete: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Index the next / previous step starts from: the loaded song, or (nothing loaded) the selected one.
        /// Does not depend on the list selection, so a lost selection cannot stop the playback.
        /// </summary>
        private int StepBaseIndex()
        {
            if (currentSongIndex >= 0)
                return currentSongIndex;
            return musicList.SelectedIndices.Count > 0 ? musicList.SelectedIndices[0] : -1;
        }

        /// <summary>Loads and plays the next existing song. Returns false if there is none (end of the playlist).</summary>
        private bool playNextSong()
        {
            int baseIndex = StepBaseIndex();
            if (baseIndex < 0)
                return false;
            int nextIndex = baseIndex + 1;
            // skip the songs that were moved / deleted
            while (nextIndex < musicList.Items.Count && IsSongMissing(nextIndex))
            {
                MarkSongMissing(nextIndex, true);
                nextIndex++;
            }
            if (nextIndex >= musicList.Items.Count)
                return false;
            return PlayIndex(nextIndex);
        }

        /// <summary>Loads and plays the previous existing song. Returns false if there is none.</summary>
        private bool playPreviousSong()
        {
            int baseIndex = StepBaseIndex();
            if (baseIndex < 0)
                return false;
            int prevIndex = Math.Min(baseIndex, musicList.Items.Count) - 1;
            // skip the songs that were moved / deleted
            while (prevIndex >= 0 && IsSongMissing(prevIndex))
            {
                MarkSongMissing(prevIndex, true);
                prevIndex--;
            }
            if (prevIndex < 0)
                return false;
            return PlayIndex(prevIndex);
        }

        private bool PlayIndex(int index)
        {
            autoSkip = true;   // the selection change below must not load the song a second time
            try
            {
                musicList.SelectedItems.Clear();
                musicList.Items[index].Selected = true;
                musicList.Items[index].EnsureVisible();
                if (selectSong(index) && output != null)
                {
                    output.Play();
                    updateDiscordRPC_Playing();
                    return true;
                }
                return false;
            }
            finally
            {
                autoSkip = false;   // never left on: it would block loading songs by clicking in the list
            }
        }

        /// <summary>The song reached its end: next song, or at the end of the playlist stop (back to the start of the song).</summary>
        private void OnSongEnded()
        {
            if (!playNextSong())
                Stop_Click(this, EventArgs.Empty);
        }

        private void nextSongButton_Click(object sender, EventArgs e)
        {
            if (output != null && pcm != null && output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                playNextSong();
        }

        private void prevSongButton_Click(object sender, EventArgs e)
        {
            if (output != null && pcm != null && output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                playPreviousSong();
        }

        // An example function that uses TagLib to extract the artist from a given file
        private string GetArtist(string filePath)
        {
            try
            {
                using (var file = TagLib.File.Create(filePath))
                    return file.Tag.FirstPerformer ?? "";
            }
            catch (Exception)
            {
                return ""; // missing / damaged file or unreadable tag
            }
        }

        private Image ResizeImage(Image image, int width, int height)
        {
            // Create new image the specified sizes
            Bitmap resizedImage = new Bitmap(width, height);

            // Drawing to the new image with the sized image
            using (Graphics graphics = Graphics.FromImage(resizedImage))
            {
                graphics.DrawImage(image, 0, 0, width, height);
            }

            return resizedImage;
        }

        private string GetDuration(string filePath)
        {
            try
            {
                using (var file = TagLib.File.Create(filePath))
                {
                    TimeSpan duration = file.Properties.Duration;
                    return duration.ToString(@"mm\:ss"); // Formatted duration (minute:second)
                }
            }
            catch (Exception)
            {
                return "N/A"; // In the event of an error or if there is no duration information
            }
        }

        private void openAudio_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Multiselect = true;
            open.Title = "Select mp3 or wav file"; // Selection window title
            open.Filter = "MP3 and WAV files (*.mp3, *.wav)|*.mp3;*.wav";
            if (open.ShowDialog() != DialogResult.OK)
                return;

            foreach (string fileName in open.FileNames)
            {
                songPaths.Add(fileName);
                string songTitle = System.IO.Path.GetFileNameWithoutExtension(fileName);
                string artist = GetArtist(fileName); // Artist (safe, no crash on a damaged tag)
                string songDuration = GetDuration(fileName);
                ListViewItem item = new ListViewItem(new string[] { songTitle, artist, songDuration });
                musicList.Items.Add(item);
            }

            foreach (string fileName in open.SafeFileNames)
                SfileName.Add(fileName);

            SaveSongsToFile(); // Save songs in the file.
        }

        private void musicList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressSelectionLoad || autoSkip)
                return; // the code that changed the selection loads the song itself (or nothing must load)
            if (musicList.SelectedItems.Count == 0)
                return;

            // Load only when the mouse button is released: if the mouse-down that selected the item
            // starts a drag (reordering), the song must NOT be loaded, so the current song keeps playing.
            LoadSelectedSongAfterClick();
        }

        private int dragSerial = 0; // incremented whenever a list item drag starts

        private async void LoadSelectedSongAfterClick()
        {
            int serial = dragSerial;
            while ((Control.MouseButtons & MouseButtons.Left) != 0)
                await Task.Delay(30);   // still holding the button: maybe a drag

            if (serial != dragSerial)
                return;                 // it was a drag, not a click
            LoadSelectedSong();
        }

        /// <summary>Loads the selected song (what a click in the list does).</summary>
        private void LoadSelectedSong()
        {
            if (draggingSong || suppressSelectionLoad || musicList.SelectedItems.Count == 0)
                return;

            int clickedIndex = musicList.SelectedItems[0].Index;
            if (clickedIndex == currentSongIndex && output != null)
                return; // already loaded (e.g. selection restored after reordering)

            if (IsSongMissing(clickedIndex))
            {
                // Missing file: tell it, but do not stop the song that is playing now
                MarkSongMissing(clickedIndex, true);
                string path = clickedIndex < songPaths.Count ? songPaths[clickedIndex] : "";
                ThemedDialog.Show(this, $"The file was not found (moved or deleted):\n{path}", "Missing song",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SelectCurrentSongSilently();
                return;
            }

            if( output != null && output.PlaybackState == NAudio.Wave.PlaybackState.Playing )
            {
                output.Pause();
                pauseButton.Visible = false;
                Play.Visible = true;
                customPBarPos.Value = 0;
                customPBarPos.CurrentDuration = 0;
            }
            if (clickedIndex >= 0 && clickedIndex < songPaths.Count)
                selectSong(clickedIndex);
        }

        /// <summary>Select the loaded (playing) song in the list without (re)loading it.</summary>
        private void SelectCurrentSongSilently()
        {
            suppressSelectionLoad = true;
            try
            {
                musicList.SelectedItems.Clear();
                if (currentSongIndex >= 0 && currentSongIndex < musicList.Items.Count)
                {
                    musicList.Items[currentSongIndex].Selected = true;
                    musicList.Items[currentSongIndex].Focused = true;
                }
            }
            finally
            {
                suppressSelectionLoad = false;
            }
        }

        /// <summary>
        /// Move a song in the playlist from index <paramref name="from"/> to index <paramref name="to"/>
        /// (final position). The playing song keeps playing, the lists stay in sync and the order is saved.
        /// </summary>
        private void MoveSong(int from, int to)
        {
            int count = musicList.Items.Count;
            if (from < 0 || from >= count || to < 0 || to >= count || from == to)
                return;

            suppressSelectionLoad = true;
            try
            {
                var item = musicList.Items[from];
                musicList.BeginUpdate();
                musicList.Items.RemoveAt(from);
                musicList.Items.Insert(to, item);
                musicList.EndUpdate();

                string path = songPaths[from];
                songPaths.RemoveAt(from);
                songPaths.Insert(to, path);

                string sfn = SfileName[from];
                SfileName.RemoveAt(from);
                SfileName.Insert(to, sfn);

                // the loaded song may have shifted by one
                if (currentSongIndex == from)
                    currentSongIndex = to;
                else if (from < currentSongIndex && to >= currentSongIndex)
                    currentSongIndex--;
                else if (from > currentSongIndex && to <= currentSongIndex)
                    currentSongIndex++;
            }
            finally
            {
                suppressSelectionLoad = false;
            }

            SelectCurrentSongSilently();
            musicList.Items[to].EnsureVisible();
            SaveSongsToFile();
        }

        // Move forward selected song in the list
        private void moveSongFWDbtn_Click(object sender, EventArgs e)
        {
            if (musicList.SelectedItems.Count > 0)
            {
                int index = musicList.SelectedIndices[0];
                MoveSong(index, index + 1);
            }
        }

        // Move backward selected song in the list
        private void moveSongBWDbtn_Click(object sender, EventArgs e)
        {
            if (musicList.SelectedItems.Count > 0)
            {
                int index = musicList.SelectedIndices[0];
                MoveSong(index, index - 1);
            }
        }

        // ---------- Reordering the playlist with drag & drop ----------

        private bool draggingSong = false;          // a list item is being dragged
        private bool suppressSelectionLoad = false; // selection is changed by code, do not load the song
        private int currentSongIndex = -1;          // index of the loaded song in the list (-1 = none)
        private Panel? dropMarker;                  // the line that shows where the song will land

        private void musicList_ItemDrag(object? sender, ItemDragEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.Item is not ListViewItem item)
                return;

            dragSerial++;
            draggingSong = true;
            try
            {
                var result = musicList.DoDragDrop(item, DragDropEffects.Move);
                if (result == DragDropEffects.None)
                    SelectCurrentSongSilently(); // dropped outside / cancelled (Esc)
            }
            finally
            {
                draggingSong = false;
                HideDropMarker();
            }
        }

        /// <summary>Insertion index (0..Count) for a screen point, and the Y of the marker line.</summary>
        private int GetDropInsertIndex(Point screenPoint, out int markerY)
        {
            Point pt = musicList.PointToClient(screenPoint);
            int count = musicList.Items.Count;
            markerY = 0;
            if (count == 0)
                return 0;

            var hit = musicList.HitTest(4, pt.Y).Item;
            if (hit == null)
            {
                var first = musicList.TopItem ?? musicList.Items[0];
                if (pt.Y < first.Bounds.Top)
                {
                    markerY = first.Bounds.Top;
                    return first.Index;
                }
                var last = musicList.Items[count - 1];
                markerY = last.Bounds.Bottom;
                return count;
            }

            Rectangle b = hit.Bounds;
            if (pt.Y < b.Top + b.Height / 2)
            {
                markerY = b.Top;
                return hit.Index;
            }
            markerY = b.Bottom;
            return hit.Index + 1;
        }

        private void ShowDropMarker(int y)
        {
            if (dropMarker == null)
            {
                dropMarker = new Panel { Height = 3, BackColor = Theme.Current.Accent, Enabled = false };
                musicList.Controls.Add(dropMarker);
            }
            dropMarker.BackColor = Theme.Current.Accent;
            dropMarker.SetBounds(0, Math.Max(0, y - 1), musicList.ClientSize.Width, 3);
            dropMarker.Visible = true;
            dropMarker.BringToFront();
        }

        private void HideDropMarker()
        {
            if (dropMarker != null)
                dropMarker.Visible = false;
        }

        private void musicList_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(typeof(ListViewItem)))
                return; // files from Explorer: handled by DragEnter

            e.Effect = DragDropEffects.Move;

            // auto-scroll when the mouse is near the top / bottom edge
            Point pt = musicList.PointToClient(new Point(e.X, e.Y));
            var top = musicList.TopItem;
            if (top != null)
            {
                int rowH = top.Bounds.Height;
                if (pt.Y < top.Bounds.Top + rowH / 2 && top.Index > 0)
                    musicList.Items[top.Index - 1].EnsureVisible();
                else if (pt.Y > musicList.ClientSize.Height - rowH)
                {
                    int below = musicList.HitTest(4, musicList.ClientSize.Height - 2).Item?.Index ?? -1;
                    if (below >= 0 && below + 1 < musicList.Items.Count)
                        musicList.Items[below + 1].EnsureVisible();
                }
            }

            GetDropInsertIndex(new Point(e.X, e.Y), out int markerY);
            ShowDropMarker(markerY);
        }

        private void musicList_DragLeave(object? sender, EventArgs e)
        {
            HideDropMarker();
        }

        // DragEnter event management
        private void musicList_DragEnter(object? sender, DragEventArgs e)
        {
            if (sender == null)
                return;

            if (e.Data != null && e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                e.Effect = DragDropEffects.Move; // reordering inside the list
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    if (IsMp3File(file) || IsWavFile(file))
                    {
                        e.Effect = DragDropEffects.Copy;
                    }
                    else
                    {
                        e.Effect = DragDropEffects.None;
                        break; // If any file has an incorrect extension, we can abort the process
                    }
                }
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private bool IsMp3File(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsWavFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase);
        }

        // DragDrop event management
        private void musicList_DragDrop(object? sender, DragEventArgs e)
        {
            if (sender == null)
                return;

            // Reordering inside the list
            if (e.Data != null && e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                HideDropMarker();
                var item = (ListViewItem)e.Data.GetData(typeof(ListViewItem))!;
                int from = item.Index;
                int insertAt = GetDropInsertIndex(new Point(e.X, e.Y), out _);
                int to = insertAt > from ? insertAt - 1 : insertAt;

                if (to == from)
                {
                    // dropped onto its own place: behave like a normal click (load the song)
                    draggingSong = false;
                    BeginInvoke(new Action(LoadSelectedSong));
                }
                else
                {
                    MoveSong(from, to);
                }
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Filedrop logic
                foreach (string fileName in files)
                {
                    songPaths.Add(fileName);
                    string songTitle = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    string artist = GetArtist(fileName); // Artist (safe, no crash on a damaged tag)
                    string songDuration = GetDuration(fileName);
                    // Files adding to the ListView
                    ListViewItem item = new ListViewItem(new string[] { songTitle, artist, songDuration });
                    musicList.Items.Add(item);
                    SfileName.Add(System.IO.Path.GetFileName(fileName));
                }

                //foreach( string fileName in files )
                //    SfileName.Add(System.IO.Path.GetFileName(fileName));

                SaveSongsToFile(); // Save songs in the file.
            }
        }

        // ---------- Lyrics (LRCLIB) ----------

        private LyricsView lyricsView = null!;
        private int lyricsRequest = 0;              // newer request (song change / manual search) wins
        private CancellationTokenSource? lyricsCts;
        private string lyricsAlbum = "";            // album tag of the loaded song
        private string lyricsTagArtist = "", lyricsTagTitle = "";   // raw tags: extra search variants
        private string? lyricsShownPath;            // song the shown lyrics belong to (for saving a timing correction)

        /// <summary>Lyrics panel + the (formerly hidden) artist / title boxes and button as manual search.</summary>
        private void SetupLyricsUi()
        {
            lyricsView = new LyricsView
            {
                Location = new Point(244, 400),
                Size = new Size(650, 126),
            };
            Controls.Add(lyricsView);
            lyricsView.BringToFront();
            lyricsView.SetStatus("Lyrics will appear here");
            lyricsView.ShiftChanged += LyricsView_ShiftChanged;

            // manual search: shows what was searched, can be corrected if the automatic match is wrong
            artistBox.SetBounds(104, 400, 128, 26);
            titleBox.SetBounds(104, 432, 128, 26);
            getLyricsBtn.SetBounds(104, 466, 128, 29);
            artistBox.Text = "";
            titleBox.Text = "";
            artistBox.PlaceholderText = "Artist";
            titleBox.PlaceholderText = "Title";
            getLyricsBtn.Text = "Search lyrics";
            getLyricsBtn.Enabled = false;            // enabled when a song is loaded
            artistBox.KeyDown += LyricsBox_KeyDown;
            titleBox.KeyDown += LyricsBox_KeyDown;
        }

        // timing correction with the buttons of the lyrics panel: saved with the lyrics (LRC [offset:] tag)
        private void LyricsView_ShiftChanged(LyricsResult lyrics)
        {
            if (lyricsShownPath != null)
                LyricsService.SaveToCache(lyricsShownPath, lyrics);
        }

        private void ShowLyrics(string path, LyricsResult? result)
        {
            if (result == null)
            {
                lyricsShownPath = null;
                lyricsView.SetStatus("No lyrics found  –  you can correct artist / title on the left and search again");
            }
            else
            {
                lyricsShownPath = path;
                lyricsView.SetLyrics(result);
            }
        }

        private void LyricsBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && getLyricsBtn.Enabled)
            {
                e.SuppressKeyPress = true;
                getLyricsBtn.PerformClick();
            }
        }

        // Manual search with the artist / title typed in the boxes (skips the cache, result is cached)
        private void getLyricsBtn_Click(object sender, EventArgs e)
        {
            if (currentSongIndex < 0 || pcm == null)
                return;
            if (string.IsNullOrWhiteSpace(titleBox.Text))
            {
                ThemedDialog.Show(this, "Please enter at least the title of the track!", "Lyrics", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            LoadLyrics(currentSongIndex, artistBox.Text.Trim(), titleBox.Text.Trim(), "", pcm.TotalTime.TotalSeconds, forceOnline: true);
        }

        /// <summary>Fetch the lyrics in the background (cache first) and show them when they arrive.</summary>
        private async void LoadLyrics(int index, string artist, string title, string album, double durationSec, bool forceOnline)
        {
            if (index < 0 || index >= songPaths.Count)
                return;

            int request = ++lyricsRequest;
            lyricsCts?.Cancel();
            var cts = lyricsCts = new CancellationTokenSource();
            string path = songPaths[index];

            // cached: show at once, no network at all
            if (!forceOnline && LyricsService.TryGetCached(path, out var cached))
            {
                ShowLyrics(path, cached);
                getLyricsBtn.Enabled = true;
                return;
            }

            lyricsShownPath = null;
            lyricsView.SetStatus("Searching lyrics…");
            getLyricsBtn.Enabled = false;
            try
            {
                // wait a little: when clicking / arrowing quickly through the list, only the song
                // that stays selected is searched (LRCLIB limits the number of requests)
                if (!forceOnline)
                    await Task.Delay(400, cts.Token);

                var result = await LyricsService.GetAsync(path, artist, title, album, durationSec, forceOnline, cts.Token,
                    lyricsTagArtist, lyricsTagTitle);
                if (request != lyricsRequest)
                    return; // another song was selected meanwhile

                ShowLyrics(path, result);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // replaced by a newer request
            }
            catch (LyricsService.BusyException)
            {
                if (request == lyricsRequest)
                    lyricsView.SetStatus("lrclib.net is busy right now  –  try again in a minute with Search lyrics");
            }
            catch (Exception)
            {
                if (request == lyricsRequest)
                    lyricsView.SetStatus("Lyrics: no connection to lrclib.net (try again with Search lyrics)");
            }
            finally
            {
                if (request == lyricsRequest)
                    getLyricsBtn.Enabled = true;
            }
        }

        /// <summary>
        /// The position that is HEARD now. pcm.CurrentTime is where the reader is, which is ahead of the
        /// speakers by the queued audio (WaveOut buffers + Windows audio engine); that part is subtracted.
        /// </summary>
        private TimeSpan GetAudiblePosition()
        {
            if (pcm == null)
                return TimeSpan.Zero;

            TimeSpan pos = pcm.CurrentTime;
            if (output != null && fader != null && output.PlaybackState != NAudio.Wave.PlaybackState.Stopped)
            {
                try
                {
                    int blockAlign = fader.WaveFormat.BlockAlign;
                    int rate = fader.WaveFormat.SampleRate;
                    long played = output.GetPosition() / blockAlign;
                    long queued = fader.FramesRendered - played;
                    if (queued > 0 && queued < rate * 2) // sanity: never more than 2 s
                        pos -= TimeSpan.FromSeconds((double)queued / rate);
                }
                catch { /* device position not available: use the reader position */ }
            }
            return pos < TimeSpan.Zero ? TimeSpan.Zero : pos;
        }

        /// <summary>Loads the song. Returns false if it could not be loaded (missing / damaged file).</summary>
        private bool selectSong(int index)
        {
            if (index >= 0 && index < songPaths.Count)
            {
                string selectedFilePath = songPaths[index];

                if (!System.IO.File.Exists(selectedFilePath))
                {
                    MarkSongMissing(index, true);
                    autoSkip = false;
                    return false;
                }
                MarkSongMissing(index, false); // it may be back (e.g. drive reconnected)

                TagLib.File? file = null;
                try
                {
                    file = TagLib.File.Create(selectedFilePath);
                }
                catch (Exception)
                {
                    // damaged / unreadable tag: play it without tag information
                }
                string title = file?.Tag.Title ?? ""; // Title
                string artist = file?.Tag.FirstPerformer ?? ""; // Artist
                lyricsAlbum = file?.Tag.Album ?? "";
                lyricsTagArtist = artist;
                lyricsTagTitle = title;
                int bitrate = file != null ? (int)file.Properties.AudioBitrate : 0; // Bitrate
                string bitrateStr = bitrate.ToString();
                //IPicture[] pictures = file.Tag.Pictures; // Cover
                IPicture[] pictures = null;
                try
                {
                    pictures = file?.Tag.Pictures;
                }
                catch( Exception ex )
                {
                    // In case of error, we set default values
                    ThemedDialog.Show(this, $"An error occurred while reading the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    coverAnim.ShowDefault(); // Default picture
                }
                finally
                {
                    file?.Dispose(); // Free up resources
                }

                if (pictures != null && pictures.Length > 0)
                {
                    IPicture picture = pictures[0];
                    using (MemoryStream memoryStream = new MemoryStream(picture.Data.Data))
                    {
                        try
                        {
                            // Own cover: slow zoom + pan over it (the animator makes its own scaled copy)
                            using (Image albumArtImage = Image.FromStream(memoryStream))
                                coverAnim.SetCover(albumArtImage);
                        }
                        catch
                        {coverAnim.ShowDefault();}
                    }
                }
                else
                    coverAnim.ShowDefault();

                // Lyrics: what will be searched (from the tags, or from the file name); editable for a manual search
                var (lyrArtist, lyrTitle) = LyricsService.GuessArtistTitle(selectedFilePath, artist, title);
                artistBox.Text = lyrArtist;
                titleBox.Text = LyricsService.CleanTitle(lyrTitle);

                DisposeWave(false);
                stopped = false;

                // the new song gets the CURRENT EQ (eqValues / bwValues always follow the bars), so changes that
                // were not saved yet are kept when the song changes

                bands = new NAudio.Extras.EqualizerBand[10]
                {
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[0],
                        Frequency = 31,
                        Gain = eqValues[0]
                    },
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[1],
                        Frequency = 62,
                        Gain = eqValues[1]
                    },
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[2],
                        Frequency = 125,
                        Gain = eqValues[2]
                    },
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[3],
                        Frequency = 250,
                        Gain = eqValues[3]
                    },
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[4],
                        Frequency = 500,
                        Gain = eqValues[4]
                    },
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[5],
                        Frequency = 1000,
                        Gain = eqValues[5]
                    },
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[6],
                        Frequency = 2000,
                        Gain = eqValues[6]
                    },
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[7],
                        Frequency = 4000,
                        Gain = eqValues[7]
                    },
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[8],
                        Frequency = 8000,
                        Gain = eqValues[8]
                    },
                    new NAudio.Extras.EqualizerBand
                    {
                        Bandwidth = bwValues[9],
                        Frequency = 16000,
                        Gain = eqValues[9]
                    }
                };

                var oldPcm = this.pcm;
                this.pcm = null;

                string fileExtension = System.IO.Path.GetExtension(SfileName[index]);

                if (fileExtension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var mp3PcmStream = NAudio.Wave.WaveFormatConversionStream.CreatePcmStream(Mp3Tolerant.Open(songPaths[index]));
                    //    var mp3PcmStream = NAudio.Wave.WaveFormatConversionStream.CreatePcmStream(new NAu.Wave.Mp3FileReader(songPaths[index]));
                        // Resampling if the sample rate is not 44100 Hz and 48000 Hz
                        if( mp3PcmStream.WaveFormat.SampleRate != 44100 && mp3PcmStream.WaveFormat.SampleRate != 48000 )
                        {
                            var targetFormat = new WaveFormat(44100, 16, mp3PcmStream.WaveFormat.Channels);
                            this.pcm = new WaveFormatConversionStream(targetFormat, mp3PcmStream);
                        }
                        else
                        {
                            this.pcm = mp3PcmStream;
                        }
                    }
                    catch( Exception ex )
                    {
                        ThemedDialog.Show(this, $"Mp3FileReader error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else if (fileExtension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var wavPcmStream = NAudio.Wave.WaveFormatConversionStream.CreatePcmStream(new NAudio.Wave.WaveFileReader(songPaths[index]));
                        if( wavPcmStream.WaveFormat.SampleRate != 44100 && wavPcmStream.WaveFormat.SampleRate != 48000 )
                        {
                            var targetFormat = new WaveFormat(44100, 16, wavPcmStream.WaveFormat.Channels);
                            this.pcm = new WaveFormatConversionStream(targetFormat, wavPcmStream);
                        }
                        else
                        {
                            this.pcm = wavPcmStream;
                        }
                    }
                    catch( Exception ex )
                    {
                        ThemedDialog.Show(this, $"WaveFileReader error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                // The previous song's file is released (otherwise it stays locked and cannot be moved / deleted)
                try { oldPcm?.Dispose(); } catch { }

                if (this.pcm == null)
                {
                    // Could not open the file: no playable song now, but no crash either
                    output = null;
                    fader = null;
                    fadeVersion++;
                    currentSongIndex = -1;
                    Play.Enabled = false;
                    pauseButton.Enabled = false;
                    Stop.Enabled = false;
                    pauseButton.Visible = false;
                    Play.Visible = true;
                    autoSkip = false;
                    return false;
                }

                // no endless silence after the song: the end of the stream must reach the fader (end detection)
                WaveChannel32 waveChannel = new WaveChannel32(pcm) { PadWithZeroes = false };
                var sampleChannel = new SampleChannel(waveChannel);
                equalizer = new NAudio.Extras.Equalizer(sampleChannel, bands);
                TimeSpan duration = this.pcm.TotalTime;
                double totalSeconds = this.pcm.TotalTime.TotalSeconds;
                int seconds = (int)totalSeconds;
                // new song starts at 0: reset the bar and the time text here, the timer only updates them
                // while playing (paused song + click on another one left the old position on the bar)
                customPBarPos.Value = 0;
                customPBarPos.CurrentDuration = 0;
                customPBarPos.Maximum = seconds;
                //output = new NAudio.Wave.DirectSoundOut();
                output = new NAu.Wave.WaveOutEvent(); // With this possible the sound volume controlling
                output.DesiredLatency = OutputLatencyMs;
                output.NumberOfBuffers = 3;
                // Full device volume on both channels: volume and balance are done in the audio chain.
                // (This also undoes a one-sided Windows mixer volume that older versions left behind.)
                output.Volume = 1.0f;

                horzRollingLabel.Text = "Name: " + SfileName[index] +
                "    " +
                "Title: " + title +
                "    " +
                "Artist: " + artist
                + "    " +
                "Bitrate: " + bitrateStr +
                "kb/s    " +
                String.Format("Duration: {0:00}:{1:00}", (int)duration.TotalMinutes, duration.Seconds);
                UpdateNowPlaying(SfileName[index], title, artist, bitrateStr);

                //    output.Init(new SampleToWaveProvider(equalizer));

                //var sampleCh = new SampleChannel(waveChannel, true);
                setVolumeDelegate = vol => sampleChannel.Volume = vol;
                // chain: ... -> equalizer -> balance -> fader (play/pause/seek) -> meter -> output
                balance = new BalanceSampleProvider(equalizer) { Balance = customHorzScrollBar1.Pan / 10f };
                balance.SnapToTarget();
                fader = new FadeSampleProvider(balance);
                fadeVersion++;
                postVolumeMeter = new MeteringSampleProvider(fader);
                postVolumeMeter.StreamVolume += OnPostVolumeMeter;
                //output.Init(postVolumeMeter);
                try
                {
                    output.Init(postVolumeMeter);
                }
                catch (Exception ex)
                {
                    ThemedDialog.Show(this, $"WaveOut initialization error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                setVolumeDelegate(volumeSlider1.Volume);

                customPBarPos.TotalDuration = seconds;

                //positionBar.Minimum = 0;
                //positionBar.Maximum = (int)pcm.TotalTime.TotalSeconds;

                Play.Enabled = true;
                pauseButton.Enabled = true;
                Stop.Enabled = true;
                /*pauseButton.Visible = false;
                Play.Visible = true;*/
                autoSkip = false;

                /// <summary>
                /// - Debug -
                /// Sample rate, Channels and bit depth informations about music file
                /// </summary>
                //ThemedDialog.Show(this, $"PCM Sample Rate: {pcm.WaveFormat.SampleRate}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //ThemedDialog.Show(this, $"PCM Channels: {pcm.WaveFormat.Channels}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //ThemedDialog.Show(this, $"PCM Bits Per Sample: {pcm.WaveFormat.BitsPerSample}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);


                if( !string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(artist) )
                {
                    DCtitle = title;
                    DCartist = artist;
                }
                else
                {
                    DCtitle = SfileName[index];
                    DCartist = "";
                }

                updateDiscordRPC_Stop();
                currentSongIndex = index;
                LoadLyrics(index, artistBox.Text, titleBox.Text, lyricsAlbum, duration.TotalSeconds, forceOnline: false);
                return true;
            }
            return false;
        }
        void OnPostVolumeMeter(object sender, StreamVolumeEventArgs e)
        {
            // we know it is stereo
            volumeMeter1.Amplitude = e.MaxSampleValues[0];
            volumeMeter2.Amplitude = e.MaxSampleValues[1];
        }

        private BalanceSampleProvider? balance;   // stereo balance in the audio chain (slider -10 ... +10)

        private void customHorzScrollBar1_PanChanged(object sender, EventArgs e)
        {
            // -10 (left) ... 0 ... +10 (right): the other channel is lowered linearly, smoothly (no crackle)
            if (balance != null)
                balance.Balance = customHorzScrollBar1.Pan / 10f;
        }

        private async void Play_Click(object sender, EventArgs e)
        {
            if (output == null || fader == null)
                return;

            // Playing and not in the middle of a fade-out -> nothing to do
            if (output.PlaybackState == NAudio.Wave.PlaybackState.Playing && !decrementVol)
                return;

            int version = ++fadeVersion;
            decrementVol = false;
            incrementVol = true;

            // UI reacts immediately, the sound follows with the fade
            stopped = false;
            pauseButton.Visible = true;
            Play.Visible = false;
            updateDiscordRPC_Playing();

            if (output.PlaybackState != NAudio.Wave.PlaybackState.Playing)
            {
                fader.SetGain(0.0f);     // start from silence
                output.Play();
            }
            // If a fade-out was running, it continues from the current gain back up (no jump)
            await fader.FadeTo(1.0f, FadeInMs);

            if (version == fadeVersion)
                incrementVol = false;
        }

        private async void pauseButton_Click(object sender, EventArgs e)
        {
            if (output == null || fader == null)
                return;

            pauseButton.Visible = false;
            Play.Visible = true;
            updateDiscordRPC_Stop();

            if (output.PlaybackState != NAudio.Wave.PlaybackState.Playing || decrementVol)
                return;

            int version = ++fadeVersion;
            incrementVol = false;
            decrementVol = true;

            // Fade to silence; after that the fader holds silence without moving the song position
            bool completed = await fader.FadeTo(0.0f, FadeOutMs, holdSilenceWhenDone: true);

            if (!completed || version != fadeVersion || output == null)
                return;

            // The fade is rendered, but the device plays it later (WaveOut buffers + Windows audio engine).
            // Wait until the device has really played the end of the fade (max. 1 s as a safety net).
            var waveOut = output;
            long targetBytes = fader.FadeEndFrame * fader.WaveFormat.BlockAlign;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 1000)
            {
                long played;
                try { played = waveOut.GetPosition(); }
                catch { break; }
                if (played >= targetBytes)
                    break;
                await Task.Delay(10);
                if (version != fadeVersion || output != waveOut)
                    return;  // Play / Stop / new song in the meantime
            }

            if (version == fadeVersion && output == waveOut &&
                waveOut.PlaybackState == NAudio.Wave.PlaybackState.Playing)
            {
                // Pause + drop the (silent) audio still queued, so on Play nothing old comes back
                waveOut.PauseAndFlush();
                fader.ResetFrameCounter();
                decrementVol = false;
            }
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            if (output != null)
            {
                stopped = true;
                incrementVol = false;
                decrementVol = false;
                fadeVersion++;          // cancel a running Play/Pause fade
                fader?.SetGain(1.0f);
                fader?.ResetFrameCounter(); // output.Stop() resets the device position too
                customPBarPos.Value = 0;
                customPBarPos.CurrentDuration = 0;
                output.Stop();                      // first stop the device, then reposition (no read in between)
                this.pcm.CurrentTime = new TimeSpan(0);
                fader?.ClearEnded();

                updateDiscordRPC_Stop();

                pauseButton.Visible = false;
                Play.Visible = true;
            }
        }

        private void deletePlaylist_Click(object sender, EventArgs e)
        {
            bool clear = ThemedDialog.Show(this, "All songs will be removed from the playlist. The music files themselves are not deleted.",
                "Delete the whole playlist?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

            if (!clear)
                return;

            if (output != null)
            {
                if (output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                {
                    stopped = true;
                    output.Stop();
                }
                customPBarPos.Value = 0;
                this.pcm.CurrentTime = new TimeSpan(0);
                coverAnim.ShowDefault();
                Play.Enabled = false;
                pauseButton.Enabled = false;
                pauseButton.Visible = false;
                Play.Visible = true;
                Stop.Enabled = false;
                DisposeWave(false);
            }
            ClearPlaylist();
        }

        private void button2_Click(object sender, EventArgs e) // Save playlist
        {
            bool saveList = ThemedDialog.Show(this, "The playlist is saved automatically after every change. Save it now anyway?",
                "Save the playlist?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

            if (!saveList)
                return;

            SaveSongsToFile();
        }

        private void button1_Click(object sender, EventArgs e) // Save EQ settings
        {
            bool saveEq = ThemedDialog.Show(this, "The current equalizer settings will be used every time the player starts.",
                "Save the EQ settings?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

            if (!saveEq)
                return;

            SaveEqSettings();
        }

        /// <summary>Writes eqsettings.txt (numbers always with "."). Returns false (after a message) if it failed.</summary>
        private bool SaveEqSettings()
        {
            StreamWriter? Write = null;
            try
            {
            Write = new StreamWriter(EqSettingsPath);
            Write.WriteLine("eq1Value=" + eqValues[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq1Scroll=" + eqScrollValues[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq2Value=" + eqValues[1].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq2Scroll=" + eqScrollValues[1].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq3Value=" + eqValues[2].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq3Scroll=" + eqScrollValues[2].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq4Value=" + eqValues[3].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq4Scroll=" + eqScrollValues[3].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq5Value=" + eqValues[4].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq5Scroll=" + eqScrollValues[4].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq6Value=" + eqValues[5].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq6Scroll=" + eqScrollValues[5].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq7Value=" + eqValues[6].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq7Scroll=" + eqScrollValues[6].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq8Value=" + eqValues[7].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq8Scroll=" + eqScrollValues[7].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq9Value=" + eqValues[8].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq9Scroll=" + eqScrollValues[8].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq10Value=" + eqValues[9].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("eq10Scroll=" + eqScrollValues[9].ToString(System.Globalization.CultureInfo.InvariantCulture));

            Write.WriteLine("bw1Value=" + bwValues[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw1Scroll=" + bwScrollValues[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw2Value=" + bwValues[1].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw2Scroll=" + bwScrollValues[1].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw3Value=" + bwValues[2].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw3Scroll=" + bwScrollValues[2].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw4Value=" + bwValues[3].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw4Scroll=" + bwScrollValues[3].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw5Value=" + bwValues[4].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw5Scroll=" + bwScrollValues[4].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw6Value=" + bwValues[5].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw6Scroll=" + bwScrollValues[5].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw7Value=" + bwValues[6].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw7Scroll=" + bwScrollValues[6].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw8Value=" + bwValues[7].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw8Scroll=" + bwScrollValues[7].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw9Value=" + bwValues[8].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw9Scroll=" + bwScrollValues[8].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw10Value=" + bwValues[9].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.WriteLine("bw10Scroll=" + bwScrollValues[9].ToString(System.Globalization.CultureInfo.InvariantCulture));
            Write.Close();
            Write = null;
            eqDirty = false;
            return true;
            }
            catch (Exception ex)
            {
                Write?.Dispose();
                ThemedDialog.Show(this, $"The EQ settings could not be saved:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void customPBarPos_ValueChanged(object sender, EventArgs e)
        {
            /*if (output != null)
            {
                if (output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                {
                    output.Pause();
                    this.pcm.CurrentTime = TimeSpan.FromSeconds(customPBarPos.Value);
                    output.Play();
                }
                else
                    this.pcm.CurrentTime = TimeSpan.FromSeconds(customPBarPos.Value);

                changingPos = false;
            }*/
            //else
            //    customPBarPos.Value = 0;
        }

        private async void customPBarPos_PositionChanged(object sender, EventArgs e)
        {
            if (output == null)
                return;

            if (pcm == null)
                return;

            var stream = pcm;
            var target = TimeSpan.FromSeconds(customPBarPos.ExactValue);
            if (target >= stream.TotalTime)
                target = stream.TotalTime - TimeSpan.FromMilliseconds(100);
            if (target < TimeSpan.Zero)
                target = TimeSpan.Zero;

            if (output.PlaybackState == NAudio.Wave.PlaybackState.Playing && fader != null)
            {
                // Short fade-out -> seek in the audio thread -> fade-in: no click, no race with the reader
                await fader.SeekSmooth(() => stream.CurrentTime = target);
                if (pcm == stream)
                    updateDiscordRPC_Playing();
            }
            else
            {
                // Paused / stopped: the device does not read now, the position can be set directly
                stream.CurrentTime = target;
                fader?.ClearEnded();

                updateDiscordRPC_Stop();
            }
        }

        private void updateDiscordRPC_Playing()
        {
            if (client == null || pcm == null)
                return; // Discord is not configured / no song loaded

            // Calculate the current UTC time and start time
            double currSeconds = this.pcm.CurrentTime.TotalSeconds;
            int sec = (int)currSeconds;
            now = DateTime.UtcNow;
            var startTime = now - TimeSpan.FromSeconds(sec); // Start playback

            TimeSpan durMinutes = this.pcm.TotalTime;

            client.SetPresence(new RichPresence()
            {
                Details = DCtitle, // Song title
                State = DCartist, // Status, such as "Playing music"
                //Timestamps = Timestamps.Now,
                Type = ActivityType.Listening,
                Timestamps = new Timestamps
                {
                    Start = startTime, // Playback start time
                    End = now + TimeSpan.FromSeconds(this.pcm.TotalTime.TotalSeconds - currSeconds) // End of playback
                },
                Assets = new Assets()
                {
                    LargeImageKey = "musical_note",
                    //LargeImageText = String.Format("Duration: {0:00}:{1:00}", (int)duration.TotalMinutes, duration.Seconds),
                    LargeImageText = String.Format("{0:00}:{1:00}", (int)durMinutes.TotalMinutes, (int)durMinutes.TotalSeconds % 60),
                    SmallImageKey = "musical_note",
                    SmallImageText = "Enjoying great tunes"
                }
            });
        }

        private void updateDiscordRPC_Stop()
        {
            if (client == null || pcm == null)
                return; // Discord is not configured / no song loaded

            TimeSpan durMinutes = this.pcm.TotalTime;
            var StopTime = new DateTime(2000, 1, 1, 1, 1, 0);
            
            client.SetPresence(new RichPresence()
            {
                Details = DCtitle, // Song title
                State = DCartist, // Status, such as "Playing music"
                Type = ActivityType.Listening,
                Timestamps = new Timestamps
                {
                    Start = StopTime, // Playback start time
                    End = StopTime // End of playback
                },
                Assets = new Assets()
                {
                    LargeImageKey = "musical_note",
                    LargeImageText = String.Format("{0:00}:{1:00}", (int)durMinutes.TotalMinutes, (int)durMinutes.TotalSeconds % 60),
                    SmallImageKey = "musical_note",
                    SmallImageText = "Enjoying great tunes"
                }
            });
        }

        private void band1CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(0);
        private void band2CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(1);
        private void band3CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(2);
        private void band4CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(3);
        private void band5CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(4);
        private void band6CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(5);
        private void band7CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(6);
        private void band8CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(7);
        private void band9CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(8);
        private void band10CustomBar_ValueChanged(object sender, EventArgs e) => BandChanged(9);

        private bool loadingEq = false;   // the bars are being set from the settings file: keep the loaded values

        /// <summary>
        /// An EQ bar changed: remember the setting (also when no song is loaded, so Save EQ / Reset work then too)
        /// and apply it to the playing song. The values survive song changes (they are not re-read from the file).
        /// </summary>
        private void BandChanged(int i)
        {
            if (loadingEq || eqValues == null)
                return;
            var bar = EqBars[i];

            // a click that does not change anything (the bars report every mouse move) is not a change
            if (bar.Value == eqScrollValues[i] && bar.BandW == bwScrollValues[i])
                return;
            eqDirty = true;   // changed, not saved yet (asked when closing)

            int selectedValue = bar.Value;
            float newGain = MapValue(selectedValue, bar.Minimum, bar.Maximum, -10.0f, 10.0f);
            eqValues[i] = newGain;
            eqScrollValues[i] = selectedValue;

            float selValue = bar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bwValues[i] = newBandwidth;
            bwScrollValues[i] = selValue;

            if (equalizer == null || bands == null)
                return;
            bands[i].Gain = newGain;
            bands[i].Bandwidth = newBandwidth;
            equalizer.Update();
        }

        private float ConvertAndInvert(float val)
        {
            const float maxValue = 10.0f;

            // The linear interpolation operation is inverted to achieve the desired result
            float convertedValue = 1.0f - (Math.Abs(val) / maxValue);

            if (val == 1.0f)
                convertedValue = 1.0f;

            // Limit the value within the desired range
            return Math.Max(0.1f, Math.Min(1.0f, convertedValue));
        }

        private float MapValue(float value, float fromLow, float fromHigh, float toLow, float toHigh)
        {
            return (value - fromLow) / (fromHigh - fromLow) * (toHigh - toLow) + toLow;
        }

        private void OnVolumeSliderChanged(object sender, EventArgs e)
        {
            if (output == null)
                return;

            //output.Volume = sliderToVol(volumeSlider1.Volume);
            //customHorzScrollBar1.Pan = 0.0f; // reset pan pot
            setVolumeDelegate?.Invoke(volumeSlider1.Volume);
        }

        private float sliderToVol(float sliderVol)
        {
            // The sliderVol range is from 0 to 50
            // Values range from 0 to 1

            float normalizedValue = sliderVol / 50.0f;

            // The transformation is, for example, with a square root
            float convertedValue = (float)Math.Sqrt(normalizedValue);

            float finalConv = normalizedValue * 50.0f;

            return finalConv;
        }

        /*private float sliderToVol( float sliderVol )
        {
            // A sliderVol tartomány -50 és 0 között van
            // Az értékek tartománya 0 és 1 között van
        
            float minSlider = 0.0f;
            float maxSlider = 50.0f;
        
            float minValue = 0.0f;
            float maxValue = 1.0f;
        
            // Lineáris interpoláció
            float normalizedValue = (sliderVol - minSlider) / (maxSlider - minSlider);
            float convertedValue = minValue + normalizedValue * (maxValue - minValue);
        
            return convertedValue;
        }*/

        private void eqSettingsFirstTime()
        {
            StreamWriter? Write;

            Write = new StreamWriter(EqSettingsPath);
            Write.WriteLine("eq1Value=0");
            Write.WriteLine("eq1Scroll=0");
            Write.WriteLine("eq2Value=0");
            Write.WriteLine("eq2Scroll=0");
            Write.WriteLine("eq3Value=0");
            Write.WriteLine("eq3Scroll=0");
            Write.WriteLine("eq4Value=0");
            Write.WriteLine("eq4Scroll=0");
            Write.WriteLine("eq5Value=0");
            Write.WriteLine("eq5Scroll=0");
            Write.WriteLine("eq6Value=0");
            Write.WriteLine("eq6Scroll=0");
            Write.WriteLine("eq7Value=0");
            Write.WriteLine("eq7Scroll=0");
            Write.WriteLine("eq8Value=0");
            Write.WriteLine("eq8Scroll=0");
            Write.WriteLine("eq9Value=0");
            Write.WriteLine("eq9Scroll=0");
            Write.WriteLine("eq10Value=0");
            Write.WriteLine("eq10Scroll=0");

            Write.WriteLine("bw1Value=0.1");
            Write.WriteLine("bw1Scroll=1");
            Write.WriteLine("bw2Value=0.1");
            Write.WriteLine("bw2Scroll=1");
            Write.WriteLine("bw3Value=0.1");
            Write.WriteLine("bw3Scroll=1");
            Write.WriteLine("bw4Value=0.1");
            Write.WriteLine("bw4Scroll=1");
            Write.WriteLine("bw5Value=0.1");
            Write.WriteLine("bw5Scroll=1");
            Write.WriteLine("bw6Value=0.1");
            Write.WriteLine("bw6Scroll=1");
            Write.WriteLine("bw7Value=0.1");
            Write.WriteLine("bw7Scroll=1");
            Write.WriteLine("bw8Value=0.1");
            Write.WriteLine("bw8Scroll=1");
            Write.WriteLine("bw9Value=0.1");
            Write.WriteLine("bw9Scroll=1");
            Write.WriteLine("bw10Value=0.1");
            Write.WriteLine("bw10Scroll=1");
            Write.Close();
        }

        private void StringRead(string buffer)
        {
            // Equalizer
            if (buffer.StartsWith("eq1Value="))
            {
                float eqVal = ParseF(buffer.Substring(9));
                eqValues[0] = eqVal;
            }
            if (buffer.StartsWith("eq1Scroll="))
            {
                int eqScroll = (int)Math.Round(ParseF(buffer.Substring(10)));
                eqScrollValues[0] = eqScroll;
            }
            if (buffer.StartsWith("eq2Value="))
            {
                float eq2Val = ParseF(buffer.Substring(9));
                eqValues[1] = eq2Val;
            }
            if (buffer.StartsWith("eq2Scroll="))
            {
                int eq2Scroll = (int)Math.Round(ParseF(buffer.Substring(10)));
                eqScrollValues[1] = eq2Scroll;
            }
            if (buffer.StartsWith("eq3Value="))
            {
                float eq3Val = ParseF(buffer.Substring(9));
                eqValues[2] = eq3Val;
            }
            if (buffer.StartsWith("eq3Scroll="))
            {
                int eq3Scroll = (int)Math.Round(ParseF(buffer.Substring(10)));
                eqScrollValues[2] = eq3Scroll;
            }
            if (buffer.StartsWith("eq4Value="))
            {
                float eq4Val = ParseF(buffer.Substring(9));
                eqValues[3] = eq4Val;
            }
            if (buffer.StartsWith("eq4Scroll="))
            {
                int eq4Scroll = (int)Math.Round(ParseF(buffer.Substring(10)));
                eqScrollValues[3] = eq4Scroll;
            }
            if (buffer.StartsWith("eq5Value="))
            {
                float eq5Val = ParseF(buffer.Substring(9));
                eqValues[4] = eq5Val;
            }
            if (buffer.StartsWith("eq5Scroll="))
            {
                int eq5Scroll = (int)Math.Round(ParseF(buffer.Substring(10)));
                eqScrollValues[4] = eq5Scroll;
            }
            if (buffer.StartsWith("eq6Value="))
            {
                float eq6Val = ParseF(buffer.Substring(9));
                eqValues[5] = eq6Val;
            }
            if (buffer.StartsWith("eq6Scroll="))
            {
                int eq6Scroll = (int)Math.Round(ParseF(buffer.Substring(10)));
                eqScrollValues[5] = eq6Scroll;
            }
            if (buffer.StartsWith("eq7Value="))
            {
                float eq7Val = ParseF(buffer.Substring(9));
                eqValues[6] = eq7Val;
            }
            if (buffer.StartsWith("eq7Scroll="))
            {
                int eq7Scroll = (int)Math.Round(ParseF(buffer.Substring(10)));
                eqScrollValues[6] = eq7Scroll;
            }
            if (buffer.StartsWith("eq8Value="))
            {
                float eq8Val = ParseF(buffer.Substring(9));
                eqValues[7] = eq8Val;
            }
            if (buffer.StartsWith("eq8Scroll="))
            {
                int eq8Scroll = (int)Math.Round(ParseF(buffer.Substring(10)));
                eqScrollValues[7] = eq8Scroll;
            }
            if (buffer.StartsWith("eq9Value="))
            {
                float eq9Val = ParseF(buffer.Substring(9));
                eqValues[8] = eq9Val;
            }
            if (buffer.StartsWith("eq9Scroll="))
            {
                int eq9Scroll = (int)Math.Round(ParseF(buffer.Substring(10)));
                eqScrollValues[8] = eq9Scroll;
            }
            if (buffer.StartsWith("eq10Value="))
            {
                float eq10Val = ParseF(buffer.Substring(10));
                eqValues[9] = eq10Val;
            }
            if (buffer.StartsWith("eq10Scroll="))
            {
                int eq10Scroll = (int)Math.Round(ParseF(buffer.Substring(11)));
                eqScrollValues[9] = eq10Scroll;
            }
            // Bandwidth
            if (buffer.StartsWith("bw1Value="))
            {
                float bwVal = ParseF(buffer.Substring(9));
                bwValues[0] = bwVal;
            }
            if (buffer.StartsWith("bw1Scroll="))
            {
                float bwScroll = ParseF(buffer.Substring(10));
                bwScrollValues[0] = bwScroll;
            }
            if (buffer.StartsWith("bw2Value="))
            {
                float bw2Val = ParseF(buffer.Substring(9));
                bwValues[1] = bw2Val;
            }
            if (buffer.StartsWith("bw2Scroll="))
            {
                float bw2Scroll = ParseF(buffer.Substring(10));
                bwScrollValues[1] = bw2Scroll;
            }
            if (buffer.StartsWith("bw3Value="))
            {
                float bw3Val = ParseF(buffer.Substring(9));
                bwValues[2] = bw3Val;
            }
            if (buffer.StartsWith("bw3Scroll="))
            {
                float bw3Scroll = ParseF(buffer.Substring(10));
                bwScrollValues[2] = bw3Scroll;
            }
            if (buffer.StartsWith("bw4Value="))
            {
                float bw4Val = ParseF(buffer.Substring(9));
                bwValues[3] = bw4Val;
            }
            if (buffer.StartsWith("bw4Scroll="))
            {
                float bw4Scroll = ParseF(buffer.Substring(10));
                bwScrollValues[3] = bw4Scroll;
            }
            if (buffer.StartsWith("bw5Value="))
            {
                float bw5Val = ParseF(buffer.Substring(9));
                bwValues[4] = bw5Val;
            }
            if (buffer.StartsWith("bw5Scroll="))
            {
                float bw5Scroll = ParseF(buffer.Substring(10));
                bwScrollValues[4] = bw5Scroll;
            }
            if (buffer.StartsWith("bw6Value="))
            {
                float bw6Val = ParseF(buffer.Substring(9));
                bwValues[5] = bw6Val;
            }
            if (buffer.StartsWith("bw6Scroll="))
            {
                float bw6Scroll = ParseF(buffer.Substring(10));
                bwScrollValues[5] = bw6Scroll;
            }
            if (buffer.StartsWith("bw7Value="))
            {
                float bw7Val = ParseF(buffer.Substring(9));
                bwValues[6] = bw7Val;
            }
            if (buffer.StartsWith("bw7Scroll="))
            {
                float bw7Scroll = ParseF(buffer.Substring(10));
                bwScrollValues[6] = bw7Scroll;
            }
            if (buffer.StartsWith("bw8Value="))
            {
                float bw8Val = ParseF(buffer.Substring(9));
                bwValues[7] = bw8Val;
            }
            if (buffer.StartsWith("bw8Scroll="))
            {
                float bw8Scroll = ParseF(buffer.Substring(10));
                bwScrollValues[7] = bw8Scroll;
            }
            if (buffer.StartsWith("bw9Value="))
            {
                float bw9Val = ParseF(buffer.Substring(9));
                bwValues[8] = bw9Val;
            }
            if (buffer.StartsWith("bw9Scroll="))
            {
                float bw9Scroll = ParseF(buffer.Substring(10));
                bwScrollValues[8] = bw9Scroll;
            }
            if (buffer.StartsWith("bw10Value="))
            {
                float bw10Val = ParseF(buffer.Substring(10));
                bwValues[9] = bw10Val;
            }
            if (buffer.StartsWith("bw10Scroll="))
            {
                float bw10Scroll = ParseF(buffer.Substring(11));
                bwScrollValues[9] = bw10Scroll;
            }
        }

        /// <summary>
        /// Reads eqsettings.txt. A damaged / unreadable file does not stop the program: the EQ starts flat then.
        /// Numbers are read with "." and "," as decimal separator (files written by older versions used the
        /// Windows number format).
        /// </summary>
        private void LoadEqSettings()
        {
            for (int i = 0; i < 10; i++)
            {
                eqValues[i] = 0; eqScrollValues[i] = 0;
                bwValues[i] = 0.1f; bwScrollValues[i] = 1;
            }
            try
            {
                foreach (string line in System.IO.File.ReadAllLines(EqSettingsPath))
                {
                    try { StringRead(line.Trim()); }
                    catch (FormatException) { /* skip a damaged line, keep the default of that value */ }
                    catch (OverflowException) { }
                }
            }
            catch (Exception)
            {
                // unreadable file: flat EQ
            }
            loadingEq = true;
            try { eqScroll(); }
            finally { loadingEq = false; }
        }

        private static float ParseF(string s) =>
            float.Parse(s.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);

        private void eqScroll()
        {
            band1CustomBar.Value = eqScrollValues[0];
            band2CustomBar.Value = eqScrollValues[1];
            band3CustomBar.Value = eqScrollValues[2];
            band4CustomBar.Value = eqScrollValues[3];
            band5CustomBar.Value = eqScrollValues[4];
            band6CustomBar.Value = eqScrollValues[5];
            band7CustomBar.Value = eqScrollValues[6];
            band8CustomBar.Value = eqScrollValues[7];
            band9CustomBar.Value = eqScrollValues[8];
            band10CustomBar.Value = eqScrollValues[9];

            band1CustomBar.BandW = bwScrollValues[0];
            band2CustomBar.BandW = bwScrollValues[1];
            band3CustomBar.BandW = bwScrollValues[2];
            band4CustomBar.BandW = bwScrollValues[3];
            band5CustomBar.BandW = bwScrollValues[4];
            band6CustomBar.BandW = bwScrollValues[5];
            band7CustomBar.BandW = bwScrollValues[6];
            band8CustomBar.BandW = bwScrollValues[7];
            band9CustomBar.BandW = bwScrollValues[8];
            band10CustomBar.BandW = bwScrollValues[9];
        }

        private float convertBarToVol(int barValue)
        {
            if (barValue == 0)
                return 0.0f;
            else if (barValue == 1)
                return 0.1f;
            else if (barValue == 2)
                return 0.2f;
            else if (barValue == 3)
                return 0.3f;
            else if (barValue == 4)
                return 0.4f;
            else if (barValue == 5)
                return 0.5f;
            else if (barValue == 6)
                return 0.6f;
            else if (barValue == 7)
                return 0.7f;
            else if (barValue == 8)
                return 0.8f;
            else if (barValue == 9)
                return 0.9f;
            else if (barValue == 10)
                return 1.0f;

            return 0.0f;
        }

        void resetBandBars()
        {
            band1CustomBar.Value = 0;
            band2CustomBar.Value = 0;
            band3CustomBar.Value = 0;
            band4CustomBar.Value = 0;
            band5CustomBar.Value = 0;
            band6CustomBar.Value = 0;
            band7CustomBar.Value = 0;
            band8CustomBar.Value = 0;
            band9CustomBar.Value = 0;
            band10CustomBar.Value = 0;
        }

        private void DisposeWave(bool pcmDisp)
        {
            if (output != null)
            {
                if (output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                    output.Stop();
                output.Dispose();
                //output = null;
            }
            if (pcmDisp && pcm != null)
            {
                pcm.Dispose();
                pcm = null;
            }
            //if( stream != null )
            //{
            //    stream.Dispose();
            //    stream = null;
            //}
        }

        private void openAudio_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Select and add .mp3 or .wav file.";
        }
        private void openAudio_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void Play_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Play/Pause.";
        }
        private void Play_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void pauseButton_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Play/Pause.";
        }

        private void pauseButton_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void exit_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Exit music player. Do not forget save changes!";
        }
        private void exit_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void Stop_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Stop music.";
        }
        private void Stop_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void button1_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Save EQ settings.";
        }
        private void button1_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void deletePlaylist_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Delete all songs from playlist.";
        }
        private void deletePlaylist_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void prevSongButton_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Play previous song.";
        }
        private void prevSongButton_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void nextSongButton_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Play next song.";
        }
        private void nextSongButton_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void moveSongFWDbtn_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Move forward selected song in the list.";
        }
        private void moveSongFWDbtn_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void moveSongBWDbtn_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Move backward selected song in the list.";
        }
        private void moveSongBWDbtn_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        private void button2_MouseEnter(object sender, EventArgs e)
        {
            hintLbl.Text = "Save playlist.";
        }

        private void button2_MouseLeave(object sender, EventArgs e)
        { hintLbl.Text = ""; }

        /*
        void ThreadFunc()
        {
            threadStarted = true;
            while( threadStarted )
            {
                if( decrementVol )
                {
                    for( float i = currentVol; i > 0.0f; i-= 0.1f )
                    {
                        output.Volume = i;
                        Thread.Sleep(50);
                    }
                    output.Pause();
                    decrementVol = false;
                }
                if( incrementVol )
                {
                    output.Play();
                    output.Volume = 0.0f;
                    for( float i = 0.0f; i <= currentVol; i+= 0.1f )
                    {
                        output.Volume = i;
                        Thread.Sleep(50);
                    }
                    incrementVol = false;
                }
            }
        }*/
    }
}
/*
namespace NAudio.Addition
{
    public class WaveProviderToWaveStream : WaveStream
    {
        private readonly IWaveProvider sourceProvider;
        private long position;
    
        public WaveProviderToWaveStream(IWaveProvider sourceProvider)
        {
            this.sourceProvider = sourceProvider;
        }
    
        public override WaveFormat WaveFormat => sourceProvider.WaveFormat;
    
        public override long Length => long.MaxValue; // Az IWaveProvider esetében nincs hossz
    
        public override long Position
        {
            get => position;
            set => throw new NotSupportedException("Setting position is not supported.");
        }
    
        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = sourceProvider.Read(buffer, offset, count);
            position += bytesRead;
            return bytesRead;
        }
    }
}
*/
namespace NAudio.Extras
{
    /// <summary>
    /// Basic example of a multi-band eq
    /// uses the same settings for both channels in stereo audio
    /// Call Update after you've updated the bands
    /// Potentially to be added to NAudio in a future version
    /// </summary>
    public class Equalizer : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly EqualizerBand[] bands;
        private readonly BiQuadFilter[,] filters;
        private readonly int channels;
        private readonly int bandCount;
        private bool updated;

        public Equalizer(ISampleProvider sourceProvider, EqualizerBand[] bands)
        {
            this.sourceProvider = sourceProvider;
            this.bands = bands;
            channels = sourceProvider.WaveFormat.Channels;
            bandCount = bands.Length;
            filters = new BiQuadFilter[channels, bands.Length];
            CreateFilters();
        }

        private void CreateFilters()
        {
            for (int bandIndex = 0; bandIndex < bandCount; bandIndex++)
            {
                var band = bands[bandIndex];
                for (int n = 0; n < channels; n++)
                {
                    if (filters[n, bandIndex] == null)
                        filters[n, bandIndex] = BiQuadFilter.PeakingEQ(sourceProvider.WaveFormat.SampleRate, band.Frequency, band.Bandwidth, band.Gain);
                    else
                        filters[n, bandIndex].SetPeakingEq(sourceProvider.WaveFormat.SampleRate, band.Frequency, band.Bandwidth, band.Gain);
                }
            }
        }

        public void Update()
        {
            updated = true;
            CreateFilters();
        }

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);

            if (updated)
            {
                CreateFilters();
                updated = false;
            }

            for (int n = 0; n < samplesRead; n++)
            {
                int ch = n % channels;

                for (int band = 0; band < bandCount; band++)
                {
                    buffer[offset + n] = filters[ch, band].Transform(buffer[offset + n]);
                }
            }
            return samplesRead;
        }
    }

    public class EqualizerBand
    {
        public float Frequency { get; set; }
        public float Gain { get; set; }
        public float Bandwidth { get; set; }
    }

    public class SampleToWaveProvider : IWaveProvider
    {
        private readonly ISampleProvider source;

        public WaveFormat WaveFormat => source.WaveFormat;

        public SampleToWaveProvider(ISampleProvider source)
        {
            if (source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                throw new ArgumentException("Must be already floating point");
            }
            this.source = source;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int count2 = count / 4;
            WaveBuffer waveBuffer = new WaveBuffer(buffer);
            return source.Read(waveBuffer.FloatBuffer, offset / 4, count2) * 4;
        }
    }
}

namespace NAu.Wave
{
    /// <summary>
    /// Alternative WaveOut class, making use of the Event callback
    /// </summary>
    public class WaveOutEvent : IWavePlayer, IWavePosition
    {
        private readonly object waveOutLock;
        private readonly SynchronizationContext syncContext;
        private IntPtr hWaveOut; // WaveOut handle
        private WaveOutBuffer[] buffers;
        private IWaveProvider waveStream;
        private volatile PlaybackState playbackState;
        private AutoResetEvent callbackEvent;

        /// <summary>
        /// Indicates playback has stopped automatically
        /// </summary>
        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        /// <summary>
        /// Gets or sets the desired latency in milliseconds
        /// Should be set before a call to Init
        /// </summary>
        public int DesiredLatency { get; set; }

        /// <summary>
        /// Gets or sets the number of buffers used
        /// Should be set before a call to Init
        /// </summary>
        public int NumberOfBuffers { get; set; }

        /// <summary>
        /// Gets or sets the device number
        /// Should be set before a call to Init
        /// This must be between -1 and <see>DeviceCount</see> - 1.
        /// -1 means stick to default device even default device is changed
        /// </summary>
        public int DeviceNumber { get; set; } = -1;

        /// <summary>
        /// Opens a WaveOut device
        /// </summary>
        public WaveOutEvent()
        {
            syncContext = SynchronizationContext.Current;
            if (syncContext != null &&
                ((syncContext.GetType().Name == "LegacyAspNetSynchronizationContext") ||
                (syncContext.GetType().Name == "AspNetSynchronizationContext")))
            {
                syncContext = null;
            }

            // set default values up
            DesiredLatency = 300;
            NumberOfBuffers = 2;

            waveOutLock = new object();
        }

        /// <summary>
        /// Initialises the WaveOut device
        /// </summary>
        /// <param name="waveProvider">WaveProvider to play</param>
        public void Init(IWaveProvider waveProvider)
        {
            if (playbackState != PlaybackState.Stopped)
            {
                throw new InvalidOperationException("Can't re-initialize during playback");
            }
            if (hWaveOut != IntPtr.Zero)
            {
                // normally we don't allow calling Init twice, but as experiment, see if we can clean up and go again
                // try to allow reuse of this waveOut device
                // n.b. risky if Playback thread has not exited
                DisposeBuffers();
                CloseWaveOut();
            }

            callbackEvent = new AutoResetEvent(false);

            waveStream = waveProvider;
            int bufferSize = waveProvider.WaveFormat.ConvertLatencyToByteSize((DesiredLatency + NumberOfBuffers - 1) / NumberOfBuffers);

            MmResult result;
            lock (waveOutLock)
            {
                result = WaveInterop.waveOutOpenWindow(out hWaveOut, (IntPtr)DeviceNumber, waveStream.WaveFormat, callbackEvent.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, WaveInterop.WaveInOutOpenFlags.CallbackEvent);
            }
            MmException.Try(result, "waveOutOpen");

            buffers = new WaveOutBuffer[NumberOfBuffers];
            playbackState = PlaybackState.Stopped;
            for (var n = 0; n < NumberOfBuffers; n++)
            {
                buffers[n] = new WaveOutBuffer(hWaveOut, bufferSize, waveStream, waveOutLock);
            }
        }

        /// <summary>
        /// Start playing the audio from the WaveStream
        /// </summary>
        public void Play()
        {
            if (buffers == null || waveStream == null)
            {
                throw new InvalidOperationException("Must call Init first");
            }
            if (playbackState == PlaybackState.Stopped)
            {
                playbackState = PlaybackState.Playing;
                callbackEvent.Set(); // give the thread a kick
                ThreadPool.QueueUserWorkItem(state => PlaybackThread(), null);
            }
            else if (playbackState == PlaybackState.Paused)
            {
                Resume();
                callbackEvent.Set(); // give the thread a kick
            }
        }

        private void PlaybackThread()
        {
            Exception exception = null;
            try
            {
                DoPlayback();
            }
            catch (Exception e)
            {
                exception = e;
            }
            finally
            {
                playbackState = PlaybackState.Stopped;
                // we're exiting our background thread
                RaisePlaybackStoppedEvent(exception);
            }
        }

        private void DoPlayback()
        {
            while (playbackState != PlaybackState.Stopped)
            {
                if (!callbackEvent.WaitOne(DesiredLatency))
                {
                    if (playbackState == PlaybackState.Playing)
                    {
                        //    Debug.WriteLine("WARNING: WaveOutEvent callback event timeout");
                    }
                }


                // requeue any buffers returned to us
                if (playbackState == PlaybackState.Playing)
                {
                    int queued = 0;
                    foreach (var buffer in buffers)
                    {
                        if (buffer.InQueue || buffer.OnDone())
                        {
                            queued++;
                        }
                    }
                    if (queued == 0)
                    {
                        // we got to the end
                        playbackState = PlaybackState.Stopped;
                        callbackEvent.Set();
                    }
                }
            }
        }

        /// <summary>
        /// Pause the audio
        /// </summary>
        public void Pause()
        {
            if (playbackState == PlaybackState.Playing)
            {
                MmResult result;
                playbackState = PlaybackState.Paused; // set this here to avoid a deadlock problem with some drivers
                lock (waveOutLock)
                {
                    result = WaveInterop.waveOutPause(hWaveOut);
                }
                if (result != MmResult.NoError)
                {
                    throw new MmException(result, "waveOutPause");
                }
            }
        }

        /// <summary>
        /// Pause and throw away the audio that is still queued in the device.
        /// On the next Play() the buffers are refilled with fresh data, so no
        /// stale (old) audio is heard after resuming. Resets the device position to 0.
        /// </summary>
        public void PauseAndFlush()
        {
            if (playbackState == PlaybackState.Playing)
            {
                MmResult result;
                playbackState = PlaybackState.Paused; // the playback thread will not requeue buffers while paused
                lock (waveOutLock)
                {
                    result = WaveInterop.waveOutPause(hWaveOut);
                    if (result == MmResult.NoError)
                        result = WaveInterop.waveOutReset(hWaveOut); // returns all queued buffers as done
                }
                if (result != MmResult.NoError)
                {
                    throw new MmException(result, "waveOutPause/waveOutReset");
                }
            }
        }

        /// <summary>
        /// Resume playing after a pause from the same position
        /// </summary>
        private void Resume()
        {
            if (playbackState == PlaybackState.Paused)
            {
                MmResult result;
                lock (waveOutLock)
                {
                    result = WaveInterop.waveOutRestart(hWaveOut);
                }
                if (result != MmResult.NoError)
                {
                    throw new MmException(result, "waveOutRestart");
                }
                playbackState = PlaybackState.Playing;
            }
        }

        /// <summary>
        /// Stop and reset the WaveOut device
        /// </summary>
        public void Stop()
        {
            if (playbackState != PlaybackState.Stopped)
            {
                // in the call to waveOutReset with function callbacks
                // some drivers will block here until OnDone is called
                // for every buffer
                playbackState = PlaybackState.Stopped; // set this here to avoid a problem with some drivers whereby 
                MmResult result;
                lock (waveOutLock)
                {
                    result = WaveInterop.waveOutReset(hWaveOut);
                }
                if (result != MmResult.NoError)
                {
                    throw new MmException(result, "waveOutReset");
                }
                callbackEvent.Set(); // give the thread a kick, make sure we exit
            }
        }

        /// <summary>
        /// Gets the current position in bytes from the wave output device.
        /// (n.b. this is not the same thing as the position within your reader
        /// stream - it calls directly into waveOutGetPosition)
        /// </summary>
        /// <returns>Position in bytes</returns>
        public long GetPosition() => WaveOutUtils.GetPositionBytes(hWaveOut, waveOutLock);

        /// <summary>
        /// Gets a <see cref="Wave.WaveFormat"/> instance indicating the format the hardware is using.
        /// </summary>
        public WaveFormat OutputWaveFormat => waveStream.WaveFormat;

        /// <summary>
        /// Playback State
        /// </summary>
        public PlaybackState PlaybackState => playbackState;

        /// <summary>
        /// Volume for this device 1.0 is full scale
        /// </summary>
        public float Volume
        {
            get => WaveOutUtils.GetWaveOutVolume(hWaveOut, waveOutLock);
            set => WaveOutUtils.SetWaveOutVolume(value, hWaveOut, waveOutLock);
        }

        #region Dispose Pattern

        /// <summary>
        /// Closes this WaveOut device
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <summary>
        /// Closes the WaveOut device and disposes of buffers
        /// </summary>
        /// <param name="disposing">True if called from <see>Dispose</see></param>
        protected void Dispose(bool disposing)
        {
            Stop();

            if (disposing)
            {
                DisposeBuffers();
            }

            CloseWaveOut();
        }

        private void CloseWaveOut()
        {
            if (callbackEvent != null)
            {
                callbackEvent.Close();
                callbackEvent = null;
            }
            lock (waveOutLock)
            {
                if (hWaveOut != IntPtr.Zero)
                {
                    WaveInterop.waveOutClose(hWaveOut);
                    hWaveOut= IntPtr.Zero;
                }
            }
        }

        private void DisposeBuffers()
        {
            if (buffers != null)
            {
                foreach (var buffer in buffers)
                {
                    buffer.Dispose();
                }
                buffers = null;
            }
        }

        /// <summary>
        /// Finalizer. Only called when user forgets to call <see>Dispose</see>
        /// </summary>
        ~WaveOutEvent()
        {
            Dispose(false);
            //    Debug.Assert(false, "WaveOutEvent device was not closed");
        }

        #endregion

        private void RaisePlaybackStoppedEvent(Exception e)
        {
            var handler = PlaybackStopped;
            if (handler != null)
            {
                if (syncContext == null)
                {
                    handler(this, new StoppedEventArgs(e));
                }
                else
                {
                    syncContext.Post(state => handler(this, new StoppedEventArgs(e)), null);
                }
            }
        }
    }
    // --- Wave out Utils ---
    public static class WaveOutUtils
    {
        // (Per-channel volume (LeftVolume / RightVolume) was removed: the balance is done in the audio chain,
        //  see BalanceSampleProvider. Changing the Windows volume per channel changed the app's mixer volume.)

        public static float GetWaveOutVolume(IntPtr hWaveOut, object lockObject)
        {
            int stereoVolume;
            MmResult result;
            lock (lockObject)
            {
                result = WaveInterop.waveOutGetVolume(hWaveOut, out stereoVolume);
            }
            MmException.Try(result, "waveOutGetVolume");
            return (stereoVolume & 0xFFFF) / (float)0xFFFF;
        }

        /// <summary>Same volume on both channels.</summary>
        public static void SetWaveOutVolume(float value, IntPtr hWaveOut, object lockObject)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            if (value > 1) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            int v = (int)(value * 0xFFFF);
            int stereoVolume = v | (v << 16);
            MmResult result;
            lock (lockObject)
            {
                result = WaveInterop.waveOutSetVolume(hWaveOut, stereoVolume);
            }
            MmException.Try(result, "waveOutSetVolume");
        }

        public static long GetPositionBytes(IntPtr hWaveOut, object lockObject)
        {
            lock (lockObject)
            {
                var mmTime = new MmTime();
                mmTime.wType = MmTime.TIME_BYTES; // request results in bytes, TODO: perhaps make this a little more flexible and support the other types?
                MmException.Try(WaveInterop.waveOutGetPosition(hWaveOut, ref mmTime, Marshal.SizeOf(mmTime)), "waveOutGetPosition");

                if (mmTime.wType != MmTime.TIME_BYTES)
                    throw new Exception(string.Format("waveOutGetPosition: wType -> Expected {0}, Received {1}", MmTime.TIME_BYTES, mmTime.wType));

                return mmTime.cb;
            }
        }
    }
}
