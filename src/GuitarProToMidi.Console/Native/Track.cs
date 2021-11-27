using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace GuitarProToMidi.Native;

public class Track
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public int Capo { get; init; }
    public int Channel { get; init; }
    public string Name { get; init; } = "";
    public List<Note> Notes { get; set; } = new();
    public int Patch { get; init; }
    public PlaybackState State { get; set; } = PlaybackState.Def;
    public int Port { get; init; }
    public List<TremoloPoint> TremoloPoints { get; private set; } = new();
    public int[] Tuning { get; set; } = { 40, 45, 50, 55, 59, 64 };
    private List<int[]> _volumeChanges = new();


    public MidiTrack GetMidi()
    {
        var midiTrack = new MidiTrack();
        midiTrack.messages.Add(new MidiMessage("midi_port", new[] { Port.ToString() }, 0));
        midiTrack.messages.Add(new MidiMessage("track_name", new[] { Name }, 0));
        midiTrack.messages.Add(new MidiMessage("program_change",
            new[] { Channel.ToString(), Patch.ToString() }, 0));
        if (!Notes.Any())
        {
            return midiTrack;
        }

        var noteOffs = new List<int[]>();
        var channelConnections =
            new List<int[]>(); //For bending and trembar: [original Channel, artificial Channel, index at when to delete artificial]
        var activeBendingPlans = new List<BendingPlan>();
        var currentIndex = 0;

        Notes.Add(new Note { Index = Notes[^1].Index + Notes[^1].Duration, Str = -2 });
        TremoloPoints = AddDetailsToTremoloPoints(TremoloPoints, 60);

        //var _notes = addSlidesToNotes(notes); //Adding slide notes here, as they should not appear as extra notes during playback

        foreach (var n in Notes)
        {
            noteOffs.Sort((x, y) => x[0].CompareTo(y[0]));


            //Check for active bendings in progress
            var currentBPs = FindAndSortCurrentBendPoints(activeBendingPlans, n.Index);
            var tremBarChange = 0.0f;
            foreach (var bp in currentBPs)
            {
                //Check first if there is a note_off event happening in the meantime..
                var newNoteOffs = new List<int[]>();
                foreach (var noteOff in noteOffs)
                {
                    if (noteOff[0] <= bp.Index) //between last and this note, a note off event should occur
                    {
                        midiTrack.messages.Add(
                            new MidiMessage("note_off",
                                new[] { "" + noteOff[2], "" + noteOff[1], "0" }, noteOff[0] - currentIndex));
                        currentIndex = noteOff[0];
                    }
                    else
                    {
                        newNoteOffs.Add(noteOff);
                    }
                }

                noteOffs = newNoteOffs;

                //Check if there are active tremPoints to be adjusted for
                var _newTremPoints = new List<TremoloPoint>();

                foreach (var tp in TremoloPoints)
                {
                    if (tp.Index <= bp.Index) //between last and this note, a note off event should occur
                    {
                        tremBarChange = tp.Value;
                    }
                    else
                    {
                        _newTremPoints.Add(tp);
                    }
                }

                TremoloPoints = _newTremPoints;

                //Check if there are active volume changes
                var _newVolumeChanges = new List<int[]>();
                foreach (var vc in _volumeChanges)
                {
                    if (vc[0] <= bp.Index) //between last and this note, a volume change event should occur
                    {
                        //channel control value
                        midiTrack.messages.Add(
                            new MidiMessage("control_change",
                                new[] { "" + bp.UsedChannel, "7", "" + vc[1] }, vc[0] - currentIndex));
                        currentIndex = vc[0];
                    }
                    else
                    {
                        _newVolumeChanges.Add(vc);
                    }
                }

                _volumeChanges = _newVolumeChanges;

                midiTrack.messages.Add(
                    new MidiMessage("pitchwheel",
                        new[] { "" + bp.UsedChannel, "" + (int)((bp.Value + tremBarChange) * 25.6f) },
                        bp.Index - currentIndex));
                currentIndex = bp.Index;
            }

            //Delete no longer active Bending Plans
            var final = new List<BendingPlan>();
            foreach (var bpl in activeBendingPlans)
            {
                var newBendingPlan = new BendingPlan(bpl.OriginalChannel, bpl.UsedChannel, new List<BendPoint>());
                foreach (var bp in bpl.BendingPoints.Where(bp => bp.Index > n.Index))
                {
                    newBendingPlan.BendingPoints.Add(bp);
                }

                if (newBendingPlan.BendingPoints.Count > 0)
                {
                    final.Add(newBendingPlan);
                }
                else //That bending plan has finished
                {
                    midiTrack.messages.Add(new MidiMessage("pitchwheel",
                        new[] { "" + bpl.UsedChannel, "-128" }, 0));
                    midiTrack.messages.Add(new MidiMessage("control_change",
                        new[] { "" + bpl.UsedChannel, "101", "127" }, 0));
                    midiTrack.messages.Add(new MidiMessage("control_change",
                        new[] { "" + bpl.UsedChannel, "10", "127" }, 0));

                    //Remove the channel from channelConnections
                    var newChannelConnections = new List<int[]>();
                    foreach (var cc in channelConnections)
                    {
                        if (cc[1] != bpl.UsedChannel)
                        {
                            newChannelConnections.Add(cc);
                        }
                    }

                    channelConnections = newChannelConnections;

                    Format.AvailableChannels[bpl.UsedChannel] = true;
                }
            }

            activeBendingPlans = final;


            var activeChannels = GetActiveChannels(channelConnections);
            var newTremPoints = new List<TremoloPoint>();
            foreach (var tp in TremoloPoints)
            {
                if (tp.Index <= n.Index) //between last and this note, a trembar event should occur
                {
                    var value = tp.Value * 25.6f;
                    value = Math.Min(Math.Max(value, -8192), 8191);
                    foreach (var ch in activeChannels)
                    {
                        midiTrack.messages.Add(
                            new MidiMessage("pitchwheel",
                                new[] { "" + ch, "" + (int)value }, tp.Index - currentIndex));
                        currentIndex = tp.Index;
                    }
                }
                else
                {
                    newTremPoints.Add(tp);
                }
            }

            TremoloPoints = newTremPoints;


            //Check if there are active volume changes
            var newVolumeChanges = new List<int[]>();
            foreach (var vc in _volumeChanges)
            {
                if (vc[0] <= n.Index) //between last and this note, a volume change event should occur
                {
                    foreach (var ch in activeChannels)
                    {
                        midiTrack.messages.Add(
                            new MidiMessage("control_change",
                                new[] { "" + ch, "7", "" + vc[1] }, vc[0] - currentIndex));
                        currentIndex = vc[0];
                    }
                }
                else
                {
                    newVolumeChanges.Add(vc);
                }
            }

            _volumeChanges = newVolumeChanges;


            var temp = new List<int[]>();
            foreach (var noteOff in noteOffs)
            {
                if (noteOff[0] <= n.Index) //between last and this note, a note off event should occur
                {
                    midiTrack.messages.Add(
                        new MidiMessage("note_off",
                            new[] { "" + noteOff[2], "" + noteOff[1], "0" }, noteOff[0] - currentIndex));
                    currentIndex = noteOff[0];
                }
                else
                {
                    temp.Add(noteOff);
                }
            }

            noteOffs = temp;

            int note;

            if (n.Str == -2)
            {
                break; //Last round
            }

            if (n.Str - 1 < 0)
            {
                Logger.Debug("String was -1");
            }

            if (n.Str - 1 >= Tuning.Length && Tuning.Length != 0)
            {
                Logger.Debug("String was higher than string amount (" + n.Str + ")");
            }

            if (Tuning.Length > 0)
            {
                note = Tuning[n.Str - 1] + Capo + n.Fret;
            }
            else
            {
                note = Capo + n.Fret;
            }

            if (n.Harmonic != HarmonicType.None) //Has Harmonics
            {
                var harmonicNote = GetHarmonic(Tuning[n.Str - 1], n.Fret, Capo, n.HarmonicFret, n.Harmonic);
                note = harmonicNote;
            }

            var noteChannel = Channel;

            if (n.BendPoints.Count > 0) //Has Bending
            {
                var usedChannel = TryToFindChannel();
                if (usedChannel == -1)
                {
                    usedChannel = Channel;
                }

                Format.AvailableChannels[usedChannel] = false;
                channelConnections.Add(new[] { Channel, usedChannel, n.Index + n.Duration });
                midiTrack.messages.Add(new MidiMessage("program_change",
                    new[] { "" + usedChannel, "" + Patch }, n.Index - currentIndex));
                noteChannel = usedChannel;
                currentIndex = n.Index;
                activeBendingPlans.Add(BendingPlan.create(n.BendPoints, Channel, usedChannel, n.Duration, n.Index,
                    n.ResizeValue, n.IsVibrato));
            }

            if (n.IsVibrato && n.BendPoints.Count == 0) //Is Vibrato & No Bending
            {
                var usedChannel = Channel;
                activeBendingPlans.Add(BendingPlan.create(n.BendPoints, Channel, usedChannel, n.Duration, n.Index,
                    n.ResizeValue, true));
            }

            if (n.Fading != Fading.None) //Fading
            {
                _volumeChanges = CreateVolumeChanges(n.Index, n.Duration, n.Velocity, n.Fading);
            }

            midiTrack.messages.Add(new MidiMessage("note_on",
                new[] { "" + noteChannel, "" + note, "" + n.Velocity }, n.Index - currentIndex));
            currentIndex = n.Index;

            if (n.BendPoints.Count > 0) //Has Bending cont.
            {
                midiTrack.messages.Add(new MidiMessage("control_change",
                    new[] { "" + noteChannel, "101", "0" }, 0));
                midiTrack.messages.Add(new MidiMessage("control_change",
                    new[] { "" + noteChannel, "100", "0" }, 0));
                midiTrack.messages.Add(new MidiMessage("control_change",
                    new[] { "" + noteChannel, "6", "6" }, 0));
                midiTrack.messages.Add(new MidiMessage("control_change",
                    new[] { "" + noteChannel, "38", "0" }, 0));
            }

            noteOffs.Add(new[] { n.Index + n.Duration, note, noteChannel });
        }


        midiTrack.messages.Add(new MidiMessage("end_of_track", new string[] { }, 0));
        return midiTrack;
    }

    private static List<Note> AddSlidesToNotes(IEnumerable<Note> notes)
    {
        var ret = new List<Note>();
        var index = -1;
        foreach (var n in notes)
        {
            index++;
            var skipWrite = false;

            if (n.SlideInFromBelow && n.Str > 1 || n.SlideInFromAbove)
            {
                var myFret = n.Fret;
                var start = n.SlideInFromAbove ? myFret + 4 : Math.Max(1, myFret - 4);
                var beginIndex = n.Index - 960 / 4; //16th before
                var lengthEach = 960 / 4 / Math.Abs(myFret - start);
                for (var x = 0; x < Math.Abs(myFret - start); x++)
                {
                    var newOne = new Note(n)
                    {
                        Duration = lengthEach,
                        Index = beginIndex + x * lengthEach,
                        Fret = start + (n.SlideInFromAbove ? -x : +x)
                    };
                    ret.Add(newOne);
                }
            }

            if (n.SlideOutDownwards && n.Str > 1 || n.SlideOutUpwards)
            {
                var myFret = n.Fret;
                var end = n.SlideOutUpwards ? myFret + 4 : Math.Max(1, myFret - 4);
                var beginIndex = n.Index + n.Duration - 960 / 4; //16th before
                var lengthEach = 960 / 4 / Math.Abs(myFret - end);
                n.Duration -= 960 / 4;
                ret.Add(n);
                skipWrite = true;
                for (var x = 0; x < Math.Abs(myFret - end); x++)
                {
                    var newOne = new Note(n);
                    newOne.Duration = lengthEach;
                    newOne.Index = beginIndex + x * lengthEach;
                    newOne.Fret = myFret + (n.SlideOutDownwards ? -x : +x);
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

            if (!skipWrite)
            {
                ret.Add(n);
            }
        }

        return ret;
    }

    private static List<int[]> CreateVolumeChanges(int index, int duration, int velocity, Fading fading)
    {
        const int segments = 20;
        var changes = new List<int[]>();
        switch (fading)
        {
            case Fading.FadeIn:
            case Fading.FadeOut:
                {
                    var step = velocity / segments;
                    var val = fading == Fading.FadeIn ? 0 : velocity;
                    if (fading == Fading.FadeOut)
                    {
                        step = (int)(-step * 1.25f);
                    }

                    for (var x = index; x < index + duration; x += duration / segments)
                    {
                        changes.Add(new[] { x, Math.Min(127, Math.Max(0, val)) });
                        val += step;
                    }

                    break;
                }
            case Fading.VolumeSwell:
                {
                    var step = (int)(velocity / (segments * 0.8f));
                    var val = 0;
                    var times = 0;
                    for (var x = index; x < index + duration; x += duration / segments)
                    {
                        changes.Add(new[] { x, Math.Min(127, Math.Max(0, val)) });
                        val += step;
                        if (times == segments / 2)
                        {
                            step = -step;
                        }

                        times++;
                    }

                    break;
                }
            case Fading.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fading), fading, "Unknown enum value.");
        }

        changes.Add(new[] { index + duration, velocity }); //Definitely go back to normal


        return changes;
    }

    private List<int> GetActiveChannels(IEnumerable<int[]> channelConnections)
    {
        var activeChannels = new List<int> { Channel };
        activeChannels.AddRange(channelConnections.Select(cc => cc[1]));

        return activeChannels;
    }

    private static int TryToFindChannel()
    {
        var cnt = 0;
        foreach (var available in Format.AvailableChannels)
        {
            if (available)
            {
                return cnt;
            }

            cnt++;
        }

        return -1;
    }

    private static int GetHarmonic(int baseTone, int fret, int capo, float harmonicFret, HarmonicType type)
    {
        //Capo, base tone and fret (if not natural harmonic) shift the harmonics simply
        var val = baseTone + capo;
        if (type != HarmonicType.Natural)
        {
            val += (int)Math.Round(harmonicFret);
        }

        val += fret;

        val += harmonicFret switch
        {
            2.4f => 34,
            2.7f => 31,
            3.2f => 28,
            4f => 24,
            5f => 19,
            5.8f => 28,
            7f => 12,
            8.2f => 28,
            9f => 19,
            9.6f => 24,
            12f => 0,
            14.7f => 19,
            16f => 12,
            17f => 19,
            19f => 0,
            21.7f => 12,
            24f => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(harmonicFret), harmonicFret, "Unhandled value.")
        };

        return Math.Min(val, 127);
    }


    private static IEnumerable<BendPoint> FindAndSortCurrentBendPoints(IEnumerable<BendingPlan> activeBendingPlans,
        int index)
    {
        var bendPoints = new List<BendPoint>();
        foreach (var bendingPlan in activeBendingPlans)
        {
            foreach (var bendPoint in bendingPlan.BendingPoints.Where(bp => bp.Index <= index))
            {
                bendPoints.Add(bendPoint with { UsedChannel = bendingPlan.UsedChannel });
            }
        }

        bendPoints.Sort((x, y) => x.Index.CompareTo(y.Index));

        return bendPoints;
    }

    private static List<TremoloPoint> AddDetailsToTremoloPoints(IEnumerable<TremoloPoint> tremoloPoints,
        int maxDistance)
    {
        var tremPoints = new List<TremoloPoint>();
        var oldValue = 0.0f;
        var oldIndex = 0;
        foreach (var tp in tremoloPoints)
        {
            if (tp.Index - oldIndex > maxDistance && !(Math.Abs(oldValue) < 0.0001 && Math.Abs(tp.Value) < 0.0001))
            //Add in-between points
            {
                for (var x = oldIndex + maxDistance; x < tp.Index; x += maxDistance)
                {
                    var value = oldValue + (tp.Value - oldValue) *
                        (((float)x - oldIndex) / ((float)tp.Index - oldIndex));
                    tremPoints.Add(new TremoloPoint(x, value));
                }
            }

            tremPoints.Add(tp);

            oldValue = tp.Value;
            oldIndex = tp.Index;
        }


        return tremPoints;
    }
}
