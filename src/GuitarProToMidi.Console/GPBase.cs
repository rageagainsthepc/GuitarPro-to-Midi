using System;
using System.Collections.Generic;

namespace GuitarProToMidi;

abstract public class GPFile
{
    //Parent class for common type
    abstract public void readSong();


    public GP4File.Clipboard clipboard = null;
    public RSEMasterEffect masterEffect = null;
    public PageSetup pageSetup = null;
    public int tempo;
    public string tempoName;
    public bool hideTempo;
    public List<DirectionSign> directions = new List<DirectionSign>();
    public string words;
    public string music;
    public List<Track> tracks = new List<Track>();
    public GPFile self;
    public string title;
    public string subtitle;
    public string interpret;
    public string album;
    public string author;
    public string copyright;
    public string tab_author;
    public string instructional;
    public int[] versionTuple = new int[] { };
    public string version = "";
    public List<Lyrics> lyrics = new List<Lyrics>();
    public List<MeasureHeader> measureHeaders = new List<MeasureHeader>();
    public TripletFeel _tripletFeel;
}
public class GPBase
{
    public const int bendPosition = 60;
    public const int bendSemitone = 25;
    public static int pointer = 0;
    public static byte[] data;

    public static void skip(int count)
    {
        pointer += count;
    }

    public static byte[] readByte(int count = 1)
    {
        return extract(pointer, count, true);
    }

    public static sbyte[] readSignedByte(int count = 1)
    {
        byte[] unsigned = extract(pointer, count, true);
        sbyte[] ret_val = new sbyte[unsigned.Length];
        for (int x = 0; x < unsigned.Length; x++)
        {
            ret_val[x] = (sbyte)unsigned[x];
        }
        return ret_val;
    }

    public static bool[] readBool(int count = 1)
    {
        byte[] vals = extract(pointer, count, true);
        bool[] ret_val = new bool[vals.Length];
        for (int x = 0; x < vals.Length; x++)
        {
            ret_val[x] = (vals[x] != 0x0);
        }
        return ret_val;
    }

    public static short[] readShort(int count = 1)
    {
        byte[] vals = extract(pointer, count * 2, true);
        short[] ret_val = new short[count];
        for (int x = 0; x < vals.Length; x += 2)
        {
            ret_val[x / 2] = (short)(vals[x] + (vals[x + 1] << 8));
        }
        return ret_val;
    }
    public static int[] readInt(int count = 1)
    {
        byte[] vals = extract(pointer, count * 4, true);
        int[] ret_val = new int[count];
        for (int x = 0; x < vals.Length; x += 4)
        {
            ret_val[x / 4] = (int)(vals[x] + (vals[x + 1] << 8) + (vals[x + 2] << 16) + (vals[x + 3] << 24));
        }
        return ret_val;
    }

    public static float[] readFloat(int count = 1)
    {
        byte[] vals = extract(pointer, count * 4, true);
        float[] ret_val = new float[count];
        for (int x = 0; x < vals.Length; x += 4)
        {
            ret_val[x / 4] = System.BitConverter.ToSingle(vals, x);
        }
        return ret_val;
    }

    public static double[] readDouble(int count = 1)
    {
        byte[] vals = extract(pointer, count * 8, true);
        double[] ret_val = new double[count];
        for (int x = 0; x < vals.Length; x += 8)
        {
            ret_val[x / 8] = System.BitConverter.ToDouble(vals, x);
        }
        return ret_val;
    }

    public static string readString(int size, int length = 0)
    {
        if (length == 0)
        {
            length = size;
        }
        int count = (size > 0) ? size : length;
        byte[] ss = (length >= 0) ? extract(pointer, length, true) : extract(pointer, size, true);
        skip(count - ss.Length);
        return System.Text.Encoding.Default.GetString(ss);
    }

    public static string readByteSizeString(int size)
    {
        return readString(size, (int)readByte()[0]);
    }

    public static string readIntSizeString()
    {
        return readString(readInt()[0]);
    }
    public static string readIntByteSizeString()
    {
        //Read length of the string increased by 1 and stored in 1 integer
        //followed by length of the string in 1 byte and finally followed by
        //character bytes.

        int d = readInt()[0] - 1;

        return readByteSizeString(d);
    }



    public static byte[] extract(int start, int length, bool advance_pointer)
    {
        if (length <= 0)
        {
            return new byte[Math.Max(0, length)];
        }
        if (length + start > data.Length)
        {
            return new byte[Math.Max(0, length)];
        }

        byte[] ret = new byte[length];
        for (int x = start; x < start + length; x++)
        {
            ret[x - start] = data[x];
        }
        if (advance_pointer) pointer += length;

        return ret;
    }
}

