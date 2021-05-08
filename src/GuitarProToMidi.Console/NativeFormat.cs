using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NLog;

namespace GuitarProToMidi {
    public class NativeFormat  {
        private readonly string _title;
        private readonly string _subtitle;
        private readonly string _artist;
        private readonly string _album;
        private readonly string _words;
        private readonly string _music;

        private readonly List<Annotation> _annotations = new List<Annotation>();
        private readonly List<Tempo> _tempos;
        private readonly List<MasterBar> _barMaster;
        private readonly List<Track> _tracks;
        private readonly List<int> _notesInMeasures = new List<int>();
        public static readonly bool[] AvailableChannels = new bool[16];

        public MidiExport.MidiExport toMidi()
        {
            MidiExport.MidiExport mid = new MidiExport.MidiExport();
            mid.midiTracks.Add(getMidiHeader()); //First, untitled track
            foreach (Track track in _tracks)
            {
                mid.midiTracks.Add(track.GetMidi());
            }
            return mid;
        }

        private MidiExport.MidiTrack getMidiHeader()
        {
            var midiHeader = new MidiExport.MidiTrack();
            //text(s) - name of song, artist etc., created by Gitaro
            //copyright - by Gitaro
            //midi port 0
            //time signature
            //key signature
            //set tempo
            ///////marker text (will be seen in file) - also Gitaro copyright blabla
            //end_of_track
            midiHeader.messages.Add(new MidiExport.MidiMessage("track_name", new string[] { "untitled" }, 0));
            midiHeader.messages.Add(new MidiExport.MidiMessage("text", new string[] { _title }, 0));
            midiHeader.messages.Add(new MidiExport.MidiMessage("text", new string[] { _subtitle }, 0));
            midiHeader.messages.Add(new MidiExport.MidiMessage("text", new string[] { _artist }, 0));
            midiHeader.messages.Add(new MidiExport.MidiMessage("text", new string[] { _album }, 0));
            midiHeader.messages.Add(new MidiExport.MidiMessage("text", new string[] { _words }, 0));
            midiHeader.messages.Add(new MidiExport.MidiMessage("text", new string[] { _music }, 0));
            midiHeader.messages.Add(new MidiExport.MidiMessage("copyright", new string[] { "Copyright 2017 by Gitaro" }, 0));
            midiHeader.messages.Add(new MidiExport.MidiMessage("marker", new string[] { _title+" / "+_artist+" - Copyright 2017 by Gitaro" }, 0));
            midiHeader.messages.Add(new MidiExport.MidiMessage("midi_port", new string[] { "0" }, 0));

            //Get tempos from List tempos, get key_signature and time_signature from barMaster
            var tempoIndex = 0;
            var masterBarIndex = 0;
            var currentIndex = 0;
            var oldTimeSignature = "";
            var oldKeySignature = "";
            if (_tempos.Count == 0) _tempos.Add(new Tempo());
            while (tempoIndex < _tempos.Count || masterBarIndex < _barMaster.Count)
            {

                //Compare next entry of both possible sources
                if (tempoIndex == _tempos.Count || _tempos[tempoIndex].position >= _barMaster[masterBarIndex].index) //next measure comes first
                {
                    if (!_barMaster[masterBarIndex].keyBoth.Equals(oldKeySignature))
                    {
                        //Add Key-Sig to midiHeader
                        midiHeader.messages.Add(new MidiExport.MidiMessage("key_signature", new string[] { ""+ _barMaster[masterBarIndex].key, ""+ _barMaster[masterBarIndex].keyType }, _barMaster[masterBarIndex].index - currentIndex));
                        currentIndex = _barMaster[masterBarIndex].index;

                        oldKeySignature = _barMaster[masterBarIndex].keyBoth;
                    }
                    if (!_barMaster[masterBarIndex].time.Equals(oldTimeSignature))
                    {
                        //Add Time-Sig to midiHeader
                        midiHeader.messages.Add(new MidiExport.MidiMessage("time_signature", new string[] { "" + _barMaster[masterBarIndex].num, "" + _barMaster[masterBarIndex].den, "24", "8" }, _barMaster[masterBarIndex].index - currentIndex));
                        currentIndex = _barMaster[masterBarIndex].index;

                        oldTimeSignature = _barMaster[masterBarIndex].time;
                    }
                     masterBarIndex++;
                }
                else //next tempo signature comes first
                {
                    //Add Tempo-Sig to midiHeader
                    int _tempo = (int)(Math.Round((60 * 1000000) / _tempos[tempoIndex].value));
                    midiHeader.messages.Add(new MidiExport.MidiMessage("set_tempo", new string[] { "" + _tempo }, _tempos[tempoIndex].position - currentIndex));
                    currentIndex = _tempos[tempoIndex].position;
                     tempoIndex++;
                }
            }



            midiHeader.messages.Add(new MidiExport.MidiMessage("end_of_track", new string[] {  }, 0));


            return midiHeader;
        }


        public NativeFormat(GPFile fromFile)
        {
            _title = fromFile.title;
            _subtitle = fromFile.subtitle;
            _artist = fromFile.interpret;
            _album = fromFile.album;
            _words = fromFile.words;
            _music = fromFile.music;
            _tempos = retrieveTempos(fromFile);
            _barMaster = retrieveMasterBars(fromFile);
            _tracks = retrieveTracks(fromFile);
            updateAvailableChannels();
        }

        private void updateAvailableChannels()
        {

            for (int x = 0; x < 16; x++) { if (x != 9) { AvailableChannels[x] = true; } else { AvailableChannels[x] = false; } }
            foreach (Track track in _tracks)
            {
                AvailableChannels[track.channel] = false;
            }
        }

        public List<Track> retrieveTracks(GPFile file)
        {
            List<Track> tracks = new List<Track>();
            foreach (global::Track tr in file.tracks)
            {
                Track track = new Track();
                track.name = tr.name;
                track.patch = tr.channel.instrument;
                track.port = tr.port;
                track.channel = tr.channel.channel;
                track.playbackState = PlaybackState.def;
                track.capo = tr.offset;
                if (tr.isMute) track.playbackState = PlaybackState.mute;
                if (tr.isSolo) track.playbackState = PlaybackState.solo;
                track.tuning = getTuning(tr.strings);

                track.notes = retrieveNotes(tr, track.tuning, track);
                tracks.Add(track);
            }

            return tracks;
        }

