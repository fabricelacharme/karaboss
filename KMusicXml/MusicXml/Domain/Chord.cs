﻿using MusicXml.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KMusicXml.MusicXml.Domain
{
    public class Chord
    {
        internal Chord() 
        {
            
            // Fab
            Pitch = new Pitch();
        }

        //public string RootNote { get; internal set; }

        public Pitch Pitch { get; internal set; }
    }
}
