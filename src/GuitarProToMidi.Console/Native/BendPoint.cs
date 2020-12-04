namespace GuitarProToMidi.Native
{
    public class BendPoint
    {
        public int Index { get; set; } //also global index of midi
        public int UsedChannel { get; set; } //After being part of BendingPlan
        public float Value { get; set; }

        public BendPoint(float value, int index)
        {
            Value = value;
            Index = index;
        }

        public BendPoint()
        {
        }
    }
}