        public void addToTremoloBarList(int index, int duration, BendEffect bend, Track myTrack)
        {
            int at;
            myTrack.tremoloPoints.Add(new TremoloPoint(0.0f,index)); //So that it can later be recognized as the beginning
            foreach (global::BendPoint bp in bend.points)
            {
                at = index + (int)(bp.GP6position * duration / 100.0f);
                var point = new TremoloPoint();
                point.index = at;
                point.value = bp.GP6value;
                myTrack.tremoloPoints.Add(point);
            }
            var tp = new TremoloPoint();
            tp.index = index + duration;
            tp.value = 0;
            myTrack.tremoloPoints.Add(tp); //Back to 0 -> Worst case there will be on the same index the final of tone 1, 0, and the beginning of tone 2.


        }

        public List<BendPoint> getBendPoints(int index, int duration, BendEffect bend)
        {
            List<BendPoint> ret = new List<BendPoint>();
            int at;
            foreach (global::BendPoint bp in bend.points)
            {
                at = index + (int)(bp.GP6position * duration / 100.0f);
                var point = new BendPoint();
                point.index = at;
                point.value = bp.GP6value;
                ret.Add(point);
            }

            return ret;
        }



        public List<Note> retrieveNotes(global::Track track, int[] tuning, Track myTrack)
        {

            List<Note> notes = new List<Note>();
            int index = 0;
            Note[] last_notes = new Note[10];
            bool[] last_was_tie = new bool[10];
            for(int x = 0; x < 10; x++) { last_was_tie[x] = false; }

            //GraceNotes if on beat - reducing the next note's length
            bool rememberGrace = false;
            bool rememberedGrace = false;
            int graceLength = 0;
            int subtractSubindex = 0;

            for (int x = 0; x < 10; x++) last_notes[x] = null;
            int measureIndex = -1;
            int notesInMeasure = 0;
            foreach (Measure m in track.measures)
            {

                notesInMeasure = 0;
                measureIndex++;
                bool skipVoice = false;
                if (m.simileMark == SimileMark.simple) //Repeat last measure
                {
                    int amountNotes = _notesInMeasures[_notesInMeasures.Count-1]; //misuse prohibited by guitarpro
                    int endPoint = notes.Count;
                    for (int x = endPoint - amountNotes; x < endPoint; x++)
                    {
                        Note newNote = new Note(notes[x]);
                        Measure oldM = track.measures[measureIndex - 1];
                        newNote.index += flipDuration(oldM.header.timeSignature.denominator) * oldM.header.timeSignature.numerator;
                        notes.Add(newNote);
                        notesInMeasure++;
                    }
                    skipVoice = true;
                }
                if (m.simileMark == SimileMark.firstOfDouble || m.simileMark == SimileMark.secondOfDouble) //Repeat first or second of last two measures
                {
                    int secondAmount = _notesInMeasures[_notesInMeasures.Count - 1]; //misuse prohibited by guitarpro
                    int firstAmount = _notesInMeasures[_notesInMeasures.Count - 2];
                    int endPoint = notes.Count-secondAmount;
                    for (int x = endPoint - firstAmount; x < endPoint; x++)
                    {
                        Note newNote = new Note(notes[x]);
                        Measure oldM1 = track.measures[measureIndex - 2];
                        Measure oldM2 = track.measures[measureIndex - 1];
                        newNote.index += flipDuration(oldM1.header.timeSignature.denominator) * oldM1.header.timeSignature.numerator;
                        newNote.index += flipDuration(oldM2.header.timeSignature.denominator) * oldM2.header.timeSignature.numerator;
                        notes.Add(newNote);
                        notesInMeasure++;
                    }
                    skipVoice = true;
                }

                foreach (Voice v in m.voices)
                {
                    if (skipVoice) break;
                    int subIndex = 0;
                    foreach (Beat b in v.beats)
                    {

                        if (b.text != null && !b.text.value.Equals("")) _annotations.Add(new Annotation(b.text.value,index+subIndex));

                        if (b.effect.tremoloBar != null) addToTremoloBarList(index + subIndex, flipDuration(b.duration), b.effect.tremoloBar, myTrack);


                        //Prepare Brush or Arpeggio
                        bool hasBrush = false;
                        int brushInit = 0;
                        int brushIncrease = 0;
                        BeatStrokeDirection brushDirection = BeatStrokeDirection.none;

                        if (b.effect.stroke != null)
                        {
                            int notesCnt = b.notes.Count;
                            brushDirection = b.effect.stroke.direction;
                            if (brushDirection != BeatStrokeDirection.none && notesCnt > 1) {
                                hasBrush = true;
                                Duration temp = new Duration();
                                temp.value = b.effect.stroke.value;
                                int brushTotalDuration = flipDuration(temp);
                                int beatTotalDuration = flipDuration(b.duration);


                                brushIncrease = brushTotalDuration / (notesCnt);
                                int startPos = index + subIndex + (int)((brushTotalDuration-brushIncrease) * (b.effect.stroke.startTime - 1));
                                int endPos = startPos + brushTotalDuration - brushIncrease;

                                if (brushDirection == BeatStrokeDirection.down)
                                {
                                    brushInit = startPos;
                                } else
                                {
                                    brushInit = endPos;
                                    brushIncrease = -brushIncrease;
                                }
                            }
                        }

                        foreach (global::Note n in b.notes)
                        {
                            Note note = new Note();
                            //Beat values
                            note.isTremBarVibrato = b.effect.vibrato;
                            note.fading = Fading.none;
                            if (b.effect.fadeIn) note.fading = Fading.fadeIn;
                            if (b.effect.fadeOut) note.fading = Fading.fadeOut;
                            if (b.effect.volumeSwell) note.fading = Fading.volumeSwell;
                            note.isSlapped = b.effect.slapEffect == SlapEffect.slapping;
                            note.isPopped = b.effect.slapEffect == SlapEffect.popping;
                            note.isHammer =  n.effect.hammer;
                            note.isRHTapped = b.effect.slapEffect == SlapEffect.tapping;
                            note.index = index + subIndex;
                            note.duration = flipDuration(b.duration);


                            //Note values
                            note.fret = n.value;
                            note.str = n.str;
                            note.velocity = n.velocity;
                            note.isVibrato = n.effect.vibrato;
                            note.isPalmMuted = n.effect.palmMute;
                            note.isMuted = n.type == NoteType.dead;

                            if (n.effect.harmonic != null) {
                                note.harmonicFret = n.effect.harmonic.fret;
                                if (n.effect.harmonic.fret == 0) //older format..
                                {
                                    if (n.effect.harmonic.type == 2) note.harmonicFret = ((ArtificialHarmonic)n.effect.harmonic).pitch.actualOvertone;
                                }
                                switch (n.effect.harmonic.type)
                                {
                                    case 1: note.harmonic = HarmonicType.natural; break;
                                    case 2: note.harmonic = HarmonicType.artificial; break;
                                    case 3: note.harmonic = HarmonicType.pinch; break;
                                    case 4: note.harmonic = HarmonicType.tapped; break;
                                    case 5: note.harmonic = HarmonicType.semi; break;

                                    default:
                                        note.harmonic = HarmonicType.natural;
                                        break;
                                }
                            }
                            if (n.effect.slides != null) {
                                foreach (SlideType sl in n.effect.slides)
                                {
                                    note.slidesToNext = note.slidesToNext || sl == SlideType.shiftSlideTo || sl == SlideType.legatoSlideTo;
                                    note.slideInFromAbove = note.slideInFromAbove || sl == SlideType.intoFromAbove;
                                    note.slideInFromBelow = note.slideInFromBelow || sl == SlideType.intoFromBelow;
                                    note.slideOutDownwards = note.slideOutDownwards || sl == SlideType.outDownwards;
                                    note.slideOutUpwards = note.slideOutUpwards || sl == SlideType.outUpwards;
                                }
                            }

                            if (n.effect.bend != null) note.bendPoints = getBendPoints(index + subIndex, flipDuration(b.duration), n.effect.bend);

                            //Ties

                            bool dontAddNote = false;

                            if (n.type == NoteType.tie)
                            {


                                    dontAddNote = true;
                                    //Find if note can simply be added to previous note

                                    var last = last_notes[Math.Max(0,note.str-1)];



                                    if (last != null) {
                                        note.fret = last.fret; //For GP3 & GP4
                                        if (last.harmonic != note.harmonic || last.harmonicFret != note.harmonicFret
                                            ) dontAddNote = false;

                                        if (dontAddNote)
                                        {
                                            note.connect = true;
                                            last.duration += note.duration;
                                            last.addBendPoints(note.bendPoints);

                                        }
                                    }

                            } else // not a tie
                            {

                                last_was_tie[Math.Max(0, note.str - 1)] = false;
                            }

                            //Extra notes to replicate certain effects


                            //Triplet Feel
                            if (!_barMaster[measureIndex].tripletFeel.Equals("none"))
                            {
                                TripletFeel trip = _barMaster[measureIndex].tripletFeel;
                                //Check if at regular 8th or 16th beat position
                                bool is_8th_pos = subIndex % 480 == 0;
                                bool is_16th_pos = subIndex % 240 == 0;
                                bool is_first = true; //first of note pair
                                if (is_8th_pos) is_first = subIndex % 960 == 0;
                                if (is_16th_pos) is_first = is_8th_pos;
                                bool is_8th = b.duration.value == 8 && !b.duration.isDotted && !b.duration.isDoubleDotted && b.duration.tuplet.enters == 1 && b.duration.tuplet.times == 1;
                                bool is_16th = b.duration.value == 16 && !b.duration.isDotted && !b.duration.isDoubleDotted && b.duration.tuplet.enters == 1 && b.duration.tuplet.times == 1;

                                if ((trip == TripletFeel.eigth && is_8th_pos && is_8th) || (trip == TripletFeel.sixteenth && is_16th_pos && is_16th))
                                {
                                    if (is_first) note.duration = (int)(note.duration * (4.0f / 3.0f));
                                    if (!is_first)
                                    {
                                        note.duration = (int)(note.duration * (2.0f / 3.0f));
                                        note.resizeValue *= (2.0f / 3.0f);
                                        note.index += (int)(note.duration * (1.0f / 3.0f));
                                    }

                                }
                                if ((trip == TripletFeel.dotted8th && is_8th_pos && is_8th) || (trip == TripletFeel.dotted16th && is_16th_pos && is_16th))
                                {
                                    if (is_first) note.duration = (int)(note.duration * 1.5f);
                                    if (!is_first)
                                    {
                                        note.duration = (int)(note.duration * 0.5f);
                                        note.resizeValue *= (0.5f);
                                        note.index += (int)(note.duration * 0.5f);
                                    }
                                }
                                if ((trip == TripletFeel.scottish8th && is_8th_pos && is_8th) || (trip == TripletFeel.scottish16th && is_16th_pos && is_16th))
                                {
                                    if (is_first) note.duration = (int)(note.duration * 0.5f);
                                    if (!is_first)
                                    {
                                        note.duration = (int)(note.duration * 1.5f);
                                        note.resizeValue *= (1.5f);
                                        note.index -= (int)(note.duration * 0.5f);
                                    }
                                }


                            }


                            //Tremolo Picking & Trill
                            if (n.effect.tremoloPicking != null || n.effect.trill != null)
                            {
                                int len = note.duration;
                                if (n.effect.tremoloPicking != null) len = flipDuration(n.effect.tremoloPicking.duration);
                                if (n.effect.trill != null) len = flipDuration(n.effect.trill.duration);
                                int origDuration = note.duration;
                                note.duration = len;
                                note.resizeValue *= ((float)len / origDuration);
                                int currentIndex = note.index + len;

                                last_notes[Math.Max(0, note.str - 1)] = note;
                                notes.Add(note);
                                notesInMeasure++;

                                dontAddNote = true; //Because we're doing it here already
                                bool originalFret = false;
                                int secondFret = note.fret;

                                if (n.effect.trill != null) { secondFret = n.effect.trill.fret - tuning[note.str - 1]; }

                                    while (currentIndex+len <= note.index + origDuration)
                                {
                                    Note newOne = new Note(note);
                                    newOne.index = currentIndex;
                                    if (!originalFret) newOne.fret = secondFret; //For trills
                                    last_notes[Math.Max(0, note.str - 1)] = newOne;
                                    if (n.effect.trill != null) newOne.isHammer = true;
                                    notes.Add(newOne);
                                    notesInMeasure++;
                                    currentIndex += len;
                                    originalFret = !originalFret;
                                }

                            }


                            //Grace Note
                            if (rememberGrace && note.duration > graceLength)
                            {
                                int orig = note.duration;
                                note.duration -= graceLength;
                                note.resizeValue *= ((float)note.duration / orig);
                                //subIndex -= graceLength;
                                rememberedGrace = true;
                            }
                            if (n.effect.grace != null)
                            {
                                bool isOnBeat = n.effect.grace.isOnBeat;

                                if (n.effect.grace.duration != -1)
                                { //GP3,4,5 format

                                    Note graceNote = new Note();
                                    graceNote.index = note.index;
                                    graceNote.fret = n.effect.grace.fret;
                                    graceNote.str = note.str;
                                    Duration dur = new Duration();
                                    dur.value = n.effect.grace.duration;
                                    graceNote.duration = flipDuration(dur); //works at least for GP5
                                    if (isOnBeat)
                                    {
                                        int orig = note.duration;
                                        note.duration -= graceNote.duration;
                                        note.index += graceNote.duration;
                                        note.resizeValue *= ((float)note.duration / orig);
                                    } else
                                    {
                                        graceNote.index -= graceNote.duration;

                                    }

                                    notes.Add(graceNote); //TODO: insert at correct position!
                                    notesInMeasure++;

                                } else {


                                    if (isOnBeat) // shorten next note
                                    {
                                        rememberGrace = true;
                                        graceLength = note.duration;
                                    } else //Change previous note
                                    {
                                        if ( notes.Count > 0)
                                        {
                                            note.index -= note.duration; //Can lead to negative indices. Midi should handle that
                                            subtractSubindex = note.duration;

                                        }
                                    }

                                }

                            }


                            //Dead Notes
                            if (n.type == NoteType.dead) {
                                int orig = note.duration;
                                note.velocity = (int)(note.velocity * 0.9f); note.duration /= 6;
                                note.resizeValue *= ((float)note.duration / orig);
                            }

                            //Ghost Notes
                            if (n.effect.palmMute) {
                                int orig = note.duration;
                                note.velocity = (int)(note.velocity * 0.7f); note.duration /= 2;
                                note.resizeValue *= ((float)note.duration / orig);
                            }
                            if (n.effect.ghostNote) { note.velocity = (int)(note.velocity * 0.8f);}


                            //Staccato, Accented, Heavy Accented
                            if (n.effect.staccato) {
                                int orig = note.duration;
                                note.duration /= 2;
                                note.resizeValue *= ((float)note.duration / orig);
                            }
                            if (n.effect.accentuatedNote) note.velocity = (int)(note.velocity * 1.2f);
                            if (n.effect.heavyAccentuatedNote) note.velocity = (int)(note.velocity * 1.4f);

                            //Arpeggio / Brush
                            if (hasBrush)
                            {
                                note.index = brushInit;
                                brushInit += brushIncrease;

                            }

                            if (!dontAddNote)
                            {
                                last_notes[Math.Max(0, note.str - 1)] = note;
                                notes.Add(note);
                                notesInMeasure++;
                            }


                        }
                        if (rememberedGrace) { subIndex -= graceLength; rememberGrace = false; rememberedGrace = false; } //After the change in duration for the second beat has been done

                        subIndex -= subtractSubindex;
                        subtractSubindex = 0;
                        subIndex += flipDuration(b.duration);

                        //Sort brushed tones
                        if (hasBrush && brushDirection == BeatStrokeDirection.up)
                        {
                            //Have to reorder them xxx123 -> xxx321
                            int notesCnt = b.notes.Count;
                            Note[] temp = new Note[notesCnt];
                            for (int x = notes.Count - notesCnt; x < notes.Count; x++)
                            {
                                temp[x - (notes.Count - notesCnt)] = new Note(notes[x]);
                            }
                            for (int x = notes.Count - notesCnt; x < notes.Count; x++)
                            {
                                notes[x] = temp[temp.Length - (x - (notes.Count - notesCnt))-1];

                            }


                        }
                        hasBrush = false;
                    }
                    break; //Consider only the first voice
                }
                int measureDuration = flipDuration(m.header.timeSignature.denominator) * m.header.timeSignature.numerator;
                _barMaster[measureIndex].duration = measureDuration;
                _barMaster[measureIndex].index = index;
                index += measureDuration;
                _notesInMeasures.Add(notesInMeasure);
            }


            return notes;
        }



