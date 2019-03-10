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
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Sanford.Multimedia.Midi;
using PicControl;
using System.IO;
using System.Text.RegularExpressions;
using Karaboss.Resources.Localization;
using Karaboss.Lrc.SharedFramework;


namespace Karaboss
{
    public partial class frmLyricsEdit : Form
    {

        /* Lyrics edition form
         * 
         * 0 - Ticks    Number of ticks
         * 1 - Time     Time in sec
         * 2 - Type     text, paragraph, linefeed
         * 3 - Note     Note value
         * 4 - Text     text        
         * 
         * 
         * 
         */
       

        frmPlayer frmPlayer;

        private bool bfilemodified = false;

        private Sequence sequence1;
        private List<plLyric> localplLyrics;

        private Track melodyTrack;
        private CLyric myLyric;

        private ContextMenuStrip dgContextMenu;
        private DataGridViewSelectedCellCollection DGV;

        enum LyricFormats
        {
            Text = 0,
            Lyric = 1
        }       

        const int COL_TICKS = 0;
        const int COL_TIME = 1;
        const int COL_TYPE = 2;
        const int COL_NOTE = 3;
        const int COL_TEXT = 4;
        

        LyricFormats TextLyricFormat;        

        int melodytracknum = 0;

        private string MIDIfileName = string.Empty;

        // Midifile characteristics
        private double _duration = 0;  // en secondes
        private int _totalTicks = 0;
        private int _bpm = 0;
        private double _ppqn;
        private int _tempo;
        private int _measurelen;


        public frmLyricsEdit(Sequence sequence, List<plLyric> plLyrics, CLyric mylyric, string fileName)
        {
            InitializeComponent();            

            MIDIfileName = fileName;
            sequence1 = sequence;
            UpdateMidiTimes();

            myLyric = mylyric;
            InitGridView();
            
            melodytracknum = myLyric.melodytracknum;
            if (melodytracknum != -1)
                melodyTrack = sequence1.tracks[melodytracknum];

            if (myLyric.lyrictype == CLyric.LyricTypes.Text)
            {
                TextLyricFormat = LyricFormats.Text;
                optFormatText.Checked = true;
            }
            else
            {
                TextLyricFormat = LyricFormats.Lyric;
                optFormatLyrics.Checked = true;
            }

            // If first time = no lyrics
            if (plLyrics.Count == 0)
                LoadTrackGuide();
            else
            {               
                // populate cells with existing Lyrics or notes
                PopulateDataGridView(plLyrics);
                // populate viewer
                PopulateTextBox(plLyrics);
            }

            // Adapt height of cells to duration between syllabes
            HeightsToDurations();

            string displayName = string.Empty;
            if (MIDIfileName != null)
                displayName = Path.GetFileName(MIDIfileName);
            SetTitle(displayName);

            dgView.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;

            // Color separators
            ColorSepRows();

            DisplayTags();

            ResizeMe();
        }


        private void DisplayTags()
        {
            string cr = Environment.NewLine;
            int i = 0;
        
            // Classic Karaoke Midi tags
            /*
            @K	(multiple) K1: FileType ex MIDI KARAOKE FILE, K2: copyright of Karaoke file
            @L	(single) Language	FRAN, ENGL        
            @W	(multiple) Copyright (of Karaoke file, not song)        
            @T	(multiple) Title1 @T<title>, Title2 @T<author>, Title3 @T<copyright>		
            @I	Information  ex Date(of Karaoke file, not song)
            @V	(single) Version ex 0100 ?             
            */

            for (i = 0; i < sequence1.KTag.Count; i++)
            {
                txtKTag.Text += sequence1.KTag[i] + cr;
            }
            for (i = 0; i < sequence1.WTag.Count; i++)
            {
                txtWTag.Text += sequence1.WTag[i] + cr;
            }
            for (i = 0; i < sequence1.TTag.Count; i++)
            {
                txtTTag.Text += sequence1.TTag[i] + cr;
            }
            for (i = 0; i < sequence1.ITag.Count; i++)
            {
                txtITag.Text += sequence1.ITag[i] + cr;
            }
            for (i = 0; i < sequence1.VTag.Count; i++)
            {
                txtVTag.Text += sequence1.VTag[i] + cr;
            }
            for (i = 0; i < sequence1.LTag.Count; i++)
            {
                txtLTag.Text += sequence1.LTag[i] + cr;
            }
        }

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
                _measurelen = sequence1.Time.Measure;
        }

      

        /// <summary>
        /// Retrieve Lyrics format from frmPlayer
        /// </summary>
        private void GuessLyricsFormat()
        {
            if (Application.OpenForms.OfType<frmPlayer>().Count() > 0)
            {
                frmPlayer = GetForm<frmPlayer>();
            }
            else
            {
                TextLyricFormat = LyricFormats.Text;
                optFormatLyrics.Checked = true;
                return;
            }

            if (frmPlayer.myLyric.lyrictype == CLyric.LyricTypes.Text)
            {
                TextLyricFormat = LyricFormats.Text;
                optFormatText.Checked = true;                                
            }
            else
            {
                TextLyricFormat = LyricFormats.Lyric;
                optFormatLyrics.Checked = true;
            }
            melodytracknum = frmPlayer.myLyric.melodytracknum;
        }


        #region gridview
        public bool IsNumeric(string input)
        {
            int test;
            return int.TryParse(input, out test);
        }

        private void DgView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            int val = 0;

