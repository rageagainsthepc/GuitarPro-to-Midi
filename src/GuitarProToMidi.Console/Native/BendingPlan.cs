using System.Collections.Generic;

namespace GuitarProToMidi.Native
{
    public class BendingPlan
    {
        public List<BendPoint> BendingPoints { get; }
        public int OriginalChannel { get; }
        public int UsedChannel { get; }

        public BendingPlan(int originalChannel, int usedChannel, List<BendPoint> bendingPoints)
        {
            BendingPoints = bendingPoints;
            OriginalChannel = originalChannel;
            UsedChannel = usedChannel;
        }
    }
}