        public int[] getTuning(List<GuitarString> strings)
        {
            int[] tuning = new int[strings.Count];
            for (int x = 0; x < tuning.Length; x++)
            {
                tuning[x] = strings[x].value;
            }

            return tuning;
        }

        public List<MasterBar> retrieveMasterBars(GPFile file)
        {
            List<MasterBar> masterBars = new List<MasterBar>();
            foreach (MeasureHeader mh in file.measureHeaders)
            {
                //(mh.timeSignature.denominator) * mh.timeSignature.numerator;
                MasterBar mb = new MasterBar();
                mb.time = mh.timeSignature.numerator + "/" + mh.timeSignature.denominator.value;
                mb.num = mh.timeSignature.numerator;
                mb.den = mh.timeSignature.denominator.value;
                string keyFull = "" + (int) mh.keySignature;
                if (keyFull.Length != 1)
                {
                    mb.keyType = int.Parse(keyFull.Substring(keyFull.Length - 1), CultureInfo.InvariantCulture);
                    mb.key = int.Parse(keyFull.Substring(0, keyFull.Length - 1), CultureInfo.InvariantCulture);
                }
                else
                {
                    mb.key = 0;
                    mb.keyType = int.Parse(keyFull, CultureInfo.InvariantCulture);
                }
                mb.keyBoth = keyFull; //Useful for midiExport later

                mb.tripletFeel = mh.tripletFeel;

                masterBars.Add(mb);
            }

            return masterBars;
        }

