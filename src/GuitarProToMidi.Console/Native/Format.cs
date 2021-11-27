using System;
using System.Collections.Generic;
using System.Globalization;

namespace GuitarProToMidi.Native;

public class Format
{
    public static readonly bool[] AvailableChannels = new bool[16];
    private readonly string _album;

    private readonly List<Annotation> _annotations = new();
    private readonly string _artist;
    private readonly List<MasterBar> _barMaster;
    private readonly string _music;
    private readonly List<int> _notesInMeasures = new();
    private readonly string _subtitle;
    private readonly List<Tempo> _tempos;
    private readonly string _title;
    private readonly List<Track> _tracks;
    private readonly string _words;


    public Format(GPFile fromFile)
    {
        _title = fromFile.title;
        _subtitle = fromFile.subtitle;
        _artist = fromFile.interpret;
        _album = fromFile.album;
        _words = fromFile.words;
        _music = fromFile.music;
        _tempos = RetrieveTempos(fromFile);
        _barMaster = RetrieveMasterBars(fromFile);
        _tracks = RetrieveTracks(fromFile);
        UpdateAvailableChannels();
    }

    public MidiExport ToMidi()
    {
        var mid = new MidiExport();
        mid.midiTracks.Add(GetMidiHeader()); //First, untitled track
        foreach (var track in _tracks)
        {
            mid.midiTracks.Add(track.GetMidi());
        }

        return mid;
    }

    private MidiTrack GetMidiHeader()
    {
        var midiHeader = new MidiTrack();
        //text(s) - name of song, artist etc., created by Gitaro
        //copyright - by Gitaro
        //midi port 0
        //time signature
        //key signature
        //set tempo
        ///////marker text (will be seen in file) - also Gitaro copyright blabla
        //end_of_track
        midiHeader.messages.Add(new MidiMessage("track_name", new[] { "untitled" }, 0));
        midiHeader.messages.Add(new MidiMessage("text", new[] { _title }, 0));
        midiHeader.messages.Add(new MidiMessage("text", new[] { _subtitle }, 0));
        midiHeader.messages.Add(new MidiMessage("text", new[] { _artist }, 0));
        midiHeader.messages.Add(new MidiMessage("text", new[] { _album }, 0));
        midiHeader.messages.Add(new MidiMessage("text", new[] { _words }, 0));
        midiHeader.messages.Add(new MidiMessage("text", new[] { _music }, 0));
        midiHeader.messages.Add(new MidiMessage("copyright", new[] { "Copyright 2017 by Gitaro" },
            0));
        midiHeader.messages.Add(new MidiMessage("marker",
            new[] { _title + " / " + _artist + " - Copyright 2017 by Gitaro" }, 0));
        midiHeader.messages.Add(new MidiMessage("midi_port", new[] { "0" }, 0));

        //Get tempos from List tempos, get key_signature and time_signature from barMaster
        var tempoIndex = 0;
        var masterBarIndex = 0;
        var currentIndex = 0;
        var oldTimeSignature = "";
        var oldKeySignature = "";
        if (_tempos.Count == 0)
        {
            _tempos.Add(new Tempo());
        }

        while (tempoIndex < _tempos.Count || masterBarIndex < _barMaster.Count)
        //Compare next entry of both possible sources
        {
            if (tempoIndex == _tempos.Count || _tempos[tempoIndex].Position >= _barMaster[masterBarIndex].Index
            ) //next measure comes first
            {
                if (!_barMaster[masterBarIndex].KeyBoth.Equals(oldKeySignature))
                {
                    //Add Key-Sig to midiHeader
                    midiHeader.messages.Add(new MidiMessage("key_signature",
                        new[] { "" + _barMaster[masterBarIndex].Key, "" + _barMaster[masterBarIndex].KeyType },
                        _barMaster[masterBarIndex].Index - currentIndex));
                    currentIndex = _barMaster[masterBarIndex].Index;

                    oldKeySignature = _barMaster[masterBarIndex].KeyBoth;
                }

                if (!_barMaster[masterBarIndex].Time.Equals(oldTimeSignature))
                {
                    //Add Time-Sig to midiHeader
                    midiHeader.messages.Add(new MidiMessage("time_signature",
                        new[]
                            {"" + _barMaster[masterBarIndex].Num, "" + _barMaster[masterBarIndex].Den, "24", "8"},
                        _barMaster[masterBarIndex].Index - currentIndex));
                    currentIndex = _barMaster[masterBarIndex].Index;

                    oldTimeSignature = _barMaster[masterBarIndex].Time;
                }

                masterBarIndex++;
            }
            else //next tempo signature comes first
            {
                //Add Tempo-Sig to midiHeader
                var tempo = (int)Math.Round(60 * 1000000 / _tempos[tempoIndex].Value);
                midiHeader.messages.Add(new MidiMessage("set_tempo", new[] { "" + tempo },
                    _tempos[tempoIndex].Position - currentIndex));
                currentIndex = _tempos[tempoIndex].Position;
                tempoIndex++;
            }
        }

        midiHeader.messages.Add(new MidiMessage("end_of_track", Array.Empty<string>(), 0));
        return midiHeader;
    }

