using System;
using System.Collections.Generic;
using System.Linq;

namespace GuitarProToMidi.Native;

public class BendingPlan
{
    public int OriginalChannel { get; }
    public int UsedChannel { get; }
    public ICollection<BendPoint> BendingPoints { get; }

    public BendingPlan(int originalChannel, int usedChannel, ICollection<BendPoint> bendingPoints)
    {
        OriginalChannel = originalChannel;
        UsedChannel = usedChannel;
        BendingPoints = bendingPoints;
    }

    public static BendingPlan create(ICollection<BendPoint> bendPoints, int originalChannel,
        int usedChannel,
        int duration, int index, float resize, bool isVibrato)
    {
        var maxDistance = duration / 10; //After this there should be a pitchwheel event
        if (isVibrato)
        {
            maxDistance = Math.Min(maxDistance, 60);
        }

        if (bendPoints.Count == 0)
        {
            //Create Vibrato Plan
            bendPoints.Add(new BendPoint(index, 0.0f, usedChannel));
            bendPoints.Add(new BendPoint(index + duration, 0.0f, usedChannel));
        }

        var bendingPoints = new List<BendPoint>();


        //Resize the points according to (changed) note duration
        bendPoints = bendPoints.Select(bp =>
            bp with { Index = (int)(index + (bp.Index - index) * resize), UsedChannel = usedChannel }).ToList();

        var oldPos = index;
        var oldValue = 0.0f;
        var start = true;
        var vibratoSize = 0;
        var vibratoChange = 0;
        if (isVibrato)
        {
            vibratoSize = 12;
            vibratoChange = 6;
        }

        var vibrato = 0;
        foreach (var bp in bendPoints)
        {
            if (bp.Index - oldPos > maxDistance)
            //Add in-between points
            {
                for (var x = oldPos + maxDistance; x < bp.Index; x += maxDistance)
                {
                    var value = oldValue + (bp.Value - oldValue) *
                        (((float)x - oldPos) / ((float)bp.Index - oldPos));
                    bendingPoints.Add(new BendPoint(x, value + vibrato, usedChannel));
                    if (isVibrato && Math.Abs(vibrato) == vibratoSize)
                    {
                        vibratoChange = -vibratoChange;
                    }

                    vibrato += vibratoChange;
                }
            }

            if (start || bp.Index != oldPos)
            {
                if (isVibrato)
                {
                    bendingPoints.Add(bp with { Value = bp.Value + vibrato });
                }
                else
                {
                    bendingPoints.Add(bp);
                }
            }

            oldPos = bp.Index;
            oldValue = bp.Value;
            if ((start || bp.Index != oldPos) && isVibrato)
            {
                oldValue -= vibrato; //Add back, so not to be influenced by it
            }

            start = false;
            if (isVibrato && Math.Abs(vibrato) == vibratoSize)
            {
                vibratoChange = -vibratoChange;
            }

            vibrato += vibratoChange;
        }

        if (Math.Abs(index + duration - oldPos) > maxDistance)
        {
            bendingPoints.Add(new BendPoint(index + duration, oldValue, usedChannel));
        }

        return new BendingPlan(originalChannel, usedChannel, bendingPoints);
    }

    private bool equals(BendingPlan other)
    {
        return OriginalChannel == other.OriginalChannel && UsedChannel == other.UsedChannel &&
               BendingPoints.SequenceEqual(other.BendingPoints);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == GetType() && @equals((BendingPlan)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(OriginalChannel, UsedChannel, BendingPoints);
    }
}
