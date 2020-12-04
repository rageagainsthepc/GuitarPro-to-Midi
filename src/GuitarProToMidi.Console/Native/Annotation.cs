namespace GuitarProToMidi.Native
{
    public class Annotation
    {
        public int Position { get; }
        public string Value { get; }

        public Annotation(string value, int position)
        {
            Value = value;
            Position = position;
        }
    }
}