        public List<Tempo> retrieveTempos(GPFile file)
        {
            List<Tempo> tempos = new List<Tempo>();
            //Version < 4 -> look at Measure Headers, >= 4 look at mixtablechanges


            int version = file.versionTuple[0];
            if (version < 4) //Look at MeasureHeaders
            {
                //Get inital tempo from file header
                Tempo init = new Tempo();
                init.position = 0;
                init.value = file.tempo;
                if (init.value != 0) tempos.Add(init);

                int pos = 0;
                float oldTempo = file.tempo;
                foreach (MeasureHeader mh in file.measureHeaders)
                {
                    Tempo t = new Tempo();
                    t.value = mh.tempo.value;
                    t.position = pos;
                    pos += flipDuration(mh.timeSignature.denominator) * mh.timeSignature.numerator;
                    if (oldTempo != t.value) tempos.Add(t);
                    oldTempo = t.value;
                }

            } else //Look at MixtableChanges - only on track 1, voice 1
            {
                int pos = 0;

                //Get inital tempo from file header
                Tempo init = new Tempo();
                init.position = 0;
                init.value = file.tempo;
                if (init.value != 0) tempos.Add(init);
                foreach (Measure m in file.tracks[0].measures)
                {
                    int smallPos = 0; //inner measure position
                    if (m.voices.Count == 0) continue;

                    foreach (Beat b in m.voices[0].beats){

                        if (b.effect != null)
                        {
                            if (b.effect.mixTableChange != null)
                            {
                                MixTableItem tempo = b.effect.mixTableChange.tempo;
                                if (tempo != null)
                                {
                                    Tempo t = new Tempo();
                                    t.value = tempo.value;
                                    t.position = pos + smallPos;

                                    tempos.Add(t);
                                }
                            }
                        }

                        smallPos += flipDuration(b.duration);
                    }
                    pos += flipDuration(m.header.timeSignature.denominator) * m.header.timeSignature.numerator;
                }
            }

            return tempos;
        }