public class Barre
{
    public int start = 0;
    public int end = 0;
    public int fret;
    public Barre(int fret = 0, int start = 0, int end = 0)
    {
        this.start = start; this.fret = fret; this.end = end;
    }
    public int[] range()
    {
        return new int[] { start, end };
    }
}

public class Beat
{
    public Voice voice;
    public List<Voice> voices = new List<Voice>();
    public Measure measure;
    public Duration duration = new Duration();
    public int start = Duration.quarterTime;
    public BeatEffect effect = new BeatEffect();
    public Octave octave = Octave.none;
    public BeatDisplay display = new BeatDisplay();
    public List<Note> notes = new List<Note>();
    public BeatStatus status;
    public BeatText text = new BeatText();

    public Beat(Voice voice = null)
    {
        this.voice = voice;
    }

    public int realStart()
    {
        int offset = start + measure.start();
        return measure.header.realStart + offset;
    }

    public bool hasVibrato()
    {
        foreach (Note note in notes)
        {
            if (note.effect.vibrato) return true;
        }
        return false;
    }

    public bool hasHarmonic()
    {
        foreach (Note note in notes)
        {
            if (note.effect.isHarmonic()) return true;
        }
        return false;
    }

    public void addNote(Note note)
    {
        note.beat = this;
        notes.Add(note);
    }
}

public class BeatDisplay
{
    public bool breakBeam = false;
    public bool forceBeam = false;
    public VoiceDirection beamDirection = VoiceDirection.none;
    public TupletBracket tupletBracket = TupletBracket.none;
    public int breakSecondary = 0;
    public bool breakSecondaryTuplet = false;
    public bool forceBracket = false;
}

public class BeatEffect
{
    public bool fadeIn = false;
    public bool fadeOut = false;
    public bool volumeSwell = false;

    public BeatStrokeDirection pickStroke = BeatStrokeDirection.none;
    public bool hasRasgueado = false;
    public BeatStroke stroke = null;
    public SlapEffect slapEffect = SlapEffect.none;
    public bool vibrato = false;
    public Chord chord = null;
    public BendEffect tremoloBar = null;
    public MixTableChange mixTableChange;

    public bool isChord() { return chord != null; }
    public bool isTremoloBar() { return tremoloBar != null; }
    public bool isSlapEffect() { return slapEffect != SlapEffect.none; }
    public bool hasPickStroke() { return pickStroke != BeatStrokeDirection.none; }
    public bool isDefault()
    {
        BeatEffect def = new BeatEffect();
        return (stroke == def.stroke && hasRasgueado == def.hasRasgueado &&
            pickStroke == def.pickStroke && fadeIn == def.fadeIn &&
            vibrato == def.vibrato && tremoloBar == def.tremoloBar &&
            slapEffect == def.slapEffect);

    }
}

public class BeatStroke
{
    public BeatStrokeDirection direction = BeatStrokeDirection.none;
    public int value = 0; //4 = quarter etc.
    public float startTime = 0.0f; //0 = falls on time, 1 = starts on time
    public BeatStroke() { }

    public BeatStroke(BeatStrokeDirection d, int v, float s)
    {
        direction = d;
        value = v;
        startTime = s;
    }

    public void setByGP6Standard(int GP6Duration)
    {
        //GP6 will use value as 30 to 480 (64th to quarter note)
        int[] possibleVals = { 1, 2, 4, 8, 16, 32, 64 };
        int translated = 64 / (GP6Duration / 30);
        int lastVal = 0;
        foreach (int val in possibleVals)
        {
            if (val == translated) { value = val; break; }
            if (val > translated && lastVal < translated)
            {
                value = (translated - lastVal > val - translated) ? val : lastVal;
                break;
            }
            lastVal = val;
        }
    }

    public int getIncrementTime(Beat beat)
    {
        int duration = 0;
        if (value > 0)
        {
            foreach (Voice voice in beat.voices)
            {
                if (voice.isEmpty()) continue;

                int currentDuration = voice.duration.time();
                if (duration == 0 || currentDuration < duration)
                {
                    duration = ((currentDuration <= Duration.quarterTime) ? currentDuration : Duration.quarterTime);
                }
                if (duration > 0)
                {
                    return (int)Math.Round((duration / 8.0f) * (4.0f / value));
                }
            }
        }
        return 0;
    }

