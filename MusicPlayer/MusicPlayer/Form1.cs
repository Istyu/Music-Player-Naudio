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

        const string closeMessage = "Are you sure that you would like to close the form? \nUnsaved settings may be lost.";
        const string closeCaption = "Music Player Closing";

        private const string LyricsApiUrl = "http://www.azlyrics.com/lyrics/";

        private DiscordRpcClient client;

        private string DCtitle = "";
        private string DCartist = "";
        private DateTime now;

        private string DCappClientID;

        public Form1()
        {
            InitializeComponent();

            artistBox.Visible = false;
            titleBox.Visible = false;
            getLyricsBtn.Visible = false;

            eqValues = new float[10];
            eqScrollValues = new int[10];

            bwValues = new float[10];
            bwScrollValues = new float[10];

            //audioFileLoaded = false;
            //currentVol = 1.0f;

            musicList.AllowDrop = true;
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
            {
                eqSettingsFirstTime();
            }
            else
            {
                StreamReader? Read;
                Read = new StreamReader("eqsettings.txt");
                while (Read.Peek() > -1)
                    StringRead(Read.ReadLine());
                Read.Close();

                eqScroll();
            }

            if (System.IO.File.Exists(listPath))
                LoadSavedSongs();


            try
            {
                using( StreamReader sr = new StreamReader( DCpath ) )
                {
                    while( !sr.EndOfStream )
                    {
                        string line = sr.ReadLine();
                        if( !string.IsNullOrEmpty( line ) )
                        {
                            if( line.StartsWith( "DiscordClientID:" ) )
                                DCappClientID = line.Substring(16);
                        }
                    }
                }
            }
            catch( Exception ex )
            {
                MessageBox.Show($"Error occurred during load DiscordID: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            /*
            Create a Discord client
            NOTE:   If you are using Unity3D, you must use the full constructor and define
                     the pipe connection.
            */
            client = new DiscordRpcClient(DCappClientID);       
            
            //Set the logger
            client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };
        
            //Subscribe to events
            client.OnReady += (sender, e) =>
            {
                Console.WriteLine("Received Ready from user {0}", e.User.Username);
            };
                
            client.OnPresenceUpdate += (sender, e) =>
            {
                Console.WriteLine("Received Update! {0}", e.Presence);
            };
            
            //Connect to the RPC
            client.Initialize();
        
            //Set the rich presence
            //Call this as many times as you want and anywhere in your code.
            client.SetPresence(new RichPresence()
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
        }

        private void exit_Click(object sender, EventArgs e)
        {
            this.Close(); // Notify the OnFormClosing() event handler
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            bool close = true;
            var result = MessageBox.Show(closeMessage, closeCaption,
                                 MessageBoxButtons.YesNo,
                                 MessageBoxIcon.Exclamation);

            // If the no button was pressed ...
            if( result == DialogResult.No )
            {
                // cancel the closure of the form.
                e.Cancel = true;
                close = false;
            }
            if( close )
            {
                client.Dispose();
                try
                {
                    DisposeWave(true);
                    Environment.Exit(0); // Application.Exit(); if there are more forms
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while exiting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            base.OnFormClosing(e);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            client.Invoke();
            if (output != null)
            {
                horzRollingLabel.Left -= 2;
                //horzRollingLabel.Text = String.Format( "{0}", horzRollingLabel.Left );
                if (horzRollingLabel.Right < 0)
                    horzRollingLabel.Left = 1169;
            }

            if (output != null && !stopped && output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
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

            if ( ( output != null && pcm != null && pcm.CurrentTime >= pcm.TotalTime ) && 
                ( !decrementVol || output.PlaybackState == NAudio.Wave.PlaybackState.Playing ) )
            {
                autoSkip = true;
                playNextSong();
            }
        }

        private void SaveSongsToFile()
        {
            // Create "myPlaylist.txt" file in the application folder
            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string playList = "myPlaylist.txt";
            string listPath = Path.Combine(executablePath, playList);

            if (!System.IO.File.Exists(listPath))
                return;

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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error occurred during save songs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadSavedSongs()
        {
            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string playList = "myPlaylist.txt";
            string listPath = Path.Combine(executablePath, playList);
            try
            {
                // We load the saved musics from the file
                using (StreamReader sr = new StreamReader(listPath))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                        {
                            songPaths.Add(line);

                            // The given music is added to the ListView
                            string songTitle = System.IO.Path.GetFileNameWithoutExtension(line);
                            string artist = GetArtist(line); // Implement this function using TagLib
                            string songDuration = GetDuration(line);

                            ListViewItem item = new ListViewItem(new string[] { songTitle, artist, songDuration });
                            musicList.Items.Add(item);
                            SfileName.Add(System.IO.Path.GetFileName(line));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error occurred during load songs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error occurred during playlist delete: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void playNextSong()
        {
            if (musicList.SelectedItems.Count > 0)
            {
                autoSkip = true;
                int nextIndex = musicList.SelectedIndices[0] + 1;
                if (nextIndex < musicList.Items.Count)
                {
                    musicList.Items[nextIndex].Selected = true;
                    selectSong(nextIndex);
                    output.Play();

                    updateDiscordRPC_Playing();
                }
            }
        }

        private void playPreviousSong()
        {
            if (musicList.SelectedItems.Count > 0)
            {
                autoSkip = true;
                int prevIndex = musicList.SelectedIndices[0] - 1;
                if (prevIndex < musicList.Items.Count && prevIndex >= 0)
                {
                    musicList.Items[prevIndex].Selected = true;
                    selectSong(prevIndex);
                    output.Play();

                    updateDiscordRPC_Playing();
                }
            }
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
            var file = TagLib.File.Create(filePath);
            return file.Tag.FirstPerformer;
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
                string selectedFilePath = fileName;
                var file = TagLib.File.Create(selectedFilePath);
                string songTitle = System.IO.Path.GetFileNameWithoutExtension(fileName);
                string artist = file.Tag.FirstPerformer; // Artist
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
            if (musicList.SelectedItems.Count > 0)
            {
                if( !autoSkip && output != null && output.PlaybackState == NAudio.Wave.PlaybackState.Playing )
                {
                    output.Pause();
                    pauseButton.Visible = false;
                    Play.Visible = true;
                    customPBarPos.Value = 0;
                    customPBarPos.CurrentDuration = 0;
                }
                int selectedIndex = musicList.SelectedItems[0].Index;
                if (selectedIndex >= 0 && selectedIndex < songPaths.Count)
                    selectSong(selectedIndex);
            }
        }

        // Move forward selected song in the list
        private void moveSongFWDbtn_Click(object sender, EventArgs e)
        {
            if (musicList.SelectedItems.Count > 0)
            {
                int maxIndex = musicList.SelectedIndices[0] + 1;
                if (maxIndex >= musicList.Items.Count)
                    return;
                ListViewItem selected = musicList.SelectedItems[0];
                int currentIndex = selected.Index;
                musicList.Items.Remove(selected);

                //musicList.SelectedItems[0].Index++;
                int newIndex = currentIndex + 1;

                musicList.Items.Insert(newIndex, selected);
                musicList.Items[newIndex].Selected = true;


                //songPaths[currentIndex] = songPaths[currentIndex+1];
                string path = songPaths[currentIndex];
                songPaths[currentIndex] = songPaths[currentIndex+1];
                songPaths[currentIndex+1] = path;


                string SFN = SfileName[currentIndex];
                SfileName[currentIndex] = SfileName[currentIndex+1];
                SfileName[currentIndex+1] = SFN;

                selectSong(newIndex);

                // DEV
                //string btnText = musicList.SelectedItems[0].Index.ToString();
                //moveSongFWDbtn.Text = btnText;
            }
        }

        // Move backward selected song in the list
        private void moveSongBWDbtn_Click(object sender, EventArgs e)
        {
            if (musicList.SelectedItems.Count > 0)
            {
                int maxIndex = musicList.SelectedIndices[0] - 1;
                if (maxIndex < musicList.Items.Count && maxIndex >= 0)
                {
                    ListViewItem selected = musicList.SelectedItems[0];
                    int currentIndex = selected.Index;
                    musicList.Items.Remove(selected);

                    int newIndex = currentIndex - 1;

                    musicList.Items.Insert(newIndex, selected);
                    musicList.Items[newIndex].Selected = true;


                    string path = songPaths[currentIndex];
                    songPaths[currentIndex] = songPaths[currentIndex-1];
                    songPaths[currentIndex-1] = path;


                    string SFN = SfileName[currentIndex];
                    SfileName[currentIndex] = SfileName[currentIndex-1];
                    SfileName[currentIndex-1] = SFN;

                    selectSong(newIndex);
                }
            }
        }

        // DragEnter event management
        private void musicList_DragEnter(object? sender, DragEventArgs e)
        {
            if (sender == null)
                return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
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
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Filedrop logic
                foreach (string fileName in files)
                {
                    songPaths.Add(fileName);
                    string selectedFilePath = fileName;
                    var file = TagLib.File.Create(selectedFilePath);
                    string songTitle = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    string artist = file.Tag.FirstPerformer; // Artist
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

        private void getLyricsBtn_Click(object sender, EventArgs e)
        {
            string artistForLyr = artistBox.Text;
            string titleForLyr = titleBox.Text;
            if (!string.IsNullOrEmpty(artistForLyr) && !string.IsNullOrEmpty(titleForLyr))
            {
                string apiUrl = $"{LyricsApiUrl}{artistForLyr}/{titleForLyr}.html";
                //string apiUrl = $"{LyricsApiUrl}jasonderulo/whenlovesucks.html";
                //System.Diagnostics.Process.Start("https://www.google.com/");
                var uri = "https://www.google.com/";
                var psi = new System.Diagnostics.ProcessStartInfo();
                psi.UseShellExecute = true;
                psi.FileName = apiUrl;
                System.Diagnostics.Process.Start(psi);
            }
            else
            {
                MessageBox.Show($"Please enter the name of the artist and the track!", "Addinfo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private string lowerLettersForTitle(string input, string artist)
        {
            // Remove special characters
            string cleanedInput = Regex.Replace(input, "[^a-zA-Z0-9 ]", "");

            string artistLower = lowerLettersForArtist(artist);

            // Remove words
            string[] wordsToRemove = { "feat", "ft", "official", "audio", "video", "remix", "mix", "original", "lyrics", artistLower }; // List of words to remove
            string[] words = cleanedInput.Split(' ');
            string filteredWords = string.Join(" ", words.Where(word => !wordsToRemove.Contains(word.ToLower())));

            // We normalize characters and use lowercase letters
            string normalizedInput = filteredWords.Normalize(NormalizationForm.FormKD);
            string lowercaseInput = new string(normalizedInput
                .Select(c => Char.ToLowerInvariant(c))
                .ToArray());

            // Replace spaces with underscroll
            string finalResult = lowercaseInput.Replace(" ", "");

            return finalResult;
        }

        private string lowerLettersForArtist(string input)
        {
            // Filter only allowed characters (letters and numbers)
            string cleanedInput = new string(input
                .Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c))
                .ToArray());

            // We normalize characters and use lowercase letters.
            string normalizedInput = cleanedInput.Normalize(NormalizationForm.FormKD);
            string lowercaseInput = new string(normalizedInput
                .Where(c => Char.IsLetterOrDigit(c) || Char.IsWhiteSpace(c))
                .Select(c => Char.ToLowerInvariant(c))
                .ToArray());

            // Replace spaces with underscroll
            string finalResult = lowercaseInput.Replace(" ", "");

            return finalResult;
        }

        private void selectSong(int index)
        {
            if (index >= 0 && index < songPaths.Count)
            {
                string selectedFilePath = songPaths[index];
                var file = TagLib.File.Create(selectedFilePath);
                string title = file.Tag.Title; // Title
                string artist = file.Tag.FirstPerformer; // Artist
                int bitrate = (int)file.Properties.AudioBitrate; // Bitrate
                string bitrateStr = bitrate.ToString();
                //IPicture[] pictures = file.Tag.Pictures; // Cover
                IPicture[] pictures = null;
                try
                {
                    pictures = file.Tag.Pictures;
                }
                catch( Exception ex )
                {
                    // In case of error, we set default values
                    MessageBox.Show($"An error occurred while reading the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    coverBox.Image = coverBox.InitialImage; // Default picture
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
                            Image albumArtImage = Image.FromStream(memoryStream);
                            // Scaling the albumArtImage to the size of coverBox
                            albumArtImage = ResizeImage(albumArtImage, coverBox.Width, coverBox.Height);
                            coverBox.Image = albumArtImage;
                        }
                        catch
                        {coverBox.Image = coverBox.InitialImage;}
                    }
                }
                else
                    coverBox.Image = coverBox.InitialImage;

                // Lyrics
                if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
                {
                    artistBox.Text = lowerLettersForArtist(artist);
                    titleBox.Text = lowerLettersForTitle(title, artist);
                }
                /*if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
                {
                    //string apiUrl = $"{LyricsApiUrl}{formattedArtist}/{formattedTitle}.html";
                    string apiUrl = $"{LyricsApiUrl}jasonderulo/whenlovesucks.html";
                    //System.Diagnostics.Process.Start("https://www.google.com/");
                    var uri = "https://www.google.com/";
                    var psi = new System.Diagnostics.ProcessStartInfo();
                    psi.UseShellExecute = true;
                    psi.FileName = apiUrl;
                    System.Diagnostics.Process.Start(psi);
                }
                else
                {
                    MessageBox.Show("Please provide the artist and song title!");
                }*/

                //audioFileLoaded = true;
                DisposeWave(false);
                resetBandBars();
                //string selectedFileName = open.SafeFileName;
                stopped = false;

                //    if(!threadStarted)
                //        thread.Start();

                //    if(!scrollThreadStarted)
                //        timeScrollThread.Start();

                StreamReader? Read;
                Read = new StreamReader("eqsettings.txt");
                while (Read.Peek() > -1)
                    StringRead(Read.ReadLine());
                Read.Close();

                eqScroll();

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

                string fileExtension = System.IO.Path.GetExtension(SfileName[index]);

                if (fileExtension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var mp3PcmStream = NAudio.Wave.WaveFormatConversionStream.CreatePcmStream(new NAudio.Wave.Mp3FileReader(songPaths[index]));
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
                        MessageBox.Show($"Mp3FileReader error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        MessageBox.Show($"WaveFileReader error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                WaveChannel32 waveChannel = new WaveChannel32(pcm);
                var sampleChannel = new SampleChannel(waveChannel);
                equalizer = new NAudio.Extras.Equalizer(sampleChannel, bands);
                TimeSpan duration = this.pcm.TotalTime;
                double totalSeconds = this.pcm.TotalTime.TotalSeconds;
                int seconds = (int)totalSeconds;
                customPBarPos.Maximum = seconds;
                //output = new NAudio.Wave.DirectSoundOut();
                output = new NAu.Wave.WaveOutEvent(); // With this possible the sound volume controlling
                output.Volume = 1.0f; // Controlling the sound volume | 1.0 = 100%, 0.5 = 50%, 0.0 = 0% |

                float convertedBarVal = barToVol(customHorzScrollBar1.Pan);
                if (customHorzScrollBar1.Pan >= 0.0f) // Right panning. Control the left Vol
                    output.LeftVolume = convertedBarVal;
                else if (customHorzScrollBar1.Pan <= -0.0f) // Left panning. Control the right Vol
                    output.RightVolume = convertedBarVal;

                horzRollingLabel.Text = "Name: " + SfileName[index] +
                "    " +
                "Title: " + title +
                "    " +
                "Artist: " + artist
                + "    " +
                "Bitrate: " + bitrateStr +
                "kb/s    " +
                String.Format("Duration: {0:00}:{1:00}", (int)duration.TotalMinutes, duration.Seconds);

                //    output.Init(new SampleToWaveProvider(equalizer));

                //var sampleCh = new SampleChannel(waveChannel, true);
                setVolumeDelegate = vol => sampleChannel.Volume = vol;
                postVolumeMeter = new MeteringSampleProvider(equalizer);
                postVolumeMeter.StreamVolume += OnPostVolumeMeter;
                //output.Init(postVolumeMeter);
                try
                {
                    output.Init(postVolumeMeter);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"WaveOut initialization error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                //MessageBox.Show($"PCM Sample Rate: {pcm.WaveFormat.SampleRate}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //MessageBox.Show($"PCM Channels: {pcm.WaveFormat.Channels}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                //MessageBox.Show($"PCM Bits Per Sample: {pcm.WaveFormat.BitsPerSample}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);


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
            }
        }
        void OnPostVolumeMeter(object sender, StreamVolumeEventArgs e)
        {
            // we know it is stereo
            volumeMeter1.Amplitude = e.MaxSampleValues[0];
            volumeMeter2.Amplitude = e.MaxSampleValues[1];
        }

        private void customHorzScrollBar1_PanChanged(object sender, EventArgs e)
        {
            if (output == null)
                return;
            /*if( customHorzScrollBar1.Pan >= 0.0f ) // Right panning. Control the left Vol
            {
                output.LeftVolume = barToVol(customHorzScrollBar1.Pan);
                label18.Text = String.Format("{0}", output.LeftVolume);
            }
            else if( customHorzScrollBar1.Pan <= 0.0f ) // Left panning. Control the right Vol
            {
                output.RightVolume = barToVol(customHorzScrollBar1.Pan);
                label18.Text = String.Format("{0}", output.RightVolume);
            }*/

            float convertedBarVal = barToVol(customHorzScrollBar1.Pan);
            if (customHorzScrollBar1.Pan >= 0.0f) // Right panning. Control the left Vol
                output.LeftVolume = convertedBarVal;
            else if (customHorzScrollBar1.Pan <= -0.0f) // Left panning. Control the right Vol
                output.RightVolume = convertedBarVal;
        }

        private float barToVol(float barValue)
        {
            const float minValue = -10.0f;
            const float maxValue = 10.0f;

            // The linear interpolation operation is inverted to achieve the desired result
            float convertedValue = 1.0f - (Math.Abs(barValue) / maxValue);

            // Limit the value within the desired range
            return Math.Max(0.0f, Math.Min(1.0f, convertedValue));
        }

        /*private float barToVol(float barValue)
        {
            float convert = 1.0f;
            //float convert = (barValue + 10.0f) / 20.0f;
            if( barValue == 0.0f )
                convert = 1.0f;
            else if( barValue == 1.0f || barValue == -1.0f )
                convert = 0.9f;
            else if( barValue == 2.0f || barValue == -2.0f )
                convert = 0.8f;
            else if( barValue == 3.0f || barValue == -3.0f )
                convert = 0.7f;
            else if( barValue == 4.0f || barValue == -4.0f )
                convert = 0.6f;
            else if( barValue == 5.0f || barValue == -5.0f )
                convert = 0.5f;
            else if( barValue == 6.0f || barValue == -6.0f )
                convert = 0.4f;
            else if( barValue == 7.0f || barValue == -7.0f )
                convert = 0.3f;
            else if( barValue == 8.0f || barValue == -8.0f )
                convert = 0.2f;
            else if( barValue == 9.0f || barValue == -9.0f )
                convert = 0.1f;
            else if( barValue == 10.0f || barValue == -10.0f )
                convert = 0.0f;
            label18.Text = String.Format("{0}", convert);
            return convert;
        }*/

        private async void Play_Click(object sender, EventArgs e)
        {
            if (output.PlaybackState != NAudio.Wave.PlaybackState.Playing)
            {
                if (!incrementVol)
                {
                    setVolumeDelegate?.Invoke(0.0f);
                    incrementVol = true;
                    await IncrementVolume();
                    incrementVol = false;
                }
                stopped = false;
                pauseButton.Visible = true;
                Play.Visible = false;

                updateDiscordRPC_Playing();
            }
        }

        private async void pauseButton_Click(object sender, EventArgs e)
        {
            if (output != null)
            {
                if (output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                {
                    if (!decrementVol)
                    {
                        decrementVol = true;
                        await DecrementVolume();
                        decrementVol = false;
                    }
                }

                pauseButton.Visible = false;
                Play.Visible = true;

                updateDiscordRPC_Stop();
            }
        }

        private async Task IncrementVolume()
        {
            setVolumeDelegate?.Invoke(0.0f);
            await Task.Run(() =>
            {
                //setVolumeDelegate?.Invoke(0.0f);
                Task.Delay(1).Wait();
                output.Play();
                for (float i = 0.0f; i <= volumeSlider1.Volume; i += (volumeSlider1.Volume / 200.0f))
                {
                    setVolumeDelegate?.Invoke(i);
                    int delay = (int)(1 + 3 * Math.Pow(1 - (i / volumeSlider1.Volume), 2)); // 1-3 ms
                    Task.Delay(delay).Wait();
                    //Task.Delay(1).Wait();
                }
            });
        }

        private async Task DecrementVolume()
        {
            await Task.Run(() =>
            {
                for (float i = volumeSlider1.Volume; i >= 0.0f; i -= (volumeSlider1.Volume / 200.0f))
                {
                    setVolumeDelegate?.Invoke(i);
                    //int delay = (int)(1 + 5 * (i / volumeSlider1.Volume)); // 1-5 ms
                    //int delay = (int)(1 + Math.Log(1 + i) * 5); // 1-5 ms, logaritmikus
                    int delay = (int)(1 + 3 * Math.Pow(i / volumeSlider1.Volume, 2)); // 1-3 ms
                    Task.Delay(delay).Wait();
                    //Task.Delay(1).Wait();
                }
                output.Pause();
            });
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            if (output != null)
            {
                stopped = true;
                incrementVol = false;
                decrementVol = false;
                customPBarPos.Value = 0;
                customPBarPos.CurrentDuration = 0;
                this.pcm.CurrentTime = new TimeSpan(0);
                output.Stop();

                updateDiscordRPC_Stop();

                pauseButton.Visible = false;
                Play.Visible = true;
            }
        }

        private void deletePlaylist_Click(object sender, EventArgs e)
        {
            bool clear = false;
            Form popupForm = new Form();
            popupForm.Text = "You want delete all songs from playlist?";
            popupForm.Size = new Size(450, 200);
            popupForm.StartPosition = FormStartPosition.CenterParent;

            System.Windows.Forms.Button buttonYes = new System.Windows.Forms.Button();
            buttonYes.Text = "Yes";
            buttonYes.Location = new Point(60, 60);
            buttonYes.Click += (sender, e) =>
            {
                clear = true;
                popupForm.Close(); // Close the popup
            };
            popupForm.Controls.Add(buttonYes);

            System.Windows.Forms.Button buttonNo = new System.Windows.Forms.Button();
            buttonNo.Text = "No";
            buttonNo.Location = new Point(295, 60);
            buttonNo.Click += (sender, e) =>
            {
                popupForm.Close(); // Close the popup
            };
            popupForm.Controls.Add(buttonNo);

            popupForm.ShowDialog();

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
                coverBox.Image = coverBox.InitialImage;
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
            bool saveList = false;
            Form popupForm = new Form();
            popupForm.Text = "You want save the playlist?";
            popupForm.Size = new Size(350, 200);
            popupForm.StartPosition = FormStartPosition.CenterParent;

            System.Windows.Forms.Button buttonYes = new System.Windows.Forms.Button();
            buttonYes.Text = "Yes";
            buttonYes.Location = new Point(60, 60);
            buttonYes.Click += (sender, e) =>
            {
                saveList = true;
                popupForm.Close(); // Close the popup
            };
            popupForm.Controls.Add(buttonYes);

            System.Windows.Forms.Button buttonNo = new System.Windows.Forms.Button();
            buttonNo.Text = "No";
            buttonNo.Location = new Point(195, 60);
            buttonNo.Click += (sender, e) =>
            {
                popupForm.Close(); // Close the popup
            };
            popupForm.Controls.Add(buttonNo);

            popupForm.ShowDialog();

            if (!saveList)
                return;

            SaveSongsToFile();
        }

        private void button1_Click(object sender, EventArgs e) // Save EQ settings
        {
            bool saveEq = false;
            Form popupForm = new Form();
            popupForm.Text = "You want save the EQ settings?";
            popupForm.Size = new Size(350, 200);
            popupForm.StartPosition = FormStartPosition.CenterParent;

            System.Windows.Forms.Button buttonYes = new System.Windows.Forms.Button();
            buttonYes.Text = "Yes";
            buttonYes.Location = new Point(60, 60);
            buttonYes.Click += (sender, e) =>
            {
                saveEq = true;
                popupForm.Close(); // Close the popup
            };
            popupForm.Controls.Add(buttonYes);

            System.Windows.Forms.Button buttonNo = new System.Windows.Forms.Button();
            buttonNo.Text = "No";
            buttonNo.Location = new Point(195, 60);
            buttonNo.Click += (sender, e) =>
            {
                popupForm.Close(); // Close the popup
            };
            popupForm.Controls.Add(buttonNo);

            popupForm.ShowDialog();

            if (!saveEq)
                return;

            StreamWriter? Write;

            Write = new StreamWriter("eqsettings.txt");
            Write.WriteLine("eq1Value=" + eqValues[0].ToString());
            Write.WriteLine("eq1Scroll=" + eqScrollValues[0].ToString());
            Write.WriteLine("eq2Value=" + eqValues[1].ToString());
            Write.WriteLine("eq2Scroll=" + eqScrollValues[1].ToString());
            Write.WriteLine("eq3Value=" + eqValues[2].ToString());
            Write.WriteLine("eq3Scroll=" + eqScrollValues[2].ToString());
            Write.WriteLine("eq4Value=" + eqValues[3].ToString());
            Write.WriteLine("eq4Scroll=" + eqScrollValues[3].ToString());
            Write.WriteLine("eq5Value=" + eqValues[4].ToString());
            Write.WriteLine("eq5Scroll=" + eqScrollValues[4].ToString());
            Write.WriteLine("eq6Value=" + eqValues[5].ToString());
            Write.WriteLine("eq6Scroll=" + eqScrollValues[5].ToString());
            Write.WriteLine("eq7Value=" + eqValues[6].ToString());
            Write.WriteLine("eq7Scroll=" + eqScrollValues[6].ToString());
            Write.WriteLine("eq8Value=" + eqValues[7].ToString());
            Write.WriteLine("eq8Scroll=" + eqScrollValues[7].ToString());
            Write.WriteLine("eq9Value=" + eqValues[8].ToString());
            Write.WriteLine("eq9Scroll=" + eqScrollValues[8].ToString());
            Write.WriteLine("eq10Value=" + eqValues[9].ToString());
            Write.WriteLine("eq10Scroll=" + eqScrollValues[9].ToString());

            Write.WriteLine("bw1Value=" + bwValues[0].ToString());
            Write.WriteLine("bw1Scroll=" + bwScrollValues[0].ToString());
            Write.WriteLine("bw2Value=" + bwValues[1].ToString());
            Write.WriteLine("bw2Scroll=" + bwScrollValues[1].ToString());
            Write.WriteLine("bw3Value=" + bwValues[2].ToString());
            Write.WriteLine("bw3Scroll=" + bwScrollValues[2].ToString());
            Write.WriteLine("bw4Value=" + bwValues[3].ToString());
            Write.WriteLine("bw4Scroll=" + bwScrollValues[3].ToString());
            Write.WriteLine("bw5Value=" + bwValues[4].ToString());
            Write.WriteLine("bw5Scroll=" + bwScrollValues[4].ToString());
            Write.WriteLine("bw6Value=" + bwValues[5].ToString());
            Write.WriteLine("bw6Scroll=" + bwScrollValues[5].ToString());
            Write.WriteLine("bw7Value=" + bwValues[6].ToString());
            Write.WriteLine("bw7Scroll=" + bwScrollValues[6].ToString());
            Write.WriteLine("bw8Value=" + bwValues[7].ToString());
            Write.WriteLine("bw8Scroll=" + bwScrollValues[7].ToString());
            Write.WriteLine("bw9Value=" + bwValues[8].ToString());
            Write.WriteLine("bw9Scroll=" + bwScrollValues[8].ToString());
            Write.WriteLine("bw10Value=" + bwValues[9].ToString());
            Write.WriteLine("bw10Scroll=" + bwScrollValues[9].ToString());
            Write.Close();
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

        private void customPBarPos_PositionChanged(object sender, EventArgs e)
        {
            if (output == null)
                return;

            if (output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
            {
                output.Pause();
                this.pcm.CurrentTime = TimeSpan.FromSeconds(customPBarPos.Value);
                output.Play();

                updateDiscordRPC_Playing();
            }
            else
            {
                this.pcm.CurrentTime = TimeSpan.FromSeconds(customPBarPos.Value);

                updateDiscordRPC_Stop();
            }
        }

        private void updateDiscordRPC_Playing()
        {
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

        private void band1CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;
            int selectedValue = band1CustomBar.Value;

            // Convert selectedValue to the range between -10.0f and 10.0f
            float newGain = MapValue(selectedValue, band1CustomBar.Minimum, band1CustomBar.Maximum, -10.0f, 10.0f);
            //float newGain = Math.Min(selectedValue, 10.0f);
            bands[0].Gain = newGain;

            eqValues[0] = newGain;
            eqScrollValues[0] = selectedValue;

            float selValue = band1CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[0].Bandwidth = newBandwidth;
            bwValues[0] = newBandwidth;
            bwScrollValues[0] = selValue;

            equalizer.Update();
        }

        private void band2CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;

            int selectedValue = band2CustomBar.Value;
            float newGain = MapValue(selectedValue, band2CustomBar.Minimum, band2CustomBar.Maximum, -10.0f, 10.0f);
            bands[1].Gain = newGain;

            eqValues[1] = newGain;
            eqScrollValues[1] = selectedValue;

            float selValue = band2CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[1].Bandwidth = newBandwidth;
            bwValues[1] = newBandwidth;
            bwScrollValues[1] = selValue;

            equalizer.Update();
        }

        private void band3CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;

            int selectedValue = band3CustomBar.Value;
            float newGain = MapValue(selectedValue, band3CustomBar.Minimum, band3CustomBar.Maximum, -10.0f, 10.0f);
            bands[2].Gain = newGain;

            eqValues[2] = newGain;
            eqScrollValues[2] = selectedValue;

            float selValue = band3CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[2].Bandwidth = newBandwidth;
            bwValues[2] = newBandwidth;
            bwScrollValues[2] = selValue;

            equalizer.Update();
        }

        private void band4CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;

            int selectedValue = band4CustomBar.Value;
            float newGain = MapValue(selectedValue, band4CustomBar.Minimum, band4CustomBar.Maximum, -10.0f, 10.0f);
            bands[3].Gain = newGain;

            eqValues[3] = newGain;
            eqScrollValues[3] = selectedValue;

            float selValue = band4CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[3].Bandwidth = newBandwidth;
            bwValues[3] = newBandwidth;
            bwScrollValues[3] = selValue;

            equalizer.Update();
        }

        private void band5CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;

            int selectedValue = band5CustomBar.Value;
            float newGain = MapValue(selectedValue, band5CustomBar.Minimum, band5CustomBar.Maximum, -10.0f, 10.0f);
            bands[4].Gain = newGain;

            eqValues[4] = newGain;
            eqScrollValues[4] = selectedValue;

            float selValue = band5CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[4].Bandwidth = newBandwidth;
            bwValues[4] = newBandwidth;
            bwScrollValues[4] = selValue;

            equalizer.Update();
        }

        private void band6CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;

            int selectedValue = band6CustomBar.Value;
            float newGain = MapValue(selectedValue, band6CustomBar.Minimum, band6CustomBar.Maximum, -10.0f, 10.0f);
            bands[5].Gain = newGain;

            eqValues[5] = newGain;
            eqScrollValues[5] = selectedValue;

            float selValue = band6CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[5].Bandwidth = newBandwidth;
            bwValues[5] = newBandwidth;
            bwScrollValues[5] = selValue;

            equalizer.Update();
        }

        private void band7CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;

            int selectedValue = band7CustomBar.Value;
            float newGain = MapValue(selectedValue, band7CustomBar.Minimum, band7CustomBar.Maximum, -10.0f, 10.0f);
            bands[6].Gain = newGain;

            eqValues[6] = newGain;
            eqScrollValues[6] = selectedValue;

            float selValue = band7CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[6].Bandwidth = newBandwidth;
            bwValues[6] = newBandwidth;
            bwScrollValues[6] = selValue;

            equalizer.Update();
        }

        private void band8CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;

            int selectedValue = band8CustomBar.Value;
            float newGain = MapValue(selectedValue, band8CustomBar.Minimum, band8CustomBar.Maximum, -10.0f, 10.0f);
            bands[7].Gain = newGain;

            eqValues[7] = newGain;
            eqScrollValues[7] = selectedValue;

            float selValue = band8CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[7].Bandwidth = newBandwidth;
            bwValues[7] = newBandwidth;
            bwScrollValues[7] = selValue;

            equalizer.Update();
        }

        private void band9CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;

            int selectedValue = band9CustomBar.Value;
            float newGain = MapValue(selectedValue, band9CustomBar.Minimum, band9CustomBar.Maximum, -10.0f, 10.0f);
            bands[8].Gain = newGain;

            eqValues[8] = newGain;
            eqScrollValues[8] = selectedValue;

            float selValue = band9CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[8].Bandwidth = newBandwidth;
            bwValues[8] = newBandwidth;
            bwScrollValues[8] = selValue;

            equalizer.Update();
        }

        private void band10CustomBar_ValueChanged(object sender, EventArgs e)
        {
            if (equalizer == null)
                return;

            int selectedValue = band10CustomBar.Value;
            float newGain = MapValue(selectedValue, band10CustomBar.Minimum, band10CustomBar.Maximum, -10.0f, 10.0f);
            bands[9].Gain = newGain;

            eqValues[9] = newGain;
            eqScrollValues[9] = selectedValue;

            float selValue = band10CustomBar.BandW;
            float newBandwidth = ConvertAndInvert(selValue);
            bands[9].Bandwidth = newBandwidth;
            bwValues[9] = newBandwidth;
            bwScrollValues[9] = selValue;

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

            Write = new StreamWriter("eqsettings.txt");
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

            Write.WriteLine("bw1Value=0,1");
            Write.WriteLine("bw1Scroll=1");
            Write.WriteLine("bw2Value=0,1");
            Write.WriteLine("bw2Scroll=1");
            Write.WriteLine("bw3Value=0,1");
            Write.WriteLine("bw3Scroll=1");
            Write.WriteLine("bw4Value=0,1");
            Write.WriteLine("bw4Scroll=1");
            Write.WriteLine("bw5Value=0,1");
            Write.WriteLine("bw5Scroll=1");
            Write.WriteLine("bw6Value=0,1");
            Write.WriteLine("bw6Scroll=1");
            Write.WriteLine("bw7Value=0,1");
            Write.WriteLine("bw7Scroll=1");
            Write.WriteLine("bw8Value=0,1");
            Write.WriteLine("bw8Scroll=1");
            Write.WriteLine("bw9Value=0,1");
            Write.WriteLine("bw9Scroll=1");
            Write.WriteLine("bw10Value=0,1");
            Write.WriteLine("bw10Scroll=1");
            Write.Close();
        }

        private void StringRead(string buffer)
        {
            // Equalizer
            if (buffer.StartsWith("eq1Value="))
            {
                float eqVal = float.Parse(buffer.Substring(9));
                eqValues[0] = eqVal;
            }
            if (buffer.StartsWith("eq1Scroll="))
            {
                int eqScroll = int.Parse(buffer.Substring(10));
                eqScrollValues[0] = eqScroll;
            }
            if (buffer.StartsWith("eq2Value="))
            {
                float eq2Val = float.Parse(buffer.Substring(9));
                eqValues[1] = eq2Val;
            }
            if (buffer.StartsWith("eq2Scroll="))
            {
                int eq2Scroll = int.Parse(buffer.Substring(10));
                eqScrollValues[1] = eq2Scroll;
            }
            if (buffer.StartsWith("eq3Value="))
            {
                float eq3Val = float.Parse(buffer.Substring(9));
                eqValues[2] = eq3Val;
            }
            if (buffer.StartsWith("eq3Scroll="))
            {
                int eq3Scroll = int.Parse(buffer.Substring(10));
                eqScrollValues[2] = eq3Scroll;
            }
            if (buffer.StartsWith("eq4Value="))
            {
                float eq4Val = float.Parse(buffer.Substring(9));
                eqValues[3] = eq4Val;
            }
            if (buffer.StartsWith("eq4Scroll="))
            {
                int eq4Scroll = int.Parse(buffer.Substring(10));
                eqScrollValues[3] = eq4Scroll;
            }
            if (buffer.StartsWith("eq5Value="))
            {
                float eq5Val = float.Parse(buffer.Substring(9));
                eqValues[4] = eq5Val;
            }
            if (buffer.StartsWith("eq5Scroll="))
            {
                int eq5Scroll = int.Parse(buffer.Substring(10));
                eqScrollValues[4] = eq5Scroll;
            }
            if (buffer.StartsWith("eq6Value="))
            {
                float eq6Val = float.Parse(buffer.Substring(9));
                eqValues[5] = eq6Val;
            }
            if (buffer.StartsWith("eq6Scroll="))
            {
                int eq6Scroll = int.Parse(buffer.Substring(10));
                eqScrollValues[5] = eq6Scroll;
            }
            if (buffer.StartsWith("eq7Value="))
            {
                float eq7Val = float.Parse(buffer.Substring(9));
                eqValues[6] = eq7Val;
            }
            if (buffer.StartsWith("eq7Scroll="))
            {
                int eq7Scroll = int.Parse(buffer.Substring(10));
                eqScrollValues[6] = eq7Scroll;
            }
            if (buffer.StartsWith("eq8Value="))
            {
                float eq8Val = float.Parse(buffer.Substring(9));
                eqValues[7] = eq8Val;
            }
            if (buffer.StartsWith("eq8Scroll="))
            {
                int eq8Scroll = int.Parse(buffer.Substring(10));
                eqScrollValues[7] = eq8Scroll;
            }
            if (buffer.StartsWith("eq9Value="))
            {
                float eq9Val = float.Parse(buffer.Substring(9));
                eqValues[8] = eq9Val;
            }
            if (buffer.StartsWith("eq9Scroll="))
            {
                int eq9Scroll = int.Parse(buffer.Substring(10));
                eqScrollValues[8] = eq9Scroll;
            }
            if (buffer.StartsWith("eq10Value="))
            {
                float eq10Val = float.Parse(buffer.Substring(10));
                eqValues[9] = eq10Val;
            }
            if (buffer.StartsWith("eq10Scroll="))
            {
                int eq10Scroll = int.Parse(buffer.Substring(11));
                eqScrollValues[9] = eq10Scroll;
            }
            // Bandwidth
            if (buffer.StartsWith("bw1Value="))
            {
                float bwVal = float.Parse(buffer.Substring(9));
                bwValues[0] = bwVal;
            }
            if (buffer.StartsWith("bw1Scroll="))
            {
                float bwScroll = float.Parse(buffer.Substring(10));
                bwScrollValues[0] = bwScroll;
            }
            if (buffer.StartsWith("bw2Value="))
            {
                float bw2Val = float.Parse(buffer.Substring(9));
                bwValues[1] = bw2Val;
            }
            if (buffer.StartsWith("bw2Scroll="))
            {
                float bw2Scroll = float.Parse(buffer.Substring(10));
                bwScrollValues[1] = bw2Scroll;
            }
            if (buffer.StartsWith("bw3Value="))
            {
                float bw3Val = float.Parse(buffer.Substring(9));
                bwValues[2] = bw3Val;
            }
            if (buffer.StartsWith("bw3Scroll="))
            {
                float bw3Scroll = float.Parse(buffer.Substring(10));
                bwScrollValues[2] = bw3Scroll;
            }
            if (buffer.StartsWith("bw4Value="))
            {
                float bw4Val = float.Parse(buffer.Substring(9));
                bwValues[3] = bw4Val;
            }
            if (buffer.StartsWith("bw4Scroll="))
            {
                float bw4Scroll = float.Parse(buffer.Substring(10));
                bwScrollValues[3] = bw4Scroll;
            }
            if (buffer.StartsWith("bw5Value="))
            {
                float bw5Val = float.Parse(buffer.Substring(9));
                bwValues[4] = bw5Val;
            }
            if (buffer.StartsWith("bw5Scroll="))
            {
                float bw5Scroll = float.Parse(buffer.Substring(10));
                bwScrollValues[4] = bw5Scroll;
            }
            if (buffer.StartsWith("bw6Value="))
            {
                float bw6Val = float.Parse(buffer.Substring(9));
                bwValues[5] = bw6Val;
            }
            if (buffer.StartsWith("bw6Scroll="))
            {
                float bw6Scroll = float.Parse(buffer.Substring(10));
                bwScrollValues[5] = bw6Scroll;
            }
            if (buffer.StartsWith("bw7Value="))
            {
                float bw7Val = float.Parse(buffer.Substring(9));
                bwValues[6] = bw7Val;
            }
            if (buffer.StartsWith("bw7Scroll="))
            {
                float bw7Scroll = float.Parse(buffer.Substring(10));
                bwScrollValues[6] = bw7Scroll;
            }
            if (buffer.StartsWith("bw8Value="))
            {
                float bw8Val = float.Parse(buffer.Substring(9));
                bwValues[7] = bw8Val;
            }
            if (buffer.StartsWith("bw8Scroll="))
            {
                float bw8Scroll = float.Parse(buffer.Substring(10));
                bwScrollValues[7] = bw8Scroll;
            }
            if (buffer.StartsWith("bw9Value="))
            {
                float bw9Val = float.Parse(buffer.Substring(9));
                bwValues[8] = bw9Val;
            }
            if (buffer.StartsWith("bw9Scroll="))
            {
                float bw9Scroll = float.Parse(buffer.Substring(10));
                bwScrollValues[8] = bw9Scroll;
            }
            if (buffer.StartsWith("bw10Value="))
            {
                float bw10Val = float.Parse(buffer.Substring(10));
                bwValues[9] = bw10Val;
            }
            if (buffer.StartsWith("bw10Scroll="))
            {
                float bw10Scroll = float.Parse(buffer.Substring(11));
                bwScrollValues[9] = bw10Scroll;
            }
        }

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

        /// <summary>
        /// LeftVolume for this device 1.0 is full scale
        /// </summary>
        public float LeftVolume
        {
            get => WaveOutUtils.GetWaveOutLeftVolume(hWaveOut, waveOutLock);
            set => WaveOutUtils.SetWaveOutLeftVolume(value, hWaveOut, waveOutLock);
        }

        /// <summary>
        /// RightVolume for this device 1.0 is full scale
        /// </summary>
        public float RightVolume
        {
            get => WaveOutUtils.GetWaveOutRightVolume(hWaveOut, waveOutLock);
            set => WaveOutUtils.SetWaveOutRightVolume(value, hWaveOut, waveOutLock);
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
        private static float getStereoVol;

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

        public static float GetWaveOutLeftVolume(IntPtr hWaveOut, object lockObject)
        {
            int stereoVolume;
            MmResult result;
            lock (lockObject)
            {
                result = WaveInterop.waveOutGetVolume(hWaveOut, out stereoVolume);
            }
            MmException.Try(result, "waveOutGetVolume");
            // We separate the left and right channel values.
            int left = stereoVolume & 0xFFFF;

            // We normalize the range and convert it to float type.
            float leftVolume = left / (float)0xFFFF;

            return leftVolume;
            //return (leftVolume & 0xFFFF) / (float)0xFFFF;
        }

        public static float GetWaveOutRightVolume(IntPtr hWaveOut, object lockObject)
        {
            int stereoVolume;
            MmResult result;
            lock (lockObject)
            {
                result = WaveInterop.waveOutGetVolume(hWaveOut, out stereoVolume);
            }
            MmException.Try(result, "waveOutGetVolume");
            // We separate the left and right channel values.
            int right = (stereoVolume >> 16) & 0xFFFF;

            // We normalize the range and convert it to float type.
            float rightVolume = right / (float)0xFFFF;

            return rightVolume;
            //return (rightVolume & 0xFFFF) / (float)0xFFFF;
        }

        public static void SetWaveOutVolume(float value, IntPtr hWaveOut, object lockObject)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            if (value > 1) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            float left = value;
            float right = value;
            getStereoVol = value;

            /*if( GetWaveOutLeftVolume(hWaveOut, lockObject) != GetWaveOutRightVolume(hWaveOut, lockObject) || GetWaveOutRightVolume(hWaveOut, lockObject) != GetWaveOutLeftVolume(hWaveOut, lockObject) )
            {
                left = GetWaveOutLeftVolume(hWaveOut, lockObject);
                right = GetWaveOutRightVolume(hWaveOut, lockObject);
            }*/
            int stereoVolume = (int)(left * 0xFFFF) + ((int)(right * 0xFFFF) << 16);
            MmResult result;
            lock (lockObject)
            {
                result = WaveInterop.waveOutSetVolume(hWaveOut, stereoVolume);
            }
            MmException.Try(result, "waveOutSetVolume");
        }

        public static void SetWaveOutLeftVolume(float value, IntPtr hWaveOut, object lockObject)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            if (value > 1) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            float left = value;
            float right = getStereoVol;

            left = Math.Max(0.0f, Math.Min(value, getStereoVol));

            int leftVolume = (int)(left * 0xFFFF) + ((int)(right * 0xFFFF) << 16);
            MmResult result;
            lock (lockObject)
            {
                result = WaveInterop.waveOutSetVolume(hWaveOut, leftVolume);
            }
            MmException.Try(result, "waveOutSetVolume");
        }

        public static void SetWaveOutRightVolume(float value, IntPtr hWaveOut, object lockObject)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            if (value > 1) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0.0 and 1.0");
            float left = getStereoVol;
            float right = value;

            right = Math.Max(0.0f, Math.Min(value, getStereoVol));

            int rightVolume = (int)(left * 0xFFFF) + ((int)(right * 0xFFFF) << 16);
            MmResult result;
            lock (lockObject)
            {
                result = WaveInterop.waveOutSetVolume(hWaveOut, rightVolume);
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
/*
    /// <summary>
    /// Class for reading from MP3 files
    /// </summary>
    public class Mp3FileReader : Mp3FileReaderBase
    {
        /// <summary>Supports opening a MP3 file</summary>
        public Mp3FileReader(string mp3FileName)
            : base(System.IO.File.OpenRead(mp3FileName), CreateAcmFrameDecompressor, true)
        {
        }

        /// <summary>
        /// Opens MP3 from a stream rather than a file
        /// Will not dispose of this stream itself
        /// </summary>
        /// <param name="inputStream">The incoming stream containing MP3 data</param>
        public Mp3FileReader(Stream inputStream)
            : base(inputStream, CreateAcmFrameDecompressor, false)
        {

        }

        /// <summary>
        /// Creates an ACM MP3 Frame decompressor. This is the default with NAudio
        /// </summary>
        /// <param name="mp3Format">A WaveFormat object based </param>
        /// <returns></returns>
        public static IMp3FrameDecompressor CreateAcmFrameDecompressor(WaveFormat mp3Format)
        {
            // new DmoMp3FrameDecompressor(this.Mp3WaveFormat); 
            return new AcmMp3FrameDecompressor(mp3Format);
        }
    }

    class Mp3Index
    {
        public long FilePosition { get; set; }
        public long SamplePosition { get; set; }
        public int SampleCount { get; set; }
        public int ByteCount { get; set; }
    }

    /// <summary>
    /// Class for reading from MP3 files
    /// </summary>
    public class Mp3FileReaderBase : WaveStream
    {
        private WaveFormat waveFormat; // Nem readonly
        private int bytesPerSample;    // Nem readonly
        private int bytesPerDecodedFrame;
        private byte[] decompressBuffer;
        private readonly FrameDecompressorBuilder frameDecompressorBuilder; // Új mező

//        private readonly WaveFormat waveFormat;
        private Stream mp3Stream;
        private readonly long mp3DataLength;
        private readonly long dataStartPosition;
        
        /// <summary>
        /// The MP3 wave format (n.b. NOT the output format of this stream - see the WaveFormat property)
        /// </summary>
        public Mp3WaveFormat Mp3WaveFormat { get; private set; }

        private readonly XingHeader xingHeader;
        private readonly bool ownInputStream;

        private List<Mp3Index> tableOfContents;
        private int tocIndex;

        private long totalSamples;
//        private readonly int bytesPerSample;
//        private readonly int bytesPerDecodedFrame;

        private IMp3FrameDecompressor decompressor;
        
//        private readonly byte[] decompressBuffer;
        private int decompressBufferOffset;
        private int decompressLeftovers;
        private bool repositionedFlag;

        private long position; // decompressed data position tracker

        private readonly object repositionLock = new object();


        /// <summary>Supports opening a MP3 file</summary>
        /// <param name="mp3FileName">MP3 File name</param>
        /// <param name="frameDecompressorBuilder">Factory method to build a frame decompressor</param>
        public Mp3FileReaderBase(string mp3FileName, FrameDecompressorBuilder frameDecompressorBuilder)
            : this(System.IO.File.OpenRead(mp3FileName), frameDecompressorBuilder, true)
        {
        }



        /// <summary>
        /// Opens MP3 from a stream rather than a file
        /// Will not dispose of this stream itself
        /// </summary>
        /// <param name="inputStream">The incoming stream containing MP3 data</param>
        /// <param name="frameDecompressorBuilder">Factory method to build a frame decompressor</param>
        public Mp3FileReaderBase(Stream inputStream, FrameDecompressorBuilder frameDecompressorBuilder)
            : this(inputStream, frameDecompressorBuilder, false)
        {
            
        }

        protected Mp3FileReaderBase(Stream inputStream, FrameDecompressorBuilder frameDecompressorBuilder, bool ownInputStream)
        {
            if (inputStream == null) throw new ArgumentNullException(nameof(inputStream));
            if (frameDecompressorBuilder == null) throw new ArgumentNullException(nameof(frameDecompressorBuilder));
            this.ownInputStream = ownInputStream;
            try
            {
                mp3Stream = inputStream;
                Id3v2Tag = Id3v2Tag.ReadTag(mp3Stream);

                dataStartPosition = mp3Stream.Position;
                var firstFrame = Mp3Frame.LoadFromStream(mp3Stream);
                if (firstFrame == null)
                    throw new InvalidDataException("Invalid MP3 file - no MP3 Frames Detected");
                double bitRate = firstFrame.BitRate;
                xingHeader = XingHeader.LoadXingHeader(firstFrame);
                // If the header exists, we can skip over it when decoding the rest of the file
                if (xingHeader != null) dataStartPosition = mp3Stream.Position;

                // workaround for a longstanding issue with some files failing to load
                // because they report a spurious sample rate change
                var secondFrame = Mp3Frame.LoadFromStream(mp3Stream);
                if (secondFrame != null &&
                    (secondFrame.SampleRate != firstFrame.SampleRate ||
                     secondFrame.ChannelMode != firstFrame.ChannelMode))
                {
                    // assume that the first frame was some kind of VBR/LAME header that we failed to recognise properly
                    dataStartPosition = secondFrame.FileOffset;
                    // forget about the first frame, the second one is the first one we really care about
                    firstFrame = secondFrame;
                }

                mp3DataLength = mp3Stream.Length - dataStartPosition;

                // try for an ID3v1 tag as well
                mp3Stream.Position = mp3Stream.Length - 128;
                byte[] tag = new byte[128];
                mp3Stream.Read(tag, 0, 128);
                if (tag[0] == 'T' && tag[1] == 'A' && tag[2] == 'G')
                {
                    Id3v1Tag = tag;
                    mp3DataLength -= 128;
                }

                mp3Stream.Position = dataStartPosition;

                // create a temporary MP3 format before we know the real bitrate
                Mp3WaveFormat = new Mp3WaveFormat(firstFrame.SampleRate,
                    firstFrame.ChannelMode == ChannelMode.Mono ? 1 : 2, firstFrame.FrameLength, (int) bitRate);

                CreateTableOfContents();
                tocIndex = 0;

                // [Bit rate in Kilobits/sec] = [Length in kbits] / [time in seconds] 
                //                            = [Length in bits ] / [time in milliseconds]

                // Note: in audio, 1 kilobit = 1000 bits.
                // Calculated as a double to minimize rounding errors
                bitRate = (mp3DataLength*8.0/TotalSeconds());

                mp3Stream.Position = dataStartPosition;

                // now we know the real bitrate we can create an accurate MP3 WaveFormat
                Mp3WaveFormat = new Mp3WaveFormat(firstFrame.SampleRate,
                    firstFrame.ChannelMode == ChannelMode.Mono ? 1 : 2, firstFrame.FrameLength, (int) bitRate);
                decompressor = frameDecompressorBuilder(Mp3WaveFormat);
                this.waveFormat = decompressor.OutputFormat;
                this.bytesPerSample = (decompressor.OutputFormat.BitsPerSample)/8*decompressor.OutputFormat.Channels;
                // no MP3 frames have more than 1152 samples in them
                this.bytesPerDecodedFrame = 1152 * bytesPerSample;
                // some MP3s I seem to get double
                this.decompressBuffer = new byte[bytesPerDecodedFrame * 2];
            }
            catch (Exception)
            {
                if (ownInputStream) inputStream.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Function that can create an MP3 Frame decompressor
        /// </summary>
        /// <param name="mp3Format">A WaveFormat object describing the MP3 file format</param>
        /// <returns>An MP3 Frame decompressor</returns>
        public delegate IMp3FrameDecompressor FrameDecompressorBuilder(WaveFormat mp3Format);

        private void CreateTableOfContents()
        {
            try
            {
                // Just a guess at how many entries we'll need so the internal array need not resize very much
                // 400 bytes per frame is probably a good enough approximation.
                tableOfContents = new List<Mp3Index>((int)(mp3DataLength / 400));
                Mp3Frame frame;
                do
                {
                    var index = new Mp3Index();
                    index.FilePosition = mp3Stream.Position;
                    index.SamplePosition = totalSamples;
                    frame = ReadNextFrame(false);
                    if (frame != null)
                    {
                        ReFrameFormat(frame);
                        ValidateFrameFormat(frame);

                        totalSamples += frame.SampleCount;
                        index.SampleCount = frame.SampleCount;
                        index.ByteCount = (int)(mp3Stream.Position - index.FilePosition);
                        tableOfContents.Add(index);
                    }
                } while (frame != null);
            }
            catch (EndOfStreamException)
            {
                // not necessarily a problem
            }
        }

        private void ValidateFrameFormat(Mp3Frame frame)
        {
            if (frame.SampleRate != Mp3WaveFormat.SampleRate)
            {
                string message =
                    String.Format(
                        "Got a frame at sample rate {0}, in an MP3 with sample rate {1}. Mp3FileReader does not support sample rate changes.",
                        frame.SampleRate, Mp3WaveFormat.SampleRate);
                throw new InvalidOperationException(message);
            }
            int channels = frame.ChannelMode == ChannelMode.Mono ? 1 : 2;
            if (channels != Mp3WaveFormat.Channels)
            {
                string message =
                    String.Format(
                        "Got a frame with channel mode {0}, in an MP3 with {1} channels. Mp3FileReader does not support changes to channel count.",
                        frame.ChannelMode, Mp3WaveFormat.Channels);
                throw new InvalidOperationException(message);
            }
        }

        private void ReFrameFormat(Mp3Frame frame)
        {
            if (frame.SampleRate != Mp3WaveFormat.SampleRate)
            {
                int newChannels = frame.ChannelMode == ChannelMode.Mono ? 1 : 2;
                if (frame.SampleRate != decompressor.OutputFormat.SampleRate ||
                    newChannels != decompressor.OutputFormat.Channels)
                {
                    // Új decompressort hozunk létre
                    if (decompressor != null)
                    {
                        decompressor.Dispose();
                        decompressor = null;
                    }
                    Mp3WaveFormat = new Mp3WaveFormat(frame.SampleRate, newChannels, frame.FrameLength, frame.BitRate);
                    decompressor = frameDecompressorBuilder(Mp3WaveFormat);
                    this.waveFormat = decompressor.OutputFormat;
                    this.bytesPerSample = waveFormat.BitsPerSample / 8 * waveFormat.Channels;
                    // no MP3 frames have more than 1152 samples in them
                    this.bytesPerDecodedFrame = 1152 * bytesPerSample;
                    // some MP3s I seem to get double
                    this.decompressBuffer = new byte[bytesPerDecodedFrame * 2];
                }
            }
        }

        /// <summary>
        /// Gets the total length of this file in milliseconds.
        /// </summary>
        private double TotalSeconds()
        {
            return (double)totalSamples / Mp3WaveFormat.SampleRate;
        }

        /// <summary>
        /// ID3v2 tag if present
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public Id3v2Tag Id3v2Tag { get; }

        /// <summary>
        /// ID3v1 tag if present
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public byte[] Id3v1Tag { get; }

        /// <summary>
        /// Reads the next mp3 frame
        /// </summary>
        /// <returns>Next mp3 frame, or null if EOF</returns>
        public Mp3Frame ReadNextFrame()
        {
            var frame = ReadNextFrame(true);
            if (frame != null) position += frame.SampleCount*bytesPerSample;
            return frame;
        }

        /// <summary>
        /// Reads the next mp3 frame
        /// </summary>
        /// <returns>Next mp3 frame, or null if EOF</returns>
        private Mp3Frame ReadNextFrame(bool readData)
        {
            Mp3Frame frame = null;
            try
            {
                frame = Mp3Frame.LoadFromStream(mp3Stream, readData);
                if (frame != null)
                {
                    tocIndex++;
                }
            }
            catch (EndOfStreamException)
            {
                // suppress for now - it means we unexpectedly got to the end of the stream
                // half way through
            }
            return frame;
        }

        /// <summary>
        /// This is the length in bytes of data available to be read out from the Read method
        /// (i.e. the decompressed MP3 length)
        /// n.b. this may return 0 for files whose length is unknown
        /// </summary>
        public override long Length => totalSamples * bytesPerSample;

        /// <summary>
        /// <see cref="WaveStream.WaveFormat"/>
        /// </summary>
        public override WaveFormat WaveFormat => waveFormat;

        /// <summary>
        /// <see cref="Stream.Position"/>
        /// </summary>
        public override long Position
        {
            get
            {
                return position;
            }
            set
            {
                lock (repositionLock)
                {
                    value = Math.Max(Math.Min(value, Length), 0);
                    var samplePosition = value / bytesPerSample;
                    Mp3Index mp3Index = null;
                    for (int index = 0; index < tableOfContents.Count; index++)
                    {
                        if (tableOfContents[index].SamplePosition + tableOfContents[index].SampleCount > samplePosition)
                        {
                            mp3Index = tableOfContents[index];
                            tocIndex = index;
                            break;
                        }
                    }

                    decompressBufferOffset = 0;
                    decompressLeftovers = 0;
                    repositionedFlag = true;

                    if (mp3Index != null)
                    {
                        // perform the reposition
                        mp3Stream.Position = mp3Index.FilePosition;

                        // set the offset into the buffer (that is yet to be populated in Read())
                        var frameOffset = samplePosition - mp3Index.SamplePosition;
                        if (frameOffset > 0)
                        {
                            decompressBufferOffset = (int)frameOffset * bytesPerSample;
                        }
                    }
                    else
                    {
                        // we are repositioning to the end of the data
                        mp3Stream.Position = mp3DataLength + dataStartPosition;
                    }

                    position = value;
                }
            }
        }

        /// <summary>
        /// Reads decompressed PCM data from our MP3 file.
        /// </summary>
        public override int Read(byte[] sampleBuffer, int offset, int numBytes)
        {
            int bytesRead = 0;
            lock (repositionLock)
            {
                if (decompressLeftovers != 0)
                {
                    int toCopy = Math.Min(decompressLeftovers, numBytes);
                    Array.Copy(decompressBuffer, decompressBufferOffset, sampleBuffer, offset, toCopy);
                    decompressLeftovers -= toCopy;
                    if (decompressLeftovers == 0)
                    {
                        decompressBufferOffset = 0;
                    }
                    else
                    {
                        decompressBufferOffset += toCopy;
                    }
                    bytesRead += toCopy;
                    offset += toCopy;
                }

                int targetTocIndex = tocIndex; // the frame index that contains the requested data

                if (repositionedFlag)
                {
                    decompressor.Reset();

                    // Seek back a few frames of the stream to get the reset decoder decode a few
                    // warm-up frames before reading the requested data. Without the warm-up phase,
                    // the first half of the frame after the reset is attenuated and does not resemble
                    // the data as it would be when reading sequentially from the beginning, because 
                    // the decoder is missing the required overlap from the previous frame.
                    tocIndex = Math.Max(0, tocIndex - 3); // no warm-up at the beginning of the stream
                    mp3Stream.Position = tableOfContents[tocIndex].FilePosition;

                    repositionedFlag = false;
                }

                while (bytesRead < numBytes)
                {
                    Mp3Frame frame = ReadNextFrame(true); // internal read - should not advance position
                    if (frame != null)
                    {
                        int decompressed = decompressor.DecompressFrame(frame, decompressBuffer, 0);

                        if (tocIndex <= targetTocIndex || decompressed == 0)
                        {
                            // The first frame after a reset usually does not immediately yield decoded samples.
                            // Because the next instructions will fail if a buffer offset is set and the frame 
                            // decoding didn't return data, we skip the part.
                            // We skip the following instructions also after decoding a warm-up frame.
                            continue;
                        }
                        // Two special cases can happen here:
                        // 1. We are interested in the first frame of the stream, but need to read the second frame too
                        //    for the decoder to return decoded data
                        // 2. We are interested in the second frame of the stream, but because reading the first frame
                        //    as warm-up didn't yield any data (because the decoder needs two frames to return data), we
                        //    get data from the first and second frame. 
                        //    This case needs special handling, and we have to purge the data of the first frame.
                        else if (tocIndex == targetTocIndex + 1 && decompressed == bytesPerDecodedFrame * 2)
                        {
                            // Purge the first frame's data
                            Array.Copy(decompressBuffer, bytesPerDecodedFrame, decompressBuffer, 0, bytesPerDecodedFrame);
                            decompressed = bytesPerDecodedFrame;
                        }

                        int toCopy = Math.Min(decompressed - decompressBufferOffset, numBytes - bytesRead);
                        Array.Copy(decompressBuffer, decompressBufferOffset, sampleBuffer, offset, toCopy);
                        if ((toCopy + decompressBufferOffset) < decompressed)
                        {
                            decompressBufferOffset = toCopy + decompressBufferOffset;
                            decompressLeftovers = decompressed - decompressBufferOffset;
                        }
                        else
                        {
                            // no lefovers
                            decompressBufferOffset = 0;
                            decompressLeftovers = 0;
                        }
                        offset += toCopy;
                        bytesRead += toCopy;
                    }
                    else
                    {
                        break;
                    }
                }
            }
//            Debug.Assert(bytesRead <= numBytes, "MP3 File Reader read too much");
            position += bytesRead;
            return bytesRead;
        }

        /// <summary>
        /// Xing header if present
        /// </summary>
        public XingHeader XingHeader => xingHeader;

        /// <summary>
        /// Disposes this WaveStream
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (mp3Stream != null)
                {
                    if (ownInputStream)
                    {
                        mp3Stream.Dispose();
                    }
                    mp3Stream = null;
                }
                if (decompressor != null)
                {
                    decompressor.Dispose();
                    decompressor = null;
                }
            }
            base.Dispose(disposing);
        }
    }*/
}