        private static int flipDuration(Duration d)
        {
            int ticks_per_beat = 960;
            int result = 0;
            switch (d.value)
            {
                case 1: result += ticks_per_beat * 4; break;
                case 2: result += ticks_per_beat * 2; break;
                case 4: result += ticks_per_beat; break;
                case 8: result += ticks_per_beat / 2; break;
                case 16: result += ticks_per_beat / 4; break;
                case 32: result += ticks_per_beat / 8; break;
                case 64: result += ticks_per_beat / 16; break;
                case 128: result += ticks_per_beat / 32; break;
            }
            if (d.isDotted) result = (int)(result * 1.5f);
            if (d.isDoubleDotted) result = (int)(result * 1.75f);

            int enters = d.tuplet.enters;
            int times = d.tuplet.times;

            //3:2 = standard triplet, 3 notes in the time of 2
            result = (int)((result*times)/(float)enters);


            return result;
        }
    }

    public class Note
    {
        public float resizeValue = 1.0f; //Should reflect any later changes made to the note duration, so that bendPoints can be adjusted

        //Values from Note
        public int str = 0;
        public int fret = 0;
        public int velocity = 100;
        public bool isVibrato = false;
        public bool isHammer = false;
        public bool isPalmMuted = false;
        public bool isMuted = false;
        public HarmonicType harmonic = HarmonicType.none;
        public float harmonicFret = 0.0f;
        public bool slidesToNext = false;
        public bool slideInFromBelow = false;
        public bool slideInFromAbove = false;
        public bool slideOutUpwards = false;
        public bool slideOutDownwards = false;
        public List<BendPoint> bendPoints = new List<BendPoint>();
        public bool connect = false; //= tie

        //Values from Beat
        public List<BendPoint> tremBarPoints = new List<BendPoint>();
        public bool isTremBarVibrato = false;
        public bool isSlapped = false;
        public bool isPopped = false;
        public int index = 0;
        public int duration = 0;
        public Fading fading = Fading.none;
        public bool isRHTapped = false;

        public Note(Note old)
        {
            str = old.str; fret = old.fret; velocity = old.velocity; isVibrato = old.isVibrato;
            isHammer = old.isHammer; isPalmMuted = old.isPalmMuted; isMuted = old.isMuted;
            harmonic = old.harmonic; harmonicFret = old.harmonicFret; slidesToNext = old.slidesToNext;
            slideInFromAbove = old.slideInFromAbove; slideInFromBelow = old.slideInFromBelow;
            slideOutDownwards = old.slideOutDownwards; slideOutUpwards = old.slideOutUpwards;
            bendPoints.AddRange(old.bendPoints);
            tremBarPoints.AddRange(old.tremBarPoints);
            isTremBarVibrato = old.isTremBarVibrato; isSlapped = old.isSlapped; isPopped = old.isPopped;
            index = old.index; duration = old.duration; fading = old.fading; isRHTapped = old.isRHTapped;
            resizeValue = old.resizeValue;
        }
        public Note() { }

        public void addBendPoints(List<BendPoint> bendPoints)
        {
            //Hopefully no calculation involved
            this.bendPoints.AddRange(bendPoints);
        }
    }

    public enum Fading
    {
        none = 0, fadeIn = 1, fadeOut = 2, volumeSwell = 3
    }

    public class Annotation
    {
        public string value = "";
        public int position = 0;
        public Annotation(string v = "", int pos = 0)
        {
            value = v; position = pos;
        }
    }

    public class TremoloPoint
    {
        public float value = 0; //0 nothing, 100 one whole tone up
        public int index = 0;

        public TremoloPoint()
        {
        }

        public TremoloPoint(float value, int index)
        {
            this.value = value;
            this.index = index;
        }
    }

    public class BendPoint
    {
        public float value = 0;
        public int index = 0; //also global index of midi
        public int usedChannel = 0; //After being part of BendingPlan
        public BendPoint(float value, int index)
        {
            this.value = value;
            this.index = index;
        }
        public BendPoint() { }
    }

    public enum HarmonicType
    {
        none = 0, natural = 1, artificial = 2, pinch = 3, semi = 4, tapped = 5
    }

    public class Track
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string name = "";
        public int patch = 0;
        public int port = 0;
        public int channel = 0;
        public int capo = 0;
        public PlaybackState playbackState = PlaybackState.def;
        public int[] tuning = new int[] { 40, 45, 50, 55, 59, 64 };
        public List<Note> notes = new List<Note>();
        public List<TremoloPoint> tremoloPoints = new List<TremoloPoint>();
        private List<int[]> volumeChanges = new List<int[]>();