    public BeatStroke swapDirection()
    {
        if (direction == BeatStrokeDirection.up)
        {
            direction = BeatStrokeDirection.down;
        }
        else if (direction == BeatStrokeDirection.down)
        {
            direction = BeatStrokeDirection.up;
        }
        return new BeatStroke(direction, value, 0.0f);
    }

}

public class BeatText
{
    public string value;
    public BeatText(string value = "")
    {
        this.value = value;
    }
}

public class BendEffect
{
    public const int semitoneLength = 1;
    public const int maxPosition = 12;
    public int maxValue = semitoneLength * 12;
    public BendType type = BendType.none;
    public int value = 0;
    public List<BendPoint> points = new List<BendPoint>();
}

public class BendPoint
{
    public int position = 0;
    public int value;

    public float GP6position = 0;
    public float GP6value = 0;
    bool vibrato = false;

    public BendPoint(int position = 0, int value = 0, bool vibrato = false)
    {
        this.position = position;
        this.value = value;
        this.vibrato = vibrato;
        GP6position = position * 100.0f / BendEffect.maxPosition;
        GP6value = value * 25.0f / BendEffect.semitoneLength;
    }

    public BendPoint(float position, float value, bool isGP6Format = true)
    {
        if (isGP6Format)
        {
            //GP6 Format: position: 0-100, value: 100 = 1 whole tone up
            this.position = (int)(position * BendEffect.maxPosition / 100);
            this.value = (int)(value * 2 * BendEffect.semitoneLength / 100);
            GP6position = position;
            GP6value = value;
        }
        else
        {
            this.position = (int)position;
            this.value = (int)value;
            GP6position = position * 100.0f / BendEffect.maxPosition;
            GP6value = value * 50.0f / BendEffect.semitoneLength;
        }
    }

    public int getTime(int duration)
    {
        return (int)(duration * (float)position / (float)BendEffect.maxPosition);
    }
}

public class Chord
{
    public int[] strings;
    public string name = "";
    public List<Barre> barres = new List<Barre>();
    public bool[] omissions = new bool[7];
    public List<Fingering> fingerings = new List<Fingering>();
    public bool newFormat;
    public int firstFret;
    public bool sharp;
    public PitchClass root;
    public ChordType type;
    public ChordExtension extension;
    public PitchClass bass;
    public ChordAlteration tonality;
    public bool add;
    public ChordAlteration fifth;
    public ChordAlteration ninth;
    public ChordAlteration eleventh;
    public bool show = true;

    public Chord(int length)
    {

        strings = new int[length];
        for (int x = 0; x < length; x++)
        {
            strings[x] = -1;
        }
    }

    public int[] notes()
    {
        List<int> valids = new List<int>();
        foreach (int s in strings)
        {
            if (s >= 0) valids.Add(s);
        }
        return valids.ToArray();
    }
}

public class DirectionSign
{
    public string name = "";
    public short measure = 0;
    public DirectionSign(string name = "", short measure = 0)
    {
        this.name = name;
        this.measure = measure;
    }
}
public class Duration
{
    public const int quarterTime = 960;
    public const int whole = 1;
    public const int half = 2;
    public const int quarter = 4;
    public const int eigth = 8;
    public const int sixteenth = 16;
    public const int thirtySecond = 32;
    public const int sixtyFourth = 64;
    public const int hundredTwentyEigth = 128;

    public int value = quarter;
    public bool isDotted = false;
    public bool isDoubleDotted = false;
    public Tuplet tuplet = new Tuplet();

    const int minTime = (int)((int)(quarterTime * (4.0f / sixtyFourth)) * 2.0f / 3.0f);
    public Duration() { }
    public Duration(int time) //Does not recognize tuplets
    { //From GP6 Format -> 30 = 64th, 480 = quarter, 1920 = whole
        int substract = 0;
        if (time >= 15) { value = hundredTwentyEigth; substract = 15; }
        if (time >= 30) { value = sixtyFourth; substract = 30; }
        if (time >= 60) { value = thirtySecond; substract = 60; }
        if (time >= 120) { value = sixteenth; substract = 120; }
        if (time >= 240) { value = eigth; substract = 240; }
        if (time >= 480) { value = quarter; substract = 480; }
        if (time >= 960) { value = half; substract = 960; }
        if (time >= 1920) { value = whole; substract = 1920; }
        time -= substract;
        if (time >= (float)(value * 0.5f)) isDotted = true;
        if (time >= (float)(value * 0.75f)) { isDotted = false; isDoubleDotted = true; }



    }

    public int time()
    {
        int result = (int)(quarterTime * (4.0f / value));
        if (isDotted) result += (int)(result / 2.0f);
        if (isDoubleDotted) result += (int)((result / 4.0f) * 3);
        return tuplet.convertTime(result);
    }

}