    private void UpdateAvailableChannels()
    {
        for (var x = 0; x < 16; x++)
        {
            if (x != 9)
            {
                AvailableChannels[x] = true;
            }
            else
            {
                AvailableChannels[x] = false;
            }
        }

        foreach (var track in _tracks)
        {
            AvailableChannels[track.Channel] = false;
        }
    }

    private List<Track> RetrieveTracks(GPFile file)
    {
        var tracks = new List<Track>();
        foreach (var tr in file.tracks)
        {
            var track = new Track
            {
                Name = tr.name,
                Patch = tr.channel.instrument,
                Port = tr.port,
                Channel = tr.channel.channel,
                State = PlaybackState.Def,
                Capo = tr.offset
            };
            if (tr.isMute)
            {
                track.State = PlaybackState.Mute;
            }

            if (tr.isSolo)
            {
                track.State = PlaybackState.Solo;
            }

            track.Tuning = GetTuning(tr.strings);

            track.Notes = RetrieveNotes(tr, track.Tuning, track);
            tracks.Add(track);
        }

        return tracks;
    }

    private static void AddToTremoloBarList(int index, int duration, BendEffect bend, Track myTrack)
    {
        myTrack.TremoloPoints.Add(new TremoloPoint(index, 0.0f)); //So that it can later be recognized as the beginning
        foreach (var bp in bend.points)
        {
            var at = index + (int)(bp.GP6position * duration / 100.0f);
            var point = new TremoloPoint(at, bp.GP6value);
            myTrack.TremoloPoints.Add(point);
        }

        var tp = new TremoloPoint(index + duration, 0);
        myTrack.TremoloPoints
            .Add(tp); //Back to 0 -> Worst case there will be on the same index the final of tone 1, 0, and the beginning of tone 2.
    }

    private static List<BendPoint> GetBendPoints(int index, int duration, BendEffect bend)
    {
        var ret = new List<BendPoint>();
        foreach (var bp in bend.points)
        {
            var at = index + (int)(bp.GP6position * duration / 100.0f);
            var point = new BendPoint(at, bp.GP6value);
            ret.Add(point);
        }

        return ret;
    }