        public MidiExport.MidiTrack GetMidi()
        {
            var midiTrack = new MidiExport.MidiTrack();
            midiTrack.messages.Add(new MidiExport.MidiMessage("midi_port", new[] {port.ToString()}, 0));
            midiTrack.messages.Add(new MidiExport.MidiMessage("track_name", new[] {name}, 0));
            midiTrack.messages.Add(new MidiExport.MidiMessage("program_change",
                new[] {channel.ToString(), patch.ToString()}, 0));
            if (!notes.Any())
            {
                return midiTrack;
            }

            var noteOffs = new List<int[]>();
            var channelConnections = new List<int[]>(); //For bending and trembar: [original Channel, artificial Channel, index at when to delete artificial]
            var activeBendingPlans = new List<BendingPlan>();
            var currentIndex = 0;

            notes.Add(new Note {index = notes[^1].index + notes[^1].duration, str = -2});
            tremoloPoints = addDetailsToTremoloPoints(tremoloPoints, 60);

            //var _notes = addSlidesToNotes(notes); //Adding slide notes here, as they should not appear as extra notes during playback

            foreach (var n in notes)
            {
                noteOffs.Sort((x, y) => x[0].CompareTo(y[0]));



                //Check for active bendings in progress
                List<BendPoint> currentBPs = findAndSortCurrentBendPoints(activeBendingPlans, n.index);
                float _tremBarChange = 0.0f;
                foreach (BendPoint bp in currentBPs)
                {
                    //Check first if there is a note_off event happening in the meantime..
                    List<int[]> newNoteOffs = new List<int[]>();
                    foreach (int[] noteOff in noteOffs)
                    {
                        if (noteOff[0] <= bp.index) //between last and this note, a note off event should occur
                        {
                            midiTrack.messages.Add(
                                new MidiExport.MidiMessage("note_off",
                                new string[] { "" + noteOff[2], "" + noteOff[1], "0" }, noteOff[0] - currentIndex));
                            currentIndex = noteOff[0];
                        }
                        else
                        {
                            newNoteOffs.Add(noteOff);
                        }
                    }
                    noteOffs = newNoteOffs;

                    //Check if there are active tremPoints to be adjusted for
                    List<TremoloPoint> _newTremPoints = new List<TremoloPoint>();

                    foreach (TremoloPoint tp in tremoloPoints)
                    {
                        if (tp.index <= bp.index) //between last and this note, a note off event should occur
                        {
                            _tremBarChange = tp.value;
                        }
                        else
                        {
                            _newTremPoints.Add(tp);
                        }
                    }
                    tremoloPoints = _newTremPoints;

                    //Check if there are active volume changes
                    List<int[]> _newVolumeChanges = new List<int[]>();
                    foreach (int[] vc in volumeChanges)
                    {
                        if (vc[0] <= bp.index) //between last and this note, a volume change event should occur
                        { //channel control value
                            midiTrack.messages.Add(
                   new MidiExport.MidiMessage("control_change",
                   new string[] { "" + bp.usedChannel, "7",""+vc[1] }, vc[0] - currentIndex));
                            currentIndex = vc[0];
                        }
                        else
                        {
                            _newVolumeChanges.Add(vc);
                        }
                    }
                    volumeChanges = _newVolumeChanges;

                    midiTrack.messages.Add(
                   new MidiExport.MidiMessage("pitchwheel",
                   new string[] { "" + bp.usedChannel, "" + (int)((bp.value+_tremBarChange) * 25.6f) }, bp.index - currentIndex));
                    currentIndex = bp.index;
                }

                        //Delete no longer active Bending Plans
                        List<BendingPlan> final = new List<BendingPlan>();
                foreach (BendingPlan bpl in activeBendingPlans)
                {

                    BendingPlan newBPL = new BendingPlan(bpl.originalChannel, bpl.usedChannel, new List<BendPoint>());
                    foreach (BendPoint bp in bpl.bendingPoints)
                    {
                        if (bp.index > n.index)
                        {
                            newBPL.bendingPoints.Add(bp);
                        }
                    }
                    if (newBPL.bendingPoints.Count > 0)
                    {
                        final.Add(newBPL);
                    }
                    else //That bending plan has finished
                    {
                        midiTrack.messages.Add(new MidiExport.MidiMessage("pitchwheel", new string[] { "" + bpl.usedChannel, "-128" }, 0));
                        midiTrack.messages.Add(new MidiExport.MidiMessage("control_change", new string[] { "" + bpl.usedChannel, "101", "127" }, 0));
                        midiTrack.messages.Add(new MidiExport.MidiMessage("control_change", new string[] { "" + bpl.usedChannel, "10", "127" }, 0));

                        //Remove the channel from channelConnections
                        List<int[]> newChannelConnections = new List<int[]>();
                        foreach (int[] cc in channelConnections)
                        {
                            if (cc[1] != bpl.usedChannel) newChannelConnections.Add(cc);
                        }
                        channelConnections = newChannelConnections;

                        NativeFormat.AvailableChannels[bpl.usedChannel] = true;
                    }
                }

                activeBendingPlans = final;




                var activeChannels = getActiveChannels(channelConnections);
                List<TremoloPoint> newTremPoints = new List<TremoloPoint>();
                foreach (TremoloPoint tp in tremoloPoints)
                {
                    if (tp.index <= n.index) //between last and this note, a trembar event should occur
                    {
                        var value = tp.value * 25.6f;
                        value =Math.Min( Math.Max(value,-8192),8191);
                        foreach (int ch in activeChannels) {
                            midiTrack.messages.Add(
                     new MidiExport.MidiMessage("pitchwheel",
                     new string[] { "" + ch, "" + (int)(value) }, tp.index - currentIndex));
                            currentIndex = tp.index;
                        }
                    }
                    else
                    {
                        newTremPoints.Add(tp);
                    }
                }
                tremoloPoints = newTremPoints;


                //Check if there are active volume changes
                List<int[]> newVolumeChanges = new List<int[]>();
                foreach (int[] vc in volumeChanges)
                {
                    if (vc[0] <= n.index) //between last and this note, a volume change event should occur
                    {

                        foreach (int ch in activeChannels)
                        {
                            midiTrack.messages.Add(
               new MidiExport.MidiMessage("control_change",
               new string[] { "" + ch, "7", "" + vc[1] }, vc[0] - currentIndex));
                            currentIndex = vc[0];
                        }
                    }
                    else
                    {
                        newVolumeChanges.Add(vc);
                    }
                }
                volumeChanges = newVolumeChanges;


                List<int[]> temp = new List<int[]>();
                foreach (int[] noteOff in noteOffs)
                {
                    if (noteOff[0] <= n.index) //between last and this note, a note off event should occur
                    {
                        midiTrack.messages.Add(
                            new MidiExport.MidiMessage("note_off",
                            new string[] { "" + noteOff[2], "" + noteOff[1], "0" }, noteOff[0] - currentIndex));
                        currentIndex = noteOff[0];
                    } else
                    {
                        temp.Add(noteOff);
                    }
                }
                noteOffs = temp;

                int velocity = n.velocity;
                int note;

                if (n.str == -2) break; //Last round

                if (n.str - 1 < 0) Logger.Debug("String was -1");
                if (n.str-1 >= tuning.Length && tuning.Length != 0) Logger.Debug("String was higher than string amount (" + n.str + ")");
                if (tuning.Length > 0) note = tuning[n.str - 1] + capo + n.fret;
                else
                {
                    note = capo + n.fret;
                }
                if (n.harmonic != HarmonicType.none) //Has Harmonics
                {
                    int harmonicNote = getHarmonic(tuning[n.str-1], n.fret, capo, n.harmonicFret, n.harmonic);
                    note = harmonicNote;
                }

                int noteChannel = channel;

                if (n.bendPoints.Count > 0) //Has Bending
                {
                    int usedChannel = tryToFindChannel();
                    if (usedChannel == -1) usedChannel = channel;
                    NativeFormat.AvailableChannels[usedChannel] = false;
                    channelConnections.Add(new int[] {channel,usedChannel,n.index + n.duration });
                    midiTrack.messages.Add(new MidiExport.MidiMessage("program_change", new string[] { ""+usedChannel, ""+patch}, n.index - currentIndex));
                    noteChannel = usedChannel;
                    currentIndex = n.index;
                    activeBendingPlans.Add(createBendingPlan(n.bendPoints,channel, usedChannel,n.duration,n.index ,n.resizeValue, n.isVibrato));
                }

                if (n.isVibrato && n.bendPoints.Count == 0) //Is Vibrato & No Bending
                {
                    int usedChannel = channel;
                    activeBendingPlans.Add(createBendingPlan(n.bendPoints, channel, usedChannel, n.duration, n.index, n.resizeValue, true));

                }

                if (n.fading != Fading.none) //Fading
                {
                    volumeChanges = createVolumeChanges(n.index, n.duration, n.velocity, n.fading);
                }

                midiTrack.messages.Add(new MidiExport.MidiMessage("note_on", new string[] { "" + noteChannel, "" + note, "" + n.velocity }, n.index - currentIndex));
                currentIndex = n.index;

                if (n.bendPoints.Count > 0) //Has Bending cont.
                {
                    midiTrack.messages.Add(new MidiExport.MidiMessage("control_change", new string[] { "" + noteChannel, "101","0" }, 0));
                    midiTrack.messages.Add(new MidiExport.MidiMessage("control_change", new string[] { "" + noteChannel, "100", "0" }, 0));
                    midiTrack.messages.Add(new MidiExport.MidiMessage("control_change", new string[] { "" + noteChannel, "6", "6" }, 0));
                    midiTrack.messages.Add(new MidiExport.MidiMessage("control_change", new string[] { "" + noteChannel, "38", "0" }, 0));


                }

                noteOffs.Add(new int[] {n.index + n.duration, note , noteChannel});

            }




            midiTrack.messages.Add(new MidiExport.MidiMessage("end_of_track", new string[] { }, 0));
            return midiTrack;
        }

