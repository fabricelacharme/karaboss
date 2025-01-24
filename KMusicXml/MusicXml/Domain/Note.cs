using System.Collections.Generic;
using System.Xml;

namespace MusicXml.Domain
{
	public class Note
	{
		public enum TieTypes
		{
			None,
			Start,
			Stop,
			Both,
		}
		
		internal Note()
		{
			Type = string.Empty;
			Duration = 0;
			TieDuration = 0;
			Voice = 1;
			Staff = -1;
			IsChordTone = false;

			IsDrums = false;
			DrumInstrument = 0;

			Velocity = 80;
			
			OctaveChange = 0;
			ChromaticTranspose = 0;

			// Fab
			Pitch = new Pitch();
			PitchDrums = new Pitch();

			TieType = TieTypes.None;

			//MeasureNumber = 0;
		}

		
		public TieTypes TieType {  get; internal set; }
		
		// Just to determinate if the note has to be played or not (pulsation with harmony)
		public string Stem {  get; internal set; }

		public string Type { get; internal set; }
		
		public int Voice { get; internal set; }

		public int Duration { get; internal set; }
		public int TieDuration { get; internal set; }
        		
		public int Velocity { get; internal set; }

        // FAB : for verses (several lyrics on the same note with different "number")
        public List<Lyric> Lyrics { get; internal set; }

		public int ChromaticTranspose { get; internal set; }
		public int OctaveChange { get; internal set; }

		public Pitch Pitch { get; internal set; }

		public Pitch PitchDrums { get; internal set; }

		public int Staff { get; internal set; }

		public bool IsChordTone { get; internal set; }

		public bool IsDrums { get; internal set; }

		public int DrumInstrument { get; internal set; }

		public bool IsRest { get; internal set; }
		
        public string Accidental { get; internal set; }

		//public int MeasureNumber { get; internal set; }
    }
}
