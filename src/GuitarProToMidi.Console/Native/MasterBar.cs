namespace GuitarProToMidi.Native;

public class MasterBar
{
    public int Den { get; init; } = 4;
    public int Duration { get; set; }
    public int Index { get; set; } //Midi Index
    public int Key { get; set; } //C, -1 = F, 1 = G
    public string KeyBoth { get; set; } = "0";
    public int KeyType { get; set; } //0 = Major, 1 = Minor
    public int Num { get; init; } = 4;
    public string Time { get; init; } = "4/4";

    public TripletFeel TripletFeel { get; set; } =
        TripletFeel.none; //additional info -> note values are changed in duration and position too
}
