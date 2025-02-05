﻿#region License

/* Copyright (c) 2018 Fabrice Lacharme
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy 
 * of this software and associated documentation files (the "Software"), to 
 * deal in the Software without restriction, including without limitation the 
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or 
 * sell copies of the Software, and to permit persons to whom the Software is 
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in 
 * all copies or substantial portions of the Software. 
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
 * THE SOFTWARE.
 */

#endregion

#region Contact

/*
 * Fabrice Lacharme
 * Email: fabrice.lacharme@gmail.com
 */

#endregion
/*
Modules nécessaires
BASS :
bass.dll
bass.Net.dll
bass_fx.dll
*/
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Un4seen.Bass;
using CDGNet;
using MP3GConverter;
using System.IO;
using System.Text.RegularExpressions;
using AudioControl;
using static Un4seen.Bass.Misc.WaveForm.WaveBuffer;

namespace Karaboss
{
    public partial class frmCDGPlayer : Form
    {


        #region "Private Declarations"

        private CDGFile mCDGFile;

        private long cdgpos = 0;

        //private CdgFileIoStream mCDGStream;
        //private int mSemitones = 0;
        private bool mPaused;
        private long mFrameCount = 0;
        private bool mStop = true;
        private string mCDGFileName;
        private string mMP3FileName;
        private string mTempDir;
        private int mMP3Stream;
        private frmCDGWindow mCDGWindow = new frmCDGWindow();
        //private frmExportCDG2AVI mExportForm;

        private bool scrolling = false;
        int newstart = 0;

        /// <summary>
        /// Player status
        /// </summary>
        private enum PlayerStates
        {
            Playing,
            Paused,
            Stopped,
            NextSong,           // select next song of a playlist
            Waiting,            // count down running between 2 songs of a playlist
            WaitingPaused,      // count down paused between 2 songs of a playlist
            LaunchNextSong      // pause between 2 songs of a playlist
        }
        private PlayerStates PlayerState;


        private bool mBassInitalized = false;

        #endregion

        public frmCDGPlayer(string filename)
        {
            InitializeComponent();
            //mCDGWindow = new frmCDGWindow();
            mCDGWindow.FormClosing += new FormClosingEventHandler(mCDGWindow_FormClosing);

            tbFileName.Text = filename;

            Init_peakLevel();
            PlayerState = PlayerStates.Stopped;
        }



        #region "Control Events"


        private void InitBass()
        {
            //'Add registration key here if you have a license
            BassNet.Registration("fabrice.lacharme@gmail.com", "2X1632326152222");
            
            try
            {
                Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

                mBassInitalized = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to initialize the audio playback system. " + ex.Message);
            }
        }

        private void btBrowse_Click(object sender, EventArgs e)
        {
            BrowseCDGZip();
        }

        /// <summary>
        /// Export CDG to AVI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btRecord_Click(object sender, EventArgs e)
        {
            if (mStop == true)
                ShowExportForm();
        }

        private void ShowExportForm()
        {
            // Affiche le formulaire frmExportCDG2AVI 
            if (Application.OpenForms.OfType<frmExportCDG2AVI>().Count() == 0)
            {
                Form mExportForm = new frmExportCDG2AVI();
                mExportForm.StartPosition = FormStartPosition.CenterScreen;
                mExportForm.Show();
            }
        }

        private void tsbPlay_Click(object sender, EventArgs e)
        {
            //Play();
        }

        private void tsbStop_Click(object sender, EventArgs e)
        {
            try
            {
                StopPlayback();
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }
        }

        private void tsbPause_Click(object sender, EventArgs e)
        {
            //Pause();
        }

        private void trbVolume_Scroll(object sender, EventArgs e)
        {
            //AdjustVolume();
        }

        private void nudKey_ValueChanged(object sender, EventArgs e)
        {
            AdjustPitch();
        }

        #endregion


        #region Form Load Close


        private void frmCDGPlayer_Load(object sender, EventArgs e)
        {
            Location = Properties.Settings.Default.frmCDGPlayerLocation;
            // Verify if this windows is visible in extended screens
            Rectangle rect = new Rectangle(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
            foreach (Screen screen in Screen.AllScreens)
                rect = Rectangle.Union(rect, screen.Bounds);

            if (Location.X > rect.Width)
                Location = new Point(0, Location.Y);
            if (Location.Y > rect.Height)
                Location = new Point(Location.X, 0);

            InitBass();
        }

        private void mCDGWindow_FormClosing(Object sender, FormClosingEventArgs e)
        {
            StopPlayback();
            mCDGWindow.Hide();
            e.Cancel = true;
        }

        private void frmCDGPlayer_FormClosed(object sender, FormClosedEventArgs e)
        {
            StopPlayback();
        }

        private void frmCDGPlayer_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.frmCDGPlayerLocation = Location;
       
            // Save settings
            Properties.Settings.Default.Save();

        }


