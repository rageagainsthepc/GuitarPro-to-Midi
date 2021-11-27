namespace GuitarProToMidi.Native;

public class Tempo
{
    public int Position { get; set; } //total position in song @ 960 ticks_per_beat
    public float Value { get; set; } = 120.0f;
}
