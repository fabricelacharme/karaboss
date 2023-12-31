﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sanford.Multimedia.Midi;
using ColorSlider;
using Karaboss;
using ChordAnalyser.Properties;
using Sanford.Multimedia.Midi.Score;


namespace ChordAnalyser.UI
{
    public partial class frmDisplayChords : Form
    {


        #region private
        private Sequence sequence1 = new Sequence();
        private ChordAnalyser.UI.ChordsControl chordAnalyserControl1;

        private Panel pnlTop;
        private Panel pnlDisplay;
        private Panel pnlBottom;

        private ColorSlider.ColorSlider positionHScrollBar;

        // Midifile characteristics
        private double _duration = 0;  // en secondes
        private int _totalTicks = 0;
        private int _bpm = 0;
        private double _ppqn;
        private int _tempo;
        private int _measurelen = 0;
        private int NbMeasures;


        #endregion private


        public frmDisplayChords(Sequence seq)
        {
            InitializeComponent();
            this.sequence1 = seq;
            
            UpdateMidiTimes();
            DisplayChordControl();

            DisplayResults();

        }


        #region Display Controls
        private void DisplayChordControl()
        {
            // Panel Top
            pnlTop = new Panel();
            pnlTop.Parent = this.tabPageDiagrams;
            pnlTop.Location = new Point(0, 0);
            pnlTop.Size = new Size(tabPageDiagrams.Width, 100);
            pnlTop.BackColor = Color.Green;
            pnlTop.Dock = DockStyle.Top;
            tabPageDiagrams.Controls.Add(pnlTop);
            

            // Panel Bottom
            pnlBottom = new Panel();
            pnlBottom.Parent = this.tabPageDiagrams;
            pnlBottom.Location = new Point(0, 0);
            pnlBottom.Size = new Size(tabPageDiagrams.Width, 100);
            pnlBottom.BackColor = Color.Red;
            pnlBottom.Dock = DockStyle.Bottom;
            tabPageDiagrams.Controls.Add(pnlBottom);


            // Panel Display
            pnlDisplay = new Panel();
            pnlDisplay.Parent = tabPageDiagrams;
            pnlDisplay.Location = new Point(tabPageDiagrams.Margin.Left, pnlTop.Height);
            pnlDisplay.Size = new Size(pnlTop.Width, tabPageDiagrams.Height - pnlTop.Height - pnlBottom.Height);
            pnlDisplay.BackColor = Color.FromArgb(70, 77, 95);
            //pnlDisplay.Dock = DockStyle.Fill;
            tabPageDiagrams.Controls.Add(pnlDisplay);


            // MIDDLE
            
            // ChordControl
            chordAnalyserControl1 = new ChordsControl();
            chordAnalyserControl1.Parent = pnlDisplay;
            chordAnalyserControl1.Sequence1 = this.sequence1;            
            chordAnalyserControl1.Size = new Size(pnlDisplay.Width, 80);
            chordAnalyserControl1.Location = new Point(0, 0);
            chordAnalyserControl1.WidthChanged += new WidthChangedEventHandler(chordAnalyserControl1_WidthChanged);
            pnlDisplay.Controls.Add(chordAnalyserControl1);

            // positionHScrollBar
            positionHScrollBar = new ColorSlider.ColorSlider();
            positionHScrollBar.Parent = pnlDisplay;
            positionHScrollBar.ThumbImage = Resources.BTN_Thumb_Blue;
            //positionHScrollBar.Size = new Size(pnlDisplay.Width - 16, 20);
            positionHScrollBar.Size = new Size(pnlDisplay.Width, 20);
            positionHScrollBar.Location = new Point(0, chordAnalyserControl1.Height);
            positionHScrollBar.Value = 0;
            positionHScrollBar.Minimum = 0;

            // Set maximum & visibility
            SetScrollBarValues();

            positionHScrollBar.TickStyle = TickStyle.None;                        
            positionHScrollBar.SmallChange = 1;
            positionHScrollBar.LargeChange = 1 + NbMeasures * sequence1.Numerator;
            positionHScrollBar.ShowDivisionsText = false;
            positionHScrollBar.ShowSmallScale = false;
            positionHScrollBar.MouseWheelBarPartitions = 1 + NbMeasures * sequence1.Numerator ;            
            positionHScrollBar.Scroll += new System.Windows.Forms.ScrollEventHandler(PositionHScrollBar_Scroll);
            positionHScrollBar.ValueChanged += new EventHandler(PositionHScollBar_ValueChanged);
            pnlDisplay.Controls.Add(positionHScrollBar);
            

        }