        #endregion


        #region "CDG + MP3 Playback Operations"

      


        private void PlayMP3Bass(string mp3FileName)
        {
            if (mBassInitalized || Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, Handle))
            {
                mMP3Stream = 0;
                mMP3Stream = Bass.BASS_StreamCreateFile(mp3FileName, 0, 0, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_PRESCAN);
                mMP3Stream = Un4seen.Bass.AddOn.Fx.BassFx.BASS_FX_TempoCreate(mMP3Stream, BASSFlag.BASS_FX_FREESOURCE | BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_SAMPLE_LOOP);
                if (mMP3Stream != 0)
                {
                    AdjustPitch();
                    AdjustVolume();


                    ShowCDGWindow();

                    Bass.BASS_ChannelPlay(mMP3Stream, false);
                }
                else {
                    throw new Exception(String.Format("Stream error: {0}", Bass.BASS_ErrorGetCode()));
                }
            }
        }

        private void StopPlaybackBass()
        {
            try
            {
                Bass.BASS_Stop();
                Bass.BASS_StreamFree(mMP3Stream);
                Bass.BASS_Free();
                mMP3Stream = 0;
                mBassInitalized = false;
            } 
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }
        }

      

        private void PausePlayback()
        {
            Bass.BASS_Pause();
        }

        private void ResumePlayback()
        {
            Bass.BASS_Pause();
        }

       

        private void AdjustPitch()
        {
            if (mMP3Stream != 0)
            {
                Bass.BASS_ChannelSetAttribute(mMP3Stream, BASSAttribute.BASS_ATTRIB_TEMPO_PITCH, (float)nudKey.Value);
            }
        }

        private void AdjustVolume()
        {           
            if (mMP3Stream != 0)
            {
                float volume = (float)sldMainVolume.Value;
                Bass.BASS_ChannelSetAttribute(mMP3Stream, BASSAttribute.BASS_ATTRIB_VOL, volume == 0 ? 0 : (volume / 100));

                int level = Bass.BASS_ChannelGetLevel(mMP3Stream);

            }
        }

        #endregion


        #region "File Access"

        private void BrowseCDGZip()
        {
            OpenFileDialog1.Filter = "CDG or Zip Files (*.zip, *.cdg)|*.zip;*.cdg";
            OpenFileDialog1.ShowDialog();
            tbFileName.Text = OpenFileDialog1.FileName;
        }

        private void PreProcessFiles()
        {
            string myCDGFileName = string.Empty;

            if (Regex.IsMatch(tbFileName.Text, "\\.zip$"))
            {
                string myTempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(myTempDir);
                mTempDir = myTempDir;
                myCDGFileName = Unzip.UnzipMP3GFiles(tbFileName.Text, myTempDir);

                // GO TO
                string myMP3FileName = Regex.Replace(myCDGFileName, "\\.cdg$", ".mp3");
                if (File.Exists(myMP3FileName))
                {
                    mMP3FileName = myMP3FileName;
                    mCDGFileName = myCDGFileName;
                    mTempDir = "";
                }


            }
            else if (Regex.IsMatch(tbFileName.Text, "\\.cdg$"))
            {
                myCDGFileName = tbFileName.Text;

                // GOTO
                string myMP3FileName = Regex.Replace(myCDGFileName, "\\.cdg$", ".mp3");
                if (File.Exists(myMP3FileName))
                {
                    mMP3FileName = myMP3FileName;
                    mCDGFileName = myCDGFileName;
                    mTempDir = "";
                }
            }
        }

        private void CleanUp()
        {
            if (mTempDir != null && mTempDir != "")
            {
                try
                {
                    Directory.Delete(mTempDir, true);
                }
                catch (Exception ex)
                {
                    Console.Write(ex.Message);
                }

            }
            mTempDir = "";
        }

        #endregion


        #region "CDG Graphics Window"

        private void ShowCDGWindow()
        {
            mCDGWindow.Show();
        }

        private void HideCDGWindow()
        {
            mCDGWindow.PictureBox1.Image = null;
            mCDGWindow.Hide();
        }


        #endregion


        #region ProgressBar
        
       


        private void Timer1_Tick(object sender, EventArgs e)
        {
            if (scrolling) return;

            switch (PlayerState)
            {
                case PlayerStates.Playing:
                    GetPeakVolume();
                    if (cdgpos <= positionHScrollBar.Maximum)
                    {
                        positionHScrollBar.Value = Convert.ToInt32(cdgpos);

                        TimeSpan t = TimeSpan.FromMilliseconds(cdgpos);
                        string pos = string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);

                        //lblPos.Text = pos;
                        pnlDisplay.displayElapsed(pos);
                    }
                    break;
                
                case PlayerStates.Paused:
                    break;
                
                case PlayerStates.Stopped:
                    if (mStop)
                        stopProgress();
                    break;
                
                default:
                    break;
            }

            /*
            if (mStop)
                stopProgress();
            else if (cdgpos <= positionHScrollBar.Maximum)
            {                
                positionHScrollBar.Value = Convert.ToInt32(cdgpos);

                TimeSpan t = TimeSpan.FromMilliseconds(cdgpos);
                string pos = string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);
                
                //lblPos.Text = pos;
                pnlDisplay.displayElapsed(pos);
            }
            */
        }


        #endregion ProgressBar


        #region buttons play stop pause

        private void startProgress(long max)
        {
            // Display progress                
            positionHScrollBar.Maximum = Convert.ToInt32(max);
            positionHScrollBar.Value = 0;

            // Duration of song
            TimeSpan t = TimeSpan.FromMilliseconds(max);
            string duration = string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);            
            pnlDisplay.DisplayDuration(duration);

            PlayerState = PlayerStates.Playing;
            Timer1.Start();
        }

        private void stopProgress()
        {
            Timer1.Stop();
            positionHScrollBar.Value = 0;

        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            PlayPauseMusic();
        }

        private void BtnPlay_MouseHover(object sender, EventArgs e)
        {
            if (PlayerState == PlayerStates.Stopped)
                btnPlay.Image = Properties.Resources.btn_blue_play;
            else if (PlayerState == PlayerStates.Paused)
                btnPlay.Image = Properties.Resources.btn_blue_pause;
            else if (PlayerState == PlayerStates.Playing)
                btnPlay.Image = Properties.Resources.btn_blue_play;
        }

        private void BtnPlay_MouseLeave(object sender, EventArgs e)
        {
            if (PlayerState == PlayerStates.Stopped)
                btnPlay.Image = Properties.Resources.btn_black_play;
            else if (PlayerState == PlayerStates.Paused)
                btnPlay.Image = Properties.Resources.btn_red_pause;
            else if (PlayerState == PlayerStates.Playing)
                btnPlay.Image = Properties.Resources.btn_green_play;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            //PauseMusic();
            StopMusic();


        }

        private void BtnStop_MouseHover(object sender, EventArgs e)
        {
            if (PlayerState == PlayerStates.Playing || PlayerState == PlayerStates.Paused)
                btnStop.Image = Properties.Resources.btn_blue_stop;
        }

        private void BtnStop_MouseLeave(object sender, EventArgs e)
        {
            btnStop.Image = Properties.Resources.btn_black_stop;
        }


        private void PlayPauseMusic()
        {
            switch (PlayerState)
            {
                case PlayerStates.Playing:
                    // If playing => pause
                    PlayerState = PlayerStates.Paused;
                    BtnStatus();
                    break;
                case PlayerStates.Paused:
                    PlayerState = PlayerStates.Playing;
                    BtnStatus();
                    Timer1.Start();
                    break;
                case PlayerStates.Stopped:
                    // First play                
                    FirstPlaySong(newstart);
                    break;
            }
            
            /*
            try
            {
                if ((mMP3Stream != 0) && Bass.BASS_ChannelIsActive(mMP3Stream) == BASSActive.BASS_ACTIVE_PLAYING)
                {
                    StopPlayback();
                }

                PreProcessFiles();
                if (mCDGFileName == null || mMP3FileName == null)
                {
                    MessageBox.Show("Cannot find a CDG and MP3 file to play together.");
                    StopPlayback();
                    return;
                }

                mPaused = false;
                mStop = false;
                mFrameCount = 0;
                mCDGFile = new CDGFile(mCDGFileName);

                cdgpos = 0;
                long cdgLength = mCDGFile.getTotalDuration();

                // Display progress                
                startProgress(cdgLength);

                // Show frmCDGWindow ici
                PlayMP3Bass(mMP3FileName);

                DateTime startTime = DateTime.Now;
                DateTime endTime = startTime.AddMilliseconds(cdgLength);
                long millisecondsRemaining = cdgLength;

                while (millisecondsRemaining > 0)
                {
                    if (mStop)
                    {
                        break;
                    }
                    millisecondsRemaining = (long)endTime.Subtract(DateTime.Now).TotalMilliseconds;
                    cdgpos = cdgLength - millisecondsRemaining;

                    while (mPaused)
                    {
                        endTime = DateTime.Now.AddMilliseconds(millisecondsRemaining);
                        Application.DoEvents();
                    }
                    mCDGFile.renderAtPosition(cdgpos);
                    mFrameCount += 1;
                    mCDGWindow.PictureBox1.Image = mCDGFile.RGBImage;

                    Bitmap mbmp = new Bitmap(mCDGFile.RGBImage);
                    mCDGWindow.PictureBox1.BackColor = mbmp.GetPixel(1, 1);

                    mCDGWindow.PictureBox1.Refresh();

                    // TODO
                    //float myFrameRate = (float)Math.Round(mFrameCount / (pos / 1000), 1);
                    Application.DoEvents();
                }
                StopPlayback();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);

            }
            */
        }

        private void PauseMusic()
        {
            mPaused = !mPaused;
            if (mMP3Stream != 0)
            {
                if (Bass.BASS_ChannelIsActive(mMP3Stream) != BASSActive.BASS_ACTIVE_PLAYING)
                {
                    Bass.BASS_ChannelPlay(mMP3Stream, false);
                    tsbPause.Text = "Pause";
                }
                else
                {
                    Bass.BASS_ChannelPause(mMP3Stream);
                    tsbPause.Text = "Resume";
                }
            }
        }

        private void FirstPlaySong(int start)
        {
            try
            {
                if ((mMP3Stream != 0) && Bass.BASS_ChannelIsActive(mMP3Stream) == BASSActive.BASS_ACTIVE_PLAYING)
                {
                    StopPlayback();
                }

                PreProcessFiles();
                if (mCDGFileName == null || mMP3FileName == null)
                {
                    MessageBox.Show("Cannot find a CDG and MP3 file to play together.");
                    StopPlayback();
                    return;
                }

                PlayerState = PlayerStates.Playing;
                mPaused = false;
                mStop = false;
                mFrameCount = 0;
                mCDGFile = new CDGFile(mCDGFileName);
                

                cdgpos = 0;
                long cdgLength = mCDGFile.getTotalDuration();

                // Display progress                
                startProgress(cdgLength);

                // Show frmCDGWindow ici
                PlayMP3Bass(mMP3FileName);

                DateTime startTime = DateTime.Now;
                DateTime endTime = startTime.AddMilliseconds(cdgLength);
                long millisecondsRemaining = cdgLength;

                while (millisecondsRemaining > 0)
                {
                    if (mStop)
                    {
                        break;
                    }
                    millisecondsRemaining = (long)endTime.Subtract(DateTime.Now).TotalMilliseconds;
                    cdgpos = cdgLength - millisecondsRemaining;

                    while (mPaused)
                    {
                        endTime = DateTime.Now.AddMilliseconds(millisecondsRemaining);
                        Application.DoEvents();
                    }
                    mCDGFile.renderAtPosition(cdgpos);
                    mFrameCount += 1;
                    mCDGWindow.PictureBox1.Image = mCDGFile.RGBImage;

                    Bitmap mbmp = new Bitmap(mCDGFile.RGBImage);
                    mCDGWindow.PictureBox1.BackColor = mbmp.GetPixel(1, 1);

                    mCDGWindow.PictureBox1.Refresh();

                    // TODO
                    //float myFrameRate = (float)Math.Round(mFrameCount / (pos / 1000), 1);
                    Application.DoEvents();
                }
                StopPlayback();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);

            }
        }

        private void StopMusic()
        {
            try
            {                
                StopPlayback();
            }
            catch (Exception ex)
            {
                Console.Write(ex.Message);
            }            
        }

        private void StopPlayback()
        {
            Console.Write("\nStopping play back...");

            mStop = true;
            HideCDGWindow();
            StopPlaybackBass();

            if (mCDGFile != null)
                mCDGFile.Dispose();
            CleanUp();

            PlayerState = PlayerStates.Stopped;
        }


        private void AfterStopped()
        {

        }

        private void BtnStatus()
        {
            switch (PlayerState)
            {
                case PlayerStates.Playing:
                    btnPlay.Image = Properties.Resources.btn_green_play;
                    btnStop.Image = Properties.Resources.btn_black_stop;
                    btnPlay.Enabled = true;  // to allow pause
                    btnStop.Enabled = true;  // to allow stop 
                    pnlDisplay.DisplayStatus("playing");                    
                    break;
                case PlayerStates.Paused:
                    btnPlay.Image = Properties.Resources.btn_red_pause;
                    btnPlay.Enabled = true;  // to allow play
                    btnStop.Enabled = true;  // to allow stop
                    pnlDisplay.DisplayStatus("paused");
                    break;
                case PlayerStates.Stopped:
                    btnPlay.Image = Properties.Resources.btn_black_play;
                    btnPlay.Enabled = true;   // to allow play
                    btnStop.Image = Properties.Resources.btn_red_stop;
                    VuPeakVolumeLeft.Level = 0;
                    VuPeakVolumeRight.Level = 0;
                    pnlDisplay.DisplayStatus("stopped");
                    break;
                default:
                    break;
            }
        }

        #endregion buttons play stop pause


        #region peak level

        private void sldMainVolume_Scroll(object sender, ScrollEventArgs e)
        {
            AdjustVolume();
        }

        /// <summary>
        /// Get master peak volume from provider of sound (Karaboss itself or an external one such as VirtualMidiSynth)
        /// </summary>
        private void GetPeakVolume()
        {
            if (mMP3Stream != 0)
            {                                
                int level = Bass.BASS_ChannelGetLevel(mMP3Stream);
                int LeftLevel = LOWORD(level);
                int RightLevel= HIWORD(level);

                VuPeakVolumeLeft.Level = LeftLevel;
                VuPeakVolumeRight.Level = RightLevel;
                
            }
        }

        

        private static int HIWORD(int n)
        {
            return (n >> 16) & 0xffff;
        }

        private static int LOWORD(int n)
        {
            return n & 0xffff;
        }

        /// <summary>
        /// Initialize control peak volume level
        /// </summary>
        private void Init_peakLevel()
        {
            this.VuPeakVolumeLeft.AnalogMeter = false;
            this.VuPeakVolumeLeft.BackColor = System.Drawing.Color.DimGray;
            this.VuPeakVolumeLeft.DialBackground = System.Drawing.Color.White;
            this.VuPeakVolumeLeft.DialTextNegative = System.Drawing.Color.Red;
            this.VuPeakVolumeLeft.DialTextPositive = System.Drawing.Color.Black;
            this.VuPeakVolumeLeft.DialTextZero = System.Drawing.Color.DarkGreen;

            // LED 1
            this.VuPeakVolumeLeft.Led1ColorOff = System.Drawing.Color.DarkGreen;
            this.VuPeakVolumeLeft.Led1ColorOn = System.Drawing.Color.LimeGreen;
            //this.VuMasterPeakVolume.Led1Count = 12;
            this.VuPeakVolumeLeft.Led1Count = 14;

            // LED 2
            this.VuPeakVolumeLeft.Led2ColorOff = System.Drawing.Color.Olive;
            this.VuPeakVolumeLeft.Led2ColorOn = System.Drawing.Color.Yellow;
            //this.VuMasterPeakVolume.Led2Count = 12;
            this.VuPeakVolumeLeft.Led2Count = 14;

            // LED 3
            this.VuPeakVolumeLeft.Led3ColorOff = System.Drawing.Color.Maroon;
            this.VuPeakVolumeLeft.Led3ColorOn = System.Drawing.Color.Red;
            //this.VuMasterPeakVolume.Led3Count = 8;
            this.VuPeakVolumeLeft.Led3Count = 10;

            // LED size
            this.VuPeakVolumeLeft.LedSize = new System.Drawing.Size(12, 2);

            this.VuPeakVolumeLeft.LedSpace = 1;
            this.VuPeakVolumeLeft.Level = 0;
            this.VuPeakVolumeLeft.LevelMax = 32768;

            //this.VuMasterPeakVolume.Location = new System.Drawing.Point(220, 33);
            this.VuPeakVolumeLeft.MeterScale = VU_MeterLibrary.MeterScale.Log10;
            this.VuPeakVolumeLeft.Name = "VuMasterPeakVolume";
            this.VuPeakVolumeLeft.NeedleColor = System.Drawing.Color.Black;
            this.VuPeakVolumeLeft.PeakHold = false;
            this.VuPeakVolumeLeft.Peakms = 1000;
            this.VuPeakVolumeLeft.PeakNeedleColor = System.Drawing.Color.Red;
            this.VuPeakVolumeLeft.ShowDialOnly = false;
            this.VuPeakVolumeLeft.ShowLedPeak = false;
            this.VuPeakVolumeLeft.ShowTextInDial = false;
            this.VuPeakVolumeLeft.Size = new System.Drawing.Size(14, 120);
            this.VuPeakVolumeLeft.TabIndex = 5;
            this.VuPeakVolumeLeft.TextInDial = new string[] {
            "-40",
            "-20",
            "-10",
            "-5",
            "0",
            "+6"};
            this.VuPeakVolumeLeft.UseLedLight = false;
            this.VuPeakVolumeLeft.VerticalBar = true;
            this.VuPeakVolumeLeft.VuText = "VU";
            //this.VuPeakVolumeLeft.Location = new Point(220, 7);



            // Right
            this.VuPeakVolumeRight.AnalogMeter = false;
            this.VuPeakVolumeRight.BackColor = System.Drawing.Color.DimGray;
            this.VuPeakVolumeRight.DialBackground = System.Drawing.Color.White;
            this.VuPeakVolumeRight.DialTextNegative = System.Drawing.Color.Red;
            this.VuPeakVolumeRight.DialTextPositive = System.Drawing.Color.Black;
            this.VuPeakVolumeRight.DialTextZero = System.Drawing.Color.DarkGreen;

            // LED 1
            this.VuPeakVolumeRight.Led1ColorOff = System.Drawing.Color.DarkGreen;
            this.VuPeakVolumeRight.Led1ColorOn = System.Drawing.Color.LimeGreen;
            //this.VuMasterPeakVolume.Led1Count = 12;
            this.VuPeakVolumeRight.Led1Count = 14;

            // LED 2
            this.VuPeakVolumeRight.Led2ColorOff = System.Drawing.Color.Olive;
            this.VuPeakVolumeRight.Led2ColorOn = System.Drawing.Color.Yellow;
            //this.VuMasterPeakVolume.Led2Count = 12;
            this.VuPeakVolumeRight.Led2Count = 14;

            // LED 3
            this.VuPeakVolumeRight.Led3ColorOff = System.Drawing.Color.Maroon;
            this.VuPeakVolumeRight.Led3ColorOn = System.Drawing.Color.Red;
            //this.VuMasterPeakVolume.Led3Count = 8;
            this.VuPeakVolumeRight.Led3Count = 10;

            // LED size
            this.VuPeakVolumeRight.LedSize = new System.Drawing.Size(12, 2);

            this.VuPeakVolumeRight.LedSpace = 1;
            this.VuPeakVolumeRight.Level = 0;
            this.VuPeakVolumeRight.LevelMax = 32768;

            //this.VuMasterPeakVolume.Location = new System.Drawing.Point(220, 33);
            this.VuPeakVolumeRight.MeterScale = VU_MeterLibrary.MeterScale.Log10;
            this.VuPeakVolumeRight.Name = "VuMasterPeakVolume";
            this.VuPeakVolumeRight.NeedleColor = System.Drawing.Color.Black;
            this.VuPeakVolumeRight.PeakHold = false;
            this.VuPeakVolumeRight.Peakms = 1000;
            this.VuPeakVolumeRight.PeakNeedleColor = System.Drawing.Color.Red;
            this.VuPeakVolumeRight.ShowDialOnly = false;
            this.VuPeakVolumeRight.ShowLedPeak = false;
            this.VuPeakVolumeRight.ShowTextInDial = false;
            this.VuPeakVolumeRight.Size = new System.Drawing.Size(14, 120);
            this.VuPeakVolumeRight.TabIndex = 5;
            this.VuPeakVolumeRight.TextInDial = new string[] {
            "-40",
            "-20",
            "-10",
            "-5",
            "0",
            "+6"};
            this.VuPeakVolumeRight.UseLedLight = false;
            this.VuPeakVolumeRight.VerticalBar = true;
            this.VuPeakVolumeRight.VuText = "VU";
            //this.VuPeakVolumeRight.Location = new Point(220, 7);

        }

        #endregion peak level

    }
}
