using System;
using System.Collections.Generic;
using System.Linq;

namespace GuitarProToMidi.Native
{
    public record BendingPlan(int OriginalChannel, int UsedChannel, ICollection<BendPoint> BendingPoints)
    {
        public virtual bool Equals(BendingPlan other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return OriginalChannel == other.OriginalChannel && UsedChannel == other.UsedChannel &&
                   BendingPoints.SequenceEqual(other.BendingPoints);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(OriginalChannel, UsedChannel, BendingPoints);
        }
    }
}