public class GraceEffect
{
    public int fret = 0;
    public int duration = -1;
    public int velocity = Velocities.def;
    public GraceEffectTransition transition = GraceEffectTransition.none;
    public bool isOnBeat = false;
    public bool isDead = false;

    public int durationTime()
    {
        return (int)(Duration.quarterTime / 16.0f * duration);

    }
}

public class GuitarString
{
    public int number, value;
    public GuitarString(int number, int value)
    {
        this.number = number; this.value = value;
    }
}

public abstract class HarmonicEffect
{
    public float fret = 0;
    public int type = 0;
}
public class NaturalHarmonic : HarmonicEffect { public NaturalHarmonic() { type = 1; } }
public class ArtificialHarmonic : HarmonicEffect
{
    public PitchClass pitch;
    public Octave octave;
    public ArtificialHarmonic(PitchClass pitch = null, Octave octave = 0)
    {
        this.pitch = pitch;
        this.octave = octave;
        this.type = 2;
    }
}
public class TappedHarmonic : HarmonicEffect
{
    public TappedHarmonic(int fret = 0)
    {
        this.fret = fret;
        this.type = 3;
    }
}
public class PinchHarmonic : HarmonicEffect { public PinchHarmonic() { type = 4; } }
public class SemiHarmonic : HarmonicEffect { public SemiHarmonic() { type = 5; } }
public class FeedbackHarmonic : HarmonicEffect { public FeedbackHarmonic() { type = 6; } }
public class LyricLine
{
    public int startingMeasure = 1;
    public string lyrics = "";
}

public class Lyrics
{
    static int maxLineCount = 5;
    public int trackChoice;
    public LyricLine[] lines;


    public Lyrics()
    {
        trackChoice = -1;
        lines = new LyricLine[maxLineCount];
        for (int x = 0; x < maxLineCount; x++)
        {
            lines[x] = new LyricLine();
        }
    }
}

public class Marker
{
    public string title = "Section";
    public myColor color = new myColor(255, 0, 0);
    public MeasureHeader measureHeader = null;
}

public enum SimileMark
{
    none = 0, simple = 1, firstOfDouble = 2, secondOfDouble = 3
}
public class Measure
{
    public const int maxVoices = 2;
    public Track track;
    public MeasureHeader header;
    public MeasureClef clef = MeasureClef.treble;
    public List<Voice> voices = new List<Voice>();
    public LineBreak lineBreak = LineBreak.none;
    public List<Beat> beats = new List<Beat>();
    public SimileMark simileMark = SimileMark.none;

    public Measure(Track track = null, MeasureHeader header = null)
    {
        if (voices.Count == 0)
        {
            for (int x = 0; x < maxVoices; x++)
            {
                voices.Add(new Voice(this));
            }
        }
        this.header = header;
        this.track = track;
    }

    public bool isEmpty()
    {
        foreach (Voice v in voices)
        {
            if (!v.isEmpty()) return false;
        }
        if (beats.Count != 0) return false;
        return true;
    }

    public int end()
    {
        return start() + length();
    }
    public int number()
    {
        return header.number;
    }
    public KeySignature keySignature()
    {
        return header.keySignature;
    }
    public int repeatClose()
    {
        return header.repeatClose;
    }
    public int start()
    {
        return header.start;
    }
    public int length()
    {
        return header.length();
    }
    public Tempo tempo()
    {
        return header.tempo;
    }
    public TimeSignature timeSignature()
    {
        return header.timeSignature;
    }
    public bool isRepeatOpen()
    {
        return header.isRepeatOpen;
    }
    public TripletFeel tripletFeel()
    {
        return header.tripletFeel;
    }
    public bool hasMarker()
    {
        return header.hasMarker();
    }
    public Marker marker()
    {
        return header.marker;
    }
    public void addVoice(Voice voice)
    {
        voice.measure = this;
        voices.Add(voice);
    }
}

public class MeasureHeader
{
    public GPFile song;
    public RepeatGroup repeatGroup = null;
    public int number = 0;
    public int start = Duration.quarterTime;
    public TimeSignature timeSignature = new TimeSignature();
    public KeySignature keySignature = KeySignature.CMajor;
    public Tempo tempo = new Tempo();
    public TripletFeel tripletFeel = TripletFeel.none;
    public bool isRepeatOpen = false;
    public int repeatClose = -1;
    public List<int> repeatAlternatives = new List<int>();
    public int realStart = -1;
    public bool hasDoubleBar = false;
    public Marker marker = null;
    public List<string> direction = new List<string>();
    public List<string> fromDirection = new List<string>();