            // If first col is edited (TICKS)
            if (dgView.CurrentCell.ColumnIndex == COL_TICKS)
            {
                if (!IsNumeric(dgView.CurrentCell.Value.ToString()))
                {
                    if (dgView.CurrentCell.RowIndex == 0)
                        dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TICKS].Value = 0;
                    else
                    {
                        val = 0;
                        if (dgView.Rows[dgView.CurrentCell.RowIndex - 1].Cells[COL_TICKS].Value != null && IsNumeric(dgView.Rows[dgView.CurrentCell.RowIndex - 1].Cells[COL_TICKS].Value.ToString()))
                            val = Convert.ToInt32(dgView.Rows[dgView.CurrentCell.RowIndex - 1].Cells[COL_TICKS].Value);
                        dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TICKS].Value = val;
                    }
                }
                else
                {
                    if (dgView.CurrentCell.RowIndex > 0)
                    {
                        if (dgView.Rows[dgView.CurrentCell.RowIndex - 1].Cells[COL_TICKS].Value != null && IsNumeric(dgView.Rows[dgView.CurrentCell.RowIndex - 1].Cells[COL_TICKS].Value.ToString()))
                            val = Convert.ToInt32(dgView.Rows[dgView.CurrentCell.RowIndex - 1].Cells[COL_TICKS].Value);
                        if (Convert.ToInt32(dgView.CurrentCell.Value) < val)
                            dgView.CurrentCell.Value = val;
                    }
                }

                // Default Type = "text"
                if (dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TYPE].Value == null)
                    dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TYPE].Value = "text";


                if (dgView.Rows[dgView.CurrentCell.RowIndex].Cells[dgView.Columns.Count - 1].Value == null)
                    dgView.Rows[dgView.CurrentCell.RowIndex].Cells[dgView.Columns.Count - 1].Value = "";

                // Ticks to time
                dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TIME].Value = TicksToTime(Convert.ToInt32(dgView.CurrentCell.Value));

            }
            else if (dgView.CurrentCell.ColumnIndex == COL_TIME)
            {
                // If COL_TIME is edited
                if (dgView.CurrentCell.Value != null)
                {
                    // Time to ticks
                    dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TICKS].Value = TimeToTicks(dgView.CurrentCell.Value.ToString());
                }
            }
            else if (dgView.CurrentCell.ColumnIndex == COL_TYPE)
            {
                // If COL_TYPE is edited
                if (dgView.CurrentCell.Value != null)
                {
                    string type = dgView.CurrentCell.Value.ToString();
                    switch (type)
                    {
                        case "text":
                            break;
                        case "par":
                            dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TEXT].Value = "\\";
                            break;
                        case "cr":
                            dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TEXT].Value = "/";
                            break;
                        default:
                            break;
                    }

                    ColorSepRows();
                }

            }
            else  if (dgView.CurrentCell.ColumnIndex == dgView.Columns.Count - 1)
            {
                // If last col is edited

                if (dgView.CurrentCell.Value == null)
                    dgView.CurrentCell.Value = "";

                if (dgView.CurrentCell.Value.ToString() == "/")
                {
                    dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TYPE].Value = "cr";
                }
                else if (dgView.CurrentCell.Value.ToString() == "\\")
                {
                    dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TYPE].Value = "par";
                }
                else 
                    dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TYPE].Value = "text";

                string c = dgView.CurrentCell.Value.ToString();
                c = c.Replace(" ", "_");
                dgView.CurrentCell.Value = c;

                // Retrieve time value of previous row
                if (dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TICKS].Value == null || !IsNumeric( dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TICKS].Value.ToString()))
                {
                    if (dgView.CurrentCell.RowIndex == 0)
                        dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TICKS].Value = 0;
                    else
                    {
                        val = 0;
                        if (dgView.Rows[dgView.CurrentCell.RowIndex - 1].Cells[COL_TICKS].Value != null && IsNumeric(dgView.Rows[dgView.CurrentCell.RowIndex - 1].Cells[COL_TICKS].Value.ToString()))
                            val = Convert.ToInt32(dgView.Rows[dgView.CurrentCell.RowIndex - 1].Cells[COL_TICKS].Value);
                        dgView.Rows[dgView.CurrentCell.RowIndex].Cells[COL_TICKS].Value = val;
                    }
                }
            }

            //Load modification into local list of lyrics
            LoadModifiedLyrics();
            PopulateTextBox(localplLyrics);

            // File was modified
            FileModified();
            
        }

        /// <summary>
        /// Display current line in textbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DgView_CellEnter(object sender, DataGridViewCellEventArgs e)
        {
            ShowCurrentLine();
        }

        private void DgView_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Delete:
                    {                        
                        foreach (DataGridViewCell C in dgView.SelectedCells)
                        {
                            if (C.ColumnIndex != 0)
                                C.Value = "";
                        }                        
                        break;
                    }
            }
        }

        /// <summary>
        /// Initialize gridview
        /// </summary>
        private void InitGridView()
        {
            dgView.Rows.Clear();
            dgView.Refresh();

            // Header color
            dgView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(43, 87, 151);
            dgView.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;

            dgView.ColumnHeadersDefaultCellStyle.Font = new Font("Arial", 12F, FontStyle.Regular);
            dgView.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            dgView.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Selection
            dgView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 137, 239);

            dgView.EnableHeadersVisualStyles = false;

            //Change cell font
            foreach (DataGridViewColumn c in dgView.Columns)
            {
                c.SortMode = DataGridViewColumnSortMode.NotSortable;                     // header not sortable
                c.DefaultCellStyle.Font = new Font("Arial", 16F, GraphicsUnit.Pixel);
                c.ReadOnly = false;               
            }
        }

        private void ColorSepRows()
        {
            for (int i = 1; i < dgView.Rows.Count; i++)
            {
                if (dgView.Rows[i].Cells[COL_TYPE].Value != null && dgView.Rows[i].Cells[COL_TYPE].Value.ToString() == "cr")
                {
                    dgView.Rows[i].DefaultCellStyle.BackColor = Color.FromArgb(239, 244, 255);
                }
                else if (dgView.Rows[i].Cells[COL_TYPE].Value != null && dgView.Rows[i].Cells[COL_TYPE].Value.ToString() == "par")
                {
                    dgView.Rows[i].DefaultCellStyle.BackColor = Color.LightGray;
                }
            }
        }


        /// <summary>
        /// Populate datagridview with lyrics
        /// </summary>
        /// <param name="plLyrics"></param>
        private void PopulateDataGridView(List<plLyric> lLyrics)
        {          
            bool bfound = false;

            int plTicksOn = 0;
            string plRealTime = "00:00.00";
            plLyric.Types plType = plLyric.Types.Text;             

            int plNote = 0;
            string sNote = string.Empty;
            string plElement = string.Empty;

            int idx = 0;


            if (melodyTrack == null)
            {
                // On affiche la liste des lyrics 
                for (idx = 0; idx < lLyrics.Count; idx++)
                {
                    plTicksOn = lLyrics[idx].TicksOn;
                    plRealTime = TicksToTime(plTicksOn);           // TODO
                    plNote = 0;
                    sNote = "";
                    plElement = lLyrics[idx].Element;
                    plElement = plElement.Replace(" ", "_");
                    plType = lLyrics[idx].Type;

                    // New Row
                    string[] rowlyric = { plTicksOn.ToString(), plRealTime, Karaclass.plTypeToString(plType), sNote, plElement };
                    dgView.Rows.Add(rowlyric);
                }
            }
            else
            {
                // Variante 1 : on affiche les lyrics par défaut et on essaye de raccrocher les notes
                for (int i = 0; i < lLyrics.Count; i++)
                {
                    bfound = false;
                    plTicksOn = lLyrics[i].TicksOn;
                    plRealTime = TicksToTime(plTicksOn);           // TODO
                    plNote = 0;
                    plElement = lLyrics[i].Element;
                    plElement = plElement.Replace(" ", "_");
                    plType = lLyrics[i].Type;

                    if (idx < lLyrics.Count)
                    {
                        // Afficher les notes dont le start est avant celui du Lyric courant
                        while (idx < melodyTrack.Notes.Count && melodyTrack.Notes[idx].StartTime < plTicksOn)
                        {
                            int beforeplTime = melodyTrack.Notes[idx].StartTime;
                            string beforeplRealTime = TicksToTime(beforeplTime);
                            int beforeplNote = melodyTrack.Notes[idx].Number;
                            string beforeplElement = "";
                            string beforeplType = "text";
                            string[] rownote = { beforeplTime.ToString(), beforeplRealTime, beforeplType, beforeplNote.ToString(), beforeplElement };
                            dgView.Rows.Add(rownote);
                            idx++;
                            if (idx >= melodyTrack.Notes.Count)
                                break;

                        }
                        // Afficher la note dont le start est égal à celui du lyric courant
                        if (idx < melodyTrack.Notes.Count && melodyTrack.Notes[idx].StartTime == plTicksOn) 
                        {
                            plNote = melodyTrack.Notes[idx].Number;
                            sNote = plNote.ToString();
                            if (plType == plLyric.Types.LineFeed || plType == plLyric.Types.Paragraph)
                                sNote = "";                           

                            string[] rowlyric = { plTicksOn.ToString(), plRealTime, Karaclass.plTypeToString(plType), sNote, plElement };
                            dgView.Rows.Add(rowlyric);
                            bfound = true; // lyric inscrit dans la grille
                            // Incrémente le compteur de notes si différent de retour chariot
                            if (plType != plLyric.Types.LineFeed)
                                idx++;
                        }
                       
                    }

                    // Lyric courant pas inscrit dans la grille ?
                    if (bfound == false)
                    {
                        sNote = plNote.ToString();
                        if (plType == plLyric.Types.LineFeed || plType == plLyric.Types.Paragraph)
                            sNote = "";

                        string[] rowlyric = { plTicksOn.ToString(), plRealTime, Karaclass.plTypeToString(plType), sNote, plElement };
                        dgView.Rows.Add(rowlyric);
                    }
                }

                // Il reste des notes ?
                while (idx < melodyTrack.Notes.Count)
                {
                    int afterplTime = melodyTrack.Notes[idx].StartTime;
                    string afterplRealTime = TicksToTime(afterplTime);
                    int afterplNote = melodyTrack.Notes[idx].Number;
                    string afterplElement = "";
                    string afterplType = "text";
                    string[] rownote = { afterplTime.ToString(), afterplRealTime, afterplType, afterplNote.ToString(), afterplElement };
                    dgView.Rows.Add(rownote);
                    idx++;
                    if (idx >= melodyTrack.Notes.Count)
                        break;

                }               
            }                             
        }

        /// <summary>
        /// Populate DataGridView with only notes of a track
        /// </summary>
        /// <param name="tracknumber"></param>
        private void PopulateDataGridViewTrack(int tracknumber)
        {
            InitGridView();

            if (tracknumber >= 0 && tracknumber < sequence1.tracks.Count)
            {
                Track track = sequence1.tracks[tracknumber];
                int plTicksOn = 0;
                string plRealTime = string.Empty;
                string plType = string.Empty;
                int plNote = 0;
                string plElement = string.Empty;

                for (int i = 0; i < track.Notes.Count; i++)
                {
                    MidiNote n = track.Notes[i];
                    plTicksOn = n.StartTime;
                    plRealTime = TicksToTime(plTicksOn);
                    plType = "text";
                    plNote = n.Number;
                    plElement = plNote.ToString();

                    string[] row = { plTicksOn.ToString(), plRealTime, plType, plNote.ToString(), plElement };
                    dgView.Rows.Add(row);
                }
            }            
        }

        #endregion gridview


        #region form load close resize

        /// <summary>
        /// Form load
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrmLyricsEdit_Load(object sender, EventArgs e)
        {
            // Récupère la taille et position de la forme

            // If window is maximized
            if (Properties.Settings.Default.frmLyricsEditMaximized)
            {
                
                Location = Properties.Settings.Default.frmLyricsEditLocation;
                //Size = Properties.Settings.Default.frmLyricsEditSize;
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                Location = Properties.Settings.Default.frmLyricsEditLocation;
                // Verify if this windows is visible in extended screens
                Rectangle rect = new Rectangle(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
                foreach (Screen screen in Screen.AllScreens)
                    rect = Rectangle.Union(rect, screen.Bounds);

                if (Location.X > rect.Width)
                    Location = new Point(0, Location.Y);
                if (Location.Y > rect.Height)
                    Location = new Point(Location.X, 0);

                Size = Properties.Settings.Default.frmLyricsEditSize;
            }
        }

        /// <summary>
        /// Form closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FrmLyricsEdit_FormClosing(object sender, FormClosingEventArgs e)
        {

            if (bfilemodified == true)
            {
                string tx = "Le fichier a été modifié, voulez-vous l'enregistrer ?";
                if (MessageBox.Show(tx, "Karaboss", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    e.Cancel = true;

                    //Load modification into local list of lyrics
                    LoadModifiedLyrics();

                    // Display new lyrics in frmLyrics
                    ReplaceLyrics();

                    // Save file
                    SaveFileProc();
                    return;
                }
                else
                {
                    if (Application.OpenForms.OfType<frmPlayer>().Count() > 0)
                    {
                        frmPlayer frmPlayer = GetForm<frmPlayer>();
                        frmPlayer.bfilemodified = false;
                    }
                }
            }       
            
            // enregistre la taille et la position de la forme
            // Copy window location to app settings                
            if (WindowState != FormWindowState.Minimized)
            {
                if (WindowState == FormWindowState.Maximized)
                {
                    Properties.Settings.Default.frmLyricsEditLocation = RestoreBounds.Location;
                    Properties.Settings.Default.frmLyricsEditMaximized = true;

                }
                else if (WindowState == FormWindowState.Normal)
                {
                    Properties.Settings.Default.frmLyricsEditLocation = Location;
                    Properties.Settings.Default.frmLyricsEditSize = Size;
                    Properties.Settings.Default.frmLyricsEditMaximized = false;
                }
                // Save settings
                Properties.Settings.Default.Save();
            }

            Dispose();
        }

        /// <summary>
        /// Form Resize
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmLyricsEdit_Resize(object sender, EventArgs e)
        {
            ResizeMe();
        }

        private void ResizeMe()
        {
            // Adapt width of last column
            int W = dgView.RowHeadersWidth + 19;
            int WP = dgView.Parent.Width;
            for (int i = 0; i < dgView.Columns.Count - 1; i++)
            {
                W += dgView.Columns[i].Width;
            }
            if (WP - W > 0)
                dgView.Columns[dgView.Columns.Count - 1].Width = WP - W;

        }


        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            // Adapt width of last column
            int W = dgView.RowHeadersWidth + 19;
            int WP = dgView.Parent.Width;
            for (int i = 0; i < dgView.Columns.Count - 1; i++)
            {
                W += dgView.Columns[i].Width;
            }
            if (WP - W > 0)
                dgView.Columns[dgView.Columns.Count - 1].Width = WP - W;
        }

        #endregion form load close resize


        #region buttons

        /// <summary>
        /// Button : save new lyrics
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnView_Click(object sender, EventArgs e)
        {

            //Load modification into local list of lyrics
            LoadModifiedLyrics();

            // Display new lyrics in frmLyrics
            ReplaceLyrics();
        }


        /// <summary>
        /// Insert a LineFeed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnInsert_Click(object sender, EventArgs e)
        {
            InsertSepLine("cr");     
        }

        /// <summary>
        /// Insert a Paragraph
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnInsertParagraph_Click(object sender, EventArgs e)
        {
            InsertSepLine("par");
      
        }

        /// <summary>
        /// Insert Linefeed or Paragraph
        /// </summary>
        /// <param name="sep"></param>
        private void InsertSepLine(string sep)
        {
            int Row = dgView.CurrentRow.Index;
            int plTicksOn = 0;
            string plRealTime = "00:00.00";
            string plElement = string.Empty;

            if (dgView.Rows[Row].Cells[COL_TICKS].Value != null)
            {
                plTicksOn = Convert.ToInt32(dgView.Rows[Row].Cells[COL_TICKS].Value);
                plRealTime = TicksToTime(plTicksOn);
            }

            if (sep == "cr")
                plElement = "/";
            else
                plElement = "\\";

            // time, type, note, text, text
            dgView.Rows.Insert(Row, plTicksOn, plRealTime, sep, "", plElement);

            //Load modification into local list of lyrics
            LoadModifiedLyrics();
            PopulateTextBox(localplLyrics);

            HeightsToDurations();

            // Color separators
            ColorSepRows();

            // File was modified
            FileModified();
        }


        /// <summary>
        /// Insert a Text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnInsertText_Click(object sender, EventArgs e)
        {
            InsertLine();
        }

        /// <summary>
        /// Insert text line
        /// </summary>
        private void InsertLine()
        {
            int Row = dgView.CurrentRow.Index;
            int plTicksOn = 0;
            string plRealTime = "00:00.00";
            int pNote = 0;
            string pElement = string.Empty;
            string pReplace = string.Empty;

            if (dgView.Rows[Row].Cells[COL_TICKS].Value != null)
            {
                plTicksOn = Convert.ToInt32(dgView.Rows[Row].Cells[COL_TICKS].Value);
                plRealTime = TicksToTime(plTicksOn);
            }

            pElement = "text";

            // Column Replace
            if (dgView.Rows[Row].Cells[COL_TEXT].Value != null)
                pReplace = dgView.Rows[Row].Cells[COL_TEXT].Value.ToString();
            else
                pReplace = "text";


            dgView.Rows.Insert(Row, plTicksOn, plRealTime, "text", pNote, pElement, pReplace);

            HeightsToDurations();

            // File was modified
            FileModified();
        }


        /// <summary>
        /// Add a space left
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnSpaceLeft_Click(object sender, EventArgs e)
        {
            int Row = dgView.CurrentRow.Index;
            if (dgView.Rows[Row].Cells[COL_TEXT].Value != null)
            {
                dgView.Rows[Row].Cells[COL_TEXT].Value = "_" + dgView.Rows[Row].Cells[COL_TEXT].Value;
            }
        }

        /// <summary>
        /// Add a space right
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnSpaceRight_Click(object sender, EventArgs e)
        {
            int Row = dgView.CurrentRow.Index;
            if (dgView.Rows[Row].Cells[COL_TEXT].Value != null)
            {
                dgView.Rows[Row].Cells[COL_TEXT].Value = dgView.Rows[Row].Cells[COL_TEXT].Value + "_";
                
                // File was modified
                FileModified();
            }
        }
        
        /// <summary>
        /// Delete a row
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnDelete_Click(object sender, EventArgs e)
        {
            DeleteLine();
        }

        private void DeleteLine()
        {
            try
            {
                int row = dgView.CurrentRow.Index;
                dgView.Rows.RemoveAt(row);

                //Load modification into local list of lyrics
                LoadModifiedLyrics();
                PopulateTextBox(localplLyrics);

                // File was modified
                FileModified();
            }
            catch (Exception Ex)
            {
                string message = "Error : " + Ex.Message;
                MessageBox.Show(message, "Error deleting line",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Save as
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnSave_Click(object sender, EventArgs e)
        {
            //Load modification into local list of lyrics
            LoadModifiedLyrics();

            // Display new lyrics in frmLyrics
            ReplaceLyrics();

            // Save file
            SaveFileProc();

            Focus();
        }

        /// <summary>
        /// Play from current time
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnPlay_Click(object sender, EventArgs e)
        {
            //Load modification into local list of lyrics
            LoadModifiedLyrics();

            // Display new lyrics in frmLyrics
            ReplaceLyrics();

            int Row = dgView.CurrentRow.Index;
            if (dgView.Rows[Row].Cells[COL_TICKS].Value != null)
            {
                int pTime = Convert.ToInt32(dgView.Rows[Row].Cells[COL_TICKS].Value);
                if (Application.OpenForms.OfType<frmPlayer>().Count() > 0)
                {
                    frmPlayer frmPlayer = GetForm<frmPlayer>();
                    frmPlayer.FirstPlaySong(pTime);
                }
            }
        }

      

        private void BtnFontPlus_Click(object sender, EventArgs e)
        {
            float emSize = txtResult.Font.Size;
            emSize++;
            txtResult.Font = new Font(txtResult.Font.FontFamily, emSize);
        }

        private void BtnFontMoins_Click(object sender, EventArgs e)
        {
            float emSize = txtResult.Font.Size;
            emSize--;
            if (emSize > 5)
                txtResult.Font = new Font(txtResult.Font.FontFamily, emSize);
        }



        #endregion buttons


        #region lrc

        /// <summary>
        /// Convert ticks to time
        /// Minutes, seconds, cent of seconds
        /// Ex: 6224 ticks => 00:09.10 (mm:ss.cent)
        /// </summary>
        /// <param name="ticks"></param>
        /// <returns></returns>
        private string TicksToTime(int ticks)
        {
            double dur = _tempo * (ticks / _ppqn) / 1000000; //seconds     
            int Min = (int)(dur / 60);
            int Sec = (int)(dur - (Min * 60));


            int Cent = (int)(100 * (dur - (Min * 60) - Sec));

            string tx = string.Format("{0:00}:{1:00}.{2:00}", Min, Sec, Cent);
            return tx;
        }

        /// <summary>
        /// Convert time to ticks
        /// 01:15.51 (min, sec, cent)
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        private int TimeToTicks(string time)
        {
            int ti = 0;
            double dur = 0;

            string[] split1 = time.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (split1.Length != 2)
                return ti;

            string min = split1[0];            

            string[] split2 = split1[1].Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (split2.Length != 2)
                return ti;

            string sec = split2[0];
            string cent = split2[1];

            // Calculate dur in seconds
            int Min = Convert.ToInt32(min);
            dur = Min * 60;
            int Sec = Convert.ToInt32(sec);
            dur += Sec;
            int Cent = Convert.ToInt32(cent);
            dur += Cent / 100;

            ti = Convert.ToInt32(_ppqn * dur * 1000000 / _tempo);

            return ti;
        }


        /// <summary>
        /// Save lyrics to lrc format, syllabe by syllabe
        /// </summary>
        /// <param name="FileName"></param>
        private void SaveLRCParcels(string File, string Tag_Title, string Tag_Artist, string Tag_Album, string Tag_Lang, string Tag_By, string Tag_DPlus)
        {
            string sTime = string.Empty;
            string sLyric = string.Empty;
            object vLyric;
            object vTime;
            string lrcs = string.Empty;
            string cr = "\r\n";

            if (Tag_Title != "")
                lrcs += "[Ti:" + Tag_Title + "]" + cr;
            if (Tag_Artist != "")
                lrcs += "[Ar:" + Tag_Artist + "]" + cr;
            if (Tag_Album != "")
                lrcs += "[Al:" + Tag_Album + "]" + cr;
            if (Tag_Lang != "")
                lrcs += "[La:" + Tag_Lang + "]" + cr;
            if (Tag_By != "")
                lrcs += "[By:" + Tag_Album + "]" + cr;
            if (Tag_DPlus != "")
                lrcs += "[D+:" + Tag_DPlus + "]" + cr;

            // Save syllabe by syllabe
            for (int i = 0; i < dgView.Rows.Count; i++)
            {
                vLyric = dgView.Rows[i].Cells[COL_TEXT].Value;
                vTime = dgView.Rows[i].Cells[COL_TIME].Value;

                if (vTime != null && vLyric != null)
                {
                    sLyric = vLyric.ToString();
                    sLyric = sLyric.Replace("_", " ");
                    sLyric = sLyric.Trim();

                    if (sLyric != "" && sLyric != cr)
                    {
                        sTime = vTime.ToString();
                        lrcs += "[" + sTime + "]" + sLyric + cr;
                    }
                }
            }

            try
            {
                System.IO.File.WriteAllText(File, lrcs);
                System.Diagnostics.Process.Start(@File);

            }
            catch (IOException)
            {
            }
        }

        /// <summary>
        /// Save Lyrics .lrc file format and by lines
        /// </summary>
        /// <param name="File"></param>
        /// <param name="Tag_Title"></param>
        /// <param name="Tag_Artist"></param>
        /// <param name="Tag_Album"></param>
        /// <param name="Tag_Lang"></param>
        /// <param name="Tag_By"></param>
        /// <param name="Tag_DPlus"></param>
        private void SaveLRCLines(string File, string Tag_Title, string Tag_Artist, string Tag_Album, string Tag_Lang, string Tag_By, string Tag_DPlus)
        {
            string sTime = string.Empty;
            string sLyric = string.Empty;
            string sLine = string.Empty;
            string sType = string.Empty;
            object vLyric;
            object vTime;
            object vType;
            string lrcs = string.Empty;
            string cr = "\r\n";


            if (Tag_Title != "")
                lrcs += "[Ti:" + Tag_Title + "]" + cr;
            if (Tag_Artist != "")
                lrcs += "[Ar:" + Tag_Artist + "]" + cr;
            if (Tag_Album != "")
                lrcs += "[Al:" + Tag_Album + "]" + cr;
            if (Tag_Lang != "")
                lrcs += "[La:" + Tag_Lang + "]" + cr;
            if (Tag_By != "")
                lrcs += "[By:" + Tag_Album + "]" + cr;
            if (Tag_DPlus != "")
                lrcs += "[D+:" + Tag_DPlus + "]" + cr;


            bool bStartLine = true;

            // Save syllabe by syllabe
            for (int i = 0; i < dgView.Rows.Count; i++)
            {
                vLyric = dgView.Rows[i].Cells[COL_TEXT].Value;
                vTime = dgView.Rows[i].Cells[COL_TIME].Value;
                vType = dgView.Rows[i].Cells[COL_TYPE].Value;

                if (vTime != null && vLyric != null && vType != null)
                {
                    sLyric = vLyric.ToString().Trim();                                        
                    sType = vType.ToString().Trim();

                    if (sLyric != "" && sType != "cr" && sType != "par")
                    {
                        if (bStartLine)
                        {
                            sTime = vTime.ToString();
                            sLine = "[" + sTime + "]" + sLyric;
                            bStartLine = false;
                        }
                        else
                        {
                            // Line continuation
                            sLine += sLyric;
                        }
                    }
                    else if (sType == "cr" || sType == "par")
                    {
                        // Start new line    
                        
                        // Save current line
                        if (sLine != "")
                        {
                            sLine = sLine.Replace("_", " ");
                            lrcs += sLine + cr;
                        }

                        // Reset all
                        bStartLine = true;
                        sLine = string.Empty;                        
                    }
                }
            }

            // Save last line
            if (sLine != "")
            {
                sLine = sLine.Replace("_", " ");
                lrcs += sLine + cr;
            }


            try
            {
                System.IO.File.WriteAllText(File, lrcs);
                System.Diagnostics.Process.Start(@File);

            }
            catch (IOException)
            {
            }
        }


        #endregion


        #region menus

        /// <summary>
        /// Menu File Save
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MnuFileSave_Click(object sender, EventArgs e)
        {
            //Load modification into local list of lyrics
            LoadModifiedLyrics();

            // Display new lyrics in frmLyrics
            ReplaceLyrics();            
            
            // save file
            SaveFileProc();
        }

        /// <summary>
        /// Menu File Save as
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MnuFileSaveAs_Click(object sender, EventArgs e)
        {
            SaveAsFileProc();
        }

        /// <summary>
        /// Save lyrics to .lrc format
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mnuFileSaveAsLrc_Click(object sender, EventArgs e)
        {
            #region select filename
            string fName = "New.lrc";
            string fPath = Path.GetDirectoryName(MIDIfileName);

            string fullName = string.Empty;
            string defName = string.Empty;

            #region search name
            if (fPath == null || fPath == "")
            {
                if (Directory.Exists(CreateNewMidiFile.DefaultDirectory))
                    fPath = CreateNewMidiFile.DefaultDirectory;
                else
                    fPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            }
            else
            {
                fName = Path.GetFileName(MIDIfileName);
            }

            string defExt = ".lrc";         // Extension
            fName = Path.GetFileNameWithoutExtension(fName);    // name without extension
            string inifName = fName + defExt;                            // Original name with extension
            defName = fName;                                    // Proposed name for dialog box

            fullName = fPath + "\\" + inifName;

            if (File.Exists(fullName) == true)
            {
                // Remove all (1) (2) etc..
                string pattern = @"[(\d)]";
                string replace = @"";
                inifName = Regex.Replace(fName, pattern, replace);

                int i = 1;
                string addName = "(" + i.ToString() + ")";
                defName = inifName + addName + defExt;
                fullName = fPath + "\\" + defName;

                while (File.Exists(fullName) == true)
                {
                    i++;
                    defName = inifName + "(" + i.ToString() + ")" + defExt;
                    fullName = fPath + "\\" + defName;
                }
            }

            #endregion search name                   

            string defFilter = "LRC files (*.lrc)|*.lrc|All files (*.*)|*.*";

            saveMidiFileDialog.Title = "Save to LRC format";
            saveMidiFileDialog.Filter = defFilter;
            saveMidiFileDialog.DefaultExt = defExt;
            saveMidiFileDialog.InitialDirectory = @fPath;
            saveMidiFileDialog.FileName = defName;

            if (saveMidiFileDialog.ShowDialog() != DialogResult.OK)
                return;

            #endregion

            string Tag_Title = string.Empty;
            string Tag_Artist = string.Empty;
            string Tag_Album = string.Empty;
            string Tag_Lang = string.Empty;
            string Tag_By = string.Empty;
            string Tag_DPlus = string.Empty;

            string FileName = saveMidiFileDialog.FileName;
            string bLRCType = "Lines";

            // Search Title & Artist
            string SingleName = Path.GetFileNameWithoutExtension(FileName);
            string[] split = SingleName.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 1)
            {
                Tag_Title = split[0].Trim();
            }
            else if (split.Length == 2)
            {
                Tag_Artist = split[0].Trim();
                Tag_Title = split[1].Trim();
            }

            if (bLRCType == "Lines")
                SaveLRCLines(FileName, Tag_Title, Tag_Artist, Tag_Album, Tag_Lang, Tag_By, Tag_DPlus);
            else
                SaveLRCParcels(FileName, Tag_Title, Tag_Artist, Tag_Album, Tag_Lang, Tag_By, Tag_DPlus);
        }


        /// <summary>
        /// Quit windowx
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MnuFileQuit_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Menu: load times of a melody track to help lyrics entering
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MnuEditLoadTrack_Click(object sender, EventArgs e)
        {
            DialogResult dr = new DialogResult();
            frmLyricsSelectTrack TrackDialog = new frmLyricsSelectTrack(sequence1);
            dr = TrackDialog.ShowDialog();

            if (dr == System.Windows.Forms.DialogResult.Cancel)
                return;

            // Get track number for melody
            // -1 if no track
            melodytracknum = TrackDialog.TrackNumber - 1;
            
            if (melodytracknum == -1)
            {
                //MessageBox.Show("No track found for the melody", "Karaboss", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dgView.Rows.Clear();
                return;
            }

            LoadTrackGuide();
        }

        /// <summary>
        /// Select track audio guide & lyrics format
        /// </summary>
        private void LoadTrackGuide()
        {
            
            PopulateDataGridViewTrack(melodytracknum);
            LoadModifiedLyrics();
            PopulateTextBox(localplLyrics);
        }

        /// <summary>
        /// Menu: load a text file containing the melody
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MnuEditLoadMelodyText_Click(object sender, EventArgs e)
        {            
            openFileDialog.Title = "Open a text file";
            openFileDialog.DefaultExt = "txt";
            openFileDialog.Filter = "Txt files|*.txt|All files|*.*";
            if (MIDIfileName != null || MIDIfileName != "")
                openFileDialog.InitialDirectory = Path.GetDirectoryName(MIDIfileName);

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fileName = openFileDialog.FileName;
                try
                {
                    using (StreamReader sr = new StreamReader(fileName))
                    {
                        String lines = sr.ReadToEnd();                        
                        LoadTextFile(lines);
                    }
                }
                catch (Exception errl)
                {
                    Console.WriteLine("The file could not be read:");
                    Console.WriteLine(errl.Message);
                }
            }
        }

        /// <summary>
        /// Load text without times
        /// </summary>
        /// <param name="source"></param>
        private void LoadTextFile(string source)
        {
            // Split into peaces of words
            source = source.Replace("\r\n", " <cr> ");
            source = source.Replace(" <cr>  <cr> ", " <cr> <cr> ");
            string[] stringSeparators = new string[] { " " };
            string[] result = source.Split(stringSeparators, StringSplitOptions.None);
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] == "")
                    result[i] = "<cr>";
            }

            // Add missing lines before
            int addl = result.Length - dgView.Rows.Count;
            if (addl > 0)
            {
                for (int i = 0; i < addl; i++)
                {
                    dgView.Rows.Add();
                }
            }

            // write lyrics on each line
            string s = string.Empty;

            int plTicksOn = 0;
            string plRealTime = "00:00.00";
            int plNote = 0;
            string plType = "text";            
            string plElement = "";


            for (int i = 0; i < result.Length; i++)
            {
                s = result[i];
               
                if (i < dgView.Rows.Count)
                {
                    if (s != "<cr>")
                    {
                        // insert TEXT
                        plType = "text";

                        if (dgView.Rows[i].Cells[COL_TICKS].Value == null)
                        {
                            dgView.Rows[i].Cells[COL_TICKS].Value = plTicksOn;
                            plRealTime = TicksToTime(plTicksOn);
                            dgView.Rows[i].Cells[COL_TIME].Value = plRealTime;
                        }
                        else
                        {
                            plTicksOn = Convert.ToInt32(dgView.Rows[i].Cells[COL_TICKS].Value);
                        }
                        
                        dgView.Rows[i].Cells[COL_TYPE].Value = plType;

                        if (dgView.Rows[i].Cells[COL_NOTE].Value == null)
                            dgView.Rows[i].Cells[COL_NOTE].Value = 0;

                        plElement = s + "_";

                        dgView.Rows[i].Cells[COL_TEXT].Value = plElement;                        
                        
                    }
                    else
                    {
                        // insert <CR>;
                        plType = "cr";
                        if (dgView.Rows[i].Cells[COL_TICKS].Value != null)
                        {
                            plTicksOn = Convert.ToInt32(dgView.Rows[i].Cells[COL_TICKS].Value);
                            plRealTime = TicksToTime(plTicksOn);
                        }
                        if (dgView.Rows[i].Cells[COL_NOTE].Value == null)
                            dgView.Rows[i].Cells[COL_NOTE].Value = 0;

                        plNote = Convert.ToInt32(dgView.Rows[i].Cells[COL_NOTE].Value);

                        plElement = "";                        

                        dgView.Rows[i].Cells[COL_TICKS].Value = plTicksOn;
                        dgView.Rows[i].Cells[COL_TIME].Value = plRealTime;
                        dgView.Rows[i].Cells[COL_TYPE].Value = plType;
                        dgView.Rows[i].Cells[COL_NOTE].Value = plNote.ToString();
                        dgView.Rows[i].Cells[COL_TEXT].Value = plElement;                        
                    }
                }
            }

            //Load modification into local list of lyrics
            LoadModifiedLyrics();
            PopulateTextBox(localplLyrics);

            // Color separators
            ColorSepRows();

            // File was modified
            FileModified();
        }

      

        /// <summary>
        /// Load a text file LRC format (times stamps + lyrics)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mnuEditLoadLRCFile_Click(object sender, EventArgs e)
        {
            openFileDialog.Title = "Open a .lrc file";
            openFileDialog.DefaultExt = "lrc";
            openFileDialog.Filter = "lrc files|*.lrc|All files|*.*";
            if (MIDIfileName != null || MIDIfileName != "")
                openFileDialog.InitialDirectory = Path.GetDirectoryName(MIDIfileName);


            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fileName = openFileDialog.FileName;

                try
                {
                    using (StreamReader sr = new StreamReader(fileName))
                    {
                        String lines = sr.ReadToEnd();
                        //Console.WriteLine(lines);
                        LoadLRCFile(lines);
                    }
                }
                catch (Exception errl)
                {
                    Console.WriteLine("The file could not be read:");
                    Console.WriteLine(errl.Message);
                }
            }
        }

        /// <summary>
        /// Load a LRC file (times + lyrics)
        /// </summary>
        /// <param name="Source"></param>
        private void LoadLRCFile(string Source)
        {
            Lyrics lyrics = new Lyrics();
            lyrics.ArrangeLyrics(Source);


            // Clear dgView
            dgView.Rows.Clear();
            
            // Add missing lines before
            int addl = lyrics.Count - dgView.Rows.Count;
            if (addl > 0)
            {
                for (int i = 0; i < addl; i++)
                {
                    dgView.Rows.Add();
                }
            }

            // ADD rows for CR
            addl = dgView.Rows.Count;
            for (int i = 0; i < addl; i++)
            {
                dgView.Rows.Add();
            }

            int plTicksOn = 0;
            string plRealTime = string.Empty;
            string plType = string.Empty;
            string plNote = string.Empty;
            string plElement = string.Empty;            
            int row = 0;

            for (int i = 0; i < lyrics.Count; i++)
            {
                LyricsLine lyline = lyrics[i];
                plRealTime = lyline.Timeline;

                if (row > 0)
                {
                    // Add CR
                    plElement = "";
                    plType = "cr";
                    dgView.Rows[row].Cells[COL_TICKS].Value = plTicksOn;
                    dgView.Rows[row].Cells[COL_TIME].Value = plRealTime;
                    dgView.Rows[row].Cells[COL_TYPE].Value = plType;
                    dgView.Rows[row].Cells[COL_NOTE].Value = plNote;
                    dgView.Rows[row].Cells[COL_TEXT].Value = plElement;
                    row++;
                }
                
                plType = "text";
                plElement = lyline.OriLyrics;

                plTicksOn = TimeToTicks(plRealTime);
                dgView.Rows[row].Cells[COL_TICKS].Value = plTicksOn;
                dgView.Rows[row].Cells[COL_TIME].Value = plRealTime;
                dgView.Rows[row].Cells[COL_TYPE].Value = plType;
                dgView.Rows[row].Cells[COL_NOTE].Value = plNote;
                dgView.Rows[row].Cells[COL_TEXT].Value = plElement;

                row++;
            }

            //Load modification into local list of lyrics
            LoadModifiedLyrics();
            PopulateTextBox(localplLyrics);

            // Color separators
            ColorSepRows();

            // File was modified
            FileModified();
        }

        /// <summary>
        /// Menu : about
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MnuHelpAbout_Click(object sender, EventArgs e)
        {
            frmAboutDialog dlg = new frmAboutDialog();
            dlg.ShowDialog();
        }


        #endregion menus


        #region context menu


        private void DgView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                dgContextMenu = new ContextMenuStrip();
                dgContextMenu.Items.Clear();

                
                // Insert line
                ToolStripMenuItem menuInsertLine = new ToolStripMenuItem(Strings.InsertNewLine);
                dgContextMenu.Items.Add(menuInsertLine);
                menuInsertLine.Click += new System.EventHandler(this.MnuInsertLine_Click);

                // Delete line
                ToolStripMenuItem menuDeleteLine = new ToolStripMenuItem(Strings.DeleteLine);
                dgContextMenu.Items.Add(menuDeleteLine);
                menuDeleteLine.Click += new System.EventHandler(this.MnuDeleteLine_Click);


                ToolStripSeparator menusep1 = new ToolStripSeparator();
                dgContextMenu.Items.Add(menusep1);

                // Décaler vers le haut
                ToolStripMenuItem menuOffsetUp = new ToolStripMenuItem(Strings.OffsetUp);
                dgContextMenu.Items.Add(menuOffsetUp);
                menuOffsetUp.Click += new System.EventHandler(this.MnuOffsetUp_Click);

                // Décaler vers le bas
                ToolStripMenuItem menuOffsetDown = new ToolStripMenuItem(Strings.OffsetDown);
                dgContextMenu.Items.Add(menuOffsetDown);
                menuOffsetDown.Click += new System.EventHandler(this.MnuOffsetDown_Click);

                ToolStripSeparator menusep2 = new ToolStripSeparator();
                dgContextMenu.Items.Add(menusep2);


                // Copier
                ToolStripMenuItem menuCopy = new ToolStripMenuItem(Strings.Copy);
                dgContextMenu.Items.Add(menuCopy);
                menuCopy.Click += new System.EventHandler(this.MnuCopy_Click);

                // Coller
                ToolStripMenuItem menuPaste = new ToolStripMenuItem(Strings.Paste);
                dgContextMenu.Items.Add(menuPaste);
                menuPaste.Click += new System.EventHandler(this.MnuPaste_Click);


                // Display menu on the listview
                dgContextMenu.Show(dgView, dgView.PointToClient(Cursor.Position));
         
            }

        }

        private void MnuDeleteLine_Click(object sender, EventArgs e)
        {
            DeleteLine();
        }

        private void MnuInsertLine_Click(object sender, EventArgs e)
        {           
            InsertLine();
        }

        private void MnuPaste_Click(object sender, EventArgs e)
        {
            int line = dgView.CurrentCell.RowIndex;
            int k = dgView.CurrentCell.ColumnIndex;            

            if (DGV.Count > 0)
            {
                for (int i = 0; i <= DGV.Count - 1; i++)
                {
                    dgView.Rows[line].Cells[k].Value = DGV[i].Value;
                    line++;
                }                                             
            }
        }

        private void MnuCopy_Click(object sender, EventArgs e)
        {
            DGV = this.dgView.SelectedCells;         

            if (dgView.GetCellCount(DataGridViewElementStates.Selected) > 0)
            {
                try
                {
                    Clipboard.SetDataObject(this.dgView.GetClipboardContent());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The Clipboard could not be accessed. Please try again.\n" + ex.Message);
                }
            }

        }


        /// <summary>
        /// Offset down the third column
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MnuOffsetDown_Click(object sender, EventArgs e)
        {
            int r = dgView.CurrentRow.Index;
            int row = 0;

            for (row = dgView.Rows.Count - 1; row > r; row--) 
            {
                dgView.Rows[row].Cells[COL_TEXT].Value = dgView.Rows[row-1].Cells[COL_TEXT].Value;
            }
            dgView.Rows[r].Cells[COL_TEXT].Value = "";
            LoadModifiedLyrics();
            PopulateTextBox(localplLyrics);
        }

        /// <summary>
        /// Offset up the third column
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MnuOffsetUp_Click(object sender, EventArgs e)
        {
            int r = dgView.CurrentRow.Index;
            int row = 0;

            for (row = r; row <= dgView.Rows.Count - 2; row++)
            {
                dgView.Rows[row].Cells[COL_TEXT].Value = dgView.Rows[row + 1].Cells[COL_TEXT].Value;
            }
            LoadModifiedLyrics();
            PopulateTextBox(localplLyrics);
        }

        #endregion context menu


        #region functions

        /// <summary>
        /// File was modified
        /// </summary>
        private void FileModified()
        {
            bfilemodified = true;
            string fName =  Path.GetFileName(MIDIfileName);
            if (fName != null && fName != "")
            {
                string fExt = Path.GetExtension(fName);             // Extension
                fName = Path.GetFileNameWithoutExtension(fName);    // name without extension

                string fShortName = fName.Replace("*", "");
                if (fShortName == fName)
                    fName = fName + "*";

                fName = fName + fExt;         
                SetTitle(fName);
            }
        }

        /// <summary>
        /// Set Title of the form
        /// </summary>
        private void SetTitle(string displayName)
        {
            displayName = displayName.Replace("__", ": ");
            displayName = displayName.Replace("_", " ");
            Text = "Karaboss - Edit Words - " + displayName;
        }

        private void SaveFileProc()
        {
            string fName = Path.GetFileName(MIDIfileName);
            string fPath = Path.GetDirectoryName(MIDIfileName);

            if (fPath == null || fPath == "" || fName == null || fName == "")
            {
                SaveAsFileProc();
                return;
            }

            string fullName = fPath + "\\" + fName;
            if (File.Exists(fullName) == false)
            {
                SaveAsFileProc();
                return;
            }

            if (Application.OpenForms.OfType<frmPlayer>().Count() > 0)
            {
                frmPlayer frmPlayer = GetForm<frmPlayer>();
                frmPlayer.InitSaveFile(fullName);
                
                // Reset title
                bfilemodified = false;
                string displayName = fName;
                SetTitle(displayName);
            }
        }

        /// <summary>
        /// Function: save as file
        /// </summary>
        private void SaveAsFileProc()
        {
            string fName = "New.kar";
            string fPath = Path.GetDirectoryName(MIDIfileName);
            
            string fullName = string.Empty;
            string defName = string.Empty;

            #region search name
            if (fPath == null || fPath == "")
            {                
                if (Directory.Exists(CreateNewMidiFile.DefaultDirectory))
                    fPath = CreateNewMidiFile.DefaultDirectory;
                else
                    fPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            }
            else
            {
                fName = Path.GetFileName(MIDIfileName);
            }

            string inifName = fName;                            // Original name with extension
            string defExt = Path.GetExtension(fName);           // Extension
            fName = Path.GetFileNameWithoutExtension(fName);    // name without extension
            defName = fName;                                    // Proposed name for dialog box

            fullName = fPath + "\\" + inifName;

            if (File.Exists(fullName) == true)
            {
                // Remove all (1) (2) etc..
                string pattern = @"[(\d)]";
                string replace = @"";
                inifName = Regex.Replace(fName, pattern, replace);



                int i = 1;
                string addName = "(" + i.ToString() + ")";
                defName = inifName + addName + defExt;
                fullName = fPath + "\\" + defName;

                while (File.Exists(fullName) == true)
                {
                    i++;
                    defName = inifName + "(" + i.ToString() + ")" + defExt;
                    fullName = fPath + "\\" + defName;
                }
            }

            #endregion search name                   

            string defFilter = "MIDI files (*.mid)|*.mid|Kar files (*.kar)|*.kar|All files (*.*)|*.*";
            if (defExt == ".kar")
                defFilter = "Kar files (*.kar)|*.kar|MIDI files (*.mid)|*.mid|All files (*.*)|*.*";

            saveMidiFileDialog.Title = "Save MIDI file";
            saveMidiFileDialog.Filter = defFilter;
            saveMidiFileDialog.DefaultExt = defExt;
            saveMidiFileDialog.InitialDirectory = @fPath;
            saveMidiFileDialog.FileName = defName;

            if (saveMidiFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fileName = saveMidiFileDialog.FileName;

                MIDIfileName = fileName;

                if (Application.OpenForms.OfType<frmPlayer>().Count() > 0)
                {
                    frmPlayer frmPlayer = GetForm<frmPlayer>();
                    frmPlayer.InitSaveFile(fileName);
                    
                    bfilemodified = false;
                    string displayName = Path.GetFileName(MIDIfileName);
                    SetTitle(displayName);
                }
            }

        }

        /// <summary>
        /// Load modification into list of lyrics
        /// Recharge la liste localpLyrics avec les données de la gridview
        /// </summary>
        private void LoadModifiedLyrics()
        {
            int plTicksOn = 0;
            string val = string.Empty;
            plLyric.Types plType = plLyric.Types.Text;
            string plElement = string.Empty;
            string plReplace = string.Empty;


            localplLyrics = new List<plLyric>();

            for (int row = 0; row < dgView.Rows.Count; row++)
            {
                if (dgView.Rows[row].Cells[COL_TICKS].Value != null)
                {
                    if (dgView.Rows[row].Cells[COL_TICKS].Value != null && dgView.Rows[row].Cells[COL_TICKS].Value.ToString() != "")
                        plTicksOn = Convert.ToInt32(dgView.Rows[row].Cells[COL_TICKS].Value);
                    else
                        plTicksOn = 0;

                    // Type
                    if (dgView.Rows[row].Cells[COL_TYPE].Value != null)
                    {
                        val = dgView.Rows[row].Cells[COL_TYPE].Value.ToString();
                        switch (val)
                        {
                            case "text":
                                plType = plLyric.Types.Text;
                                break;
                            case "cr":
                                plType = plLyric.Types.LineFeed;
                                break;
                            case "par":
                                plType = plLyric.Types.Paragraph;
                                break;
                            default:
                                plType = plLyric.Types.Text;
                                break;
                        }
                    }
                    else
                    {
                        plType = plLyric.Types.Text;
                    }

                    // Element
                    if (dgView.Rows[row].Cells[COL_TEXT].Value != null)
                    {
                        if (plType == plLyric.Types.LineFeed)
                            plElement = "/";
                        else if (plType == plLyric.Types.Paragraph)
                            plElement = "\\";
                        else
                            plElement = dgView.Rows[row].Cells[COL_TEXT].Value.ToString();
                    }
                    else
                        plElement = "text";

                    // replace again spaces
                    plElement = plElement.Replace("_", " ");
                    localplLyrics.Add(new plLyric() { Type = plType, Element = plElement, TicksOn = plTicksOn });

                    // TODO add TicksOff
                }
            }

        }

        /// <summary>
        /// Replace lyrics in frmPlayer
        /// Appelle la méthode ReplaceLyrics de frmPlayer
        /// </summary>
        private void ReplaceLyrics()
        {
            CLyric.LyricTypes ltype;

            if (TextLyricFormat == LyricFormats.Text)
                ltype = CLyric.LyricTypes.Text;
            else
                ltype = CLyric.LyricTypes.Lyric;

            if (Application.OpenForms.OfType<frmPlayer>().Count() > 0)
            {
                frmPlayer frmPlayer = GetForm<frmPlayer>();
                frmPlayer.ReplaceLyrics(localplLyrics, ltype, melodytracknum);
            }

        }

        /// <summary>
        /// Locate form
        /// </summary>
        /// <typeparam name="TForm"></typeparam>
        /// <returns></returns>
        private TForm GetForm<TForm>()
            where TForm : Form
        {
            return (TForm)Application.OpenForms.OfType<TForm>().FirstOrDefault();
        }

        /// <summary>
        /// Display modifications into a textbox
        /// </summary>
        /// <param name="lLyrics"></param>
        private void PopulateTextBox(List<plLyric> lLyrics)
        {
            string plElement = string.Empty;
            plLyric.Types plType = plLyric.Types.Text;
            string tx = string.Empty;

            for (int i = 0; i < lLyrics.Count; i++)
            {
                // Affiche les blancs
                plElement = lLyrics[i].Element;
                //plElement = plElement.Replace("\r", "\r\n");

                plElement = plElement.Replace("\\", "\r\n\r\n");   // Paragraph
                plElement = plElement.Replace("/", "\r\n");        // LineFeed


                plType = lLyrics[i].Type;

                tx += plElement;

            }
            txtResult.Text = tx;

            txtResult.SelectAll();
            txtResult.SelectionAlignment = HorizontalAlignment.Center;

        }

        /// <summary>
        /// Show line of texbox currently edited
        /// </summary>
        private void ShowCurrentLine()
        {
            int r = dgView.CurrentCell.RowIndex;
                       
            // Text before current
            string tx = string.Empty;
            string s = string.Empty;

            for (int row = 0; row < r; row++)
            {
                s = string.Empty;

                if (dgView.Rows[row].Cells[COL_TYPE].Value != null)
                {
                    if (dgView.Rows[row].Cells[COL_TYPE].Value.ToString() == "cr")
                        s = "\n";
                    else if (dgView.Rows[row].Cells[COL_TYPE].Value.ToString() == "par")
                        s = "\n\n";
                    else if (dgView.Rows[row].Cells[COL_TYPE].Value.ToString() == "text")
                    {
                        if (dgView.Rows[row].Cells[COL_TEXT].Value != null)
                            s = dgView.Rows[row].Cells[COL_TEXT].Value.ToString();
                    }

                }
               
                s = s.Replace("_", " ");
                //s = s.Replace("\r", "\n");

                tx += s;
            }

            if (tx != "")
            {
                int start = txtResult.Text.IndexOf(tx);
                if (start == 0)
                {
                    int L = tx.Length;
                    txtResult.SelectionColor = txtResult.ForeColor;

                    txtResult.SelectionStart = 0;
                    txtResult.SelectionLength = L;
                    txtResult.SelectionColor = Color.White;                    
                }
            }
        }

        /// <summary>
        /// height of rows = duration 
        /// </summary>
        private void HeightsToDurations()
        {
            int plTicksOn = 0;
            int n = 0;
            int averageDuration = 0;
            int Duration = 0;
            int H = 0;
            int H0 = 22;
            int newH = 0;
            int delta = 0;
            int previousTime = 0;

            // Average duration
            for (int row = 0; row < dgView.Rows.Count; row++)
            {
                if (dgView.Rows[row].Cells[COL_TICKS].Value != null)
                {
                    plTicksOn = Convert.ToInt32(dgView.Rows[row].Cells[COL_TICKS].Value);
                    if (previousTime == 0)
                    {
                        previousTime = plTicksOn;
                    }
                    else
                    {
                        if (plTicksOn > previousTime)
                        {
                            averageDuration += (plTicksOn - previousTime);
                            previousTime = plTicksOn;
                            n++;
                        }
                    }                    
                }
            }

            if (n > 0)
                averageDuration = averageDuration / n;

            previousTime = 0;
            for (int row = 0; row < dgView.Rows.Count; row++)
            {
                if (dgView.Rows[row].Cells[COL_TICKS].Value != null)
                {
                    plTicksOn = Convert.ToInt32(dgView.Rows[row].Cells[COL_TICKS].Value);
                    if (plTicksOn > 0)
                    {
                        if (previousTime == 0)
                        {
                            previousTime = plTicksOn;
                        }
                        else if (plTicksOn > previousTime)
                        {
                            H = dgView.Rows[row].Height;
                            Duration = plTicksOn - previousTime;
                            delta = Duration / averageDuration;

                            if (delta > 0)
                            {
                                newH = H + H * delta;
                                if (newH > 5 * H0)
                                    newH = 5 * H0;
                                dgView.Rows[row].Height = newH;
                            }
                            previousTime = plTicksOn;
                        }
                    }
                }
            }
        }


        #endregion functions


        #region Option lyrics format
        private void OptFormatText_CheckedChanged(object sender, EventArgs e)
        {
            if (optFormatText.Checked)
                TextLyricFormat = LyricFormats.Text;
        }

        private void OptFormatLyrics_CheckedChanged(object sender, EventArgs e)
        {
            if (optFormatLyrics.Checked)
                TextLyricFormat = LyricFormats.Lyric;
        }



        #endregion


        #region Tags

        /// <summary>
        /// Save tags
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSaveTags_Click(object sender, EventArgs e)
        {
            bool bModified = false;
            string tx = string.Empty;         

            string[] S;
            string newline = string.Empty;

            sequence1.ITag.Clear();
            sequence1.KTag.Clear();
            sequence1.LTag.Clear();
            sequence1.TTag.Clear();
            sequence1.VTag.Clear();
            sequence1.WTag.Clear();

            tx = txtITag.Text.Trim();
            S = tx.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in S)
            {
                newline = line.Trim();
                if (newline != "")
                    sequence1.ITag.Add(line.Trim());
            }
            tx = txtKTag.Text.Trim();
            S = tx.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in S)
            {
                newline = line.Trim();
                if (newline != "")
                    sequence1.KTag.Add(line.Trim());
            }
            tx = txtLTag.Text.Trim();
            S = tx.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in S)
            {
                newline = line.Trim();
                if (newline != "")
                    sequence1.LTag.Add(line.Trim());
            }
            tx = txtTTag.Text.Trim();
            S = tx.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in S)
            {
                newline = line.Trim();
                if (newline != "")
                    sequence1.TTag.Add(line.Trim());
            }
            tx = txtVTag.Text.Trim();
            S = tx.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in S)
            {
                newline = line.Trim();
                if (newline != "")
                    sequence1.VTag.Add(line.Trim());
            }
            tx = txtWTag.Text.Trim();
            S = tx.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in S)
            {
                newline = line.Trim();
                if (newline != "")
                    sequence1.WTag.Add(line.Trim());
            }          

            if (sequence1.ITag.Count != 0 || sequence1.KTag.Count != 0 || sequence1.LTag.Count != 0 || sequence1.TTag.Count != 0 || sequence1.VTag.Count != 0 || sequence1.WTag.Count != 0)
            {
                bModified = true;
            }


            if (bModified == true)
            {
                AddTags();

                if (Application.OpenForms.OfType<frmPlayer>().Count() > 0)
                {
                    frmPlayer frmPlayer = GetForm<frmPlayer>();                    
                    frmPlayer.FileModified();
                }
                MessageBox.Show("Tags saved successfully", "Karaboss", MessageBoxButtons.OK, MessageBoxIcon.Information);               
            }
        }

        /// <summary>
        /// Add tags to midi file
        /// </summary>
        private void AddTags()
        {
            int i = 0;

            // @#Title      Title
            // @#Artist     Artist
            // @#Album      Album
            // @#Copyright  Copyright
            // @#Date       Date
            // @#Editor     Editor
            // @#Genre      Genre        
            // @#Evaluation Evaluation
            // @#Comment    Comment

            // Remove prev tags
            Track track = sequence1.tracks[0];
            track.RemoveTagsEvent("@#");

            string Comment = "@#Comment=" + sequence1.TagComment;
            AddTag(Comment);

            string Evaluation = "@#Evaluation=" + sequence1.TagEvaluation;
            AddTag(Evaluation);

            string Genre = "@#Genre=" + sequence1.TagGenre;
            AddTag(Genre);

            string Editor = "@#Editor=" + sequence1.TagEditor;
            AddTag(Editor);

            string Date = "@#Date=" + sequence1.TagDate;
            AddTag(Date);

            string Copyright = "@#Copyright=" + sequence1.TagCopyright;
            AddTag(Copyright);

            string Album = "@#Album=" + sequence1.TagAlbum;
            AddTag(Album);

            string Artist = "@#Artist=" + sequence1.TagArtist;
            AddTag(Artist);

            string Title = "@#Title=" + sequence1.TagTitle;
            AddTag(Title);

            // Classic Karaoke tags
            string tx = string.Empty;
            track.RemoveTagsEvent("@I");
            track.RemoveTagsEvent("@K");
            track.RemoveTagsEvent("@L");
            track.RemoveTagsEvent("@T");
            track.RemoveTagsEvent("@V");
            track.RemoveTagsEvent("@W");

            for (i = sequence1.ITag.Count - 1; i >= 0; i--)
            {
                tx = "@I" + sequence1.ITag[i];
                AddTag(tx);
            }
            for (i = sequence1.KTag.Count - 1; i >= 0; i--)
            {
                tx = "@K" + sequence1.KTag[i];
                AddTag(tx);
            }
            for (i = sequence1.LTag.Count - 1; i >= 0; i--)
            {
                tx = "@L" + sequence1.LTag[i];
                AddTag(tx);
            }
            for (i = sequence1.TTag.Count - 1; i >= 0; i--)
            {
                tx = "@T" + sequence1.TTag[i];
                AddTag(tx);
            }
            for (i = sequence1.VTag.Count - 1; i >= 0; i--)
            {
                tx = "@V" + sequence1.VTag[i];
                AddTag(tx);
            }
            for (i = sequence1.WTag.Count - 1; i >= 0; i--)
            {
                tx = "@W" + sequence1.WTag[i];
                AddTag(tx);
            }
        }

        /// <summary>
        /// Insert Tag at tick 0
        /// </summary>
        /// <param name="strTag"></param>
        private void AddTag(string strTag)
        {
            Track track = sequence1.tracks[0];
            int currentTick = 0;
            string currentElement = strTag;

            // Transforme en byte la nouvelle chaine
            byte[] newdata = new byte[currentElement.Length];
            for (int u = 0; u < newdata.Length; u++)
            {
                newdata[u] = (byte)currentElement[u];
            }

            MetaMessage mtMsg;

            mtMsg = new MetaMessage(MetaType.Text, newdata);

            // Insert new message
            track.Insert(currentTick, mtMsg);
        }

        #endregion
    }
}
