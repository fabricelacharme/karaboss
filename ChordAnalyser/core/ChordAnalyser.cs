﻿#region License

/* Copyright (c) 2024 Fabrice Lacharme
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
using System.Linq;
using Sanford.Multimedia.Midi;
using ChordAnalyser;


namespace ChordsAnalyser
{
    public class ChordAnalyser
    {        

        readonly Analyser Analyser = new Analyser();
        
        static readonly List<MidiNote[]> lnMidiNote = new List<MidiNote[]>();
        static List<int[]> lnIntNote = new List<int[]>();
        static readonly List<string[]> lnStringNote = new List<string[]>();

        #region properties

        // Search by half measure
        // int measure
        // string Chord 1st half measure
        // string chord 2nd half measure
        public Dictionary<int, (string, string)> Gridchords { get; set; }

        // Dictionary chodr by beat      
       // int beat
       // string chord, int ticks
        public Dictionary<int, (string, int)> GridBeatChords { get; set; }


        private readonly static Dictionary<int, List<int>> dictnotes = new Dictionary<int, List<int>>();

        #endregion properties

        #region private
        private static Sequence sequence1 = new Sequence();        
        
        // Midifile characteristics
        private double _duration = 0;  // en secondes
        private int _totalTicks = 0;        
        private double _ppqn;
        private int _tempo;
        private int _measurelen = 0;
        private int _nbMeasures;
        private int _nbBeats;

        private readonly string ChordNotFound = "<Chord not found>";
        private readonly List<string> LstNoChords = new List<string>() { "<Chord not found>" };
        private readonly string EmptyChord = "<Empty>";
        private readonly List<string> LstEmptyChords = new List<string>() { "<Empty>" };

        #endregion private

        public ChordAnalyser() { }

        
        public ChordAnalyser(Sequence seq)
        {
            sequence1 = seq;

            UpdateMidiTimes();

            Gridchords = new Dictionary<int, (string, string)>(_nbMeasures);
            for (int i = 1; i <= _nbMeasures; i++)
            {
                Gridchords[i] = (ChordNotFound, ChordNotFound); 
            }


            // Dictionary :
            // int = measure number
            // string : chord
            GridBeatChords = new Dictionary<int, (string, int)>(_nbBeats);
            for (int i = 1; i <= _nbBeats; i++)
            {
                GridBeatChords[i] = (ChordNotFound, 0);
            }
            
            // Search by half measure
            SearchByHalfMeasureMethod();

            // Transfers gridchods to GridBeatChords
            CreateGridBeatChords();

            // Populate List of chords by ticks: lstChords
            //PopulateListChords();

            // Other method : Search by beat
            //SearchByBeatMethod();



        }


        private void SearchByHalfMeasureMethod()
        {
            // Search notes in measures
            SearchByHalfMeasure();

            // if search notes fails, take the bass line
            //SearchByBass();

            // display results
            //PublishResults(Gridchords);

        }


        private void SearchByBeatMethod()
        {
            SearchByBeat();
        }

        #region variante

        private void SearchByBeat()
        {
            int nbBeatsPerMeasure = sequence1.Numerator;
            int measure;
            int beat;
            int timeinmeasure;

            // init dictionary                        
            for (int i = 1; i <= _nbBeats; i++)
            {
                dictnotes[i] = new List<int>();                                
            }

            //Search notes
            foreach (Sanford.Multimedia.Midi.Track track in sequence1)
            {
                if (track.ContainsNotes && track.MidiChannel != 9)
                {
                    foreach (MidiNote note in track.Notes)
                    {
                        measure = DetermineMeasure(note.StartTime);
                        timeinmeasure = (int)GetTimeInMeasure(note.StartTime);
                        beat = 1 + (measure - 1) * nbBeatsPerMeasure + timeinmeasure;
                        
                        // Harvest notes belonging to each beat
                        dictnotes[beat].Add(note.Number);
                    }
                }
            }  
            
            for (int ibeat = 1; ibeat <= _nbBeats; ibeat++)
            {
                SearchBeat(ibeat);
            } 

        }

        // Search chord for each beat
        /*
        private void SearchBeat2(int beat)
        {            
            if (dictnotes[beat].Count > 0)
            {
                int rootnote;
                
                List<string> notletters = TransposeToLetterChord(dictnotes[beat]);
                
                // Remove doubles
                notletters = notletters.Distinct().ToList();

                // Remove impossible chords
                notletters = CheckImpossibleChordString(notletters);

                // Translate strings to int
                List<int> lstInt = TransposeToIntChord(notletters);
                rootnote = lstInt[0];

                // Get all Combinations of notes numbers          
                List<List<int>> llnotes = GetAllChordsCombinations(lstInt);
                

                // Minor the value of the notes of a chord and ensure that each note has a value greater than the previous one.
                llnotes = SortChordsByNotesNumber(llnotes);


                // Search major or minor triads note                    
                List<int> lroot = DetermineRoot(llnotes, rootnote);

                List<List<List<int>>> lroots = DetermineRoots(llnotes);

                if (lroot != null)
                {
                    string res = Analyser.determine(lroot);
                    GridBeatChords[beat] = res;                    
                }
                else
                {
                    GridBeatChords[beat] = ChordNotFound;
                }

            }
        }

        */

        
        
        private void SearchBeat(int beat)
        {
            if (dictnotes[beat].Count > 0)
            {
                List<string> notletters = TransposeToLetterChord(dictnotes[beat]);
                // Dictionary with notes sorted by apparition
                Dictionary<string, int> dictbestnotes = GetBestNotes(notletters);

                int ticks = beat;

                // Remove doubles
                // TODO
                // Notes appearing more frequently than others should be favorised
                // Pb found: a chord C with many (C, E, G) and just a single "A" note is viewed as a "Am7" chord (A, C, E, G) 

                // Ex
                // (C, 10), (G, 5), (E, 8), (A, 2)
                // => chord C, E, G
                // If more than 3 notes => search first 3 more frequent notes
                var sortedDict = from entry in dictbestnotes orderby entry.Value descending select entry;
                dictbestnotes = sortedDict.ToDictionary<KeyValuePair<string, int>, string, int>(pair => pair.Key, pair => pair.Value);

                List<string> bestnotletters = new List<string>();
                //List<string> bestnotlettersbis = new List<string>();
                List<string> bestnotletters4 = new List<string>();

                // Hard selection => bestnotletters
                //List<string> v = dictbestnotes.Take(3);

                int moy = 0;
                int lastvalue = 0;
                if (dictbestnotes.Count >= 3)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        bestnotletters.Add(dictbestnotes.ElementAt(i).Key);
                        //bestnotlettersbis.Add(dictbestnotes.ElementAt(i).Key);
                        bestnotletters4.Add(dictbestnotes.ElementAt(i).Key);
                        moy += dictbestnotes.ElementAt(i).Value;
                    }
                    moy = moy / 3;
                    lastvalue = dictbestnotes.ElementAt(2).Value;

                    if (dictbestnotes.Count > 3)
                    {
                        /*
                        if (lastvalue > 1)
                        {
                            for (int i = 3; i < dictbestnotes.Count; i++)
                            {
                                if (dictbestnotes.ElementAt(i).Value >= lastvalue - 1)
                                    bestnotletters.Add(dictbestnotes.ElementAt(i).Key);
                            }
                        }
                        */
                        for (int i = 3; i < dictbestnotes.Count; i++)
                        {
                            if (dictbestnotes.ElementAt(i).Value >= moy)
                                bestnotletters.Add(dictbestnotes.ElementAt(i).Key);
                        }

                        bestnotletters4.Add(dictbestnotes.ElementAt(3).Key);
                        //bestnotlettersbis[2] = bestnotletters4[3];
                    }
                }

                // Try best notes, if not, try all notes                
                //notletters = notletters.Distinct().ToList();
                //notletters = bestnotletters;
                List<int> lroot = null;

                // Try with best notes
                if (bestnotletters.Count > 2)
                    lroot = GetChord(bestnotletters);

                /*
                if (lroot == null && bestnotlettersbis.Count > 2)
                {
                    lroot = GetChord(bestnotlettersbis);
                    if (lroot != null)
                        Console.WriteLine("************ best note bis succeeded");
                }
                */

                if (lroot == null && bestnotletters4.Count == 4)
                    lroot = GetChord(bestnotletters4);

                if (lroot == null)
                {
                    // Try with all notes
                    notletters = notletters.Distinct().ToList();
                    if (notletters.Count > 2)
                        lroot = GetChord(notletters);
                }

                             

                if (lroot != null)
                {
                    string res = Analyser.determine(lroot);
                    GridBeatChords[beat] = (res, ticks);
                }
                else
                {
                    GridBeatChords[beat] = (ChordNotFound, ticks);
                }

            }
        }
        
        #endregion variante



        #region Search by half measure method

        /// <summary>
        /// Harvest notes
        /// </summary>
        private void SearchByHalfMeasure()
        {
            int tStart;
            int tEnd;
            int MeasureEnd;
            int MeasureStart;
            float st;
            
            // Collect all notes of all tracks for each measure and try to fing a chord
            for (int _measure = 1; _measure <= _nbMeasures; _measure++)
            {

                // Create a list only for permutations                
                List<int> lstfirstmidiNotes = new List<int>();
                List<int> lstSecmidiNotes = new List<int>();

                #region harvest notes per measure
                // Harvest notes on each measure
                foreach (Sanford.Multimedia.Midi.Track track in sequence1)
                {
                    if (track.ContainsNotes && track.MidiChannel != 9)
                    {
                        foreach (MidiNote note in track.Notes)
                        {
                            tStart = note.StartTime;                                                        
                            MeasureStart = DetermineMeasure(tStart);
                            
                            // Note after current measure => exit
                            if (MeasureStart > _measure)
                                break;

                            // Bornes de la mesure courante
                            int startmeasureticks = (_measure - 1) * _measurelen;
                            int endmeasureticks = _measure * _measurelen;

                            tEnd = note.EndTime;
                            MeasureEnd = DetermineMeasure(tEnd);
                           

                            st = -1;
                            
                            if (MeasureStart < _measure && MeasureEnd >= _measure)
                            {
                                // Keep note if note starts in previous measure, but is mostly in current measure (or next ones)                                
                                if (tEnd - startmeasureticks > startmeasureticks - tStart)
                                    st = 0;
                            }
                            else if (MeasureStart == _measure && MeasureEnd == _measure)
                            {
                                // Keep note if note entirely located in current measure
                                st = GetTimeInMeasure(tStart);
                            }
                            else if (MeasureStart == _measure &&  MeasureEnd > _measure)
                            {
                                // Keep note if note starts in current measure, and continue in next one, but is mostly in current measure                                
                                if(endmeasureticks - tStart > tEnd - endmeasureticks)
                                    st = GetTimeInMeasure(tStart);
                            }
                                                        
                            
                            if (st != -1)
                            {
                                // Treat differently 3/4 and 4/4                                
                                if (sequence1.Numerator % 3 == 0)
                                {
                                    if (st <= 2* sequence1.Numerator / 3)
                                    {
                                        // add note to first part of the measure
                                        lstfirstmidiNotes.Add(note.Number);
                                    }
                                    else
                                    {
                                        // Add note to second part of the measure
                                        lstSecmidiNotes.Add(note.Number);
                                    }
                                }
                                else
                                {
                                    if (st < sequence1.Numerator / 2)
                                    {
                                        // add note to first part of the measure
                                        lstfirstmidiNotes.Add(note.Number);
                                    }
                                    else
                                    {
                                        // Add note to second part of the measure
                                        lstSecmidiNotes.Add(note.Number);
                                    }
                                }

                            }
                        }
                    }
                }
                #endregion harvest notes per measure


                #region search
 
                SearchMeasure(_measure, 1, lstfirstmidiNotes);

                SearchMeasure(_measure, 2, lstSecmidiNotes);

                #endregion search
            }
        }

      
        /// <summary>
        /// Search chords in each measure
        /// </summary>
        /// <param name="_measure"></param>
        /// <param name="section"></param>
        /// <param name="notes"></param>
        private void SearchMeasure(int _measure, int section,  List<int> notes)
        {
            if (notes.Count == 0)
            {
                if (section == 1)
                {
                    if (Gridchords[_measure].Item1 == ChordNotFound)
                        Gridchords[_measure] = (EmptyChord, Gridchords[_measure].Item2);
                }
                else if (section == 2)
                {
                    if (Gridchords[_measure].Item2 == ChordNotFound)
                        Gridchords[_measure] = (Gridchords[_measure].Item1, EmptyChord);
                }
            }
            else
            {
                List<string> notletters = TransposeToLetterChord(notes);
                // Dictionary with notes sorted by apparition
                Dictionary<string, int> dictbestnotes = GetBestNotes(notletters);


                // Remove doubles
                // TODO
                // Notes appearing more frequently than others should be favorised
                // Pb found: a chord C with many (C, E, G) and just a single "A" note is viewed as a "Am7" chord (A, C, E, G) 

                // Ex
                // (C, 10), (G, 5), (E, 8), (A, 2)
                // => chord C, E, G
                // If more than 3 notes => search first 3 more frequent notes
                var sortedDict = from entry in dictbestnotes orderby entry.Value descending select entry;
                dictbestnotes = sortedDict.ToDictionary<KeyValuePair<string, int>, string, int>(pair => pair.Key, pair => pair.Value);

                // 19/12/2024
                // Traiter ce cas de figure : supprimer le C# pas compatible avec le C
                /*
                    {[C, 14]}
                    {[A, 4]}
                    {[G, 3]}
                    {[C#, 2]}
                    {[E, 2]}
                    {[D, 1]}
                */
                dictbestnotes = FilterBestNotes(dictbestnotes);   


                List<string> bestnotletters = new List<string>();                
                List<string> bestnotletters4 = new List<string>();
                

                int moy = 0;
                int lastvalue = 0;
                if (dictbestnotes.Count >= 3)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        bestnotletters.Add(dictbestnotes.ElementAt(i).Key);                        
                        bestnotletters4.Add(dictbestnotes.ElementAt(i).Key);
                        moy += dictbestnotes.ElementAt(i).Value;                        
                    }
                    moy = moy / 3;
                    lastvalue = dictbestnotes.ElementAt(2).Value;

                    if (dictbestnotes.Count > 3)
                    {
                        
                        for (int i = 3; i < dictbestnotes.Count; i++)
                        {
                            if (dictbestnotes.ElementAt(i).Value >= moy)
                                bestnotletters.Add(dictbestnotes.ElementAt(i).Key);
                        }

                        bestnotletters4.Add(dictbestnotes.ElementAt(3).Key);
                        
                    }
                } 


                // Try best notes, if not, try all notes                                
                List<int> lroot = null;
                
                // Try with best notes
                if (bestnotletters.Count > 2)
                    lroot = GetChord(bestnotletters);
                

                if (lroot == null && bestnotletters4.Count == 4)                
                    lroot = GetChord(bestnotletters4);

                if (lroot == null) { 
                    // Try with all notes
                    notletters = notletters.Distinct().ToList();
                    if (notletters.Count > 2)
                        lroot = GetChord(notletters);
                }

               
                if (lroot != null)
                {
                    string res = Analyser.determine(lroot);
                    if (section == 1)
                    {
                        if (Gridchords[_measure].Item1 == ChordNotFound)
                            Gridchords[_measure] = (res, Gridchords[_measure].Item2);
                    }
                    else if (section == 2)
                    {
                        if (Gridchords[_measure].Item2 == ChordNotFound)
                            Gridchords[_measure] = (Gridchords[_measure].Item1, res);

                    }
                }                
            }
        }

        private void SearchByBass()
        {
            // Collect all notes of all tracks for each measure and try to fing a chord
            for (int _measure = 1; _measure <= _nbMeasures; _measure++)
            {
                // Create a list only for permutations                
                List<int> lstfirstmidiNotes = new List<int>();
                List<int> lstSecmidiNotes = new List<int>();

                #region harvest notes per measure
                // Search bass track
                foreach (Sanford.Multimedia.Midi.Track track in sequence1)
                {
                    // Consider only bass tracks
                    if (32 <= track.ProgramChange && track.ProgramChange <= 39 && track.ContainsNotes)
                    {
                        foreach (MidiNote note in track.Notes)
                        {
                            int Measure = DetermineMeasure(note.StartTime);

                            if (Measure > _measure)
                                break;

                            if (Measure == _measure)
                            {
                                float st = GetTimeInMeasure(note.StartTime);

                                if (st < sequence1.Denominator / 2)
                                {
                                    // add note to first part of the measure
                                    lstfirstmidiNotes.Add(note.Number);
                                }
                                else
                                {
                                    // Add note to second part of the measure
                                    lstSecmidiNotes.Add(note.Number);
                                }
                            }
                        }
                    }

                }

                #endregion harvest notes per measure

                #region search

                SearchMeasureBass(_measure, 1, lstfirstmidiNotes);

                SearchMeasureBass(_measure, 2, lstSecmidiNotes);

                #endregion search

            }
        }

        private void SearchMeasureBass(int _measure, int section, List<int> notes)
        {
            if (notes.Count == 0)
                return;

            int rootnote;

            List<string> notletters = TransposeToLetterChord(notes);
            

            // Remove doubles
            notletters = notletters.Distinct().ToList();

            // Remove impossible chords
            notletters = CheckImpossibleChordString(notletters);

            // Translate strings to int
            List<int> lstInt = TransposeToIntChord(notletters);
            rootnote = lstInt[0];

            // Get all Combinations of notes numbers          
            List<List<int>> llnotes = GetAllChordsCombinations(lstInt);

            // Minor the value of the notes of a chord and ensure that each note has a value greater than the previous one.
            llnotes = SortChordsByNotesNumber(llnotes);


            // Search major or minor triads note                    
            List<int> lroot = DetermineRoot(llnotes, rootnote);

            if (lroot != null)
            {
                string res = Analyser.determine(lroot);
                if (section == 1)
                {
                    if (Gridchords[_measure].Item1 == ChordNotFound)
                        Gridchords[_measure] = (res, Gridchords[_measure].Item2);
                }
                else if (section == 2)
                {
                    if (Gridchords[_measure].Item2 == ChordNotFound)
                        Gridchords[_measure] = (Gridchords[_measure].Item1, res);

                }
            }
            else
            {
                string res = Analyser.determine(llnotes[0]);
                if (res != "")
                {
                    if (section == 1)
                    {
                        if (Gridchords[_measure].Item1 == ChordNotFound)
                            Gridchords[_measure] = (res, Gridchords[_measure].Item2);
                    }
                    else if (section == 2)
                    {
                        if (Gridchords[_measure].Item2 == ChordNotFound)
                            Gridchords[_measure] = (Gridchords[_measure].Item1, res);

                    }
                }
            }


        }

        #endregion Search method


        #region publish results
        /*
        /// <summary>
        /// Populate list of chords by ticks: lstChords
        /// </summary>
        private void PopulateListChords()
        {
            int nbBeatsPerMeasure = sequence1.Numerator;
            int numerator = nbBeatsPerMeasure;
            int measure;
            int beat;            
            int beatDuration = _measurelen / nbBeatsPerMeasure;            
            int ticks;

            string chordName = string.Empty;
            string lastChordName = "-1";


            lstChords = new List<(int, string)> ();

            for (int i = 1; i <= Gridchords.Count; i++)
            {
                measure = i;

                // 1st half
                chordName = Gridchords[i].Item1;
                if (chordName != string.Empty && chordName != EmptyChord && chordName != ChordNotFound && chordName != lastChordName)
                {
                    lastChordName = chordName;
                    beat = 1 + (measure - 1) * numerator;
                    ticks = beatDuration * (beat - 1);
                    lstChords.Add((ticks, chordName));
                }

                // 2nd half
                chordName = Gridchords[i].Item2;
                if (chordName != string.Empty && chordName != EmptyChord && chordName != ChordNotFound && chordName != lastChordName)
                {
                    lastChordName = chordName;
                    beat = 1 + (measure - 1) * numerator + (numerator / 2);
                    ticks = beatDuration * (beat - 1);
                    lstChords.Add((ticks, chordName));
                }
            }
        }
        */

                /// <summary>
                /// Store results in dictionnary chords by beat GridBeatChord
                /// </summary>
        private void CreateGridBeatChords()
        {
            int measure;
            int beat;
            int ticks;
            string chordName;
            string lastChordName = "-1";
            int numerator = sequence1.Numerator;
            int nbBeatsPerMeasure = sequence1.Numerator;
            int beatDuration = _measurelen / nbBeatsPerMeasure;

            GridBeatChords = new Dictionary<int, (string, int)>(_nbBeats);
            for (int i = 1; i <= _nbBeats; i++) 
            {
                ticks = (i - 1) * beatDuration;
                GridBeatChords[i] = (ChordNotFound, ticks);            
            }

            for (int i = 1; i <= Gridchords.Count; i++)
            {
                measure = i;

                // 1st half
                chordName = Gridchords[i].Item1;
                if (chordName != string.Empty && chordName != EmptyChord && chordName != ChordNotFound && chordName != lastChordName)
                {
                    lastChordName = chordName;
                    beat = 1 + (measure - 1) * numerator;
                    ticks = (beat - 1) * beatDuration;
                    GridBeatChords[beat] = (chordName, ticks);

                }

                // 2nd half
                chordName = Gridchords[i].Item2;
                if (chordName != string.Empty && chordName != EmptyChord && chordName != ChordNotFound && chordName != lastChordName)
                {
                    lastChordName = chordName;
                    beat = 1 + (measure - 1) * numerator + (numerator / 2);
                    ticks = (beat - 1) * beatDuration;
                    GridBeatChords[beat] = (chordName, ticks);
                }
            }
        }
        

        /// <summary>
        /// DEBUG /Display result
        /// </summary>
        /// <param name="dict"></param>
        private void PublishResults(Dictionary<int, (string, string)> dict)
        {

            foreach (KeyValuePair<int, (string, string)> pair in dict)
            {
                Console.WriteLine(string.Format("{0} - {1}", pair.Key, pair.Value));
            }
        }

        #endregion publish results



        #region MIDI

        /// <summary>
        /// Upadate MIDI times
        /// </summary>
        private void UpdateMidiTimes()
        {
            _totalTicks = sequence1.GetLength();
            _tempo = sequence1.Tempo;            
            _ppqn = sequence1.Division;
            
            //_duration = _tempo * (_totalTicks / _ppqn) / 1000000; //seconds
            
                        
            if (sequence1.Time != null)
            {                
                _measurelen = sequence1.Time.Measure;                                
                _nbMeasures = Convert.ToInt32(Math.Ceiling((double)_totalTicks / _measurelen)); // rounds up to the next full integer                 

                int nbBeatsPerMeasure = sequence1.Numerator;
                int beatDuration = _measurelen / nbBeatsPerMeasure;                
                _nbBeats = _nbMeasures * nbBeatsPerMeasure;
            }
        }


        /// <summary>
        /// Return in which measure is the chord
        /// </summary>
        /// <param name="chord"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private int DetermineMeasure(int ticks)
        {
            return 1 + ticks / _measurelen;            
        }


        /// <summary>
        /// Get time inside measure
        /// </summary>
        /// <param name="ticks"></param>
        /// <returns></returns>
        public float GetTimeInMeasure(int ticks)
        {
            // Num measure
            int curmeasure = 1 + ticks / _measurelen;
            // Temps dans la mesure
            float timeinmeasure = sequence1.Numerator - ((curmeasure * _measurelen - ticks) / (float)(_measurelen / sequence1.Numerator));
            
            return timeinmeasure;                       
        }

        #endregion MIDI


        #region private functions
        private List<int> GetChord(List<string> chord)
        {
            if (chord.Count == 0)
                return null;
            

            // Remove impossible chords
            chord = CheckImpossibleChordString(chord);

            // Translate strings to int                
            List<int> lstInt = TransposeToIntChord(chord);

            // La note la plus trouvée est la première
            int rootnote = lstInt[0];

            // Get all Combinations of notes numbers          
            List<List<int>> llnotes = GetAllChordsCombinations(lstInt);

            // Minor the value of the notes of a chord and ensure that each note has a value greater than the previous one.
            llnotes = SortChordsByNotesNumber(llnotes);


            // Search major or minor triads note                    
            List<int> lroot = DetermineRoot(llnotes, rootnote);

            // Alternative
            /*
            List<List<List<int>>> lroots = DetermineRoots(llnotes);
            foreach (List<List<int>> l in lroots)
            {
                List<int> lr = DetermineRoot(l);
                string res = Analyser.determine(lr);
                Console.WriteLine(res);
            }
            */

            return lroot;

        }

        private List<int> CheckImpossibleChordInt(List<int> lstInt)
        {
            List<string> notletters = TransposeToLetterChord(lstInt);
            List<string> ress = new List<string>();
            foreach (string s in notletters)
                ress.Add(s);


            List<int> res = new List<int>();
            List<string> letters = new List<string>() { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

            foreach (string s in notletters)
            {
                if (s.Length == 1)
                    if (notletters.Contains(s + "#"))
                        ress.Remove(s + "#");
            }

            for (int i = 0; i < ress.Count; i++)
            {
                res.Add(letters.IndexOf(ress[i]));
            }

            return res;
        }


        /// <summary>
        /// Remove impossible notes from the dictionary 
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        Dictionary<string, int> FilterBestNotes(Dictionary<string, int> dict)
        {

            /*
                {[C, 14]}
                {[A, 4]}
                {[G, 3]}
                {[C#, 2]}
                {[E, 2]}
                {[D, 1]}
                */
            Dictionary<string, int> res = new Dictionary<string, int>();
            string chord;
            //string s;

            if (dict.Count <= 3)
                return dict;
            
            // Filter best chord 
            chord = dict.ElementAt(0).Key;
            res.Add(dict.ElementAt(0).Key,dict.ElementAt(0).Value);

            int x = TransposeToInt(chord);
            int y;
            for (int i = 1; i < dict.Count; i++)
            {
                chord = dict.ElementAt(i).Key;
                y = TransposeToInt(chord);
                if (y != x - 1 && y != x + 1)
                    res.Add(dict.ElementAt(i).Key, dict.ElementAt(i).Value);
            }

            /*
            if (chord.Length == 1)
            {
                for (int i = 1; i < dict.Count; i++)
                {
                    s = dict.ElementAt(i).Key;
                    if (s != chord + "#")
                    {
                        res.Add(dict.ElementAt(i).Key, dict.ElementAt(i).Value);
                    }
                }

            }
            else if (chord.Length == 2) 
            {
                for (int i = 1; i < dict.Count; i++)
                {
                    s = dict.ElementAt(i).Key;
                    if (s != chord.Substring(0, 1))
                    {
                        res.Add(dict.ElementAt(i).Key, dict.ElementAt(i).Value);
                    }
                }
            }
            */
            return res;

        }

        

        private List<string> CheckImpossibleChordString(List<string> lstString)
        {
            List<string> res = new List<string>();
            foreach (string s in lstString)
                res.Add(s);

            foreach (string s in lstString)
            {
                if (s.Length == 1)
                {
                    if (lstString.Contains(s + "#"))
                    {
                        while (res.Contains(s + "#"))
                            res.Remove(s + "#");
                    }
                }
            }
            return res;

        }

       

        /// <summary>
        /// Remove a note if in double inside a chord
        /// If two C exists, remove one
        /// </summary>
        /// <param name="notes"></param>
        /// <returns></returns>
        private List<List<int>> RemoveDoubles(List<List<int>> lschords)
        {
            List<List<int>> res = new List<List<int>>();
            //int n = 0;
            for (int j = 0; j < lschords.Count; j++)
            {
                List<int> lsnotes = lschords[j];
                for (int i = 0; i < lsnotes.Count; i++)                
                    lsnotes[i] = lsnotes[i] % 12;
                
                lsnotes = lsnotes.Distinct().ToList();
                res.Add(lsnotes);
            }

            return res;
        }

        /// <summary>
        /// Retrieve notes letter for a chord
        /// </summary>
        /// <param name="notes"></param>
        /// <returns></returns>
        private List<string> TransposeToLetterChord(List<int> notes)
        {
            List<string> letters = new List<string>() { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"};            
            List<string> res = new List<string>();            
            string l = string.Empty;
            int x = 0;
            foreach (int n in notes)
            {                
                x = n % 12;
                l = letters[x];
                res.Add(l);
            }            
            return res;
        }

        /// <summary>
        /// Transpose letters to int
        /// </summary>
        /// <param name="notes"></param>
        /// <returns></returns>
        private List<int> TransposeToIntChord(List<string> notes)
        {
            List<int> res = new List<int>();
            Dictionary<string, int> _note_dict = new Dictionary<string, int>() { { "C", 0 }, { "D", 2 }, { "E", 4 }, { "F", 5 }, { "G", 7 }, { "A", 9 }, { "B", 11 } };

            for (int i = 0; i < notes.Count; i++)
            {
                string n = notes[i];
                string n0 = n.Substring(0, 1);
                
                int x = _note_dict[n0];
                if (n.Length > 1)
                {
                    if (n.Substring(1, 1) == "#")
                    {
                        x += 1;
                    }
                    else
                    {
                        x -= 1;
                        if (x < 0)  // Cb = B
                            x = 11;
                    }
                }

                res.Add(x);
            }
            return res;
        }

        private int TransposeToInt(string n)
        {            
            Dictionary<string, int> _note_dict = new Dictionary<string, int>() { { "C", 0 }, { "D", 2 }, { "E", 4 }, { "F", 5 }, { "G", 7 }, { "A", 9 }, { "B", 11 } };
            string n0 = n.Substring(0, 1);
            int x = _note_dict[n0];
            if (n.Length > 1)
            {
                if (n.Substring(1, 1) == "#")
                {
                    x += 1;
                }
                else
                {
                    x -= 1;
                    if (x < 0)  // Cb = B
                        x = 11;
                }
            }

            return x;
        }


        /// <summary>
        /// Return all combinations of notes numbers
        /// </summary>
        /// <param name="chord"></param>
        /// <returns></returns>
        List<List<int>> GetAllChordsCombinations(List<int> lstInt)
        {
            List<List<int>> res = new List<List<int>>();

            int[] intNotes = new int[lstInt.Count];
            for (int i = 0; i < lstInt.Count; i++)
                intNotes[i] = lstInt[i];

            lnIntNote = new List<int[]>();
            // Build ln = list of all combinations of int
            PermuteIntNote(intNotes, 0, lstInt.Count - 1);

            foreach (int[] arry in lnIntNote)
            {
                List<int> lll = new List<int>();
                for (int i = 0; i < arry.Length; i++)
                {
                    lll.Add(arry[i]);
                }
                res.Add(lll);
            }
            return res;
        }

        /// <summary>
        /// Get notes classified with number of appearance
        /// </summary>
        Dictionary<string,int> GetBestNotes(List<string> chords)
        {
            Dictionary<string,int> res = new Dictionary<string,int>();

            foreach(string ch in chords)
            {
                if (!res.ContainsKey(ch) )
                {
                    res[ch] = 1;
                }
                else
                {
                    res[ch] += 1;
                }

            }

            return res;

        }

        #region permutations

        /// <summary>
        /// Get all combinations of a set of values
        /// </summary>
        /// <param name="arry"></param>
        /// <param name="i"></param>
        /// <param name="n"></param>
        static void PermuteMidiNote(MidiNote[] arry, int i, int n)
        {            
            int j;
            if (i == n)
            {                
                MidiNote[] m = new MidiNote[arry.Count()];
                for (int x = 0; x < arry.Count();x++)
                    m[x] = arry[x];
                lnMidiNote.Add(m);
            }
            else
            {
                for (j = i; j <= n; j++)
                {
                    SwapMidiNote(ref arry[i], ref arry[j]);
                    PermuteMidiNote(arry, i + 1, n);
                    SwapMidiNote(ref arry[i], ref arry[j]); //backtrack
                }
            }            
        }

        static void SwapMidiNote(ref MidiNote a, ref MidiNote b)
        {
            MidiNote tmp;
            tmp = a;
            a = b;
            b = tmp;
        }


        static void PermuteIntNote(int[] arry, int i, int n)
        {
            int j;
            if (i == n)
            {
                int[] m = new int[arry.Count()];
                for (int x = 0; x < arry.Count(); x++)
                    m[x] = arry[x];
                lnIntNote.Add(m);
            }
            else
            {
                for (j = i; j <= n; j++)
                {
                    SwapIntNote(ref arry[i], ref arry[j]);
                    PermuteIntNote(arry, i + 1, n);
                    SwapIntNote(ref arry[i], ref arry[j]); //backtrack
                }
            }
        }

        static void SwapIntNote(ref int a, ref int b)
        {
            int tmp;
            tmp = a;
            a = b;
            b = tmp;
        }


        static void PermuteStringNote(string[] arry, int i, int n)
        {
            int j;
            if (i == n)
            {
                string[] m = new string[arry.Count()];
                for (int x = 0; x < arry.Count(); x++)
                    m[x] = arry[x];
                lnStringNote.Add(m);
            }
            else
            {
                for (j = i; j <= n; j++)
                {
                    SwapStringNote(ref arry[i], ref arry[j]);
                    PermuteStringNote(arry, i + 1, n);
                    SwapStringNote(ref arry[i], ref arry[j]); //backtrack
                }
            }
        }
        static void SwapStringNote(ref string a, ref string b)
        {
            string tmp;
            tmp = a;
            a = b;
            b = tmp;
        }

        #endregion permutations


        /// <summary>
        /// Minor the value of the notes of a chord and ensure that each note has a value greater than the previous one.
        /// </summary>
        /// <param name="ll"></param>
        /// <returns></returns>
        private List<List<int>> SortChordsByNotesNumber(List<List<int>> ll)
        {
            int n;
            int prevnumber = 0;
            List<List<int>> res = new List<List<int>>();

            for (int j = 0; j < ll.Count; j++)
            {
                List<int> lsnotes = ll[j];
                int t = lsnotes[0];
                t = t % 12;
                lsnotes[0] = t;
                prevnumber = t;

                for (int i = 1; i < lsnotes.Count; i++)
                {
                    n = lsnotes[i] % 12;
                    if (n < prevnumber)
                    {
                        while (n < prevnumber)
                            n += 12;
                    }
                    lsnotes[i] = n;
                    prevnumber = n;
                }
                res.Add(lsnotes);
            }
            return res;
        }
    

        /// <summary>
        /// Select the chord existing in the proposed list
        /// </summary>
        /// <param name="lsnotes"></param>
        /// <returns></returns>
        private List<int> DetermineRoot(List<List<int>> lsnotes, int rootnote)
        {
            /* {0,1,2} => 1 - 0, 2 - 0 ET {0,2,1} ????
             * {1,2,0} => 2 - 1, 0 - 1 ET {1,0,2}
             * {2,0,1} => 0 - 2, 1 - 2
             * 
             */

            // Search chords having rootnote            
            foreach (List<int> chord in lsnotes)
            {
                if (chord.Count > 3 && chord[0] == rootnote)
                {
                    if (IsMajorChord7(chord) || IsMinorChord7(chord))
                        return chord;
                }
            }

            foreach (List<int> chord in lsnotes)
            {
                if (chord.Count > 2 && chord[0] == rootnote)
                {
                    if (IsMajorChord(chord) || IsMinorChord(chord))
                        return chord;
                }
            }
            

            foreach (List<int> chord in lsnotes)
            {
                if (chord.Count > 3)
                {
                    if (IsMajorChord7(chord) || IsMinorChord7(chord))
                        return chord;
                }
            }
            

            foreach (List<int> chord in lsnotes)
            {                
                if (chord.Count > 2)
                {
                    if (IsMajorChord(chord) || IsMinorChord(chord))
                        return chord;
                }
            }
            

            return null;  
        }

        /// <summary>
        /// Retruns list of possible chords
        /// </summary>
        /// <param name="lsnotes"></param>
        /// <returns></returns>
        private List<List<List<int>>> DetermineRoots(List<List<int>> lsnotes)
        {
            List<List<int>> lstMajorChords = new List<List<int>>();
            List<List<int>> lstMinorChords = new List<List<int>>();
            List<List<int>> lstMajorChords7 = new List<List<int>>();
            List<List<int>> lstMinorChords7 = new List<List<int>>();

            List<List<List<int>>> res = new List<List<List<int>>>();

            foreach (List<int> chord in lsnotes)
            {
                if (chord.Count > 3)
                {
                    if (IsMajorChord7(chord)) 
                    {
                        lstMajorChords7.Add(new List<int> { chord[0], chord[1], chord[2], chord[3] });
                    }
                    else if (IsMinorChord7(chord)) 
                    {
                        lstMinorChords7.Add(new List<int> { chord[0], chord[1], chord[2], chord[3] });
                    }                        
                }

                if (chord.Count > 2)
                {
                    if (IsMajorChord(chord))
                    {                        
                        lstMajorChords.Add(new List<int> { chord[0], chord[1], chord[2] });
                    }
                    else if (IsMinorChord(chord))
                    {                        
                        lstMinorChords.Add(new List<int> { chord[0], chord[1], chord[2] });
                    }
                }
            }

            if (lstMajorChords7.Count > 0)
                res.Add(lstMajorChords7);
            if (lstMinorChords7.Count > 0)
                res.Add(lstMinorChords7);
            if (lstMajorChords.Count > 0)
                res.Add(lstMajorChords);
            if (lstMinorChords.Count > 0)
                res.Add(lstMinorChords);

            return res;
        }

        List<MidiNote> Rotate(List<MidiNote> notes)
        {
            MidiNote first = notes[0];
            notes.RemoveAt(0);
            notes.Add(first);

            return notes;

        }

        /// <summary>
        /// Sort Midi notes by number
        /// </summary>
        /// <param name="notes"></param>
        /// <returns></returns>
        private List<MidiNote> SortNotes(List<MidiNote> notes)
        {
            List<MidiNote> l = new List<MidiNote>();
            var res = from n in notes
                        orderby n.Number
                        ascending
                        select n;
            foreach (var x in res)
            {
                l.Add(x);
            }
            return l;
        }

        static bool IsMajorChord(List<int> notes)
        {
            // Un coup ça marche sans % 12, un coup avec
            // Si les number des notes sont dans le bon ordre, c'est bon 
            if (notes.Count < 3) return false;
            
            // A major chord consists of the root, major third, and perfect fifth
            return (notes[1] - notes[0] == 4) && (notes[2] - notes[0] == 7);
        }

        static bool IsMinorChord(List<int> notes)
        {
            if (notes.Count <3) return false;

            // A minor chord consists of the root, minor third, and perfect fifth
            return (notes[1] - notes[0] == 3) && (notes[2] - notes[0] == 7);
        }

        static bool IsMajorChord7(List<int> notes)
        {
            // Un coup ça marche sans % 12, un coup avec
            // Si les number des notes sont dans le bon ordre, c'est bon 
            if (notes.Count < 4) return false;

            // A major chord consists of the root, major third, and perfect fifth
            return (notes[1] - notes[0] == 4) && (notes[2] - notes[0] == 7) && (notes[3] - notes[0] == 10);
        }

        static bool IsMinorChord7(List<int> notes)
        {
            if (notes.Count < 4) return false;

            // A minor chord consists of the root, minor third, and perfect fifth
            return (notes[1] - notes[0] == 3) && (notes[2] - notes[0] == 7) && (notes[3] - notes[0] == 10);
        }

        #endregion private functions
    }
}