    public bool hasMarker()
    {
        return (marker != null);
    }

    public int length()
    {
        return timeSignature.numerator * timeSignature.denominator.time();
    }

}

public class MidiChannel
{
    public int channel, effectChannel, instrument, volume, balance, chorus,
        reverb, phaser, tremolo, bank;

    static int DEFAULT_PERCUSSION_CHANNEL = 9;

    public MidiChannel()
    {
        this.channel = 0; this.effectChannel = 1; this.instrument = 25; this.volume = 104;
        this.balance = 64; this.chorus = 0; this.reverb = 0; this.phaser = 0;
        this.tremolo = 0; this.bank = 0;
    }

    public bool isPercussionChannel()
    {
        return channel % 16 == DEFAULT_PERCUSSION_CHANNEL;
    }
}

public enum WahState
{
    off = -2, none = -1, opened = 0, closed = 100
}
public class WahEffect
{
    public WahState state = WahState.none;
    public bool display = false;
}
public class MixTableChange
{
    public string tempoName;
    public bool hideTempo;
    public bool useRSE;
    public MixTableItem instrument = null;
    public MixTableItem volume = null;
    public MixTableItem balance = null;
    public MixTableItem chorus = null;
    public MixTableItem reverb = null;
    public MixTableItem phaser = null;
    public MixTableItem tremolo = null;
    public MixTableItem tempo = null;
    public WahEffect wah = null;
    public RSEInstrument rse = null;

    public MixTableChange(string tempoName = "", bool hideTempo = true, bool useRSE = true)
    {
        this.tempoName = tempoName;
        this.hideTempo = hideTempo;
        this.useRSE = useRSE;
    }

    public bool isJustWah()
    {
        return (instrument == null && volume == null && balance == null && chorus == null && reverb == null &&
            phaser == null && tremolo == null && wah != null);
    }

}

public class MixTableItem
{
    public int value;
    public int duration;
    public bool allTracks;
    public MixTableItem(int value = 0, int duration = 0, bool allTracks = false)
    {
        this.value = value;
        this.duration = duration;
        this.allTracks = allTracks;
    }

}

public class myColor
{
    float r, g, b, a = 1.0f;
    public myColor(int r, int g, int b, int a = 255)
    {
        this.r = (float)r / 255.0f;
        this.g = (float)g / 255.0f;
        this.b = (float)b / 255.0f;
        this.a = (float)a / 255.0f;
    }

}

public class Note
{
    public Beat beat;
    public int value = 0;
    public int velocity = Velocities.def;
    public int str = 0;
    public bool swapAccidentals = false;
    public NoteEffect effect = new NoteEffect();
    public double durationPercent = 1.0;
    public NoteType type = NoteType.rest;
    public int duration;
    public int tuplet;

    public Note(Beat beat = null)
    {
        this.beat = beat;
    }

    public int realValue()
    {
        return (value + beat.voice.measure.track.strings[str - 1].value);
    }

}

public class NoteEffect
{
    public bool vibrato = false;
    public List<SlideType> slides = new List<SlideType>();
    public bool hammer = false;
    public bool ghostNote = false;
    public bool accentuatedNote = false;
    public bool heavyAccentuatedNote = false;
    public bool palmMute = false;
    public bool staccato = false;
    public bool letRing = false;
    public Fingering leftHandFinger = Fingering.open;
    public Fingering rightHandFinger = Fingering.open;
    public Note note = null;
    public BendEffect bend = null;
    public HarmonicEffect harmonic = null;
    public GraceEffect grace = null;
    public TrillEffect trill = null;
    public TremoloPickingEffect tremoloPicking = null;

    public bool isBend()
    {
        return (bend != null && bend.points.Count > 0);
    }
    public bool isHarmonic() { return harmonic != null; }
    public bool isGrace() { return grace != null; }
    public bool isTrill() { return trill != null; }
    public bool isTremoloPicking() { return tremoloPicking != null; }

    public bool isFingering() { return ((int)leftHandFinger > -1 || (int)rightHandFinger > -1); }
}

public class Padding
{
    public int right, top, left, bottom;
    public Padding(int right = 0, int top = 0, int left = 0, int bottom = 0)
    {
        this.right = right; this.top = top; this.left = left; this.bottom = bottom;
    }
}
public class Point
{
    public int x, y;
    public Point(int x = 0, int y = 0)
    {
        this.x = x; this.y = y;
    }
}

