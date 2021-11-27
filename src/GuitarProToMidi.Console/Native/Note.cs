using System.Collections.Generic;

namespace GuitarProToMidi.Native;

public class Note
{
    public List<BendPoint> BendPoints { get; set; } = new();
    public bool Connect { get; set; }
    public int Duration { get; set; }
    public Fading Fading { get; set; } = Fading.None;
    public int Fret { get; set; }
    public HarmonicType Harmonic { get; set; } = HarmonicType.None;
    public float HarmonicFret { get; set; }
    public int Index { get; set; }
    public bool IsHammer { get; set; }
    public bool IsMuted { get; set; }
    public bool IsPalmMuted { get; set; }
    public bool IsPopped { get; set; }
    public bool IsRhTapped { get; set; }
    public bool IsSlapped { get; set; }
    public bool IsTremBarVibrato { get; init; }
    public bool IsVibrato { get; set; }

    public float
        ResizeValue
    { get; set; } =
        1.0f; //Should reflect any later changes made to the note duration, so that bendPoints can be adjusted

    public bool SlideInFromAbove { get; set; }
    public bool SlideInFromBelow { get; set; }
    public bool SlideOutDownwards { get; set; }
    public bool SlideOutUpwards { get; set; }
    public bool SlidesToNext { get; set; }

    //Values from Note
    public int Str { get; set; }

    //Values from Beat
    private readonly List<BendPoint> _tremBarPoints = new();
    public int Velocity { get; set; } = 100;

    public Note(Note old)
    {
        Str = old.Str;
        Fret = old.Fret;
        Velocity = old.Velocity;
        IsVibrato = old.IsVibrato;
        IsHammer = old.IsHammer;
        IsPalmMuted = old.IsPalmMuted;
        IsMuted = old.IsMuted;
        Harmonic = old.Harmonic;
        HarmonicFret = old.HarmonicFret;
        SlidesToNext = old.SlidesToNext;
        SlideInFromAbove = old.SlideInFromAbove;
        SlideInFromBelow = old.SlideInFromBelow;
        SlideOutDownwards = old.SlideOutDownwards;
        SlideOutUpwards = old.SlideOutUpwards;
        BendPoints.AddRange(old.BendPoints);
        _tremBarPoints.AddRange(old._tremBarPoints);
        IsTremBarVibrato = old.IsTremBarVibrato;
        IsSlapped = old.IsSlapped;
        IsPopped = old.IsPopped;
        Index = old.Index;
        Duration = old.Duration;
        Fading = old.Fading;
        IsRhTapped = old.IsRhTapped;
        ResizeValue = old.ResizeValue;
    }

    public Note()
    {
    }

    public void AddBendPoints(IEnumerable<BendPoint> bendPoints) => BendPoints.AddRange(bendPoints);
}
