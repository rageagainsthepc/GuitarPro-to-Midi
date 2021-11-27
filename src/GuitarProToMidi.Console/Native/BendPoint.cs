namespace GuitarProToMidi.Native;

public record BendPoint(int Index, float Value = 0.0f, int UsedChannel = -1);