public enum HeaderFooterElements
{
    none = 0x000,
    title = 0x001,
    subtitle = 0x002,
    artist = 0x004,
    album = 0x008,
    words = 0x010,
    music = 0x020,
    wordsAndMusic = 0x040,
    copyright = 0x080,
    pageNumber = 0x100,
    all = title | subtitle | artist | album | words | music | wordsAndMusic | copyright | pageNumber
}
public class PageSetup
{
    /*The page setup describes how the document is rendered.

        Page setup contains page size, margins, paddings, and how the title
        elements are rendered.

        Following template vars are available for defining the page texts:

        - ``%title%``: will be replaced with Song.title
        - ``%subtitle%``: will be replaced with Song.subtitle
        - ``%artist%``: will be replaced with Song.artist
        - ``%album%``: will be replaced with Song.album
        - ``%words%``: will be replaced with Song.words
        - ``%music%``: will be replaced with Song.music
        - ``%WORDSANDMUSIC%``: will be replaced with the according word
          and music values
        - ``%copyright%``: will be replaced with Song.copyright
        - ``%N%``: will be replaced with the current page number (if
          supported by layout)
        - ``%P%``: will be replaced with the number of pages (if supported
          by layout)*/
    public Point pageSize = new Point(210, 297);
    public Padding pageMargin = new Padding(10, 15, 10, 10);
    public float scoreSizeProportion = 1.0f;
    public HeaderFooterElements headerAndFooter = HeaderFooterElements.all;
    public string title = "%title%";
    public string subtitle = "%subtitle%";
    public string artist = "%artist%";
    public string album = "%album%";
    public string words = "Words by %words%";
    public string music = "Music by %music%";
    public string wordsAndMusic = "Words & Music by %WORDSMUSIC%";
    public string copyright = "Copyright %copyright%\nAll Rights Reserved - International Copyright Secured";
    public string pageNumber = "Page %N%/%P%";

}
public class PitchClass
{
    /*Constructor provides several overloads. Each overload provides keyword
    argument *intonation* that may be either "sharp" or "flat".

    First of overloads is (tone, accidental):

    :param tone: integer of whole-tone.
    :param accidental: flat (-1), none (0) or sharp (1).

    >>> p = PitchClass(4, -1)
    >>> vars(p)
    {'accidental': -1, 'intonation': 'flat', 'just': 4, 'value': 3}
    >>> print p
    Eb
    >>> p = PitchClass(4, -1, intonation='sharp')
    >>> vars(p)
    {'accidental': -1, 'intonation': 'flat', 'just': 4, 'value': 3}
    >>> print p
    D#

    Second, semitone number can be directly passed to constructor:

    :param semitone: integer of semitone.

    >>> p = PitchClass(3)
    >>> print p
    Eb
    >>> p = PitchClass(3, intonation='sharp')
    >>> print p
    D#

    And last, but not least, note name:

    :param name: string representing note.

    >>> p = PitchClass('D#')
    >>> print p
    D#*/
    public int just = 0;
    public int accidental = 0;
    public int value = 0;
    public string intonation = null;
    public float actualOvertone = 0.0f;

    public PitchClass(int arg0i = 0, int arg1i = -1, string arg0s = "", string intonation = "", float actualOvertone = 0.0f)
    {
        string[] _notes_sharp = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        string[] _notes_flat = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };
        int value = 0;
        string str = "";
        int accidental = 0;
        int pitch = 0;
        this.actualOvertone = actualOvertone; //Make it simpler to use later in internal format

        if (arg1i == -1)
        {
            if (!arg0s.Equals(""))
            {
                str = arg0s;
                for (int x = 0; x < _notes_sharp.Length; x++)
                {
                    if (str.Equals(_notes_sharp[x])) { value = x; break; }
                    if (str.Equals(_notes_flat[x])) { value = x; break; }
                }

            }
            else
            {
                value = arg0i % 12;

                str = _notes_sharp[Math.Max(value, 0)];
                if (intonation.Equals("flat")) str = _notes_flat[value];
            }

            if (str.EndsWith("b")) { accidental = -1; }
            else if (str.EndsWith("#")) { accidental = 1; }

        }
        else
        {
            pitch = arg0i; accidental = arg1i;
            this.just = pitch % 12;
            this.accidental = accidental;
            this.value = this.just + accidental;
            if (intonation != null) { this.intonation = intonation; }
            else
            {
                if (accidental == -1) { this.intonation = "flat"; }
                else { this.intonation = "sharp"; }
            }

        }
    }
}

public class RepeatGroup
{
    public List<MeasureHeader> measureHeaders = new List<MeasureHeader>();
    public List<MeasureHeader> openings = new List<MeasureHeader>();
    public List<MeasureHeader> closings = new List<MeasureHeader>();
    public bool isClosed = false;

