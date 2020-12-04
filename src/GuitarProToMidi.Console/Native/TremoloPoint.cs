namespace GuitarProToMidi.Native
{
    public class TremoloPoint
    {
        public int Index { get; init; }
        public float Value { get; init; } //0 nothing, 100 one whole tone up

        public TremoloPoint()
        {
        }

        public TremoloPoint(float value, int index)
        {
            this.Value = value;
            this.Index = index;
        }
    }
}