    private List<Note> RetrieveNotes(GuitarProToMidi.Track track, int[] tuning, Track myTrack)
    {
        var notes = new List<Note>();
        var index = 0;
        var lastNotes = new Note[10];
        var lastWasTie = new bool[10];
        for (var x = 0; x < 10; x++)
        {
            lastWasTie[x] = false;
        }

        //GraceNotes if on beat - reducing the next note's length
        var rememberGrace = false;
        var rememberedGrace = false;
        var graceLength = 0;
        var subtractSubindex = 0;

        for (var x = 0; x < 10; x++)
        {
            lastNotes[x] = null;
        }

        var measureIndex = -1;
        foreach (var m in track.measures)
        {
            var notesInMeasure = 0;
            measureIndex++;
            var skipVoice = false;
            switch (m.simileMark)
            {
                //Repeat last measure
                case SimileMark.simple:
                    {
                        var amountNotes = _notesInMeasures[^1]; //misuse prohibited by guitarpro
                        var endPoint = notes.Count;
                        for (var x = endPoint - amountNotes; x < endPoint; x++)
                        {
                            var newNote = new Note(notes[x]);
                            var oldM = track.measures[measureIndex - 1];
                            newNote.Index += FlipDuration(oldM.header.timeSignature.denominator) *
                                             oldM.header.timeSignature.numerator;
                            notes.Add(newNote);
                            notesInMeasure++;
                        }

                        skipVoice = true;
                        break;
                    }
                case SimileMark.firstOfDouble:
                //Repeat first or second of last two measures
                case SimileMark.secondOfDouble:
                    {
                        var secondAmount = _notesInMeasures[^1]; //misuse prohibited by guitarpro
                        var firstAmount = _notesInMeasures[^2];
                        var endPoint = notes.Count - secondAmount;
                        for (var x = endPoint - firstAmount; x < endPoint; x++)
                        {
                            var newNote = new Note(notes[x]);
                            var oldM1 = track.measures[measureIndex - 2];
                            var oldM2 = track.measures[measureIndex - 1];
                            newNote.Index += FlipDuration(oldM1.header.timeSignature.denominator) *
                                             oldM1.header.timeSignature.numerator;
                            newNote.Index += FlipDuration(oldM2.header.timeSignature.denominator) *
                                             oldM2.header.timeSignature.numerator;
                            notes.Add(newNote);
                            notesInMeasure++;
                        }

                        skipVoice = true;
                        break;
                    }
                case SimileMark.none:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(m.simileMark), m.simileMark, "Unknown enum value.");
            }

            foreach (var v in m.voices)
            {
                if (skipVoice)
                {
                    break;
                }

                var subIndex = 0;
                foreach (var b in v.beats)
                {
                    if (b.text != null && !b.text.value.Equals(""))
                    {
                        _annotations.Add(new Annotation(b.text.value, index + subIndex));
                    }

                    if (b.effect.tremoloBar != null)
                    {
                        AddToTremoloBarList(index + subIndex, FlipDuration(b.duration), b.effect.tremoloBar,
                            myTrack);
                    }


                    //Prepare Brush or Arpeggio
                    var hasBrush = false;
                    var brushInit = 0;
                    var brushIncrease = 0;
                    var brushDirection = BeatStrokeDirection.none;

                    if (b.effect.stroke != null)
                    {
                        var notesCnt = b.notes.Count;
                        brushDirection = b.effect.stroke.direction;
                        if (brushDirection != BeatStrokeDirection.none && notesCnt > 1)
                        {
                            hasBrush = true;
                            var temp = new Duration { value = b.effect.stroke.value };
                            var brushTotalDuration = FlipDuration(temp);
                            //var beatTotalDuration = flipDuration(b.duration);


                            brushIncrease = brushTotalDuration / notesCnt;
                            var startPos = index + subIndex + (int)((brushTotalDuration - brushIncrease) *
                                                                     (b.effect.stroke.startTime - 1));
                            var endPos = startPos + brushTotalDuration - brushIncrease;

                            if (brushDirection == BeatStrokeDirection.down)
                            {
                                brushInit = startPos;
                            }
                            else
                            {
                                brushInit = endPos;
                                brushIncrease = -brushIncrease;
                            }
                        }
                    }

                    foreach (var n in b.notes)
                    {
                        var note = new Note { IsTremBarVibrato = b.effect.vibrato, Fading = Fading.None };
                        //Beat values
                        if (b.effect.fadeIn)
                        {
                            note.Fading = Fading.FadeIn;
                        }

                        if (b.effect.fadeOut)
                        {
                            note.Fading = Fading.FadeOut;
                        }

                        if (b.effect.volumeSwell)
                        {
                            note.Fading = Fading.VolumeSwell;
                        }

                        note.IsSlapped = b.effect.slapEffect == SlapEffect.slapping;
                        note.IsPopped = b.effect.slapEffect == SlapEffect.popping;
                        note.IsHammer = n.effect.hammer;
                        note.IsRhTapped = b.effect.slapEffect == SlapEffect.tapping;
                        note.Index = index + subIndex;
                        note.Duration = FlipDuration(b.duration);


                        //Note values
                        note.Fret = n.value;
                        note.Str = n.str;
                        note.Velocity = n.velocity;
                        note.IsVibrato = n.effect.vibrato;
                        note.IsPalmMuted = n.effect.palmMute;
                        note.IsMuted = n.type == NoteType.dead;

                        if (n.effect.harmonic != null)
                        {
                            note.HarmonicFret = n.effect.harmonic.fret;
                            if (n.effect.harmonic.fret == 0) //older format..
                            {
                                if (n.effect.harmonic.type == 2)
                                {
                                    note.HarmonicFret = ((ArtificialHarmonic)n.effect.harmonic).pitch
                                        .actualOvertone;
                                }
                            }

                            note.Harmonic = n.effect.harmonic.type switch
                            {
                                1 => HarmonicType.Natural,
                                2 => HarmonicType.Artificial,
                                3 => HarmonicType.Pinch,
                                4 => HarmonicType.Tapped,
                                5 => HarmonicType.Semi,
                                _ => HarmonicType.Natural
                            };
                        }

                        if (n.effect.slides != null)
                        {
                            foreach (var sl in n.effect.slides)
                            {
                                note.SlidesToNext = note.SlidesToNext || sl == SlideType.shiftSlideTo ||
                                                    sl == SlideType.legatoSlideTo;
                                note.SlideInFromAbove = note.SlideInFromAbove || sl == SlideType.intoFromAbove;
                                note.SlideInFromBelow = note.SlideInFromBelow || sl == SlideType.intoFromBelow;
                                note.SlideOutDownwards = note.SlideOutDownwards || sl == SlideType.outDownwards;
                                note.SlideOutUpwards = note.SlideOutUpwards || sl == SlideType.outUpwards;
                            }
                        }

                        if (n.effect.bend != null)
                        {
                            note.BendPoints = GetBendPoints(index + subIndex, FlipDuration(b.duration),
                                n.effect.bend);
                        }

                        //Ties

                        var dontAddNote = false;

                        if (n.type == NoteType.tie)
                        {
                            dontAddNote = true;
                            //Find if note can simply be added to previous note

                            var last = lastNotes[Math.Max(0, note.Str - 1)];


                            if (last != null)
                            {
                                note.Fret = last.Fret; //For GP3 & GP4
                                if (last.Harmonic != note.Harmonic ||
                                    Math.Abs(last.HarmonicFret - note.HarmonicFret) > 0.0001
                                )
                                {
                                    dontAddNote = false;
                                }

                                if (dontAddNote)
                                {
                                    note.Connect = true;
                                    last.Duration += note.Duration;
                                    last.AddBendPoints(note.BendPoints);
                                }
                            }
                        }
                        else // not a tie
                        {
                            lastWasTie[Math.Max(0, note.Str - 1)] = false;
                        }

                        //Extra notes to replicate certain effects


                        //Triplet Feel
                        if (_barMaster[measureIndex].TripletFeel != TripletFeel.none)
                        {
                            var trip = _barMaster[measureIndex].TripletFeel;
                            //Check if at regular 8th or 16th beat position
                            var is8ThPos = subIndex % 480 == 0;
                            var is16ThPos = subIndex % 240 == 0;
                            var isFirst = true; //first of note pair
                            if (is8ThPos)
                            {
                                isFirst = subIndex % 960 == 0;
                            }

                            if (is16ThPos)
                            {
                                isFirst = is8ThPos;
                            }

                            var is8Th = b.duration.value == 8 && !b.duration.isDotted &&
                                        !b.duration.isDoubleDotted && b.duration.tuplet.enters == 1 &&
                                        b.duration.tuplet.times == 1;
                            var is16Th = b.duration.value == 16 && !b.duration.isDotted &&
                                         !b.duration.isDoubleDotted && b.duration.tuplet.enters == 1 &&
                                         b.duration.tuplet.times == 1;

                            switch (trip)
                            {
                                case TripletFeel.eigth when is8ThPos && is8Th:
                                case TripletFeel.sixteenth when is16ThPos && is16Th:
                                    {
                                        if (isFirst)
                                        {
                                            note.Duration = (int)(note.Duration * (4.0f / 3.0f));
                                        }
                                        else
                                        {
                                            note.Duration = (int)(note.Duration * (2.0f / 3.0f));
                                            note.ResizeValue *= 2.0f / 3.0f;
                                            note.Index += (int)(note.Duration * (1.0f / 3.0f));
                                        }

                                        break;
                                    }
                                case TripletFeel.dotted8th when is8ThPos && is8Th:
                                case TripletFeel.dotted16th when is16ThPos && is16Th:
                                    {
                                        if (isFirst)
                                        {
                                            note.Duration = (int)(note.Duration * 1.5f);
                                        }
                                        else
                                        {
                                            note.Duration = (int)(note.Duration * 0.5f);
                                            note.ResizeValue *= 0.5f;
                                            note.Index += (int)(note.Duration * 0.5f);
                                        }

                                        break;
                                    }
                                case TripletFeel.scottish8th when is8ThPos && is8Th:
                                case TripletFeel.scottish16th when is16ThPos && is16Th:
                                    {
                                        if (isFirst)
                                        {
                                            note.Duration = (int)(note.Duration * 0.5f);
                                        }
                                        else
                                        {
                                            note.Duration = (int)(note.Duration * 1.5f);
                                            note.ResizeValue *= 1.5f;
                                            note.Index -= (int)(note.Duration * 0.5f);
                                        }

                                        break;
                                    }
                                case TripletFeel.none:
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(trip), trip, "Unknown enum value.");
                            }
                        }

                        //Tremolo Picking & Trill
                        if (n.effect.tremoloPicking != null || n.effect.trill != null)
                        {
                            var len = note.Duration;
                            if (n.effect.tremoloPicking != null)
                            {
                                len = FlipDuration(n.effect.tremoloPicking.duration);
                            }

                            if (n.effect.trill != null)
                            {
                                len = FlipDuration(n.effect.trill.duration);
                            }

                            var origDuration = note.Duration;
                            note.Duration = len;
                            note.ResizeValue *= (float)len / origDuration;
                            var currentIndex = note.Index + len;

                            lastNotes[Math.Max(0, note.Str - 1)] = note;
                            notes.Add(note);
                            notesInMeasure++;

                            dontAddNote = true; //Because we're doing it here already
                            var originalFret = false;
                            var secondFret = note.Fret;

                            if (n.effect.trill != null)
                            {
                                secondFret = n.effect.trill.fret - tuning[note.Str - 1];
                            }

                            while (currentIndex + len <= note.Index + origDuration)
                            {
                                var newOne = new Note(note) { Index = currentIndex };
                                if (!originalFret)
                                {
                                    newOne.Fret = secondFret; //For trills
                                }

                                lastNotes[Math.Max(0, note.Str - 1)] = newOne;
                                if (n.effect.trill != null)
                                {
                                    newOne.IsHammer = true;
                                }

                                notes.Add(newOne);
                                notesInMeasure++;
                                currentIndex += len;
                                originalFret = !originalFret;
                            }
                        }


                        //Grace Note
                        if (rememberGrace && note.Duration > graceLength)
                        {
                            var orig = note.Duration;
                            note.Duration -= graceLength;
                            note.ResizeValue *= (float)note.Duration / orig;
                            //subIndex -= graceLength;
                            rememberedGrace = true;
                        }

                        if (n.effect.grace != null)
                        {
                            var isOnBeat = n.effect.grace.isOnBeat;

                            if (n.effect.grace.duration != -1)
                            {
                                //GP3,4,5 format

                                var graceNote = new Note
                                {
                                    Index = note.Index,
                                    Fret = n.effect.grace.fret,
                                    Str = note.Str
                                };
                                var dur = new Duration { value = n.effect.grace.duration };
                                graceNote.Duration = FlipDuration(dur); //works at least for GP5
                                if (isOnBeat)
                                {
                                    var orig = note.Duration;
                                    note.Duration -= graceNote.Duration;
                                    note.Index += graceNote.Duration;
                                    note.ResizeValue *= (float)note.Duration / orig;
                                }
                                else
                                {
                                    graceNote.Index -= graceNote.Duration;
                                }

                                notes.Add(graceNote); //TODO: insert at correct position!
                                notesInMeasure++;
                            }
                            else
                            {
                                if (isOnBeat) // shorten next note
                                {
                                    rememberGrace = true;
                                    graceLength = note.Duration;
                                }
                                else //Change previous note
                                {
                                    if (notes.Count > 0)
                                    {
                                        note.Index -=
                                            note.Duration; //Can lead to negative indices. Midi should handle that
                                        subtractSubindex = note.Duration;
                                    }
                                }
                            }
                        }


                        //Dead Notes
                        if (n.type == NoteType.dead)
                        {
                            var orig = note.Duration;
                            note.Velocity = (int)(note.Velocity * 0.9f);
                            note.Duration /= 6;
                            note.ResizeValue *= (float)note.Duration / orig;
                        }

                        //Ghost Notes
                        if (n.effect.palmMute)
                        {
                            var orig = note.Duration;
                            note.Velocity = (int)(note.Velocity * 0.7f);
                            note.Duration /= 2;
                            note.ResizeValue *= (float)note.Duration / orig;
                        }

                        if (n.effect.ghostNote)
                        {
                            note.Velocity = (int)(note.Velocity * 0.8f);
                        }


                        //Staccato, Accented, Heavy Accented
                        if (n.effect.staccato)
                        {
                            var orig = note.Duration;
                            note.Duration /= 2;
                            note.ResizeValue *= (float)note.Duration / orig;
                        }

                        if (n.effect.accentuatedNote)
                        {
                            note.Velocity = (int)(note.Velocity * 1.2f);
                        }

                        if (n.effect.heavyAccentuatedNote)
                        {
                            note.Velocity = (int)(note.Velocity * 1.4f);
                        }

                        //Arpeggio / Brush
                        if (hasBrush)
                        {
                            note.Index = brushInit;
                            brushInit += brushIncrease;
                        }

                        if (!dontAddNote)
                        {
                            lastNotes[Math.Max(0, note.Str - 1)] = note;
                            notes.Add(note);
                            notesInMeasure++;
                        }
                    }

                    if (rememberedGrace)
                    {
                        subIndex -= graceLength;
                        rememberGrace = false;
                        rememberedGrace = false;
                    } //After the change in duration for the second beat has been done

                    subIndex -= subtractSubindex;
                    subtractSubindex = 0;
                    subIndex += FlipDuration(b.duration);

                    //Sort brushed tones
                    if (hasBrush && brushDirection == BeatStrokeDirection.up)
                    {
                        //Have to reorder them xxx123 -> xxx321
                        var notesCnt = b.notes.Count;
                        var temp = new Note[notesCnt];
                        for (var x = notes.Count - notesCnt; x < notes.Count; x++)
                        {
                            temp[x - (notes.Count - notesCnt)] = new Note(notes[x]);
                        }

                        for (var x = notes.Count - notesCnt; x < notes.Count; x++)
                        {
                            notes[x] = temp[temp.Length - (x - (notes.Count - notesCnt)) - 1];
                        }
                    }
                }

                break; //Consider only the first voice
            }