    public void addMeasureHeader(MeasureHeader h)
    {
        if (!(openings.Count > 0)) openings.Add(h);

        measureHeaders.Add(h);
        h.repeatGroup = this;
        if (h.repeatClose > 0)
        {
            closings.Add(h);
            isClosed = true;
        }
        else if (isClosed)
        {
            isClosed = false;
            openings.Add(h);
        }
    }
}

public class RSEEqualizer
{
    public float gain;
    public List<float> knobs = null;
    public RSEEqualizer(List<float> knobs = null, float gain = 0.0f)
    {
        this.gain = gain;
        this.knobs = knobs;
    }
}
public class RSEMasterEffect
{
    public int volume = 0;
    public int reverb = 0;
    public RSEEqualizer equalizer = null;
    public RSEMasterEffect(int volume = 0, int reverb = 0, RSEEqualizer equalizer = null)
    {
        this.volume = volume;
        this.reverb = reverb;
        this.equalizer = equalizer;
        if (equalizer != null && equalizer.knobs == null)
        {
            equalizer.knobs = new List<float> { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
        }
    }

}
public class Tempo
{
    public int value;
    public Tempo(int value = 120)
    {
        this.value = value;
    }
}

public class TimeSignature
{
    public int numerator = 4;
    public Duration denominator = new Duration();
    public int[] beams = { 0, 0, 0, 0 };
}

public class Track
{
    public GPFile song;
    public int number = 0;
    public int offset = 0; //Capo
    public bool isSolo = false;
    public bool isMute = false;
    public bool isVisible = true;
    public bool indicateTuning = true;
    public string name = "";
    public List<Measure> measures = new List<Measure>();
    public List<GuitarString> strings = new List<GuitarString>();
    public string tuningName = "";
    public MidiChannel channel = new MidiChannel();
    public myColor color = new myColor(255, 0, 0);
    public TrackSettings settings = new TrackSettings();
    public int port = 0;
    public bool isPercussionTrack = false;
    public bool isBanjoTrack = false;
    public bool is12StringedGuitarTrack = false;
    public bool useRSE = false;
    public int fretCount = 0;
    public TrackRSE rse = null;

    public Track(GPFile song, int number, List<GuitarString> strings = null, List<Measure> measures = null)
    {
        this.song = song;
        this.number = number;
        if (strings != null) this.strings = strings;
        if (measures != null) this.measures = measures;
    }

    public void addMeasure(Measure measure)
    {
        measure.track = this;
        measures.Add(measure);
    }
}

public class RSEInstrument
{
    public int instrument = -1;
    public int unknown = 1;
    public int soundBank = -1;
    public int effectNumber = -1;
    public string effectCategory = "";
    public string effect = "";
}

public enum Accentuation
{
    none = 0, verySoft = 1, soft = 2, medium = 3, strong = 4, veryStrong = 5
}
public class TrackRSE
{
    public RSEInstrument instrument = null;
    public RSEEqualizer equalizer = null;
    public int humanize = 0;
    public Accentuation autoAccentuation = Accentuation.none;

    public TrackRSE()
    {
        if (equalizer != null && equalizer.knobs == null)
        {
            equalizer.knobs = new List<float> { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
        }
    }
}
public class TrackSettings
{
    public bool tablature = true;
    public bool notation = true;
    public bool diagramsAreBelow = false;
    public bool showRyhthm = false;
    public bool forceHorizontal = false;
    public bool forceChannels = false;
    public bool diagramList = true;
    public bool diagramsInScore = false;
    public bool autoLetRing = false;
    public bool autoBrush = false;
    public bool extendRhythmic = false;
}

public class TremoloPickingEffect
{
    public Duration duration = new Duration();
}

public class TrillEffect
{
    public int fret = 0;
    public Duration duration = new Duration();
}

public class Tuplet
{
    public int enters = 1;
    public int times = 1;
    public int convertTime(int time)
    {
        return (int)(time * (float)times / (float)enters);
    }
}

public class Velocities
{
    public const int minVelocity = 15;
    public const int velocityIncrement = 16;
    public const int pianoPianissimo = minVelocity;
    public const int pianissimo = minVelocity + velocityIncrement;
    public const int piano = minVelocity + velocityIncrement * 2;
    public const int mezzoPiano = minVelocity + velocityIncrement * 3;
    public const int mezzoForte = minVelocity + velocityIncrement * 4;
    public const int forte = minVelocity + velocityIncrement * 5;
    public const int fortissimo = minVelocity + velocityIncrement * 6;
    public const int forteFortissimo = minVelocity + velocityIncrement * 7;
    public const int def = forte;
}

public class Voice
{
    public Measure measure;
    public List<Beat> beats = new List<Beat>();
    public VoiceDirection direction = VoiceDirection.none;
    public Duration duration;