        #endregion Display Controls

        #region HSCrollBar

        private void SetScrollBarValues()
        {
            if (pnlDisplay == null || chordAnalyserControl1 == null)
                return;

            // Width of control
            int W = chordAnalyserControl1.maxStaffWidth;
            
            if (W <= pnlDisplay.Width)
            {
                positionHScrollBar.Visible = false;
                positionHScrollBar.Maximum = 0;
                chordAnalyserControl1.OffsetX = 0;
                positionHScrollBar.Value = 0;
            }
            else if (W > pnlDisplay.Width)
            {                
                positionHScrollBar.Maximum = W - pnlDisplay.Width;
                positionHScrollBar.Visible = true;
            }

        }

        private void PositionHScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            //throw new NotImplementedException();
            //chordAnalyserControl1.OffsetX = e.NewValue;
        }

        private void PositionHScollBar_ValueChanged(object sender, EventArgs e)
        {
            ColorSlider.ColorSlider c = (ColorSlider.ColorSlider)sender;
            chordAnalyserControl1.OffsetX = Convert.ToInt32(c.Value);
        }

        /// <summary>
        /// Set positionHScrollbar Width equal to chord control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private void chordAnalyserControl1_WidthChanged(object sender, int value)
        {            
            // Set maximum & visibility
            SetScrollBarValues();
        }


        #endregion HScrollBar

        #region Display results

        private void DisplayResults()
        {
            
            // Display chods in the textbox
            ChordsAnalyser.ChordAnalyser Analyser = new ChordsAnalyser.ChordAnalyser(sequence1);
            Dictionary<int, (string, string)> Gridchords = Analyser.Gridchords;

            string res = string.Empty;
            foreach (KeyValuePair<int, (string, string)> pair in Gridchords)
            {
                res += string.Format("{0} - {1}", pair.Key, pair.Value) + "\r\n";
            }
            txtOverview.Text = res;


            // Display Chords in boxes
            this.chordAnalyserControl1.Gridchords = Gridchords;


        }

        #endregion Display results

        #region Form
        private void frmDisplayChords_Resize(object sender, EventArgs e)
        {
            pnlDisplay.Width = pnlTop.Width;
            pnlDisplay.Height = tabPageDiagrams.Height - pnlTop.Height - pnlBottom.Height;
            
            if (chordAnalyserControl1 != null)                            
                positionHScrollBar.Width = (pnlDisplay.Width > chordAnalyserControl1.Width ? chordAnalyserControl1.Width : pnlDisplay.Width);
            
            // Set maximum & visibility
            SetScrollBarValues();
        }

        #endregion Form


        #region Midi

        /// <summary>
        /// Upadate MIDI times
        /// </summary>
        private void UpdateMidiTimes()
        {
            _totalTicks = sequence1.GetLength();
            _tempo = sequence1.Tempo;
            _ppqn = sequence1.Division;
            _duration = _tempo * (_totalTicks / _ppqn) / 1000000; //seconds            

            if (sequence1.Time != null)
            {
                _measurelen = sequence1.Time.Measure;
                NbMeasures = _totalTicks / _measurelen;
            }
        }

        #endregion Midi
    }
}