        private List<Note> addSlidesToNotes(List<Note> notes)
        {
            List<Note> ret = new List<Note>();
            int index = -1;
            foreach (Note n in notes)
            {
                index++;
                bool skipWrite = false;

                if ((n.slideInFromBelow && n.str > 1) || n.slideInFromAbove)
                {
                    int myFret = n.fret;
                    int start = n.slideInFromAbove ? myFret + 4 : Math.Max(1,myFret-4);
                    int beginIndex = n.index - 960 / 4; //16th before
                    int lengthEach = (960 / 4) / Math.Abs(myFret - start);
                    for (int x = 0; x < Math.Abs(myFret - start); x++)
                    {
                        Note newOne = new Note(n);
                        newOne.duration = lengthEach;
                        newOne.index = beginIndex + x * lengthEach;
                        newOne.fret = start + (n.slideInFromAbove?-x:+x);
                        ret.Add(newOne);
                    }
                }

                if ((n.slideOutDownwards && n.str>1) || n.slideOutUpwards)
                {
                    int myFret = n.fret;
                    int end = n.slideOutUpwards ? myFret + 4 : Math.Max(1, myFret - 4);
                    int beginIndex = (n.index+n.duration) - 960 / 4; //16th before
                    int lengthEach = (960 / 4) / Math.Abs(myFret - end);
                    n.duration -= 960 / 4;
                    ret.Add(n); skipWrite = true;
                    for (int x = 0; x < Math.Abs(myFret - end); x++)
                    {
                        Note newOne = new Note(n);
                        newOne.duration = lengthEach;
                        newOne.index = beginIndex + x * lengthEach;
                        newOne.fret = myFret + (n.slideOutDownwards ? -x : +x);
                        ret.Add(newOne);
                    }
                }
                /*
                if (n.slidesToNext)
                {
                    int slideTo = -1;
                    //Find next note on same string
                    for (int x = index+1; x < notes.Count; x++)
                    {
                        if (notes[x].str == n.str)
                        {
                            slideTo = notes[x].fret;
                            break;
                        }
                    }

                    if (slideTo != -1 && slideTo != n.fret) //Found next tone on string
                    {
                        int myStr = n.str;
                        int end = slideTo;
                        int beginIndex = (n.index + n.duration) - 960 / 4; //16th before
                        int lengthEach = (960 / 4) / Math.Abs(myStr - end);
                        n.duration -= 960 / 4;
                        ret.Add(n); skipWrite = true;
                        for (int x = 0; x < Math.Abs(myStr - end); x++)
                        {
                            Note newOne = new Note(n);
                            newOne.duration = lengthEach;
                            newOne.index = beginIndex + x * lengthEach;
                            newOne.fret = myStr + (slideTo < n.fret ? -x : +x);
                            ret.Add(newOne);
                        }
                    }
                }
                */

                if (!skipWrite) ret.Add(n);
            }

            return ret;
        }

        private List<int[]> createVolumeChanges(int index, int duration, int velocity, Fading fading)
        {
            int segments = 20;
            List<int[]> changes = new List<int[]>();
            if (fading == Fading.fadeIn || fading == Fading.fadeOut)
            {
                int step = velocity / segments;
                int val = fading==Fading.fadeIn?0:velocity;
                if (fading == Fading.fadeOut) step = (int)(-step*1.25f);

                for (int x=index; x < index+duration; x+= (duration / segments))
                {
                    changes.Add(new int[] {x,Math.Min(127,Math.Max(0,val)) });
                    val += step;
                }

            }

            if (fading == Fading.volumeSwell)
            {
                int step = (int)(velocity / (segments * 0.8f));
                int val = 0;
                int times = 0;
                for (int x = index; x < index + duration; x += (duration / segments))
                {

                    changes.Add(new int[] { x, Math.Min(127, Math.Max(0, val)) });
                    val += step;
                    if (times == segments/2) step = -step;
                    times++;
                }
            }
            changes.Add(new int[] { index + duration, velocity }); //Definitely go back to normal


            return changes;
        }

        private List<int> getActiveChannels(List<int[]> channelConnections)
        {
            List<int> ret_val = new List<int>();
            ret_val.Add(channel);
            foreach (int[] cc in channelConnections)
            {
                ret_val.Add(cc[1]);
            }
            return ret_val;
        }