    public Voice(Measure measure = null)
    {
        this.measure = measure;
    }
    public bool isEmpty()
    {
        return beats.Count == 0;
    }

    public void addBeat(Beat beat)
    {
        beat.voice = this;
        beats.Add(beat);
    }
}

public enum BeatStatus
{
    empty = 0, normal = 1, rest = 2
}

public enum BeatStrokeDirection
{
    none = 0, up = 1, down = 2
}

public enum BendType
{
    //: No Preset.
    none = 0,
    // Bends
    // =====
    //: A simple bend.
    bend = 1,
    //: A bend and release afterwards.
    bendRelease = 2,
    //: A bend, then release and rebend.
    bendReleaseBend = 3,
    //: Prebend.
    prebend = 4,
    //: Prebend and then release.
    prebendRelease = 5,

    // Tremolobar
    // ==========
    //: Dip the bar down and then back up.
    dip = 6,
    //: Dive the bar.
    dive = 7,
    //: Release the bar up.
    releaseUp = 8,
    //: Dip the bar up and then back down.
    invertedDip = 9,
    //: Return the bar.
    return_ = 10,
    //: Release the bar down.
    releaseDown = 11
}

public enum ChordType
{
    major = 0, seventh = 1, majorSeventh = 2, sixth = 3, minor = 4, minorSeventh = 5, minorMajor = 6,
    minorSixth = 7, suspendedSecond = 8, suspendedFourth = 9, seventhSuspendedSecond = 10,
    seventhSuspendedFourth = 11, diminished = 12, augmented = 13, power = 14
}

public enum ChordAlteration
{
    perfect = 0, diminished = 1, augmented = 2
}

public enum ChordExtension
{
    none = 0, ninth = 1, eleventh = 2, thirteenth = 3
}

public enum Fingering
{
    unknown = -2, open = -1, thump = 0, index = 1, middle = 2, annular = 3, little = 4
}

public enum GraceEffectTransition
{
    none = 0, slide = 1, bend = 2, hammer = 3
}

public enum KeySignature
{
    FMajorFlat = -80,
    CMajorFlat = -70,
    GMajorFlat = -60,
    DMajorFlat = -50,
    AMajorFlat = -40,
    EMajorFlat = -30,
    BMajorFlat = -20,
    FMajor = -10,
    CMajor = 00,
    GMajor = 10,
    DMajor = 20,
    AMajor = 30,
    EMajor = 40,
    BMajor = 50,
    FMajorSharp = 60,
    CMajorSharp = 70,
    GMajorSharp = 80,

    DMinorFlat = -81,
    AMinorFlat = -71,
    EMinorFlat = -61,
    BMinorFlat = -51,
    FMinor = -41,
    CMinor = -31,
    GMinor = -21,
    DMinor = -11,
    AMinor = 01,
    EMinor = 11,
    BMinor = 21,
    FMinorSharp = 31,
    CMinorSharp = 41,
    GMinorSharp = 51,
    DMinorSharp = 61,
    AMinorSharp = 71,
    EMinorSharp = 81
}

public enum LineBreak
{
    none = 0, break_ = 1, protect = 2
}

public enum MeasureClef
{
    treble = 0, bass = 1, tenor = 2, alto = 3, neutral = 4 //drums
}

public enum NoteType
{
    rest = 0, normal = 1, tie = 2, dead = 3
}

public enum SlapEffect
{
    none = 0, tapping = 1, slapping = 2, popping = 3
}

public enum SlideType
{
    intoFromAbove = -2, intoFromBelow = -1, none = 0, shiftSlideTo = 1, legatoSlideTo = 2,
    outDownwards = 3, outUpwards = 4, pickScrapeOutDownwards = 5, pickScrapeOutUpwards = 6
}

public enum TripletFeel
{
    none = 0,
    eigth = 1,
    sixteenth = 2,
    dotted8th = 3,
    dotted16th = 4,
    scottish8th = 5,
    scottish16th = 6
}

public enum TupletBracket
{
    none = 0, start = 1, end = 2
}

public enum Octave
{
    none = 0, ottava = 1, quindicesima = 2, ottavaBassa = 3, quindicesimaBassa = 4
}

public enum VoiceDirection
{
    none = 0, up = 1, down = 2
}