            var measureDuration =
                FlipDuration(m.header.timeSignature.denominator) * m.header.timeSignature.numerator;
            _barMaster[measureIndex].Duration = measureDuration;
            _barMaster[measureIndex].Index = index;
            index += measureDuration;
            _notesInMeasures.Add(notesInMeasure);
        }


        return notes;
    }


    private static int[] GetTuning(IReadOnlyList<GuitarString> strings)
    {
        var tuning = new int[strings.Count];
        for (var x = 0; x < tuning.Length; x++)
        {
            tuning[x] = strings[x].value;
        }

        return tuning;
    }

    private static List<MasterBar> RetrieveMasterBars(GPFile file)
    {
        var masterBars = new List<MasterBar>();
        foreach (var mh in file.measureHeaders)
        {
            //(mh.timeSignature.denominator) * mh.timeSignature.numerator;
            var mb = new MasterBar
            {
                Time = mh.timeSignature.numerator + "/" + mh.timeSignature.denominator.value,
                Num = mh.timeSignature.numerator,
                Den = mh.timeSignature.denominator.value
            };
            var keyFull = "" + (int)mh.keySignature;
            if (keyFull.Length != 1)
            {
                mb.KeyType = int.Parse(keyFull.Substring(keyFull.Length - 1), CultureInfo.InvariantCulture);
                mb.Key = int.Parse(keyFull.Substring(0, keyFull.Length - 1), CultureInfo.InvariantCulture);
            }
            else
            {
                mb.Key = 0;
                mb.KeyType = int.Parse(keyFull, CultureInfo.InvariantCulture);
            }

            mb.KeyBoth = keyFull; //Useful for midiExport later

            mb.TripletFeel = mh.tripletFeel;

            masterBars.Add(mb);
        }

        return masterBars;
    }

    private static List<Tempo> RetrieveTempos(GPFile file)
    {
        var tempos = new List<Tempo>();
        //Version < 4 -> look at Measure Headers, >= 4 look at mixtablechanges


        var version = file.versionTuple[0];
        if (version < 4) //Look at MeasureHeaders
        {
            //Get inital tempo from file header
            var init = new Tempo();
            init.Position = 0;
            init.Value = file.tempo;
            if (init.Value != 0)
            {
                tempos.Add(init);
            }

            var pos = 0;
            float oldTempo = file.tempo;
            foreach (var mh in file.measureHeaders)
            {
                var t = new Tempo { Value = mh.tempo.value, Position = pos };
                pos += FlipDuration(mh.timeSignature.denominator) * mh.timeSignature.numerator;
                if (Math.Abs(oldTempo - t.Value) > 0.0001)
                {
                    tempos.Add(t);
                }

                oldTempo = t.Value;
            }
        }
        else //Look at MixtableChanges - only on track 1, voice 1
        {
            var pos = 0;

            //Get inital tempo from file header
            var init = new Tempo { Position = 0, Value = file.tempo };
            if (init.Value != 0)
            {
                tempos.Add(init);
            }

            foreach (var m in file.tracks[0].measures)
            {
                var smallPos = 0; //inner measure position
                if (m.voices.Count == 0)
                {
                    continue;
                }

                foreach (var b in m.voices[0].beats)
                {
                    var tempo = b.effect?.mixTableChange?.tempo;
                    if (tempo != null)
                    {
                        var t = new Tempo { Value = tempo.value, Position = pos + smallPos };

                        tempos.Add(t);
                    }

                    smallPos += FlipDuration(b.duration);
                }

                pos += FlipDuration(m.header.timeSignature.denominator) * m.header.timeSignature.numerator;
            }
        }

        return tempos;
    }

    private static int FlipDuration(Duration duration)
    {
        const int ticksPerBeat = 960;
        var result = 0;
        result += duration.value switch
        {
            1 => ticksPerBeat * 4,
            2 => ticksPerBeat * 2,
            4 => ticksPerBeat,
            8 => ticksPerBeat / 2,
            16 => ticksPerBeat / 4,
            32 => ticksPerBeat / 8,
            64 => ticksPerBeat / 16,
            128 => ticksPerBeat / 32,
            _ => throw new ArgumentOutOfRangeException(nameof(duration.value), duration.value, "Invalid flip duration.")
        };

        if (duration.isDotted)
        {
            result = (int)(result * 1.5f);
        }

        if (duration.isDoubleDotted)
        {
            result = (int)(result * 1.75f);
        }

        var enters = duration.tuplet.enters;
        var times = duration.tuplet.times;

        //3:2 = standard triplet, 3 notes in the time of 2
        result = (int)(result * times / (float)enters);

        return result;
    }
}