        public int tryToFindChannel()
        {
            int cnt = 0;
            foreach (bool available in NativeFormat.AvailableChannels)
            {
                if (available) return cnt;
                cnt++;
            }
            return -1;
        }

        public int getHarmonic(int baseTone, int fret, int capo, float harmonicFret, HarmonicType type)
        {
            int val = 0;
            //Capo, base tone and fret (if not natural harmonic) shift the harmonics simply
            val = val + baseTone + capo;
            if (type != HarmonicType.natural)
            {
                val += (int)Math.Round(harmonicFret);
            }
            val += fret;

            if (harmonicFret == 2.4f) val += 34;
            if (harmonicFret == 2.7f) val += 31;
            if (harmonicFret == 3.2f) val += 28;
            if (harmonicFret == 4f) val += 24;
            if (harmonicFret == 5f) val += 19;
            if (harmonicFret == 5.8f) val += 28;
            if (harmonicFret == 7f) val += 12;
            if (harmonicFret == 8.2f) val += 28;
            if (harmonicFret == 9f) val += 19;
            if (harmonicFret == 9.6f) val += 24;
            if (harmonicFret == 12f) val += 0;
            if (harmonicFret == 14.7f) val += 19;
            if (harmonicFret == 16f) val += 12;
            if (harmonicFret == 17f) val += 19;
            if (harmonicFret == 19f) val += 0;
            if (harmonicFret == 21.7f) val += 12;
            if (harmonicFret == 24f) val += 0;

            return Math.Min(val,127);
        }


        public List<BendPoint> findAndSortCurrentBendPoints(List<BendingPlan> activeBendingPlans, int index)
        {
            List<BendPoint> bps = new List<BendPoint>();
            foreach(BendingPlan bpl in activeBendingPlans)
            {
                foreach (BendPoint bp in bpl.bendingPoints)
                {
                    if (bp.index <= index)
                    {
                        bp.usedChannel = bpl.usedChannel;
                        bps.Add(bp);
                    }
                }
            }
            bps.Sort((x, y) => x.index.CompareTo(y.index));

            return bps;
        }

        public List<TremoloPoint> addDetailsToTremoloPoints(List<TremoloPoint> tremoloPoints, int maxDistance)
        {
            List<TremoloPoint> tremPoints = new List<TremoloPoint>();
            float oldValue = 0.0f;
            int oldIndex = 0;
            foreach (TremoloPoint tp in tremoloPoints)
            {
                if ((tp.index - oldIndex) > maxDistance && !(oldValue == 0.0f && tp.value == 0.0f))
                {
                    //Add in-between points
                    for (int x = oldIndex + maxDistance; x < tp.index; x += maxDistance)
                    {
                        float value = oldValue + (tp.value - oldValue) * (((float)x - oldIndex) / ((float)tp.index - oldIndex));
                        tremPoints.Add(new TremoloPoint(value, x));

                    }
                }
                tremPoints.Add(tp);

                oldValue = tp.value;
                oldIndex = tp.index;
            }


            return tremPoints;
        }

        public BendingPlan createBendingPlan(List<BendPoint> bendPoints, int originalChannel, int usedChannel, int duration, int index, float resize, bool isVibrato)
        {
           int maxDistance = duration / 10; //After this there should be a pitchwheel event
            if (isVibrato) maxDistance = Math.Min(maxDistance,60);

            if (bendPoints.Count == 0)
            {
                //Create Vibrato Plan
                bendPoints.Add(new BendPoint(0.0f,index));
                bendPoints.Add(new BendPoint(0.0f, index+duration));

            }

            List<BendPoint> bendingPoints = new List<BendPoint>();


            //Resize the points according to (changed) note duration
            foreach (BendPoint bp in bendPoints)
            {
                bp.index = (int)(index + ((bp.index - index) * resize));
                bp.usedChannel = usedChannel;
            }

            int old_pos = index;
            float old_value = 0.0f;
            bool start = true;
            int vibratoSize = 0;
            int vibratoChange = 0;
            if (isVibrato) vibratoSize = 12;
            if (isVibrato) vibratoChange = 6;
            int vibrato = 0;
            foreach (BendPoint bp in bendPoints)
            {
                if ((bp.index - old_pos) > maxDistance)
                {
                    //Add in-between points
                    for (int x = old_pos+ maxDistance; x < bp.index; x += maxDistance)
                    {
                        float value =  old_value + (bp.value - old_value) * (((float)x-old_pos)/((float)bp.index - old_pos));
                        bendingPoints.Add(new BendPoint(value+vibrato,x));
                        if (isVibrato && Math.Abs(vibrato) == vibratoSize) vibratoChange = -vibratoChange;
                        vibrato += vibratoChange;

                    }
                }
                if (start || bp.index != old_pos)
                {
                    if (isVibrato) bp.value += vibrato;
                    bendingPoints.Add(bp);

                }
                old_pos = bp.index;
                old_value = bp.value;
                if ((start || bp.index != old_pos) && isVibrato) old_value -= vibrato; //Add back, so not to be influenced by it
                start = false;
                if (isVibrato && Math.Abs(vibrato) == vibratoSize) vibratoChange = -vibratoChange;
                vibrato += vibratoChange;
            }
            if (Math.Abs(index+duration - old_pos) > maxDistance)
            {
                bendingPoints.Add(new BendPoint(old_value,index+duration));
            }

            return new BendingPlan(originalChannel, usedChannel, bendingPoints);
        }

    }

    public class BendingPlan
    {
        public List<BendPoint> bendingPoints = new List<BendPoint>();
        //List<int> positions = new List<int>(); //index where to put the points
        public int originalChannel = 0;
        public int usedChannel = 0;
        public BendingPlan(int originalChannel, int usedChannel, List<BendPoint> bendingPoints)
        {
            this.bendingPoints = bendingPoints;
            //this.positions = positions;
            this.originalChannel = originalChannel;
            this.usedChannel = usedChannel;

        }
    }

    public class MasterBar
    {

        public string time = "4/4";
        public int num = 4;
        public int den = 4;
        public TripletFeel tripletFeel = TripletFeel.none; //additional info -> note values are changed in duration and position too
        public int duration = 0;
        public int index = 0; //Midi Index
        public int key = 0; //C, -1 = F, 1 = G
        public int keyType = 0; //0 = Major, 1 = Minor
        public string keyBoth = "0";
    }


    public enum PlaybackState
    {
        def = 0, mute = 1, solo = 2
    }

    public class Tempo
    {
        public float value = 120.0f;
        public int position = 0; //total position in song @ 960 ticks_per_beat
    }

}
